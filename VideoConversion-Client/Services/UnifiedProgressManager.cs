using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Threading;
using VideoConversion_Client.Models;
using VideoConversion_Client.ViewModels;

namespace VideoConversion_Client.Services
{
    /// <summary>
    /// 统一进度更新系统 - 解决标识符断层问题
    /// </summary>
    public class UnifiedProgressManager
    {
        private readonly ObservableCollection<FileItemViewModel> _fileItems;
        private readonly DatabaseService _dbService;

        public UnifiedProgressManager(ObservableCollection<FileItemViewModel> fileItems)
        {
            _fileItems = fileItems;
            _dbService = DatabaseService.Instance;
        }

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
                        Utils.Logger.Info("Progress", $"✅ 统一进度更新: {fileItem.FileName}");
                        Utils.Logger.Info("Progress", $"   标识符: {identifier}");
                        Utils.Logger.Info("Progress", $"   阶段: {phase}");
                        Utils.Logger.Info("Progress", $"   进度: {safeProgress:F1}%");
                        if (speed.HasValue) Utils.Logger.Info("Progress", $"   速度: {speed.Value:F2}x");
                        if (eta.HasValue) Utils.Logger.Info("Progress", $"   预计剩余: {eta.Value:F0}秒");
                        
                        // 更新FileItemViewModel
                        await UpdateFileItemViewModel(fileItem, safeProgress, phase, speed, eta, message);
                        
                        // 同步更新本地数据库
                        await UpdateLocalDatabase(fileItem, safeProgress, phase, speed, eta, message);
                    }
                    else
                    {
                        Utils.Logger.Warning("Progress", $"⚠️ 未找到对应的文件项: {identifier}");
                        Utils.Logger.Warning("Progress", $"   当前FileItems数量: {_fileItems.Count}");
                        Utils.Logger.Warning("Progress", $"   查找阶段: {phase}");
                    }
                });
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("Progress", $"❌ 统一进度更新失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 智能查找文件项 - 支持多种标识符类型
        /// </summary>
        private FileItemViewModel? FindFileItemIntelligently(string identifier)
        {
            // 🔑 优先级查找策略
            
            // 1. 优先使用TaskId查找（服务器TaskId）
            var fileItem = _fileItems.FirstOrDefault(f => f.TaskId == identifier);
            if (fileItem != null)
            {
                Utils.Logger.Debug("Progress", $"🎯 通过TaskId找到文件项: {identifier}");
                return fileItem;
            }
            
            // 2. 使用LocalTaskId查找
            fileItem = _fileItems.FirstOrDefault(f => f.LocalTaskId == identifier);
            if (fileItem != null)
            {
                Utils.Logger.Debug("Progress", $"🎯 通过LocalTaskId找到文件项: {identifier}");
                return fileItem;
            }
            
            // 3. 使用文件名查找（兼容上传阶段）
            fileItem = _fileItems.FirstOrDefault(f => Path.GetFileName(f.FilePath) == identifier);
            if (fileItem != null)
            {
                Utils.Logger.Debug("Progress", $"🎯 通过文件名找到文件项: {identifier}");
                return fileItem;
            }
            
            // 4. 使用文件路径查找
            fileItem = _fileItems.FirstOrDefault(f => f.FilePath == identifier);
            if (fileItem != null)
            {
                Utils.Logger.Debug("Progress", $"🎯 通过文件路径找到文件项: {identifier}");
                return fileItem;
            }
            
            return null;
        }

        /// <summary>
        /// 更新FileItemViewModel的状态和进度
        /// </summary>
        private async Task UpdateFileItemViewModel(FileItemViewModel fileItem, double progress, string phase,
            double? speed, double? eta, string? message)
        {
            // 更新基本进度
            fileItem.Progress = progress;
            
            // 🔑 根据阶段更新状态 - 完整的生命周期管理
            switch (phase.ToLower())
            {
                case "pending":
                    fileItem.Status = FileItemStatus.Pending;
                    fileItem.StatusText = message ?? "等待处理";
                    break;
                    
                case "uploading":
                    fileItem.Status = FileItemStatus.Uploading;
                    fileItem.StatusText = message ?? $"上传中... {progress:F1}%";
                    break;
                    
                case "upload_completed":
                    fileItem.Status = FileItemStatus.UploadCompleted;
                    fileItem.StatusText = message ?? "上传完成，等待转换...";
                    break;
                    
                case "converting":
                    fileItem.Status = FileItemStatus.Converting;
                    var speedText = speed.HasValue ? $" ({speed.Value:F1}x)" : "";
                    var etaText = eta.HasValue ? $" 剩余{eta.Value:F0}秒" : "";
                    fileItem.StatusText = message ?? $"转换中... {progress:F1}%{speedText}{etaText}";
                    break;
                    
                case "completed":
                    fileItem.Status = FileItemStatus.Completed;
                    fileItem.StatusText = message ?? "转换完成";
                    fileItem.Progress = 100;
                    break;
                    
                case "failed":
                    fileItem.Status = FileItemStatus.Failed;
                    fileItem.StatusText = message ?? "处理失败";
                    break;
                    
                case "cancelled":
                    fileItem.Status = FileItemStatus.Cancelled;
                    fileItem.StatusText = message ?? "已取消";
                    break;
                    
                default:
                    fileItem.StatusText = message ?? $"{phase}... {progress:F1}%";
                    break;
            }
            
            Utils.Logger.Debug("Progress", $"📱 UI更新完成: {fileItem.FileName} -> {fileItem.StatusText}");
        }

        /// <summary>
        /// 同步更新本地数据库
        /// </summary>
        private async Task UpdateLocalDatabase(FileItemViewModel fileItem, double progress, string phase,
            double? speed, double? eta, string? message)
        {
            try
            {
                var taskId = !string.IsNullOrEmpty(fileItem.TaskId) ? fileItem.TaskId : fileItem.LocalTaskId;
                if (string.IsNullOrEmpty(taskId)) return;
                
                // 构建进度历史记录
                var progressRecord = new
                {
                    Timestamp = DateTime.Now,
                    Phase = phase,
                    Progress = progress,
                    Speed = speed,
                    ETA = eta,
                    Message = message
                };
                
                // 更新数据库
                await _dbService.UpdateTaskProgressAsync(taskId, progress, phase, speed, eta, 
                    JsonSerializer.Serialize(progressRecord));
                
                Utils.Logger.Debug("Progress", $"💾 数据库更新完成: {taskId}");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("Progress", $"❌ 更新本地数据库失败: {ex.Message}");
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
                Utils.Logger.Info("Progress", $"📊 批量进度更新: {completedFiles}/{totalFiles} 文件完成, 总进度: {overallProgress:F1}%");
                
                if (!string.IsNullOrEmpty(currentFile))
                {
                    await UpdateProgressAsync(currentFile, currentFileProgress, "uploading", 
                        message: $"上传中... {currentFileProgress:F1}% ({completedFiles}/{totalFiles})");
                }
                
                // 可以在这里添加批量进度的UI更新逻辑
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("Progress", $"❌ 批量进度更新失败: {ex.Message}");
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
                    Utils.Logger.Info("Task", $"🎉 任务完成，准备下载: {taskId}");
                    // 这里为后续的下载功能预留接口
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("Progress", $"❌ 任务完成处理失败: {ex.Message}");
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
    }
}
