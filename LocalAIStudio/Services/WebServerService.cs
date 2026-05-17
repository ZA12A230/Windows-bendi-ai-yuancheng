using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LocalAIStudio.Services
{
    public class WebServerService
    {
        private static readonly Lazy<WebServerService> _instance = 
            new Lazy<WebServerService>(() => new WebServerService());
        public static WebServerService Instance => _instance.Value;

        private readonly Dictionary<string, HttpListener> _listeners = new Dictionary<string, HttpListener>();
        private readonly Dictionary<string, WebsiteInfo> _websites = new Dictionary<string, WebsiteInfo>();
        private readonly string _websitesDataPath;
        private CancellationTokenSource? _cts;

        public event EventHandler<WebsiteInfo>? WebsiteStarted;
        public event EventHandler<WebsiteInfo>? WebsiteStopped;
        public event EventHandler<WebsiteInfo>? WebsiteAdded;
        public event EventHandler<WebsiteInfo>? WebsiteRemoved;
        public event EventHandler<string>? AccessLogReceived;

        public IReadOnlyDictionary<string, WebsiteInfo> Websites => _websites;
        public ServerConfig Config { get; private set; } = new ServerConfig();

        public WebServerService()
        {
            _websitesDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "websites.json");
            LoadWebsites();
        }

        public WebsiteInfo AddWebsite(string name, string rootPath, int port)
        {
            var website = new WebsiteInfo(name, rootPath, port);
            _websites[website.Id] = website;
            SaveWebsites();
            WebsiteAdded?.Invoke(this, website);
            return website;
        }

        public void RemoveWebsite(string websiteId)
        {
            if (_websites.TryGetValue(websiteId, out var website))
            {
                StopWebsite(websiteId);
                _websites.Remove(websiteId);
                SaveWebsites();
                WebsiteRemoved?.Invoke(this, website);
            }
        }

        public void UpdateWebsite(WebsiteInfo website)
        {
            if (_websites.ContainsKey(website.Id))
            {
                _websites[website.Id] = website;
                SaveWebsites();
            }
        }

        public WebsiteInfo? GetWebsite(string websiteId)
        {
            return _websites.TryGetValue(websiteId, out var website) ? website : null;
        }

        public async Task<bool> StartWebsite(string websiteId)
        {
            if (!_websites.TryGetValue(websiteId, out var website))
                return false;

            if (website.IsRunning)
                return true;

            try
            {
                var listener = new HttpListener();
                listener.Prefixes.Add($"http://localhost:{website.Port}/");
                listener.Prefixes.Add($"http://127.0.0.1:{website.Port}/");
                listener.Prefixes.Add($"http://+:{website.Port}/");

                listener.Start();
                _listeners[websiteId] = listener;
                website.IsRunning = true;

                _cts = new CancellationTokenSource();
                _ = Task.Run(() => ListenLoop(websiteId, listener, _cts.Token));

                SaveWebsites();
                WebsiteStarted?.Invoke(this, website);
                LogAccess($"Website '{website.Name}' started on port {website.Port}");

                return true;
            }
            catch (Exception ex)
            {
                LogAccess($"Failed to start website '{website.Name}': {ex.Message}");
                return false;
            }
        }

        public async Task StopWebsite(string websiteId)
        {
            if (!_websites.TryGetValue(websiteId, out var website))
                return;

            try
            {
                if (_listeners.TryGetValue(websiteId, out var listener))
                {
                    listener.Stop();
                    listener.Close();
                    _listeners.Remove(websiteId);
                }

                website.IsRunning = false;
                SaveWebsites();
                WebsiteStopped?.Invoke(this, website);
                LogAccess($"Website '{website.Name}' stopped");
            }
            catch (Exception ex)
            {
                LogAccess($"Failed to stop website '{website.Name}': {ex.Message}");
            }
        }

        public async Task StartAllWebsites()
        {
            foreach (var website in _websites.Values.Where(w => !w.IsRunning))
            {
                await StartWebsite(website.Id);
            }
        }

        public async Task StopAllWebsites()
        {
            foreach (var websiteId in _listeners.Keys.ToList())
            {
                await StopWebsite(websiteId);
            }
        }

        private async Task ListenLoop(string websiteId, HttpListener listener, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && listener.IsListening)
            {
                try
                {
                    var context = await listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequest(websiteId, context), ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LogAccess($"Listener error: {ex.Message}");
                }
            }
        }

        private async Task HandleRequest(string websiteId, HttpListenerContext context)
        {
            try
            {
                var request = context.Request;
                var response = context.Response;

                if (!_websites.TryGetValue(websiteId, out var website))
                {
                    SendErrorResponse(response, "Website not found", 404);
                    return;
                }

                var urlPath = request.Url.AbsolutePath;
                if (urlPath == "/")
                    urlPath = "/index.html";

                var localPath = Path.Combine(website.RootPath, urlPath.TrimStart('/'));
                localPath = Path.GetFullPath(localPath);

                if (!localPath.StartsWith(Path.GetFullPath(website.RootPath)))
                {
                    SendErrorResponse(response, "Forbidden", 403);
                    return;
                }

                if (File.Exists(localPath))
                {
                    await ServeFile(response, localPath);
                    website.AccessCount++;
                    website.LastAccessTime = DateTime.Now;
                }
                else if (Directory.Exists(localPath) && Config.EnableDirectoryBrowsing)
                {
                    await ServeDirectoryListing(response, localPath, urlPath);
                }
                else
                {
                    SendErrorResponse(response, "Not Found", 404);
                }

                LogAccess($"Served: {urlPath}");
            }
            catch (Exception ex)
            {
                LogAccess($"Request error: {ex.Message}");
                SendErrorResponse(context.Response, "Internal Server Error", 500);
            }
        }

        private async Task ServeFile(HttpListenerResponse response, string filePath)
        {
            try
            {
                var ext = Path.GetExtension(filePath).ToLower();
                response.ContentType = GetContentType(ext);
                response.AddHeader("Access-Control-Allow-Origin", Config.CorsOrigin);

                var buffer = await File.ReadAllBytesAsync(filePath);
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                response.Close();
            }
            catch (Exception ex)
            {
                LogAccess($"Serve file error: {ex.Message}");
                SendErrorResponse(response, "Error reading file", 500);
            }
        }

        private async Task ServeDirectoryListing(HttpListenerResponse response, string dirPath, string urlPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html><head>");
            sb.AppendLine("<meta charset='utf-8'>");
            sb.AppendLine($"<title>Index of {urlPath}</title>");
            sb.AppendLine("<style>body{font-family:Arial;margin:20px}a{color:#0066cc}</style>");
            sb.AppendLine("</head><body>");
            sb.AppendLine($"<h1>Index of {urlPath}</h1><ul>");

            if (urlPath != "/")
            {
                var parentPath = urlPath.TrimEnd('/');
                var lastSlash = parentPath.LastIndexOf('/');
                sb.AppendLine($"<li><a href='{(lastSlash > 0 ? parentPath.Substring(0, lastSlash) : "/") }'>..</a></li>");
            }

            foreach (var dir in Directory.GetDirectories(dirPath).OrderBy(d => d))
                sb.AppendLine($"<li>📁 <a href='{urlPath.TrimEnd('/')}/{Path.GetFileName(dir)}/'>{Path.GetFileName(dir)}/</a></li>");

            foreach (var file in Directory.GetFiles(dirPath).OrderBy(f => f))
                sb.AppendLine($"<li>📄 <a href='{urlPath.TrimEnd('/')}/{Path.GetFileName(file)}'>{Path.GetFileName(file)}</a></li>");

            sb.AppendLine("</ul></body></html>");

            var buffer = Encoding.UTF8.GetBytes(sb.ToString());
            response.ContentType = "text/html; charset=utf-8";
            response.AddHeader("Access-Control-Allow-Origin", Config.CorsOrigin);
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.Close();
        }

        private void SendErrorResponse(HttpListenerResponse response, string message, int statusCode)
        {
            try
            {
                var html = $"<!DOCTYPE html><html><head><title>{statusCode} {message}</title></head>" +
                          $"<body><h1>{statusCode} {message}</h1></body></html>";
                var buffer = Encoding.UTF8.GetBytes(html);
                response.StatusCode = statusCode;
                response.ContentType = "text/html; charset=utf-8";
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.Close();
            }
            catch { }
        }

        private string GetContentType(string ext)
        {
            return ext switch
            {
                ".html" or ".htm" => "text/html; charset=utf-8",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".json" => "application/json",
                ".xml" => "application/xml",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".svg" => "image/svg+xml",
                ".ico" => "image/x-icon",
                ".pdf" => "application/pdf",
                ".zip" => "application/zip",
                ".txt" => "text/plain",
                _ => "application/octet-stream"
            };
        }

        private void SaveWebsites()
        {
            try
            {
                var json = JsonSerializer.Serialize(_websites.Values.ToList(), new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_websitesDataPath, json);
            }
            catch (Exception ex)
            {
                LogAccess($"Save error: {ex.Message}");
            }
        }

        private void LoadWebsites()
        {
            try
            {
                if (File.Exists(_websitesDataPath))
                {
                    var json = File.ReadAllText(_websitesDataPath);
                    var websites = JsonSerializer.Deserialize<List<WebsiteInfo>>(json);
                    if (websites != null)
                    {
                        foreach (var website in websites)
                        {
                            _websites[website.Id] = website;
                            website.IsRunning = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogAccess($"Load error: {ex.Message}");
            }
        }

        public string GetWebsitesFolder()
        {
            var folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Websites");
            Directory.CreateDirectory(folder);
            return folder;
        }

        public async Task<string> UploadFiles(string websiteId, string sourceFolder)
        {
            if (!_websites.TryGetValue(websiteId, out var website))
                return "Website not found";

            try
            {
                if (!Directory.Exists(sourceFolder))
                    return "Source folder not found";

                if (!Directory.Exists(website.RootPath))
                    Directory.CreateDirectory(website.RootPath);

                await CopyDirectoryAsync(sourceFolder, website.RootPath);
                return "Files uploaded successfully";
            }
            catch (Exception ex)
            {
                return $"Upload failed: {ex.Message}";
            }
        }

        private async Task CopyDirectoryAsync(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = Path.Combine(destDir, Path.GetFileName(file));
                await Task.Run(() => File.Copy(file, destFile, true));
            }
            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
                await CopyDirectoryAsync(dir, destSubDir);
            }
        }

        private void LogAccess(string message)
        {
            var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [WebServer] {message}";
            Debug.WriteLine(logMessage);
            AccessLogReceived?.Invoke(this, logMessage);
        }
    }
}
