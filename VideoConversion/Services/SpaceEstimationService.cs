using VideoConversion.Models;

namespace VideoConversion.Services
{
    /// <summary>
    /// 空间预估服务
    /// </summary>
    public class SpaceEstimationService
    {
        private readonly ILogger<SpaceEstimationService> _logger;
        private readonly FFmpegFormatDetectionService _formatDetectionService;

        // 编码器压缩比数据库（基于实际测试数据）
        private readonly Dictionary<string, double> _codecCompressionRatios = new()
        {
            // NVIDIA NVENC 编码器
            { "h264_nvenc", 0.65 },
            { "h265_nvenc", 0.45 },
            { "av1_nvenc", 0.35 },
            
            // 软件编码器
            { "libx264", 0.70 },
            { "libx265", 0.50 },
            { "libaom-av1", 0.40 },
            { "libvpx-vp9", 0.55 },
            
            // 通用编码器名称
            { "h264", 0.68 },
            { "h265", 0.48 },
            { "hevc", 0.48 },
            { "av1", 0.38 },
            { "vp9", 0.58 }
        };

        // 格式容器开销
        private readonly Dictionary<string, double> _formatOverheads = new()
        {
            { "mp4", 1.02 },
            { "mkv", 1.05 },
            { "avi", 1.08 },
            { "mov", 1.03 },
            { "webm", 1.01 },
            { "flv", 1.06 },
            { "wmv", 1.07 },
            { "m4v", 1.02 }
        };

        // 分辨率调整系数
        private readonly Dictionary<string, double> _resolutionMultipliers = new()
        {
            { "8k", 2.0 },
            { "4k", 1.5 },
            { "2160p", 1.5 },
            { "1440p", 1.2 },
            { "1080p", 1.0 },
            { "720p", 0.7 },
            { "480p", 0.5 },
            { "360p", 0.3 },
            { "240p", 0.2 }
        };

        public SpaceEstimationService(
            ILogger<SpaceEstimationService> logger,
            FFmpegFormatDetectionService formatDetectionService)
        {
            _logger = logger;
            _formatDetectionService = formatDetectionService;
        }

        /// <summary>
        /// 预估输出文件大小
        /// </summary>
        public long EstimateOutputSize(long originalSize, ConversionSettings settings)
        {
            try
            {
                _logger.LogDebug("开始预估输出文件大小: 原始大小={OriginalMB}MB", originalSize / 1024.0 / 1024);

                // 1. 获取基础压缩比
                var compressionRatio = GetCompressionRatio(settings.VideoCodec, settings.VideoBitrate, originalSize);
                
                // 2. 应用格式开销
                var formatMultiplier = GetFormatMultiplier(settings.OutputFormat);
                
                // 3. 应用分辨率调整
                var resolutionMultiplier = GetResolutionMultiplier(settings.Resolution);
                
                // 4. 应用质量调整
                var qualityMultiplier = GetQualityMultiplier(settings.Quality, settings.VideoBitrate);
                
                // 5. 计算预估大小
                var estimatedSize = (long)(originalSize * compressionRatio * formatMultiplier * resolutionMultiplier * qualityMultiplier);
                
                // 6. 应用合理性检查
                estimatedSize = ApplyReasonabilityCheck(originalSize, estimatedSize, settings);

                _logger.LogDebug("空间预估完成: 原始={OriginalMB}MB, 预估={EstimatedMB}MB, 压缩比={Ratio:F2}", 
                    originalSize / 1024.0 / 1024, 
                    estimatedSize / 1024.0 / 1024,
                    (double)estimatedSize / originalSize);

                return estimatedSize;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "预估输出文件大小失败");
                // 返回保守估计（原文件大小的80%）
                return (long)(originalSize * 0.8);
            }
        }

