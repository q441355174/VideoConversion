using SqlSugar;
using System;
using System.ComponentModel;

namespace VideoConversion_Client.Models
{
    /// <summary>
    /// 系统设置数据库实体
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
        /// 自动开始转换
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public bool AutoStartConversion { get; set; } = true;

        /// <summary>
        /// 显示通知
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public bool ShowNotifications { get; set; } = true;

        /// <summary>
        /// 默认输出路径
        /// </summary>
        [SugarColumn(Length = 1000, IsNullable = true)]
        public string? DefaultOutputPath { get; set; } = "";

        /// <summary>
        /// 创建时间
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public DateTime CreateTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 更新时间
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public DateTime UpdateTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 版本号（用于数据迁移）
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public int Version { get; set; } = 1;

        /// <summary>
        /// 备注信息
        /// </summary>
        [SugarColumn(Length = 1000, IsNullable = true)]
        public string? Remarks { get; set; }

        /// <summary>
        /// 转换为SystemSettingsModel
        /// </summary>
        public SystemSettingsModel ToModel()
        {
            return new SystemSettingsModel
            {
                ServerAddress = this.ServerAddress,
                MaxConcurrentUploads = this.MaxConcurrentUploads,
                MaxConcurrentDownloads = this.MaxConcurrentDownloads,
                AutoStartConversion = this.AutoStartConversion,
                ShowNotifications = this.ShowNotifications,
                DefaultOutputPath = this.DefaultOutputPath ?? ""
            };
        }

        /// <summary>
        /// 从SystemSettingsModel创建实体
        /// </summary>
        public static SystemSettingsEntity FromModel(SystemSettingsModel model)
        {
            return new SystemSettingsEntity
            {
                ServerAddress = model.ServerAddress,
                MaxConcurrentUploads = model.MaxConcurrentUploads,
                MaxConcurrentDownloads = model.MaxConcurrentDownloads,
                AutoStartConversion = model.AutoStartConversion,
                ShowNotifications = model.ShowNotifications,
                DefaultOutputPath = model.DefaultOutputPath,
                CreateTime = DateTime.Now,
                UpdateTime = DateTime.Now
            };
        }

        /// <summary>
        /// 更新实体数据
        /// </summary>
        public void UpdateFromModel(SystemSettingsModel model)
        {
            this.ServerAddress = model.ServerAddress;
            this.MaxConcurrentUploads = model.MaxConcurrentUploads;
            this.MaxConcurrentDownloads = model.MaxConcurrentDownloads;
            this.AutoStartConversion = model.AutoStartConversion;
            this.ShowNotifications = model.ShowNotifications;
            this.DefaultOutputPath = model.DefaultOutputPath;
            this.UpdateTime = DateTime.Now;
        }

        /// <summary>
        /// 验证数据有效性
        /// </summary>
        public bool IsValid()
        {
            try
            {
                // 验证服务器地址
                if (string.IsNullOrWhiteSpace(ServerAddress))
                    return false;

                var uri = new Uri(ServerAddress);
                if (uri.Scheme != "http" && uri.Scheme != "https")
                    return false;

                // 验证并发数量
                if (MaxConcurrentUploads <= 0 || MaxConcurrentUploads > 20)
                    return false;

                if (MaxConcurrentDownloads <= 0 || MaxConcurrentDownloads > 20)
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 重置为默认值
        /// </summary>
        public void ResetToDefaults()
        {
            ServerAddress = "http://localhost:5065";
            MaxConcurrentUploads = 3;
            MaxConcurrentDownloads = 3;
            AutoStartConversion = true;
            ShowNotifications = true;
            DefaultOutputPath = "";
            UpdateTime = DateTime.Now;
        }

        /// <summary>
        /// 创建副本
        /// </summary>
        public SystemSettingsEntity Clone()
        {
            return new SystemSettingsEntity
            {
                Id = this.Id,
                ServerAddress = this.ServerAddress,
                MaxConcurrentUploads = this.MaxConcurrentUploads,
                MaxConcurrentDownloads = this.MaxConcurrentDownloads,
                AutoStartConversion = this.AutoStartConversion,
                ShowNotifications = this.ShowNotifications,
                DefaultOutputPath = this.DefaultOutputPath,
                CreateTime = this.CreateTime,
                UpdateTime = this.UpdateTime,
                Version = this.Version,
                Remarks = this.Remarks
            };
        }

        /// <summary>
        /// 获取设置摘要信息
        /// </summary>
        public string GetSummary()
        {
            return $"服务器: {ServerAddress}, 上传并发: {MaxConcurrentUploads}, 下载并发: {MaxConcurrentDownloads}, " +
                   $"自动转换: {(AutoStartConversion ? "是" : "否")}, 显示通知: {(ShowNotifications ? "是" : "否")}";
        }
    }
}
