using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace VideoConversion.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DebugController : ControllerBase
    {
        private readonly ILogger<DebugController> _logger;

        public DebugController(ILogger<DebugController> logger)
        {
            _logger = logger;
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
