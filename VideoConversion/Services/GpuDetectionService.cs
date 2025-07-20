using System.Diagnostics;
using System.Text.RegularExpressions;

namespace VideoConversion.Services
{
    /// <summary>
    /// GPU硬件加速检测服务
    /// </summary>
    public class GpuDetectionService
    {
        private readonly ILogger<GpuDetectionService> _logger;
        private readonly IConfiguration _configuration;
        private readonly FFmpegConfigurationService _ffmpegConfig;
        private GpuCapabilities? _cachedCapabilities;

        public GpuDetectionService(
            ILogger<GpuDetectionService> logger,
            IConfiguration configuration,
            FFmpegConfigurationService ffmpegConfig)
        {
            _logger = logger;
            _configuration = configuration;
            _ffmpegConfig = ffmpegConfig;

            _logger.LogInformation("GPU检测服务初始化，FFmpeg配置状态: {IsInitialized}",
                _ffmpegConfig.IsInitialized);
        }

        /// <summary>
        /// 检测GPU硬件加速能力
        /// </summary>
        public async Task<GpuCapabilities> DetectGpuCapabilitiesAsync()
        {
            if (_cachedCapabilities != null)
            {
                return _cachedCapabilities;
            }

            _logger.LogInformation("开始检测GPU硬件加速能力...");

            var capabilities = new GpuCapabilities();

            try
            {
                // 检测FFmpeg支持的编码器
                var encoders = await GetAvailableEncodersAsync();
                
                // 检测NVIDIA NVENC
                var nvencEncoders = encoders.Where(e => e.Contains("nvenc")).ToList();
                if (nvencEncoders.Any())
                {
                    _logger.LogInformation("发现NVENC编码器: {Encoders}", string.Join(", ", nvencEncoders));

                    // 测试主要的NVENC编码器
                    var testEncoder = nvencEncoders.Contains("h264_nvenc") ? "h264_nvenc" : nvencEncoders.First();
                    var nvencWorks = await TestGpuEncoderAsync(testEncoder);

                    if (nvencWorks)
                    {
                        capabilities.NvencSupported = true;
                        capabilities.NvencEncoders = nvencEncoders;
                        _logger.LogInformation("NVIDIA NVENC测试成功: {Encoders}", string.Join(", ", nvencEncoders));
                    }
                    else
                    {
                        _logger.LogWarning("NVIDIA NVENC测试失败，可能缺少驱动或硬件不支持");
                    }
                }

                // 检测Intel QSV
                var qsvEncoders = encoders.Where(e => e.Contains("qsv")).ToList();
                if (qsvEncoders.Any())
                {
                    _logger.LogInformation("发现QSV编码器: {Encoders}", string.Join(", ", qsvEncoders));

                    var testEncoder = qsvEncoders.Contains("h264_qsv") ? "h264_qsv" : qsvEncoders.First();
                    var qsvWorks = await TestGpuEncoderAsync(testEncoder);

                    if (qsvWorks)
                    {
                        capabilities.QsvSupported = true;
                        capabilities.QsvEncoders = qsvEncoders;
                        _logger.LogInformation("Intel QSV测试成功: {Encoders}", string.Join(", ", qsvEncoders));
                    }
                    else
                    {
                        _logger.LogWarning("Intel QSV测试失败，可能缺少驱动或硬件不支持");
                    }
                }

                // 检测AMD VCE/AMF
                var amfEncoders = encoders.Where(e => e.Contains("amf")).ToList();
                if (amfEncoders.Any())
                {
                    _logger.LogInformation("发现AMF编码器: {Encoders}", string.Join(", ", amfEncoders));

                    var testEncoder = amfEncoders.Contains("h264_amf") ? "h264_amf" : amfEncoders.First();
                    var amfWorks = await TestGpuEncoderAsync(testEncoder);

                    if (amfWorks)
                    {
                        capabilities.AmfSupported = true;
                        capabilities.AmfEncoders = amfEncoders;
                        _logger.LogInformation("AMD VCE/AMF测试成功: {Encoders}", string.Join(", ", amfEncoders));
                    }
                    else
                    {
                        _logger.LogWarning("AMD VCE/AMF测试失败，可能缺少驱动或硬件不支持");
                    }
                }

                // 检测VAAPI (Linux)
                var vaapiEncoders = encoders.Where(e => e.Contains("vaapi")).ToList();
                if (vaapiEncoders.Any())
                {
                    _logger.LogInformation("发现VAAPI编码器: {Encoders}", string.Join(", ", vaapiEncoders));

                    var testEncoder = vaapiEncoders.Contains("h264_vaapi") ? "h264_vaapi" : vaapiEncoders.First();
                    var vaapiWorks = await TestGpuEncoderAsync(testEncoder);

                    if (vaapiWorks)
                    {
                        capabilities.VaapiSupported = true;
                        capabilities.VaapiEncoders = vaapiEncoders;
                        _logger.LogInformation("VAAPI测试成功: {Encoders}", string.Join(", ", vaapiEncoders));
                    }
                    else
                    {
                        _logger.LogWarning("VAAPI测试失败，可能缺少驱动或硬件不支持");
                    }
                }

                // 检测FFmpeg版本和编译选项
                await DetectFFmpegInfoAsync();

                // 检测硬件加速设备
                await DetectHardwareAccelDevicesAsync();

                // 检测系统GPU信息
                await DetectSystemGpuInfoAsync(capabilities);

                _cachedCapabilities = capabilities;
                _logger.LogInformation("GPU检测完成 - NVENC: {Nvenc}, QSV: {Qsv}, AMF: {Amf}, VAAPI: {Vaapi}",
                    capabilities.NvencSupported, capabilities.QsvSupported, capabilities.AmfSupported, capabilities.VaapiSupported);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GPU检测失败");
            }

            return capabilities;
        }

