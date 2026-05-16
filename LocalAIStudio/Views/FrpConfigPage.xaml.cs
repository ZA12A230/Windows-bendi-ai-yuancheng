using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Navigation;
using LocalAIStudio.Services;

namespace LocalAIStudio.Views
{
    public partial class FrpConfigPage : UserControl
    {
        private FrpConfig _currentConfig;
        private bool _isStarting = false;

        public FrpConfigPage()
        {
            InitializeComponent();
            Loaded += FrpConfigPage_Loaded;
        }

        private void FrpConfigPage_Loaded(object sender, RoutedEventArgs e)
        {
            _currentConfig = FrpService.Instance.LoadConfig();
            FillFormFromConfig(_currentConfig);
            UpdateFrpStatusDisplay();

            // 订阅服务状态变化
            FrpService.Instance.StatusChanged += FrpService_StatusChanged;
        }

        private void FillFormFromConfig(FrpConfig config)
        {
            BrowserUrlTextBox.Text = config.BrowserUrl;
            IpPrefixText.Text = config.IpPrefix;
            IpLastSegmentTextBox.Text = config.IpLastSegment;
            UsernameTextBox.Text = config.Username;
            
            // 处理密码
            if (!string.IsNullOrEmpty(config.Password))
            {
                PasswordBox.Password = config.Password;
            }
            
            CustomSubdomainTextBox.Text = config.CustomSubdomain;
            EnableFrpToggle.IsChecked = config.IsEnabled;
            SubdomainSection.Visibility = config.IsEnabled ? Visibility.Visible : Visibility.Collapsed;

            UpdateAccessInfo();
        }

        private bool ValidateInput()
        {
            bool isValid = true;

            // 验证 IP 最后一段
            IpErrorText.Visibility = Visibility.Collapsed;
            if (string.IsNullOrWhiteSpace(IpLastSegmentTextBox.Text))
            {
                IpErrorText.Text = "IPv4地址最后一段必须填写";
                IpErrorText.Visibility = Visibility.Visible;
                isValid = false;
            }
            else if (!int.TryParse(IpLastSegmentTextBox.Text, out int ipPart) || ipPart < 0 || ipPart > 255)
            {
                IpErrorText.Text = "请输入有效的数字（0-255）";
                IpErrorText.Visibility = Visibility.Visible;
                isValid = false;
            }

            // 验证账号名
            UsernameErrorText.Visibility = Visibility.Collapsed;
            if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
            {
                UsernameErrorText.Text = "账号名必须填写";
                UsernameErrorText.Visibility = Visibility.Visible;
                isValid = false;
            }
            else if (!System.Text.RegularExpressions.Regex.IsMatch(UsernameTextBox.Text, "^[a-zA-Z0-9_]+$"))
            {
                UsernameErrorText.Text = "账号名仅允许英文字母、数字和下划线";
                UsernameErrorText.Visibility = Visibility.Visible;
                isValid = false;
            }

            // 验证密码
            PasswordErrorText.Visibility = Visibility.Collapsed;
            if (string.IsNullOrWhiteSpace(PasswordBox.Password))
            {
                PasswordErrorText.Text = "密码必须填写";
                PasswordErrorText.Visibility = Visibility.Visible;
                isValid = false;
            }
            else if (!System.Text.RegularExpressions.Regex.IsMatch(PasswordBox.Password, "^(?=.*[a-zA-Z])(?=.*[0-9])[a-zA-Z0-9]+$"))
            {
                PasswordErrorText.Text = "密码必须包含英文和数字的组合";
                PasswordErrorText.Visibility = Visibility.Visible;
                isValid = false;
            }

            return isValid;
        }

        private FrpConfig GetConfigFromForm()
        {
            return new FrpConfig
            {
                BrowserUrl = BrowserUrlTextBox.Text,
                IpPrefix = IpPrefixText.Text,
                IpLastSegment = IpLastSegmentTextBox.Text,
                Username = UsernameTextBox.Text,
                Password = PasswordBox.Password,
                CustomSubdomain = CustomSubdomainTextBox.Text,
                IsEnabled = EnableFrpToggle.IsChecked ?? false
            };
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInput())
                return;

            var config = GetConfigFromForm();
            FrpService.Instance.SaveConfig(config);
            _currentConfig = config;

            MessageBox.Show("配置保存成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            UpdateAccessInfo();
        }

        private async void StartStopButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isStarting)
                return;

            try
            {
                _isStarting = true;
                StartStopButton.IsEnabled = false;

                if (FrpService.Instance.IsRunning)
                {
                    // 停止服务
                    await FrpService.Instance.StopAsync();
                    StartStopButton.Content = "启动服务";
                    StartStopButton.Background = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0x10, 0xB9, 0x81));
                }
                else
                {
                    // 验证并启动
                    if (!ValidateInput())
                        return;

                    var config = GetConfigFromForm();
                    FrpService.Instance.SaveConfig(config);
                    _currentConfig = config;

                    bool started = await FrpService.Instance.StartAsync(config);
                    if (started)
                    {
                        StartStopButton.Content = "停止服务";
                        StartStopButton.Background = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(0xEF, 0x44, 0x44));
                        MessageBox.Show("内网穿透服务已启动！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("启动失败，请检查 frpc.exe 是否在应用目录中。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isStarting = false;
                StartStopButton.IsEnabled = true;
            }
        }

        private void EnableFrpToggle_Changed(object sender, RoutedEventArgs e)
        {
            bool isEnabled = EnableFrpToggle.IsChecked ?? false;
            SubdomainSection.Visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = e.Uri.ToString(),
                UseShellExecute = true
            });
            e.Handled = true;
        }

        private void IpLastSegmentTextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            // 只允许数字输入
            if (!char.IsDigit(e.Text[0]))
            {
                e.Handled = true;
            }
        }

        private void FrpService_StatusChanged(object? sender, bool isRunning)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateFrpStatusDisplay();
            });
        }

        private void UpdateFrpStatusDisplay()
        {
            if (FrpService.Instance.IsRunning)
            {
                StatusIndicator.Fill = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x10, 0xB9, 0x81));
                StatusText.Text = "已连接";
                StatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x06, 0x5F, 0x46));

                StartStopButton.Content = "停止服务";
                StartStopButton.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xEF, 0x44, 0x44));

                AccessInfoSection.Visibility = Visibility.Visible;
            }
            else
            {
                StatusIndicator.Fill = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x94, 0xA3, 0xB8));
                StatusText.Text = "未连接";
                StatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x64, 0x74, 0x8B));

                StartStopButton.Content = "启动服务";
                StartStopButton.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x10, 0xB9, 0x81));

                AccessInfoSection.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateAccessInfo()
        {
            if (!string.IsNullOrEmpty(_currentConfig?.IpLastSegment))
            {
                string localUrl = $"http://{_currentConfig.IpPrefix}{_currentConfig.IpLastSegment}:8080";
                LocalAccessUrl.Text = $"本地访问：{localUrl}";

                if (!string.IsNullOrEmpty(_currentConfig.CustomSubdomain))
                {
                    string remoteUrl = $"http://{_currentConfig.CustomSubdomain}.aiyuancheng.com";
                    RemoteAccessUrl.Text = $"外网访问：{remoteUrl}";
                    RemoteAccessUrl.Visibility = Visibility.Visible;
                }
                else
                {
                    RemoteAccessUrl.Visibility = Visibility.Collapsed;
                }

                AccessInfoSection.Visibility = Visibility.Visible;
            }
        }
    }
}
