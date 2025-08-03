using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SqlSugar;
using VideoConversion_ClientTo.Domain.Entities;
using VideoConversion_ClientTo.Infrastructure.Data.Entities;
using VideoConversion_ClientTo.Infrastructure;

namespace VideoConversion_ClientTo.Infrastructure.Services
{
    /// <summary>
    /// SqlSugar数据库服务 - 与Client项目完全一致
    /// </summary>
    public class SqlSugarDatabaseService : IDatabaseService, IDisposable
    {
        private readonly SqlSugarScope _db;
        private static SqlSugarDatabaseService? _instance;
        private static readonly object _lock = new object();

        /// <summary>
        /// 单例实例
        /// </summary>
        public static SqlSugarDatabaseService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new SqlSugarDatabaseService();
                        }
                    }
                }
                return _instance;
            }
        }

        private SqlSugarDatabaseService()
        {
            // 获取程序根目录 - 与Client项目一致
            var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var dbPath = Path.Combine(appDirectory, "VideoConversion.db");

            // 配置SqlSugar - 与Client项目一致
            var config = new ConnectionConfig()
            {
                ConnectionString = $"Data Source={dbPath}",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            };

            _db = new SqlSugarScope(config);

            // 初始化数据库表（异步执行）
            _ = Task.Run(async () => await InitializeDatabaseAsync());
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
        private async Task InitializeDatabaseAsync()
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
                    Utils.Logger.Warning("SqlSugarDatabaseService", $"设置SQLite PRAGMA失败，继续使用默认设置: {pragmaEx.Message}");
                }

                // 创建系统设置表
                _db.CodeFirst.InitTables<SystemSettingsEntity>();

                // 🔑 创建本地转换任务表
                _db.CodeFirst.InitTables<LocalConversionTaskEntity>();

                // 🔧 确保默认设置记录存在
                await EnsureDefaultSettingsExist();

                Utils.Logger.Info("SqlSugarDatabaseService", "数据库初始化完成，包含LocalConversionTask表和默认设置");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SqlSugarDatabaseService", $"数据库初始化失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 初始化数据库
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                Utils.Logger.Info("SqlSugarDatabaseService", "🔄 初始化数据库");
                // SqlSugar的初始化在构造函数中已完成
                await Task.CompletedTask;
                Utils.Logger.Info("SqlSugarDatabaseService", "✅ 数据库初始化完成");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SqlSugarDatabaseService", $"❌ 数据库初始化失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 获取任务
        /// </summary>
        public async Task<LocalConversionTaskEntity?> GetTaskAsync(string taskId)
        {
            try
            {
                // 根据任意TaskId获取本地任务 - 与Client项目一致
                var sql = @"
                    SELECT * FROM LocalConversionTasks
                    WHERE LocalId = @TaskId OR ServerTaskId = @TaskId OR CurrentTaskId = @TaskId
                    LIMIT 1";

                var result = await _db.Ado.SqlQuerySingleAsync<LocalConversionTaskEntity>(sql, new { TaskId = taskId });
                return result;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SqlSugarDatabaseService", $"❌ 获取任务失败 {taskId}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取所有任务
        /// </summary>
        public async Task<List<LocalConversionTaskEntity>> GetAllTasksAsync()
        {
            try
            {
                return await _db.Queryable<LocalConversionTaskEntity>()
                    .OrderBy(t => t.CreatedAt, OrderByType.Desc)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SqlSugarDatabaseService", $"❌ 获取所有任务失败: {ex.Message}");
                return new List<LocalConversionTaskEntity>();
            }
        }

        /// <summary>
        /// 获取已完成任务
        /// </summary>
        public async Task<List<LocalConversionTaskEntity>> GetCompletedTasksAsync()
        {
            try
            {
                return await _db.Queryable<LocalConversionTaskEntity>()
                    .Where(t => t.Status == "Completed")
                    .OrderBy(t => t.CompletedAt, OrderByType.Desc)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SqlSugarDatabaseService", $"❌ 获取已完成任务失败: {ex.Message}");
                return new List<LocalConversionTaskEntity>();
            }
        }

        /// <summary>
        /// 保存任务
        /// </summary>
        public async Task<LocalConversionTaskEntity> SaveTaskAsync(ConversionTask task)
        {
            try
            {
                var existingTask = await GetTaskAsync(task.Id.Value);
                
                if (existingTask != null)
                {
                    // 更新现有任务
                    UpdateTaskEntity(existingTask, task);
                    existingTask.UpdatedAt = DateTime.UtcNow;
                    await _db.Updateable(existingTask).ExecuteCommandAsync();
                }
                else
                {
                    // 创建新任务
                    existingTask = CreateTaskEntity(task);
                    await _db.Insertable(existingTask).ExecuteCommandAsync();
                }

                Utils.Logger.Debug("SqlSugarDatabaseService", $"💾 任务已保存: {task.Id}");
                
                return existingTask;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SqlSugarDatabaseService", $"❌ 保存任务失败 {task.Id}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 删除任务
        /// </summary>
        public async Task<bool> DeleteTaskAsync(string taskId)
        {
            try
            {
                var result = await _db.Deleteable<LocalConversionTaskEntity>()
                    .Where(t => t.LocalId == taskId || t.ServerTaskId == taskId || t.CurrentTaskId == taskId)
                    .ExecuteCommandAsync();
                
                Utils.Logger.Debug("SqlSugarDatabaseService", $"🗑️ 任务已删除: {taskId}");
                return result > 0;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SqlSugarDatabaseService", $"❌ 删除任务失败 {taskId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取设置
        /// </summary>
        public async Task<string?> GetSettingAsync(string key)
        {
            try
            {
                var setting = await _db.Queryable<SystemSettingsEntity>()
                    .Where(s => s.Id == 1)
                    .FirstAsync();

                if (setting == null)
                {
                    Utils.Logger.Warning("SqlSugarDatabaseService", "⚠️ 系统设置记录不存在，尝试创建默认设置");
                    await EnsureDefaultSettingsExist();

                    // 重新获取设置
                    setting = await _db.Queryable<SystemSettingsEntity>()
                        .Where(s => s.Id == 1)
                        .FirstAsync();
                }

                return key switch
                {
                    "ServerAddress" => setting?.ServerAddress,
                    "MaxConcurrentUploads" => setting?.MaxConcurrentUploads.ToString(),
                    "MaxConcurrentDownloads" => setting?.MaxConcurrentDownloads.ToString(),
                    "MaxConcurrentChunks" => setting?.MaxConcurrentChunks.ToString(),
                    "DefaultOutputPath" => setting?.DefaultOutputPath,
                    "ConversionSettings" => setting?.ConversionSettings,
                    _ => null
                };
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SqlSugarDatabaseService", $"❌ 获取设置失败 {key}: {ex.Message}");

                // 返回默认值
                return key switch
                {
                    "ServerAddress" => "http://localhost:5065",
                    "MaxConcurrentUploads" => "3",
                    "MaxConcurrentDownloads" => "3",
                    "MaxConcurrentChunks" => "4",
                    "DefaultOutputPath" => "",
                    "ConversionSettings" => null,
                    _ => null
                };
            }
        }

        /// <summary>
        /// 设置值
        /// </summary>
        public async Task SetSettingAsync(string key, string value)
        {
            try
            {
                var setting = await _db.Queryable<SystemSettingsEntity>()
                    .Where(s => s.Id == 1)
                    .FirstAsync();

                if (setting == null)
                {
                    setting = new SystemSettingsEntity { Id = 1 };
                    
                    switch (key)
                    {
                        case "ServerAddress":
                            setting.ServerAddress = value;
                            break;
                        case "MaxConcurrentUploads":
                            setting.MaxConcurrentUploads = int.TryParse(value, out var uploads) ? uploads : 3;
                            break;
                        case "MaxConcurrentDownloads":
                            setting.MaxConcurrentDownloads = int.TryParse(value, out var downloads) ? downloads : 3;
                            break;
                        case "MaxConcurrentChunks":
                            setting.MaxConcurrentChunks = int.TryParse(value, out var chunks) ? chunks : 4;
                            break;
                        case "DefaultOutputPath":
                            setting.DefaultOutputPath = value;
                            break;
                        case "ConversionSettings":
                            setting.ConversionSettings = value;
                            break;
                    }
                    
                    await _db.Insertable(setting).ExecuteCommandAsync();
                }
                else
                {
                    switch (key)
                    {
                        case "ServerAddress":
                            setting.ServerAddress = value;
                            break;
                        case "MaxConcurrentUploads":
                            setting.MaxConcurrentUploads = int.TryParse(value, out var uploads) ? uploads : 3;
                            break;
                        case "MaxConcurrentDownloads":
                            setting.MaxConcurrentDownloads = int.TryParse(value, out var downloads) ? downloads : 3;
                            break;
                        case "MaxConcurrentChunks":
                            setting.MaxConcurrentChunks = int.TryParse(value, out var chunks) ? chunks : 4;
                            break;
                        case "DefaultOutputPath":
                            setting.DefaultOutputPath = value;
                            break;
                        case "ConversionSettings":
                            setting.ConversionSettings = value;
                            break;
                    }
                    
                    setting.UpdatedAt = DateTime.UtcNow;
                    await _db.Updateable(setting).ExecuteCommandAsync();
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SqlSugarDatabaseService", $"❌ 设置值失败 {key}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 获取布尔设置
        /// </summary>
        public async Task<bool> GetBoolSettingAsync(string key, bool defaultValue = false)
        {
            try
            {
                var setting = await _db.Queryable<SystemSettingsEntity>()
                    .Where(s => s.Id == 1)
                    .FirstAsync();
                
                return key switch
                {
                    "AutoStartConversion" => setting?.AutoStartConversion ?? defaultValue,
                    "ShowNotifications" => setting?.ShowNotifications ?? defaultValue,
                    _ => defaultValue
                };
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SqlSugarDatabaseService", $"❌ 获取布尔设置失败 {key}: {ex.Message}");
                return defaultValue;
            }
        }

        /// <summary>
        /// 设置布尔值
        /// </summary>
        public async Task SetBoolSettingAsync(string key, bool value)
        {
            try
            {
                var setting = await _db.Queryable<SystemSettingsEntity>()
                    .Where(s => s.Id == 1)
                    .FirstAsync();

                if (setting == null)
                {
                    setting = new SystemSettingsEntity { Id = 1 };
                    
                    switch (key)
                    {
                        case "AutoStartConversion":
                            setting.AutoStartConversion = value;
                            break;
                        case "ShowNotifications":
                            setting.ShowNotifications = value;
                            break;
                    }
                    
                    await _db.Insertable(setting).ExecuteCommandAsync();
                }
                else
                {
                    switch (key)
                    {
                        case "AutoStartConversion":
                            setting.AutoStartConversion = value;
                            break;
                        case "ShowNotifications":
                            setting.ShowNotifications = value;
                            break;
                    }
                    
                    setting.UpdatedAt = DateTime.UtcNow;
                    await _db.Updateable(setting).ExecuteCommandAsync();
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SqlSugarDatabaseService", $"❌ 设置布尔值失败 {key}: {ex.Message}");
                throw;
            }
        }

        #region 私有方法

        private LocalConversionTaskEntity CreateTaskEntity(ConversionTask task)
        {
            return new LocalConversionTaskEntity
            {
                LocalId = Guid.NewGuid().ToString(),
                CurrentTaskId = task.Id.Value,
                FilePath = task.SourceFile.FilePath,
                FileName = task.SourceFile.FileName,
                FileSize = task.SourceFile.FileSize,
                Status = task.Status.ToString(),
                Progress = task.Progress,
                ErrorMessage = task.ErrorMessage,
                CreatedAt = task.CreatedAt,
                UpdatedAt = DateTime.UtcNow,
                StartedAt = task.StartedAt,
                CompletedAt = task.CompletedAt
            };
        }

        private void UpdateTaskEntity(LocalConversionTaskEntity entity, ConversionTask task)
        {
            entity.Status = task.Status.ToString();
            entity.Progress = task.Progress;
            entity.ErrorMessage = task.ErrorMessage;
            entity.StartedAt = task.StartedAt;
            entity.CompletedAt = task.CompletedAt;
        }

        #endregion

        #region 私有辅助方法

        /// <summary>
        /// 确保默认设置记录存在
        /// </summary>
        private async Task EnsureDefaultSettingsExist()
        {
            try
            {
                // 检查是否已存在ID=1的设置记录
                var existingSetting = await _db.Queryable<SystemSettingsEntity>()
                    .Where(s => s.Id == 1)
                    .FirstAsync();

                if (existingSetting == null)
                {
                    // 创建默认设置记录
                    var defaultSettings = new SystemSettingsEntity
                    {
                        Id = 1,
                        ServerAddress = "http://localhost:5065",
                        MaxConcurrentUploads = 3,
                        MaxConcurrentDownloads = 3,
                        MaxConcurrentChunks = 4,
                        AutoStartConversion = false,
                        ShowNotifications = true,
                        DefaultOutputPath = "",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        Version = 1,
                        Remarks = "系统默认设置"
                    };

                    await _db.Insertable(defaultSettings).ExecuteCommandAsync();
                    Utils.Logger.Info("SqlSugarDatabaseService", "✅ 已创建默认系统设置记录");
                }
                else
                {
                    Utils.Logger.Info("SqlSugarDatabaseService", "✅ 默认系统设置记录已存在");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SqlSugarDatabaseService", $"❌ 确保默认设置存在失败: {ex.Message}");
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
