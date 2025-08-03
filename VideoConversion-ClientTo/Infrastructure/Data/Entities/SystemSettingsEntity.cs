using SqlSugar;
using System;
using System.ComponentModel;

namespace VideoConversion_ClientTo.Infrastructure.Data.Entities
{
    /// <summary>
    /// 系统设置数据库实体 - 与Client项目完全一致
    /// </summary>
    [SugarTable("SystemSettings")]
    public class SystemSettingsEntity
    {
        /// <summary>
        /// 主键ID
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        /// <summary>
        /// 服务器地址
        /// </summary>
        [SugarColumn(Length = 500, IsNullable = false)]
        public string ServerAddress { get; set; } = "http://localhost:5065";

        /// <summary>
        /// 最大同时上传数量
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public int MaxConcurrentUploads { get; set; } = 3;

        /// <summary>
        /// 最大同时下载数量
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public int MaxConcurrentDownloads { get; set; } = 3;

        /// <summary>
        /// 最大分片并发数
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public int MaxConcurrentChunks { get; set; } = 4;

        /// <summary>
        /// 是否自动开始转换
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public bool AutoStartConversion { get; set; } = true;

        /// <summary>
        /// 是否显示通知
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public bool ShowNotifications { get; set; } = true;

        /// <summary>
        /// 默认输出路径
        /// </summary>
        [SugarColumn(Length = 1000, IsNullable = true)]
        public string? DefaultOutputPath { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 更新时间
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 版本号（用于数据迁移）
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public int Version { get; set; } = 1;

        /// <summary>
        /// 转换设置JSON
        /// </summary>
        [SugarColumn(ColumnDataType = "TEXT", IsNullable = true)]
        public string? ConversionSettings { get; set; }

        /// <summary>
        /// 备注信息
        /// </summary>
        [SugarColumn(Length = 1000, IsNullable = true)]
        public string? Remarks { get; set; }
    }
}
