using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace LocalAIStudio.Services
{
    public class WatchdogService
    {
        #region Singleton
        private static readonly Lazy<WatchdogService> _instance = 
            new Lazy<WatchdogService>(() => new WatchdogService());
        public static WatchdogService Instance => _instance.Value;
        #endregion

        private readonly Dictionary<string, ProcessInfo> _monitoredProcesses = new Dictionary<string, ProcessInfo>();
        private readonly Dictionary<string, DateTime> _restartHistory = new Dictionary<string, DateTime>();
        private CancellationTokenSource? _cts;
        private CancellationTokenSource? _adaptiveCts;
        private bool _isRunning = false;
        private bool _adaptiveModeEnabled = false;
        private int _restartCount = 0;
        private const int MaxRestartAttempts = 5;
        private const int RestartCooldownSeconds = 30;

        public event EventHandler<string>? ProcessRestarted;
        public event EventHandler<string>? ProcessCrashed;
        public event EventHandler<(string process, string reason)>? RestartFailed;
        public event EventHandler<PerformanceAlert>? PerformanceAlertRaised;

        public bool IsRunning => _isRunning;
        public bool AdaptiveModeEnabled => _adaptiveModeEnabled;
        public int RestartCount => _restartCount;

        #region Process Management

        public void RegisterProcess(string processName, string exePath, string? arguments = null, bool autoRestart = true)
        {
            if (_monitoredProcesses.ContainsKey(processName))
            {
                Debug.WriteLine($"Process {processName} already registered");
                return;
            }

            var processInfo = new ProcessInfo
            {
                Name = processName,
                ExePath = exePath,
                Arguments = arguments,
                AutoRestart = autoRestart,
                IsRunning = false,
                LastStartTime = DateTime.MinValue
            };

            _monitoredProcesses[processName] = processInfo;
            Debug.WriteLine($"Registered process for monitoring: {processName}");
        }

        public void UnregisterProcess(string processName)
        {
            if (_monitoredProcesses.ContainsKey(processName))
            {
                StopProcess(processName);
                _monitoredProcesses.Remove(processName);
                Debug.WriteLine($"Unregistered process: {processName}");
            }
        }

        public void StartMonitoring()
        {
            if (_isRunning) return;

            _isRunning = true;
            _cts = new CancellationTokenSource();
            _ = MonitorLoop(_cts.Token);
            Debug.WriteLine("Watchdog service started");
        }

        public void StopMonitoring()
        {
            if (!_isRunning) return;

            _cts?.Cancel();
            _isRunning = false;
            Debug.WriteLine("Watchdog service stopped");
        }

        private async Task MonitorLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    foreach (var kvp in _monitoredProcesses.ToList())
                    {
                        var processName = kvp.Key;
                        var processInfo = kvp.Value;

                        if (!processInfo.AutoRestart)
                            continue;

                        var process = GetProcessByName(processName);
                        
                        if (process == null || process.HasExited)
                        {
                            if (processInfo.IsRunning && DateTime.Now - processInfo.LastStartTime > TimeSpan.FromSeconds(RestartCooldownSeconds))
                            {
                                await TryRestartProcess(processName);
                            }
                            else if (!processInfo.IsRunning)
                            {
                                await TryStartProcess(processName);
                            }
                        }
                    }

                    await Task.Delay(3000, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Monitor loop error: {ex.Message}");
                    await Task.Delay(5000, ct);
                }
            }
        }

        private Process? GetProcessByName(string name)
        {
            try
            {
                return Process.GetProcessesByName(name)
                    .FirstOrDefault(p => !p.HasExited);
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> TryStartProcess(string processName)
        {
            if (!_monitoredProcesses.TryGetValue(processName, out var processInfo))
                return false;

            if (processInfo.IsRunning)
                return true;

            try
            {
                if (!File.Exists(processInfo.ExePath))
                {
                    Debug.WriteLine($"Process executable not found: {processInfo.ExePath}");
                    return false;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = processInfo.ExePath,
                    Arguments = processInfo.Arguments ?? "",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var process = Process.Start(startInfo);
                if (process != null)
                {
                    processInfo.IsRunning = true;
                    processInfo.LastStartTime = DateTime.Now;
                    processInfo.ProcessId = process.Id;

                    ProcessRestarted?.Invoke(this, processName);
                    Debug.WriteLine($"Started process: {processName}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to start process {processName}: {ex.Message}");
                RestartFailed?.Invoke(this, (processName, ex.Message));
            }

            return false;
        }

        private async Task<bool> TryRestartProcess(string processName)
        {
            if (!_monitoredProcesses.TryGetValue(processName, out var processInfo))
                return false;

            if (_restartHistory.TryGetValue(processName, out var lastRestart))
            {
                if (DateTime.Now - lastRestart < TimeSpan.FromSeconds(RestartCooldownSeconds))
                {
                    Debug.WriteLine($"Restart cooldown active for {processName}");
                    return false;
                }
            }

            if (_restartCount >= MaxRestartAttempts)
            {
                Debug.WriteLine($"Max restart attempts reached for {processName}");
                RestartFailed?.Invoke(this, (processName, "Max restart attempts reached"));
                return false;
            }

            Debug.WriteLine($"Attempting to restart {processName}...");
            ProcessCrashed?.Invoke(this, processName);

            var success = await TryStartProcess(processName);
            
            if (success)
            {
                _restartHistory[processName] = DateTime.Now;
                _restartCount++;
            }

            return success;
        }

        public async Task StopProcess(string processName)
        {
            if (!_monitoredProcesses.TryGetValue(processName, out var processInfo))
                return;

            try
            {
                var process = Process.GetProcessById(processInfo.ProcessId);
                if (!process.HasExited)
                {
                    process.Kill();
                    await process.WaitForExitAsync();
                }
            }
            catch (ArgumentException) { }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error stopping process {processName}: {ex.Message}");
            }
            finally
            {
                processInfo.IsRunning = false;
                processInfo.ProcessId = 0;
            }
        }

        public void SetProcessRunning(string processName, bool running, int? processId = null)
        {
            if (_monitoredProcesses.TryGetValue(processName, out var processInfo))
            {
                processInfo.IsRunning = running;
                processInfo.ProcessId = processId ?? 0;
                if (running)
                {
                    processInfo.LastStartTime = DateTime.Now;
                }
            }
        }

        #endregion

        #region Adaptive Mode

        public void StartAdaptiveMode(int systemThreshold = 80, int aiThreshold = 50)
        {
            if (_adaptiveModeEnabled) return;

            _adaptiveModeEnabled = true;
            _adaptiveCts = new CancellationTokenSource();
            _ = AdaptiveMonitorLoop(_adaptiveCts.Token, systemThreshold, aiThreshold);
            Debug.WriteLine("Adaptive mode started");
        }

        public void StopAdaptiveMode()
        {
            if (!_adaptiveModeEnabled) return;

            _adaptiveCts?.Cancel();
            _adaptiveModeEnabled = false;
            Debug.WriteLine("Adaptive mode stopped");
        }

        private async Task AdaptiveMonitorLoop(CancellationToken ct, int systemThreshold, int aiThreshold)
        {
            PerformanceCounter? cpuCounter = null;
            PerformanceCounter? aiCpuCounter = null;

            try
            {
                cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                cpuCounter.NextValue();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to initialize CPU counter: {ex.Message}");
            }

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    double systemUsage = 0;
                    
                    if (cpuCounter != null)
                    {
                        systemUsage = cpuCounter.NextValue();
                    }

                    double aiUsage = GetAiProcessCpuUsage();

                    if (systemUsage > systemThreshold)
                    {
                        PerformanceAlertRaised?.Invoke(this, new PerformanceAlert
                        {
                            AlertType = AlertType.SystemHigh,
                            Usage = systemUsage,
                            Threshold = systemThreshold,
                            Message = $"系统CPU占用超过 {systemThreshold}%"
                        });

                        ReduceAiPriority();
                    }

                    if (aiUsage > aiThreshold)
                    {
                        PerformanceAlertRaised?.Invoke(this, new PerformanceAlert
                        {
                            AlertType = AlertType.AiProcessHigh,
                            Usage = aiUsage,
                            Threshold = aiThreshold,
                            Message = $"AI进程CPU占用超过 {aiThreshold}%"
                        });

                        LimitAiCpuCores();
                    }

                    await Task.Delay(5000, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Adaptive monitor error: {ex.Message}");
                    await Task.Delay(10000, ct);
                }
            }
        }

        private double GetAiProcessCpuUsage()
        {
            try
            {
                var processes = Process.GetProcessesByName("ollama");
                if (processes.Length > 0)
                {
                    return Math.Min(100, processes[0].TotalProcessorTime.TotalMilliseconds / 
                                   Environment.ProcessorCount / 10);
                }
            }
            catch { }
            return 0;
        }

        private void ReduceAiPriority()
        {
            try
            {
                var processes = Process.GetProcessesByName("ollama");
                foreach (var process in processes)
                {
                    process.PriorityClass = ProcessPriorityClass.Idle;
                    Debug.WriteLine($"Reduced AI process priority to Idle");
                }

                var llmProcesses = Process.GetProcessesByName("llama");
                foreach (var process in llmProcesses)
                {
                    process.PriorityClass = ProcessPriorityClass.Idle;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to reduce AI priority: {ex.Message}");
            }
        }

        private void LimitAiCpuCores()
        {
            try
            {
                var processes = Process.GetProcessesByName("ollama");
                foreach (var process in processes)
                {
                    process.PriorityClass = ProcessPriorityClass.BelowNormal;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to limit AI CPU cores: {ex.Message}");
            }
        }

        #endregion

        #region Silent Start

        public bool ShouldStartSilently()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\LocalAIStudio\Settings", false);
                if (key != null)
                {
                    return Convert.ToInt32(key.GetValue("SilentStart", 0)) == 1;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to check silent start setting: {ex.Message}");
            }
            return false;
        }

        public void SetSilentStart(bool enabled)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\LocalAIStudio\Settings", true);
                key.SetValue("SilentStart", enabled ? 1 : 0, RegistryValueKind.DWord);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to set silent start: {ex.Message}");
            }
        }

        #endregion

        #region Auto Start

        public bool IsAutoStartEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false);
                return key?.GetValue("LocalAIStudio") != null;
            }
            catch { }
            return false;
        }

        public void SetAutoStart(bool enabled)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (key == null) return;

                if (enabled)
                {
                    var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        key.SetValue("LocalAIStudio", $"\"{exePath}\"", RegistryValueKind.String);
                    }
                }
                else
                {
                    key.DeleteValue("LocalAIStudio", false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to set auto start: {ex.Message}");
            }
        }

        #endregion

        #region Update

        public async Task<UpdateInfo?> CheckForUpdatesAsync(string updateUrl = "https://api.github.com/repos/yourusername/LocalAIStudio/releases/latest")
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "LocalAIStudio");
                client.Timeout = TimeSpan.FromSeconds(30);

                var response = await client.GetStringAsync(updateUrl);
                var release = JsonSerializer.Deserialize<GitHubRelease>(response);

                if (release == null)
                    return null;

                var currentVersion = GetCurrentVersion();
                var latestVersion = release.TagName?.TrimStart('v') ?? "0.0.0";

                if (CompareVersion(latestVersion, currentVersion) > 0)
                {
                    var asset = release.Assets?.FirstOrDefault(a => 
                        a.Name?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true);

                    return new UpdateInfo
                    {
                        CurrentVersion = currentVersion,
                        LatestVersion = latestVersion,
                        ReleaseNotes = release.Body,
                        DownloadUrl = asset?.BrowserDownloadUrl,
                        PublishedAt = release.PublishedAt
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to check for updates: {ex.Message}");
            }

            return null;
        }

        private string GetCurrentVersion()
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
        }

        private int CompareVersion(string version1, string version2)
        {
            var v1Parts = version1.Split('.').Select(int.Parse).ToArray();
            var v2Parts = version2.Split('.').Select(int.Parse).ToArray();

            for (int i = 0; i < Math.Max(v1Parts.Length, v2Parts.Length); i++)
            {
                int v1 = i < v1Parts.Length ? v1Parts[i] : 0;
                int v2 = i < v2Parts.Length ? v2Parts[i] : 0;

                if (v1 > v2) return 1;
                if (v1 < v2) return -1;
            }

            return 0;
        }

        public async Task<string?> DownloadUpdateAsync(string downloadUrl, IProgress<double>? progress = null)
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "LocalAIStudio");
                client.Timeout = TimeSpan.FromMinutes(10);

                var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1;
                var tempPath = Path.Combine(Path.GetTempPath(), $"LocalAIStudio_update_{Guid.NewGuid()}.exe");

                using var stream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);

                var buffer = new byte[8192];
                long totalBytesRead = 0;
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    totalBytesRead += bytesRead;

                    if (totalBytes > 0)
                    {
                        progress?.Report((double)totalBytesRead / totalBytes * 100);
                    }
                }

                return tempPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to download update: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> ApplyUpdateAsync(string updatePath)
        {
            try
            {
                var currentExe = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(currentExe))
                    return false;

                var batchPath = Path.Combine(Path.GetTempPath(), $"update_{Guid.NewGuid()}.bat");
                var batch = $@"
@echo off
timeout /t 2 /nobreak > nul
copy /y ""{updatePath}"" ""{currentExe}""
del ""{updatePath}""
start """" ""{currentExe}""
del ""{batchPath}""
";
                await File.WriteAllTextAsync(batchPath, batch);

                var startInfo = new ProcessStartInfo
                {
                    FileName = batchPath,
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                Process.Start(startInfo);
                
                await Task.Delay(1000);
                Environment.Exit(0);
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to apply update: {ex.Message}");
                return false;
            }
        }

        #endregion
    }

    #region Data Classes

    public class ProcessInfo
    {
        public string Name { get; set; } = "";
        public string ExePath { get; set; } = "";
        public string? Arguments { get; set; }
        public bool AutoRestart { get; set; }
        public bool IsRunning { get; set; }
        public int ProcessId { get; set; }
        public DateTime LastStartTime { get; set; }
    }

    public class PerformanceAlert
    {
        public AlertType AlertType { get; set; }
        public double Usage { get; set; }
        public int Threshold { get; set; }
        public string Message { get; set; } = "";
        public DateTime Time { get; set; } = DateTime.Now;
    }

    public enum AlertType
    {
        SystemHigh,
        AiProcessHigh,
        MemoryHigh,
        DiskLow
    }

    public class UpdateInfo
    {
        public string CurrentVersion { get; set; } = "";
        public string LatestVersion { get; set; } = "";
        public string? ReleaseNotes { get; set; }
        public string? DownloadUrl { get; set; }
        public DateTime? PublishedAt { get; set; }
    }

    public class GitHubRelease
    {
        public string? TagName { get; set; }
        public string? Body { get; set; }
        public DateTime? PublishedAt { get; set; }
        public List<GitHubAsset>? Assets { get; set; }
    }

    public class GitHubAsset
    {
        public string? Name { get; set; }
        public string? BrowserDownloadUrl { get; set; }
    }

    #endregion
}
