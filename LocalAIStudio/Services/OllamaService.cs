using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace LocalAIStudio.Services
{
    public class ModelInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public string Modified { get; set; } = string.Empty;
    }

    public class MirrorSource
    {
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsDefault { get; set; } = false;
    }

    public class OllamaService
    {
        private const string OfficialDownloadUrl = "https://ollama.com/download";
        private const string AliyunMirrorUrl = "https://registry.ollama.ai";
        private const string DefaultInstallPath = @"C:\Program Files\Ollama\ollama.exe";
        private const string RegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\ollama.exe";
        private const string ConfigFileName = "config.json";

        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(30)
        };

        public static bool IsInstalled()
        {
            if (File.Exists(DefaultInstallPath))
            {
                return true;
            }

            using var key = Registry.LocalMachine.OpenSubKey(RegistryPath, false);
            if (key != null)
            {
                var path = key.GetValue(string.Empty) as string;
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    return true;
                }
            }

            using var key32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32)
                .OpenSubKey(RegistryPath, false);
            if (key32 != null)
            {
                var path = key32.GetValue(string.Empty) as string;
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    return true;
                }
            }

            return false;
        }

        public static string? GetOllamaPath()
        {
            if (File.Exists(DefaultInstallPath))
            {
                return DefaultInstallPath;
            }

            using var key = Registry.LocalMachine.OpenSubKey(RegistryPath, false);
            if (key != null)
            {
                var path = key.GetValue(string.Empty) as string;
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    return path;
                }
            }

            return null;
        }

        public static async Task<List<ModelInfo>> GetInstalledModelsAsync()
        {
            var models = new List<ModelInfo>();
            try
            {
                var ollamaPath = GetOllamaPath();
                if (string.IsNullOrEmpty(ollamaPath))
                {
                    return models;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = ollamaPath,
                    Arguments = "list",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();

                    if (process.ExitCode == 0)
                    {
                        var output = await process.StandardOutput.ReadToEndAsync();
                        models = ParseModelList(output);
                    }
                }
            }
            catch
            {
                // 忽略错误
            }
            return models;
        }

        private static List<ModelInfo> ParseModelList(string output)
        {
            var models = new List<ModelInfo>();
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length >= 4)
                {
                    var name = parts[0];
                    if (name.StartsWith("NAME"))
                    {
                        continue;
                    }

                    var model = new ModelInfo();
                    model.Name = name;
                    model.Id = parts[parts.Length - 1];
                    if (parts.Length > 3)
                    {
                        model.Size = string.Join(" ", parts, parts.Length - 3, 2);
                        model.Modified = string.Join(" ", parts, parts.Length - 1, 1);
                    }
                    models.Add(model);
                }
            }

            return models;
        }

        public static async Task PullModelAsync(string modelName, IProgress<int>? progress = null, IProgress<string>? status = null, CancellationToken cancellationToken = default)
        {
            var ollamaPath = GetOllamaPath();
            if (string.IsNullOrEmpty(ollamaPath))
            {
                throw new Exception("Ollama not found");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = ollamaPath,
                Arguments = $"pull {modelName}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process();
            process.StartInfo = startInfo;
            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    status?.Report(e.Data);
                    var match = Regex.Match(e.Data, @"\d+%");
                    if (match.Success)
                    {
                        var pct = int.Parse(match.Value.Trim('%'));
                        progress?.Report(pct);
                    }
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                throw new Exception("Model pull failed");
            }
        }

        public static async Task<Process?> StartOllamaServeAsync(CancellationToken cancellationToken = default)
        {
            var ollamaPath = GetOllamaPath();
            if (string.IsNullOrEmpty(ollamaPath))
            {
                throw new Exception("Ollama not found");
            }

            // Check if already running
            var existing = Process.GetProcessesByName("ollama");
            if (existing.Length > 0)
            {
                return existing[0];
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = ollamaPath,
                Arguments = "serve",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var process = Process.Start(startInfo);
            await Task.Delay(2000);
            return process;
        }

        public static async Task DownloadAndInstallAsync(IProgress<int> progress,
            IProgress<string> status, CancellationToken cancellationToken)
        {
            try
            {
                status.Report("正在检测最佳下载源...");

                var installUrl = await FindBestDownloadUrlAsync(cancellationToken);
                if (string.IsNullOrEmpty(installUrl))
                {
                    throw new Exception("无法找到合适的下载链接");
                }

                status.Report("准备下载...");

                var tempPath = Path.Combine(Path.GetTempPath(), $"ollama-setup-{Guid.NewGuid():N}.exe");

                try
                {
                    await DownloadFileAsync(installUrl, tempPath, progress, status, cancellationToken);

                    status.Report("正在安装...");
                    progress.Report(100);

                    await InstallOllamaAsync(tempPath, cancellationToken);

                    status.Report("安装完成！");
                }
                finally
                {
                    if (File.Exists(tempPath))
                    {
                        try { File.Delete(tempPath); } catch { }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new Exception($"安装失败: {ex.Message}", ex);
            }
        }

        private static async Task<string> FindBestDownloadUrlAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, AliyunMirrorUrl);
                using var response = await _httpClient.SendAsync(request, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return AliyunMirrorUrl;
                }
            }
            catch { }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, OfficialDownloadUrl);
                using var response = await _httpClient.SendAsync(request, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return OfficialDownloadUrl;
                }
            }
            catch { }

            return OfficialDownloadUrl;
        }

        private static async Task DownloadFileAsync(string url, string destinationPath,
            IProgress<int> progress, IProgress<string> status, CancellationToken cancellationToken)
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            var downloadedBytes = 0L;

            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                downloadedBytes += bytesRead;

                if (totalBytes > 0)
                {
                    var percent = (int)((double)downloadedBytes / totalBytes * 100);
                    progress.Report(Math.Min(100, percent));
                    status.Report($"下载中 {percent}% ({FormatBytes(downloadedBytes)}/{FormatBytes(totalBytes)})");
                }
            }
        }

        private static async Task InstallOllamaAsync(string setupPath, CancellationToken cancellationToken)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = setupPath,
                Arguments = "/S",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = Process.Start(startInfo) ?? throw new Exception("无法启动安装程序");
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                throw new Exception($"安装程序退出代码: {process.ExitCode}");
            }

            await Task.Delay(2000, cancellationToken);
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;

            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:0.##} {sizes[order]}";
        }

        public static List<MirrorSource> GetAvailableMirrorSources()
        {
            return new List<MirrorSource>
            {
                new MirrorSource
                {
                    Name = "自动选择（推荐）",
                    Url = "auto",
                    Description = "程序自动测试并选择速度最快的镜像源",
                    IsDefault = true
                },
                new MirrorSource
                {
                    Name = "阿里云",
                    Url = "https://registry.ollama.ai",
                    Description = "阿里云镜像，速度快且稳定"
                },
                new MirrorSource
                {
                    Name = "魔搭社区",
                    Url = "https://ollama.modelscope.cn",
                    Description = "国内AI社区，资源丰富"
                },
                new MirrorSource
                {
                    Name = "浙江大学",
                    Url = "https://ollama.zju.edu.cn",
                    Description = "浙江大学教育网镜像"
                },
                new MirrorSource
                {
                    Name = "DeepSeek官方",
                    Url = "https://ollama.deepseek.com",
                    Description = "DeepSeek模型专用镜像"
                },
                new MirrorSource
                {
                    Name = "官方源",
                    Url = "https://registry.ollama.ai",
                    Description = "Ollama官方镜像源（可能较慢）"
                }
            };
        }

        public static string GetOllamaConfigPath()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var ollamaDir = Path.Combine(appDataPath, ".ollama");
            if (!Directory.Exists(ollamaDir))
            {
                Directory.CreateDirectory(ollamaDir);
            }
            return Path.Combine(ollamaDir, ConfigFileName);
        }

        public static bool SetMirrorSource(string mirrorUrl, bool isAuto = false)
        {
            try
            {
                var configPath = GetOllamaConfigPath();
                var config = new
                {
                    registry = new
                    {
                        mirrors = new Dictionary<string, string>
                        {
                            { "registry.ollama.ai", isAuto ? "https://registry.ollama.ai" : mirrorUrl }
                        }
                    }
                };

                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                var jsonContent = JsonSerializer.Serialize(config, jsonOptions);
                File.WriteAllText(configPath, jsonContent);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<MirrorSource?> AutoSelectBestMirrorAsync(CancellationToken cancellationToken = default)
        {
            var mirrors = GetAvailableMirrorSources();
            var bestMirror = mirrors.Find(m => m.IsDefault);
            var bestTime = TimeSpan.MaxValue;

            foreach (var mirror in mirrors)
            {
                if (mirror.Url == "auto") continue;

                try
                {
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    using var request = new HttpRequestMessage(HttpMethod.Head, mirror.Url);
                    using var response = await _httpClient.SendAsync(request, cancellationToken);
                    stopwatch.Stop();

                    if (response.IsSuccessStatusCode && stopwatch.Elapsed < bestTime)
                    {
                        bestTime = stopwatch.Elapsed;
                        bestMirror = mirror;
                    }
                }
                catch
                {
                    // 忽略测试失败的镜像
                }
            }

            return bestMirror;
        }

        public static async Task<bool> TestMirrorConnectivityAsync(string url, CancellationToken cancellationToken = default)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, url);
                request.Timeout = TimeSpan.FromSeconds(5);
                using var response = await _httpClient.SendAsync(request, cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public static bool RestartOllamaService()
        {
            try
            {
                var ollamaPath = GetOllamaPath();
                if (string.IsNullOrEmpty(ollamaPath)) return false;

                var processes = Process.GetProcessesByName("ollama");
                foreach (var process in processes)
                {
                    try
                    {
                        process.Kill();
                        process.WaitForExit(5000);
                    }
                    catch { }
                }

                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "sc",
                        Arguments = "start ollama",
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        Verb = "runas"
                    };

                    try
                    {
                        Process.Start(startInfo);
                    }
                    catch
                    {
                        // 如果服务启动失败，尝试直接启动 ollama serve
                        var serveInfo = new ProcessStartInfo
                        {
                            FileName = ollamaPath,
                            Arguments = "serve",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            WindowStyle = ProcessWindowStyle.Hidden
                        };
                        Process.Start(serveInfo);
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
