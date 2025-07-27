using VideoConversion.Models;
using Microsoft.AspNetCore.SignalR;
using VideoConversion.Hubs;

namespace VideoConversion.Services
{
    /// <summary>
    /// 下载跟踪服务
    /// </summary>
    public class DownloadTrackingService
    {
        private readonly DatabaseService _databaseService;
        private readonly AdvancedFileCleanupService _cleanupService;
        private readonly IHubContext<ConversionHub> _hubContext;
        private readonly ILogger<DownloadTrackingService> _logger;

        // 下载记录缓存
        private readonly Dictionary<string, DownloadRecord> _downloadCache = new();
        private readonly object _cacheLock = new object();

        // 清理调度器
        private readonly Timer _cleanupScheduler;

        public DownloadTrackingService(
            DatabaseService databaseService,
            AdvancedFileCleanupService cleanupService,
            IHubContext<ConversionHub> hubContext,
            ILogger<DownloadTrackingService> logger)
        {
            _databaseService = databaseService;
            _cleanupService = cleanupService;
            _hubContext = hubContext;
            _logger = logger;

            // 设置清理调度器（每小时检查一次）
            _cleanupScheduler = new Timer(async _ => await ProcessScheduledCleanupsAsync(), 
                null, TimeSpan.FromMinutes(10), TimeSpan.FromHours(1));

            _logger.LogInformation("DownloadTrackingService 初始化完成");
        }

        #region 下载跟踪

        /// <summary>
        /// 记录文件下载
        /// </summary>
        public async Task TrackDownloadAsync(string taskId, string clientIp = "", string userAgent = "")
        {
            try
            {
                var task = await _databaseService.GetTaskAsync(taskId);
                if (task == null)
                {
                    _logger.LogWarning("任务不存在，无法跟踪下载: {TaskId}", taskId);
                    return;
                }

                var downloadRecord = new DownloadRecord
                {
                    TaskId = taskId,
                    FileName = task.TaskName,
                    FilePath = task.OutputFilePath ?? "",
                    FileSize = task.OutputFileSize,
                    DownloadTime = DateTime.Now,
                    ClientIp = clientIp,
                    UserAgent = userAgent,
                    ScheduledCleanupTime = DateTime.Now.AddHours(24) // 默认24小时后清理
                };

                // 保存到数据库
                await SaveDownloadRecordAsync(downloadRecord);

                // 添加到缓存
                lock (_cacheLock)
                {
                    _downloadCache[taskId] = downloadRecord;
                }

                _logger.LogInformation("📥 记录文件下载: TaskId={TaskId}, FileName={FileName}, ClientIp={ClientIp}", 
                    taskId, downloadRecord.FileName, clientIp);

                // 通知客户端下载记录
                await NotifyDownloadTrackedAsync(downloadRecord);

                // 调度清理任务
                await ScheduleCleanupAsync(downloadRecord);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "跟踪文件下载失败: {TaskId}", taskId);
            }
        }

        /// <summary>
        /// 获取下载记录
        /// </summary>
        public async Task<DownloadRecord?> GetDownloadRecordAsync(string taskId)
        {
            try
            {
                // 先从缓存查找
                lock (_cacheLock)
                {
                    if (_downloadCache.TryGetValue(taskId, out var cachedRecord))
                    {
                        return cachedRecord;
                    }
                }

                // 从数据库查找
                var sql = "SELECT * FROM DownloadRecord WHERE TaskId = @TaskId ORDER BY DownloadTime DESC LIMIT 1";
                var record = await _databaseService.GetDatabaseAsync().Ado.SqlQuerySingleAsync<DownloadRecord>(sql, new { TaskId = taskId });

                if (record != null)
                {
                    lock (_cacheLock)
                    {
                        _downloadCache[taskId] = record;
                    }
                }

                return record;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取下载记录失败: {TaskId}", taskId);
                return null;
            }
        }

