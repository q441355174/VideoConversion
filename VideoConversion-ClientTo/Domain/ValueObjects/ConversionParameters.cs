using System;

namespace VideoConversion_ClientTo.Domain.ValueObjects
{
    /// <summary>
    /// STEP-1: 值对象 - 转换参数
    /// 职责: 封装转换配置参数和验证
    /// </summary>
    public class ConversionParameters : IEquatable<ConversionParameters>
    {
        private ConversionParameters(
            string outputFormat,
            string resolution,
            string videoCodec,
            string audioCodec,
            string videoQuality,
            string audioQuality,
            string preset)
        {
            OutputFormat = ValidateOutputFormat(outputFormat);
            Resolution = ValidateResolution(resolution);
            VideoCodec = ValidateVideoCodec(videoCodec);
            AudioCodec = ValidateAudioCodec(audioCodec);
            VideoQuality = videoQuality ?? throw new ArgumentNullException(nameof(videoQuality));
            AudioQuality = audioQuality ?? throw new ArgumentNullException(nameof(audioQuality));
            Preset = preset ?? throw new ArgumentNullException(nameof(preset));
        }

        public string OutputFormat { get; }
        public string Resolution { get; }
        public string VideoCodec { get; }
        public string AudioCodec { get; }
        public string VideoQuality { get; }
        public string AudioQuality { get; }
        public string Preset { get; }

        // 工厂方法
        public static ConversionParameters Create(
            string outputFormat,
            string resolution,
            string videoCodec,
            string audioCodec,
            string videoQuality,
            string audioQuality,
            string preset)
        {
            return new ConversionParameters(
                outputFormat, resolution, videoCodec, audioCodec,
                videoQuality, audioQuality, preset);
        }

        // 预设工厂方法
        public static ConversionParameters CreateDefault()
        {
            return new ConversionParameters(
                "mp4", "1920x1080", "libx264", "aac", "23", "192k", "medium");
        }

        public static ConversionParameters CreateFast1080p()
        {
            return new ConversionParameters(
                "mp4", "1920x1080", "libx264", "aac", "23", "192k", "fast");
        }

        public static ConversionParameters CreateHighQuality()
        {
            return new ConversionParameters(
                "mp4", "1920x1080", "libx264", "aac", "18", "256k", "slow");
        }

        // 验证方法
        private static string ValidateOutputFormat(string format)
        {
            if (string.IsNullOrWhiteSpace(format))
                throw new ArgumentException("Output format cannot be null or empty", nameof(format));

            var validFormats = new[] { "mp4", "avi", "mov", "mkv", "webm" };
            var normalizedFormat = format.ToLowerInvariant();
            
            if (!Array.Exists(validFormats, f => f == normalizedFormat))
                throw new ArgumentException($"Unsupported output format: {format}", nameof(format));
                
            return normalizedFormat;
        }

        private static string ValidateResolution(string resolution)
        {
            if (string.IsNullOrWhiteSpace(resolution))
                throw new ArgumentException("Resolution cannot be null or empty", nameof(resolution));

            var validResolutions = new[] 
            { 
                "1920x1080", "1280x720", "854x480", "640x360", 
                "3840x2160", "2560x1440", "1366x768" 
            };
            
            if (!Array.Exists(validResolutions, r => r == resolution))
                throw new ArgumentException($"Unsupported resolution: {resolution}", nameof(resolution));
                
            return resolution;
        }

        private static string ValidateVideoCodec(string codec)
        {
            if (string.IsNullOrWhiteSpace(codec))
                throw new ArgumentException("Video codec cannot be null or empty", nameof(codec));

            var validCodecs = new[] { "libx264", "libx265", "libvpx", "libvpx-vp9" };
            
            if (!Array.Exists(validCodecs, c => c == codec))
                throw new ArgumentException($"Unsupported video codec: {codec}", nameof(codec));
                
            return codec;
        }

        private static string ValidateAudioCodec(string codec)
        {
            if (string.IsNullOrWhiteSpace(codec))
                throw new ArgumentException("Audio codec cannot be null or empty", nameof(codec));

            var validCodecs = new[] { "aac", "mp3", "opus", "vorbis" };
            
            if (!Array.Exists(validCodecs, c => c == codec))
                throw new ArgumentException($"Unsupported audio codec: {codec}", nameof(codec));
                
            return codec;
        }

        // 相等性比较
        public bool Equals(ConversionParameters? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            
            return OutputFormat == other.OutputFormat &&
                   Resolution == other.Resolution &&
                   VideoCodec == other.VideoCodec &&
                   AudioCodec == other.AudioCodec &&
                   VideoQuality == other.VideoQuality &&
                   AudioQuality == other.AudioQuality &&
                   Preset == other.Preset;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as ConversionParameters);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(OutputFormat, Resolution, VideoCodec, AudioCodec, VideoQuality, AudioQuality, Preset);
        }

        public override string ToString()
        {
            return $"{OutputFormat} {Resolution} ({VideoCodec}/{AudioCodec})";
        }

        // 操作符重载
        public static bool operator ==(ConversionParameters? left, ConversionParameters? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(ConversionParameters? left, ConversionParameters? right)
        {
            return !Equals(left, right);
        }
    }
}
