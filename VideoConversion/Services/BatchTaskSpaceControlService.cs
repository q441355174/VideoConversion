using VideoConversion.Models;
using Microsoft.AspNetCore.SignalR;
using VideoConversion.Hubs;

namespace VideoConversion.Services
{
    /// <summary>
    /// 批量任务空间控制服务
    /// </summary>
    public class BatchTaskSpaceControlService
    {
        private readonly DatabaseService _databaseService;
        private readonly DiskSpaceService _diskSpaceService;
        private readonly SpaceEstimationService _spaceEstimationService;
        private readonly IHubContext<ConversionHub> _hubContext;
        private readonly ILogger<BatchTaskSpaceControlService> _logger;

        // 批量任务状态跟踪
        private readonly Dictionary<string, BatchTaskInfo> _activeBatches = new();
        private readonly object _batchLock = new object();

        // 空间监控定时器
        private readonly Timer _spaceMonitorTimer;

        public BatchTaskSpaceControlService(
            DatabaseService databaseService,
            DiskSpaceService diskSpaceService,
            SpaceEstimationService spaceEstimationService,
            IHubContext<ConversionHub> hubContext,
            ILogger<BatchTaskSpaceControlService> logger)
        {
            _databaseService = databaseService;
            _diskSpaceService = diskSpaceService;
            _spaceEstimationService = spaceEstimationService;
            _hubContext = hubContext;
            _logger = logger;

            // 设置空间监控定时器（每30秒检查一次）
            _spaceMonitorTimer = new Timer(async _ => await MonitorSpaceAndControlTasks(), 
                null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

            //_logger.LogInformation("BatchTaskSpaceControlService 初始化完成");
        }

        #region 批量任务注册和管理

        /// <summary>
        /// 注册批量任务
        /// </summary>
        public async Task<string> RegisterBatchTaskAsync(List<string> taskIds, string clientId = "")
        {
            try
            {
                var batchId = Guid.NewGuid().ToString();
                var batchInfo = new BatchTaskInfo
                {
                    BatchId = batchId,
                    TaskIds = taskIds,
                    ClientId = clientId,
                    CreatedAt = DateTime.Now,
                    Status = BatchTaskStatus.Active,
                    TotalTasks = taskIds.Count,
                    CompletedTasks = 0
                };

                // 计算批量任务的空间需求
                await CalculateBatchSpaceRequirementAsync(batchInfo);

                lock (_batchLock)
                {
                    _activeBatches[batchId] = batchInfo;
                }

                _logger.LogInformation("📦 注册批量任务: BatchId={BatchId}, 任务数={TaskCount}, 预估空间={SpaceGB:F2}GB", 
                    batchId, taskIds.Count, batchInfo.EstimatedSpaceRequirementGB);

                // 立即检查空间并控制任务
                await CheckSpaceAndControlBatchAsync(batchInfo);

                return batchId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "注册批量任务失败");
                throw;
            }
        }

        /// <summary>
        /// 更新任务完成状态
        /// </summary>
        public async Task UpdateTaskCompletionAsync(string taskId)
        {
            try
            {
                lock (_batchLock)
                {
                    foreach (var batch in _activeBatches.Values)
                    {
                        if (batch.TaskIds.Contains(taskId))
                        {
                            batch.CompletedTasks++;
                            batch.LastUpdatedAt = DateTime.Now;

                            _logger.LogDebug("📋 更新批量任务进度: BatchId={BatchId}, 完成={Completed}/{Total}", 
                                batch.BatchId, batch.CompletedTasks, batch.TotalTasks);

                            // 检查是否全部完成
                            if (batch.CompletedTasks >= batch.TotalTasks)
                            {
                                batch.Status = BatchTaskStatus.Completed;
                                _logger.LogInformation("✅ 批量任务完成: BatchId={BatchId}", batch.BatchId);
                            }
                            break;
                        }
                    }
                }

                // 清理已完成的批量任务
                await CleanupCompletedBatchesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新任务完成状态失败: {TaskId}", taskId);
            }
        }

