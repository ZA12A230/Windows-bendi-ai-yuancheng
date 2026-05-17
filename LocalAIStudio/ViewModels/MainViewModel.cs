using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using LocalAIStudio.Models;
using LocalAIStudio.Services;
using Microsoft.Win32;

namespace LocalAIStudio.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private string _currentPage = "硬件显示";
        private string _systemInfo = "";
        private string _localIp = "";
        private string _publicIp = "";
        private string _username = "";
        private string _chatInput = "";
        private string _chatResponse = "";
        private string _remoteAddress = "";
        private bool _isConnected;
        private bool _isStreaming;
        private double _cpuUsage;
        private double _memoryUsage;
        private double _gpuUsage;
        private double _diskUsage;
        private double _networkUsage;
        private double _aiCpuUsage;
        private CancellationTokenSource _chatCts;

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<string> NavigateToPage;
        public event EventHandler<string> ChatMessageReceived;
        public event EventHandler<(double time, double system, double ai)> ChartDataUpdated;

        #region Properties

        public string CurrentPage
        {
            get => _currentPage;
            set { _currentPage = value; OnPropertyChanged(); }
        }

        public string SystemInfo
        {
            get => _systemInfo;
            set { _systemInfo = value; OnPropertyChanged(); }
        }

        public string LocalIp
        {
            get => _localIp;
            set { _localIp = value; OnPropertyChanged(); }
        }

        public string PublicIp
        {
            get => _publicIp;
            set { _publicIp = value; OnPropertyChanged(); }
        }

        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(); }
        }

        public string ChatInput
        {
            get => _chatInput;
            set { _chatInput = value; OnPropertyChanged(); }
        }

        public string ChatResponse
        {
            get => _chatResponse;
            set { _chatResponse = value; OnPropertyChanged(); }
        }

        public string RemoteAddress
        {
            get => _remoteAddress;
            set { _remoteAddress = value; OnPropertyChanged(); }
        }

        public bool IsConnected
        {
            get => _isConnected;
            set { _isConnected = value; OnPropertyChanged(); }
        }

        public bool IsStreaming
        {
            get => _isStreaming;
            set { _isStreaming = value; OnPropertyChanged(); }
        }

        public double CpuUsage
        {
            get => _cpuUsage;
            set { _cpuUsage = value; OnPropertyChanged(); }
        }

        public double MemoryUsage
        {
            get => _memoryUsage;
            set { _memoryUsage = value; OnPropertyChanged(); }
        }

        public double GpuUsage
        {
            get => _gpuUsage;
            set { _gpuUsage = value; OnPropertyChanged(); }
        }

        public double DiskUsage
        {
            get => _diskUsage;
            set { _diskUsage = value; OnPropertyChanged(); }
        }

        public double NetworkUsage
        {
            get => _networkUsage;
            set { _networkUsage = value; OnPropertyChanged(); }
        }

        public double AiCpuUsage
        {
            get => _aiCpuUsage;
            set { _aiCpuUsage = value; OnPropertyChanged(); }
        }

        public ObservableCollection<string> ChatMessages { get; } = new ObservableCollection<string>();
        public ObservableCollection<WebsiteInfo> Websites { get; } = new ObservableCollection<WebsiteInfo>();
        public ObservableCollection<string> Logs { get; } = new ObservableCollection<string>();

        #endregion

        #region Commands

        public ICommand NavigateCommand { get; }
        public ICommand SendChatCommand { get; }
        public ICommand StopChatCommand { get; }
        public ICommand ConnectRemoteCommand { get; }
        public ICommand DisconnectRemoteCommand { get; }
        public ICommand StartWebsiteCommand { get; }
        public ICommand StopWebsiteCommand { get; }
        public ICommand OpenInBrowserCommand { get; }
        public ICommand CopyLinkCommand { get; }

        #endregion

        public MainViewModel()
        {
            NavigateCommand = new RelayCommand(Navigate);
            SendChatCommand = new AsyncRelayCommand(SendChatAsync);
            StopChatCommand = new RelayCommand(_ => StopChat());
            ConnectRemoteCommand = new RelayCommand(_ => ConnectRemote());
            DisconnectRemoteCommand = new RelayCommand(_ => DisconnectRemote());
            StartWebsiteCommand = new AsyncRelayCommand(StartWebsiteAsync);
            StopWebsiteCommand = new AsyncRelayCommand(StopWebsiteAsync);
            OpenInBrowserCommand = new RelayCommand(OpenInBrowser);
            CopyLinkCommand = new RelayCommand(CopyLink);

            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            LoadSystemInfo();
            await LoadNetworkInfoAsync();
            LoadUserSettings();
            LoadWebsites();
            StartMonitoring();
        }

        private void LoadSystemInfo()
        {
            SystemInfo = $"Windows {Environment.OSVersion.Version.Major}.{Environment.OSVersion.Version.Minor} | " +
                        $"{Environment.ProcessorCount} 核心 | {GetInstalledMemory()}GB 内存";
        }

        private string GetInstalledMemory()
        {
            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                foreach (var obj in searcher.Get())
                {
                    var bytes = Convert.ToDouble(obj["TotalPhysicalMemory"]);
                    return (bytes / 1024 / 1024 / 1024).ToString("F1");
                }
            }
            catch { }
            return "8";
        }

        private async Task LoadNetworkInfoAsync()
        {
            LocalIp = GetLocalIpAddress();

            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                PublicIp = await client.GetStringAsync("https://api.ipify.org");
            }
            catch
            {
                PublicIp = "获取失败";
            }
        }

        private string GetLocalIpAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }
            }
            catch { }
            return "127.0.0.1";
        }

        private void LoadUserSettings()
        {
            var config = RemoteDesktopService.Instance.LoadConfig();
            Username = string.IsNullOrEmpty(config.Username) ? "未设置" : config.Username + " ****";
        }

        private void LoadWebsites()
        {
            Websites.Clear();
            foreach (var site in WebServerService.Instance.Websites.Values)
            {
                Websites.Add(site);
            }
        }

        private PerformanceCounter _cpuCounter;
        private PerformanceCounter _ramCounter;
        private DateTime _lastNetworkBytes = DateTime.Now;
        private long _lastTotalBytes = 0;

        private void StartMonitoring()
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _ramCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use");

                _lastTotalBytes = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up)
                    .Sum(n => n.GetIPStatistics().BytesReceived + n.GetIPStatistics().BytesSent);
            }
            catch { }

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            timer.Tick += (s, e) => UpdatePerformanceData();
            timer.Start();
        }

        private void UpdatePerformanceData()
        {
            try
            {
                CpuUsage = _cpuCounter != null ? _cpuCounter.NextValue() : 0;
                MemoryUsage = _ramCounter != null ? _ramCounter.NextValue() : 0;

                var currentBytes = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up)
                    .Sum(n => n.GetIPStatistics().BytesReceived + n.GetIPStatistics().BytesSent);

                var elapsed = (DateTime.Now - _lastNetworkBytes).TotalSeconds;
                if (elapsed > 0)
                {
                    var bytesPerSec = (currentBytes - _lastTotalBytes) / elapsed;
                    NetworkUsage = Math.Min(100, bytesPerSec / 1024 / 1024 / 10);
                }

                _lastTotalBytes = currentBytes;
                _lastNetworkBytes = DateTime.Now;

                AiCpuUsage = GetAiProcessCpuUsage();

                ChartDataUpdated.Invoke(this, (DateTime.Now.TimeOfDay.TotalSeconds, CpuUsage, AiCpuUsage));
            }
            catch { }
        }

        private double GetAiProcessCpuUsage()
        {
            try
            {
                var ollamaProcs = Process.GetProcessesByName("ollama");
                if (ollamaProcs.Length > 0)
                {
                    ollamaProcs[0].Refresh();
                    return ollamaProcs[0].TotalProcessorTime.TotalMilliseconds /
                           Environment.ProcessorCount / 10;
                }
            }
            catch { }
            return 0;
        }

        private void Navigate(object param)
        {
            if (param is string page)
            {
                CurrentPage = page;
                NavigateToPage.Invoke(this, page);
            }
        }

        private async Task SendChatAsync(object param)
        {
            if (string.IsNullOrWhiteSpace(ChatInput)) return;

            var message = ChatInput;
            ChatInput = "";
            IsStreaming = true;
            ChatResponse = "";

            ChatMessages.Add($"你: {message}");
            var fullResponse = new StringBuilder();

            try
            {
                _chatCts = new CancellationTokenSource();
                var models = await OllamaService.GetInstalledModelsAsync();

                if (models.Count == 0)
                {
                    ChatResponse = "错误: 未检测到已安装的Ollama模型，请先安装模型。";
                    ChatMessages.Add($"助手: {ChatResponse}");
                    IsStreaming = false;
                    return;
                }

                var modelName = models[0].Name;
                var url = $"http://localhost:11434/api/generate";

                var requestBody = new { model = modelName, prompt = message, stream = true };
                var json = JsonSerializer.Serialize(requestBody);

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromMinutes(10);

                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };

                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, _chatCts.Token);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync(_chatCts.Token);
                using var reader = new StreamReader(stream);

                while (!reader.EndOfStream && !_chatCts.Token.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync();
                    if (!string.IsNullOrEmpty(line))
                    {
                        try
                        {
                            var result = JsonSerializer.Deserialize<OllamaResponse>(line);
                            if (result != null && !string.IsNullOrEmpty(result.response))
                            {
                                fullResponse.Append(result.response);
                                ChatResponse = fullResponse.ToString();
                                ChatMessageReceived.Invoke(this, result.response);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                ChatResponse = fullResponse.ToString();
            }
            catch (Exception ex)
            {
                ChatResponse = $"错误: {ex.Message}";
            }
            finally
            {
                if (!string.IsNullOrEmpty(fullResponse.ToString()))
                {
                    ChatMessages.Add($"助手: {fullResponse}");
                }
                IsStreaming = false;
                if (_chatCts != null)
                    _chatCts.Dispose();
                _chatCts = null;
            }
        }

        private void StopChat()
        {
            if (_chatCts != null)
                _chatCts.Cancel();
        }

        private void ConnectRemote()
        {
            if (string.IsNullOrWhiteSpace(RemoteAddress))
            {
                System.Windows.MessageBox.Show("请输入远程连接地址", "提示");
                return;
            }

            IsConnected = true;
            Logs.Add($"[{DateTime.Now:HH:mm:ss}] 正在连接到 {RemoteAddress}...");
        }

        private void DisconnectRemote()
        {
            IsConnected = false;
            Logs.Add($"[{DateTime.Now:HH:mm:ss}] 已断开远程连接");
        }

        private async Task StartWebsiteAsync(object param)
        {
            if (param is WebsiteInfo site)
            {
                await WebServerService.Instance.StartWebsite(site.Id);
                LoadWebsites();
            }
        }

        private async Task StopWebsiteAsync(object param)
        {
            if (param is WebsiteInfo site)
            {
                await WebServerService.Instance.StopWebsite(site.Id);
                LoadWebsites();
            }
        }

        private void OpenInBrowser(object param)
        {
            if (param is WebsiteInfo site && site.IsRunning)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = site.LocalUrl,
                    UseShellExecute = true
                });
            }
        }

        private void CopyLink(object param)
        {
            if (param is WebsiteInfo site)
            {
                System.Windows.Clipboard.SetText(site.LocalUrl);
                System.Windows.MessageBox.Show("链接已复制到剪贴板", "提示");
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private class OllamaResponse
        {
            public string response { get; set; }
            public bool done { get; set; }
        }
    }
}
