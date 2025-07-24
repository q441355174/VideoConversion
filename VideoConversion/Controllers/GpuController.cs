using Microsoft.AspNetCore.Mvc;
using VideoConversion.Services;
using VideoConversion.Controllers.Base;
using VideoConversion.Models;

namespace VideoConversion.Controllers
{
    /// <summary>
    /// GPU硬件加速信息控制器 - 已优化使用 BaseApiController
    /// </summary>
    [Route("api/[controller]")]
    public class GpuController : BaseApiController
    {
        private readonly GpuDetectionService _gpuDetectionService;
        private readonly GpuDeviceInfoService _gpuDeviceInfoService;
        public GpuController(
            ILogger<GpuController> logger,
            GpuDetectionService gpuDetectionService,
            GpuDeviceInfoService gpuDeviceInfoService) : base(logger)
        {
            _gpuDetectionService = gpuDetectionService;
            _gpuDeviceInfoService = gpuDeviceInfoService;
        }

        /// <summary>
        /// 获取GPU硬件加速能力信息 - 已优化使用 BaseApiController
        /// </summary>
        [HttpGet("capabilities")]
        public async Task<IActionResult> GetGpuCapabilities()
        {
            return await SafeExecuteAsync(
                async () =>
                {
                    var capabilities = await _gpuDetectionService.DetectGpuCapabilitiesAsync();

                    return new
                    {
                        hasAnyGpuSupport = capabilities.HasAnyGpuSupport,
                        supportedTypes = capabilities.GetSupportedGpuTypes(),
                        nvidia = new
                        {
                            supported = capabilities.NvencSupported,
                            encoders = capabilities.NvencEncoders
                        },
                        intel = new
                        {
                            supported = capabilities.QsvSupported,
                            encoders = capabilities.QsvEncoders
                        },
                        amd = new
                        {
                            supported = capabilities.AmfSupported,
                            encoders = capabilities.AmfEncoders
                        },
                        vaapi = new
                        {
                            supported = capabilities.VaapiSupported,
                            encoders = capabilities.VaapiEncoders
                        },
                        gpuDevices = capabilities.GpuDevices,
                        // 添加更多有用信息
                        detectionTime = DateTime.Now,
                        systemInfo = new
                        {
                            platform = Environment.OSVersion.Platform.ToString(),
                            architecture = Environment.OSVersion.VersionString
                        }
                    };
                },
                "获取GPU硬件加速能力信息",
                "GPU能力信息获取成功"
            );
        }

        /// <summary>
        /// 获取推荐的GPU编码器 - 已优化使用 BaseApiController
        /// </summary>
        [HttpGet("recommended-encoder")]
        public async Task<IActionResult> GetRecommendedEncoder([FromQuery] string codec = "h264")
        {
            // 使用基类的参数验证
            if (string.IsNullOrWhiteSpace(codec))
                return ValidationError("编码器类型不能为空");

            return await SafeExecuteAsync<object>(
                async () =>
                {
                    var recommendedEncoder = await _gpuDetectionService.GetRecommendedGpuEncoderAsync(codec);

                    if (recommendedEncoder != null)
                    {
                        return new
                        {
                            codec = codec,
                            recommendedEncoder = recommendedEncoder,
                            hasGpuSupport = true,
                            message = $"推荐使用 {recommendedEncoder} 进行GPU加速",
                            // 添加更多有用信息
                            performance = "GPU加速可显著提升编码速度",
                            supportedCodecs = new[] { "h264", "h265", "av1" }
                        };
                    }
                    else
                    {
                        return new
                        {
                            codec = codec,
                            recommendedEncoder = (string?)null,
                            hasGpuSupport = false,
                            message = $"未找到支持 {codec} 的GPU编码器，建议使用CPU编码",
                            fallbackEncoder = $"lib{codec}",
                            performance = "将使用CPU编码，速度较慢但兼容性更好"
                        };
                    }
                },
                "获取推荐的GPU编码器",
                "推荐编码器获取成功"
            );
        }
      
        /// <summary>
        /// 检测GPU硬件信息
        /// </summary>
        [HttpGet("detect")]
        public async Task<IActionResult> DetectGpu()
        {
            return await SafeExecuteAsync(
                async () =>
                {
                    // 获取真实的GPU设备信息
                    var gpuDevices = await _gpuDeviceInfoService.GetGpuDeviceInfoAsync();

                    // 转换为API响应格式
                    var result = gpuDevices.Select(device => new
                    {
                        name = device.Name,
                        vendor = device.Vendor,
                        driver = device.Driver,
                        memory = device.Memory,
                        encoder = device.Encoder,
                        maxResolution = device.MaxResolution,
                        performanceLevel = device.PerformanceLevel,
                        supported = device.Supported,
                        supportedFormats = device.SupportedFormats,
                        reason = device.Reason
                    }).ToList();

                    return result;
                },
                "检测GPU硬件信息",
                "GPU硬件检测完成"
            );
        }




    }

    /// <summary>
    /// 测试编码器请求模型
    /// </summary>
    public class TestEncoderRequest
    {
        public string Encoder { get; set; } = string.Empty;
        public string? TestFile { get; set; }
        public int Duration { get; set; } = 10; // 测试时长（秒）
    }
}
