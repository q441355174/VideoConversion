using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VideoConversion_Client.Models;
using VideoConversion_Client.Utils;

namespace VideoConversion_Client.Views
{
    public partial class FileUploadView : UserControl
    {
        public event EventHandler<EventArgs>? SettingsRequested;

        private bool _hasFiles = false;
        private bool _isConverting = false;
        private List<string> _selectedFiles = new List<string>();

        // 使用ObservableCollection来管理文件列表
        public ObservableCollection<FileItemViewModel> FileItems { get; } = new();

        public FileUploadView()
        {
            InitializeComponent();
            UpdateViewState();
            SetupDragAndDrop();

            // 设置ItemsControl的数据源
            var fileListContainer = this.FindControl<ItemsControl>("FileListContainer");
            if (fileListContainer != null)
            {
                fileListContainer.ItemsSource = FileItems;
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void SetupDragAndDrop()
        {
            // 为空状态视图设置拖拽事件
            var emptyStateView = this.FindControl<Border>("EmptyStateView");
            if (emptyStateView != null)
            {
                emptyStateView.AddHandler(DragDrop.DragEnterEvent, FileDropZone_DragEnter);
                emptyStateView.AddHandler(DragDrop.DragLeaveEvent, FileDropZone_DragLeave);
                emptyStateView.AddHandler(DragDrop.DropEvent, FileDropZone_Drop);
            }

            // 为文件列表视图设置拖拽事件
            var fileListView = this.FindControl<Grid>("FileListView");
            if (fileListView != null)
            {
                var border = fileListView.Children.OfType<Border>().FirstOrDefault();
                if (border != null)
                {
                    border.AddHandler(DragDrop.DragEnterEvent, FileDropZone_DragEnter);
                    border.AddHandler(DragDrop.DragLeaveEvent, FileDropZone_DragLeave);
                    border.AddHandler(DragDrop.DropEvent, FileDropZone_Drop);
                }
            }
        }



        // 更新视图状态
        private void UpdateViewState()
        {
            var emptyStateView = this.FindControl<Border>("EmptyStateView");
            var fileListView = this.FindControl<Grid>("FileListView");

            if (emptyStateView != null && fileListView != null)
            {
                if (_hasFiles)
                {
                    emptyStateView.IsVisible = false;
                    fileListView.IsVisible = true;
                }
                else
                {
                    emptyStateView.IsVisible = true;
                    fileListView.IsVisible = false;
                }
            }
        }

        // 文件拖拽区域点击事件
        private async void FileDropZone_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            await OpenFileDialog();
        }

        // 拖拽进入事件
        private void FileDropZone_DragEnter(object? sender, DragEventArgs e)
        {
            // 检查拖拽的数据是否包含文件
            if (e.Data.Contains(DataFormats.Files))
            {
                e.DragEffects = DragDropEffects.Copy;

                // 更新拖拽区域的视觉效果
                if (sender is Border border)
                {
                    border.BorderBrush = Avalonia.Media.Brush.Parse("#9b59b6");
                    border.BorderThickness = new Avalonia.Thickness(3);
                    border.Background = Avalonia.Media.Brush.Parse("#f0f0ff");
                }
            }
            else
            {
                e.DragEffects = DragDropEffects.None;
            }
        }

        // 拖拽离开事件
        private void FileDropZone_DragLeave(object? sender, DragEventArgs e)
        {
            // 恢复拖拽区域的原始视觉效果
            if (sender is Border border)
            {
                border.BorderBrush = Avalonia.Media.Brush.Parse("#e0e0e0");
                border.BorderThickness = new Avalonia.Thickness(2);
                border.Background = Avalonia.Media.Brush.Parse("#f5f5f5");
            }
        }

        // 拖拽放下事件
        private async void FileDropZone_Drop(object? sender, DragEventArgs e)
        {
            // 恢复拖拽区域的原始视觉效果
            if (sender is Border border)
            {
                border.BorderBrush = Avalonia.Media.Brush.Parse("#e0e0e0");
                border.BorderThickness = new Avalonia.Thickness(2);
                border.Background = Avalonia.Media.Brush.Parse("#f5f5f5");
            }

            // 处理拖拽的文件
            if (e.Data.Contains(DataFormats.Files))
            {
                var files = e.Data.GetFiles();
                if (files != null)
                {
                    await ProcessDroppedFiles(files);
                }
            }
        }

        // 选择文件按钮点击事件
        private async void SelectFileBtn_Click(object? sender, RoutedEventArgs e)
        {
            await OpenFileDialog();
        }

        // 选择文件夹按钮点击事件
        private async void SelectFolderBtn_Click(object? sender, RoutedEventArgs e)
        {
            await OpenFolderDialog();
        }

        // 打开文件对话框
        private async Task OpenFileDialog()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "选择视频文件",
                AllowMultiple = true,
                FileTypeFilter = new[]
                {
                    new Avalonia.Platform.Storage.FilePickerFileType("视频文件")
                    {
                        Patterns = new[] { "*.mp4", "*.avi", "*.mov", "*.mkv", "*.wmv", "*.flv", "*.webm", "*.m4v", "*.3gp" }
                    }
                }
            });

