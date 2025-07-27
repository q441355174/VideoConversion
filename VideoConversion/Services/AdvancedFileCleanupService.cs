using VideoConversion.Models;
using Microsoft.AspNetCore.SignalR;
using VideoConversion.Hubs;

namespace VideoConversion.Services
{
    /// <summary>
    /// 高级文件清理服务
    /// </summary>
    public class AdvancedFileCleanupService
    {
        private readonly DatabaseService _databaseService;
        private readonly DiskSpaceService _diskSpaceService;
        private readonly IHubContext<ConversionHub> _hubContext;
        private readonly ILogger<AdvancedFileCleanupService> _logger;

        // 清理策略配置
        private readonly CleanupConfig _config;
        
        // 定时清理器
        private readonly Timer _scheduledCleanupTimer;
        private readonly Timer _emergencyCleanupTimer;

        // 清理统计
        private readonly CleanupStatistics _statistics = new();

        public AdvancedFileCleanupService(
            DatabaseService databaseService,
            DiskSpaceService diskSpaceService,
            IHubContext<ConversionHub> hubContext,
            ILogger<AdvancedFileCleanupService> logger)
        {
            _databaseService = databaseService;
            _diskSpaceService = diskSpaceService;
            _hubContext = hubContext;
            _logger = logger;

            // 初始化清理配置
            _config = LoadCleanupConfig();

            // 设置定时清理（每小时执行一次）
            _scheduledCleanupTimer = new Timer(async _ => await PerformScheduledCleanupAsync(), 
                null, TimeSpan.FromMinutes(5), TimeSpan.FromHours(1));

            // 设置紧急清理监控（每5分钟检查一次）
            _emergencyCleanupTimer = new Timer(async _ => await CheckEmergencyCleanupAsync(), 
                null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));

            _logger.LogInformation("AdvancedFileCleanupService 初始化完成");
        }

        #region 清理策略配置

        /// <summary>
        /// 加载清理配置
        /// </summary>
        private CleanupConfig LoadCleanupConfig()
        {
            return new CleanupConfig
            {
                // 转换完成后清理
                CleanupAfterConversion = true,
                ConversionCleanupDelayMinutes = 5, // 转换完成5分钟后清理

                // 下载完成后清理
                CleanupAfterDownload = true,
                DownloadCleanupDelayHours = 24, // 下载完成24小时后清理

                // 临时文件清理
                CleanupTempFiles = true,
                TempFileRetentionHours = 2, // 临时文件保留2小时

                // 失败任务清理
                CleanupFailedTasks = true,
                FailedTaskRetentionDays = 7, // 失败任务保留7天

                // 紧急清理阈值
                EmergencyCleanupThreshold = 95.0, // 使用率超过95%触发紧急清理
                AggressiveCleanupThreshold = 90.0, // 使用率超过90%触发激进清理

                // 孤儿文件清理
                CleanupOrphanFiles = true,
                OrphanFileRetentionDays = 1, // 孤儿文件保留1天

                // 日志文件清理
                CleanupLogFiles = true,
                LogFileRetentionDays = 30 // 日志文件保留30天
            };
        }

