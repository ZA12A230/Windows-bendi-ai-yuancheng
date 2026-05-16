using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using LocalAIStudio.Services;

namespace LocalAIStudio.Services
{
    public class RemoteAccessApiServer
    {
        #region Singleton
        private static readonly Lazy<RemoteAccessApiServer> _instance = 
            new Lazy<RemoteAccessApiServer>(() => new RemoteAccessApiServer());
        public static RemoteAccessApiServer Instance => _instance.Value;
        #endregion

        private HttpListener? _httpListener;
        private CancellationTokenSource? _cts;
        private bool _isRunning = false;
        private string? _currentUsername;
        private string? _currentPasswordHash;

        public event EventHandler? ApiServerStarted;
        public event EventHandler? ApiServerStopped;
        public event EventHandler<string>? AccessLogReceived;

        public bool IsRunning => _isRunning;
        public int Port { get; private set; } = 8080;

        public RemoteAccessApiServer()
        {
        }

        #region Server Control
        public async Task StartServer(int port = 8080)
        {
            if (_isRunning) return;

            Port = port;
            _cts = new CancellationTokenSource();
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add($"http://localhost:{port}/");
            _httpListener.Prefixes.Add($"http://127.0.0.1:{port}/");

            try
            {
                _httpListener.Start();
                _isRunning = true;
                LogAccess("API server started on port " + port);
                ApiServerStarted?.Invoke(this, EventArgs.Empty);

                HardwareMonitorService.Instance.StartRemoteAccessMonitoring();

                _ = Task.Run(() => ListenLoop(_cts.Token));
            }
            catch (Exception ex)
            {
                LogAccess($"Failed to start server: {ex.Message}");
            }
        }

        public async Task StopServer()
        {
            if (!_isRunning) return;

            try
            {
                _cts?.Cancel();
                _httpListener?.Stop();
                _isRunning = false;
                HardwareMonitorService.Instance.StopRemoteAccessMonitoring();
                ApiServerStopped?.Invoke(this, EventArgs.Empty);
                LogAccess("API server stopped");
            }
            catch (Exception ex)
            {
                LogAccess($"Stop server error: {ex.Message}");
            }
        }