            if (files.Count > 0)
            {
                foreach (var file in files)
                {
                    AddFile(file.Path.LocalPath);
                }
            }
        }

        // 打开文件夹对话框
        private async Task OpenFolderDialog()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                Title = "选择包含视频文件的文件夹",
                AllowMultiple = false
            });

            if (folders.Count > 0)
            {
                var folder = folders[0];
                var videoFiles = Directory.GetFiles(folder.Path.LocalPath, "*.*", SearchOption.AllDirectories)
                    .Where(file => new[] { ".mp4", ".avi", ".mov", ".mkv", ".wmv", ".flv", ".webm", ".m4v", ".3gp" }
                        .Contains(Path.GetExtension(file).ToLower()))
                    .ToArray();

                foreach (var file in videoFiles)
                {
                    AddFile(file);
                }
            }
        }

        // 处理拖拽的文件和文件夹
        private async Task ProcessDroppedFiles(IEnumerable<IStorageItem> items)
        {
            var supportedExtensions = new[] { ".mp4", ".avi", ".mov", ".mkv", ".wmv", ".flv", ".webm", ".m4v", ".3gp" };

            foreach (var item in items)
            {
                if (item is IStorageFile file)
                {
                    // 处理单个文件
                    var extension = Path.GetExtension(file.Name).ToLower();
                    if (supportedExtensions.Contains(extension))
                    {
                        AddFile(file.Path.LocalPath);
                    }
                }
                else if (item is IStorageFolder folder)
                {
                    // 处理文件夹 - 递归查找视频文件
                    await ProcessFolderRecursively(folder.Path.LocalPath, supportedExtensions);
                }
            }
        }

        // 递归处理文件夹中的视频文件
        private async Task ProcessFolderRecursively(string folderPath, string[] supportedExtensions)
        {
            try
            {
                // 获取文件夹中的所有视频文件
                var videoFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
                    .Where(file => supportedExtensions.Contains(Path.GetExtension(file).ToLower()))
                    .ToArray();

                // 添加找到的视频文件
                foreach (var file in videoFiles)
                {
                    AddFile(file);
                }

                // 如果找到了文件，显示提示信息
                if (videoFiles.Length > 0)
                {
                    // 可以在这里添加状态提示，比如"已添加 X 个视频文件"
                    System.Diagnostics.Debug.WriteLine($"从文件夹 {folderPath} 中添加了 {videoFiles.Length} 个视频文件");
                }
            }
            catch (Exception ex)
            {
                // 处理文件夹访问错误
                System.Diagnostics.Debug.WriteLine($"处理文件夹时出错: {ex.Message}");
            }
        }

        // 添加文件到列表
        private void AddFile(string filePath)
        {
            if (!_selectedFiles.Contains(filePath))
            {
                _selectedFiles.Add(filePath);
                CreateFileItemViewModel(filePath);

                if (!_hasFiles)
                {
                    _hasFiles = true;
                    UpdateViewState();
                }
            }
        }

        // 创建文件项ViewModel
        private async void CreateFileItemViewModel(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            var fileInfo = new FileInfo(filePath);

            // 创建FileItemViewModel
            var fileItemViewModel = new FileItemViewModel
            {
                FileName = fileName,
                FilePath = filePath,
                SourceFormat = Path.GetExtension(filePath).TrimStart('.').ToUpper(),
                SourceResolution = "分析中...",
                FileSize = FileSizeFormatter.FormatBytesAuto(fileInfo.Length),
                Duration = "分析中...",
                TargetFormat = "MP4",
                TargetResolution = "1920×1080",
                Status = FileItemStatus.Pending,
                Progress = 0,
                StatusText = "等待处理"
            };

            // 添加到集合中
            FileItems.Add(fileItemViewModel);

            // 异步获取视频信息和缩略图
            _ = Task.Run(async () =>
            {
                try
                {
                    // 获取视频信息
                    var videoInfo = await Services.VideoInfoService.Instance.GetVideoInfoAsync(filePath);

                    // 获取缩略图
                    var thumbnail = await Services.ThumbnailService.Instance.GetThumbnailAsync(filePath, 100, 70);

                    // 在UI线程更新信息
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        fileItemViewModel.SourceResolution = videoInfo.Resolution;
                        fileItemViewModel.Duration = videoInfo.Duration;
                        fileItemViewModel.Thumbnail = thumbnail;
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"获取视频信息失败: {ex.Message}");
                }
            });
        }

        // 转换文件事件处理
        private async void ConvertFile_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is FileItemViewModel fileItem)
            {
                await StartConversionAsync(fileItem);
            }
        }

        // 删除文件事件处理
        private void RemoveFile_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is FileItemViewModel fileItem)
            {
                RemoveFileItem(fileItem);
            }
        }

        // 删除文件项
        private void RemoveFileItem(FileItemViewModel fileItem)
        {
            _selectedFiles.Remove(fileItem.FilePath);
            FileItems.Remove(fileItem);

            if (FileItems.Count == 0)
            {
                _hasFiles = false;
                UpdateViewState();
            }
        }

        // 开始转换单个文件
        private async Task StartConversionAsync(FileItemViewModel fileItem)
        {
            try
            {
                // 设置转换状态
                fileItem.Status = FileItemStatus.Converting;
                fileItem.StatusText = "正在转换...";
                fileItem.Progress = 0;

                // 这里应该调用实际的转换服务
                // 暂时模拟转换过程
                for (int i = 0; i <= 100; i += 5)
                {
                    fileItem.Progress = i;

                    // 更新状态文本显示进度
                    fileItem.StatusText = $"正在转换... {i}%";

                    await Task.Delay(100); // 模拟转换时间
                }

                // 转换完成
                fileItem.Status = FileItemStatus.Completed;
                fileItem.StatusText = "转换完成";
                fileItem.Progress = 100;

                // 显示成功通知
                ShowNotification($"转换完成: {fileItem.FileName}", "success");
            }
            catch (Exception ex)
            {
                // 转换失败
                fileItem.Status = FileItemStatus.Failed;
                fileItem.StatusText = $"转换失败: {ex.Message}";
                fileItem.Progress = 0;

                // 显示错误通知
                ShowNotification($"转换失败: {fileItem.FileName}", "error");
                System.Diagnostics.Debug.WriteLine($"转换失败: {ex.Message}");
            }
        }

        // 显示通知消息
        private void ShowNotification(string message, string type)
        {
            // 这里可以实现通知显示逻辑
            // 暂时输出到调试控制台
            System.Diagnostics.Debug.WriteLine($"[{type.ToUpper()}] {message}");
        }

        // 清空所有文件
        private void ClearAllBtn_Click(object? sender, RoutedEventArgs e)
        {
            _selectedFiles.Clear();
            FileItems.Clear();

            if (_hasFiles)
            {
                _hasFiles = false;
                UpdateViewState();
            }
        }

        // 创建文件项UI
        private async Task<Border> CreateFileItemUIAsync(string fileName, string format, string resolution, string size, string duration, string filePath, Models.FileItemProgress progressInfo)
        {
            var border = new Border
            {
                Background = Avalonia.Media.Brushes.White,
                BorderBrush = Avalonia.Media.Brush.Parse("#e0e0e0"),
                BorderThickness = new Avalonia.Thickness(1),
                CornerRadius = new Avalonia.CornerRadius(8),
                Padding = new Avalonia.Thickness(15),
                Margin = new Avalonia.Thickness(0, 5),
                Tag = filePath // 用于标识文件路径
            };

            // 主容器使用Grid，支持重叠布局
            var mainGrid = new Grid();

            // 内容网格
            var contentGrid = new Grid();
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition(120, GridUnitType.Pixel));
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            // 缩略图
            var thumbnailBorder = new Border
            {
                Background = Avalonia.Media.Brush.Parse("#f0f0f0"),
                CornerRadius = new Avalonia.CornerRadius(6),
                Width = 100,
                Height = 70,
                Tag = $"thumbnail_{filePath}" // 用于后续更新缩略图
            };

            // 异步加载缩略图
            _ = Task.Run(async () =>
            {
                try
                {
                    var thumbnail = await Services.ThumbnailService.Instance.GetThumbnailAsync(filePath, 100, 70);
                    if (thumbnail != null)
                    {
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            var image = new Avalonia.Controls.Image
                            {
                                Source = thumbnail,
                                Stretch = Avalonia.Media.Stretch.UniformToFill
                            };
                            thumbnailBorder.Child = image;
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"加载缩略图失败: {ex.Message}");
                }
            });

            Grid.SetColumn(thumbnailBorder, 0);

            // 文件信息
            var infoPanel = CreateFileInfoPanel(fileName, format, resolution, size, duration, progressInfo);
            Grid.SetColumn(infoPanel, 1);

            // 转换按钮
            var convertPanel = CreateConvertPanel(filePath);
            Grid.SetColumn(convertPanel, 2);

            contentGrid.Children.Add(thumbnailBorder);
            contentGrid.Children.Add(infoPanel);
            contentGrid.Children.Add(convertPanel);

            // 删除按钮（右上角）
            var deleteBtn = new Button
            {
                Content = "✕",
                Background = Avalonia.Media.Brushes.Transparent,
                Foreground = Avalonia.Media.Brush.Parse("#999"),
                BorderThickness = new Avalonia.Thickness(0),
                Padding = new Avalonia.Thickness(6),
                FontSize = 16,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                Margin = new Avalonia.Thickness(0, 5, 5, 0),
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
            };
            Avalonia.Controls.ToolTip.SetTip(deleteBtn, "删除文件");
            deleteBtn.Click += (s, e) => RemoveFile(filePath);

            // 添加到主容器
            mainGrid.Children.Add(contentGrid);
            mainGrid.Children.Add(deleteBtn);

            border.Child = mainGrid;
            return border;
        }

        // 创建文件信息面板
        private StackPanel CreateFileInfoPanel(string fileName, string format, string resolution, string size, string duration, Models.FileItemProgress progressInfo)
        {
            var panel = new StackPanel
            {
                Margin = new Avalonia.Thickness(15, 0),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            // 文件名
            var fileNameText = new TextBlock
            {
                Text = fileName,
                FontSize = 15,
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                Foreground = Avalonia.Media.Brush.Parse("#333"),
                TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
                MaxWidth = 400, // 限制最大宽度，避免过长
                TextWrapping = Avalonia.Media.TextWrapping.NoWrap
            };
            panel.Children.Add(fileNameText);

            // 主要对比行：原文件信息 → 转换后信息
            var comparisonRow = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 15,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Margin = new Avalonia.Thickness(0, 8, 0, 0)
            };

            // 原文件信息区域
            var sourceInfoPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 12,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            var sourceFormatPanel = CreateInfoItemWithIcon("📄", format);
            var sourceResolutionPanel = CreateInfoItemWithIcon("📐", resolution);

            sourceInfoPanel.Children.Add(sourceFormatPanel);
            sourceInfoPanel.Children.Add(sourceResolutionPanel);

            // 转换箭头
            var arrowText = new TextBlock
            {
                Text = "→",
                FontSize = 18,
                FontWeight = Avalonia.Media.FontWeight.Bold,
                Foreground = Avalonia.Media.Brush.Parse("#9b59b6"),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Margin = new Avalonia.Thickness(8, 0)
            };

            // 转换后信息区域
            var targetInfoPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 12,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Tag = "targetInfo" // 用于后续根据设置更新
            };

            var targetFormatPanel = CreateInfoItemWithIcon("🎯", "MP4");
            var targetResolutionPanel = CreateInfoItemWithIcon("📐", "1920×1080");

            targetInfoPanel.Children.Add(targetFormatPanel);
            targetInfoPanel.Children.Add(targetResolutionPanel);

            // 组装主要对比行
            comparisonRow.Children.Add(sourceInfoPanel);
            comparisonRow.Children.Add(arrowText);
            comparisonRow.Children.Add(targetInfoPanel);

            panel.Children.Add(comparisonRow);

            // 次要信息行（文件大小和时长）
            var secondaryInfoRow = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 15,
                Margin = new Avalonia.Thickness(0, 5, 0, 0)
            };

            var sizePanel = CreateInfoItemWithIcon("💾", size);
            var durationPanel = CreateInfoItemWithIcon("⏱️", duration);

            secondaryInfoRow.Children.Add(sizePanel);
            secondaryInfoRow.Children.Add(durationPanel);

            panel.Children.Add(secondaryInfoRow);

            // 进度信息面板
            var progressPanel = CreateProgressPanel(progressInfo);
            panel.Children.Add(progressPanel);

            return panel;
        }

        // 创建进度面板
        private StackPanel CreateProgressPanel(Models.FileItemProgress progressInfo)
        {
            var panel = new StackPanel
            {
                Margin = new Avalonia.Thickness(0, 10, 0, 0),
                Tag = $"progress_{progressInfo.FilePath}" // 用于后续更新
            };

            // 状态文本
            var statusText = new TextBlock
            {
                Text = progressInfo.StatusDisplayText,
                FontSize = 12,
                Foreground = Avalonia.Media.Brush.Parse("#666"),
                Margin = new Avalonia.Thickness(0, 0, 0, 5),
                Tag = "statusText"
            };
            panel.Children.Add(statusText);

            // 进度条
            var progressBar = new Avalonia.Controls.ProgressBar
            {
                Value = progressInfo.Progress,
                Minimum = 0,
                Maximum = 100,
                Height = 6,
                Background = Avalonia.Media.Brush.Parse("#f0f0f0"),
                Foreground = Avalonia.Media.Brush.Parse("#9b59b6"),
                CornerRadius = new Avalonia.CornerRadius(3),
                IsVisible = false, // 初始隐藏，开始处理时显示
                Tag = "progressBar"
            };
            panel.Children.Add(progressBar);

            // 详细信息文本（上传速度、剩余时间等）
            var detailText = new TextBlock
            {
                Text = "",
                FontSize = 11,
                Foreground = Avalonia.Media.Brush.Parse("#999"),
                Margin = new Avalonia.Thickness(0, 3, 0, 0),
                IsVisible = false, // 初始隐藏
                Tag = "detailText"
            };
            panel.Children.Add(detailText);

            return panel;
        }



        // 更新文件信息
        private void UpdateFileItemInfo(string filePath, Services.VideoFileInfo videoInfo)
        {
            try
            {
                var fileListContainer = this.FindControl<StackPanel>("FileListContainer");
                if (fileListContainer == null) return;

                // 查找对应的文件项
                foreach (var child in fileListContainer.Children)
                {
                    if (child is Border border && border.Tag?.ToString() == filePath)
                    {
                        // 更新文件信息显示
                        UpdateFileInfoInBorder(border, videoInfo);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新文件信息失败: {ex.Message}");
            }
        }

        // 在Border中更新文件信息
        private void UpdateFileInfoInBorder(Border border, Services.VideoFileInfo videoInfo)
        {
            try
            {
                if (border.Child is Grid grid)
                {
                    // 查找信息面板并更新
                    foreach (var child in grid.Children)
                    {
                        if (child is StackPanel panel && Grid.GetColumn(child) == 1)
                        {
                            UpdateInfoPanel(panel, videoInfo);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新Border中的文件信息失败: {ex.Message}");
            }
        }

        // 创建带图标的信息项
        private StackPanel CreateInfoItemWithIcon(string icon, string text)
        {
            var panel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 4,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            var iconText = new TextBlock
            {
                Text = icon,
                FontSize = 13,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Margin = new Avalonia.Thickness(0, 0, 2, 0)
            };

            var contentText = new TextBlock
            {
                Text = text,
                FontSize = 13,
                Foreground = Avalonia.Media.Brush.Parse("#555"),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Tag = "content", // 用于后续更新内容
                FontWeight = Avalonia.Media.FontWeight.Medium
            };

            panel.Children.Add(iconText);
            panel.Children.Add(contentText);

            return panel;
        }

        // 更新信息面板
        private void UpdateInfoPanel(StackPanel panel, Services.VideoFileInfo videoInfo)
        {
            try
            {
                // 更新分辨率和时长信息
                foreach (var child in panel.Children)
                {
                    if (child is StackPanel subPanel)
                    {
                        foreach (var subChild in subPanel.Children)
                        {
                            if (subChild is StackPanel infoPanel)
                            {
                                // 查找内容文本块
                                foreach (var infoChild in infoPanel.Children)
                                {
                                    if (infoChild is TextBlock textBlock && textBlock.Tag?.ToString() == "content")
                                    {
                                        var text = textBlock.Text;
                                        if (text == "分析中...")
                                        {
                                            // 根据图标判断是什么信息
                                            var iconText = infoPanel.Children.FirstOrDefault() as TextBlock;
                                            if (iconText != null)
                                            {
                                                switch (iconText.Text)
                                                {
                                                    case "📐": // 分辨率
                                                        textBlock.Text = videoInfo.Resolution;
                                                        break;
                                                    case "⏱️": // 时长
                                                        textBlock.Text = videoInfo.Duration;
                                                        break;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新信息面板失败: {ex.Message}");
            }
        }

        // 更新文件项进度
        public void UpdateFileProgress(string filePath, Models.FileItemProgress progressInfo)
        {
            try
            {
                var fileListContainer = this.FindControl<StackPanel>("FileListContainer");
                if (fileListContainer == null) return;

                // 查找对应的文件项
                foreach (var child in fileListContainer.Children)
                {
                    if (child is Border border && border.Tag?.ToString() == filePath)
                    {
                        UpdateProgressInBorder(border, progressInfo);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新文件进度失败: {ex.Message}");
            }
        }

        // 在Border中更新进度信息
        private void UpdateProgressInBorder(Border border, Models.FileItemProgress progressInfo)
        {
            try
            {
                if (border.Child is Grid grid)
                {
                    // 查找进度面板并更新
                    foreach (var child in grid.Children)
                    {
                        if (child is StackPanel panel && Grid.GetColumn(child) == 1)
                        {
                            // 查找进度面板
                            foreach (var subChild in panel.Children)
                            {
                                if (subChild is StackPanel progressPanel &&
                                    progressPanel.Tag?.ToString() == $"progress_{progressInfo.FilePath}")
                                {
                                    UpdateProgressPanel(progressPanel, progressInfo);
                                    break;
                                }
                            }
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新Border中的进度信息失败: {ex.Message}");
            }
        }

        // 更新进度面板
        private void UpdateProgressPanel(StackPanel progressPanel, Models.FileItemProgress progressInfo)
        {
            try
            {
                foreach (var child in progressPanel.Children)
                {
                    if (child.Tag?.ToString() == "statusText" && child is TextBlock statusText)
                    {
                        statusText.Text = progressInfo.StatusDisplayText;
                    }
                    else if (child.Tag?.ToString() == "progressBar" && child is Avalonia.Controls.ProgressBar progressBar)
                    {
                        progressBar.Value = progressInfo.Progress;
                        progressBar.IsVisible = progressInfo.IsProcessing;
                    }
                    else if (child.Tag?.ToString() == "detailText" && child is TextBlock detailText)
                    {
                        var details = new List<string>();

                        if (progressInfo.Status == Models.FileItemStatus.Uploading)
                        {
                            if (!string.IsNullOrEmpty(progressInfo.UploadProgressText))
                                details.Add(progressInfo.UploadProgressText);
                            if (!string.IsNullOrEmpty(progressInfo.UploadSpeedText))
                                details.Add(progressInfo.UploadSpeedText);
                        }
                        else if (progressInfo.Status == Models.FileItemStatus.Converting)
                        {
                            details.Add($"转换进度: {progressInfo.ProgressText}");
                            if (!string.IsNullOrEmpty(progressInfo.ConversionSpeedText))
                                details.Add($"速度: {progressInfo.ConversionSpeedText}");
                        }

                        if (!string.IsNullOrEmpty(progressInfo.EstimatedTimeRemainingText))
                            details.Add($"剩余: {progressInfo.EstimatedTimeRemainingText}");

                        detailText.Text = string.Join(" | ", details);
                        detailText.IsVisible = details.Count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新进度面板失败: {ex.Message}");
            }
        }

        // 创建转换按钮面板
        private StackPanel CreateConvertPanel(string filePath)
        {
            var panel = new StackPanel
            {
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Margin = new Avalonia.Thickness(15, 0, 0, 0)
            };

            var convertBtn = new Button
            {
                Content = "转换",
                Background = Avalonia.Media.Brush.Parse("#9b59b6"),
                Foreground = Avalonia.Media.Brushes.White,
                Padding = new Avalonia.Thickness(20, 8),
                CornerRadius = new Avalonia.CornerRadius(20),
                FontSize = 12,
                FontWeight = Avalonia.Media.FontWeight.Medium
            };
            convertBtn.Click += async (s, e) => await StartConversionAsync();

            panel.Children.Add(convertBtn);

            return panel;
        }

        // 根据转换设置更新目标信息
        public void UpdateTargetInfoFromSettings()
        {
            try
            {
                var fileListContainer = this.FindControl<StackPanel>("FileListContainer");
                if (fileListContainer == null) return;

                // 获取当前的转换设置
                var currentSettings = GetCurrentConversionSettings();

                // 更新每个文件项的目标信息
                foreach (var child in fileListContainer.Children)
                {
                    if (child is Border border)
                    {
                        UpdateTargetInfoInBorder(border, currentSettings);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新目标信息失败: {ex.Message}");
            }
        }

        // 获取当前转换设置
        private TargetConversionSettings GetCurrentConversionSettings()
        {
            // 这里应该从UI控件或设置服务中获取当前的转换设置
            // 暂时返回默认设置
            return new TargetConversionSettings
            {
                OutputFormat = "MP4",
                Resolution = "1920×1080",
                VideoCodec = "H.264",
                AudioCodec = "AAC",
                Quality = "高质量"
            };
        }

        // 在Border中更新目标信息
        private void UpdateTargetInfoInBorder(Border border, TargetConversionSettings settings)
        {
            try
            {
                if (border.Child is Grid grid)
                {
                    // 查找信息面板
                    foreach (var child in grid.Children)
                    {
                        if (child is StackPanel panel && Grid.GetColumn(child) == 1)
                        {
                            UpdateTargetInfoInPanel(panel, settings);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新Border中的目标信息失败: {ex.Message}");
            }
        }

        // 转换设置类
        private class TargetConversionSettings
        {
            public string OutputFormat { get; set; } = "";
            public string Resolution { get; set; } = "";
            public string VideoCodec { get; set; } = "";
            public string AudioCodec { get; set; } = "";
            public string Quality { get; set; } = "";
        }

        // 在面板中更新目标信息
        private void UpdateTargetInfoInPanel(StackPanel panel, TargetConversionSettings settings)
        {
            try
            {
                foreach (var child in panel.Children)
                {
                    if (child is StackPanel subPanel)
                    {
                        // 查找目标信息面板
                        foreach (var subChild in subPanel.Children)
                        {
                            if (subChild is StackPanel targetPanel && targetPanel.Tag?.ToString() == "targetInfo")
                            {
                                // 更新目标格式和分辨率
                                var children = targetPanel.Children.ToList();
                                if (children.Count >= 2)
                                {
                                    // 更新格式
                                    if (children[0] is StackPanel formatPanel)
                                    {
                                        UpdateInfoItemContent(formatPanel, settings.OutputFormat);
                                    }

                                    // 更新分辨率
                                    if (children[1] is StackPanel resolutionPanel)
                                    {
                                        UpdateInfoItemContent(resolutionPanel, settings.Resolution);
                                    }
                                }
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新面板中的目标信息失败: {ex.Message}");
            }
        }

        // 更新信息项内容
        private void UpdateInfoItemContent(StackPanel infoPanel, string newContent)
        {
            try
            {
                foreach (var child in infoPanel.Children)
                {
                    if (child is TextBlock textBlock && textBlock.Tag?.ToString() == "content")
                    {
                        textBlock.Text = newContent;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新信息项内容失败: {ex.Message}");
            }
        }

        // 移除文件
        private void RemoveFile(string filePath)
        {
            _selectedFiles.Remove(filePath);

            var fileListContainer = this.FindControl<StackPanel>("FileListContainer");
            if (fileListContainer != null)
            {
                // 查找要删除的Border元素
                Border? itemToRemove = null;
                foreach (var child in fileListContainer.Children)
                {
                    if (child is Border border && border.Tag?.ToString() == filePath)
                    {
                        itemToRemove = border;
                        break;
                    }
                }

                if (itemToRemove != null)
                {
                    fileListContainer.Children.Remove(itemToRemove);
                }
            }

            if (_selectedFiles.Count == 0)
            {
                _hasFiles = false;
                UpdateViewState();
            }
        }



        // 设置按钮点击事件
        private void SettingsBtn_Click(object? sender, RoutedEventArgs e)
        {
            ShowSettingsDialog();
        }

        private async void ShowSettingsDialog()
        {
            var settingsWindow = new ConversionSettingsWindow(new ConversionSettings());

            // 获取主窗口作为父窗口
            var mainWindow = TopLevel.GetTopLevel(this) as Window;

            if (mainWindow != null)
            {
                var result = await settingsWindow.ShowDialog<ConversionSettings?>(mainWindow);
                if (result != null)
                {
                    // 处理设置结果
                }
            }
        }

        // 转换按钮点击事件
        private async void ConvertBtn_Click(object? sender, RoutedEventArgs e)
        {
            await StartConversionAsync();
        }

        // 转换全部按钮点击事件
        private async void ConvertAllBtn_Click(object? sender, RoutedEventArgs e)
        {
            await StartConversionAsync();
        }

        private async Task StartConversionAsync()
        {
            if (_selectedFiles.Count == 0)
            {
                ShowNotification("请先选择要转换的文件", "warning");
                return;
            }

            _isConverting = true;
            UpdateViewState();

            try
            {
                var apiService = new Services.ApiService();
                var totalFiles = _selectedFiles.Count;
                var completedFiles = 0;

                foreach (var filePath in _selectedFiles.ToList())
                {
                    try
                    {
                        // 创建转换请求
                        var request = CreateConversionRequest(filePath);

                        // 创建进度报告器
                        var progress = new Progress<Services.UploadProgress>(p =>
                        {
                            // 更新UI进度
                            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                UpdateUploadProgress(filePath, p);
                            });
                        });

                        // 开始转换
                        ShowNotification($"开始转换: {Path.GetFileName(filePath)}", "info");

                        var result = await apiService.StartConversionAsync(filePath, request, progress);

                        if (result.Success)
                        {
                            completedFiles++;
                            ShowNotification($"转换启动成功: {Path.GetFileName(filePath)}", "success");

                            // 从列表中移除已开始转换的文件
                            _selectedFiles.Remove(filePath);
                            RemoveFileFromUI(filePath);
                        }
                        else
                        {
                            ShowNotification($"转换启动失败: {result.Message}", "error");
                        }
                    }
                    catch (Exception ex)
                    {
                        ShowNotification($"转换文件 {Path.GetFileName(filePath)} 时出错: {ex.Message}", "error");
                    }
                }

                ShowNotification($"批量转换完成，成功启动 {completedFiles}/{totalFiles} 个文件的转换",
                    completedFiles == totalFiles ? "success" : "warning");
            }
            catch (Exception ex)
            {
                ShowNotification($"批量转换失败: {ex.Message}", "error");
            }
            finally
            {
                _isConverting = false;
                UpdateViewState();
            }
        }

        // 创建转换请求
        private StartConversionRequest CreateConversionRequest(string filePath)
        {
            var outputFormatCombo = this.FindControl<ComboBox>("OutputFormatCombo");
            var selectedFormat = outputFormatCombo?.SelectedItem?.ToString() ?? "MP4";

            return new StartConversionRequest
            {
                TaskName = Path.GetFileNameWithoutExtension(filePath),
                Preset = "Fast 1080p30", // 可以从UI获取
                OutputFormat = selectedFormat,
                Resolution = "1920x1080", // 可以从UI获取
                VideoCodec = "H.264", // 可以从UI获取
                AudioCodec = "AAC", // 可以从UI获取
                VideoQuality = "23", // 可以从UI获取
                AudioQuality = "128", // 可以从UI获取
                FrameRate = "30" // 可以从UI获取
            };
        }

        // 更新上传进度
        private void UpdateUploadProgress(string filePath, Services.UploadProgress progress)
        {
            // 在UI中显示上传进度
            // 可以在文件项中添加进度条
            System.Diagnostics.Debug.WriteLine($"上传进度 {Path.GetFileName(filePath)}: {progress.Percentage:F1}%");
        }

        // 从UI中移除文件项
        private void RemoveFileFromUI(string filePath)
        {
            var container = this.FindControl<StackPanel>("FileListContainer");
            if (container == null) return;

            // 查找并移除对应的文件项
            var itemToRemove = container.Children
                .OfType<Border>()
                .FirstOrDefault(border => border.Tag?.ToString() == filePath);

            if (itemToRemove != null)
            {
                container.Children.Remove(itemToRemove);
            }

            // 更新视图状态
            if (_selectedFiles.Count == 0)
            {
                _hasFiles = false;
                UpdateViewState();
            }
        }


    }
}
