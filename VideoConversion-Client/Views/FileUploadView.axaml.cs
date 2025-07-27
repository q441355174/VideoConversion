using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
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
using VideoConversion_Client.Services;

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

        // 磁盘空间API服务
        private DiskSpaceApiService? _diskSpaceApiService;

        public FileUploadView()
        {
            Utils.Logger.Info("FileUploadView", "初始化开始");

            InitializeComponent();

            // 设置DataContext为自身，这样XAML中的绑定才能工作
            this.DataContext = this;

            UpdateViewState();
            SetupDragAndDrop();

            // 设置ItemsControl的数据源
            var fileListContainer = this.FindControl<ItemsControl>("FileListContainer");
            if (fileListContainer != null)
            {
                fileListContainer.ItemsSource = FileItems;
                Utils.Logger.Info("FileUploadView", "ItemsControl数据源已设置");
            }
            else
            {
                Utils.Logger.Error("FileUploadView", "未找到FileListContainer控件");
            }

            // 检查初始状态
            CheckItemsControlStatus("初始化完成");

            // 初始化磁盘空间API服务
            try
            {
                var systemSettings = Services.SystemSettingsService.Instance;
                var serverAddress = systemSettings.GetServerAddress();
                _diskSpaceApiService = new DiskSpaceApiService(serverAddress);
                Utils.Logger.Info("FileUploadView", $"磁盘空间API服务初始化完成，服务器地址: {serverAddress}");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileUploadView", $"磁盘空间API服务初始化失败: {ex.Message}");
            }

            Utils.Logger.Info("FileUploadView", "初始化完成");
        }

        /// <summary>
        /// 检查ItemsControl的状态和数据绑定
        /// </summary>
        private void CheckItemsControlStatus(string context = "")
        {
            try
            {
                var fileListContainer = this.FindControl<ItemsControl>("FileListContainer");
                if (fileListContainer != null)
                {
                    Utils.Logger.Info("FileUploadView", $"[{context}] ItemsControl状态检查:");
                    Utils.Logger.Info("FileUploadView", $"  - ItemsSource为null: {fileListContainer.ItemsSource == null}");
                    Utils.Logger.Info("FileUploadView", $"  - Items数量: {fileListContainer.Items?.Count ?? 0}");
                    Utils.Logger.Info("FileUploadView", $"  - FileItems数量: {FileItems.Count}");
                    Utils.Logger.Info("FileUploadView", $"  - IsVisible: {fileListContainer.IsVisible}");

                    if (fileListContainer.ItemsSource != null)
                    {
                        Utils.Logger.Info("FileUploadView", $"  - ItemsSource类型: {fileListContainer.ItemsSource.GetType().Name}");
                    }
                }
                else
                {
                    Utils.Logger.Error("FileUploadView", $"[{context}] 未找到ItemsControl");
                }

                // 检查视图状态
                var emptyStateView = this.FindControl<Border>("EmptyStateView");
                var fileListView = this.FindControl<Grid>("FileListView");
                Utils.Logger.Info("FileUploadView", $"[{context}] 视图状态:");
                Utils.Logger.Info("FileUploadView", $"  - EmptyStateView.IsVisible: {emptyStateView?.IsVisible}");
                Utils.Logger.Info("FileUploadView", $"  - FileListView.IsVisible: {fileListView?.IsVisible}");
                Utils.Logger.Info("FileUploadView", $"  - _hasFiles: {_hasFiles}");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileUploadView", $"检查ItemsControl状态失败 [{context}]", ex);
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

            Utils.Logger.Info("FileUploadView", $"UpdateViewState调用 - _hasFiles: {_hasFiles}");

            if (emptyStateView != null && fileListView != null)
            {
                if (_hasFiles)
                {
                    emptyStateView.IsVisible = false;
                    fileListView.IsVisible = true;
                    Utils.Logger.Info("FileUploadView", "设置为文件列表视图 - EmptyStateView: false, FileListView: true");
                }
                else
                {
                    emptyStateView.IsVisible = true;
                    fileListView.IsVisible = false;
                    Utils.Logger.Info("FileUploadView", "设置为空状态视图 - EmptyStateView: true, FileListView: false");
                }

                // 验证设置结果
                Utils.Logger.Info("FileUploadView", $"设置后实际状态 - EmptyStateView.IsVisible: {emptyStateView.IsVisible}, FileListView.IsVisible: {fileListView.IsVisible}");
            }
            else
            {
                Utils.Logger.Error("FileUploadView", $"视图控件未找到 - EmptyStateView: {emptyStateView != null}, FileListView: {fileListView != null}");
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

        // 转码设置按钮点击事件
        private async void ConversionSettingsBtn_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                // 创建设置窗口（它会自动使用全局设置服务）
                var settingsWindow = new ConversionSettingsWindow();
                var parentWindow = TopLevel.GetTopLevel(this) as Window;

                // 显示设置窗口
                await settingsWindow.ShowDialog(parentWindow);

                // 检查设置是否有变化
                if (settingsWindow.SettingsChanged)
                {
                    // 设置已经在窗口中直接更新到全局服务了
                    // 这里只需要记录日志，ConversionSettingsService的事件会自动触发UI更新
                    Utils.Logger.Info("Settings", "转码设置已更新，UI将自动刷新");
                }
                else
                {
                    Utils.Logger.Info("Settings", "转码设置未更改");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Info("Settings", $"打开转码设置失败: {ex.Message}");
            }
        }

        // 打开文件对话框
        private async Task OpenFileDialog()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            Utils.Logger.Info("FileUploadView", "打开文件选择对话框");

            // 从FilePreprocessor获取支持的文件扩展名
            var supportedExtensions = Utils.FilePreprocessor.GetSupportedExtensions();
            var patterns = supportedExtensions.Select(ext => $"*{ext}").ToArray();

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "选择视频文件",
                AllowMultiple = true,
                FileTypeFilter = new[]
                {
                    new Avalonia.Platform.Storage.FilePickerFileType("视频文件")
                    {
                        Patterns = patterns
                    }
                }
            });

            Utils.Logger.Info("FileUploadView", $"用户选择了 {files.Count} 个文件");

            if (files.Count > 0)
            {
                try
                {
                    // 禁用UI，防止重复操作
                    this.IsEnabled = false;

                    // 显示处理进度
                    UpdateStatus("📁 正在处理选择的文件，请稍候...");

                    // 转换为文件路径列表
                    var filePaths = files.Select(f => f.Path.LocalPath).ToArray();
                    foreach (var path in filePaths)
                    {
                        Utils.Logger.Info("FileUploadView", $"选择的文件: {path}");
                    }

                    // 创建并显示进度窗口
                    var progressWindow = new PreprocessProgressWindow();
                    progressWindow.InitializeProgress(filePaths);

                    // 显示进度窗口
                    var mainWindow = TopLevel.GetTopLevel(this) as Window;
                    if (mainWindow != null)
                    {
                        progressWindow.Show(mainWindow);
                    }

                    // 使用FilePreprocessor批量处理文件（同步等待完成）
                    var result = await Utils.FilePreprocessor.PreprocessFilesAsync(
                        filePaths,
                        includeSubdirectories: false,
                        progressCallback: progressWindow.UpdateFileStatus,
                        fileCompletedCallback: progressWindow.MarkFileCompleted,
                        cancellationToken: progressWindow.CancellationToken);

                    if (result.Success)
                    {
                        Utils.Logger.Info("FileUploadView", $"FilePreprocessor处理成功，共处理了 {result.ProcessedFiles.Count} 个文件");

                        // 添加处理成功的文件 - 确保在UI线程上执行
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            int addedCount = 0;
                            foreach (var processedFile in result.ProcessedFiles)
                            {
                                Utils.Logger.Info("FileUploadView", $"检查文件: {processedFile.FileName}, ViewModel为null: {processedFile.ViewModel == null}, 已存在: {_selectedFiles.Contains(processedFile.FilePath)}");

                                if (processedFile.ViewModel != null && !_selectedFiles.Contains(processedFile.FilePath))
                                {
                                    _selectedFiles.Add(processedFile.FilePath);
                                    FileItems.Add(processedFile.ViewModel);
                                    addedCount++;
                                    Utils.Logger.Info("FileUploadView", $"成功添加文件到UI: {processedFile.FileName}");
                                    // 缩略图已经在FilePreprocessor中处理了，无需重复获取
                                }
                            }
                            Utils.Logger.Info("FileUploadView", $"实际添加到UI的文件数量: {addedCount}, 当前FileItems总数: {FileItems.Count}");

                            // 🔥 关键：确保设置_hasFiles并更新视图状态
                            if (FileItems.Count > 0 && !_hasFiles)
                            {
                                _hasFiles = true;
                                UpdateViewState();
                                Utils.Logger.Info("FileUploadView", "文件对话框：已设置_hasFiles=true并更新视图状态");
                            }

                            // 检查ItemsControl状态
                            CheckItemsControlStatus("文件对话框处理完成");
                        });

                        // 显示处理结果
                        var stats = result.Statistics;
                        UpdateStatus($"✅ 已添加 {stats.ProcessedFiles} 个文件 ({stats.FormattedTotalSize})");

                        // 如果用户没有取消，关闭进度窗口
                        if (!progressWindow.IsCancelled)
                        {
                            // 等待一小段时间让用户看到完成状态
                            await Task.Delay(1000);
                            progressWindow.Close();
                        }

                        if (result.SkippedFiles.Any())
                        {
                            await Services.MessageBoxService.ShowInfoAsync($"跳过了 {result.SkippedFiles.Count} 个不支持的文件");
                        }

                        if (!_hasFiles && result.ProcessedFiles.Any())
                        {
                            _hasFiles = true;
                            UpdateViewState();
                        }
                    }
                    else
                    {
                        await Services.MessageBoxService.ShowErrorAsync($"处理文件失败: {result.ErrorMessage}");
                        UpdateStatus("❌ 文件处理失败");
                    }

                    UpdateFileCountDisplay();
                }
                finally
                {
                    // 重新启用UI
                    this.IsEnabled = true;
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

                // 使用FilePreprocessor处理文件夹
                var result = await Utils.FilePreprocessor.PreprocessFilesAsync(
                    new[] { folder.Path.LocalPath },
                    includeSubdirectories: true);

                if (result.Success)
                {
                    // 添加处理成功的文件 - 确保在UI线程上执行
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        foreach (var processedFile in result.ProcessedFiles)
                        {
                            if (processedFile.ViewModel != null)
                            {
                                FileItems.Add(processedFile.ViewModel);
                            }
                        }
                    });

                    // 显示处理结果
                    var stats = result.Statistics;
                    UpdateStatus($"✅ 从文件夹添加了 {stats.ProcessedFiles} 个文件 ({stats.FormattedTotalSize})");

                    if (result.SkippedFiles.Any())
                    {
                        await Services.MessageBoxService.ShowInfoAsync($"跳过了 {result.SkippedFiles.Count} 个不支持的文件");
                    }
                }
                else
                {
                    await Services.MessageBoxService.ShowErrorAsync($"处理文件夹失败: {result.ErrorMessage}");
                }

                UpdateFileCountDisplay();
            }
        }

        // 处理拖拽的文件和文件夹
        private async Task ProcessDroppedFiles(IEnumerable<IStorageItem> items)
        {
            try
            {
                // 禁用UI，防止重复操作
                this.IsEnabled = false;

                // 显示处理进度
                UpdateStatus("📁 正在处理文件，请稍候...");
                Utils.Logger.Info("FileUploadView", "开始处理拖拽的文件");

                // 转换为文件路径列表
                var filePaths = new List<string>();
                foreach (var item in items)
                {
                    if (item is IStorageFile file)
                    {
                        filePaths.Add(file.Path.LocalPath);
                        Utils.Logger.Info("FileUploadView", $"拖拽文件: {file.Path.LocalPath}");
                    }
                    else if (item is IStorageFolder folder)
                    {
                        filePaths.Add(folder.Path.LocalPath);
                        Utils.Logger.Info("FileUploadView", $"拖拽文件夹: {folder.Path.LocalPath}");
                    }
                }

                Utils.Logger.Info("FileUploadView", $"开始处理 {filePaths.Count} 个路径");

                // 创建并显示进度窗口
                var progressWindow = new PreprocessProgressWindow();
                progressWindow.InitializeProgress(filePaths);

                // 显示进度窗口
                var mainWindow = TopLevel.GetTopLevel(this) as Window;
                if (mainWindow != null)
                {
                    progressWindow.Show(mainWindow);
                }

                // 使用FilePreprocessor预处理文件（同步等待完成）
                var result = await Utils.FilePreprocessor.PreprocessFilesAsync(
                    filePaths,
                    includeSubdirectories: true,
                    progressCallback: progressWindow.UpdateFileStatus,
                    fileCompletedCallback: progressWindow.MarkFileCompleted,
                    cancellationToken: progressWindow.CancellationToken);

                Utils.Logger.Info("FileUploadView", $"FilePreprocessor处理完成，返回 {result.ProcessedFiles.Count} 个文件");

                // 如果用户没有取消，关闭进度窗口
                if (!progressWindow.IsCancelled)
                {
                    // 等待一小段时间让用户看到完成状态
                    await Task.Delay(1000);
                    progressWindow.Close();
                }

                if (!result.Success)
                {
                    await Services.MessageBoxService.ShowErrorAsync($"文件预处理失败: {result.ErrorMessage}");
                    UpdateStatus("❌ 文件处理失败");
                    return;
                }

                // 添加处理成功的文件 - 确保在UI线程上执行
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Utils.Logger.Info("FileUploadView", "开始在UI线程上添加文件到FileItems");
                    var addedCount = 0;

                    foreach (var processedFile in result.ProcessedFiles)
                    {
                        if (processedFile.ViewModel != null)
                        {
                            FileItems.Add(processedFile.ViewModel);
                            addedCount++;
                            Utils.Logger.Info("FileUploadView", $"成功添加文件: {processedFile.ViewModel.FileName}");
                            // 缩略图已经在FilePreprocessor中处理了，无需重复获取
                        }
                        else
                        {
                            Utils.Logger.Warning("FileUploadView", $"文件ViewModel为null: {processedFile.FileName}");
                        }
                    }

                    Utils.Logger.Info("FileUploadView", $"实际添加到UI的文件数量: {addedCount}, 当前FileItems总数: {FileItems.Count}");

                    // 🔥 关键：确保设置_hasFiles并更新视图状态
                    if (FileItems.Count > 0 && !_hasFiles)
                    {
                        _hasFiles = true;
                        UpdateViewState();
                        Utils.Logger.Info("FileUploadView", "拖拽文件：已设置_hasFiles=true并更新视图状态");
                    }

                    // 检查ItemsControl状态
                    CheckItemsControlStatus("拖拽文件处理完成");
                });

                // 显示处理结果
                var stats = result.Statistics;
                var statusMessage = $"✅ 已添加 {stats.ProcessedFiles} 个文件 ({stats.FormattedTotalSize})";
                if (stats.LargeFiles > 0)
                {
                    statusMessage += $" (包含 {stats.LargeFiles} 个大文件)";
                }
                UpdateStatus(statusMessage);

                // 显示跳过的文件信息
                if (result.SkippedFiles.Any())
                {
                    var message = $"跳过了 {result.SkippedFiles.Count} 个文件:\n{string.Join("\n", result.SkippedFiles.Take(5))}";
                    if (result.SkippedFiles.Count > 5)
                    {
                        message += $"\n... 还有 {result.SkippedFiles.Count - 5} 个文件";
                    }

                    await Services.MessageBoxService.ShowWarningAsync(message);
                }

                UpdateFileCountDisplay();
            }
            catch (Exception ex)
            {
                Utils.Logger.Info("DragDrop", $"处理拖拽文件失败: {ex.Message}");
                await Services.MessageBoxService.ShowErrorAsync($"处理文件失败: {ex.Message}");
                UpdateStatus("❌ 文件处理失败");
            }
            finally
            {
                // 重新启用UI
                this.IsEnabled = true;
                Utils.Logger.Info("UI", "UI已重新启用");
            }
        }

        // 添加文件到列表
        private async void AddFile(string filePath)
        {
            if (_selectedFiles.Contains(filePath))
                return;

            try
            {
                // 使用FilePreprocessor预处理单个文件
                var result = await Utils.FilePreprocessor.PreprocessFilesAsync(
                    new[] { filePath },
                    includeSubdirectories: false);

                if (result.Success && result.ProcessedFiles.Any())
                {
                    var processedFile = result.ProcessedFiles.First();
                    if (processedFile.ViewModel != null)
                    {
                        _selectedFiles.Add(filePath);

                        // 确保在UI线程上添加到FileItems
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            FileItems.Add(processedFile.ViewModel);
                        });
                        // 缩略图已经在FilePreprocessor中处理了，无需重复获取

                        if (!_hasFiles)
                        {
                            _hasFiles = true;
                            UpdateViewState();
                        }

                        UpdateFileCountDisplay();
                    }
                }
                else if (result.SkippedFiles.Any())
                {
                    Utils.Logger.Info("FileList", $"文件被跳过: {string.Join(", ", result.SkippedFiles)}");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Info("FileList", $"添加文件失败: {ex.Message}");
            }
        }

        // 更新状态显示
        private void UpdateStatus(string status)
        {
            try
            {
                // 这里可以更新状态栏或其他UI元素
                Utils.Logger.Info("Status", $"状态更新: {status}");

                // 如果有状态栏控件，可以在这里更新
                // var statusBar = this.FindControl<TextBlock>("StatusBar");
                // if (statusBar != null)
                // {
                //     statusBar.Text = status;
                // }
            }
            catch (Exception ex)
            {
                Utils.Logger.Info("Status", $"更新状态失败: {ex.Message}");
            }
        }

        // 更新文件数量显示
        private void UpdateFileCountDisplay()
        {
            try
            {
                var fileCount = FileItems.Count;
                var totalSize = FileItems.Sum(f =>
                {
                    try
                    {
                        var fileInfo = new FileInfo(f.FilePath);
                        return fileInfo.Length;
                    }
                    catch
                    {
                        return 0L;
                    }
                });

                var formattedSize = Utils.FileSizeFormatter.FormatBytesAuto(totalSize);
                var displayText = $"已选择 {fileCount} 个文件 ({formattedSize})";

                Utils.Logger.Info("FileCount", $"文件数量更新: {displayText}");

                // 如果有文件计数显示控件，可以在这里更新
                // var fileCountLabel = this.FindControl<TextBlock>("FileCountLabel");
                // if (fileCountLabel != null)
                // {
                //     fileCountLabel.Text = displayText;
                // }
            }
            catch (Exception ex)
            {
                Utils.Logger.Info("FileCount", $"更新文件数量显示失败: {ex.Message}");
            }
        }

        // 获取支持格式的显示文本
        private string GetSupportedFormatsDisplayText()
        {
            try
            {
                var extensions = Utils.FilePreprocessor.GetSupportedExtensions();
                var formats = extensions.Select(ext => ext.TrimStart('.').ToUpper()).ToArray();
                return string.Join(", ", formats);
            }
            catch
            {
                return "MP4, AVI, MOV, MKV, WMV, FLV, WebM";
            }
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
                // 调用FileItemViewModel的转换方法
                var success = await fileItem.StartConversionAsync();

                if (success)
                {
                    ShowNotification($"转换启动成功: {fileItem.FileName}", "success");
                }
                else
                {
                    ShowNotification($"转换启动失败: {fileItem.FileName}", "error");
                }
            }
            catch (Exception ex)
            {
                // 转换失败
                fileItem.Status = FileItemStatus.Failed;
                fileItem.StatusText = $"转换失败: {ex.Message}";
                fileItem.Progress = 0;

                // 显示错误通知
                ShowNotification($"转换失败: {fileItem.FileName}", "error");
                Utils.Logger.Info("Conversion", $"转换失败: {ex.Message}");
            }
        }

        // 显示通知消息
        private void ShowNotification(string message, string type)
        {
            // 这里可以实现通知显示逻辑
            // 暂时输出到调试控制台
            Utils.Logger.Info("Notification", $"[{type.ToUpper()}] {message}");
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
                Utils.Logger.Info("Progress", $"更新文件进度失败: {ex.Message}");
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
                Utils.Logger.Info("Progress", $"更新Border中的进度信息失败: {ex.Message}");
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
                Utils.Logger.Info("Progress", $"更新进度面板失败: {ex.Message}");
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

        // 更新所有文件项的预估值
        private async void UpdateAllFileItemsEstimatedValues()
        {
            try
            {
                if (!FileItems.Any()) return;

                // 创建ProcessedFileInfo列表用于批量更新
                var processedFiles = FileItems.Select(fileItem =>
                {
                    var fileInfo = new FileInfo(fileItem.FilePath);
                    return new Utils.FilePreprocessor.ProcessedFileInfo
                    {
                        FilePath = fileItem.FilePath,
                        FileName = fileItem.FileName,
                        FileSize = fileInfo.Length,
                        ViewModel = fileItem
                    };
                }).ToList();

                // 使用FilePreprocessor批量更新预估数据
                await Utils.FilePreprocessor.UpdateEstimatedDataAsync(processedFiles);

                Utils.Logger.Info("Estimation", $"已更新 {processedFiles.Count} 个文件的预估值");
            }
            catch (Exception ex)
            {
                Utils.Logger.Info("Estimation", $"批量更新文件项预估值失败: {ex.Message}");
            }
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

                // 同时更新预估值
                UpdateAllFileItemsEstimatedValues();
            }
            catch (Exception ex)
            {
                Utils.Logger.Info("TargetInfo", $"更新目标信息失败: {ex.Message}");
            }
        }

        // 获取当前转换设置
        private TargetConversionSettings GetCurrentConversionSettings()
        {
            try
            {
                var settingsService = Services.ConversionSettingsService.Instance;
                var settings = settingsService.CurrentSettings;

                return new TargetConversionSettings
                {
                    OutputFormat = "MP4", // 固定为MP4
                    Resolution = settingsService.GetFormattedResolution(),
                    VideoCodec = settings.VideoCodec,
                    AudioCodec = settings.AudioCodec,
                    Quality = GetQualityDescription(settings.VideoQuality ?? "23")
                };
            }
            catch (Exception ex)
            {
                Utils.Logger.Info("Settings", $"获取转换设置失败: {ex.Message}");
                // 返回默认设置
                return new TargetConversionSettings
                {
                    OutputFormat = "MP4",
                    Resolution = "1920×1080",
                    VideoCodec = "H.264",
                    AudioCodec = "AAC",
                    Quality = "高质量"
                };
            }
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
                Utils.Logger.Info("TargetInfo", $"更新Border中的目标信息失败: {ex.Message}");
            }
        }

        // 获取质量描述
        private string GetQualityDescription(string bitrate)
        {
            try
            {
                if (bitrate.EndsWith("k", StringComparison.OrdinalIgnoreCase))
                {
                    var value = bitrate.Substring(0, bitrate.Length - 1);
                    if (double.TryParse(value, out var kbps))
                    {
                        if (kbps >= 8000) return "超高质量";
                        if (kbps >= 5000) return "高质量";
                        if (kbps >= 3000) return "中等质量";
                        if (kbps >= 1500) return "标准质量";
                        return "低质量";
                    }
                }
                return "高质量";
            }
            catch
            {
                return "高质量";
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
                Utils.Logger.Info("TargetInfo", $"更新面板中的目标信息失败: {ex.Message}");
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
                Utils.Logger.Info("InfoItem", $"更新信息项内容失败: {ex.Message}");
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
            Utils.Logger.Info("Upload", "=== 开始批量文件转换流程 ===");

            if (FileItems.Count == 0)
            {
                Utils.Logger.Info("Upload", "❌ 没有选择文件，退出转换流程");
                ShowNotification("请先选择要转换的文件", "warning");
                return;
            }

            // 检查磁盘空间
            if (!await CheckDiskSpaceAsync())
            {
                Utils.Logger.Info("Upload", "❌ 磁盘空间不足，退出转换流程");
                return;
            }

            Utils.Logger.Info("Upload", $"文件列表中共有 {FileItems.Count} 个文件");

            _isConverting = true;
            UpdateViewState();

            try
            {
                Utils.Logger.Info("Upload", "🔧 初始化API服务");
                var apiService = new Services.ApiService();
                Utils.Logger.Info("Upload", $"API服务地址: {apiService.BaseUrl}");

                var filesToConvert = FileItems.Where(f => f.Status == FileItemStatus.Pending).ToList();
                Utils.Logger.Info("Upload", $"待转换文件数量: {filesToConvert.Count}");

                if (filesToConvert.Count == 0)
                {
                    Utils.Logger.Info("Upload", "❌ 没有待转换的文件，退出转换流程");
                    ShowNotification("没有待转换的文件", "warning");
                    return;
                }

                // 打印待转换文件列表
                foreach (var file in filesToConvert)
                {
                    Utils.Logger.Info("Upload", $"📁 待转换文件: {file.FileName} (大小: {file.FileSize})");
                    Utils.Logger.Info("Upload", $"   路径: {file.FilePath}");
                }

                var filePaths = filesToConvert.Select(f => f.FilePath).ToList();
                var request = CreateConversionRequest();

                Utils.Logger.Info("Upload", $"🎯 转换参数: 格式={request.OutputFormat}, 分辨率={request.Resolution}, 视频编码={request.VideoCodec}");

                // 创建批量进度报告器
                var progress = new Progress<Services.BatchUploadProgress>(p =>
                {
                    Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        // 详细进度日志
                        Utils.Logger.Info("Upload", $"📊 批量进度更新: {p.CompletedFiles}/{p.TotalFiles} 文件完成, 总进度: {p.OverallProgress:F1}%");
                        if (!string.IsNullOrEmpty(p.CurrentFile))
                        {
                            Utils.Logger.Info("Upload", $"   当前文件: {p.CurrentFile} - {p.CurrentFileProgress:F1}%");
                        }
                        UpdateBatchProgress(p);
                    });
                });

                Utils.Logger.Info("Upload", $"🚀 开始调用批量转换API，文件数量: {filePaths.Count}");
                ShowNotification($"开始批量转换 {filePaths.Count} 个文件", "info");

                // 使用新的批量转换API
                var result = await apiService.StartBatchConversionAsync(filePaths, request, progress);

                Utils.Logger.Info("Upload", "📥 收到批量转换API响应");

                if (result.Success && result.Data != null)
                {
                    var batchResult = result.Data;
                    Utils.Logger.Info("Upload", $"✅ 批量转换API调用成功");
                    Utils.Logger.Info("Upload", $"📊 批量结果统计: 成功 {batchResult.SuccessCount}/{batchResult.TotalFiles} 个文件");

                    ShowNotification($"批量转换完成：成功 {batchResult.SuccessCount}/{batchResult.TotalFiles} 个文件",
                        batchResult.SuccessCount == batchResult.TotalFiles ? "success" : "warning");

                    // 更新文件项状态
                    Utils.Logger.Info("Upload", "🔄 开始更新文件项状态");
                    foreach (var taskResult in batchResult.Results)
                    {
                        var fileItem = FileItems.FirstOrDefault(f => f.FilePath == taskResult.FilePath);
                        if (fileItem != null)
                        {
                            if (taskResult.Success)
                            {
                                Utils.Logger.Info("Upload", $"✅ 文件转换启动成功: {Path.GetFileName(taskResult.FilePath)} -> TaskId: {taskResult.TaskId}");
                                fileItem.TaskId = taskResult.TaskId;
                                fileItem.Status = FileItemStatus.Converting;
                                fileItem.StatusText = "转换已启动";
                                fileItem.Progress = 0; // 重置进度，准备显示转换进度
                                Utils.Logger.Info("Upload", $"🔄 重置进度条，准备显示转换进度: {fileItem.FileName}");

                                // 加入SignalR任务组以接收转换进度（在UI线程上执行）
                                if (!string.IsNullOrEmpty(taskResult.TaskId))
                                {
                                    Utils.Logger.Info("Upload", $"🔗 准备加入SignalR任务组: {taskResult.TaskId}");

                                    // 使用UI线程调度器执行SignalR操作
                                    Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                                    {
                                        try
                                        {
                                            var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                                                ? desktop.MainWindow as MainWindow
                                                : null;

                                            if (mainWindow?.DataContext is ViewModels.MainWindowViewModel viewModel)
                                            {
                                                Utils.Logger.Info("Upload", $"🔗 在UI线程上加入SignalR任务组: {taskResult.TaskId}");
                                                await viewModel.JoinTaskGroupAsync(taskResult.TaskId);
                                                Utils.Logger.Info("Upload", $"✅ 成功加入SignalR任务组: {taskResult.TaskId}");
                                            }
                                            else
                                            {
                                                Utils.Logger.Info("Upload", "⚠️ 无法获取MainWindowViewModel，跳过加入任务组");
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Utils.Logger.Info("Upload", $"❌ 加入任务组异常: {ex.Message}");
                                        }
                                    });
                                }
                            }
                            else
                            {
                                Utils.Logger.Info("Upload", $"❌ 文件转换启动失败: {Path.GetFileName(taskResult.FilePath)} -> {taskResult.Message}");
                                fileItem.Status = FileItemStatus.Failed;
                                fileItem.StatusText = $"启动失败: {taskResult.Message}";
                            }
                        }
                        else
                        {
                            Utils.Logger.Info("Upload", $"⚠️ 找不到对应的文件项: {taskResult.FilePath}");
                        }
                    }
                    Utils.Logger.Info("Upload", "✅ 文件项状态更新完成");
                }
                else
                {
                    Utils.Logger.Info("Upload", $"❌ 批量转换API调用失败");
                    Utils.Logger.Info("Upload", $"失败原因: {result.Message}");
                    Utils.Logger.Info("Upload", $"错误类型: {result.ErrorType}");
                    ShowNotification($"批量转换失败: {result.Message}", "error");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Info("Upload", $"💥 批量转换过程中发生异常");
                Utils.Logger.Info("Upload", $"异常类型: {ex.GetType().Name}");
                Utils.Logger.Info("Upload", $"异常消息: {ex.Message}");
                Utils.Logger.Info("Upload", $"异常堆栈: {ex.StackTrace}");
                ShowNotification($"批量转换失败: {ex.Message}", "error");
            }
            finally
            {
                Utils.Logger.Info("Upload", "🏁 批量转换流程结束，恢复UI状态");
                _isConverting = false;
                UpdateViewState();
                Utils.Logger.Info("Upload", "=== 批量文件转换流程完成 ===");
            }
        }

        // 更新批量转换进度
        private void UpdateBatchProgress(Services.BatchUploadProgress progress)
        {
            try
            {
                // 检查是否暂停
                if (progress.IsPaused)
                {
                    Utils.Logger.Info("UI", $"⏸️ 批量转换已暂停: {progress.PauseReason}");
                    ShowNotification($"批量转换已暂停: {progress.PauseReason}", "warning");

                    // 更新UI状态为暂停
                    _isConverting = false;
                    UpdateViewState();
                    return;
                }

                // 验证进度值，防止负值
                var safeProgress = Math.Max(0, Math.Min(100, progress.CurrentFileProgress));
                var safeOverallProgress = Math.Max(0, Math.Min(100, progress.OverallProgress));

                Utils.Logger.Info("UI", $"🎯 UI进度更新: 当前文件={progress.CurrentFile}, 文件进度={safeProgress:F1}%, 总进度={safeOverallProgress:F1}%");

                // 如果进度值异常，记录详细信息用于调试
                if (progress.CurrentFileProgress < 0 || progress.CurrentFileProgress > 100)
                {
                    Utils.Logger.Info("UI", $"⚠️ 检测到异常进度值: 原始值={progress.CurrentFileProgress:F1}%, 修正为={safeProgress:F1}%");
                }

                // 更新当前文件的进度
                var currentFileItem = FileItems.FirstOrDefault(f => Path.GetFileName(f.FilePath) == progress.CurrentFile);
                if (currentFileItem != null)
                {
                    Utils.Logger.Info("UI", $"✅ 找到文件项: {currentFileItem.FileName}, 更新进度: {currentFileItem.Progress:F1}% -> {safeProgress:F1}%");

                    // 更新进度值（确保不为负数）
                    currentFileItem.Progress = safeProgress;

                    // 根据进度阶段更新状态文本
                    if (safeProgress < 100)
                    {
                        // 上传阶段
                        currentFileItem.Status = Models.FileItemStatus.Uploading;
                        currentFileItem.StatusText = $"上传中... {safeProgress:F1}%";
                    }
                    else
                    {
                        // 上传完成，等待转换
                        currentFileItem.Status = Models.FileItemStatus.UploadCompleted;
                        currentFileItem.StatusText = "上传完成，等待转换...";
                        currentFileItem.Progress = 0; // 重置进度，准备显示转换进度
                    }

                    Utils.Logger.Info("UI", $"✅ 文件项状态已更新: {currentFileItem.FileName} = {currentFileItem.StatusText}, 进度={currentFileItem.Progress:F1}%");
                }
                else
                {
                    Utils.Logger.Info("UI", $"❌ 未找到文件项: {progress.CurrentFile}");
                    Utils.Logger.Info("UI", $"   当前FileItems数量: {FileItems.Count}");
                    foreach (var item in FileItems.Take(3))
                    {
                        Utils.Logger.Info("UI", $"   文件项: {item.FileName}");
                    }
                }

                // 更新整体进度显示
                var statusText = $"批量转换进度: {progress.CompletedFiles}/{progress.TotalFiles} 文件, 总进度: {progress.OverallProgress:F1}%";
                if (!string.IsNullOrEmpty(progress.CurrentFile))
                {
                    statusText += $" (当前: {progress.CurrentFile})";
                }

                Utils.Logger.Info("UI", $"📊 整体进度状态: {statusText}");
            }
            catch (Exception ex)
            {
                Utils.Logger.Info("UI", $"❌ 更新批量进度失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理转换进度更新（由SignalR调用）
        /// </summary>
        public void UpdateConversionProgress(string taskId, double progress, double? speed = null, double? eta = null)
        {
            try
            {
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    // 验证进度值，防止负值
                    var safeProgress = Math.Max(0, Math.Min(100, progress));

                    Utils.Logger.Info("UI", $"🔄 转换进度更新: TaskId={taskId}, 进度={safeProgress:F1}%, 速度={speed:F2}x");

                    // 查找对应的文件项
                    var fileItem = FileItems.FirstOrDefault(f => f.TaskId == taskId);
                    if (fileItem != null)
                    {
                        Utils.Logger.Info("UI", $"✅ 找到转换文件项: {fileItem.FileName}, 更新转换进度: {fileItem.Progress:F1}% -> {safeProgress:F1}%");

                        // 更新转换状态和进度
                        fileItem.Status = Models.FileItemStatus.Converting;
                        fileItem.Progress = safeProgress;
                        fileItem.StatusText = $"转换中... {safeProgress:F1}%";

                        // 更新转换速度和预计时间（FileItemViewModel没有这些属性，暂时跳过）
                        // TODO: 如果需要显示转换速度和预计时间，需要在FileItemViewModel中添加这些属性

                        // 如果转换完成
                        if (safeProgress >= 100)
                        {
                            fileItem.Status = Models.FileItemStatus.Completed;
                            fileItem.StatusText = "转换完成";
                            fileItem.Progress = 100;
                            Utils.Logger.Info("UI", $"🎉 文件转换完成: {fileItem.FileName}");
                        }

                        Utils.Logger.Info("UI", $"✅ 转换进度已更新: {fileItem.FileName} = {fileItem.StatusText}, 进度={fileItem.Progress:F1}%");
                    }
                    else
                    {
                        Utils.Logger.Info("UI", $"❌ 未找到转换文件项: TaskId={taskId}");
                        Utils.Logger.Info("UI", $"   当前FileItems数量: {FileItems.Count}");
                        foreach (var item in FileItems.Take(3))
                        {
                            Utils.Logger.Info("UI", $"   文件项: {item.FileName}, TaskId={item.TaskId}");
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Utils.Logger.Info("UI", $"❌ 更新转换进度失败: {ex.Message}");
            }
        }

        // 创建转换请求（使用全局设置服务的设置）
        private StartConversionRequest CreateConversionRequest()
        {
            // 获取全局转码设置
            var globalSettings = Services.ConversionSettingsService.Instance.CurrentSettings;

            // 获取UI中的输出格式设置（如果有的话）
            var outputFormatCombo = this.FindControl<ComboBox>("OutputFormatCombo");
            var selectedFormatTag = GetComboBoxSelectedTag(outputFormatCombo);
            var selectedFormat = selectedFormatTag ?? globalSettings.OutputFormat ?? "mp4";

            // 智能格式选项将在ApiService中统一处理，这里保持原始选择

            return new StartConversionRequest
            {
                // 基本信息
                TaskName = "批量转换",
                Preset = "CPU Standard 1080p", // 使用默认预设

                // 基本设置（使用全局设置）
                OutputFormat = selectedFormat,
                Resolution = globalSettings.Resolution,

                // 视频设置（使用全局设置）
                VideoCodec = globalSettings.VideoCodec,
                FrameRate = globalSettings.FrameRate,
                QualityMode = globalSettings.QualityMode,
                VideoQuality = globalSettings.VideoQuality,
                EncodingPreset = globalSettings.EncodingPreset,
                Profile = globalSettings.Profile,

                // 音频设置（使用全局设置）
                AudioCodec = globalSettings.AudioCodec,
                AudioChannels = globalSettings.AudioChannels,
                AudioQualityMode = globalSettings.AudioQualityMode,
                AudioQuality = globalSettings.AudioQuality,
                SampleRate = globalSettings.SampleRate,
                AudioVolume = globalSettings.AudioVolume,

                // 高级选项（使用全局设置）
                StartTime = globalSettings.StartTime,
                EndTime = globalSettings.EndTime,
                DurationLimit = globalSettings.DurationLimit,
                Deinterlace = globalSettings.Deinterlace,
                Denoise = globalSettings.Denoise,
                ColorSpace = globalSettings.ColorSpace,
                PixelFormat = globalSettings.PixelFormat,
                CustomParams = globalSettings.CustomParams,
                CustomParameters = globalSettings.CustomParameters,
                HardwareAcceleration = globalSettings.HardwareAcceleration,
                VideoFilters = globalSettings.VideoFilters,
                AudioFilters = globalSettings.AudioFilters,

                // 任务设置（使用全局设置）
                Priority = globalSettings.Priority,
                MaxRetries = globalSettings.MaxRetries,
                Tags = globalSettings.Tags,
                Notes = globalSettings.Notes,

                // 编码选项（使用全局设置）
                TwoPass = globalSettings.TwoPass,
                FastStart = globalSettings.FastStart,
                CopyTimestamps = globalSettings.CopyTimestamps
            };
        }

        // 更新上传进度
        private void UpdateUploadProgress(string filePath, Services.UploadProgress progress)
        {
            // 在UI中显示上传进度
            // 可以在文件项中添加进度条
            Utils.Logger.Info("Upload", $"上传进度 {Path.GetFileName(filePath)}: {progress.Percentage:F1}%");
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

        /// <summary>
        /// 获取ComboBox选中项的Tag值
        /// </summary>
        private string? GetComboBoxSelectedTag(ComboBox? comboBox)
        {
            if (comboBox?.SelectedItem is ComboBoxItem item)
            {
                return item.Tag?.ToString();
            }
            return null;
        }

        /// <summary>
        /// 解析智能格式选项为具体格式
        /// </summary>
        private string ResolveSmartFormat(string selectedFormat, ObservableCollection<FileItemViewModel> fileItems)
        {
            return selectedFormat switch
            {
                "keep_original" => ResolveKeepOriginalFormat(fileItems),
                "auto_best" => ResolveAutoBestFormat(fileItems),
                _ => selectedFormat // 已经是具体格式，直接返回
            };
        }

        /// <summary>
        /// 解析"保持原格式"选项
        /// </summary>
        private string ResolveKeepOriginalFormat(ObservableCollection<FileItemViewModel> fileItems)
        {
            if (fileItems.Count == 0)
                return "mp4";

            // 如果只有一个文件，使用该文件的格式
            if (fileItems.Count == 1)
            {
                return GetFileFormat(fileItems.First().FilePath);
            }

            // 多个文件时，统计格式使用频率，选择最常见的格式
            var formatCounts = fileItems
                .GroupBy(f => GetFileFormat(f.FilePath))
                .ToDictionary(g => g.Key, g => g.Count());

            var mostCommonFormat = formatCounts
                .OrderByDescending(kvp => kvp.Value)
                .First().Key;

            return mostCommonFormat;
        }

        /// <summary>
        /// 解析"自动选择最佳格式"选项
        /// </summary>
        private string ResolveAutoBestFormat(ObservableCollection<FileItemViewModel> fileItems)
        {
            if (fileItems.Count == 0)
                return "mp4";

            // 分析文件特征，选择最佳输出格式
            var formats = fileItems.Select(f => GetFileFormat(f.FilePath)).Distinct().ToList();
            var hasHighQualitySource = fileItems.Any(f => IsHighQualityFormat(GetFileFormat(f.FilePath)));
            var hasWebOptimizedSource = fileItems.Any(f => IsWebOptimizedFormat(GetFileFormat(f.FilePath)));
            var hasLegacySource = fileItems.Any(f => IsLegacyFormat(GetFileFormat(f.FilePath)));

            // 智能选择逻辑
            if (hasHighQualitySource)
            {
                return "mkv"; // 高质量源文件，推荐MKV保持质量
            }
            else if (hasWebOptimizedSource)
            {
                return "webm"; // Web优化源文件，保持WebM
            }
            else if (hasLegacySource)
            {
                return "mp4"; // 传统格式，转换为MP4提升兼容性
            }
            else
            {
                return "mp4"; // 默认选择MP4，最佳兼容性
            }
        }

        /// <summary>
        /// 获取文件格式
        /// </summary>
        private string GetFileFormat(string filePath)
        {
            var extension = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();

            // 标准化格式名称
            return extension switch
            {
                "mpeg" => "mpg",
                "m2ts" => "m2ts",
                "mts" => "mts",
                _ => extension
            };
        }

        /// <summary>
        /// 判断是否为高质量格式
        /// </summary>
        private bool IsHighQualityFormat(string format)
        {
            var highQualityFormats = new[] { "mkv", "mov", "m4v", "m2ts", "mts" };
            return highQualityFormats.Contains(format);
        }

        /// <summary>
        /// 判断是否为Web优化格式
        /// </summary>
        private bool IsWebOptimizedFormat(string format)
        {
            var webFormats = new[] { "webm", "mp4" };
            return webFormats.Contains(format);
        }

        /// <summary>
        /// 判断是否为传统格式
        /// </summary>
        private bool IsLegacyFormat(string format)
        {
            var legacyFormats = new[] { "avi", "wmv", "flv", "vob", "asf", "rm", "rmvb", "mpg" };
            return legacyFormats.Contains(format);
        }

        /// <summary>
        /// 检查磁盘空间是否足够
        /// </summary>
        private async Task<bool> CheckDiskSpaceAsync()
        {
            try
            {
                if (_diskSpaceApiService == null)
                {
                    Utils.Logger.Info("Upload", "⚠️ 磁盘空间API服务未初始化，跳过空间检查");
                    return true; // 如果服务未初始化，允许继续
                }

                // 计算所有文件的总大小
                long totalFileSize = 0;
                foreach (var fileItem in FileItems)
                {
                    if (File.Exists(fileItem.FilePath))
                    {
                        var fileInfo = new FileInfo(fileItem.FilePath);
                        totalFileSize += fileInfo.Length;
                    }
                }

                if (totalFileSize == 0)
                {
                    Utils.Logger.Info("Upload", "⚠️ 文件总大小为0，跳过空间检查");
                    return true;
                }

                Utils.Logger.Info("Upload", $"📊 检查磁盘空间: 文件总大小={totalFileSize / 1024.0 / 1024:F2}MB");

                // 调用空间检查API
                var spaceCheckResult = await _diskSpaceApiService.CheckSpaceAsync(totalFileSize);

                if (spaceCheckResult?.Success == true)
                {
                    if (spaceCheckResult.HasEnoughSpace)
                    {
                        Utils.Logger.Info("Upload", $"✅ 磁盘空间充足: 需要={spaceCheckResult.RequiredSpaceGB:F2}GB, 可用={spaceCheckResult.AvailableSpaceGB:F2}GB");
                        return true;
                    }
                    else
                    {
                        var message = $"磁盘空间不足！\n需要: {spaceCheckResult.RequiredSpaceGB:F2} GB\n可用: {spaceCheckResult.AvailableSpaceGB:F2} GB\n\n请清理磁盘空间后重试。";

                        Utils.Logger.Info("Upload", $"❌ 磁盘空间不足: {spaceCheckResult.Message}");
                        ShowNotification("磁盘空间不足", "error");

                        // 显示详细的空间不足对话框
                        await ShowDiskSpaceInsufficientDialog(message);
                        return false;
                    }
                }
                else
                {
                    Utils.Logger.Info("Upload", $"⚠️ 空间检查失败: {spaceCheckResult?.Message}，允许继续转换");
                    return true; // 检查失败时允许继续，避免阻塞用户
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Info("Upload", $"❌ 磁盘空间检查异常: {ex.Message}，允许继续转换");
                return true; // 异常时允许继续，避免阻塞用户
            }
        }

        /// <summary>
        /// 显示磁盘空间不足对话框
        /// </summary>
        private async Task ShowDiskSpaceInsufficientDialog(string message)
        {
            try
            {
                ShowNotification(message, "error");
                Utils.Logger.Info("Upload", $"显示磁盘空间不足提示: {message}");
            }
            catch (Exception ex)
            {
                Utils.Logger.Info("Upload", $"显示磁盘空间不足对话框失败: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _diskSpaceApiService?.Dispose();
        }
    }
}
