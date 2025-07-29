using VideoConversion.Models;
using Microsoft.AspNetCore.SignalR;
using VideoConversion.Hubs;
using SqlSugar;

namespace VideoConversion.Services
{
    /// <summary>
    /// 磁盘空间管理服务
    /// </summary>
    public class DiskSpaceService
    {
        private readonly DatabaseService _databaseService;
        private readonly IHubContext<ConversionHub> _hubContext;
        private readonly ILogger<DiskSpaceService> _logger;
        private readonly Timer _spaceMonitorTimer;
        private readonly SemaphoreSlim _spaceCalculationSemaphore = new(1, 1);
        private readonly SemaphoreSlim _databaseAccessSemaphore = new(1, 1);

        // 默认配置
        private const long DEFAULT_MAX_SPACE = 100L * 1024 * 1024 * 1024; // 100GB
        private const long DEFAULT_RESERVED_SPACE = 5L * 1024 * 1024 * 1024; // 5GB

        public DiskSpaceService(
            DatabaseService databaseService,
            IHubContext<ConversionHub> hubContext,
            ILogger<DiskSpaceService> logger)
        {
            _databaseService = databaseService;
            _hubContext = hubContext;
            _logger = logger;

            // 延迟启动定时监控，给数据库初始化时间
            _spaceMonitorTimer = new Timer(async _ => await MonitorDiskSpace(),
                null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30));

           // _logger.LogInformation("DiskSpaceService 初始化完成");
        }

        #region 配置管理

        /// <summary>
        /// 获取磁盘空间配置
        /// </summary>
        public async Task<DiskSpaceConfig> GetSpaceConfigAsync()
        {
            await _databaseAccessSemaphore.WaitAsync();
            try
            {
                var config = await Task.Run(() =>
                {
                    try
                    {
                        var db = _databaseService.GetDatabaseAsync();
                        return db.Queryable<DiskSpaceConfig>()
                            .OrderBy(c => c.Id, OrderByType.Desc)
                            .First();
                    }
                    catch (Exception queryEx)
                    {
                        _logger.LogWarning(queryEx, "查询磁盘空间配置失败，将创建默认配置");
                        return null;
                    }
                });

                if (config == null)
                {
                    // 创建默认配置
                    config = new DiskSpaceConfig
                    {
                        MaxTotalSpace = DEFAULT_MAX_SPACE,
                        ReservedSpace = DEFAULT_RESERVED_SPACE,
                        IsEnabled = true,
                        UpdatedAt = DateTime.Now,
                        UpdatedBy = "System"
                    };

                    try
                    {
                        await SetSpaceConfigAsync(config);
                    }
                    catch (Exception setEx)
                    {
                        _logger.LogError(setEx, "设置默认磁盘空间配置失败");
                        // 即使设置失败，也返回默认配置
                    }
                }

                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取磁盘空间配置失败");

                // 返回默认配置
                return new DiskSpaceConfig
                {
                    MaxTotalSpace = DEFAULT_MAX_SPACE,
                    ReservedSpace = DEFAULT_RESERVED_SPACE,
                    IsEnabled = true,
                    UpdatedAt = DateTime.Now,
                    UpdatedBy = "System"
                };
            }
            finally
            {
                _databaseAccessSemaphore.Release();
            }
        }

        /// <summary>
        /// 设置磁盘空间配置
        /// </summary>
        public async Task<bool> SetSpaceConfigAsync(DiskSpaceConfig config)
        {
            await _databaseAccessSemaphore.WaitAsync();
            try
            {
                config.UpdatedAt = DateTime.Now;

                // 验证配置
                if (!ValidateSpaceConfig(config))
                {
                    _logger.LogWarning("磁盘空间配置验证失败: MaxSpace={MaxSpace}, ReservedSpace={ReservedSpace}",
                        config.MaxTotalSpace, config.ReservedSpace);
                    return false;
                }

                var result = await Task.Run(() =>
                {
                    try
                    {
                        var db = _databaseService.GetDatabaseAsync();

                        // 删除旧配置
                        db.Deleteable<DiskSpaceConfig>().ExecuteCommand();

                        // 插入新配置
                        return db.Insertable(config).ExecuteCommand();
                    }
                    catch (Exception dbEx)
                    {
                        _logger.LogWarning(dbEx, "数据库操作失败");
                        return 0;
                    }
                });
                
                if (result > 0)
                {
                    _logger.LogInformation("磁盘空间配置已更新: MaxSpace={MaxSpaceGB}GB, ReservedSpace={ReservedSpaceGB}GB", 
                        config.MaxTotalSpace / 1024.0 / 1024 / 1024, 
                        config.ReservedSpace / 1024.0 / 1024 / 1024);
                    
                    // 通知配置变更
                    await NotifySpaceConfigChanged(config);
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "设置磁盘空间配置失败");
                return false;
            }
            finally
            {
                _databaseAccessSemaphore.Release();
            }
        }

