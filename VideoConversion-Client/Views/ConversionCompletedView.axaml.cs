using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Logging;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VideoConversion_Client.Models;
using VideoConversion_Client.Services;

namespace VideoConversion_Client.Views
{
    public partial class ConversionCompletedView : UserControl
    {
        private readonly ApiService _apiService;
        private readonly SignalRService _signalRService;
        private List<ConversionTask> _completedTasks = new();
        private List<ConversionTask> _filteredTasks = new();

        // 事件：请求切换到上传页面
        public event EventHandler? NavigateToUploadRequested;

        public ConversionCompletedView()
        {
            InitializeComponent();

            // 初始化服务
            var settingsService = SystemSettingsService.Instance;
            _apiService = new ApiService { BaseUrl = settingsService.GetServerAddress() };
            _signalRService = new SignalRService(settingsService.GetServerAddress());

            // 注册SignalR事件
            RegisterSignalREvents();

            // 启动SignalR连接
            _ = InitializeSignalRAsync();

            // 加载已完成的文件
            _ = LoadCompletedFilesAsync();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void SearchBtn_Click(object? sender, RoutedEventArgs e)
        {
            var searchBox = this.FindControl<TextBox>("SearchBox");
            var searchToggleBtn = this.FindControl<Button>("SearchToggleBtn");

            if (searchBox != null && searchToggleBtn != null)
            {
                searchBox.IsVisible = !searchBox.IsVisible;
                if (searchBox.IsVisible)
                {
                    searchBox.Focus();
                    searchToggleBtn.Content = "✕";
                    Avalonia.Controls.ToolTip.SetTip(searchToggleBtn, "关闭搜索");
                }
                else
                {
                    searchBox.Text = "";
                    searchToggleBtn.Content = "🔍";
                    Avalonia.Controls.ToolTip.SetTip(searchToggleBtn, "搜索文件");
                    // 清除搜索过滤，显示所有文件
                    FilterCompletedFiles("");
                }
            }
        }

        private void SearchBox_TextChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e)
        {
            if (sender is TextBox searchBox)
            {
                FilterCompletedFiles(searchBox.Text ?? "");
            }
        }

        private async void RefreshBtn_Click(object? sender, RoutedEventArgs e)
        {
            await LoadCompletedFilesAsync();
        }

