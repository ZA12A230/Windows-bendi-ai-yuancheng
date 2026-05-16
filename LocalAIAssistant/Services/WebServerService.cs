using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LocalAIAssistant.Models;

namespace LocalAIAssistant.Services
{
    public class WebServerService
    {
        private HttpListener? _httpListener;
        private CancellationTokenSource? _cancellationTokenSource;
        private WebServerConfig _config = new();
        private bool _isRunning;

        public event EventHandler<AccessLogEntry>? OnAccessLog;

        public async Task StartAsync(WebServerConfig config)
        {
            if (_isRunning)
            {
                throw new InvalidOperationException("Server is already running");
            }

            _config = config;
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add($"http://+:{config.Port}/");

            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                _httpListener.Start();
                _isRunning = true;

                _ = Task.Run(() => ListenAsync(_cancellationTokenSource.Token));
            }
            catch (HttpListenerException ex)
            {
                if (ex.ErrorCode == 5)
                {
                    throw new Exception("需要管理员权限来绑定端口。请以管理员身份运行程序，或执行: netsh http add urlacl url=http://+:{config.Port}/ user=Everyone");
                }
                throw;
            }
        }

        public async Task StopAsync()
        {
            _cancellationTokenSource?.Cancel();
            _httpListener?.Stop();
            _httpListener?.Close();
            _isRunning = false;
        }

        private async Task ListenAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _httpListener != null && _httpListener.IsListening)
            {
                try
                {
                    var context = await _httpListener.GetContextAsync();
                    _ = ProcessRequestAsync(context, token);
                }
                catch (HttpListenerException) { }
                catch (OperationCanceledException) { }
            }
        }

        private async Task ProcessRequestAsync(HttpListenerContext context, CancellationToken token)
        {
            var request = context.Request;
            var response = context.Response;

            var logEntry = new AccessLogEntry
            {
                Time = DateTime.Now,
                IpAddress = request.RemoteEndPoint?.Address.ToString() ?? "Unknown",
                Method = request.HttpMethod,
                Path = request.Url?.AbsolutePath ?? "/",
                StatusCode = 200
            };

            try
            {
                var path = request.Url?.AbsolutePath ?? "/";
                if (path.StartsWith("/")) path = path.Substring(1);

                var rootPath = string.IsNullOrEmpty(_config.RootPath)
                    ? AppDomain.CurrentDomain.BaseDirectory
                    : _config.RootPath;

                var filePath = Path.Combine(rootPath, path);

                if (Directory.Exists(filePath))
                {
                    if (_config.EnableDirectoryBrowsing)
                    {
                        await SendDirectoryListingAsync(response, filePath, path);
                        logEntry.StatusCode = 200;
                    }
                    else
                    {
                        filePath = Path.Combine(filePath, _config.DefaultPage);
                        if (File.Exists(filePath))
                        {
                            await SendFileAsync(response, filePath);
                            logEntry.StatusCode = 200;
                        }
                        else
                        {
                            await SendErrorAsync(response, 403, "Forbidden");
                            logEntry.StatusCode = 403;
                        }
                    }
                }
                else if (File.Exists(filePath))
                {
                    await SendFileAsync(response, filePath);
                    logEntry.StatusCode = 200;
                }
                else
                {
                    await SendErrorAsync(response, 404, "Not Found");
                    logEntry.StatusCode = 404;
                }
            }
            catch (Exception)
            {
                await SendErrorAsync(response, 500, "Internal Server Error");
                logEntry.StatusCode = 500;
            }
            finally
            {
                OnAccessLog?.Invoke(this, logEntry);
                response.Close();
            }
        }

        private async Task SendFileAsync(HttpListenerResponse response, string filePath)
        {
            var contentType = GetContentType(filePath);
            response.ContentType = contentType;
            response.StatusCode = 200;

            var bytes = await File.ReadAllBytesAsync(filePath);
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        }

        private async Task SendDirectoryListingAsync(HttpListenerResponse response, string directoryPath, string virtualPath)
        {
            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html><head><title>Directory Listing</title>");
            html.AppendLine("<style>body{font-family:Arial,sans-serif;margin:20px;} a{color:#0066cc;text-decoration:none;} a:hover{text-decoration:underline;} li{margin:5px 0;}</style>");
            html.AppendLine("</head><body>");
            html.AppendLine($"<h1>Directory: /{virtualPath}</h1>");
            html.AppendLine("<ul>");

            if (!string.IsNullOrEmpty(virtualPath))
            {
                html.AppendLine("<li><a href=\"../\">../</a></li>");
            }

            foreach (var dir in Directory.GetDirectories(directoryPath))
            {
                var name = Path.GetFileName(dir);
                html.AppendLine($"<li><a href=\"{name}/\">{name}/</a></li>");
            }

            foreach (var file in Directory.GetFiles(directoryPath))
            {
                var name = Path.GetFileName(file);
                html.AppendLine($"<li><a href=\"{name}\">{name}</a></li>");
            }

            html.AppendLine("</ul></body></html>");

            response.ContentType = "text/html; charset=utf-8";
            response.StatusCode = 200;

            var bytes = Encoding.UTF8.GetBytes(html.ToString());
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        }

        private async Task SendErrorAsync(HttpListenerResponse response, int statusCode, string message)
        {
            response.StatusCode = statusCode;
            response.ContentType = "text/html; charset=utf-8";

            var html = $"<!DOCTYPE html><html><body><h1>{statusCode} - {message}</h1></body></html>";
            var bytes = Encoding.UTF8.GetBytes(html);
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        }

        private string GetContentType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".html" or ".htm" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".json" => "application/json",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".svg" => "image/svg+xml",
                ".ico" => "image/x-icon",
                ".pdf" => "application/pdf",
                ".zip" => "application/zip",
                ".xml" => "application/xml",
                ".txt" => "text/plain",
                _ => "application/octet-stream"
            };
        }
    }
}
