using System.Diagnostics;
using Xabe.FFmpeg;
using VideoConversion.Models;
using Microsoft.AspNetCore.SignalR;
using VideoConversion.Hubs;

namespace VideoConversion.Services
{
    /// <summary>
    /// 视频转换服务
    /// </summary>
    public class VideoConversionService
    {
        private readonly DatabaseService _databaseService;
        private readonly IHubContext<ConversionHub> _hubContext;
        private readonly ILogger<VideoConversionService> _logger;
        private readonly LoggingService _loggingService;
        private readonly IConfiguration _configuration;
        private readonly SemaphoreSlim _conversionSemaphore;

        public VideoConversionService(
            DatabaseService databaseService,
            IHubContext<ConversionHub> hubContext,
            ILogger<VideoConversionService> logger,
            LoggingService loggingService,
            IConfiguration configuration)
        {
            _databaseService = databaseService;
            _hubContext = hubContext;
            _logger = logger;
            _loggingService = loggingService;
            _configuration = configuration;

            // 限制并发转换数量
            var maxConcurrent = _configuration.GetValue<int>("VideoConversion:MaxConcurrentConversions", 2);
            _conversionSemaphore = new SemaphoreSlim(maxConcurrent, maxConcurrent);

            // 设置FFmpeg路径（如果需要）
            InitializeFFmpeg();
        }

        /// <summary>
        /// 初始化FFmpeg
        /// </summary>
        private void InitializeFFmpeg()
        {
            try
            {
                // FFmpeg.SetExecutablesPath("path/to/ffmpeg"); // 如果需要指定FFmpeg路径
                _logger.LogInformation("FFmpeg初始化完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FFmpeg初始化失败");
            }
        }

        /// <summary>
        /// 开始转换任务
        /// </summary>
        public async Task StartConversionAsync(string taskId)
        {
            await _conversionSemaphore.WaitAsync();
            
            try
            {
                var task = await _databaseService.GetTaskAsync(taskId);
                if (task == null)
                {
                    _logger.LogError("任务不存在: {TaskId}", taskId);
                    return;
                }

                if (task.Status != ConversionStatus.Pending)
                {
                    _logger.LogWarning("任务状态不正确: {TaskId}, Status: {Status}", taskId, task.Status);
                    return;
                }

                await ConvertVideoAsync(task);
            }
            finally
            {
                _conversionSemaphore.Release();
            }
        }

