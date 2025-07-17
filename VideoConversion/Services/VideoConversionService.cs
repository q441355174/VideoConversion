using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
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
        private readonly LoggingService _loggingService;
        private readonly ILogger<VideoConversionService> _logger;
        private readonly SemaphoreSlim _conversionSemaphore;

        public VideoConversionService(
            DatabaseService databaseService,
            IHubContext<ConversionHub> hubContext,
            LoggingService loggingService,
            ILogger<VideoConversionService> logger)
        {
            _databaseService = databaseService;
            _hubContext = hubContext;
            _loggingService = loggingService;
            _logger = logger;
            _conversionSemaphore = new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount);

            InitializeFFmpeg();
        }

        private string _ffmpegPath = "";
        private string _ffprobePath = "";

        /// <summary>
        /// 初始化FFmpeg
        /// </summary>
        private void InitializeFFmpeg()
        {
            try
            {
                // 获取当前工作目录（项目根目录）
                var currentDirectory = Environment.CurrentDirectory;
                var ffmpegDir = Path.Combine(currentDirectory, "ffmpeg");

                _logger.LogDebug("当前工作目录: {CurrentDirectory}", currentDirectory);
                _logger.LogDebug("检查FFmpeg路径: {FFmpegPath}", ffmpegDir);

                // 如果ffmpeg目录存在，设置FFmpeg路径
                if (Directory.Exists(ffmpegDir))
                {
                    _ffmpegPath = Path.Combine(ffmpegDir, "ffmpeg.exe");
                    _ffprobePath = Path.Combine(ffmpegDir, "ffprobe.exe");

                    _logger.LogDebug("检查FFmpeg文件: {FFmpegExe}", _ffmpegPath);
                    _logger.LogDebug("检查FFprobe文件: {FFprobeExe}", _ffprobePath);

                    if (File.Exists(_ffmpegPath) && File.Exists(_ffprobePath))
                    {
                        _logger.LogInformation("✅ FFmpeg配置完成: {FFmpegPath}", ffmpegDir);
                    }
                    else
                    {
                        _logger.LogWarning("❌ FFmpeg二进制文件不存在:");
                        _logger.LogWarning("  - ffmpeg.exe存在: {FFmpegExists}", File.Exists(_ffmpegPath));
                        _logger.LogWarning("  - ffprobe.exe存在: {FFprobeExists}", File.Exists(_ffprobePath));

                        // 尝试使用系统PATH中的FFmpeg
                        _ffmpegPath = "ffmpeg";
                        _ffprobePath = "ffprobe";
                    }
                }
                else
                {
                    _logger.LogWarning("❌ FFmpeg目录不存在: {FFmpegPath}", ffmpegDir);
                    _logger.LogWarning("尝试使用系统PATH中的FFmpeg");

                    // 尝试使用系统PATH中的FFmpeg
                    _ffmpegPath = "ffmpeg";
                    _ffprobePath = "ffprobe";
                }

                _logger.LogDebug("FFmpeg初始化完成");
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
                    _logger.LogWarning("任务不存在: {TaskId}", taskId);
                    return;
                }

                // 现在任务状态应该是Converting（由TryStartTaskAsync设置）
                if (task.Status != ConversionStatus.Converting)
                {
                    _logger.LogWarning("任务状态不正确: {TaskId}, Status: {Status}，期望状态: Converting", taskId, task.Status);
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
            var taskStartTime = DateTime.Now;
            try
            {
                _logger.LogInformation("开始转换视频: {TaskId}", task.Id);
                _loggingService.LogConversionStarted(task.Id, task.TaskName, task.OriginalFileName, task.OutputFormat);

                // 状态已在TryStartTaskAsync中更新为Converting，这里只需要通知进度
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
                _logger.LogInformation("开始分析媒体文件: {FilePath}", task.OriginalFilePath);
                await NotifyProgressAsync(task.Id, 5, "正在分析视频文件...");

                var videoDuration = await GetVideoDurationAsync(task.OriginalFilePath);
                task.Duration = videoDuration;

                _logger.LogInformation("媒体文件分析完成 - 时长: {Duration}秒", task.Duration);

                await _databaseService.UpdateTaskAsync(task);
                await NotifyProgressAsync(task.Id, 10, "文件分析完成，开始转换...");

                _logger.LogInformation("开始执行FFmpeg转换...");
                _logger.LogInformation("输入文件: {InputFile}", task.OriginalFilePath);
                _logger.LogInformation("输出文件: {OutputFile}", task.OutputFilePath);

                await NotifyProgressAsync(task.Id, 15, "开始视频转换...");

                // 执行转换 - 使用Process直接调用FFmpeg
                var conversionStartTime = DateTime.Now;
                _logger.LogInformation("🎬 开始FFmpeg转换，设置进度回调...");

                var success = await RunFFmpegWithProgressAsync(task, conversionStartTime);

                _logger.LogInformation("FFmpeg转换完成，结果: {Success}", success);

                if (success)
                {
                    // 获取输出文件大小
                    var outputFileInfo = new FileInfo(task.OutputFilePath);
                    task.OutputFileSize = outputFileInfo.Length;

                    // 更新任务状态
                    await _databaseService.UpdateTaskStatusAsync(task.Id, ConversionStatus.Completed);
                    await _databaseService.UpdateTaskAsync(task);
                    await NotifyProgressAsync(task.Id, 100, "转换完成");

                    var duration = DateTime.Now - taskStartTime;
                    _logger.LogInformation("视频转换完成: {TaskId}, 耗时: {Duration}", task.Id, duration);
                    _loggingService.LogConversionCompleted(task.Id, task.TaskName, duration, task.OutputFileSize);
                }
                else
                {
                    throw new Exception("FFmpeg转换失败");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "视频转换失败: {TaskId}", task.Id);
                _loggingService.LogConversionFailed(task.Id, task.TaskName, ex);

                string errorMessage = ex.Message;

                // 检查是否是FFmpeg相关错误
                if (ex.Message.Contains("FFmpeg") || ex.Message.Contains("ffmpeg"))
                {
                    errorMessage = "FFmpeg未找到或配置错误。请按照项目中的'FFmpeg配置指南.txt'配置FFmpeg后重试。";
                    _logger.LogError("FFmpeg配置问题，请检查ffmpeg目录或系统PATH");
                }

                await _databaseService.UpdateTaskStatusAsync(task.Id, ConversionStatus.Failed, errorMessage);
                await NotifyProgressAsync(task.Id, task.Progress, $"转换失败: {errorMessage}");
            }
        }

        /// <summary>
        /// 判断是否为纯音频格式
        /// </summary>
        private bool IsAudioOnlyFormat(string format)
        {
            var audioFormats = new[] { "mp3", "aac", "flac", "wav", "ogg", "m4a" };
            return audioFormats.Contains(format.ToLower());
        }

        /// <summary>
        /// 取消转换任务
        /// </summary>
        public async Task CancelConversionAsync(string taskId)
        {
            try
            {
                await _databaseService.UpdateTaskStatusAsync(taskId, ConversionStatus.Cancelled, "用户取消");
                await NotifyProgressAsync(taskId, 0, "任务已取消");
                _logger.LogInformation("任务已取消: {TaskId}", taskId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取消任务失败: {TaskId}", taskId);
            }
        }

        /// <summary>
        /// 获取视频时长
        /// </summary>
        private async Task<double> GetVideoDurationAsync(string filePath)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = _ffprobePath,
                    Arguments = $"-v quiet -show_entries format=duration -of csv=p=0 \"{filePath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0 && double.TryParse(output.Trim(), out var duration))
                {
                    return duration;
                }

                _logger.LogWarning("无法获取视频时长: {FilePath}", filePath);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取视频时长失败: {FilePath}", filePath);
                return 0;
            }
        }

        /// <summary>
        /// 运行FFmpeg并解析进度
        /// </summary>
        private async Task<bool> RunFFmpegWithProgressAsync(ConversionTask task, DateTime startTime)
        {
            try
            {
                var arguments = BuildFFmpegArguments(task);
                _logger.LogInformation("FFmpeg命令: {FFmpegPath} {Arguments}", _ffmpegPath, arguments);

                var startInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };

                var tcs = new TaskCompletionSource<bool>();
                var progressRegex = new Regex(@"time=(\d{2}):(\d{2}):(\d{2})\.(\d{2})");

                process.ErrorDataReceived += async (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        _logger.LogDebug("FFmpeg输出: {Output}", e.Data);
                        await ParseFFmpegProgress(e.Data, task, startTime, progressRegex);
                    }
                };

                process.Exited += (sender, e) =>
                {
                    tcs.SetResult(process.ExitCode == 0);
                };

                process.EnableRaisingEvents = true;
                process.Start();
                process.BeginErrorReadLine();

                return await tcs.Task;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FFmpeg执行失败: {TaskId}", task.Id);
                return false;
            }
        }

        /// <summary>
        /// 解析FFmpeg进度输出
        /// </summary>
        private async Task ParseFFmpegProgress(string output, ConversionTask task, DateTime startTime, Regex progressRegex)
        {
            try
            {
                // 尝试解析time=格式的进度
                var timeMatch = progressRegex.Match(output);
                if (timeMatch.Success && task.Duration.HasValue && task.Duration.Value > 0)
                {
                    var hours = int.Parse(timeMatch.Groups[1].Value);
                    var minutes = int.Parse(timeMatch.Groups[2].Value);
                    var seconds = int.Parse(timeMatch.Groups[3].Value);
                    var centiseconds = int.Parse(timeMatch.Groups[4].Value);

                    var currentSeconds = hours * 3600 + minutes * 60 + seconds + centiseconds / 100.0;
                    await UpdateProgress(task, startTime, currentSeconds);
                    return;
                }

                // 尝试解析out_time_ms=格式的进度（微秒）
                var outTimeMatch = Regex.Match(output, @"out_time_ms=(\d+)");
                if (outTimeMatch.Success && task.Duration.HasValue && task.Duration.Value > 0)
                {
                    var microseconds = long.Parse(outTimeMatch.Groups[1].Value);
                    var currentSeconds = microseconds / 1000000.0;
                    await UpdateProgress(task, startTime, currentSeconds);
                    return;
                }

                // 尝试解析out_time=格式的进度
                var outTimeFormatMatch = Regex.Match(output, @"out_time=(\d{2}):(\d{2}):(\d{2})\.(\d{6})");
                if (outTimeFormatMatch.Success && task.Duration.HasValue && task.Duration.Value > 0)
                {
                    var hours = int.Parse(outTimeFormatMatch.Groups[1].Value);
                    var minutes = int.Parse(outTimeFormatMatch.Groups[2].Value);
                    var seconds = int.Parse(outTimeFormatMatch.Groups[3].Value);
                    var microseconds = int.Parse(outTimeFormatMatch.Groups[4].Value);

                    var currentSeconds = hours * 3600 + minutes * 60 + seconds + microseconds / 1000000.0;
                    await UpdateProgress(task, startTime, currentSeconds);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "解析FFmpeg进度失败: {TaskId} - {Output}", task.Id, output);
            }
        }

        /// <summary>
        /// 更新任务进度
        /// </summary>
        private async Task UpdateProgress(ConversionTask task, DateTime startTime, double currentSeconds)
        {
            try
            {
                if (!task.Duration.HasValue || task.Duration.Value <= 0) return;

                var progressPercent = Math.Min((int)((currentSeconds / task.Duration.Value) * 100), 99);
                var elapsed = DateTime.Now - startTime;
                var speed = elapsed.TotalSeconds > 0 ? currentSeconds / elapsed.TotalSeconds : 0;
                var remainingSeconds = speed > 0 ? (int)((task.Duration.Value - currentSeconds) / speed) : 0;

                _logger.LogDebug("📊 FFmpeg进度: {Progress}% ({Current:F1}/{Total:F1}秒)",
                    progressPercent, currentSeconds, task.Duration.Value);

                // 异步通知进度
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await NotifyProgressAsync(task.Id, progressPercent,
                            $"转换中... {progressPercent}%", speed, remainingSeconds);

                        await _databaseService.UpdateTaskProgressAsync(task.Id, progressPercent,
                            currentSeconds, speed, remainingSeconds);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "更新进度失败: {TaskId}", task.Id);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新进度失败: {TaskId}", task.Id);
            }
        }

        /// <summary>
        /// 构建FFmpeg命令参数
        /// </summary>
        private string BuildFFmpegArguments(ConversionTask task)
        {
            var args = new List<string>
            {
                "-y", // 覆盖输出文件
                "-progress", "pipe:2", // 输出进度到stderr
                $"-i \"{task.OriginalFilePath}\"" // 输入文件
            };

            // 根据任务配置添加编码参数
            if (!string.IsNullOrEmpty(task.VideoCodec))
            {
                args.Add($"-c:v {task.VideoCodec}");
            }

            if (!string.IsNullOrEmpty(task.AudioCodec))
            {
                args.Add($"-c:a {task.AudioCodec}");
            }

            // 视频质量/比特率
            if (!string.IsNullOrEmpty(task.VideoQuality))
            {
                if (task.QualityMode == "CRF" && int.TryParse(task.VideoQuality, out var crf))
                {
                    args.Add($"-crf {crf}");
                }
                else if (task.QualityMode == "Bitrate")
                {
                    if (int.TryParse(task.VideoQuality.Replace("k", ""), out var bitrate))
                    {
                        args.Add($"-b:v {bitrate}k");
                    }
                }
            }

            // 音频质量/比特率
            if (!string.IsNullOrEmpty(task.AudioQuality))
            {
                if (int.TryParse(task.AudioQuality.Replace("k", ""), out var bitrate))
                {
                    args.Add($"-b:a {bitrate}k");
                }
            }

            // 分辨率
            if (!string.IsNullOrEmpty(task.Resolution) && task.Resolution != "原始")
            {
                var parts = task.Resolution.Split('x');
                if (parts.Length == 2 && int.TryParse(parts[0], out var width) && int.TryParse(parts[1], out var height))
                {
                    args.Add($"-s {width}x{height}");
                }
            }

            // 帧率
            if (!string.IsNullOrEmpty(task.FrameRate) && task.FrameRate != "原始")
            {
                if (double.TryParse(task.FrameRate, out var fps))
                {
                    args.Add($"-r {fps}");
                }
            }

            // 音频声道数
            if (!string.IsNullOrEmpty(task.AudioChannels) && int.TryParse(task.AudioChannels, out var channels))
            {
                args.Add($"-ac {channels}");
            }

            // 采样率
            if (!string.IsNullOrEmpty(task.SampleRate) && int.TryParse(task.SampleRate, out var sampleRate))
            {
                args.Add($"-ar {sampleRate}");
            }

            // 自定义参数
            if (!string.IsNullOrEmpty(task.CustomParams))
            {
                args.Add(task.CustomParams);
            }

            args.Add($"\"{task.OutputFilePath}\""); // 输出文件

            return string.Join(" ", args);
        }

        /// <summary>
        /// 通知进度更新
        /// </summary>
        private async Task NotifyProgressAsync(string taskId, int progress, string message, double speed = 0, int remainingSeconds = 0)
        {
            try
            {
                _logger.LogDebug("📡 发送进度更新: {TaskId} - {Progress}% - {Message}", taskId, progress, message);

                await _hubContext.Clients.Group($"task_{taskId}").SendAsync("ProgressUpdate", new
                {
                    TaskId = taskId,
                    Progress = progress,
                    Message = message,
                    Speed = speed,
                    RemainingSeconds = remainingSeconds
                });

                _logger.LogDebug("✅ 进度更新发送成功: {TaskId} - {Progress}%", taskId, progress);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 发送进度更新失败: {TaskId} - {Progress}%", taskId, progress);
            }
        }
    }
}