        /// <summary>
        /// 获取压缩比
        /// </summary>
        private double GetCompressionRatio(string? videoCodec, int? videoBitrate, long originalSize)
        {
            var codec = videoCodec?.ToLower() ?? "h264";
            
            // 从数据库获取基础压缩比
            var baseRatio = _codecCompressionRatios.GetValueOrDefault(codec, 0.7);
            
            // 如果指定了比特率，基于比特率进行更精确的计算
            if (videoBitrate.HasValue && videoBitrate.Value > 0)
            {
                // 估算原始比特率（假设原始文件是合理编码的）
                var estimatedOriginalBitrate = EstimateOriginalBitrate(originalSize);
                
                if (estimatedOriginalBitrate > 0)
                {
                    var bitrateRatio = (double)videoBitrate.Value / estimatedOriginalBitrate;
                    
                    // 比特率比例影响最终大小，但有上下限
                    bitrateRatio = Math.Max(0.2, Math.Min(2.0, bitrateRatio));
                    
                    baseRatio *= bitrateRatio;
                }
            }
            
            return baseRatio;
        }

        /// <summary>
        /// 估算原始文件比特率
        /// </summary>
        private int EstimateOriginalBitrate(long fileSize)
        {
            // 假设平均视频时长为30分钟，音频比特率为128kbps
            var assumedDurationSeconds = 30 * 60; // 30分钟
            var assumedAudioBitrate = 128; // 128kbps
            
            var totalBitrate = (int)((fileSize * 8) / assumedDurationSeconds / 1000); // kbps
            var videoBitrate = totalBitrate - assumedAudioBitrate;
            
            return Math.Max(500, videoBitrate); // 最小500kbps
        }

        /// <summary>
        /// 获取格式开销系数
        /// </summary>
        private double GetFormatMultiplier(string? outputFormat)
        {
            var format = outputFormat?.ToLower() ?? "mp4";
            return _formatOverheads.GetValueOrDefault(format, 1.02);
        }

        /// <summary>
        /// 获取分辨率调整系数
        /// </summary>
        private double GetResolutionMultiplier(string? resolution)
        {
            if (string.IsNullOrEmpty(resolution))
                return 1.0;
                
            var res = resolution.ToLower();
            return _resolutionMultipliers.GetValueOrDefault(res, 1.0);
        }

        /// <summary>
        /// 获取质量调整系数
        /// </summary>
        private double GetQualityMultiplier(string? quality, int? videoBitrate)
        {
            if (videoBitrate.HasValue && videoBitrate.Value > 0)
            {
                // 如果指定了比特率，质量参数的影响较小
                return 1.0;
            }
            
            return quality?.ToLower() switch
            {
                "low" or "fast" => 0.8,
                "medium" or "balanced" => 1.0,
                "high" or "slow" => 1.2,
                "ultra" or "veryslow" => 1.4,
                _ => 1.0
            };
        }

        /// <summary>
        /// 应用合理性检查
        /// </summary>
        private long ApplyReasonabilityCheck(long originalSize, long estimatedSize, ConversionSettings settings)
        {
            // 设置合理的上下限
            var minSize = (long)(originalSize * 0.1); // 最小不能小于原文件的10%
            var maxSize = (long)(originalSize * 2.0);  // 最大不能超过原文件的200%
            
            // 特殊情况调整
            if (settings.VideoCodec?.ToLower().Contains("lossless") == true)
            {
                maxSize = (long)(originalSize * 3.0); // 无损编码可能更大
            }
            
            if (settings.OutputFormat?.ToLower() == "gif")
            {
                maxSize = (long)(originalSize * 5.0); // GIF可能很大
            }
            
            return Math.Max(minSize, Math.Min(maxSize, estimatedSize));
        }

        /// <summary>
        /// 计算临时文件空间需求
        /// </summary>
        public long EstimateTempFileSpace(long originalSize, ConversionSettings settings)
        {
            // 临时文件包括：
            // 1. 分片上传的临时文件（原文件大小）
            // 2. FFmpeg处理过程中的临时文件（约原文件的20%）
            // 3. 缓存和日志文件（约原文件的5%）
            
            var chunkTempSpace = originalSize; // 分片临时文件
            var ffmpegTempSpace = (long)(originalSize * 0.2); // FFmpeg临时文件
            var cacheTempSpace = (long)(originalSize * 0.05); // 缓存文件
            
            return chunkTempSpace + ffmpegTempSpace + cacheTempSpace;
        }

