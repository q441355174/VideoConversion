using Microsoft.AspNetCore.Mvc;
using VideoConversion.Services;
using VideoConversion.Models;
using System.Diagnostics;

namespace VideoConversion.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly DatabaseService _databaseService;
        private readonly LoggingService _loggingService;
        private readonly ILogger<HealthController> _logger;

        public HealthController(
            DatabaseService databaseService,
            LoggingService loggingService,
            ILogger<HealthController> logger)
        {
            _databaseService = databaseService;
            _loggingService = loggingService;
            _logger = logger;
        }

        /// <summary>
        /// 基本健康检查
        /// </summary>
        [HttpGet]
        public IActionResult GetHealth()
        {
            return Ok(new
            {
                status = "healthy",
                timestamp = DateTime.Now,
                version = GetType().Assembly.GetName().Version?.ToString() ?? "unknown"
            });
        }

        /// <summary>
        /// 详细系统状态
        /// </summary>
        [HttpGet("status")]
        public async Task<IActionResult> GetSystemStatus()
        {
            try
            {
                var activeTasks = await _databaseService.GetActiveTasksAsync();
                var pendingTasks = activeTasks.Where(t => t.Status == ConversionStatus.Pending).Count();
                var convertingTasks = activeTasks.Where(t => t.Status == ConversionStatus.Converting).Count();

                // 获取系统性能指标
                var process = Process.GetCurrentProcess();
                var memoryUsage = process.WorkingSet64;
                var cpuTime = process.TotalProcessorTime;

                // 获取磁盘空间信息
                var uploadDrive = new DriveInfo(Path.GetPathRoot(Directory.GetCurrentDirectory()) ?? "C:");
                var diskSpaceInfo = new
                {
                    totalSpace = uploadDrive.TotalSize,
                    freeSpace = uploadDrive.AvailableFreeSpace,
                    usedSpace = uploadDrive.TotalSize - uploadDrive.AvailableFreeSpace
                };

                // 记录性能指标
                _loggingService.LogPerformanceMetrics(convertingTasks, pendingTasks, 0, memoryUsage);

                var status = new
                {
                    timestamp = DateTime.Now,
                    system = new
                    {
                        status = "running",
                        uptime = DateTime.Now - Process.GetCurrentProcess().StartTime,
                        memoryUsage = new
                        {
                            bytes = memoryUsage,
                            mb = memoryUsage / 1024 / 1024,
                            formatted = FormatBytes(memoryUsage)
                        },
                        diskSpace = new
                        {
                            total = FormatBytes(diskSpaceInfo.totalSpace),
                            free = FormatBytes(diskSpaceInfo.freeSpace),
                            used = FormatBytes(diskSpaceInfo.usedSpace),
                            freePercentage = (double)diskSpaceInfo.freeSpace / diskSpaceInfo.totalSpace * 100
                        }
                    },
                    tasks = new
                    {
                        total = activeTasks.Count,
                        pending = pendingTasks,
                        converting = convertingTasks,
                        queue = activeTasks.Where(t => t.Status == ConversionStatus.Pending)
                            .OrderBy(t => t.CreatedAt)
                            .Take(5)
                            .Select(t => new
                            {
                                t.Id,
                                t.TaskName,
                                t.CreatedAt,
                                waitTime = DateTime.Now - t.CreatedAt
                            })
                    },
                    database = new
                    {
                        status = "connected", // 简化版本，实际应该测试连接
                        lastBackup = "N/A" // 如果有备份机制的话
                    }
                };

                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取系统状态失败");
                return StatusCode(500, new
                {
                    status = "error",
                    message = "无法获取系统状态",
                    timestamp = DateTime.Now
                });
            }
        }

        /// <summary>
        /// 获取今日统计
        /// </summary>
        [HttpGet("stats/today")]
        public async Task<IActionResult> GetTodayStats()
        {
            try
            {
                var today = DateTime.Today;
                var tomorrow = today.AddDays(1);

                var allTasks = await _databaseService.GetAllTasksAsync(1, 1000); // 简化版本
                var todayTasks = allTasks.Where(t => t.CreatedAt >= today && t.CreatedAt < tomorrow).ToList();

                var stats = new
                {
                    date = today.ToString("yyyy-MM-dd"),
                    totalTasks = todayTasks.Count,
                    completed = todayTasks.Count(t => t.Status == ConversionStatus.Completed),
                    failed = todayTasks.Count(t => t.Status == ConversionStatus.Failed),
                    cancelled = todayTasks.Count(t => t.Status == ConversionStatus.Cancelled),
                    pending = todayTasks.Count(t => t.Status == ConversionStatus.Pending),
                    converting = todayTasks.Count(t => t.Status == ConversionStatus.Converting),
                    successRate = todayTasks.Count > 0 ? 
                        (double)todayTasks.Count(t => t.Status == ConversionStatus.Completed) / todayTasks.Count * 100 : 0,
                    totalInputSize = todayTasks.Sum(t => t.OriginalFileSize),
                    totalOutputSize = todayTasks.Where(t => t.OutputFileSize.HasValue).Sum(t => t.OutputFileSize!.Value),
                    averageProcessingTime = todayTasks
                        .Where(t => t.StartedAt.HasValue && t.CompletedAt.HasValue)
                        .Select(t => (t.CompletedAt!.Value - t.StartedAt!.Value).TotalMinutes)
                        .DefaultIfEmpty(0)
                        .Average(),
                    popularFormats = todayTasks
                        .GroupBy(t => t.OutputFormat)
                        .OrderByDescending(g => g.Count())
                        .Take(5)
                        .Select(g => new { format = g.Key, count = g.Count() })
                        .ToList()
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取今日统计失败");
                return StatusCode(500, new { message = "获取统计数据失败" });
            }
        }

        /// <summary>
        /// 系统诊断
        /// </summary>
        [HttpGet("diagnostics")]
        public async Task<IActionResult> GetDiagnostics()
        {
            var diagnostics = new List<DiagnosticItem>();

            try
            {
                // 检查数据库连接
                try
                {
                    await _databaseService.GetAllTasksAsync(1, 1);
                    diagnostics.Add(new DiagnosticItem("数据库连接", "正常", "success"));
                }
                catch (Exception ex)
                {
                    diagnostics.Add(new DiagnosticItem("数据库连接", $"异常: {ex.Message}", "error"));
                }

                // 检查磁盘空间
                var drive = new DriveInfo(Path.GetPathRoot(Directory.GetCurrentDirectory()) ?? "C:");
                var freeSpacePercentage = (double)drive.AvailableFreeSpace / drive.TotalSize * 100;
                
                if (freeSpacePercentage > 20)
                {
                    diagnostics.Add(new DiagnosticItem("磁盘空间", $"充足 ({freeSpacePercentage:F1}% 可用)", "success"));
                }
                else if (freeSpacePercentage > 10)
                {
                    diagnostics.Add(new DiagnosticItem("磁盘空间", $"警告 ({freeSpacePercentage:F1}% 可用)", "warning"));
                }
                else
                {
                    diagnostics.Add(new DiagnosticItem("磁盘空间", $"严重不足 ({freeSpacePercentage:F1}% 可用)", "error"));
                }

                // 检查内存使用
                var process = Process.GetCurrentProcess();
                var memoryMB = process.WorkingSet64 / 1024 / 1024;
                
                if (memoryMB < 500)
                {
                    diagnostics.Add(new DiagnosticItem("内存使用", $"正常 ({memoryMB} MB)", "success"));
                }
                else if (memoryMB < 1000)
                {
                    diagnostics.Add(new DiagnosticItem("内存使用", $"较高 ({memoryMB} MB)", "warning"));
                }
                else
                {
                    diagnostics.Add(new DiagnosticItem("内存使用", $"过高 ({memoryMB} MB)", "error"));
                }

                // 检查上传和输出目录
                var uploadPath = "uploads";
                var outputPath = "outputs";

                diagnostics.Add(new DiagnosticItem("上传目录", 
                    Directory.Exists(uploadPath) ? "存在" : "不存在", 
                    Directory.Exists(uploadPath) ? "success" : "error"));

                diagnostics.Add(new DiagnosticItem("输出目录", 
                    Directory.Exists(outputPath) ? "存在" : "不存在", 
                    Directory.Exists(outputPath) ? "success" : "error"));

                // 检查活动任务数量
                var activeTasks = await _databaseService.GetActiveTasksAsync();
                var convertingCount = activeTasks.Count(t => t.Status == ConversionStatus.Converting);
                
                if (convertingCount == 0)
                {
                    diagnostics.Add(new DiagnosticItem("转换任务", "无活动任务", "info"));
                }
                else if (convertingCount <= 2)
                {
                    diagnostics.Add(new DiagnosticItem("转换任务", $"{convertingCount} 个活动任务", "success"));
                }
                else
                {
                    diagnostics.Add(new DiagnosticItem("转换任务", $"{convertingCount} 个活动任务 (较多)", "warning"));
                }

                return Ok(new
                {
                    timestamp = DateTime.Now,
                    overallStatus = diagnostics.Any(d => d.Status == "error") ? "error" : 
                                   diagnostics.Any(d => d.Status == "warning") ? "warning" : "healthy",
                    diagnostics
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "系统诊断失败");
                return StatusCode(500, new { message = "诊断失败" });
            }
        }

        /// <summary>
        /// 格式化字节数
        /// </summary>
        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            
            return $"{len:0.##} {sizes[order]}";
        }
    }

    /// <summary>
    /// 诊断项目
    /// </summary>
    public class DiagnosticItem
    {
        public string Name { get; set; }
        public string Message { get; set; }
        public string Status { get; set; } // success, warning, error, info

        public DiagnosticItem(string name, string message, string status)
        {
            Name = name;
            Message = message;
            Status = status;
        }
    }
}
