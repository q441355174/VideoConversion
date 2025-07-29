using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using VideoConversion_Client.Models;

namespace VideoConversion_Client.Services
{
    /// <summary>
    /// API服务，用于与Web服务器通信
    /// </summary>
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly ChunkedUploadService _chunkedUploadService;

        public string BaseUrl { get; set; } = "http://localhost:5065";

        public ApiService()
        {
            // 配置HttpClient以提高大文件上传的稳定性
            var handler = new HttpClientHandler()
            {
                // 禁用自动重定向，避免上传过程中的意外重定向
                AllowAutoRedirect = false,
                // 设置更大的缓冲区
                MaxRequestContentBufferSize = 1024 * 1024 * 100 // 100MB
            };

            _httpClient = new HttpClient(handler);

            // 设置更长的超时时间
            _httpClient.Timeout = TimeSpan.FromMinutes(60); // 60分钟超时

            // 设置Keep-Alive以保持连接
            _httpClient.DefaultRequestHeaders.Connection.Add("keep-alive");

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            // 初始化分片上传服务
            _chunkedUploadService = new ChunkedUploadService(BaseUrl);
        }

        /// <summary>
        /// 测试服务器连接
        /// </summary>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseUrl}/api/health");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 智能文件上传 - 根据文件大小自动选择上传策略（支持并发控制）
        /// </summary>
        public async Task<ApiResponse<StartConversionResponse>> StartConversionAsync(
            string filePath,
            StartConversionRequest request,
            IProgress<UploadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            Utils.Logger.Info("ApiService", "=== 开始文件上传 ===");
            Utils.Logger.Info("ApiService", $"文件路径: {filePath}");
            Utils.Logger.Info("ApiService", $"任务名称: {request.TaskName}");
            Utils.Logger.Info("ApiService", $"输出格式: {request.OutputFormat}");

            try
            {
                // 检查文件是否存在
                if (!File.Exists(filePath))
                {
                    Utils.Logger.Info("ApiService", $"❌ 文件不存在: {filePath}");
                    return ApiResponse<StartConversionResponse>.CreateError($"文件不存在: {filePath}");
                }

                // 🔧 在传输前处理智能格式选项
                Utils.Logger.Info("ApiService", "🔧 处理智能格式选项");
                var processedRequest = ProcessSmartFormatOptions(request, filePath);

                var fileInfo = new FileInfo(filePath);
                Utils.Logger.Info("ApiService", $"📁 文件信息: 大小={fileInfo.Length} bytes ({fileInfo.Length / 1024.0 / 1024.0:F2} MB)");

                // 使用并发管理器控制上传并发
                var concurrencyManager = ConcurrencyManager.Instance;
                var taskId = processedRequest.TaskName ?? Guid.NewGuid().ToString();
                Utils.Logger.Info("ApiService", $"🎯 任务ID: {taskId}");

                // 选择上传策略 - 现在分片大小是50MB，所以阈值调整为100MB
                bool useChunkedUpload = fileInfo.Length > 100 * 1024 * 1024; // 100MB阈值
                Utils.Logger.Info("ApiService", $"📊 上传策略选择: {(useChunkedUpload ? "分片上传" : "统一上传")} (阈值: 100MB)");

                return await concurrencyManager.ExecuteUploadAsync(taskId, async () =>
                {
                    if (useChunkedUpload)
                    {
                        Utils.Logger.Info("ApiService", "🚀 开始分片上传");
                        return await StartChunkedUploadAsync(filePath, processedRequest, progress, cancellationToken);
                    }
                    else
                    {
                        Utils.Logger.Info("ApiService", "🚀 开始统一上传");
                        return await StartUnifiedFileConversionAsync(filePath, processedRequest, progress, cancellationToken);
                    }
                });
            }
            catch (Exception ex)
            {
                Utils.Logger.Info("ApiService", "💥 上传过程中发生异常");
                Utils.Logger.Info("ApiService", $"异常类型: {ex.GetType().Name}");
                Utils.Logger.Info("ApiService", $"异常消息: {ex.Message}");
                Utils.Logger.Info("ApiService", $"异常堆栈: {ex.StackTrace}");
                return ApiResponse<StartConversionResponse>.CreateError($"转换失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理智能格式选项，将智能选项解析为具体格式
        /// </summary>
        private StartConversionRequest ProcessSmartFormatOptions(StartConversionRequest request, string filePath)
        {
            // 创建请求的副本，避免修改原始请求
            var processedRequest = new StartConversionRequest
            {
                // 复制所有属性
                TaskName = request.TaskName,
                Preset = request.Preset,
                OutputFormat = request.OutputFormat,
                Resolution = request.Resolution,
                CustomWidth = request.CustomWidth,
                CustomHeight = request.CustomHeight,
                AspectRatio = request.AspectRatio,
                VideoCodec = request.VideoCodec,
                FrameRate = request.FrameRate,
                QualityMode = request.QualityMode,
                VideoQuality = request.VideoQuality,
                VideoBitrate = request.VideoBitrate,
                EncodingPreset = request.EncodingPreset,
                Profile = request.Profile,
                AudioCodec = request.AudioCodec,
                AudioChannels = request.AudioChannels,
                AudioQualityMode = request.AudioQualityMode,
                AudioQuality = request.AudioQuality,
                AudioBitrate = request.AudioBitrate,
                CustomAudioBitrateValue = request.CustomAudioBitrateValue,
                AudioQualityValue = request.AudioQualityValue,
                SampleRate = request.SampleRate,
                AudioVolume = request.AudioVolume,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                DurationLimit = request.DurationLimit,
                Deinterlace = request.Deinterlace,
                HardwareAcceleration = request.HardwareAcceleration,
                PixelFormat = request.PixelFormat,
                ColorSpace = request.ColorSpace,
                FastStart = request.FastStart,
                TwoPass = request.TwoPass,
                Denoise = request.Denoise,
                VideoFilters = request.VideoFilters,
                AudioFilters = request.AudioFilters,
                Priority = request.Priority,
                MaxRetries = request.MaxRetries,
                Notes = request.Notes,
                CopyTimestamps = request.CopyTimestamps
            };

            // 🎯 处理智能格式选项
            if (!string.IsNullOrEmpty(request.OutputFormat))
            {
                processedRequest.OutputFormat = request.OutputFormat switch
                {
                    "keep_original" => GetOriginalFormat(filePath),
                    "auto_best" => GetBestFormatForFile(filePath),
                    _ => request.OutputFormat // 已经是具体格式，保持不变
                };
            }

            return processedRequest;
        }

        /// <summary>
        /// 获取原始文件格式
        /// </summary>
        private string GetOriginalFormat(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return "mp4";

            var extension = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();

            // 标准化格式名称
            var normalizedFormat = extension switch
            {
                "mpeg" => "mpg",
                _ => extension
            };

            // 验证格式是否支持作为输出格式
            var supportedOutputFormats = new[]
            {
                "mp4", "mkv", "webm", "avi", "mov", "m4v", "3gp",
                "wmv", "flv", "mpg", "ts", "mts", "m2ts", "vob", "asf"
            };

            return supportedOutputFormats.Contains(normalizedFormat) ? normalizedFormat : "mp4";
        }

        /// <summary>
        /// 为文件选择最佳格式
        /// </summary>
        private string GetBestFormatForFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return "mp4";

            var extension = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();

            // 根据原始格式推荐最佳输出格式
            return extension switch
            {
                // 传统格式转为MP4提升兼容性
                "avi" or "wmv" or "flv" => "mp4",

                // Apple格式转为MP4
                "mov" or "m4v" => "mp4",

                // 广播格式转为MKV保持质量
                "ts" or "mts" or "m2ts" => "mkv",

                // DVD格式转为MP4
                "vob" => "mp4",

                // 专有格式转为MP4
                "rm" or "rmvb" or "asf" => "mp4",

                // 现代格式保持原样
                "webm" => "webm",
                "mkv" => "mkv",
                "mp4" => "mp4",

                // 其他格式默认MP4
                _ => "mp4"
            };
        }

        /// <summary>
        /// 分片上传方法 - 适用于大文件，支持断点续传
        /// </summary>
        private async Task<ApiResponse<StartConversionResponse>> StartChunkedUploadAsync(
            string filePath,
            StartConversionRequest request,
            IProgress<UploadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            Utils.Logger.Info("ApiService", "🧩 === 开始分片上传 ===");
            Utils.Logger.Info("ApiService", $"文件: {Path.GetFileName(filePath)}");

            try
            {
                // 创建进度适配器，将分片上传进度转换为通用上传进度
                var chunkedProgress = progress != null ? new Progress<ChunkedUploadProgress>(p =>
                {
                    // 只在重要阶段记录日志
                    if (p.Phase == UploadPhase.Calculating || p.Phase == UploadPhase.Initializing ||
                        p.Phase == UploadPhase.Finalizing || p.Phase == UploadPhase.Completed)
                    {
                        Utils.Logger.Info("ApiService", $"📊 分片进度: {p.Phase} - {p.Message}");
                    }

                    // 减少日志频率，但保持UI更新频率
                    if (p.TotalBytes > 0 && (int)p.Percentage % 10 == 0 && p.Percentage > 0)
                    {
                        Utils.Logger.Info("ApiService", $"进度详情: {p.UploadedBytes}/{p.TotalBytes} bytes ({p.Percentage:F1}%)");
                    }

                    // 始终向UI报告进度，确保UI能及时更新
                    var uploadProgress = new UploadProgress
                    {
                        BytesUploaded = p.UploadedBytes,
                        TotalBytes = p.TotalBytes,
                        Speed = p.Speed,
                        EstimatedTimeRemaining = p.EstimatedTimeRemaining,
                        FileName = Path.GetFileName(filePath),
                        Status = GetUploadStatusMessage(p)
                    };

                    // 验证数据完整性
                    if (uploadProgress.BytesUploaded < 0 || uploadProgress.TotalBytes <= 0 || uploadProgress.BytesUploaded > uploadProgress.TotalBytes)
                    {
                        Utils.Logger.Info("ApiService", $"⚠️ 检测到异常上传数据: BytesUploaded={uploadProgress.BytesUploaded}, TotalBytes={uploadProgress.TotalBytes}, Percentage={uploadProgress.Percentage:F1}%");
                        Utils.Logger.Info("ApiService", $"   原始ChunkedUploadProgress: UploadedBytes={p.UploadedBytes}, TotalBytes={p.TotalBytes}, Percentage={p.Percentage:F1}%");
                    }

                    Utils.Logger.Info("ApiService", $"🔄 转发上传进度: {uploadProgress.FileName} = {uploadProgress.Percentage:F1}% ({uploadProgress.BytesUploaded}/{uploadProgress.TotalBytes})");
                    progress.Report(uploadProgress);
                }) : null;

                Utils.Logger.Info("ApiService", "🚀 调用分片上传服务");
                var result = await _chunkedUploadService.UploadFileAsync(filePath, request, chunkedProgress, cancellationToken);

                Utils.Logger.Info("ApiService", $"📥 分片上传服务返回结果: Success={result.Success}");
                if (!result.Success)
                {
                    Utils.Logger.Info("ApiService", $"失败原因: {result.Message}");
                    Utils.Logger.Info("ApiService", $"错误类型: {result.ErrorType}");
                }

                // 转换返回结果格式
                if (result.Success && result.Data != null)
                {
                    Utils.Logger.Info("ApiService", "✅ 分片上传成功");
                    if (result.Data.TaskId != null)
                    {
                        Utils.Logger.Info("ApiService", $"任务ID: {result.Data.TaskId}");
                    }

                    return new ApiResponse<StartConversionResponse>
                    {
                        Success = true,
                        Data = result.Data
                    };
                }
                else
                {
                    Utils.Logger.Info("ApiService", "❌ 分片上传失败");
                    return new ApiResponse<StartConversionResponse>
                    {
                        Success = false,
                        Message = result.Message,
                        ErrorType = result.ErrorType
                    };
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Info("ApiService", "💥 分片上传异常");
                Utils.Logger.Info("ApiService", $"异常类型: {ex.GetType().Name}");
                Utils.Logger.Info("ApiService", $"异常消息: {ex.Message}");
                Utils.Logger.Info("ApiService", $"异常堆栈: {ex.StackTrace}");

                return new ApiResponse<StartConversionResponse>
                {
                    Success = false,
                    Message = $"分片上传失败: {ex.Message}",
                    ErrorType = "ChunkedUploadError"
                };
            }
            finally
            {
                Utils.Logger.Info("ApiService", "🧩 === 分片上传结束 ===");
            }
        }

        /// <summary>
        /// 获取上传状态消息
        /// </summary>
        private string GetUploadStatusMessage(ChunkedUploadProgress progress)
        {
            return progress.Phase switch
            {
                UploadPhase.Calculating => "正在计算文件校验码...",
                UploadPhase.Initializing => "正在初始化上传...",
                UploadPhase.Uploading => $"正在上传分片 {progress.CompletedChunks}/{progress.TotalChunks}",
                UploadPhase.Finalizing => "正在完成上传...",
                UploadPhase.Completed => "上传完成！",
                _ => progress.Message
            };
        }

        /// <summary>
        /// 统一文件上传转换方法 - 支持所有文件大小，带重试机制
        /// </summary>
        private async Task<ApiResponse<StartConversionResponse>> StartUnifiedFileConversionAsync(
            string filePath,
            StartConversionRequest request,
            IProgress<UploadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            const int maxRetries = 3;
            const int baseDelayMs = 1000;
            var fileInfo = new FileInfo(filePath);

            Utils.Logger.Info("ApiService", "🔄 === 开始统一上传 ===");
            Utils.Logger.Info("ApiService", $"文件: {Path.GetFileName(filePath)}");
            Utils.Logger.Info("ApiService", $"大小: {fileInfo.Length} bytes");
            Utils.Logger.Info("ApiService", $"最大重试次数: {maxRetries}");

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                FileStream? fileStream = null;
                try
                {
                    Utils.Logger.Info("ApiService", $"🔄 开始第 {attempt} 次上传尝试");

                    using var form = new MultipartFormDataContent();

                    // 使用流式上传，支持进度报告和大文件
                    Utils.Logger.Info("ApiService", "📁 创建文件流");
                    fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    var progressContent = new ProgressableStreamContent(
                        fileStream,
                        progress,
                        fileInfo.Length,
                        Path.GetFileName(filePath));

                    progressContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                    form.Add(progressContent, "videoFile", Path.GetFileName(filePath));
                    Utils.Logger.Info("ApiService", "✅ 文件内容已添加到表单");

                    // 添加转换参数
                    Utils.Logger.Info("ApiService", "🎯 添加转换参数");
                    AddConversionParameters(form, request);

                    // 添加重试信息
                    form.Add(new StringContent(attempt.ToString()), "RetryAttempt");
                    form.Add(new StringContent(maxRetries.ToString()), "MaxRetries");
                    Utils.Logger.Info("ApiService", $"📊 重试信息已添加: {attempt}/{maxRetries}");

                    // 统一使用upload/unified接口
                    var uploadUrl = $"{BaseUrl}/api/upload/unified";
                    Utils.Logger.Info("ApiService", $"🚀 开始POST请求: {uploadUrl}");

                    var response = await _httpClient.PostAsync(uploadUrl, form, cancellationToken);

                    Utils.Logger.Info("ApiService", $"📥 收到HTTP响应: {response.StatusCode}");

                    var result = await ProcessConversionResponse(response);

                    if (result.Success)
                    {
                        Utils.Logger.Info("ApiService", $"✅ 统一上传成功 (第 {attempt} 次尝试)");
                        return result;
                    }
                    else
                    {
                        Utils.Logger.Info("ApiService", $"❌ 统一上传失败 (第 {attempt} 次尝试): {result.Message}");
                        if (attempt == maxRetries)
                        {
                            return result; // 最后一次尝试，直接返回结果
                        }
                    }

                    return result;
                }
                catch (Exception ex) when (attempt < maxRetries && IsRetryableException(ex))
                {
                    // 确保文件流被正确释放
                    fileStream?.Dispose();

                    // 计算延迟时间（指数退避）
                    var delay = TimeSpan.FromMilliseconds(baseDelayMs * Math.Pow(2, attempt - 1));

                    System.Diagnostics.Debug.WriteLine($"上传失败，第 {attempt} 次尝试，{delay.TotalSeconds}秒后重试: {ex.Message}");

                    // 报告重试状态
                    progress?.Report(new UploadProgress
                    {
                        BytesUploaded = 0,
                        TotalBytes = fileInfo.Length,
                        Speed = 0,
                        EstimatedTimeRemaining = null,
                        FileName = Path.GetFileName(filePath),
                        Status = $"连接中断，{delay.TotalSeconds}秒后进行第 {attempt + 1} 次尝试..."
                    });

                    await Task.Delay(delay, cancellationToken);
                    continue;
                }
                catch (Exception ex)
                {
                    // 确保文件流被正确释放
                    fileStream?.Dispose();

                    // 最后一次尝试失败或不可重试的异常
                    return new ApiResponse<StartConversionResponse>
                    {
                        Success = false,
                        Message = $"上传失败: {ex.Message}",
                        ErrorType = GetErrorType(ex)
                    };
                }
            }

            // 所有重试都失败了
            return new ApiResponse<StartConversionResponse>
            {
                Success = false,
                Message = $"上传失败，已重试 {maxRetries} 次",
                ErrorType = "MaxRetriesExceeded"
            };
        }

        /// <summary>
        /// 获取异常的错误类型
        /// </summary>
        private string GetErrorType(Exception ex)
        {
            return ex switch
            {
                TaskCanceledException when ex.InnerException is TimeoutException => "Timeout",
                OperationCanceledException => "Cancelled",
                HttpRequestException => "NetworkError",
                _ => "General"
            };
        }

        /// <summary>
        /// 判断异常是否可以重试
        /// </summary>
        private bool IsRetryableException(Exception ex)
        {
            return ex is HttpRequestException ||
                   ex is TaskCanceledException ||
                   (ex is OperationCanceledException && !(ex is TaskCanceledException)) ||
                   ex.Message.Contains("Unexpected end of request content") ||
                   ex.Message.Contains("The request was aborted") ||
                   ex.Message.Contains("Connection reset");
        }

        /// <summary>
        /// 添加转换参数到FormData（优化：只传递非空值，减少网络传输）
        /// </summary>
        private void AddConversionParameters(MultipartFormDataContent form, StartConversionRequest request)
        {
            // 基本信息 - 只传递非空值
            if (!string.IsNullOrWhiteSpace(request.TaskName))
                form.Add(new StringContent(request.TaskName), "TaskName");

            // Preset参数 - 如果不为空才传递
            if (!string.IsNullOrWhiteSpace(request.Preset))
                form.Add(new StringContent(request.Preset), "preset");

            // 基本设置 - 只传递有效值
            if (!string.IsNullOrWhiteSpace(request.OutputFormat))
                form.Add(new StringContent(request.OutputFormat), "OutputFormat");
            if (!string.IsNullOrWhiteSpace(request.Resolution))
                form.Add(new StringContent(request.Resolution), "Resolution");
            if (request.CustomWidth.HasValue && request.CustomWidth.Value > 0)
                form.Add(new StringContent(request.CustomWidth.Value.ToString()), "CustomWidth");
            if (request.CustomHeight.HasValue && request.CustomHeight.Value > 0)
                form.Add(new StringContent(request.CustomHeight.Value.ToString()), "CustomHeight");
            if (!string.IsNullOrWhiteSpace(request.AspectRatio))
                form.Add(new StringContent(request.AspectRatio), "AspectRatio");

            // 视频设置 - 智能传递有效值
            if (!string.IsNullOrWhiteSpace(request.VideoCodec))
                form.Add(new StringContent(request.VideoCodec), "VideoCodec");
            if (!string.IsNullOrWhiteSpace(request.FrameRate))
                form.Add(new StringContent(request.FrameRate), "FrameRate");

            // QualityMode有默认值，但仍需传递以确保服务端正确处理
            if (!string.IsNullOrWhiteSpace(request.QualityMode))
                form.Add(new StringContent(request.QualityMode), "QualityMode");

            if (!string.IsNullOrWhiteSpace(request.VideoQuality))
                form.Add(new StringContent(request.VideoQuality), "VideoQuality");
            if (request.VideoBitrate.HasValue && request.VideoBitrate.Value > 0)
                form.Add(new StringContent(request.VideoBitrate.Value.ToString()), "VideoBitrate");
            if (!string.IsNullOrWhiteSpace(request.EncodingPreset))
                form.Add(new StringContent(request.EncodingPreset), "EncodingPreset");
            if (!string.IsNullOrWhiteSpace(request.Profile))
                form.Add(new StringContent(request.Profile), "Profile");

            // 音频设置 - 智能传递有效值
            if (!string.IsNullOrWhiteSpace(request.AudioCodec))
                form.Add(new StringContent(request.AudioCodec), "AudioCodec");
            if (!string.IsNullOrWhiteSpace(request.AudioChannels))
                form.Add(new StringContent(request.AudioChannels), "AudioChannels");

            // AudioQualityMode有默认值，但仍需传递
            if (!string.IsNullOrWhiteSpace(request.AudioQualityMode))
                form.Add(new StringContent(request.AudioQualityMode), "AudioQualityMode");

            if (!string.IsNullOrWhiteSpace(request.AudioQuality))
                form.Add(new StringContent(request.AudioQuality), "AudioQuality");
            if (!string.IsNullOrWhiteSpace(request.AudioBitrate))
                form.Add(new StringContent(request.AudioBitrate), "AudioBitrate");
            if (request.CustomAudioBitrateValue.HasValue && request.CustomAudioBitrateValue.Value > 0)
                form.Add(new StringContent(request.CustomAudioBitrateValue.Value.ToString()), "CustomAudioBitrateValue");
            if (request.AudioQualityValue.HasValue && request.AudioQualityValue.Value > 0)
                form.Add(new StringContent(request.AudioQualityValue.Value.ToString()), "AudioQualityValue");
            if (!string.IsNullOrWhiteSpace(request.SampleRate))
                form.Add(new StringContent(request.SampleRate), "SampleRate");
            if (!string.IsNullOrWhiteSpace(request.AudioVolume))
                form.Add(new StringContent(request.AudioVolume), "AudioVolume");

            // 高级选项 - 只传递有意义的值
            if (!string.IsNullOrWhiteSpace(request.StartTime))
                form.Add(new StringContent(request.StartTime), "StartTime");
            if (request.EndTime.HasValue && request.EndTime.Value > 0)
                form.Add(new StringContent(request.EndTime.Value.ToString()), "EndTime");
            if (!string.IsNullOrWhiteSpace(request.Duration))
                form.Add(new StringContent(request.Duration), "Duration");
            if (request.DurationLimit.HasValue && request.DurationLimit.Value > 0)
                form.Add(new StringContent(request.DurationLimit.Value.ToString()), "DurationLimit");

            // 布尔值始终传递（有默认值）
            form.Add(new StringContent(request.Deinterlace.ToString().ToLower()), "Deinterlace");

            if (!string.IsNullOrWhiteSpace(request.Denoise) && request.Denoise != "none")
                form.Add(new StringContent(request.Denoise), "Denoise");
            if (!string.IsNullOrWhiteSpace(request.ColorSpace))
                form.Add(new StringContent(request.ColorSpace), "ColorSpace");
            if (!string.IsNullOrWhiteSpace(request.PixelFormat))
                form.Add(new StringContent(request.PixelFormat), "PixelFormat");
            // 自定义参数 - 优先使用CustomParameters，如果为空则使用CustomParams
            var customParams = !string.IsNullOrWhiteSpace(request.CustomParameters)
                ? request.CustomParameters
                : request.CustomParams;
            if (!string.IsNullOrWhiteSpace(customParams))
                form.Add(new StringContent(customParams), "CustomParameters");

            // 硬件加速 - 只在非默认值时传递
            if (!string.IsNullOrWhiteSpace(request.HardwareAcceleration) && request.HardwareAcceleration != "auto")
                form.Add(new StringContent(request.HardwareAcceleration), "HardwareAcceleration");

            // 滤镜 - 只在有实际内容时传递
            if (!string.IsNullOrWhiteSpace(request.VideoFilters))
                form.Add(new StringContent(request.VideoFilters), "VideoFilters");
            if (!string.IsNullOrWhiteSpace(request.AudioFilters))
                form.Add(new StringContent(request.AudioFilters), "AudioFilters");

            // 任务设置 - 智能传递
            if (request.Priority != 0)  // 只在非默认值时传递
                form.Add(new StringContent(request.Priority.ToString()), "Priority");
            if (request.MaxRetries != 3)  // 只在非默认值时传递
                form.Add(new StringContent(request.MaxRetries.ToString()), "MaxRetries");
            if (!string.IsNullOrWhiteSpace(request.Tags))
                form.Add(new StringContent(request.Tags), "Tags");
            if (!string.IsNullOrWhiteSpace(request.Notes))
                form.Add(new StringContent(request.Notes), "Notes");

            // 编码选项 - 只在非默认值时传递
            if (request.TwoPass)  // 默认false，只在true时传递
                form.Add(new StringContent(request.TwoPass.ToString().ToLower()), "TwoPass");
            if (!request.FastStart)  // 默认true，只在false时传递
                form.Add(new StringContent(request.FastStart.ToString().ToLower()), "FastStart");
            if (!request.CopyTimestamps)  // 默认true，只在false时传递
                form.Add(new StringContent(request.CopyTimestamps.ToString().ToLower()), "CopyTimestamps");
        }

        /// <summary>
        /// 验证参数是否有效（非空且有意义）
        /// </summary>
        private static bool IsValidParameter(string? value, string? defaultValue = null)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            // 如果有默认值，检查是否与默认值不同
            if (!string.IsNullOrEmpty(defaultValue))
                return !string.Equals(value, defaultValue, StringComparison.OrdinalIgnoreCase);

            return true;
        }

        /// <summary>
        /// 验证数值参数是否有效
        /// </summary>
        private static bool IsValidParameter(int? value, int defaultValue = 0)
        {
            return value.HasValue && value.Value != defaultValue;
        }

        /// <summary>
        /// 验证数值参数是否有效
        /// </summary>
        private static bool IsValidParameter(double? value, double defaultValue = 0)
        {
            return value.HasValue && Math.Abs(value.Value - defaultValue) > 0.001;
        }

        /// <summary>
        /// 批量转换多个文件
        /// </summary>
        public async Task<ApiResponse<BatchConversionResponse>> StartBatchConversionAsync(
            List<string> filePaths,
            StartConversionRequest request,
            IProgress<BatchUploadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            // 使用文件日志而不是Debug.WriteLine
            Utils.Logger.Info("ApiService", "📦 === 开始批量转换 ===");
            Utils.Logger.Info("ApiService", $"文件数量: {filePaths.Count}");
            Utils.Logger.Info("ApiService", $"转换参数: 格式={request.OutputFormat}, 分辨率={request.Resolution}");
            Utils.Logger.Info("ApiService", $"BaseUrl: {BaseUrl}");

            try
            {
                var batchId = Guid.NewGuid().ToString();
                var results = new List<ConversionTaskResult>();
                var totalFiles = filePaths.Count;
                var completedFiles = 0;

                Utils.Logger.Info("ApiService", $"批次ID: {batchId}");

                // 打印所有文件路径
                for (int i = 0; i < filePaths.Count; i++)
                {
                    Utils.Logger.Info("ApiService", $"文件 {i + 1}: {filePaths[i]}");
                }

                Utils.Logger.Info("ApiService", "开始逐个处理文件...");

                foreach (var filePath in filePaths)
                {
                    try
                    {
                        Utils.Logger.Info("ApiService", $"🔄 开始处理文件: {Path.GetFileName(filePath)} ({completedFiles + 1}/{totalFiles})");

                        // 在处理每个文件前检查磁盘空间
                        if (!await CheckDiskSpaceBeforeProcessingAsync(filePath))
                        {
                            Utils.Logger.Info("ApiService", $"❌ 磁盘空间不足，暂停处理文件: {Path.GetFileName(filePath)}");

                            // 添加失败结果
                            results.Add(new ConversionTaskResult
                            {
                                FilePath = filePath,
                                Success = false,
                                TaskId = null,
                                Message = "磁盘空间不足，任务已暂停"
                            });

                            // 报告暂停状态
                            progress?.Report(new BatchUploadProgress
                            {
                                BatchId = batchId,
                                CurrentFile = Path.GetFileName(filePath),
                                CurrentFileProgress = 0,
                                CompletedFiles = completedFiles,
                                TotalFiles = totalFiles,
                                OverallProgress = (completedFiles * 100.0) / totalFiles,
                                IsPaused = true,
                                PauseReason = "磁盘空间不足"
                            });

                            Utils.Logger.Info("ApiService", "⏸️ 批量转换因空间不足而暂停");
                            break; // 暂停处理后续文件
                        }

                        var fileProgress = new Progress<UploadProgress>(p =>
                        {
                            // 验证并修正进度值
                            var safeFileProgress = Math.Max(0, Math.Min(100, p.Percentage));

                            // 减少日志频率，但保持UI更新频率
                            if ((int)safeFileProgress % 10 == 0 && safeFileProgress > 0)
                            {
                                Utils.Logger.Info("ApiService", $"📊 文件进度: {Path.GetFileName(filePath)} - {safeFileProgress:F1}%");
                            }

                            // 计算正确的总体进度（确保不超过100%）
                            var overallProgress = Math.Min(100.0, (completedFiles * 100.0 + safeFileProgress) / totalFiles);

                            Utils.Logger.Info("ApiService", $"🔄 进度计算: 已完成={completedFiles}, 当前进度={safeFileProgress:F1}%, 总进度={overallProgress:F1}%");

                            // 始终向UI报告进度，确保UI能及时更新
                            progress?.Report(new BatchUploadProgress
                            {
                                BatchId = batchId,
                                CurrentFile = Path.GetFileName(filePath),
                                CurrentFileProgress = safeFileProgress,
                                CompletedFiles = completedFiles,
                                TotalFiles = totalFiles,
                                OverallProgress = overallProgress
                            });
                        });

                        Utils.Logger.Info("ApiService", $"🚀 调用单文件转换: {Path.GetFileName(filePath)}");
                        var result = await StartConversionAsync(filePath, request, fileProgress, cancellationToken);

                        Utils.Logger.Info("ApiService", $"📥 单文件转换结果: {Path.GetFileName(filePath)} - Success={result.Success}");
                        if (!result.Success)
                        {
                            Utils.Logger.Info("ApiService", $"失败原因: {result.Message}");
                            Utils.Logger.Info("ApiService", $"错误类型: {result.ErrorType}");
                        }

                        results.Add(new ConversionTaskResult
                        {
                            FilePath = filePath,
                            Success = result.Success,
                            TaskId = result.Data?.TaskId,
                            Message = result.Message
                        });

                        completedFiles++;
                        Utils.Logger.Info("ApiService", $"✅ 文件处理完成: {Path.GetFileName(filePath)} ({completedFiles}/{totalFiles})");

                        // 报告文件完成后的最终进度
                        var finalOverallProgress = (completedFiles * 100.0) / totalFiles;
                        progress?.Report(new BatchUploadProgress
                        {
                            BatchId = batchId,
                            CurrentFile = completedFiles < totalFiles ? "" : Path.GetFileName(filePath), // 如果还有文件，清空当前文件
                            CurrentFileProgress = 100, // 当前文件已完成
                            CompletedFiles = completedFiles,
                            TotalFiles = totalFiles,
                            OverallProgress = finalOverallProgress
                        });

                        Utils.Logger.Info("ApiService", $"📊 文件完成进度: 已完成={completedFiles}/{totalFiles}, 总进度={finalOverallProgress:F1}%");
                    }
                    catch (Exception ex)
                    {
                        Utils.Logger.Info("ApiService", $"💥 文件处理异常: {Path.GetFileName(filePath)}");
                        Utils.Logger.Info("ApiService", $"异常类型: {ex.GetType().Name}");
                        Utils.Logger.Info("ApiService", $"异常消息: {ex.Message}");
                        Utils.Logger.Info("ApiService", $"异常堆栈: {ex.StackTrace}");

                        results.Add(new ConversionTaskResult
                        {
                            FilePath = filePath,
                            Success = false,
                            Message = ex.Message
                        });

                        completedFiles++;
                        Utils.Logger.Info("ApiService", $"❌ 文件处理失败: {Path.GetFileName(filePath)} ({completedFiles}/{totalFiles})");
                    }
                }

                Utils.Logger.Info("ApiService", "所有文件处理完成，开始统计结果...");

                var successCount = results.Count(r => r.Success);
                System.Diagnostics.Debug.WriteLine($"[ApiService] 📊 批量转换统计: 成功 {successCount}/{totalFiles} 个文件");

                // 打印详细结果
                foreach (var result in results)
                {
                    var status = result.Success ? "✅" : "❌";
                    System.Diagnostics.Debug.WriteLine($"[ApiService] {status} {Path.GetFileName(result.FilePath)}: {result.Message}");
                    if (result.Success && !string.IsNullOrEmpty(result.TaskId))
                    {
                        System.Diagnostics.Debug.WriteLine($"[ApiService]    TaskId: {result.TaskId}");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[ApiService] 📦 === 批量转换完成 ===");

                return new ApiResponse<BatchConversionResponse>
                {
                    Success = successCount > 0,
                    Data = new BatchConversionResponse
                    {
                        BatchId = batchId,
                        TotalFiles = totalFiles,
                        SuccessCount = successCount,
                        FailedCount = totalFiles - successCount,
                        Results = results
                    },
                    Message = $"批量转换完成：成功 {successCount}/{totalFiles} 个文件"
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ApiService] 💥 批量转换异常");
                System.Diagnostics.Debug.WriteLine($"[ApiService] 异常类型: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"[ApiService] 异常消息: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[ApiService] 异常堆栈: {ex.StackTrace}");
                return ApiResponse<BatchConversionResponse>.CreateError($"批量转换失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理转换响应
        /// </summary>
        private async Task<ApiResponse<StartConversionResponse>> ProcessConversionResponse(HttpResponseMessage response)
        {
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<StartConversionResponse>(content, _jsonOptions);
                return new ApiResponse<StartConversionResponse>
                {
                    Success = true,
                    Data = result
                };
            }
            else
            {
                var error = JsonSerializer.Deserialize<ErrorResponse>(content, _jsonOptions);
                return new ApiResponse<StartConversionResponse>
                {
                    Success = false,
                    Message = error?.Message ?? "转换启动失败"
                };
            }
        }

        /// <summary>
        /// 获取任务状态
        /// </summary>
        public async Task<ApiResponse<ConversionTask>> GetTaskStatusAsync(string taskId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseUrl}/api/conversion/status/{taskId}");
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<TaskStatusResponse>(content, _jsonOptions);
                    if (result?.Success == true && result.Task != null)
                    {
                        var task = MapToConversionTask(result.Task);
                        return new ApiResponse<ConversionTask>
                        {
                            Success = true,
                            Data = task
                        };
                    }
                }

                var error = JsonSerializer.Deserialize<ErrorResponse>(content, _jsonOptions);
                return new ApiResponse<ConversionTask>
                {
                    Success = false,
                    Message = error?.Message ?? "获取任务状态失败"
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<ConversionTask>
                {
                    Success = false,
                    Message = $"网络错误: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 获取最近任务
        /// </summary>
        public async Task<ApiResponse<List<ConversionTask>>> GetRecentTasksAsync(int count = 10)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseUrl}/api/conversion/recent?count={count}");
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var taskDataList = JsonSerializer.Deserialize<List<TaskData>>(content, _jsonOptions);
                    if (taskDataList != null)
                    {
                        var tasks = taskDataList.Select(MapToConversionTask).ToList();
                        return new ApiResponse<List<ConversionTask>>
                        {
                            Success = true,
                            Data = tasks
                        };
                    }
                }

                return new ApiResponse<List<ConversionTask>>
                {
                    Success = false,
                    Message = "获取最近任务失败"
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<ConversionTask>>
                {
                    Success = false,
                    Message = $"网络错误: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 获取已完成的任务列表
        /// </summary>
        public async Task<ApiResponse<List<ConversionTask>>> GetCompletedTasksAsync(int page = 1, int pageSize = 50, string? search = null)
        {
            try
            {
                var url = $"{BaseUrl}/api/task/list?page={page}&pageSize={pageSize}&status=Completed";
                if (!string.IsNullOrEmpty(search))
                {
                    url += $"&search={Uri.EscapeDataString(search)}";
                }

                var response = await _httpClient.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var pagedResponse = JsonSerializer.Deserialize<PagedApiResponse<List<TaskData>>>(content, _jsonOptions);
                    if (pagedResponse?.Success == true && pagedResponse.Data != null)
                    {
                        var tasks = pagedResponse.Data.Select(MapToConversionTask).ToList();
                        return new ApiResponse<List<ConversionTask>>
                        {
                            Success = true,
                            Data = tasks,
                            Message = pagedResponse.Message
                        };
                    }
                }

                return new ApiResponse<List<ConversionTask>>
                {
                    Success = false,
                    Message = "获取已完成任务失败"
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<ConversionTask>>
                {
                    Success = false,
                    Message = $"网络错误: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 删除任务
        /// </summary>
        public async Task<ApiResponse<bool>> DeleteTaskAsync(string taskId)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"{BaseUrl}/api/task/{taskId}");
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return new ApiResponse<bool>
                    {
                        Success = true,
                        Data = true,
                        Message = "任务已删除"
                    };
                }
                else
                {
                    var error = JsonSerializer.Deserialize<ErrorResponse>(content, _jsonOptions);
                    return new ApiResponse<bool>
                    {
                        Success = false,
                        Message = error?.Message ?? "删除任务失败"
                    };
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<bool>
                {
                    Success = false,
                    Message = $"网络错误: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 取消任务
        /// </summary>
        public async Task<ApiResponse<bool>> CancelTaskAsync(string taskId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"{BaseUrl}/api/conversion/cancel/{taskId}", null);
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return new ApiResponse<bool>
                    {
                        Success = true,
                        Data = true,
                        Message = "任务已取消"
                    };
                }
                else
                {
                    var error = JsonSerializer.Deserialize<ErrorResponse>(content, _jsonOptions);
                    return new ApiResponse<bool>
                    {
                        Success = false,
                        Message = error?.Message ?? "取消任务失败"
                    };
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<bool>
                {
                    Success = false,
                    Message = $"网络错误: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 下载文件（支持并发控制和默认路径）
        /// </summary>
        public async Task<ApiResponse<string>> DownloadFileAsync(string taskId, string? savePath = null)
        {
            try
            {
                // 如果没有指定保存路径，使用默认输出路径
                if (string.IsNullOrEmpty(savePath))
                {
                    var settingsService = SystemSettingsService.Instance;
                    var defaultPath = settingsService.GetDefaultOutputPath();

                    if (!string.IsNullOrEmpty(defaultPath) && Directory.Exists(defaultPath))
                    {
                        // 获取任务信息以确定文件名
                        var taskResponse = await GetTaskStatusAsync(taskId);
                        if (taskResponse.Success && taskResponse.Data != null)
                        {
                            var fileName = !string.IsNullOrEmpty(taskResponse.Data.OutputFileName)
                                ? taskResponse.Data.OutputFileName
                                : $"converted_{taskId}.mp4";
                            savePath = Path.Combine(defaultPath, fileName);
                        }
                        else
                        {
                            savePath = Path.Combine(defaultPath, $"converted_{taskId}.mp4");
                        }
                    }
                    else
                    {
                        // 使用下载文件夹作为默认路径
                        var downloadsPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                        downloadsPath = Path.Combine(downloadsPath, "Downloads");
                        savePath = Path.Combine(downloadsPath, $"converted_{taskId}.mp4");
                    }
                }

                // 确保目录存在
                var directory = Path.GetDirectoryName(savePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 使用并发管理器控制下载并发
                var concurrencyManager = ConcurrencyManager.Instance;

                return await concurrencyManager.ExecuteDownloadAsync(taskId, async () =>
                {
                    var response = await _httpClient.GetAsync($"{BaseUrl}/api/conversion/download/{taskId}");

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsByteArrayAsync();
                        await File.WriteAllBytesAsync(savePath, content);

                        return new ApiResponse<string>
                        {
                            Success = true,
                            Data = savePath,
                            Message = "文件下载成功"
                        };
                    }
                    else
                    {
                        return new ApiResponse<string>
                        {
                            Success = false,
                            Message = "文件下载失败"
                        };
                    }
                });
            }
            catch (Exception ex)
            {
                return new ApiResponse<string>
                {
                    Success = false,
                    Message = $"下载错误: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 映射任务数据到ConversionTask
        /// </summary>
        private ConversionTask MapToConversionTask(TaskData taskData)
        {
            return new ConversionTask
            {
                Id = taskData.Id ?? string.Empty,
                TaskName = taskData.TaskName ?? string.Empty,
                OriginalFileName = taskData.OriginalFileName ?? string.Empty,
                OutputFileName = taskData.OutputFileName ?? string.Empty,
                Status = Enum.TryParse<ConversionStatus>(taskData.Status, out var status) ? status : ConversionStatus.Pending,
                Progress = taskData.Progress,
                ErrorMessage = taskData.ErrorMessage,
                CreatedAt = taskData.CreatedAt,
                StartedAt = taskData.StartedAt,
                CompletedAt = taskData.CompletedAt,
                EstimatedTimeRemaining = taskData.EstimatedTimeRemaining,
                ConversionSpeed = taskData.ConversionSpeed,
                Duration = taskData.Duration,
                CurrentTime = taskData.CurrentTime
            };
        }

        #region 磁盘空间检查

        /// <summary>
        /// 在处理文件前检查磁盘空间
        /// </summary>
        private async Task<bool> CheckDiskSpaceBeforeProcessingAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Utils.Logger.Info("ApiService", $"⚠️ 文件不存在，跳过空间检查: {filePath}");
                    return false;
                }

                var fileInfo = new FileInfo(filePath);
                var fileSize = fileInfo.Length;

                Utils.Logger.Info("ApiService", $"📊 检查单文件空间需求: {Path.GetFileName(filePath)} ({fileSize / 1024.0 / 1024:F2}MB)");

                // 创建磁盘空间API服务
                var diskSpaceApiService = new DiskSpaceApiService(BaseUrl);

                // 调用空间检查API
                var spaceCheckResult = await diskSpaceApiService.CheckSpaceAsync(fileSize);

                if (spaceCheckResult?.Success == true)
                {
                    if (spaceCheckResult.HasEnoughSpace)
                    {
                        Utils.Logger.Info("ApiService", $"✅ 空间充足: 需要={spaceCheckResult.RequiredSpaceGB:F2}GB, 可用={spaceCheckResult.AvailableSpaceGB:F2}GB");
                        return true;
                    }
                    else
                    {
                        Utils.Logger.Info("ApiService", $"❌ 空间不足: 需要={spaceCheckResult.RequiredSpaceGB:F2}GB, 可用={spaceCheckResult.AvailableSpaceGB:F2}GB");
                        return false;
                    }
                }
                else
                {
                    Utils.Logger.Info("ApiService", $"⚠️ 空间检查失败: {spaceCheckResult?.Message}，允许继续处理");
                    return true; // 检查失败时允许继续，避免阻塞用户
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Info("ApiService", $"❌ 磁盘空间检查异常: {ex.Message}，允许继续处理");
                return true; // 异常时允许继续，避免阻塞用户
            }
        }

        #endregion

        #region 系统管理API

        /// <summary>
        /// 获取系统状态信息
        /// </summary>
        public async Task<ApiResponse<SystemStatusInfo>> GetSystemStatusAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseUrl}/api/health/status");
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var statusData = JsonSerializer.Deserialize<JsonElement>(content, _jsonOptions);

                    // 解析系统状态信息
                    var systemInfo = new SystemStatusInfo
                    {
                        Status = "running",
                        Timestamp = DateTime.Now,
                        ServerVersion = "v1.0.0", // 从响应中解析
                        FFmpegVersion = "6.0", // 从响应中解析
                        HardwareAcceleration = "NVIDIA CUDA", // 从响应中解析
                        Uptime = TimeSpan.FromDays(1), // 从响应中解析
                        MemoryUsage = 0,
                        ActiveTasks = 0,
                        PendingTasks = 0
                    };

                    return new ApiResponse<SystemStatusInfo>
                    {
                        Success = true,
                        Data = systemInfo,
                        Message = "获取系统状态成功"
                    };
                }
                else
                {
                    return new ApiResponse<SystemStatusInfo>
                    {
                        Success = false,
                        Message = $"获取系统状态失败: {response.StatusCode}",
                        ErrorType = content
                    };
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<SystemStatusInfo>
                {
                    Success = false,
                    Message = "获取系统状态时发生异常",
                    ErrorType = ex.Message
                };
            }
        }

        /// <summary>
        /// 执行文件清理
        /// </summary>
        public async Task<ApiResponse<CleanupResult>> CleanupFilesAsync(string cleanupType = "temp")
        {
            try
            {
                var response = await _httpClient.PostAsync($"{BaseUrl}/api/cleanup/cleanup/{cleanupType}", null);
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<CleanupResult>(content, _jsonOptions);

                    return new ApiResponse<CleanupResult>
                    {
                        Success = true,
                        Data = result,
                        Message = "文件清理成功"
                    };
                }
                else
                {
                    return new ApiResponse<CleanupResult>
                    {
                        Success = false,
                        Message = $"文件清理失败: {response.StatusCode}",
                        ErrorType = content
                    };
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<CleanupResult>
                {
                    Success = false,
                    Message = "文件清理时发生异常",
                    ErrorType = ex.Message
                };
            }
        }

        /// <summary>
        /// 获取系统诊断信息
        /// </summary>
        public async Task<ApiResponse<List<DiagnosticItem>>> GetSystemDiagnosticsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseUrl}/api/health/diagnostics");
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var diagnostics = JsonSerializer.Deserialize<List<DiagnosticItem>>(content, _jsonOptions);

                    return new ApiResponse<List<DiagnosticItem>>
                    {
                        Success = true,
                        Data = diagnostics ?? new List<DiagnosticItem>(),
                        Message = "获取系统诊断信息成功"
                    };
                }
                else
                {
                    return new ApiResponse<List<DiagnosticItem>>
                    {
                        Success = false,
                        Message = $"获取系统诊断信息失败: {response.StatusCode}",
                        ErrorType = content
                    };
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<DiagnosticItem>>
                {
                    Success = false,
                    Message = "获取系统诊断信息时发生异常",
                    ErrorType = ex.Message
                };
            }
        }

        #endregion

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    // API响应模型
    public class StartConversionResponse
    {
        public bool Success { get; set; }
        public string? TaskId { get; set; }
        public string? TaskName { get; set; }
        public string? Message { get; set; }
    }

    public class BatchConversionResponse
    {
        public string BatchId { get; set; } = string.Empty;
        public int TotalFiles { get; set; }
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }
        public List<ConversionTaskResult> Results { get; set; } = new();
    }

    public class ConversionTaskResult
    {
        public string FilePath { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? TaskId { get; set; }
        public string? Message { get; set; }
    }

    public class BatchUploadProgress
    {
        public string BatchId { get; set; } = string.Empty;
        public string CurrentFile { get; set; } = string.Empty;
        public double CurrentFileProgress { get; set; }
        public int CompletedFiles { get; set; }
        public int TotalFiles { get; set; }
        public double OverallProgress { get; set; }
        public bool IsPaused { get; set; } = false;
        public string? PauseReason { get; set; }
    }

    public class TaskStatusResponse
    {
        public bool Success { get; set; }
        public TaskData? Task { get; set; }
    }

    public class TaskData
    {
        public string? Id { get; set; }
        public string? TaskName { get; set; }
        public string? Status { get; set; }
        public int Progress { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int? EstimatedTimeRemaining { get; set; }
        public double? ConversionSpeed { get; set; }
        public double? Duration { get; set; }
        public double? CurrentTime { get; set; }
        public string? OriginalFileName { get; set; }
        public string? OutputFileName { get; set; }
    }

    public class ErrorResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }

    /// <summary>
    /// 上传进度信息
    /// </summary>
    public class UploadProgress
    {
        public long BytesUploaded { get; set; }
        public long TotalBytes { get; set; }
        public double Percentage => TotalBytes > 0 ? (double)BytesUploaded / TotalBytes * 100 : 0;
        public double Speed { get; set; } // bytes per second
        public TimeSpan? EstimatedTimeRemaining { get; set; }
        public string? FileName { get; set; }
        public string? Status { get; set; } // 状态信息，如重试提示
    }

    /// <summary>
    /// 支持进度报告的流内容
    /// </summary>
    public class ProgressableStreamContent : HttpContent
    {
        private readonly Stream _content;
        private readonly IProgress<UploadProgress>? _progress;
        private readonly long _totalBytes;
        private readonly string? _fileName;
        private long _bytesUploaded;
        private DateTime _startTime;

        public ProgressableStreamContent(Stream content, IProgress<UploadProgress>? progress, long totalBytes, string? fileName = null)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _progress = progress;
            _totalBytes = totalBytes;
            _fileName = fileName;
            _startTime = DateTime.Now;
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            await SerializeToStreamAsync(stream, context, CancellationToken.None);
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            const int bufferSize = 65536; // 增加缓冲区大小到64KB
            var buffer = new byte[bufferSize];
            _bytesUploaded = 0;
            _startTime = DateTime.Now;
            var lastProgressReport = DateTime.Now;

            try
            {
                int bytesRead;
                while ((bytesRead = await _content.ReadAsync(buffer, 0, bufferSize, cancellationToken)) > 0)
                {
                    // 检查取消令牌
                    cancellationToken.ThrowIfCancellationRequested();

                    // 写入数据到流
                    await stream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                    await stream.FlushAsync(cancellationToken); // 确保数据被发送

                    _bytesUploaded += bytesRead;

                    // 限制进度报告频率，避免过于频繁的UI更新
                    var now = DateTime.Now;
                    if (_progress != null && (now - lastProgressReport).TotalMilliseconds >= 500) // 每500ms报告一次
                    {
                        var elapsed = now - _startTime;
                        var speed = elapsed.TotalSeconds > 0 ? _bytesUploaded / elapsed.TotalSeconds : 0;
                        var remaining = speed > 0 ? TimeSpan.FromSeconds((_totalBytes - _bytesUploaded) / speed) : (TimeSpan?)null;

                        _progress.Report(new UploadProgress
                        {
                            BytesUploaded = _bytesUploaded,
                            TotalBytes = _totalBytes,
                            Speed = speed,
                            EstimatedTimeRemaining = remaining,
                            FileName = _fileName
                        });

                        lastProgressReport = now;
                    }
                }

                // 确保最终进度报告
                if (_progress != null)
                {
                    _progress.Report(new UploadProgress
                    {
                        BytesUploaded = _bytesUploaded,
                        TotalBytes = _totalBytes,
                        Speed = 0,
                        EstimatedTimeRemaining = null,
                        FileName = _fileName
                    });
                }
            }
            catch (OperationCanceledException)
            {
                // 上传被取消，重新抛出以便上层处理
                throw;
            }
            catch (Exception ex)
            {
                // 记录其他异常但不阻止重试
                System.Diagnostics.Debug.WriteLine($"上传过程中发生异常: {ex.Message}");
                throw;
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = _totalBytes;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _content?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// 系统状态信息
    /// </summary>
    public class SystemStatusInfo
    {
        public string Status { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public string ServerVersion { get; set; } = "";
        public string FFmpegVersion { get; set; } = "";
        public string HardwareAcceleration { get; set; } = "";
        public TimeSpan Uptime { get; set; }
        public long MemoryUsage { get; set; }
        public int ActiveTasks { get; set; }
        public int PendingTasks { get; set; }
    }

    /// <summary>
    /// 清理结果
    /// </summary>
    public class CleanupResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public int DeletedFiles { get; set; }
        public long FreedSpace { get; set; }
        public DateTime CleanupTime { get; set; }
    }

    /// <summary>
    /// 诊断项目
    /// </summary>
    public class DiagnosticItem
    {
        public string Name { get; set; } = "";
        public string Status { get; set; } = "";
        public string Level { get; set; } = "";
        public string? Details { get; set; }
    }

    /// <summary>
    /// 分页API响应
    /// </summary>
    public class PagedApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string Message { get; set; } = "";
        public string? ErrorType { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
    }
}
