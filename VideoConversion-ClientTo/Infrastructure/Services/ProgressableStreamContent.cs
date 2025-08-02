using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using VideoConversion_ClientTo.Application.DTOs;
using VideoConversion_ClientTo.Infrastructure;

namespace VideoConversion_ClientTo.Infrastructure.Services
{
    /// <summary>
    /// 支持进度报告的流内容 - 与Client项目一致的实现
    /// </summary>
    public class ProgressableStreamContent : HttpContent
    {
        private readonly Stream _content;
        private readonly IProgress<UploadProgress>? _progress;
        private readonly long _totalBytes;
        private readonly string _fileName;
        private readonly int _bufferSize;

        public ProgressableStreamContent(
            Stream content, 
            IProgress<UploadProgress>? progress, 
            long totalBytes, 
            string fileName,
            int bufferSize = 81920) // 80KB buffer
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _progress = progress;
            _totalBytes = totalBytes;
            _fileName = fileName;
            _bufferSize = bufferSize;

            Utils.Logger.Info("ProgressableStreamContent", 
                $"✅ 初始化进度流: {fileName}, 大小: {totalBytes} bytes, 缓冲区: {bufferSize} bytes");
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            // 开始序列化流（移除日志）
            
            var buffer = new byte[_bufferSize];
            long totalBytesRead = 0;
            var startTime = DateTime.Now;
            var lastProgressReport = DateTime.Now;
            const int progressReportIntervalMs = 100; // 每100ms报告一次进度

            try
            {
                _content.Position = 0; // 确保从头开始读取
                
                int bytesRead;
                while ((bytesRead = await _content.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await stream.WriteAsync(buffer, 0, bytesRead);
                    totalBytesRead += bytesRead;

                    // 限制进度报告频率，避免过于频繁的UI更新
                    var now = DateTime.Now;
                    if ((now - lastProgressReport).TotalMilliseconds >= progressReportIntervalMs || totalBytesRead >= _totalBytes)
                    {
                        var percentage = _totalBytes > 0 ? (double)totalBytesRead / _totalBytes * 100 : 0;
                        var elapsed = now - startTime;
                        var speed = elapsed.TotalSeconds > 0 ? totalBytesRead / elapsed.TotalSeconds : 0;
                        var eta = speed > 0 ? (_totalBytes - totalBytesRead) / speed : 0;

                        _progress?.Report(new UploadProgress
                        {
                            Percentage = Math.Min(percentage, 100), // 确保不超过100%
                            BytesUploaded = totalBytesRead,
                            TotalBytes = _totalBytes,
                            Speed = speed,
                            EstimatedTimeRemaining = eta
                        });

                        lastProgressReport = now;

                        // 上传进度更新（移除日志）
                    }
                }

                // 确保最终进度为100%
                if (_progress != null && totalBytesRead >= _totalBytes)
                {
                    var finalElapsed = DateTime.Now - startTime;
                    var finalSpeed = finalElapsed.TotalSeconds > 0 ? _totalBytes / finalElapsed.TotalSeconds : 0;

                    _progress.Report(new UploadProgress
                    {
                        Percentage = 100,
                        BytesUploaded = _totalBytes,
                        TotalBytes = _totalBytes,
                        Speed = finalSpeed,
                        EstimatedTimeRemaining = 0
                    });
                }

                // 流序列化完成（移除日志）
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ProgressableStreamContent", 
                    $"❌ 流序列化失败: {_fileName} - {ex.Message}");
                throw;
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = _totalBytes;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _content?.Dispose();
                Utils.Logger.Debug("ProgressableStreamContent", $"🗑️ 进度流已释放: {_fileName}");
            }
            base.Dispose(disposing);
        }
    }
}
