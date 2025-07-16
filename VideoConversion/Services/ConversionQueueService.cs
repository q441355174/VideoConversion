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
                // 获取待处理的任务
                var pendingTasks = await databaseService.GetActiveTasksAsync();
                var pendingTasksOnly = pendingTasks.Where(t => t.Status == ConversionStatus.Pending).ToList();

                if (pendingTasksOnly.Any())
                {
                    _logger.LogDebug("发现 {Count} 个待处理任务", pendingTasksOnly.Count);

                    foreach (var task in pendingTasksOnly)
                    {
                        try
                        {
                            // 启动转换任务（异步执行，不等待完成）
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await conversionService.StartConversionAsync(task.Id);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "转换任务执行失败: {TaskId}", task.Id);
                                }
                            });

                            _logger.LogInformation("启动转换任务: {TaskId} - {TaskName}", task.Id, task.TaskName);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "启动转换任务失败: {TaskId}", task.Id);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取待处理任务失败");
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
