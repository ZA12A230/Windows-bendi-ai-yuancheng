using System.Windows;
using System.Windows.Controls;
using LocalAIStudio.Services;

namespace LocalAIStudio.Views
{
    public partial class SettingsPage : System.Windows.Controls.UserControl
    {
        public SettingsPage()
        {
            InitializeComponent();
            Loaded += SettingsPage_Loaded;
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadCurrentSettings();
        }

        private void LoadCurrentSettings()
        {
            var service = SettingsService.Instance;

            AutoStartToggle.IsChecked = service.AutoStartEnabled;
            AdminModeToggle.IsChecked = service.AdminModeEnabled;
            ScreenOffToggle.IsChecked = service.ScreenOffReplacementEnabled;
            SilentStartToggle.IsChecked = service.SilentStartEnabled;
            AdaptiveModeToggle.IsChecked = service.AdaptiveModeEnabled;

            CpuThresholdBox.Text = service.SystemCpuThreshold.ToString();
            AiThresholdBox.Text = service.AiCpuThreshold.ToString();

            AdaptiveConfigBorder.Visibility = service.AdaptiveModeEnabled ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ToggleButton_Changed(object sender, RoutedEventArgs e)
        {
            if (AdaptiveModeToggle.IsChecked == true)
            {
                AdaptiveConfigBorder.Visibility = Visibility.Visible;
            }
            else if (AdaptiveModeToggle.IsChecked == false)
            {
                AdaptiveConfigBorder.Visibility = Visibility.Collapsed;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var service = SettingsService.Instance;

                // 保存开关状态
                if (AutoStartToggle.IsChecked.HasValue)
                    service.AutoStartEnabled = AutoStartToggle.IsChecked.Value;

                if (AdminModeToggle.IsChecked.HasValue)
                    service.AdminModeEnabled = AdminModeToggle.IsChecked.Value;

                if (ScreenOffToggle.IsChecked.HasValue)
                    service.ScreenOffReplacementEnabled = ScreenOffToggle.IsChecked.Value;

                if (SilentStartToggle.IsChecked.HasValue)
                    service.SilentStartEnabled = SilentStartToggle.IsChecked.Value;

                if (AdaptiveModeToggle.IsChecked.HasValue)
                    service.AdaptiveModeEnabled = AdaptiveModeToggle.IsChecked.Value;

                // 保存阈值配置
                if (int.TryParse(CpuThresholdBox.Text, out int cpuThreshold))
                    service.SystemCpuThreshold = cpuThreshold;

                if (int.TryParse(AiThresholdBox.Text, out int aiThreshold))
                    service.AiCpuThreshold = aiThreshold;

                MessageBox.Show("设置已保存！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存设置时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
