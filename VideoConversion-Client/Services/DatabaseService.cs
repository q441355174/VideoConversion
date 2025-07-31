using SqlSugar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using VideoConversion_Client.Models;

namespace VideoConversion_Client.Services
{
    /// <summary>
    /// 数据库服务，管理SQLite数据库连接和操作
    /// </summary>
    public class DatabaseService
    {
        private static DatabaseService? _instance;
        private static readonly object _lock = new object();
        private readonly SqlSugarScope _db;

        public static DatabaseService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new DatabaseService();
                        }
                    }
                }
                return _instance;
            }
        }

        private DatabaseService()
        {
            // 获取程序根目录
            var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var dbPath = Path.Combine(appDirectory, "VideoConversion.db");

            // 配置SqlSugar
            var config = new ConnectionConfig()
            {
                ConnectionString = $"Data Source={dbPath}",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            };

            _db = new SqlSugarScope(config);

            // 初始化数据库表
            InitializeDatabase();
        }

        /// <summary>
        /// 获取数据库实例
        /// </summary>
        public SqlSugarScope GetDatabase()
        {
            return _db;
        }

        /// <summary>
        /// 初始化数据库表结构
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
                    System.Diagnostics.Debug.WriteLine($"设置SQLite PRAGMA失败，继续使用默认设置: {pragmaEx.Message}");
                }

                // 创建系统设置表
                _db.CodeFirst.InitTables<SystemSettingsEntity>();

                // 🔑 创建本地转换任务表
                _db.CodeFirst.InitTables<LocalConversionTask>();

                System.Diagnostics.Debug.WriteLine("数据库初始化完成，包含LocalConversionTask表");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"数据库初始化失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 获取系统设置
        /// </summary>
        public SystemSettingsEntity? GetSystemSettings()
        {
            try
            {
                return _db.Queryable<SystemSettingsEntity>()
                         .OrderByDescending(x => x.UpdateTime)
                         .First();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取系统设置失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 保存系统设置
        /// </summary>
        public bool SaveSystemSettings(SystemSettingsEntity settings)
        {
            try
            {
                // 删除旧设置（保持只有一条记录）
                _db.Deleteable<SystemSettingsEntity>().ExecuteCommand();
                
                // 插入新设置
                settings.UpdateTime = DateTime.Now;
                var result = _db.Insertable(settings).ExecuteCommand();
                
                System.Diagnostics.Debug.WriteLine($"保存系统设置成功，影响行数: {result}");
                return result > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存系统设置失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 更新系统设置
        /// </summary>
        public bool UpdateSystemSettings(SystemSettingsEntity settings)
        {
            try
            {
                settings.UpdateTime = DateTime.Now;
                var result = _db.Updateable(settings).ExecuteCommand();
                
                System.Diagnostics.Debug.WriteLine($"更新系统设置成功，影响行数: {result}");
                return result > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新系统设置失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 检查数据库连接
        /// </summary>
        public bool TestConnection()
        {
            try
            {
                var result = _db.Ado.GetString("SELECT 'OK'");
                return result == "OK";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"数据库连接测试失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取数据库文件路径
        /// </summary>
        public string GetDatabasePath()
        {
            var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(appDirectory, "VideoConversion.db");
        }

        /// <summary>
        /// 获取数据库文件大小
        /// </summary>
        public long GetDatabaseSize()
        {
            try
            {
                var dbPath = GetDatabasePath();
                if (File.Exists(dbPath))
                {
                    var fileInfo = new FileInfo(dbPath);
                    return fileInfo.Length;
                }
                return 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取数据库大小失败: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// 备份数据库
        /// </summary>
        public bool BackupDatabase(string backupPath)
        {
            try
            {
                var dbPath = GetDatabasePath();
                if (File.Exists(dbPath))
                {
                    File.Copy(dbPath, backupPath, true);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"备份数据库失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 恢复数据库
        /// </summary>
        public bool RestoreDatabase(string backupPath)
        {
            try
            {
                if (File.Exists(backupPath))
                {
                    var dbPath = GetDatabasePath();
                    File.Copy(backupPath, dbPath, true);
                    
                    // 重新初始化数据库连接
                    InitializeDatabase();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"恢复数据库失败: {ex.Message}");
                return false;
            }
        }

        #region LocalConversionTask 相关方法

        /// <summary>
        /// 保存本地任务列表
        /// </summary>
        public async Task SaveLocalTasksAsync(List<LocalConversionTask> tasks)
        {
            try
            {
                // 确保表存在
                _db.CodeFirst.InitTables<LocalConversionTask>();

                await _db.Insertable(tasks).ExecuteCommandAsync();
                System.Diagnostics.Debug.WriteLine($"✅ 保存了 {tasks.Count} 个本地任务");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 保存本地任务失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 更新服务器TaskId映射
        /// </summary>
        public async Task UpdateServerTaskMappingAsync(string localId, string serverTaskId, string? batchId = null)
        {
            try
            {
                var updateData = new Dictionary<string, object>
                {
                    ["ServerTaskId"] = serverTaskId,
                    ["CurrentTaskId"] = serverTaskId, // 切换到服务器TaskId
                    ["ServerTaskCreatedAt"] = DateTime.Now
                };

                if (!string.IsNullOrEmpty(batchId))
                {
                    updateData["BatchId"] = batchId;
                }

                var sql = $@"
                    UPDATE LocalConversionTasks
                    SET {string.Join(", ", updateData.Keys.Select(k => $"{k} = @{k}"))}
                    WHERE LocalId = @LocalId";

                updateData["LocalId"] = localId;

                await _db.Ado.ExecuteCommandAsync(sql, updateData);

                System.Diagnostics.Debug.WriteLine($"✅ TaskId映射更新成功: {localId} -> {serverTaskId}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 更新TaskId映射失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 更新任务进度（统一接口）
        /// </summary>
        public async Task UpdateTaskProgressAsync(string taskId, double progress, string phase,
            double? speed = null, double? eta = null, string? progressHistory = null)
        {
            try
            {
                var updateData = new
                {
                    Progress = (int)Math.Round(progress),
                    CurrentPhase = phase,
                    ConversionSpeed = speed,
                    EstimatedTimeRemaining = eta.HasValue ? (int)eta.Value : (int?)null,
                    ProgressHistory = progressHistory
                };

                // 根据阶段更新时间戳
                var timeUpdates = new Dictionary<string, object>();
                switch (phase.ToLower())
                {
                    case "uploading":
                        if (progress <= 1) timeUpdates["UploadStartedAt"] = DateTime.Now;
                        break;
                    case "upload_completed":
                        timeUpdates["UploadCompletedAt"] = DateTime.Now;
                        break;
                    case "converting":
                        if (progress <= 1) timeUpdates["ConversionStartedAt"] = DateTime.Now;
                        break;
                    case "completed":
                        timeUpdates["CompletedAt"] = DateTime.Now;
                        break;
                }

                // 构建完整的更新数据
                var allUpdates = new Dictionary<string, object>(timeUpdates);
                foreach (var prop in updateData.GetType().GetProperties())
                {
                    var value = prop.GetValue(updateData);
                    if (value != null)
                    {
                        allUpdates[prop.Name] = value;
                    }
                }

                // 执行更新（支持LocalTaskId和ServerTaskId）
                var sql = $@"
                    UPDATE LocalConversionTasks
                    SET {string.Join(", ", allUpdates.Keys.Select(k => $"{k} = @{k}"))}
                    WHERE LocalId = @TaskId OR ServerTaskId = @TaskId OR CurrentTaskId = @TaskId";

                allUpdates["TaskId"] = taskId;

                await _db.Ado.ExecuteCommandAsync(sql, allUpdates);

                System.Diagnostics.Debug.WriteLine($"✅ 任务进度更新成功: {taskId} -> {phase} {progress:F1}%");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 更新任务进度失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 根据任意TaskId获取本地任务
        /// </summary>
        public async Task<LocalConversionTask?> GetLocalTaskByAnyIdAsync(string taskId)
        {
            try
            {
                var sql = @"
                    SELECT * FROM LocalConversionTasks
                    WHERE LocalId = @TaskId OR ServerTaskId = @TaskId OR CurrentTaskId = @TaskId
                    LIMIT 1";

                return await _db.Ado.SqlQuerySingleAsync<LocalConversionTask>(sql, new { TaskId = taskId });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 获取本地任务失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 更新任务状态
        /// </summary>
        public async Task UpdateTaskStatusAsync(string taskId, ConversionStatus status, string? errorMessage = null)
        {
            try
            {
                var updateData = new Dictionary<string, object>
                {
                    ["Status"] = status
                };

                if (!string.IsNullOrEmpty(errorMessage))
                {
                    updateData["ErrorMessage"] = errorMessage;
                    updateData["LastError"] = errorMessage;
                }

                if (status == ConversionStatus.Failed)
                {
                    updateData["LastRetryAt"] = DateTime.Now;
                }

                var sql = $@"
                    UPDATE LocalConversionTasks
                    SET {string.Join(", ", updateData.Keys.Select(k => $"{k} = @{k}"))}
                    WHERE LocalId = @TaskId OR ServerTaskId = @TaskId OR CurrentTaskId = @TaskId";

                updateData["TaskId"] = taskId;

                await _db.Ado.ExecuteCommandAsync(sql, updateData);

                System.Diagnostics.Debug.WriteLine($"✅ 任务状态更新成功: {taskId} -> {status}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 更新任务状态失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 更新任务下载状态
        /// </summary>
        public async Task UpdateTaskDownloadStatusAsync(string taskId, string localOutputPath)
        {
            try
            {
                var updateData = new Dictionary<string, object>
                {
                    ["IsDownloaded"] = true,
                    ["LocalOutputPath"] = localOutputPath,
                    ["DownloadedAt"] = DateTime.Now
                };

                var sql = $@"
                    UPDATE LocalConversionTasks
                    SET {string.Join(", ", updateData.Keys.Select(k => $"{k} = @{k}"))}
                    WHERE LocalId = @TaskId OR ServerTaskId = @TaskId OR CurrentTaskId = @TaskId";

                updateData["TaskId"] = taskId;

                await _db.Ado.ExecuteCommandAsync(sql, updateData);

                System.Diagnostics.Debug.WriteLine($"✅ 任务下载状态更新成功: {taskId}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 更新任务下载状态失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 更新源文件处理状态
        /// </summary>
        public async Task UpdateSourceFileProcessingAsync(string localId, string action, string? archivePath = null)
        {
            try
            {
                var updateData = new Dictionary<string, object>
                {
                    ["SourceFileProcessed"] = true,
                    ["SourceFileAction"] = action,
                    ["SourceFileProcessedAt"] = DateTime.Now
                };

                if (!string.IsNullOrEmpty(archivePath))
                {
                    updateData["ArchivePath"] = archivePath;
                }

                var sql = $@"
                    UPDATE LocalConversionTasks
                    SET {string.Join(", ", updateData.Keys.Select(k => $"{k} = @{k}"))}
                    WHERE LocalId = @LocalId";

                updateData["LocalId"] = localId;

                await _db.Ado.ExecuteCommandAsync(sql, updateData);

                System.Diagnostics.Debug.WriteLine($"✅ 源文件处理状态更新成功: {localId} -> {action}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 更新源文件处理状态失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 获取所有本地任务
        /// </summary>
        public async Task<List<LocalConversionTask>> GetAllLocalTasksAsync()
        {
            try
            {
                return await _db.Queryable<LocalConversionTask>()
                              .OrderByDescending(t => t.CreatedAt)
                              .ToListAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 获取所有本地任务失败: {ex.Message}");
                return new List<LocalConversionTask>();
            }
        }

        /// <summary>
        /// 根据状态获取本地任务
        /// </summary>
        public async Task<List<LocalConversionTask>> GetLocalTasksByStatusAsync(ConversionStatus status)
        {
            try
            {
                return await _db.Queryable<LocalConversionTask>()
                              .Where(t => t.Status == status)
                              .OrderByDescending(t => t.CreatedAt)
                              .ToListAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 根据状态获取本地任务失败: {ex.Message}");
                return new List<LocalConversionTask>();
            }
        }

        /// <summary>
        /// 删除本地任务
        /// </summary>
        public async Task DeleteLocalTaskAsync(string taskId)
        {
            try
            {
                var sql = @"
                    DELETE FROM LocalConversionTasks
                    WHERE LocalId = @TaskId OR ServerTaskId = @TaskId OR CurrentTaskId = @TaskId";

                await _db.Ado.ExecuteCommandAsync(sql, new { TaskId = taskId });

                System.Diagnostics.Debug.WriteLine($"✅ 本地任务删除成功: {taskId}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 删除本地任务失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 清理已完成的旧任务
        /// </summary>
        public async Task CleanupOldCompletedTasksAsync(int daysToKeep = 30)
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-daysToKeep);

                var sql = @"
                    DELETE FROM LocalConversionTasks
                    WHERE Status = @CompletedStatus
                    AND CompletedAt < @CutoffDate";

                var deletedCount = await _db.Ado.ExecuteCommandAsync(sql, new
                {
                    CompletedStatus = ConversionStatus.Completed,
                    CutoffDate = cutoffDate
                });

                System.Diagnostics.Debug.WriteLine($"✅ 清理了 {deletedCount} 个旧的已完成任务");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 清理旧任务失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 数据库健康检查
        /// </summary>
        public async Task HealthCheckAsync()
        {
            try
            {
                // 简单的查询测试数据库连接
                await _db.Ado.GetIntAsync("SELECT COUNT(*) FROM sqlite_master WHERE type='table'");
                System.Diagnostics.Debug.WriteLine("✅ 数据库健康检查通过");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 数据库健康检查失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 重置数据库连接
        /// </summary>
        public async Task ResetConnectionAsync()
        {
            try
            {
                // 重新初始化数据库
                InitializeDatabase();
                await HealthCheckAsync();
                System.Diagnostics.Debug.WriteLine("✅ 数据库连接重置成功");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 重置数据库连接失败: {ex.Message}");
                throw;
            }
        }

        #endregion

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _db?.Dispose();
        }
    }
}
