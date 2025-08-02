using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using VideoConversion_ClientTo.Presentation.ViewModels;
using VideoConversion_ClientTo.Infrastructure;
using VideoConversion_ClientTo.Views;

namespace VideoConversion_ClientTo.Infrastructure.Services
{
    /// <summary>
    /// 统一进度管理器 - 融合Client和ClientTo的完整实现
    /// </summary>
    public class UnifiedProgressManager
    {
        #region 私有字段

        private readonly ObservableCollection<FileItemViewModel> _fileItems;
        private readonly Dictionary<string, string> _taskIdMappings; // LocalTaskId -> ServerTaskId
        private readonly Dictionary<string, FileProcessingContext> _processingContexts;

        // 进度窗口相关
        private PreprocessProgressWindow? _progressWindow;
        private Window? _parentWindow;
        private CancellationTokenSource? _cancellationTokenSource;

        // 批量处理状态
        private bool _isBatchProcessing = false;
        private int _totalFilesInBatch = 0;
        private int _completedFilesInBatch = 0;
        private DateTime _batchStartTime;

        #endregion

        #region 事件

        /// <summary>
        /// 批量处理开始事件
        /// </summary>
        public event EventHandler<BatchProcessingEventArgs>? BatchProcessingStarted;

        /// <summary>
        /// 批量处理完成事件
        /// </summary>
        public event EventHandler<BatchProcessingEventArgs>? BatchProcessingCompleted;

        /// <summary>
        /// 文件处理进度更新事件
        /// </summary>
        public event EventHandler<FileProcessingProgressEventArgs>? FileProcessingProgress;

        /// <summary>
        /// 单个文件处理完成事件
        /// </summary>
        public event EventHandler<FileProcessingCompletedEventArgs>? FileProcessingCompleted;

        #endregion

        #region 构造函数

        public UnifiedProgressManager(ObservableCollection<FileItemViewModel> fileItems, Window? parentWindow = null)
        {
            _fileItems = fileItems ?? throw new ArgumentNullException(nameof(fileItems));
            _taskIdMappings = new Dictionary<string, string>();
            _processingContexts = new Dictionary<string, FileProcessingContext>();
            _parentWindow = parentWindow;

            Utils.Logger.Info("UnifiedProgressManager", "✅ 增强版统一进度管理器已初始化");
        }

        #endregion

        #region 批量处理方法

        /// <summary>
        /// 开始批量文件处理 - 融合Client的进度窗口功能
        /// </summary>
        public async Task<BatchProcessingResult> StartBatchProcessingAsync(
            IEnumerable<string> filePaths,
            Func<string, IProgress<FileProcessingProgress>, CancellationToken, Task<FileItemViewModel?>> processFileFunc,
            bool showProgressWindow = true,
            string windowTitle = "处理文件")
        {
            try
            {
                var filePathList = filePaths.ToList();
                _totalFilesInBatch = filePathList.Count;
                _completedFilesInBatch = 0;
                _batchStartTime = DateTime.Now;
                _isBatchProcessing = true;

                Utils.Logger.Info("UnifiedProgressManager", $"🚀 开始批量处理 {_totalFilesInBatch} 个文件");

                // 创建取消令牌
                _cancellationTokenSource = new CancellationTokenSource();

                // 显示进度窗口
                if (showProgressWindow)
                {
                    await ShowProgressWindowAsync(filePathList, windowTitle);
                }

                // 触发批量处理开始事件
                BatchProcessingStarted?.Invoke(this, new BatchProcessingEventArgs
                {
                    TotalFiles = _totalFilesInBatch,
                    StartTime = _batchStartTime
                });

                var result = new BatchProcessingResult
                {
                    TotalFiles = _totalFilesInBatch,
                    StartTime = _batchStartTime
                };

                // 逐个处理文件
                foreach (var filePath in filePathList)
                {
                    if (_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        result.WasCancelled = true;
                        break;
                    }

                    try
                    {
                        // 创建进度报告器
                        var progress = new Progress<FileProcessingProgress>(p =>
                            OnFileProcessingProgress(filePath, p));

                        // 处理单个文件
                        var fileItem = await processFileFunc(filePath, progress, _cancellationTokenSource.Token);

                        if (fileItem != null)
                        {
                            // 添加到UI集合
                            await Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                _fileItems.Add(fileItem);
                            });

                            result.ProcessedFiles.Add(filePath);
                            result.SuccessfulFiles++;
                        }
                        else
                        {
                            result.FailedFiles.Add(filePath);
                        }

                        _completedFilesInBatch++;

                        // 更新进度窗口
                        UpdateProgressWindow(filePath, true);

                        // 触发单个文件完成事件
                        FileProcessingCompleted?.Invoke(this, new FileProcessingCompletedEventArgs
                        {
                            FilePath = filePath,
                            Success = fileItem != null,
                            CompletedFiles = _completedFilesInBatch,
                            TotalFiles = _totalFilesInBatch
                        });
                    }
                    catch (Exception ex)
                    {
                        Utils.Logger.Error("UnifiedProgressManager", $"❌ 处理文件失败: {filePath} - {ex.Message}");
                        result.FailedFiles.Add(filePath);
                        _completedFilesInBatch++;

                        // 更新进度窗口
                        UpdateProgressWindow(filePath, false, ex.Message);
                    }
                }