        /// <summary>
        /// 获取FFmpeg支持的编码器列表
        /// </summary>
        private async Task<List<string>> GetAvailableEncodersAsync()
        {
            var encoders = new List<string>();

            try
            {
                _logger.LogInformation("检查FFmpeg路径: {FFmpegPath}", _ffmpegConfig.FFmpegPath);

                if (!_ffmpegConfig.IsInitialized || !File.Exists(_ffmpegConfig.FFmpegPath))
                {
                    _logger.LogError("FFmpeg未正确配置或文件不存在: {FFmpegPath}", _ffmpegConfig.FFmpegPath);
                    return encoders;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegConfig.FFmpegPath,
                    Arguments = "-encoders",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };
                _logger.LogDebug("启动FFmpeg进程获取编码器列表...");

                process.Start();

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                _logger.LogDebug("FFmpeg进程退出码: {ExitCode}", process.ExitCode);

                if (process.ExitCode != 0)
                {
                    _logger.LogError("FFmpeg执行失败，退出码: {ExitCode}, 错误: {Error}", process.ExitCode, error);
                    return encoders;
                }

                if (string.IsNullOrEmpty(output))
                {
                    _logger.LogWarning("FFmpeg输出为空");
                    return encoders;
                }

                // 解析编码器列表 - 基于博客最佳实践的改进方法
                var lines = output.Split('\n');

                // 更精确的编码器匹配模式
                var encoderRegex = new Regex(@"^\s*V[\.F][\.S][\.X][\.B][\.D]\s+(\S+)\s+(.+)$", RegexOptions.Compiled);

                _logger.LogDebug("开始解析FFmpeg编码器列表...");

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("---") || trimmedLine.StartsWith("Encoders:"))
                        continue;

                    var match = encoderRegex.Match(line);
                    if (match.Success)
                    {
                        var encoderName = match.Groups[1].Value;
                        var description = match.Groups[2].Value;

                        encoders.Add(encoderName);

                        // 记录GPU相关编码器
                        if (IsGpuEncoder(encoderName))
                        {
                            _logger.LogDebug("发现GPU编码器: {Encoder} - {Description}", encoderName, description);
                        }
                    }
                    else if (trimmedLine.Contains("nvenc") || trimmedLine.Contains("qsv") ||
                             trimmedLine.Contains("amf") || trimmedLine.Contains("vaapi"))
                    {
                        // 备用解析方法：直接从行中提取编码器名称
                        var parts = trimmedLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2)
                        {
                            var encoderName = parts[1];
                            if (IsGpuEncoder(encoderName) && !encoders.Contains(encoderName))
                            {
                                encoders.Add(encoderName);
                                _logger.LogDebug("备用方法发现GPU编码器: {Encoder}", encoderName);
                            }
                        }
                    }
                }

                _logger.LogInformation("总共找到 {Count} 个编码器", encoders.Count);
                _logger.LogDebug("编码器列表: {Encoders}", string.Join(", ", encoders.Take(10)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取FFmpeg编码器列表失败，FFmpeg路径: {FFmpegPath}", _ffmpegConfig.FFmpegPath);
            }

            return encoders;
        }

