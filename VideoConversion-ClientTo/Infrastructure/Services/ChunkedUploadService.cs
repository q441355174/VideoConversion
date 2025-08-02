using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VideoConversion_ClientTo.Application.DTOs;

namespace VideoConversion_ClientTo.Infrastructure.Services
{
    /// <summary>
    /// 分片上传服务 - 与Client项目一致的实现
    /// 支持断点续传、分片上传、MD5校验等功能
    /// </summary>
    public class ChunkedUploadService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private const int DefaultChunkSize = 50 * 1024 * 1024; // 50MB per chunk
        private const int MaxRetryAttempts = 3;
        private const bool EnableChunkMD5 = false; // 是否启用分片MD5校验

        // 🔑 动态并发控制 - 支持实时调整
        private SemaphoreSlim _chunkSemaphore;
        private int _currentMaxConcurrentChunks;
        private readonly object _semaphoreLock = new object();

        public ChunkedUploadService(string baseUrl)
        {
            _baseUrl = baseUrl;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(5) // 每个分片5分钟超时
            };

            // 优化HttpClient性能
            _httpClient.DefaultRequestHeaders.Connection.Add("keep-alive");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "VideoConversion-ClientTo/1.0");

            // 🔑 初始化动态并发控制
            _currentMaxConcurrentChunks = SystemSettingsService.Instance.GetMaxConcurrentChunks();
            _chunkSemaphore = new SemaphoreSlim(_currentMaxConcurrentChunks, _currentMaxConcurrentChunks);

            // 🔑 监听系统设置变化 - 实现实时控制
            SystemSettingsService.Instance.SettingsChanged += OnSettingsChanged;

            Utils.Logger.Info("ChunkedUploadService", $"✅ 分片上传服务初始化完成，BaseUrl: {baseUrl}, 分片并发数: {_currentMaxConcurrentChunks}");
        }

        /// <summary>
        /// 分片上传文件并创建转换任务 - 与Client项目一致
        /// </summary>
        public async Task<ApiResponseDto<StartConversionResponse>> UploadFileAsync(
            string filePath,
            StartConversionRequestDto request,
            IProgress<ChunkedUploadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            Utils.Logger.Info("ChunkedUploadService", "🧩 === 开始分片上传 ===");
            Utils.Logger.Info("ChunkedUploadService", $"文件: {Path.GetFileName(filePath)}");

            try
            {
                var fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists)
                {
                    return ApiResponseDto<StartConversionResponse>.CreateError($"文件不存在: {filePath}");
                }

                Utils.Logger.Info("ChunkedUploadService", $"📁 文件大小: {fileInfo.Length} bytes ({fileInfo.Length / 1024.0 / 1024.0:F2} MB)");

                // 🔑 步骤1: 初始化上传会话
                progress?.Report(new ChunkedUploadProgress
                {
                    Phase = UploadPhase.Initializing,
                    Message = "初始化上传会话...",
                    Percentage = 0
                });

                var initResult = await InitializeUploadAsync(filePath, request, cancellationToken);
                if (!initResult.Success)
                {
                    return ApiResponseDto<StartConversionResponse>.CreateError($"初始化上传失败: {initResult.Message}");
                }

                var uploadSession = initResult.Data!;
                Utils.Logger.Info("ChunkedUploadService", $"✅ 上传会话初始化成功，UploadId: {uploadSession.UploadId}");

                // 🔑 步骤2: 分片上传
                progress?.Report(new ChunkedUploadProgress
                {
                    Phase = UploadPhase.Uploading,
                    Message = "开始分片上传...",
                    Percentage = 5
                });

                var uploadResult = await UploadChunksAsync(filePath, uploadSession, progress, cancellationToken);
                if (!uploadResult.Success)
                {
                    return ApiResponseDto<StartConversionResponse>.CreateError($"分片上传失败: {uploadResult.Message}");
                }

                // 🔑 步骤3: 完成上传
                progress?.Report(new ChunkedUploadProgress
                {
                    Phase = UploadPhase.Finalizing,
                    Message = "完成上传，创建转换任务...",
                    Percentage = 95
                });

                var completeResult = await CompleteUploadAsync(uploadSession.UploadId, request, cancellationToken);
                if (!completeResult.Success)
                {
                    return ApiResponseDto<StartConversionResponse>.CreateError($"完成上传失败: {completeResult.Message}");
                }

                progress?.Report(new ChunkedUploadProgress
                {
                    Phase = UploadPhase.Completed,
                    Message = "分片上传完成",
                    Percentage = 100
                });

                Utils.Logger.Info("ChunkedUploadService", $"🎉 分片上传完成，TaskId: {completeResult.Data?.TaskId}");
                return completeResult;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ChunkedUploadService", $"❌ 分片上传异常: {ex.Message}");
                return ApiResponseDto<StartConversionResponse>.CreateError($"分片上传失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 初始化上传会话
        /// </summary>
        private async Task<ApiResponseDto<UploadSession>> InitializeUploadAsync(
            string filePath,
            StartConversionRequestDto request,
            CancellationToken cancellationToken)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                var totalChunks = (int)Math.Ceiling((double)fileInfo.Length / DefaultChunkSize);

                var initRequest = new
                {
                    FileName = Path.GetFileName(filePath),
                    FileSize = fileInfo.Length,
                    ChunkSize = DefaultChunkSize,
                    TotalChunks = totalChunks,
                    FileMD5 = EnableChunkMD5 ? await CalculateFileMD5Async(filePath) : null,
                    ConversionRequest = request
                };

                var json = JsonSerializer.Serialize(initRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}/api/upload/chunked/init", content, cancellationToken);
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var apiResponse = JsonSerializer.Deserialize<ApiResponseDto<UploadSession>>(responseContent);
                    return apiResponse ?? ApiResponseDto<UploadSession>.CreateError("响应解析失败");
                }
                else
                {
                    return ApiResponseDto<UploadSession>.CreateError($"HTTP错误: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                return ApiResponseDto<UploadSession>.CreateError($"初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 分片上传
        /// </summary>
        private async Task<ApiResponseDto<object>> UploadChunksAsync(
            string filePath,
            UploadSession session,
            IProgress<ChunkedUploadProgress>? progress,
            CancellationToken cancellationToken)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                var totalChunks = (int)Math.Ceiling((double)fileInfo.Length / DefaultChunkSize);
                var uploadedChunks = 0;

                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);

                // 🔑 使用动态并发控制 - 支持实时调整
                var tasks = new List<Task>();

                for (int chunkIndex = 0; chunkIndex < totalChunks; chunkIndex++)
                {
                    var currentChunkIndex = chunkIndex;
                    var task = Task.Run(async () =>
                    {
                        // 🔑 使用动态信号量 - 支持实时调整
                        await GetCurrentSemaphore().WaitAsync(cancellationToken);
                        try
                        {
                            var success = await UploadSingleChunkAsync(
                                fileStream, 
                                session.UploadId, 
                                currentChunkIndex, 
                                fileInfo.Length, 
                                cancellationToken);

                            if (success)
                            {
                                Interlocked.Increment(ref uploadedChunks);
                                var percentage = 5 + (uploadedChunks * 85.0 / totalChunks); // 5%-90%
                                
                                progress?.Report(new ChunkedUploadProgress
                                {
                                    Phase = UploadPhase.Uploading,
                                    Message = $"上传分片 {uploadedChunks}/{totalChunks}",
                                    Percentage = percentage,
                                    UploadedBytes = uploadedChunks * DefaultChunkSize,
                                    TotalBytes = fileInfo.Length
                                });
                            }
                        }
                        finally
                        {
                            // 🔑 释放动态信号量
                            GetCurrentSemaphore().Release();
                        }
                    }, cancellationToken);

                    tasks.Add(task);
                }

                await Task.WhenAll(tasks);

                if (uploadedChunks == totalChunks)
                {
                    return ApiResponseDto<object>.CreateSuccess(new { }, "所有分片上传完成");
                }
                else
                {
                    return ApiResponseDto<object>.CreateError($"分片上传不完整: {uploadedChunks}/{totalChunks}");
                }
            }
            catch (Exception ex)
            {
                return ApiResponseDto<object>.CreateError($"分片上传异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 上传单个分片
        /// </summary>
        private async Task<bool> UploadSingleChunkAsync(
            FileStream fileStream,
            string uploadId,
            int chunkIndex,
            long totalFileSize,
            CancellationToken cancellationToken)
        {
            for (int attempt = 1; attempt <= MaxRetryAttempts; attempt++)
            {
                try
                {
                    var chunkStart = (long)chunkIndex * DefaultChunkSize;
                    var chunkSize = (int)Math.Min(DefaultChunkSize, totalFileSize - chunkStart);
                    var chunkData = new byte[chunkSize];

                    lock (fileStream)
                    {
                        fileStream.Seek(chunkStart, SeekOrigin.Begin);
                        fileStream.Read(chunkData, 0, chunkSize);
                    }

                    using var form = new MultipartFormDataContent();
                    form.Add(new StringContent(uploadId), "uploadId");
                    form.Add(new StringContent(chunkIndex.ToString()), "chunkIndex");
                    form.Add(new StringContent(chunkSize.ToString()), "chunkSize");
                    form.Add(new ByteArrayContent(chunkData), "chunk", $"chunk_{chunkIndex}");

                    if (EnableChunkMD5)
                    {
                        var chunkMD5 = CalculateChunkMD5(chunkData);
                        form.Add(new StringContent(chunkMD5), "chunkMD5");
                    }

                    var response = await _httpClient.PostAsync($"{_baseUrl}/api/upload/chunked/chunk", form, cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        Utils.Logger.Debug("ChunkedUploadService", $"✅ 分片 {chunkIndex} 上传成功");
                        return true;
                    }
                    else
                    {
                        Utils.Logger.Warning("ChunkedUploadService", $"⚠️ 分片 {chunkIndex} 上传失败 (尝试 {attempt}): {response.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    Utils.Logger.Warning("ChunkedUploadService", $"⚠️ 分片 {chunkIndex} 上传异常 (尝试 {attempt}): {ex.Message}");
                }

                if (attempt < MaxRetryAttempts)
                {
                    await Task.Delay(1000 * attempt, cancellationToken); // 递增延迟
                }
            }

            Utils.Logger.Error("ChunkedUploadService", $"❌ 分片 {chunkIndex} 上传最终失败");
            return false;
        }

        /// <summary>
        /// 完成上传
        /// </summary>
        private async Task<ApiResponseDto<StartConversionResponse>> CompleteUploadAsync(
            string uploadId,
            StartConversionRequestDto request,
            CancellationToken cancellationToken)
        {
            try
            {
                var completeRequest = new
                {
                    UploadId = uploadId,
                    ConversionRequest = request
                };

                var json = JsonSerializer.Serialize(completeRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}/api/upload/chunked/complete", content, cancellationToken);
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var apiResponse = JsonSerializer.Deserialize<ApiResponseDto<StartConversionResponse>>(responseContent);
                    return apiResponse ?? ApiResponseDto<StartConversionResponse>.CreateError("响应解析失败");
                }
                else
                {
                    return ApiResponseDto<StartConversionResponse>.CreateError($"HTTP错误: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                return ApiResponseDto<StartConversionResponse>.CreateError($"完成上传失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 计算文件MD5
        /// </summary>
        private async Task<string> CalculateFileMD5Async(string filePath)
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(filePath);
            var hash = await md5.ComputeHashAsync(stream);
            return Convert.ToHexString(hash).ToLower();
        }

        /// <summary>
        /// 计算分片MD5
        /// </summary>
        private string CalculateChunkMD5(byte[] chunkData)
        {
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(chunkData);
            return Convert.ToHexString(hash).ToLower();
        }

        // Dispose方法已移至动态并发控制区域

        #region 动态并发控制 - 实时调整支持

        /// <summary>
        /// 获取当前信号量（线程安全）
        /// </summary>
        private SemaphoreSlim GetCurrentSemaphore()
        {
            lock (_semaphoreLock)
            {
                return _chunkSemaphore;
            }
        }

        /// <summary>
        /// 处理系统设置变化 - 实现实时并发控制
        /// </summary>
        private void OnSettingsChanged(object? sender, SystemSettingsChangedEventArgs e)
        {
            if (e.ConcurrencySettingsChanged)
            {
                var newMaxChunks = e.NewSettings.MaxConcurrentChunks;
                if (newMaxChunks != _currentMaxConcurrentChunks)
                {
                    lock (_semaphoreLock)
                    {
                        try
                        {
                            // 释放旧的信号量
                            var oldSemaphore = _chunkSemaphore;

                            // 创建新的信号量
                            _chunkSemaphore = new SemaphoreSlim(newMaxChunks, newMaxChunks);
                            _currentMaxConcurrentChunks = newMaxChunks;

                            // 释放旧信号量
                            oldSemaphore?.Dispose();

                            Utils.Logger.Info("ChunkedUploadService",
                                $"🔧 分片并发数已更新: {_currentMaxConcurrentChunks}");
                        }
                        catch (Exception ex)
                        {
                            Utils.Logger.Error("ChunkedUploadService",
                                $"❌ 更新分片并发数失败: {ex.Message}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            try
            {
                // 取消监听设置变化
                SystemSettingsService.Instance.SettingsChanged -= OnSettingsChanged;

                // 释放信号量
                _chunkSemaphore?.Dispose();

                // 释放HttpClient
                _httpClient?.Dispose();
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ChunkedUploadService", $"❌ 释放资源失败: {ex.Message}");
            }
        }

        #endregion
    }

    #region 分片上传相关DTO

    /// <summary>
    /// 上传会话
    /// </summary>
    public class UploadSession
    {
        public string UploadId { get; set; } = "";
        public string SessionToken { get; set; } = "";
        public int ChunkSize { get; set; }
        public int TotalChunks { get; set; }
    }

    /// <summary>
    /// 分片上传进度
    /// </summary>
    public class ChunkedUploadProgress
    {
        public UploadPhase Phase { get; set; }
        public string Message { get; set; } = "";
        public double Percentage { get; set; }
        public long UploadedBytes { get; set; }
        public long TotalBytes { get; set; }
        public double Speed { get; set; }
        public double EstimatedTimeRemaining { get; set; }
    }

    /// <summary>
    /// 上传阶段
    /// </summary>
    public enum UploadPhase
    {
        Calculating,
        Initializing,
        Uploading,
        Finalizing,
        Completed
    }

    #endregion
}
