using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LocalAIStudio.Services;

namespace LocalAIStudio.Views
{
    public partial class OllamaSetupPage : System.Windows.Controls.UserControl
    {
        public event EventHandler SetupCompleted;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isOllamaInstalled = false;
        private MirrorSource _selectedMirror;

        public OllamaSetupPage()
        {
            InitializeComponent();
            Loaded += OllamaSetupPage_Loaded;
        }

        private async void OllamaSetupPage_Loaded(object sender, RoutedEventArgs e)
        {
            await System.Threading.Tasks.Task.Delay(500);
            CheckOllamaInstallation();
        }

        private void CheckOllamaInstallation()
        {
            var isInstalled = OllamaService.IsInstalled();
            _isOllamaInstalled = isInstalled;

            if (isInstalled)
            {
                ShowInstalledSuccess();
            }
            else
            {
                ShowInstallLinks();
            }
        }

        private void ShowInstalledSuccess()
        {
            StatusTitle.Text = "已检测到 Ollama";
            StatusMessage.Text = "Ollama 已成功安装在您的系统上。";
            Spinner.Visibility = Visibility.Collapsed;
            CheckIcon.Visibility = Visibility.Visible;
            InstallLinksPanel.Visibility = Visibility.Collapsed;
            MirrorConfigPanel.Visibility = Visibility.Visible;

            LoadMirrorSources();
            PlayCheckAnimation();
        }

        private void ShowInstallLinks()
        {
            StatusTitle.Text = "暂未安装 Ollama";
            StatusMessage.Text = "请安装 Ollama 以继续使用 Local AI Studio。";
            Spinner.Visibility = Visibility.Collapsed;
            CheckIcon.Visibility = Visibility.Collapsed;
            InstallLinksPanel.Visibility = Visibility.Visible;
            MirrorConfigPanel.Visibility = Visibility.Collapsed;
        }

        private void LoadMirrorSources()
        {
            var mirrors = OllamaService.GetAvailableMirrorSources();
            MirrorSourceComboBox.ItemsSource = mirrors;
            var defaultMirror = mirrors.Find(m => m.IsDefault);
            if (defaultMirror != null)
            {
                MirrorSourceComboBox.SelectedItem = defaultMirror;
            }
            ConfigPathText.Text = $"配置文件位置: {OllamaService.GetOllamaConfigPath()}";
        }

        private void PlayCheckAnimation()
        {
            var duration = new Duration(TimeSpan.FromMilliseconds(600));
            var bounceEase = new BounceEase
            {
                Bounces = 2,
                Bounciness = 5,
                EasingMode = EasingMode.EaseOut
            };

            var scaleTransform = new ScaleTransform(0.1, 0.1);
            CheckIconContainer.RenderTransform = scaleTransform;
            CheckIconContainer.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);

            var scaleAnim = new DoubleAnimation
            {
                From = 0.1,
                To = 1,
                Duration = duration,
                EasingFunction = bounceEase
            };

            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        }

        private async void OneClickDeploy_Click(object sender, RoutedEventArgs e)
        {
            if (_cancellationTokenSource != null)
                return;

            OneClickDeployButton.IsEnabled = false;
            ProgressPanel.Visibility = Visibility.Visible;
            InstallProgressBar.Value = 0;

            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                var progress = new Progress<int>(pct =>
                {
                    InstallProgressBar.Value = pct;
                });

                var status = new Progress<string>(msg =>
                {
                    ProgressText.Text = msg;
                });

                await OllamaService.DownloadAndInstallAsync(progress, status, _cancellationTokenSource.Token);
                await System.Threading.Tasks.Task.Delay(500);
                CheckOllamaInstallation();
                System.Windows.MessageBox.Show("Ollama 安装成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                System.Windows.MessageBox.Show("安装已取消。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"安装失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                OneClickDeployButton.IsEnabled = true;
                ProgressPanel.Visibility = Visibility.Collapsed;
            }
            finally
            {
                if (_cancellationTokenSource != null)
                    _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private void CopyOfficialLink_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Clipboard.SetText("https://ollama.com/download");
            var originalButton = sender as Button;
            if (originalButton != null)
            {
                var originalContent = originalButton.Content;
                originalButton.Content = "已复制";
                System.Threading.Tasks.Task.Delay(1500).ContinueWith(_ =>
                {
                    Dispatcher.Invoke(() => originalButton.Content = originalContent);
                });
            }
        }

        private void CopyAliyunLink_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Clipboard.SetText("https://mirrors.aliyun.com/ollama/windows");
            var originalButton = sender as Button;
            if (originalButton != null)
            {
                var originalContent = originalButton.Content;
                originalButton.Content = "已复制";
                System.Threading.Tasks.Task.Delay(1500).ContinueWith(_ =>
                {
                    Dispatcher.Invoke(() => originalButton.Content = originalContent);
                });
            }
        }