        /// <summary>
        /// 判断是否为GPU编码器
        /// </summary>
        private bool IsGpuEncoder(string encoderName)
        {
            if (string.IsNullOrEmpty(encoderName))
                return false;

            var lowerName = encoderName.ToLower();

            // NVIDIA编码器
            if (lowerName.Contains("nvenc") || lowerName.Contains("cuda"))
                return true;

            // Intel编码器
            if (lowerName.Contains("qsv") || lowerName.Contains("quicksync"))
                return true;

            // AMD编码器
            if (lowerName.Contains("amf") || lowerName.Contains("vce"))
                return true;

            // VAAPI编码器
            if (lowerName.Contains("vaapi"))
                return true;

            // 其他硬件编码器
            if (lowerName.Contains("videotoolbox") || lowerName.Contains("mediacodec"))
                return true;

            return false;
        }

        /// <summary>
        /// 检测FFmpeg版本和编译信息
        /// </summary>
        private async Task DetectFFmpegInfoAsync()
        {
            try
            {
                if (!_ffmpegConfig.IsInitialized || !File.Exists(_ffmpegConfig.FFmpegPath))
                {
                    _logger.LogError("FFmpeg未正确配置或文件不存在: {FFmpegPath}", _ffmpegConfig.FFmpegPath);
                    return;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegConfig.FFmpegPath,
                    Arguments = "-version",
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
                    var lines = output.Split('\n');
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("ffmpeg version"))
                        {
                            _logger.LogInformation("FFmpeg版本: {Version}", line.Trim());
                        }
                        else if (line.Contains("configuration:"))
                        {
                            var config = line.Substring(line.IndexOf("configuration:") + "configuration:".Length).Trim();

                            // 检查GPU相关的编译选项
                            var gpuOptions = new[]
                            {
                                "--enable-nvenc", "--enable-cuda", "--enable-cuvid",
                                "--enable-libmfx", "--enable-qsv",
                                "--enable-amf", "--enable-d3d11va",
                                "--enable-vaapi", "--enable-vdpau"
                            };

                            var foundOptions = gpuOptions.Where(option => config.Contains(option)).ToList();

                            if (foundOptions.Any())
                            {
                                _logger.LogInformation("FFmpeg编译包含GPU支持: {Options}", string.Join(", ", foundOptions));
                            }
                            else
                            {
                                _logger.LogWarning("FFmpeg编译可能不包含GPU硬件加速支持");
                            }

                            break;
                        }
                    }
                }
                else
                {
                    _logger.LogError("无法获取FFmpeg版本信息，退出码: {ExitCode}", process.ExitCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检测FFmpeg信息失败");
            }
        }

        /// <summary>
        /// 检测硬件加速设备 - 基于博客最佳实践
        /// </summary>
        private async Task DetectHardwareAccelDevicesAsync()
        {
            try
            {
                if (!_ffmpegConfig.IsInitialized || !File.Exists(_ffmpegConfig.FFmpegPath))
                {
                    _logger.LogWarning("FFmpeg未正确配置，跳过硬件加速设备检测");
                    return;
                }

                _logger.LogInformation("检测硬件加速设备...");

                // 检测可用的硬件加速方法
                var arguments = "-hwaccels";

                var startInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegConfig.FFmpegPath,
                    Arguments = arguments,
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
                    var lines = output.Split('\n');
                    var hwaccels = new List<string>();

                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (!string.IsNullOrEmpty(trimmed) &&
                            !trimmed.StartsWith("Hardware acceleration methods:") &&
                            !trimmed.StartsWith("---"))
                        {
                            hwaccels.Add(trimmed);
                        }
                    }

                    if (hwaccels.Any())
                    {
                        _logger.LogInformation("支持的硬件加速方法: {Methods}", string.Join(", ", hwaccels));

                        // 分析每种硬件加速方法
                        foreach (var hwaccel in hwaccels)
                        {
                            AnalyzeHardwareAccelMethod(hwaccel);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("未检测到硬件加速方法");
                    }
                }
                else
                {
                    _logger.LogWarning("⚠️ 无法获取硬件加速设备列表");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 检测硬件加速设备失败");
            }
        }

