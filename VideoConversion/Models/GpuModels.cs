namespace VideoConversion.Models
{
    /// <summary>
    /// GPU设备信息
    /// </summary>
    public class GpuDeviceInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Vendor { get; set; } = string.Empty;
        public string Driver { get; set; } = string.Empty;
        public string Memory { get; set; } = string.Empty;
        public string Encoder { get; set; } = string.Empty;
        public string MaxResolution { get; set; } = string.Empty;
        public string PerformanceLevel { get; set; } = string.Empty;
        public bool Supported { get; set; }
        public string[] SupportedFormats { get; set; } = Array.Empty<string>();
        public string? Reason { get; set; }
    }
}
