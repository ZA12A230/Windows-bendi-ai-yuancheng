using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LocalAIStudio.Services;
using Microsoft.Win32;

namespace LocalAIStudio.Views
{
    public partial class WizardWindow : Window
    {
        private int _currentStep = 0;
        private readonly int _totalSteps = 9;
        private bool _ollamaInstalled = false;
        private bool _modelsDetected = false;
        private System.Threading.Timer _deployTimer;
        private System.Threading.Timer _modelCheckTimer;

        public WizardWindow()
        {
            InitializeComponent();
            Loaded += WizardWindow_Loaded;
        }

        private void WizardWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateProgressIndicators();
            UpdateButtonPositions();
            LoadLocalIPv4();
            
            // 启动欢迎动画
            var storyboard = (Storyboard)FindResource("FadeIn");
            storyboard.Begin(this);
        }

        private void UpdateProgressIndicators()
        {
            ProgressIndicators.Items.Clear();
            for (int i = 0; i < _totalSteps; i++)
            {
                var indicator = new Border
                {
                    Width = i == _currentStep ? 30 : 10,
                    Height = 10,
                    CornerRadius = new CornerRadius(5),
                    Background = i <= _currentStep ? 
                        new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3B82F6")) :
                        new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E5E7EB")),
                    Margin = new Thickness(5, 0, 5, 0)
                };

                var animation = new DoubleAnimation
                {
                    To = i == _currentStep ? 30 : 10,
                    Duration = TimeSpan.FromMilliseconds(300),
                    EasingFunction = new QuadraticEase()
                };

                indicator.BeginAnimation(Border.WidthProperty, animation);

                ProgressIndicators.Items.Add(indicator);
            }
        }