        /// <summary>
        /// 分析硬件加速方法
        /// </summary>
        private void AnalyzeHardwareAccelMethod(string method)
        {
            switch (method.ToLower())
            {
                case "cuda":
                    _logger.LogInformation("CUDA硬件加速可用 - 支持NVIDIA GPU");
                    break;
                case "qsv":
                    _logger.LogInformation("QSV硬件加速可用 - 支持Intel GPU");
                    break;
                case "d3d11va":
                    _logger.LogInformation("D3D11VA硬件加速可用 - 支持Windows GPU");
                    break;
                case "dxva2":
                    _logger.LogInformation("DXVA2硬件加速可用 - 支持Windows GPU");
                    break;
                case "vaapi":
                    _logger.LogInformation("VAAPI硬件加速可用 - 支持Linux GPU");
                    break;
                case "videotoolbox":
                    _logger.LogInformation("VideoToolbox硬件加速可用 - 支持macOS GPU");
                    break;
                default:
                    _logger.LogDebug("检测到硬件加速方法: {Method}", method);
                    break;
            }
        }

        /// <summary>
        /// 检测系统GPU信息
        /// </summary>
        private async Task DetectSystemGpuInfoAsync(GpuCapabilities capabilities)
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    await DetectWindowsGpuInfoAsync(capabilities);
                }
                else if (OperatingSystem.IsLinux())
                {
                    await DetectLinuxGpuInfoAsync(capabilities);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "检测系统GPU信息失败");
            }
        }

        /// <summary>
        /// 检测Windows GPU信息
        /// </summary>
        private async Task DetectWindowsGpuInfoAsync(GpuCapabilities capabilities)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "wmic",
                    Arguments = "path win32_VideoController get name",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                var gpuNames = output.Split('\n')
                    .Where(line => !string.IsNullOrWhiteSpace(line) && !line.Contains("Name"))
                    .Select(line => line.Trim())
                    .Where(line => !string.IsNullOrEmpty(line))
                    .ToList();

                capabilities.GpuDevices = gpuNames;
                _logger.LogInformation("检测到GPU设备: {Devices}", string.Join(", ", gpuNames));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "检测Windows GPU信息失败");
            }
        }

        /// <summary>
        /// 检测Linux GPU信息
        /// </summary>
        private async Task DetectLinuxGpuInfoAsync(GpuCapabilities capabilities)
        {
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

                var gpuNames = output.Split('\n')
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Select(line => line.Trim())
                    .ToList();

                capabilities.GpuDevices = gpuNames;
                _logger.LogInformation("检测到GPU设备: {Devices}", string.Join(", ", gpuNames));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "检测Linux GPU信息失败");
            }
        }

        /// <summary>
        /// 获取推荐的GPU编码器
        /// </summary>
        public async Task<string?> GetRecommendedGpuEncoderAsync(string codec = "h264")
        {
            var capabilities = await DetectGpuCapabilitiesAsync();

            // 优先级：NVENC > QSV > AMF > VAAPI
            if (capabilities.NvencSupported && capabilities.NvencEncoders.Any(e => e.Contains(codec)))
            {
                return capabilities.NvencEncoders.First(e => e.Contains(codec));
            }

            if (capabilities.QsvSupported && capabilities.QsvEncoders.Any(e => e.Contains(codec)))
            {
                return capabilities.QsvEncoders.First(e => e.Contains(codec));
            }

            if (capabilities.AmfSupported && capabilities.AmfEncoders.Any(e => e.Contains(codec)))
            {
                return capabilities.AmfEncoders.First(e => e.Contains(codec));
            }

            if (capabilities.VaapiSupported && capabilities.VaapiEncoders.Any(e => e.Contains(codec)))
            {
                return capabilities.VaapiEncoders.First(e => e.Contains(codec));
            }

            return null;
        }

        /// <summary>
        /// 测试GPU编码器是否真正可用 - 基于博客最佳实践
        /// </summary>
        public async Task<bool> TestGpuEncoderAsync(string encoder)
        {
            try
            {
                if (!_ffmpegConfig.IsInitialized || !File.Exists(_ffmpegConfig.FFmpegPath))
                {
                    _logger.LogError("FFmpeg未正确配置，无法测试编码器: {Encoder}", encoder);
                    return false;
                }

                _logger.LogInformation("测试GPU编码器: {Encoder}", encoder);

                // 根据编码器类型构建不同的测试命令
                var arguments = BuildTestCommand(encoder);
                _logger.LogDebug("测试命令: {Command}", arguments);

                var startInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegConfig.FFmpegPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();

                // 设置超时时间
                var timeoutTask = Task.Delay(15000); // 15秒超时
                var processTask = process.WaitForExitAsync();

                var completedTask = await Task.WhenAny(processTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    _logger.LogWarning("GPU编码器测试超时: {Encoder}", encoder);
                    try
                    {
                        process.Kill();
                    }
                    catch { }
                    return false;
                }

                var success = process.ExitCode == 0;

                if (success)
                {
                    _logger.LogInformation("GPU编码器测试成功: {Encoder}", encoder);
                }
                else
                {
                    _logger.LogWarning("GPU编码器测试失败: {Encoder}, 退出码: {ExitCode}", encoder, process.ExitCode);

                    // 分析错误信息
                    AnalyzeEncoderError(encoder, error);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "测试GPU编码器异常: {Encoder}", encoder);
                return false;
            }
        }

        /// <summary>
        /// 构建GPU编码器测试命令
        /// </summary>
        private string BuildTestCommand(string encoder)
        {
            var lowerEncoder = encoder.ToLower();

            // NVIDIA NVENC编码器
            if (lowerEncoder.Contains("nvenc"))
            {
                return $"-f lavfi -i testsrc=duration=2:size=320x240:rate=1 -c:v {encoder} -preset fast -b:v 100k -f null -";
            }

            // Intel QSV编码器
            if (lowerEncoder.Contains("qsv"))
            {
                return $"-f lavfi -i testsrc=duration=2:size=320x240:rate=1 -c:v {encoder} -preset fast -b:v 100k -f null -";
            }

            // AMD AMF编码器
            if (lowerEncoder.Contains("amf"))
            {
                return $"-f lavfi -i testsrc=duration=2:size=320x240:rate=1 -c:v {encoder} -quality speed -b:v 100k -f null -";
            }

            // VAAPI编码器
            if (lowerEncoder.Contains("vaapi"))
            {
                return $"-f lavfi -i testsrc=duration=2:size=320x240:rate=1 -vaapi_device /dev/dri/renderD128 -c:v {encoder} -b:v 100k -f null -";
            }

            // 默认测试命令
            return $"-f lavfi -i testsrc=duration=2:size=320x240:rate=1 -c:v {encoder} -f null -";
        }

        /// <summary>
        /// 分析编码器错误信息
        /// </summary>
        private void AnalyzeEncoderError(string encoder, string error)
        {
            if (string.IsNullOrEmpty(error))
                return;

            var lowerError = error.ToLower();
            var lowerEncoder = encoder.ToLower();

            if (lowerEncoder.Contains("nvenc"))
            {
                if (lowerError.Contains("no nvidia"))
                {
                    _logger.LogWarning("NVENC失败原因: 未检测到NVIDIA GPU");
                }
                else if (lowerError.Contains("driver"))
                {
                    _logger.LogWarning("NVENC失败原因: NVIDIA驱动程序问题");
                }
                else if (lowerError.Contains("cuda"))
                {
                    _logger.LogWarning("NVENC失败原因: CUDA初始化失败");
                }
            }
            else if (lowerEncoder.Contains("qsv"))
            {
                if (lowerError.Contains("mfx"))
                {
                    _logger.LogWarning("QSV失败原因: Intel Media SDK问题");
                }
                else if (lowerError.Contains("device"))
                {
                    _logger.LogWarning("QSV失败原因: Intel GPU设备不可用");
                }
            }
            else if (lowerEncoder.Contains("amf"))
            {
                if (lowerError.Contains("amf"))
                {
                    _logger.LogWarning("AMF失败原因: AMD Media Framework不可用");
                }
                else if (lowerError.Contains("d3d11"))
                {
                    _logger.LogWarning("AMF失败原因: DirectX 11不可用");
                }
            }

            _logger.LogDebug("详细错误信息: {Error}", error);
        }

        /// <summary>
        /// 清除缓存，强制重新检测
        /// </summary>
        public void ClearCache()
        {
            _cachedCapabilities = null;
        }
    }

    /// <summary>
    /// GPU硬件加速能力信息
    /// </summary>
    public class GpuCapabilities
    {
        public bool NvencSupported { get; set; }
        public bool QsvSupported { get; set; }
        public bool AmfSupported { get; set; }
        public bool VaapiSupported { get; set; }

        public List<string> NvencEncoders { get; set; } = new();
        public List<string> QsvEncoders { get; set; } = new();
        public List<string> AmfEncoders { get; set; } = new();
        public List<string> VaapiEncoders { get; set; } = new();

        public List<string> GpuDevices { get; set; } = new();

        public bool HasAnyGpuSupport => NvencSupported || QsvSupported || AmfSupported || VaapiSupported;

        public string GetSupportedGpuTypes()
        {
            var types = new List<string>();
            if (NvencSupported) types.Add("NVIDIA NVENC");
            if (QsvSupported) types.Add("Intel QSV");
            if (AmfSupported) types.Add("AMD VCE/AMF");
            if (VaapiSupported) types.Add("VAAPI");
            return string.Join(", ", types);
        }
    }
}
