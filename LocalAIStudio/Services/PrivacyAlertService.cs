using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace LocalAIStudio.Services
{
    public class PrivacyAlertService
    {
        #region Singleton
        private static readonly Lazy<PrivacyAlertService> _instance = 
            new Lazy<PrivacyAlertService>(() => new PrivacyAlertService());
        public static PrivacyAlertService Instance => _instance.Value;
        #endregion

        private NotifyIcon _notifyIcon;
        private ContextMenuStrip _contextMenu;
        private bool _isMonitoring = false;
        private bool _isCameraActive = false;
        private bool _isMicrophoneActive = false;
        private bool _isRemoteAccessActive = false;
        private bool _initialized = false;

        public event EventHandler DisconnectRequested;
        public event EventHandler<string> LogMessage;

        public bool IsCameraActive
        {
            get => _isCameraActive;
            set
            {
                _isCameraActive = value;
                if (_initialized) UpdateIcon();
            }
        }

        public bool IsMicrophoneActive
        {
            get => _isMicrophoneActive;
            set
            {
                _isMicrophoneActive = value;
                if (_initialized) UpdateIcon();
            }
        }

        public bool IsRemoteAccessActive
        {
            get => _isRemoteAccessActive;
            set
            {
                _isRemoteAccessActive = value;
                if (_initialized) UpdateIcon();
            }
        }

        public PrivacyAlertService()
        {
            // 不在构造函数中初始化，延迟到 StartMonitoring
        }

        private void InitializeNotifyIcon()
        {
            if (_initialized) return;

            try
            {
                _contextMenu = new ContextMenuStrip();
                _contextMenu.Items.Add("⚠️ 正在被远程访问", null, (s, e) => ShowStatus());
                _contextMenu.Items.Add(new ToolStripSeparator());
                _contextMenu.Items.Add("🔴 强制断开连接", null, (s, e) => ForceDisconnect());
                _contextMenu.Items.Add(new ToolStripSeparator());
                _contextMenu.Items.Add("📊 查看状态", null, (s, e) => ShowStatus());
                _contextMenu.Items.Add("❌ 关闭提示", null, (s, e) => HideAlert());

                _notifyIcon = new NotifyIcon
                {
                    Icon = CreateWarningIcon(),
                    Visible = false,
                    Text = "Local AI Studio",
                    ContextMenuStrip = _contextMenu
                };

                _notifyIcon.DoubleClick += (s, e) => ShowMainWindow();
                _initialized = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"初始化通知图标失败: {ex.Message}");
            }
        }

        private Icon CreateWarningIcon()
        {
            try
            {
                var bitmap = new Bitmap(32, 32);
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.Clear(Color.Transparent);

                    using (var brush = new SolidBrush(Color.FromArgb(255, 245, 158, 11)))
                    {
                        g.FillEllipse(brush, 2, 2, 28, 28);
                    }

                    using (var pen = new Pen(Color.White, 2))
                    {
                        g.DrawLine(pen, 16, 8, 16, 18);
                        g.FillEllipse(Brushes.White, 14, 20, 4, 4);
                    }
                }

                return Icon.FromHandle(bitmap.GetHicon());
            }
            catch
            {
                return SystemIcons.Warning;
            }
        }

        private Icon CreateCameraIcon()
        {
            try
            {
                var bitmap = new Bitmap(32, 32);
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.Clear(Color.Transparent);

                    using (var brush = new SolidBrush(Color.FromArgb(255, 239, 68, 68)))
                    {
                        g.FillEllipse(brush, 2, 2, 28, 28);
                    }

                    using (var pen = new Pen(Color.White, 2))
                    {
                        g.DrawRectangle(pen, 10, 10, 12, 8);
                        g.DrawLine(pen, 8, 14, 10, 14);
                        g.DrawLine(pen, 22, 14, 24, 14);
                    }
                }

                return Icon.FromHandle(bitmap.GetHicon());
            }
            catch
            {
                return SystemIcons.Error;
            }
        }

        private Icon CreateMicrophoneIcon()
        {
            try
            {
                var bitmap = new Bitmap(32, 32);
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.Clear(Color.Transparent);

                    using (var brush = new SolidBrush(Color.FromArgb(255, 168, 85, 247)))
                    {
                        g.FillEllipse(brush, 2, 2, 28, 28);
                    }

                    g.FillEllipse(Brushes.White, 12, 8, 8, 12);
                    g.DrawLine(new Pen(Color.White, 2), 16, 20, 16, 24);
                    g.DrawLine(new Pen(Color.White, 2), 12, 24, 20, 24);
                }

                return Icon.FromHandle(bitmap.GetHicon());
            }
            catch
            {
                return SystemIcons.Information;
            }
        }

        private Icon CreateSecureIcon()
        {
            try
            {
                var bitmap = new Bitmap(32, 32);
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.Clear(Color.Transparent);

                    using (var brush = new SolidBrush(Color.FromArgb(255, 34, 197, 94)))
                    {
                        g.FillEllipse(brush, 2, 2, 28, 28);
                    }

                    g.DrawString("✓", new Font(System.Drawing.FontFamily.GenericSansSerif, 16, System.Drawing.FontStyle.Bold), 
                                Brushes.White, 6, 4);
                }

                return Icon.FromHandle(bitmap.GetHicon());
            }
            catch
            {
                return SystemIcons.Application;
            }
        }

        private void UpdateIcon()
        {
            if (_notifyIcon == null) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    if (_isCameraActive)
                    {
                        _notifyIcon.Icon = CreateCameraIcon();
                        _notifyIcon.Text = "⚠️ 摄像头正在被访问";
                        _notifyIcon.Visible = true;
                        ShowBalloonTip("隐私提醒", "摄像头正在被远程访问", ToolTipIcon.Warning);
                    }
                    else if (_isMicrophoneActive)
                    {
                        _notifyIcon.Icon = CreateMicrophoneIcon();
                        _notifyIcon.Text = "⚠️ 麦克风正在被访问";
                        _notifyIcon.Visible = true;
                        ShowBalloonTip("隐私提醒", "麦克风正在被远程访问", ToolTipIcon.Warning);
                    }
                    else if (_isRemoteAccessActive)
                    {
                        _notifyIcon.Icon = CreateWarningIcon();
                        _notifyIcon.Text = "⚠️ 正在被远程访问";
                        _notifyIcon.Visible = true;
                    }
                    else
                    {
                        _notifyIcon.Visible = false;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"更新图标失败: {ex.Message}");
                }
            });
        }

        private void ShowBalloonTip(string title, string message, ToolTipIcon icon)
        {
            try
            {
                _notifyIcon.ShowBalloonTip(3000, title, message, icon);
            }
            catch { }
        }

        public void ShowAlert(string title, string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _notifyIcon.ShowBalloonTip(3000, title, message, ToolTipIcon.Info);
            });
        }

        public void HideAlert()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                }
                _isCameraActive = false;
                _isMicrophoneActive = false;
                _isRemoteAccessActive = false;
            });
        }

        public void ForceDisconnect()
        {
            LogMessage.Invoke(this, "用户强制断开连接");
            DisconnectRequested.Invoke(this, EventArgs.Empty);

            Application.Current.Dispatcher.Invoke(() =>
            {
                Task.Run(async () =>
                {
                    await StopAllServices();
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        System.Windows.MessageBox.Show(
                            "已强制断开所有远程连接。\n\n如果问题持续存在，请检查网络连接。",
                            "已断开连接",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Information);
                    });
                });
            });
        }

        private async Task StopAllServices()
        {
            try
            {
                RemoteAccessApiServer.Instance.StopServer();
                HardwareMonitorService.Instance.StopCamera();
                HardwareMonitorService.Instance.StopIntercom();
                HardwareMonitorService.Instance.StopRemoteAccessMonitoring();
                RemoteDesktopService.Instance.StopService();
            }
            catch (Exception ex)
            {
                LogMessage.Invoke(this, $"断开连接时出错: {ex.Message}");
            }
        }

        public void ShowStatus()
        {
            var status = $"📊 Local AI Studio 状态\n\n" +
                        $"摄像头: {((_isCameraActive ? "🔴 正在访问" : "✅ 未使用"))}\n" +
                        $"麦克风: {((_isMicrophoneActive ? "🔴 正在访问" : "✅ 未使用"))}\n" +
                        $"远程访问: {((_isRemoteAccessActive ? "⚠️ 已连接" : "✅ 未连接"))}\n\n" +
                        $"点击\"强制断开连接\"可立即终止所有远程访问。";

            System.Windows.MessageBox.Show(status, "状态信息", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }

        private void ShowMainWindow()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow != null)
                {
                    mainWindow.Show();
                    mainWindow.WindowState = WindowState.Normal;
                    mainWindow.Activate();
                }
            });
        }

        public void StartMonitoring()
        {
            try
            {
                // 先初始化通知图标
                InitializeNotifyIcon();
                _isMonitoring = true;
                
                try
                {
                    HardwareMonitorService.Instance.CameraStateChanged += OnCameraStateChanged;
                    HardwareMonitorService.Instance.MicrophoneActiveChanged += OnMicrophoneStateChanged;
                    HardwareMonitorService.Instance.RemoteAccessStarted += OnRemoteAccessStarted;
                    HardwareMonitorService.Instance.RemoteAccessStopped += OnRemoteAccessStopped;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"订阅硬件监控事件失败: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"启动隐私监控失败: {ex.Message}");
            }
        }

        public void StopMonitoring()
        {
            _isMonitoring = false;
            HideAlert();

            HardwareMonitorService.Instance.CameraStateChanged -= OnCameraStateChanged;
            HardwareMonitorService.Instance.MicrophoneActiveChanged -= OnMicrophoneStateChanged;
            HardwareMonitorService.Instance.RemoteAccessStarted -= OnRemoteAccessStarted;
            HardwareMonitorService.Instance.RemoteAccessStopped -= OnRemoteAccessStopped;
        }

        private void OnCameraStateChanged(object sender, bool active)
        {
            IsCameraActive = active;
            LogMessage.Invoke(this, $"摄像头状态变更: {((active ? "启用" : "禁用"))}");
        }

        private void OnMicrophoneStateChanged(object sender, bool active)
        {
            IsMicrophoneActive = active;
            LogMessage.Invoke(this, $"麦克风状态变更: {((active ? "启用" : "禁用"))}");
        }

        private void OnRemoteAccessStarted(object sender, EventArgs e)
        {
            IsRemoteAccessActive = true;
            ShowAlert("远程访问已启动", "您的计算机正在被远程访问。\n如需终止，请点击通知图标并选择\"强制断开连接\"。");
            LogMessage.Invoke(this, "远程访问已启动");
        }

        private void OnRemoteAccessStopped(object sender, EventArgs e)
        {
            IsRemoteAccessActive = false;
            if (!_isCameraActive && !_isMicrophoneActive)
            {
                HideAlert();
            }
            LogMessage.Invoke(this, "远程访问已停止");
        }
    }
}
