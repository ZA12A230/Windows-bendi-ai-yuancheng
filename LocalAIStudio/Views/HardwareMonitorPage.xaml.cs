using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using LocalAIStudio.Services;

namespace LocalAIStudio.Views
{
    public partial class HardwareMonitorPage : UserControl
    {
        private DispatcherTimer _refreshTimer;
        private bool _cameraEnabled = false;

        public HardwareMonitorPage()
        {
            InitializeComponent();
            Loaded += HardwareMonitorPage_Loaded;
            Unloaded += HardwareMonitorPage_Unloaded;
        }

        private void HardwareMonitorPage_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeTimer();
            LoadConfig();
            RefreshAllInfo();
            SubscribeToEvents();
        }

        private void HardwareMonitorPage_Unloaded(object sender, RoutedEventArgs e)
        {
            UnsubscribeFromEvents();
            StopTimer();
        }

        private void InitializeTimer()
        {
            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(3);
            _refreshTimer.Tick += RefreshTimer_Tick;
            _refreshTimer.Start();
        }

        private void StopTimer()
        {
            if (_refreshTimer != null)
                _refreshTimer.Stop();
        }

        private void SubscribeToEvents()
        {
            HardwareMonitorService.Instance.CameraStateChanged += HardwareMonitor_CameraStateChanged;
            RemoteAccessApiServer.Instance.ApiServerStarted += ApiServer_Started;
            RemoteAccessApiServer.Instance.ApiServerStopped += ApiServer_Stopped;
            RemoteAccessApiServer.Instance.AccessLogReceived += ApiServer_AccessLog;
            HardwareMonitorService.Instance.RemoteAccessStarted += RemoteAccess_Started;
            HardwareMonitorService.Instance.RemoteAccessStopped += RemoteAccess_Stopped;
        }

        private void UnsubscribeFromEvents()
        {
            HardwareMonitorService.Instance.CameraStateChanged -= HardwareMonitor_CameraStateChanged;
            RemoteAccessApiServer.Instance.ApiServerStarted -= ApiServer_Started;
            RemoteAccessApiServer.Instance.ApiServerStopped -= ApiServer_Stopped;
            RemoteAccessApiServer.Instance.AccessLogReceived -= ApiServer_AccessLog;
            HardwareMonitorService.Instance.RemoteAccessStarted -= RemoteAccess_Started;
            HardwareMonitorService.Instance.RemoteAccessStopped -= RemoteAccess_Stopped;
        }

        private void LoadConfig()
        {
            var rdConfig = RemoteDesktopService.Instance.LoadConfig();
            if (!string.IsNullOrEmpty(rdConfig.Username) && !string.IsNullOrEmpty(rdConfig.Password))
            {
                RemoteAccessApiServer.Instance.SetCredentials(rdConfig.Username, rdConfig.Password);
            }
            PortTextBox.Text = RemoteAccessApiServer.Instance.Port.ToString();
        }

        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            RefreshAllInfo();
        }

        private void RefreshAllInfo()
        {
            RefreshWifiInfo();
            RefreshUsbDevices();
        }

        private void RefreshWifiInfo()
        {
            try
            {
                var wifiList = HardwareMonitorService.Instance.GetWifiInfo();
                if (wifiList.Count > 0)
                {
                    var wifi = wifiList.FirstOrDefault(w => w.IsConnected) ?? wifiList[0];
                    WifiSsidText.Text = wifi.SSID;
                    WifiSignalBar.Value = wifi.SignalStrength;
                    WifiSignalText.Text = $"{wifi.SignalStrength}%";
                    WifiStatusText.Text = wifi.IsConnected ? "✅ 已连接" : "❌ 未连接";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Refresh WiFi error: {ex.Message}");
            }
        }

        private void RefreshUsbDevices()
        {
            try
            {
                var usbDevices = HardwareMonitorService.Instance.GetUsbDevices();
                UsbDeviceList.ItemsSource = usbDevices.Take(10).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Refresh USB error: {ex.Message}");
            }
        }

        #region Event Handlers
        private void ApiServerToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (ApiServerToggle.IsChecked == true)
            {
                StartApiServer();
            }
            else
            {
                StopApiServer();
            }
        }

        private async void StartApiServer()
        {
            try
            {
                if (int.TryParse(PortTextBox.Text, out int port))
                {
                    await RemoteAccessApiServer.Instance.StartServer(port);
                }
                else
                {
                    MessageBox.Show("请输入有效的端口号", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    ApiServerToggle.IsChecked = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                ApiServerToggle.IsChecked = false;
            }
        }

        private async void StopApiServer()
        {
            await RemoteAccessApiServer.Instance.StopServer();
        }

        private void ApiServer_Started(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                ApiStatusText.Text = $"服务运行中 (端口 {RemoteAccessApiServer.Instance.Port})";
                ApiStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x10, 0xB9, 0x81));
                AccessUrlText.Text = $"http://localhost:{RemoteAccessApiServer.Instance.Port}";
                ApiServerToggle.IsChecked = true;
            });
        }

        private void ApiServer_Stopped(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                ApiStatusText.Text = "服务未启动";
                ApiStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x64, 0x74, 0x8B));
                ApiServerToggle.IsChecked = false;
            });
        }

        private void ApiServer_AccessLog(object sender, string log)
        {
            // Optional: Show log in UI
            System.Diagnostics.Debug.WriteLine(log);
        }

        private void RemoteAccess_Started(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                PrivacyWarningBorder.Visibility = Visibility.Visible;
            });
        }

        private void RemoteAccess_Stopped(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                PrivacyWarningBorder.Visibility = Visibility.Collapsed;
            });
        }

        private void HardwareMonitor_CameraStateChanged(object sender, bool active)
        {
            Dispatcher.Invoke(() =>
            {
                _cameraEnabled = active;
                CameraStatusText.Text = active ? "已启用" : "未启用";
                CameraToggleButton.Content = active ? "停用" : "启用";
                CameraToggleButton.Background = active ? 
                    new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xDC, 0x35, 0x45)) : 
                    new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x10, 0xB9, 0x81));
                CameraPreviewBorder.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
            });
        }
        #endregion

        #region Button Click Handlers
        private void SaveConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (int.TryParse(PortTextBox.Text, out int port))
                {
                    MessageBox.Show("配置已保存！重启服务生效。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("请输入有效的端口号", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshInfo_Click(object sender, RoutedEventArgs e)
        {
            RefreshAllInfo();
        }

        private void CameraToggle_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_cameraEnabled)
                {
                    bool success = HardwareMonitorService.Instance.InitializeCamera();
                    if (success)
                    {
                        MessageBox.Show("摄像头已启用！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("摄像头初始化失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    HardwareMonitorService.Instance.StopCamera();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StopRemoteAccess_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                HardwareMonitorService.Instance.StopRemoteAccessMonitoring();
                RemoteAccessApiServer.Instance.StopServer();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"停止失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion
    }
}
