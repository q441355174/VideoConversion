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
using System.Text.Json;

namespace VideoConversion_Client.Views
{
    public partial class ConversionCompletedView : UserControl
    {
        private readonly ApiService _apiService;
        private readonly SignalRService _signalRService;
        private readonly DatabaseService _localDbService;
        private List<ConversionTask> _completedTasks = new();
        private List<ConversionTask> _filteredTasks = new();
        private List<LocalConversionTask> _localTasks = new();

        // 事件：请求切换到上传页面
        public event EventHandler? NavigateToUploadRequested;

        // 公共接口：数据关联状态
        public bool IsDataAssociated { get; private set; } = false;
        public string LastAssociationStats { get; private set; } = "";

        public ConversionCompletedView()
        {
            InitializeComponent();

            // 初始化服务
            var settingsService = SystemSettingsService.Instance;
            _apiService = new ApiService { BaseUrl = settingsService.GetServerAddress() };
            _signalRService = new SignalRService(settingsService.GetServerAddress());
            _localDbService = DatabaseService.Instance;

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
        /// 任务完成事件处理（增强版：同步本地状态）
        /// </summary>
        private async void OnTaskCompleted(string taskId, string taskName, bool success, string? errorMessage)
        {
            try
            {
                Utils.Logger.Info("ConversionCompletedView", $"📢 收到任务完成通知: {taskName} (成功: {success})");

                if (success)
                {
                    // 1. 更新本地数据库状态
                    await _localDbService.UpdateTaskStatusAsync(taskId, ConversionStatus.Completed);

                    // 2. 重新加载已完成的文件列表（这会重新关联数据）
                    await LoadCompletedFilesAsync();

                    Utils.Logger.Info("ConversionCompletedView", $"✅ 任务完成处理成功: {taskName}");
                }
                else
                {
                    // 处理失败情况
                    await _localDbService.UpdateTaskStatusAsync(taskId, ConversionStatus.Failed, errorMessage);
                    Utils.Logger.Warning("ConversionCompletedView", $"⚠️ 任务失败: {taskName} - {errorMessage}");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionCompletedView", $"❌ 处理任务完成事件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 任务删除事件处理（增强版：同步本地数据）
        /// </summary>
        private async void OnTaskDeleted(string taskId)
        {
            try
            {
                Utils.Logger.Info("ConversionCompletedView", $"📢 收到任务删除通知: {taskId}");

                // 1. 从内存列表中移除已删除的任务
                var removedCount = _completedTasks.RemoveAll(t => t.Id == taskId);
                _filteredTasks.RemoveAll(t => t.Id == taskId);

                // 2. 同步更新本地数据库（标记为已删除或直接删除）
                var localTask = _localTasks.FirstOrDefault(lt =>
                    lt.ServerTaskId == taskId || lt.CurrentTaskId == taskId);

                if (localTask != null)
                {
                    // 可以选择删除本地记录或标记为已删除
                    // 这里选择保留本地记录但更新状态
                    await _localDbService.UpdateTaskStatusAsync(taskId, ConversionStatus.Cancelled, "服务器任务已删除");
                    Utils.Logger.Info("ConversionCompletedView", $"💾 本地任务状态已更新: {localTask.LocalId}");
                }

                Utils.Logger.Info("ConversionCompletedView", $"✅ 任务删除处理完成: {taskId} (移除 {removedCount} 个)");

                // 3. 更新UI
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    RefreshCompletedFilesList();
                    UpdateCompletedStats();
                    UpdateEmptyStateVisibility();
                });
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionCompletedView", $"❌ 处理任务删除事件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载已完成的文件（增强版：关联本地数据）
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

                Utils.Logger.Info("ConversionCompletedView", "🔄 开始加载已完成任务数据...");

                // 1. 并行获取服务端和本地数据
                var serverTasksTask = _apiService.GetCompletedTasksAsync(1, 100);
                var localTasksTask = _localDbService.GetAllLocalTasksAsync();

                await Task.WhenAll(serverTasksTask, localTasksTask);

                var serverResponse = await serverTasksTask;
                var localTasks = await localTasksTask;

                if (serverResponse.Success && serverResponse.Data != null)
                {
                    Utils.Logger.Info("ConversionCompletedView", $"📥 获取到服务端任务: {serverResponse.Data.Count} 个");
                    Utils.Logger.Info("ConversionCompletedView", $"💾 获取到本地任务: {localTasks.Count} 个");

                    // 2. 关联合并数据
                    var mergedTasks = await MergeServerAndLocalDataAsync(serverResponse.Data, localTasks);

                    _completedTasks = mergedTasks;
                    _filteredTasks = new List<ConversionTask>(_completedTasks);
                    _localTasks = localTasks;

                    Utils.Logger.Info("ConversionCompletedView", $"✅ 数据关联完成，最终任务数: {_completedTasks.Count} 个");

                    // 更新关联状态
                    IsDataAssociated = true;
                    LastAssociationStats = GetDataAssociationStats();
                    Utils.Logger.Info("ConversionCompletedView", $"📊 关联统计: {LastAssociationStats}");

                    // 后台执行批量同步和检查
                    _ = Task.Run(async () =>
                    {
                        await BatchSyncLocalStatusAsync();
                        await BatchCheckLocalFilesAsync();
                    });

                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        SetLoadingState(false);
                        RefreshCompletedFilesListEnhanced();
                        UpdateCompletedStats();
                        UpdateEmptyStateVisibility();
                    });
                }
                else
                {
                    Utils.Logger.Error("ConversionCompletedView", $"❌ 获取服务端数据失败: {serverResponse.Message}");
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        SetLoadingState(false);
                        ShowErrorMessage(serverResponse.Message ?? "加载失败");
                    });
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionCompletedView", $"❌ 加载已完成文件失败: {ex.Message}");
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    SetLoadingState(false);
                    ShowErrorMessage($"加载已完成文件失败: {ex.Message}");
                });
            }
        }

        /// <summary>
        /// 公共方法：手动触发数据重新关联
        /// </summary>
        public async Task RefreshDataAssociationAsync()
        {
            try
            {
                Utils.Logger.Info("ConversionCompletedView", "🔄 手动触发数据重新关联...");
                await LoadCompletedFilesAsync();
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionCompletedView", $"❌ 手动关联失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 公共方法：获取当前关联状态信息
        /// </summary>
        public string GetCurrentAssociationInfo()
        {
            return $"关联状态: {(IsDataAssociated ? "已关联" : "未关联")} | {LastAssociationStats}";
        }

        /// <summary>
        /// 关联合并服务端和本地数据
        /// </summary>
        private async Task<List<ConversionTask>> MergeServerAndLocalDataAsync(List<ConversionTask> serverTasks, List<LocalConversionTask> localTasks)
        {
            try
            {
                Utils.Logger.Info("ConversionCompletedView", "🔗 开始关联服务端和本地数据...");

                var mergedTasks = new List<ConversionTask>();

                foreach (var serverTask in serverTasks)
                {
                    // 1. 查找对应的本地任务
                    var localTask = FindMatchingLocalTask(serverTask, localTasks);

                    // 2. 创建增强的任务对象
                    var enhancedTask = CreateEnhancedTask(serverTask, localTask);

                    mergedTasks.Add(enhancedTask);

                    if (localTask != null)
                    {
                        Utils.Logger.Debug("ConversionCompletedView",
                            $"✅ 关联成功: {serverTask.Id} <-> {localTask.LocalId} ({localTask.FileName})");
                    }
                    else
                    {
                        Utils.Logger.Debug("ConversionCompletedView",
                            $"⚠️ 未找到本地数据: {serverTask.Id} ({serverTask.OriginalFileName})");
                    }
                }

                Utils.Logger.Info("ConversionCompletedView", $"🎯 数据关联完成: {mergedTasks.Count} 个任务");
                return mergedTasks;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionCompletedView", $"❌ 数据关联失败: {ex.Message}");
                // 发生错误时返回原始服务端数据
                return serverTasks;
            }
        }

        /// <summary>
        /// 查找匹配的本地任务
        /// </summary>
        private LocalConversionTask? FindMatchingLocalTask(ConversionTask serverTask, List<LocalConversionTask> localTasks)
        {
            // 优先级1: 通过ServerTaskId精确匹配
            var matchByServerId = localTasks.FirstOrDefault(lt =>
                !string.IsNullOrEmpty(lt.ServerTaskId) && lt.ServerTaskId == serverTask.Id);
            if (matchByServerId != null)
            {
                return matchByServerId;
            }

            // 优先级2: 通过CurrentTaskId匹配
            var matchByCurrentId = localTasks.FirstOrDefault(lt =>
                !string.IsNullOrEmpty(lt.CurrentTaskId) && lt.CurrentTaskId == serverTask.Id);
            if (matchByCurrentId != null)
            {
                return matchByCurrentId;
            }

            // 优先级3: 通过文件名和大小模糊匹配
            var matchByFileInfo = localTasks.FirstOrDefault(lt =>
                lt.FileName == serverTask.OriginalFileName &&
                Math.Abs(lt.FileSize - (serverTask.OriginalFileSize ?? 0)) < 1024); // 允许1KB误差
            if (matchByFileInfo != null)
            {
                Utils.Logger.Debug("ConversionCompletedView",
                    $"🔍 通过文件信息匹配: {serverTask.OriginalFileName}");
                return matchByFileInfo;
            }

            return null;
        }

        /// <summary>
        /// 创建增强的任务对象（合并服务端和本地数据）
        /// </summary>
        private ConversionTask CreateEnhancedTask(ConversionTask serverTask, LocalConversionTask? localTask)
        {
            // 基于服务端数据创建任务对象
            var enhancedTask = new ConversionTask
            {
                // 服务端核心数据
                Id = serverTask.Id,
                TaskName = serverTask.TaskName,
                OriginalFileName = serverTask.OriginalFileName,
                OriginalFilePath = serverTask.OriginalFilePath,
                OutputFileName = serverTask.OutputFileName,
                OutputFilePath = serverTask.OutputFilePath,
                Status = serverTask.Status,
                Progress = serverTask.Progress,
                CreatedAt = serverTask.CreatedAt,
                StartedAt = serverTask.StartedAt,
                CompletedAt = serverTask.CompletedAt,
                OriginalFileSize = serverTask.OriginalFileSize,
                OutputFileSize = serverTask.OutputFileSize,
                InputFormat = serverTask.InputFormat,
                OutputFormat = serverTask.OutputFormat,
                VideoCodec = serverTask.VideoCodec,
                AudioCodec = serverTask.AudioCodec,
                VideoQuality = serverTask.VideoQuality,
                AudioQuality = serverTask.AudioQuality,
                Resolution = serverTask.Resolution,
                FrameRate = serverTask.FrameRate,
                ErrorMessage = serverTask.ErrorMessage,
                ConversionSpeed = serverTask.ConversionSpeed,
                EstimatedTimeRemaining = serverTask.EstimatedTimeRemaining,
                Duration = serverTask.Duration,
                CurrentTime = serverTask.CurrentTime
            };

            // 如果有本地数据，则增强任务信息
            if (localTask != null)
            {
                // 添加本地特有的信息到自定义字段
                enhancedTask.Notes = CreateEnhancedNotes(serverTask.Notes, localTask);

                // 如果本地有更详细的错误信息，使用本地的
                if (!string.IsNullOrEmpty(localTask.LastError) &&
                    string.IsNullOrEmpty(enhancedTask.ErrorMessage))
                {
                    enhancedTask.ErrorMessage = localTask.LastError;
                }

                // 更新输出文件大小（如果本地有更准确的数据）
                if (localTask.OutputFileSize > 0 && enhancedTask.OutputFileSize == 0)
                {
                    enhancedTask.OutputFileSize = localTask.OutputFileSize;
                }
            }

            return enhancedTask;
        }

        /// <summary>
        /// 创建增强的备注信息（包含本地状态）
        /// </summary>
        private string CreateEnhancedNotes(string originalNotes, LocalConversionTask localTask)
        {
            var enhancedInfo = new List<string>();

            // 保留原始备注
            if (!string.IsNullOrEmpty(originalNotes))
            {
                enhancedInfo.Add(originalNotes);
            }

            // 添加本地状态信息
            var localStatus = new
            {
                LocalId = localTask.LocalId,
                IsDownloaded = localTask.IsDownloaded,
                LocalOutputPath = localTask.LocalOutputPath,
                DownloadedAt = localTask.DownloadedAt,
                SourceFileProcessed = localTask.SourceFileProcessed,
                SourceFileAction = localTask.SourceFileAction,
                ArchivePath = localTask.ArchivePath,
                RetryCount = localTask.RetryCount
            };

            enhancedInfo.Add($"本地状态: {JsonSerializer.Serialize(localStatus)}");

            return string.Join(" | ", enhancedInfo);
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

            RefreshCompletedFilesListEnhanced();
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
                    Utils.Logger.Info("ConversionCompletedView", $"🔍 准备打开文件: {task.TaskName}");

                    // 1. 首先检查本地是否已下载
                    var localTask = FindMatchingLocalTask(task, _localTasks);
                    string? localFilePath = null;

                    if (localTask != null && localTask.IsDownloaded && !string.IsNullOrEmpty(localTask.LocalOutputPath))
                    {
                        if (File.Exists(localTask.LocalOutputPath))
                        {
                            localFilePath = localTask.LocalOutputPath;
                            Utils.Logger.Info("ConversionCompletedView", $"✅ 使用本地已下载文件: {localFilePath}");
                        }
                        else
                        {
                            Utils.Logger.Warning("ConversionCompletedView", $"⚠️ 本地文件不存在，重新下载: {localTask.LocalOutputPath}");
                            // 更新本地数据库状态
                            await _localDbService.UpdateTaskDownloadStatusAsync(task.Id, "");
                        }
                    }

                    // 2. 如果本地没有文件，从服务器下载
                    if (string.IsNullOrEmpty(localFilePath))
                    {
                        Utils.Logger.Info("ConversionCompletedView", "📥 从服务器下载文件...");
                        var response = await _apiService.DownloadFileAsync(task.Id);
                        if (response.Success && !string.IsNullOrEmpty(response.Data))
                        {
                            localFilePath = response.Data;

                            // 更新本地数据库的下载状态
                            if (localTask != null)
                            {
                                await _localDbService.UpdateTaskDownloadStatusAsync(task.Id, localFilePath);
                                Utils.Logger.Info("ConversionCompletedView", $"💾 更新本地下载状态: {task.Id}");
                            }
                        }
                        else
                        {
                            Utils.Logger.Error("ConversionCompletedView", $"❌ 下载文件失败: {response.Message}");
                            return;
                        }
                    }

                    // 3. 打开文件所在的文件夹并选中文件
                    if (!string.IsNullOrEmpty(localFilePath) && File.Exists(localFilePath))
                    {
                        Process.Start("explorer.exe", $"/select,\"{localFilePath}\"");
                        Utils.Logger.Info("ConversionCompletedView", $"✅ 文件打开成功: {localFilePath}");
                    }
                    else
                    {
                        Utils.Logger.Error("ConversionCompletedView", "❌ 文件不存在或下载失败");
                    }
                }
                else
                {
                    Utils.Logger.Error("ConversionCompletedView", $"❌ 未找到任务: {taskIdOrFileName}");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionCompletedView", $"❌ 打开文件夹失败: {ex.Message}");
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

        /// <summary>
        /// 获取任务的本地状态信息
        /// </summary>
        private string GetTaskLocalStatusInfo(ConversionTask task)
        {
            var localTask = FindMatchingLocalTask(task, _localTasks);
            if (localTask == null)
            {
                return "无本地数据";
            }

            var statusParts = new List<string>();

            // 下载状态
            if (localTask.IsDownloaded)
            {
                statusParts.Add("✅ 已下载");
                if (localTask.DownloadedAt.HasValue)
                {
                    statusParts.Add($"下载时间: {localTask.DownloadedAt.Value:MM-dd HH:mm}");
                }
            }
            else
            {
                statusParts.Add("📥 未下载");
            }

            // 源文件处理状态
            if (localTask.SourceFileProcessed)
            {
                statusParts.Add($"源文件: {localTask.SourceFileAction}");
            }

            // 重试信息
            if (localTask.RetryCount > 0)
            {
                statusParts.Add($"重试: {localTask.RetryCount}次");
            }

            return string.Join(" | ", statusParts);
        }

        /// <summary>
        /// 检查任务是否有本地文件可用
        /// </summary>
        private bool HasLocalFileAvailable(ConversionTask task)
        {
            var localTask = FindMatchingLocalTask(task, _localTasks);
            return localTask != null &&
                   localTask.IsDownloaded &&
                   !string.IsNullOrEmpty(localTask.LocalOutputPath) &&
                   File.Exists(localTask.LocalOutputPath);
        }

        /// <summary>
        /// 获取任务的完整文件路径（优先本地，其次服务器）
        /// </summary>
        private async Task<string?> GetTaskFilePathAsync(ConversionTask task)
        {
            // 1. 检查本地文件
            var localTask = FindMatchingLocalTask(task, _localTasks);
            if (localTask != null && localTask.IsDownloaded && !string.IsNullOrEmpty(localTask.LocalOutputPath))
            {
                if (File.Exists(localTask.LocalOutputPath))
                {
                    return localTask.LocalOutputPath;
                }
            }

            // 2. 从服务器下载
            try
            {
                var response = await _apiService.DownloadFileAsync(task.Id);
                if (response.Success && !string.IsNullOrEmpty(response.Data))
                {
                    // 更新本地数据库
                    if (localTask != null)
                    {
                        await _localDbService.UpdateTaskDownloadStatusAsync(task.Id, response.Data);
                    }
                    return response.Data;
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionCompletedView", $"获取文件路径失败: {ex.Message}");
            }

            return null;
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
        /// 格式化文件大小（可空版本）
        /// </summary>
        private string FormatFileSize(long? bytes)
        {
            return FormatFileSize(bytes ?? 0);
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
        /// 刷新完成文件列表（增强版：显示本地状态）
        /// </summary>
        private void RefreshCompletedFilesListEnhanced()
        {
            try
            {
                var completedFilesListBox = this.FindControl<ListBox>("CompletedFilesListBox");
                if (completedFilesListBox != null && _filteredTasks.Any())
                {
                    // 创建增强的显示项目
                    var enhancedItems = _filteredTasks.Select(task => new
                    {
                        Task = task,
                        LocalStatus = GetTaskLocalStatusInfo(task),
                        HasLocalFile = HasLocalFileAvailable(task),
                        DisplayName = $"{task.TaskName} ({task.OriginalFileName})",
                        StatusIcon = GetTaskStatusIcon(task),
                        FileSizeText = FormatFileSize(task.OutputFileSize ?? task.OriginalFileSize ?? 0),
                        CompletedTimeText = task.CompletedAt?.ToString("MM-dd HH:mm") ?? "未知时间"
                    }).ToList();

                    completedFilesListBox.ItemsSource = enhancedItems;

                    Utils.Logger.Debug("ConversionCompletedView", $"📋 刷新完成列表: {enhancedItems.Count} 个项目");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionCompletedView", $"❌ 刷新完成列表失败: {ex.Message}");
                // 回退到原始方法
                RefreshCompletedFilesList();
            }
        }

        /// <summary>
        /// 获取任务状态图标
        /// </summary>
        private string GetTaskStatusIcon(ConversionTask task)
        {
            var localTask = FindMatchingLocalTask(task, _localTasks);

            if (localTask != null && localTask.IsDownloaded)
            {
                return "💾"; // 已下载到本地
            }

            return task.Status switch
            {
                ConversionStatus.Completed => "✅",
                ConversionStatus.Failed => "❌",
                ConversionStatus.Cancelled => "⏹️",
                _ => "📄"
            };
        }



        /// <summary>
        /// 批量同步本地状态
        /// </summary>
        private async Task BatchSyncLocalStatusAsync()
        {
            try
            {
                Utils.Logger.Info("ConversionCompletedView", "🔄 开始批量同步本地状态...");

                int syncCount = 0;
                foreach (var task in _completedTasks)
                {
                    var localTask = FindMatchingLocalTask(task, _localTasks);
                    if (localTask != null)
                    {
                        // 同步状态到本地数据库
                        await _localDbService.UpdateTaskStatusAsync(task.Id, task.Status, task.ErrorMessage);

                        // 如果服务端任务已完成但本地状态不是完成，则更新
                        if (task.Status == ConversionStatus.Completed && localTask.Status != ConversionStatus.Completed)
                        {
                            await _localDbService.UpdateTaskStatusAsync(task.Id, ConversionStatus.Completed);
                            syncCount++;
                        }
                    }
                }

                Utils.Logger.Info("ConversionCompletedView", $"✅ 批量同步完成: {syncCount} 个任务状态已更新");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionCompletedView", $"❌ 批量同步失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 批量检查本地文件状态
        /// </summary>
        private async Task BatchCheckLocalFilesAsync()
        {
            try
            {
                Utils.Logger.Info("ConversionCompletedView", "🔍 开始批量检查本地文件状态...");

                int checkedCount = 0;
                int missingCount = 0;

                foreach (var localTask in _localTasks.Where(lt => lt.IsDownloaded))
                {
                    if (!string.IsNullOrEmpty(localTask.LocalOutputPath))
                    {
                        if (!File.Exists(localTask.LocalOutputPath))
                        {
                            // 文件不存在，更新状态
                            await _localDbService.UpdateTaskDownloadStatusAsync(
                                localTask.CurrentTaskId ?? localTask.LocalId, "");
                            missingCount++;

                            Utils.Logger.Warning("ConversionCompletedView",
                                $"⚠️ 本地文件丢失: {localTask.FileName} - {localTask.LocalOutputPath}");
                        }
                        checkedCount++;
                    }
                }

                Utils.Logger.Info("ConversionCompletedView",
                    $"✅ 文件检查完成: 检查 {checkedCount} 个，发现 {missingCount} 个丢失");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionCompletedView", $"❌ 批量文件检查失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取数据关联统计信息
        /// </summary>
        private string GetDataAssociationStats()
        {
            try
            {
                var serverTaskCount = _completedTasks.Count;
                var localTaskCount = _localTasks.Count;

                var associatedCount = _completedTasks.Count(serverTask =>
                    FindMatchingLocalTask(serverTask, _localTasks) != null);

                var downloadedCount = _localTasks.Count(lt => lt.IsDownloaded);

                return $"服务端: {serverTaskCount} | 本地: {localTaskCount} | 关联: {associatedCount} | 已下载: {downloadedCount}";
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionCompletedView", $"❌ 获取统计信息失败: {ex.Message}");
                return "统计信息获取失败";
            }
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
