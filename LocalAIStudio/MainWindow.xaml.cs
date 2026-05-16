using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Hardcodet.Wpf.TaskbarNotification;
using LocalAIStudio.Services;

namespace LocalAIStudio
{
    public partial class MainWindow : Window
    {
        private TaskbarIcon? _notifyIcon;
        private bool _isButtonMoved = false;
        private bool _ollamaInstalled = false;
        private CancellationTokenSource? _cancellationTokenSource;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _notifyIcon = (TaskbarIcon)FindResource("NotifyIcon");
            EnableBlurEffect();
            PlayEntryAnimation();
            CheckOllamaInstallation();
        }

        private async void CheckOllamaInstallation()
        {
            await Task.Delay(500);

            var isInstalled = OllamaService.IsInstalled();
            _ollamaInstalled = isInstalled;

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
            NextButton.IsEnabled = true;

            PlayCheckmarkAnimation();
        }

        private void ShowInstallLinks()
        {
            StatusTitle.Text = "暂未安装 Ollama";
            StatusMessage.Text = "请安装 Ollama 以继续使用 Local AI Studio。";
            Spinner.Visibility = Visibility.Collapsed;
            CheckIcon.Visibility = Visibility.Collapsed;
            InstallLinksPanel.Visibility = Visibility.Visible;
            NextButton.IsEnabled = false;
        }

        private void PlayCheckmarkAnimation()
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
            CheckIconContainer.RenderTransformOrigin = new Point(0.5, 0.5);

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
            if (_cancellationTokenSource != null) return;

            OneClickDeployButton.IsEnabled = false;
            ProgressPanel.Visibility = Visibility.Visible;
            InstallProgressBar.Value = 0;

            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                var progress = new Progress<int>(percent =>
                {
                    InstallProgressBar.Value = percent;
                });

                var status = new Progress<string>(msg =>
                {
                    ProgressText.Text = msg;
                });

                await OllamaService.DownloadAndInstallAsync(progress, status, _cancellationTokenSource.Token);

                await Task.Delay(500);
                _ollamaInstalled = true;
                ShowInstalledSuccess();
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
            ShowCopySuccess();
        }

        private void CopyAliyunLink_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText("https://mirrors.aliyun.com/ollama/windows");
            ShowCopySuccess();
        }

        private void ShowCopySuccess()
        {
            var originalButton = sender as Button;
            if (originalButton != null)
            {
                var originalContent = originalButton.Content;
                originalButton.Content = "已复制";
                Task.Delay(1500).ContinueWith(_ => Dispatcher.BeginInvoke(() =>
                {
                    originalButton.Content = originalContent;
                }));
            }
        }

        private void OfficialLink_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://ollama.com/download",
                UseShellExecute = true
            });
        }

        private void AliyunLink_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://mirrors.aliyun.com/ollama/windows",
                UseShellExecute = true
            });
        }

        private void EnableBlurEffect()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var accentPolicy = new AccentPolicy
            {
                AccentState = AccentState.ACCENT_ENABLE_BLURBEHIND,
                AccentFlags = AccentFlags.ACCENT_ENABLE_ALL,
                GradientColor = 0
            };
            var accentStructSize = Marshal.SizeOf(accentPolicy);
            var accentPtr = Marshal.AllocHGlobal(accentStructSize);
            Marshal.StructureToPtr(accentPolicy, accentPtr, false);

            var data = new WindowCompositionAttributeData
            {
                Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                SizeOfData = accentStructSize,
                Data = accentPtr
            };

            SetWindowCompositionAttribute(hwnd, ref data);
            Marshal.FreeHGlobal(accentPtr);
        }

        private void PlayEntryAnimation()
        {
            var duration = new Duration(TimeSpan.FromMilliseconds(600));
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            var opacityAnim = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = duration,
                EasingFunction = ease
            };

            var scaleAnim = new DoubleAnimation
            {
                From = 0.9,
                To = 1,
                Duration = duration,
                EasingFunction = ease
            };

            var scaleTransform = (ScaleTransform)MainGrid.RenderTransform;
            MainGrid.BeginAnimation(OpacityProperty, opacityAnim);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isButtonMoved && _ollamaInstalled)
            {
                AnimateButtonToBottomRight();
                _isButtonMoved = true;
            }
        }

        private void AnimateButtonToBottomRight()
        {
            var buttonTransform = (TranslateTransform)NextButton.RenderTransform;

            var containerWidth = ButtonContainer.ActualWidth;
            var buttonWidth = NextButton.ActualWidth;

            var dx = containerWidth / 2 - buttonWidth / 2 - 40;
            var dy = 0;

            var duration = new Duration(TimeSpan.FromMilliseconds(500));
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            var xAnim = new DoubleAnimation
            {
                From = 0,
                To = dx,
                Duration = duration,
                EasingFunction = ease
            };

            buttonTransform.BeginAnimation(TranslateTransform.XProperty, xAnim);
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void NotifyIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            Show();
            Activate();
        }

        private void ShowWindowMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Show();
            Activate();
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        #region Window Interop

        internal enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_GRADIENT = 1,
            ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
            ACCENT_ENABLE_BLURBEHIND = 3,
            ACCENT_ENABLE_ACRYLICBLURBEHIND = 4
        }

        [Flags]
        internal enum AccentFlags
        {
            ACCENT_NONE = 0,
            ACCENT_ENABLE_ALL = 3
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct AccentPolicy
        {
            public AccentState AccentState;
            public AccentFlags AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        internal enum WindowCompositionAttribute
        {
            WCA_ACCENT_POLICY = 19
        }

        [DllImport("user32.dll")]
        internal static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        #endregion
    }
}