                result.EndTime = DateTime.Now;
                result.Duration = result.EndTime - result.StartTime;
                result.IsCompleted = true;

                // 等待一段时间让用户看到完成状态
                if (showProgressWindow && _progressWindow != null && !result.WasCancelled)
                {
                    await Task.Delay(1500);
                }

                // 关闭进度窗口
                await CloseProgressWindowAsync();

                // 触发批量处理完成事件
                BatchProcessingCompleted?.Invoke(this, new BatchProcessingEventArgs
                {
                    TotalFiles = _totalFilesInBatch,
                    CompletedFiles = _completedFilesInBatch,
                    StartTime = _batchStartTime,
                    EndTime = result.EndTime,
                    Duration = result.Duration,
                    WasCancelled = result.WasCancelled
                });

                Utils.Logger.Info("UnifiedProgressManager",
                    $"🎉 批量处理完成 - 成功: {result.SuccessfulFiles}, 失败: {result.FailedFiles.Count}, 耗时: {result.Duration.TotalSeconds:F1}秒");

                return result;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("UnifiedProgressManager", $"❌ 批量处理失败: {ex.Message}");
                await CloseProgressWindowAsync();

                return new BatchProcessingResult
                {
                    TotalFiles = _totalFilesInBatch,
                    StartTime = _batchStartTime,
                    EndTime = DateTime.Now,
                    IsCompleted = false,
                    ErrorMessage = ex.Message
                };
            }
            finally
            {
                _isBatchProcessing = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        #endregion

        #region 进度窗口管理

        /// <summary>
        /// 显示进度窗口
        /// </summary>
        private async Task ShowProgressWindowAsync(List<string> filePaths, string title)
        {
            try
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _progressWindow = new PreprocessProgressWindow();
                    _progressWindow.Title = title;
                    _progressWindow.InitializeProgress(filePaths);

                    if (_parentWindow != null)
                    {
                        _progressWindow.Show(_parentWindow);
                    }
                    else
                    {
                        _progressWindow.Show();
                    }
                });

