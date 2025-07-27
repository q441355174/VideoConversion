using Microsoft.AspNetCore.Mvc;
using VideoConversion.Services;

namespace VideoConversion.Controllers
{
    /// <summary>
    /// 清理管理API控制器
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class CleanupController : ControllerBase
    {
        private readonly AdvancedFileCleanupService _cleanupService;
        private readonly ILogger<CleanupController> _logger;

        public CleanupController(
            AdvancedFileCleanupService cleanupService,
            ILogger<CleanupController> logger)
        {
            _cleanupService = cleanupService;
            _logger = logger;
        }

        /// <summary>
        /// 获取清理配置
        /// </summary>
        [HttpGet("config")]
        public async Task<IActionResult> GetCleanupConfig()
        {
            try
            {
                // TODO: 从数据库获取配置
                var config = new CleanupConfig(); // 临时返回默认配置
                
                return Ok(new
                {
                    success = true,
                    data = config,
                    message = "获取清理配置成功"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取清理配置失败");
                return StatusCode(500, new
                {
                    success = false,
                    message = "获取清理配置失败",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// 更新清理配置
        /// </summary>
        [HttpPost("config")]
        public async Task<IActionResult> UpdateCleanupConfig([FromBody] CleanupConfig config)
        {
            try
            {
                if (config == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "配置数据不能为空"
                    });
                }

                // 验证配置
                if (config.EmergencyCleanupThreshold <= config.AggressiveCleanupThreshold)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "紧急清理阈值必须大于激进清理阈值"
                    });
                }

                if (config.ConversionCleanupDelayMinutes < 0 || 
                    config.DownloadCleanupDelayHours < 0 || 
                    config.TempFileRetentionHours < 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "保留时间不能为负数"
                    });
                }

                await _cleanupService.UpdateCleanupConfigAsync(config);

                _logger.LogInformation("清理配置已更新");

                return Ok(new
                {
                    success = true,
                    message = "清理配置更新成功"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新清理配置失败");
                return StatusCode(500, new
                {
                    success = false,
                    message = "更新清理配置失败",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// 手动触发清理
        /// </summary>
        [HttpPost("manual")]
        public async Task<IActionResult> TriggerManualCleanup([FromBody] ManualCleanupRequest request)
        {
            try
            {
                if (request == null)
                {
                    request = new ManualCleanupRequest(); // 使用默认配置
                }

                _logger.LogInformation("开始手动清理: {Request}", request);

                var result = await _cleanupService.PerformManualCleanupAsync(request);

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        totalCleanedSize = result.TotalCleanedSize,
                        totalCleanedSizeMB = Math.Round(result.TotalCleanedSize / 1024.0 / 1024, 2),
                        totalCleanedFiles = result.TotalCleanedFiles,
                        details = new
                        {
                            originalFiles = new { size = result.OriginalFilesCleanedSize, count = result.OriginalFilesCleanedCount },
                            convertedFiles = new { size = result.ConvertedFilesCleanedSize, count = result.ConvertedFilesCleanedCount },
                            tempFiles = new { size = result.TempFilesCleanedSize, count = result.TempFilesCleanedCount },
                            orphanFiles = new { size = result.OrphanFilesCleanedSize, count = result.OrphanFilesCleanedCount },
                            logFiles = new { size = result.LogFilesCleanedSize, count = result.LogFilesCleanedCount }
                        }
                    },
                    message = $"手动清理完成，释放空间 {result.TotalCleanedSize / 1024.0 / 1024:F2} MB"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "手动清理失败");
                return StatusCode(500, new
                {
                    success = false,
                    message = "手动清理失败",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// 获取清理统计信息
        /// </summary>
        [HttpGet("statistics")]
        public async Task<IActionResult> GetCleanupStatistics()
        {
            try
            {
                // TODO: 从数据库获取统计信息
                var statistics = new CleanupStatistics(); // 临时返回空统计

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        totalCleanedSize = statistics.TotalCleanedSize,
                        totalCleanedSizeMB = Math.Round(statistics.TotalCleanedSize / 1024.0 / 1024, 2),
                        totalCleanedFiles = statistics.TotalCleanedFiles,
                        totalCleanupRuns = statistics.TotalCleanupRuns,
                        lastCleanupTime = statistics.LastCleanupTime,
                        lastEmergencyCleanupTime = statistics.LastEmergencyCleanupTime
                    },
                    message = "获取清理统计成功"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取清理统计失败");
                return StatusCode(500, new
                {
                    success = false,
                    message = "获取清理统计失败",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// 获取可清理文件预览
        /// </summary>
        [HttpGet("preview")]
        public async Task<IActionResult> GetCleanupPreview([FromQuery] bool includeTempFiles = true,
            [FromQuery] bool includeDownloadedFiles = false,
            [FromQuery] bool includeOrphanFiles = true,
            [FromQuery] bool includeFailedTasks = false,
            [FromQuery] bool includeLogFiles = false)
        {
            try
            {
                var preview = new
                {
                    tempFiles = includeTempFiles ? await GetTempFilesPreviewAsync() : null,
                    downloadedFiles = includeDownloadedFiles ? await GetDownloadedFilesPreviewAsync() : null,
                    orphanFiles = includeOrphanFiles ? await GetOrphanFilesPreviewAsync() : null,
                    failedTaskFiles = includeFailedTasks ? await GetFailedTaskFilesPreviewAsync() : null,
                    logFiles = includeLogFiles ? await GetLogFilesPreviewAsync() : null
                };

                return Ok(new
                {
                    success = true,
                    data = preview,
                    message = "获取清理预览成功"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取清理预览失败");
                return StatusCode(500, new
                {
                    success = false,
                    message = "获取清理预览失败",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// 清理特定类型的文件
        /// </summary>
        [HttpPost("cleanup/{type}")]
        public async Task<IActionResult> CleanupSpecificType(string type, [FromQuery] bool ignoreRetention = false)
        {
            try
            {
                var request = new ManualCleanupRequest
                {
                    IgnoreRetention = ignoreRetention
                };

                switch (type.ToLower())
                {
                    case "temp":
                        request.CleanupTempFiles = true;
                        break;
                    case "downloaded":
                        request.CleanupDownloadedFiles = true;
                        break;
                    case "orphan":
                        request.CleanupOrphanFiles = true;
                        break;
                    case "failed":
                        request.CleanupFailedTasks = true;
                        break;
                    case "logs":
                        request.CleanupLogFiles = true;
                        break;
                    default:
                        return BadRequest(new
                        {
                            success = false,
                            message = "不支持的清理类型。支持的类型: temp, downloaded, orphan, failed, logs"
                        });
                }

                var result = await _cleanupService.PerformManualCleanupAsync(request);

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        cleanupType = type,
                        totalCleanedSize = result.TotalCleanedSize,
                        totalCleanedSizeMB = Math.Round(result.TotalCleanedSize / 1024.0 / 1024, 2),
                        totalCleanedFiles = result.TotalCleanedFiles
                    },
                    message = $"{type} 文件清理完成"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理特定类型文件失败: {Type}", type);
                return StatusCode(500, new
                {
                    success = false,
                    message = $"清理 {type} 文件失败",
                    error = ex.Message
                });
            }
        }

        #region 私有方法

        private async Task<object> GetTempFilesPreviewAsync()
        {
            try
            {
                var tempDirectories = new[] { "temp", "uploads/temp", "uploads/chunks" };
                var totalSize = 0L;
                var totalCount = 0;
                var oldestFile = DateTime.MaxValue;
                var newestFile = DateTime.MinValue;

                foreach (var tempDir in tempDirectories)
                {
                    if (!Directory.Exists(tempDir)) continue;

                    var files = Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories);
                    
                    foreach (var file in files)
                    {
                        try
                        {
                            var fileInfo = new FileInfo(file);
                            totalSize += fileInfo.Length;
                            totalCount++;
                            
                            if (fileInfo.LastWriteTime < oldestFile)
                                oldestFile = fileInfo.LastWriteTime;
                            if (fileInfo.LastWriteTime > newestFile)
                                newestFile = fileInfo.LastWriteTime;
                        }
                        catch
                        {
                            // 忽略无法访问的文件
                        }
                    }
                }

                return new
                {
                    totalSize = totalSize,
                    totalSizeMB = Math.Round(totalSize / 1024.0 / 1024, 2),
                    totalCount = totalCount,
                    oldestFile = oldestFile == DateTime.MaxValue ? (DateTime?)null : oldestFile,
                    newestFile = newestFile == DateTime.MinValue ? (DateTime?)null : newestFile
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取临时文件预览失败");
                return new { error = ex.Message };
            }
        }

        private async Task<object> GetDownloadedFilesPreviewAsync()
        {
            // TODO: 实现已下载文件预览
            return new { message = "已下载文件预览功能开发中" };
        }

        private async Task<object> GetOrphanFilesPreviewAsync()
        {
            try
            {
                // TODO: 实现孤儿文件检测逻辑
                return new { message = "孤儿文件预览功能开发中" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取孤儿文件预览失败");
                return new { error = ex.Message };
            }
        }

        private async Task<object> GetFailedTaskFilesPreviewAsync()
        {
            // TODO: 实现失败任务文件预览
            return new { message = "失败任务文件预览功能开发中" };
        }

        private async Task<object> GetLogFilesPreviewAsync()
        {
            try
            {
                var logDirectories = new[] { "logs", "Logs" };
                var totalSize = 0L;
                var totalCount = 0;

                foreach (var logDir in logDirectories)
                {
                    if (!Directory.Exists(logDir)) continue;

                    var files = Directory.GetFiles(logDir, "*.log", SearchOption.AllDirectories);
                    
                    foreach (var file in files)
                    {
                        try
                        {
                            var fileInfo = new FileInfo(file);
                            totalSize += fileInfo.Length;
                            totalCount++;
                        }
                        catch
                        {
                            // 忽略无法访问的文件
                        }
                    }
                }

                return new
                {
                    totalSize = totalSize,
                    totalSizeMB = Math.Round(totalSize / 1024.0 / 1024, 2),
                    totalCount = totalCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取日志文件预览失败");
                return new { error = ex.Message };
            }
        }

        #endregion
    }
}
