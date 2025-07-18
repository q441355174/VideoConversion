using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using VideoConversion.Services;

namespace VideoConversion.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DebugController : ControllerBase
    {
        private readonly ILogger<DebugController> _logger;
        private readonly DatabaseService _databaseService;
        private readonly FileService _fileService;

        public DebugController(ILogger<DebugController> logger, DatabaseService databaseService, FileService fileService)
        {
            _logger = logger;
            _databaseService = databaseService;
            _fileService = fileService;
        }

        [HttpPost("test")]
        public IActionResult TestUpload([FromForm] TestRequest request)
        {
            try
            {
                _logger.LogInformation("=== 调试测试开始 ===");
                _logger.LogInformation("文件: {FileName}", request.VideoFile?.FileName);
                _logger.LogInformation("任务名称: {TaskName}", request.TaskName);
                _logger.LogInformation("预设: {Preset}", request.Preset);
                _logger.LogInformation("音频音量: {AudioVolume}", request.AudioVolume);
                _logger.LogInformation("快速启动: {FastStart}", request.FastStart);
                _logger.LogInformation("复制时间戳: {CopyTimestamps}", request.CopyTimestamps);

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("模型验证失败:");
                    foreach (var error in ModelState)
                    {
                        _logger.LogWarning("字段 {Field}: {Errors}", error.Key, 
                            string.Join(", ", error.Value.Errors.Select(e => e.ErrorMessage)));
                    }
                    return BadRequest(ModelState);
                }

                return Ok(new { 
                    success = true, 
                    message = "测试成功",
                    data = new {
                        fileName = request.VideoFile?.FileName,
                        taskName = request.TaskName,
                        preset = request.Preset,
                        audioVolume = request.AudioVolume,
                        fastStart = request.FastStart,
                        copyTimestamps = request.CopyTimestamps
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "调试测试失败");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("cleanup")]
        public async Task<IActionResult> CleanupTestData()
        {
            try
        {
                _logger.LogInformation("=== 开始清理测试数据 ===");

                // 获取所有任务
                var allTasks = await _databaseService.GetAllTasksAsync(1, 1000);
                _logger.LogInformation("找到 {Count} 个任务", allTasks.Count);

                int deletedTasks = 0;
                int deletedFiles = 0;

                foreach (var task in allTasks)
                {
                    try
                    {
                        // 删除相关文件
                        if (!string.IsNullOrEmpty(task.OriginalFilePath) && System.IO.File.Exists(task.OriginalFilePath))
                        {
                            System.IO.File.Delete(task.OriginalFilePath);
                            deletedFiles++;
                            _logger.LogInformation("删除原始文件: {FilePath}", task.OriginalFilePath);
                        }

                        if (!string.IsNullOrEmpty(task.OutputFilePath) && System.IO.File.Exists(task.OutputFilePath))
                        {
                            System.IO.File.Delete(task.OutputFilePath);
                            deletedFiles++;
                            _logger.LogInformation("删除输出文件: {FilePath}", task.OutputFilePath);
                        }

                        // 删除数据库记录
                        await _databaseService.DeleteTaskAsync(task.Id);
                        deletedTasks++;
                        _logger.LogInformation("删除任务记录: {TaskId} - {TaskName}", task.Id, task.TaskName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "删除任务失败: {TaskId}", task.Id);
                    }
                }

                // 清理空的上传和输出目录中的文件
                var uploadsDir = "uploads";
                var outputsDir = "outputs";

                if (Directory.Exists(uploadsDir))
                {
                    var uploadFiles = Directory.GetFiles(uploadsDir);
                    foreach (var file in uploadFiles)
                    {
                        try
                        {
                            System.IO.File.Delete(file);
                            deletedFiles++;
                            _logger.LogInformation("删除上传文件: {FilePath}", file);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "删除上传文件失败: {FilePath}", file);
                        }
                    }
                }

                if (Directory.Exists(outputsDir))
                {
                    var outputFiles = Directory.GetFiles(outputsDir);
                    foreach (var file in outputFiles)
                    {
                        try
                        {
                            System.IO.File.Delete(file);
                            deletedFiles++;
                            _logger.LogInformation("删除输出文件: {FilePath}", file);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "删除输出文件失败: {FilePath}", file);
                        }
                    }
                }

                _logger.LogInformation("=== 清理完成 ===");
                _logger.LogInformation("删除任务: {DeletedTasks} 个", deletedTasks);
                _logger.LogInformation("删除文件: {DeletedFiles} 个", deletedFiles);

                return Ok(new
                {
                    success = true,
                    message = "测试数据清理完成",
                    data = new
                    {
                        deletedTasks = deletedTasks,
                        deletedFiles = deletedFiles
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理测试数据失败");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("tasks")]
        public async Task<IActionResult> GetRawTasks()
        {
            try
            {
                _logger.LogInformation("=== 获取原始任务数据 ===");

                var tasks = await _databaseService.GetAllTasksAsync(1, 100);
                _logger.LogInformation("获取到 {Count} 个任务", tasks?.Count ?? 0);

                if (tasks != null && tasks.Any())
                {
                    foreach (var task in tasks.Take(3)) // 只显示前3个任务的详细信息
                    {
                        _logger.LogInformation("任务详情: ID={Id}, TaskName={TaskName}, OriginalFileName={OriginalFileName}, OutputFormat={OutputFormat}, Status={Status}",
                            task.Id, task.TaskName, task.OriginalFileName, task.OutputFormat, task.Status);
                    }
                }

                return Ok(new
                {
                    success = true,
                    count = tasks?.Count ?? 0,
                    tasks = tasks?.Select(t => new
                    {
                        t.Id,
                        t.TaskName,
                        t.OriginalFileName,
                        t.OutputFileName,
                        t.OriginalFileSize,
                        t.OutputFileSize,
                        t.InputFormat,
                        t.OutputFormat,
                        t.VideoCodec,
                        t.AudioCodec,
                        Status = t.Status.ToString(),
                        t.Progress,
                        t.CreatedAt,
                        t.StartedAt,
                        t.CompletedAt,
                        t.ErrorMessage
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取原始任务数据失败");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }

    public class TestRequest
    {
        [Required(ErrorMessage = "请选择要转换的视频文件")]
        public IFormFile VideoFile { get; set; } = null!;
        
        public string? TaskName { get; set; }
        public string Preset { get; set; } = string.Empty;
        public string? OutputFormat { get; set; }
        public string? VideoCodec { get; set; }
        public string? AudioCodec { get; set; }
        public string? QualityMode { get; set; }
        public string? EncodingPreset { get; set; }
        public string? AudioQualityMode { get; set; }
        public string? AudioBitrate { get; set; }
        public int? AudioVolume { get; set; } = 100;
        public bool FastStart { get; set; } = false;
        public bool CopyTimestamps { get; set; } = false;
    }
}
