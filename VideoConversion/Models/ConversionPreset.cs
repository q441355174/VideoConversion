namespace VideoConversion.Models
{
    /// <summary>
    /// 转换预设配置（类似HandBrake的预设）
    /// </summary>
    public class ConversionPreset
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string OutputFormat { get; set; } = string.Empty;
        public string VideoCodec { get; set; } = string.Empty;
        public string AudioCodec { get; set; } = string.Empty;
        public string VideoQuality { get; set; } = string.Empty;
        public string AudioQuality { get; set; } = string.Empty;
        public string? Resolution { get; set; }
        public string? FrameRate { get; set; }
        public bool IsDefault { get; set; } = false;

        /// <summary>
        /// 获取所有预设配置
        /// </summary>
        public static List<ConversionPreset> GetAllPresets()
        {
            return new List<ConversionPreset>
            {
                // 通用预设
                new ConversionPreset
                {
                    Name = "Fast 1080p30",
                    Description = "快速转换为1080p 30fps，适合快速预览",
                    OutputFormat = "mp4",
                    VideoCodec = "libx264",
                    AudioCodec = "aac",
                    VideoQuality = "23",
                    AudioQuality = "128k",
                    Resolution = "1920x1080",
                    FrameRate = "30",
                    IsDefault = false // 不再是默认预设
                },
                new ConversionPreset
                {
                    Name = "High Quality 1080p",
                    Description = "高质量1080p，文件较大但质量最佳",
                    OutputFormat = "mp4",
                    VideoCodec = "libx264",
                    AudioCodec = "aac",
                    VideoQuality = "18",
                    AudioQuality = "192k",
                    Resolution = "1920x1080"
                },
                new ConversionPreset
                {
                    Name = "Web Optimized",
                    Description = "网络优化版本，适合在线播放",
                    OutputFormat = "mp4",
                    VideoCodec = "libx264",
                    AudioCodec = "aac",
                    VideoQuality = "25",
                    AudioQuality = "128k",
                    Resolution = "1280x720"
                },
                
                // 移动设备预设
                new ConversionPreset
                {
                    Name = "iPhone/iPad",
                    Description = "适合iPhone和iPad播放",
                    OutputFormat = "mp4",
                    VideoCodec = "libx264",
                    AudioCodec = "aac",
                    VideoQuality = "22",
                    AudioQuality = "160k",
                    Resolution = "1920x1080"
                },
                new ConversionPreset
                {
                    Name = "Android",
                    Description = "适合Android设备播放",
                    OutputFormat = "mp4",
                    VideoCodec = "libx264",
                    AudioCodec = "aac",
                    VideoQuality = "23",
                    AudioQuality = "128k",
                    Resolution = "1920x1080"
                },

                // 社交媒体预设
                new ConversionPreset
                {
                    Name = "YouTube",
                    Description = "YouTube上传优化",
                    OutputFormat = "mp4",
                    VideoCodec = "libx264",
                    AudioCodec = "aac",
                    VideoQuality = "21",
                    AudioQuality = "192k",
                    Resolution = "1920x1080"
                },
                new ConversionPreset
                {
                    Name = "Instagram",
                    Description = "Instagram视频格式",
                    OutputFormat = "mp4",
                    VideoCodec = "libx264",
                    AudioCodec = "aac",
                    VideoQuality = "23",
                    AudioQuality = "128k",
                    Resolution = "1080x1080"
                },

                // 压缩预设
                new ConversionPreset
                {
                    Name = "Small Size",
                    Description = "小文件大小，适合存储空间有限的情况",
                    OutputFormat = "mp4",
                    VideoCodec = "libx264",
                    AudioCodec = "aac",
                    VideoQuality = "28",
                    AudioQuality = "96k",
                    Resolution = "854x480"
                },

                // GPU加速预设 - NVIDIA NVENC
                new ConversionPreset
                {
                    Name = "GPU Fast 1080p (NVENC)",
                    Description = "NVIDIA GPU加速，快速1080p转换",
                    OutputFormat = "mp4",
                    VideoCodec = "h264_nvenc",
                    AudioCodec = "aac",
                    VideoQuality = "23",
                    AudioQuality = "128k",
                    Resolution = "1920x1080",
                    FrameRate = "30",
                    IsDefault = true // 设为默认预设
                },
                new ConversionPreset
                {
                    Name = "GPU High Quality 1080p (NVENC)",
                    Description = "NVIDIA GPU加速，高质量1080p",
                    OutputFormat = "mp4",
                    VideoCodec = "h264_nvenc",
                    AudioCodec = "aac",
                    VideoQuality = "18",
                    AudioQuality = "192k",
                    Resolution = "1920x1080"
                },
                new ConversionPreset
                {
                    Name = "GPU 4K Ultra (NVENC)",
                    Description = "NVIDIA GPU加速，4K超高清",
                    OutputFormat = "mp4",
                    VideoCodec = "h264_nvenc",
                    AudioCodec = "aac",
                    VideoQuality = "20",
                    AudioQuality = "256k",
                    Resolution = "3840x2160"
                },
                new ConversionPreset
                {
                    Name = "GPU HEVC 1080p (NVENC)",
                    Description = "NVIDIA GPU加速，H.265编码",
                    OutputFormat = "mp4",
                    VideoCodec = "hevc_nvenc",
                    AudioCodec = "aac",
                    VideoQuality = "23",
                    AudioQuality = "128k",
                    Resolution = "1920x1080"
                },

                // GPU加速预设 - Intel QSV
                new ConversionPreset
                {
                    Name = "GPU Fast 1080p (Intel QSV)",
                    Description = "Intel GPU加速，快速1080p转换",
                    OutputFormat = "mp4",
                    VideoCodec = "h264_qsv",
                    AudioCodec = "aac",
                    VideoQuality = "23",
                    AudioQuality = "128k",
                    Resolution = "1920x1080",
                    FrameRate = "30"
                },
                new ConversionPreset
                {
                    Name = "GPU HEVC 1080p (Intel QSV)",
                    Description = "Intel GPU加速，H.265编码",
                    OutputFormat = "mp4",
                    VideoCodec = "hevc_qsv",
                    AudioCodec = "aac",
                    VideoQuality = "23",
                    AudioQuality = "128k",
                    Resolution = "1920x1080"
                },

                // GPU加速预设 - AMD VCE
                new ConversionPreset
                {
                    Name = "GPU Fast 1080p (AMD VCE)",
                    Description = "AMD GPU加速，快速1080p转换",
                    OutputFormat = "mp4",
                    VideoCodec = "h264_amf",
                    AudioCodec = "aac",
                    VideoQuality = "23",
                    AudioQuality = "128k",
                    Resolution = "1920x1080",
                    FrameRate = "30"
                },
                new ConversionPreset
                {
                    Name = "GPU HEVC 1080p (AMD VCE)",
                    Description = "AMD GPU加速，H.265编码",
                    OutputFormat = "mp4",
                    VideoCodec = "hevc_amf",
                    AudioCodec = "aac",
                    VideoQuality = "23",
                    AudioQuality = "128k",
                    Resolution = "1920x1080"
                },

                // 其他格式
                new ConversionPreset
                {
                    Name = "WebM",
                    Description = "WebM格式，适合网页播放",
                    OutputFormat = "webm",
                    VideoCodec = "libvpx-vp9",
                    AudioCodec = "libvorbis",
                    VideoQuality = "30",
                    AudioQuality = "128k"
                },
                new ConversionPreset
                {
                    Name = "Audio Only (MP3)",
                    Description = "仅提取音频为MP3格式",
                    OutputFormat = "mp3",
                    VideoCodec = "",
                    AudioCodec = "libmp3lame",
                    VideoQuality = "",
                    AudioQuality = "192k"
                }
            };
        }

        /// <summary>
        /// 根据名称获取预设
        /// </summary>
        public static ConversionPreset? GetPresetByName(string name)
        {
            return GetAllPresets().FirstOrDefault(p => p.Name == name);
        }

        /// <summary>
        /// 获取默认预设
        /// </summary>
        public static ConversionPreset GetDefaultPreset()
        {
            return GetAllPresets().First(p => p.IsDefault);
        }
    }
}