        private void UpdateButtonPositions()
        {
            // 下一步按钮动画
            var nextTranslate = new TranslateTransform();
            NextButton.RenderTransform = nextTranslate;

            DoubleAnimation moveAnimation;
            if (_currentStep == 0)
            {
                // 第一步：按钮在中间
                moveAnimation = new DoubleAnimation
                {
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(500),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
            }
            else
            {
                // 其他步骤：按钮在右下角
                moveAnimation = new DoubleAnimation
                {
                    To = 100,
                    Duration = TimeSpan.FromMilliseconds(500),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
            }

            nextTranslate.BeginAnimation(TranslateTransform.XProperty, moveAnimation);

            // 显示/隐藏上一步按钮
            BackButton.Visibility = _currentStep > 0 ? Visibility.Visible : Visibility.Collapsed;

            // 更新按钮文本
            if (_currentStep == _totalSteps - 1)
            {
                NextButton.Content = "完成 ✓";
            }
            else
            {
                NextButton.Content = "下一步 →";
            }
        }

        private void LoadLocalIPv4()
        {
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        LocalIPv4Text.Text = ip.ToString();
                        
                        // 自动填入 IP 前3位
                        var parts = ip.ToString().Split('.');
                        if (parts.Length == 4)
                        {
                            FrpServerAddress.Text = $"{parts[0]}.{parts[1]}.{parts[2]}.frpserver.com";
                        }
                        break;
                    }
                }
            }
            catch
            {
                LocalIPv4Text.Text = "无法检测";
            }
        }

        private async void WelcomeNextButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateToStep(1);
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep < _totalSteps - 1)
            {
                NavigateToStep(_currentStep + 1);
            }
            else
            {
                CompleteWizard();
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep > 0)
            {
                NavigateToStep(_currentStep - 1);
            }
        }

        private void NavigateToStep(int step)
        {
            // 淡出当前页面
            var currentPage = GetCurrentPageGrid();
            if (currentPage != null)
            {
                var fadeOut = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(300)
                };
                fadeOut.Completed += (s, e) =>
                {
                    currentPage.Visibility = Visibility.Collapsed;
                    
                    _currentStep = step;
                    UpdateProgressIndicators();
                    UpdateButtonPositions();

                    var nextPage = GetCurrentPageGrid();
                    if (nextPage != null)
                    {
                        nextPage.Visibility = Visibility.Visible;
                        
                        // 淡入新页面
                        var fadeIn = new DoubleAnimation
                        {
                            From = 0,
                            To = 1,
                            Duration = TimeSpan.FromMilliseconds(300)
                        };
                        nextPage.BeginAnimation(OpacityProperty, fadeIn);

                        // 根据步骤执行相应操作
                        ExecuteStepAction(step);
                    }
                };
                currentPage.BeginAnimation(OpacityProperty, fadeOut);
            }
        }

        private Grid GetCurrentPageGrid()
        {
            switch (_currentStep)
            {
                case 0: return WelcomePage;
                case 1: return OllamaPage;
                case 2: return ModelsPage;
                case 3: return SettingsPage;
                case 4: return FrpPage;
                case 5: return RemoteDesktopPage;
                case 6: return HardwarePage;
                case 7: return WebServerPage;
                case 8: return CompletePage;
                default: return WelcomePage;
            }
        }

        private void ExecuteStepAction(int step)
        {
            switch (step)
            {
                case 1:
                    CheckOllamaInstallation();
                    break;
                case 2:
                    CheckInstalledModels();
                    break;
                case 4:
                    FrpConfigPanel.Visibility = FrpEnableToggle.IsChecked == true ? 
                        Visibility.Visible : Visibility.Collapsed;
                    break;
            }
        }

        private async void CheckOllamaInstallation()
        {
            OllamaStatusText.Text = "正在检测 Ollama...";
            OllamaStatusBorder.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FEF3C7"));

            await Task.Delay(500);

            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/c ollama --version",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    _ollamaInstalled = true;
                    OllamaStatusText.Text = $"已安装 Ollama {output.Trim()}";
                    OllamaStatusIcon.Text = "✓";
                    OllamaStatusBorder.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F0FDF4"));
                    OllamaStatusBorder.BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#22C55E"));
                    OllamaStatusIcon.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#22C55E"));
                    OllamaStatusText.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#166534"));
                }
                else
                {
                    _ollamaInstalled = false;
                    OllamaStatusText.Text = "暂未安装 Ollama";
                    OllamaStatusIcon.Text = "✗";
                    OllamaStatusBorder.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FEE2E2"));
                    OllamaStatusBorder.BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#EF4444"));
                    OllamaStatusIcon.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#EF4444"));
                    OllamaStatusText.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#991B1B"));
                }
            }
            catch
            {
                _ollamaInstalled = false;
                OllamaStatusText.Text = "暂未安装 Ollama";
                OllamaStatusIcon.Text = "✗";
                OllamaStatusBorder.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FEE2E2"));
                OllamaStatusBorder.BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#EF4444"));
                OllamaStatusIcon.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#EF4444"));
                OllamaStatusText.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#991B1B"));
            }
        }

        private async void CheckInstalledModels()
        {
            ModelsStatusText.Text = "正在检测已安装的模型...";

            await Task.Delay(500);

            if (!_ollamaInstalled)
            {
                _modelsDetected = false;
                ModelsStatusText.Text = "暂未安装本地 AI 模型";
                ModelsStatusIcon.Text = "✗";
                ModelsStatusBorder.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FEE2E2"));
                ModelsListBox.ItemsSource = null;
                return;
            }

            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/c ollama list",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var models = new List<string>();

                for (int i = 1; i < lines.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(lines[i]) && !lines[i].Contains("NAME"))
                    {
                        var parts = lines[i].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]))
                        {
                            models.Add(parts[0]);
                        }
                    }
                }

                if (models.Count > 0)
                {
                    _modelsDetected = true;
                    ModelsStatusText.Text = $"已检测到 {models.Count} 个已安装模型";
                    ModelsStatusIcon.Text = "✓";
                    ModelsStatusBorder.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F0FDF4"));
                    ModelsListBox.ItemsSource = models;
                }
                else
                {
                    _modelsDetected = false;
                    ModelsStatusText.Text = "暂未安装本地 AI 模型";
                    ModelsStatusIcon.Text = "✗";
                    ModelsStatusBorder.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FEE2E2"));
                    ModelsListBox.ItemsSource = null;
                }
            }
            catch
            {
                _modelsDetected = false;
                ModelsStatusText.Text = "暂未安装本地 AI 模型";
                ModelsStatusIcon.Text = "✗";
                ModelsStatusBorder.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FEE2E2"));
                ModelsListBox.ItemsSource = null;
            }
        }

        private void CopyDownloadLink_Click(object sender, RoutedEventArgs e)
        {
            var links = $"官网下载：{OllamaOfficialLink.Text}\n国内加速：{OllamaChinaLink.Text}";
            try
            {
                System.Windows.Clipboard.SetText(links);
                System.Windows.MessageBox.Show("下载链接已复制到剪贴板！", "提示", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch
            {
                System.Windows.MessageBox.Show(links, "下载链接", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void DeployOllama_Click(object sender, RoutedEventArgs e)
        {
            DeployOllamaButton.IsEnabled = false;
            DeployOllamaButton.Content = "正在部署...";
            DeployStatus.Text = "正在后台部署 Ollama，请稍候...";

            await Task.Run(async () =>
            {
                try
                {
                    // 先尝试国内镜像
                    DeployStatus.Dispatcher.Invoke(() => 
                        DeployStatus.Text = "尝试从国内镜像下载...");

                    var downloadUrl = "https://github.com/ollama/ollama/releases/latest/download/ollama-windows-amd64.exe";
                    var tempPath = Path.Combine(Path.GetTempPath(), "ollama-installer.exe");

                    using (var client = new HttpClient())
                    {
                        client.Timeout = TimeSpan.FromMinutes(10);
                        
                        try
                        {
                            var response = await client.GetAsync(downloadUrl);
                            if (response.IsSuccessStatusCode)
                            {
                                var bytes = await response.Content.ReadAsByteArrayAsync();
                                await File.WriteAllBytesAsync(tempPath, bytes);

                                DeployStatus.Dispatcher.Invoke(() =>
                                {
                                    DeployStatus.Text = "下载完成，正在安装...";
                                });

                                // 执行安装
                                var installProcess = new Process
                                {
                                    StartInfo = new ProcessStartInfo
                                    {
                                        FileName = tempPath,
                                        Arguments = "/S",
                                        UseShellExecute = true,
                                        CreateNoWindow = true
                                    }
                                };

                                installProcess.Start();
                                await installProcess.WaitForExitAsync();

                                DeployStatus.Dispatcher.Invoke(() =>
                                {
                                    DeployOllamaButton.Content = "部署完成 ✓";
                                    DeployOllamaButton.Background = new SolidColorBrush(
                                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#22C55E"));
                                    DeployStatus.Text = "Ollama 部署成功！";
                                });

                                // 重新检测
                                await Task.Delay(2000);
                                CheckOllamaInstallation();
                                return;
                            }
                        }
                        catch
                        {
                            // 国内镜像失败，尝试官网
                        }

                        // 尝试官网下载
                        DeployStatus.Dispatcher.Invoke(() =>
                            DeployStatus.Text = "国内镜像不可用，尝试官网下载...");

                        downloadUrl = "https://ollama.com/download/ollama-windows-amd64.exe";
                        
                        using (var webClient = new HttpClient())
                        {
                            webClient.Timeout = TimeSpan.FromMinutes(15);
                            var bytes = await webClient.GetByteArrayAsync(downloadUrl);
                            await File.WriteAllBytesAsync(tempPath, bytes);

                            DeployStatus.Dispatcher.Invoke(() =>
                            {
                                DeployStatus.Text = "下载完成，正在安装...";
                            });

                            var installProcess = new Process
                            {
                                StartInfo = new ProcessStartInfo
                                {
                                    FileName = tempPath,
                                    Arguments = "/S",
                                    UseShellExecute = true
                                }
                            };

                            installProcess.Start();
                            await installProcess.WaitForExitAsync();

                            DeployStatus.Dispatcher.Invoke(() =>
                            {
                                DeployOllamaButton.Content = "部署完成 ✓";
                                DeployOllamaButton.Background = new SolidColorBrush(
                                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#22C55E"));
                                DeployStatus.Text = "Ollama 部署成功！";
                            });

                            await Task.Delay(2000);
                            CheckOllamaInstallation();
                        }
                    }
                }
                catch (Exception ex)
                {
                    DeployStatus.Dispatcher.Invoke(() =>
                    {
                        DeployOllamaButton.IsEnabled = true;
                        DeployOllamaButton.Content = "🚀 一键部署 Ollama";
                        DeployStatus.Text = $"部署失败：{ex.Message}";
                    });
                }
            });
        }

        private async void InstallModel_Click(object sender, RoutedEventArgs e)
        {
            var modelUrl = ModelInstallUrl.Text.Trim();
            if (string.IsNullOrWhiteSpace(modelUrl))
            {
                ModelInstallStatus.Text = "请输入模型安装链接";
                return;
            }

            if (!_ollamaInstalled)
            {
                ModelInstallStatus.Text = "请先安装 Ollama";
                return;
            }

            ModelInstallStatus.Text = "正在安装模型，请稍候...";

            await Task.Run(async () =>
            {
                try
                {
                    // 从 URL 提取模型名称
                    var modelName = modelUrl;
                    if (modelUrl.Contains("/library/"))
                    {
                        var parts = modelUrl.Split(new[] { "/library/" }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 1)
                        {
                            modelName = parts[1].TrimEnd('/');
                        }
                    }

                    ModelInstallStatus.Dispatcher.Invoke(() =>
                        ModelInstallStatus.Text = $"正在安装模型：{modelName}...");

                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/c ollama pull {modelName}",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    await process.WaitForExitAsync();

                    var error = await process.StandardError.ReadToEndAsync();

                    if (process.ExitCode == 0)
                    {
                        ModelInstallStatus.Dispatcher.Invoke(() =>
                        {
                            ModelInstallStatus.Text = $"模型 {modelName} 安装成功！";
                            ModelInstallUrl.Text = "";
                        });

                        // 重新检查模型
                        await Task.Delay(1000);
                        CheckInstalledModels();
                    }
                    else
                    {
                        ModelInstallStatus.Dispatcher.Invoke(() =>
                            ModelInstallStatus.Text = $"安装失败：{error}");
                    }
                }
                catch (Exception ex)
                {
                    ModelInstallStatus.Dispatcher.Invoke(() =>
                        ModelInstallStatus.Text = $"安装失败：{ex.Message}");
                }
            });
        }

        private void FrpEnableToggle_Click(object sender, RoutedEventArgs e)
        {
            FrpConfigPanel.Visibility = FrpEnableToggle.IsChecked == true ? 
                Visibility.Visible : Visibility.Collapsed;
        }

        private void AutoStartToggle_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var key = Registry.CurrentUser.CreateSubKey(@"Software\LocalAIStudio\Settings");
                key.SetValue("AutoStart", AutoStartToggle.IsChecked == true ? 1 : 0);
                key.Close();

                // 设置开机自启动
                var startupKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                if (AutoStartToggle.IsChecked == true)
                {
                    var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                    startupKey.SetValue("LocalAIStudio", $"\"{exePath}\"");
                }
                else
                {
                    startupKey.DeleteValue("LocalAIStudio", false);
                }
                startupKey.Close();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"设置开机自启动失败：{ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            var result = System.Windows.MessageBox.Show(
                "确定要跳过设置吗？\n\n您可以在后续通过设置页面修改这些选项。",
                "跳过设置",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                CompleteWizard();
            }
        }

        private void CompleteWizard()
        {
            try
            {
                // 保存所有设置
                SaveAllSettings();

                // 标记向导已完成
                var key = Registry.CurrentUser.CreateSubKey(@"Software\LocalAIStudio\Settings");
                key.SetValue("WizardCompleted", 1);
                key.Close();

                // 关闭向导并显示主窗口
                var mainWindow = new MainWindow();
                mainWindow.Show();
                
                this.Close();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"保存设置失败：{ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveAllSettings()
        {
            var key = Registry.CurrentUser.CreateSubKey(@"Software\LocalAIStudio\Settings");

            // 系统设置
            key.SetValue("AutoStart", AutoStartToggle.IsChecked == true ? 1 : 0);
            key.SetValue("AdminMode", AdminToggle.IsChecked == true ? 1 : 0);
            key.SetValue("ScreenOff", ScreenOffToggle.IsChecked == true ? 1 : 0);
            key.SetValue("SilentStart", SilentStartToggle.IsChecked == true ? 1 : 0);
            key.SetValue("AdaptiveMode", AdaptiveModeToggle.IsChecked == true ? 1 : 0);

            // 内网穿透设置
            key.SetValue("FrpEnabled", FrpEnableToggle.IsChecked == true ? 1 : 0);
            key.SetValue("FrpServer", FrpServerAddress.Text);
            key.SetValue("FrpPort", FrpServerPort.Text);
            key.SetValue("FrpToken", FrpAuthToken.Text);
            key.SetValue("FrpSubdomain", FrpSubdomain.Text);

            // 账号信息
            key.SetValue("Username", UsernameText.Text);
            key.SetValue("Password", SecurityService.Encrypt(PasswordBox.Password));

            // 本地 IP
            key.SetValue("LocalIPv4", LocalIPv4Text.Text);

            key.Close();
        }

        private void StartUsingButton_Click(object sender, RoutedEventArgs e)
        {
            CompleteWizard();
        }
    }
}