        /// <summary>
        /// 移除批量任务
        /// </summary>
        public void RemoveBatchTask(string batchId)
        {
            lock (_batchLock)
            {
                if (_activeBatches.Remove(batchId))
                {
                    _logger.LogInformation("🗑️ 移除批量任务: BatchId={BatchId}", batchId);
                }
            }
        }

        #endregion

        #region 空间监控和控制

        /// <summary>
        /// 监控空间并控制任务
        /// </summary>
        private async Task MonitorSpaceAndControlTasks()
        {
            try
            {
                var currentUsage = await _diskSpaceService.GetCurrentSpaceStatusAsync();
                if (currentUsage == null)
                {
                    _logger.LogWarning("⚠️ 无法获取当前空间使用情况，跳过批量任务控制");
                    return;
                }

                var availableSpaceGB = currentUsage.AvailableSpace / 1024.0 / 1024.0 / 1024.0;
                var usagePercentage = currentUsage.UsagePercentage;

                _logger.LogDebug("📊 空间监控: 可用={AvailableGB:F2}GB, 使用率={UsagePercent:F1}%", 
                    availableSpaceGB, usagePercentage);

                // 检查所有活跃的批量任务
                List<BatchTaskInfo> batchesToCheck;
                lock (_batchLock)
                {
                    batchesToCheck = _activeBatches.Values
                        .Where(b => b.Status == BatchTaskStatus.Active || b.Status == BatchTaskStatus.Paused)
                        .ToList();
                }

                foreach (var batch in batchesToCheck)
                {
                    await CheckSpaceAndControlBatchAsync(batch);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "空间监控和任务控制失败");
            }
        }

