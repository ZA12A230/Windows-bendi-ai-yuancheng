using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using LocalAIAssistant.Models;
using LocalAIAssistant.Services;

namespace LocalAIAssistant.Views.Pages
{
    public partial class ModelsPage : Page
    {
        private readonly OllamaService _ollamaService;
        private readonly ObservableCollection<ModelInfo> _models = new();

        public ModelsPage()
        {
            InitializeComponent();
            _ollamaService = new OllamaService();
            ModelsDataGrid.ItemsSource = _models;
            LoadModels();
        }

        private async void LoadModels()
        {
            var models = await _ollamaService.GetModelDetailsAsync();
            _models.Clear();
            foreach (var model in models)
            {
                _models.Add(model);
            }
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            var modelName = ModelNameInput.Text.Trim();
            if (string.IsNullOrEmpty(modelName))
            {
                MessageBox.Show("请输入模型名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DownloadProgressPanel.Visibility = Visibility.Visible;
            DownloadButton.IsEnabled = false;

            try
            {
                await _ollamaService.PullModelAsync(modelName, progress =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        DownloadProgressBar.Value = progress;
                        DownloadStatusText.Text = $"下载中... {progress:F1}%";
                    });
                });

                MessageBox.Show($"模型 {modelName} 下载完成", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadModels();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"下载失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                DownloadProgressPanel.Visibility = Visibility.Collapsed;
                DownloadButton.IsEnabled = true;
                ModelNameInput.Text = string.Empty;
            }
        }

        private async void DeleteModel_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is ModelInfo model)
            {
                var result = MessageBox.Show($"确定要删除模型 {model.Name} 吗?", "确认删除",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        await _ollamaService.DeleteModelAsync(model.Name);
                        _models.Remove(model);
                        MessageBox.Show("模型已删除", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"删除失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
    }
}
