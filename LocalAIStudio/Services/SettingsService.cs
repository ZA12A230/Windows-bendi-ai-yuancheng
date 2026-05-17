using System;
using System.Diagnostics;
using System.Management;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace LocalAIStudio.Services
{
    public class SettingsService
    {
        #region Singleton
        private static readonly Lazy<SettingsService> _instance = new Lazy<SettingsService>(() => new SettingsService());
        public static SettingsService Instance => _instance.Value;
        #endregion

        private readonly string _appPath = Process.GetCurrentProcess().MainModule.FileName ?? string.Empty;
        private readonly string _appName = Process.GetCurrentProcess().ProcessName;
        private CancellationTokenSource _monitoringTokenSource;

        public bool AutoStartEnabled
        {
            get => GetAutoStartFromRegistry();
            set => SetAutoStartToRegistry(value);
        }

        public bool AdminModeEnabled
        {
            get => GetAdminModeFromRegistry();
            set => SetAdminModeToRegistry(value);
        }

        public bool ScreenOffReplacementEnabled
        {
            get => GetScreenOffFromRegistry();
            set => SetScreenOffToRegistry(value);
        }

        public bool SilentStartEnabled
        {
            get => GetSilentStartFromRegistry();
            set => SetSilentStartToRegistry(value);
        }

        public bool AdaptiveModeEnabled
        {
            get => GetAdaptiveModeFromRegistry();
            set
            {
                SetAdaptiveModeToRegistry(value);
                if (value)
                {
                    StartMonitoring();
                }
                else
                {
                    StopMonitoring();
                }
            }
        }

        public int SystemCpuThreshold
        {
            get => GetIntRegistryValue("SystemCpuThreshold", 80);
            set => SetIntRegistryValue("SystemCpuThreshold", value);
        }

        public int AiCpuThreshold
        {
            get => GetIntRegistryValue("AiCpuThreshold", 50);
            set => SetIntRegistryValue("AiCpuThreshold", value);
        }

        #region Registry Helpers
        private const string RegKeyPath = @"Software\LocalAIStudio";

        private RegistryKey GetOrCreateAppKey(bool writable = false)
        {
            var key = Registry.CurrentUser.OpenSubKey(RegKeyPath, writable);
            if (key == null)
            {
                key = Registry.CurrentUser.CreateSubKey(RegKeyPath, writable);
            }
            return key;
        }

        private bool GetBoolRegistryValue(string name, bool defaultValue = false)
        {
            using (var key = GetOrCreateAppKey())
            {
                var val = key.GetValue(name);
                if (val == null) return defaultValue;
                return Convert.ToBoolean(val);
            }
        }

        private void SetBoolRegistryValue(string name, bool value)
        {
            using (var key = GetOrCreateAppKey(true))
            {
                key.SetValue(name, value ? 1 : 0, RegistryValueKind.DWord);
            }
        }

        private int GetIntRegistryValue(string name, int defaultValue = 0)
        {
            using (var key = GetOrCreateAppKey())
            {
                var val = key.GetValue(name);
                if (val == null) return defaultValue;
                return Convert.ToInt32(val);
            }
        }

        private void SetIntRegistryValue(string name, int value)
        {
            using (var key = GetOrCreateAppKey(true))
            {
                key.SetValue(name, value, RegistryValueKind.DWord);
            }
        }
        #endregion

        #region Auto Start
        private bool GetAutoStartFromRegistry()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    if (key == null) return false;
                    var val = key.GetValue(_appName);
                    return val != null && val.ToString() == _appPath;
                }
            }
            catch
            {
                return false;
            }
        }

        private void SetAutoStartToRegistry(bool enable)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key == null) return;

                    if (enable)
                    {
                        key.SetValue(_appName, _appPath);
                    }
                    else
                    {
                        if (key.GetValue(_appName) != null)
                        {
                            key.DeleteValue(_appName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting auto start: {ex.Message}");
            }
        }
        #endregion

        #region Admin Mode
        private bool GetAdminModeFromRegistry() => GetBoolRegistryValue("AdminModeEnabled");

        private void SetAdminModeToRegistry(bool enable)
        {
            SetBoolRegistryValue("AdminModeEnabled", enable);

            // 实现方式：使用计划任务在下次启动时提权
            try
            {
                if (enable)
                {
                    // TODO: 创建计划任务来实现自动提权
                    // 这里需要使用任务计划程序API或schtasks命令
                }
                else
                {
                    // TODO: 删除计划任务
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting admin mode: {ex.Message}");
            }
        }
        #endregion

        #region Screen Off Replacement
        private bool GetScreenOffFromRegistry() => GetBoolRegistryValue("ScreenOffReplacementEnabled");

        private void SetScreenOffToRegistry(bool enable)
        {
            SetBoolRegistryValue("ScreenOffReplacementEnabled", enable);
            ApplyScreenOffReplacement(enable);
        }

        [DllImport("powrprof.dll", SetLastError = true)]
        private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

        // 禁用鼠标和键盘唤醒事件的API
        [DllImport("user32.dll")]
        private static extern IntPtr SetThreadExecutionState(EXECUTION_STATE esFlags);

        [Flags]
        private enum EXECUTION_STATE : uint
        {
            ES_CONTINUOUS = 0x80000000,
            ES_DISPLAY_REQUIRED = 0x00000002,
            ES_SYSTEM_REQUIRED = 0x00000001
        }

        public void ScreenOff()
        {
            // 禁用鼠标和键盘唤醒
            DisableAllWakeEvents();
            // 执行息屏（睡眠）
            SetSuspendState(false, true, true);
        }

        private void DisableAllWakeEvents()
        {
            try
            {
                // 使用Powercfg命令禁用所有设备的唤醒功能
                Process.Start(new ProcessStartInfo
                {
                    FileName = "powercfg",
                    Arguments = "-devicedisablewake *",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }).WaitForExit();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error disabling wake events: {ex.Message}");
            }
        }

        private void ApplyScreenOffReplacement(bool enable)
        {
            try
            {
                // 方案：使用组策略禁用关机按钮，同时我们创建替换快捷方式
                using (var policyKey = Registry.LocalMachine.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", true))
                {
                    if (policyKey != null)
                    {
                        if (enable)
                        {
                            policyKey.SetValue("NoClose", 1, RegistryValueKind.DWord);
                        }
                        else
                        {
                            if (policyKey.GetValue("NoClose") != null)
                            {
                                policyKey.DeleteValue("NoClose");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying screen off replacement: {ex.Message}");
            }
        }
        #endregion

        #region Silent Start
        private bool GetSilentStartFromRegistry() => GetBoolRegistryValue("SilentStartEnabled");

        private void SetSilentStartToRegistry(bool enable) => SetBoolRegistryValue("SilentStartEnabled", enable);
        #endregion

        #region Adaptive Mode
        private bool GetAdaptiveModeFromRegistry() => GetBoolRegistryValue("AdaptiveModeEnabled");

        private void SetAdaptiveModeToRegistry(bool enable) => SetBoolRegistryValue("AdaptiveModeEnabled", enable);

        private void StartMonitoring()
        {
            StopMonitoring();
            _monitoringTokenSource = new CancellationTokenSource();
            Task.Run(() => MonitoringLoopAsync(_monitoringTokenSource.Token), _monitoringTokenSource.Token);
        }

        private void StopMonitoring()
        {
            if (_monitoringTokenSource != null)
            {
                _monitoringTokenSource.Cancel();
                _monitoringTokenSource.Dispose();
            }
            _monitoringTokenSource = null;
        }

        private async System.Threading.Tasks.Task MonitoringLoopAsync(CancellationToken token)
        {
            var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            cpuCounter.NextValue(); // 第一次读取通常是0，丢弃

            while (!token.IsCancellationRequested)
            {
                try
                {
                    await System.Threading.Tasks.Task.Delay(2000, token);

                    // 获取系统总CPU占用
                    float systemCpuUsage = cpuCounter.NextValue();

                    // 获取Ollama进程的CPU占用
                    float aiCpuUsage = 0;
                    Process aiProcess = null;

                    var processes = Process.GetProcessesByName("ollama");
                    if (processes.Length > 0)
                    {
                        aiProcess = processes[0];
                        aiCpuUsage = GetProcessCpuUsage(aiProcess);
                    }

                    // 判断是否需要调整优先级
                    int systemThreshold = SystemCpuThreshold;
                    int aiThreshold = AiCpuThreshold;

                    if (systemCpuUsage > systemThreshold || aiCpuUsage > aiThreshold)
                    {
                        if (aiProcess != null && aiProcess.PriorityClass != ProcessPriorityClass.BelowNormal)
                        {
                            aiProcess.PriorityClass = ProcessPriorityClass.BelowNormal;
                            Debug.WriteLine($"Adaptive mode: lowered priority due to high CPU (system:{systemCpuUsage:F1}%, AI:{aiCpuUsage:F1}%)");
                        }
                    }
                    else
                    {
                        if (aiProcess != null && aiProcess.PriorityClass != ProcessPriorityClass.Normal)
                        {
                            aiProcess.PriorityClass = ProcessPriorityClass.Normal;
                            Debug.WriteLine($"Adaptive mode: restored priority (system:{systemCpuUsage:F1}%, AI:{aiCpuUsage:F1}%)");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in monitoring loop: {ex.Message}");
                }
            }

            cpuCounter.Dispose();
        }

        private float GetProcessCpuUsage(Process process)
        {
            try
            {
                // 简单的方法是使用WMI或PerformanceCounter
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT PercentProcessorTime FROM Win32_PerfFormattedData_PerfProc_Process WHERE IDProcess=" + process.Id))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var val = obj["PercentProcessorTime"];
                        if (val != null)
                        {
                            return Convert.ToSingle(val);
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors
            }
            return 0;
        }
        #endregion
    }
}
