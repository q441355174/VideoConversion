using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VideoConversion_ClientTo.Application.Interfaces;
using VideoConversion_ClientTo.Domain.Entities;
using VideoConversion_ClientTo.Infrastructure.Services;
using VideoConversion_ClientTo.ViewModels;

namespace VideoConversion_ClientTo.Presentation.ViewModels
{
    /// <summary>
    /// 文件上传视图模型
    /// </summary>
    public partial class FileUploadViewModel : ViewModelBase
    {
        private readonly IConversionTaskService _conversionTaskService;
        private readonly IFileDialogService _fileDialogService;
        private readonly IFilePreprocessorService _filePreprocessorService;

        [ObservableProperty]
        private ObservableCollection<FileItemViewModel> _fileItems = new();

        [ObservableProperty]
        private bool _isEmptyStateVisible = true;

        [ObservableProperty]
        private bool _isFileListVisible = false;

        [ObservableProperty]
        private string _fileCountText = "0 个文件";

        [ObservableProperty]
        private string _totalSizeText = "0 MB";

        [ObservableProperty]
        private bool _isLoading = false;

        [ObservableProperty]
        private string _processingStatus = "";

        [ObservableProperty]
        private bool _isProcessing = false;

        public FileUploadViewModel()
        {
            try
            {
                _conversionTaskService = Infrastructure.ServiceLocator.GetConversionTaskService();
                _fileDialogService = Infrastructure.ServiceLocator.GetRequiredService<IFileDialogService>();
                _filePreprocessorService = Infrastructure.ServiceLocator.GetRequiredService<IFilePreprocessorService>();
                Utils.Logger.Info("FileUploadViewModel", "✅ 文件上传视图模型已初始化");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileUploadViewModel", $"❌ 初始化失败: {ex.Message}");
                throw;
            }

            UpdateUI();
        }

        #region 命令

