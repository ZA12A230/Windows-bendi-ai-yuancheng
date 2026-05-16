using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using LocalAIAssistant.Models;

namespace LocalAIAssistant.Services
{
    public class TunnelService
    {
        private Process? _frpProcess;
        private string? _publicUrl;
        private bool _isRunning;

        public async Task<string> StartTunnelAsync(TunnelConfig config)
        {
            if (_isRunning)
            {
                throw new InvalidOperationException("Tunnel is already running");
            }

            var frpPath = FindFrpPath();
            if (string.IsNullOrEmpty(frpPath))
            {
                frpPath = await DownloadFrpAsync();
            }

            var configPath = GenerateFrpConfig(config);

            _frpProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = frpPath,
                    Arguments = $"-c \"{configPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            _frpProcess.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data) && e.Data.Contains("start proxy success"))
                {
                    _publicUrl = $"http://{config.ServerAddress}:{config.RemotePort}";
                }
            };

            _frpProcess.Start();
            _frpProcess.BeginOutputReadLine();
            _isRunning = true;

            await Task.Delay(2000);

            if (string.IsNullOrEmpty(_publicUrl))
            {
                _publicUrl = $"http://{config.ServerAddress}:{config.RemotePort}";
            }

            return _publicUrl;
        }

        public async Task StopTunnelAsync()
        {
            if (_frpProcess != null && !_frpProcess.HasExited)
            {
                _frpProcess.Kill();
                _frpProcess = null;
            }

            _isRunning = false;
            _publicUrl = null;
        }

        private string? FindFrpPath()
        {
            var possiblePaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "frp", "frpc.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "frp", "frpc.exe"),
                "frpc.exe"
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return null;
        }

        private async Task<string> DownloadFrpAsync()
        {
            var frpDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "frp");
            Directory.CreateDirectory(frpDir);

            var frpPath = Path.Combine(frpDir, "frpc.exe");

            if (!File.Exists(frpPath))
            {
                using var client = new WebClient();
                var downloadUrl = "https://github.com/fatedier/frp/releases/download/v0.52.3/frp_0.52.3_windows_amd64.zip";
                var zipPath = Path.Combine(frpDir, "frp.zip");

                await client.DownloadFileTaskAsync(downloadUrl, zipPath);

                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, frpDir);
                File.Delete(zipPath);

                var extractedDir = Directory.GetDirectories(frpDir, "frp_*")[0];
                foreach (var file in Directory.GetFiles(extractedDir))
                {
                    File.Move(file, Path.Combine(frpDir, Path.GetFileName(file)));
                }
                Directory.Delete(extractedDir, true);
            }

            return frpPath;
        }

        private string GenerateFrpConfig(TunnelConfig config)
        {
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "frp_config.ini");

            var configContent = $@"
[common]
server_addr = {config.ServerAddress}
server_port = {config.ServerPort}
{(string.IsNullOrEmpty(config.AuthToken) ? "" : $"token = {config.AuthToken}")}

[ollama]
type = tcp
local_ip = 127.0.0.1
local_port = {config.LocalPort}
remote_port = {config.RemotePort}
{(string.IsNullOrEmpty(config.Subdomain) ? "" : $"subdomain = {config.Subdomain}")}
";

            File.WriteAllText(configPath, configContent);
            return configPath;
        }
    }
}