        /// <summary>
        /// 更新清理配置
        /// </summary>
        public async Task UpdateCleanupConfigAsync(CleanupConfig config)
        {
            try
            {
                // 验证配置
                if (config.EmergencyCleanupThreshold <= config.AggressiveCleanupThreshold)
                {
                    throw new ArgumentException("紧急清理阈值必须大于激进清理阈值");
                }

                // 更新配置
                _config.CleanupAfterConversion = config.CleanupAfterConversion;
                _config.ConversionCleanupDelayMinutes = config.ConversionCleanupDelayMinutes;
                _config.CleanupAfterDownload = config.CleanupAfterDownload;
                _config.DownloadCleanupDelayHours = config.DownloadCleanupDelayHours;
                _config.CleanupTempFiles = config.CleanupTempFiles;
                _config.TempFileRetentionHours = config.TempFileRetentionHours;
                _config.CleanupFailedTasks = config.CleanupFailedTasks;
                _config.FailedTaskRetentionDays = config.FailedTaskRetentionDays;
                _config.EmergencyCleanupThreshold = config.EmergencyCleanupThreshold;
                _config.AggressiveCleanupThreshold = config.AggressiveCleanupThreshold;
                _config.CleanupOrphanFiles = config.CleanupOrphanFiles;
                _config.OrphanFileRetentionDays = config.OrphanFileRetentionDays;
                _config.CleanupLogFiles = config.CleanupLogFiles;
                _config.LogFileRetentionDays = config.LogFileRetentionDays;

                // TODO: 保存配置到数据库
                _logger.LogInformation("清理配置已更新");

                // 通知客户端配置变更
                await NotifyConfigChangedAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新清理配置失败");
                throw;
            }
        }

        #endregion

        #region 定时清理

        /// <summary>
        /// 执行定时清理
        /// </summary>
        private async Task PerformScheduledCleanupAsync()
        {
            try
            {
                _logger.LogDebug("开始执行定时清理...");

                var cleanupResult = new CleanupResult();

                // 1. 清理转换完成的文件
                if (_config.CleanupAfterConversion)
                {
                    var conversionCleanup = await CleanupCompletedConversionsAsync();
                    cleanupResult.Merge(conversionCleanup);
                }

                // 2. 清理下载完成的文件
                if (_config.CleanupAfterDownload)
                {
                    var downloadCleanup = await CleanupDownloadedFilesAsync();
                    cleanupResult.Merge(downloadCleanup);
                }

                // 3. 清理临时文件
                if (_config.CleanupTempFiles)
                {
                    var tempCleanup = await CleanupTempFilesAsync();
                    cleanupResult.Merge(tempCleanup);
                }

                // 4. 清理失败任务文件
                if (_config.CleanupFailedTasks)
                {
                    var failedCleanup = await CleanupFailedTaskFilesAsync();
                    cleanupResult.Merge(failedCleanup);
                }

                // 5. 清理孤儿文件
                if (_config.CleanupOrphanFiles)
                {
                    var orphanCleanup = await CleanupOrphanFilesAsync();
                    cleanupResult.Merge(orphanCleanup);
                }

                // 6. 清理日志文件
                if (_config.CleanupLogFiles)
                {
                    var logCleanup = await CleanupLogFilesAsync();
                    cleanupResult.Merge(logCleanup);
                }

                // 更新统计信息
                _statistics.UpdateStatistics(cleanupResult);

                if (cleanupResult.TotalCleanedSize > 0)
                {
                    _logger.LogInformation("🧹 定时清理完成: 释放空间={SizeMB:F2}MB, 清理文件={FileCount}个", 
                        cleanupResult.TotalCleanedSize / 1024.0 / 1024, cleanupResult.TotalCleanedFiles);

                    // 通知客户端清理结果
                    await NotifyCleanupCompletedAsync(cleanupResult, "定时清理");

                    // 更新磁盘空间统计
                    await _diskSpaceService.UpdateSpaceUsage(-cleanupResult.TotalCleanedSize, SpaceCategory.TempFiles);
                }
                else
                {
                    _logger.LogDebug("定时清理完成，无文件需要清理");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "定时清理失败");
            }
        }

