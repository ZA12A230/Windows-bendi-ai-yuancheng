using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace LocalAIStudio.Services
{
    public class FrpService
    {
        #region Singleton
        private static readonly Lazy<FrpService> _instance = new Lazy<FrpService>(() => new FrpService());
        public static FrpService Instance => _instance.Value;
        #endregion

        private Process? _frpProcess;
        private string _frpcPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "frpc.exe");
        private string _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "frpc.ini");
        private CancellationTokenSource? _monitorCts;

        public bool IsRunning => _frpProcess != null && !_frpProcess.HasExited;

        public event EventHandler<bool>? StatusChanged;

        public FrpService()
        {
            // 初始化时检查并保存示例配置（如果不存在）
            InitializeExampleConfig();
        }

        private void InitializeExampleConfig()
        {
            if (!File.Exists(_frpcPath))
            {
                // 创建示例配置说明
                string exampleConfig = @"
# 本应用需要 frpc.exe 才能正常工作
# 请从 https://github.com/fatedier/frp/releases 下载对应版本的 frpc.exe
# 并放置在应用程序同一目录下
";
                File.WriteAllText(_configPath + ".example", exampleConfig, Encoding.UTF8);
            }
        }

        public string GetLocalIpPrefix()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        string ipString = ip.ToString();
                        string[] parts = ipString.Split('.');
                        if (parts.Length == 4)
                        {
                            return $"{parts[0]}.{parts[1]}.{parts[2]}.";
                        }
                    }
                }

                // 如果找不到，使用常见内网网段
                return "192.168.1.";
            }
            catch
            {
                return "192.168.1.";
            }
        }

        public bool ValidateConfig(FrpConfig config)
        {
            if (string.IsNullOrWhiteSpace(config.IpLastSegment))
                return false;

            if (string.IsNullOrWhiteSpace(config.Username))
                return false;

            if (string.IsNullOrWhiteSpace(config.Password))
                return false;

            // 账号名验证：纯英文
            if (!System.Text.RegularExpressions.Regex.IsMatch(config.Username, "^[a-zA-Z0-9_]+$"))
                return false;

            // 密码验证：英文数字组合
            if (!System.Text.RegularExpressions.Regex.IsMatch(config.Password, "^(?=.*[a-zA-Z])(?=.*[0-9])[a-zA-Z0-9]+$"))
                return false;

            return true;
        }

        public string GenerateConfig(FrpConfig config)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[common]");
            sb.AppendLine("server_addr = www.aiyuancheng.com");
            sb.AppendLine("server_port = 7000");
            sb.AppendLine($"token = {config.Password}");
            sb.AppendLine();

            // 本地服务暴露
            int localPort = 8080;
            if (!string.IsNullOrWhiteSpace(config.BrowserUrl))
            {
                try
                {
                    var uri = new Uri(config.BrowserUrl);
                    localPort = uri.Port;
                }
                catch { }
            }

            sb.AppendLine("[web]");
            sb.AppendLine("type = tcp");
            sb.AppendLine($"local_ip = {config.IpPrefix}{config.IpLastSegment}");
            sb.AppendLine($"local_port = {localPort}");
            sb.AppendLine("remote_port = 80");
            sb.AppendLine();

            // 如果有自定义子域名，添加HTTP代理
            if (!string.IsNullOrWhiteSpace(config.CustomSubdomain))
            {
                sb.AppendLine("[web_http]");
                sb.AppendLine("type = http");
                sb.AppendLine($"local_ip = {config.IpPrefix}{config.IpLastSegment}");
                sb.AppendLine($"local_port = {localPort}");
                sb.AppendLine($"custom_domains = {config.CustomSubdomain}.aiyuancheng.com");
            }

            return sb.ToString();
        }

        public async Task<bool> StartAsync(FrpConfig config, CancellationToken cancellationToken = default)
        {
            if (IsRunning)
                return true;

            if (!File.Exists(_frpcPath))
            {
                throw new FileNotFoundException("未找到 frpc.exe，请确保已正确放置 frp 客户端程序");
            }

            // 生成配置文件
            string configContent = GenerateConfig(config);
            File.WriteAllText(_configPath, configContent, Encoding.UTF8);

            // 保存配置
            SaveConfig(config);

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = _frpcPath,
                    Arguments = $"-c \"{_configPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                _frpProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
                _frpProcess.Exited += FrpProcess_Exited;
                _frpProcess.OutputDataReceived += FrpProcess_OutputDataReceived;
                _frpProcess.ErrorDataReceived += FrpProcess_ErrorDataReceived;

                bool started = _frpProcess.Start();
                if (started)
                {
                    _frpProcess.BeginOutputReadLine();
                    _frpProcess.BeginErrorReadLine();

                    _monitorCts = new CancellationTokenSource();
                    _ = Task.Run(() => MonitorProcessAsync(_monitorCts.Token), _monitorCts.Token);

                    StatusChanged?.Invoke(this, true);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"启动 frp 失败: {ex.Message}");
                return false;
            }
        }

        public async Task StopAsync()
        {
            if (!IsRunning)
                return;

            try
            {
                _monitorCts?.Cancel();
                _frpProcess?.Kill();
                await Task.Delay(500);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"停止 frp 失败: {ex.Message}");
            }
            finally
            {
                _frpProcess?.Dispose();
                _frpProcess = null;
                StatusChanged?.Invoke(this, false);
            }
        }

        private void FrpProcess_Exited(object? sender, EventArgs e)
        {
            StatusChanged?.Invoke(this, false);
        }

        private void FrpProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Debug.WriteLine($"frp: {e.Data}");
            }
        }

        private void FrpProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Debug.WriteLine($"frp error: {e.Data}");
            }
        }

        private async Task MonitorProcessAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(1000, cancellationToken);
                if (_frpProcess != null && _frpProcess.HasExited)
                {
                    StatusChanged?.Invoke(this, false);
                    break;
                }
            }
        }

        public FrpConfig LoadConfig()
        {
            var config = new FrpConfig();
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\LocalAIStudio\Frp", false))
                {
                    if (key != null)
                    {
                        config.BrowserUrl = (string)key.GetValue("BrowserUrl", "http://localhost:8080");
                        config.IpPrefix = (string)key.GetValue("IpPrefix", GetLocalIpPrefix());
                        config.IpLastSegment = (string)key.GetValue("IpLastSegment", "");
                        config.Username = (string)key.GetValue("Username", "");
                        config.Password = (string)key.GetValue("Password", "");
                        config.CustomSubdomain = (string)key.GetValue("CustomSubdomain", "");
                        config.IsEnabled = Convert.ToBoolean(key.GetValue("IsEnabled", false));
                    }
                    else
                    {
                        config.IpPrefix = GetLocalIpPrefix();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载配置失败: {ex.Message}");
                config.IpPrefix = GetLocalIpPrefix();
            }
            return config;
        }

        public void SaveConfig(FrpConfig config)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\LocalAIStudio\Frp", true))
                {
                    key.SetValue("BrowserUrl", config.BrowserUrl ?? "", RegistryValueKind.String);
                    key.SetValue("IpPrefix", config.IpPrefix ?? "", RegistryValueKind.String);
                    key.SetValue("IpLastSegment", config.IpLastSegment ?? "", RegistryValueKind.String);
                    key.SetValue("Username", config.Username ?? "", RegistryValueKind.String);
                    key.SetValue("Password", config.Password ?? "", RegistryValueKind.String);
                    key.SetValue("CustomSubdomain", config.CustomSubdomain ?? "", RegistryValueKind.String);
                    key.SetValue("IsEnabled", config.IsEnabled ? 1 : 0, RegistryValueKind.DWord);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存配置失败: {ex.Message}");
            }
        }
    }

    public class FrpConfig
    {
        public string BrowserUrl { get; set; } = "http://localhost:8080";
        public string IpPrefix { get; set; } = "";
        public string IpLastSegment { get; set; } = "";
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string CustomSubdomain { get; set; } = "";
        public bool IsEnabled { get; set; } = false;
    }
}
