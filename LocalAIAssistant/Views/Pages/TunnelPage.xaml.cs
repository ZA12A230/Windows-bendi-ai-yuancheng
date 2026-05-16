using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LocalAIAssistant.Services;

namespace LocalAIAssistant.Views.Pages
{
    public partial class TunnelPage : Page
    {
        private readonly TunnelService _tunnelService;
        private bool _isTunnelRunning;

        public TunnelPage()
        {
            InitializeComponent();
            _tunnelService = new TunnelService();
        }

        private async void StartTunnelButton_Click(object sender, RoutedEventArgs e)
        {
            var serverAddress = ServerAddressInput.Text.Trim();
            var serverPort = ServerPortInput.Text.Trim();
            var localPort = LocalPortInput.Text.Trim();
            var remotePort = RemotePortInput.Text.Trim();
            var subdomain = SubdomainInput.Text.Trim();
            var authToken = AuthTokenInput.Text.Trim();

            if (string.IsNullOrEmpty(serverAddress) || string.IsNullOrEmpty(serverPort) || string.IsNullOrEmpty(localPort))
            {
                MessageBox.Show("请填写必要的服务器配置", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                StartTunnelButton.IsEnabled = false;
                TunnelStatusText.Text = "正在连接...";

                var config = new TunnelConfig
                {
                    ServerAddress = serverAddress,
                    ServerPort = int.Parse(serverPort),
                    LocalPort = int.Parse(localPort),
                    RemotePort = string.IsNullOrEmpty(remotePort) ? 0 : int.Parse(remotePort),
                    Subdomain = subdomain,
                    AuthToken = authToken
                };

                var publicUrl = await _tunnelService.StartTunnelAsync(config);

                TunnelStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                TunnelStatusText.Text = "已连接";
                PublicUrlText.Text = publicUrl;
                StopTunnelButton.IsEnabled = true;
                _isTunnelRunning = true;

                MessageBox.Show($"内网穿透已启动\n公网地址: {publicUrl}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                TunnelStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                TunnelStatusText.Text = "连接失败";
                StartTunnelButton.IsEnabled = true;
            }
        }

        private async void StopTunnelButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _tunnelService.StopTunnelAsync();
                TunnelStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                TunnelStatusText.Text = "未连接";
                PublicUrlText.Text = "-";
                StartTunnelButton.IsEnabled = true;
                StopTunnelButton.IsEnabled = false;
                _isTunnelRunning = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"停止失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CopyUrlButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(PublicUrlText.Text) && PublicUrlText.Text != "-")
            {
                Clipboard.SetText(PublicUrlText.Text);
                MessageBox.Show("已复制到剪贴板", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
