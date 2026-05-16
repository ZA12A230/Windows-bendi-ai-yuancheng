using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LocalAIAssistant.Services;

namespace LocalAIAssistant.Views.Pages
{
    public partial class CameraPage : Page
    {
        private readonly CameraService _cameraService;
        private readonly ObservableCollection<string> _cameras = new();
        private bool _isCameraRunning;

        public CameraPage()
        {
            InitializeComponent();
            _cameraService = new CameraService();
            CameraComboBox.ItemsSource = _cameras;
            LoadCameras();
        }

        private async void LoadCameras()
        {
            var cameras = await _cameraService.GetAvailableCamerasAsync();
            _cameras.Clear();
            foreach (var camera in cameras)
            {
                _cameras.Add(camera);
            }
            if (_cameras.Count > 0)
            {
                CameraComboBox.SelectedIndex = 0;
            }
        }

        private async void StartCameraButton_Click(object sender, RoutedEventArgs e)
        {
            if (CameraComboBox.SelectedItem == null)
            {
                MessageBox.Show("请选择一个摄像头", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                StartCameraButton.IsEnabled = false;
                var selectedCamera = CameraComboBox.SelectedItem.ToString();

                await _cameraService.StartCameraAsync(selectedCamera, frame =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        LocalCameraImage.Source = frame;
                        LocalCameraImage.Visibility = Visibility.Visible;
                        LocalCameraStatusText.Visibility = Visibility.Collapsed;
                    });
                });

                CameraStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                CameraStatusText.Text = "运行中";
                StopCameraButton.IsEnabled = true;
                _isCameraRunning = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动摄像头失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                StartCameraButton.IsEnabled = true;
            }
        }

        private async void StopCameraButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _cameraService.StopCameraAsync();
                CameraStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                CameraStatusText.Text = "未启动";
                StartCameraButton.IsEnabled = true;
                StopCameraButton.IsEnabled = false;
                _isCameraRunning = false;

                LocalCameraImage.Visibility = Visibility.Collapsed;
                LocalCameraStatusText.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"停止摄像头失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void EnableRemoteAccessCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isCameraRunning)
            {
                MessageBox.Show("请先启动摄像头", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                EnableRemoteAccessCheckBox.IsChecked = false;
                return;
            }

            try
            {
                var url = await _cameraService.EnableRemoteAccessAsync();
                CameraAccessUrlText.Text = url;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启用远程访问失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                EnableRemoteAccessCheckBox.IsChecked = false;
            }
        }

        private async void EnableRemoteAccessCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                await _cameraService.DisableRemoteAccessAsync();
                CameraAccessUrlText.Text = "-";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"禁用远程访问失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
