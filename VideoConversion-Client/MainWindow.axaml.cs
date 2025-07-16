using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VideoConversion_Client.Models;
using VideoConversion_Client.Services;

namespace VideoConversion_Client
{
    public partial class MainWindow : Window
    {
        // 服务
        private ApiService apiService;
        private SignalRService signalRService;
        
        // 数据
        private ObservableCollection<ConversionTask> conversionTasks;
        private string? currentTaskId;
        
        // 状态
        private bool isConverting = false;
        private bool isConnectedToServer = false;

        // 页面内容
        private ScrollViewer? conversionPage;
        private ScrollViewer? historyPage;
        private ScrollViewer? settingsPage;

        public MainWindow()
        {
            InitializeComponent();
            
            // 初始化服务
            apiService = new ApiService();
            signalRService = new SignalRService();
            conversionTasks = new ObservableCollection<ConversionTask>();
            
            InitializeServices();
            InitializePresets();
            
            // 窗口关闭事件
            Closing += OnWindowClosing;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private async void InitializeServices()
        {
            try
            {
                // 初始化SignalR事件处理
                signalRService.Connected += () =>
                {
                    isConnectedToServer = true;
                    UpdateStatus("✅ 已连接到服务器");
                    UpdateConnectionIndicator(true);
                };

                signalRService.Disconnected += () =>
                {
                    isConnectedToServer = false;
                    UpdateStatus("❌ 与服务器断开连接");
                    UpdateConnectionIndicator(false);
                };

                signalRService.ProgressUpdated += OnProgressUpdated;
                signalRService.StatusUpdated += OnStatusUpdated;
                signalRService.TaskCompleted += OnTaskCompleted;
                signalRService.Error += OnSignalRError;

                // 尝试连接到SignalR
                await signalRService.ConnectAsync();

                // 加载最近的任务
                await LoadRecentTasks();
            }
            catch (Exception ex)
            {
                UpdateStatus($"❌ 初始化服务失败: {ex.Message}");
            }
        }

        private void InitializePresets()
        {
            var presetComboBox = this.FindControl<ComboBox>("PresetComboBox");
            if (presetComboBox != null)
            {
                var presets = ConversionPreset.GetAllPresets();
                foreach (var preset in presets)
                {
                    presetComboBox.Items.Add(preset.Name);
                }
                presetComboBox.SelectedIndex = 0;
            }
        }

        private async Task LoadRecentTasks()
        {
            try
            {
                var response = await apiService.GetRecentTasksAsync(10);
                if (response.Success && response.Data != null)
                {
                    conversionTasks.Clear();
                    foreach (var task in response.Data)
                    {
                        conversionTasks.Add(task);
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"❌ 加载历史任务失败: {ex.Message}");
            }
        }

        private void UpdateStatus(string message)
        {
            var statusText = this.FindControl<TextBlock>("StatusText");
            if (statusText != null)
            {
                statusText.Text = message;
            }
        }

        private void UpdateConnectionIndicator(bool connected)
        {
            var indicator = this.FindControl<Border>("ConnectionIndicator");
            if (indicator != null)
            {
                indicator.Background = connected ? 
                    Avalonia.Media.Brushes.Green : 
                    Avalonia.Media.Brushes.Red;
            }
        }

        // SignalR事件处理方法
        private void OnProgressUpdated(string taskId, int progress, string message, double? speed, int? remainingSeconds)
        {
            if (currentTaskId == taskId)
            {
                var progressBar = this.FindControl<ProgressBar>("ProgressBar");
                var progressText = this.FindControl<TextBlock>("ProgressText");
                
                if (progressBar != null)
                    progressBar.Value = progress;
                
                if (progressText != null)
                    progressText.Text = $"{message} ({progress}%)";
                
                var speedText = speed.HasValue ? $" - {speed.Value:F1}x" : "";
                var timeText = remainingSeconds.HasValue ? 
                    $" - 剩余: {TimeSpan.FromSeconds(remainingSeconds.Value):hh\\:mm\\:ss}" : "";
                
                UpdateStatus($"📊 转换进度: {progress}%{speedText}{timeText}");
            }

            // 更新任务列表中的进度
            var task = conversionTasks.FirstOrDefault(t => t.Id == taskId);
            if (task != null)
            {
                task.Progress = progress;
                task.ConversionSpeed = speed;
                task.EstimatedTimeRemaining = remainingSeconds;
            }
        }

        private void OnStatusUpdated(string taskId, string status, string? errorMessage)
        {
            var task = conversionTasks.FirstOrDefault(t => t.Id == taskId);
            if (task != null)
            {
                if (Enum.TryParse<ConversionStatus>(status, out var conversionStatus))
                {
                    task.Status = conversionStatus;
                }
                task.ErrorMessage = errorMessage;
            }

            if (currentTaskId == taskId)
            {
                UpdateStatus($"📋 状态更新: {status}");
                
                if (status == "Completed" || status == "Failed" || status == "Cancelled")
                {
                    ResetConversionUI();
                }
            }
        }

        private void OnTaskCompleted(string taskId, string taskName, bool success, string? errorMessage)
        {
            var message = success ? 
                $"🎉 任务完成: {taskName}" : 
                $"❌ 任务失败: {taskName} - {errorMessage}";
            
            UpdateStatus(message);

            if (currentTaskId == taskId)
            {
                ResetConversionUI();
            }
        }

        private void OnSignalRError(string errorMessage)
        {
            UpdateStatus($"❌ SignalR错误: {errorMessage}");
        }

        private void ResetConversionUI()
        {
            isConverting = false;
            currentTaskId = null;
            
            var startButton = this.FindControl<Button>("StartButton");
            var cancelButton = this.FindControl<Button>("CancelButton");
            var progressSection = this.FindControl<Border>("ProgressSection");
            
            if (startButton != null) startButton.IsVisible = true;
            if (cancelButton != null) cancelButton.IsVisible = false;
            if (progressSection != null) progressSection.IsVisible = false;
        }

        private string GetFileSize(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                var sizeInMB = fileInfo.Length / (1024.0 * 1024.0);
                return sizeInMB > 1024 ? $"{sizeInMB / 1024:F1} GB" : $"{sizeInMB:F1} MB";
            }
            catch
            {
                return "未知大小";
            }
        }

        private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
        {
            try
            {
                // 断开SignalR连接
                if (signalRService != null)
                {
                    await signalRService.DisconnectAsync();
                    signalRService.Dispose();
                }

                // 释放API服务
                apiService?.Dispose();
            }
            catch (Exception ex)
            {
                // 记录错误但不阻止关闭
                System.Diagnostics.Debug.WriteLine($"关闭时清理失败: {ex.Message}");
            }
        }

        // 导航事件处理
        private void ConversionNavButton_Click(object? sender, RoutedEventArgs e)
        {
            ShowConversionPage();
        }

        private void HistoryNavButton_Click(object? sender, RoutedEventArgs e)
        {
            ShowHistoryPage();
        }

        private void SettingsNavButton_Click(object? sender, RoutedEventArgs e)
        {
            ShowSettingsPage();
        }

        // 文件选择事件
        private async void BrowseButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                UpdateStatus("📂 正在打开文件选择对话框...");

                var options = new FilePickerOpenOptions
                {
                    Title = "选择视频文件",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("视频文件")
                        {
                            Patterns = new[] { "*.mp4", "*.avi", "*.mov", "*.mkv", "*.wmv", "*.flv", "*.webm", "*.m4v", "*.3gp", "*.mpg", "*.mpeg", "*.ts", "*.mts" }
                        },
                        FilePickerFileTypes.All
                    }
                };

