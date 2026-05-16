using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace LocalAIAssistant.Services
{
    public class RemoteDesktopService
    {
        private TcpListener? _tcpListener;
        private TcpClient? _client;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isServerRunning;
        private string? _connectionCode;

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hDestDC, int x, int y, int nWidth, int nHeight, IntPtr hSrcDC, int xSrc, int ySrc, int dwRop);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hDC);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        private const int SRCCOPY = 0x00CC0020;

        public async Task<(string Address, string Code)> StartServerAsync()
        {
            if (_isServerRunning)
            {
                throw new InvalidOperationException("Server is already running");
            }

            _connectionCode = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
            _tcpListener = new TcpListener(IPAddress.Any, 5900);
            _tcpListener.Start();
            _isServerRunning = true;

            _cancellationTokenSource = new CancellationTokenSource();

            _ = Task.Run(async () =>
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        var client = await _tcpListener.AcceptTcpClientAsync();
                        _client = client;
                        _ = HandleClientAsync(client, _cancellationTokenSource.Token);
                    }
                    catch { }
                }
            }, _cancellationTokenSource.Token);

            var localIp = GetLocalIpAddress();
            return ($"{localIp}:5900", _connectionCode);
        }

        public async Task StopServerAsync()
        {
            _cancellationTokenSource?.Cancel();
            _client?.Close();
            _tcpListener?.Stop();
            _isServerRunning = false;
        }

        public async Task<bool> ConnectAsync(string remoteHost)
        {
            try
            {
                var parts = remoteHost.Split(':');
                var host = parts[0];
                var port = parts.Length > 1 ? int.Parse(parts[1]) : 5900;

                _client = new TcpClient();
                await _client.ConnectAsync(host, port);
                return _client.Connected;
            }
            catch
            {
                return false;
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            var stream = client.GetStream();

            while (!token.IsCancellationRequested && client.Connected)
            {
                try
                {
                    var screenshot = CaptureScreen();
                    var imageData = ImageToBytes(screenshot);

                    var lengthBytes = BitConverter.GetBytes(imageData.Length);
                    await stream.WriteAsync(lengthBytes, 0, 4, token);
                    await stream.WriteAsync(imageData, 0, imageData.Length, token);

                    await Task.Delay(100, token);
                }
                catch { break; }
            }
        }

        private Bitmap CaptureScreen()
        {
            var screenWidth = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width;
            var screenHeight = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height;

            var bitmap = new Bitmap(screenWidth, screenHeight);

            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(0, 0, 0, 0, new System.Drawing.Size(screenWidth, screenHeight));
            }

            return bitmap;
        }

        private byte[] ImageToBytes(Bitmap bitmap)
        {
            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Jpeg);
            return ms.ToArray();
        }

        private string GetLocalIpAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            return "127.0.0.1";
        }
    }
}