        /// <summary>
        /// 获取所有待清理的下载记录
        /// </summary>
        public async Task<List<DownloadRecord>> GetPendingCleanupRecordsAsync()
        {
            try
            {
                var sql = @"
                    SELECT * FROM DownloadRecord 
                    WHERE IsCleanedUp = 0 AND ScheduledCleanupTime <= @CurrentTime
                    ORDER BY ScheduledCleanupTime ASC";

                return await _databaseService.GetDatabaseAsync().Ado.SqlQueryAsync<DownloadRecord>(sql, new 
                { 
                    CurrentTime = DateTime.Now 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取待清理下载记录失败");
                return new List<DownloadRecord>();
            }
        }

        /// <summary>
        /// 更新下载记录的清理状态
        /// </summary>
        public async Task MarkAsCleanedUpAsync(string taskId)
        {
            try
            {
                var sql = @"
                    UPDATE DownloadRecord 
                    SET IsCleanedUp = 1, CleanedUpTime = @CleanedUpTime 
                    WHERE TaskId = @TaskId";

                await _databaseService.GetDatabaseAsync().Ado.ExecuteCommandAsync(sql, new 
                { 
                    TaskId = taskId,
                    CleanedUpTime = DateTime.Now
                });

                // 从缓存中移除
                lock (_cacheLock)
                {
                    _downloadCache.Remove(taskId);
                }

                _logger.LogDebug("标记下载记录为已清理: {TaskId}", taskId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "标记下载记录为已清理失败: {TaskId}", taskId);
            }
        }

        #endregion

        #region 清理调度

        /// <summary>
        /// 调度清理任务
        /// </summary>
        private async Task ScheduleCleanupAsync(DownloadRecord record)
        {
            try
            {
                var delay = record.ScheduledCleanupTime - DateTime.Now;
                if (delay.TotalMilliseconds > 0)
                {
                    // 创建延迟清理任务
                    _ = Task.Delay(delay).ContinueWith(async _ =>
                    {
                        await CleanupDownloadedFileAsync(record.TaskId);
                    });

                    _logger.LogDebug("已调度文件清理: TaskId={TaskId}, 清理时间={CleanupTime}", 
                        record.TaskId, record.ScheduledCleanupTime);
                }
                else
                {
                    // 立即清理
                    await CleanupDownloadedFileAsync(record.TaskId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "调度清理任务失败: {TaskId}", record.TaskId);
            }
        }

        /// <summary>
        /// 处理定时清理
        /// </summary>
        private async Task ProcessScheduledCleanupsAsync()
        {
            try
            {
                var pendingRecords = await GetPendingCleanupRecordsAsync();
                
                if (pendingRecords.Count > 0)
                {
                    _logger.LogInformation("🕒 处理定时清理: {Count}个文件", pendingRecords.Count);

                    foreach (var record in pendingRecords)
                    {
                        await CleanupDownloadedFileAsync(record.TaskId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理定时清理失败");
            }
        }

        /// <summary>
        /// 清理已下载的文件
        /// </summary>
        private async Task CleanupDownloadedFileAsync(string taskId)
        {
            try
            {
                var record = await GetDownloadRecordAsync(taskId);
                if (record == null || record.IsCleanedUp)
                {
                    return;
                }

                var task = await _databaseService.GetTaskAsync(taskId);
                if (task == null)
                {
                    await MarkAsCleanedUpAsync(taskId);
                    return;
                }

                // 检查文件是否存在
                if (string.IsNullOrEmpty(task.OutputFilePath) || !File.Exists(task.OutputFilePath))
                {
                    await MarkAsCleanedUpAsync(taskId);
                    return;
                }

                // 执行文件清理
                var fileInfo = new FileInfo(task.OutputFilePath);
                var fileSize = fileInfo.Length;
                
                File.Delete(task.OutputFilePath);

                // 标记为已清理
                await MarkAsCleanedUpAsync(taskId);

                _logger.LogInformation("🗑️ 已清理下载文件: TaskId={TaskId}, FileName={FileName}, Size={SizeMB:F2}MB", 
                    taskId, record.FileName, fileSize / 1024.0 / 1024);

                // 通知客户端文件已清理
                await NotifyFileCleanedUpAsync(record, fileSize);

                // 更新磁盘空间统计
                // 这里可以调用DiskSpaceService更新统计
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理已下载文件失败: {TaskId}", taskId);
            }
        }

        #endregion

        #region 下载统计

        /// <summary>
        /// 获取下载统计信息
        /// </summary>
        public async Task<DownloadStatistics> GetDownloadStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                startDate ??= DateTime.Now.AddDays(-30); // 默认30天
                endDate ??= DateTime.Now;

                var sql = @"
                    SELECT 
                        COUNT(*) as TotalDownloads,
                        SUM(FileSize) as TotalDownloadedSize,
                        COUNT(DISTINCT ClientIp) as UniqueClients,
                        AVG(FileSize) as AverageFileSize
                    FROM DownloadRecord 
                    WHERE DownloadTime BETWEEN @StartDate AND @EndDate";

                var result = await _databaseService.GetDatabaseAsync().Ado.SqlQuerySingleAsync<dynamic>(sql, new 
                { 
                    StartDate = startDate,
                    EndDate = endDate
                });

                return new DownloadStatistics
                {
                    TotalDownloads = result.TotalDownloads ?? 0,
                    TotalDownloadedSize = result.TotalDownloadedSize ?? 0,
                    UniqueClients = result.UniqueClients ?? 0,
                    AverageFileSize = result.AverageFileSize ?? 0,
                    StartDate = startDate.Value,
                    EndDate = endDate.Value
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取下载统计失败");
                return new DownloadStatistics();
            }
        }

        /// <summary>
        /// 获取热门下载文件
        /// </summary>
        public async Task<List<PopularDownload>> GetPopularDownloadsAsync(int topCount = 10)
        {
            try
            {
                var sql = @"
                    SELECT 
                        TaskId,
                        FileName,
                        COUNT(*) as DownloadCount,
                        SUM(FileSize) as TotalSize,
                        MAX(DownloadTime) as LastDownloadTime
                    FROM DownloadRecord 
                    WHERE DownloadTime >= @StartDate
                    GROUP BY TaskId, FileName
                    ORDER BY DownloadCount DESC
                    LIMIT @TopCount";

                var results = await _databaseService.GetDatabaseAsync().Ado.SqlQueryAsync<PopularDownload>(sql, new 
                { 
                    StartDate = DateTime.Now.AddDays(-30),
                    TopCount = topCount
                });

                return results.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取热门下载失败");
                return new List<PopularDownload>();
            }
        }

        #endregion

        #region 通知方法

        /// <summary>
        /// 通知下载跟踪
        /// </summary>
        private async Task NotifyDownloadTrackedAsync(DownloadRecord record)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("DownloadTracked", new
                {
                    TaskId = record.TaskId,
                    FileName = record.FileName,
                    FileSize = record.FileSize,
                    FileSizeMB = Math.Round(record.FileSize / 1024.0 / 1024, 2),
                    DownloadTime = record.DownloadTime,
                    ScheduledCleanupTime = record.ScheduledCleanupTime,
                    ClientIp = record.ClientIp,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "通知下载跟踪失败");
            }
        }

        /// <summary>
        /// 通知文件已清理
        /// </summary>
        private async Task NotifyFileCleanedUpAsync(DownloadRecord record, long fileSize)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("DownloadedFileCleanedUp", new
                {
                    TaskId = record.TaskId,
                    FileName = record.FileName,
                    FileSize = fileSize,
                    FileSizeMB = Math.Round(fileSize / 1024.0 / 1024, 2),
                    DownloadTime = record.DownloadTime,
                    CleanupTime = DateTime.Now,
                    RetentionHours = Math.Round((DateTime.Now - record.DownloadTime).TotalHours, 1),
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "通知文件清理失败");
            }
        }

        #endregion

        #region 数据库操作

        /// <summary>
        /// 保存下载记录
        /// </summary>
        private async Task SaveDownloadRecordAsync(DownloadRecord record)
        {
            try
            {
                var sql = @"
                    INSERT INTO DownloadRecord 
                    (TaskId, FileName, FilePath, FileSize, DownloadTime, ClientIp, UserAgent, ScheduledCleanupTime, IsCleanedUp)
                    VALUES 
                    (@TaskId, @FileName, @FilePath, @FileSize, @DownloadTime, @ClientIp, @UserAgent, @ScheduledCleanupTime, @IsCleanedUp)";

                await _databaseService.GetDatabaseAsync().Ado.ExecuteCommandAsync(sql, record);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存下载记录失败: {TaskId}", record.TaskId);
                throw;
            }
        }

        /// <summary>
        /// 初始化下载记录表
        /// </summary>
        public async Task InitializeDatabaseAsync()
        {
            var maxRetries = 3;
            var retryDelay = TimeSpan.FromSeconds(1);

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    _logger.LogDebug("开始初始化下载记录表，尝试次数: {Attempt}/{MaxRetries}", attempt, maxRetries);

                    // 获取数据库实例
                    var db = _databaseService.GetDatabaseAsync();
                    if (db == null)
                    {
                        throw new InvalidOperationException("数据库服务未初始化");
                    }

                    // 分别执行SQL语句，避免SQLite多语句执行问题
                    var createTableSql = @"
                        CREATE TABLE IF NOT EXISTS DownloadRecord (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            TaskId TEXT NOT NULL,
                            FileName TEXT NOT NULL,
                            FilePath TEXT NOT NULL,
                            FileSize INTEGER NOT NULL,
                            DownloadTime DATETIME NOT NULL,
                            ClientIp TEXT,
                            UserAgent TEXT,
                            ScheduledCleanupTime DATETIME NOT NULL,
                            IsCleanedUp INTEGER DEFAULT 0,
                            CleanedUpTime DATETIME,
                            CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                        )";

                    await db.Ado.ExecuteCommandAsync(createTableSql);
                    _logger.LogDebug("下载记录表创建成功");

                    // 创建索引
                    var indexSqls = new[]
                    {
                        "CREATE INDEX IF NOT EXISTS idx_downloadrecord_taskid ON DownloadRecord(TaskId)",
                        "CREATE INDEX IF NOT EXISTS idx_downloadrecord_scheduledcleanuptime ON DownloadRecord(ScheduledCleanupTime)",
                        "CREATE INDEX IF NOT EXISTS idx_downloadrecord_downloadtime ON DownloadRecord(DownloadTime)"
                    };

                    foreach (var indexSql in indexSqls)
                    {
                        await db.Ado.ExecuteCommandAsync(indexSql);
                    }

                    _logger.LogInformation("下载记录表初始化完成");
                    return; // 成功，退出重试循环
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "初始化下载记录表失败，尝试次数: {Attempt}/{MaxRetries}", attempt, maxRetries);

                    if (attempt == maxRetries)
                    {
                        _logger.LogError("下载记录表初始化最终失败，已达到最大重试次数");
                        throw;
                    }

                    // 等待后重试
                    await Task.Delay(retryDelay);
                    retryDelay = TimeSpan.FromSeconds(retryDelay.TotalSeconds * 2); // 指数退避
                }
            }
        }