        private async Task ListenLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _httpListener != null && _httpListener.IsListening)
            {
                try
                {
                    var context = await _httpListener.GetContextAsync();
                    _ = Task.Run(() => HandleRequestAsync(context), ct);
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
        #endregion

        #region Authentication
        public void SetCredentials(string username, string password)
        {
            _currentUsername = username;
            _currentPasswordHash = ComputeHash(password);
        }

        private bool Authenticate(HttpListenerRequest request)
        {
            if (string.IsNullOrEmpty(_currentUsername) || string.IsNullOrEmpty(_currentPasswordHash))
            {
                return true;
            }

            var query = HttpUtility.ParseQueryString(request.Url.Query);
            var username = query["username"];
            var password = query["password"];

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                return username == _currentUsername && ComputeHash(password) == _currentPasswordHash;
            }

            var authHeader = request.Headers["Authorization"];
            if (!string.IsNullOrEmpty(authHeader))
            {
                var parts = authHeader.Split(' ');
                if (parts.Length == 2 && parts[0].ToLower() == "basic")
                {
                    var credentials = Encoding.UTF8.GetString(Convert.FromBase64String(parts[1]));
                    var credParts = credentials.Split(':');
                    if (credParts.Length == 2)
                    {
                        return credParts[0] == _currentUsername && ComputeHash(credParts[1]) == _currentPasswordHash;
                    }
                }
            }

            return false;
        }

        private string ComputeHash(string input)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                return Convert.ToBase64String(bytes);
            }
        }
        #endregion

        #region Request Handler
        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            try
            {
                var request = context.Request;
                var response = context.Response;

                LogAccess($"Request: {request.HttpMethod} {request.Url.PathAndQuery}");

                response.AddHeader("Access-Control-Allow-Origin", "*");
                response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                response.AddHeader("Access-Control-Allow-Headers", "Content-Type, Authorization");

                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 200;
                    response.Close();
                    return;
                }

                if (!Authenticate(request))
                {
                    SendErrorResponse(response, "Unauthorized", 401);
                    return;
                }

                await HandleRouteAsync(request, response);
            }
            catch (Exception ex)
            {
                LogAccess($"Handler error: {ex.Message}");
                SendErrorResponse(context.Response, "Internal Server Error", 500);
            }
        }

        private async Task HandleRouteAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            var path = request.Url.AbsolutePath;
            var pathLower = path.ToLower();

            switch (pathLower)
            {
                case "/api/health":
                    SendJsonResponse(response, new { status = "ok" });
                    break;

                case "/api/camera/snapshot":
                    await HandleCameraSnapshot(request, response);
                    break;

                case "/api/camera/stream":
                    await HandleCameraStream(request, response);
                    break;

                case "/api/intercom/start":
                    HandleIntercomStart(response);
                    break;

                case "/api/intercom/stop":
                    HandleIntercomStop(response);
                    break;

                case "/api/intercom/play":
                    await HandleIntercomPlay(request, response);
                    break;

                case "/api/audio/play":
                    await HandleAudioPlay(request, response);
                    break;

                case "/api/wifi":
                    HandleGetWifiInfo(response);
                    break;

                case "/api/devices/usb":
                    HandleGetUsbDevices(response);
                    break;

                case "/api/devices/all":
                    HandleGetAllDevices(response);
                    break;

                case "/api/status":
                    HandleGetStatus(response);
                    break;

                default:
                    SendErrorResponse(response, "Not Found", 404);
                    break;
            }
        }
        #endregion

        #region Camera API Endpoints
        private async Task HandleCameraSnapshot(HttpListenerRequest request, HttpListenerResponse response)
        {
            var frame = HardwareMonitorService.Instance.CaptureFrame();
            if (frame != null)
            {
                response.ContentType = "image/jpeg";
                response.ContentLength64 = frame.Length;
                await response.OutputStream.WriteAsync(frame, 0, frame.Length);
            }
            else
            {
                SendErrorResponse(response, "Camera capture failed", 500);
            }
            response.Close();
        }

        private async Task HandleCameraStream(HttpListenerRequest request, HttpListenerResponse response)
        {
            response.ContentType = "multipart/x-mixed-replace; boundary=frame";
            response.AddHeader("Cache-Control", "no-cache");

            try
            {
                var boundary = Encoding.UTF8.GetBytes("\r\n--frame\r\n");
                var contentTypeHeader = Encoding.UTF8.GetBytes("Content-Type: image/jpeg\r\n\r\n");

                while (response.IsClientConnected && IsRunning)
                {
                    var frame = HardwareMonitorService.Instance.CaptureFrame();
                    if (frame != null)
                    {
                        await response.OutputStream.WriteAsync(boundary, 0, boundary.Length);
                        await response.OutputStream.WriteAsync(contentTypeHeader, 0, contentTypeHeader.Length);
                        await response.OutputStream.WriteAsync(frame, 0, frame.Length);
                        await response.OutputStream.FlushAsync();
                    }
                    await Task.Delay(100);
                }
            }
            catch
            {
            }
            response.Close();
        }
        #endregion

        #region Intercom API Endpoints（对讲功能）
        private void HandleIntercomStart(HttpListenerResponse response)
        {
            try
            {
                HardwareMonitorService.Instance.StartIntercom();
                SendJsonResponse(response, new { success = true, message = "对讲已启动" });
            }
            catch (Exception ex)
            {
                SendErrorResponse(response, $"启动对讲失败: {ex.Message}", 500);
            }
        }

        private void HandleIntercomStop(HttpListenerResponse response)
        {
            try
            {
                HardwareMonitorService.Instance.StopIntercom();
                SendJsonResponse(response, new { success = true, message = "对讲已停止" });
            }
            catch (Exception ex)
            {
                SendErrorResponse(response, $"停止对讲失败: {ex.Message}", 500);
            }
        }

        private async Task HandleIntercomPlay(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (request.HttpMethod == "POST" && request.HasEntityBody)
            {
                try
                {
                    using (var ms = new MemoryStream())
                    {
                        await request.InputStream.CopyToAsync(ms);
                        var audioData = ms.ToArray();

                        await HardwareMonitorService.Instance.PlayRemoteAudio(audioData);
                        SendJsonResponse(response, new { success = true, message = "音频已播放" });
                    }
                }
                catch (Exception ex)
                {
                    SendErrorResponse(response, $"播放音频失败: {ex.Message}", 500);
                }
            }
            else
            {
                SendErrorResponse(response, "Method Not Allowed", 405);
            }
        }

        private async Task HandleAudioPlay(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (request.HttpMethod == "POST" && request.HasEntityBody)
            {
                try
                {
                    using (var ms = new MemoryStream())
                    {
                        await request.InputStream.CopyToAsync(ms);
                        var audioData = ms.ToArray();

                        await HardwareMonitorService.Instance.PlayRemoteAudioWav(audioData);
                        SendJsonResponse(response, new { success = true });
                    }
                }
                catch (Exception ex)
                {
                    SendErrorResponse(response, $"播放音频失败: {ex.Message}", 500);
                }
            }
            else
            {
                SendErrorResponse(response, "Method Not Allowed", 405);
            }
        }
        #endregion

        #region System Info API Endpoints
        private void HandleGetWifiInfo(HttpListenerResponse response)
        {
            var wifiList = HardwareMonitorService.Instance.GetWifiInfo();
            SendJsonResponse(response, new { success = true, data = wifiList });
        }

        private void HandleGetUsbDevices(HttpListenerResponse response)
        {
            var devices = HardwareMonitorService.Instance.GetUsbDevices();
            SendJsonResponse(response, new { success = true, data = devices });
        }

        private void HandleGetAllDevices(HttpListenerResponse response)
        {
            var devices = HardwareMonitorService.Instance.GetAllSystemDevices();
            SendJsonResponse(response, new { success = true, data = devices });
        }

        private void HandleGetStatus(HttpListenerResponse response)
        {
            var status = new
            {
                cameraActive = HardwareMonitorService.Instance.IsCameraActive,
                intercomActive = HardwareMonitorService.Instance.IsIntercomActive,
                remoteActive = HardwareMonitorService.Instance.IsRemoteAccessActive,
                serverActive = IsRunning
            };
            SendJsonResponse(response, new { success = true, data = status });
        }
        #endregion

        #region Response Helpers
        private void SendJsonResponse(HttpListenerResponse response, object data)
        {
            var json = JsonSerializer.Serialize(data);
            var buffer = Encoding.UTF8.GetBytes(json);
            response.ContentType = "application/json";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.Close();
        }

        private void SendErrorResponse(HttpListenerResponse response, string message, int statusCode = 400)
        {
            response.StatusCode = statusCode;
            var error = new { success = false, error = message };
            var json = JsonSerializer.Serialize(error);
            var buffer = Encoding.UTF8.GetBytes(json);
            response.ContentType = "application/json";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.Close();
        }
        #endregion

        #region Logging
        private void LogAccess(string message)
        {
            var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            System.Diagnostics.Debug.WriteLine(logMessage);
            AccessLogReceived?.Invoke(this, logMessage);
        }
        #endregion
    }
}
