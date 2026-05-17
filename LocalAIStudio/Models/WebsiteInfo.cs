using System;
using System.Collections.Generic;

namespace LocalAIStudio.Models
{
    public class WebsiteInfo
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public string RootPath { get; set; } = "";
        public int Port { get; set; } = 8080;
        public bool IsRunning { get; set; } = false;
        public string CustomDomain { get; set; }
        public DateTime CreatedTime { get; set; } = DateTime.Now;
        public DateTime LastAccessTime { get; set; }
        public int AccessCount { get; set; } = 0;

        public string LocalUrl => $"http://localhost:{Port}";
        public string LocalNetworkUrl => $"http://0.0.0.0:{Port}";
        public string PublicUrl => !string.IsNullOrEmpty(CustomDomain) 
            ? $"http://{CustomDomain}.aiyuancheng.com:{Port}" 
            : "";

        public WebsiteInfo()
        {
        }

        public WebsiteInfo(string name, string rootPath, int port)
        {
            Name = name;
            RootPath = rootPath;
            Port = port;
        }
    }

    public class ServerConfig
    {
        public bool EnableCors { get; set; } = true;
        public string CorsOrigin { get; set; } = "*";
        public bool EnableDirectoryBrowsing { get; set; } = true;
        public Dictionary<string, string> CustomHostMappings { get; set; } = new Dictionary<string, string>();
    }
}
