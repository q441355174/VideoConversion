namespace VideoConversion_Client.Models
{
    public class ConversionSettings
    {
        public string VideoCodec { get; set; } = "自动";
        public string Resolution { get; set; } = "自动";
        public string FrameRate { get; set; } = "自动";
        public string Bitrate { get; set; } = "自动";
        public string AudioCodec { get; set; } = "自动";
        public string AudioQuality { get; set; } = "自动";
        public string HardwareAcceleration { get; set; } = "自动";
        public string Threads { get; set; } = "自动";

        public ConversionSettings Clone()
        {
            return new ConversionSettings
            {
                VideoCodec = VideoCodec,
                Resolution = Resolution,
                FrameRate = FrameRate,
                Bitrate = Bitrate,
                AudioCodec = AudioCodec,
                AudioQuality = AudioQuality,
                HardwareAcceleration = HardwareAcceleration,
                Threads = Threads
            };
        }

        public override string ToString()
        {
            return $"视频: {VideoCodec} {Resolution} {FrameRate} {Bitrate} | 音频: {AudioCodec} {AudioQuality} | 硬件: {HardwareAcceleration} | 线程: {Threads}";
        }
    }
}
