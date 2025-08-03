using System.Collections.Generic;

namespace VideoConversion_ClientTo.Application.DTOs
{
    /// <summary>
    /// 手动清理请求 - 与服务器端完全一致
    /// </summary>
    public class ManualCleanupRequest
    {
        /// <summary>
        /// 清理临时文件
        /// </summary>
        public bool CleanupTempFiles { get; set; } = true;

        /// <summary>
        /// 清理已下载文件
        /// </summary>
        public bool CleanupDownloadedFiles { get; set; } = false;

        /// <summary>
        /// 清理孤儿文件
        /// </summary>
        public bool CleanupOrphanFiles { get; set; } = true;

        /// <summary>
        /// 清理失败任务文件
        /// </summary>
        public bool CleanupFailedTasks { get; set; } = true;

        /// <summary>
        /// 清理日志文件
        /// </summary>
        public bool CleanupLogFiles { get; set; } = false;

        /// <summary>
        /// 忽略保留时间限制
        /// </summary>
        public bool IgnoreRetention { get; set; } = false;

        /// <summary>
        /// 重写ToString方法
        /// </summary>
        public override string ToString()
        {
            var options = new List<string>();
            if (CleanupTempFiles) options.Add("临时文件");
            if (CleanupDownloadedFiles) options.Add("已下载文件");
            if (CleanupOrphanFiles) options.Add("孤儿文件");
            if (CleanupFailedTasks) options.Add("失败任务");
            if (CleanupLogFiles) options.Add("日志文件");
            
            var optionsStr = options.Count > 0 ? string.Join(", ", options) : "无";
            return $"清理选项: {optionsStr}, 忽略保留时间: {IgnoreRetention}";
        }
    }
}
