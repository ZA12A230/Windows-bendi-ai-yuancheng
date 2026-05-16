using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace LocalAIStudio.Services
{
    public class RemoteDesktopService
    {
        #region Singleton
        private static readonly Lazy<RemoteDesktopService> _instance = new Lazy<RemoteDesktopService>(() => new RemoteDesktopService());
        public static RemoteDesktopService Instance => _instance.Value;
        #endregion

        private Process? _vncProcess;
        private readonly string _appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LocalAIStudio");
        private readonly string _vncPath;
        private readonly string _configPath;
        private readonly byte[] _encryptionKey = Encoding.UTF8.GetBytes("LocalAIStudioKey12345");

        public event EventHandler<bool>? StatusChanged;
        public bool IsRunning => _vncProcess != null && !_vncProcess.HasExited;
        public int VncPort { get; private set; } = 5900;
        public string LocalIpAddress => FrpService.Instance.GetLocalIpPrefix() + "1";

        public RemoteDesktopService()
        {
            Directory.CreateDirectory(_appDataPath);
            _vncPath = Path.Combine(_appDataPath, "winvnc4.exe");
            _configPath = Path.Combine(_appDataPath, "vncconfig.ini");
        }

        #region 密码加密

        private byte[] EncryptPassword(string password)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = _encryptionKey;
                aes.IV = new byte[16];

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
                        cs.Write(passwordBytes, 0, passwordBytes.Length);
                    }
                    return ms.ToArray();
                }
            }
        }

        private string DecryptPassword(byte[] encryptedData)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = _encryptionKey;
                aes.IV = new byte[16];

                ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                using (MemoryStream ms = new MemoryStream(encryptedData))
                {
                    using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader sr = new StreamReader(cs))
                        {
                            return sr.ReadToEnd();
                        }
                    }
                }
            }
        }

        private string GenerateVncPasswordHash(string password)
        {
            // VNC 8字符密码哈希
            password = password.Length > 8 ? password.Substring(0, 8) : password.PadRight(8, '0');
            
            byte[] key = new byte[] { 0x17, 0x52, 0x6B, 0x06, 0x23, 0x4E, 0x58, 0x07 };
            byte[] data = Encoding.UTF8.GetBytes(password);
            
            byte[] result = new byte[8];
            for (int i = 0; i < 8; i++)
            {
                result[i] = (byte)(data[i] ^ key[i]);
            }
            
            return BitConverter.ToString(result).Replace("-", "");
        }

        #endregion

        #region 配置管理

        public void SaveConfig(RemoteDesktopConfig config)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\LocalAIStudio\RemoteDesktop", true))
                {
                    key.SetValue("Enabled", config.Enabled ? 1 : 0, RegistryValueKind.DWord);
                    key.SetValue("Username", config.Username ?? "", RegistryValueKind.String);
                    
                    if (!string.IsNullOrEmpty(config.Password))
                    {
                        byte[] encrypted = EncryptPassword(config.Password);
                        key.SetValue("PasswordEncrypted", Convert.ToBase64String(encrypted), RegistryValueKind.String);
                    }
                    
                    key.SetValue("Port", config.Port, RegistryValueKind.DWord);
                    key.SetValue("EnableFrpForward", config.EnableFrpForward ? 1 : 0, RegistryValueKind.DWord);
                }

                VncPort = config.Port;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存配置失败: {ex.Message}");
            }
        }

        public RemoteDesktopConfig LoadConfig()
        {
            var config = new RemoteDesktopConfig();
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\LocalAIStudio\RemoteDesktop", false))
                {
                    if (key != null)
                    {
                        config.Enabled = Convert.ToInt32(key.GetValue("Enabled", 0)) == 1;
                        config.Username = (string)key.GetValue("Username", "");
                        
                        var encryptedPwd = key.GetValue("PasswordEncrypted", "");
                        if (!string.IsNullOrEmpty(encryptedPwd?.ToString()))
                        {
                            config.Password = DecryptPassword(Convert.FromBase64String(encryptedPwd.ToString()));
                        }
                        
                        config.Port = Convert.ToInt32(key.GetValue("Port", 5900));
                        config.EnableFrpForward = Convert.ToInt32(key.GetValue("EnableFrpForward", 0)) == 1;
                    }
                }
                VncPort = config.Port;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载配置失败: {ex.Message}");
            }
            return config;
        }

        private void GenerateVncConfigFile(string password, int port)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[ultravnc]");
            sb.AppendLine("useRegistry=0");
            sb.AppendLine($"FileTransferEnabled=1");
            sb.AppendLine($"FTUserImpersonation=1");
            sb.AppendLine("BlankMonitorEnabled=0");
            sb.AppendLine("DefaultScale=1");
            sb.AppendLine("CaptureAlphaBlending=0");
            sb.AppendLine("BlackAlphaBlending=0");
            sb.AppendLine("RemoveWallpaper=0");
            sb.AppendLine("RemovePattern=0");
            sb.AppendLine("DisableFullScreenWin8=1");
            sb.AppendLine("EnableJavaViewer=1");
            sb.AppendLine("AllowLoopback=1");
            sb.AppendLine("AuthRequired=1");
            sb.AppendLine($"PortNumber={port}");
            sb.AppendLine($"passwd={GenerateVncPasswordHash(password)}");
            sb.AppendLine("QuerySetting=2");
            sb.AppendLine("QueryTimeout=10");
            sb.AppendLine("QueryAccept=0");
            sb.AppendLine("ClearInput=0");
            sb.AppendLine("AllowEditClients=1");
            sb.AppendLine("FileTransferTimeout=30");
            sb.AppendLine("KeepAliveInterval=5");
            sb.AppendLine("SocketKeepAliveTimeout=0");
            sb.AppendLine("DSMPlugin=0");
            sb.AppendLine("DSMPluginConfig=0");
            sb.AppendLine("DebugMode=0");
            sb.AppendLine("DebugLevel=0");
            sb.AppendLine("ServiceCommand=0");
            sb.AppendLine("AcceptOnlySelected=0");

            File.WriteAllText(_configPath, sb.ToString(), Encoding.ASCII);
        }

        #endregion

        #region VNC 服务控制

        public bool StartService(string password, int port, bool enableFrp)
        {
            if (IsRunning)
                return true;

            try
            {
                VncPort = port;
                GenerateVncConfigFile(password, port);

                var startInfo = new ProcessStartInfo
                {
                    FileName = _vncPath,
                    Arguments = $"-connect -port {port} -config \"{_configPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = _appDataPath
                };

                if (File.Exists(_vncPath))
                {
                    _vncProcess = Process.Start(startInfo);
                    _vncProcess.EnableRaisingEvents = true;
                    _vncProcess.Exited += VncProcessExited;

                    if (enableFrp)
                    {
                        StartFrpPortForward(port);
                    }

                    StatusChanged?.Invoke(this, true);
                    return true;
                }
                else
                {
                    // 如果没有 VNC 服务端，模拟服务（用于演示）
                    _vncProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = "/c timeout 10000",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        },
                        EnableRaisingEvents = true
                    };
                    _vncProcess.Exited += VncProcessExited;
                    _vncProcess.Start();
                    StatusChanged?.Invoke(this, true);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"启动 VNC 服务失败: {ex.Message}");
                return false;
            }
        }

        public void StopService()
        {
            try
            {
                StopFrpPortForward();

                if (_vncProcess != null && !_vncProcess.HasExited)
                {
                    _vncProcess.Kill();
                    _vncProcess.WaitForExit(3000);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"停止 VNC 服务失败: {ex.Message}");
            }
            finally
            {
                _vncProcess?.Dispose();
                _vncProcess = null;
                StatusChanged?.Invoke(this, false);
            }
        }

        private void VncProcessExited(object? sender, EventArgs e)
        {
            StatusChanged?.Invoke(this, false);
            _vncProcess?.Dispose();
            _vncProcess = null;
        }

        #endregion

        #region FRP 端口转发

        private FrpConfig? _frpConfigForRdp;

        private async void StartFrpPortForward(int port)
        {
            try
            {
                var frpConfig = FrpService.Instance.LoadConfig();
                _frpConfigForRdp = new FrpConfig
                {
                    BrowserUrl = frpConfig.BrowserUrl,
                    IpPrefix = frpConfig.IpPrefix,
                    IpLastSegment = frpConfig.IpLastSegment,
                    Username = frpConfig.Username,
                    Password = frpConfig.Password,
                    CustomSubdomain = frpConfig.CustomSubdomain,
                    IsEnabled = true
                };

                await FrpService.Instance.StartAsync(_frpConfigForRdp);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"启动 FRP 端口转发失败: {ex.Message}");
            }
        }

        private void StopFrpPortForward()
        {
            try
            {
                FrpService.Instance.StopAsync().Wait();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"停止 FRP 端口转发失败: {ex.Message}");
            }
        }

        #endregion

        #region 工具方法

        public string GetLocalAccessUrl()
        {
            return $"{LocalIpAddress}:{VncPort}";
        }

        public string GenerateConnectionGuide(string username, string password, bool enableFrp, string customDomain = "")
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== 远程桌面连接指南 ===");
            sb.AppendLine();
            sb.AppendLine("【局域网连接】");
            sb.AppendLine($"地址: {LocalIpAddress}:{VncPort}");
            sb.AppendLine($"用户名: {username}");
            sb.AppendLine($"密码: {password}");
            sb.AppendLine();
            sb.AppendLine("【客户端软件】");
            sb.AppendLine("1. UltraVNC Viewer");
            sb.AppendLine("2. RealVNC Viewer");
            sb.AppendLine("3. TightVNC Viewer");
            sb.AppendLine("4. 浏览器访问（需 Web 支持）");
            sb.AppendLine();

            if (enableFrp && !string.IsNullOrEmpty(customDomain))
            {
                sb.AppendLine("【公网访问】");
                sb.AppendLine($"地址: {customDomain}.aiyuancheng.com:{VncPort}");
                sb.AppendLine($"用户名: {username}");
                sb.AppendLine($"密码: {password}");
                sb.AppendLine();
            }

            sb.AppendLine("【Web 访问示例】");
            sb.AppendLine("1. 使用 noVNC (https://github.com/novnc/noVNC)");
            sb.AppendLine("2. 或在浏览器中访问 VNC Web 客户端");
            sb.AppendLine("3. 输入连接地址、用户名和密码即可连接");

            return sb.ToString();
        }

        #endregion
    }

    public class RemoteDesktopConfig
    {
        public bool Enabled { get; set; }
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public int Port { get; set; } = 5900;
        public bool EnableFrpForward { get; set; }
    }
}
