using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using LocalAIStudio.Services;

namespace LocalAIStudio.Views
{
    public partial class OllamaSetupPage : UserControl
    {
        public event EventHandler? SetupCompleted;

        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isOllamaInstalled = false;

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

            PlayCheckAnimation();
            SetupCompleted?.Invoke(this, EventArgs.Empty);
        }

        private void ShowInstallLinks()
        {
            StatusTitle.Text = "暂未安装 Ollama";
            StatusMessage.Text = "请安装 Ollama 以继续使用 Local AI Studio。";
            Spinner.Visibility = Visibility.Collapsed;
            CheckIcon.Visibility = Visibility.Collapsed;
            InstallLinksPanel.Visibility = Visibility.Visible;
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
                MessageBox.Show("Ollama 安装成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("安装已取消。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"安装失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                OneClickDeployButton.IsEnabled = true;
                ProgressPanel.Visibility = Visibility.Collapsed;
            }
            finally
            {
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private void CopyOfficialLink_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText("https://ollama.com/download");
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
            Clipboard.SetText("https://mirrors.aliyun.com/ollama/windows");
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

        public bool IsOllamaInstalled => _isOllamaInstalled;
    }
}