                var result = await StorageProvider.OpenFilePickerAsync(options);
                var file = result?.FirstOrDefault();

                if (file != null)
                {
                    var filePath = file.Path.LocalPath;
                    var filePathTextBox = this.FindControl<TextBox>("FilePathTextBox");
                    var taskNameTextBox = this.FindControl<TextBox>("TaskNameTextBox");

                    if (filePathTextBox != null)
                        filePathTextBox.Text = filePath;

                    // 自动生成任务名称
                    if (taskNameTextBox != null && string.IsNullOrEmpty(taskNameTextBox.Text))
                    {
                        taskNameTextBox.Text = Path.GetFileNameWithoutExtension(filePath);
                    }

                    UpdateStatus($"✅ 已选择文件: {Path.GetFileName(filePath)} ({GetFileSize(filePath)})");
                }
                else
                {
                    UpdateStatus("❌ 未选择任何文件");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"❌ 选择文件失败: {ex.Message}");
            }
        }

        // 转换控制事件
        private async void StartButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var filePathTextBox = this.FindControl<TextBox>("FilePathTextBox");
                var taskNameTextBox = this.FindControl<TextBox>("TaskNameTextBox");
                var presetComboBox = this.FindControl<ComboBox>("PresetComboBox");

                if (string.IsNullOrEmpty(filePathTextBox?.Text))
                {
                    UpdateStatus("⚠️ 请先选择视频文件");
                    return;
                }

                if (string.IsNullOrEmpty(taskNameTextBox?.Text))
                {
                    UpdateStatus("⚠️ 请输入任务名称");
                    return;
                }

                if (!File.Exists(filePathTextBox.Text))
                {
                    UpdateStatus("⚠️ 选择的文件不存在");
                    return;
                }

                var selectedPreset = presetComboBox?.SelectedItem?.ToString() ?? "Fast 1080p30";
                var preset = ConversionPreset.GetPresetByName(selectedPreset);

                if (preset == null)
                {
                    UpdateStatus("⚠️ 无效的转换预设");
                    return;
                }

                // 准备转换请求
                var request = new StartConversionRequest
                {
                    TaskName = taskNameTextBox.Text,
                    Preset = selectedPreset,
                    OutputFormat = preset.OutputFormat,
                    Resolution = preset.Resolution,
                    VideoCodec = preset.VideoCodec,
                    AudioCodec = preset.AudioCodec,
                    VideoQuality = preset.VideoQuality,
                    AudioQuality = preset.AudioQuality,
                    FrameRate = preset.FrameRate
                };

                // 更新UI状态
                SetConversionUI(true);
                UpdateStatus($"🚀 开始转换: {taskNameTextBox.Text} (预设: {selectedPreset})");

                // 调用API开始转换
                var response = await apiService.StartConversionAsync(filePathTextBox.Text, request);

                if (response.Success && response.Data != null)
                {
                    currentTaskId = response.Data.TaskId;
                    UpdateStatus($"✅ 转换任务已创建: {response.Data.TaskName}");

                    // 加入SignalR任务组以接收进度更新
                    if (!string.IsNullOrEmpty(currentTaskId))
                    {
                        await signalRService.JoinTaskGroupAsync(currentTaskId);
                    }

                    // 创建新的任务对象并添加到列表
                    var newTask = new ConversionTask
                    {
                        Id = currentTaskId ?? Guid.NewGuid().ToString(),
                        TaskName = taskNameTextBox.Text,
                        OriginalFileName = Path.GetFileName(filePathTextBox.Text),
                        Status = ConversionStatus.Pending,
                        Progress = 0,
                        CreatedAt = DateTime.Now
                    };

                    conversionTasks.Insert(0, newTask);
                }
                else
                {
                    UpdateStatus($"❌ 启动转换失败: {response.Message}");
                    ResetConversionUI();
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"❌ 转换失败: {ex.Message}");
                ResetConversionUI();
            }
        }

        private async void CancelButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(currentTaskId))
                {
                    UpdateStatus("⏹️ 正在取消转换...");

                    // 通过SignalR请求取消任务
                    await signalRService.CancelTaskAsync(currentTaskId);

                    // 也可以通过API取消
                    var response = await apiService.CancelTaskAsync(currentTaskId);

                    if (response.Success)
                    {
                        UpdateStatus("✅ 转换已取消");
                    }
                    else
                    {
                        UpdateStatus($"⚠️ 取消转换失败: {response.Message}");
                    }
                }

                ResetConversionUI();
            }
            catch (Exception ex)
            {
                UpdateStatus($"❌ 取消转换失败: {ex.Message}");
            }
        }

        private async void TestButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                UpdateStatus("🔗 正在测试服务器连接...");

                // 测试API连接
                var apiConnected = await apiService.TestConnectionAsync();

                if (apiConnected)
                {
                    UpdateStatus("✅ API连接测试成功");

                    // 测试SignalR连接
                    if (!isConnectedToServer)
                    {
                        UpdateStatus("🔗 正在连接SignalR...");
                        var signalRConnected = await signalRService.ConnectAsync();

                        if (signalRConnected)
                        {
                            UpdateStatus("✅ 服务器连接测试成功 - API和SignalR都正常");
                        }
                        else
                        {
                            UpdateStatus("⚠️ API连接正常，但SignalR连接失败");
                        }
                    }
                    else
                    {
                        UpdateStatus("✅ 服务器连接测试成功 - API和SignalR都正常");
                    }

                    // 刷新最近任务
                    await LoadRecentTasks();
                }
                else
                {
                    UpdateStatus("❌ 服务器连接失败 - 请检查服务器是否运行");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"❌ 连接测试失败: {ex.Message}");
            }
        }

        private void SetConversionUI(bool converting)
        {
            isConverting = converting;

            var startButton = this.FindControl<Button>("StartButton");
            var cancelButton = this.FindControl<Button>("CancelButton");
            var progressSection = this.FindControl<Border>("ProgressSection");
            var progressBar = this.FindControl<ProgressBar>("ProgressBar");
            var progressText = this.FindControl<TextBlock>("ProgressText");

            if (startButton != null) startButton.IsVisible = !converting;
            if (cancelButton != null) cancelButton.IsVisible = converting;
            if (progressSection != null) progressSection.IsVisible = converting;

            if (converting)
            {
                if (progressBar != null) progressBar.Value = 0;
                if (progressText != null) progressText.Text = "正在上传文件...";
            }
        }

        // 页面切换方法
        private void ShowConversionPage()
        {
            var contentArea = this.FindControl<Border>("ContentArea");
            if (contentArea != null && conversionPage == null)
            {
                // 转换页面已经在XAML中定义，只需要确保显示
                var existingPage = contentArea.Child as ScrollViewer;
                if (existingPage?.Name == "ConversionPage")
                {
                    conversionPage = existingPage;
                }
            }
            UpdateNavigationButtons("conversion");
        }

        private void ShowHistoryPage()
        {
            if (historyPage == null)
            {
                historyPage = CreateHistoryPage();
            }

            var contentArea = this.FindControl<Border>("ContentArea");
            if (contentArea != null)
            {
                contentArea.Child = historyPage;
            }

            UpdateNavigationButtons("history");
        }

        private void ShowSettingsPage()
        {
            if (settingsPage == null)
            {
                settingsPage = CreateSettingsPage();
            }

            var contentArea = this.FindControl<Border>("ContentArea");
            if (contentArea != null)
            {
                contentArea.Child = settingsPage;
            }

            UpdateNavigationButtons("settings");
        }

        private void UpdateNavigationButtons(string activePage)
        {
            var conversionNav = this.FindControl<Button>("ConversionNavButton");
            var historyNav = this.FindControl<Button>("HistoryNavButton");
            var settingsNav = this.FindControl<Button>("SettingsNavButton");

            // 重置所有按钮样式
            if (conversionNav != null) conversionNav.Background = Avalonia.Media.Brushes.LightBlue;
            if (historyNav != null) historyNav.Background = Avalonia.Media.Brushes.LightGreen;
            if (settingsNav != null) settingsNav.Background = Avalonia.Media.Brushes.LightYellow;

            // 高亮当前页面按钮
            switch (activePage)
            {
                case "conversion":
                    if (conversionNav != null) conversionNav.Background = Avalonia.Media.Brushes.DarkBlue;
                    break;
                case "history":
                    if (historyNav != null) historyNav.Background = Avalonia.Media.Brushes.DarkGreen;
                    break;
                case "settings":
                    if (settingsNav != null) settingsNav.Background = Avalonia.Media.Brushes.DarkOrange;
                    break;
            }
        }

        private ScrollViewer CreateHistoryPage()
        {
            var scrollViewer = new ScrollViewer();
            var stackPanel = new StackPanel { Spacing = 20, Margin = new Avalonia.Thickness(20) };

            // 页面标题
            var title = new TextBlock
            {
                Text = "转换历史记录",
                Classes = { "page-title" },
                Foreground = Avalonia.Media.Brushes.DarkGreen
            };
            stackPanel.Children.Add(title);

            // 历史记录区域
            var historyBorder = new Border
            {
                Classes = { "section", "history-section" }
            };

            var historyStack = new StackPanel { Spacing = 15 };

            var historyTitle = new TextBlock
            {
                Text = "📋 最近转换任务",
                Classes = { "section-title" },
                Foreground = Avalonia.Media.Brushes.DarkCyan
            };
            historyStack.Children.Add(historyTitle);

            var historyList = new ListBox
            {
                Background = Avalonia.Media.Brushes.White,
                Height = 300
            };

            // 填充历史记录
            foreach (var task in conversionTasks)
            {
                var listItem = new ListBoxItem
                {
                    Content = $"{task.TaskName} - {task.StatusText} ({task.ProgressText})",
                    Padding = new Avalonia.Thickness(10, 8),
                    FontSize = 14,
                    Tag = task
                };
                historyList.Items.Add(listItem);
            }

            historyStack.Children.Add(historyList);
            historyBorder.Child = historyStack;
            stackPanel.Children.Add(historyBorder);

            scrollViewer.Content = stackPanel;
            return scrollViewer;
        }

        private ScrollViewer CreateSettingsPage()
        {
            var scrollViewer = new ScrollViewer();
            var stackPanel = new StackPanel { Spacing = 20, Margin = new Avalonia.Thickness(20) };

            // 页面标题
            var title = new TextBlock
            {
                Text = "应用设置",
                Classes = { "page-title" },
                Foreground = Avalonia.Media.Brushes.DarkOrange
            };
            stackPanel.Children.Add(title);

            // 服务器设置区域
            var serverBorder = new Border
            {
                Classes = { "section", "server-section" }
            };

            var serverStack = new StackPanel { Spacing = 15 };

            var serverTitle = new TextBlock
            {
                Text = "🌐 服务器设置",
                Classes = { "section-title" },
                Foreground = Avalonia.Media.Brushes.DarkSlateBlue
            };
            serverStack.Children.Add(serverTitle);

            var serverGrid = new Grid();
            serverGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            serverGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            serverGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            var serverLabel = new TextBlock
            {
                Text = "服务器地址:",
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Margin = new Avalonia.Thickness(0, 0, 15, 0),
                FontSize = 14,
                FontWeight = Avalonia.Media.FontWeight.Bold
            };
            Grid.SetRow(serverLabel, 0);
            Grid.SetColumn(serverLabel, 0);
            serverGrid.Children.Add(serverLabel);

            var serverUrlTextBox = new TextBox
            {
                Name = "ServerUrlTextBox",
                Text = "http://localhost:5000",
                Classes = { "input-field" }
            };
            Grid.SetRow(serverUrlTextBox, 0);
            Grid.SetColumn(serverUrlTextBox, 1);
            serverGrid.Children.Add(serverUrlTextBox);

            serverStack.Children.Add(serverGrid);

            var testServerButton = new Button
            {
                Content = "🔗 测试服务器连接",
                Classes = { "action-button" },
                Background = Avalonia.Media.Brushes.Blue,
                Foreground = Avalonia.Media.Brushes.White,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Margin = new Avalonia.Thickness(0, 10, 0, 0)
            };
            testServerButton.Click += TestServerButton_Click;
            serverStack.Children.Add(testServerButton);

            serverBorder.Child = serverStack;
            stackPanel.Children.Add(serverBorder);

            scrollViewer.Content = stackPanel;
            return scrollViewer;
        }

        private async void TestServerButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var serverUrlTextBox = settingsPage?.Content is StackPanel sp ?
                    FindControlInPanel(sp, "ServerUrlTextBox") as TextBox : null;

                var newUrl = serverUrlTextBox?.Text?.Trim();
                if (string.IsNullOrEmpty(newUrl))
                {
                    UpdateStatus("⚠️ 请输入服务器地址");
                    return;
                }

                UpdateStatus("🔗 正在测试服务器连接...");

                // 更新API服务的基础URL
                apiService.BaseUrl = newUrl;

                // 测试连接
                var connected = await apiService.TestConnectionAsync();

                if (connected)
                {
                    UpdateStatus("✅ 服务器连接测试成功");

                    // 重新连接SignalR
                    await signalRService.DisconnectAsync();
                    signalRService = new SignalRService(newUrl);

                    // 重新注册事件
                    signalRService.Connected += () =>
                    {
                        isConnectedToServer = true;
                        UpdateStatus("✅ 已连接到服务器");
                        UpdateConnectionIndicator(true);
                    };

                    signalRService.Disconnected += () =>
                    {
                        isConnectedToServer = false;
                        UpdateStatus("❌ 与服务器断开连接");
                        UpdateConnectionIndicator(false);
                    };

                    signalRService.ProgressUpdated += OnProgressUpdated;
                    signalRService.StatusUpdated += OnStatusUpdated;
                    signalRService.TaskCompleted += OnTaskCompleted;
                    signalRService.Error += OnSignalRError;

                    await signalRService.ConnectAsync();
                }
                else
                {
                    UpdateStatus("❌ 服务器连接失败 - 请检查地址和服务器状态");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"❌ 服务器连接测试失败: {ex.Message}");
            }
        }

        private Control? FindControlInPanel(Panel panel, string name)
        {
            foreach (var child in panel.Children)
            {
                if (child.Name == name)
                    return child;

                if (child is Panel childPanel)
                {
                    var found = FindControlInPanel(childPanel, name);
                    if (found != null)
                        return found;
                }
                else if (child is Border border && border.Child is Panel borderPanel)
                {
                    var found = FindControlInPanel(borderPanel, name);
                    if (found != null)
                        return found;
                }
                else if (child is Grid grid)
                {
                    var found = FindControlInPanel(grid, name);
                    if (found != null)
                        return found;
                }
            }
            return null;
        }
    }
}
