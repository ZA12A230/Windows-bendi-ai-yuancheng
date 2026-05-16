using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using LocalAIAssistant.Models;
using LocalAIAssistant.Services;

namespace LocalAIAssistant.Views.Pages
{
    public partial class HardwarePage : Page
    {
        private readonly DispatcherTimer _monitorTimer;
        private readonly HardwareMonitorService _hardwareMonitor;
        private readonly ObservableCollection<DiskPartitionInfo> _diskPartitions = new();

        public HardwarePage()
        {
            InitializeComponent();
            _hardwareMonitor = new HardwareMonitorService();
            DiskPartitionsControl.ItemsSource = _diskPartitions;

            LoadStaticInfo();
            LoadDiskPartitions();

            _monitorTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _monitorTimer.Tick += MonitorTimer_Tick;
            _monitorTimer.Start();
        }

        private void LoadStaticInfo()
        {
            var cpuInfo = _hardwareMonitor.GetCpuInfo();
            CpuNameText.Text = cpuInfo.Name;
            CpuCoresText.Text = $"核心数: {cpuInfo.Cores}";

            var gpuInfo = _hardwareMonitor.GetGpuInfo();
            GpuNameText.Text = gpuInfo.Name;
            GpuMemoryText.Text = $"显存: {gpuInfo.MemoryGB:F1} GB";

            var (memoryUsed, memoryTotal, _) = _hardwareMonitor.GetMemoryUsage();
            MemoryTotalText.Text = $"总内存: {memoryTotal:F1} GB";
        }

        private void LoadDiskPartitions()
        {
            var partitions = _hardwareMonitor.GetDiskPartitions();
            _diskPartitions.Clear();
            foreach (var partition in partitions)
            {
                _diskPartitions.Add(partition);
            }
        }

        private void MonitorTimer_Tick(object? sender, EventArgs e)
        {
            var cpuUsage = _hardwareMonitor.GetCpuUsage();
            CpuUsageText.Text = $"使用率: {cpuUsage:F1}%";
            CpuTempText.Text = $"温度: {_hardwareMonitor.GetCpuTemperature():F0}°C";

            var (memoryUsed, memoryTotal, memoryPercent) = _hardwareMonitor.GetMemoryUsage();
            MemoryUsedText.Text = $"已使用: {memoryUsed:F1} GB";
            MemoryFreeText.Text = $"可用: {memoryTotal - memoryUsed:F1} GB";
            MemoryProgressBar.Value = memoryPercent;

            var gpuUsage = _hardwareMonitor.GetGpuUsage();
            GpuUsageText.Text = $"使用率: {gpuUsage:F1}%";
            GpuTempText.Text = $"温度: {_hardwareMonitor.GetGpuTemperature():F0}°C";

            var (diskUsed, diskTotal, diskPercent) = _hardwareMonitor.GetDiskUsage();
            DiskTotalText.Text = $"总容量: {diskTotal:F1} GB";
            DiskUsedText.Text = $"已使用: {diskUsed:F1} GB";
            DiskFreeText.Text = $"可用: {diskTotal - diskUsed:F1} GB";
            DiskProgressBar.Value = diskPercent;

            var (downloadSpeed, uploadSpeed) = _hardwareMonitor.GetNetworkSpeed();
            DownloadSpeedText.Text = FormatSpeed(downloadSpeed);
            UploadSpeedText.Text = FormatSpeed(uploadSpeed);
        }

        private string FormatSpeed(double bytesPerSecond)
        {
            if (bytesPerSecond < 1024)
                return $"{bytesPerSecond:F0} B/s";
            if (bytesPerSecond < 1024 * 1024)
                return $"{bytesPerSecond / 1024:F1} KB/s";
            return $"{bytesPerSecond / (1024 * 1024):F1} MB/s";
        }
    }
}
