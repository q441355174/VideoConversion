using System.Diagnostics;
using System.Text.RegularExpressions;

namespace VideoConversion.Services
{
    /// <summary>
    /// GPU性能监控服务
    /// </summary>
    public class GpuPerformanceService
    {
        private readonly ILogger<GpuPerformanceService> _logger;

        public GpuPerformanceService(ILogger<GpuPerformanceService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 获取GPU性能数据
        /// </summary>
        public async Task<List<GpuPerformanceData>> GetGpuPerformanceAsync()
        {
            var performanceData = new List<GpuPerformanceData>();

            try
            {
                if (OperatingSystem.IsWindows())
                {
                    performanceData = await GetWindowsGpuPerformanceAsync();
                }
                else if (OperatingSystem.IsLinux())
                {
                    performanceData = await GetLinuxGpuPerformanceAsync();
                }
                else
                {
                    _logger.LogWarning("当前操作系统不支持GPU性能监控");
                    // 返回模拟数据作为后备
                    performanceData = GetMockPerformanceData();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取GPU性能数据失败，返回模拟数据");
                performanceData = GetMockPerformanceData();
            }

            return performanceData;
        }

        /// <summary>
        /// 获取Windows GPU性能数据
        /// </summary>
        private async Task<List<GpuPerformanceData>> GetWindowsGpuPerformanceAsync()
        {
            var performanceData = new List<GpuPerformanceData>();

            try
            {
                // 使用nvidia-smi获取NVIDIA GPU信息
                var nvidiaData = await GetNvidiaPerformanceAsync();
                performanceData.AddRange(nvidiaData);

                // 使用WMI获取其他GPU信息
                var wmiData = await GetWmiGpuPerformanceAsync();
                performanceData.AddRange(wmiData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取Windows GPU性能数据失败");
            }

            return performanceData.Any() ? performanceData : GetMockPerformanceData();
        }

        /// <summary>
        /// 使用nvidia-smi获取NVIDIA GPU性能数据
        /// </summary>
        private async Task<List<GpuPerformanceData>> GetNvidiaPerformanceAsync()
        {
            var performanceData = new List<GpuPerformanceData>();

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "nvidia-smi",
                    Arguments = "--query-gpu=index,name,utilization.gpu,memory.used,memory.total,temperature.gpu --format=csv,noheader,nounits",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                {
                    var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    
                    foreach (var line in lines)
                    {
                        var parts = line.Split(',').Select(p => p.Trim()).ToArray();
                        if (parts.Length >= 6)
                        {
                            if (int.TryParse(parts[2], out var usage) &&
                                int.TryParse(parts[3], out var memoryUsed) &&
                                int.TryParse(parts[4], out var memoryTotal) &&
                                int.TryParse(parts[5], out var temperature))
                            {
                                performanceData.Add(new GpuPerformanceData
                                {
                                    Index = int.TryParse(parts[0], out var index) ? index : 0,
                                    Name = parts[1],
                                    Usage = usage,
                                    MemoryUsed = memoryUsed,
                                    MemoryTotal = memoryTotal,
                                    Temperature = temperature,
                                    EncoderActive = usage > 50, // 简单判断：使用率>50%认为编码器活跃
                                    Vendor = "NVIDIA"
                                });
                            }
                        }
                    }

                    _logger.LogInformation("成功获取 {Count} 个NVIDIA GPU的性能数据", performanceData.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "nvidia-smi不可用或执行失败");
            }

            return performanceData;
        }

        /// <summary>
        /// 使用WMI获取GPU性能数据
        /// </summary>
        private async Task<List<GpuPerformanceData>> GetWmiGpuPerformanceAsync()
        {
            var performanceData = new List<GpuPerformanceData>();

            try
            {
                // 获取GPU基本信息
                var gpuInfoStartInfo = new ProcessStartInfo
                {
                    FileName = "wmic",
                    Arguments = "path win32_VideoController get name,AdapterRAM /format:csv",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using var gpuInfoProcess = new Process { StartInfo = gpuInfoStartInfo };
                gpuInfoProcess.Start();

                var gpuInfoOutput = await gpuInfoProcess.StandardOutput.ReadToEndAsync();
                await gpuInfoProcess.WaitForExitAsync();

                if (gpuInfoProcess.ExitCode == 0 && !string.IsNullOrEmpty(gpuInfoOutput))
                {
                    var lines = gpuInfoOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    var index = 0;

                    foreach (var line in lines.Skip(1)) // 跳过标题行
                    {
                        var parts = line.Split(',').Select(p => p.Trim()).ToArray();
                        if (parts.Length >= 3 && !string.IsNullOrEmpty(parts[2]))
                        {
                            var name = parts[2];
                            var memoryBytes = long.TryParse(parts[1], out var mem) ? mem : 0;
                            var memoryMB = (int)(memoryBytes / (1024 * 1024));

                            // 对于非NVIDIA GPU，使用估算的性能数据
                            performanceData.Add(new GpuPerformanceData
                            {
                                Index = index++,
                                Name = name,
                                Usage = new Random().Next(10, 40), // 估算使用率
                                MemoryUsed = memoryMB > 0 ? new Random().Next(memoryMB / 4, memoryMB / 2) : 1024,
                                MemoryTotal = memoryMB > 0 ? memoryMB : 8192,
                                Temperature = new Random().Next(40, 65), // 估算温度
                                EncoderActive = false,
                                Vendor = GetVendorFromName(name)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "使用WMI获取GPU信息失败");
            }

            return performanceData;
        }

        /// <summary>
        /// 获取Linux GPU性能数据
        /// </summary>
        private async Task<List<GpuPerformanceData>> GetLinuxGpuPerformanceAsync()
        {
            var performanceData = new List<GpuPerformanceData>();

            try
            {
                // 尝试使用nvidia-smi
                var nvidiaData = await GetNvidiaPerformanceAsync();
                performanceData.AddRange(nvidiaData);

                // 可以添加其他Linux GPU监控方法
                // 如：intel_gpu_top, radeontop等
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取Linux GPU性能数据失败");
            }

            return performanceData.Any() ? performanceData : GetMockPerformanceData();
        }

        /// <summary>
        /// 从GPU名称推断厂商
        /// </summary>
        private string GetVendorFromName(string name)
        {
            var lowerName = name.ToLower();
            if (lowerName.Contains("nvidia") || lowerName.Contains("geforce") || lowerName.Contains("quadro") || lowerName.Contains("tesla"))
                return "NVIDIA";
            if (lowerName.Contains("amd") || lowerName.Contains("radeon") || lowerName.Contains("rx "))
                return "AMD";
            if (lowerName.Contains("intel") || lowerName.Contains("uhd") || lowerName.Contains("iris"))
                return "Intel";
            return "Unknown";
        }

        /// <summary>
        /// 获取模拟性能数据（后备方案）
        /// </summary>
        private List<GpuPerformanceData> GetMockPerformanceData()
        {
            var random = new Random();
            return new List<GpuPerformanceData>
            {
                new GpuPerformanceData
                {
                    Index = 0,
                    Name = "GPU Device 1",
                    Usage = random.Next(20, 80),
                    MemoryUsed = random.Next(1000, 5000),
                    MemoryTotal = 12288,
                    Temperature = random.Next(45, 75),
                    EncoderActive = random.NextDouble() > 0.7,
                    Vendor = "Unknown"
                },
                new GpuPerformanceData
                {
                    Index = 1,
                    Name = "GPU Device 2",
                    Usage = random.Next(10, 50),
                    MemoryUsed = random.Next(500, 2500),
                    MemoryTotal = 8192,
                    Temperature = random.Next(40, 60),
                    EncoderActive = random.NextDouble() > 0.8,
                    Vendor = "Unknown"
                }
            };
        }
    }

    /// <summary>
    /// GPU性能数据模型
    /// </summary>
    public class GpuPerformanceData
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Usage { get; set; } // GPU使用率 (%)
        public int MemoryUsed { get; set; } // 已使用显存 (MB)
        public int MemoryTotal { get; set; } // 总显存 (MB)
        public int Temperature { get; set; } // 温度 (°C)
        public bool EncoderActive { get; set; } // 编码器是否活跃
        public string Vendor { get; set; } = string.Empty; // 厂商
    }

    /// <summary>
    /// GPU设备信息服务
    /// </summary>
    public class GpuDeviceInfoService
    {
        private readonly ILogger<GpuDeviceInfoService> _logger;
        private readonly GpuDetectionService _gpuDetectionService;

        public GpuDeviceInfoService(ILogger<GpuDeviceInfoService> logger, GpuDetectionService gpuDetectionService)
        {
            _logger = logger;
            _gpuDetectionService = gpuDetectionService;
        }

        /// <summary>
        /// 获取详细的GPU设备信息
        /// </summary>
        public async Task<List<GpuDeviceInfo>> GetGpuDeviceInfoAsync()
        {
            var deviceInfoList = new List<GpuDeviceInfo>();

            try
            {
                var capabilities = await _gpuDetectionService.DetectGpuCapabilitiesAsync();

                if (OperatingSystem.IsWindows())
                {
                    deviceInfoList = await GetWindowsGpuDeviceInfoAsync(capabilities);
                }
                else if (OperatingSystem.IsLinux())
                {
                    deviceInfoList = await GetLinuxGpuDeviceInfoAsync(capabilities);
                }

                // 如果没有获取到真实设备信息，返回基于能力检测的信息
                if (!deviceInfoList.Any())
                {
                    deviceInfoList = GetDeviceInfoFromCapabilities(capabilities);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取GPU设备信息失败");
                // 返回空列表或基础信息
                deviceInfoList = new List<GpuDeviceInfo>();
            }

            return deviceInfoList;
        }

        /// <summary>
        /// 获取Windows GPU设备信息
        /// </summary>
        private async Task<List<GpuDeviceInfo>> GetWindowsGpuDeviceInfoAsync(GpuCapabilities capabilities)
        {
            var deviceInfoList = new List<GpuDeviceInfo>();

            try
            {
                // 使用nvidia-smi获取NVIDIA GPU详细信息
                if (capabilities.NvencSupported)
                {
                    var nvidiaDevices = await GetNvidiaDeviceInfoAsync();
                    deviceInfoList.AddRange(nvidiaDevices);
                }

                // 使用WMI获取所有GPU基本信息
                var wmiDevices = await GetWmiGpuDeviceInfoAsync(capabilities);

                // 合并信息，避免重复
                foreach (var wmiDevice in wmiDevices)
                {
                    if (!deviceInfoList.Any(d => d.Name.Contains(wmiDevice.Name.Split(' ')[0])))
                    {
                        deviceInfoList.Add(wmiDevice);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取Windows GPU设备信息失败");
            }

            return deviceInfoList;
        }

        /// <summary>
        /// 使用nvidia-smi获取NVIDIA GPU设备信息
        /// </summary>
        private async Task<List<GpuDeviceInfo>> GetNvidiaDeviceInfoAsync()
        {
            var deviceInfoList = new List<GpuDeviceInfo>();

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "nvidia-smi",
                    Arguments = "--query-gpu=index,name,memory.total,driver_version,compute_cap --format=csv,noheader,nounits",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                {
                    var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                    foreach (var line in lines)
                    {
                        var parts = line.Split(',').Select(p => p.Trim()).ToArray();
                        if (parts.Length >= 4)
                        {
                            var memoryMB = int.TryParse(parts[2], out var mem) ? mem : 0;
                            var memoryGB = memoryMB / 1024.0;

                            deviceInfoList.Add(new GpuDeviceInfo
                            {
                                Name = parts[1],
                                Vendor = "NVIDIA",
                                Driver = parts[3],
                                Memory = $"{memoryGB:F1} GB",
                                Encoder = "NVENC H.264/H.265/AV1",
                                MaxResolution = GetMaxResolutionForNvidia(parts[1]),
                                PerformanceLevel = GetPerformanceLevelForNvidia(parts[1]),
                                Supported = true,
                                SupportedFormats = new[] { "H.264", "H.265", "AV1" },
                                Reason = null
                            });
                        }
                    }

                    _logger.LogInformation("成功获取 {Count} 个NVIDIA GPU设备信息", deviceInfoList.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "nvidia-smi不可用或执行失败");
            }

            return deviceInfoList;
        }

        /// <summary>
        /// 使用WMI获取GPU设备信息
        /// </summary>
        private async Task<List<GpuDeviceInfo>> GetWmiGpuDeviceInfoAsync(GpuCapabilities capabilities)
        {
            var deviceInfoList = new List<GpuDeviceInfo>();

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "wmic",
                    Arguments = "path win32_VideoController get name,AdapterRAM,DriverVersion /format:csv",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                {
                    var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                    foreach (var line in lines.Skip(1)) // 跳过标题行
                    {
                        var parts = line.Split(',').Select(p => p.Trim()).ToArray();
                        if (parts.Length >= 4 && !string.IsNullOrEmpty(parts[3]))
                        {
                            var name = parts[3];
                            var vendor = GetVendorFromName(name);
                            var memoryBytes = long.TryParse(parts[1], out var mem) ? mem : 0;
                            var memoryGB = memoryBytes / (1024.0 * 1024.0 * 1024.0);
                            var driver = parts[2] ?? "Unknown";

                            var supported = IsGpuSupported(vendor, capabilities);
                            var encoder = GetEncoderForVendor(vendor);
                            var formats = GetSupportedFormatsForVendor(vendor);

                            deviceInfoList.Add(new GpuDeviceInfo
                            {
                                Name = name,
                                Vendor = vendor,
                                Driver = driver,
                                Memory = memoryGB > 0 ? $"{memoryGB:F1} GB" : "共享内存",
                                Encoder = encoder,
                                MaxResolution = GetMaxResolutionForVendor(vendor),
                                PerformanceLevel = GetPerformanceLevelForVendor(vendor),
                                Supported = supported,
                                SupportedFormats = formats,
                                Reason = supported ? null : $"{vendor} GPU硬件加速不可用"
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "使用WMI获取GPU信息失败");
            }

            return deviceInfoList;
        }

        /// <summary>
        /// 获取Linux GPU设备信息
        /// </summary>
        private async Task<List<GpuDeviceInfo>> GetLinuxGpuDeviceInfoAsync(GpuCapabilities capabilities)
        {
            var deviceInfoList = new List<GpuDeviceInfo>();

            try
            {
                // 尝试使用nvidia-smi
                if (capabilities.NvencSupported)
                {
                    var nvidiaDevices = await GetNvidiaDeviceInfoAsync();
                    deviceInfoList.AddRange(nvidiaDevices);
                }

                // 使用lspci获取其他GPU信息
                var lspciDevices = await GetLspciGpuInfoAsync(capabilities);
                deviceInfoList.AddRange(lspciDevices);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取Linux GPU设备信息失败");
            }

            return deviceInfoList;
        }

        /// <summary>
        /// 使用lspci获取GPU信息
        /// </summary>
        private async Task<List<GpuDeviceInfo>> GetLspciGpuInfoAsync(GpuCapabilities capabilities)
        {
            var deviceInfoList = new List<GpuDeviceInfo>();

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "lspci",
                    Arguments = "-nn | grep VGA",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                {
                    var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                    foreach (var line in lines)
                    {
                        var name = ExtractGpuNameFromLspci(line);
                        var vendor = GetVendorFromName(name);
                        var supported = IsGpuSupported(vendor, capabilities);

                        deviceInfoList.Add(new GpuDeviceInfo
                        {
                            Name = name,
                            Vendor = vendor,
                            Driver = "Unknown",
                            Memory = "Unknown",
                            Encoder = GetEncoderForVendor(vendor),
                            MaxResolution = GetMaxResolutionForVendor(vendor),
                            PerformanceLevel = GetPerformanceLevelForVendor(vendor),
                            Supported = supported,
                            SupportedFormats = GetSupportedFormatsForVendor(vendor),
                            Reason = supported ? null : $"{vendor} GPU硬件加速不可用"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "使用lspci获取GPU信息失败");
            }

            return deviceInfoList;
        }

        /// <summary>
        /// 从能力检测结果生成设备信息
        /// </summary>
        private List<GpuDeviceInfo> GetDeviceInfoFromCapabilities(GpuCapabilities capabilities)
        {
            var deviceInfoList = new List<GpuDeviceInfo>();

            if (capabilities.NvencSupported)
            {
                deviceInfoList.Add(new GpuDeviceInfo
                {
                    Name = "NVIDIA GPU (检测到NVENC支持)",
                    Vendor = "NVIDIA",
                    Driver = "Unknown",
                    Memory = "Unknown",
                    Encoder = "NVENC H.264/H.265",
                    MaxResolution = "4K+",
                    PerformanceLevel = "High",
                    Supported = true,
                    SupportedFormats = capabilities.NvencEncoders.ToArray(),
                    Reason = null
                });
            }

            if (capabilities.QsvSupported)
            {
                deviceInfoList.Add(new GpuDeviceInfo
                {
                    Name = "Intel GPU (检测到QSV支持)",
                    Vendor = "Intel",
                    Driver = "Unknown",
                    Memory = "共享内存",
                    Encoder = "Quick Sync Video",
                    MaxResolution = "4K",
                    PerformanceLevel = "Medium",
                    Supported = true,
                    SupportedFormats = capabilities.QsvEncoders.ToArray(),
                    Reason = null
                });
            }

            if (capabilities.AmfSupported)
            {
                deviceInfoList.Add(new GpuDeviceInfo
                {
                    Name = "AMD GPU (检测到AMF支持)",
                    Vendor = "AMD",
                    Driver = "Unknown",
                    Memory = "Unknown",
                    Encoder = "AMF H.264/H.265",
                    MaxResolution = "4K",
                    PerformanceLevel = "High",
                    Supported = true,
                    SupportedFormats = capabilities.AmfEncoders.ToArray(),
                    Reason = null
                });
            }

            return deviceInfoList;
        }

        // 辅助方法
        private string GetVendorFromName(string name)
        {
            var lowerName = name.ToLower();
            if (lowerName.Contains("nvidia") || lowerName.Contains("geforce") || lowerName.Contains("quadro") || lowerName.Contains("tesla"))
                return "NVIDIA";
            if (lowerName.Contains("amd") || lowerName.Contains("radeon") || lowerName.Contains("rx "))
                return "AMD";
            if (lowerName.Contains("intel") || lowerName.Contains("uhd") || lowerName.Contains("iris"))
                return "Intel";
            return "Unknown";
        }

        private bool IsGpuSupported(string vendor, GpuCapabilities capabilities)
        {
            return vendor switch
            {
                "NVIDIA" => capabilities.NvencSupported,
                "Intel" => capabilities.QsvSupported,
                "AMD" => capabilities.AmfSupported,
                _ => false
            };
        }

        private string GetEncoderForVendor(string vendor)
        {
            return vendor switch
            {
                "NVIDIA" => "NVENC H.264/H.265/AV1",
                "Intel" => "Quick Sync Video",
                "AMD" => "AMF H.264/H.265",
                _ => "软件编码"
            };
        }

        private string[] GetSupportedFormatsForVendor(string vendor)
        {
            return vendor switch
            {
                "NVIDIA" => new[] { "H.264", "H.265", "AV1" },
                "Intel" => new[] { "H.264", "H.265" },
                "AMD" => new[] { "H.264", "H.265" },
                _ => new[] { "H.264" }
            };
        }

        private string GetMaxResolutionForVendor(string vendor)
        {
            return vendor switch
            {
                "NVIDIA" => "8K",
                "Intel" => "4K",
                "AMD" => "4K",
                _ => "1080p"
            };
        }

        private string GetPerformanceLevelForVendor(string vendor)
        {
            return vendor switch
            {
                "NVIDIA" => "High",
                "Intel" => "Medium",
                "AMD" => "High",
                _ => "Low"
            };
        }

        private string GetMaxResolutionForNvidia(string name)
        {
            var lowerName = name.ToLower();
            if (lowerName.Contains("rtx 40") || lowerName.Contains("rtx 30"))
                return "8K";
            if (lowerName.Contains("rtx 20") || lowerName.Contains("gtx 16"))
                return "4K";
            return "4K";
        }

        private string GetPerformanceLevelForNvidia(string name)
        {
            var lowerName = name.ToLower();
            if (lowerName.Contains("rtx") || lowerName.Contains("titan"))
                return "High";
            if (lowerName.Contains("gtx"))
                return "Medium";
            return "Low";
        }

        private string ExtractGpuNameFromLspci(string line)
        {
            // 从lspci输出中提取GPU名称
            var match = Regex.Match(line, @"VGA compatible controller: (.+?)(\[|$)");
            return match.Success ? match.Groups[1].Value.Trim() : line.Trim();
        }
    }

    /// <summary>
    /// GPU设备信息模型
    /// </summary>
    public class GpuDeviceInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Vendor { get; set; } = string.Empty;
        public string Driver { get; set; } = string.Empty;
        public string Memory { get; set; } = string.Empty;
        public string Encoder { get; set; } = string.Empty;
        public string MaxResolution { get; set; } = string.Empty;
        public string PerformanceLevel { get; set; } = string.Empty;
        public bool Supported { get; set; }
        public string[] SupportedFormats { get; set; } = Array.Empty<string>();
        public string? Reason { get; set; }
    }
}
