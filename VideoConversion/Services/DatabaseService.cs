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
                InitKeyType = InitKeyType.Attribute,
                // 添加事务和并发控制配置
                ConfigureExternalServices = new ConfigureExternalServices()
                {
                    // 禁用缓存以确保数据一致性
                    DataInfoCacheService = null,
                },
                // 设置更严格的事务隔离级别
                MoreSettings = new ConnMoreSettings()
                {
                    // 禁用查询缓存
                    IsAutoRemoveDataCache = true,
                    // 设置命令超时
                    SqlServerCodeFirstNvarchar = true
                }
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
                _logger.LogDebug("开始插入任务到数据库: {TaskId} - {TaskName}", task.Id, task.TaskName);

                var startTime = DateTime.Now;
                await _db.Insertable(task).ExecuteCommandAsync();
                var duration = DateTime.Now - startTime;

                _logger.LogInformation("任务创建成功: {TaskId} - {TaskName}", task.Id, task.TaskName);
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
                _logger.LogDebug("更新任务状态: {TaskId} -> {Status}", taskId, status);

                // 构建更新字段
                var updateFields = new Dictionary<string, object>
                {
                    [nameof(ConversionTask.Status)] = status
                };

                // 根据状态设置时间字段
                if (status == ConversionStatus.Converting)
                {
                    updateFields[nameof(ConversionTask.StartedAt)] = DateTime.Now;
                }
                else if (status == ConversionStatus.Completed || status == ConversionStatus.Failed || status == ConversionStatus.Cancelled)
                {
                    updateFields[nameof(ConversionTask.CompletedAt)] = DateTime.Now;
                }

                if (!string.IsNullOrEmpty(errorMessage))
                {
                    updateFields[nameof(ConversionTask.ErrorMessage)] = errorMessage;
                }

                // 使用原生SQL确保可靠性
                var sql = "UPDATE ConversionTasks SET ";
                var parameters = new List<SugarParameter>();
                var setParts = new List<string>();

                int paramIndex = 0;
                foreach (var field in updateFields)
                {
                    var paramName = $"@param{paramIndex}";
                    setParts.Add($"{field.Key} = {paramName}");
                    parameters.Add(new SugarParameter(paramName, field.Value));
                    paramIndex++;
                }

                sql += string.Join(", ", setParts);
                sql += " WHERE Id = @taskId";
                parameters.Add(new SugarParameter("@taskId", taskId));

                _logger.LogDebug("执行SQL: {Sql}", sql);

                // 执行更新
                var result = await _db.Ado.ExecuteCommandAsync(sql, parameters);

                _logger.LogInformation("任务状态更新: {TaskId} -> {Status}", taskId, status);

                // 强制验证更新结果 - 使用原生SQL确保一致性
                await Task.Delay(200); // 更长延迟确保数据库写入完成

                var verifySql = "SELECT * FROM ConversionTasks WHERE Id = @taskId";
                var verifyParams = new List<SugarParameter> { new SugarParameter("@taskId", taskId) };
                var verifyTasks = await _db.Ado.SqlQueryAsync<ConversionTask>(verifySql, verifyParams);
                var verifyTask = verifyTasks.FirstOrDefault();

                if (verifyTask != null && verifyTask.Status != status)
                {
                    _logger.LogWarning("状态验证失败，期望: {ExpectedStatus}, 实际: {ActualStatus}", status, verifyTask.Status);

                    // 重试一次更新
                    var retryResult = await _db.Ado.ExecuteCommandAsync(sql, parameters);
                    _logger.LogDebug("重试更新结果: 影响行数 {RetryResult}", retryResult);

                    return retryResult > 0;
                }

                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 更新任务状态失败: {TaskId} -> {Status}", taskId, status);
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
                // 使用原生SQL确保数据一致性
                var sql = @"SELECT * FROM ConversionTasks
                           WHERE Status IN (@pendingStatus, @convertingStatus)
                           ORDER BY CreatedAt";

                var parameters = new List<SugarParameter>
                {
                    new SugarParameter("@pendingStatus", ConversionStatus.Pending),
                    new SugarParameter("@convertingStatus", ConversionStatus.Converting)
                };

                var tasks = await _db.Ado.SqlQueryAsync<ConversionTask>(sql, parameters);

                if (tasks.Any())
                {
                    _logger.LogDebug("查询到 {Count} 个活动任务", tasks.Count);
                }

                return tasks.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 获取活动任务失败");
                throw;
            }
        }

        /// <summary>
        /// 原子性地将任务从Pending状态更新为Converting状态
        /// 这可以防止多个进程同时处理同一个任务
        /// </summary>
        public async Task<bool> TryStartTaskAsync(string taskId)
        {
            try
            {
                _logger.LogDebug("尝试启动任务: {TaskId}", taskId);

                // 使用原生SQL确保原子性：只有当状态为Pending时才更新为Converting
                var sql = @"UPDATE ConversionTasks
                           SET Status = @convertingStatus, StartedAt = @startedAt
                           WHERE Id = @taskId AND Status = @pendingStatus";

                var parameters = new List<SugarParameter>
                {
                    new SugarParameter("@convertingStatus", ConversionStatus.Converting),
                    new SugarParameter("@startedAt", DateTime.Now),
                    new SugarParameter("@taskId", taskId),
                    new SugarParameter("@pendingStatus", ConversionStatus.Pending)
                };

                var result = await _db.Ado.ExecuteCommandAsync(sql, parameters);
                var success = result > 0;

                if (success)
                {
                    _logger.LogDebug("任务启动成功: {TaskId}", taskId);
                }
                else
                {
                    _logger.LogDebug("任务启动失败: {TaskId} (可能已被其他进程处理)", taskId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 尝试启动任务失败: {TaskId}", taskId);
                return false;
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