        /// <summary>
        /// 检查紧急清理
        /// </summary>
        private async Task CheckEmergencyCleanupAsync()
        {
            try
            {
                var spaceStatus = await _diskSpaceService.GetCurrentSpaceStatusAsync();
                if (spaceStatus == null) return;

                var usagePercentage = spaceStatus.UsagePercentage;

                // 检查是否需要紧急清理
                if (usagePercentage >= _config.EmergencyCleanupThreshold)
                {
                    _logger.LogWarning("🚨 触发紧急清理: 使用率={UsagePercent:F1}%", usagePercentage);
                    await PerformEmergencyCleanupAsync();
                }
                // 检查是否需要激进清理
                else if (usagePercentage >= _config.AggressiveCleanupThreshold)
                {
                    _logger.LogWarning("⚡ 触发激进清理: 使用率={UsagePercent:F1}%", usagePercentage);
                    await PerformAggressiveCleanupAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查紧急清理失败");
            }
        }

        #endregion

        #region 紧急清理

        /// <summary>
        /// 执行紧急清理
        /// </summary>
        private async Task PerformEmergencyCleanupAsync()
        {
            try
            {
                var cleanupResult = new CleanupResult();

                // 紧急清理策略：立即清理所有可清理的文件
                _logger.LogWarning("🚨 开始紧急清理...");

                // 1. 立即清理所有临时文件（忽略保留期）
                var tempCleanup = await CleanupTempFilesAsync(ignoreRetention: true);
                cleanupResult.Merge(tempCleanup);

                // 2. 立即清理已下载的文件（忽略保留期）
                var downloadCleanup = await CleanupDownloadedFilesAsync(ignoreRetention: true);
                cleanupResult.Merge(downloadCleanup);

                // 3. 清理孤儿文件
                var orphanCleanup = await CleanupOrphanFilesAsync(ignoreRetention: true);
                cleanupResult.Merge(orphanCleanup);

                // 4. 清理旧的日志文件
                var logCleanup = await CleanupLogFilesAsync(aggressiveMode: true);
                cleanupResult.Merge(logCleanup);

                _logger.LogWarning("🚨 紧急清理完成: 释放空间={SizeMB:F2}MB, 清理文件={FileCount}个", 
                    cleanupResult.TotalCleanedSize / 1024.0 / 1024, cleanupResult.TotalCleanedFiles);

                // 通知客户端紧急清理结果
                await NotifyCleanupCompletedAsync(cleanupResult, "紧急清理");

                // 更新磁盘空间统计
                if (cleanupResult.TotalCleanedSize > 0)
                {
                    await _diskSpaceService.UpdateSpaceUsage(-cleanupResult.TotalCleanedSize, SpaceCategory.TempFiles);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "紧急清理失败");
            }
        }

        /// <summary>
        /// 执行激进清理
        /// </summary>
        private async Task PerformAggressiveCleanupAsync()
        {
            try
            {
                var cleanupResult = new CleanupResult();

                _logger.LogWarning("⚡ 开始激进清理...");

                // 激进清理策略：缩短保留期，更积极地清理文件
                
                // 1. 清理临时文件（缩短保留期到30分钟）
                var tempCleanup = await CleanupTempFilesAsync(customRetentionHours: 0.5);
                cleanupResult.Merge(tempCleanup);

                // 2. 清理已下载文件（缩短保留期到6小时）
                var downloadCleanup = await CleanupDownloadedFilesAsync(customRetentionHours: 6);
                cleanupResult.Merge(downloadCleanup);

                // 3. 清理孤儿文件（缩短保留期到6小时）
                var orphanCleanup = await CleanupOrphanFilesAsync(customRetentionHours: 6);
                cleanupResult.Merge(orphanCleanup);

                _logger.LogWarning("⚡ 激进清理完成: 释放空间={SizeMB:F2}MB, 清理文件={FileCount}个", 
                    cleanupResult.TotalCleanedSize / 1024.0 / 1024, cleanupResult.TotalCleanedFiles);

                // 通知客户端激进清理结果
                await NotifyCleanupCompletedAsync(cleanupResult, "激进清理");

                // 更新磁盘空间统计
                if (cleanupResult.TotalCleanedSize > 0)
                {
                    await _diskSpaceService.UpdateSpaceUsage(-cleanupResult.TotalCleanedSize, SpaceCategory.TempFiles);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "激进清理失败");
            }
        }

        #endregion

        #region 手动清理

        /// <summary>
        /// 手动触发清理
        /// </summary>
        public async Task<CleanupResult> PerformManualCleanupAsync(ManualCleanupRequest request)
        {
            try
            {
                _logger.LogInformation("🔧 开始手动清理: {Request}", request);

                var cleanupResult = new CleanupResult();

                if (request.CleanupTempFiles)
                {
                    var tempCleanup = await CleanupTempFilesAsync(ignoreRetention: request.IgnoreRetention);
                    cleanupResult.Merge(tempCleanup);
                }

                if (request.CleanupDownloadedFiles)
                {
                    var downloadCleanup = await CleanupDownloadedFilesAsync(ignoreRetention: request.IgnoreRetention);
                    cleanupResult.Merge(downloadCleanup);
                }

                if (request.CleanupOrphanFiles)
                {
                    var orphanCleanup = await CleanupOrphanFilesAsync(ignoreRetention: request.IgnoreRetention);
                    cleanupResult.Merge(orphanCleanup);
                }

                if (request.CleanupFailedTasks)
                {
                    var failedCleanup = await CleanupFailedTaskFilesAsync(ignoreRetention: request.IgnoreRetention);
                    cleanupResult.Merge(failedCleanup);
                }

                if (request.CleanupLogFiles)
                {
                    var logCleanup = await CleanupLogFilesAsync(aggressiveMode: request.IgnoreRetention);
                    cleanupResult.Merge(logCleanup);
                }

                _logger.LogInformation("🔧 手动清理完成: 释放空间={SizeMB:F2}MB, 清理文件={FileCount}个", 
                    cleanupResult.TotalCleanedSize / 1024.0 / 1024, cleanupResult.TotalCleanedFiles);

                // 通知客户端手动清理结果
                await NotifyCleanupCompletedAsync(cleanupResult, "手动清理");

                // 更新磁盘空间统计
                if (cleanupResult.TotalCleanedSize > 0)
                {
                    await _diskSpaceService.UpdateSpaceUsage(-cleanupResult.TotalCleanedSize, SpaceCategory.TempFiles);
                }

                return cleanupResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "手动清理失败");
                throw;
            }
        }

