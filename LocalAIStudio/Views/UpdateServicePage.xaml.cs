using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LocalAIStudio.Services;

namespace LocalAIStudio.Views
{
    public partial class UpdateServicePage : UserControl
    {
        private ObservableCollection<ProcessInfo> _monitoredProcesses = new ObservableCollection<ProcessInfo>();
        private string? _pendingUpdatePath;

        public UpdateServicePage()
        {
            InitializeComponent();
            Loaded += UpdateServicePage_Loaded;
        }

        private void UpdateServicePage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            LoadMonitoredProcesses();
            InitializeWatchdog();
        }

        private void LoadSettings()
        {
            WatchdogToggle.IsChecked = WatchdogService.Instance.IsRunning;
            AdaptiveModeToggle.IsChecked = WatchdogService.Instance.AdaptiveModeEnabled;
            SilentStartToggle.IsChecked = WatchdogService.Instance.ShouldStartSilently();

            var currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            CurrentVersionText.Text = $"v{currentVersion?.Major}.{currentVersion?.Minor}.{currentVersion?.Build}";
        }

        private void LoadMonitoredProcesses()
        {
            _monitoredProcesses.Clear();

            RegisterDefaultProcesses();

            foreach (var kvp in WatchdogService.Instance.GetType()
                .GetField("_monitoredProcesses", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(WatchdogService.Instance) as System.Collections.Generic.Dictionary<string, ProcessInfo>)
            {
                _monitoredProcesses.Add(kvp.Value);
            }

            MonitoredProcessesList.ItemsSource = _monitoredProcesses;
        }

        private void RegisterDefaultProcesses()
        {
            var ollamaPath = OllamaService.Instance.GetOllamaPath();
            if (!string.IsNullOrEmpty(ollamaPath))
            {
                WatchdogService.Instance.RegisterProcess("ollama", ollamaPath);
            }

            var frpcPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "frpc.exe");
            if (File.Exists(frpcPath))
            {
                WatchdogService.Instance.RegisterProcess("frpc", frpcPath);
            }

            var winvncPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                "LocalAIStudio", "winvnc4.exe");
            if (File.Exists(winvncPath))
            {
                WatchdogService.Instance.RegisterProcess("winvnc", winvncPath);
            }
        }

        private void InitializeWatchdog()
        {
            WatchdogService.Instance.ProcessRestarted += (s, name) =>
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateStatusText.Text = $"[{DateTime.Now:HH:mm:ss}] {name} 已重启";
                    LoadMonitoredProcesses();
                });
            };

            WatchdogService.Instance.ProcessCrashed += (s, name) =>
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateStatusText.Text = $"[{DateTime.Now:HH:mm:ss}] {name} 意外停止，正在重启...";
                });
            };

            WatchdogService.Instance.PerformanceAlertRaised += (s, alert) =>
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateStatusText.Text = $"[{DateTime.Now:HH:mm:ss}] {alert.Message}";
                });
            };
        }

        private void WatchdogToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (WatchdogToggle.IsChecked == true)
            {
                WatchdogService.Instance.StartMonitoring();
                WatchdogStatusText.Text = "运行中";
                WatchdogStatusText.Foreground = (System.Windows.Media.SolidColorBrush)FindResource("SuccessGreen");
            }
            else
            {
                WatchdogService.Instance.StopMonitoring();
                WatchdogStatusText.Text = "已停止";
                WatchdogStatusText.Foreground = (System.Windows.Media.SolidColorBrush)FindResource("TextSecondaryBrush");
            }
        }

        private void AdaptiveModeToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (AdaptiveModeToggle.IsChecked == true)
            {
                int systemThreshold = 80;
                int aiThreshold = 50;

                if (int.TryParse(SystemThresholdText.Text, out int sys))
                    systemThreshold = sys;
                if (int.TryParse(AiThresholdText.Text, out int ai))
                    aiThreshold = ai;

                WatchdogService.Instance.StartAdaptiveMode(systemThreshold, aiThreshold);
            }
            else
            {
                WatchdogService.Instance.StopAdaptiveMode();
            }
        }

        private void SilentStartToggle_Changed(object sender, RoutedEventArgs e)
        {
            WatchdogService.Instance.SetSilentStart(SilentStartToggle.IsChecked == true);
        }

        private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            UpdateStatusText.Text = "正在检查更新...";

            try
            {
                var updateInfo = await WatchdogService.Instance.CheckForUpdatesAsync();

                if (updateInfo != null)
                {
                    UpdateAvailableBorder.Visibility = Visibility.Visible;
                    NewVersionText.Text = $"v{updateInfo.LatestVersion}";
                    ReleaseNotesText.Text = updateInfo.ReleaseNotes ?? "暂无更新说明";
                    UpdateStatusText.Text = $"发现新版本: v{updateInfo.LatestVersion}";
                }
                else
                {
                    UpdateAvailableBorder.Visibility = Visibility.Collapsed;
                    UpdateStatusText.Text = "当前已是最新版本";
                    MessageBox.Show("当前已是最新版本", "检查更新", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                UpdateStatusText.Text = $"检查更新失败: {ex.Message}";
                MessageBox.Show($"检查更新失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void DownloadUpdate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var updateInfo = await WatchdogService.Instance.CheckForUpdatesAsync();
                
                if (updateInfo == null || string.IsNullOrEmpty(updateInfo.DownloadUrl))
                {
                    MessageBox.Show("无法获取更新链接", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                DownloadProgress.Visibility = Visibility.Visible;
                DownloadUpdateButton.IsEnabled = false;
                UpdateStatusText.Text = "正在下载更新...";

                var progress = new Progress<double>(p =>
                {
                    DownloadProgress.Value = p;
                    UpdateStatusText.Text = $"下载进度: {p:F1}%";
                });

                _pendingUpdatePath = await WatchdogService.Instance.DownloadUpdateAsync(updateInfo.DownloadUrl, progress);

                if (!string.IsNullOrEmpty(_pendingUpdatePath))
                {
                    UpdateStatusText.Text = "下载完成，正在安装更新...";
                    
                    var result = MessageBox.Show(
                        "更新已下载完成。是否立即重启并安装更新？\n\n注意：安装过程中程序将短暂退出。",
                        "安装更新",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        await WatchdogService.Instance.ApplyUpdateAsync(_pendingUpdatePath);
                    }
                    else
                    {
                        UpdateStatusText.Text = "更新已保存，稍后将在下次启动时安装";
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateStatusText.Text = $"下载失败: {ex.Message}";
                MessageBox.Show($"下载失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                DownloadProgress.Visibility = Visibility.Collapsed;
                DownloadUpdateButton.IsEnabled = true;
            }
        }

        private void RefreshProcesses_Click(object sender, RoutedEventArgs e)
        {
            LoadMonitoredProcesses();
            UpdateStatusText.Text = "进程列表已刷新";
        }
    }
}
