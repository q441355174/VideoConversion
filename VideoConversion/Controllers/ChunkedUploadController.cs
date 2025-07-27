using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using VideoConversion.Controllers.Base;
using VideoConversion.Hubs;
using VideoConversion.Models;
using VideoConversion.Services;

namespace VideoConversion.Controllers
{
    /// <summary>
    /// 分片上传控制器 - 基于WebUploader思想实现
    /// 支持断点续传、分片上传、MD5校验等功能
    /// </summary>
    [Route("api/upload/chunked")]
    public class ChunkedUploadController : BaseApiController
    {
        private readonly FileService _fileService;
        private readonly ConversionTaskService _conversionTaskService;
        private readonly IHubContext<ConversionHub> _hubContext;
        private readonly IConfiguration _configuration;
        private readonly DiskSpaceService _diskSpaceService;
        
        // 上传会话管理
        private static readonly ConcurrentDictionary<string, UploadSession> _uploadSessions = new();
        private static readonly ConcurrentDictionary<string, HashSet<int>> _uploadedChunks = new();

        public ChunkedUploadController(
            ILogger<ChunkedUploadController> logger,
            FileService fileService,
            ConversionTaskService conversionTaskService,
            IHubContext<ConversionHub> hubContext,
            IConfiguration configuration,
            DiskSpaceService diskSpaceService) : base(logger)
        {
            _fileService = fileService;
            _conversionTaskService = conversionTaskService;
            _hubContext = hubContext;
            _configuration = configuration;
            _diskSpaceService = diskSpaceService;

            Logger.LogInformation("🧩 ChunkedUploadController 已初始化");
            Logger.LogInformation("路由: /api/upload/chunked");
        }

        /// <summary>
        /// 初始化分片上传
        /// </summary>
        [HttpPost("init")]
        public async Task<IActionResult> InitializeUpload([FromBody] InitUploadRequest? request)
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];

            // 立即记录请求到达
            Logger.LogInformation("🚨 ChunkedUploadController.InitializeUpload 方法被调用！");
            Logger.LogInformation("🚨 RequestId: {RequestId}", requestId);
            Logger.LogInformation("🚨 Request对象是否为null: {IsNull}", request == null);

