using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace LocalAIStudio.Views
{
    public class AddWebsiteDialog : Window
    {
        public string WebsiteName { get; private set; } = "";
        public string WebsitePath { get; private set; } = "";
        public int WebsitePort { get; private set; } = 8080;

        private TextBox _nameTextBox;
        private TextBox _pathTextBox;
        private TextBox _portTextBox;
        private Button _okButton;
        private Button _cancelButton;
        private Button _browseButton;

        public AddWebsiteDialog()
        {
            Title = "添加网站";
            Width = 500;
            Height = 320;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.ToolWindow;
            Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FAFBFC"));

            var mainGrid = new Grid { Margin = new Thickness(20) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _nameTextBox = new TextBox { Height = 40, FontSize = 14, Padding = new Thickness(10, 8, 10, 8) };
            _pathTextBox = new TextBox { Height = 40, FontSize = 14, Padding = new Thickness(10, 8, 10, 8) };
            _portTextBox = new TextBox { Text = "8080", Height = 40, FontSize = 14, Padding = new Thickness(10, 8, 10, 8) };

            var nameLabel = new TextBlock { Text = "网站名称", FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 5) };
            var pathLabel = new TextBlock { Text = "网站根目录", FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 15, 0, 5) };
            var portLabel = new TextBlock { Text = "端口号", FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 15, 0, 5) };

            var pathPanel = new Grid();
            pathPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pathPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _browseButton = new Button { Content = "浏览...", Width = 80, Height = 40, Margin = new Thickness(10, 0, 0, 0) };
            _browseButton.Click += BrowseButton_Click;
            System.Windows.Controls.Grid.SetColumn(_pathTextBox, 0);
            System.Windows.Controls.Grid.SetColumn(_browseButton, 1);
            pathPanel.Children.Add(_pathTextBox);
            pathPanel.Children.Add(_browseButton);

            System.Windows.Controls.Grid.SetRow(nameLabel, 0);
            System.Windows.Controls.Grid.SetRow(_nameTextBox, 1);
            System.Windows.Controls.Grid.SetRow(pathLabel, 2);
            System.Windows.Controls.Grid.SetRow(pathPanel, 3);
            System.Windows.Controls.Grid.SetRow(portLabel, 4);

            mainGrid.Children.Add(nameLabel);
            mainGrid.Children.Add(_nameTextBox);
            mainGrid.Children.Add(pathLabel);
            mainGrid.Children.Add(pathPanel);

            var portPanel = new StackPanel();
            portPanel.Children.Add(portLabel);
            portPanel.Children.Add(_portTextBox);
            System.Windows.Controls.Grid.SetRow(portPanel, 4);
            mainGrid.Children.Add(portPanel);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 20, 0, 0)
            };

            _okButton = new Button { Content = "确定", Width = 100, Height = 36, Margin = new Thickness(0, 0, 10, 0) };
            _okButton.Click += OkButton_Click;
            _cancelButton = new Button { Content = "取消", Width = 100, Height = 36 };
            _cancelButton.Click += CancelButton_Click;

            buttonPanel.Children.Add(_okButton);
            buttonPanel.Children.Add(_cancelButton);

            var contentPanel = new Grid();
            contentPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            contentPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            System.Windows.Controls.Grid.SetRow(mainGrid, 0);
            System.Windows.Controls.Grid.SetRow(buttonPanel, 1);
            contentPanel.Children.Add(mainGrid);
            contentPanel.Children.Add(buttonPanel);

            Content = contentPanel;
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择网站根目录",
                UseDescriptionForTitle = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _pathTextBox.Text = dialog.SelectedPath;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_nameTextBox.Text))
            {
                MessageBox.Show("请输入网站名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(_pathTextBox.Text))
            {
                MessageBox.Show("请选择网站根目录", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(_portTextBox.Text, out int port) || port < 1 || port > 65535)
            {
                MessageBox.Show("请输入有效的端口号 (1-65535)", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            WebsiteName = _nameTextBox.Text.Trim();
            WebsitePath = _pathTextBox.Text.Trim();
            WebsitePort = port;

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
