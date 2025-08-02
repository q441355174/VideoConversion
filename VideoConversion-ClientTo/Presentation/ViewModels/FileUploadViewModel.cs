using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VideoConversion_ClientTo.Application.DTOs;
using VideoConversion_ClientTo.Application.Interfaces;
using VideoConversion_ClientTo.Domain.ValueObjects;
using VideoConversion_ClientTo.Infrastructure.Services;
using VideoConversion_ClientTo.ViewModels;
using FileProcessingProgress = VideoConversion_ClientTo.Infrastructure.Services.FileProcessingProgress;

namespace VideoConversion_ClientTo.Presentation.ViewModels
{
    /// <summary>
    /// 批量上传进度信息 - 与Client项目一致
    /// </summary>
    public class BatchUploadProgress
    {
        public int TotalFiles { get; set; }
        public int CompletedFiles { get; set; }
        public double OverallProgress { get; set; }
        public string CurrentFile { get; set; } = "";
        public double CurrentFileProgress { get; set; }
        public string Status { get; set; } = "";
    }

    /// <summary>
    /// 文件上传视图模型 - 完整功能实现
    /// </summary>
    public partial class FileUploadViewModel : ViewModelBase
    {
        #region 私有字段

        private readonly IConversionTaskService _conversionTaskService;
        private readonly IFileDialogService _fileDialogService;
        private readonly IFilePreprocessorService _filePreprocessorService;
        private readonly UnifiedProgressManager _progressManager;
        private readonly FFmpegService _ffmpegService;
        private readonly ThumbnailService _thumbnailService;
        private readonly List<string> _selectedFiles = new();
        private bool _hasFiles = false;
        private bool _isConverting = false;

        #endregion

        #region 可观察属性

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
        private string _statusText = "就绪 - 请选择视频文件开始转换";

        [ObservableProperty]
        private bool _canStartConversion = false;

        [ObservableProperty]
        private bool _canSelectFiles = true;

        #endregion

        [ObservableProperty]
        private string _processingStatus = "";

        [ObservableProperty]
        private bool _isProcessing = false;

        #region 构造函数

        public FileUploadViewModel()
        {
            try
            {
                _conversionTaskService = Infrastructure.ServiceLocator.GetConversionTaskService();
                _fileDialogService = Infrastructure.ServiceLocator.GetRequiredService<IFileDialogService>();
                _filePreprocessorService = Infrastructure.ServiceLocator.GetRequiredService<IFilePreprocessorService>();

                // 初始化新的服务
                _ffmpegService = FFmpegService.Instance;
                _thumbnailService = ThumbnailService.Instance;

                // 获取父窗口用于进度窗口
                var parentWindow = GetParentWindow();
                _progressManager = new UnifiedProgressManager(FileItems, parentWindow);

                // 文件上传视图模型初始化完成（移除日志）
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileUploadViewModel", $"❌ 初始化失败: {ex.Message}");
                // 不抛出异常，使用简化模式
            }

            // 初始化状态
            UpdateViewState();
            UpdateUI();
        }

        #endregion

        #region 转换相关辅助方法

        /// <summary>
        /// 创建转换请求 - 使用当前转换设置
        /// </summary>
        private StartConversionRequestDto CreateConversionRequest()
        {
            var settings = ConversionSettingsService.Instance.CurrentSettings;

            return new StartConversionRequestDto
            {
                TaskName = "批量转换任务",
                Preset = settings.Preset,
                OutputFormat = settings.OutputFormat,
                Resolution = settings.Resolution,
                VideoCodec = settings.VideoCodec,
                AudioCodec = settings.AudioCodec,
                VideoQuality = settings.VideoQuality,
                AudioBitrate = settings.AudioQuality,
                EncodingPreset = settings.Preset,
                HardwareAcceleration = "auto",
                FastStart = true,
                TwoPass = false
            };
        }

        /// <summary>
        /// 统一TaskId管理和本地数据库保存 - 与Client项目逻辑一致
        /// </summary>
        private async Task<Dictionary<string, string>> SaveTasksToLocalDatabaseWithUnifiedManagementAsync(
            List<string> filePaths,
            StartConversionRequestDto request)
        {
            var taskIdMapping = new Dictionary<string, string>();

            try
            {
                Utils.Logger.Info("FileUploadViewModel", "💾 开始保存任务到本地数据库");

                foreach (var filePath in filePaths)
                {
                    // 生成本地TaskId
                    var localTaskId = Guid.NewGuid().ToString();

                    // 查找对应的FileItem
                    var fileItem = FileItems.FirstOrDefault(f => f.FilePath == filePath);
                    if (fileItem != null)
                    {
                        // 设置本地TaskId
                        fileItem.LocalTaskId = localTaskId;

                        // 创建任务记录
                        var taskDto = new ConversionTaskDto
                        {
                            Id = localTaskId,
                            TaskName = Path.GetFileNameWithoutExtension(filePath),
                            OriginalFileName = Path.GetFileName(filePath),
                            OriginalFilePath = filePath,
                            OriginalFileSize = GetFileSizeInBytes(fileItem.FileSize),
                            OutputFormat = request.OutputFormat,
                            VideoCodec = request.VideoCodec,
                            AudioCodec = request.AudioCodec,
                            VideoQuality = request.VideoQuality,
                            Resolution = request.Resolution,
                            Status = 0, // Pending
                            Progress = 0,
                            CreatedAt = DateTime.Now
                        };

                        // 保存到本地数据库
                        await _conversionTaskService.CreateTaskAsync(
                            taskDto.TaskName,
                            taskDto.OriginalFilePath!,
                            taskDto.OriginalFileSize ?? 0,
                            ConversionSettingsService.Instance.CurrentSettings.ToConversionParameters());

                        taskIdMapping[filePath] = localTaskId;

                        Utils.Logger.Info("FileUploadViewModel", $"✅ 任务已保存: {Path.GetFileName(filePath)} -> {localTaskId}");
                    }
                }

                Utils.Logger.Info("FileUploadViewModel", $"💾 本地数据库保存完成，共 {taskIdMapping.Count} 个任务");
                return taskIdMapping;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileUploadViewModel", $"❌ 保存任务到本地数据库失败: {ex.Message}");
                return taskIdMapping;
            }
        }