        #endregion

        #region 具体清理方法

        /// <summary>
        /// 清理转换完成的文件
        /// </summary>
        private async Task<CleanupResult> CleanupCompletedConversionsAsync()
        {
            var result = new CleanupResult();
            
            try
            {
                var cutoffTime = DateTime.Now.AddMinutes(-_config.ConversionCleanupDelayMinutes);
                var completedTasks = await GetCompletedTasksBeforeAsync(cutoffTime);

                foreach (var task in completedTasks)
                {
                    try
                    {
                        // 清理原文件
                        if (!string.IsNullOrEmpty(task.OriginalFilePath) && File.Exists(task.OriginalFilePath))
                        {
                            var size = new FileInfo(task.OriginalFilePath).Length;
                            File.Delete(task.OriginalFilePath);
                            result.OriginalFilesCleanedSize += size;
                            result.OriginalFilesCleanedCount++;
                        }

                        // 清理分片文件
                        var chunkSize = await CleanupChunkFilesAsync(task.Id);
                        result.TempFilesCleanedSize += chunkSize.size;
                        result.TempFilesCleanedCount += chunkSize.count;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "清理转换完成文件失败: {TaskId}", task.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理转换完成文件失败");
            }

            return result;
        }

        /// <summary>
        /// 清理已下载的文件
        /// </summary>
        private async Task<CleanupResult> CleanupDownloadedFilesAsync(bool ignoreRetention = false, double? customRetentionHours = null)
        {
            var result = new CleanupResult();
            
            try
            {
                var retentionHours = customRetentionHours ?? _config.DownloadCleanupDelayHours;
                var cutoffTime = ignoreRetention ? DateTime.Now : DateTime.Now.AddHours(-retentionHours);
                
                var downloadedTasks = await GetDownloadedTasksBeforeAsync(cutoffTime);

                foreach (var task in downloadedTasks)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(task.OutputFilePath) && File.Exists(task.OutputFilePath))
                        {
                            var size = new FileInfo(task.OutputFilePath).Length;
                            File.Delete(task.OutputFilePath);
                            result.ConvertedFilesCleanedSize += size;
                            result.ConvertedFilesCleanedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "清理已下载文件失败: {TaskId}", task.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理已下载文件失败");
            }

            return result;
        }

        /// <summary>
        /// 清理临时文件
        /// </summary>
        private async Task<CleanupResult> CleanupTempFilesAsync(bool ignoreRetention = false, double? customRetentionHours = null)
        {
            var result = new CleanupResult();
            
            try
            {
                var retentionHours = customRetentionHours ?? _config.TempFileRetentionHours;
                var cutoffTime = ignoreRetention ? DateTime.Now : DateTime.Now.AddHours(-retentionHours);
                
                var tempDirectories = new[] { "temp", "uploads/temp", "uploads/chunks" };

                foreach (var tempDir in tempDirectories)
                {
                    if (!Directory.Exists(tempDir)) continue;

                    var files = Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories);
                    
                    foreach (var file in files)
                    {
                        try
                        {
                            var fileInfo = new FileInfo(file);
                            if (fileInfo.LastWriteTime < cutoffTime)
                            {
                                result.TempFilesCleanedSize += fileInfo.Length;
                                result.TempFilesCleanedCount++;
                                File.Delete(file);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "删除临时文件失败: {FilePath}", file);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理临时文件失败");
            }

            return result;
        }

        /// <summary>
        /// 清理失败任务文件
        /// </summary>
        private async Task<CleanupResult> CleanupFailedTaskFilesAsync(bool ignoreRetention = false)
        {
            var result = new CleanupResult();
            
            try
            {
                var cutoffTime = ignoreRetention ? DateTime.Now : DateTime.Now.AddDays(-_config.FailedTaskRetentionDays);
                var failedTasks = await GetFailedTasksBeforeAsync(cutoffTime);

                foreach (var task in failedTasks)
                {
                    try
                    {
                        // 清理原文件
                        if (!string.IsNullOrEmpty(task.OriginalFilePath) && File.Exists(task.OriginalFilePath))
                        {
                            var size = new FileInfo(task.OriginalFilePath).Length;
                            File.Delete(task.OriginalFilePath);
                            result.OriginalFilesCleanedSize += size;
                            result.OriginalFilesCleanedCount++;
                        }

                        // 清理输出文件（如果存在）
                        if (!string.IsNullOrEmpty(task.OutputFilePath) && File.Exists(task.OutputFilePath))
                        {
                            var size = new FileInfo(task.OutputFilePath).Length;
                            File.Delete(task.OutputFilePath);
                            result.ConvertedFilesCleanedSize += size;
                            result.ConvertedFilesCleanedCount++;
                        }

                        // 清理分片文件
                        var chunkSize = await CleanupChunkFilesAsync(task.Id);
                        result.TempFilesCleanedSize += chunkSize.size;
                        result.TempFilesCleanedCount += chunkSize.count;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "清理失败任务文件失败: {TaskId}", task.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理失败任务文件失败");
            }

            return result;
        }

        /// <summary>
        /// 清理孤儿文件
        /// </summary>
        private async Task<CleanupResult> CleanupOrphanFilesAsync(bool ignoreRetention = false, double? customRetentionHours = null)
        {
            var result = new CleanupResult();
            
            try
            {
                var retentionHours = customRetentionHours ?? (_config.OrphanFileRetentionDays * 24);
                var cutoffTime = ignoreRetention ? DateTime.Now : DateTime.Now.AddHours(-retentionHours);
                
                // 获取所有任务的文件路径
                var allTasks = await GetAllTasksAsync();
                var validFilePaths = new HashSet<string>();
                
                foreach (var task in allTasks)
                {
                    if (!string.IsNullOrEmpty(task.OriginalFilePath))
                        validFilePaths.Add(Path.GetFullPath(task.OriginalFilePath));
                    if (!string.IsNullOrEmpty(task.OutputFilePath))
                        validFilePaths.Add(Path.GetFullPath(task.OutputFilePath));
                }

                // 检查uploads和outputs目录中的孤儿文件
                var directories = new[] { "uploads", "outputs" };
                
                foreach (var dir in directories)
                {
                    if (!Directory.Exists(dir)) continue;

                    var files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
                    
                    foreach (var file in files)
                    {
                        try
                        {
                            var fullPath = Path.GetFullPath(file);
                            if (!validFilePaths.Contains(fullPath))
                            {
                                var fileInfo = new FileInfo(file);
                                if (fileInfo.LastWriteTime < cutoffTime)
                                {
                                    result.OrphanFilesCleanedSize += fileInfo.Length;
                                    result.OrphanFilesCleanedCount++;
                                    File.Delete(file);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "删除孤儿文件失败: {FilePath}", file);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理孤儿文件失败");
            }

            return result;
        }

        /// <summary>
        /// 清理日志文件
        /// </summary>
        private async Task<CleanupResult> CleanupLogFilesAsync(bool aggressiveMode = false)
        {
            var result = new CleanupResult();
            
            try
            {
                var retentionDays = aggressiveMode ? 7 : _config.LogFileRetentionDays;
                var cutoffTime = DateTime.Now.AddDays(-retentionDays);
                
                var logDirectories = new[] { "logs", "Logs" };

                foreach (var logDir in logDirectories)
                {
                    if (!Directory.Exists(logDir)) continue;

                    var files = Directory.GetFiles(logDir, "*.log", SearchOption.AllDirectories);
                    
                    foreach (var file in files)
                    {
                        try
                        {
                            var fileInfo = new FileInfo(file);
                            if (fileInfo.LastWriteTime < cutoffTime)
                            {
                                result.LogFilesCleanedSize += fileInfo.Length;
                                result.LogFilesCleanedCount++;
                                File.Delete(file);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "删除日志文件失败: {FilePath}", file);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理日志文件失败");
            }

            return result;
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 清理分片文件
        /// </summary>
        private async Task<(long size, int count)> CleanupChunkFilesAsync(string taskId)
        {
            var cleanedSize = 0L;
            var cleanedCount = 0;
            
            try
            {
                var chunkDir = Path.Combine("uploads", "chunks", taskId);
                
                if (Directory.Exists(chunkDir))
                {
                    var files = Directory.GetFiles(chunkDir, "*", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        try
                        {
                            var fileInfo = new FileInfo(file);
                            cleanedSize += fileInfo.Length;
                            cleanedCount++;
                            File.Delete(file);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "删除分片文件失败: {FilePath}", file);
                        }
                    }
                    
                    try
                    {
                        Directory.Delete(chunkDir, true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "删除分片目录失败: {DirPath}", chunkDir);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理分片文件失败: {TaskId}", taskId);
            }

            return (cleanedSize, cleanedCount);
        }

        /// <summary>
        /// 通知清理完成
        /// </summary>
        private async Task NotifyCleanupCompletedAsync(CleanupResult result, string cleanupType)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("CleanupCompleted", new
                {
                    CleanupType = cleanupType,
                    TotalCleanedSize = result.TotalCleanedSize,
                    TotalCleanedSizeMB = Math.Round(result.TotalCleanedSize / 1024.0 / 1024, 2),
                    TotalCleanedFiles = result.TotalCleanedFiles,
                    Details = new
                    {
                        OriginalFiles = new { Size = result.OriginalFilesCleanedSize, Count = result.OriginalFilesCleanedCount },
                        ConvertedFiles = new { Size = result.ConvertedFilesCleanedSize, Count = result.ConvertedFilesCleanedCount },
                        TempFiles = new { Size = result.TempFilesCleanedSize, Count = result.TempFilesCleanedCount },
                        OrphanFiles = new { Size = result.OrphanFilesCleanedSize, Count = result.OrphanFilesCleanedCount },
                        LogFiles = new { Size = result.LogFilesCleanedSize, Count = result.LogFilesCleanedCount }
                    },
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "通知清理完成失败");
            }
        }

        /// <summary>
        /// 通知配置变更
        /// </summary>
        private async Task NotifyConfigChangedAsync()
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("CleanupConfigChanged", new
                {
                    Config = _config,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "通知配置变更失败");
            }
        }

        #endregion

        #region 数据库查询方法

        private async Task<List<ConversionTask>> GetCompletedTasksBeforeAsync(DateTime cutoffTime)
        {
            try
            {
                var sql = "SELECT * FROM ConversionTask WHERE Status = @Status AND CompletedAt < @CutoffTime";
                return await _databaseService.GetDatabaseAsync().Ado.SqlQueryAsync<ConversionTask>(sql, new 
                { 
                    Status = ConversionStatus.Completed.ToString(), 
                    CutoffTime = cutoffTime 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取已完成任务失败");
                return new List<ConversionTask>();
            }
        }

        private async Task<List<ConversionTask>> GetDownloadedTasksBeforeAsync(DateTime cutoffTime)
        {
            try
            {
                // TODO: 实现下载记录跟踪
                // 这里需要一个下载记录表来跟踪文件下载时间
                var sql = "SELECT * FROM ConversionTask WHERE Status = @Status AND CompletedAt < @CutoffTime";
                return await _databaseService.GetDatabaseAsync().Ado.SqlQueryAsync<ConversionTask>(sql, new 
                { 
                    Status = ConversionStatus.Completed.ToString(), 
                    CutoffTime = cutoffTime 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取已下载任务失败");
                return new List<ConversionTask>();
            }
        }

        private async Task<List<ConversionTask>> GetFailedTasksBeforeAsync(DateTime cutoffTime)
        {
            try
            {
                var sql = "SELECT * FROM ConversionTask WHERE Status = @Status AND CreatedAt < @CutoffTime";
                return await _databaseService.GetDatabaseAsync().Ado.SqlQueryAsync<ConversionTask>(sql, new 
                { 
                    Status = ConversionStatus.Failed.ToString(), 
                    CutoffTime = cutoffTime 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取失败任务失败");
                return new List<ConversionTask>();
            }
        }

        private async Task<List<ConversionTask>> GetAllTasksAsync()
        {
            try
            {
                var sql = "SELECT * FROM ConversionTask";
                return await _databaseService.GetDatabaseAsync().Ado.SqlQueryAsync<ConversionTask>(sql);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取所有任务失败");
                return new List<ConversionTask>();
            }
        }

        #endregion

        public void Dispose()
        {
            _scheduledCleanupTimer?.Dispose();
            _emergencyCleanupTimer?.Dispose();
        }
    }

    #region 数据模型

    /// <summary>
    /// 清理配置
    /// </summary>
    public class CleanupConfig
    {
        // 转换完成后清理
        public bool CleanupAfterConversion { get; set; } = true;
        public int ConversionCleanupDelayMinutes { get; set; } = 5;

        // 下载完成后清理
        public bool CleanupAfterDownload { get; set; } = true;
        public double DownloadCleanupDelayHours { get; set; } = 24;

        // 临时文件清理
        public bool CleanupTempFiles { get; set; } = true;
        public double TempFileRetentionHours { get; set; } = 2;

        // 失败任务清理
        public bool CleanupFailedTasks { get; set; } = true;
        public int FailedTaskRetentionDays { get; set; } = 7;

        // 紧急清理阈值
        public double EmergencyCleanupThreshold { get; set; } = 95.0;
        public double AggressiveCleanupThreshold { get; set; } = 90.0;

        // 孤儿文件清理
        public bool CleanupOrphanFiles { get; set; } = true;
        public int OrphanFileRetentionDays { get; set; } = 1;

        // 日志文件清理
        public bool CleanupLogFiles { get; set; } = true;
        public int LogFileRetentionDays { get; set; } = 30;
    }

    /// <summary>
    /// 清理结果
    /// </summary>
    public class CleanupResult
    {
        // 原文件清理
        public long OriginalFilesCleanedSize { get; set; }
        public int OriginalFilesCleanedCount { get; set; }

        // 转换文件清理
        public long ConvertedFilesCleanedSize { get; set; }
        public int ConvertedFilesCleanedCount { get; set; }

        // 临时文件清理
        public long TempFilesCleanedSize { get; set; }
        public int TempFilesCleanedCount { get; set; }

        // 孤儿文件清理
        public long OrphanFilesCleanedSize { get; set; }
        public int OrphanFilesCleanedCount { get; set; }

        // 日志文件清理
        public long LogFilesCleanedSize { get; set; }
        public int LogFilesCleanedCount { get; set; }

        // 总计
        public long TotalCleanedSize => OriginalFilesCleanedSize + ConvertedFilesCleanedSize +
                                       TempFilesCleanedSize + OrphanFilesCleanedSize + LogFilesCleanedSize;
        public int TotalCleanedFiles => OriginalFilesCleanedCount + ConvertedFilesCleanedCount +
                                       TempFilesCleanedCount + OrphanFilesCleanedCount + LogFilesCleanedCount;

        /// <summary>
        /// 合并清理结果
        /// </summary>
        public void Merge(CleanupResult other)
        {
            OriginalFilesCleanedSize += other.OriginalFilesCleanedSize;
            OriginalFilesCleanedCount += other.OriginalFilesCleanedCount;
            ConvertedFilesCleanedSize += other.ConvertedFilesCleanedSize;
            ConvertedFilesCleanedCount += other.ConvertedFilesCleanedCount;
            TempFilesCleanedSize += other.TempFilesCleanedSize;
            TempFilesCleanedCount += other.TempFilesCleanedCount;
            OrphanFilesCleanedSize += other.OrphanFilesCleanedSize;
            OrphanFilesCleanedCount += other.OrphanFilesCleanedCount;
            LogFilesCleanedSize += other.LogFilesCleanedSize;
            LogFilesCleanedCount += other.LogFilesCleanedCount;
        }
    }

    /// <summary>
    /// 手动清理请求
    /// </summary>
    public class ManualCleanupRequest
    {
        public bool CleanupTempFiles { get; set; } = true;
        public bool CleanupDownloadedFiles { get; set; } = false;
        public bool CleanupOrphanFiles { get; set; } = true;
        public bool CleanupFailedTasks { get; set; } = false;
        public bool CleanupLogFiles { get; set; } = false;
        public bool IgnoreRetention { get; set; } = false;

        public override string ToString()
        {
            var items = new List<string>();
            if (CleanupTempFiles) items.Add("临时文件");
            if (CleanupDownloadedFiles) items.Add("已下载文件");
            if (CleanupOrphanFiles) items.Add("孤儿文件");
            if (CleanupFailedTasks) items.Add("失败任务");
            if (CleanupLogFiles) items.Add("日志文件");

            var result = string.Join(", ", items);
            if (IgnoreRetention) result += " (忽略保留期)";

            return result;
        }
    }

    /// <summary>
    /// 清理统计
    /// </summary>
    public class CleanupStatistics
    {
        public long TotalCleanedSize { get; private set; }
        public int TotalCleanedFiles { get; private set; }
        public int TotalCleanupRuns { get; private set; }
        public DateTime LastCleanupTime { get; private set; }
        public DateTime LastEmergencyCleanupTime { get; private set; }

        public void UpdateStatistics(CleanupResult result)
        {
            TotalCleanedSize += result.TotalCleanedSize;
            TotalCleanedFiles += result.TotalCleanedFiles;
            TotalCleanupRuns++;
            LastCleanupTime = DateTime.Now;
        }

        public void RecordEmergencyCleanup()
        {
            LastEmergencyCleanupTime = DateTime.Now;
        }
    }

    #endregion
}
