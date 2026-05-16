using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
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

    public class OllamaService
    {
        private const string OfficialDownloadUrl = "https://ollama.com/download";
        private const string AliyunMirrorUrl = "https://mirrors.aliyun.com/ollama/windows";
        private const string DefaultInstallPath = @"C:\Program Files\Ollama\ollama.exe";
        private const string RegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\ollama.exe";

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
    }
}
