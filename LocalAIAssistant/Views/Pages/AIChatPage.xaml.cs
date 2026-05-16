using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LocalAIAssistant.Models;
using LocalAIAssistant.Services;

namespace LocalAIAssistant.Views.Pages
{
    public partial class AIChatPage : Page
    {
        private readonly OllamaService _ollamaService;
        private readonly ObservableCollection<ChatMessage> _messages = new();

        public AIChatPage()
        {
            InitializeComponent();
            _ollamaService = new OllamaService();
            ChatMessagesControl.ItemsSource = _messages;
            LoadModels();
        }

        private async void LoadModels()
        {
            var models = await _ollamaService.GetInstalledModelsAsync();
            ModelComboBox.Items.Clear();
            foreach (var model in models)
            {
                ModelComboBox.Items.Add(model);
            }
            if (ModelComboBox.Items.Count > 0)
            {
                ModelComboBox.SelectedIndex = 0;
            }
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            await SendMessageAsync();
        }

        private async void MessageInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
            {
                e.Handled = true;
                await SendMessageAsync();
            }
        }

        private async System.Threading.Tasks.Task SendMessageAsync()
        {
            var message = MessageInput.Text.Trim();
            if (string.IsNullOrEmpty(message)) return;

            var selectedModel = ModelComboBox.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedModel))
            {
                MessageBox.Show("请先选择一个模型", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _messages.Add(new ChatMessage { Role = "用户", Content = message, IsUser = true });
            MessageInput.Text = string.Empty;

            SendButton.IsEnabled = false;
            try
            {
                var response = await _ollamaService.ChatAsync(selectedModel, message);
                _messages.Add(new ChatMessage { Role = "AI", Content = response, IsUser = false });
            }
            catch (Exception ex)
            {
                _messages.Add(new ChatMessage { Role = "系统", Content = $"错误: {ex.Message}", IsUser = false });
            }
            finally
            {
                SendButton.IsEnabled = true;
            }

            ChatScrollViewer.ScrollToEnd();
        }

        private void ClearChatButton_Click(object sender, RoutedEventArgs e)
        {
            _messages.Clear();
        }
    }

    public class ChatMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public bool IsUser { get; set; }
    }

    public class MessageBackgroundConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (bool)value ? new SolidColorBrush(Color.FromRgb(99, 102, 241)) : new SolidColorBrush(Color.FromRgb(45, 45, 63));
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class MessageAlignmentConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (bool)value ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
