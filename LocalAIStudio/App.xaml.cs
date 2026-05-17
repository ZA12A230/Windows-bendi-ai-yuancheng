using System;
using System.Windows;
using LocalAIStudio.Services;

namespace LocalAIStudio
{
    public partial class App : System.Windows.Application
    {
        private bool _mainWindowShown = false;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // 先显示主窗口，确保用户能看到程序
                ShowMainWindowFirst();
                
                InitializeServices();
                
                // 延迟检查静默启动，确保 MainWindow 已完全初始化
                Dispatcher.BeginInvoke(new Action(CheckSilentStart), 
                    System.Windows.Threading.DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"启动失败: {ex.Message}\n\n{ex.StackTrace}", 
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }
        }

        private void ShowMainWindowFirst()
        {
            System.Diagnostics.Debug.WriteLine("正在显示主窗口...");
            
            if (MainWindow == null)
            {
                var mainWindow = new MainWindow();
                MainWindow = mainWindow;
                mainWindow.Show();
                mainWindow.Activate();
                mainWindow.Focus();
                _mainWindowShown = true;
                System.Diagnostics.Debug.WriteLine("主窗口已显示");
            }
        }

        private void InitializeServices()
        {
            System.Diagnostics.Debug.WriteLine("正在初始化服务...");

            try
            {
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"服务初始化警告: {ex.Message}");
                // 服务初始化失败不应阻止窗口显示
            }
        }

        private void CheckSilentStart()
        {
            try
            {
                // 默认不启用静默启动，确保窗口能正常显示
                bool shouldStartSilently = false;
                try
                {
                    shouldStartSilently = WatchdogService.Instance.ShouldStartSilently();
                }
                catch
                {
                    shouldStartSilently = false;
                }

                if (shouldStartSilently)
                {
                    System.Diagnostics.Debug.WriteLine("静默启动模式");

                    if (MainWindow != null && _mainWindowShown)
                    {
                        // 确认用户要静默启动再隐藏
                        var result = System.Windows.MessageBox.Show(
                            "检测到静默启动设置。是否要隐藏窗口？\n\n选择'否'将保持窗口可见。",
                            "启动选项",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);
                        
                        if (result == MessageBoxResult.Yes)
                        {
                            MainWindow.WindowState = WindowState.Minimized;
                            MainWindow.ShowInTaskbar = false;
                            MainWindow.Hide();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"静默启动检查失败: {ex.Message}");
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
