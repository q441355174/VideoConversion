using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
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
        /// 开始转换任务
        /// </summary>
        public async Task<ApiResponse<StartConversionResponse>> StartConversionAsync(string filePath, StartConversionRequest request)
        {
            try
            {
                using var form = new MultipartFormDataContent();
                
                // 添加文件
                var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(filePath));
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                form.Add(fileContent, "VideoFile", Path.GetFileName(filePath));

                // 添加其他参数
                if (!string.IsNullOrEmpty(request.TaskName))
                    form.Add(new StringContent(request.TaskName), "TaskName");
                
                form.Add(new StringContent(request.Preset), "Preset");
                
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

                var response = await _httpClient.PostAsync($"{BaseUrl}/api/conversion/start", form);
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
        /// 下载文件
        /// </summary>
        public async Task<ApiResponse<string>> DownloadFileAsync(string taskId, string savePath)
        {
            try
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
}
