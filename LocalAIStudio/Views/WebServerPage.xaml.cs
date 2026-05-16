using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LocalAIStudio.Models;
using LocalAIStudio.Services;
using Microsoft.Win32;

namespace LocalAIStudio.Views
{
    public partial class WebServerPage : UserControl
    {
        private ObservableCollection<WebsiteInfo> _websites = new ObservableCollection<WebsiteInfo>();
        private WebsiteInfo? _selectedWebsite;
        private bool _isUpdatingUI = false;

        public WebServerPage()
        {
            InitializeComponent();
            Loaded += WebServerPage_Loaded;
        }

        private void WebServerPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadWebsites();
            SubscribeToEvents();
            UpdateEmptyHint();
        }

        private void SubscribeToEvents()
        {
            WebServerService.Instance.WebsiteAdded += OnWebsiteAdded;
            WebServerService.Instance.WebsiteRemoved += OnWebsiteRemoved;
            WebServerService.Instance.WebsiteStarted += OnWebsiteStarted;
            WebServerService.Instance.WebsiteStopped += OnWebsiteStopped;
        }

        private void LoadWebsites()
        {
            _websites.Clear();
            foreach (var website in WebServerService.Instance.Websites.Values)
            {
                _websites.Add(website);
            }
            WebsiteListBox.ItemsSource = _websites;
            UpdateEmptyHint();
        }

        private void UpdateEmptyHint()
        {
            EmptyHint.Visibility = _websites.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnWebsiteAdded(object? sender, WebsiteInfo website)
        {
            Dispatcher.Invoke(() =>
            {
                _websites.Add(website);
                UpdateEmptyHint();
            });
        }

        private void OnWebsiteRemoved(object? sender, WebsiteInfo website)
        {
            Dispatcher.Invoke(() =>
            {
                var item = _websites.FirstOrDefault(w => w.Id == website.Id);
                if (item != null)
                {
                    _websites.Remove(item);
                }
                UpdateEmptyHint();
                ClearDetailsPanel();
            });
        }

        private void OnWebsiteStarted(object? sender, WebsiteInfo website)
        {
            Dispatcher.Invoke(() =>
            {
                var item = _websites.FirstOrDefault(w => w.Id == website.Id);
                if (item != null)
                {
                    var index = _websites.IndexOf(item);
                    if (index >= 0)
                    {
                        _websites[index] = website;
                        _websites[index].IsRunning = true;
                    }
                }
            });
        }

        private void OnWebsiteStopped(object? sender, WebsiteInfo website)
        {
            Dispatcher.Invoke(() =>
            {
                var item = _websites.FirstOrDefault(w => w.Id == website.Id);
                if (item != null)
                {
                    var index = _websites.IndexOf(item);
                    if (index >= 0)
                    {
                        _websites[index] = website;
                        _websites[index].IsRunning = false;
                    }
                }
            });
        }

        private void AddWebsite_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddWebsiteDialog();
            if (dialog.ShowDialog() == true)
            {
                var website = WebServerService.Instance.AddWebsite(
                    dialog.WebsiteName,
                    dialog.WebsitePath,
                    dialog.WebsitePort);

                if (!Directory.Exists(website.RootPath))
                {
                    Directory.CreateDirectory(website.RootPath);
                }
            }
        }

        private async void StartAll_Click(object sender, RoutedEventArgs e)
        {
            await WebServerService.Instance.StartAllWebsites();
        }

        private async void StopAll_Click(object sender, RoutedEventArgs e)
        {
            await WebServerService.Instance.StopAllWebsites();
        }

