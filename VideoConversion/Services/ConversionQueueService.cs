using VideoConversion.Models;
using Microsoft.AspNetCore.SignalR;
using VideoConversion.Hubs;

namespace VideoConversion.Services
{
    /// <summary>
    /// 转换队列后台服务
    /// </summary>
    public class ConversionQueueService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ConversionQueueService> _logger;
        private readonly IConfiguration _configuration;
        private readonly int _checkIntervalSeconds;
        private readonly HashSet<string> _runningTasks = new(); // 跟踪正在执行的任务
        private readonly HashSet<string> _cancelledTasks = new(); // 跟踪被取消的任务

        public ConversionQueueService(
            IServiceProvider serviceProvider,
            ILogger<ConversionQueueService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _configuration = configuration;
            _checkIntervalSeconds = _configuration.GetValue<int>("VideoConversion:QueueCheckIntervalSeconds", 10);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("转换队列服务启动");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessPendingTasksAsync();
                    await Task.Delay(TimeSpan.FromSeconds(_checkIntervalSeconds), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // 正常停止
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "处理转换队列时发生错误");
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); // 错误后等待更长时间
                }
            }

            _logger.LogInformation("转换队列服务停止");
        }

        /// <summary>
        /// 处理待处理的任务
        /// </summary>
        private async Task ProcessPendingTasksAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var databaseService = scope.ServiceProvider.GetRequiredService<DatabaseService>();
            var conversionService = scope.ServiceProvider.GetRequiredService<VideoConversionService>();

            try
            {
                // 获取待处理的任务（只处理Pending状态且不在运行列表中的任务）
                var pendingTasks = await databaseService.GetActiveTasksAsync();

                // 只在有任务时记录日志
                if (pendingTasks.Any())
                {
                    _logger.LogDebug("队列检查 - 活动任务总数: {Count}", pendingTasks.Count);
                }

                // 检查是否有失败的任务仍在活动列表中（这不应该发生）
                var failedTasks = pendingTasks.Where(t => t.Status == ConversionStatus.Failed).ToList();
                if (failedTasks.Any())
                {
                    _logger.LogError("发现 {Count} 个失败任务仍在活动列表中，这是数据库查询错误:", failedTasks.Count);
                    foreach (var task in failedTasks)
                    {
                        _logger.LogError("失败任务: {TaskId} ({TaskName}) - 状态: {Status}",
                            task.Id, task.TaskName, task.Status);
                    }
                }

                var pendingTasksOnly = pendingTasks
                    .Where(t => t.Status == ConversionStatus.Pending)
                    .Where(t => !_runningTasks.Contains(t.Id))
                    .ToList();

                // 额外检查：确保没有已完成的任务被误认为待处理
                var completedTasks = pendingTasks.Where(t => t.Status == ConversionStatus.Completed).ToList();
                if (completedTasks.Any())
                {
                    _logger.LogWarning("发现 {Count} 个已完成但仍在活动列表中的任务:", completedTasks.Count);
                    foreach (var task in completedTasks)
                    {
                        _logger.LogWarning("已完成任务: {TaskId} ({TaskName}) - 状态: {Status}",
                            task.Id, task.TaskName, task.Status);
                    }
                }

                if (pendingTasksOnly.Any())
                {
                    _logger.LogInformation("处理 {Count} 个待处理任务", pendingTasksOnly.Count);

                    foreach (var task in pendingTasksOnly)
                    {
                        // 检查任务是否已被取消
                        if (_cancelledTasks.Contains(task.Id))
                        {
                            _logger.LogDebug("任务 {TaskId} 已被取消，跳过", task.Id);
                            _cancelledTasks.Remove(task.Id); // 清理取消列表
                            continue;
                        }

                        // 检查任务是否已经在执行中
                        lock (_runningTasks)
                        {
                            if (_runningTasks.Contains(task.Id))
                            {
                                _logger.LogDebug("任务 {TaskId} 已在执行中，跳过", task.Id);
                                continue;
                            }
                        }

                        try
                        {
                            // 使用原子操作尝试启动任务
                            var canStart = await databaseService.TryStartTaskAsync(task.Id);
                            if (!canStart)
                            {
                                continue;
                            }

                            // 只有成功获得任务锁后才添加到运行列表
                            lock (_runningTasks)
                            {
                                _runningTasks.Add(task.Id);
                            }

                            // 启动转换任务（异步执行，不等待完成）
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    // 在开始转换前再次检查是否被取消
                                    if (_cancelledTasks.Contains(task.Id))
                                    {
                                        _logger.LogInformation("任务 {TaskId} 在开始前被取消", task.Id);
                                        await conversionService.CancelConversionAsync(task.Id);
                                        return;
                                    }

                                    await conversionService.StartConversionAsync(task.Id);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "转换任务执行失败: {TaskId}", task.Id);
                                }
                                finally
                                {
                                    // 任务完成后从运行列表和取消列表中移除
                                    lock (_runningTasks)
                                    {
                                        _runningTasks.Remove(task.Id);
                                    }
                                    _cancelledTasks.Remove(task.Id);
                                }
                            });

                            _logger.LogInformation("启动转换任务: {TaskId} - {TaskName}", task.Id, task.TaskName);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "启动转换任务失败: {TaskId}", task.Id);
                            // 启动失败时也要从运行列表中移除
                            lock (_runningTasks)
                            {
                                _runningTasks.Remove(task.Id);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取待处理任务失败");
            }
        }

        /// <summary>
        /// 取消任务
        /// </summary>
        public async Task CancelTaskAsync(string taskId)
        {
            try
            {
                _logger.LogInformation("🚫 收到取消任务请求: {TaskId}", taskId);

                // 添加到取消列表
                _cancelledTasks.Add(taskId);

                // 如果任务正在运行，从运行列表中移除
                _runningTasks.Remove(taskId);

                // 使用作用域服务更新数据库状态
                using var scope = _serviceProvider.CreateScope();
                var videoConversionService = scope.ServiceProvider.GetRequiredService<VideoConversionService>();
                await videoConversionService.CancelConversionAsync(taskId);

                _logger.LogInformation("✅ 任务取消完成: {TaskId}", taskId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 取消任务失败: {TaskId}", taskId);
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("正在停止转换队列服务...");
            await base.StopAsync(cancellationToken);
        }
    }

    /// <summary>
    /// 文件清理后台服务
    /// </summary>
    public class FileCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<FileCleanupService> _logger;
        private readonly IConfiguration _configuration;
        private readonly int _cleanupIntervalMinutes;

        public FileCleanupService(
            IServiceProvider serviceProvider,
            ILogger<FileCleanupService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _configuration = configuration;
            _cleanupIntervalMinutes = _configuration.GetValue<int>("VideoConversion:CleanupIntervalMinutes", 60);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("文件清理服务启动，清理间隔: {Interval} 分钟", _cleanupIntervalMinutes);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PerformCleanupAsync();
                    await Task.Delay(TimeSpan.FromMinutes(_cleanupIntervalMinutes), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "文件清理时发生错误");
                    await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken); // 错误后等待10分钟
                }
            }

            _logger.LogInformation("文件清理服务停止");
        }

        /// <summary>
        /// 执行清理操作
        /// </summary>
        private async Task PerformCleanupAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var databaseService = scope.ServiceProvider.GetRequiredService<DatabaseService>();
            var fileService = scope.ServiceProvider.GetRequiredService<FileService>();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<ConversionHub>>();

            try
            {
                _logger.LogInformation("开始清理旧文件和任务记录");

                // 清理旧的任务记录
                var cleanedTasks = await databaseService.CleanupOldTasksAsync(30); // 清理30天前的任务

                // 清理旧文件
                var cleanedFiles = await fileService.CleanupOldFilesAsync(7); // 清理7天前的文件

                if (cleanedTasks > 0 || cleanedFiles > 0)
                {
                    var message = $"清理完成: {cleanedTasks} 个任务记录, {cleanedFiles} 个文件";
                    _logger.LogInformation(message);
                    
                    // 通知客户端
                    await hubContext.SendSystemNotificationAsync(message, "success");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理操作失败");
                
                // 通知客户端清理失败
                await scope.ServiceProvider.GetRequiredService<IHubContext<ConversionHub>>()
                    .SendSystemNotificationAsync("文件清理失败", "error");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("正在停止文件清理服务...");
            await base.StopAsync(cancellationToken);
        }
    }
}