        [RelayCommand]
        private async Task SelectFileAsync()
        {
            try
            {
                Utils.Logger.Info("FileUploadViewModel", "📄 选择文件");
                IsLoading = true;
                ProcessingStatus = "正在选择文件...";

                var selectedFiles = await _fileDialogService.SelectVideoFilesAsync();
                if (selectedFiles.Any())
                {
                    await ProcessFilesAsync(selectedFiles);
                }
                else
                {
                    Utils.Logger.Info("FileUploadViewModel", "ℹ️ 用户取消了文件选择");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileUploadViewModel", $"❌ 选择文件失败: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                ProcessingStatus = "";
            }
        }

        [RelayCommand]
        private async Task SelectFolderAsync()
        {
            try
            {
                Utils.Logger.Info("FileUploadViewModel", "📂 选择文件夹");
                IsLoading = true;
                ProcessingStatus = "正在选择文件夹...";

                var selectedFiles = await _fileDialogService.SelectFolderAsync();
                if (selectedFiles.Any())
                {
                    await ProcessFilesAsync(selectedFiles);
                }
                else
                {
                    Utils.Logger.Info("FileUploadViewModel", "ℹ️ 用户取消了文件夹选择");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileUploadViewModel", $"❌ 选择文件夹失败: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                ProcessingStatus = "";
            }
        }

        [RelayCommand]
        private async Task ClearAllAsync()
        {
            try
            {
                Utils.Logger.Info("FileUploadViewModel", "🗑️ 清空所有文件");
                FileItems.Clear();
                UpdateUI();
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileUploadViewModel", $"❌ 清空文件失败: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task ConvertAllAsync()
        {
            try
            {
                Utils.Logger.Info("FileUploadViewModel", "🚀 开始转换所有文件");
                IsLoading = true;

                foreach (var fileItem in FileItems)
                {
                    if (!fileItem.IsConverting && !fileItem.IsCompleted)
                    {
                        await StartConversionAsync(fileItem);
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileUploadViewModel", $"❌ 转换所有文件失败: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task OpenConversionSettingsAsync()
        {
            try
            {
                Utils.Logger.Info("FileUploadViewModel", "⚙️ 打开转换设置");

                // 创建并显示转换设置窗口
                var settingsWindow = new Views.ConversionSettingsWindow();

                // 获取主窗口
                if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
                {
                    await settingsWindow.ShowDialog(desktop.MainWindow);
                }
                else
                {
                    settingsWindow.Show();
                }

                Utils.Logger.Info("FileUploadViewModel", "✅ 转换设置窗口已关闭");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileUploadViewModel", $"❌ 打开转换设置失败: {ex.Message}");
            }
        }

        #endregion

        #region 拖拽处理

        /// <summary>
        /// 处理拖拽的文件
        /// </summary>
        public async Task HandleDroppedFilesAsync(string[] filePaths)
        {
            try
            {
                Utils.Logger.Info("FileUploadViewModel", $"📁 处理拖拽文件: {filePaths.Length} 个");
                await ProcessFilesAsync(filePaths);
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileUploadViewModel", $"❌ 处理拖拽文件失败: {ex.Message}");
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 处理文件列表
        /// </summary>
        private async Task ProcessFilesAsync(IEnumerable<string> filePaths)
        {
            try
            {
                IsProcessing = true;
                ProcessingStatus = "正在处理文件...";

                var progress = new Progress<string>(status => ProcessingStatus = status);
                var result = await _filePreprocessorService.PreprocessFilesAsync(
                    filePaths,
                    includeSubdirectories: true,
                    progress: progress);

                if (result.Success)
                {
                    // 添加处理成功的文件
                    foreach (var processedFile in result.ProcessedFiles)
                    {
                        if (processedFile.ViewModel != null)
                        {
                            FileItems.Add(processedFile.ViewModel);
                        }
                    }

                    UpdateUI();

                    // 显示处理结果
                    var stats = result.Statistics;
                    if (stats != null)
                    {
                        ProcessingStatus = $"✅ 添加了 {stats.ProcessedFiles} 个文件 ({stats.FormattedTotalSize})";
                        Utils.Logger.Info("FileUploadViewModel", ProcessingStatus);

                        if (result.SkippedFiles.Any())
                        {
                            Utils.Logger.Warning("FileUploadViewModel", $"⚠️ 跳过了 {result.SkippedFiles.Count} 个文件");
                        }
                    }
                }
                else
                {
                    ProcessingStatus = $"❌ 处理失败: {result.ErrorMessage}";
                    Utils.Logger.Error("FileUploadViewModel", ProcessingStatus);
                }
            }
            catch (Exception ex)
            {
                ProcessingStatus = $"❌ 处理文件时发生错误: {ex.Message}";
                Utils.Logger.Error("FileUploadViewModel", ProcessingStatus);
            }
            finally
            {
                IsProcessing = false;
                // 3秒后清除状态消息
                _ = Task.Delay(3000).ContinueWith(_ => ProcessingStatus = "");
            }
        }

        private void UpdateUI()
        {
            var hasFiles = FileItems.Count > 0;
            IsEmptyStateVisible = !hasFiles;
            IsFileListVisible = hasFiles;

            FileCountText = $"{FileItems.Count} 个文件";
            
            var totalSize = FileItems.Sum(f => f.FileSize);
            TotalSizeText = FormatFileSize(totalSize);
        }

        private async Task StartConversionAsync(FileItemViewModel fileItem)
        {
            try
            {
                Utils.Logger.Info("FileUploadViewModel", $"🚀 开始转换文件: {fileItem.FileName}");
                
                fileItem.IsConverting = true;
                fileItem.Progress = 0;

                // 创建转换参数
                var parameters = Domain.ValueObjects.ConversionParameters.CreateDefault();
                
                // 创建转换任务
                var task = await _conversionTaskService.CreateTaskAsync(
                    fileItem.FileName,
                    fileItem.FilePath,
                    fileItem.FileSize,
                    parameters);

                // 开始转换
                var request = new Application.DTOs.StartConversionRequestDto
                {
                    TaskName = fileItem.FileName,
                    Preset = "Fast 1080p30"
                };

                var success = await _conversionTaskService.StartConversionAsync(task.Id, request);
                if (success)
                {
                    Utils.Logger.Info("FileUploadViewModel", $"✅ 文件转换开始成功: {fileItem.FileName}");
                }
                else
                {
                    fileItem.IsConverting = false;
                    Utils.Logger.Error("FileUploadViewModel", $"❌ 文件转换开始失败: {fileItem.FileName}");
                }
            }
            catch (Exception ex)
            {
                fileItem.IsConverting = false;
                Utils.Logger.Error("FileUploadViewModel", $"❌ 开始转换文件失败: {ex.Message}");
            }
        }

        private string FormatFileSize(long bytes)
        {
            if (bytes == 0) return "0 B";

            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double len = bytes;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 添加文件
        /// </summary>
        public void AddFile(string filePath, long fileSize)
        {
            try
            {
                var fileName = System.IO.Path.GetFileName(filePath);
                var fileItem = new FileItemViewModel
                {
                    FileName = fileName,
                    FilePath = filePath,
                    FileSize = fileSize
                };

                FileItems.Add(fileItem);
                UpdateUI();
                
                Utils.Logger.Info("FileUploadViewModel", $"📁 添加文件: {fileName}");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileUploadViewModel", $"❌ 添加文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新转换进度
        /// </summary>
        public void UpdateConversionProgress(string taskId, int progress, double? speed, double? eta)
        {
            try
            {
                // 根据taskId查找对应的文件项并更新进度
                // 这里需要建立taskId和fileItem的映射关系
                Utils.Logger.Debug("FileUploadViewModel", $"📊 更新转换进度: {taskId} - {progress}%");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileUploadViewModel", $"❌ 更新转换进度失败: {ex.Message}");
            }
        }

        #endregion
    }

    /// <summary>
    /// 文件项视图模型
    /// </summary>
    public partial class FileItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _fileName = string.Empty;

        [ObservableProperty]
        private string _filePath = string.Empty;

        [ObservableProperty]
        private long _fileSize;

        [ObservableProperty]
        private int _progress;

        [ObservableProperty]
        private bool _isConverting;

        [ObservableProperty]
        private bool _isCompleted;

        [ObservableProperty]
        private string _status = "等待中";

        public string FormattedFileSize => FormatFileSize(FileSize);

        private string FormatFileSize(long bytes)
        {
            if (bytes == 0) return "0 B";

            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double len = bytes;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
