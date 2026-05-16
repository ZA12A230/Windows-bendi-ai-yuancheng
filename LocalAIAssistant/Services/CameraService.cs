using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LocalAIAssistant.Services
{
    public class CameraService
    {
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isRunning;
        private string? _remoteAccessUrl;

        [DllImport("avicap32.dll")]
        private static extern IntPtr capCreateCaptureWindow(string lpszWindowName, int dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, int nID);

        [DllImport("user32.dll")]
        private static extern bool SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);

        private const int WM_CAP_DRIVER_CONNECT = 0x40A;
        private const int WM_CAP_DRIVER_DISCONNECT = 0x40B;
        private const int WM_CAP_EDIT_COPY = 0x41E;
        private const int WM_CAP_GRAB_FRAME = 0x43C;
        private const int WS_CHILD = 0x40000000;
        private const int WS_VISIBLE = 0x10000000;

        private IntPtr _captureWindow = IntPtr.Zero;

        public async Task<System.Collections.Generic.List<string>> GetAvailableCamerasAsync()
        {
            var cameras = new System.Collections.Generic.List<string>();
            for (int i = 0; i < 10; i++)
            {
                cameras.Add($"摄像头 {i}");
            }
            return cameras;
        }

        public async Task StartCameraAsync(string cameraName, Action<ImageSource> frameCallback)
        {
            if (_isRunning) return;

            _cancellationTokenSource = new CancellationTokenSource();
            _isRunning = true;

            await Task.Run(() =>
            {
                _captureWindow = capCreateCaptureWindow("CameraWindow", WS_CHILD | WS_VISIBLE, 0, 0, 640, 480, IntPtr.Zero, 0);

                if (SendMessage(_captureWindow, WM_CAP_DRIVER_CONNECT, 0, 0))
                {
                    while (!_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        try
                        {
                            SendMessage(_captureWindow, WM_CAP_GRAB_FRAME, 0, 0);
                            SendMessage(_captureWindow, WM_CAP_EDIT_COPY, 0, 0);

                            if (Clipboard.ContainsImage())
                            {
                                var bitmap = Clipboard.GetImage() as Bitmap;
                                if (bitmap != null)
                                {
                                    var bitmapSource = ConvertToBitmapSource(bitmap);
                                    bitmapSource.Freeze();
                                    frameCallback?.Invoke(bitmapSource);
                                }
                            }
                        }
                        catch { }

                        Thread.Sleep(33);
                    }
                }
            }, _cancellationTokenSource.Token);
        }

        public async Task StopCameraAsync()
        {
            if (!_isRunning) return;

            _cancellationTokenSource?.Cancel();

            if (_captureWindow != IntPtr.Zero)
            {
                SendMessage(_captureWindow, WM_CAP_DRIVER_DISCONNECT, 0, 0);
                DestroyWindow(_captureWindow);
                _captureWindow = IntPtr.Zero;
            }

            _isRunning = false;
        }

        public async Task<string> EnableRemoteAccessAsync()
        {
            _remoteAccessUrl = $"http://localhost:8080/camera/{Guid.NewGuid():N}";
            return _remoteAccessUrl;
        }

        public async Task DisableRemoteAccessAsync()
        {
            _remoteAccessUrl = null;
        }

        private BitmapSource ConvertToBitmapSource(Bitmap bitmap)
        {
            var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var bitmapData = bitmap.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);

            try
            {
                var bitmapSource = BitmapSource.Create(
                    bitmap.Width,
                    bitmap.Height,
                    96,
                    96,
                    PixelFormats.Bgra32,
                    null,
                    bitmapData.Scan0,
                    bitmapData.Stride * bitmap.Height,
                    bitmapData.Stride);

                return bitmapSource;
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }
        }
    }
}
