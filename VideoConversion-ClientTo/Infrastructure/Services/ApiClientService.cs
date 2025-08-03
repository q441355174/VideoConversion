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
            // API客户端服务初始化完成（移除日志）
        }

        #region 基础属性

        /// <summary>
        /// 服务器基础URL
        /// </summary>
        public string? BaseUrl => _baseUrl;

        #endregion

        #region 连接测试

        /// <summary>
        /// 测试服务器连接（与Client项目一致）
        /// </summary>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/health");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

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
                // 🔧 修复端点路径，与服务器端一致
                var response = await GetAsync<List<ConversionTaskDto>>("/api/task/list?status=Processing");

                // 只在失败时记录日志
                if (!response.Success)
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
            // 基础URL设置完成（移除日志）
        }

        public void SetTimeout(TimeSpan timeout)
        {
            _httpClient.Timeout = timeout;
            // 超时时间设置完成（移除日志）
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

                    // 🔧 处理服务器端的包装响应格式
                    using var document = JsonDocument.Parse(content);
                    var root = document.RootElement;

                    // 检查是否是服务器端的标准响应格式 {success: true, data: {...}}
                    if (root.TryGetProperty("success", out var successProp) && successProp.GetBoolean())
                    {
                        if (root.TryGetProperty("data", out var dataProp))
                        {
                            // 反序列化data字段
                            var data = JsonSerializer.Deserialize<T>(dataProp.GetRawText(), new JsonSerializerOptions
                            {
                                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                                PropertyNameCaseInsensitive = true
                            });

                            return ApiResponseDto<T>.CreateSuccess(data!);
                        }
                        else
                        {
                            // 没有data字段，可能是简单的成功响应
                            return ApiResponseDto<T>.CreateSuccess(default(T)!);
                        }
                    }
                    else
                    {
                        // 不是标准格式，尝试直接反序列化
                        var data = JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                            PropertyNameCaseInsensitive = true
                        });

                        return ApiResponseDto<T>.CreateSuccess(data!);
                    }
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
                // 🔧 直接调用服务器API并手动解析，因为服务器返回格式与DiskSpaceDto不匹配
                var response = await _httpClient.GetAsync("/api/diskspace/usage");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();

                    // 解析服务器返回的格式
                    using var document = JsonDocument.Parse(content);
                    var root = document.RootElement;

                    if (root.TryGetProperty("success", out var successProp) && successProp.GetBoolean() &&
                        root.TryGetProperty("data", out var dataProp))
                    {
                        // 🔧 正确解析服务器返回的完整磁盘空间数据
                        var totalSpaceGB = dataProp.TryGetProperty("totalSpaceGB", out var totalProp) ? totalProp.GetDouble() : 100.0;
                        var usedSpaceGB = dataProp.TryGetProperty("usedSpaceGB", out var usedProp) ? usedProp.GetDouble() : 0.0;
                        var availableSpaceGB = dataProp.TryGetProperty("availableSpaceGB", out var availableProp) ? availableProp.GetDouble() : totalSpaceGB;
                        var reservedSpaceGB = dataProp.TryGetProperty("reservedSpaceGB", out var reservedProp) ? reservedProp.GetDouble() : 0.0;

                        // 🔧 计算用户可用的总空间：总空间 - 保留空间
                        var userTotalSpaceGB = totalSpaceGB - reservedSpaceGB;

                        var diskSpaceDto = new DiskSpaceDto
                        {
                            TotalSpace = (long)(userTotalSpaceGB * 1024 * 1024 * 1024), // 显示给用户的总空间 = 总空间 - 保留空间
                            UsedSpace = (long)(usedSpaceGB * 1024 * 1024 * 1024), // 实际已使用空间
                            AvailableSpace = (long)(availableSpaceGB * 1024 * 1024 * 1024) // 可用空间
                        };

                        Utils.Logger.Debug("ApiClientService", $"✅ 磁盘空间获取成功: 已用{usedSpaceGB:F1}GB/用户总计{userTotalSpaceGB:F1}GB/可用{availableSpaceGB:F1}GB (物理总计{totalSpaceGB:F1}GB, 保留{reservedSpaceGB:F1}GB)");
                        return ApiResponseDto<DiskSpaceDto>.CreateSuccess(diskSpaceDto, "获取磁盘空间成功");
                    }
                    else
                    {
                        Utils.Logger.Warning("ApiClientService", "⚠️ 磁盘空间API返回格式错误");
                        return ApiResponseDto<DiskSpaceDto>.CreateError("磁盘空间API返回格式错误");
                    }
                }
                else
                {
                    Utils.Logger.Warning("ApiClientService", $"⚠️ 磁盘空间HTTP请求失败: {response.StatusCode}");
                    return ApiResponseDto<DiskSpaceDto>.CreateError($"HTTP错误: {response.StatusCode}");
                }
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
