namespace LocalAIAssistant.Models
{
    public class ModelInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public string Modified { get; set; } = string.Empty;
        public string Digest { get; set; } = string.Empty;
    }

    public class DiskPartitionInfo
    {
        public string Name { get; set; } = string.Empty;
        public double TotalGB { get; set; }
        public double UsedGB { get; set; }
        public double FreeGB { get; set; }
        public double UsedPercent { get; set; }
        public string FreeSpace => $"{FreeGB:F1} GB 可用";
    }

    public class CpuInfo
    {
        public string Name { get; set; } = string.Empty;
        public int Cores { get; set; }
        public int Threads { get; set; }
        public double MaxFrequency { get; set; }
    }

    public class GpuInfo
    {
        public string Name { get; set; } = string.Empty;
        public double MemoryGB { get; set; }
        public string DriverVersion { get; set; } = string.Empty;
    }

    public class TunnelConfig
    {
        public string ServerAddress { get; set; } = string.Empty;
        public int ServerPort { get; set; }
        public int LocalPort { get; set; }
        public int RemotePort { get; set; }
        public string Subdomain { get; set; } = string.Empty;
        public string AuthToken { get; set; } = string.Empty;
    }

    public class WebServerConfig
    {
        public int Port { get; set; } = 8080;
        public string RootPath { get; set; } = string.Empty;
        public string DefaultPage { get; set; } = "index.html";
        public bool EnableDirectoryBrowsing { get; set; }
    }

    public class AccessLogEntry
    {
        public DateTime Time { get; set; }
        public string IpAddress { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public int StatusCode { get; set; }
    }

    public class AppSettings
    {
        public bool AutoStart { get; set; }
        public bool MinimizeToTray { get; set; } = true;
        public bool StartMinimized { get; set; }
        public bool AutoStartOllama { get; set; } = true;
        public string OllamaPath { get; set; } = string.Empty;
        public int OllamaPort { get; set; } = 11434;
        public string DefaultModel { get; set; } = string.Empty;
        public bool EnableScreenOff { get; set; }
    }
}
