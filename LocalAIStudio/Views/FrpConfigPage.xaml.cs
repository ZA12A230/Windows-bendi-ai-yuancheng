using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using LocalAIStudio.Services;

namespace LocalAIStudio.Views
{
    public partial class FrpConfigPage : UserControl
    {
        private FrpConfig _config = new FrpConfig();
        private bool _isStarting = false;

        public FrpConfigPage()
        {
            InitializeComponent();
            Loaded += FrpConfigPage_Loaded;
            Unloaded += FrpConfigPage_Unloaded;
        }

        private void FrpConfigPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadConfig();
            UpdateStatusDisplay();
            FrpService.Instance.StatusChanged += FrpService_StatusChanged;
        }

        private void FrpConfigPage_Unloaded(object sender, RoutedEventArgs e)
        {
            FrpService.Instance.StatusChanged -= FrpService_StatusChanged;
        }

        private void LoadConfig()
        {
            _config = FrpService.Instance.LoadConfig();

            BrowserUrlTextBox.Text = _config.BrowserUrl;
            IpPrefixText.Text = _config.IpPrefix;
            IpLastSegmentTextBox.Text = _config.IpLastSegment;
            UsernameTextBox.Text = _config.Username;
            // PasswordBox is secure, we'll not pre-fill, but will save
            CustomSubdomainTextBox.Text = _config.CustomSubdomain;
            EnableFrpToggle.IsChecked = _config.IsEnabled;

            UpdateSubdomainVisibility();
        }

        private void FrpService_StatusChanged(object? sender, bool isRunning)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateStatusDisplay();
            });
        }

        private void UpdateStatusDisplay()
        {
            bool isRunning = FrpService.Instance.IsRunning;

            if (isRunning)
            {
                StatusIndicator.Fill = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x10, 0xB9, 0x81));
                StatusText.Text = "已连接";
                StatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x10, 0xB9, 0x81));
                StartStopButton.Content = "停止服务";
                StartStopButton.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xEF, 0x44, 0x44));
                AccessInfoSection.Visibility = Visibility.Visible;
                UpdateAccessUrls();
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

        private void UpdateAccessUrls()
        {
            string fullIp = $"{_config.IpPrefix}{_config.IpLastSegment}";
            string localUrl = _config.BrowserUrl.Replace("localhost", fullIp);
            LocalAccessUrl.Text = $"局域网访问: {localUrl}";

            if (!string.IsNullOrWhiteSpace(_config.CustomSubdomain))
            {
                RemoteAccessUrl.Text = $"公网访问: http://{_config.CustomSubdomain}.aiyuancheng.com";
                RemoteAccessUrl.Visibility = Visibility.Visible;
            }
            else
            {
                RemoteAccessUrl.Visibility = Visibility.Collapsed;
            }
        }

        private void EnableFrpToggle_Changed(object sender, RoutedEventArgs e)
        {
            UpdateSubdomainVisibility();
        }

        private void UpdateSubdomainVisibility()
        {
            if (EnableFrpToggle.IsChecked == true)
            {
                SubdomainSection.Visibility = Visibility.Visible;
            }
            else
            {
                SubdomainSection.Visibility = Visibility.Collapsed;
            }
        }

        private void IpLastSegmentTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // 只允许输入数字
            if (!char.IsDigit(e.Text, 0))
            {
                e.Handled = true;
            }
        }

        private bool ValidateInputs()
        {
            bool isValid = true;

            // 验证IP最后一段
            if (string.IsNullOrWhiteSpace(IpLastSegmentTextBox.Text))
            {
                IpErrorText.Text = "请填写IPv4地址的最后一段";
                IpErrorText.Visibility = Visibility.Visible;
                isValid = false;
            }
            else if (!byte.TryParse(IpLastSegmentTextBox.Text, out byte ipPart) || ipPart == 0 || ipPart > 254)
            {
                IpErrorText.Text = "IP段必须在 1-254 之间";
                IpErrorText.Visibility = Visibility.Visible;
                isValid = false;
            }
            else
            {
                IpErrorText.Visibility = Visibility.Collapsed;
            }

            // 验证用户名
            if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
            {
                UsernameErrorText.Text = "请填写账号名";
                UsernameErrorText.Visibility = Visibility.Visible;
                isValid = false;
            }
            else if (!System.Text.RegularExpressions.Regex.IsMatch(UsernameTextBox.Text, "^[a-zA-Z0-9_]+$"))
            {
                UsernameErrorText.Text = "账号名仅允许英文字母、数字和下划线";
                UsernameErrorText.Visibility = Visibility.Visible;
                isValid = false;
            }
            else
            {
                UsernameErrorText.Visibility = Visibility.Collapsed;
            }

            // 验证密码
            var password = PasswordBox.Password;
            if (string.IsNullOrWhiteSpace(password))
            {
                PasswordErrorText.Text = "请填写密码";
                PasswordErrorText.Visibility = Visibility.Visible;
                isValid = false;
            }
            else if (!System.Text.RegularExpressions.Regex.IsMatch(password, "^(?=.*[a-zA-Z])(?=.*[0-9])[a-zA-Z0-9]+$"))
            {
                PasswordErrorText.Text = "密码必须包含英文和数字的组合";
                PasswordErrorText.Visibility = Visibility.Visible;
                isValid = false;
            }
            else
            {
                PasswordErrorText.Visibility = Visibility.Collapsed;
            }

            return isValid;
        }

        private void UpdateConfigFromUi()
        {
            _config.BrowserUrl = BrowserUrlTextBox.Text;
            _config.IpPrefix = IpPrefixText.Text;
            _config.IpLastSegment = IpLastSegmentTextBox.Text;
            _config.Username = UsernameTextBox.Text;
            _config.Password = PasswordBox.Password;
            _config.CustomSubdomain = CustomSubdomainTextBox.Text;
            _config.IsEnabled = EnableFrpToggle.IsChecked == true;
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInputs())
            {
                return;
            }

            UpdateConfigFromUi();
            FrpService.Instance.SaveConfig(_config);
            MessageBox.Show("配置保存成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void StartStopButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isStarting)
            {
                return;
            }

            if (FrpService.Instance.IsRunning)
            {
                await FrpService.Instance.StopAsync();
            }
            else
            {
                if (!ValidateInputs())
                {
                    return;
                }

                UpdateConfigFromUi();
                _isStarting = true;
                StartStopButton.IsEnabled = false;

                try
                {
                    bool success = await FrpService.Instance.StartAsync(_config);
                    if (success)
                    {
                        MessageBox.Show("内网穿透服务启动成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("启动失败，请检查 frpc.exe 是否存在", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"启动失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    _isStarting = false;
                    StartStopButton.IsEnabled = true;
                }
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = e.Uri.ToString(),
                    UseShellExecute = true
                });
            }
            catch { }
            e.Handled = true;
        }
    }
}
