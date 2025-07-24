namespace VideoConversion.Services
{
    /// <summary>
    /// FFmpeg配置服务 - 统一管理FFmpeg路径和配置
    /// </summary>
    public class FFmpegConfigurationService
    {
        private readonly ILogger<FFmpegConfigurationService> _logger;
        private readonly IConfiguration _configuration;

        public string FFmpegPath { get; private set; } = "";
        public string FFprobePath { get; private set; } = "";
        public bool IsInitialized { get; private set; } = false;

        public FFmpegConfigurationService(
            ILogger<FFmpegConfigurationService> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            InitializeFFmpeg(); 
        }

        /// <summary>
        /// 初始化FFmpeg配置
        /// </summary>
        private void InitializeFFmpeg()
        {
            try
            {
                // 获取当前程序目录
                var currentDirectory = Environment.CurrentDirectory;
                var ffmpegDirectory = Path.Combine(currentDirectory, "ffmpeg");

                _logger.LogDebug("当前工作目录: {CurrentDirectory}", currentDirectory);
                _logger.LogDebug("FFmpeg目录: {FFmpegDirectory}", ffmpegDirectory);

                // 设置FFmpeg和FFprobe路径
                FFmpegPath = Path.Combine(ffmpegDirectory, "ffmpeg.exe");
                FFprobePath = Path.Combine(ffmpegDirectory, "ffprobe.exe");

                _logger.LogDebug("检查FFmpeg文件: {FFmpegExe}", FFmpegPath);
                _logger.LogDebug("检查FFprobe文件: {FFprobeExe}", FFprobePath);

                if (File.Exists(FFmpegPath) && File.Exists(FFprobePath))
                {
                    IsInitialized = true;
                    _logger.LogInformation("FFmpeg配置完成: {FFmpegPath}", ffmpegDirectory);
                }
                else
                {
                    _logger.LogWarning("FFmpeg二进制文件不存在: ffmpeg={FFmpegExists}, ffprobe={FFprobeExists}", 
                        File.Exists(FFmpegPath), File.Exists(FFprobePath));

                    // 尝试使用系统PATH中的FFmpeg
                    FFmpegPath = "ffmpeg";
                    FFprobePath = "ffprobe";
                    
                    if (ValidateSystemFFmpeg())
                    {
                        IsInitialized = true;
                        _logger.LogInformation("使用系统PATH中的FFmpeg");
                    }
                    else
                    {
                        _logger.LogError("FFmpeg未找到，请确保FFmpeg已正确安装");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "初始化FFmpeg配置失败");
            }
        }

        /// <summary>
        /// 验证系统PATH中的FFmpeg
        /// </summary>
        private bool ValidateSystemFFmpeg()
        {
            try
            {
                using var process = new System.Diagnostics.Process();
                process.StartInfo.FileName = "ffmpeg";
                process.StartInfo.Arguments = "-version";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                process.Start();
                process.WaitForExit(5000); // 5秒超时

                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 验证FFmpeg安装
        /// </summary>
        public bool ValidateFFmpegInstallation()
        {
            if (!IsInitialized)
            {
                _logger.LogWarning("FFmpeg未初始化");
                return false;
            }

            try
            {
                // 检查文件存在性（如果是本地路径）
                if (Path.IsPathRooted(FFmpegPath))
                {
                    if (!File.Exists(FFmpegPath) || !File.Exists(FFprobePath))
                    {
                        _logger.LogError("FFmpeg文件不存在: {FFmpegPath}, {FFprobePath}", FFmpegPath, FFprobePath);
                        return false;
                    }
                }

                // 测试FFmpeg命令
                using var process = new System.Diagnostics.Process();
                process.StartInfo.FileName = FFmpegPath;
                process.StartInfo.Arguments = "-version";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(10000); // 10秒超时

                var success = process.ExitCode == 0 && output.Contains("ffmpeg version");
                
                if (success)
                {
                    _logger.LogInformation("FFmpeg验证成功");
                }
                else
                {
                    _logger.LogError("FFmpeg验证失败，退出码: {ExitCode}", process.ExitCode);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "验证FFmpeg安装时发生异常");
                return false;
            }
        }

        /// <summary>
        /// 获取FFmpeg版本信息
        /// </summary>
        public async Task<string?> GetFFmpegVersionAsync()
        {
            if (!IsInitialized)
                return null;

            try
            {
                using var process = new System.Diagnostics.Process();
                process.StartInfo.FileName = FFmpegPath;
                process.StartInfo.Arguments = "-version";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    var lines = output.Split('\n');
                    var versionLine = lines.FirstOrDefault(l => l.StartsWith("ffmpeg version"));
                    return versionLine?.Trim();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取FFmpeg版本失败");
            }

            return null;
        }

        /// <summary>
        /// 重新初始化FFmpeg配置
        /// </summary>
        public void Reinitialize()
        {
            IsInitialized = false;
            InitializeFFmpeg();
        }
    }
}