        /// <summary>
        /// 计算总空间需求
        /// </summary>
        public SpaceRequirement CalculateTotalSpaceRequirement(long originalSize, ConversionSettings settings)
        {
            var outputSize = EstimateOutputSize(originalSize, settings);
            var tempSize = EstimateTempFileSpace(originalSize, settings);
            
            return new SpaceRequirement
            {
                OriginalFileSize = originalSize,
                EstimatedOutputSize = outputSize,
                TempFileSize = tempSize,
                TotalRequiredSize = originalSize + outputSize + tempSize,
                CompressionRatio = (double)outputSize / originalSize,
                Settings = settings
            };
        }

        /// <summary>
        /// 批量计算空间需求
        /// </summary>
        public BatchSpaceRequirement CalculateBatchSpaceRequirement(List<BatchFileInfo> files)
        {
            var totalOriginalSize = 0L;
            var totalEstimatedOutputSize = 0L;
            var totalTempSize = 0L;
            var fileRequirements = new List<SpaceRequirement>();

            foreach (var file in files)
            {
                var requirement = CalculateTotalSpaceRequirement(file.FileSize, file.Settings);
                fileRequirements.Add(requirement);
                
                totalOriginalSize += requirement.OriginalFileSize;
                totalEstimatedOutputSize += requirement.EstimatedOutputSize;
                totalTempSize += requirement.TempFileSize;
            }

            return new BatchSpaceRequirement
            {
                FileCount = files.Count,
                TotalOriginalSize = totalOriginalSize,
                TotalEstimatedOutputSize = totalEstimatedOutputSize,
                TotalTempSize = totalTempSize,
                TotalRequiredSize = totalOriginalSize + totalEstimatedOutputSize + totalTempSize,
                FileRequirements = fileRequirements,
                AverageCompressionRatio = fileRequirements.Average(f => f.CompressionRatio)
            };
        }

        /// <summary>
        /// 更新压缩比数据（基于实际转换结果）
        /// </summary>
        public void UpdateCompressionRatio(string videoCodec, long originalSize, long actualOutputSize)
        {
            try
            {
                var actualRatio = (double)actualOutputSize / originalSize;
                var codec = videoCodec.ToLower();
                
                if (_codecCompressionRatios.ContainsKey(codec))
                {
                    // 使用加权平均更新压缩比（新数据权重30%，历史数据权重70%）
                    var currentRatio = _codecCompressionRatios[codec];
                    var updatedRatio = currentRatio * 0.7 + actualRatio * 0.3;
                    
                    _codecCompressionRatios[codec] = updatedRatio;
                    
                    _logger.LogDebug("更新压缩比: {Codec} {OldRatio:F3} -> {NewRatio:F3}", 
                        codec, currentRatio, updatedRatio);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新压缩比失败: {Codec}", videoCodec);
            }
        }
    }

    /// <summary>
    /// 空间需求信息
    /// </summary>
    public class SpaceRequirement
    {
        public long OriginalFileSize { get; set; }
        public long EstimatedOutputSize { get; set; }
        public long TempFileSize { get; set; }
        public long TotalRequiredSize { get; set; }
        public double CompressionRatio { get; set; }
        public ConversionSettings Settings { get; set; } = new();
    }

    /// <summary>
    /// 批量空间需求信息
    /// </summary>
    public class BatchSpaceRequirement
    {
        public int FileCount { get; set; }
        public long TotalOriginalSize { get; set; }
        public long TotalEstimatedOutputSize { get; set; }
        public long TotalTempSize { get; set; }
        public long TotalRequiredSize { get; set; }
        public double AverageCompressionRatio { get; set; }
        public List<SpaceRequirement> FileRequirements { get; set; } = new();
    }

    /// <summary>
    /// 批量文件信息
    /// </summary>
    public class BatchFileInfo
    {
        public string FileName { get; set; } = "";
        public long FileSize { get; set; }
        public ConversionSettings Settings { get; set; } = new();
    }

    /// <summary>
    /// 转换设置
    /// </summary>
    public class ConversionSettings
    {
        public string? VideoCodec { get; set; }
        public string? OutputFormat { get; set; }
        public string? Resolution { get; set; }
        public string? Quality { get; set; }
        public int? VideoBitrate { get; set; }
        public int? AudioBitrate { get; set; }
    }
}