            try
            {
                Logger.LogInformation("[{RequestId}] 🧩 === 收到分片上传初始化请求 ===", requestId);
                Logger.LogInformation("[{RequestId}] 客户端IP: {ClientIP}", requestId, GetClientIpAddress());
                Logger.LogInformation("[{RequestId}] 请求时间: {RequestTime}", requestId, DateTime.Now);
                Logger.LogInformation("[{RequestId}] HTTP方法: {Method}", requestId, HttpContext.Request.Method);
                Logger.LogInformation("[{RequestId}] 请求路径: {Path}", requestId, HttpContext.Request.Path);
                Logger.LogInformation("[{RequestId}] Content-Type: {ContentType}", requestId, HttpContext.Request.ContentType);

                if (request == null)
                {
                    Logger.LogError("[{RequestId}] ❌ InitUploadRequest为null", requestId);
                    return BadRequest(new { success = false, message = "请求数据为空" });
                }

                Logger.LogInformation("[{RequestId}] 📋 请求参数详情:", requestId);
                Logger.LogInformation("[{RequestId}]   UploadId: {UploadId}", requestId, request.UploadId);
                Logger.LogInformation("[{RequestId}]   FileName: {FileName}", requestId, request.FileName);
                Logger.LogInformation("[{RequestId}]   FileSize: {FileSize} bytes ({FileSizeMB:F2} MB)", requestId, request.FileSize, request.FileSize / 1024.0 / 1024.0);
                Logger.LogInformation("[{RequestId}]   FileMd5: {MD5}", requestId, request.FileMd5);

                if (request.ConversionRequest != null)
                {
                    Logger.LogInformation("[{RequestId}]   转换参数: 格式={OutputFormat}, 分辨率={Resolution}", requestId,
                        request.ConversionRequest.OutputFormat, request.ConversionRequest.Resolution);
                }

                // 验证请求
                if (string.IsNullOrEmpty(request.UploadId))
                {
                    Logger.LogError("UploadId为空");
                    return BadRequest(new { success = false, message = "UploadId不能为空" });
                }

                if (string.IsNullOrEmpty(request.FileName))
                {
                    Logger.LogError("FileName为空");
                    return BadRequest(new { success = false, message = "文件名不能为空" });
                }

                if (request.FileSize <= 0)
                {
                    Logger.LogError("FileSize无效: {FileSize}", request.FileSize);
                    return BadRequest(new { success = false, message = "文件大小无效" });
                }

                // 如果ConversionRequest为null，创建默认的
                if (request.ConversionRequest == null)
                {
                    Logger.LogWarning("[{RequestId}] ConversionRequest为null，使用默认设置", requestId);
                    request.ConversionRequest = new ChunkedConversionRequest
                    {
                        TaskName = Path.GetFileNameWithoutExtension(request.FileName),
                        OutputFormat = "mp4"
                    };
                }

                Logger.LogInformation("[{RequestId}] 转换请求验证通过", requestId);

                // 检查磁盘空间是否足够
                Logger.LogInformation("[{RequestId}] 🔍 开始检查磁盘空间...", requestId);
                var spaceCheckRequest = new SpaceCheckRequest
                {
                    OriginalFileSize = request.FileSize,
                    EstimatedOutputSize = EstimateOutputSize(request.FileSize, request.ConversionRequest),
                    TaskType = "upload_and_conversion",
                    IncludeTempSpace = true
                };

                var spaceCheckResult = await _diskSpaceService.CheckSpaceAsync(spaceCheckRequest);

                if (!spaceCheckResult.HasEnoughSpace)
                {
                    Logger.LogWarning("[{RequestId}] ❌ 磁盘空间不足: 需要={RequiredGB}GB, 可用={AvailableGB}GB",
                        requestId,
                        Math.Round(spaceCheckResult.RequiredSpace / 1024.0 / 1024 / 1024, 2),
                        Math.Round(spaceCheckResult.AvailableSpace / 1024.0 / 1024 / 1024, 2));

                    return BadRequest(new {
                        success = false,
                        message = $"磁盘空间不足：需要 {Math.Round(spaceCheckResult.RequiredSpace / 1024.0 / 1024 / 1024, 2)}GB，可用 {Math.Round(spaceCheckResult.AvailableSpace / 1024.0 / 1024 / 1024, 2)}GB",
                        errorType = "InsufficientDiskSpace",
                        requiredSpaceGB = Math.Round(spaceCheckResult.RequiredSpace / 1024.0 / 1024 / 1024, 2),
                        availableSpaceGB = Math.Round(spaceCheckResult.AvailableSpace / 1024.0 / 1024 / 1024, 2),
                        details = spaceCheckResult.Details
                    });
                }

                Logger.LogInformation("[{RequestId}] ✅ 磁盘空间检查通过: 需要={RequiredGB}GB, 可用={AvailableGB}GB",
                    requestId,
                    Math.Round(spaceCheckResult.RequiredSpace / 1024.0 / 1024 / 1024, 2),
                    Math.Round(spaceCheckResult.AvailableSpace / 1024.0 / 1024 / 1024, 2));

                // 检查文件是否已存在（秒传功能）
                var existingFile = await CheckFileExistsAsync(request.FileMd5, request.FileSize);
                if (existingFile != null)
                {
                    Logger.LogInformation("文件已存在，启用秒传: MD5={MD5}", request.FileMd5);
                    
                    // 直接创建转换任务
                    var quickResult = await CreateConversionTaskAsync(existingFile, request.ConversionRequest);
                    if (quickResult.Success)
                    {
                        return Ok(new
                        {
                            success = true,
                            uploadId = request.UploadId,
                            fileExists = true,
                            message = "文件已存在，秒传成功",
                            taskId = quickResult.TaskId,
                            taskName = quickResult.TaskName
                        });
                    }
                }

                // 创建上传会话
                var chunkSize = _configuration.GetValue<int>("VideoConversion:ChunkSize", 2 * 1024 * 1024); // 默认2MB
                var session = new UploadSession
                {
                    UploadId = request.UploadId,
                    FileName = request.FileName,
                    FileSize = request.FileSize,
                    FileMd5 = request.FileMd5,
                    ChunkSize = chunkSize,
                    TotalChunks = (int)Math.Ceiling((double)request.FileSize / chunkSize),
                    ConversionRequest = request.ConversionRequest,
                    CreatedAt = DateTime.Now,
                    TempDirectory = Path.Combine(Path.GetTempPath(), "chunked_uploads", request.UploadId)
                };

                Logger.LogInformation("[{RequestId}] 上传会话配置: ChunkSize={ChunkSize}, TotalChunks={TotalChunks}",
                    requestId, chunkSize, session.TotalChunks);

                // 创建临时目录
                Directory.CreateDirectory(session.TempDirectory);

                _uploadSessions[request.UploadId] = session;
                _uploadedChunks[request.UploadId] = new HashSet<int>();

                Logger.LogInformation("分片上传会话创建成功: UploadId={UploadId}, TotalChunks={TotalChunks}, ChunkSize={ChunkSize}",
                    request.UploadId, session.TotalChunks, chunkSize);

                return Ok(new
                {
                    success = true,
                    uploadId = request.UploadId,
                    chunkSize = chunkSize,
                    totalChunks = session.TotalChunks,
                    fileExists = false,
                    message = "上传会话初始化成功"
                });
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "初始化分片上传失败: UploadId={UploadId}", request.UploadId);
                return ServerError("初始化上传失败");
            }
        }

        /// <summary>
        /// 上传分片
        /// </summary>
        [HttpPost("chunk")]
        [RequestSizeLimit(100 * 1024 * 1024)] // 100MB per chunk (支持50MB分片 + 额外开销)
        public async Task<IActionResult> UploadChunk()
        {
            var startTime = DateTime.Now;
            try
            {
                var form = await Request.ReadFormAsync();
                var uploadId = form["uploadId"].ToString();
                var chunkIndex = int.Parse(form["chunkIndex"].ToString());
                var totalChunks = int.Parse(form["totalChunks"].ToString());
                var chunkMd5 = form["chunkMd5"].ToString();
                var chunkFile = form.Files["chunk"];

                if (chunkFile == null || chunkFile.Length == 0)
                {
                    return BadRequest(new { success = false, message = "分片数据为空" });
                }

                // 获取上传会话
                if (!_uploadSessions.TryGetValue(uploadId, out var session))
                {
                    return BadRequest(new { success = false, message = "上传会话不存在" });
                }

                // 只在特定条件下记录分片接收日志，减少噪音
                var shouldLogChunk = (chunkIndex + 1) % Math.Max(1, totalChunks / 20) == 0 ||
                                   chunkIndex == 0 ||
                                   chunkIndex == totalChunks - 1;

                if (shouldLogChunk)
                {
                    var progressPercent = (double)(chunkIndex + 1) / totalChunks * 100;
                    Logger.LogInformation("接收分片: UploadId={UploadId}, 进度={Progress:F1}% ({ChunkIndex}/{TotalChunks}), Size={Size}",
                        uploadId, progressPercent, chunkIndex + 1, totalChunks, chunkFile.Length);
                }

                // 保存分片
                var chunkPath = Path.Combine(session.TempDirectory, $"chunk_{chunkIndex:D6}");

                // 可配置的分片MD5校验
                var enableChunkMD5 = _configuration.GetValue<bool>("VideoConversion:EnableChunkMD5Validation", false);
                if (enableChunkMD5)
                {
                    using var stream = chunkFile.OpenReadStream();
                    using var md5 = MD5.Create();
                    var actualMd5 = Convert.ToHexString(await md5.ComputeHashAsync(stream)).ToLowerInvariant();

                    if (actualMd5 != chunkMd5.ToLowerInvariant())
                    {
                        Logger.LogWarning("分片MD5校验失败: UploadId={UploadId}, Chunk={ChunkIndex}, Expected={Expected}, Actual={Actual}",
                            uploadId, chunkIndex, chunkMd5, actualMd5);
                        return BadRequest(new { success = false, message = "分片数据校验失败" });
                    }
                    Logger.LogDebug("分片MD5校验通过: UploadId={UploadId}, Chunk={ChunkIndex}", uploadId, chunkIndex);

                    // 重置流位置用于保存文件
                    stream.Seek(0, SeekOrigin.Begin);
                    using var fileStream = new FileStream(chunkPath, FileMode.Create, FileAccess.Write);
                    await stream.CopyToAsync(fileStream);
                }
                else
                {
                    Logger.LogDebug("跳过分片MD5校验: UploadId={UploadId}, Chunk={ChunkIndex} (性能优化)", uploadId, chunkIndex);

                    // 直接保存文件，无需MD5校验
                    using var stream = chunkFile.OpenReadStream();
                    using var fileStream = new FileStream(chunkPath, FileMode.Create, FileAccess.Write);
                    await stream.CopyToAsync(fileStream);
                }

                // 记录已上传的分片
                _uploadedChunks[uploadId].Add(chunkIndex);

                // 通知进度
                var uploadedChunks = _uploadedChunks[uploadId].Count;
                var progress = (double)uploadedChunks / totalChunks * 100;

                await _hubContext.Clients.All.SendAsync("ChunkUploaded", new
                {
                    UploadId = uploadId,
                    ChunkIndex = chunkIndex,
                    UploadedChunks = uploadedChunks,
                    TotalChunks = totalChunks,
                    Progress = progress,
                    Timestamp = DateTime.Now
                });

                // 只在进度节点记录成功日志，包含性能信息
                if (shouldLogChunk)
                {
                    var duration = DateTime.Now - startTime;
                    var chunkSizeMB = chunkFile.Length / 1024.0 / 1024.0;
                    var speedMBps = chunkSizeMB / duration.TotalSeconds;

                    Logger.LogInformation("分片上传: UploadId={UploadId}, 进度={Progress:F1}% ({UploadedChunks}/{TotalChunks}), " +
                        "分片大小={ChunkSizeMB:F2}MB, 耗时={Duration:F2}秒, 速度={Speed:F2}MB/s",
                        uploadId, progress, uploadedChunks, totalChunks, chunkSizeMB, duration.TotalSeconds, speedMBps);
                }

                return Ok(new
                {
                    success = true,
                    chunkIndex = chunkIndex,
                    uploadedChunks = uploadedChunks,
                    totalChunks = totalChunks,
                    progress = progress
                });
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "分片上传失败");
                return ServerError("分片上传失败");
            }
        }

        /// <summary>
        /// 获取上传状态
        /// </summary>
        [HttpGet("status/{uploadId}")]
        public IActionResult GetUploadStatus(string uploadId)
        {
            try
            {
                if (!_uploadSessions.TryGetValue(uploadId, out var session))
                {
                    return NotFound(new { success = false, message = "上传会话不存在" });
                }

                var uploadedChunks = _uploadedChunks.TryGetValue(uploadId, out var chunks) ? chunks.ToList() : new List<int>();
                var uploadedBytes = uploadedChunks.Count * session.ChunkSize;

                return Ok(new
                {
                    success = true,
                    uploadId = uploadId,
                    status = "uploading",
                    uploadedChunks = uploadedChunks,
                    totalChunks = session.TotalChunks,
                    uploadedBytes = Math.Min(uploadedBytes, session.FileSize),
                    totalBytes = session.FileSize,
                    progress = (double)uploadedChunks.Count / session.TotalChunks * 100
                });
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "获取上传状态失败: UploadId={UploadId}", uploadId);
                return ServerError("获取上传状态失败");
            }
        }

        /// <summary>
        /// 完成上传
        /// </summary>
        [HttpPost("complete/{uploadId}")]
        public async Task<IActionResult> CompleteUpload(string uploadId)
        {
            try
            {
                if (!_uploadSessions.TryGetValue(uploadId, out var session))
                {
                    return BadRequest(new { success = false, message = "上传会话不存在" });
                }

                Logger.LogInformation("开始完成分片上传: UploadId={UploadId}", uploadId);

                // 验证所有分片都已上传
                var uploadedChunks = _uploadedChunks.TryGetValue(uploadId, out var chunks) ? chunks : new HashSet<int>();
                if (uploadedChunks.Count != session.TotalChunks)
                {
                    return BadRequest(new { 
                        success = false, 
                        message = $"分片不完整，已上传 {uploadedChunks.Count}/{session.TotalChunks}" 
                    });
                }

                // 合并分片
                var finalFilePath = await MergeChunksAsync(session);
                if (string.IsNullOrEmpty(finalFilePath))
                {
                    return ServerError("合并分片失败");
                }

                // 可配置的最终文件校验
                var enableFinalMD5 = _configuration.GetValue<bool>("VideoConversion:EnableFinalFileMD5Validation", false);
                if (enableFinalMD5)
                {
                    Logger.LogInformation("开始最终文件MD5校验: {FilePath}", finalFilePath);
                    if (!await ValidateFinalFileAsync(finalFilePath, session.FileMd5, session.FileSize))
                    {
                        System.IO.File.Delete(finalFilePath);
                        return BadRequest(new { success = false, message = "文件校验失败" });
                    }
                    Logger.LogInformation("最终文件MD5校验通过: {FilePath}", finalFilePath);
                }
                else
                {
                    // 只验证文件大小，跳过耗时的MD5校验
                    var fileInfo = new FileInfo(finalFilePath);
                    if (fileInfo.Length != session.FileSize)
                    {
                        Logger.LogWarning("文件大小不匹配: Expected={Expected}, Actual={Actual}", session.FileSize, fileInfo.Length);
                        System.IO.File.Delete(finalFilePath);
                        return BadRequest(new { success = false, message = "文件大小校验失败" });
                    }
                    Logger.LogInformation("跳过MD5校验，仅验证文件大小: {FilePath} ({FileSize} bytes)", finalFilePath, fileInfo.Length);
                }

                // 创建转换任务
                var taskResult = await CreateConversionTaskAsync(finalFilePath, session.ConversionRequest);
                
                // 清理上传会话
                CleanupUploadSession(uploadId);

                if (taskResult.Success)
                {
                    Logger.LogInformation("分片上传完成，转换任务创建成功: UploadId={UploadId}, TaskId={TaskId}",
                        uploadId, taskResult.TaskId);

                    await _hubContext.Clients.All.SendAsync("UploadCompleted", new
                    {
                        UploadId = uploadId,
                        TaskId = taskResult.TaskId,
                        FileName = session.FileName,
                        Timestamp = DateTime.Now
                    });

                    return Ok(new
                    {
                        success = true,
                        taskId = taskResult.TaskId,
                        taskName = taskResult.TaskName,
                        message = "上传完成，转换任务已创建"
                    });
                }
                else
                {
                    return BadRequest(new { success = false, message = taskResult.ErrorMessage });
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "完成分片上传失败: UploadId={UploadId}", uploadId);
                CleanupUploadSession(uploadId);
                return ServerError("完成上传失败");
            }
        }

        /// <summary>
        /// 检查文件是否已存在（秒传功能）
        /// </summary>
        private async Task<string?> CheckFileExistsAsync(string fileMd5, long fileSize)
        {
            try
            {
                var uploadsPath = _configuration.GetValue<string>("VideoConversion:UploadsPath", "uploads");
                if (!Directory.Exists(uploadsPath))
                    return null;

                var files = System.IO.Directory.GetFiles(uploadsPath, "*", SearchOption.AllDirectories);

                var quickIdThresholdMB = _configuration.GetValue<long>("VideoConversion:QuickIdThresholdMB", 500);
                var quickIdThreshold = quickIdThresholdMB * 1024 * 1024;

                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.Length == fileSize)
                    {
                        // 对于大文件，使用快速标识匹配；小文件使用MD5匹配
                        if (fileSize > quickIdThreshold)
                        {
                            // 大文件：使用快速标识匹配（基于文件路径、大小、修改时间）
                            var quickId = GenerateQuickFileId(file, fileInfo);
                            if (quickId == fileMd5.ToLowerInvariant())
                            {
                                Logger.LogInformation("大文件快速匹配成功: {FilePath} (快速标识)", file);
                                return file;
                            }
                        }
                        else
                        {
                            // 小文件：使用传统MD5匹配
                            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                            using var md5 = MD5.Create();
                            var hash = await md5.ComputeHashAsync(stream);
                            var actualMd5 = Convert.ToHexString(hash).ToLowerInvariant();

                            if (actualMd5 == fileMd5.ToLowerInvariant())
                            {
                                Logger.LogInformation("小文件MD5匹配成功: {FilePath}", file);
                                return file;
                            }
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "检查文件是否存在时发生错误");
                return null;
            }
        }

        /// <summary>
        /// 生成快速文件标识（与客户端保持一致）
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
        /// 合并分片
        /// </summary>
        private async Task<string?> MergeChunksAsync(UploadSession session)
        {
            var startTime = DateTime.Now;
            try
            {
                Logger.LogInformation("开始合并分片: UploadId={UploadId}, TotalChunks={TotalChunks}", session.UploadId, session.TotalChunks);

                var uploadsPath = _configuration.GetValue<string>("VideoConversion:UploadsPath", "uploads");
                Directory.CreateDirectory(uploadsPath);

                var finalFilePath = Path.Combine(uploadsPath, $"{session.UploadId}_{session.FileName}");

                using var outputStream = new FileStream(finalFilePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 1024 * 1024); // 1MB缓冲区

                for (int i = 0; i < session.TotalChunks; i++)
                {
                    var chunkPath = Path.Combine(session.TempDirectory, $"chunk_{i:D6}");
                    if (!System.IO.File.Exists(chunkPath))
                    {
                        Logger.LogError("分片文件不存在: {ChunkPath}", chunkPath);
                        return null;
                    }

                    using var chunkStream = new FileStream(chunkPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1024 * 1024); // 1MB缓冲区
                    await chunkStream.CopyToAsync(outputStream);

                    // 每10个分片记录一次进度
                    if ((i + 1) % 10 == 0 || i == session.TotalChunks - 1)
                    {
                        var progress = (double)(i + 1) / session.TotalChunks * 100;
                        Logger.LogDebug("合并进度: {Progress:F1}% ({Current}/{Total})", progress, i + 1, session.TotalChunks);
                    }
                }

                var duration = DateTime.Now - startTime;
                var fileSizeMB = outputStream.Length / 1024.0 / 1024.0;
                var speedMBps = fileSizeMB / duration.TotalSeconds;

                Logger.LogInformation("分片合并完成: {FinalFilePath}, Size={SizeMB:F2}MB, 耗时={Duration:F1}秒, 速度={Speed:F2}MB/s",
                    finalFilePath, fileSizeMB, duration.TotalSeconds, speedMBps);
                return finalFilePath;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "合并分片失败: UploadId={UploadId}", session.UploadId);
                return null;
            }
        }

        /// <summary>
        /// 验证最终文件
        /// </summary>
        private async Task<bool> ValidateFinalFileAsync(string filePath, string expectedMd5, long expectedSize)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length != expectedSize)
                {
                    Logger.LogError("文件大小不匹配: Expected={Expected}, Actual={Actual}", expectedSize, fileInfo.Length);
                    return false;
                }

                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var md5 = MD5.Create();
                var hash = await md5.ComputeHashAsync(stream);
                var actualMd5 = Convert.ToHexString(hash).ToLowerInvariant();

                if (actualMd5 != expectedMd5.ToLowerInvariant())
                {
                    Logger.LogError("文件MD5不匹配: Expected={Expected}, Actual={Actual}", expectedMd5, actualMd5);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "验证文件失败: {FilePath}", filePath);
                return false;
            }
        }

        /// <summary>
        /// 创建转换任务
        /// </summary>
        private async Task<(bool Success, string TaskId, string TaskName, string ErrorMessage)> CreateConversionTaskAsync(
            string filePath, ChunkedConversionRequest conversionRequest)
        {
            try
            {
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
                var fileInfo = new FileInfo(filePath);

                // 构建转换任务请求
                var taskRequest = new ConversionTaskRequest
                {
                    TaskName = conversionRequest.TaskName ?? Path.GetFileNameWithoutExtension(filePath),
                    OutputFormat = conversionRequest.OutputFormat ?? "mp4",
                    Resolution = conversionRequest.Resolution,
                    VideoCodec = conversionRequest.VideoCodec,
                    AudioCodec = conversionRequest.AudioCodec,
                    QualityMode = conversionRequest.QualityMode,
                    VideoQuality = conversionRequest.VideoQuality
                };

                // 创建转换任务
                var result = await _conversionTaskService.CreateTaskFromUploadedFile(
                    filePath,
                    fileInfo.Name,
                    fileInfo.Length,
                    taskRequest,
                    clientIp);

                if (result.Success)
                {
                    Logger.LogInformation("转换任务创建成功: TaskId={TaskId}, FilePath={FilePath}", result.TaskId, filePath);
                }

                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "创建转换任务失败: FilePath={FilePath}", filePath);
                return (false, "", "", ex.Message);
            }
        }

        /// <summary>
        /// 清理上传会话
        /// </summary>
        private void CleanupUploadSession(string uploadId)
        {
            try
            {
                if (_uploadSessions.TryRemove(uploadId, out var session))
                {
                    // 删除临时目录
                    if (Directory.Exists(session.TempDirectory))
                    {
                        Directory.Delete(session.TempDirectory, true);
                    }
                }

                _uploadedChunks.TryRemove(uploadId, out _);

                Logger.LogInformation("上传会话清理完成: UploadId={UploadId}", uploadId);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "清理上传会话失败: UploadId={UploadId}", uploadId);
            }
        }
        /// <summary>
        /// 预估输出文件大小
        /// </summary>
        private long EstimateOutputSize(long originalSize, ChunkedConversionRequest conversionRequest)
        {
            // 基于编码器的压缩比
            var compressionRatio = (conversionRequest.VideoCodec?.ToLower()) switch
            {
                "h264" or "h264_nvenc" => 0.7,
                "h265" or "hevc" or "h265_nvenc" => 0.5,
                "av1" => 0.4,
                "vp9" => 0.6,
                _ => 0.8 // 默认压缩比
            };

            // 基于输出格式的调整
            var formatMultiplier = (conversionRequest.OutputFormat?.ToLower()) switch
            {
                "mp4" => 1.0,
                "mkv" => 1.05,
                "avi" => 1.1,
                "mov" => 1.02,
                "webm" => 0.9,
                _ => 1.0
            };

            // 基于分辨率的调整
            var resolutionMultiplier = (conversionRequest.Resolution?.ToLower()) switch
            {
                "4k" or "2160p" => 1.5,
                "1440p" => 1.2,
                "1080p" => 1.0,
                "720p" => 0.7,
                "480p" => 0.5,
                _ => 1.0
            };

            var estimatedSize = (long)(originalSize * compressionRatio * formatMultiplier * resolutionMultiplier);

            // 确保预估大小不会小于原文件的20%或大于原文件的150%
            var minSize = (long)(originalSize * 0.2);
            var maxSize = (long)(originalSize * 1.5);

            return Math.Max(minSize, Math.Min(maxSize, estimatedSize));
        }
    }

    /// <summary>
    /// 上传会话信息
    /// </summary>
    public class UploadSession
    {
        public string UploadId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string FileMd5 { get; set; } = string.Empty;
        public int ChunkSize { get; set; }
        public int TotalChunks { get; set; }
        public ChunkedConversionRequest ConversionRequest { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public string TempDirectory { get; set; } = string.Empty;
    }

    /// <summary>
    /// 初始化上传请求
    /// </summary>
    public class InitUploadRequest
    {
        public string UploadId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string FileMd5 { get; set; } = string.Empty;
        public ChunkedConversionRequest? ConversionRequest { get; set; }
    }

    /// <summary>
    /// 分片上传专用的转换请求（不包含VideoFile）
    /// </summary>
    public class ChunkedConversionRequest
    {
        public string? TaskName { get; set; }
        public string Preset { get; set; } = string.Empty;
        public string? OutputFormat { get; set; }
        public string? Resolution { get; set; }
        public string? VideoCodec { get; set; }
        public string? AudioCodec { get; set; }
        public string? QualityMode { get; set; }
        public string? VideoQuality { get; set; }
        public string? AudioQuality { get; set; }
        public string? FrameRate { get; set; }
        public string? HardwareAcceleration { get; set; }
        public string? CustomParameters { get; set; }
    }
}
