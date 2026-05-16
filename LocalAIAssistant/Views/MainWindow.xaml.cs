using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Hardcodet.Wpf.TaskbarNotification;
using LocalAIAssistant.Views.Pages;

namespace LocalAIAssistant.Views
{
    public partial class MainWindow : Window
    {
        private TaskbarIcon? _notifyIcon;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _notifyIcon = (TaskbarIcon)FindResource("NotifyIcon");
            EnableBlurEffect();
            NavigateToPage("Dashboard");
        }

        private void EnableBlurEffect()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var accentPolicy = new AccentPolicy
            {
                AccentState = AccentState.ACCENT_ENABLE_BLURBEHIND,
                AccentFlags = AccentFlags.ACCENT_ENABLE_ALL
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

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowBorder.CornerRadius = new CornerRadius(0);
            }
            else
            {
                WindowBorder.CornerRadius = new CornerRadius(20);
            }
        }

        private void NavButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.RadioButton radio && radio.Tag is string pageName)
            {
                NavigateToPage(pageName);
            }
        }

        public void NavigateToPage(string pageName)
        {
            ContentFrame.Content = pageName switch
            {
                "Dashboard" => new DashboardPage(),
                "AIChat" => new AIChatPage(),
                "Models" => new ModelsPage(),
                "Hardware" => new HardwarePage(),
                "RemoteDesktop" => new RemoteDesktopPage(),
                "Camera" => new CameraPage(),
                "Tunnel" => new TunnelPage(),
                "WebServer" => new WebServerPage(),
                "Settings" => new SettingsPage(),
                _ => new DashboardPage()
            };
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

        private void OpenSettingsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Show();
            Activate();
            NavigateToPage("Settings");
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