        /// <summary>
        /// 验证空间配置
        /// </summary>
        private bool ValidateSpaceConfig(DiskSpaceConfig config)
        {
            // 最大空间必须大于保留空间
            if (config.MaxTotalSpace <= config.ReservedSpace)
                return false;
            
            // 最大空间不能小于1GB
            if (config.MaxTotalSpace < 1024L * 1024 * 1024)
                return false;
            
            // 保留空间不能小于1GB
            if (config.ReservedSpace < 1024L * 1024 * 1024)
                return false;
            
            return true;
        }

        #endregion

        #region 空间计算

        /// <summary>
        /// 计算当前空间使用情况
        /// </summary>
        public async Task<SpaceUsage> CalculateCurrentUsageAsync()
        {
            await _spaceCalculationSemaphore.WaitAsync();
            try
            {
                var usage = new SpaceUsage();

                // 计算上传文件大小
                usage.UploadedFilesSize = await CalculateDirectorySize("uploads");
                
                // 计算转换文件大小
                usage.ConvertedFilesSize = await CalculateDirectorySize("outputs");
                
                // 计算临时文件大小
                usage.TempFilesSize = await CalculateDirectorySize("temp");
                
                usage.LastCalculatedAt = DateTime.Now;

                // 更新数据库
                await UpdateSpaceUsageInDatabase(usage);

                _logger.LogDebug("空间使用计算完成: 上传={UploadedMB}MB, 转换={ConvertedMB}MB, 临时={TempMB}MB", 
                    usage.UploadedFilesSize / 1024 / 1024,
                    usage.ConvertedFilesSize / 1024 / 1024,
                    usage.TempFilesSize / 1024 / 1024);

                return usage;
            }
            finally
            {
                _spaceCalculationSemaphore.Release();
            }
        }

        /// <summary>
        /// 计算目录大小
        /// </summary>
        private async Task<long> CalculateDirectorySize(string directoryPath)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                    return 0;

