using Microsoft.AspNetCore.Mvc;
using VideoConversion.Services;
using VideoConversion.Models;

namespace VideoConversion.Controllers
{
    /// <summary>
    /// 下载管理API控制器
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class DownloadController : ControllerBase
    {
        private readonly DatabaseService _databaseService;
        private readonly DownloadTrackingService _downloadTrackingService;
        private readonly ILogger<DownloadController> _logger;

        public DownloadController(
            DatabaseService databaseService,
            DownloadTrackingService downloadTrackingService,
            ILogger<DownloadController> logger)
        {
            _databaseService = databaseService;
            _downloadTrackingService = downloadTrackingService;
            _logger = logger;
        }

        /// <summary>
        /// 下载转换后的文件
        /// </summary>
        [HttpGet("{taskId}")]
        public async Task<IActionResult> DownloadFile(string taskId)
        {
            try
            {
                var task = await _databaseService.GetTaskAsync(taskId);
                if (task == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "任务不存在"
                    });
                }

                if (task.Status != ConversionStatus.Completed)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "任务尚未完成，无法下载"
                    });
                }

                if (string.IsNullOrEmpty(task.OutputFilePath) || !System.IO.File.Exists(task.OutputFilePath))
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "文件不存在或已被清理"
                    });
                }

                // 获取客户端信息
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
                var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

                // 跟踪下载
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _downloadTrackingService.TrackDownloadAsync(taskId, clientIp, userAgent);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "跟踪下载失败: {TaskId}", taskId);
                    }
                });

                // 返回文件
                var fileBytes = await System.IO.File.ReadAllBytesAsync(task.OutputFilePath);
                var fileName = Path.GetFileName(task.OutputFilePath);
                var contentType = GetContentType(fileName);

                _logger.LogInformation("📥 文件下载: TaskId={TaskId}, FileName={FileName}, ClientIp={ClientIp}", 
                    taskId, fileName, clientIp);

                return File(fileBytes, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "下载文件失败: {TaskId}", taskId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "下载文件失败",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// 获取下载记录
        /// </summary>
        [HttpGet("{taskId}/record")]
        public async Task<IActionResult> GetDownloadRecord(string taskId)
        {
            try
            {
                var record = await _downloadTrackingService.GetDownloadRecordAsync(taskId);
                if (record == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "下载记录不存在"
                    });
                }

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        taskId = record.TaskId,
                        fileName = record.FileName,
                        fileSize = record.FileSize,
                        fileSizeMB = Math.Round(record.FileSize / 1024.0 / 1024, 2),
                        downloadTime = record.DownloadTime,
                        scheduledCleanupTime = record.ScheduledCleanupTime,
                        isCleanedUp = record.IsCleanedUp,
                        cleanedUpTime = record.CleanedUpTime,
                        clientIp = record.ClientIp,
                        retentionHours = record.IsCleanedUp 
                            ? (record.CleanedUpTime - record.DownloadTime)?.TotalHours 
                            : (DateTime.Now - record.DownloadTime).TotalHours
                    },
                    message = "获取下载记录成功"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取下载记录失败: {TaskId}", taskId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "获取下载记录失败",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// 获取下载统计信息
        /// </summary>
        [HttpGet("statistics")]
        public async Task<IActionResult> GetDownloadStatistics([FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var statistics = await _downloadTrackingService.GetDownloadStatisticsAsync(startDate, endDate);

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        totalDownloads = statistics.TotalDownloads,
                        totalDownloadedSize = statistics.TotalDownloadedSize,
                        totalDownloadedSizeMB = Math.Round(statistics.TotalDownloadedSizeMB, 2),
                        uniqueClients = statistics.UniqueClients,
                        averageFileSize = statistics.AverageFileSize,
                        averageFileSizeMB = Math.Round(statistics.AverageFileSizeMB, 2),
                        startDate = statistics.StartDate,
                        endDate = statistics.EndDate,
                        periodDays = (statistics.EndDate - statistics.StartDate).Days
                    },
                    message = "获取下载统计成功"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取下载统计失败");
                return StatusCode(500, new
                {
                    success = false,
                    message = "获取下载统计失败",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// 获取热门下载
        /// </summary>
        [HttpGet("popular")]
        public async Task<IActionResult> GetPopularDownloads([FromQuery] int topCount = 10)
        {
            try
            {
                var popularDownloads = await _downloadTrackingService.GetPopularDownloadsAsync(topCount);

                return Ok(new
                {
                    success = true,
                    data = popularDownloads.Select(p => new
                    {
                        taskId = p.TaskId,
                        fileName = p.FileName,
                        downloadCount = p.DownloadCount,
                        totalSize = p.TotalSize,
                        totalSizeMB = Math.Round(p.TotalSizeMB, 2),
                        lastDownloadTime = p.LastDownloadTime
                    }),
                    message = "获取热门下载成功"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取热门下载失败");
                return StatusCode(500, new
                {
                    success = false,
                    message = "获取热门下载失败",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// 获取待清理的下载文件
        /// </summary>
        [HttpGet("pending-cleanup")]
        public async Task<IActionResult> GetPendingCleanupFiles()
        {
            try
            {
                var pendingRecords = await _downloadTrackingService.GetPendingCleanupRecordsAsync();

                return Ok(new
                {
                    success = true,
                    data = pendingRecords.Select(r => new
                    {
                        taskId = r.TaskId,
                        fileName = r.FileName,
                        fileSize = r.FileSize,
                        fileSizeMB = Math.Round(r.FileSize / 1024.0 / 1024, 2),
                        downloadTime = r.DownloadTime,
                        scheduledCleanupTime = r.ScheduledCleanupTime,
                        hoursUntilCleanup = Math.Round((r.ScheduledCleanupTime - DateTime.Now).TotalHours, 1),
                        retentionHours = Math.Round((DateTime.Now - r.DownloadTime).TotalHours, 1)
                    }),
                    message = "获取待清理文件成功"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取待清理文件失败");
                return StatusCode(500, new
                {
                    success = false,
                    message = "获取待清理文件失败",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// 手动清理已下载文件
        /// </summary>
        [HttpPost("{taskId}/cleanup")]
        public async Task<IActionResult> ManualCleanupDownloadedFile(string taskId)
        {
            try
            {
                var record = await _downloadTrackingService.GetDownloadRecordAsync(taskId);
                if (record == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "下载记录不存在"
                    });
                }

                if (record.IsCleanedUp)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "文件已经被清理"
                    });
                }

                var task = await _databaseService.GetTaskAsync(taskId);
                if (task == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "任务不存在"
                    });
                }

                if (string.IsNullOrEmpty(task.OutputFilePath) || !System.IO.File.Exists(task.OutputFilePath))
                {
                    // 文件不存在，直接标记为已清理
                    await _downloadTrackingService.MarkAsCleanedUpAsync(taskId);
                    return Ok(new
                    {
                        success = true,
                        message = "文件不存在，已标记为清理完成"
                    });
                }

                // 执行清理
                var fileInfo = new FileInfo(task.OutputFilePath);
                var fileSize = fileInfo.Length;
                
                System.IO.File.Delete(task.OutputFilePath);
                await _downloadTrackingService.MarkAsCleanedUpAsync(taskId);

                _logger.LogInformation("🗑️ 手动清理下载文件: TaskId={TaskId}, FileName={FileName}, Size={SizeMB:F2}MB", 
                    taskId, record.FileName, fileSize / 1024.0 / 1024);

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        taskId = taskId,
                        fileName = record.FileName,
                        fileSize = fileSize,
                        fileSizeMB = Math.Round(fileSize / 1024.0 / 1024, 2),
                        cleanupTime = DateTime.Now
                    },
                    message = "文件清理成功"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "手动清理下载文件失败: {TaskId}", taskId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "清理文件失败",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// 延长文件保留时间
        /// </summary>
        [HttpPost("{taskId}/extend")]
        public async Task<IActionResult> ExtendRetentionTime(string taskId, [FromBody] ExtendRetentionRequest request)
        {
            try
            {
                if (request == null || request.ExtendHours <= 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "延长时间必须大于0"
                    });
                }

                var record = await _downloadTrackingService.GetDownloadRecordAsync(taskId);
                if (record == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "下载记录不存在"
                    });
                }

                if (record.IsCleanedUp)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "文件已经被清理，无法延长保留时间"
                    });
                }

                // 更新清理时间
                var newCleanupTime = record.ScheduledCleanupTime.AddHours(request.ExtendHours);
                
                var sql = @"
                    UPDATE DownloadRecord 
                    SET ScheduledCleanupTime = @NewCleanupTime 
                    WHERE TaskId = @TaskId";

                await _databaseService.GetDatabaseAsync().Ado.ExecuteCommandAsync(sql, new 
                { 
                    TaskId = taskId,
                    NewCleanupTime = newCleanupTime
                });

                _logger.LogInformation("⏰ 延长文件保留时间: TaskId={TaskId}, 延长={ExtendHours}小时, 新清理时间={NewCleanupTime}", 
                    taskId, request.ExtendHours, newCleanupTime);

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        taskId = taskId,
                        fileName = record.FileName,
                        originalCleanupTime = record.ScheduledCleanupTime,
                        newCleanupTime = newCleanupTime,
                        extendedHours = request.ExtendHours
                    },
                    message = $"文件保留时间已延长 {request.ExtendHours} 小时"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "延长文件保留时间失败: {TaskId}", taskId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "延长保留时间失败",
                    error = ex.Message
                });
            }
        }

        #region 私有方法

        /// <summary>
        /// 获取文件的Content-Type
        /// </summary>
        private string GetContentType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".mp4" => "video/mp4",
                ".avi" => "video/x-msvideo",
                ".mov" => "video/quicktime",
                ".wmv" => "video/x-ms-wmv",
                ".flv" => "video/x-flv",
                ".webm" => "video/webm",
                ".mkv" => "video/x-matroska",
                ".m4v" => "video/x-m4v",
                _ => "application/octet-stream"
            };
        }

        #endregion
    }

    #region 请求模型

    /// <summary>
    /// 延长保留时间请求
    /// </summary>
    public class ExtendRetentionRequest
    {
        public double ExtendHours { get; set; }
    }

    #endregion
}