        #endregion

        public void Dispose()
        {
            _cleanupScheduler?.Dispose();
        }
    }

    #region 数据模型

    /// <summary>
    /// 下载记录
    /// </summary>
    public class DownloadRecord
    {
        public int Id { get; set; }
        public string TaskId { get; set; } = "";
        public string FileName { get; set; } = "";
        public string FilePath { get; set; } = "";
        public long FileSize { get; set; }
        public DateTime DownloadTime { get; set; }
        public string ClientIp { get; set; } = "";
        public string UserAgent { get; set; } = "";
        public DateTime ScheduledCleanupTime { get; set; }
        public bool IsCleanedUp { get; set; }
        public DateTime? CleanedUpTime { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 下载统计
    /// </summary>
    public class DownloadStatistics
    {
        public int TotalDownloads { get; set; }
        public long TotalDownloadedSize { get; set; }
        public int UniqueClients { get; set; }
        public double AverageFileSize { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public double TotalDownloadedSizeMB => TotalDownloadedSize / 1024.0 / 1024;
        public double AverageFileSizeMB => AverageFileSize / 1024.0 / 1024;
    }

    /// <summary>
    /// 热门下载
    /// </summary>
    public class PopularDownload
    {
        public string TaskId { get; set; } = "";
        public string FileName { get; set; } = "";
        public int DownloadCount { get; set; }
        public long TotalSize { get; set; }
        public DateTime LastDownloadTime { get; set; }

        public double TotalSizeMB => TotalSize / 1024.0 / 1024;
    }

    #endregion
}
