using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VideoConversion_ClientTo.Application.DTOs;
using VideoConversion_ClientTo.Domain.Models;
using VideoConversion_ClientTo.Infrastructure;

namespace VideoConversion_ClientTo.Infrastructure.Services
{
    /// <summary>
    /// API服务 - 与Client项目一致的实现
    /// </summary>
    public class ApiService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly ConcurrencyManager _concurrencyManager;
        private ChunkedUploadService _chunkedUploadService; // 🔑 移除readonly以支持动态重新创建
        private bool _disposed = false;

        public string BaseUrl { get; set; } = "http://localhost:5065";

        public ApiService()
        {
            // 配置HttpClient以提高大文件上传的稳定性 - 与Client项目一致
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

            // 初始化并发管理器
            _concurrencyManager = ConcurrencyManager.Instance;

            // 🔑 从系统设置获取BaseUrl并初始化分片上传服务
            BaseUrl = SystemSettingsService.Instance.GetServerAddress();
            _chunkedUploadService = new ChunkedUploadService(BaseUrl);

            // 🔑 监听系统设置变化以更新BaseUrl
            SystemSettingsService.Instance.SettingsChanged += OnSettingsChanged;
        }

        /// <summary>
        /// 批量转换多个文件 - 与Client项目一致的实现
        /// </summary>
        public async Task<ApiResponseDto<BatchConversionResponse>> StartBatchConversionAsync(
            List<string> filePaths,
            StartConversionRequestDto request,
            IProgress<BatchUploadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
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

                // // 打印所有文件路径
                // for (int i = 0; i < filePaths.Count; i++)
                // {
                //     Utils.Logger.Info("ApiService", $"文件 {i + 1}: {filePaths[i]}");
                // }


                foreach (var filePath in filePaths)
                {
                    try
                    {
                        Utils.Logger.Info("ApiService", $"🔄 开始处理文件: {Path.GetFileName(filePath)} ({completedFiles + 1}/{totalFiles})");
                        // 检查磁盘空间
                        if (!await CheckDiskSpaceBeforeProcessingAsync(filePath))
                        {
                            Utils.Logger.Info("ApiService", $"❌ 磁盘空间不足，暂停处理文件: {Path.GetFileName(filePath)}");

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

                            break; // 暂停处理后续文件
                        }

                        var fileProgress = new Progress<UploadProgress>(p =>
                        {
                            // 验证并修正进度值
                            var safeFileProgress = Math.Max(0, Math.Min(100, p.Percentage));

                            // 计算正确的总体进度
                            var overallProgress = Math.Min(100.0, (completedFiles * 100.0 + safeFileProgress) / totalFiles);

                            // 向UI报告进度
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

                        // 🔑 智能格式处理
                        var processedRequest = ProcessSmartFormatOptions(request, filePath);

                        var result = await StartConversionAsync(filePath, processedRequest, fileProgress, cancellationToken);

                        if (!result.Success)
                        {
                            Utils.Logger.Info("ApiService", $"失败原因: {result.Message}");
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
                            CurrentFile = completedFiles < totalFiles ? "" : Path.GetFileName(filePath),
                            CurrentFileProgress = 100,
                            CompletedFiles = completedFiles,
                            TotalFiles = totalFiles,
                            OverallProgress = finalOverallProgress
                        });

                    }
                    catch (Exception ex)
                    {
                        Utils.Logger.Error("ApiService", $"💥 文件处理异常: {Path.GetFileName(filePath)} - {ex.Message}");

                        results.Add(new ConversionTaskResult
                        {
                            FilePath = filePath,
                            Success = false,
                            Message = ex.Message
                        });

                        completedFiles++;
                    }
                }


                var successCount = results.Count(r => r.Success);

                var batchResponse = new BatchConversionResponse
                {
                    BatchId = batchId,
                    TotalFiles = totalFiles,
                    SuccessCount = successCount,
                    Results = results
                };

                Utils.Logger.Info("ApiService", "🎉 批量转换完成");
                return ApiResponseDto<BatchConversionResponse>.CreateSuccess(batchResponse, "批量转换完成");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ApiService", $"❌ 批量转换失败: {ex.Message}");
                return ApiResponseDto<BatchConversionResponse>.CreateError($"批量转换失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 单文件转换 - 智能上传策略
        /// </summary>
        public async Task<ApiResponseDto<StartConversionResponse>> StartConversionAsync(
            string filePath,
            StartConversionRequestDto request,
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
                    Utils.Logger.Error("ApiService", $"❌ 文件不存在: {filePath}");
                    return ApiResponseDto<StartConversionResponse>.CreateError($"文件不存在: {filePath}");
                }

                var fileInfo = new FileInfo(filePath);
                Utils.Logger.Info("ApiService", $"📁 文件信息: 大小={fileInfo.Length} bytes ({fileInfo.Length / 1024.0 / 1024.0:F2} MB)");

                // 选择上传策略 - 100MB阈值
                bool useChunkedUpload = fileInfo.Length > 100 * 1024 * 1024; // 100MB阈值

                // 🔑 使用并发控制执行上传
                var fileName = Path.GetFileName(filePath);
                var taskId = Guid.NewGuid().ToString();

                if (useChunkedUpload)
                {
                    Utils.Logger.Info("ApiService", "🚀 开始分片上传（并发控制）");
                    return await _concurrencyManager.ExecuteUploadAsync(taskId, async () =>
                    {
                        return await StartChunkedUploadAsync(filePath, request, progress, cancellationToken);
                    });
                }
                else
                {
                    Utils.Logger.Info("ApiService", "🚀 开始统一上传（并发控制）");
                    return await _concurrencyManager.ExecuteUploadAsync(taskId, async () =>
                    {
                        return await StartUnifiedFileConversionAsync(filePath, request, progress, cancellationToken);
                    });
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ApiService", $"💥 上传过程中发生异常: {ex.Message}");
                return ApiResponseDto<StartConversionResponse>.CreateError($"转换失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查磁盘空间
        /// </summary>
        private async Task<bool> CheckDiskSpaceBeforeProcessingAsync(string filePath)
        {
            try
            {
                // 模拟磁盘空间检查
                await Task.Delay(100);
                
                var fileInfo = new FileInfo(filePath);
                var drive = new DriveInfo(Path.GetPathRoot(filePath) ?? "C:");
                
                // 需要至少2倍文件大小的空间
                var requiredSpace = fileInfo.Length * 2;
                var availableSpace = drive.AvailableFreeSpace;
                
                Utils.Logger.Info("ApiService", $"💾 磁盘空间检查: 需要={requiredSpace / 1024 / 1024}MB, 可用={availableSpace / 1024 / 1024}MB");
                
                return availableSpace > requiredSpace;
            }
            catch (Exception ex)
            {
                Utils.Logger.Warning("ApiService", $"⚠️ 磁盘空间检查失败: {ex.Message}");
                return true; // 检查失败时允许继续
            }
        }

        /// <summary>
        /// 分片上传实现 - 使用Client项目的真实ChunkedUploadService
        /// </summary>
        private async Task<ApiResponseDto<StartConversionResponse>> StartChunkedUploadAsync(
            string filePath,
            StartConversionRequestDto request,
            IProgress<UploadProgress>? progress,
            CancellationToken cancellationToken)
        {
            Utils.Logger.Info("ApiService", "🧩 === 开始分片上传 ===");
            Utils.Logger.Info("ApiService", $"文件: {Path.GetFileName(filePath)}");

            try
            {
                // 🔑 创建进度适配器，将分片上传进度转换为通用上传进度 - 与Client项目一致
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

                    // 🔑 始终向UI报告进度，确保UI能及时更新
                    var uploadProgress = new UploadProgress
                    {
                        BytesUploaded = p.UploadedBytes,
                        TotalBytes = p.TotalBytes,
                        Speed = p.Speed,
                        EstimatedTimeRemaining = p.EstimatedTimeRemaining,
                        Percentage = p.Percentage
                    };

                    // 验证数据完整性
                    if (uploadProgress.BytesUploaded < 0 || uploadProgress.TotalBytes <= 0 || uploadProgress.BytesUploaded > uploadProgress.TotalBytes)
                    {
                        Utils.Logger.Info("ApiService", $"⚠️ 检测到异常上传数据: BytesUploaded={uploadProgress.BytesUploaded}, TotalBytes={uploadProgress.TotalBytes}, Percentage={uploadProgress.Percentage:F1}%");
                        Utils.Logger.Info("ApiService", $"   原始ChunkedUploadProgress: UploadedBytes={p.UploadedBytes}, TotalBytes={p.TotalBytes}, Percentage={p.Percentage:F1}%");
                    }

                    Utils.Logger.Debug("ApiService", $"🔄 转发上传进度: {Path.GetFileName(filePath)} = {uploadProgress.Percentage:F1}% ({uploadProgress.BytesUploaded}/{uploadProgress.TotalBytes})");
                    progress.Report(uploadProgress);
                }) : null;

                Utils.Logger.Info("ApiService", "🚀 调用分片上传服务");
                var result = await _chunkedUploadService.UploadFileAsync(filePath, request, chunkedProgress, cancellationToken);

                Utils.Logger.Info("ApiService", $"📥 分片上传服务返回结果: Success={result.Success}");
                if (!result.Success)
                {
                    Utils.Logger.Info("ApiService", $"失败原因: {result.Message}");
                }

                return result;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ApiService", $"💥 分片上传异常: {ex.Message}");
                return ApiResponseDto<StartConversionResponse>.CreateError($"分片上传失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 统一上传实现 - 使用Client项目的真实API调用
        /// </summary>
        private async Task<ApiResponseDto<StartConversionResponse>> StartUnifiedFileConversionAsync(
            string filePath,
            StartConversionRequestDto request,
            IProgress<UploadProgress>? progress,
            CancellationToken cancellationToken)
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

                    // 🔑 使用流式上传，支持进度报告和大文件 - 与Client项目一致
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

                    // 🔑 添加转换参数 - 与Client项目一致
                    Utils.Logger.Info("ApiService", "🎯 添加转换参数");
                    AddConversionParameters(form, request);

                    // 添加重试信息
                    form.Add(new StringContent(attempt.ToString()), "RetryAttempt");
                    form.Add(new StringContent(maxRetries.ToString()), "MaxRetries");
                    Utils.Logger.Info("ApiService", $"📊 重试信息已添加: {attempt}/{maxRetries}");

                    // 🔑 统一使用upload/unified接口 - 与Client项目一致
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
                        Utils.Logger.Warning("ApiService", $"⚠️ 第 {attempt} 次上传失败: {result.Message}");

                        if (attempt == maxRetries)
                        {
                            Utils.Logger.Error("ApiService", $"❌ 统一上传最终失败，已重试 {maxRetries} 次");
                            return result;
                        }

                        // 计算重试延迟
                        var delay = baseDelayMs * attempt;
                        Utils.Logger.Info("ApiService", $"⏳ 等待 {delay}ms 后重试");
                        await Task.Delay(delay, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    Utils.Logger.Info("ApiService", "⏹️ 统一上传被取消");
                    return ApiResponseDto<StartConversionResponse>.CreateError("上传被取消");
                }
                catch (Exception ex)
                {
                    Utils.Logger.Error("ApiService", $"💥 第 {attempt} 次上传异常: {ex.Message}");

                    if (attempt == maxRetries)
                    {
                        Utils.Logger.Error("ApiService", $"❌ 统一上传最终失败: {ex.Message}");
                        return ApiResponseDto<StartConversionResponse>.CreateError($"统一上传失败: {ex.Message}");
                    }

                    var delay = baseDelayMs * attempt;
                    Utils.Logger.Info("ApiService", $"⏳ 异常后等待 {delay}ms 重试");
                    await Task.Delay(delay, cancellationToken);
                }
                finally
                {
                    fileStream?.Dispose();
                }
            }

            return ApiResponseDto<StartConversionResponse>.CreateError("统一上传失败，已达到最大重试次数");
        }

        #region Client项目的真实API辅助方法

        /// <summary>
        /// 添加转换参数到表单 - 与Client项目一致
        /// </summary>
        private void AddConversionParameters(MultipartFormDataContent form, StartConversionRequestDto request)
        {
            form.Add(new StringContent(request.TaskName ?? ""), "TaskName");
            form.Add(new StringContent(request.OutputFormat ?? ""), "OutputFormat");
            form.Add(new StringContent(request.Resolution ?? ""), "Resolution");
            form.Add(new StringContent(request.VideoCodec ?? ""), "VideoCodec");
            form.Add(new StringContent(request.AudioCodec ?? ""), "AudioCodec");
            form.Add(new StringContent(request.VideoQuality ?? ""), "VideoQuality");
            form.Add(new StringContent(request.AudioBitrate ?? ""), "AudioBitrate");
            form.Add(new StringContent(request.EncodingPreset ?? ""), "EncodingPreset");
            form.Add(new StringContent(request.HardwareAcceleration ?? ""), "HardwareAcceleration");
            form.Add(new StringContent(request.FastStart.ToString()), "FastStart");
            form.Add(new StringContent(request.TwoPass.ToString()), "TwoPass");

            Utils.Logger.Info("ApiService", "✅ 转换参数已添加到表单");
        }

        /// <summary>
        /// 处理转换响应 - 与Client项目一致
        /// </summary>
        private async Task<ApiResponseDto<StartConversionResponse>> ProcessConversionResponse(HttpResponseMessage response)
        {
            try
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                Utils.Logger.Info("ApiService", $"📄 响应内容: {responseContent}");

                if (response.IsSuccessStatusCode)
                {
                    var apiResponse = JsonSerializer.Deserialize<ApiResponseDto<StartConversionResponse>>(responseContent, _jsonOptions);

                    if (apiResponse?.Success == true && apiResponse.Data != null)
                    {
                        Utils.Logger.Info("ApiService", $"✅ 转换启动成功，TaskId: {apiResponse.Data.TaskId}");
                        return apiResponse;
                    }
                    else
                    {
                        var errorMessage = apiResponse?.Message ?? "未知错误";
                        Utils.Logger.Error("ApiService", $"❌ API返回失败: {errorMessage}");
                        return ApiResponseDto<StartConversionResponse>.CreateError(errorMessage);
                    }
                }
                else
                {
                    Utils.Logger.Error("ApiService", $"❌ HTTP请求失败: {response.StatusCode} - {response.ReasonPhrase}");
                    return ApiResponseDto<StartConversionResponse>.CreateError($"HTTP错误: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ApiService", $"❌ 处理响应异常: {ex.Message}");
                return ApiResponseDto<StartConversionResponse>.CreateError($"响应处理失败: {ex.Message}");
            }
        }

        #endregion

        #region 智能格式处理

        /// <summary>
        /// 智能格式处理 - 与Client项目一致的智能选项解析
        /// </summary>
        private StartConversionRequestDto ProcessSmartFormatOptions(StartConversionRequestDto request, string filePath)
        {
            try
            {
                Utils.Logger.Info("ApiService", "🧠 开始智能格式处理");

                var processedRequest = new StartConversionRequestDto
                {
                    TaskName = request.TaskName,
                    Preset = request.Preset,
                    OutputFormat = request.OutputFormat,
                    Resolution = request.Resolution,
                    VideoCodec = request.VideoCodec,
                    AudioCodec = request.AudioCodec,
                    VideoQuality = request.VideoQuality,
                    AudioBitrate = request.AudioBitrate,
                    EncodingPreset = request.EncodingPreset,
                    HardwareAcceleration = request.HardwareAcceleration,
                    FastStart = request.FastStart,
                    TwoPass = request.TwoPass
                };

                var fileExtension = Path.GetExtension(filePath).ToLower();
                Utils.Logger.Info("ApiService", $"📁 源文件格式: {fileExtension}");

                // 🔧 智能输出格式选择 - 使用ConversionOptions处理
                if (request.OutputFormat == "智能选择" || string.IsNullOrEmpty(request.OutputFormat) ||
                    request.OutputFormat == "keep_original" || request.OutputFormat == "auto_best")
                {
                    processedRequest.OutputFormat = ConversionOptions.ResolveSmartFormat(request.OutputFormat ?? "auto_best", filePath);
                    Utils.Logger.Info("ApiService", $"🎯 智能选择输出格式: {processedRequest.OutputFormat}");
                }

                // 智能分辨率选择
                if (request.Resolution == "智能选择" || string.IsNullOrEmpty(request.Resolution))
                {
                    processedRequest.Resolution = DetermineOptimalResolution(filePath);
                    Utils.Logger.Info("ApiService", $"🎯 智能选择分辨率: {processedRequest.Resolution}");
                }

                // 智能编码器选择
                if (request.VideoCodec == "智能选择" || string.IsNullOrEmpty(request.VideoCodec))
                {
                    processedRequest.VideoCodec = DetermineOptimalVideoCodec(processedRequest.OutputFormat);
                    Utils.Logger.Info("ApiService", $"🎯 智能选择视频编码器: {processedRequest.VideoCodec}");
                }

                // 智能音频编码器选择
                if (request.AudioCodec == "智能选择" || string.IsNullOrEmpty(request.AudioCodec))
                {
                    processedRequest.AudioCodec = DetermineOptimalAudioCodec(processedRequest.OutputFormat);
                    Utils.Logger.Info("ApiService", $"🎯 智能选择音频编码器: {processedRequest.AudioCodec}");
                }

                // 智能质量选择
                if (request.VideoQuality == "智能选择" || string.IsNullOrEmpty(request.VideoQuality))
                {
                    processedRequest.VideoQuality = DetermineOptimalVideoQuality(processedRequest.Resolution);
                    Utils.Logger.Info("ApiService", $"🎯 智能选择视频质量: {processedRequest.VideoQuality}");
                }

                Utils.Logger.Info("ApiService", "✅ 智能格式处理完成");
                return processedRequest;
            }
            catch (Exception ex)
            {
                Utils.Logger.Warning("ApiService", $"⚠️ 智能格式处理失败，使用原始设置: {ex.Message}");
                return request;
            }
        }

        /// <summary>
        /// 确定最佳输出格式
        /// </summary>
        private string DetermineOptimalOutputFormat(string sourceExtension)
        {
            return sourceExtension switch
            {
                ".avi" or ".wmv" or ".flv" or ".mov" => "mp4", // 老格式转换为MP4
                ".mkv" => "mp4", // MKV转换为更兼容的MP4
                ".webm" => "mp4", // WebM转换为MP4以获得更好的兼容性
                ".mp4" => "mp4", // MP4保持MP4
                ".m4v" => "mp4", // M4V转换为MP4
                _ => "mp4" // 默认使用MP4
            };
        }

        /// <summary>
        /// 确定最佳分辨率
        /// </summary>
        private string DetermineOptimalResolution(string filePath)
        {
            // 在实际实现中，这里会分析视频文件获取原始分辨率
            // 然后根据原始分辨率智能选择目标分辨率

            // 模拟智能分辨率选择逻辑
            return "1920x1080"; // 默认1080p
        }

        /// <summary>
        /// 确定最佳视频编码器
        /// </summary>
        private string DetermineOptimalVideoCodec(string outputFormat)
        {
            return outputFormat.ToLower() switch
            {
                "mp4" => "libx264", // MP4使用H.264
                "webm" => "libvpx-vp9", // WebM使用VP9
                "mkv" => "libx264", // MKV使用H.264
                _ => "libx264" // 默认H.264
            };
        }

        /// <summary>
        /// 确定最佳音频编码器
        /// </summary>
        private string DetermineOptimalAudioCodec(string outputFormat)
        {
            return outputFormat.ToLower() switch
            {
                "mp4" => "aac", // MP4使用AAC
                "webm" => "libopus", // WebM使用Opus
                "mkv" => "aac", // MKV使用AAC
                _ => "aac" // 默认AAC
            };
        }

        /// <summary>
        /// 确定最佳视频质量
        /// </summary>
        private string DetermineOptimalVideoQuality(string resolution)
        {
            return resolution switch
            {
                "3840x2160" => "18", // 4K使用CRF 18
                "1920x1080" => "23", // 1080p使用CRF 23
                "1280x720" => "25", // 720p使用CRF 25
                "854x480" => "28", // 480p使用CRF 28
                _ => "23" // 默认CRF 23
            };
        }

        #endregion

        #region 系统管理API - 与Client项目一致

        /// <summary>
        /// 获取系统状态信息
        /// </summary>
        public async Task<ApiResponseDto<SystemStatusDto>> GetSystemStatusAsync()
        {
            try
            {
                Utils.Logger.Info("ApiService", "🔍 获取系统状态信息");
                var url = $"{BaseUrl}/api/health/status";
                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();

                    // 解析服务器返回的嵌套格式
                    using var document = JsonDocument.Parse(content);
                    var root = document.RootElement;

                    var systemStatusDto = new SystemStatusDto
                    {
                        ServerVersion = GetStringValue(root, "version") ?? "未知",
                        FfmpegVersion = "6.0", // 服务器暂未提供，使用默认值
                        HardwareAcceleration = "未知", // 服务器暂未提供
                        Uptime = GetStringValue(root, "system.uptime") ?? "未知",
                        AvailableDiskSpace = GetLongValue(root, "system.diskSpace.freeBytes") ?? 0,
                        TotalDiskSpace = GetLongValue(root, "system.diskSpace.totalBytes") ?? 0,
                        ActiveTasks = GetIntValue(root, "tasks.converting") ?? 0,
                        QueuedTasks = GetIntValue(root, "tasks.pending") ?? 0,
                        CpuUsage = 0, // 服务器暂未提供
                        MemoryUsage = GetDoubleValue(root, "system.memoryUsage.mb") ?? 0
                    };

                    var result = ApiResponseDto<SystemStatusDto>.CreateSuccess(systemStatusDto, "获取系统状态成功");
                    Utils.Logger.Info("ApiService", "✅ 系统状态获取成功");
                    return result;
                }
                else
                {
                    Utils.Logger.Warning("ApiService", $"⚠️ 系统状态HTTP请求失败: {response.StatusCode}");
                    return ApiResponseDto<SystemStatusDto>.CreateError($"HTTP错误: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ApiService", $"❌ 获取系统状态异常: {ex.Message}");
                return ApiResponseDto<SystemStatusDto>.CreateError($"获取系统状态失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取系统诊断信息
        /// </summary>
        public async Task<ApiResponseDto<List<SystemDiagnosticDto>>> GetSystemDiagnosticsAsync()
        {
            try
            {
                Utils.Logger.Info("ApiService", "🔍 获取系统诊断信息");
                var url = $"{BaseUrl}/api/health/diagnostics";
                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();

                    // 解析服务器返回的诊断格式
                    using var document = JsonDocument.Parse(content);
                    var root = document.RootElement;

                    var diagnostics = new List<SystemDiagnosticDto>();

                    if (root.TryGetProperty("diagnostics", out var diagnosticsArray) && diagnosticsArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in diagnosticsArray.EnumerateArray())
                        {
                            var diagnostic = new SystemDiagnosticDto
                            {
                                Category = item.TryGetProperty("category", out var catProp) ? catProp.GetString() ?? "系统" : "系统",
                                Level = item.TryGetProperty("status", out var statusProp) ? MapStatusToLevel(statusProp.GetString()) : "Info",
                                Message = item.TryGetProperty("message", out var msgProp) ? msgProp.GetString() ?? "" : "",
                                Timestamp = DateTime.Now,
                                Details = item.TryGetProperty("details", out var detailsProp) ? detailsProp.GetString() : null
                            };
                            diagnostics.Add(diagnostic);
                        }
                    }

                    var result = ApiResponseDto<List<SystemDiagnosticDto>>.CreateSuccess(diagnostics, "获取系统诊断成功");
                    Utils.Logger.Info("ApiService", $"✅ 系统诊断信息获取成功，共 {diagnostics.Count} 条");
                    return result;
                }
                else
                {
                    Utils.Logger.Warning("ApiService", $"⚠️ 系统诊断HTTP请求失败: {response.StatusCode}");
                    return ApiResponseDto<List<SystemDiagnosticDto>>.CreateError($"HTTP错误: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ApiService", $"❌ 获取系统诊断异常: {ex.Message}");
                return ApiResponseDto<List<SystemDiagnosticDto>>.CreateError($"获取系统诊断失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取磁盘空间配置
        /// </summary>
        public async Task<ApiResponseDto<DiskSpaceConfigDto>> GetDiskSpaceConfigAsync()
        {
            try
            {
                Utils.Logger.Info("ApiService", "🔍 获取磁盘空间配置");
                var url = $"{BaseUrl}/api/diskspace/config";
                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();

                    // 解析服务器返回的格式
                    using var document = JsonDocument.Parse(content);
                    var root = document.RootElement;

                    if (root.TryGetProperty("success", out var successProp) && successProp.GetBoolean() &&
                        root.TryGetProperty("data", out var dataProp))
                    {
                        var diskSpaceConfig = new DiskSpaceConfigDto
                        {
                            MinFreeSpace = (long)((dataProp.TryGetProperty("reservedSpaceGB", out var reservedProp) ? reservedProp.GetDouble() : 10.0) * 1024 * 1024 * 1024),
                            AutoCleanup = dataProp.TryGetProperty("isEnabled", out var enabledProp) && enabledProp.GetBoolean(),
                            CleanupIntervalHours = 24, // 默认值，服务器暂未提供
                            MaxFileAgeHours = 168, // 默认值，服务器暂未提供
                            CleanupPath = "" // 默认值，服务器暂未提供
                        };

                        var result = ApiResponseDto<DiskSpaceConfigDto>.CreateSuccess(diskSpaceConfig, "获取磁盘配置成功");
                        Utils.Logger.Info("ApiService", "✅ 磁盘空间配置获取成功");
                        return result;
                    }
                    else
                    {
                        Utils.Logger.Warning("ApiService", "⚠️ 磁盘配置API返回格式错误");
                        return ApiResponseDto<DiskSpaceConfigDto>.CreateError("磁盘配置API返回格式错误");
                    }
                }
                else
                {
                    Utils.Logger.Warning("ApiService", $"⚠️ 磁盘配置HTTP请求失败: {response.StatusCode}");
                    return ApiResponseDto<DiskSpaceConfigDto>.CreateError($"HTTP错误: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ApiService", $"❌ 获取磁盘配置异常: {ex.Message}");
                return ApiResponseDto<DiskSpaceConfigDto>.CreateError($"获取磁盘配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 配置磁盘空间设置
        /// </summary>
        public async Task<ApiResponseDto<bool>> ConfigureDiskSpaceAsync(DiskSpaceConfigDto config)
        {
            try
            {
                Utils.Logger.Info("ApiService", "⚙️ 配置磁盘空间设置");
                var url = $"{BaseUrl}/api/diskspace/config";

                // 转换为服务器期望的格式
                var serverRequest = new
                {
                    maxTotalSpaceGB = config.MinFreeSpace / (1024.0 * 1024.0 * 1024.0) + 100.0, // 假设总空间比最小空闲空间大100GB
                    reservedSpaceGB = config.MinFreeSpace / (1024.0 * 1024.0 * 1024.0),
                    isEnabled = config.AutoCleanup
                };

                var json = JsonSerializer.Serialize(serverRequest, _jsonOptions);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<ApiResponseDto<bool>>(responseContent, _jsonOptions);

                    if (result?.Success == true)
                    {
                        Utils.Logger.Info("ApiService", "✅ 磁盘空间配置成功");
                        return result;
                    }
                    else
                    {
                        Utils.Logger.Warning("ApiService", $"⚠️ 磁盘配置API返回失败: {result?.Message}");
                        return ApiResponseDto<bool>.CreateError(result?.Message ?? "配置磁盘空间失败");
                    }
                }
                else
                {
                    Utils.Logger.Warning("ApiService", $"⚠️ 磁盘配置HTTP请求失败: {response.StatusCode}");
                    return ApiResponseDto<bool>.CreateError($"HTTP错误: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ApiService", $"❌ 配置磁盘空间异常: {ex.Message}");
                return ApiResponseDto<bool>.CreateError($"配置磁盘空间失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 设置磁盘空间配置（与Client项目一致的新方法）
        /// </summary>
        public async Task<ApiResponseDto<bool>> SetSpaceConfigAsync(double maxTotalSpaceGB, double reservedSpaceGB, bool isEnabled = true)
        {
            try
            {
                Utils.Logger.Info("ApiService", $"⚙️ 设置磁盘空间配置: {maxTotalSpaceGB}GB/{reservedSpaceGB}GB, 启用: {isEnabled}");
                var url = $"{BaseUrl}/api/diskspace/config";

                // 🔧 使用与Client项目和服务端一致的格式
                var serverRequest = new
                {
                    maxTotalSpaceGB = maxTotalSpaceGB,
                    reservedSpaceGB = reservedSpaceGB,
                    isEnabled = isEnabled
                };

                var json = JsonSerializer.Serialize(serverRequest, _jsonOptions);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();

                    // 解析服务器返回的格式
                    using var document = JsonDocument.Parse(responseContent);
                    var root = document.RootElement;

                    if (root.TryGetProperty("success", out var successProp) && successProp.GetBoolean())
                    {
                        Utils.Logger.Info("ApiService", "✅ 磁盘空间配置设置成功");
                        return ApiResponseDto<bool>.CreateSuccess(true, "配置更新成功");
                    }
                    else
                    {
                        var message = root.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "配置失败";
                        Utils.Logger.Warning("ApiService", $"⚠️ 磁盘配置API返回失败: {message}");
                        return ApiResponseDto<bool>.CreateError(message ?? "配置磁盘空间失败");
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Utils.Logger.Warning("ApiService", $"⚠️ 磁盘配置HTTP请求失败: {response.StatusCode}, {errorContent}");
                    return ApiResponseDto<bool>.CreateError($"HTTP {response.StatusCode}: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ApiService", $"❌ 设置磁盘空间配置异常: {ex.Message}");
                return ApiResponseDto<bool>.CreateError($"设置磁盘空间配置异常: {ex.Message}");
            }
        }

        #endregion

        #region JSON解析辅助方法

        /// <summary>
        /// 从JSON元素中获取字符串值
        /// </summary>
        private static string? GetStringValue(JsonElement element, string path)
        {
            try
            {
                var parts = path.Split('.');
                var current = element;

                foreach (var part in parts)
                {
                    if (current.TryGetProperty(part, out var property))
                    {
                        current = property;
                    }
                    else
                    {
                        return null;
                    }
                }

                return current.GetString();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 从JSON元素中获取长整型值
        /// </summary>
        private static long? GetLongValue(JsonElement element, string path)
        {
            try
            {
                var parts = path.Split('.');
                var current = element;

                foreach (var part in parts)
                {
                    if (current.TryGetProperty(part, out var property))
                    {
                        current = property;
                    }
                    else
                    {
                        return null;
                    }
                }

                return current.GetInt64();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 从JSON元素中获取整型值
        /// </summary>
        private static int? GetIntValue(JsonElement element, string path)
        {
            try
            {
                var parts = path.Split('.');
                var current = element;

                foreach (var part in parts)
                {
                    if (current.TryGetProperty(part, out var property))
                    {
                        current = property;
                    }
                    else
                    {
                        return null;
                    }
                }

                return current.GetInt32();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 从JSON元素中获取双精度值
        /// </summary>
        private static double? GetDoubleValue(JsonElement element, string path)
        {
            try
            {
                var parts = path.Split('.');
                var current = element;

                foreach (var part in parts)
                {
                    if (current.TryGetProperty(part, out var property))
                    {
                        current = property;
                    }
                    else
                    {
                        return null;
                    }
                }

                return current.GetDouble();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 将服务器状态映射为诊断级别
        /// </summary>
        private static string MapStatusToLevel(string? status)
        {
            return status switch
            {
                "success" => "Info",
                "warning" => "Warning",
                "error" => "Error",
                "info" => "Info",
                _ => "Info"
            };
        }

        #endregion

        public void Dispose()
        {
            if (!_disposed)
            {
                // 取消监听设置变化
                SystemSettingsService.Instance.SettingsChanged -= OnSettingsChanged;

                _httpClient?.Dispose();
                _chunkedUploadService?.Dispose();
                _disposed = true;
            }
        }

        /// <summary>
        /// 处理系统设置变化
        /// </summary>
        private void OnSettingsChanged(object? sender, SystemSettingsChangedEventArgs e)
        {
            if (e.ServerAddressChanged)
            {
                try
                {
                    // 更新BaseUrl
                    BaseUrl = e.NewSettings.ServerAddress;

                    // 重新创建ChunkedUploadService以使用新的BaseUrl
                    var oldChunkedUploadService = _chunkedUploadService;
                    _chunkedUploadService = new ChunkedUploadService(BaseUrl);

                    // 释放旧的服务
                    oldChunkedUploadService?.Dispose();

                    Utils.Logger.Info("ApiService", $"🔧 服务器地址已更新: {BaseUrl}");
                }
                catch (Exception ex)
                {
                    Utils.Logger.Error("ApiService", $"❌ 更新服务器地址失败: {ex.Message}");
                }
            }
        }
        #region 文件清理API

        /// <summary>
        /// 手动触发文件清理 - 与Client项目完全一致
        /// </summary>
        public async Task<ApiResponseDto<CleanupResult>> TriggerManualCleanupAsync(ManualCleanupRequest request)
        {
            try
            {
                Utils.Logger.Info("ApiService", $"🧹 开始手动清理: {request}");

                var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{BaseUrl}/api/cleanup/manual", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    using var document = JsonDocument.Parse(responseContent);
                    var root = document.RootElement;

                    if (root.TryGetProperty("success", out var successProp) && successProp.GetBoolean() &&
                        root.TryGetProperty("data", out var dataProp))
                    {
                        var result = new CleanupResult
                        {
                            TotalCleanedFiles = dataProp.TryGetProperty("totalCleanedFiles", out var filesProp) ? filesProp.GetInt32() : 0,
                            TotalCleanedSize = dataProp.TryGetProperty("totalCleanedSize", out var sizeProp) ? sizeProp.GetInt64() : 0,
                            EndTime = DateTime.Now
                        };

                        Utils.Logger.Info("ApiService", $"✅ 手动清理完成: {result}");
                        return ApiResponseDto<CleanupResult>.CreateSuccess(result, "清理完成");
                    }
                }

                Utils.Logger.Warning("ApiService", $"⚠️ 手动清理失败: {response.StatusCode}");
                return ApiResponseDto<CleanupResult>.CreateError($"清理失败: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ApiService", $"❌ 手动清理异常: {ex.Message}");
                return ApiResponseDto<CleanupResult>.CreateError($"清理异常: {ex.Message}");
            }
        }

        #endregion
    }

    #region 分片上传相关DTO

    /// <summary>
    /// 分片信息
    /// </summary>
    public class ChunkInfo
    {
        public int Index { get; set; }
        public int Size { get; set; }
        public string ETag { get; set; } = "";
    }

    /// <summary>
    /// 初始化上传响应
    /// </summary>
    public class InitUploadResponse
    {
        public string UploadId { get; set; } = "";
        public string SessionToken { get; set; } = "";
    }

    /// <summary>
    /// 分片上传响应
    /// </summary>
    public class ChunkUploadResponse
    {
        public string ETag { get; set; } = "";
        public int ChunkIndex { get; set; }
    }

    #endregion
}
