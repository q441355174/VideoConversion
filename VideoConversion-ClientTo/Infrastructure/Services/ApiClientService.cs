using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VideoConversion_ClientTo.Application.Interfaces;
using VideoConversion_ClientTo.Application.DTOs;

namespace VideoConversion_ClientTo.Infrastructure.Services
{
    /// <summary>
    /// STEP-7: 简化的API客户端服务实现
    /// 职责: 与服务端进行HTTP通信
    /// </summary>
    public class ApiClientService : IApiClient
    {
        private readonly HttpClient _httpClient;
        private string _baseUrl = "http://localhost:5065";

        public ApiClientService(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("VideoConversionApi");
            _httpClient.BaseAddress = new Uri(_baseUrl);
            Utils.Logger.Info("ApiClientService", "✅ API客户端服务已初始化");
        }

        #region 基础属性

        /// <summary>
        /// 服务器基础URL
        /// </summary>
        public string? BaseUrl => _baseUrl;

        #endregion

        #region 基础HTTP操作

        public async Task<ApiResponseDto<T>> GetAsync<T>(string endpoint)
        {
            try
            {
                Utils.Logger.Debug("ApiClientService", $"🌐 GET请求: {endpoint}");
                var response = await _httpClient.GetAsync(endpoint);
                return await ProcessResponseAsync<T>(response);
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ApiClientService", $"❌ GET请求失败: {endpoint}", ex);
                return ApiResponseDto<T>.CreateError($"请求失败: {ex.Message}");
            }
        }

        public async Task<ApiResponseDto<T>> PostAsync<T>(string endpoint, object? data = null)
        {
            try
            {
                Utils.Logger.Debug("ApiClientService", $"🌐 POST请求: {endpoint}");
                var content = data != null ? CreateJsonContent(data) : null;
                var response = await _httpClient.PostAsync(endpoint, content);
                return await ProcessResponseAsync<T>(response);
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ApiClientService", $"❌ POST请求失败: {endpoint}", ex);
                return ApiResponseDto<T>.CreateError($"请求失败: {ex.Message}");
            }
        }

        public async Task<ApiResponseDto<T>> PutAsync<T>(string endpoint, object? data = null)
        {
            try
            {
                Utils.Logger.Debug("ApiClientService", $"🌐 PUT请求: {endpoint}");
                var content = data != null ? CreateJsonContent(data) : null;
                var response = await _httpClient.PutAsync(endpoint, content);
                return await ProcessResponseAsync<T>(response);
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ApiClientService", $"❌ PUT请求失败: {endpoint}", ex);
                return ApiResponseDto<T>.CreateError($"请求失败: {ex.Message}");
            }
        }

        public async Task<ApiResponseDto<T>> DeleteAsync<T>(string endpoint)
        {
            try
            {
                Utils.Logger.Debug("ApiClientService", $"🌐 DELETE请求: {endpoint}");
                var response = await _httpClient.DeleteAsync(endpoint);
                return await ProcessResponseAsync<T>(response);
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ApiClientService", $"❌ DELETE请求失败: {endpoint}", ex);
                return ApiResponseDto<T>.CreateError($"请求失败: {ex.Message}");
            }
        }

        #endregion

        #region 任务相关API

        public async Task<ApiResponseDto<List<ConversionTaskDto>>> GetActiveTasksAsync()
        {
            try
            {
                Utils.Logger.Info("ApiClientService", "📋 获取活跃任务列表");

                var response = await GetAsync<List<ConversionTaskDto>>("/api/tasks/active");

                if (response.Success)
                {
                    Utils.Logger.Info("ApiClientService", $"✅ 获取活跃任务成功: {response.Data?.Count ?? 0} 个任务");
                }
                else
                {
                    Utils.Logger.Warning("ApiClientService", $"⚠️ 获取活跃任务失败: {response.Message}");
                }

                return response;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ApiClientService", $"❌ 获取活跃任务失败: {ex.Message}");
                return ApiResponseDto<List<ConversionTaskDto>>.CreateError($"获取活跃任务失败: {ex.Message}");
            }
        }

        public async Task<ApiResponseDto<List<ConversionTaskDto>>> GetCompletedTasksAsync(int page = 1, int pageSize = 50)
        {
            var endpoint = $"/api/conversion/completed?page={page}&pageSize={pageSize}";
            return await GetAsync<List<ConversionTaskDto>>(endpoint);
        }

        public async Task<ApiResponseDto<ConversionTaskDto>> GetTaskAsync(string taskId)
        {
            var endpoint = $"/api/conversion/task/{taskId}";
            return await GetAsync<ConversionTaskDto>(endpoint);
        }

        public async Task<ApiResponseDto<StartConversionResponseDto>> StartConversionAsync(StartConversionRequestDto request)
        {
            var endpoint = "/api/conversion/start";
            return await PostAsync<StartConversionResponseDto>(endpoint, request);
        }