        private async void ToggleWebsite_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string websiteId)
            {
                var website = WebServerService.Instance.GetWebsite(websiteId);
                if (website != null)
                {
                    if (website.IsRunning)
                    {
                        await WebServerService.Instance.StopWebsite(websiteId);
                    }
                    else
                    {
                        bool success = await WebServerService.Instance.StartWebsite(websiteId);
                        if (!success)
                        {
                            MessageBox.Show($"无法启动网站，可能端口 {website.Port} 已被占用",
                                "启动失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }
            }
        }

        private void DeleteWebsite_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string websiteId)
            {
                var website = WebServerService.Instance.GetWebsite(websiteId);
                if (website != null)
                {
                    var result = MessageBox.Show(
                        $"确定要删除网站 '{website.Name}' 吗？\n\n注意：这不会删除网站文件",
                        "确认删除",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        WebServerService.Instance.RemoveWebsite(websiteId);
                    }
                }
            }
        }

        private void WebsiteListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (WebsiteListBox.SelectedItem is WebsiteInfo website)
            {
                _selectedWebsite = website;
                UpdateDetailsPanel();
            }
        }

        private void UpdateDetailsPanel()
        {
            if (_selectedWebsite == null) return;

            _isUpdatingUI = true;
            WebsiteNameText.Text = _selectedWebsite.Name;
            WebsitePortText.Text = _selectedWebsite.Port.ToString();
            WebsitePathText.Text = _selectedWebsite.RootPath;
            CustomDomainText.Text = _selectedWebsite.CustomDomain ?? "";
            AccessCountText.Text = _selectedWebsite.AccessCount.ToString();
            LastAccessText.Text = _selectedWebsite.LastAccessTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-";
            _isUpdatingUI = false;
        }

        private void ClearDetailsPanel()
        {
            _isUpdatingUI = true;
            WebsiteNameText.Text = "";
            WebsitePortText.Text = "";
            WebsitePathText.Text = "";
            CustomDomainText.Text = "";
            AccessCountText.Text = "0";
            LastAccessText.Text = "-";
            _isUpdatingUI = false;
            _selectedWebsite = null;
        }

        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedWebsite == null) return;

            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择网站根目录",
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                WebsitePathText.Text = dialog.SelectedPath;
                _selectedWebsite.RootPath = dialog.SelectedPath;
                WebServerService.Instance.UpdateWebsite(_selectedWebsite);
            }
        }

        private void WebsiteNameText_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_selectedWebsite == null || _isUpdatingUI) return;
            _selectedWebsite.Name = WebsiteNameText.Text;
            WebServerService.Instance.UpdateWebsite(_selectedWebsite);
        }

        private void WebsitePortText_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_selectedWebsite == null || _isUpdatingUI) return;
            if (int.TryParse(WebsitePortText.Text, out int port))
            {
                _selectedWebsite.Port = port;
                WebServerService.Instance.UpdateWebsite(_selectedWebsite);
            }
        }

        private void CustomDomainText_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_selectedWebsite == null || _isUpdatingUI) return;
            _selectedWebsite.CustomDomain = string.IsNullOrWhiteSpace(CustomDomainText.Text) ? null : CustomDomainText.Text;
            WebServerService.Instance.UpdateWebsite(_selectedWebsite);
        }

        private async void UploadFolder_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedWebsite == null)
            {
                MessageBox.Show("请先选择一个网站", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择包含HTML文件的文件夹"
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var result = await WebServerService.Instance.UploadFiles(_selectedWebsite.Id, dialog.SelectedPath);
                MessageBox.Show(result, "上传结果", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void UploadFile_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedWebsite == null)
            {
                MessageBox.Show("请先选择一个网站", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new OpenFileDialog
            {
                Title = "选择HTML文件",
                Filter = "HTML文件|*.html;*.htm|所有文件|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    foreach (var file in dialog.FileNames)
                    {
                        var fileName = Path.GetFileName(file);
                        var destPath = Path.Combine(_selectedWebsite.RootPath, fileName);
                        File.Copy(file, destPath, true);
                    }
                    MessageBox.Show($"成功上传 {dialog.FileNames.Length} 个文件", "上传成功", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"上传失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    public class AddWebsiteDialog : Window
    {
        public string WebsiteName { get; private set; } = "";
        public string WebsitePath { get; private set; } = "";
        public int WebsitePort { get; private set; } = 8080;

        private TextBox _nameTextBox;
        private TextBox _portTextBox;
        private TextBox _pathTextBox;

        public AddWebsiteDialog()
        {
            Title = "添加网站";
            Width = 500;
            Height = 380;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 250, 252));

            var grid = new Grid { Margin = new Thickness(25) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var titleText = new TextBlock { Text = "添加新网站", FontSize = 20, FontWeight = FontWeights.Bold, 
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(59, 130, 246)), 
                Margin = new Thickness(0, 0, 0, 20) };
            grid.Children.Add(titleText);

            var nameLabel = new TextBlock { Text = "网站名称", FontSize = 14, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 116, 139)), Margin = new Thickness(0, 0, 0, 5) };
            Grid.SetRow(nameLabel, 1);
            grid.Children.Add(nameLabel);

            _nameTextBox = new TextBox { Margin = new Thickness(0, 0, 0, 15), FontSize = 14, Padding = new Thickness(10) };
            Grid.SetRow(_nameTextBox, 1);
            grid.Children.Add(_nameTextBox);

            var pathLabel = new TextBlock { Text = "网站路径", FontSize = 14, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 116, 139)), Margin = new Thickness(0, 0, 0, 5), Grid.SetRow = 2 };
            Grid.SetRow(pathLabel, 2);
            grid.Children.Add(pathLabel);

            var pathPanel = new Grid();
            pathPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pathPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _pathTextBox = new TextBox { FontSize = 14, Padding = new Thickness(10) };
            Grid.SetColumn(_pathTextBox, 0);
            pathPanel.Children.Add(_pathTextBox);

            var browseBtn = new Button { Content = "浏览", Width = 70, Margin = new Thickness(10, 0, 0, 0), Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(108, 117, 125)), Foreground = System.Windows.Media.Brushes.White, BorderThickness = new Thickness(0) };
            browseBtn.Click += (s, e) =>
            {
                var dialog = new System.Windows.Forms.FolderBrowserDialog();
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _pathTextBox.Text = dialog.SelectedPath;
                }
            };
            Grid.SetColumn(browseBtn, 1);
            pathPanel.Children.Add(browseBtn);

            Grid.SetRow(pathPanel, 3);
            grid.Children.Add(pathPanel);

            var portLabel = new TextBlock { Text = "端口", FontSize = 14, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 116, 139)), Margin = new Thickness(0, 15, 0, 5) };
            Grid.SetRow(portLabel, 4);
            grid.Children.Add(portLabel);

            _portTextBox = new TextBox { Text = "8080", FontSize = 14, Padding = new Thickness(10), Width = 120, HorizontalAlignment = HorizontalAlignment.Left };
            Grid.SetRow(_portTextBox, 4);
            grid.Children.Add(_portTextBox);

            var hintText = new TextBlock { Text = "建议使用 8080-8999 之间的端口", FontSize = 12, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 163, 184)), Margin = new Thickness(130, 20, 0, 0) };
            Grid.SetRow(hintText, 4);
            grid.Children.Add(hintText);

            Content = grid;

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 30, 0, 0) };
            var okBtn = new Button { Content = "添加", Width = 100, Height = 40, Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(59, 130, 246)), Foreground = System.Windows.Media.Brushes.White, BorderThickness = new Thickness(0), FontSize = 14 };
            okBtn.Click += OkBtn_Click;
            buttonPanel.Children.Add(okBtn);

            var cancelBtn = new Button { Content = "取消", Width = 100, Height = 40, Margin = new Thickness(10, 0, 0, 0), Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(203, 213, 225)), BorderThickness = new Thickness(0), FontSize = 14 };
            cancelBtn.Click += (s, e) => DialogResult = false;
            buttonPanel.Children.Add(cancelBtn);

            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(buttonPanel, 5);
            grid.Children.Add(buttonPanel);
        }

        private void OkBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_nameTextBox.Text))
            {
                MessageBox.Show("请输入网站名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(_pathTextBox.Text))
            {
                MessageBox.Show("请选择网站路径", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(_portTextBox.Text, out int port) || port < 1 || port > 65535)
            {
                MessageBox.Show("请输入有效的端口号 (1-65535)", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            WebsiteName = _nameTextBox.Text;
            WebsitePath = _pathTextBox.Text;
            WebsitePort = port;

            DialogResult = true;
        }
    }
}
