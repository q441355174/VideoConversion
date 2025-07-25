using System;

namespace VideoConversion_Client.Utils
{
    /// <summary>
    /// 文件大小格式化工具类
    /// </summary>
    public static class FileSizeFormatter
    {
        /// <summary>
        /// 格式化字节数为可读的文件大小
        /// </summary>
        /// <param name="bytes">字节数</param>
        /// <param name="decimalPlaces">小数位数</param>
        /// <returns>格式化后的文件大小字符串</returns>
        public static string FormatBytes(long bytes, int decimalPlaces = 2)
        {
            if (bytes == 0)
                return "0 B";

            if (bytes < 0)
                return $"-{FormatBytes(-bytes, decimalPlaces)}";

            string[] sizes = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len.ToString($"F{decimalPlaces}")} {sizes[order]}";
        }

        /// <summary>
        /// 格式化字节数为可读的文件大小（自动选择小数位数）
        /// </summary>
        /// <param name="bytes">字节数</param>
        /// <returns>格式化后的文件大小字符串</returns>
        public static string FormatBytesAuto(long bytes)
        {
            if (bytes == 0)
                return "0 B";

            if (bytes < 0)
                return $"-{FormatBytesAuto(-bytes)}";

            string[] sizes = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            // 根据大小自动选择小数位数
            int decimalPlaces = order switch
            {
                0 => 0,  // B - 不需要小数
                1 => len < 10 ? 1 : 0,  // KB - 小于10KB显示1位小数
                2 => len < 10 ? 2 : 1,  // MB - 小于10MB显示2位小数，否则1位
                _ => len < 10 ? 2 : 1   // GB及以上 - 小于10显示2位小数，否则1位
            };

            return $"{len.ToString($"F{decimalPlaces}")} {sizes[order]}";
        }

        /// <summary>
        /// 格式化传输速度
        /// </summary>
        /// <param name="bytesPerSecond">每秒字节数</param>
        /// <param name="decimalPlaces">小数位数</param>
        /// <returns>格式化后的速度字符串</returns>
        public static string FormatSpeed(double bytesPerSecond, int decimalPlaces = 1)
        {
            if (bytesPerSecond <= 0)
                return "0 B/s";

            string[] sizes = { "B/s", "KB/s", "MB/s", "GB/s", "TB/s" };
            double speed = bytesPerSecond;
            int order = 0;

            while (speed >= 1024 && order < sizes.Length - 1)
            {
                order++;
                speed = speed / 1024;
            }

            return $"{speed.ToString($"F{decimalPlaces}")} {sizes[order]}";
        }

        /// <summary>
        /// 解析文件大小字符串为字节数
        /// </summary>
        /// <param name="sizeString">文件大小字符串（如 "1.5 MB"）</param>
        /// <returns>字节数，解析失败返回-1</returns>
        public static long ParseSize(string sizeString)
        {
            if (string.IsNullOrWhiteSpace(sizeString))
                return -1;

            try
            {
                var parts = sizeString.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2)
                    return -1;

                if (!double.TryParse(parts[0], out double value))
                    return -1;

                var unit = parts[1].ToUpperInvariant();
                var multiplier = unit switch
                {
                    "B" => 1L,
                    "KB" => 1024L,
                    "MB" => 1024L * 1024L,
                    "GB" => 1024L * 1024L * 1024L,
                    "TB" => 1024L * 1024L * 1024L * 1024L,
                    "PB" => 1024L * 1024L * 1024L * 1024L * 1024L,
                    _ => -1L
                };

                if (multiplier == -1)
                    return -1;

                return (long)(value * multiplier);
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// 获取文件大小的简短表示（用于UI显示）
        /// </summary>
        /// <param name="bytes">字节数</param>
        /// <returns>简短的文件大小字符串</returns>
        public static string GetShortSize(long bytes)
        {
            if (bytes == 0)
                return "0";

            if (bytes < 0)
                return $"-{GetShortSize(-bytes)}";

            string[] sizes = { "B", "K", "M", "G", "T" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            // 简短格式，最多1位小数
            if (order == 0)
                return $"{(int)len}{sizes[order]}";
            else
                return $"{len:F1}{sizes[order]}";
        }

        /// <summary>
        /// 比较两个文件大小
        /// </summary>
        /// <param name="size1">文件大小1</param>
        /// <param name="size2">文件大小2</param>
        /// <returns>比较结果：-1表示size1小于size2，0表示相等，1表示size1大于size2</returns>
        public static int CompareSize(string size1, string size2)
        {
            var bytes1 = ParseSize(size1);
            var bytes2 = ParseSize(size2);

            if (bytes1 == -1 || bytes2 == -1)
                return 0; // 解析失败，认为相等

            return bytes1.CompareTo(bytes2);
        }

        /// <summary>
        /// 获取文件大小的百分比表示
        /// </summary>
        /// <param name="currentBytes">当前字节数</param>
        /// <param name="totalBytes">总字节数</param>
        /// <returns>百分比字符串</returns>
        public static string GetPercentage(long currentBytes, long totalBytes)
        {
            if (totalBytes <= 0)
                return "0%";

            var percentage = (double)currentBytes / totalBytes * 100;
            return $"{percentage:F1}%";
        }

        /// <summary>
        /// 估算剩余时间
        /// </summary>
        /// <param name="currentBytes">已传输字节数</param>
        /// <param name="totalBytes">总字节数</param>
        /// <param name="bytesPerSecond">传输速度（字节/秒）</param>
        /// <returns>剩余时间字符串</returns>
        public static string EstimateRemainingTime(long currentBytes, long totalBytes, double bytesPerSecond)
        {
            if (bytesPerSecond <= 0 || currentBytes >= totalBytes)
                return "未知";

            var remainingBytes = totalBytes - currentBytes;
            var remainingSeconds = remainingBytes / bytesPerSecond;

            if (remainingSeconds < 60)
                return $"{remainingSeconds:F0}秒";
            else if (remainingSeconds < 3600)
                return $"{remainingSeconds / 60:F0}分钟";
            else
                return $"{remainingSeconds / 3600:F1}小时";
        }
    }
}
