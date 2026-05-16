using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LocalAIAssistant.Services;

namespace LocalAIAssistant.Views.Pages
{
    public partial class RemoteDesktopPage : Page
    {
        private readonly RemoteDesktopService _remoteDesktopService;
        private bool _isServerRunning;

        public RemoteDesktopPage()
        {
            InitializeComponent();
            _remoteDesktopService = new RemoteDesktopService();
        }

        private async void StartServerButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StartServerButton.IsEnabled = false;
                var (address, code) = await _remoteDesktopService.StartServerAsync();

                RemoteAddressText.Text = address;
                ConnectionCodeText.Text = $"连接码: {code}";
                StopServerButton.IsEnabled = true;
                _isServerRunning = true;

                ConnectionStatusText.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                ConnectionStatusText.Text = "服务已启动，等待连接...";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动服务失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                StartServerButton.IsEnabled = true;
            }
        }

        private async void StopServerButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _remoteDesktopService.StopServerAsync();
                RemoteAddressText.Text = "未启动";
                ConnectionCodeText.Text = "连接码: -";
                StartServerButton.IsEnabled = true;
                StopServerButton.IsEnabled = false;
                _isServerRunning = false;

                ConnectionStatusText.Foreground = new SolidColorBrush(Color.FromRgb(161, 161, 170));
                ConnectionStatusText.Text = "未连接";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"停止服务失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            var remoteHost = RemoteHostInput.Text.Trim();
            if (string.IsNullOrEmpty(remoteHost))
            {
                MessageBox.Show("请输入远程地址或连接码", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                ConnectButton.IsEnabled = false;
                ConnectionStatusText.Text = "正在连接...";

                var success = await _remoteDesktopService.ConnectAsync(remoteHost);
                if (success)
                {
                    ConnectionStatusText.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                    ConnectionStatusText.Text = "已连接";
                    RemoteVideoControl.Visibility = Visibility.Visible;
                }
                else
                {
                    ConnectionStatusText.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    ConnectionStatusText.Text = "连接失败";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"连接失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                ConnectionStatusText.Text = "连接失败";
            }
            finally
            {
                ConnectButton.IsEnabled = true;
            }
        }
    }
}