        private void OfficialLink_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://ollama.com/download",
                UseShellExecute = true
            });
        }

        private void AliyunLink_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://mirrors.aliyun.com/ollama/windows",
                UseShellExecute = true
            });
        }

        private void MirrorSourceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedMirror = MirrorSourceComboBox.SelectedItem as MirrorSource;
        }

        private async void TestMirrorButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedMirror == null || _selectedMirror.Url == "auto")
            {
                MirrorStatusText.Text = "请选择一个有效的镜像源进行测试";
                return;
            }

            TestMirrorButton.IsEnabled = false;
            MirrorStatusText.Text = "正在测试连接...";

            try
            {
                var isAvailable = await OllamaService.TestMirrorConnectivityAsync(_selectedMirror.Url);
                if (isAvailable)
                {
                    MirrorStatusText.Text = $"✓ {_selectedMirror.Name} 连接成功！";
                    MirrorStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x10, 0xB9, 0x81));
                }
                else
                {
                    MirrorStatusText.Text = $"✗ {_selectedMirror.Name} 连接失败";
                    MirrorStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xEF, 0x44, 0x44));
                }
            }
            catch (Exception ex)
            {
                MirrorStatusText.Text = $"测试失败: {ex.Message}";
                MirrorStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xEF, 0x44, 0x44));
            }
            finally
            {
                TestMirrorButton.IsEnabled = true;
            }
        }

        private async void AutoSelectButton_Click(object sender, RoutedEventArgs e)
        {
            AutoSelectButton.IsEnabled = false;
            MirrorStatusText.Text = "正在自动选择最佳镜像...";

            try
            {
                var bestMirror = await OllamaService.AutoSelectBestMirrorAsync();
                if (bestMirror != null)
                {
                    MirrorSourceComboBox.SelectedItem = bestMirror;
                    MirrorStatusText.Text = $"✓ 已自动选择最佳镜像: {bestMirror.Name}";
                    MirrorStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x10, 0xB9, 0x81));
                }
                else
                {
                    MirrorStatusText.Text = "无法找到可用镜像源，请手动选择";
                    MirrorStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xEF, 0x44, 0x44));
                }
            }
            catch (Exception ex)
            {
                MirrorStatusText.Text = $"自动选择失败: {ex.Message}";
                MirrorStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xEF, 0x44, 0x44));
            }
            finally
            {
                AutoSelectButton.IsEnabled = true;
            }
        }

        private void ApplyMirrorButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedMirror == null)
            {
                System.Windows.MessageBox.Show("请先选择一个镜像源", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ApplyMirrorButton.IsEnabled = false;

            try
            {
                bool isAuto = _selectedMirror.Url == "auto";
                bool success = OllamaService.SetMirrorSource(_selectedMirror.Url, isAuto);

                if (success)
                {
                    System.Windows.MessageBox.Show($"镜像源配置成功！\n\n当前镜像: {_selectedMirror.Name}\n\n建议重启 Ollama 服务以确保配置生效。",
                        "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    System.Windows.MessageBox.Show("镜像源配置失败，请检查权限", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"配置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ApplyMirrorButton.IsEnabled = true;
            }
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            SetupCompleted.Invoke(this, EventArgs.Empty);
        }

        public bool IsOllamaInstalled => _isOllamaInstalled;
    }
}
