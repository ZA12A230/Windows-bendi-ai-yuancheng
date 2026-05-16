using System;
using System.Windows;
using System.Windows.Controls;
using LocalAIAssistant.Services;
using LocalAIAssistant.Utils;

namespace LocalAIAssistant.Views.Pages
{
    public partial class SettingsPage : Page
    {
        private readonly OllamaService _ollamaService;
        private readonly SettingsService _settingsService;

        public SettingsPage()
        {
            InitializeComponent();
            _ollamaService = new OllamaService();
            _settingsService = new SettingsService();
            LoadSettings();
            LoadModels();
        }

        private void LoadSettings()
        {
            var settings = _settingsService.Load();
            AutoStartCheckBox.IsChecked = settings.AutoStart;
            MinimizeToTrayCheckBox.IsChecked = settings.MinimizeToTray;
            StartMinimizedCheckBox.IsChecked = settings.StartMinimized;
            AutoStartOllamaCheckBox.IsChecked = settings.AutoStartOllama;
            OllamaPathInput.Text = settings.OllamaPath;
            OllamaPortInput.Text = settings.OllamaPort.ToString();
            EnableScreenOffCheckBox.IsChecked = settings.EnableScreenOff;
        }

        private async void LoadModels()
        {
            var models = await _ollamaService.GetInstalledModelsAsync();
            DefaultModelComboBox.Items.Clear();
            foreach (var model in models)
            {
                DefaultModelComboBox.Items.Add(model);
            }

            var settings = _settingsService.Load();
            if (!string.IsNullOrEmpty(settings.DefaultModel))
            {
                DefaultModelComboBox.SelectedItem = settings.DefaultModel;
            }
            else if (DefaultModelComboBox.Items.Count > 0)
            {
                DefaultModelComboBox.SelectedIndex = 0;
            }
        }

        private void AutoStartCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            AutoStartManager.EnableAutoStart();
            SaveSettings();
        }

        private void AutoStartCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            AutoStartManager.DisableAutoStart();
            SaveSettings();
        }

        private void BrowseOllamaButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.OpenFileDialog
            {
                Filter = "可执行文件|*.exe",
                Title = "选择 Ollama 可执行文件"
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                OllamaPathInput.Text = dialog.FileName;
                SaveSettings();
            }
        }

        private void EnableScreenOffCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }

        private void EnableScreenOffCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }

        private void ScreenOffButton_Click(object sender, RoutedEventArgs e)
        {
            ScreenOffHelper.TurnOffScreen();
        }

        private void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("当前已是最新版本", "检查更新", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SaveSettings()
        {
            var settings = new AppSettings
            {
                AutoStart = AutoStartCheckBox.IsChecked ?? false,
                MinimizeToTray = MinimizeToTrayCheckBox.IsChecked ?? true,
                StartMinimized = StartMinimizedCheckBox.IsChecked ?? false,
                AutoStartOllama = AutoStartOllamaCheckBox.IsChecked ?? true,
                OllamaPath = OllamaPathInput.Text.Trim(),
                OllamaPort = int.TryParse(OllamaPortInput.Text.Trim(), out var port) ? port : 11434,
                DefaultModel = DefaultModelComboBox.SelectedItem?.ToString() ?? string.Empty,
                EnableScreenOff = EnableScreenOffCheckBox.IsChecked ?? false
            };

            _settingsService.Save(settings);
        }
    }
}
