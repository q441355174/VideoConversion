using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VideoConversion_ClientTo.Application.Interfaces;
using VideoConversion_ClientTo.Application.DTOs;
using VideoConversion_ClientTo.Domain.Entities;
using VideoConversion_ClientTo.Domain.ValueObjects;

namespace VideoConversion_ClientTo.Infrastructure.Services
{
    /// <summary>
    /// STEP-7: 简化的转换任务服务实现
    /// 职责: 转换任务的业务逻辑处理
    /// </summary>
    public class ConversionTaskService : IConversionTaskService
    {
        private readonly IApiClient _apiClient;
        private readonly List<ConversionTask> _localTasks = new();

        public ConversionTaskService(IApiClient apiClient)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            Utils.Logger.Info("ConversionTaskService", "✅ 转换任务服务已初始化");
        }

        #region 任务管理

        public async Task<ConversionTask> CreateTaskAsync(
            string taskName,
            string filePath,
            long fileSize,
            ConversionParameters parameters)
        {
            try
            {
                var task = ConversionTask.Create(taskName, filePath, fileSize, parameters);
                _localTasks.Add(task);

                // 任务创建完成（移除日志）
                return task;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionTaskService", $"❌ 创建任务失败: {ex.Message}");
                throw;
            }
        }

        public async Task<ConversionTask?> GetTaskAsync(TaskId taskId)
        {
            try
            {
                // 先从本地查找
                var localTask = _localTasks.FirstOrDefault(t => t.Id == taskId);
                if (localTask != null)
                {
                    return localTask;
                }

                // 从服务器获取
                var response = await _apiClient.GetTaskAsync(taskId.Value);
                if (response.Success && response.Data != null)
                {
                    // 这里可以添加DTO到Domain的映射
                    Utils.Logger.Info("ConversionTaskService", $"📥 从服务器获取任务: {taskId}");
                }

                return null;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionTaskService", $"❌ 获取任务失败 {taskId}: {ex.Message}");
                return null;
            }
        }

        public async Task<IEnumerable<ConversionTask>> GetAllTasksAsync()
        {
            try
            {
                return _localTasks.ToList();
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionTaskService", $"❌ 获取所有任务失败: {ex.Message}");
                return new List<ConversionTask>();
            }
        }

        public async Task<IEnumerable<ConversionTask>> GetCompletedTasksAsync(int page = 1, int pageSize = 50)
        {
            try
            {
                Utils.Logger.Info("ConversionTaskService", $"📋 获取已完成任务: 第{page}页, 每页{pageSize}条");

                var completedTasks = _localTasks.Where(t => t.IsCompleted).ToList();
                Utils.Logger.Info("ConversionTaskService", $"✅ 本地已完成任务: {completedTasks.Count} 个");

                return completedTasks;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionTaskService", $"❌ 获取已完成任务失败: {ex.Message}");
                return new List<ConversionTask>();
            }
        }

        public async Task<IEnumerable<ConversionTask>> GetActiveTasksAsync()
        {
            try
            {
                return _localTasks.Where(t => t.IsInProgress).ToList();
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionTaskService", $"❌ 获取活动任务失败: {ex.Message}");
                return new List<ConversionTask>();
            }
        }

