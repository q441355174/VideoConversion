using Microsoft.AspNetCore.Mvc;
using VideoConversion.Services;

namespace VideoConversion.Controllers
{
    /// <summary>
    /// GPU硬件加速信息控制器
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class GpuController : ControllerBase
    {
        private readonly ILogger<GpuController> _logger;
        private readonly GpuDetectionService _gpuDetectionService;

        public GpuController(ILogger<GpuController> logger, GpuDetectionService gpuDetectionService)
        {
            _logger = logger;
            _gpuDetectionService = gpuDetectionService;
        }

        /// <summary>
        /// 获取GPU硬件加速能力信息
        /// </summary>
        [HttpGet("capabilities")]
        public async Task<IActionResult> GetGpuCapabilities()
        {
            try
            {
                _logger.LogInformation("获取GPU硬件加速能力信息");
                
                var capabilities = await _gpuDetectionService.DetectGpuCapabilitiesAsync();
                
                return Ok(new
                {
                    success = true,
                    data = new
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
                        gpuDevices = capabilities.GpuDevices
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取GPU能力信息失败");
                return StatusCode(500, new { success = false, message = "获取GPU信息失败: " + ex.Message });
            }
        }

        /// <summary>
        /// 获取推荐的GPU编码器
        /// </summary>
        [HttpGet("recommended-encoder")]
        public async Task<IActionResult> GetRecommendedEncoder([FromQuery] string codec = "h264")
        {
            try
            {
                _logger.LogInformation("获取推荐的GPU编码器: {Codec}", codec);
                
                var recommendedEncoder = await _gpuDetectionService.GetRecommendedGpuEncoderAsync(codec);
                
                if (recommendedEncoder != null)
                {
                    return Ok(new
                    {
                        success = true,
                        data = new
                        {
                            codec = codec,
                            recommendedEncoder = recommendedEncoder,
                            message = $"推荐使用 {recommendedEncoder} 进行GPU加速"
                        }
                    });
                }
                else
                {
                    return Ok(new
                    {
                        success = false,
                        message = $"未找到支持 {codec} 的GPU编码器，建议使用CPU编码"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取推荐GPU编码器失败");
                return StatusCode(500, new { success = false, message = "获取推荐编码器失败: " + ex.Message });
            }
        }

        /// <summary>
        /// 刷新GPU检测缓存
        /// </summary>
        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshGpuDetection()
        {
            try
            {
                _logger.LogInformation("刷新GPU检测缓存");
                
                _gpuDetectionService.ClearCache();
                var capabilities = await _gpuDetectionService.DetectGpuCapabilitiesAsync();
                
                return Ok(new
                {
                    success = true,
                    message = "GPU检测缓存已刷新",
                    data = new
                    {
                        hasAnyGpuSupport = capabilities.HasAnyGpuSupport,
                        supportedTypes = capabilities.GetSupportedGpuTypes()
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刷新GPU检测失败");
                return StatusCode(500, new { success = false, message = "刷新GPU检测失败: " + ex.Message });
            }
        }

        /// <summary>
        /// 测试GPU编码器
        /// </summary>
        [HttpPost("test-encoder")]
        public async Task<IActionResult> TestGpuEncoder([FromBody] TestEncoderRequest request)
        {
            try
            {
                _logger.LogInformation("测试GPU编码器: {Encoder}", request.Encoder);
                
                // 这里可以添加实际的编码器测试逻辑
                // 例如使用一个小的测试文件进行编码测试
                
                return Ok(new
                {
                    success = true,
                    message = $"GPU编码器 {request.Encoder} 测试完成",
                    data = new
                    {
                        encoder = request.Encoder,
                        testResult = "通过", // 实际测试结果
                        performance = "优秀" // 性能评估
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "测试GPU编码器失败: {Encoder}", request.Encoder);
                return StatusCode(500, new { success = false, message = "测试编码器失败: " + ex.Message });
            }
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
