namespace VideoConversion.Services
{
    /// <summary>
    /// 日志记录服务
    /// </summary>
    public class LoggingService
    {
        private readonly ILogger<LoggingService> _logger;

        public LoggingService(ILogger<LoggingService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 记录转换任务开始
        /// </summary>
        public void LogConversionStarted(string taskId, string taskName, string inputFile, string outputFormat)
        {
            _logger.LogInformation("转换任务开始 - TaskId: {TaskId}, TaskName: {TaskName}, InputFile: {InputFile}, OutputFormat: {OutputFormat}",
                taskId, taskName, inputFile, outputFormat);
        }

        /// <summary>
        /// 记录转换任务完成
        /// </summary>
        public void LogConversionCompleted(string taskId, string taskName, TimeSpan duration, long outputFileSize) 
        {
            _logger.LogInformation("转换任务完成 - TaskId: {TaskId}, TaskName: {TaskName}, Duration: {Duration}, OutputSize: {OutputSize}",
                taskId, taskName, duration, outputFileSize);
        }

        /// <summary>
        /// 记录转换任务失败
        /// </summary>
        public void LogConversionFailed(string taskId, string taskName, Exception exception)
        {
            _logger.LogError(exception, "转换任务失败 - TaskId: {TaskId}, TaskName: {TaskName}",
                taskId, taskName);
        }

        /// <summary>
        /// 记录转换任务取消
        /// </summary>
        public void LogConversionCancelled(string taskId, string taskName)
        {
            _logger.LogWarning("转换任务被取消 - TaskId: {TaskId}, TaskName: {TaskName}",
                taskId, taskName);
        }

        /// <summary>
        /// 记录文件上传
        /// </summary>
        public void LogFileUploaded(string fileName, long fileSize, string clientIp)
        {
            _logger.LogInformation("文件上传 - FileName: {FileName}, FileSize: {FileSize}, ClientIP: {ClientIP}",
                fileName, fileSize, clientIp);
        }

        /// <summary>
        /// 记录文件下载
        /// </summary>
        public void LogFileDownloaded(string taskId, string fileName, string clientIp)
        {
            _logger.LogInformation("文件下载 - TaskId: {TaskId}, FileName: {FileName}, ClientIP: {ClientIP}",
                taskId, fileName, clientIp);
        }

        /// <summary>
        /// 记录文件删除
        /// </summary>
        public void LogFileDeleted(string filePath, string reason)
        {
            _logger.LogInformation("文件删除 - FilePath: {FilePath}, Reason: {Reason}",
                filePath, reason);
        }

        /// <summary>
        /// 记录系统性能指标
        /// </summary>
        public void LogPerformanceMetrics(int activeTasks, int pendingTasks, double cpuUsage, long memoryUsage)
        {
            _logger.LogInformation("系统性能指标 - ActiveTasks: {ActiveTasks}, PendingTasks: {PendingTasks}, CPU: {CPU}%, Memory: {Memory}MB",
                activeTasks, pendingTasks, cpuUsage, memoryUsage / 1024 / 1024);
        }

        /// <summary>
        /// 记录数据库操作
        /// </summary>
        public void LogDatabaseOperation(string operation, string tableName, int affectedRows, TimeSpan duration)
        {
            _logger.LogDebug("数据库操作 - Operation: {Operation}, Table: {TableName}, AffectedRows: {AffectedRows}, Duration: {Duration}ms",
                operation, tableName, affectedRows, duration.TotalMilliseconds);
        }

        /// <summary>
        /// 记录SignalR连接事件
        /// </summary>
        public void LogSignalRConnection(string connectionId, string eventType, string? userId = null)
        {
            _logger.LogDebug("SignalR连接事件 - ConnectionId: {ConnectionId}, Event: {EventType}, UserId: {UserId}",
                connectionId, eventType, userId ?? "Anonymous");
        }

        /// <summary>
        /// 记录安全事件
        /// </summary>
        public void LogSecurityEvent(string eventType, string clientIp, string? details = null)
        {
            _logger.LogWarning("安全事件 - EventType: {EventType}, ClientIP: {ClientIP}, Details: {Details}",
                eventType, clientIp, details ?? "无");
        }

        /// <summary>
        /// 记录配置变更
        /// </summary>
        public void LogConfigurationChange(string configKey, string oldValue, string newValue, string changedBy)
        {
            _logger.LogInformation("配置变更 - Key: {ConfigKey}, OldValue: {OldValue}, NewValue: {NewValue}, ChangedBy: {ChangedBy}",
                configKey, oldValue, newValue, changedBy);
        }

        /// <summary>
        /// 记录清理操作
        /// </summary>
        public void LogCleanupOperation(string operationType, int itemsProcessed, TimeSpan duration)
        {
            _logger.LogInformation("清理操作 - Type: {OperationType}, ItemsProcessed: {ItemsProcessed}, Duration: {Duration}",
                operationType, itemsProcessed, duration);
        }

        /// <summary>
        /// 记录API调用
        /// </summary>
        public void LogApiCall(string endpoint, string method, string clientIp, int statusCode, TimeSpan duration)
        {
            var logLevel = statusCode >= 400 ? LogLevel.Warning : LogLevel.Information;
            _logger.Log(logLevel, "API调用 - Endpoint: {Endpoint}, Method: {Method}, ClientIP: {ClientIP}, StatusCode: {StatusCode}, Duration: {Duration}ms",
                endpoint, method, clientIp, statusCode, duration.TotalMilliseconds);
        }

        /// <summary>
        /// 记录FFmpeg输出
        /// </summary>
        public void LogFFmpegOutput(string taskId, string output, bool isError = false)
        {
            if (isError)
            {
                _logger.LogError("FFmpeg错误输出 - TaskId: {TaskId}, Output: {Output}", taskId, output);
            }
            else
            {
                _logger.LogDebug("FFmpeg输出 - TaskId: {TaskId}, Output: {Output}", taskId, output);
            }
        }

        /// <summary>
        /// 记录队列状态
        /// </summary>
        public void LogQueueStatus(int queueLength, int processingCount, int completedToday, int failedToday)
        {
            _logger.LogInformation("队列状态 - QueueLength: {QueueLength}, Processing: {ProcessingCount}, CompletedToday: {CompletedToday}, FailedToday: {FailedToday}",
                queueLength, processingCount, completedToday, failedToday);
        }
    }
}
