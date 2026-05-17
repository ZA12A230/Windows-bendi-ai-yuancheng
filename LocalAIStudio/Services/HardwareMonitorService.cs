using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace LocalAIStudio.Services
{
    public class HardwareMonitorService
    {
        #region Singleton
        private static readonly Lazy<HardwareMonitorService> _instance = 
            new Lazy<HardwareMonitorService>(() => new HardwareMonitorService());
        public static HardwareMonitorService Instance => _instance.Value;
        #endregion

        #region DLL Imports
        [DllImport("avicap32.dll", EntryPoint = "capCreateCaptureWindowA")]
        private static extern IntPtr capCreateCaptureWindow(string lpszWindowName, int dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, int nID);

        [DllImport("avicap32.dll")]
        private static extern bool capDriverConnect(IntPtr hWnd, int i);

        [DllImport("avicap32.dll")]
        private static extern bool capDriverDisconnect(IntPtr hWnd);

        [DllImport("avicap32.dll")]
        private static extern bool capEditCopy(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);
        #endregion

        private IntPtr _captureWindow = IntPtr.Zero;
        private bool _cameraActive = false;
        private bool _isMonitoring = false;
        private Thread? _captureThread;
        private byte[]? _lastFrame;
        private DateTime _lastFrameTime;
        
        private WaveOutEvent? _waveOut;
        private WaveFileReader? _waveReader;
        private BufferedWaveProvider? _bufferedWaveProvider;
        private bool _intercomActive = false;

        public event EventHandler<bool>? CameraStateChanged;
        public event EventHandler<bool>? MicrophoneActiveChanged;
        public event EventHandler<bool>? IntercomStateChanged;
        public event EventHandler? RemoteAccessStarted;
        public event EventHandler? RemoteAccessStopped;
        public event EventHandler<byte[]>? AudioDataReceived;

        public bool IsCameraActive => _cameraActive;
        public bool IsRemoteAccessActive => _isMonitoring;
        public bool IsIntercomActive => _intercomActive;

        public HardwareMonitorService()
        {
        }

        #region Camera Functions
        
        public bool InitializeCamera()
        {
            try
            {
                if (_captureWindow != IntPtr.Zero)
                {
                    DestroyWindow(_captureWindow);
                    _captureWindow = IntPtr.Zero;
                }

                _captureWindow = capCreateCaptureWindow("Capture", 0, 0, 0, 640, 480, IntPtr.Zero, 0);

                if (_captureWindow != IntPtr.Zero)
                {
                    if (capDriverConnect(_captureWindow, 0))
                    {
                        _cameraActive = true;
                        CameraStateChanged?.Invoke(this, true);
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Initialize camera error: {ex.Message}");
                return false;
            }
        }

        public byte[]? CaptureFrame()
        {
            try
            {
                if (!_cameraActive || _captureWindow == IntPtr.Zero)
                {
                    if (!InitializeCamera())
                    {
                        return CaptureScreenShot();
                    }
                }

                if (capEditCopy(_captureWindow))
                {
                    var dataObj = System.Windows.Forms.Clipboard.GetDataObject();
                    if (dataObj != null && dataObj.GetDataPresent(System.Windows.Forms.DataFormats.Bitmap))
                    {
                        var bmp = (Bitmap)dataObj.GetData(System.Windows.Forms.DataFormats.Bitmap);
                        if (bmp != null)
                        {
                            using (var ms = new MemoryStream())
                            {
                                bmp.Save(ms, ImageFormat.Jpeg);
                                _lastFrame = ms.ToArray();
                                _lastFrameTime = DateTime.Now;
                                return _lastFrame;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Capture frame error: {ex.Message}");
            }
            return _lastFrame;
        }

        public byte[]? CaptureScreenShot()
        {
            try
            {
                var bounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
                using (var bmp = new Bitmap(bounds.Width, bounds.Height))
                {
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.CopyFromScreen(0, 0, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
                    }
                    using (var ms = new MemoryStream())
                    {
                        bmp.Save(ms, ImageFormat.Jpeg);
                        return ms.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Screenshot error: {ex.Message}");
                return null;
            }
        }

        public void StopCamera()
        {
            try
            {
                if (_captureWindow != IntPtr.Zero)
                {
                    capDriverDisconnect(_captureWindow);
                    DestroyWindow(_captureWindow);
                    _captureWindow = IntPtr.Zero;
                }
                _cameraActive = false;
                CameraStateChanged?.Invoke(this, false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Stop camera error: {ex.Message}");
            }
        }
        #endregion

        #region Intercom Functions（对讲功能 - 远程说话主机发声）
        
        public void StartIntercom()
        {
            try
            {
                if (_intercomActive) return;

                _intercomActive = true;
                
                _bufferedWaveProvider = new BufferedWaveProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2))
                {
                    DiscardOnBufferOverflow = true,
                    BufferDuration = TimeSpan.FromSeconds(5)
                };

                _waveOut = new WaveOutEvent();
                _waveOut.Init(_bufferedWaveProvider);
                _waveOut.Play();

                IntercomStateChanged?.Invoke(this, true);
                MicrophoneActiveChanged?.Invoke(this, true);
                Debug.WriteLine("Intercom started - remote audio will play on host speakers");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Start intercom error: {ex.Message}");
                _intercomActive = false;
            }
        }

        public void StopIntercom()
        {
            try
            {
                _intercomActive = false;

                _waveOut?.Stop();
                _waveOut?.Dispose();
                _waveOut = null;

                _waveReader?.Dispose();
                _waveReader = null;

                _bufferedWaveProvider = null;

                IntercomStateChanged?.Invoke(this, false);
                MicrophoneActiveChanged?.Invoke(this, false);
                Debug.WriteLine("Intercom stopped");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Stop intercom error: {ex.Message}");
            }
        }

        public async Task PlayRemoteAudio(byte[] audioData)
        {
            if (!_intercomActive || _bufferedWaveProvider == null)
            {
                Debug.WriteLine("Intercom not active, audio data ignored");
                return;
            }

            try
            {
                AudioDataReceived?.Invoke(this, audioData);
                _bufferedWaveProvider.AddSamples(audioData, 0, audioData.Length);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Play remote audio error: {ex.Message}");
            }
        }

        public async Task PlayRemoteAudioWav(byte[] wavData)
        {
            try
            {
                using (var ms = new MemoryStream(wavData))
                {
                    var reader = new WaveFileReader(ms);
                    
                    var waveOut = new WaveOutEvent();
                    waveOut.Init(reader);
                    waveOut.Play();

                    while (waveOut.PlaybackState == PlaybackState.Playing)
                    {
                        await Task.Delay(100);
                    }

                    waveOut.Stop();
                    waveOut.Dispose();
                    reader.Dispose();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Play WAV audio error: {ex.Message}");
                await PlayMp3OrOther(wavData);
            }
        }

        private async Task PlayMp3OrOther(byte[] audioData)
        {
            try
            {
                var tempPath = Path.Combine(Path.GetTempPath(), $"temp_audio_{Guid.NewGuid()}.wav");
                await File.WriteAllBytesAsync(tempPath, audioData);

                Process.Start(new ProcessStartInfo
                {
                    FileName = tempPath,
                    UseShellExecute = true
                });

                _ = Task.Delay(30000).ContinueWith(t =>
                {
                    if (File.Exists(tempPath))
                    {
                        try { File.Delete(tempPath); } catch { }
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Play audio error: {ex.Message}");
            }
        }
        #endregion

        #region WiFi Information
        public List<WifiInfo> GetWifiInfo()
        {
            var wifiList = new List<WifiInfo>();

            try
            {
                var process = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = "wlan show interfaces",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var p = Process.Start(process))
                {
                    if (p != null)
                    {
                        string output = p.StandardOutput.ReadToEnd();
                        ParseNetshOutput(output, wifiList);
                    }
                }

                if (wifiList.Count == 0)
                {
                    GetWifiFromWMI(wifiList);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Get WiFi info error: {ex.Message}");
            }

            return wifiList;
        }

        private void ParseNetshOutput(string output, List<WifiInfo> wifiList)
        {
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            WifiInfo? currentWifi = null;

            foreach (var line in lines)
            {
                if (line.Trim().StartsWith("SSID"))
                {
                    if (currentWifi != null) wifiList.Add(currentWifi);
                    currentWifi = new WifiInfo();
                    var parts = line.Split(':');
                    if (parts.Length > 1) currentWifi.SSID = parts[1].Trim();
                }
                else if (line.Trim().StartsWith("Signal") && currentWifi != null)
                {
                    var parts = line.Split(':');
                    if (parts.Length > 1)
                    {
                        if (int.TryParse(parts[1].Trim().TrimEnd('%'), out int signal))
                        {
                            currentWifi.SignalStrength = signal;
                        }
                    }
                }
                else if (line.Trim().StartsWith("State") && currentWifi != null)
                {
                    var parts = line.Split(':');
                    if (parts.Length > 1)
                    {
                        currentWifi.IsConnected = parts[1].Trim().Contains("connected");
                    }
                }
                else if (line.Trim().StartsWith("BSSID") && currentWifi != null)
                {
                    var parts = line.Split(':');
                    if (parts.Length > 1) currentWifi.BSSID = parts[1].Trim();
                }
                else if (line.Trim().StartsWith("Channel") && currentWifi != null)
                {
                    var parts = line.Split(':');
                    if (parts.Length > 1) currentWifi.Channel = parts[1].Trim();
                }
            }

            if (currentWifi != null) wifiList.Add(currentWifi);
        }

        private void GetWifiFromWMI(List<WifiInfo> wifiList)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapter WHERE PhysicalAdapter=true"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        var name = obj["Name"]?.ToString();
                        if (!string.IsNullOrEmpty(name) && name.Contains("Wireless"))
                        {
                            wifiList.Add(new WifiInfo
                            {
                                SSID = name,
                                IsConnected = true,
                                SignalStrength = 100
                            });
                        }
                    }
                }
            }
            catch { }
        }
        #endregion

        #region USB Devices
        public List<UsbDeviceInfo> GetUsbDevices()
        {
            var devices = new List<UsbDeviceInfo>();

            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE PNPDeviceID LIKE '%USB%'"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        try
                        {
                            var device = new UsbDeviceInfo
                            {
                                Name = obj["Name"]?.ToString() ?? "Unknown",
                                Description = obj["Description"]?.ToString() ?? "",
                                Status = obj["Status"]?.ToString() ?? "",
                                DeviceId = obj["DeviceID"]?.ToString() ?? "",
                                PnpDeviceId = obj["PNPDeviceID"]?.ToString() ?? ""
                            };

                            if (!string.IsNullOrEmpty(device.Name) && !device.Name.Contains("Unknown"))
                            {
                                devices.Add(device);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Get USB devices error: {ex.Message}");
            }

            return devices;
        }
        #endregion

        #region All System Devices
        public List<SystemDeviceInfo> GetAllSystemDevices()
        {
            var devices = new List<SystemDeviceInfo>();

            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        try
                        {
                            var device = new SystemDeviceInfo
                            {
                                Name = obj["Name"]?.ToString() ?? "Unknown",
                                Description = obj["Description"]?.ToString() ?? "",
                                Status = obj["Status"]?.ToString() ?? "",
                                Manufacturer = obj["Manufacturer"]?.ToString() ?? "",
                                DeviceId = obj["DeviceID"]?.ToString() ?? "",
                                Class = obj["PNPClass"]?.ToString() ?? ""
                            };

                            devices.Add(device);
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Get system devices error: {ex.Message}");
            }

            return devices;
        }
        #endregion

        #region Monitoring Control
        public void StartRemoteAccessMonitoring()
        {
            if (_isMonitoring) return;

            _isMonitoring = true;
            RemoteAccessStarted?.Invoke(this, EventArgs.Empty);
        }

        public void StopRemoteAccessMonitoring()
        {
            _isMonitoring = false;
            StopCamera();
            StopIntercom();
            RemoteAccessStopped?.Invoke(this, EventArgs.Empty);
        }
        #endregion
    }

    #region Data Classes
    public class WifiInfo
    {
        public string SSID { get; set; } = "";
        public int SignalStrength { get; set; }
        public bool IsConnected { get; set; }
        public string BSSID { get; set; } = "";
        public string Channel { get; set; } = "";
    }

    public class UsbDeviceInfo
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Status { get; set; } = "";
        public string DeviceId { get; set; } = "";
        public string PnpDeviceId { get; set; } = "";
    }

    public class SystemDeviceInfo
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Status { get; set; } = "";
        public string Manufacturer { get; set; } = "";
        public string DeviceId { get; set; } = "";
        public string Class { get; set; } = "";
    }
    #endregion
}