        /// <summary>
        /// 处理批量转换成功
        /// </summary>
        private async Task HandleBatchConversionSuccessAsync(object batchResult, List<FileItemViewModel> filesToConvert)
        {
            try
            {
                Utils.Logger.Info("FileUploadViewModel", "✅ 批量转换API调用成功");

                // 这里需要根据实际的batchResult结构来处理
                // 暂时使用模拟逻辑
                StatusText = $"✅ 批量转换已启动，正在处理 {filesToConvert.Count} 个文件";

                // 更新文件状态为转换中
                foreach (var fileItem in filesToConvert)
                {
                    // 使用统一进度更新方法
                    UpdateFileItemProgress(fileItem, 0, "转换已启动", "converting");

                    // 加入SignalR任务组
                    if (!string.IsNullOrEmpty(fileItem.LocalTaskId))
                    {
                        await JoinTaskGroupAsync(fileItem.LocalTaskId);
                    }
                }

                Utils.Logger.Info("FileUploadViewModel", $"🎉 批量转换启动完成，{filesToConvert.Count} 个文件开始转换");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileUploadViewModel", $"❌ 处理批量转换成功结果失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理批量转换失败
        /// </summary>
        private async Task HandleBatchConversionFailureAsync(string? errorMessage, List<FileItemViewModel> filesToConvert)
        {
            try
            {
                Utils.Logger.Error("FileUploadViewModel", $"❌ 批量转换API调用失败: {errorMessage}");
                StatusText = $"❌ 转换启动失败: {errorMessage}";

                // 重置文件状态
                foreach (var fileItem in filesToConvert)
                {
                    fileItem.Status = FileItemStatus.Pending;
                    fileItem.StatusText = "转换失败";
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileUploadViewModel", $"❌ 处理批量转换失败结果异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新批量进度 - 使用统一进度更新方法
        /// </summary>
        private void UpdateBatchProgress(BatchUploadProgress progress)
        {
            try
            {
                // 更新总体进度显示
                StatusText = $"批量处理: {progress.CompletedFiles}/{progress.TotalFiles} 文件完成 ({progress.OverallProgress:F1}%)";

                // 更新当前文件进度 - 使用统一方法避免冲突
                if (!string.IsNullOrEmpty(progress.CurrentFile))
                {
                    var fileItem = FileItems.FirstOrDefault(f => f.FilePath == progress.CurrentFile);
                    if (fileItem != null)
                    {
                        // 只在文件还未开始转换时更新进度（避免与SignalR转换进度冲突）
                        if (fileItem.Status != FileItemStatus.Converting)
                        {
                            UpdateFileItemProgress(fileItem, progress.CurrentFileProgress,
                                $"处理中... {progress.CurrentFileProgress:F1}%", "processing");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileUploadViewModel", $"❌ 更新批量进度失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取文件大小（字节）
        /// </summary>
        private long GetFileSizeInBytes(string fileSizeText)
        {
            try
            {
                if (string.IsNullOrEmpty(fileSizeText)) return 0;

                var sizeText = fileSizeText.Replace(" ", "").ToUpper();

                if (sizeText.EndsWith("GB"))
                {
                    if (double.TryParse(sizeText.Replace("GB", ""), out var gb))
                        return (long)(gb * 1024 * 1024 * 1024);
                }
                else if (sizeText.EndsWith("MB"))
                {
                    if (double.TryParse(sizeText.Replace("MB", ""), out var mb))
                        return (long)(mb * 1024 * 1024);
                }
                else if (sizeText.EndsWith("KB"))
                {
                    if (double.TryParse(sizeText.Replace("KB", ""), out var kb))
                        return (long)(kb * 1024);
                }

                return 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 预估转换后文件大小 - 与Client项目算法一致
        /// </summary>
        private long EstimateConvertedFileSize(long originalSizeBytes, ConversionParameters settings)
        {
            try
            {
                // 基础压缩比率（根据编码器和质量）
                double compressionRatio = 1.0;

                // 根据视频编码器调整
                compressionRatio *= settings.VideoCodec.ToLower() switch
                {
                    var codec when codec.Contains("h265") || codec.Contains("hevc") => 0.5, // H.265更高效
                    var codec when codec.Contains("h264") => 0.7, // H.264标准效率
                    var codec when codec.Contains("vp9") => 0.6, // VP9高效
                    var codec when codec.Contains("av1") => 0.4, // AV1最高效
                    _ => 0.8 // 其他编码器
                };

                // 根据质量设置调整
                if (double.TryParse(settings.VideoQuality, out var quality))
                {
                    // CRF值越低质量越高，文件越大
                    compressionRatio *= quality switch
                    {
                        <= 18 => 1.2, // 高质量
                        <= 23 => 1.0, // 标准质量
                        <= 28 => 0.8, // 中等质量
                        _ => 0.6 // 低质量
                    };
                }

                // 根据分辨率调整
                compressionRatio *= settings.Resolution switch
                {
                    "3840x2160" => 2.0, // 4K
                    "1920x1080" => 1.0, // 1080p
                    "1280x720" => 0.6,  // 720p
                    "854x480" => 0.4,   // 480p
                    _ => 1.0 // 保持原始或其他
                };

                var estimatedSize = (long)(originalSizeBytes * compressionRatio);

                // 确保最小值
                return Math.Max(estimatedSize, originalSizeBytes / 10);
            }
            catch
            {
                // 出错时返回原大小的70%作为估算
                return (long)(originalSizeBytes * 0.7);
            }
        }

        /// <summary>
        /// 加入SignalR任务组
        /// </summary>
        private async Task JoinTaskGroupAsync(string taskId)
        {
            try
            {
                var signalRService = SignalRService.Instance;
                if (signalRService.IsConnected)
                {
                    await signalRService.JoinTaskGroupAsync(taskId);

                    // 注册进度更新回调 - 使用统一进度管理
                    _progressManager.RegisterTaskProgress(taskId, (progress, status, speed, eta) =>
                    {
                        // 在UI线程中执行
                        Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            // 查找对应的文件项并更新进度
                            var fileItem = FileItems.FirstOrDefault(f => f.TaskId == taskId || f.LocalTaskId == taskId);
                            if (fileItem != null)
                            {
                                // 使用统一的进度更新方法
                                UpdateFileItemProgress(fileItem, progress, status, "converting", speed, eta);

                                Utils.Logger.Debug("FileUploadViewModel",
                                    $"📊 SignalR进度回调: {fileItem.FileName} - {progress:F1}%");
                            }
                            else
                            {
                                Utils.Logger.Warning("FileUploadViewModel",
                                    $"⚠️ SignalR回调未找到文件项: TaskId={taskId}");
                            }
                        });
                    });
                }
                else
                {
                    Utils.Logger.Warning("FileUploadViewModel", "⚠️ SignalR未连接，无法加入任务组");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileUploadViewModel", $"❌ 加入任务组失败: {taskId} - {ex.Message}");
            }
        }

        #endregion

        #region 统一进度管理

        /// <summary>
        /// 统一的文件项进度更新方法 - 防止进度冲突
        /// </summary>
        private void UpdateFileItemProgress(
            FileItemViewModel fileItem,
            double progress,
            string status,
            string phase = "",
            double? speed = null,
            double? eta = null)
        {
            try
            {
                // 确保进度值在有效范围内
                var safeProgress = Math.Max(0, Math.Min(100, progress));

                // 根据阶段更新状态
                switch (phase.ToLower())
                {
                    case "analyzing":
                        fileItem.Status = FileItemStatus.Pending;
                        fileItem.StatusText = $"分析中... {safeProgress:F0}%";
                        break;

                    case "processing":
                        fileItem.Status = FileItemStatus.Pending;
                        fileItem.StatusText = status;
                        break;

                    case "uploading":
                        fileItem.Status = FileItemStatus.Uploading;
                        fileItem.StatusText = $"上传中... {safeProgress:F0}%";
                        break;

                    case "converting":
                        fileItem.Status = FileItemStatus.Converting;
                        fileItem.IsConverting = true;
                        fileItem.StatusText = speed.HasValue ?
                            $"转换中... {safeProgress:F0}% (速度: {speed:F1}x)" :
                            $"转换中... {safeProgress:F0}%";
                        break;

                    default:
                        fileItem.StatusText = status;
                        break;
                }

                // 更新进度
                fileItem.Progress = safeProgress;

                // 处理完成状态
                if (safeProgress >= 100)
                {
                    switch (phase.ToLower())
                    {
                        case "converting":
                            fileItem.Status = FileItemStatus.Completed;
                            fileItem.StatusText = "转换完成";
                            fileItem.IsConverting = false;
                            fileItem.CanConvert = false;
                            break;

                        case "uploading":
                            fileItem.Status = FileItemStatus.Uploading;
                            fileItem.StatusText = "上传完成，等待转换...";
                            break;
                    }
                }

                Utils.Logger.Debug("FileUploadViewModel",
                    $"✅ 统一进度更新: {fileItem.FileName} - {safeProgress:F1}% - {fileItem.StatusText}");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileUploadViewModel",
                    $"❌ 统一进度更新失败: {fileItem.FileName} - {ex.Message}");
            }
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 处理文件项移除请求
        /// </summary>
        private void OnFileItemRemoveRequested(FileItemViewModel fileItem)
        {
            try
            {
                Utils.Logger.Info("FileUploadViewModel", $"🗑️ 处理文件移除请求: {fileItem.FileName}");

                // 在UI线程中移除文件项
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (FileItems.Contains(fileItem))
                    {
                        // 取消订阅事件
                        fileItem.RemoveRequested -= OnFileItemRemoveRequested;
                        fileItem.ConversionRequested -= OnFileItemConversionRequested;

                        // 如果正在转换，先取消转换
                        if (fileItem.IsConverting)
                        {
                            fileItem.CancelConversion();
                        }

                        // 从集合中移除
                        FileItems.Remove(fileItem);

                        Utils.Logger.Info("FileUploadViewModel", $"✅ 文件已移除: {fileItem.FileName}");

                        // 更新状态
                        UpdateViewState();
                    }
                });
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileUploadViewModel", $"❌ 移除文件失败: {fileItem.FileName} - {ex.Message}");
            }
        }

        /// <summary>
        /// 处理单个文件转换请求
        /// </summary>
        private async void OnFileItemConversionRequested(FileItemViewModel fileItem)
        {
            try
            {
                Utils.Logger.Info("FileUploadViewModel", $"🚀 处理单个文件转换请求: {fileItem.FileName}");

                // 使用与批量转换相同的逻辑处理单个文件
                var filePaths = new List<string> { fileItem.FilePath };
                var request = CreateConversionRequest();

                // 🔑 统一TaskId管理和本地数据库保存
                var taskIdMapping = await SaveTasksToLocalDatabaseWithUnifiedManagementAsync(filePaths, request);

                if (taskIdMapping.ContainsKey(fileItem.FilePath))
                {
                    var localTaskId = taskIdMapping[fileItem.FilePath];
                    fileItem.LocalTaskId = localTaskId;

                    // 🔑 调用批量转换API（单个文件）
                    var result = await _conversionTaskService.StartBatchConversionAsync(filePaths, request, null);

                    if (result.Success)
                    {
                        // 使用统一进度更新方法
                        UpdateFileItemProgress(fileItem, 0, "转换已启动", "converting");

                        // 加入SignalR任务组
                        await JoinTaskGroupAsync(localTaskId);

                        Utils.Logger.Info("FileUploadViewModel", $"✅ 单个文件转换启动成功: {fileItem.FileName}");
                    }
                    else
                    {
                        UpdateFileItemProgress(fileItem, 0, $"转换失败: {result.Message}", "failed");
                        Utils.Logger.Error("FileUploadViewModel", $"❌ 单个文件转换启动失败: {fileItem.FileName} - {result.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileUploadViewModel", $"❌ 处理单个文件转换请求失败: {fileItem.FileName} - {ex.Message}");
                UpdateFileItemProgress(fileItem, 0, $"转换异常: {ex.Message}", "failed");
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取父窗口
        /// </summary>
        private Avalonia.Controls.Window? GetParentWindow()
        {
            try
            {
                if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                {
                    return desktop.MainWindow;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region 命令实现

        /// <summary>
        /// 选择文件命令
        /// </summary>
        [RelayCommand]
        private async Task SelectFileAsync()
        {
            try
            {
                Utils.Logger.Info("FileUploadViewModel", "📄 开始选择文件");
                IsLoading = true;
                CanSelectFiles = false;
                ProcessingStatus = "正在选择文件...";
                StatusText = "📁 正在选择文件，请稍候...";

                // 使用文件对话框选择文件
                var selectedFiles = await _fileDialogService?.SelectVideoFilesAsync() ?? new List<string>();
                if (selectedFiles.Any())
                {
                    await ProcessFilesAsync(selectedFiles);
                }
                else
                {
                    Utils.Logger.Info("FileUploadViewModel", "ℹ️ 用户取消了文件选择");
                    StatusText = "就绪 - 请选择视频文件开始转换";
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileUploadViewModel", $"❌ 选择文件失败: {ex.Message}");
                StatusText = $"❌ 选择文件失败: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
                CanSelectFiles = true;
                ProcessingStatus = "";
            }
        }

        /// <summary>
        /// 选择文件夹命令
        /// </summary>
        [RelayCommand]
        private async Task SelectFolderAsync()
        {
            try
            {
                Utils.Logger.Info("FileUploadViewModel", "📂 开始选择文件夹");
                IsLoading = true;
                CanSelectFiles = false;
                ProcessingStatus = "正在选择文件夹...";
                StatusText = "📁 正在选择文件夹，请稍候...";

                var selectedFiles = await _fileDialogService?.SelectFolderAsync() ?? new List<string>();
                if (selectedFiles.Any())
                {
                    await ProcessFilesAsync(selectedFiles);
                }
                else
                {
                    Utils.Logger.Info("FileUploadViewModel", "ℹ️ 用户取消了文件夹选择");
                    StatusText = "就绪 - 请选择视频文件开始转换";
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileUploadViewModel", $"❌ 选择文件夹失败: {ex.Message}");
                StatusText = $"❌ 选择文件夹失败: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
                CanSelectFiles = true;
                ProcessingStatus = "";
            }
        }

        /// <summary>
        /// 清空所有文件命令
        /// </summary>
        [RelayCommand]
        private async Task ClearAllAsync()
        {
            try
            {
                Utils.Logger.Info("FileUploadViewModel", "🗑️ 清空所有文件");

                // 停止所有正在进行的转换
                foreach (var fileItem in FileItems.Where(f => f.IsConverting))
                {
                    fileItem.CancelConversionCommand.Execute(null);
                }

                FileItems.Clear();
                _selectedFiles.Clear();
                _hasFiles = false;

                UpdateViewState();
                UpdateUI();

                StatusText = "就绪 - 请选择视频文件开始转换";
                Utils.Logger.Info("FileUploadViewModel", "✅ 所有文件已清空");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileUploadViewModel", $"❌ 清空文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 开始批量转换命令 - 按照Client项目的正确逻辑实现
        /// </summary>
        [RelayCommand]
        private async Task StartConversionAsync()
        {
            Utils.Logger.Info("FileUploadViewModel", "=== 开始批量文件转换流程 ===");

            try
            {
                // 验证文件
                if (FileItems.Count == 0)
                {
                    Utils.Logger.Info("FileUploadViewModel", "❌ 没有选择文件，退出转换流程");
                    StatusText = "请先选择要转换的文件";
                    return;
                }

                Utils.Logger.Info("FileUploadViewModel", $"文件列表中共有 {FileItems.Count} 个文件");

                _isConverting = true;
                CanSelectFiles = false;
                UpdateViewState();

                // 获取待转换文件
                var filesToConvert = FileItems.Where(f => f.Status == FileItemStatus.Pending).ToList();
                Utils.Logger.Info("FileUploadViewModel", $"待转换文件数量: {filesToConvert.Count}");

                if (filesToConvert.Count == 0)
                {
                    Utils.Logger.Info("FileUploadViewModel", "❌ 没有待转换的文件，退出转换流程");
                    StatusText = "没有待转换的文件";
                    return;
                }

                // 待转换文件列表准备完成（移除日志）

                var filePaths = filesToConvert.Select(f => f.FilePath).ToList();
                var request = CreateConversionRequest();

                Utils.Logger.Info("FileUploadViewModel", $"🎯 转换参数: 格式={request.OutputFormat}, 分辨率={request.Resolution}, 视频编码={request.VideoCodec}");

                StatusText = $"开始批量转换 {filePaths.Count} 个文件";

                // 🔑 === 统一TaskId管理和本地数据库保存的核心实现 ===
                Utils.Logger.Info("FileUploadViewModel", "💾 === 开始统一TaskId管理和本地数据库保存 ===");
                var taskIdMapping = await SaveTasksToLocalDatabaseWithUnifiedManagementAsync(filePaths, request);
                Utils.Logger.Info("FileUploadViewModel", $"📊 本地任务数据库已更新，建立了 {taskIdMapping.Count} 个统一TaskId映射关系");

                // 系统状态和TaskId映射准备完成（移除日志）

                // 创建批量进度报告器
                var progress = new Progress<object>(p =>
                {
                    if (p is BatchUploadProgress batchProgress)
                    {
                        Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            // 批量进度更新（移除日志）
                            UpdateBatchProgress(batchProgress);
                        });
                    }
                });

                Utils.Logger.Info("FileUploadViewModel", $"🚀 开始调用批量转换API，文件数量: {filePaths.Count}");

                // 🔑 使用批量转换API - 与Client项目逻辑一致
                var result = await _conversionTaskService.StartBatchConversionAsync(filePaths, request, progress);

                Utils.Logger.Info("FileUploadViewModel", "📥 收到批量转换API响应");

                if (result.Success && result.Data != null)
                {
                    await HandleBatchConversionSuccessAsync(result.Data, filesToConvert);
                }
                else
                {
                    await HandleBatchConversionFailureAsync(result.Message, filesToConvert);
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileUploadViewModel", $"❌ 批量转换异常: {ex.Message}");
                StatusText = $"❌ 转换异常: {ex.Message}";

                // 重置所有转换中的文件状态
                var convertingFiles = FileItems.Where(f => f.Status == FileItemStatus.Converting).ToList();
                foreach (var fileItem in convertingFiles)
                {
                    fileItem.Status = FileItemStatus.Pending;
                    fileItem.StatusText = "转换异常";
                }
            }
            finally
            {
                _isConverting = false;
                CanSelectFiles = true;
                UpdateViewState();
            }
        }

        #endregion

        #region 核心文件处理方法

        /// <summary>
        /// 处理文件列表 - 使用增强版批量处理
        /// </summary>
        private async Task ProcessFilesAsync(IEnumerable<string> filePaths)
        {
            try
            {
                var filePathList = filePaths.ToList();
                Utils.Logger.Info("FileUploadViewModel", $"🔄 开始处理文件，数量: {filePathList.Count}");

                IsProcessing = true;
                CanSelectFiles = false;
                ProcessingStatus = "正在处理文件...";
                StatusText = "📋 正在分析文件，请稍候...";

                // 1. 文件验证和过滤
                var validFiles = new List<string>();
                var invalidFiles = new List<string>();

                foreach (var filePath in filePathList)
                {
                    if (await IsValidVideoFileAsync(filePath))
                    {
                        // 检查是否已经添加过
                        if (!_selectedFiles.Contains(filePath) && !FileItems.Any(f => f.FilePath == filePath))
                        {
                            validFiles.Add(filePath);
                            _selectedFiles.Add(filePath);
                        }
                        else
                        {
                            Utils.Logger.Info("FileUploadViewModel", $"⚠️ 文件已存在，跳过: {Path.GetFileName(filePath)}");
                        }
                    }
                    else
                    {
                        invalidFiles.Add(filePath);
                    }
                }

                Utils.Logger.Info("FileUploadViewModel", $"✅ 文件验证完成 - 有效: {validFiles.Count}, 无效: {invalidFiles.Count}");

                // 2. 使用增强版批量处理有效文件
                if (validFiles.Any())
                {
                    var result = await _progressManager.StartBatchProcessingAsync(
                        validFiles,
                        ProcessSingleFileWithProgressAsync,
                        showProgressWindow: validFiles.Count > 1, // 多文件时显示进度窗口
                        windowTitle: $"处理 {validFiles.Count} 个视频文件"
                    );

                    // 处理结果
                    if (result.IsCompleted)
                    {
                        if (result.WasCancelled)
                        {
                            StatusText = $"⚠️ 处理已取消 - 成功: {result.SuccessfulFiles}, 失败: {result.FailedFiles.Count}";
                        }
                        else
                        {
                            StatusText = $"✅ 处理完成 - 成功: {result.SuccessfulFiles}, 失败: {result.FailedFiles.Count}, 耗时: {result.Duration.TotalSeconds:F1}秒";
                        }
                    }
                    else
                    {
                        StatusText = $"❌ 处理失败: {result.ErrorMessage}";
                    }
                }

                // 3. 处理无效文件
                if (invalidFiles.Any())
                {
                    var invalidFileNames = invalidFiles.Select(Path.GetFileName).Take(3);
                    var message = $"发现 {invalidFiles.Count} 个无效文件: {string.Join(", ", invalidFileNames)}";
                    if (invalidFiles.Count > 3) message += "...";

                    Utils.Logger.Warning("FileUploadViewModel", message);

                    if (validFiles.Any())
                    {
                        StatusText += $" (跳过 {invalidFiles.Count} 个无效文件)";
                    }
                    else
                    {
                        StatusText = $"⚠️ {message}";
                    }
                }

                // 4. 更新UI状态
                _hasFiles = FileItems.Count > 0;
                UpdateViewState();
                UpdateUI();

                if (!validFiles.Any() && !invalidFiles.Any())
                {
                    StatusText = "就绪 - 请选择视频文件开始转换";
                }

                // 文件处理完成（移除日志）
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileUploadViewModel", $"❌ 处理文件失败: {ex.Message}");
                StatusText = $"❌ 处理文件失败: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
                CanSelectFiles = true;
                ProcessingStatus = "";
            }
        }

        /// <summary>
        /// 处理单个转换任务并报告进度 - 用于批量转换
        /// </summary>
        private async Task<FileItemViewModel?> ProcessSingleConversionWithProgressAsync(
            string filePath,
            IProgress<FileProcessingProgress> progress,
            CancellationToken cancellationToken)
        {
            try
            {
                // 查找对应的文件项
                var fileItem = FileItems.FirstOrDefault(f => f.FilePath == filePath);
                if (fileItem == null)
                {
                    Utils.Logger.Warning("FileUploadViewModel", $"⚠️ 未找到文件项: {filePath}");
                    return null;
                }

                // 报告开始转换
                progress?.Report(new FileProcessingProgress
                {
                    Progress = 0,
                    Status = "准备转换...",
                    Phase = "initializing",
                    CurrentOperation = "获取转换设置"
                });

                // 检查取消
                cancellationToken.ThrowIfCancellationRequested();

                // 获取当前转换设置
                var conversionSettings = ConversionSettingsService.Instance.CurrentSettings;

                // 报告创建转换任务
                progress?.Report(new FileProcessingProgress
                {
                    Progress = 10,
                    Status = "创建转换任务...",
                    Phase = "creating_task",
                    CurrentOperation = "向服务器发送转换请求"
                });

                // 检查取消
                cancellationToken.ThrowIfCancellationRequested();

                // 创建转换任务
                var conversionTask = await _conversionTaskService.CreateTaskAsync(
                    fileItem.FileName,
                    fileItem.FilePath,
                    long.Parse(fileItem.FileSize.Replace(" MB", "").Replace(" KB", "").Replace(" GB", "")) * 1024 * 1024, // 转换为字节
                    conversionSettings.ToConversionParameters());

                if (conversionTask == null)
                {
                    progress?.Report(new FileProcessingProgress
                    {
                        Progress = 0,
                        Status = "创建任务失败",
                        Phase = "failed"
                    });
                    return null;
                }

                // 报告启动转换
                progress?.Report(new FileProcessingProgress
                {
                    Progress = 20,
                    Status = "启动转换...",
                    Phase = "starting_conversion",
                    CurrentOperation = "服务器开始处理视频"
                });

                // 检查取消
                cancellationToken.ThrowIfCancellationRequested();

                // 启动转换
                var startResult = await _conversionTaskService.StartConversionAsync(
                    conversionTask.Id,
                    CreateConversionRequest());

                if (!startResult)
                {
                    progress?.Report(new FileProcessingProgress
                    {
                        Progress = 0,
                        Status = "启动转换失败",
                        Phase = "failed"
                    });
                    return null;
                }

                // 更新文件项状态
                fileItem.TaskId = conversionTask.Id;
                fileItem.Status = FileItemStatus.Converting;
                fileItem.StatusText = "转换中...";

                // 报告转换已启动
                progress?.Report(new FileProcessingProgress
                {
                    Progress = 30,
                    Status = "转换已启动",
                    Phase = "converting",
                    CurrentOperation = "服务器正在处理视频"
                });

                // 加入SignalR任务组以接收进度更新
                await JoinTaskGroupAsync(conversionTask.Id);

                // 报告完成
                progress?.Report(new FileProcessingProgress
                {
                    Progress = 100,
                    Status = "转换任务已启动",
                    Phase = "completed",
                    CurrentOperation = "等待服务器处理完成"
                });

                // 转换任务启动完成（移除日志）
                return fileItem;
            }
            catch (OperationCanceledException)
            {
                // 转换任务取消（移除日志）
                return null;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileUploadViewModel", $"❌ 转换任务失败: {Path.GetFileName(filePath)} - {ex.Message}");

                progress?.Report(new FileProcessingProgress
                {
                    Progress = 0,
                    Status = $"转换失败: {ex.Message}",
                    Phase = "failed"
                });

                return null;
            }
        }

        /// <summary>
        /// 处理单个文件并报告进度 - 用于批量处理
        /// </summary>
        private async Task<FileItemViewModel?> ProcessSingleFileWithProgressAsync(
            string filePath,
            IProgress<FileProcessingProgress> progress,
            CancellationToken cancellationToken)
        {
            try
            {
                // 报告开始处理
                progress?.Report(new FileProcessingProgress
                {
                    Progress = 0,
                    Status = "开始处理...",
                    Phase = "initializing",
                    CurrentOperation = "准备分析文件"
                });

                // 检查取消
                cancellationToken.ThrowIfCancellationRequested();

                // 创建基础文件项
                var fileItem = await CreateFileItemAsync(filePath);
                if (fileItem == null)
                {
                    progress?.Report(new FileProcessingProgress
                    {
                        Progress = 0,
                        Status = "创建文件项失败",
                        Phase = "failed"
                    });
                    return null;
                }

                // 报告文件信息分析阶段
                progress?.Report(new FileProcessingProgress
                {
                    Progress = 25,
                    Status = "分析视频信息...",
                    Phase = "analyzing",
                    CurrentOperation = "使用FFmpeg分析视频"
                });

                // 检查取消
                cancellationToken.ThrowIfCancellationRequested();

                // 分析视频信息
                await AnalyzeVideoFileWithProgressAsync(fileItem, progress, cancellationToken);

                // 报告完成
                progress?.Report(new FileProcessingProgress
                {
                    Progress = 100,
                    Status = "处理完成",
                    Phase = "completed",
                    CurrentOperation = "文件已添加到列表"
                });

                // 单文件处理完成（移除日志）
                return fileItem;
            }
            catch (OperationCanceledException)
            {
                // 文件处理取消（移除日志）
                return null;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileUploadViewModel", $"❌ 单文件处理失败: {Path.GetFileName(filePath)} - {ex.Message}");

                progress?.Report(new FileProcessingProgress
                {
                    Progress = 0,
                    Status = $"处理失败: {ex.Message}",
                    Phase = "failed"
                });

                return null;
            }
        }

        /// <summary>
        /// 处理有效文件列表
        /// </summary>
        private async Task ProcessValidFilesAsync(List<string> validFiles)
        {
            try
            {
                Utils.Logger.Info("FileUploadViewModel", $"📋 开始处理 {validFiles.Count} 个有效文件");

                var processedCount = 0;
                foreach (var filePath in validFiles)
                {
                    processedCount++;
                    ProcessingStatus = $"正在处理文件 {processedCount}/{validFiles.Count}...";
                    StatusText = $"📋 正在分析文件 ({processedCount}/{validFiles.Count})";

                    try
                    {
                        // 创建文件项
                        var fileItem = await CreateFileItemAsync(filePath);
                        if (fileItem != null)
                        {
                            // 订阅事件
                            fileItem.RemoveRequested += OnFileItemRemoveRequested;
                            fileItem.ConversionRequested += OnFileItemConversionRequested;

                            // 在UI线程中添加到集合
                            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                            {
                                FileItems.Add(fileItem);
                            });

                            // 文件处理完成（移除日志）
                        }
                    }
                    catch (Exception ex)
                    {
                        Utils.Logger.Error("FileUploadViewModel", $"❌ 处理文件失败: {Path.GetFileName(filePath)} - {ex.Message}");
                    }

                    // 添加小延迟避免UI阻塞
                    await Task.Delay(100);
                }

                Utils.Logger.Info("FileUploadViewModel", $"🎉 所有有效文件处理完成，成功: {FileItems.Count}");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileUploadViewModel", $"❌ 处理有效文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建文件项
        /// </summary>
        private async Task<FileItemViewModel?> CreateFileItemAsync(string filePath)
        {
            try
            {
                var fileInfo = new System.IO.FileInfo(filePath);
                if (!fileInfo.Exists)
                {
                    Utils.Logger.Warning("FileUploadViewModel", $"文件不存在: {filePath}");
                    return null;
                }

                // 创建基础文件项
                var fileItem = new FileItemViewModel
                {
                    FileName = fileInfo.Name,
                    FilePath = filePath,
                    FileSize = FormatFileSize(fileInfo.Length),
                    LocalTaskId = Guid.NewGuid().ToString(),
                    Status = FileItemStatus.Pending,
                    StatusText = "等待处理"
                };

                // 异步分析视频信息
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await AnalyzeVideoFileAsync(fileItem);
                    }
                    catch (Exception ex)
                    {
                        Utils.Logger.Error("FileUploadViewModel", $"❌ 分析视频文件失败: {fileItem.FileName} - {ex.Message}");
                    }
                });

                return fileItem;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileUploadViewModel", $"❌ 创建文件项失败: {Path.GetFileName(filePath)} - {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 分析视频文件信息 - 使用FFmpeg完整分析
        /// </summary>
        private async Task AnalyzeVideoFileAsync(FileItemViewModel fileItem)
        {
            await AnalyzeVideoFileWithProgressAsync(fileItem, null, CancellationToken.None);
        }

        /// <summary>
        /// 分析视频文件信息并报告进度 - 增强版
        /// </summary>
        private async Task AnalyzeVideoFileWithProgressAsync(
            FileItemViewModel fileItem,
            IProgress<FileProcessingProgress>? progress,
            CancellationToken cancellationToken)
        {
            try
            {
                // 开始分析视频文件（移除日志）

                // 更新状态
                fileItem.StatusText = "正在分析...";

                // 报告FFmpeg分析阶段
                progress?.Report(new FileProcessingProgress
                {
                    Progress = 30,
                    Status = "分析视频信息...",
                    Phase = "ffmpeg_analysis",
                    CurrentOperation = "使用FFmpeg获取视频元数据"
                });

                // 检查取消
                cancellationToken.ThrowIfCancellationRequested();

                // 使用FFmpeg服务获取视频信息
                var videoInfo = await _ffmpegService.GetVideoInfoAsync(fileItem.FilePath);
                if (videoInfo != null)
                {
                    // 更新视频信息
                    fileItem.SourceFormat = videoInfo.Format;
                    fileItem.SourceResolution = videoInfo.Resolution;
                    fileItem.Duration = videoInfo.Duration;
                    fileItem.EstimatedFileSize = videoInfo.EstimatedSize;
                    fileItem.EstimatedDuration = videoInfo.EstimatedDuration;

                    // FFmpeg分析完成（移除日志）
                }
                else
                {
                    // FFmpeg分析失败，使用默认值
                    fileItem.SourceFormat = Path.GetExtension(fileItem.FilePath).TrimStart('.').ToUpper();
                    fileItem.SourceResolution = "未知";
                    fileItem.Duration = "未知";
                    fileItem.EstimatedFileSize = "预估中...";
                    fileItem.EstimatedDuration = "预估中...";

                    Utils.Logger.Warning("FileUploadViewModel", $"⚠️ FFmpeg分析失败，使用默认值: {fileItem.FileName}");
                }

                // 报告缩略图生成阶段
                progress?.Report(new FileProcessingProgress
                {
                    Progress = 70,
                    Status = "生成缩略图...",
                    Phase = "thumbnail_generation",
                    CurrentOperation = "生成视频预览缩略图"
                });

                // 检查取消
                cancellationToken.ThrowIfCancellationRequested();

                // 生成缩略图 - 与Client项目一致的实现
                try
                {
                    fileItem.StatusText = "生成缩略图...";

                    // 使用64x64尺寸，与UI显示一致
                    var thumbnail = await _thumbnailService.GetThumbnailAsync(fileItem.FilePath, 64, 64);
                    if (thumbnail != null)
                    {
                        fileItem.Thumbnail = thumbnail;
                        fileItem.HasThumbnail = true;
                        Utils.Logger.Info("FileUploadViewModel", $"✅ 缩略图生成完成: {fileItem.FileName} (64x64)");
                    }
                    else
                    {
                        fileItem.HasThumbnail = false;
                        Utils.Logger.Warning("FileUploadViewModel", $"⚠️ 缩略图生成失败: {fileItem.FileName}");
                    }
                }
                catch (Exception ex)
                {
                    fileItem.HasThumbnail = false;
                    Utils.Logger.Warning("FileUploadViewModel", $"⚠️ 缩略图生成异常: {fileItem.FileName} - {ex.Message}");
                }

                // 计算目标转换信息 - 与Client项目一致
                try
                {
                    fileItem.StatusText = "计算预估数据...";
                    var conversionSettings = ConversionSettingsService.Instance.CurrentSettings;

                    // 设置目标格式和分辨率
                    fileItem.TargetFormat = conversionSettings.OutputFormat.ToUpper();
                    fileItem.TargetResolution = conversionSettings.Resolution;

                    // 计算预估文件大小
                    var originalSizeBytes = GetFileSizeInBytes(fileItem.FileSize);
                    var estimatedSizeBytes = EstimateConvertedFileSize(originalSizeBytes, conversionSettings.ToConversionParameters());
                    fileItem.EstimatedFileSize = FormatFileSize(estimatedSizeBytes);

                    // 预估时长（通常与原时长相同）
                    fileItem.EstimatedDuration = fileItem.Duration;

                    // 预估数据计算完成（移除日志）
                }
                catch (Exception ex)
                {
                    Utils.Logger.Warning("FileUploadViewModel", $"⚠️ 预估数据计算失败: {fileItem.FileName} - {ex.Message}");

                    // 设置默认值
                    var conversionSettings = ConversionSettingsService.Instance.CurrentSettings;
                    fileItem.TargetFormat = conversionSettings.OutputFormat.ToUpper();
                    fileItem.TargetResolution = conversionSettings.Resolution;
                    fileItem.EstimatedFileSize = "预估中...";
                    fileItem.EstimatedDuration = fileItem.Duration;
                }

                // 报告最终完成
                progress?.Report(new FileProcessingProgress
                {
                    Progress = 95,
                    Status = "完成分析",
                    Phase = "finalizing",
                    CurrentOperation = "更新文件信息"
                });

                fileItem.StatusText = "等待处理";
                // 视频分析完成（移除日志）
            }
            catch (OperationCanceledException)
            {
                fileItem.StatusText = "分析已取消";
                // 视频分析取消（移除日志）
                throw;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileUploadViewModel", $"❌ 分析视频文件失败: {fileItem.FileName} - {ex.Message}");

                // 设置默认值
                fileItem.SourceFormat = Path.GetExtension(fileItem.FilePath).TrimStart('.').ToUpper();
                fileItem.SourceResolution = "未知";
                fileItem.Duration = "未知";
                fileItem.StatusText = "分析失败";

                progress?.Report(new FileProcessingProgress
                {
                    Progress = 0,
                    Status = $"分析失败: {ex.Message}",
                    Phase = "failed"
                });
            }
        }

        /// <summary>
        /// 验证是否为有效的视频文件
        /// </summary>
        private async Task<bool> IsValidVideoFileAsync(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                    return false;

                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                var supportedExtensions = new[]
                {
                    ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".m4v",
                    ".3gp", ".3g2", ".asf", ".divx", ".f4v", ".m2ts", ".mts", ".ts",
                    ".vob", ".rm", ".rmvb", ".ogv", ".dv", ".amv", ".mpg", ".mpeg",
                    ".m1v", ".m2v", ".m4p", ".m4b", ".qt", ".yuv", ".mxf", ".roq",
                    ".nsv", ".f4p", ".f4a", ".f4b"
                };

                if (!supportedExtensions.Any(ext => ext == extension))
                {
                    Utils.Logger.Info("FileUploadViewModel", $"❌ 不支持的文件格式: {extension}");
                    return false;
                }

                // 检查文件大小（避免过大的文件）
                var fileInfo = new System.IO.FileInfo(filePath);
                if (fileInfo.Length > 10L * 1024 * 1024 * 1024) // 10GB限制
                {
                    Utils.Logger.Warning("FileUploadViewModel", $"⚠️ 文件过大: {fileInfo.Length / (1024 * 1024)} MB");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileUploadViewModel", $"❌ 验证文件失败: {Path.GetFileName(filePath)} - {ex.Message}");
                return false;
            }
        }

        #endregion

        #region 设置相关命令

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

        #region 状态管理和UI更新方法

        /// <summary>
        /// 更新视图状态
        /// </summary>
        private void UpdateViewState()
        {
            CanStartConversion = _hasFiles && !_isConverting && FileItems.Any(f => f.Status == FileItemStatus.Pending);
            CanSelectFiles = !_isConverting && !IsProcessing;
        }

        /// <summary>
        /// 更新UI状态
        /// </summary>
        private void UpdateUI()
        {
            var hasFiles = FileItems.Count > 0;
            IsEmptyStateVisible = !hasFiles;
            IsFileListVisible = hasFiles;

            FileCountText = $"{FileItems.Count} 个文件";

            // 计算总大小（从字符串解析）
            var totalSizeBytes = 0L;
            foreach (var item in FileItems)
            {
                // 这里需要解析FileSize字符串，暂时使用简化逻辑
                totalSizeBytes += 1024 * 1024; // 假设每个文件1MB
            }
            TotalSizeText = FormatFileSize(totalSizeBytes);

            // 更新其他状态
            _hasFiles = hasFiles;
            UpdateViewState();
        }

        /// <summary>
        /// 格式化文件大小
        /// </summary>
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

        #region 拖拽支持方法

        /// <summary>
        /// 处理拖拽文件
        /// </summary>
        public async Task HandleDropAsync(string[] filePaths)
        {
            if (filePaths?.Length > 0)
            {
                await ProcessFilesAsync(filePaths);
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 更新转换进度（由SignalR调用）- 使用统一进度管理器
        /// </summary>
        public async void UpdateConversionProgress(string taskId, double progress, string status, double? fps = null, double? eta = null)
        {
            try
            {
                // 使用统一进度管理器处理进度更新
                await _progressManager.UpdateProgressAsync(
                    taskId,
                    progress,
                    "converting",
                    fps,
                    eta,
                    status
                );

                Utils.Logger.Debug("FileUploadViewModel", $"📊 统一进度管理器更新: {taskId} - {progress}%");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileUploadViewModel", $"❌ 更新转换进度失败: {ex.Message}");

                // 降级到直接更新
                try
                {
                    var fileItem = FileItems.FirstOrDefault(f => f.TaskId == taskId || f.LocalTaskId == taskId);
                    if (fileItem != null)
                    {
                        fileItem.UpdateProgress(progress, status, fps, eta);
                        Utils.Logger.Debug("FileUploadViewModel", $"📊 降级更新转换进度: {fileItem.FileName} - {progress}%");
                    }
                }
                catch (Exception fallbackEx)
                {
                    Utils.Logger.Error("FileUploadViewModel", $"❌ 降级更新也失败: {fallbackEx.Message}");
                }
            }
        }

        /// <summary>
        /// 标记文件转换完成 - 使用统一进度管理器
        /// </summary>
        public async void MarkFileCompleted(string taskId, bool success, string? message = null)
        {
            try
            {
                // 使用统一进度管理器处理完成状态
                await _progressManager.OnTaskCompletedAsync(taskId, success, message);

                // 文件转换完成（移除日志）

                // 更新整体状态
                UpdateViewState();
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileUploadViewModel", $"❌ 标记文件完成失败: {ex.Message}");

                // 降级到直接更新
                try
                {
                    var fileItem = FileItems.FirstOrDefault(f => f.TaskId == taskId || f.LocalTaskId == taskId);
                    if (fileItem != null)
                    {
                        fileItem.MarkCompleted(success, message);
                        UpdateViewState();
                    }
                }
                catch (Exception fallbackEx)
                {
                    Utils.Logger.Error("FileUploadViewModel", $"❌ 降级标记完成也失败: {fallbackEx.Message}");
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// 文件项视图模型 - 完整功能实现
    /// </summary>
    public partial class FileItemViewModel : ObservableObject
    {
        #region 基础属性

        [ObservableProperty]
        private string _fileName = string.Empty;

        [ObservableProperty]
        private string _filePath = string.Empty;

        [ObservableProperty]
        private string _sourceFormat = string.Empty;

        [ObservableProperty]
        private string _sourceResolution = "分析中...";

        [ObservableProperty]
        private string _fileSize = string.Empty;

        [ObservableProperty]
        private string _duration = "分析中...";

        [ObservableProperty]
        private string _targetFormat = "MP4";

        [ObservableProperty]
        private string _targetResolution = "1920×1080";

        [ObservableProperty]
        private string _estimatedFileSize = "预估中...";

        [ObservableProperty]
        private string _estimatedDuration = "预估中...";

        [ObservableProperty]
        private FileItemStatus _status = FileItemStatus.Pending;

        [ObservableProperty]
        private double _progress = 0;

        [ObservableProperty]
        private string _statusText = "等待处理";

        [ObservableProperty]
        private Avalonia.Media.Imaging.Bitmap? _thumbnail;

        [ObservableProperty]
        private bool _hasThumbnail = false;

        [ObservableProperty]
        private string? _taskId;

        [ObservableProperty]
        private string? _localTaskId;

        [ObservableProperty]
        private bool _isConverting = false;

        [ObservableProperty]
        private bool _canConvert = true;

        #endregion

        #region 计算属性

        /// <summary>
        /// 状态标签，用于XAML样式绑定
        /// </summary>
        public string StatusTag => Status.ToString();

        #endregion

        #region 转换控制方法

        /// <summary>
        /// 开始转换此文件 - 调用真实转换服务
        /// </summary>
        [RelayCommand]
        public async Task StartConversionAsync()
        {
            if (IsConverting || !CanConvert)
                return;

            try
            {
                IsConverting = true;
                CanConvert = false;
                Status = FileItemStatus.Uploading;
                StatusText = "准备转换...";
                Progress = 0;

                Utils.Logger.Info("FileItemViewModel", $"🚀 开始单个文件转换: {FileName}");

                // 🔑 触发单个文件转换请求事件
                // 让父级FileUploadViewModel处理实际的转换逻辑
                ConversionRequested?.Invoke(this);

                // 模拟转换启动过程
                await Task.Delay(500);

                // 更新状态
                Status = FileItemStatus.Converting;
                StatusText = "转换已启动";

                Utils.Logger.Info("FileItemViewModel", $"✅ 单个文件转换请求已发送: {FileName}");
            }
            catch (Exception ex)
            {
                Status = FileItemStatus.Failed;
                StatusText = $"转换失败: {ex.Message}";
                Progress = 0;
                Utils.Logger.Error("FileItemViewModel", $"❌ 单个文件转换失败: {FileName} - {ex.Message}");
            }
            finally
            {
                IsConverting = false;
                CanConvert = Status == FileItemStatus.Failed || Status == FileItemStatus.Cancelled;
            }
        }

        /// <summary>
        /// 取消转换
        /// </summary>
        [RelayCommand]
        public void CancelConversion()
        {
            Status = FileItemStatus.Cancelled;
            StatusText = "已取消";
            IsConverting = false;
            CanConvert = true;
            // 转换取消（移除日志）
        }

        /// <summary>
        /// 重试转换
        /// </summary>
        [RelayCommand]
        public async Task RetryConversionAsync()
        {
            if (Status == FileItemStatus.Failed || Status == FileItemStatus.Cancelled)
            {
                // 重置状态
                Status = FileItemStatus.Pending;
                StatusText = "等待处理";
                Progress = 0;
                TaskId = null;
                CanConvert = true;

                // 重新开始转换
                await StartConversionAsync();
                Utils.Logger.Info("FileItemViewModel", $"🔄 重试转换: {FileName}");
            }
        }

        /// <summary>
        /// 移除文件命令
        /// </summary>
        [RelayCommand]
        public void Remove()
        {
            // 这个命令会被父级FileUploadViewModel处理
            // 通过事件或回调机制通知父级移除此文件项
            Utils.Logger.Info("FileItemViewModel", $"🗑️ 请求移除文件: {FileName}");

            // 触发移除事件
            RemoveRequested?.Invoke(this);
        }

        /// <summary>
        /// 移除请求事件
        /// </summary>
        public event Action<FileItemViewModel>? RemoveRequested;

        /// <summary>
        /// 转换请求事件
        /// </summary>
        public event Action<FileItemViewModel>? ConversionRequested;

        /// <summary>
        /// 获取文件大小（字节）
        /// </summary>
        private long GetFileSizeInBytes()
        {
            try
            {
                if (string.IsNullOrEmpty(FileSize)) return 0;

                var sizeText = FileSize.Replace(" ", "").ToUpper();

                if (sizeText.EndsWith("GB"))
                {
                    if (double.TryParse(sizeText.Replace("GB", ""), out var gb))
                        return (long)(gb * 1024 * 1024 * 1024);
                }
                else if (sizeText.EndsWith("MB"))
                {
                    if (double.TryParse(sizeText.Replace("MB", ""), out var mb))
                        return (long)(mb * 1024 * 1024);
                }
                else if (sizeText.EndsWith("KB"))
                {
                    if (double.TryParse(sizeText.Replace("KB", ""), out var kb))
                        return (long)(kb * 1024);
                }

                return 0;
            }
            catch
            {
                return 0;
            }
        }



        /// <summary>
        /// 更新转换进度（由SignalR调用）- 统一进度管理版本
        /// </summary>
        public void UpdateProgress(double progress, string status, double? fps = null, double? eta = null)
        {
            try
            {
                // 确保在UI线程中执行
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    // 🔑 使用统一的进度更新逻辑
                    UpdateProgressInternal(progress, status, "converting", fps, eta);
                });
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileItemViewModel", $"❌ 更新进度失败: {FileName} - {ex.Message}");
            }
        }

        /// <summary>
        /// 内部统一进度更新方法 - 防止进度冲突
        /// </summary>
        private void UpdateProgressInternal(
            double progress,
            string status,
            string phase = "",
            double? speed = null,
            double? eta = null)
        {
            try
            {
                // 确保进度值在有效范围内
                var safeProgress = Math.Max(0, Math.Min(100, progress));

                // 根据阶段更新状态
                switch (phase.ToLower())
                {
                    case "analyzing":
                        Status = FileItemStatus.Pending;
                        StatusText = $"分析中... {safeProgress:F0}%";
                        break;

                    case "uploading":
                        Status = FileItemStatus.Uploading;
                        StatusText = $"上传中... {safeProgress:F0}%";
                        break;

                    case "converting":
                        Status = FileItemStatus.Converting;
                        IsConverting = true;
                        StatusText = speed.HasValue ?
                            $"转换中... {safeProgress:F0}% (速度: {speed:F1}x)" :
                            $"转换中... {safeProgress:F0}%";
                        break;

                    default:
                        StatusText = status;
                        break;
                }

                // 更新进度
                Progress = safeProgress;

                // 处理完成状态
                if (safeProgress >= 100 && phase == "converting")
                {
                    Status = FileItemStatus.Completed;
                    StatusText = "转换完成";
                    IsConverting = false;
                    CanConvert = false;
                    Progress = 100; // 确保进度条显示完整
                }

                Utils.Logger.Debug("FileItemViewModel",
                    $"✅ 进度已更新: {FileName} - {safeProgress:F1}% - {StatusText}");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileItemViewModel",
                    $"❌ 内部进度更新失败: {FileName} - {ex.Message}");
            }
        }

        /// <summary>
        /// 标记转换完成
        /// </summary>
        public void MarkCompleted(bool success, string? message = null)
        {
            IsConverting = false;

            if (success)
            {
                Status = FileItemStatus.Completed;
                StatusText = "转换完成";
                Progress = 100;
                CanConvert = false;
            }
            else
            {
                Status = FileItemStatus.Failed;
                StatusText = message ?? "转换失败";
                CanConvert = true;
            }
        }

        #endregion
    }

    /// <summary>
    /// 文件项状态枚举
    /// </summary>
    public enum FileItemStatus
    {
        /// <summary>
        /// 等待处理
        /// </summary>
        Pending,

        /// <summary>
        /// 上传中
        /// </summary>
        Uploading,

        /// <summary>
        /// 上传完成
        /// </summary>
        UploadCompleted,

        /// <summary>
        /// 转换中
        /// </summary>
        Converting,

        /// <summary>
        /// 转换完成
        /// </summary>
        Completed,

        /// <summary>
        /// 转换失败
        /// </summary>
        Failed,

        /// <summary>
        /// 已取消
        /// </summary>
        Cancelled
    }
}
