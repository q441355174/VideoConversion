using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VideoConversion_ClientTo.Infrastructure.Data;
using VideoConversion_ClientTo.Infrastructure.Data.Entities;
using VideoConversion_ClientTo.Domain.Entities;

namespace VideoConversion_ClientTo.Infrastructure.Services
{
    /// <summary>
    /// 数据库服务接口
    /// </summary>
    public interface IDatabaseService
    {
        Task InitializeAsync();
        Task<LocalConversionTaskEntity?> GetTaskAsync(string taskId);
        Task<List<LocalConversionTaskEntity>> GetAllTasksAsync();
        Task<List<LocalConversionTaskEntity>> GetCompletedTasksAsync();
        Task<LocalConversionTaskEntity> SaveTaskAsync(ConversionTask task);
        Task<bool> DeleteTaskAsync(string taskId);
        Task<string?> GetSettingAsync(string key);
        Task SetSettingAsync(string key, string value);
        Task<bool> GetBoolSettingAsync(string key, bool defaultValue = false);
        Task SetBoolSettingAsync(string key, bool value);
    }

    /// <summary>
    /// 数据库服务实现
    /// 职责: 管理本地数据库操作
    /// </summary>
    public class DatabaseService : IDatabaseService
    {
        private readonly LocalDbContext _context;

        public DatabaseService(LocalDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// 初始化数据库
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                Utils.Logger.Info("DatabaseService", "🔄 初始化数据库");
                await _context.EnsureDatabaseCreatedAsync();
                Utils.Logger.Info("DatabaseService", "✅ 数据库初始化完成");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("DatabaseService", $"❌ 数据库初始化失败: {ex.Message}");
                throw;
            }
        }

        #region 任务管理

        /// <summary>
        /// 获取任务
        /// </summary>
        public async Task<LocalConversionTaskEntity?> GetTaskAsync(string taskId)
        {
            try
            {
                return await _context.ConversionTasks
                    .FirstOrDefaultAsync(t => t.TaskId == taskId);
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("DatabaseService", $"❌ 获取任务失败 {taskId}: {ex.Message}");
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
                return await _context.ConversionTasks
                    .OrderByDescending(t => t.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("DatabaseService", $"❌ 获取所有任务失败: {ex.Message}");
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
                return await _context.ConversionTasks
                    .Where(t => t.Status == "Completed")
                    .OrderByDescending(t => t.CompletedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("DatabaseService", $"❌ 获取已完成任务失败: {ex.Message}");
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
                    _context.ConversionTasks.Update(existingTask);
                }
                else
                {
                    // 创建新任务
                    existingTask = CreateTaskEntity(task);
                    _context.ConversionTasks.Add(existingTask);
                }

                await _context.SaveChangesAsync();
                Utils.Logger.Debug("DatabaseService", $"💾 任务已保存: {task.Id}");
                
                return existingTask;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("DatabaseService", $"❌ 保存任务失败 {task.Id}: {ex.Message}");
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
                var task = await GetTaskAsync(taskId);
                if (task != null)
                {
                    _context.ConversionTasks.Remove(task);
                    await _context.SaveChangesAsync();
                    Utils.Logger.Debug("DatabaseService", $"🗑️ 任务已删除: {taskId}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("DatabaseService", $"❌ 删除任务失败 {taskId}: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region 设置管理

        /// <summary>
        /// 获取设置值
        /// </summary>
        public async Task<string?> GetSettingAsync(string key)
        {
            try
            {
                var setting = await _context.SystemSettings
                    .FirstOrDefaultAsync(s => s.Key == key);
                return setting?.Value;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("DatabaseService", $"❌ 获取设置失败 {key}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 设置值
        /// </summary>
        public async Task SetSettingAsync(string key, string value)
        {
            try
            {
                var setting = await _context.SystemSettings
                    .FirstOrDefaultAsync(s => s.Key == key);

                if (setting != null)
                {
                    setting.SetValue(value);
                    _context.SystemSettings.Update(setting);
                }
                else
                {
                    setting = new SystemSettingsEntity
                    {
                        Key = key,
                        Value = value,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.SystemSettings.Add(setting);
                }

                await _context.SaveChangesAsync();
                Utils.Logger.Debug("DatabaseService", $"⚙️ 设置已保存: {key} = {value}");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("DatabaseService", $"❌ 保存设置失败 {key}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 获取布尔设置
        /// </summary>
        public async Task<bool> GetBoolSettingAsync(string key, bool defaultValue = false)
        {
            var value = await GetSettingAsync(key);
            return bool.TryParse(value, out var result) ? result : defaultValue;
        }

        /// <summary>
        /// 设置布尔值
        /// </summary>
        public async Task SetBoolSettingAsync(string key, bool value)
        {
            await SetSettingAsync(key, value.ToString());
        }

        #endregion

        #region 私有方法

        private LocalConversionTaskEntity CreateTaskEntity(ConversionTask task)
        {
            return new LocalConversionTaskEntity
            {
                TaskId = task.Id.Value,
                TaskName = task.Name.Value,
                SourceFilePath = task.SourceFile.FilePath,
                SourceFileName = task.SourceFile.FileName,
                FileSize = task.SourceFile.FileSize,
                OutputFormat = task.Parameters.OutputFormat,
                Status = task.Status.ToString(),
                Progress = task.Progress,
                Speed = task.Speed,
                EstimatedRemainingSeconds = task.EstimatedRemaining?.TotalSeconds,
                ErrorMessage = task.ErrorMessage,
                ConversionParameters = System.Text.Json.JsonSerializer.Serialize(task.Parameters),
                CreatedAt = task.CreatedAt,
                UpdatedAt = DateTime.UtcNow,
                StartedAt = task.StartedAt,
                CompletedAt = task.CompletedAt
            };
        }

        private void UpdateTaskEntity(LocalConversionTaskEntity entity, ConversionTask task)
        {
            entity.TaskName = task.Name.Value;
            entity.Status = task.Status.ToString();
            entity.Progress = task.Progress;
            entity.Speed = task.Speed;
            entity.EstimatedRemainingSeconds = task.EstimatedRemaining?.TotalSeconds;
            entity.ErrorMessage = task.ErrorMessage;
            entity.StartedAt = task.StartedAt;
            entity.CompletedAt = task.CompletedAt;
        }

        #endregion
    }
}
