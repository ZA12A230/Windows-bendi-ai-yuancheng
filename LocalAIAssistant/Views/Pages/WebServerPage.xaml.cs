using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LocalAIAssistant.Models;
using LocalAIAssistant.Services;

namespace LocalAIAssistant.Views.Pages
{
    public partial class WebServerPage : Page
    {
        private readonly WebServerService _webServerService;
        private readonly ObservableCollection<AccessLogEntry> _accessLogs = new();
        private bool _isServerRunning;

        public WebServerPage()
        {
            InitializeComponent();
            _webServerService = new WebServerService();
            AccessLogDataGrid.ItemsSource = _accessLogs;

            _webServerService.OnAccessLog += (sender, log) =>
            {
                Dispatcher.Invoke(() =>
                {
                    _accessLogs.Insert(0, log);
                    if (_accessLogs.Count > 100)
                    {
                        _accessLogs.RemoveAt(_accessLogs.Count - 1);
                    }
                });
            };
        }

        private async void StartServerButton_Click(object sender, RoutedEventArgs e)
        {
            var port = PortInput.Text.Trim();
            var rootPath = RootPathInput.Text.Trim();
            var defaultPage = DefaultPageInput.Text.Trim();
            var enableDirectoryBrowsing = EnableDirectoryBrowsingCheckBox.IsChecked ?? false;

            if (string.IsNullOrEmpty(port))
            {
                MessageBox.Show("请输入监听端口", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                StartServerButton.IsEnabled = false;

                var config = new WebServerConfig
                {
                    Port = int.Parse(port),
                    RootPath = rootPath,
                    DefaultPage = defaultPage,
                    EnableDirectoryBrowsing = enableDirectoryBrowsing
                };

                await _webServerService.StartAsync(config);

                ServerStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                ServerStatusText.Text = "运行中";
                ServerUrlText.Text = $"访问地址: http://localhost:{port}";
                StopServerButton.IsEnabled = true;
                _isServerRunning = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                StartServerButton.IsEnabled = true;
            }
        }

        private async void StopServerButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _webServerService.StopAsync();
                ServerStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                ServerStatusText.Text = "未启动";
                ServerUrlText.Text = "访问地址: -";
                StartServerButton.IsEnabled = true;
                StopServerButton.IsEnabled = false;
                _isServerRunning = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"停止失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择网站根目录"
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                RootPathInput.Text = dialog.SelectedPath;
            }
        }
    }
}