        /// <summary>
        /// 初始化SignalR连接
        /// </summary>
        private async Task InitializeSignalRAsync()
        {
            try
            {
                if (!_signalRService.IsConnected)
                {
                    await _signalRService.ConnectAsync();
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionCompletedView", $"SignalR连接失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 注册SignalR事件
        /// </summary>
        private void RegisterSignalREvents()
        {
            _signalRService.TaskCompleted += OnTaskCompleted;
            _signalRService.TaskDeleted += OnTaskDeleted;
        }

        /// <summary>
        /// 任务完成事件处理
        /// </summary>
        private async void OnTaskCompleted(string taskId, string taskName, bool success, string? errorMessage)
        {
            if (success)
            {
                // 重新加载已完成的文件列表
                await LoadCompletedFilesAsync();
            }
        }

        /// <summary>
        /// 任务删除事件处理
        /// </summary>
        private async void OnTaskDeleted(string taskId)
        {
            // 从列表中移除已删除的任务
            _completedTasks.RemoveAll(t => t.Id == taskId);
            _filteredTasks.RemoveAll(t => t.Id == taskId);

            // 更新UI
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                RefreshCompletedFilesList();
                UpdateCompletedStats();
                UpdateEmptyStateVisibility();
            });
        }

        /// <summary>
        /// 加载已完成的文件
        /// </summary>
        private async Task LoadCompletedFilesAsync()
        {
            try
            {
                // 显示加载状态
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    SetLoadingState(true);
                });

                var response = await _apiService.GetCompletedTasksAsync(1, 100);
                if (response.Success && response.Data != null)
                {
                    _completedTasks = response.Data;
                    _filteredTasks = new List<ConversionTask>(_completedTasks);

                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        SetLoadingState(false);
                        RefreshCompletedFilesList();
                        UpdateCompletedStats();
                        UpdateEmptyStateVisibility();
                    });
                }
                else
                {
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        SetLoadingState(false);
                        ShowErrorMessage(response.Message ?? "加载失败");
                    });
                }
            }
            catch (Exception ex)
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    SetLoadingState(false);
                    ShowErrorMessage($"加载已完成文件失败: {ex.Message}");
                });
                Utils.Logger.Error("ConversionCompletedView", $"加载已完成文件失败: {ex.Message}");
            }
        }

        private void FilterCompletedFiles(string searchText)
        {
            if (string.IsNullOrEmpty(searchText))
            {
                _filteredTasks = new List<ConversionTask>(_completedTasks);
            }
            else
            {
                _filteredTasks = _completedTasks.Where(t =>
                    t.TaskName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    t.OriginalFileName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    (t.OutputFileName?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false)
                ).ToList();
            }

            RefreshCompletedFilesList();
            UpdateCompletedStats();
            UpdateEmptyStateVisibility();
        }

        private void UpdateFilteredStats(string searchText)
        {
            var completedContainer = this.FindControl<StackPanel>("CompletedFileListContainer");
            var statsText = this.FindControl<TextBlock>("CompletedStatsText");

            if (completedContainer != null && statsText != null)
            {
                var visibleCount = completedContainer.Children
                    .OfType<Border>()
                    .Count(border => border.IsVisible);

                var totalCount = completedContainer.Children.Count;

                if (string.IsNullOrEmpty(searchText))
                {
                    statsText.Text = totalCount == 0 ? "0 项，0 GB" : $"{totalCount} 项，28.45 GB";
                }
                else
                {
                    statsText.Text = $"找到 {visibleCount} 项（共 {totalCount} 项）";
                }
            }
        }

        /// <summary>
        /// 刷新已完成文件列表
        /// </summary>
        private void RefreshCompletedFilesList()
        {
            var completedContainer = this.FindControl<StackPanel>("CompletedFileListContainer");
            if (completedContainer == null) return;

            completedContainer.Children.Clear();

            foreach (var task in _filteredTasks)
            {
                var completedItem = CreateCompletedFileItemUI(task);
                completedContainer.Children.Add(completedItem);
            }
        }

        // 创建完成文件项UI（从ConversionTask）
        private Border CreateCompletedFileItemUI(ConversionTask task)
        {
            var fileName = task.OriginalFileName ?? task.TaskName;
            var format = task.OutputFormat ?? task.InputFormat ?? "MP4";
            var resolution = task.Resolution ?? "未知";
            var size = FormatFileSize(task.OutputFileSize ?? task.OriginalFileSize ?? 0);
            var duration = FormatDuration(task.CompletedAt - task.StartedAt);

            return CreateCompletedFileItemUI(fileName, format, resolution, size, duration, task.Id);
        }

        // 创建完成文件项UI
        private Border CreateCompletedFileItemUI(string fileName, string format, string resolution, string size, string duration, string? taskId = null)
        {
            var border = new Border
            {
                Background = Avalonia.Media.Brushes.White,
                BorderBrush = Avalonia.Media.Brush.Parse("#e0e0e0"),
                BorderThickness = new Avalonia.Thickness(1),
                CornerRadius = new Avalonia.CornerRadius(8),
                Padding = new Avalonia.Thickness(15),
                Margin = new Avalonia.Thickness(0, 5),
                Tag = taskId ?? fileName // 用于标识任务ID或文件名
            };

            var grid = new Grid();
            // 添加列定义
            grid.ColumnDefinitions.Add(new ColumnDefinition(80, GridUnitType.Pixel));  // 缩略图
            grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));    // 文件名
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));         // 格式
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));         // 分辨率
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));         // 文件大小
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));         // 时长
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));         // 删除按钮
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));         // 下载按钮
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));         // 文件夹按钮
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));         // 更多按钮
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            // 缩略图
            var thumbnailBorder = new Border
            {
                Background = Avalonia.Media.Brush.Parse("#f0f0f0"),
                CornerRadius = new Avalonia.CornerRadius(6),
                Width = 60,
                Height = 45
            };
            Grid.SetColumn(thumbnailBorder, 0);

            // 文件名
            var namePanel = new StackPanel
            {
                Margin = new Avalonia.Thickness(15, 0),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            var fileNameText = new TextBlock
            {
                Text = fileName,
                FontSize = 14,
                FontWeight = Avalonia.Media.FontWeight.Medium,
                Foreground = Avalonia.Media.Brush.Parse("#333"),
                TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis
            };
            var editIcon = new TextBlock
            {
                Text = "✏️",
                FontSize = 12,
                Foreground = Avalonia.Media.Brush.Parse("#999"),
                Margin = new Avalonia.Thickness(0, 2, 0, 0)
            };
            namePanel.Children.Add(fileNameText);
            namePanel.Children.Add(editIcon);
            Grid.SetColumn(namePanel, 1);

            // 格式
            var formatPanel = CreateInfoColumn("📄", format);
            Grid.SetColumn(formatPanel, 2);

            // 分辨率
            var resolutionPanel = CreateInfoColumn("📐", resolution);
            Grid.SetColumn(resolutionPanel, 3);

            // 文件大小
            var sizePanel = CreateInfoColumn("💾", size);
            Grid.SetColumn(sizePanel, 4);

            // 时长
            var durationPanel = CreateInfoColumn("⏱️", duration);
            Grid.SetColumn(durationPanel, 5);

            // 删除按钮
            var deleteBtn = new Button
            {
                Content = "✕",
                Background = Avalonia.Media.Brushes.Transparent,
                Foreground = Avalonia.Media.Brush.Parse("#999"),
                BorderThickness = new Avalonia.Thickness(0),
                Padding = new Avalonia.Thickness(8),
                Margin = new Avalonia.Thickness(10, 0, 5, 0)
            };
            deleteBtn.Click += (s, e) => RemoveCompletedFile(taskId ?? fileName);
            Grid.SetColumn(deleteBtn, 6);

            // 下载按钮
            var downloadBtn = new Button
            {
                Content = "📥",
                Background = Avalonia.Media.Brushes.Transparent,
                Foreground = Avalonia.Media.Brush.Parse("#999"),
                BorderThickness = new Avalonia.Thickness(0),
                Padding = new Avalonia.Thickness(8),
                Margin = new Avalonia.Thickness(5, 0)
            };
            Avalonia.Controls.ToolTip.SetTip(downloadBtn, "下载文件");
            downloadBtn.Click += (s, e) => {
                if (!string.IsNullOrEmpty(taskId))
                    DownloadFile(taskId);
                else
                    OpenFileFolder(fileName);
            };
            Grid.SetColumn(downloadBtn, 7);

            // 文件夹按钮
            var folderBtn = new Button
            {
                Content = "📁",
                Background = Avalonia.Media.Brushes.Transparent,
                Foreground = Avalonia.Media.Brush.Parse("#999"),
                BorderThickness = new Avalonia.Thickness(0),
                Padding = new Avalonia.Thickness(8),
                Margin = new Avalonia.Thickness(5, 0)
            };
            Avalonia.Controls.ToolTip.SetTip(folderBtn, "打开文件夹");
            folderBtn.Click += (s, e) => OpenFileFolder(taskId ?? fileName);
            Grid.SetColumn(folderBtn, 8);

            // 更多选项按钮
            var moreBtn = new Button
            {
                Content = "⋯",
                Background = Avalonia.Media.Brushes.Transparent,
                Foreground = Avalonia.Media.Brush.Parse("#999"),
                BorderThickness = new Avalonia.Thickness(0),
                Padding = new Avalonia.Thickness(8),
                Margin = new Avalonia.Thickness(5, 0, 0, 0)
            };
            Avalonia.Controls.ToolTip.SetTip(moreBtn, "更多选项");
            Grid.SetColumn(moreBtn, 9);

            grid.Children.Add(thumbnailBorder);
            grid.Children.Add(namePanel);
            grid.Children.Add(formatPanel);
            grid.Children.Add(resolutionPanel);
            grid.Children.Add(sizePanel);
            grid.Children.Add(durationPanel);
            grid.Children.Add(deleteBtn);
            grid.Children.Add(downloadBtn);
            grid.Children.Add(folderBtn);
            grid.Children.Add(moreBtn);

            border.Child = grid;
            return border;
        }

        // 创建信息列
        private StackPanel CreateInfoColumn(string icon, string text)
        {
            var panel = new StackPanel
            {
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Margin = new Avalonia.Thickness(10, 0)
            };

            var iconText = new TextBlock
            {
                Text = icon,
                FontSize = 12,
                Foreground = Avalonia.Media.Brush.Parse("#666"),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };

            var valueText = new TextBlock
            {
                Text = text,
                FontSize = 12,
                Foreground = Avalonia.Media.Brush.Parse("#666"),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };

            panel.Children.Add(iconText);
            panel.Children.Add(valueText);

            return panel;
        }

        private async void RemoveCompletedFile(string taskIdOrFileName)
        {
            try
            {
                // 如果是taskId，尝试删除任务
                var task = _completedTasks.FirstOrDefault(t => t.Id == taskIdOrFileName);
                if (task != null)
                {
                    var response = await _apiService.DeleteTaskAsync(task.Id);
                    if (response.Success)
                    {
                        // 从本地列表中移除
                        _completedTasks.Remove(task);
                        _filteredTasks.Remove(task);

                        RefreshCompletedFilesList();
                        UpdateCompletedStats();
                        UpdateEmptyStateVisibility();

                        Utils.Logger.Info("ConversionCompletedView", $"任务删除成功: {task.TaskName}");
                    }
                    else
                    {
                        Utils.Logger.Error("ConversionCompletedView", $"删除任务失败: {response.Message}");
                    }
                }
                else
                {
                    // 兼容旧的文件名删除方式
                    var taskByName = _completedTasks.FirstOrDefault(t =>
                        t.OriginalFileName == taskIdOrFileName || t.TaskName == taskIdOrFileName);
                    if (taskByName != null)
                    {
                        _completedTasks.Remove(taskByName);
                        _filteredTasks.Remove(taskByName);

                        RefreshCompletedFilesList();
                        UpdateCompletedStats();
                        UpdateEmptyStateVisibility();
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionCompletedView", $"删除文件失败: {ex.Message}");
            }
        }

        private async void OpenFileFolder(string taskIdOrFileName)
        {
            try
            {
                // 查找任务
                var task = _completedTasks.FirstOrDefault(t => t.Id == taskIdOrFileName) ??
                          _completedTasks.FirstOrDefault(t => t.OriginalFileName == taskIdOrFileName || t.TaskName == taskIdOrFileName);

                if (task != null)
                {
                    // 下载文件
                    var response = await _apiService.DownloadFileAsync(task.Id);
                    if (response.Success && !string.IsNullOrEmpty(response.Data))
                    {
                        // 打开文件所在的文件夹并选中文件
                        if (File.Exists(response.Data))
                        {
                            Process.Start("explorer.exe", $"/select,\"{response.Data}\"");
                            Utils.Logger.Info("ConversionCompletedView", $"文件下载并打开成功: {response.Data}");
                        }
                    }
                    else
                    {
                        Utils.Logger.Error("ConversionCompletedView", $"下载文件失败: {response.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionCompletedView", $"打开文件夹失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 下载文件
        /// </summary>
        private async void DownloadFile(string taskId)
        {
            try
            {
                var task = _completedTasks.FirstOrDefault(t => t.Id == taskId);
                if (task != null)
                {
                    var response = await _apiService.DownloadFileAsync(taskId);
                    if (response.Success && !string.IsNullOrEmpty(response.Data))
                    {
                        Utils.Logger.Info("ConversionCompletedView", $"文件下载成功: {response.Data}");

                        // 可以选择打开文件夹
                        var folder = Path.GetDirectoryName(response.Data);
                        if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                        {
                            Process.Start("explorer.exe", folder);
                        }
                    }
                    else
                    {
                        Utils.Logger.Error("ConversionCompletedView", $"下载文件失败: {response.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionCompletedView", $"下载文件失败: {ex.Message}");
            }
        }

        private void UpdateCompletedStats()
        {
            var statsText = this.FindControl<TextBlock>("CompletedStatsText");
            var clearButton = this.FindControl<Button>("ClearListButton");
            var searchBox = this.FindControl<TextBox>("SearchBox");

            if (statsText != null)
            {
                var count = _filteredTasks.Count;
                var totalCount = _completedTasks.Count;

                // 如果正在搜索，显示过滤后的统计
                if (searchBox != null && searchBox.IsVisible && !string.IsNullOrEmpty(searchBox.Text))
                {
                    var totalSize = _filteredTasks.Sum(t => t.OutputFileSize ?? t.OriginalFileSize ?? 0);
                    var sizeText = FormatFileSize(totalSize);
                    statsText.Text = $"找到 {count} 项（共 {totalCount} 项），{sizeText}";
                }
                else
                {
                    // 计算总大小
                    var totalSize = _completedTasks.Sum(t => t.OutputFileSize ?? t.OriginalFileSize ?? 0);
                    var sizeText = FormatFileSize(totalSize);
                    statsText.Text = count == 0 ? "0 项，0 B" : $"{count} 项，{sizeText}";
                }

                // 控制清空按钮的显示
                if (clearButton != null)
                {
                    clearButton.IsVisible = totalCount > 0;
                }
            }
        }

        private void UpdateEmptyStateVisibility()
        {
            var loadingPanel = this.FindControl<StackPanel>("LoadingPanel");
            var emptyStatePanel = this.FindControl<StackPanel>("EmptyStatePanel");
            var fileListScrollViewer = this.FindControl<ScrollViewer>("FileListScrollViewer");

            if (emptyStatePanel != null && fileListScrollViewer != null && loadingPanel != null)
            {
                bool isLoading = loadingPanel.IsVisible;
                bool hasFiles = _filteredTasks.Count > 0;

                emptyStatePanel.IsVisible = !isLoading && !hasFiles;
                fileListScrollViewer.IsVisible = !isLoading && hasFiles;
            }
        }

        // 新增的事件处理方法
        private void StartConversionBtn_Click(object? sender, RoutedEventArgs e)
        {
            // 触发导航事件，让主窗口处理页面切换
            Utils.Logger.Info("ConversionCompletedView", "请求切换到上传页面");
            NavigateToUploadRequested?.Invoke(this, EventArgs.Empty);
        }

        private async void ClearListBtn_Click(object? sender, RoutedEventArgs e)
        {
            // 清空已完成任务列表
            _completedTasks.Clear();
            _filteredTasks.Clear();

            RefreshCompletedFilesList();
            UpdateCompletedStats();
            UpdateEmptyStateVisibility();
        }

        private void OpenOutputFolderBtn_Click(object? sender, RoutedEventArgs e)
        {
            // 打开输出文件夹
            try
            {
                var outputPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "VideoConversion", "Output");
                if (System.IO.Directory.Exists(outputPath))
                {
                    System.Diagnostics.Process.Start("explorer.exe", outputPath);
                }
                else
                {
                    // 如果文件夹不存在，创建它
                    System.IO.Directory.CreateDirectory(outputPath);
                    System.Diagnostics.Process.Start("explorer.exe", outputPath);
                }
            }
            catch (Exception ex)
            {
                // 处理错误，可以显示消息框或日志
                System.Diagnostics.Debug.WriteLine($"打开输出文件夹失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 格式化文件大小
        /// </summary>
        private string FormatFileSize(long bytes)
        {
            if (bytes == 0) return "0 B";

            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;

            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:F2} {sizes[order]}";
        }

        /// <summary>
        /// 格式化持续时间
        /// </summary>
        private string FormatDuration(TimeSpan? duration)
        {
            if (!duration.HasValue || duration.Value.TotalSeconds <= 0)
                return "未知";

            var ts = duration.Value;
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
            else
                return $"{ts.Minutes:D2}:{ts.Seconds:D2}";
        }

        /// <summary>
        /// 设置加载状态
        /// </summary>
        private void SetLoadingState(bool isLoading)
        {
            var loadingPanel = this.FindControl<StackPanel>("LoadingPanel");
            var fileListScrollViewer = this.FindControl<ScrollViewer>("FileListScrollViewer");
            var emptyStatePanel = this.FindControl<StackPanel>("EmptyStatePanel");

            if (loadingPanel != null)
                loadingPanel.IsVisible = isLoading;

            if (fileListScrollViewer != null)
                fileListScrollViewer.IsVisible = !isLoading && _filteredTasks.Count > 0;

            if (emptyStatePanel != null)
                emptyStatePanel.IsVisible = !isLoading && _filteredTasks.Count == 0;
        }

        /// <summary>
        /// 显示错误消息
        /// </summary>
        private void ShowErrorMessage(string message)
        {
            // 这里可以显示一个错误提示，暂时记录到日志
            Utils.Logger.Error("ConversionCompletedView", message);

            // 可以考虑显示一个Toast通知或者在界面上显示错误信息
            // 暂时更新空状态面板显示错误信息
            var emptyStatePanel = this.FindControl<StackPanel>("EmptyStatePanel");
            if (emptyStatePanel != null && emptyStatePanel.Children.Count > 1)
            {
                if (emptyStatePanel.Children[1] is StackPanel textPanel && textPanel.Children.Count > 0)
                {
                    if (textPanel.Children[0] is TextBlock titleText)
                    {
                        titleText.Text = "加载失败";
                    }
                    if (textPanel.Children.Count > 1 && textPanel.Children[1] is TextBlock descText)
                    {
                        descText.Text = message;
                    }
                }
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Dispose()
        {
            if (_signalRService != null)
            {
                _signalRService.TaskCompleted -= OnTaskCompleted;
                _signalRService.TaskDeleted -= OnTaskDeleted;
            }
        }
    }
}
