using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Hardcodet.Wpf.TaskbarNotification;

namespace LocalAIStudio
{
    public partial class MainWindow : Window
    {
        private TaskbarIcon? _notifyIcon;
        private bool _isButtonMoved = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _notifyIcon = (TaskbarIcon)FindResource("NotifyIcon");
            EnableBlurEffect();
            PlayEntryAnimation();
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
            if (!_isButtonMoved)
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
