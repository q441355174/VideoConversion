using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VideoConversion_Client.Services;

namespace VideoConversion_ClientTo.Domain.Models
{
    /// <summary>
    /// 转换选项管理类 - 替代硬编码，提供结构化的选项管理
    /// </summary>
    public static class ConversionOptions
    {
        #region 输出格式选项

        /// <summary>
        /// 获取支持的输出格式列表
        /// </summary>
        public static List<FormatOption> GetSupportedFormats()
        {
            return new List<FormatOption>
            {
                // 推荐格式
                new FormatOption("mp4", "MP4 (推荐)", "最佳兼容性，通用格式", true),
                new FormatOption("mkv", "MKV (高质量)", "开源容器，支持多轨道", true),
                new FormatOption("webm", "WebM (Web优化)", "Web播放优化，文件较小", true),

                // 通用格式
                new FormatOption("avi", "AVI (兼容性)", "传统格式，广泛支持", false),
                new FormatOption("mov", "MOV (Apple)", "Apple QuickTime格式", false),
                new FormatOption("m4v", "M4V (iTunes)", "iTunes视频格式", false),

                // 移动设备格式
                new FormatOption("3gp", "3GP (移动设备)", "移动设备兼容格式", false),

                // 传统格式
                new FormatOption("wmv", "WMV (Windows)", "Windows Media格式", false),
                new FormatOption("flv", "FLV (Flash)", "Flash视频格式", false),

                // 广播格式
                new FormatOption("mpg", "MPEG (标准)", "标准MPEG格式", false),
                new FormatOption("ts", "TS (传输流)", "传输流格式", false),
                new FormatOption("mts", "MTS (AVCHD)", "AVCHD摄像机格式", false),
                new FormatOption("m2ts", "M2TS (蓝光)", "蓝光光盘格式", false),

                // 特殊格式
                new FormatOption("vob", "VOB (DVD)", "DVD视频格式", false),
                new FormatOption("asf", "ASF (Windows Media)", "Windows Media格式", false),

                // 智能选项
                new FormatOption("keep_original", "保持原格式", "与源文件相同格式", false),
                new FormatOption("auto_best", "自动选择最佳格式", "根据内容自动选择", false)
            };
        }

        /// <summary>
        /// 获取输出格式显示名称列表（用于UI绑定）
        /// </summary>
        public static List<string> GetOutputFormatDisplayNames()
        {
            return GetSupportedFormats().Select(f => f.DisplayName).ToList();
        }

        /// <summary>
        /// 根据显示名称获取格式值
        /// </summary>
        public static string GetFormatValueByDisplayName(string displayName)
        {
            var format = GetSupportedFormats().FirstOrDefault(f => f.DisplayName == displayName);
            return format?.Value ?? "mp4";
        }

        /// <summary>
        /// 根据格式值获取显示名称
        /// </summary>
        public static string GetDisplayNameByFormatValue(string value)
        {
            var format = GetSupportedFormats().FirstOrDefault(f => f.Value.Equals(value, StringComparison.OrdinalIgnoreCase));
            return format?.DisplayName ?? "MP4 (推荐)";
        }

        #endregion

        #region 分辨率选项

        /// <summary>
        /// 获取分辨率选项
        /// </summary>
        public static List<string> GetResolutionOptions()
        {
            return new List<string>
            {
                "保持原始",
                "3840x2160 (4K Ultra HD)",
                "2560x1440 (2K QHD)",
                "1920x1080 (Full HD)",
                "1280x720 (HD)",
                "854x480 (SD)",
                "640x360 (低质量)",
                "自定义分辨率"
            };
        }

        #endregion

        #region 视频编码器选项

        /// <summary>
        /// 获取视频编码器选项
        /// </summary>
        public static List<string> GetVideoCodecOptions()
        {
            return new List<string>
            {
                "H.264 (CPU)",
                "H.264 (NVENC)",
                "H.264 (QSV)",
                "H.264 (AMF)",
                "H.265 (CPU)",
                "H.265 (NVENC)",
                "H.265 (QSV)",
                "H.265 (AMF)",
                "VP9 (CPU)",
                "AV1 (CPU)"
            };
        }

        #endregion

        #region 音频编码器选项

        /// <summary>
        /// 获取音频编码器选项
        /// </summary>
        public static List<string> GetAudioCodecOptions()
        {
            return new List<string>
            {
                "AAC (推荐)",
                "MP3 (兼容性)",
                "AC3 (杜比)",
                "FLAC (无损)",
                "Opus (高效)",
                "Vorbis (开源)",
                "保持原始"
            };
        }

        #endregion

        #region 质量模式选项

        /// <summary>
        /// 获取质量模式选项
        /// </summary>
        public static List<string> GetQualityModeOptions()
        {
            return new List<string>
            {
                "恒定质量 (CRF)",
                "平均码率 (ABR)",
                "恒定码率 (CBR)",
                "可变码率 (VBR)"
            };
        }

        #endregion

        #region 编码预设选项

        /// <summary>
        /// 获取编码预设选项
        /// </summary>
        public static List<string> GetEncodingPresetOptions()
        {
            return new List<string>
            {
                "超快 (ultrafast)",
                "非常快 (superfast)",
                "很快 (veryfast)",
                "快速 (faster)",
                "快 (fast)",
                "中等 (推荐)",
                "慢 (slow)",
                "很慢 (slower)",
                "非常慢 (veryslow)"
            };
        }

        #endregion

        #region 预设选项

        /// <summary>
        /// 获取预设选项
        /// </summary>
        public static List<string> GetPresetOptions()
        {
            return new List<string>
            {
                "GPU Fast 1080p (NVENC)",
                "GPU High Quality 1080p (NVENC)",
                "GPU 4K Ultra (NVENC)",
                "GPU Fast 1080p (QSV)",
                "GPU High Quality 1080p (QSV)",
                "GPU Fast 1080p (AMF)",
                "GPU High Quality 1080p (AMF)",
                "CPU Standard 1080p",
                "CPU High Quality 1080p"
            };
        }

        #endregion

        #region 智能格式处理

        /// <summary>
        /// 验证输出格式是否支持
        /// </summary>
        public static bool IsFormatSupported(string format)
        {
            var supportedFormats = GetSupportedFormats();
            return supportedFormats.Any(f => f.Value.Equals(format, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 处理智能格式选择
        /// </summary>
        public static string ResolveSmartFormat(string selectedFormat, string originalFilePath)
        {
            return selectedFormat switch
            {
                "keep_original" => GetOriginalFormat(originalFilePath),
                "auto_best" => GetBestFormatForFile(originalFilePath),
                _ => selectedFormat
            };
        }

        /// <summary>
        /// 获取原始文件格式
        /// </summary>
        private static string GetOriginalFormat(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return "mp4";

            var extension = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();
            return IsFormatSupported(extension) ? extension : "mp4";
        }

        /// <summary>
        /// 为文件选择最佳格式
        /// </summary>
        private static string GetBestFormatForFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return "mp4";

            var extension = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();
            
            // 根据源格式智能选择最佳输出格式
            return extension switch
            {
                "avi" or "wmv" or "flv" => "mp4", // 旧格式转换为MP4
                "mov" => "mp4", // QuickTime转换为MP4
                "mkv" => "mkv", // 保持MKV
                "webm" => "webm", // 保持WebM
                _ => "mp4" // 默认使用MP4
            };
        }

        #endregion
    }
}
