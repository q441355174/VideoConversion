using VideoConversion.Models;

namespace VideoConversion.Services
{
    /// <summary>
    /// GPU设备信息服务 - 简化版本，不包含性能监控
    /// </summary>
    public class GpuDeviceInfoService
    {
        private readonly ILogger<GpuDeviceInfoService> _logger;
        private readonly GpuDetectionService _gpuDetectionService;

        public GpuDeviceInfoService(
            ILogger<GpuDeviceInfoService> logger,
            GpuDetectionService gpuDetectionService)
        {
            _logger = logger;
            _gpuDetectionService = gpuDetectionService;
        }

        /// <summary>
        /// 获取GPU设备信息
        /// </summary>
        public async Task<List<GpuDeviceInfo>> GetGpuDeviceInfoAsync()
        {
            var deviceInfoList = new List<GpuDeviceInfo>();

            try
            { 
                var capabilities = await _gpuDetectionService.DetectGpuCapabilitiesAsync();

                // 基于能力检测生成设备信息
                deviceInfoList = GetDeviceInfoFromCapabilities(capabilities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取GPU设备信息失败");
                // 返回空列表
                deviceInfoList = new List<GpuDeviceInfo>();
            }

            return deviceInfoList;
        }

        /// <summary>
        /// 从GPU能力信息生成设备信息
        /// </summary>
        private List<GpuDeviceInfo> GetDeviceInfoFromCapabilities(GpuCapabilities capabilities)
        {
            var deviceInfoList = new List<GpuDeviceInfo>();

            // 使用真实的GPU设备信息
            if (capabilities.GpuDevices != null && capabilities.GpuDevices.Any())
            {
                foreach (var gpuDevice in capabilities.GpuDevices)
                {
                    var deviceInfo = CreateGpuDeviceInfo(gpuDevice, capabilities);
                    if (deviceInfo != null)
                    {
                        deviceInfoList.Add(deviceInfo);
                    }
                }
            }
            else
            {
                // 如果没有检测到具体设备，但有硬件加速支持，创建通用设备信息
                if (capabilities.NvencSupported)
                {
                    deviceInfoList.Add(CreateGenericGpuInfo("NVIDIA", "NVENC", capabilities.NvencEncoders));
                }

                if (capabilities.QsvSupported)
                {
                    deviceInfoList.Add(CreateGenericGpuInfo("Intel", "Quick Sync Video", capabilities.QsvEncoders));
                }

                if (capabilities.AmfSupported)
                {
                    deviceInfoList.Add(CreateGenericGpuInfo("AMD", "AMF", capabilities.AmfEncoders));
                }
            }

            // 如果没有任何GPU支持，返回空列表（前端会显示无GPU支持消息）
            if (!capabilities.HasAnyGpuSupport)
            {
                _logger.LogInformation("未检测到任何GPU硬件加速支持");
            }

            return deviceInfoList;
        }

        /// <summary>
        /// 根据真实GPU设备名称创建设备信息
        /// </summary>
        private GpuDeviceInfo? CreateGpuDeviceInfo(string gpuDeviceName, GpuCapabilities capabilities)
        {
            var lowerName = gpuDeviceName.ToLower();

            // 判断GPU厂商和支持情况
            if (lowerName.Contains("nvidia") || lowerName.Contains("geforce") || lowerName.Contains("quadro") || lowerName.Contains("tesla"))
            {
                return new GpuDeviceInfo
                {
                    Name = gpuDeviceName,
                    Vendor = "NVIDIA",
                    Driver = "Unknown",
                    Memory = GetEstimatedMemory(lowerName),
                    Encoder = "NVENC",
                    MaxResolution = "8K",
                    PerformanceLevel = GetPerformanceLevel(lowerName),
                    Supported = capabilities.NvencSupported,
                    SupportedFormats = capabilities.NvencSupported ? new[] { "H.264", "H.265" } : Array.Empty<string>(),
                    Reason = capabilities.NvencSupported ? null : "NVENC编码器不可用"
                };
            }
            else if (lowerName.Contains("intel") || lowerName.Contains("uhd") || lowerName.Contains("iris"))
            {
                return new GpuDeviceInfo
                {
                    Name = gpuDeviceName,
                    Vendor = "Intel",
                    Driver = "Unknown",
                    Memory = "Shared",
                    Encoder = "Quick Sync Video",
                    MaxResolution = "4K",
                    PerformanceLevel = "Medium",
                    Supported = capabilities.QsvSupported,
                    SupportedFormats = capabilities.QsvSupported ? new[] { "H.264", "H.265" } : Array.Empty<string>(),
                    Reason = capabilities.QsvSupported ? null : "Quick Sync Video不可用"
                };
            }
            else if (lowerName.Contains("amd") || lowerName.Contains("radeon") || lowerName.Contains("rx "))
            {
                return new GpuDeviceInfo
                {
                    Name = gpuDeviceName,
                    Vendor = "AMD",
                    Driver = "Unknown",
                    Memory = GetEstimatedMemory(lowerName),
                    Encoder = "AMF",
                    MaxResolution = "4K",
                    PerformanceLevel = GetPerformanceLevel(lowerName),
                    Supported = capabilities.AmfSupported,
                    SupportedFormats = capabilities.AmfSupported ? new[] { "H.264", "H.265" } : Array.Empty<string>(),
                    Reason = capabilities.AmfSupported ? null : "AMF编码器不可用"
                };
            }

            // 未知GPU厂商
            return new GpuDeviceInfo
            {
                Name = gpuDeviceName,
                Vendor = "Unknown",
                Driver = "Unknown",
                Memory = "Unknown",
                Encoder = "None",
                MaxResolution = "Unknown",
                PerformanceLevel = "Unknown",
                Supported = false,
                SupportedFormats = Array.Empty<string>(),
                Reason = "未知GPU厂商，不支持硬件加速"
            };
        }

        /// <summary>
        /// 创建通用GPU信息（当无法获取具体设备信息时）
        /// </summary>
        private GpuDeviceInfo CreateGenericGpuInfo(string vendor, string encoder, List<string> encoders)
        {
            return new GpuDeviceInfo
            {
                Name = $"{vendor} GPU",
                Vendor = vendor,
                Driver = "Unknown",
                Memory = vendor == "Intel" ? "Shared" : "Unknown",
                Encoder = encoder,
                MaxResolution = vendor == "NVIDIA" ? "8K" : "4K",
                PerformanceLevel = vendor == "NVIDIA" ? "High" : "Medium",
                Supported = true,
                SupportedFormats = new[] { "H.264", "H.265" },
                Reason = null
            };
        }

        /// <summary>
        /// 根据GPU名称估算显存大小
        /// </summary>
        private string GetEstimatedMemory(string gpuName)
        {
            var lowerName = gpuName.ToLower();

            // RTX 40系列
            if (lowerName.Contains("4090")) return "24GB";
            if (lowerName.Contains("4080")) return "16GB";
            if (lowerName.Contains("4070 ti")) return "12GB";
            if (lowerName.Contains("4070")) return "12GB";
            if (lowerName.Contains("4060 ti")) return "16GB";
            if (lowerName.Contains("4060")) return "8GB";

            // RTX 30系列
            if (lowerName.Contains("3090 ti")) return "24GB";
            if (lowerName.Contains("3090")) return "24GB";
            if (lowerName.Contains("3080 ti")) return "12GB";
            if (lowerName.Contains("3080")) return "10GB";
            if (lowerName.Contains("3070 ti")) return "8GB";
            if (lowerName.Contains("3070")) return "8GB";
            if (lowerName.Contains("3060 ti")) return "8GB";
            if (lowerName.Contains("3060")) return "12GB";

            // GTX 16系列
            if (lowerName.Contains("1660 ti")) return "6GB";
            if (lowerName.Contains("1660")) return "6GB";
            if (lowerName.Contains("1650")) return "4GB";

            // GTX 10系列
            if (lowerName.Contains("1080 ti")) return "11GB";
            if (lowerName.Contains("1080")) return "8GB";
            if (lowerName.Contains("1070 ti")) return "8GB";
            if (lowerName.Contains("1070")) return "8GB";
            if (lowerName.Contains("1060")) return "6GB";
            if (lowerName.Contains("1050 ti")) return "4GB";
            if (lowerName.Contains("1050")) return "2GB";

            // AMD RX系列
            if (lowerName.Contains("rx 7900")) return "20GB";
            if (lowerName.Contains("rx 6900")) return "16GB";
            if (lowerName.Contains("rx 6800")) return "16GB";
            if (lowerName.Contains("rx 6700")) return "12GB";
            if (lowerName.Contains("rx 6600")) return "8GB";
            if (lowerName.Contains("rx 580")) return "8GB";
            if (lowerName.Contains("rx 570")) return "4GB";

            return "Unknown";
        }

        /// <summary>
        /// 根据GPU名称估算性能等级
        /// </summary>
        private string GetPerformanceLevel(string gpuName)
        {
            var lowerName = gpuName.ToLower();

            // 高端GPU
            if (lowerName.Contains("4090") || lowerName.Contains("4080") ||
                lowerName.Contains("3090") || lowerName.Contains("3080") ||
                lowerName.Contains("1080 ti") ||
                lowerName.Contains("rx 7900") || lowerName.Contains("rx 6900"))
                return "High";

            // 中高端GPU
            if (lowerName.Contains("4070") || lowerName.Contains("3070") ||
                lowerName.Contains("1080") || lowerName.Contains("1070 ti") || lowerName.Contains("1070") ||
                lowerName.Contains("rx 6800") || lowerName.Contains("rx 6700"))
                return "High";

            // 中端GPU
            if (lowerName.Contains("4060") || lowerName.Contains("3060") ||
                lowerName.Contains("1660") || lowerName.Contains("1060") ||
                lowerName.Contains("rx 6600") || lowerName.Contains("rx 580"))
                return "Medium";

            // 入门级GPU
            if (lowerName.Contains("1650") || lowerName.Contains("1050") ||
                lowerName.Contains("rx 570") || lowerName.Contains("rx 560"))
                return "Low";

            // 默认中等性能
            return "Medium";
        }
    }
}
