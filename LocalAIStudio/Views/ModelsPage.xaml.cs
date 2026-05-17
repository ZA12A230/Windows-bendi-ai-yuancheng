using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using LocalAIStudio.Services;

namespace LocalAIStudio.Views
{
    public partial class ModelsPage : System.Windows.Controls.UserControl
    {
        public ObservableCollection<Services.ModelInfo> Models { get; set; }
        public HashSet<string> SelectedModels { get; private set; }
        public event EventHandler ModelsSelectedChanged;

        private CancellationTokenSource _cancellationTokenSource;

        public ModelsPage()
        {
            InitializeComponent();
            Models = new ObservableCollection<Services.ModelInfo>();
            SelectedModels = new HashSet<string>();
            ModelsList.ItemsSource = Models;
            Loaded += ModelsPage_Loaded;
        }

        private async void ModelsPage_Loaded(object sender, RoutedEventArgs e)
        {
            await RefreshModelsAsync();
        }

        public async System.Threading.Tasks.Task RefreshModelsAsync()
        {
            var models = await OllamaService.GetInstalledModelsAsync();
            Models.Clear();
            foreach (var model in models)
            {
                Models.Add(model);
            }

            if (Models.Count == 0)
            {
                EmptyStateBorder.Visibility = Visibility.Visible;
                ModelsListBorder.Visibility = Visibility.Collapsed;
            }
            else
            {
                EmptyStateBorder.Visibility = Visibility.Collapsed;
                ModelsListBorder.Visibility = Visibility.Visible;
            }

            UpdateSelectedModels();
        }

        private void CopyUrlButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(ModelLibraryUrl.Text))
            {
                System.Windows.Clipboard.SetText(ModelLibraryUrl.Text);
                var originalContent = CopyUrlButton.Content;
                CopyUrlButton.Content = "已复制";
                System.Threading.Tasks.Task.Delay(1500).ContinueWith(_ =>
                {
                    Dispatcher.Invoke(() => CopyUrlButton.Content = originalContent);
                });
            }
        }

        private async void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            var modelText = ModelInput.Text.Trim();
            var modelName = ParseModelName(modelText);
            if (string.IsNullOrEmpty(modelName))
            {
                MessageBox.Show("请输入有效的模型名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_cancellationTokenSource != null)
                return;

            InstallButton.IsEnabled = false;
            InstallProgressBorder.Visibility = Visibility.Visible;
            InstallProgressBar.Value = 0;

            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                var progress = new Progress<int>(pct =>
                {
                    InstallProgressBar.Value = pct;
                });

                var status = new Progress<string>(msg =>
                {
                    InstallStatusText.Text = msg;
                });

                await OllamaService.PullModelAsync(modelName, progress, status, _cancellationTokenSource.Token);
                await RefreshModelsAsync();
                System.Windows.MessageBox.Show($"模型 {modelName} 安装成功", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                // 用户取消了操作
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"安装模型失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (_cancellationTokenSource != null)
                    _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
                InstallButton.IsEnabled = true;
                InstallProgressBorder.Visibility = Visibility.Collapsed;
            }
        }

        private string ParseModelName(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            // 尝试解析类似 "ollama run llama2" 这样的输入
            var parts = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
            {
                var index = Array.FindIndex(parts, p => p.Equals("run", StringComparison.OrdinalIgnoreCase));
                if (index >= 0 && index < parts.Length - 1)
                {
                    return parts[index + 1];
                }
                return parts[parts.Length - 1]; // 直接取最后一个
            }
            return string.Empty;
        }

        private void ModelCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.DataContext is Services.ModelInfo model)
            {
                if (checkBox.IsChecked == true)
                {
                    if (!SelectedModels.Contains(model.Name))
                        SelectedModels.Add(model.Name);
                }
                else
                {
                    if (SelectedModels.Contains(model.Name))
                        SelectedModels.Remove(model.Name);
                }

                UpdateSelectedModels();
                ModelsSelectedChanged.Invoke(this, EventArgs.Empty);
            }
        }

        private void UpdateSelectedModels()
        {
            // 如果需要，可以在这里更新UI以反映选择状态
        }

        public List<string> GetSelectedModels()
        {
            return SelectedModels.ToList();
        }
    }
}