                Utils.Logger.Info("UnifiedProgressManager", "✅ 进度窗口已显示");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("UnifiedProgressManager", $"❌ 显示进度窗口失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新进度窗口
        /// </summary>
        private void UpdateProgressWindow(string filePath, bool success, string? errorMessage = null)
        {
            try
            {
                if (_progressWindow != null)
                {
                    var status = success ? "处理完成" : $"处理失败: {errorMessage}";
                    _progressWindow.UpdateFileStatus(filePath, status);
                    _progressWindow.MarkFileCompleted(filePath, success);
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("UnifiedProgressManager", $"❌ 更新进度窗口失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 关闭进度窗口
        /// </summary>
        private async Task CloseProgressWindowAsync()
        {
            try
            {
                if (_progressWindow != null)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        _progressWindow.Close();
                        _progressWindow = null;
                    });
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("UnifiedProgressManager", $"❌ 关闭进度窗口失败: {ex.Message}");
            }
        }

        #endregion

        #region 文件处理进度回调

        /// <summary>
        /// 文件处理进度回调
        /// </summary>
        private void OnFileProcessingProgress(string filePath, FileProcessingProgress progress)
        {
            try
            {
                // 更新进度窗口
                if (_progressWindow != null)
                {
                    _progressWindow.UpdateFileStatus(filePath, progress.Status, progress.Progress);
                }

                // 触发进度事件
                FileProcessingProgress?.Invoke(this, new FileProcessingProgressEventArgs
                {
                    FilePath = filePath,
                    Progress = progress.Progress,
                    Status = progress.Status,
                    Phase = progress.Phase,
                    CompletedFiles = _completedFilesInBatch,
                    TotalFiles = _totalFilesInBatch
                });

                Utils.Logger.Debug("UnifiedProgressManager",
                    $"📊 文件处理进度: {System.IO.Path.GetFileName(filePath)} - {progress.Status} - {progress.Progress:F1}%");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("UnifiedProgressManager", $"❌ 处理文件进度回调失败: {ex.Message}");
            }
        }

        #endregion

        #region 任务进度注册

        /// <summary>
        /// 注册任务进度回调
        /// </summary>
        public void RegisterTaskProgress(string taskId, Action<double, string, double?, double?> progressCallback)
        {
            try
            {
                // 这里可以存储进度回调，用于SignalR进度更新
                // 暂时直接调用，后续可以扩展为字典存储
                Utils.Logger.Debug("UnifiedProgressManager", $"📋 已注册任务进度回调: {taskId}");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("UnifiedProgressManager", $"❌ 注册任务进度回调失败: {taskId} - {ex.Message}");
            }
        }

        #endregion

        /// <summary>
        /// 统一进度更新方法 - 支持所有阶段的进度跟踪
        /// </summary>
        public async Task UpdateProgressAsync(string identifier, double progress, string phase, 
            double? speed = null, double? eta = null, string? message = null)
        {
            try
            {
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    var safeProgress = Math.Max(0, Math.Min(100, progress));
                    
                    // 🔑 智能查找文件项 - 解决标识符不一致问题
                    var fileItem = FindFileItemIntelligently(identifier);
                    
                    if (fileItem != null)
                    {
                        // 更新进度和状态
                        fileItem.Progress = safeProgress;
                        
                        // 根据阶段更新状态
                        switch (phase.ToLower())
                        {
                            case "uploading":
                                fileItem.Status = FileItemStatus.Uploading;
                                fileItem.StatusText = message ?? $"上传中... {safeProgress:F1}%";
                                break;
                                
                            case "converting":
                                fileItem.Status = FileItemStatus.Converting;
                                fileItem.StatusText = message ?? $"转换中... {safeProgress:F1}%";
                                break;
                                
                            case "completed":
                                fileItem.Status = FileItemStatus.Completed;
                                fileItem.StatusText = message ?? "转换完成";
                                fileItem.Progress = 100;
                                fileItem.IsConverting = false;
                                fileItem.CanConvert = false;
                                break;
                                
                            case "failed":
                                fileItem.Status = FileItemStatus.Failed;
                                fileItem.StatusText = message ?? "转换失败";
                                fileItem.IsConverting = false;
                                fileItem.CanConvert = true;
                                break;
                                
                            case "cancelled":
                                fileItem.Status = FileItemStatus.Cancelled;
                                fileItem.StatusText = message ?? "已取消";
                                fileItem.IsConverting = false;
                                fileItem.CanConvert = true;
                                break;
                                
                            default:
                                fileItem.StatusText = message ?? $"处理中... {safeProgress:F1}%";
                                break;
                        }

                        // 构建详细的状态信息
                        var statusDetails = new List<string>();
                        if (speed.HasValue && speed.Value > 0)
                        {
                            statusDetails.Add($"速度: {speed.Value:F2}x");
                        }
                        if (eta.HasValue && eta.Value > 0)
                        {
                            var etaTime = TimeSpan.FromSeconds(eta.Value);
                            statusDetails.Add($"剩余: {etaTime:mm\\:ss}");
                        }

                        if (statusDetails.Count > 0)
                        {
                            fileItem.StatusText += $" ({string.Join(", ", statusDetails)})";
                        }

                        Utils.Logger.Debug("UnifiedProgressManager", 
                            $"📊 进度更新: {fileItem.FileName} - {phase} - {safeProgress:F1}%");
                    }
                    else
                    {
                        Utils.Logger.Warning("UnifiedProgressManager", 
                            $"⚠️ 未找到对应的文件项: {identifier}");
                    }
                });
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("UnifiedProgressManager", $"❌ 进度更新失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 智能查找文件项 - 解决标识符不一致问题
        /// </summary>
        private FileItemViewModel? FindFileItemIntelligently(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
                return null;

            try
            {
                // 1. 直接通过TaskId查找
                var fileItem = _fileItems.FirstOrDefault(f => f.TaskId == identifier);
                if (fileItem != null)
                {
                    return fileItem;
                }

                // 2. 通过LocalTaskId查找
                fileItem = _fileItems.FirstOrDefault(f => f.LocalTaskId == identifier);
                if (fileItem != null)
                {
                    return fileItem;
                }

                // 3. 通过TaskId映射查找
                if (_taskIdMappings.ContainsKey(identifier))
                {
                    var mappedTaskId = _taskIdMappings[identifier];
                    fileItem = _fileItems.FirstOrDefault(f => f.TaskId == mappedTaskId || f.LocalTaskId == mappedTaskId);
                    if (fileItem != null)
                    {
                        return fileItem;
                    }
                }

                // 4. 通过文件名查找（最后的降级方案）
                fileItem = _fileItems.FirstOrDefault(f => f.FileName == identifier);
                if (fileItem != null)
                {
                    Utils.Logger.Warning("UnifiedProgressManager", 
                        $"⚠️ 通过文件名查找到文件项: {identifier}");
                    return fileItem;
                }

                return null;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("UnifiedProgressManager", $"❌ 智能查找文件项失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 添加TaskId映射
        /// </summary>
        public void AddTaskIdMapping(string localTaskId, string serverTaskId)
        {
            try
            {
                if (!string.IsNullOrEmpty(localTaskId) && !string.IsNullOrEmpty(serverTaskId))
                {
                    _taskIdMappings[localTaskId] = serverTaskId;
                    Utils.Logger.Info("UnifiedProgressManager", 
                        $"🔗 添加TaskId映射: {localTaskId} -> {serverTaskId}");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("UnifiedProgressManager", $"❌ 添加TaskId映射失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 移除TaskId映射
        /// </summary>
        public void RemoveTaskIdMapping(string localTaskId)
        {
            try
            {
                if (_taskIdMappings.ContainsKey(localTaskId))
                {
                    _taskIdMappings.Remove(localTaskId);
                    Utils.Logger.Info("UnifiedProgressManager", $"🗑️ 移除TaskId映射: {localTaskId}");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("UnifiedProgressManager", $"❌ 移除TaskId映射失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 批量更新进度
        /// </summary>
        public async Task UpdateBatchProgressAsync(string batchIdentifier, double overallProgress, 
            int completedFiles, int totalFiles, string? currentFile = null, double currentFileProgress = 0)
        {
            try
            {
                Utils.Logger.Info("UnifiedProgressManager", 
                    $"📊 批量进度更新: {completedFiles}/{totalFiles} 文件完成, 总进度: {overallProgress:F1}%");
                
                if (!string.IsNullOrEmpty(currentFile))
                {
                    await UpdateProgressAsync(currentFile, currentFileProgress, "uploading", 
                        message: $"上传中... {currentFileProgress:F1}% ({completedFiles}/{totalFiles})");
                }
                
                // 可以在这里添加批量进度的UI更新逻辑
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("UnifiedProgressManager", $"❌ 批量进度更新失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 任务完成处理
        /// </summary>
        public async Task OnTaskCompletedAsync(string taskId, bool success, string? message = null)
        {
            try
            {
                // 使用统一进度管理器处理完成状态
                await UpdateProgressAsync(
                    taskId, 
                    success ? 100 : 0, 
                    success ? "completed" : "failed",
                    message: message ?? (success ? "转换完成" : "转换失败")
                );
                
                if (success)
                {
                    Utils.Logger.Info("UnifiedProgressManager", $"🎉 任务完成，准备下载: {taskId}");
                    // 这里为后续的下载功能预留接口
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("UnifiedProgressManager", $"❌ 任务完成处理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取当前活跃任务数量
        /// </summary>
        public int GetActiveTaskCount()
        {
            return _fileItems.Count(f => f.Status == FileItemStatus.Uploading || 
                                        f.Status == FileItemStatus.Converting);
        }

        /// <summary>
        /// 获取指定状态的任务数量
        /// </summary>
        public int GetTaskCountByStatus(FileItemStatus status)
        {
            return _fileItems.Count(f => f.Status == status);
        }

        /// <summary>
        /// 获取总体进度统计
        /// </summary>
        public ProgressStatistics GetProgressStatistics()
        {
            try
            {
                var total = _fileItems.Count;
                var completed = _fileItems.Count(f => f.Status == FileItemStatus.Completed);
                var failed = _fileItems.Count(f => f.Status == FileItemStatus.Failed);
                var active = GetActiveTaskCount();
                var pending = _fileItems.Count(f => f.Status == FileItemStatus.Pending);

                var overallProgress = total > 0 ? (double)completed / total * 100 : 0;

                return new ProgressStatistics
                {
                    TotalFiles = total,
                    CompletedFiles = completed,
                    FailedFiles = failed,
                    ActiveFiles = active,
                    PendingFiles = pending,
                    OverallProgress = overallProgress
                };
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("UnifiedProgressManager", $"❌ 获取进度统计失败: {ex.Message}");
                return new ProgressStatistics();
            }
        }

        /// <summary>
        /// 重置所有任务状态
        /// </summary>
        public async Task ResetAllTasksAsync()
        {
            try
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    foreach (var fileItem in _fileItems)
                    {
                        fileItem.Status = FileItemStatus.Pending;
                        fileItem.Progress = 0;
                        fileItem.StatusText = "等待处理";
                        fileItem.IsConverting = false;
                        fileItem.CanConvert = true;
                        fileItem.TaskId = null;
                    }
                });

                _taskIdMappings.Clear();
                Utils.Logger.Info("UnifiedProgressManager", "🔄 所有任务状态已重置");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("UnifiedProgressManager", $"❌ 重置任务状态失败: {ex.Message}");
            }
        }
    }

    #region 支持类和数据模型

    /// <summary>
    /// 进度统计信息
    /// </summary>
    public class ProgressStatistics
    {
        public int TotalFiles { get; set; }
        public int CompletedFiles { get; set; }
        public int FailedFiles { get; set; }
        public int ActiveFiles { get; set; }
        public int PendingFiles { get; set; }
        public double OverallProgress { get; set; }
        public TimeSpan? EstimatedTimeRemaining { get; set; }
        public double? AverageProcessingTime { get; set; }
    }

    /// <summary>
    /// 批量处理结果
    /// </summary>
    public class BatchProcessingResult
    {
        public int TotalFiles { get; set; }
        public int SuccessfulFiles { get; set; }
        public List<string> ProcessedFiles { get; set; } = new();
        public List<string> FailedFiles { get; set; } = new();
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public bool IsCompleted { get; set; }
        public bool WasCancelled { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// 文件处理上下文
    /// </summary>
    public class FileProcessingContext
    {
        public string FilePath { get; set; } = "";
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string CurrentPhase { get; set; } = "";
        public double Progress { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsSuccessful { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// 文件处理进度信息
    /// </summary>
    public class FileProcessingProgress
    {
        public double Progress { get; set; }
        public string Status { get; set; } = "";
        public string Phase { get; set; } = "";
        public string? CurrentOperation { get; set; }
        public TimeSpan? EstimatedTimeRemaining { get; set; }
    }

    /// <summary>
    /// 批量处理事件参数
    /// </summary>
    public class BatchProcessingEventArgs : EventArgs
    {
        public int TotalFiles { get; set; }
        public int CompletedFiles { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan? Duration { get; set; }
        public bool WasCancelled { get; set; }
    }

    /// <summary>
    /// 文件处理进度事件参数
    /// </summary>
    public class FileProcessingProgressEventArgs : EventArgs
    {
        public string FilePath { get; set; } = "";
        public double Progress { get; set; }
        public string Status { get; set; } = "";
        public string Phase { get; set; } = "";
        public int CompletedFiles { get; set; }
        public int TotalFiles { get; set; }
    }

    /// <summary>
    /// 文件处理完成事件参数
    /// </summary>
    public class FileProcessingCompletedEventArgs : EventArgs
    {
        public string FilePath { get; set; } = "";
        public bool Success { get; set; }
        public int CompletedFiles { get; set; }
        public int TotalFiles { get; set; }
        public string? ErrorMessage { get; set; }
    }

    #endregion
}
