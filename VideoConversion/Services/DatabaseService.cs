using SqlSugar;
using VideoConversion.Models;

namespace VideoConversion.Services
{
    /// <summary>
    /// 数据库服务
    /// </summary>
    public class DatabaseService
    {
        private readonly SqlSugarScope _db;
        private readonly ILogger<DatabaseService> _logger;

        public DatabaseService(ILogger<DatabaseService> logger, IConfiguration configuration)
        {
            _logger = logger;
            
            // 获取数据库连接字符串
            var connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? "Data Source=videoconversion.db";

            _db = new SqlSugarScope(new ConnectionConfig()
            {
                ConnectionString = connectionString,
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            });

            // 初始化数据库
            InitializeDatabase();
        }

        /// <summary>
        /// 初始化数据库
        /// </summary>
        private void InitializeDatabase()
        {
            try
            {
                // 创建表
                _db.CodeFirst.InitTables<ConversionTask>();
                _logger.LogInformation("数据库初始化完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "数据库初始化失败");
                throw;
            }
        }

        /// <summary>
        /// 创建转换任务
        /// </summary>
        public async Task<ConversionTask> CreateTaskAsync(ConversionTask task)
        {
            try
            {
                _logger.LogInformation("📝 开始插入任务到数据库");
                _logger.LogInformation("任务ID: {TaskId}", task.Id);
                _logger.LogInformation("任务名称: {TaskName}", task.TaskName);
                _logger.LogInformation("原始文件: {OriginalFileName}", task.OriginalFileName);
                _logger.LogInformation("输出格式: {OutputFormat}", task.OutputFormat);

                var startTime = DateTime.Now;
                await _db.Insertable(task).ExecuteCommandAsync();
                var duration = DateTime.Now - startTime;

                _logger.LogInformation("✅ 数据库插入成功: {TaskId} (耗时: {Duration}ms)", task.Id, duration.TotalMilliseconds);
                return task;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 数据库插入失败: {TaskId}", task.Id);
                _logger.LogError("错误详情: {ErrorMessage}", ex.Message);
                _logger.LogError("SQL错误: {SqlError}", ex.InnerException?.Message);
                throw;
            }
        }

        /// <summary>
        /// 更新转换任务
        /// </summary>
        public async Task<bool> UpdateTaskAsync(ConversionTask task)
        {
            try
            {
                var result = await _db.Updateable(task).ExecuteCommandAsync();
                _logger.LogDebug("更新转换任务: {TaskId}", task.Id);
                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新转换任务失败: {TaskId}", task.Id);
                throw;
            }
        }

        /// <summary>
        /// 更新任务进度
        /// </summary>
        public async Task<bool> UpdateTaskProgressAsync(string taskId, int progress, 
            double? currentTime = null, double? speed = null, int? estimatedTimeRemaining = null)
        {
            try
            {
                var updateColumns = new List<string> { nameof(ConversionTask.Progress) };
                var task = new ConversionTask { Id = taskId, Progress = progress };

                if (currentTime.HasValue)
                {
                    task.CurrentTime = currentTime.Value;
                    updateColumns.Add(nameof(ConversionTask.CurrentTime));
                }

                if (speed.HasValue)
                {
                    task.ConversionSpeed = speed.Value;
                    updateColumns.Add(nameof(ConversionTask.ConversionSpeed));
                }

                if (estimatedTimeRemaining.HasValue)
                {
                    task.EstimatedTimeRemaining = estimatedTimeRemaining.Value;
                    updateColumns.Add(nameof(ConversionTask.EstimatedTimeRemaining));
                }

                var result = await _db.Updateable(task)
                    .UpdateColumns(updateColumns.ToArray())
                    .ExecuteCommandAsync();

                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新任务进度失败: {TaskId}", taskId);
                throw;
            }
        }

        /// <summary>
        /// 更新任务状态
        /// </summary>
        public async Task<bool> UpdateTaskStatusAsync(string taskId, ConversionStatus status, string? errorMessage = null)
        {
            try
            {
                var task = new ConversionTask 
                { 
                    Id = taskId, 
                    Status = status,
                    ErrorMessage = errorMessage
                };

                var updateColumns = new List<string> { nameof(ConversionTask.Status) };

                if (status == ConversionStatus.Converting && !await GetTaskAsync(taskId).ContinueWith(t => t.Result?.StartedAt.HasValue ?? false))
                {
                    task.StartedAt = DateTime.Now;
                    updateColumns.Add(nameof(ConversionTask.StartedAt));
                }
                else if (status == ConversionStatus.Completed || status == ConversionStatus.Failed || status == ConversionStatus.Cancelled)
                {
                    task.CompletedAt = DateTime.Now;
                    updateColumns.Add(nameof(ConversionTask.CompletedAt));
                }

                if (!string.IsNullOrEmpty(errorMessage))
                {
                    updateColumns.Add(nameof(ConversionTask.ErrorMessage));
                }

                var result = await _db.Updateable(task)
                    .UpdateColumns(updateColumns.ToArray())
                    .ExecuteCommandAsync();

                _logger.LogInformation("更新任务状态: {TaskId} -> {Status}", taskId, status);
                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新任务状态失败: {TaskId}", taskId);
                throw;
            }
        }

        /// <summary>
        /// 获取转换任务
        /// </summary>
        public async Task<ConversionTask?> GetTaskAsync(string taskId)
        {
            try
            {
                return await _db.Queryable<ConversionTask>()
                    .Where(t => t.Id == taskId)
                    .FirstAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取转换任务失败: {TaskId}", taskId);
                throw;
            }
        }

        /// <summary>
        /// 获取所有转换任务
        /// </summary>
        public async Task<List<ConversionTask>> GetAllTasksAsync(int pageIndex = 1, int pageSize = 50)
        {
            try
            {
                return await _db.Queryable<ConversionTask>()
                    .OrderBy(t => t.CreatedAt, OrderByType.Desc)
                    .ToPageListAsync(pageIndex, pageSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取转换任务列表失败");
                throw;
            }
        }

        /// <summary>
        /// 获取正在进行的任务
        /// </summary>
        public async Task<List<ConversionTask>> GetActiveTasksAsync()
        {
            try
            {
                return await _db.Queryable<ConversionTask>()
                    .Where(t => t.Status == ConversionStatus.Pending || t.Status == ConversionStatus.Converting)
                    .OrderBy(t => t.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取活动任务失败");
                throw;
            }
        }

        /// <summary>
        /// 删除转换任务
        /// </summary>
        public async Task<bool> DeleteTaskAsync(string taskId)
        {
            try
            {
                var result = await _db.Deleteable<ConversionTask>()
                    .Where(t => t.Id == taskId)
                    .ExecuteCommandAsync();

                _logger.LogInformation("删除转换任务: {TaskId}", taskId);
                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除转换任务失败: {TaskId}", taskId);
                throw;
            }
        }

        /// <summary>
        /// 清理旧任务
        /// </summary>
        public async Task<int> CleanupOldTasksAsync(int daysOld = 30)
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-daysOld);
                var result = await _db.Deleteable<ConversionTask>()
                    .Where(t => t.CreatedAt < cutoffDate && 
                               (t.Status == ConversionStatus.Completed || 
                                t.Status == ConversionStatus.Failed || 
                                t.Status == ConversionStatus.Cancelled))
                    .ExecuteCommandAsync();

                _logger.LogInformation("清理了 {Count} 个旧任务", result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理旧任务失败");
                throw;
            }
        }
    }
}
