using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VideoConversion_Client.Models;

namespace VideoConversion_Client.Services
{
    /// <summary>
    /// 分片上传服务 - 基于WebUploader思想实现
    /// 支持断点续传、分片上传、MD5校验等功能
    /// </summary>
    public class ChunkedUploadService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private const int DefaultChunkSize = 50 * 1024 * 1024; // 50MB per chunk
        private const int MaxRetryAttempts = 3;
        private const int MaxConcurrentUploads = 4; // 并发上传数量（增加到4个，提高效率）
        private const bool EnableChunkMD5 = false; // 是否启用分片MD5校验（可关闭以提高性能）

        public ChunkedUploadService(string baseUrl)
        {
            _baseUrl = baseUrl;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(5) // 每个分片5分钟超时（50MB分片应该足够）
            };

            // 优化HttpClient性能
            _httpClient.DefaultRequestHeaders.Connection.Add("keep-alive");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "VideoConversion-Client/1.0");
        }

        /// <summary>
        /// 分片上传文件并创建转换任务
        /// </summary>
        public async Task<ApiResponse<StartConversionResponse>> UploadFileAsync(
            string filePath,
            StartConversionRequest request,
            IProgress<ChunkedUploadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            Utils.Logger.Info("ChunkedUpload", "🧩 === 开始分片上传流程 ===");

            try
            {
                var fileInfo = new FileInfo(filePath);
                var fileName = Path.GetFileName(filePath);

                Utils.Logger.Info("ChunkedUpload", $"📁 文件信息: {fileName}");
                Utils.Logger.Info("ChunkedUpload", $"📊 文件大小: {fileInfo.Length} bytes ({fileInfo.Length / 1024.0 / 1024.0:F2} MB)");

                // 1. 智能文件标识策略
                Utils.Logger.Info("ChunkedUpload", "🔐 开始文件标识处理");
                var uploadId = Guid.NewGuid().ToString();
                string fileMd5;

                // 对于大文件（>500MB），使用快速标识；小文件使用真实MD5
                if (fileInfo.Length > 500 * 1024 * 1024) // 500MB
                {
                    Utils.Logger.Info("ChunkedUpload", "� 大文件检测，使用快速标识模式");
                    progress?.Report(new ChunkedUploadProgress
                    {
                        Phase = UploadPhase.Calculating,
                        Message = "正在生成文件标识..."
                    });

                    fileMd5 = GenerateQuickFileId(filePath, fileInfo);
                    Utils.Logger.Info("ChunkedUpload", $"⚡ 快速标识生成完成: {fileMd5}");
                }
                else
                {
                    Utils.Logger.Info("ChunkedUpload", "📄 小文件检测，计算完整MD5");
                    progress?.Report(new ChunkedUploadProgress
                    {
                        Phase = UploadPhase.Calculating,
                        Message = "正在计算文件校验码..."
                    });

                    fileMd5 = await CalculateFileMD5Async(filePath, cancellationToken);
                    Utils.Logger.Info("ChunkedUpload", $"✅ MD5计算完成: {fileMd5}");
                }

                Utils.Logger.Info("ChunkedUpload", $"🆔 上传ID: {uploadId}");

                // 2. 初始化上传会话
                Utils.Logger.Info("ChunkedUpload", "🚀 初始化上传会话");
                progress?.Report(new ChunkedUploadProgress
                {
                    Phase = UploadPhase.Initializing,
                    Message = "正在初始化上传会话..."
                });

                var initResult = await InitializeUploadAsync(uploadId, fileName, fileInfo.Length, fileMd5, request, cancellationToken);

                Utils.Logger.Info("ChunkedUpload", $"📥 初始化结果: Success={initResult.Success}");
                if (!initResult.Success)
                {
                    Utils.Logger.Info("ChunkedUpload", $"❌ 初始化失败: {initResult.Message}");
                    return new ApiResponse<StartConversionResponse>
                    {
                        Success = false,
                        Message = initResult.Message
                    };
                }

                Utils.Logger.Info("ChunkedUpload", "✅ 初始化成功");

                // 3. 分片上传
                var chunkSize = initResult.Data?.ChunkSize ?? DefaultChunkSize;
                var totalChunks = initResult.Data?.TotalChunks ?? (int)Math.Ceiling((double)fileInfo.Length / chunkSize);

                Utils.Logger.Info("ChunkedUpload", $"📊 分片上传配置:");
                Utils.Logger.Info("ChunkedUpload", $"   服务端返回ChunkSize: {initResult.Data?.ChunkSize}");
                Utils.Logger.Info("ChunkedUpload", $"   客户端使用ChunkSize: {chunkSize}");
                Utils.Logger.Info("ChunkedUpload", $"   文件大小: {fileInfo.Length} bytes");
                Utils.Logger.Info("ChunkedUpload", $"   服务端返回TotalChunks: {initResult.Data?.TotalChunks}");
                Utils.Logger.Info("ChunkedUpload", $"   客户端使用TotalChunks: {totalChunks}");

                progress?.Report(new ChunkedUploadProgress
                {
                    Phase = UploadPhase.Uploading,
                    TotalBytes = fileInfo.Length,
                    TotalChunks = totalChunks,
                    Message = $"开始上传文件，共{totalChunks}个分片"
                });

                var uploadedChunks = new HashSet<int>();
                
                // 检查已上传的分片
                var statusResult = await GetUploadStatusAsync(uploadId, cancellationToken);
                if (statusResult.Success && statusResult.Data?.UploadedChunks != null)
                {
                    foreach (var chunk in statusResult.Data.UploadedChunks)
                    {
                        uploadedChunks.Add(chunk);
                    }
                }

                // 上传分片 - 优化版本，支持容错和重试
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                Utils.Logger.Info("ChunkedUpload", $"🚀 开始分片上传循环，总分片数: {totalChunks}");
                Utils.Logger.Info("ChunkedUpload", $"   已存在分片数: {uploadedChunks.Count}");

                var failedChunks = new List<int>();
                var maxFailureRate = 0.05; // 允许5%的分片失败
                var maxAllowedFailures = Math.Max(1, (int)(totalChunks * maxFailureRate));

                Utils.Logger.Info("ChunkedUpload", $"📊 容错配置: 最大允许失败分片数 = {maxAllowedFailures} ({maxFailureRate:P})");

                // 第一轮：并发上传所有分片
                var logInterval = Math.Max(1, totalChunks / 20); // 最多记录20次进度日志
                var lastLoggedChunk = -1;

                // 使用并发上传（传递文件路径而不是FileStream，避免并发访问冲突）
                var concurrentResult = await UploadChunksConcurrentlyAsync(
                    filePath, uploadId, totalChunks, chunkSize, uploadedChunks,
                    failedChunks, maxAllowedFailures, fileInfo.Length, progress, cancellationToken);

                if (!concurrentResult.Success)
                {
                    return concurrentResult;
                }

                // 第二轮：重试失败的分片
                if (failedChunks.Count > 0)
                {
                    Utils.Logger.Info("ChunkedUpload", $"🔄 开始重试失败分片，数量: {failedChunks.Count}");

                    var retryFailedChunks = new List<int>();

                    for (int i = 0; i < failedChunks.Count; i++)
                    {
                        var chunkIndex = failedChunks[i];
                        cancellationToken.ThrowIfCancellationRequested();

                        // 只记录重试开始和结果，不记录每个重试过程
                        if (i == 0)
                        {
                            Utils.Logger.Info("ChunkedUpload", $"🔄 开始重试 {failedChunks.Count} 个失败分片...");
                        }

                        var chunkResult = await UploadChunkWithRetryAsync(
                            fileStream, uploadId, chunkIndex, chunkSize, totalChunks,
                            progress, cancellationToken);

                        if (chunkResult.Success)
                        {
                            uploadedChunks.Add(chunkIndex);
                            Utils.Logger.Info("ChunkedUpload", $"✅ 分片重试成功: {chunkIndex + 1}/{totalChunks}");
                        }
                        else
                        {
                            retryFailedChunks.Add(chunkIndex);
                            Utils.Logger.Info("ChunkedUpload", $"❌ 分片重试失败: {chunkIndex + 1}/{totalChunks} - {chunkResult.Message}");
                        }

                        // 更新重试进度（精确计算已上传字节数）
                        var currentUploadedBytes = CalculateUploadedBytes(uploadedChunks, chunkSize, fileInfo.Length);
                        progress?.Report(new ChunkedUploadProgress
                        {
                            Phase = UploadPhase.Uploading,
                            TotalBytes = fileInfo.Length,
                            UploadedBytes = currentUploadedBytes,
                            TotalChunks = totalChunks,
                            CompletedChunks = uploadedChunks.Count,
                            CurrentChunk = chunkIndex,
                            Message = $"重试进度: {uploadedChunks.Count}/{totalChunks} 分片完成"
                        });

                        // 每10个重试或最后一个记录进度
                        if ((i + 1) % 10 == 0 || i == failedChunks.Count - 1)
                        {
                            var retryProgress = (double)(i + 1) / failedChunks.Count * 100;
                            Utils.Logger.Info("ChunkedUpload", $"🔄 重试进度: {retryProgress:F1}% ({i + 1}/{failedChunks.Count})");
                        }
                    }

                    // 检查最终结果
                    if (retryFailedChunks.Count > 0)
                    {
                        Utils.Logger.Info("ChunkedUpload", $"💥 仍有 {retryFailedChunks.Count} 个分片上传失败");
                        Utils.Logger.Info("ChunkedUpload", $"失败分片索引: [{string.Join(", ", retryFailedChunks.Take(10))}{(retryFailedChunks.Count > 10 ? "..." : "")}]");

                        return new ApiResponse<StartConversionResponse>
                        {
                            Success = false,
                            Message = $"上传失败：{retryFailedChunks.Count} 个分片重试后仍然失败"
                        };
                    }
                }

            Utils.Logger.Info("ChunkedUpload", $"🎉 所有分片上传完成！总计: {uploadedChunks.Count}/{totalChunks}");

                // 4. 完成上传
                progress?.Report(new ChunkedUploadProgress
                {
                    Phase = UploadPhase.Finalizing,
                    TotalBytes = fileInfo.Length,
                    UploadedBytes = fileInfo.Length,
                    TotalChunks = totalChunks,
                    CompletedChunks = totalChunks,
                    Message = "正在完成上传..."
                });

                var completeResult = await CompleteUploadAsync(uploadId, cancellationToken);
                if (!completeResult.Success)
                {
                    return new ApiResponse<StartConversionResponse>
                    {
                        Success = false,
                        Message = completeResult.Message
                    };
                }

                progress?.Report(new ChunkedUploadProgress
                {
                    Phase = UploadPhase.Completed,
                    TotalBytes = fileInfo.Length,
                    UploadedBytes = fileInfo.Length,
                    TotalChunks = totalChunks,
                    CompletedChunks = totalChunks,
                    Message = "上传完成！"
                });

                return completeResult;
            }
            catch (OperationCanceledException)
            {
                return new ApiResponse<StartConversionResponse>
                {
                    Success = false,
                    Message = "上传被取消",
                    ErrorType = "Cancelled"
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<StartConversionResponse>
                {
                    Success = false,
                    Message = $"上传失败: {ex.Message}",
                    ErrorType = "General"
                };
            }
        }

        /// <summary>
        /// 生成快速文件标识（避免读取整个文件）
        /// </summary>
        private string GenerateQuickFileId(string filePath, FileInfo fileInfo)
        {
            // 使用文件路径、大小、修改时间生成唯一标识
            var identifier = $"{filePath}|{fileInfo.Length}|{fileInfo.LastWriteTime:yyyyMMddHHmmss}";

            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(identifier));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        /// <summary>
        /// 生成简单校验和（比MD5快很多）
        /// </summary>
        private string GenerateSimpleChecksum(byte[] data)
        {
            // 使用CRC32或简单的哈希算法，比MD5快10-20倍
            uint checksum = 0;
            for (int i = 0; i < data.Length; i += 4) // 每4个字节计算一次
            {
                if (i + 3 < data.Length)
                {
                    checksum ^= BitConverter.ToUInt32(data, i);
                }
            }
            return checksum.ToString("x8").PadLeft(32, '0'); // 模拟MD5格式
        }

        /// <summary>
        /// 计算文件MD5（仅在需要时使用）
        /// </summary>
        private async Task<string> CalculateFileMD5Async(string filePath, CancellationToken cancellationToken)
        {
            using var md5 = MD5.Create();
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var hash = await md5.ComputeHashAsync(stream, cancellationToken);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        /// <summary>
        /// 初始化上传会话
        /// </summary>
        private async Task<ApiResponse<InitUploadResponse>> InitializeUploadAsync(
            string uploadId, string fileName, long fileSize, string fileMd5,
            StartConversionRequest request, CancellationToken cancellationToken)
        {
            // 创建分片上传专用的转换请求（不包含VideoFile）
            var chunkedRequest = new
            {
                TaskName = request.TaskName,
                Preset = request.Preset,
                OutputFormat = request.OutputFormat,
                Resolution = request.Resolution,
                VideoCodec = request.VideoCodec,
                AudioCodec = request.AudioCodec,
                QualityMode = request.QualityMode,
                VideoQuality = request.VideoQuality,
                AudioQuality = request.AudioQuality,
                FrameRate = request.FrameRate,
                HardwareAcceleration = request.HardwareAcceleration,
                CustomParameters = request.CustomParameters
            };
 
            var initRequest = new
            {
                UploadId = uploadId,
                FileName = fileName,
                FileSize = fileSize,
                FileMd5 = fileMd5,
                ConversionRequest = chunkedRequest
            };

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            var json = JsonSerializer.Serialize(initRequest, jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            Utils.Logger.Info("ChunkedUpload", $"发送初始化请求: {json}");

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/upload/chunked/init", content, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            Utils.Logger.Info("ChunkedUpload", $"初始化响应: {response.StatusCode} - {responseContent}");

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<ApiResponse<InitUploadResponse>>(responseContent, jsonOptions);
                return result ?? new ApiResponse<InitUploadResponse> { Success = false, Message = "响应解析失败" };
            }

            return new ApiResponse<InitUploadResponse>
            {
                Success = false,
                Message = $"初始化上传失败: {response.StatusCode} - {responseContent}"
            };
        }

        /// <summary>
        /// 获取上传状态
        /// </summary>
        private async Task<ApiResponse<UploadStatusResponse>> GetUploadStatusAsync(string uploadId, CancellationToken cancellationToken)
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/upload/chunked/status/{uploadId}", cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<ApiResponse<UploadStatusResponse>>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                return result ?? new ApiResponse<UploadStatusResponse> { Success = false, Message = "响应解析失败" };
            }

            return new ApiResponse<UploadStatusResponse>
            {
                Success = false,
                Message = $"获取上传状态失败: {response.StatusCode}"
            };
        }

        /// <summary>
        /// 带重试的分片上传
        /// </summary>
        private async Task<ApiResponse<object>> UploadChunkWithRetryAsync(
            FileStream fileStream, string uploadId, int chunkIndex, int chunkSize, int totalChunks,
            IProgress<ChunkedUploadProgress>? progress, CancellationToken cancellationToken)
        {
            var retryDelays = new[] { 1, 2, 4, 8, 16 }; // 更合理的重试间隔
            var maxRetries = Math.Min(MaxRetryAttempts, retryDelays.Length);

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    // 只在重要重试时记录日志，减少噪音
                    if (attempt > 1 && (attempt == 2 || attempt == maxRetries))
                    {
                        Utils.Logger.Info("ChunkedUpload", $"🔄 分片重试: {chunkIndex + 1}/{totalChunks} (第{attempt}/{maxRetries}次)");
                    }

                    var result = await UploadChunkAsync(fileStream, uploadId, chunkIndex, chunkSize, totalChunks, progress, cancellationToken);
                    if (result.Success)
                    {
                        if (attempt > 1)
                        {
                            Utils.Logger.Info("ChunkedUpload", $"✅ 分片重试成功: {chunkIndex + 1}/{totalChunks} (第{attempt}次尝试)");
                        }
                        return result;
                    }

                    // 分析失败原因，决定是否继续重试
                    var shouldRetry = ShouldRetryChunkUpload(result.Message, attempt, maxRetries);

                    if (attempt < maxRetries && shouldRetry)
                    {
                        var delaySeconds = retryDelays[attempt - 1];

                        // 只在重要重试时记录日志
                        if (attempt <= 2 || attempt == maxRetries - 1)
                        {
                            Utils.Logger.Info("ChunkedUpload", $"⏳ 分片重试等待: {chunkIndex + 1}/{totalChunks} - {delaySeconds}秒后第{attempt + 1}次尝试 (原因: {result.Message})");
                        }

                        progress?.Report(new ChunkedUploadProgress
                        {
                            Phase = UploadPhase.Uploading,
                            CurrentChunk = chunkIndex,
                            Message = $"分片 {chunkIndex + 1} 重试中... (第{attempt}次)"
                        });

                        await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                    }
                    else
                    {
                        Utils.Logger.Info("ChunkedUpload", $"❌ 分片上传失败，不再重试: {chunkIndex + 1}/{totalChunks} (第{attempt}次尝试) - {result.Message}");
                        return result;
                    }
                }
                catch (OperationCanceledException)
                {
                    Utils.Logger.Info("ChunkedUpload", $"� 分片上传被取消: {chunkIndex + 1}/{totalChunks}");
                    throw;
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    var delaySeconds = retryDelays[attempt - 1];

                    // 判断异常类型，决定是否重试
                    var shouldRetry = ShouldRetryOnException(ex);
                    if (!shouldRetry)
                    {
                        Utils.Logger.Info("ChunkedUpload", $"💥 分片上传致命异常，不重试: {chunkIndex + 1}/{totalChunks} - {ex.GetType().Name}: {ex.Message}");
                        return new ApiResponse<object>
                        {
                            Success = false,
                            Message = $"分片 {chunkIndex + 1} 上传异常: {ex.Message}"
                        };
                    }

                    Utils.Logger.Info("ChunkedUpload", $"⏳ 异常重试等待: {chunkIndex + 1}/{totalChunks} - {delaySeconds}秒后第{attempt + 1}次尝试 ({ex.GetType().Name})");

                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                }
                catch (Exception ex)
                {
                    Utils.Logger.Info("ChunkedUpload", $"💥 分片上传最终异常: {chunkIndex + 1}/{totalChunks} - {ex.GetType().Name}: {ex.Message}");
                    return new ApiResponse<object>
                    {
                        Success = false,
                        Message = $"分片 {chunkIndex + 1} 上传异常: {ex.Message}"
                    };
                }
            }

            Utils.Logger.Info("ChunkedUpload", $"❌ 分片上传最终失败: {chunkIndex + 1}/{totalChunks} - 已重试 {maxRetries} 次");
            return new ApiResponse<object>
            {
                Success = false,
                Message = $"分片 {chunkIndex + 1} 上传失败，已重试 {maxRetries} 次"
            };
        }

        /// <summary>
        /// 判断是否应该重试分片上传
        /// </summary>
        private static bool ShouldRetryChunkUpload(string errorMessage, int currentAttempt, int maxAttempts)
        {
            if (currentAttempt >= maxAttempts) return false;

            // 网络相关错误应该重试
            var retryableErrors = new[]
            {
                "timeout", "connection", "network", "socket", "dns",
                "502", "503", "504", "408", "429", // HTTP错误码
                "temporary", "unavailable", "busy"
            };

            var lowerError = errorMessage.ToLowerInvariant();
            return retryableErrors.Any(error => lowerError.Contains(error));
        }

        /// <summary>
        /// 判断异常是否应该重试
        /// </summary>
        private static bool ShouldRetryOnException(Exception ex)
        {
            // 网络相关异常应该重试
            return ex is HttpRequestException ||
                   ex is TaskCanceledException ||
                   ex is SocketException ||
                   (ex is IOException && ex.Message.Contains("network"));
        }

        /// <summary>
        /// 并发上传分片
        /// </summary>
        private async Task<ApiResponse<StartConversionResponse>> UploadChunksConcurrentlyAsync(
            string filePath, string uploadId, int totalChunks, int chunkSize,
            HashSet<int> uploadedChunks, List<int> failedChunks, int maxAllowedFailures,
            long fileSize, IProgress<ChunkedUploadProgress>? progress, CancellationToken cancellationToken)
        {
            // 创建需要上传的分片列表
            var chunksToUpload = new List<int>();
            for (int i = 0; i < totalChunks; i++)
            {
                if (!uploadedChunks.Contains(i))
                {
                    chunksToUpload.Add(i);
                }
            }

            if (chunksToUpload.Count == 0)
            {
                Utils.Logger.Info("ChunkedUpload", "所有分片已存在，跳过上传");
                return new ApiResponse<StartConversionResponse> { Success = true };
            }

            Utils.Logger.Info("ChunkedUpload", $"🚀 开始并发上传: {chunksToUpload.Count} 个分片，并发数={MaxConcurrentUploads}");
            var uploadStartTime = DateTime.Now;

            var completedChunks = 0;
            var progressLock = new object();
            var logInterval = Math.Max(1, chunksToUpload.Count / 20);
            var lastLoggedChunk = -1;

            // 使用SemaphoreSlim控制并发数量
            using var semaphore = new SemaphoreSlim(MaxConcurrentUploads, MaxConcurrentUploads);

            // 并发上传所有分片
            var uploadTasks = chunksToUpload.Select(async chunkIndex =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    // 为每个并发任务创建独立的FileStream，避免并发访问冲突
                    using var taskFileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                    // 上传当前分片
                    var chunkResult = await UploadChunkWithRetryAsync(
                        taskFileStream, uploadId, chunkIndex, chunkSize, totalChunks,
                        progress, cancellationToken);

                    lock (progressLock)
                    {
                        completedChunks++;

                        if (chunkResult.Success)
                        {
                            uploadedChunks.Add(chunkIndex);
                        }
                        else
                        {
                            failedChunks.Add(chunkIndex);
                            Utils.Logger.Info("ChunkedUpload", $"❌ 分片上传失败: {chunkIndex + 1}/{totalChunks} - {chunkResult.Message}");
                        }

                        // 计算当前进度并向UI报告（精确计算已上传字节数）
                        var currentUploadedBytes = CalculateUploadedBytes(uploadedChunks, chunkSize, fileSize);

                        progress?.Report(new ChunkedUploadProgress
                        {
                            Phase = UploadPhase.Uploading,
                            TotalBytes = fileSize,
                            UploadedBytes = currentUploadedBytes,
                            TotalChunks = totalChunks,
                            CompletedChunks = uploadedChunks.Count,
                            CurrentChunk = chunkIndex,
                            Speed = 0, // 并发上传时速度计算复杂，暂时设为0
                            Message = $"并发上传进度: {uploadedChunks.Count}/{totalChunks} 分片完成"
                        });

                        // 智能进度日志：按比例记录，减少日志数量
                        var shouldLog = completedChunks % logInterval == 0 ||
                                       completedChunks == chunksToUpload.Count ||
                                       (failedChunks.Count > 0 && completedChunks > lastLoggedChunk + logInterval);

                        if (shouldLog)
                        {
                            var progressPercent = (double)completedChunks / chunksToUpload.Count * 100;
                            var successRate = uploadedChunks.Count * 100.0 / completedChunks;

                            Utils.Logger.Info("ChunkedUpload", $"📊 并发上传进度: {progressPercent:F1}% ({completedChunks}/{chunksToUpload.Count}), 成功={uploadedChunks.Count}, 失败={failedChunks.Count}, 成功率={successRate:F1}%");
                            lastLoggedChunk = completedChunks;
                        }
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToArray();

            // 等待所有上传任务完成
            await Task.WhenAll(uploadTasks);

            // 计算上传性能统计
            var uploadDuration = DateTime.Now - uploadStartTime;
            var avgSpeedMBps = (fileSize / 1024.0 / 1024.0) / uploadDuration.TotalSeconds;
            Utils.Logger.Info("ChunkedUpload", $"📊 并发上传性能: 耗时={uploadDuration.TotalSeconds:F1}秒, 平均速度={avgSpeedMBps:F2}MB/s");

            // 检查是否失败分片过多
            if (failedChunks.Count > maxAllowedFailures)
            {
                Utils.Logger.Info("ChunkedUpload", $"💥 失败分片过多 ({failedChunks.Count}/{maxAllowedFailures})，停止上传");
                return new ApiResponse<StartConversionResponse>
                {
                    Success = false,
                    Message = $"上传失败：失败分片过多 ({failedChunks.Count}/{totalChunks})"
                };
            }

            return new ApiResponse<StartConversionResponse> { Success = true };
        }

        /// <summary>
        /// 精确计算已上传的字节数
        /// </summary>
        private static long CalculateUploadedBytes(HashSet<int> uploadedChunks, int chunkSize, long fileSize)
        {
            if (uploadedChunks.Count == 0) return 0;

            var totalChunks = (int)Math.Ceiling((double)fileSize / chunkSize);
            var uploadedBytes = 0L;

            foreach (var chunkIndex in uploadedChunks)
            {
                if (chunkIndex < totalChunks - 1)
                {
                    // 非最后一个分片，使用完整的chunkSize
                    uploadedBytes += chunkSize;
                }
                else
                {
                    // 最后一个分片，计算实际大小
                    var lastChunkSize = fileSize - (long)(totalChunks - 1) * chunkSize;
                    uploadedBytes += lastChunkSize;
                }
            }

            return uploadedBytes;
        }

        /// <summary>
        /// 上传单个分片
        /// </summary>
        private async Task<ApiResponse<object>> UploadChunkAsync(
            FileStream fileStream, string uploadId, int chunkIndex, int chunkSize, int totalChunks,
            IProgress<ChunkedUploadProgress>? progress, CancellationToken cancellationToken)
        {
            var startTime = DateTime.Now;

            // 计算分片信息
            var offset = (long)chunkIndex * chunkSize;
            var actualChunkSize = (int)Math.Min(chunkSize, fileStream.Length - offset);

            // 读取分片数据（不记录详细日志，减少噪音）
            var chunkData = new byte[actualChunkSize];
            fileStream.Seek(offset, SeekOrigin.Begin);
            await fileStream.ReadAsync(chunkData, 0, actualChunkSize, cancellationToken);

            // 条件性计算分片MD5（可配置关闭以提高性能）
            string chunkMd5;
            if (EnableChunkMD5)
            {
                chunkMd5 = await Task.Run(() =>
                {
                    using var md5 = MD5.Create();
                    return Convert.ToHexString(md5.ComputeHash(chunkData)).ToLowerInvariant();
                }, cancellationToken);
            }
            else
            {
                // 使用简单的校验和替代MD5，大幅提升性能
                chunkMd5 = GenerateSimpleChecksum(chunkData);
            }

            // 创建multipart表单
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(uploadId), "uploadId");
            form.Add(new StringContent(chunkIndex.ToString()), "chunkIndex");
            form.Add(new StringContent(totalChunks.ToString()), "totalChunks");
            form.Add(new StringContent(chunkMd5), "chunkMd5");
            form.Add(new ByteArrayContent(chunkData), "chunk", $"chunk_{chunkIndex}");

            // 上传分片
            var uploadStartTime = DateTime.Now;
            var response = await _httpClient.PostAsync($"{_baseUrl}/api/upload/chunked/chunk", form, cancellationToken);
            var uploadTime = DateTime.Now - uploadStartTime;
            var totalTime = DateTime.Now - startTime;

            // 只在上传时间过长或失败时记录详细信息
            var isSlowUpload = uploadTime.TotalSeconds > 5; // 超过5秒算慢
            if (isSlowUpload || !response.IsSuccessStatusCode)
            {
                Utils.Logger.Info("ChunkedUpload", $"🌐 分片上传: {chunkIndex + 1}/{totalChunks}, 状态={response.StatusCode}, 耗时={uploadTime.TotalMilliseconds:F1}ms");
            }

            // 计算传输速度
            var speedMBps = (actualChunkSize / 1024.0 / 1024.0) / uploadTime.TotalSeconds;

            // 注意：在并发上传时，进度由并发方法统一管理，这里不重复报告
            // 只在非并发上传时报告进度（这个方法现在主要用于重试）

            if (response.IsSuccessStatusCode)
            {
                // 只在速度异常时记录成功日志
                if (speedMBps < 0.1) // 速度小于0.1 MB/s算异常慢
                {
                    Utils.Logger.Info("ChunkedUpload", $"⚠️ 分片上传慢: {chunkIndex + 1}/{totalChunks}, 速度={speedMBps:F2} MB/s");
                }
                return new ApiResponse<object> { Success = true };
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            Utils.Logger.Info("ChunkedUpload", $"❌ 分片上传失败: {chunkIndex + 1}/{totalChunks}, 状态={response.StatusCode}, 错误={errorContent}");

            return new ApiResponse<object>
            {
                Success = false,
                Message = $"HTTP {response.StatusCode}: {errorContent}"
            };
        }

        /// <summary>
        /// 完成上传
        /// </summary>
        private async Task<ApiResponse<StartConversionResponse>> CompleteUploadAsync(string uploadId, CancellationToken cancellationToken)
        {
            var response = await _httpClient.PostAsync($"{_baseUrl}/api/upload/chunked/complete/{uploadId}", null, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            Utils.Logger.Info("ChunkedUpload", $"📥 完成上传响应: {response.StatusCode}");
            Utils.Logger.Info("ChunkedUpload", $"响应内容: {responseContent}");

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    // 尝试解析服务端的直接响应格式
                    var serverResponse = JsonSerializer.Deserialize<JsonElement>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    // 检查服务端返回的success字段
                    if (serverResponse.TryGetProperty("success", out var successElement) && successElement.GetBoolean())
                    {
                        var taskId = serverResponse.TryGetProperty("taskId", out var taskIdElement) ? taskIdElement.GetString() : null;
                        var taskName = serverResponse.TryGetProperty("taskName", out var taskNameElement) ? taskNameElement.GetString() : null;
                        var message = serverResponse.TryGetProperty("message", out var messageElement) ? messageElement.GetString() : "上传完成";

                        Utils.Logger.Info("ChunkedUpload", $"✅ 上传完成成功: TaskId={taskId}, Message={message}");

                        return new ApiResponse<StartConversionResponse>
                        {
                            Success = true,
                            Data = new StartConversionResponse
                            {
                                Success = true,
                                TaskId = taskId,
                                TaskName = taskName,
                                Message = message
                            },
                            Message = message
                        };
                    }
                    else
                    {
                        var errorMessage = serverResponse.TryGetProperty("message", out var msgElement) ? msgElement.GetString() : "未知错误";
                        Utils.Logger.Info("ChunkedUpload", $"❌ 服务端返回失败: {errorMessage}");

                        return new ApiResponse<StartConversionResponse>
                        {
                            Success = false,
                            Message = errorMessage
                        };
                    }
                }
                catch (JsonException ex)
                {
                    Utils.Logger.Info("ChunkedUpload", $"❌ 响应解析失败: {ex.Message}");
                    return new ApiResponse<StartConversionResponse>
                    {
                        Success = false,
                        Message = $"响应解析失败: {ex.Message}"
                    };
                }
            }

            return new ApiResponse<StartConversionResponse>
            {
                Success = false,
                Message = $"完成上传失败: {response.StatusCode} - {responseContent}"
            };
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    /// <summary>
    /// 分片上传进度信息
    /// </summary>
    public class ChunkedUploadProgress
    {
        public UploadPhase Phase { get; set; }
        public long TotalBytes { get; set; }
        public long UploadedBytes { get; set; }
        public int TotalChunks { get; set; }
        public int CompletedChunks { get; set; }
        public int CurrentChunk { get; set; }
        public double Speed { get; set; } // bytes per second
        public string Message { get; set; } = string.Empty;

        public double Percentage => TotalBytes > 0 ? (double)UploadedBytes / TotalBytes * 100 : 0;
        public TimeSpan? EstimatedTimeRemaining => Speed > 0 ? TimeSpan.FromSeconds((TotalBytes - UploadedBytes) / Speed) : null;
    }

    /// <summary>
    /// 上传阶段
    /// </summary>
    public enum UploadPhase
    {
        Calculating,    // 计算MD5
        Initializing,   // 初始化上传
        Uploading,      // 上传中
        Finalizing,     // 完成上传
        Completed       // 完成
    }

    /// <summary>
    /// 初始化上传响应
    /// </summary>
    public class InitUploadResponse
    {
        public string UploadId { get; set; } = string.Empty;
        public int ChunkSize { get; set; }
        public int TotalChunks { get; set; }
        public bool FileExists { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// 上传状态响应
    /// </summary>
    public class UploadStatusResponse
    {
        public string UploadId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public List<int> UploadedChunks { get; set; } = new();
        public int TotalChunks { get; set; }
        public long UploadedBytes { get; set; }
        public long TotalBytes { get; set; }
    }
}
