using System;
using System.ComponentModel.DataAnnotations;

namespace VideoConversion_ClientTo.Infrastructure.Data.Entities
{
    /// <summary>
    /// 系统设置实体
    /// 职责: 存储系统配置信息
    /// </summary>
    public class SystemSettingsEntity
    {
        /// <summary>
        /// 设置键（主键）
        /// </summary>
        [Key]
        [Required]
        [MaxLength(100)]
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// 设置值
        /// </summary>
        [MaxLength(2000)]
        public string? Value { get; set; }

        /// <summary>
        /// 设置描述
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 获取布尔值
        /// </summary>
        public bool GetBoolValue(bool defaultValue = false)
        {
            if (string.IsNullOrEmpty(Value))
                return defaultValue;

            return bool.TryParse(Value, out var result) ? result : defaultValue;
        }

        /// <summary>
        /// 获取整数值
        /// </summary>
        public int GetIntValue(int defaultValue = 0)
        {
            if (string.IsNullOrEmpty(Value))
                return defaultValue;

            return int.TryParse(Value, out var result) ? result : defaultValue;
        }

        /// <summary>
        /// 获取双精度值
        /// </summary>
        public double GetDoubleValue(double defaultValue = 0.0)
        {
            if (string.IsNullOrEmpty(Value))
                return defaultValue;

            return double.TryParse(Value, out var result) ? result : defaultValue;
        }

        /// <summary>
        /// 获取字符串值
        /// </summary>
        public string GetStringValue(string defaultValue = "")
        {
            return Value ?? defaultValue;
        }

        /// <summary>
        /// 设置值
        /// </summary>
        public void SetValue(object? value)
        {
            Value = value?.ToString();
            UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// 系统设置键常量
    /// </summary>
    public static class SystemSettingsKeys
    {
        public const string ServerUrl = "ServerUrl";
        public const string DefaultOutputFormat = "DefaultOutputFormat";
        public const string DefaultOutputLocation = "DefaultOutputLocation";
        public const string AutoStartConversion = "AutoStartConversion";
        public const string MaxConcurrentTasks = "MaxConcurrentTasks";
        public const string EnableNotifications = "EnableNotifications";
        public const string AutoDeleteCompletedTasks = "AutoDeleteCompletedTasks";
        public const string KeepCompletedTasksDays = "KeepCompletedTasksDays";
        public const string DefaultVideoQuality = "DefaultVideoQuality";
        public const string DefaultAudioQuality = "DefaultAudioQuality";
        public const string EnableHardwareAcceleration = "EnableHardwareAcceleration";
        public const string PreferredHardwareAcceleration = "PreferredHardwareAcceleration";
        public const string LogLevel = "LogLevel";
        public const string ThemeMode = "ThemeMode";
        public const string Language = "Language";
    }
}