        /// <summary>
        /// 转换视频
        /// </summary>
        private async Task ConvertVideoAsync(ConversionTask task)
        {
            var startTime = DateTime.Now;
            try
            {
                _logger.LogInformation("开始转换视频: {TaskId}", task.Id);
                _loggingService.LogConversionStarted(task.Id, task.TaskName, task.OriginalFileName, task.OutputFormat);

                // 更新状态为转换中
                await _databaseService.UpdateTaskStatusAsync(task.Id, ConversionStatus.Converting);
                await NotifyProgressAsync(task.Id, 0, "开始转换...");

                // 检查输入文件是否存在
                if (!File.Exists(task.OriginalFilePath))
                {
                    throw new FileNotFoundException($"输入文件不存在: {task.OriginalFilePath}");
                }

                // 确保输出目录存在
                var outputDir = Path.GetDirectoryName(task.OutputFilePath);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                // 获取媒体信息
                var mediaInfo = await FFmpeg.GetMediaInfo(task.OriginalFilePath);
                task.Duration = mediaInfo.Duration.TotalSeconds;
                await _databaseService.UpdateTaskAsync(task);

                // 创建转换
                var conversion = CreateConversion(task, mediaInfo);
                
                // 设置进度回调
                conversion.OnProgress += async (sender, args) =>
                {
                    var progress = (int)((args.Duration.TotalSeconds / task.Duration.Value) * 100);
                    progress = Math.Min(progress, 100);
                    
                    var speed = args.Duration.TotalSeconds > 0 ? args.Duration.TotalSeconds / args.TotalLength.TotalSeconds : 0;
                    var remainingSeconds = speed > 0 ? (int)((task.Duration.Value - args.Duration.TotalSeconds) / speed) : 0;

                    await _databaseService.UpdateTaskProgressAsync(
                        task.Id, 
                        progress, 
                        args.Duration.TotalSeconds, 
                        speed, 
                        remainingSeconds);

                    await NotifyProgressAsync(task.Id, progress, $"转换中... {progress}%", speed, remainingSeconds);
                };

                // 执行转换
                var result = await conversion.Start();
                
                // 检查输出文件
                if (File.Exists(task.OutputFilePath))
                {
                    var outputFileInfo = new FileInfo(task.OutputFilePath);
                    task.OutputFileSize = outputFileInfo.Length;
                    task.Status = ConversionStatus.Completed;
                    task.Progress = 100;

                    await _databaseService.UpdateTaskAsync(task);
                    await _databaseService.UpdateTaskStatusAsync(task.Id, ConversionStatus.Completed);
                    await NotifyProgressAsync(task.Id, 100, "转换完成！");

                    var duration = DateTime.Now - startTime;
                    _logger.LogInformation("视频转换完成: {TaskId}", task.Id);
                    _loggingService.LogConversionCompleted(task.Id, task.TaskName, duration, task.OutputFileSize);
                }
                else
                {
                    throw new Exception("转换完成但输出文件不存在");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "视频转换失败: {TaskId}", task.Id);
                _loggingService.LogConversionFailed(task.Id, task.TaskName, ex);

                await _databaseService.UpdateTaskStatusAsync(task.Id, ConversionStatus.Failed, ex.Message);
                await NotifyProgressAsync(task.Id, task.Progress, $"转换失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建转换配置
        /// </summary>
        private IConversion CreateConversion(ConversionTask task, IMediaInfo mediaInfo)
        {
            var conversion = FFmpeg.Conversions.New();

            // 添加输入流
            foreach (var stream in mediaInfo.Streams)
            {
                conversion.AddStream(stream);
            }

            // 设置输出路径
            conversion.SetOutput(task.OutputFilePath);

            // 构建详细的FFmpeg参数
            var parameters = BuildFFmpegParameters(task);

            // 添加参数到转换
            if (parameters.Any())
            {
                conversion.AddParameter(string.Join(" ", parameters));
            }

            return conversion;
        }

        /// <summary>
        /// 构建FFmpeg参数
        /// </summary>
        private List<string> BuildFFmpegParameters(ConversionTask task)
        {
            var parameters = new List<string>();
            var isAudioOnly = IsAudioOnlyFormat(task.OutputFormat);

            // 时间范围设置
            if (task.StartTime.HasValue && task.StartTime.Value > 0)
            {
                parameters.Add($"-ss {task.StartTime.Value}");
            }

            if (task.DurationLimit.HasValue && task.DurationLimit.Value > 0)
            {
                parameters.Add($"-t {task.DurationLimit.Value}");
            }

            // 视频设置（非纯音频格式）
            if (!isAudioOnly)
            {
                BuildVideoParameters(parameters, task);
            }
            else
            {
                parameters.Add("-vn"); // 禁用视频
            }

            // 音频设置
            BuildAudioParameters(parameters, task);

            // 高级选项
            BuildAdvancedParameters(parameters, task);

            // 自定义参数（最后添加，可以覆盖前面的设置）
            if (!string.IsNullOrEmpty(task.CustomParams))
            {
                parameters.Add(task.CustomParams);
            }

            return parameters;
        }

        /// <summary>
        /// 构建视频参数
        /// </summary>
        private void BuildVideoParameters(List<string> parameters, ConversionTask task)
        {
            // 视频编解码器
            if (!string.IsNullOrEmpty(task.VideoCodec))
            {
                parameters.Add($"-c:v {task.VideoCodec}");
            }

            // 编码预设
            if (!string.IsNullOrEmpty(task.EncodingPreset))
            {
                parameters.Add($"-preset {task.EncodingPreset}");
            }

            // H.264配置文件
            if (!string.IsNullOrEmpty(task.Profile))
            {
                parameters.Add($"-profile:v {task.Profile}");
            }

            // 质量控制
            if (task.QualityMode == "crf" && !string.IsNullOrEmpty(task.VideoQuality))
            {
                if (int.TryParse(task.VideoQuality, out var crf))
                {
                    parameters.Add($"-crf {crf}");
                }
            }
            else if (task.QualityMode == "bitrate" && task.VideoQuality?.EndsWith("k") == true)
            {
                parameters.Add($"-b:v {task.VideoQuality}");
            }

            // 分辨率
            if (!string.IsNullOrEmpty(task.Resolution))
            {
                var parts = task.Resolution.Split('x');
                if (parts.Length == 2 && int.TryParse(parts[0], out var width) && int.TryParse(parts[1], out var height))
                {
                    parameters.Add($"-s {width}x{height}");
                }
            }

            // 帧率
            if (!string.IsNullOrEmpty(task.FrameRate) && double.TryParse(task.FrameRate, out var fps))
            {
                parameters.Add($"-r {fps}");
            }

            // 像素格式
            if (!string.IsNullOrEmpty(task.PixelFormat))
            {
                parameters.Add($"-pix_fmt {task.PixelFormat}");
            }

            // 色彩空间
            if (!string.IsNullOrEmpty(task.ColorSpace))
            {
                parameters.Add($"-colorspace {task.ColorSpace}");
            }

            // 去隔行扫描
            if (task.Deinterlace)
            {
                parameters.Add("-vf yadif");
            }

            // 降噪
            if (!string.IsNullOrEmpty(task.Denoise))
            {
                var existingVf = parameters.FirstOrDefault(p => p.StartsWith("-vf"));
                if (existingVf != null)
                {
                    var index = parameters.IndexOf(existingVf);
                    parameters[index] = $"{existingVf},{task.Denoise}";
                }
                else
                {
                    parameters.Add($"-vf {task.Denoise}");
                }
            }

            // 两遍编码
            if (task.TwoPass)
            {
                parameters.Add("-pass 1");
            }
        }

        /// <summary>
        /// 构建音频参数
        /// </summary>
        private void BuildAudioParameters(List<string> parameters, ConversionTask task)
        {
            // 音频编解码器
            if (!string.IsNullOrEmpty(task.AudioCodec))
            {
                parameters.Add($"-c:a {task.AudioCodec}");
            }

            // 音频质量
            if (task.AudioQualityMode == "bitrate" && !string.IsNullOrEmpty(task.AudioQuality))
            {
                if (task.AudioQuality.EndsWith("k"))
                {
                    parameters.Add($"-b:a {task.AudioQuality}");
                }
            }
            else if (task.AudioQualityMode == "quality" && !string.IsNullOrEmpty(task.AudioQuality))
            {
                if (int.TryParse(task.AudioQuality, out var quality))
                {
                    parameters.Add($"-q:a {quality}");
                }
            }

            // 声道数
            if (!string.IsNullOrEmpty(task.AudioChannels) && int.TryParse(task.AudioChannels, out var channels))
            {
                parameters.Add($"-ac {channels}");
            }

            // 采样率
            if (!string.IsNullOrEmpty(task.SampleRate) && int.TryParse(task.SampleRate, out var sampleRate))
            {
                parameters.Add($"-ar {sampleRate}");
            }

            // 音量调整
            if (!string.IsNullOrEmpty(task.AudioVolume) && int.TryParse(task.AudioVolume, out var volume) && volume != 100)
            {
                var volumeFilter = $"volume={volume / 100.0:F2}";
                var existingAf = parameters.FirstOrDefault(p => p.StartsWith("-af"));
                if (existingAf != null)
                {
                    var index = parameters.IndexOf(existingAf);
                    parameters[index] = $"{existingAf},{volumeFilter}";
                }
                else
                {
                    parameters.Add($"-af {volumeFilter}");
                }
            }
        }

        /// <summary>
        /// 构建高级参数
        /// </summary>
        private void BuildAdvancedParameters(List<string> parameters, ConversionTask task)
        {
            // 快速启动（优化网络播放）
            if (task.FastStart && (task.OutputFormat == "mp4" || task.OutputFormat == "mov"))
            {
                parameters.Add("-movflags +faststart");
            }

            // 保持时间戳
            if (task.CopyTimestamps)
            {
                parameters.Add("-copyts");
            }

            // 其他通用设置
            parameters.Add("-avoid_negative_ts make_zero");
        }

        /// <summary>
        /// 判断是否为纯音频格式
        /// </summary>
        private bool IsAudioOnlyFormat(string format)
        {
            var audioFormats = new[] { "mp3", "aac", "flac", "ogg", "wav", "m4a" };
            return audioFormats.Contains(format.ToLower());
        }

        /// <summary>
        /// 通知进度更新
        /// </summary>
        private async Task NotifyProgressAsync(string taskId, int progress, string message, double? speed = null, int? remainingSeconds = null)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("ProgressUpdate", new
                {
                    TaskId = taskId,
                    Progress = progress,
                    Message = message,
                    Speed = speed,
                    RemainingSeconds = remainingSeconds,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送进度通知失败: {TaskId}", taskId);
            }
        }

        /// <summary>
        /// 取消转换任务
        /// </summary>
        public async Task<bool> CancelConversionAsync(string taskId)
        {
            try
            {
                var task = await _databaseService.GetTaskAsync(taskId);
                if (task == null || task.Status != ConversionStatus.Converting)
                {
                    return false;
                }

                await _databaseService.UpdateTaskStatusAsync(taskId, ConversionStatus.Cancelled);
                await NotifyProgressAsync(taskId, task.Progress, "转换已取消");
                
                _logger.LogInformation("取消转换任务: {TaskId}", taskId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取消转换任务失败: {TaskId}", taskId);
                return false;
            }
        }

        /// <summary>
        /// 获取媒体信息
        /// </summary>
        public async Task<IMediaInfo?> GetMediaInfoAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return null;
                }

                return await FFmpeg.GetMediaInfo(filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取媒体信息失败: {FilePath}", filePath);
                return null;
            }
        }
    }
}
