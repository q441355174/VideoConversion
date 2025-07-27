using Microsoft.AspNetCore.Mvc;
using VideoConversion.Models;
using VideoConversion.Services;

namespace VideoConversion.Controllers
{
    /// <summary>
    /// 磁盘空间管理控制器
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class DiskSpaceController : ControllerBase
    {
        private readonly DiskSpaceService _diskSpaceService;
        private readonly ILogger<DiskSpaceController> _logger;

        public DiskSpaceController(
            DiskSpaceService diskSpaceService,
            ILogger<DiskSpaceController> logger)
        {
            _diskSpaceService = diskSpaceService;
            _logger = logger;
        }

        /// <summary>
        /// 获取磁盘空间配置
        /// </summary>
        [HttpGet("config")]
        public async Task<IActionResult> GetSpaceConfig()
        {
            try
            {
                var config = await _diskSpaceService.GetSpaceConfigAsync();
                
                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        maxTotalSpaceGB = Math.Round(config.MaxTotalSpace / 1024.0 / 1024 / 1024, 2),
                        reservedSpaceGB = Math.Round(config.ReservedSpace / 1024.0 / 1024 / 1024, 2),
                        isEnabled = config.IsEnabled,
                        updatedAt = config.UpdatedAt,
                        updatedBy = config.UpdatedBy
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取磁盘空间配置失败");
                return StatusCode(500, new
                {
                    success = false,
                    message = "获取磁盘空间配置失败",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// 设置磁盘空间配置
        /// </summary>
        [HttpPost("config")]
        public async Task<IActionResult> SetSpaceConfig([FromBody] DiskSpaceConfigRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "配置参数无效",
                        errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
                    });
                }

                var config = new DiskSpaceConfig
                {
                    MaxTotalSpace = (long)(request.MaxTotalSpaceGB * 1024 * 1024 * 1024),
                    ReservedSpace = (long)(request.ReservedSpaceGB * 1024 * 1024 * 1024),
                    IsEnabled = request.IsEnabled,
                    UpdatedBy = "API"
                };

                var success = await _diskSpaceService.SetSpaceConfigAsync(config);

                if (success)
                {
                    _logger.LogInformation("磁盘空间配置已更新: MaxSpace={MaxSpaceGB}GB, ReservedSpace={ReservedSpaceGB}GB, Enabled={IsEnabled}",
                        request.MaxTotalSpaceGB, request.ReservedSpaceGB, request.IsEnabled);

                    return Ok(new
                    {
                        success = true,
                        message = "磁盘空间配置已更新",
                        data = new
                        {
                            maxTotalSpaceGB = request.MaxTotalSpaceGB,
                            reservedSpaceGB = request.ReservedSpaceGB,
                            isEnabled = request.IsEnabled
                        }
                    });
                }
                else
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "磁盘空间配置更新失败，请检查参数是否有效"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "设置磁盘空间配置失败");
                return StatusCode(500, new
                {
                    success = false,
                    message = "设置磁盘空间配置失败",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// 获取磁盘空间使用情况
        /// </summary>
        [HttpGet("usage")]
        public async Task<IActionResult> GetSpaceUsage()
        {
            try
            {
                var status = await _diskSpaceService.GetCurrentSpaceStatusAsync();
                var usage = await _diskSpaceService.CalculateCurrentUsageAsync();

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        // 空间状态
                        totalSpaceGB = Math.Round(status.TotalSpace / 1024.0 / 1024 / 1024, 2),
                        usedSpaceGB = Math.Round(status.UsedSpace / 1024.0 / 1024 / 1024, 2),
                        availableSpaceGB = Math.Round(status.AvailableSpace / 1024.0 / 1024 / 1024, 2),
                        reservedSpaceGB = Math.Round(status.ReservedSpace / 1024.0 / 1024 / 1024, 2),
                        usagePercentage = Math.Round(status.UsagePercentage, 1),
                        hasSufficientSpace = status.HasSufficientSpace,
                        
                        // 详细使用情况
                        details = new
                        {
                            uploadedFilesGB = Math.Round(usage.UploadedFilesSize / 1024.0 / 1024 / 1024, 2),
                            convertedFilesGB = Math.Round(usage.ConvertedFilesSize / 1024.0 / 1024 / 1024, 2),
                            tempFilesGB = Math.Round(usage.TempFilesSize / 1024.0 / 1024 / 1024, 2),
                            lastCalculatedAt = usage.LastCalculatedAt
                        },
                        
                        updateTime = status.UpdateTime
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取磁盘空间使用情况失败");
                return StatusCode(500, new
                {
                    success = false,
                    message = "获取磁盘空间使用情况失败",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// 检查任务空间需求
        /// </summary>
        [HttpPost("check-space")]
        public async Task<IActionResult> CheckSpace([FromBody] SpaceCheckRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "请求参数无效",
                        errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
                    });
                }

                var result = await _diskSpaceService.CheckSpaceAsync(request);

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        hasEnoughSpace = result.HasEnoughSpace,
                        requiredSpaceGB = Math.Round(result.RequiredSpace / 1024.0 / 1024 / 1024, 2),
                        availableSpaceGB = Math.Round(result.AvailableSpace / 1024.0 / 1024 / 1024, 2),
                        message = result.Message,
                        details = result.Details != null ? new
                        {
                            originalFileSpaceGB = Math.Round(result.Details.OriginalFileSpace / 1024.0 / 1024 / 1024, 2),
                            outputFileSpaceGB = Math.Round(result.Details.OutputFileSpace / 1024.0 / 1024 / 1024, 2),
                            tempFileSpaceGB = Math.Round(result.Details.TempFileSpace / 1024.0 / 1024 / 1024, 2),
                            reservedSpaceGB = Math.Round(result.Details.ReservedSpace / 1024.0 / 1024 / 1024, 2),
                            currentUsedSpaceGB = Math.Round(result.Details.CurrentUsedSpace / 1024.0 / 1024 / 1024, 2),
                            totalConfiguredSpaceGB = Math.Round(result.Details.TotalConfiguredSpace / 1024.0 / 1024 / 1024, 2)
                        } : null
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查任务空间需求失败");
                return StatusCode(500, new
                {
                    success = false,
                    message = "检查任务空间需求失败",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// 强制刷新空间使用情况
        /// </summary>
        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshSpaceUsage()
        {
            try
            {
                var usage = await _diskSpaceService.CalculateCurrentUsageAsync();
                var status = await _diskSpaceService.GetCurrentSpaceStatusAsync();

                _logger.LogInformation("手动刷新磁盘空间使用情况完成");

                return Ok(new
                {
                    success = true,
                    message = "空间使用情况已刷新",
                    data = new
                    {
                        totalUsedSpaceGB = Math.Round(usage.TotalUsedSpace / 1024.0 / 1024 / 1024, 2),
                        usagePercentage = Math.Round(status.UsagePercentage, 1),
                        refreshedAt = DateTime.Now
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刷新空间使用情况失败");
                return StatusCode(500, new
                {
                    success = false,
                    message = "刷新空间使用情况失败",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// 获取空间预估信息
        /// </summary>
        [HttpPost("estimate")]
        public async Task<IActionResult> EstimateSpace([FromBody] SpaceEstimateRequest request)
        {
            try
            {
                // 基于文件类型和转换设置预估输出大小
                var estimatedOutputSize = EstimateOutputSize(request.OriginalFileSize, request.OutputFormat, request.VideoCodec);
                
                var spaceCheckRequest = new SpaceCheckRequest
                {
                    OriginalFileSize = request.OriginalFileSize,
                    EstimatedOutputSize = estimatedOutputSize,
                    TaskType = "conversion",
                    IncludeTempSpace = true
                };

                var result = await _diskSpaceService.CheckSpaceAsync(spaceCheckRequest);

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        originalFileSizeGB = Math.Round(request.OriginalFileSize / 1024.0 / 1024 / 1024, 2),
                        estimatedOutputSizeGB = Math.Round(estimatedOutputSize / 1024.0 / 1024 / 1024, 2),
                        totalRequiredSpaceGB = Math.Round(result.RequiredSpace / 1024.0 / 1024 / 1024, 2),
                        hasEnoughSpace = result.HasEnoughSpace,
                        compressionRatio = Math.Round((double)estimatedOutputSize / request.OriginalFileSize, 2),
                        message = result.Message
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "预估空间需求失败");
                return StatusCode(500, new
                {
                    success = false,
                    message = "预估空间需求失败",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// 预估输出文件大小
        /// </summary>
        private long EstimateOutputSize(long originalSize, string? outputFormat, string? videoCodec)
        {
            // 基于编码器的压缩比
            var compressionRatio = (videoCodec?.ToLower()) switch
            {
                "h264" or "h264_nvenc" => 0.7,
                "h265" or "hevc" or "h265_nvenc" => 0.5,
                "av1" => 0.4,
                "vp9" => 0.6,
                _ => 0.8 // 默认压缩比
            };

            // 基于输出格式的调整
            var formatMultiplier = (outputFormat?.ToLower()) switch
            {
                "mp4" => 1.0,
                "mkv" => 1.05,
                "avi" => 1.1,
                "mov" => 1.02,
                "webm" => 0.9,
                _ => 1.0
            };

            return (long)(originalSize * compressionRatio * formatMultiplier);
        }
    }

    /// <summary>
    /// 空间预估请求
    /// </summary>
    public class SpaceEstimateRequest
    {
        public long OriginalFileSize { get; set; }
        public string? OutputFormat { get; set; }
        public string? VideoCodec { get; set; }
        public string? Resolution { get; set; }
        public int? VideoBitrate { get; set; }
    }
}
