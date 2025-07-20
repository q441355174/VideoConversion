using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
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
        private readonly FFmpegConfigurationService _ffmpegConfig;
        private readonly NotificationService _notificationService;
        private readonly SemaphoreSlim _conversionSemaphore;

        // 进程跟踪：任务ID -> FFmpeg进程
        private static readonly ConcurrentDictionary<string, Process> _runningProcesses = new();

        // 取消令牌：任务ID -> CancellationTokenSource
        private static readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokens = new();

        public VideoConversionService(
            DatabaseService databaseService,
            IHubContext<ConversionHub> hubContext,
            LoggingService loggingService,
            ILogger<VideoConversionService> logger,
            FFmpegConfigurationService ffmpegConfig,
            NotificationService notificationService)
        {
            _databaseService = databaseService;
            _hubContext = hubContext;
            _loggingService = loggingService;
            _logger = logger;
            _ffmpegConfig = ffmpegConfig;
            _notificationService = notificationService;
            _conversionSemaphore = new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount);

            _logger.LogInformation("VideoConversionService 初始化完成，FFmpeg配置状态: {IsInitialized}",
                _ffmpegConfig.IsInitialized);
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

                    // 更新任务对象的状态和完成时间
                    task.Status = ConversionStatus.Completed;
                    task.CompletedAt = DateTime.Now;
                    task.Progress = 100;

                    // 更新数据库中的任务信息
                    await _databaseService.UpdateTaskAsync(task);
                    await NotifyProgressAsync(task.Id, 100, "转换完成");

                    // 通知任务状态变化
                    await NotifyTaskStatusChangeAsync(task.Id, ConversionStatus.Completed, 100, "转换完成");

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

                // 通知任务状态变化
                await NotifyTaskStatusChangeAsync(task.Id, ConversionStatus.Failed, task.Progress, $"转换失败: {errorMessage}");
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
        /// 取消转换任务 - 终止FFmpeg进程
        /// </summary>
        public async Task CancelConversionAsync(string taskId)
        {
            try
            {
                _logger.LogInformation("开始取消转换任务: {TaskId}", taskId);

                // 1. 首先检查是否有正在运行的进程
                if (_runningProcesses.TryGetValue(taskId, out var process))
                {
                    _logger.LogInformation("找到正在运行的FFmpeg进程: {TaskId} -> PID: {ProcessId}", taskId, process.Id);

                    // 2. 触发取消令牌
                    if (_cancellationTokens.TryGetValue(taskId, out var cancellationTokenSource))
                    {
                        _logger.LogInformation("📤 触发取消令牌: {TaskId}", taskId);
                        cancellationTokenSource.Cancel();
                    }

                    // 3. 直接终止进程（作为备用方案）
                    try
                    {
                        if (!process.HasExited)
                        {
                            _logger.LogInformation("💀 直接终止FFmpeg进程: PID {ProcessId}", process.Id);

                            if (OperatingSystem.IsWindows())
                            {
                                // Windows: 使用taskkill强制终止进程树
                                var killProcess = new Process
                                {
                                    StartInfo = new ProcessStartInfo
                                    {
                                        FileName = "taskkill",
                                        Arguments = $"/PID {process.Id} /T /F",
                                        UseShellExecute = false,
                                        CreateNoWindow = true,
                                        RedirectStandardOutput = true,
                                        RedirectStandardError = true
                                    }
                                };

                                killProcess.Start();
                                var output = await killProcess.StandardOutput.ReadToEndAsync();
                                var error = await killProcess.StandardError.ReadToEndAsync();
                                await killProcess.WaitForExitAsync();

                                _logger.LogInformation("taskkill输出: {Output}", output);
                                if (!string.IsNullOrEmpty(error))
                                {
                                    _logger.LogWarning("taskkill错误: {Error}", error);
                                }
                            }
                            else
                            {
                                // Linux/Mac: 使用SIGKILL信号
                                process.Kill(entireProcessTree: true);
                            }

                            // 等待进程退出确认
                            var timeout = TimeSpan.FromSeconds(3);
                            var exitTask = process.WaitForExitAsync();
                            var timeoutTask = Task.Delay(timeout);

                            var completedTask = await Task.WhenAny(exitTask, timeoutTask);

                            if (completedTask == exitTask)
                            {
                                _logger.LogInformation("FFmpeg进程已成功终止: {TaskId}", taskId);
                            }
                            else
                            {
                                _logger.LogWarning("FFmpeg进程终止超时，但取消请求已发送: {TaskId}", taskId);
                            }
                        }
                        else
                        {
                            _logger.LogInformation("ℹ️ FFmpeg进程已经退出: {TaskId}", taskId);
                        }
                    }
                    catch (Exception processEx)
                    {
                        _logger.LogError(processEx, "❌ 终止FFmpeg进程时发生错误: {TaskId}", taskId);
                    }
                }
                else
                {
                    _logger.LogInformation("ℹ️ 未找到正在运行的FFmpeg进程，可能任务尚未开始或已完成: {TaskId}", taskId);
                }

                // 4. 更新数据库状态
                await _databaseService.UpdateTaskStatusAsync(taskId, ConversionStatus.Cancelled, "用户取消");

                // 5. 发送通知
                await NotifyProgressAsync(taskId, 0, "任务已取消");
                await NotifyTaskStatusChangeAsync(taskId, ConversionStatus.Cancelled, 0, "任务已取消");

                // 6. 清理跟踪信息
                _runningProcesses.TryRemove(taskId, out _);
                _cancellationTokens.TryRemove(taskId, out _);

                _logger.LogInformation("任务取消完成: {TaskId}", taskId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取消任务失败: {TaskId}", taskId);
                throw; // 重新抛出异常，让调用者知道取消失败
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
                    FileName = _ffmpegConfig.FFprobePath,
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
        /// 运行FFmpeg并解析进度 - 支持取消
        /// </summary>
        private async Task<bool> RunFFmpegWithProgressAsync(ConversionTask task, DateTime startTime)
        {
            // 创建取消令牌
            var cancellationTokenSource = new CancellationTokenSource();
            _cancellationTokens[task.Id] = cancellationTokenSource;

            try
            {
                var arguments = BuildFFmpegArguments(task);
                _logger.LogInformation("🎬 启动FFmpeg进程: {TaskId}", task.Id);
                _logger.LogInformation("🎯 FFmpeg命令: {FFmpegPath} {Arguments}", _ffmpegConfig.FFmpegPath, arguments);

                var startInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegConfig.FFmpegPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                var process = new Process { StartInfo = startInfo };

                // 注册进程到跟踪字典
                _runningProcesses[task.Id] = process;
                _logger.LogInformation("📝 进程已注册到跟踪列表: {TaskId} -> PID: {ProcessId}", task.Id, "待启动");

                var tcs = new TaskCompletionSource<bool>();
                var progressRegex = new Regex(@"time=(\d{2}):(\d{2}):(\d{2})\.(\d{2})");

                // 设置进程事件处理
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
                    var exitCode = process.ExitCode;
                    _logger.LogInformation("🏁 FFmpeg进程退出: {TaskId} -> 退出码: {ExitCode}", task.Id, exitCode);

                    // 从跟踪字典中移除
                    _runningProcesses.TryRemove(task.Id, out _);
                    _cancellationTokens.TryRemove(task.Id, out _);

                    // 检查是否被取消
                    if (cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        _logger.LogInformation("❌ 任务被用户取消: {TaskId}", task.Id);
                        tcs.SetResult(false);
                    }
                    else
                    {
                        tcs.SetResult(exitCode == 0);
                    }
                };

                process.EnableRaisingEvents = true;

                // 启动进程
                process.Start();
                _logger.LogInformation("🚀 FFmpeg进程已启动: {TaskId} -> PID: {ProcessId}", task.Id, process.Id);

                process.BeginErrorReadLine();

                // 等待进程完成或被取消
                var completionTask = tcs.Task;
                var cancellationTask = Task.Delay(-1, cancellationTokenSource.Token);

                var completedTask = await Task.WhenAny(completionTask, cancellationTask);

                if (completedTask == cancellationTask)
                {
                    // 任务被取消
                    _logger.LogWarning("检测到取消请求，正在终止FFmpeg进程: {TaskId}", task.Id);

                    try
                    {
                        if (!process.HasExited)
                        {
                            // 优雅终止：发送Ctrl+C信号
                            _logger.LogInformation("📤 发送终止信号到FFmpeg进程: PID {ProcessId}", process.Id);

                            // Windows下使用taskkill命令优雅终止
                            if (OperatingSystem.IsWindows())
                            {
                                var killProcess = new Process
                                {
                                    StartInfo = new ProcessStartInfo
                                    {
                                        FileName = "taskkill",
                                        Arguments = $"/PID {process.Id} /T /F",
                                        UseShellExecute = false,
                                        CreateNoWindow = true
                                    }
                                };
                                killProcess.Start();
                                await killProcess.WaitForExitAsync();
                            }
                            else
                            {
                                // Linux/Mac下使用SIGTERM信号
                                process.Kill(entireProcessTree: true);
                            }

                            // 等待进程退出，最多等待5秒
                            var exitTask = process.WaitForExitAsync();
                            var timeoutTask = Task.Delay(5000);
                            var result = await Task.WhenAny(exitTask, timeoutTask);

                            if (result == timeoutTask)
                            {
                                _logger.LogWarning("FFmpeg进程未在5秒内退出，强制终止: PID {ProcessId}", process.Id);
                                process.Kill(entireProcessTree: true);
                            }

                            _logger.LogInformation("FFmpeg进程已成功终止: {TaskId}", task.Id);
                        }
                    }
                    catch (Exception killEx)
                    {
                        _logger.LogError(killEx, "终止FFmpeg进程失败: {TaskId}", task.Id);
                    }
                    finally
                    {
                        // 确保清理资源
                        try
                        {
                            process.Dispose();
                        }
                        catch { }

                        _runningProcesses.TryRemove(task.Id, out _);
                        _cancellationTokens.TryRemove(task.Id, out _);
                    }

                    return false; // 取消的任务返回失败
                }
                else
                {
                    // 正常完成
                    var result = await completionTask;
                    process.Dispose();
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ FFmpeg执行失败: {TaskId}", task.Id);

                // 清理资源
                _runningProcesses.TryRemove(task.Id, out _);
                _cancellationTokens.TryRemove(task.Id, out _);

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
                "-progress", "pipe:2" // 输出进度到stderr
            };

            // 添加硬件加速参数（必须在输入文件之前）
            AddHardwareAccelerationArgs(args, task);

            // 添加输入文件（硬件加速参数之后）
            args.Add($"-i \"{task.OriginalFilePath}\"");

            // 根据任务配置添加编码参数
            if (!string.IsNullOrEmpty(task.VideoCodec))
            {
                args.Add($"-c:v {task.VideoCodec}");

                // 为GPU编码器添加特定参数
                AddGpuEncoderSpecificArgs(args, task);
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

            // 构建视频滤镜（现代化方式，替代-s和-r）
            var videoFilters = new List<string>();

            // 分辨率滤镜
            if (!string.IsNullOrEmpty(task.Resolution) && task.Resolution != "原始")
            {
                var parts = task.Resolution.Split('x');
                if (parts.Length == 2 && int.TryParse(parts[0], out var width) && int.TryParse(parts[1], out var height))
                {
                    videoFilters.Add($"scale={width}:{height}");
                }
            }

            // 帧率滤镜
            if (!string.IsNullOrEmpty(task.FrameRate) && task.FrameRate != "原始")
            {
                if (double.TryParse(task.FrameRate, out var fps))
                {
                    videoFilters.Add($"fps={fps}");
                }
            }

            // 应用视频滤镜
            if (videoFilters.Count > 0)
            {
                args.Add($"-vf \"{string.Join(",", videoFilters)}\"");
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
        /// 通知进度更新 - 使用统一的 NotificationService
        /// </summary>
        private async Task NotifyProgressAsync(string taskId, int progress, string message, double speed = 0, int remainingSeconds = 0)
        {
            await _notificationService.NotifyProgressAsync(taskId, progress, message, speed, remainingSeconds);
        }

        /// <summary>
        /// 通知任务状态变化（全局通知）- 使用统一的 NotificationService
        /// </summary>
        private async Task NotifyTaskStatusChangeAsync(string taskId, ConversionStatus status, int progress, string message)
        {
            await _notificationService.NotifyStatusChangeAsync(taskId, status, message);
        }

        /// <summary>
        /// 添加硬件加速参数 - 基于博客最佳实践
        /// </summary>
        private void AddHardwareAccelerationArgs(List<string> args, ConversionTask task)
        {
            if (string.IsNullOrEmpty(task.VideoCodec)) return;

            var lowerCodec = task.VideoCodec.ToLower();

            // NVIDIA NVENC - 最基础配置（兼容GTX 1070 Ti）
            if (lowerCodec.Contains("nvenc"))
            {
                // 只使用最基础的硬件加速参数
                args.Add("-hwaccel cuda");
                // 移除可能不兼容的高级参数
                // args.Add("-hwaccel_output_format cuda");
                // args.Add("-extra_hw_frames 3");

                _logger.LogInformation("🚀 启用NVIDIA CUDA硬件加速 (NVENC) - 基础模式");
            }
            // Intel QSV - 改进的参数配置
            else if (lowerCodec.Contains("qsv"))
            {
                // 输入硬件加速
                args.Add("-hwaccel qsv");
                args.Add("-hwaccel_output_format qsv");

                // QSV特定优化参数
                args.Add("-extra_hw_frames 3");

                _logger.LogInformation("🚀 启用Intel QSV硬件加速");
            }
            // AMD VCE/AMF - 改进的参数配置
            else if (lowerCodec.Contains("amf"))
            {
                // Windows下使用D3D11VA
                if (OperatingSystem.IsWindows())
                {
                    args.Add("-hwaccel d3d11va");
                    args.Add("-hwaccel_output_format d3d11");
                }
                else
                {
                    // Linux下可能使用VAAPI
                    args.Add("-hwaccel vaapi");
                    args.Add("-hwaccel_output_format vaapi");
                }

                _logger.LogInformation("🚀 启用AMD VCE/AMF硬件加速");
            }
            // VAAPI (Linux) - 改进的参数配置
            else if (lowerCodec.Contains("vaapi"))
            {
                args.Add("-hwaccel vaapi");
                args.Add("-hwaccel_output_format vaapi");

                // 尝试不同的VAAPI设备
                var vaapiDevices = new[] { "/dev/dri/renderD128", "/dev/dri/renderD129", "/dev/dri/card0" };
                var deviceFound = false;

                foreach (var device in vaapiDevices)
                {
                    if (File.Exists(device))
                    {
                        args.Add($"-vaapi_device {device}");
                        _logger.LogInformation("🚀 启用VAAPI硬件加速，设备: {Device}", device);
                        deviceFound = true;
                        break;
                    }
                }

                if (!deviceFound)
                {
                    args.Add("-vaapi_device /dev/dri/renderD128"); // 默认设备
                    _logger.LogWarning("未找到VAAPI设备，使用默认设备");
                }
            }
        }

        /// <summary>
        /// 获取正在运行的任务列表
        /// </summary>
        public List<string> GetRunningTaskIds()
        {
            return _runningProcesses.Keys.ToList();
        }

        /// <summary>
        /// 检查任务是否正在运行
        /// </summary>
        public bool IsTaskRunning(string taskId)
        {
            return _runningProcesses.ContainsKey(taskId);
        }

        /// <summary>
        /// 清理所有进程（应用关闭时调用）
        /// </summary>
        public async Task CleanupAllProcessesAsync()
        {
            _logger.LogInformation("🧹 开始清理所有FFmpeg进程...");

            var runningTasks = _runningProcesses.Keys.ToList();
            var cleanupTasks = runningTasks.Select(taskId => CancelConversionAsync(taskId));

            try
            {
                await Task.WhenAll(cleanupTasks);
                _logger.LogInformation("所有FFmpeg进程清理完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理FFmpeg进程时发生错误");
            }
        }

        /// <summary>
        /// 获取进程统计信息
        /// </summary>
        public object GetProcessStatistics()
        {
            var runningCount = _runningProcesses.Count;
            var processes = _runningProcesses.Select(kvp => new
            {
                TaskId = kvp.Key,
                ProcessId = kvp.Value.Id,
                HasExited = kvp.Value.HasExited,
                StartTime = kvp.Value.StartTime
            }).ToList();

            return new
            {
                RunningProcessCount = runningCount,
                Processes = processes,
                Timestamp = DateTime.Now
            };
        }

        /// <summary>
        /// 为GPU编码器添加特定参数 - 基于博客最佳实践
        /// </summary>
        private void AddGpuEncoderSpecificArgs(List<string> args, ConversionTask task)
        {
            if (string.IsNullOrEmpty(task.VideoCodec)) return;

            var lowerCodec = task.VideoCodec.ToLower();

            // NVIDIA NVENC特定参数 - 最简化配置（兼容GTX 1070 Ti）
            if (lowerCodec.Contains("nvenc"))
            {
                // 最基础的编码参数
                args.Add("-preset fast"); // 使用传统预设系统
                // 移除可能不兼容的参数
                // args.Add("-profile:v high");

                // 简化的码率控制
                if (task.QualityMode == "CRF" && !string.IsNullOrEmpty(task.VideoQuality))
                {
                    args.Add($"-cq {task.VideoQuality}"); // 恒定质量模式
                }
                else
                {
                    // 使用比特率模式
                    args.Add("-b:v 5000k"); // 固定比特率
                }

                // 移除所有可能不兼容的高级参数
                // args.Add("-bf 2");

                // HEVC特定参数
                if (lowerCodec.Contains("hevc"))
                {
                    args.Add("-tag:v hvc1"); // 兼容性标签
                }

                _logger.LogInformation("🎯 应用NVENC最简参数（GTX 1070 Ti兼容）");
            }
            // Intel QSV特定参数 - 优化配置
            else if (lowerCodec.Contains("qsv"))
            {
                // 基础编码参数
                args.Add("-preset medium"); // 预设
                args.Add("-profile:v high"); // 配置文件
                args.Add("-level 4.1"); // 级别

                // QSV优化参数
                args.Add("-look_ahead 1"); // 启用前瞻
                args.Add("-look_ahead_depth 40"); // 前瞻深度
                args.Add("-mbbrc 1"); // 宏块级码率控制
                args.Add("-extbrc 1"); // 扩展码率控制
                args.Add("-adaptive_i 1"); // 自适应I帧
                args.Add("-adaptive_b 1"); // 自适应B帧
                args.Add("-b_strategy 1"); // B帧策略

                _logger.LogInformation("🎯 应用QSV优化参数");
            }
            // AMD AMF特定参数 - 优化配置
            else if (lowerCodec.Contains("amf"))
            {
                // 基础编码参数
                args.Add("-quality balanced"); // 质量模式：speed/balanced/quality
                args.Add("-profile:v high"); // 配置文件
                args.Add("-level 4.1"); // 级别

                // AMF优化参数
                args.Add("-preanalysis 1"); // 启用预分析
                args.Add("-vbaq 1"); // 方差自适应量化
                args.Add("-enforce_hrd 1"); // 强制HRD兼容
                args.Add("-filler_data 1"); // 填充数据
                args.Add("-frame_skipping 0"); // 禁用跳帧

                // 码率控制
                if (task.QualityMode == "CRF")
                {
                    args.Add("-rc cqp"); // 恒定量化参数
                }
                else
                {
                    args.Add("-rc vbr_peak"); // 峰值可变码率
                }

                _logger.LogInformation("🎯 应用AMF优化参数");
            }
            // VAAPI特定参数 - 优化配置
            else if (lowerCodec.Contains("vaapi"))
            {
                // 基础编码参数
                args.Add("-profile:v high"); // 配置文件
                args.Add("-level 4.1"); // 级别

                // VAAPI优化参数
                args.Add("-quality 4"); // 质量级别 (1-8)
                args.Add("-compression_level 4"); // 压缩级别

                // 如果是HEVC
                if (lowerCodec.Contains("hevc"))
                {
                    args.Add("-sei hdr"); // HDR SEI信息
                }

                _logger.LogInformation("🎯 应用VAAPI优化参数");
            }
        }
    }
}