        public async Task<ApiResponseDto<object>> CancelTaskAsync(string taskId)
        {
            var endpoint = $"/api/conversion/cancel/{taskId}";
            return await PostAsync<object>(endpoint);
        }

        public async Task<ApiResponseDto<object>> DeleteTaskAsync(string taskId)
        {
            var endpoint = $"/api/conversion/task/{taskId}";
            return await DeleteAsync<object>(endpoint);
        }

        #endregion

        #region 文件操作API

        public async Task<ApiResponseDto<string>> UploadFileAsync(string filePath, IProgress<UploadProgressDto>? progress = null)
        {
            try
            {
                Utils.Logger.Info("ApiClientService", $"📤 开始上传文件: {filePath}");
                
                // 简化实现，实际应该实现分片上传和进度报告
                using var form = new MultipartFormDataContent();
                using var fileContent = new ByteArrayContent(await System.IO.File.ReadAllBytesAsync(filePath));
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                form.Add(fileContent, "file", System.IO.Path.GetFileName(filePath));

                var response = await _httpClient.PostAsync("/api/conversion/upload", form);
                var result = await ProcessResponseAsync<string>(response);
                
                if (result.Success)
                {
                    Utils.Logger.Info("ApiClientService", $"✅ 文件上传成功: {filePath}");
                }
                
                return result;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ApiClientService", $"❌ 文件上传失败: {filePath}", ex);
                return ApiResponseDto<string>.CreateError($"上传失败: {ex.Message}");
            }
        }

        public async Task<ApiResponseDto<string>> DownloadFileAsync(string taskId)
        {
            var endpoint = $"/api/conversion/download/{taskId}";
            return await GetAsync<string>(endpoint);
        }

        public async Task<ApiResponseDto<SpaceCheckResponseDto>> CheckSpaceAsync(long requiredBytes)
        {
            var endpoint = $"/api/system/space-check?requiredBytes={requiredBytes}";
            return await GetAsync<SpaceCheckResponseDto>(endpoint);
        }

        #endregion

        #region 配置

        public void SetBaseUrl(string baseUrl)
        {
            _baseUrl = baseUrl;
            _httpClient.BaseAddress = new Uri(baseUrl);
            Utils.Logger.Info("ApiClientService", $"🔧 基础URL已设置: {baseUrl}");
        }

        public void SetTimeout(TimeSpan timeout)
        {
            _httpClient.Timeout = timeout;
            Utils.Logger.Info("ApiClientService", $"🔧 超时时间已设置: {timeout}");
        }

        public void SetHeader(string name, string value)
        {
            _httpClient.DefaultRequestHeaders.Remove(name);
            _httpClient.DefaultRequestHeaders.Add(name, value);
            Utils.Logger.Debug("ApiClientService", $"🔧 请求头已设置: {name} = {value}");
        }

        #endregion

        #region 私有方法

        private StringContent CreateJsonContent(object data)
        {
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            return new StringContent(json, Encoding.UTF8, "application/json");
        }

        private async Task<ApiResponseDto<T>> ProcessResponseAsync<T>(HttpResponseMessage response)
        {
            try
            {
                var content = await response.Content.ReadAsStringAsync();
                
                if (response.IsSuccessStatusCode)
                {
                    if (typeof(T) == typeof(string))
                    {
                        return ApiResponseDto<T>.CreateSuccess((T)(object)content);
                    }
                    
                    var data = JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        PropertyNameCaseInsensitive = true
                    });
                    
                    return ApiResponseDto<T>.CreateSuccess(data!);
                }
                else
                {
                    Utils.Logger.Warning("ApiClientService", $"⚠️ API请求失败: {response.StatusCode} - {content}");
                    return ApiResponseDto<T>.CreateError($"请求失败: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ApiClientService", $"❌ 处理响应失败: {ex.Message}");
                return ApiResponseDto<T>.CreateError($"处理响应失败: {ex.Message}");
            }
        }

        public async Task<ApiResponseDto<DiskSpaceDto>> GetDiskSpaceAsync()
        {
            try
            {
                Utils.Logger.Info("ApiClientService", "📊 获取磁盘空间信息");

                var response = await GetAsync<DiskSpaceDto>("/api/space/info");

                if (response.Success)
                {
                    Utils.Logger.Info("ApiClientService", "✅ 磁盘空间信息获取成功");
                }
                else
                {
                    Utils.Logger.Warning("ApiClientService", $"⚠️ 磁盘空间信息获取失败: {response.Message}");
                }

                return response;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ApiClientService", $"❌ 获取磁盘空间信息失败: {ex.Message}");
                return ApiResponseDto<DiskSpaceDto>.CreateError($"获取磁盘空间信息失败: {ex.Message}");
            }
        }

        #endregion

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
