using System;
using System.Windows;
using System.Windows.Threading;
using LocalAIStudio.Services;

namespace LocalAIStudio
{
    public partial class App : System.Windows.Application
    {
        private bool _servicesInitialized = false;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 先确保主窗口加载完成后再初始化服务
            Dispatcher.BeginInvoke(new Action(InitializeApp), DispatcherPriority.Loaded);
        }

        private void InitializeApp()
        {
            try
            {
                // 确保主窗口已显示
                if (MainWindow == null)
                {
                    var window = new MainWindow();
                    window.Show();
                }
                else
                {
                    MainWindow.Show();
                }

                // 延迟初始化服务，避免阻塞UI
                Dispatcher.BeginInvoke(new Action(InitializeServices), DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"启动初始化失败: {ex.Message}");
                // 即使初始化服务失败，也要保持窗口打开
            }
        }

        private void InitializeServices()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("正在初始化服务...");

                // 安全地初始化各个服务
                try
                {
                    if (SettingsService.Instance.AdaptiveModeEnabled)
                    {
                        WatchdogService.Instance.StartAdaptiveMode(
                            SettingsService.Instance.SystemCpuThreshold,
                            SettingsService.Instance.AiCpuThreshold);
                        System.Diagnostics.Debug.WriteLine("自适应模式已启动");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"初始化自适应模式失败: {ex.Message}");
                }

                try
                {
                    PrivacyAlertService.Instance.StartMonitoring();
                    System.Diagnostics.Debug.WriteLine("隐私监控已启动");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"初始化隐私监控失败: {ex.Message}");
                }

                try
                {
                    WatchdogService.Instance.StartMonitoring();
                    System.Diagnostics.Debug.WriteLine("进程守护已启动");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"初始化进程守护失败: {ex.Message}");
                }

                CheckSilentStart();
                _servicesInitialized = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"服务初始化失败: {ex.Message}");
            }
        }

        private void CheckSilentStart()
        {
            try
            {
                if (WatchdogService.Instance.ShouldStartSilently())
                {
                    System.Diagnostics.Debug.WriteLine("静默启动模式");

                    if (MainWindow != null)
                    {
                        MainWindow.WindowState = WindowState.Minimized;
                        MainWindow.ShowInTaskbar = false;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"检查静默启动失败: {ex.Message}");
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                if (_servicesInitialized)
                {
                    WatchdogService.Instance.StopMonitoring();
                    WatchdogService.Instance.StopAdaptiveMode();
                    PrivacyAlertService.Instance.StopMonitoring();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"退出时清理失败: {ex.Message}");
            }

            base.OnExit(e);
        }
    }
}
