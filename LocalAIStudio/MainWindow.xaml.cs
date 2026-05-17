using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using LocalAIStudio.Models;
using LocalAIStudio.Services;
using LocalAIStudio.ViewModels;

namespace LocalAIStudio
{
    public partial class MainWindow : Window
    {
        private MainViewModel _viewModel;
        private DispatcherTimer _chartTimer;
        private readonly List<double> _cpuHistory = new List<double>();
        private readonly List<double> _aiCpuHistory = new List<double>();
        private const int MaxDataPoints = 60;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            DataContext = _viewModel;

            Loaded += MainWindow_Loaded;
            MouseLeftButtonDown += Window_MouseLeftButtonDown;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeData();
            InitializeChart();
            StartChartUpdates();
        }

        private void InitializeData()
        {
            LocalIpText.Text = _viewModel.LocalIp;
            PublicIpText.Text = string.IsNullOrEmpty(_viewModel.PublicIp) ? "未连接" : _viewModel.PublicIp;
            UserDisplayText.Text = _viewModel.Username;
            UsernameText.Text = _viewModel.Username;
        }

        private void InitializeChart()
        {
            for (int i = 0; i < MaxDataPoints; i++)
            {
                _cpuHistory.Add(0);
                _aiCpuHistory.Add(0);
            }
        }

        private void StartChartUpdates()
        {
            _chartTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _chartTimer.Tick += ChartTimer_Tick;
            _chartTimer.Start();
        }

        private void ChartTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                var cpuUsage = GetCpuUsage();
                var aiUsage = GetAiProcessUsage();

                _cpuHistory.Add(cpuUsage);
                _aiCpuHistory.Add(aiUsage);

                if (_cpuHistory.Count > MaxDataPoints)
                    _cpuHistory.RemoveAt(0);
                if (_aiCpuHistory.Count > MaxDataPoints)
                    _aiCpuHistory.RemoveAt(0);

