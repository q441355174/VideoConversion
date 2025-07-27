using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VideoConversion_Client.Utils;

namespace VideoConversion_Client.Services
{
    /// <summary>
    /// 磁盘空间API服务
    /// </summary>
    public class DiskSpaceApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public DiskSpaceApiService(string baseUrl)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        #region 配置管理

        /// <summary>
        /// 获取磁盘空间配置
        /// </summary>
        public async Task<DiskSpaceConfigResponse?> GetSpaceConfigAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/diskspace/config");
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<VideoConversion_Client.Models.ApiResponse<DiskSpaceConfigData>>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (result?.Success == true && result.Data != null)
                    {
                        return new DiskSpaceConfigResponse
                        {
                            Success = true,
                            MaxTotalSpaceGB = result.Data.MaxTotalSpaceGB,
                            ReservedSpaceGB = result.Data.ReservedSpaceGB,
                            IsEnabled = result.Data.IsEnabled,
                            UpdatedAt = result.Data.UpdatedAt,
                            UpdatedBy = result.Data.UpdatedBy
                        };
                    }
                }

                Utils.Logger.Info("DiskSpaceApi", $"获取空间配置失败: {response.StatusCode}");
                return new DiskSpaceConfigResponse { Success = false, Message = $"HTTP {response.StatusCode}" };
            }
            catch (Exception ex)
            {
                Utils.Logger.Info("DiskSpaceApi", $"获取空间配置异常: {ex.Message}");
                return new DiskSpaceConfigResponse { Success = false, Message = ex.Message };
            }
        }

        /// <summary>
        /// 设置磁盘空间配置
        /// </summary>
        public async Task<DiskSpaceConfigResponse> SetSpaceConfigAsync(double maxTotalSpaceGB, double reservedSpaceGB, bool isEnabled = true)
        {
            try
            {
                var request = new
                {
                    maxTotalSpaceGB = maxTotalSpaceGB,
                    reservedSpaceGB = reservedSpaceGB,
                    isEnabled = isEnabled
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}/api/diskspace/config", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<VideoConversion_Client.Models.ApiResponse<object>>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (result?.Success == true)
                    {
                        Utils.Logger.Info("DiskSpaceApi", $"设置空间配置成功: {maxTotalSpaceGB}GB/{reservedSpaceGB}GB");
                        return new DiskSpaceConfigResponse
                        {
                            Success = true,
                            MaxTotalSpaceGB = maxTotalSpaceGB,
                            ReservedSpaceGB = reservedSpaceGB,
                            IsEnabled = isEnabled,
                            Message = "配置更新成功"
                        };
                    }
                }

                Utils.Logger.Info("DiskSpaceApi", $"设置空间配置失败: {response.StatusCode}");
                return new DiskSpaceConfigResponse { Success = false, Message = $"HTTP {response.StatusCode}" };
            }
            catch (Exception ex)
            {
                Utils.Logger.Info("DiskSpaceApi", $"设置空间配置异常: {ex.Message}");
                return new DiskSpaceConfigResponse { Success = false, Message = ex.Message };
            }
        }

        #endregion

        #region 空间使用查询

        /// <summary>
        /// 获取磁盘空间使用情况
        /// </summary>
        public async Task<DiskSpaceUsageResponse?> GetSpaceUsageAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/diskspace/usage");
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<VideoConversion_Client.Models.ApiResponse<DiskSpaceUsageData>>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (result?.Success == true && result.Data != null)
                    {
                        return new DiskSpaceUsageResponse
                        {
                            Success = true,
                            TotalSpaceGB = result.Data.TotalSpaceGB,
                            UsedSpaceGB = result.Data.UsedSpaceGB,
                            AvailableSpaceGB = result.Data.AvailableSpaceGB,
                            ReservedSpaceGB = result.Data.ReservedSpaceGB,
                            UsagePercentage = result.Data.UsagePercentage,
                            HasSufficientSpace = result.Data.HasSufficientSpace,
                            Details = result.Data.Details,
                            UpdateTime = result.Data.UpdateTime
                        };
                    }
                }

                Utils.Logger.Info("DiskSpaceApi", $"获取空间使用情况失败: {response.StatusCode}");
                return new DiskSpaceUsageResponse { Success = false, Message = $"HTTP {response.StatusCode}" };
            }
            catch (Exception ex)
            {
                Utils.Logger.Info("DiskSpaceApi", $"获取空间使用情况异常: {ex.Message}");
                return new DiskSpaceUsageResponse { Success = false, Message = ex.Message };
            }
        }

        /// <summary>
        /// 刷新空间使用情况
        /// </summary>
        public async Task<bool> RefreshSpaceUsageAsync()
        {
            try
            {
                var response = await _httpClient.PostAsync($"{_baseUrl}/api/diskspace/refresh", null);
                
                if (response.IsSuccessStatusCode)
                {
                    Utils.Logger.Info("DiskSpaceApi", "刷新空间使用情况成功");
                    return true;
                }

                Utils.Logger.Info("DiskSpaceApi", $"刷新空间使用情况失败: {response.StatusCode}");
                return false;
            }
            catch (Exception ex)
            {
                Utils.Logger.Info("DiskSpaceApi", $"刷新空间使用情况异常: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region 空间检查

        /// <summary>
        /// 检查任务空间需求
        /// </summary>
        public async Task<SpaceCheckResponse?> CheckSpaceAsync(long originalFileSize, long? estimatedOutputSize = null, bool includeTempSpace = true)
        {
            try
            {
                var request = new
                {
                    originalFileSize = originalFileSize,
                    estimatedOutputSize = estimatedOutputSize,
                    taskType = "conversion",
                    includeTempSpace = includeTempSpace
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}/api/diskspace/check-space", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<VideoConversion_Client.Models.ApiResponse<SpaceCheckData>>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (result?.Success == true && result.Data != null)
                    {
                        return new SpaceCheckResponse
                        {
                            Success = true,
                            HasEnoughSpace = result.Data.HasEnoughSpace,
                            RequiredSpaceGB = result.Data.RequiredSpaceGB,
                            AvailableSpaceGB = result.Data.AvailableSpaceGB,
                            Message = result.Data.Message,
                            Details = result.Data.Details
                        };
                    }
                }

                Utils.Logger.Info("DiskSpaceApi", $"检查空间需求失败: {response.StatusCode}");
                return new SpaceCheckResponse { Success = false, Message = $"HTTP {response.StatusCode}" };
            }
            catch (Exception ex)
            {
                Utils.Logger.Info("DiskSpaceApi", $"检查空间需求异常: {ex.Message}");
                return new SpaceCheckResponse { Success = false, Message = ex.Message };
            }
        }

        /// <summary>
        /// 预估空间需求
        /// </summary>
        public async Task<SpaceEstimateResponse?> EstimateSpaceAsync(long originalFileSize, string? outputFormat = null, string? videoCodec = null)
        {
            try
            {
                var request = new
                {
                    originalFileSize = originalFileSize,
                    outputFormat = outputFormat,
                    videoCodec = videoCodec
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}/api/diskspace/estimate", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<VideoConversion_Client.Models.ApiResponse<SpaceEstimateData>>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (result?.Success == true && result.Data != null)
                    {
                        return new SpaceEstimateResponse
                        {
                            Success = true,
                            OriginalFileSizeGB = result.Data.OriginalFileSizeGB,
                            EstimatedOutputSizeGB = result.Data.EstimatedOutputSizeGB,
                            TotalRequiredSpaceGB = result.Data.TotalRequiredSpaceGB,
                            HasEnoughSpace = result.Data.HasEnoughSpace,
                            CompressionRatio = result.Data.CompressionRatio,
                            Message = result.Data.Message
                        };
                    }
                }

                Utils.Logger.Info("DiskSpaceApi", $"预估空间需求失败: {response.StatusCode}");
                return new SpaceEstimateResponse { Success = false, Message = $"HTTP {response.StatusCode}" };
            }
            catch (Exception ex)
            {
                Utils.Logger.Info("DiskSpaceApi", $"预估空间需求异常: {ex.Message}");
                return new SpaceEstimateResponse { Success = false, Message = ex.Message };
            }
        }

        #endregion

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    #region 数据模型

    public class DiskSpaceConfigData
    {
        public double MaxTotalSpaceGB { get; set; }
        public double ReservedSpaceGB { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }

    public class DiskSpaceConfigResponse
    {
        public bool Success { get; set; }
        public double MaxTotalSpaceGB { get; set; }
        public double ReservedSpaceGB { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public string? Message { get; set; }
    }

    public class DiskSpaceUsageData
    {
        public double TotalSpaceGB { get; set; }
        public double UsedSpaceGB { get; set; }
        public double AvailableSpaceGB { get; set; }
        public double ReservedSpaceGB { get; set; }
        public double UsagePercentage { get; set; }
        public bool HasSufficientSpace { get; set; }
        public DiskSpaceUsageDetails? Details { get; set; }
        public DateTime UpdateTime { get; set; }
    }

    public class DiskSpaceUsageDetails
    {
        public double UploadedFilesGB { get; set; }
        public double ConvertedFilesGB { get; set; }
        public double TempFilesGB { get; set; }
        public DateTime LastCalculatedAt { get; set; }
    }

    public class DiskSpaceUsageResponse
    {
        public bool Success { get; set; }
        public double TotalSpaceGB { get; set; }
        public double UsedSpaceGB { get; set; }
        public double AvailableSpaceGB { get; set; }
        public double ReservedSpaceGB { get; set; }
        public double UsagePercentage { get; set; }
        public bool HasSufficientSpace { get; set; }
        public DiskSpaceUsageDetails? Details { get; set; }
        public DateTime UpdateTime { get; set; }
        public string? Message { get; set; }
    }

    public class SpaceCheckData
    {
        public bool HasEnoughSpace { get; set; }
        public double RequiredSpaceGB { get; set; }
        public double AvailableSpaceGB { get; set; }
        public string? Message { get; set; }
        public object? Details { get; set; }
    }

    public class SpaceCheckResponse
    {
        public bool Success { get; set; }
        public bool HasEnoughSpace { get; set; }
        public double RequiredSpaceGB { get; set; }
        public double AvailableSpaceGB { get; set; }
        public string? Message { get; set; }
        public object? Details { get; set; }
    }

    public class SpaceEstimateData
    {
        public double OriginalFileSizeGB { get; set; }
        public double EstimatedOutputSizeGB { get; set; }
        public double TotalRequiredSpaceGB { get; set; }
        public bool HasEnoughSpace { get; set; }
        public double CompressionRatio { get; set; }
        public string? Message { get; set; }
    }

    public class SpaceEstimateResponse
    {
        public bool Success { get; set; }
        public double OriginalFileSizeGB { get; set; }
        public double EstimatedOutputSizeGB { get; set; }
        public double TotalRequiredSpaceGB { get; set; }
        public bool HasEnoughSpace { get; set; }
        public double CompressionRatio { get; set; }
        public string? Message { get; set; }
    }

    #endregion
}
