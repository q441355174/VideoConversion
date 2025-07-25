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

        public string BaseUrl { get; set; } = "http://localhost:5065";

        public ApiService()
        {
            _httpClient = new HttpClient();
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
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
            try
            {
                var fileInfo = new FileInfo(filePath);
                const long largeFileThreshold = 100 * 1024 * 1024; // 100MB

                // 使用并发管理器控制上传并发
                var concurrencyManager = ConcurrencyManager.Instance;
                var taskId = request.TaskName ?? Guid.NewGuid().ToString();

                return await concurrencyManager.ExecuteUploadAsync(taskId, async () =>
                {
                    if (fileInfo.Length > largeFileThreshold)
                    {
                        // 大文件使用专门的上传接口
                        return await StartLargeFileConversionAsync(filePath, request, progress, cancellationToken);
                    }
                    else
                    {
                        // 小文件使用直接转换接口
                        return await StartNormalFileConversionAsync(filePath, request, cancellationToken);
                    }
                });
            }
            catch (Exception ex)
            {
                return ApiResponse<StartConversionResponse>.CreateError($"转换失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 普通文件转换 (≤100MB)
        /// </summary>
        private async Task<ApiResponse<StartConversionResponse>> StartNormalFileConversionAsync(
            string filePath,
            StartConversionRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var form = new MultipartFormDataContent();

                // 添加文件
                var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(filePath, cancellationToken));
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                form.Add(fileContent, "videoFile", Path.GetFileName(filePath));

                // 添加转换参数
                AddConversionParameters(form, request);

                var response = await _httpClient.PostAsync($"{BaseUrl}/api/conversion/start", form, cancellationToken);
                return await ProcessConversionResponse(response);
            }
            catch (Exception ex)
            {
                return new ApiResponse<StartConversionResponse>
                {
                    Success = false,
                    Message = $"网络错误: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 大文件转换 (>100MB)
        /// </summary>
        private async Task<ApiResponse<StartConversionResponse>> StartLargeFileConversionAsync(
            string filePath,
            StartConversionRequest request,
            IProgress<UploadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var form = new MultipartFormDataContent();

                // 添加文件 - 使用流式上传支持进度报告
                var fileInfo = new FileInfo(filePath);
                var progressContent = new ProgressableStreamContent(
                    new FileStream(filePath, FileMode.Open, FileAccess.Read),
                    progress,
                    fileInfo.Length);

                progressContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                form.Add(progressContent, "videoFile", Path.GetFileName(filePath));

                // 添加转换参数
                AddConversionParameters(form, request);

                var response = await _httpClient.PostAsync($"{BaseUrl}/api/upload/large-file", form, cancellationToken);
                return await ProcessConversionResponse(response);
            }
            catch (Exception ex)
            {
                return new ApiResponse<StartConversionResponse>
                {
                    Success = false,
                    Message = $"网络错误: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 添加转换参数到FormData
        /// </summary>
        private void AddConversionParameters(MultipartFormDataContent form, StartConversionRequest request)
        {
            if (!string.IsNullOrEmpty(request.TaskName))
                form.Add(new StringContent(request.TaskName), "TaskName");

            form.Add(new StringContent(request.Preset), "preset");

            if (!string.IsNullOrEmpty(request.OutputFormat))
                form.Add(new StringContent(request.OutputFormat), "OutputFormat");

            if (!string.IsNullOrEmpty(request.Resolution))
                form.Add(new StringContent(request.Resolution), "Resolution");

            if (!string.IsNullOrEmpty(request.VideoCodec))
                form.Add(new StringContent(request.VideoCodec), "VideoCodec");

            if (!string.IsNullOrEmpty(request.AudioCodec))
                form.Add(new StringContent(request.AudioCodec), "AudioCodec");

            if (!string.IsNullOrEmpty(request.VideoQuality))
                form.Add(new StringContent(request.VideoQuality), "VideoQuality");

            if (!string.IsNullOrEmpty(request.AudioQuality))
                form.Add(new StringContent(request.AudioQuality), "AudioQuality");

            if (!string.IsNullOrEmpty(request.FrameRate))
                form.Add(new StringContent(request.FrameRate), "FrameRate");
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
            const int bufferSize = 8192;
            var buffer = new byte[bufferSize];
            _bytesUploaded = 0;
            _startTime = DateTime.Now;

            int bytesRead;
            while ((bytesRead = await _content.ReadAsync(buffer, 0, bufferSize)) > 0)
            {
                await stream.WriteAsync(buffer, 0, bytesRead);
                _bytesUploaded += bytesRead;

                // 报告进度
                if (_progress != null)
                {
                    var elapsed = DateTime.Now - _startTime;
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
                }
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
}