                return await Task.Run(() =>
                {
                    var dirInfo = new DirectoryInfo(directoryPath);
                    return dirInfo.EnumerateFiles("*", SearchOption.AllDirectories)
                                  .Sum(file => file.Length);
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "计算目录大小失败: {DirectoryPath}", directoryPath);
                return 0;
            }
        }

        /// <summary>
        /// 更新数据库中的空间使用情况
        /// </summary>
        private async Task UpdateSpaceUsageInDatabase(SpaceUsage usage)
        {
            await _databaseAccessSemaphore.WaitAsync();
            try
            {
                await Task.Run(() =>
                {
                    try
                    {
                        var db = _databaseService.GetDatabaseAsync();

                        // 删除旧记录
                        db.Deleteable<SpaceUsage>().ExecuteCommand();

                        // 插入新记录
                        db.Insertable(usage).ExecuteCommand();
                    }
                    catch (Exception dbEx)
                    {
                        _logger.LogWarning(dbEx, "数据库操作失败");
                        throw;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新空间使用数据库失败");
            }
            finally
            {
                _databaseAccessSemaphore.Release();
            }
        }

        #endregion

        #region 空间检查

        /// <summary>
        /// 检查空间是否足够
        /// </summary>
        public async Task<SpaceCheckResponse> CheckSpaceAsync(SpaceCheckRequest request)
        {
            try
            {
                var config = await GetSpaceConfigAsync();
                var usage = await CalculateCurrentUsageAsync();
                
                // 如果未启用空间限制，直接返回成功
                if (!config.IsEnabled)
                {
                    return new SpaceCheckResponse
                    {
                        HasEnoughSpace = true,
                        RequiredSpace = request.OriginalFileSize + (request.EstimatedOutputSize ?? 0),
                        AvailableSpace = long.MaxValue,
                        Message = "空间限制已禁用"
                    };
                }

                // 计算所需空间
                var requiredSpace = CalculateRequiredSpace(request);
                
                // 计算可用空间
                var availableSpace = config.MaxTotalSpace - usage.TotalUsedSpace - config.ReservedSpace;
                
                var hasEnoughSpace = availableSpace >= requiredSpace;
                
                var response = new SpaceCheckResponse
                {
                    HasEnoughSpace = hasEnoughSpace,
                    RequiredSpace = requiredSpace,
                    AvailableSpace = Math.Max(0, availableSpace),
                    Message = hasEnoughSpace ? "空间充足" : "空间不足",
                    Details = new SpaceCheckDetails
                    {
                        OriginalFileSpace = request.OriginalFileSize,
                        OutputFileSpace = request.EstimatedOutputSize ?? 0,
                        TempFileSpace = request.IncludeTempSpace ? request.OriginalFileSize / 10 : 0, // 临时文件约为原文件的10%
                        ReservedSpace = config.ReservedSpace,
                        CurrentUsedSpace = usage.TotalUsedSpace,
                        TotalConfiguredSpace = config.MaxTotalSpace
                    }
                };

                _logger.LogInformation("空间检查完成: 需要={RequiredMB}MB, 可用={AvailableMB}MB, 结果={Result}", 
                    requiredSpace / 1024 / 1024, 
                    availableSpace / 1024 / 1024, 
                    hasEnoughSpace ? "充足" : "不足");

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "空间检查失败");
                return new SpaceCheckResponse
                {
                    HasEnoughSpace = false,
                    Message = $"空间检查失败: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 计算所需空间
        /// </summary>
        private long CalculateRequiredSpace(SpaceCheckRequest request)
        {
            var requiredSpace = request.OriginalFileSize;
            
            // 添加预估输出文件大小
            if (request.EstimatedOutputSize.HasValue)
            {
                requiredSpace += request.EstimatedOutputSize.Value;
            }
            else
            {
                // 如果没有提供预估大小，使用默认压缩比（70%）
                requiredSpace += (long)(request.OriginalFileSize * 0.7);
            }
            
            // 添加临时文件空间（约为原文件的10%）
            if (request.IncludeTempSpace)
            {
                requiredSpace += request.OriginalFileSize / 10;
            }
            
            return requiredSpace;
        }

        #endregion

        #region 空间监控

        /// <summary>
        /// 监控磁盘空间
        /// </summary>
        private async Task MonitorDiskSpace()
        {
            try
            {
                var status = await GetCurrentSpaceStatusAsync();
                
                // 通过SignalR广播空间状态
                await _hubContext.Clients.Group("space_monitoring")
                    .SendAsync("DiskSpaceUpdate", status);
                
                // 检查空间警告
                if (status.UsagePercentage > 90)
                {
                    _logger.LogWarning("磁盘空间严重不足: {UsagePercentage:F1}%", status.UsagePercentage);
                    await NotifySpaceWarning(status, "磁盘空间严重不足");
                }
                else if (status.UsagePercentage > 80)
                {
                    _logger.LogWarning("磁盘空间不足: {UsagePercentage:F1}%", status.UsagePercentage);
                    await NotifySpaceWarning(status, "磁盘空间不足");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "监控磁盘空间失败");
            }
        }

        /// <summary>
        /// 获取当前空间状态
        /// </summary>
        public async Task<DiskSpaceStatus> GetCurrentSpaceStatusAsync()
        {
            try
            {
                var config = await GetSpaceConfigAsync();
                var usage = await CalculateCurrentUsageAsync();

                var availableSpace = Math.Max(0, config.MaxTotalSpace - usage.TotalUsedSpace - config.ReservedSpace);

                return new DiskSpaceStatus
                {
                    TotalSpace = config.MaxTotalSpace,
                    UsedSpace = usage.TotalUsedSpace,
                    AvailableSpace = availableSpace,
                    ReservedSpace = config.ReservedSpace,
                    HasSufficientSpace = availableSpace > config.ReservedSpace,
                    MinRequiredSpace = config.ReservedSpace,
                    UpdateTime = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取当前空间状态失败");

                // 返回默认状态，避免系统崩溃
                return new DiskSpaceStatus
                {
                    TotalSpace = DEFAULT_MAX_SPACE,
                    UsedSpace = 0,
                    AvailableSpace = DEFAULT_MAX_SPACE - DEFAULT_RESERVED_SPACE,
                    ReservedSpace = DEFAULT_RESERVED_SPACE,
                    HasSufficientSpace = true,
                    MinRequiredSpace = DEFAULT_RESERVED_SPACE,
                    UpdateTime = DateTime.Now
                };
            }
        }

        #endregion

        #region 通知方法

        /// <summary>
        /// 通知空间配置变更
        /// </summary>
        private async Task NotifySpaceConfigChanged(DiskSpaceConfig config)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("SpaceConfigChanged", new
                {
                    MaxTotalSpaceGB = config.MaxTotalSpace / 1024.0 / 1024 / 1024,
                    ReservedSpaceGB = config.ReservedSpace / 1024.0 / 1024 / 1024,
                    IsEnabled = config.IsEnabled,
                    UpdatedAt = config.UpdatedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "通知空间配置变更失败");
            }
        }

        /// <summary>
        /// 通知空间警告
        /// </summary>
        private async Task NotifySpaceWarning(DiskSpaceStatus status, string message)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("SpaceWarning", new
                {
                    Message = message,
                    UsagePercentage = status.UsagePercentage,
                    AvailableSpaceGB = status.AvailableSpace / 1024.0 / 1024 / 1024,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "通知空间警告失败");
            }
        }

        #endregion

        #region 空间使用更新

        /// <summary>
        /// 更新空间使用情况
        /// </summary>
        public async Task UpdateSpaceUsage(long sizeChange, SpaceCategory category)
        {
            try
            {
                await _spaceCalculationSemaphore.WaitAsync();

                var usage = await GetCurrentSpaceUsageFromDatabase();
                if (usage == null)
                {
                    usage = new SpaceUsage();
                }

                // 根据类别更新对应的空间使用
                switch (category)
                {
                    case SpaceCategory.OriginalFiles:
                        usage.UploadedFilesSize = Math.Max(0, usage.UploadedFilesSize + sizeChange);
                        break;
                    case SpaceCategory.ConvertedFiles:
                        usage.ConvertedFilesSize = Math.Max(0, usage.ConvertedFilesSize + sizeChange);
                        break;
                    case SpaceCategory.TempFiles:
                        usage.TempFilesSize = Math.Max(0, usage.TempFilesSize + sizeChange);
                        break;
                }

                usage.LastCalculatedAt = DateTime.Now;
                await UpdateSpaceUsageInDatabase(usage);

                _logger.LogDebug("空间使用更新: {Category} {ChangeType}{ChangeMB}MB, 总使用={TotalMB}MB",
                    category,
                    sizeChange >= 0 ? "+" : "",
                    sizeChange / 1024.0 / 1024,
                    usage.TotalUsedSpace / 1024.0 / 1024);
            }
            finally
            {
                _spaceCalculationSemaphore.Release();
            }
        }

        /// <summary>
        /// 从数据库获取当前空间使用情况
        /// </summary>
        private async Task<SpaceUsage?> GetCurrentSpaceUsageFromDatabase()
        {
            try
            {
                var sql = "SELECT * FROM SpaceUsage ORDER BY Id DESC LIMIT 1";
                return await _databaseService.GetDatabaseAsync().Ado.SqlQuerySingleAsync<SpaceUsage>(sql);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "从数据库获取空间使用情况失败");
                return null;
            }
        }

        #endregion

        public void Dispose()
        {
            _spaceMonitorTimer?.Dispose();
            _spaceCalculationSemaphore?.Dispose();
        }
    }
}