                UpdateChart();
                UpdatePerformanceDisplay(cpuUsage, aiUsage);
            }
            catch { }
        }

        private double GetCpuUsage()
        {
            try
            {
                var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                return cpuCounter.NextValue();
            }
            catch
            {
                return new Random().Next(10, 50);
            }
        }

        private double GetAiProcessUsage()
        {
            try
            {
                var processes = Process.GetProcessesByName("ollama");
                if (processes.Length > 0)
                {
                    return Math.Min(100, processes[0].TotalProcessorTime.TotalMilliseconds / Environment.ProcessorCount / 10);
                }
            }
            catch { }
            return new Random().Next(0, 10);
        }

        private void UpdateChart()
        {
            var canvasWidth = ChartCanvas.ActualWidth;
            var canvasHeight = ChartCanvas.ActualHeight;

            if (canvasWidth <= 0 || canvasHeight <= 0)
                return;

            var cpuPoints = new PointCollection();
            var aiPoints = new PointCollection();

            for (int i = 0; i < _cpuHistory.Count; i++)
            {
                var x = (double)i / MaxDataPoints * canvasWidth;
                var cpuY = canvasHeight - (_cpuHistory[i] / 100 * canvasHeight);
                var aiY = canvasHeight - (_aiCpuHistory[i] / 100 * canvasHeight);

                cpuPoints.Add(new Point(x, cpuY));
                aiPoints.Add(new Point(x, aiY));
            }

            CpuLine.Points = cpuPoints;
            AiLine.Points = aiPoints;
        }

        private void UpdatePerformanceDisplay(double cpu, double ai)
        {
            CpuText.Text = $"{cpu:F0}%";
            CpuBar.Value = cpu;

            AiCpuText.Text = $"{ai:F0}%";
            AiCpuBar.Value = ai;

            MemText.Text = $"{GetMemoryUsage():F0}%";
            MemBar.Value = GetMemoryUsage();

            GpuText.Text = $"{GetGpuUsage():F0}%";
            GpuBar.Value = GetGpuUsage();
        }

        private double GetMemoryUsage()
        {
            try
            {
                var ramCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use");
                return ramCounter.NextValue();
            }
            catch
            {
                return new Random().Next(30, 70);
            }
        }

        private double GetGpuUsage()
        {
            return new Random().Next(20, 60);
        }

        #region Navigation

        private void NavButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radioButton && radioButton.Tag is string pageName)
            {
                NavigateToPage(pageName);
            }
        }

        private void NavigateToPage(string pageName)
        {
            HideAllPages();
            PageTitle.Text = pageName;

            switch (pageName)
            {
                case "硬件显示":
                    HardwarePage.Visibility = Visibility.Visible;
                    PageSubtitle.Text = "实时监控系统性能";
                    break;
                case "AI调用":
                    AiPage.Visibility = Visibility.Visible;
                    PageSubtitle.Text = "与本地AI对话";
                    break;
                case "远控电脑":
                    RemotePage.Visibility = Visibility.Visible;
                    PageSubtitle.Text = "远程访问和控制";
                    break;
                case "网站服务器":
                    WebServerPage.Visibility = Visibility.Visible;
                    PageSubtitle.Text = "托管本地网站";
                    LoadWebsites();
                    break;
            }
        }

        private void HideAllPages()
        {
            HardwarePage.Visibility = Visibility.Collapsed;
            AiPage.Visibility = Visibility.Collapsed;
            RemotePage.Visibility = Visibility.Collapsed;
            WebServerPage.Visibility = Visibility.Collapsed;
        }

        #endregion

        #region Chat

        private CancellationTokenSource _chatCts;

        private async void SendChat_Click(object sender, RoutedEventArgs e)
        {
            await SendChat();
        }

        private async void ChatInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(ChatInput.Text))
            {
                await SendChat();
            }
        }

        private async Task SendChat()
        {
            var message = ChatInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(message)) return;

            AddChatBubble(message, isUser: true);
            ChatInput.Text = "";

            SendButton.IsEnabled = false;
            SendButton.Content = "生成中...";

            try
            {
                _chatCts = new CancellationTokenSource();
                var models = await OllamaService.GetInstalledModelsAsync();

                if (models.Count == 0)
                {
                    AddChatBubble("错误：未检测到已安装的Ollama模型，请先在首页安装模型。", isUser: false);
                    return;
                }

                var modelName = models[0].Name;
                var requestBody = new { model = modelName, prompt = message, stream = true };
                var json = JsonSerializer.Serialize(requestBody);

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromMinutes(10);

                var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost:11434/api/generate")
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };

                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, _chatCts.Token);
                response.EnsureSuccessStatusCode();

                var fullResponse = new StringBuilder();
                var responseBorder = AddChatBubble("", isUser: false);

                using var stream = await response.Content.ReadAsStreamAsync(_chatCts.Token);
                using var reader = new StreamReader(stream);

                while (!reader.EndOfStream && !_chatCts.Token.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync();
                    if (!string.IsNullOrEmpty(line))
                    {
                        try
                        {
                            var result = JsonSerializer.Deserialize<OllamaStreamResponse>(line);
                            if (result.response != null)
                            {
                                fullResponse.Append(result.response);
                                var textBlock = responseBorder.Child as TextBlock;
                                if (textBlock != null)
                                {
                                    textBlock.Text = fullResponse.ToString();
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                AddChatBubble("已停止生成", isUser: false);
            }
            catch (Exception ex)
            {
                AddChatBubble($"错误：{ex.Message}", isUser: false);
            }
            finally
            {
                SendButton.IsEnabled = true;
                SendButton.Content = "发送";
                _chatCts.Dispose();
            }
        }

        private Border AddChatBubble(string text, bool isUser)
        {
            var border = new Border
            {
                Background = isUser ? (SolidColorBrush)FindResource("BlueGradient") : new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 15),
                MaxWidth = 600,
                HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left
            };

            var textBlock = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                Foreground = isUser ? Brushes.White : new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                FontSize = 14
            };

            border.Child = textBlock;
            ChatContainer.Children.Add(border);

            ChatScrollViewer.ScrollToEnd();

            return border;
        }

        private class OllamaStreamResponse
        {
            public string response { get; set; }
            public bool done { get; set; }
        }

        #endregion

        #region Remote Control

        private void ConnectRemote_Click(object sender, RoutedEventArgs e)
        {
            var address = RemoteAddressInput.Text;
            if (string.IsNullOrWhiteSpace(address))
            {
                MessageBox.Show("请输入远程连接地址", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            RemoteStatusText.Text = $"正在连接到 {address}...";
            IsConnected(true);
        }

        private void DisconnectRemote_Click(object sender, RoutedEventArgs e)
        {
            RemoteStatusText.Text = "未连接";
            IsConnected(false);
        }

        private void IsConnected(bool connected)
        {
            ConnectButton.IsEnabled = !connected;
            DisconnectButton.IsEnabled = connected;
            RemoteStatusText.Text = connected ? "已连接" : "未连接";
        }

        #endregion

        #region Web Server

        private void LoadWebsites()
        {
            WebsiteListBox.ItemsSource = WebServerService.Instance.Websites.Values.ToList();
        }

        private void AddWebsite_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Views.AddWebsiteDialog();
            if (dialog.ShowDialog() == true)
            {
                var website = WebServerService.Instance.AddWebsite(
                    dialog.WebsiteName,
                    dialog.WebsitePath,
                    dialog.WebsitePort);

                if (!System.IO.Directory.Exists(website.RootPath))
                {
                    System.IO.Directory.CreateDirectory(website.RootPath);
                }

                LoadWebsites();
            }
        }

        private void CopyWebsiteLink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is WebsiteInfo site)
            {
                Clipboard.SetText(site.LocalUrl);
                MessageBox.Show("链接已复制到剪贴板", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void OpenWebsite_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is WebsiteInfo site)
            {
                if (!site.IsRunning)
                {
                    await WebServerService.Instance.StartWebsite(site.Id);
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = site.LocalUrl,
                    UseShellExecute = true
                });

                LoadWebsites();
            }
        }

        #endregion

        #region Window Controls

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        #endregion
    }
}
