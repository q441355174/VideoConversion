using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace VideoConversion.Services
{
    /// <summary>
    /// FFmpeg格式检测服务
    /// </summary>
    public class FFmpegFormatDetectionService
    {
        private readonly ILogger<FFmpegFormatDetectionService> _logger;
        private readonly FFmpegConfigurationService _ffmpegConfig;
        private List<string>? _supportedInputFormats;
        private List<string>? _supportedOutputFormats;
        private Dictionary<string, List<string>>? _formatCodecMap;

        public FFmpegFormatDetectionService(
            ILogger<FFmpegFormatDetectionService> logger,
            FFmpegConfigurationService ffmpegConfig)
        {
            _logger = logger;
            _ffmpegConfig = ffmpegConfig;
        }

        /// <summary>
        /// 获取FFmpeg支持的输入格式
        /// </summary>
        public async Task<List<string>> GetSupportedInputFormatsAsync()
        {
            if (_supportedInputFormats != null)
                return _supportedInputFormats;

            _supportedInputFormats = await DetectFormatsAsync(true);
            return _supportedInputFormats;
        }

        /// <summary>
        /// 获取FFmpeg支持的输出格式
        /// </summary>
        public async Task<List<string>> GetSupportedOutputFormatsAsync()
        {
            if (_supportedOutputFormats != null)
                return _supportedOutputFormats;

            _supportedOutputFormats = await DetectFormatsAsync(false);
            return _supportedOutputFormats;
        }

        /// <summary>
        /// 检查特定格式是否支持转换
        /// </summary>
        public async Task<bool> IsFormatConversionSupportedAsync(string inputFormat, string outputFormat)
        {
            var inputFormats = await GetSupportedInputFormatsAsync();
            var outputFormats = await GetSupportedOutputFormatsAsync();

            var inputSupported = inputFormats.Any(f => 
                f.Equals(inputFormat, StringComparison.OrdinalIgnoreCase) ||
                f.Contains(inputFormat, StringComparison.OrdinalIgnoreCase));

            var outputSupported = outputFormats.Any(f => 
                f.Equals(outputFormat, StringComparison.OrdinalIgnoreCase) ||
                f.Contains(outputFormat, StringComparison.OrdinalIgnoreCase));

            return inputSupported && outputSupported;
        }

        /// <summary>
        /// 获取扩展格式支持列表
        /// </summary>
        public async Task<Dictionary<string, bool>> GetExtendedFormatSupportAsync()
        {
            var formats = new[]
            {
                "mp4", "avi", "mov", "mkv", "wmv", "flv", "webm", "m4v", "3gp",
                "mpg", "mpeg", "ts", "mts", "m2ts", "vob", "asf", "rm", "rmvb"
            };

            var supportMap = new Dictionary<string, bool>();
            var inputFormats = await GetSupportedInputFormatsAsync();
            var outputFormats = await GetSupportedOutputFormatsAsync();

            foreach (var format in formats)
            {
                var inputSupported = inputFormats.Any(f => 
                    f.Contains(format, StringComparison.OrdinalIgnoreCase));
                var outputSupported = outputFormats.Any(f => 
                    f.Contains(format, StringComparison.OrdinalIgnoreCase));

                supportMap[format] = inputSupported && outputSupported;
            }

            return supportMap;
        }

        /// <summary>
        /// 检测FFmpeg支持的格式
        /// </summary>
        private async Task<List<string>> DetectFormatsAsync(bool isInput)
        {
            var formats = new List<string>();

            try
            {
                if (!_ffmpegConfig.IsInitialized || !File.Exists(_ffmpegConfig.FFmpegPath))
                {
                    _logger.LogError("FFmpeg未正确配置");
                    return formats;
                }

                var arguments = isInput ? "-demuxers" : "-muxers";
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

                if (process.ExitCode != 0)
                {
                    _logger.LogError("FFmpeg格式检测失败，退出码: {ExitCode}", process.ExitCode);
                    return formats;
                }

                // 解析格式列表
                var lines = output.Split('\n');
                var formatRegex = new Regex(@"^\s*[DE ][E ]\s+(\S+)\s+(.+)$", RegexOptions.Compiled);

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    if (string.IsNullOrEmpty(trimmedLine) || 
                        trimmedLine.StartsWith("---") || 
                        trimmedLine.Contains("File formats:"))
                        continue;

                    var match = formatRegex.Match(line);
                    if (match.Success)
                    {
                        var formatName = match.Groups[1].Value;
                        formats.Add(formatName);
                    }
                }

                _logger.LogInformation("检测到 {Count} 个{Type}格式", 
                    formats.Count, isInput ? "输入" : "输出");

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检测FFmpeg格式时发生异常");
            }

            return formats;
        }

        /// <summary>
        /// 获取格式的详细信息
        /// </summary>
        public async Task<FormatInfo?> GetFormatInfoAsync(string format)
        {
            try
            {
                if (!_ffmpegConfig.IsInitialized)
                    return null;

                var arguments = $"-f {format} -h";
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

                if (process.ExitCode == 0)
                {
                    return ParseFormatInfo(format, output);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取格式信息失败: {Format}", format);
            }

            return null;
        }

        /// <summary>
        /// 解析格式信息
        /// </summary>
        private FormatInfo ParseFormatInfo(string format, string output)
        {
            var info = new FormatInfo
            {
                Name = format,
                Description = ExtractDescription(output),
                SupportedCodecs = ExtractSupportedCodecs(output),
                Extensions = ExtractExtensions(output)
            };

            return info;
        }

        private string ExtractDescription(string output)
        {
            var lines = output.Split('\n');
            foreach (var line in lines)
            {
                if (line.Contains("Common extensions:") || line.Contains("Description:"))
                {
                    var parts = line.Split(':');
                    if (parts.Length > 1)
                        return parts[1].Trim();
                }
            }
            return "";
        }

        private List<string> ExtractSupportedCodecs(string output)
        {
            var codecs = new List<string>();
            var lines = output.Split('\n');
            
            foreach (var line in lines)
            {
                if (line.Contains("Supported codecs:"))
                {
                    var codecLine = line.Substring(line.IndexOf(':') + 1).Trim();
                    codecs.AddRange(codecLine.Split(' ', StringSplitOptions.RemoveEmptyEntries));
                    break;
                }
            }

            return codecs;
        }

        private List<string> ExtractExtensions(string output)
        {
            var extensions = new List<string>();
            var lines = output.Split('\n');
            
            foreach (var line in lines)
            {
                if (line.Contains("Common extensions:"))
                {
                    var extLine = line.Substring(line.IndexOf(':') + 1).Trim();
                    extensions.AddRange(extLine.Split(',', ' ', StringSplitOptions.RemoveEmptyEntries)
                        .Select(e => e.Trim().TrimStart('.')));
                    break;
                }
            }

            return extensions;
        }
    }

    /// <summary>
    /// 格式信息
    /// </summary>
    public class FormatInfo
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public List<string> SupportedCodecs { get; set; } = new();
        public List<string> Extensions { get; set; } = new();
    }
}
