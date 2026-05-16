using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using Hardcodet.Wpf.TaskbarNotification;
using LocalAIStudio.Views;
using LocalAIStudio.Services;

namespace LocalAIStudio
{
    public partial class MainWindow : Window
    {
        private TaskbarIcon? _notifyIcon;
        private OllamaSetupPage? _ollamaSetupPage;
        private ModelsPage? _modelsPage;
        private FrpConfigPage? _frpConfigPage;
        private RemoteDesktopPage? _remoteDesktopPage;
        private SettingsPage? _settingsPage;
        private int _currentPage = 0;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _notifyIcon = (TaskbarIcon)FindResource("NotifyIcon");
            EnableBlurEffect();
            PlayEntryAnimation();

            // 检查静默启动设置
            if (SettingsService.Instance.SilentStartEnabled)
            {
                Hide();
            }

            NavigateToPage(0);

            // 启动自适应模式如果已开启
            if (SettingsService.Instance.AdaptiveModeEnabled)
            {
                SettingsService.Instance.AdaptiveModeEnabled = true;
            }
        }

        private void NavigateToPage(int pageIndex)
        {
            _currentPage = pageIndex;
            UpdateNavigationButtons(pageIndex);

            switch (pageIndex)
            {
                case 0: // 首页
                    if (_ollamaSetupPage == null)
                    {
                        _ollamaSetupPage = new OllamaSetupPage();
                        _ollamaSetupPage.SetupCompleted += OllamaSetupPage_SetupCompleted;
                    }
                    PageContent.Content = _ollamaSetupPage;
                    break;

                case 1: // 模型管理
                    if (_modelsPage == null)
                    {
                        _modelsPage = new ModelsPage();
                    }
                    PageContent.Content = _modelsPage;
                    break;

                case 2: // 内网穿透
                    if (_frpConfigPage == null)
                    {
                        _frpConfigPage = new FrpConfigPage();
                    }
                    PageContent.Content = _frpConfigPage;
                    break;

                case 3: // 远程桌面
                    if (_remoteDesktopPage == null)
                    {
                        _remoteDesktopPage = new RemoteDesktopPage();
                    }
                    PageContent.Content = _remoteDesktopPage;
                    break;

                case 4: // 设置
                    if (_settingsPage == null)
                    {
                        _settingsPage = new SettingsPage();
                    }
                    PageContent.Content = _settingsPage;
                    break;
            }
        }

        private void UpdateNavigationButtons(int activeIndex)
        {
            var buttons = new Button[] { HomeNavButton, ModelsNavButton, FrpNavButton, RemoteDesktopNavButton, SettingsNavButton };
            for (int i = 0; i < buttons.Length; i++)
            {
                var border = (Border)buttons[i].Template.FindName("Border", buttons[i]);
                if (border != null)
                {
                    if (i == activeIndex)
                    {
                        border.Background = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(0xE8, 0xF0, 0xFE));
                        buttons[i].Foreground = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(0x3B, 0x82, 0xF6));
                    }
                    else
                    {
                        border.Background = System.Windows.Media.Brushes.Transparent;
                        buttons[i].Foreground = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(0x64, 0x74, 0x8B));
                    }
                }
            }
        }

        private void HomeNavButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(0);
        }

        private void ModelsNavButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(1);
        }

        private void FrpNavButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(2);
        }

        private void RemoteDesktopNavButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(3);
        }

        private void SettingsNavButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(4);
        }

        private void OllamaSetupPage_SetupCompleted(object? sender, EventArgs e)
        {
            // 可以在完成时做一些处理
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
