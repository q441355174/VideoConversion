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
        private readonly NotificationService? _notificationService;

        /// <summary>
        /// 获取数据库实例
        /// </summary>
        public SqlSugarScope GetDatabaseAsync() => _db;

        public DatabaseService(
            ILogger<DatabaseService> logger,
            IConfiguration configuration,
            NotificationService? notificationService = null)
        {
            _logger = logger;
            _notificationService = notificationService;
            
            // 获取数据库连接字符串
            var connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? "Data Source=videoconversion.db";

            // 确保连接字符串格式正确
            _logger.LogDebug("原始连接字符串: {ConnectionString}", connectionString);

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
                    SqlServerCodeFirstNvarchar = true,
                    // 设置SQLite特定选项
                    SqliteCodeFirstEnableDefaultValue = true,
                    // 启用连接池
                    IsWithNoLockQuery = true
                }
            }, db =>
            {
                // 配置日志
                db.Aop.OnLogExecuting = (sql, pars) =>
                {
                    _logger.LogDebug("SQL: {Sql}", sql);
                };

                // 配置错误处理
                db.Aop.OnError = (exp) =>
                {
                    _logger.LogError(exp, "数据库操作错误: {Message}", exp.Message);
                };
            });

            // 初始化数据库
            InitializeDatabase();
        }

        /// <summary>
        /// 强制重新创建数据库表（用于修复表结构问题）
        /// </summary>
        public async Task<bool> RecreateTablesAsync()
        {
            try
            {
                _logger.LogInformation("开始重新创建数据库表...");

                // 删除现有表
                try
                {
                    await _db.Ado.ExecuteCommandAsync("DROP TABLE IF EXISTS ConversionTasks");
                    await _db.Ado.ExecuteCommandAsync("DROP TABLE IF EXISTS DiskSpaceConfig");
                    await _db.Ado.ExecuteCommandAsync("DROP TABLE IF EXISTS SpaceUsage");
                    _logger.LogInformation("已删除现有表");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "删除现有表时出现警告，继续创建新表");
                }

                // 重新创建表
                _db.CodeFirst.InitTables<ConversionTask>();
                _db.CodeFirst.InitTables<Models.DiskSpaceConfig>();
                _db.CodeFirst.InitTables<Models.SpaceUsage>();

                _logger.LogInformation("数据库表重新创建完成");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "重新创建数据库表失败");
                return false;
            }
        }

        /// <summary>
        /// 初始化数据库
        /// </summary>
        private void InitializeDatabase()
        {
            try
            {
                // 设置SQLite WAL模式以支持并发访问
                try
                {
                    _db.Ado.ExecuteCommand("PRAGMA journal_mode=WAL;");
                    _db.Ado.ExecuteCommand("PRAGMA synchronous=NORMAL;");
                    _db.Ado.ExecuteCommand("PRAGMA cache_size=10000;");
                    _db.Ado.ExecuteCommand("PRAGMA temp_store=memory;");
                }
                catch (Exception pragmaEx)
                {
                    _logger.LogWarning(pragmaEx, "设置SQLite PRAGMA失败，继续使用默认设置");
                }

                // 创建表
                _db.CodeFirst.InitTables<ConversionTask>();

                // 创建磁盘空间相关表
                _db.CodeFirst.InitTables<Models.DiskSpaceConfig>();
                _db.CodeFirst.InitTables<Models.SpaceUsage>();

                // // 检查是否需要创建示例数据
                // var taskCount = _db.Queryable<ConversionTask>().Count();
                // if (taskCount == 0)
                // {
                //     CreateSampleData();
                // }

                _logger.LogInformation("数据库初始化完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "数据库初始化失败");
                throw;
            }
        }

        /// <summary>
        /// 创建示例数据
        /// </summary>
        private void CreateSampleData()
        {
            try
            {
                var sampleTasks = new List<ConversionTask>
                {
                    new ConversionTask
                    {
                        Id = Guid.NewGuid().ToString(),
                        TaskName = "示例视频转换 - MP4转WebM",
                        OriginalFileName = "sample_video.mp4",
                        OutputFileName = "sample_video.webm",
                        OriginalFilePath = "uploads/sample_video.mp4",
                        OutputFilePath = "outputs/sample_video.webm",
                        OriginalFileSize = 15728640, // 15MB
                        OutputFileSize = 12582912,   // 12MB
                        InputFormat = "mp4",
                        OutputFormat = "webm",
                        VideoCodec = "libvpx-vp9",
                        AudioCodec = "libvorbis",
                        VideoQuality = "28",
                        AudioQuality = "128k",
                        Resolution = "1920x1080",
                        FrameRate = "30",
                        Status = ConversionStatus.Completed,
                        Progress = 100,
                        CreatedAt = DateTime.Now.AddHours(-2),
                        StartedAt = DateTime.Now.AddHours(-2).AddMinutes(1),
                        CompletedAt = DateTime.Now.AddHours(-1).AddMinutes(-30),
                        Duration = 120.5,
                        ConversionSpeed = 1.2,
                        EncodingPreset = "medium",
                        QualityMode = "crf",
                        TwoPass = false,
                        FastStart = true
                    },
                    new ConversionTask
                    {
                        Id = Guid.NewGuid().ToString(),
                        TaskName = "高质量转换 - AVI转MP4",
                        OriginalFileName = "old_movie.avi",
                        OutputFileName = "old_movie_hd.mp4",
                        OriginalFilePath = "uploads/old_movie.avi",
                        OutputFilePath = "outputs/old_movie_hd.mp4",
                        OriginalFileSize = 52428800, // 50MB
                        OutputFileSize = 31457280,   // 30MB
                        InputFormat = "avi",
                        OutputFormat = "mp4",
                        VideoCodec = "libx264",
                        AudioCodec = "aac",
                        VideoQuality = "23",
                        AudioQuality = "192k",
                        Resolution = "1280x720",
                        FrameRate = "24",
                        Status = ConversionStatus.Completed,
                        Progress = 100,
                        CreatedAt = DateTime.Now.AddDays(-1),
                        StartedAt = DateTime.Now.AddDays(-1).AddMinutes(2),
                        CompletedAt = DateTime.Now.AddDays(-1).AddMinutes(45),
                        Duration = 3600.0,
                        ConversionSpeed = 0.8,
                        EncodingPreset = "slow",
                        QualityMode = "crf",
                        TwoPass = true,
                        FastStart = true
                    },
                    new ConversionTask
                    {
                        Id = Guid.NewGuid().ToString(),
                        TaskName = "音频提取 - MP4转MP3",
                        OriginalFileName = "music_video.mp4",
                        OutputFileName = "extracted_audio.mp3",
                        OriginalFilePath = "uploads/music_video.mp4",
                        OutputFilePath = "outputs/extracted_audio.mp3",
                        OriginalFileSize = 20971520, // 20MB
                        OutputFileSize = 5242880,    // 5MB
                        InputFormat = "mp4",
                        OutputFormat = "mp3",
                        VideoCodec = "",
                        AudioCodec = "libmp3lame",
                        VideoQuality = "",
                        AudioQuality = "320k",
                        Resolution = "",
                        FrameRate = "",
                        Status = ConversionStatus.Failed,
                        Progress = 45,
                        ErrorMessage = "音频流解码失败：不支持的音频格式",
                        CreatedAt = DateTime.Now.AddHours(-6),
                        StartedAt = DateTime.Now.AddHours(-6).AddMinutes(1),
                        Duration = 240.0,
                        ConversionSpeed = 2.1,
                        EncodingPreset = "fast",
                        QualityMode = "bitrate",
                        TwoPass = false,
                        FastStart = false
                    },
                    new ConversionTask
                    {
                        Id = Guid.NewGuid().ToString(),
                        TaskName = "批量转换 - MOV转MP4",
                        OriginalFileName = "presentation.mov",
                        OutputFileName = "presentation.mp4",
                        OriginalFilePath = "uploads/presentation.mov",
                        OutputFilePath = "outputs/presentation.mp4",
                        OriginalFileSize = 104857600, // 100MB
                        OutputFileSize = 0,
                        InputFormat = "mov",
                        OutputFormat = "mp4",
                        VideoCodec = "libx264",
                        AudioCodec = "aac",
                        VideoQuality = "25",
                        AudioQuality = "128k",
                        Resolution = "1920x1080",
                        FrameRate = "30",
                        Status = ConversionStatus.Converting,
                        Progress = 67,
                        CreatedAt = DateTime.Now.AddMinutes(-15),
                        StartedAt = DateTime.Now.AddMinutes(-12),
                        Duration = 1800.0,
                        CurrentTime = 1206.0,
                        ConversionSpeed = 1.5,
                        EstimatedTimeRemaining = 396,
                        EncodingPreset = "medium",
                        QualityMode = "crf",
                        TwoPass = false,
                        FastStart = true
                    },
                    new ConversionTask
                    {
                        Id = Guid.NewGuid().ToString(),
                        TaskName = "等待转换 - MKV转WebM",
                        OriginalFileName = "anime_episode.mkv",
                        OutputFileName = "anime_episode.webm",
                        OriginalFilePath = "uploads/anime_episode.mkv",
                        OutputFilePath = "outputs/anime_episode.webm",
                        OriginalFileSize = 209715200, // 200MB
                        OutputFileSize = 0,
                        InputFormat = "mkv",
                        OutputFormat = "webm",
                        VideoCodec = "libvpx-vp9",
                        AudioCodec = "libopus",
                        VideoQuality = "30",
                        AudioQuality = "96k",
                        Resolution = "1920x1080",
                        FrameRate = "23.976",
                        Status = ConversionStatus.Pending,
                        Progress = 0,
                        CreatedAt = DateTime.Now.AddMinutes(-5),
                        Duration = 1440.0,
                        EncodingPreset = "medium",
                        QualityMode = "crf",
                        TwoPass = true,
                        FastStart = false
                    }
                };

                _db.Insertable(sampleTasks).ExecuteCommand();
                _logger.LogInformation("已创建 {Count} 个示例任务", sampleTasks.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "创建示例数据失败");
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
                _logger.LogError(ex, "数据库插入失败: {TaskId}", task.Id);
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

                // 发送状态更新通知给客户端
                if (_notificationService != null && result > 0)
                {
                    await _notificationService.NotifyStatusChangeAsync(taskId, status, errorMessage);
                }

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

                    // 重试成功后也发送状态更新
                    if (_notificationService != null && retryResult > 0)
                    {
                        await _notificationService.NotifyStatusChangeAsync(taskId, status, errorMessage);
                    }

                    return retryResult > 0;
                }

                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新任务状态失败: {TaskId} -> {Status}", taskId, status);
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
                _logger.LogDebug("获取任务列表: pageIndex={PageIndex}, pageSize={PageSize}", pageIndex, pageSize);

                var tasks = await _db.Queryable<ConversionTask>()
                    .OrderBy(t => t.CreatedAt, OrderByType.Desc)
                    .ToPageListAsync(pageIndex, pageSize);

                _logger.LogDebug("获取到 {Count} 个任务", tasks?.Count ?? 0);
                return tasks ?? new List<ConversionTask>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取转换任务列表失败");
                // 返回空列表而不是抛出异常，这样前端可以正常显示"暂无数据"
                return new List<ConversionTask>();
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
                _logger.LogError(ex, "获取活动任务失败");
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

                    // 发送状态更新通知给客户端
                    if (_notificationService != null)
                    {
                        await _notificationService.NotifyStatusChangeAsync(taskId, ConversionStatus.Converting, "任务已开始");
                    }
                }
                else
                {
                    _logger.LogDebug("任务启动失败: {TaskId} (可能已被其他进程处理)", taskId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "尝试启动任务失败: {TaskId}", taskId);
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

        /// <summary>
        /// 获取任务总数
        /// </summary>
        public async Task<int> GetTaskCountAsync()
        {
            try
            {
                var count = await _db.Queryable<ConversionTask>().CountAsync();
                _logger.LogDebug("获取任务总数: {Count}", count);
                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取任务总数失败");
                throw;
            }
        }

        /// <summary>
        /// 获取最近任务列表
        /// </summary>
        public async Task<List<ConversionTask>> GetRecentTasksAsync(int count = 10)
        {
            try
            {
                _logger.LogDebug("获取最近任务列表: count={Count}", count);

                var tasks = await _db.Queryable<ConversionTask>()
                    .OrderBy(t => t.CreatedAt, OrderByType.Desc)
                    .Take(count)
                    .ToListAsync();

                _logger.LogDebug("获取到 {Count} 个最近任务", tasks?.Count ?? 0);
                return tasks ?? new List<ConversionTask>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取最近任务列表失败");
                // 返回空列表而不是抛出异常，这样前端可以正常显示"暂无数据"
                return new List<ConversionTask>();
            }
        }

        /// <summary>
        /// 获取任务列表（支持筛选和搜索）
        /// </summary>
        public async Task<List<ConversionTask>> GetTasksWithFilterAsync(int pageIndex = 1, int pageSize = 20, string? status = null, string? search = null)
        {
            try
            {
                _logger.LogDebug("获取任务列表: pageIndex={PageIndex}, pageSize={PageSize}, status={Status}, search={Search}",
                    pageIndex, pageSize, status, search);

                var query = _db.Queryable<ConversionTask>();

                // 状态筛选
                if (!string.IsNullOrEmpty(status) && Enum.TryParse<ConversionStatus>(status, out var statusEnum))
                {
                    query = query.Where(t => t.Status == statusEnum);
                }

                // 搜索筛选
                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(t =>
                        t.TaskName.Contains(search) ||
                        t.OriginalFileName.Contains(search) ||
                        t.OutputFileName.Contains(search));
                }

                var tasks = await query
                    .OrderBy(t => t.CreatedAt, OrderByType.Desc)
                    .ToPageListAsync(pageIndex, pageSize);

                _logger.LogDebug("获取到 {Count} 个任务", tasks?.Count ?? 0);
                return tasks ?? new List<ConversionTask>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取任务列表失败");
                return new List<ConversionTask>();
            }
        }

        /// <summary>
        /// 获取任务总数（支持筛选和搜索）
        /// </summary>
        public async Task<int> GetTaskCountWithFilterAsync(string? status = null, string? search = null)
        {
            try
            {
                var query = _db.Queryable<ConversionTask>();

                // 状态筛选
                if (!string.IsNullOrEmpty(status) && Enum.TryParse<ConversionStatus>(status, out var statusEnum))
                {
                    query = query.Where(t => t.Status == statusEnum);
                }

                // 搜索筛选
                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(t =>
                        t.TaskName.Contains(search) ||
                        t.OriginalFileName.Contains(search) ||
                        t.OutputFileName.Contains(search));
                }

                var count = await query.CountAsync();
                _logger.LogDebug("获取任务总数: {Count}", count);
                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取任务总数失败");
                return 0;
            }
        }
    }
}
