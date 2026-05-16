using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace LocalAIStudio.Services
{
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

        public static string? GetInstallPath()
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

            await fileStream.FlushAsync(cancellationToken);
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