        /// <summary>
        /// 检查空间并控制批量任务
        /// </summary>
        private async Task CheckSpaceAndControlBatchAsync(BatchTaskInfo batchInfo)
        {
            try
            {
                var currentUsage = await _diskSpaceService.GetCurrentSpaceStatusAsync();
                if (currentUsage == null) return;

                var availableSpaceGB = currentUsage.AvailableSpace / 1024.0 / 1024.0 / 1024.0;
                var usagePercentage = currentUsage.UsagePercentage;

                // 空间不足的阈值
                var spaceInsufficientThreshold = 85.0; // 使用率超过85%
                var reason = "";

                // 只进行监控和通知，不执行暂停操作（暂停由客户端处理）
                if (batchInfo.Status == BatchTaskStatus.Active)
                {
                    if (usagePercentage > spaceInsufficientThreshold)
                    {
                        reason = $"磁盘使用率过高 ({usagePercentage:F1}%)";
                        await NotifySpaceWarningAsync(batchInfo, reason);
                    }
                    else if (availableSpaceGB < batchInfo.EstimatedSpaceRequirementGB)
                    {
                        reason = $"可用空间不足 (需要{batchInfo.EstimatedSpaceRequirementGB:F2}GB，可用{availableSpaceGB:F2}GB)";
                        await NotifySpaceWarningAsync(batchInfo, reason);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查空间并控制批量任务失败: {BatchId}", batchInfo.BatchId);
            }
        }



        #endregion

        #region 空间警告通知

        /// <summary>
        /// 通知空间警告
        /// </summary>
        private async Task NotifySpaceWarningAsync(BatchTaskInfo batchInfo, string reason)
        {
            try
            {
                var notification = new
                {
                    BatchId = batchInfo.BatchId,
                    Warning = "SpaceInsufficient",
                    Reason = reason,
                    UsagePercentage = (await _diskSpaceService.GetCurrentSpaceStatusAsync())?.UsagePercentage ?? 0,
                    AvailableSpaceGB = (await _diskSpaceService.GetCurrentSpaceStatusAsync())?.AvailableSpace / 1024.0 / 1024 / 1024 ?? 0,
                    RequiredSpaceGB = batchInfo.EstimatedSpaceRequirementGB,
                    Timestamp = DateTime.Now
                };

                // 通知特定批量任务组
                await _hubContext.Clients.Group($"batch_{batchInfo.BatchId}")
                    .SendAsync("BatchSpaceWarning", notification);

                // 通知所有客户端
                await _hubContext.Clients.All.SendAsync("BatchSpaceWarning", notification);

                _logger.LogWarning("⚠️ 批量任务空间警告: BatchId={BatchId}, 原因={Reason}",
                    batchInfo.BatchId, reason);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "通知批量任务空间警告失败: {BatchId}", batchInfo.BatchId);
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 计算批量任务的空间需求
        /// </summary>
        private async Task CalculateBatchSpaceRequirementAsync(BatchTaskInfo batchInfo)
        {
            try
            {
                var totalSpaceRequirement = 0L;

                foreach (var taskId in batchInfo.TaskIds)
                {
                    var task = await _databaseService.GetTaskAsync(taskId);
                    if (task != null)
                    {
                        // 使用SpaceEstimationService计算空间需求
                        var settings = new ConversionSettings
                        {
                            VideoCodec = task.VideoCodec,
                            OutputFormat = task.OutputFormat,
                            Resolution = task.Resolution,
                            Quality = "medium" // 默认质量
                        };

                        var spaceRequirement = _spaceEstimationService.CalculateTotalSpaceRequirement(
                            task.OriginalFileSize, settings);

                        totalSpaceRequirement += spaceRequirement.TotalRequiredSize;
                    }
                }

                batchInfo.EstimatedSpaceRequirementGB = totalSpaceRequirement / 1024.0 / 1024.0 / 1024.0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "计算批量任务空间需求失败: {BatchId}", batchInfo.BatchId);
                batchInfo.EstimatedSpaceRequirementGB = 0;
            }
        }

        /// <summary>
        /// 清理已完成的批量任务
        /// </summary>
        private async Task CleanupCompletedBatchesAsync()
        {
            try
            {
                var cutoffTime = DateTime.Now.AddHours(-1); // 保留1小时
                var completedBatches = new List<string>();

                lock (_batchLock)
                {
                    foreach (var kvp in _activeBatches)
                    {
                        var batch = kvp.Value;
                        if (batch.Status == BatchTaskStatus.Completed && batch.LastUpdatedAt < cutoffTime)
                        {
                            completedBatches.Add(kvp.Key);
                        }
                    }

                    foreach (var batchId in completedBatches)
                    {
                        _activeBatches.Remove(batchId);
                    }
                }

                if (completedBatches.Count > 0)
                {
                    _logger.LogInformation("🧹 清理已完成的批量任务: {Count}个", completedBatches.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理已完成批量任务失败");
            }
        }



        #endregion

        public void Dispose()
        {
            _spaceMonitorTimer?.Dispose();
        }
    }

    #region 数据模型

    /// <summary>
    /// 批量任务信息
    /// </summary>
    public class BatchTaskInfo
    {
        public string BatchId { get; set; } = "";
        public List<string> TaskIds { get; set; } = new();
        public string ClientId { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdatedAt { get; set; }
        public BatchTaskStatus Status { get; set; }
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public double EstimatedSpaceRequirementGB { get; set; }
        
        // 暂停相关
        public DateTime? PausedAt { get; set; }
        public string? PauseReason { get; set; }
        
        // 恢复相关
        public DateTime? ResumedAt { get; set; }
        public string? ResumeReason { get; set; }
    }

    /// <summary>
    /// 批量任务状态
    /// </summary>
    public enum BatchTaskStatus
    {
        Active,     // 活跃
        Paused,     // 暂停
        Completed,  // 完成
        Cancelled   // 取消
    }

    #endregion
}
