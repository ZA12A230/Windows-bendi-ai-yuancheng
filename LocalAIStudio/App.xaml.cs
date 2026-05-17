using System;
using System.Windows;
using LocalAIStudio.Services;

namespace LocalAIStudio
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                InitializeServices();
                CheckSilentStart();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }
        }

        private void InitializeServices()
        {
            System.Diagnostics.Debug.WriteLine("正在初始化服务...");

            if (SettingsService.Instance.AdaptiveModeEnabled)
            {
                WatchdogService.Instance.StartAdaptiveMode(
                    SettingsService.Instance.SystemCpuThreshold,
                    SettingsService.Instance.AiCpuThreshold);
                System.Diagnostics.Debug.WriteLine("自适应模式已启动");
            }

            PrivacyAlertService.Instance.StartMonitoring();
            System.Diagnostics.Debug.WriteLine("隐私监控已启动");

            WatchdogService.Instance.StartMonitoring();
            System.Diagnostics.Debug.WriteLine("进程守护已启动");
        }

        private void CheckSilentStart()
        {
            if (WatchdogService.Instance.ShouldStartSilently())
            {
                System.Diagnostics.Debug.WriteLine("静默启动模式");

                if (MainWindow != null)
                {
                    MainWindow.WindowState = WindowState.Minimized;
                    MainWindow.ShowInTaskbar = false;
                    MainWindow.Hide();
                }
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                WatchdogService.Instance.StopMonitoring();
                WatchdogService.Instance.StopAdaptiveMode();
                PrivacyAlertService.Instance.StopMonitoring();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"退出时清理失败: {ex.Message}");
            }

            base.OnExit(e);
        }
    }
}
