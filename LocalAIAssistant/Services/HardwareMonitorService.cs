using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using LocalAIAssistant.Models;

namespace LocalAIAssistant.Services
{
    public class HardwareMonitorService
    {
        private readonly PerformanceCounter _cpuCounter;
        private readonly PerformanceCounter _memCounter;
        private readonly PerformanceCounter[] _netCounters;
        private long _lastBytesSent;
        private long _lastBytesReceived;
        private DateTime _lastNetworkCheck;

        public HardwareMonitorService()
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _memCounter = new PerformanceCounter("Memory", "Available MBytes");
            _netCounters = GetNetworkCounters();
            _lastNetworkCheck = DateTime.Now;
        }

        public double GetCpuUsage()
        {
            try
            {
                return _cpuCounter.NextValue();
            }
            catch
            {
                return 0;
            }
        }

        public (double UsedGB, double TotalGB, double Percent) GetMemoryUsage()
        {
            try
            {
                var availableMB = _memCounter.NextValue();
                using var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem");
                var totalKB = 0UL;
                foreach (var obj in searcher.Get())
                {
                    totalKB = Convert.ToUInt64(obj["TotalVisibleMemorySize"]);
                }

                var totalGB = totalKB / 1024.0 / 1024.0;
                var availableGB = availableMB / 1024.0;
                var usedGB = totalGB - availableGB;
                var percent = (usedGB / totalGB) * 100;

                return (usedGB, totalGB, percent);
            }
            catch
            {
                return (0, 16, 0);
            }
        }

        public double GetGpuUsage()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_VideoController");
                foreach (var obj in searcher.Get())
                {
                    var driverModel = obj["DriverModel"]?.ToString();
                    if (!string.IsNullOrEmpty(driverModel))
                    {
                        return 0;
                    }
                }

                using var gpuSearcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine");
                double totalUsage = 0;
                int count = 0;
                foreach (var obj in gpuSearcher.Get())
                {
                    var usage = obj["UtilizationPercentage"];
                    if (usage != null)
                    {
                        totalUsage += Convert.ToDouble(usage);
                        count++;
                    }
                }
                return count > 0 ? totalUsage / count : 0;
            }
            catch
            {
                return 0;
            }
        }

        public CpuInfo GetCpuInfo()
        {
            var info = new CpuInfo();
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
                foreach (var obj in searcher.Get())
                {
                    info.Name = obj["Name"]?.ToString() ?? "Unknown CPU";
                    info.Cores = Convert.ToInt32(obj["NumberOfCores"]);
                    info.Threads = Convert.ToInt32(obj["NumberOfLogicalProcessors"]);
                    info.MaxFrequency = Convert.ToDouble(obj["MaxClockSpeed"]) / 1000;
                    break;
                }
            }
            catch
            {
                info.Name = "Unknown CPU";
                info.Cores = Environment.ProcessorCount;
            }
            return info;
        }

        public GpuInfo GetGpuInfo()
        {
            var info = new GpuInfo();
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
                foreach (var obj in searcher.Get())
                {
                    info.Name = obj["Name"]?.ToString() ?? "Unknown GPU";
                    var adapterRAM = obj["AdapterRAM"];
                    if (adapterRAM != null)
                    {
                        info.MemoryGB = Convert.ToInt64(adapterRAM) / 1024.0 / 1024.0 / 1024.0;
                    }
                    info.DriverVersion = obj["DriverVersion"]?.ToString() ?? "";
                    break;
                }
            }
            catch
            {
                info.Name = "Unknown GPU";
            }
            return info;
        }

        public double GetCpuTemperature()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM MSAcpi_ThermalZoneTemperature");
                foreach (var obj in searcher.Get())
                {
                    var temp = obj["CurrentTemperature"];
                    if (temp != null)
                    {
                        return (Convert.ToDouble(temp) / 10) - 273.15;
                    }
                }
            }
            catch { }
            return 0;
        }

        public double GetGpuTemperature()
        {
            return 0;
        }

        public (double DownloadSpeed, double UploadSpeed) GetNetworkSpeed()
        {
            try
            {
                long totalSent = 0;
                long totalReceived = 0;

                foreach (var counter in _netCounters)
                {
                    try
                    {
                        totalSent += (long)counter.NextValue();
                    }
                    catch { }
                }

                var now = DateTime.Now;
                var elapsed = (now - _lastNetworkCheck).TotalSeconds;

                double downloadSpeed = 0;
                double uploadSpeed = 0;

                if (elapsed > 0)
                {
                    downloadSpeed = (totalReceived - _lastBytesReceived) / elapsed;
                    uploadSpeed = (totalSent - _lastBytesSent) / elapsed;
                }

                _lastBytesSent = totalSent;
                _lastBytesReceived = totalReceived;
                _lastNetworkCheck = now;

                return (Math.Max(0, downloadSpeed), Math.Max(0, uploadSpeed));
            }
            catch
            {
                return (0, 0);
            }
        }

        public (double UsedGB, double TotalGB, double Percent) GetDiskUsage()
        {
            try
            {
                var drive = DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady && d.DriveType == DriveType.Fixed);
                if (drive != null)
                {
                    var totalGB = drive.TotalSize / 1024.0 / 1024.0 / 1024.0;
                    var freeGB = drive.TotalFreeSpace / 1024.0 / 1024.0 / 1024.0;
                    var usedGB = totalGB - freeGB;
                    var percent = (usedGB / totalGB) * 100;
                    return (usedGB, totalGB, percent);
                }
            }
            catch { }
            return (0, 500, 0);
        }

        public List<DiskPartitionInfo> GetDiskPartitions()
        {
            var partitions = new List<DiskPartitionInfo>();
            try
            {
                foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
                {
                    var totalGB = drive.TotalSize / 1024.0 / 1024.0 / 1024.0;
                    var freeGB = drive.TotalFreeSpace / 1024.0 / 1024.0 / 1024.0;
                    var usedGB = totalGB - freeGB;
                    partitions.Add(new DiskPartitionInfo
                    {
                        Name = drive.Name,
                        TotalGB = Math.Round(totalGB, 1),
                        UsedGB = Math.Round(usedGB, 1),
                        FreeGB = Math.Round(freeGB, 1),
                        UsedPercent = Math.Round((usedGB / totalGB) * 100, 1)
                    });
                }
            }
            catch { }
            return partitions;
        }

        private PerformanceCounter[] GetNetworkCounters()
        {
            var counters = new List<PerformanceCounter>();
            try
            {
                var category = new PerformanceCounterCategory("Network Interface");
                var instances = category.GetInstanceNames();
                foreach (var instance in instances)
                {
                    try
                    {
                        counters.Add(new PerformanceCounter("Network Interface", "Bytes Sent/sec", instance));
                        counters.Add(new PerformanceCounter("Network Interface", "Bytes Received/sec", instance));
                    }
                    catch { }
                }
            }
            catch { }
            return counters.ToArray();
        }
    }
}
