using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using LocalAIAssistant.Services;

namespace LocalAIAssistant.Views.Pages
{
    public partial class DashboardPage : Page
    {
        private readonly DispatcherTimer _monitorTimer;
        private readonly HardwareMonitorService _hardwareMonitor;
        private readonly OllamaService _ollamaService;
        private readonly ObservableCollection<string> _installedModels = new();

        public DashboardPage()
        {
            InitializeComponent();
            _hardwareMonitor = new HardwareMonitorService();
            _ollamaService = new OllamaService();
            ModelsListBox.ItemsSource = _installedModels;

            _monitorTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _monitorTimer.Tick += MonitorTimer_Tick;
            _monitorTimer.Start();

            UpdateOllamaStatus();
            LoadInstalledModels();
        }

        private void MonitorTimer_Tick(object? sender, EventArgs e)
        {
            var cpuUsage = _hardwareMonitor.GetCpuUsage();
            var (memoryUsed, memoryTotal, memoryPercent) = _hardwareMonitor.GetMemoryUsage();
            var gpuUsage = _hardwareMonitor.GetGpuUsage();
            var (downloadSpeed, uploadSpeed) = _hardwareMonitor.GetNetworkSpeed();

            CpuUsageText.Text = $"{cpuUsage:F1}%";
            CpuProgress.Value = cpuUsage;

            MemoryUsageText.Text = $"{memoryUsed:F1} GB";
            MemoryProgress.Value = memoryPercent;

            GpuUsageText.Text = $"{gpuUsage:F1}%";
            GpuProgress.Value = gpuUsage;

            NetworkSpeedText.Text = $"{FormatSpeed(downloadSpeed + uploadSpeed)}";
        }

        private string FormatSpeed(double bytesPerSecond)
        {
            if (bytesPerSecond < 1024)
                return $"{bytesPerSecond:F0} B/s";
            if (bytesPerSecond < 1024 * 1024)
                return $"{bytesPerSecond / 1024:F1} KB/s";
            return $"{bytesPerSecond / (1024 * 1024):F1} MB/s";
        }

        private async void UpdateOllamaStatus()
        {
            var isRunning = await _ollamaService.IsRunningAsync();
            OllamaStatusIndicator.Fill = isRunning ? new SolidColorBrush(Color.FromRgb(34, 197, 94)) : new SolidColorBrush(Color.FromRgb(239, 68, 68));
            OllamaStatusText.Text = isRunning ? "运行中" : "未运行";

            if (isRunning)
            {
                var version = await _ollamaService.GetVersionAsync();
                OllamaVersionText.Text = $"版本: {version}";
            }
        }

        private async void LoadInstalledModels()
        {
            var models = await _ollamaService.GetInstalledModelsAsync();
            _installedModels.Clear();
            foreach (var model in models)
            {
                _installedModels.Add(model);
            }
        }

        private void StartOllamaButton_Click(object sender, RoutedEventArgs e)
        {
            _ollamaService.StartOllama();
            UpdateOllamaStatus();
        }

        private void OpenAIChat_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is Views.MainWindow mainWindow)
            {
                mainWindow.NavigateToPage("AIChat");
            }
        }

        private void DownloadModel_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is Views.MainWindow mainWindow)
            {
                mainWindow.NavigateToPage("Models");
            }
        }

        private void StartTunnel_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is Views.MainWindow mainWindow)
            {
                mainWindow.NavigateToPage("Tunnel");
            }
        }

        private void OpenRemoteDesktop_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is Views.MainWindow mainWindow)
            {
                mainWindow.NavigateToPage("RemoteDesktop");
            }
        }
    }
}