        public async Task<bool> DeleteTaskAsync(TaskId taskId)
        {
            try
            {
                var task = _localTasks.FirstOrDefault(t => t.Id == taskId);
                if (task != null)
                {
                    _localTasks.Remove(task);
                    Utils.Logger.Info("ConversionTaskService", $"🗑️ 任务已删除: {taskId}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionTaskService", $"❌ 删除任务失败: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region 任务操作

        public async Task<bool> StartConversionAsync(TaskId taskId, StartConversionRequestDto request)
        {
            try
            {
                var task = _localTasks.FirstOrDefault(t => t.Id == taskId);
                if (task == null)
                {
                    Utils.Logger.Warning("ConversionTaskService", $"⚠️ 任务不存在: {taskId}");
                    return false;
                }

                if (!task.CanStart)
                {
                    Utils.Logger.Warning("ConversionTaskService", $"⚠️ 任务无法启动: {taskId}, 状态: {task.Status}");
                    return false;
                }

                // 调用API开始转换
                var response = await _apiClient.StartConversionAsync(request);
                if (response.Success)
                {
                    task.Start();
                    return true;
                }
                else
                {
                    Utils.Logger.Error("ConversionTaskService", $"❌ 转换开始失败: {response.Message}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionTaskService", $"❌ 启动转换失败: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> CancelTaskAsync(TaskId taskId)
        {
            try
            {
                var task = _localTasks.FirstOrDefault(t => t.Id == taskId);
                if (task != null && task.CanCancel)
                {
                    task.Cancel();
                    await _apiClient.CancelTaskAsync(taskId.Value);
                    // 任务取消完成（移除日志）
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionTaskService", $"❌ 取消任务失败: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RetryTaskAsync(TaskId taskId)
        {
            try
            {
                var task = _localTasks.FirstOrDefault(t => t.Id == taskId);
                if (task != null && task.IsFailed)
                {
                    // 重置任务状态并重新开始
                    Utils.Logger.Info("ConversionTaskService", $"🔄 重试任务: {taskId}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionTaskService", $"❌ 重试任务失败: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region 进度管理

        public async Task UpdateTaskProgressAsync(TaskId taskId, int progress, double? speed = null, TimeSpan? estimatedRemaining = null)
        {
            try
            {
                var task = _localTasks.FirstOrDefault(t => t.Id == taskId);
                if (task != null && task.IsInProgress)
                {
                    task.UpdateProgress(progress, speed, estimatedRemaining);
                    TaskProgressUpdated?.Invoke(this, new TaskProgressUpdatedEventArgs(taskId, progress, speed, estimatedRemaining));
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionTaskService", $"❌ 更新任务进度失败: {ex.Message}");
            }
        }

        public async Task CompleteTaskAsync(TaskId taskId)
        {
            try
            {
                var task = _localTasks.FirstOrDefault(t => t.Id == taskId);
                if (task != null && task.IsInProgress)
                {
                    task.Complete();
                    TaskCompleted?.Invoke(this, new TaskCompletedEventArgs(taskId, task.Name.Value, true));
                    Utils.Logger.Info("ConversionTaskService", $"✅ 任务完成: {taskId}");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionTaskService", $"❌ 完成任务失败: {ex.Message}");
            }
        }

        public async Task FailTaskAsync(TaskId taskId, string errorMessage)
        {
            try
            {
                var task = _localTasks.FirstOrDefault(t => t.Id == taskId);
                if (task != null)
                {
                    task.Fail(errorMessage);
                    TaskCompleted?.Invoke(this, new TaskCompletedEventArgs(taskId, task.Name.Value, false, errorMessage));
                    Utils.Logger.Error("ConversionTaskService", $"❌ 任务失败: {taskId}, 错误: {errorMessage}");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionTaskService", $"❌ 标记任务失败时出错: {ex.Message}");
            }
        }

        #endregion

        #region 文件操作

        public async Task<string?> DownloadTaskFileAsync(TaskId taskId)
        {
            try
            {
                var response = await _apiClient.DownloadFileAsync(taskId.Value);
                if (response.Success)
                {
                    Utils.Logger.Info("ConversionTaskService", $"📥 文件下载成功: {taskId}");
                    return response.Data;
                }
                return null;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionTaskService", $"❌ 下载文件失败: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> HasLocalFileAsync(TaskId taskId)
        {
            // 简化实现，实际应该检查本地文件系统
            return false;
        }

        public async Task<string?> GetLocalFilePathAsync(TaskId taskId)
        {
            // 简化实现，实际应该返回本地文件路径
            return null;
        }

        #endregion

        #region 事件

        public event EventHandler<TaskStatusChangedEventArgs>? TaskStatusChanged;
        public event EventHandler<TaskProgressUpdatedEventArgs>? TaskProgressUpdated;
        public event EventHandler<TaskCompletedEventArgs>? TaskCompleted;

        /// <summary>
        /// 批量转换API - 使用真实API服务
        /// </summary>
        public async Task<ApiResponseDto<object>> StartBatchConversionAsync(
            List<string> filePaths,
            StartConversionRequestDto request,
            IProgress<object>? progress = null)
        {
            try
            {
                Utils.Logger.Info("ConversionTaskService", $"🚀 开始批量转换API调用，文件数量: {filePaths.Count}");

                // 🔑 使用真实的API服务
                using var apiService = new ApiService();

                // 创建进度转换器
                var apiProgress = progress != null ? new Progress<BatchUploadProgress>(p => progress.Report(p)) : null;

                // 调用真实的批量转换API
                var result = await apiService.StartBatchConversionAsync(filePaths, request, apiProgress);

                if (result.Success)
                {
                    Utils.Logger.Info("ConversionTaskService", $"✅ 批量转换API调用成功，BatchId: {result.Data?.BatchId}");
                    return ApiResponseDto<object>.CreateSuccess(result.Data, result.Message);
                }
                else
                {
                    Utils.Logger.Error("ConversionTaskService", $"❌ 批量转换API调用失败: {result.Message}");
                    return ApiResponseDto<object>.CreateError(result.Message);
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionTaskService", $"❌ 批量转换API调用异常: {ex.Message}");
                return ApiResponseDto<object>.CreateError($"批量转换失败: {ex.Message}");
            }
        }

        #endregion
    }
}
