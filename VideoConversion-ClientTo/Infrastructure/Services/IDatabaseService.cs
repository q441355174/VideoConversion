using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VideoConversion_ClientTo.Domain.Entities;
using VideoConversion_ClientTo.Infrastructure.Data.Entities;

namespace VideoConversion_ClientTo.Infrastructure.Services
{
    /// <summary>
    /// 数据库服务接口 - 与Client项目一致
    /// </summary>
    public interface IDatabaseService
    {
        /// <summary>
        /// 初始化数据库
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// 获取任务
        /// </summary>
        Task<LocalConversionTaskEntity?> GetTaskAsync(string taskId);

        /// <summary>
        /// 获取所有任务
        /// </summary>
        Task<List<LocalConversionTaskEntity>> GetAllTasksAsync();

        /// <summary>
        /// 获取已完成任务
        /// </summary>
        Task<List<LocalConversionTaskEntity>> GetCompletedTasksAsync();

        /// <summary>
        /// 保存任务
        /// </summary>
        Task<LocalConversionTaskEntity> SaveTaskAsync(ConversionTask task);

        /// <summary>
        /// 删除任务
        /// </summary>
        Task<bool> DeleteTaskAsync(string taskId);

        /// <summary>
        /// 获取设置
        /// </summary>
        Task<string?> GetSettingAsync(string key);

        /// <summary>
        /// 设置值
        /// </summary>
        Task SetSettingAsync(string key, string value);

        /// <summary>
        /// 获取布尔设置
        /// </summary>
        Task<bool> GetBoolSettingAsync(string key, bool defaultValue = false);

        /// <summary>
        /// 设置布尔值
        /// </summary>
        Task SetBoolSettingAsync(string key, bool value);
    }
}
