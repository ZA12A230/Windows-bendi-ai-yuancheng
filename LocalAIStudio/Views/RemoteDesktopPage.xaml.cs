using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using LocalAIStudio.Services;

namespace LocalAIStudio.Views
{
    public partial class RemoteDesktopPage : System.Windows.Controls.UserControl
    {
        private RemoteDesktopConfig _config = new RemoteDesktopConfig();
        private bool _isStarting = false;

        public RemoteDesktopPage()
        {
            InitializeComponent();
            Loaded += RemoteDesktopPage_Loaded;
            Unloaded += RemoteDesktopPage_Unloaded;
        }

        private void RemoteDesktopPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadConfig();
            UpdateStatusDisplay();
            RemoteDesktopService.Instance.StatusChanged += RemoteDesktopService_StatusChanged;
        }

        private void RemoteDesktopPage_Unloaded(object sender, RoutedEventArgs e)
        {
            RemoteDesktopService.Instance.StatusChanged -= RemoteDesktopService_StatusChanged;
        }

        private void LoadConfig()
        {
            _config = RemoteDesktopService.Instance.LoadConfig();

            EnableRdpToggle.IsChecked = _config.Enabled;
            UsernameTextBox.Text = _config.Username;
            PortTextBox.Text = _config.Port.ToString();
            EnableFrpToggle.IsChecked = _config.EnableFrpForward;

            // 加载 FRP 配置中的自定义域名
            var frpConfig = FrpService.Instance.LoadConfig();
            CustomDomainTextBox.Text = frpConfig.CustomSubdomain;

            UpdateFrpSectionVisibility();
        }

        private void SaveConfig()
        {
            _config.Enabled = EnableRdpToggle.IsChecked == true;
            _config.Username = UsernameTextBox.Text;
            _config.Password = PasswordBox.Password;
            _config.Port = int.TryParse(PortTextBox.Text, out int port) ? port : 5900;
            _config.EnableFrpForward = EnableFrpToggle.IsChecked == true;

            RemoteDesktopService.Instance.SaveConfig(_config);

            // 保存自定义域名到 FRP 配置
            if (_config.EnableFrpForward)
            {
                var frpConfig = FrpService.Instance.LoadConfig();
                frpConfig.CustomSubdomain = CustomDomainTextBox.Text;
                FrpService.Instance.SaveConfig(frpConfig);
            }
        }

        private void RemoteDesktopService_StatusChanged(object? sender, bool isRunning)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateStatusDisplay();
            });
        }

        private void UpdateStatusDisplay()
        {
            bool isRunning = RemoteDesktopService.Instance.IsRunning;

            if (isRunning)
            {
                StatusIndicator.Fill = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x10, 0xB9, 0x81));
                StatusText.Text = "服务运行中";
                StatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x10, 0xB9, 0x81));
                StartStopButton.Content = "停止服务";
                StartStopButton.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xEF, 0x44, 0x44));
                AccessInfoSection.Visibility = Visibility.Visible;
                UpdateAccessInfo();
            }
            else
            {
                StatusIndicator.Fill = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x94, 0xA3, 0xB8));
                StatusText.Text = "未启用";
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
            LocalAccessText.Text = $"【局域网】地址: {RemoteDesktopService.Instance.LocalIpAddress}:{_config.Port}";

            if (_config.EnableFrpForward && !string.IsNullOrEmpty(CustomDomainTextBox.Text))
            {
                RemoteAccessText.Text = $"【公网】地址: http://{CustomDomainTextBox.Text}.aiyuancheng.com:{_config.Port}";
                RemoteAccessText.Visibility = Visibility.Visible;
            }
            else
            {
                RemoteAccessText.Visibility = Visibility.Collapsed;
            }
        }

        private void EnableRdpToggle_Changed(object sender, RoutedEventArgs e)
        {
            UpdateFrpSectionVisibility();
        }

        private void EnableFrpToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (EnableFrpToggle.IsChecked == true && EnableRdpToggle.IsChecked == true)
            {
                CustomDomainSection.Visibility = Visibility.Visible;
            }
            else
            {
                CustomDomainSection.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateFrpSectionVisibility()
        {
            if (EnableRdpToggle.IsChecked == true && EnableFrpToggle.IsChecked == true)
            {
                CustomDomainSection.Visibility = Visibility.Visible;
            }
            else
            {
                CustomDomainSection.Visibility = Visibility.Collapsed;
            }
        }

        private bool ValidateInputs()
        {
            bool isValid = true;

            // 验证用户名
            if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
            {
                UsernameErrorText.Text = "请输入用户名";
                UsernameErrorText.Visibility = Visibility.Visible;
                isValid = false;
            }
            else if (UsernameTextBox.Text.Length < 3)
            {
                UsernameErrorText.Text = "用户名至少3个字符";
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
                PasswordErrorText.Text = "请输入密码";
                PasswordErrorText.Visibility = Visibility.Visible;
                isValid = false;
            }
            else if (password.Length < 6)
            {
                PasswordErrorText.Text = "密码至少6个字符";
                PasswordErrorText.Visibility = Visibility.Visible;
                isValid = false;
            }
            else
            {
                PasswordErrorText.Visibility = Visibility.Collapsed;
            }

            // 验证端口
            if (!int.TryParse(PortTextBox.Text, out int port) || port < 1 || port > 65535)
            {
                PortErrorText.Text = "端口范围 1-65535";
                PortErrorText.Visibility = Visibility.Visible;
                isValid = false;
            }
            else
            {
                PortErrorText.Visibility = Visibility.Collapsed;
            }

            return isValid;
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInputs())
                return;

            SaveConfig();
            MessageBox.Show("配置保存成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void StartStopButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isStarting)
                return;

            if (RemoteDesktopService.Instance.IsRunning)
            {
                RemoteDesktopService.Instance.StopService();
            }
            else
            {
                if (!ValidateInputs())
                    return;

                _isStarting = true;
                StartStopButton.IsEnabled = false;

                try
                {
                    SaveConfig();

                    bool success = RemoteDesktopService.Instance.StartService(
                        PasswordBox.Password,
                        int.Parse(PortTextBox.Text),
                        EnableFrpToggle.IsChecked == true);

                    if (success)
                    {
                        MessageBox.Show("远程桌面服务启动成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("启动失败，请检查配置。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private void ShowGuideButton_Click(object sender, RoutedEventArgs e)
        {
            string guide = RemoteDesktopService.Instance.GenerateConnectionGuide(
                _config.Username,
                PasswordBox.Password,
                _config.EnableFrpForward,
                CustomDomainTextBox.Text);

            MessageBox.Show(guide, "连接指南", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
