using System;
using System.IO;
using System.Linq;

namespace VideoConversion.Utils
{
    /// <summary>
    /// 文件名处理工具类
    /// </summary>
    public static class FileNameHelper
    {
        /// <summary>
        /// 确保文件名在指定目录中是唯一的
        /// </summary>
        /// <param name="directoryPath">目录路径</param>
        /// <param name="fileName">原始文件名</param>
        /// <returns>唯一的文件名</returns>
        public static string EnsureUniqueFileName(string directoryPath, string fileName)
        {
            if (string.IsNullOrEmpty(directoryPath))
                throw new ArgumentException("目录路径不能为空", nameof(directoryPath));
            
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentException("文件名不能为空", nameof(fileName));

            var filePath = Path.Combine(directoryPath, fileName);
            
            // 如果文件不存在，直接返回原文件名
            if (!File.Exists(filePath))
                return fileName;

            // 文件存在，生成唯一文件名
            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);
            var counter = 1;

            do
            {
                var newFileName = $"{nameWithoutExt}_{counter}{extension}";
                filePath = Path.Combine(directoryPath, newFileName);
                
                if (!File.Exists(filePath))
                    return newFileName;
                    
                counter++;
            } while (counter < 10000); // 防止无限循环

            // 如果尝试了10000次还没找到唯一名称，使用GUID
            return $"{nameWithoutExt}_{Guid.NewGuid():N}{extension}";
        }

        /// <summary>
        /// 生成唯一文件名（基于时间戳和GUID）
        /// </summary>
        /// <param name="originalFileName">原始文件名</param>
        /// <returns>带时间戳和GUID的唯一文件名</returns>
        public static string GenerateUniqueFileName(string originalFileName)
        {
            if (string.IsNullOrEmpty(originalFileName))
                throw new ArgumentException("文件名不能为空", nameof(originalFileName));

            var extension = Path.GetExtension(originalFileName);
            var nameWithoutExt = Path.GetFileNameWithoutExtension(originalFileName);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var guid = Guid.NewGuid().ToString("N")[..8];
            
            return $"{nameWithoutExt}_{timestamp}_{guid}{extension}";
        }

        /// <summary>
        /// 生成输出文件路径
        /// </summary>
        /// <param name="outputDirectory">输出目录</param>
        /// <param name="originalFileName">原始文件名</param>
        /// <param name="outputFormat">输出格式</param>
        /// <param name="customName">自定义名称</param>
        /// <returns>输出文件路径</returns>
        public static string GenerateOutputFilePath(string outputDirectory, string originalFileName, string outputFormat, string? customName = null)
        {
            var nameWithoutExt = customName ?? Path.GetFileNameWithoutExtension(originalFileName);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"{nameWithoutExt}_converted_{timestamp}.{outputFormat}";
            
            return Path.Combine(outputDirectory, fileName);
        }

        /// <summary>
        /// 生成安全的文件名（移除非法字符）
        /// </summary>
        /// <param name="fileName">原始文件名</param>
        /// <returns>安全的文件名</returns>
        public static string GetSafeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return "unnamed_file";

            // 获取非法字符
            var invalidChars = Path.GetInvalidFileNameChars();
            
            // 替换非法字符为下划线
            foreach (var invalidChar in invalidChars)
            {
                fileName = fileName.Replace(invalidChar, '_');
            }

            // 移除连续的下划线
            while (fileName.Contains("__"))
            {
                fileName = fileName.Replace("__", "_");
            }

            // 移除开头和结尾的下划线和空格
            fileName = fileName.Trim('_', ' ');

            // 如果文件名为空或只有扩展名，使用默认名称
            if (string.IsNullOrEmpty(fileName) || fileName.StartsWith("."))
            {
                var extension = Path.GetExtension(fileName);
                fileName = $"unnamed_file{extension}";
            }

            return fileName;
        }

        /// <summary>
        /// 验证文件名是否有效
        /// </summary>
        /// <param name="fileName">文件名</param>
        /// <returns>是否有效</returns>
        public static bool IsValidFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            // 检查是否包含非法字符
            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var invalidChar in invalidChars)
            {
                if (fileName.Contains(invalidChar))
                    return false;
            }

            // 检查是否为保留名称（Windows）
            var reservedNames = new[]
            {
                "CON", "PRN", "AUX", "NUL",
                "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
            };

            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName).ToUpperInvariant();
            foreach (var reservedName in reservedNames)
            {
                if (nameWithoutExt == reservedName)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 获取文件的显示名称（截断过长的文件名）
        /// </summary>
        /// <param name="fileName">文件名</param>
        /// <param name="maxLength">最大长度</param>
        /// <returns>显示用的文件名</returns>
        public static string GetDisplayFileName(string fileName, int maxLength = 50)
        {
            if (string.IsNullOrEmpty(fileName) || fileName.Length <= maxLength)
                return fileName;

            var extension = Path.GetExtension(fileName);
            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);

            var availableLength = maxLength - extension.Length - 3; // 3 for "..."

            if (availableLength <= 0)
                return fileName.Substring(0, Math.Min(maxLength, fileName.Length));

            return $"{nameWithoutExt.Substring(0, availableLength)}...{extension}";
        }

        /// <summary>
        /// 生成用户友好的下载文件名
        /// </summary>
        /// <param name="originalFileName">原始文件名</param>
        /// <param name="outputFormat">输出格式</param>
        /// <param name="includeConvertedSuffix">是否包含"converted"后缀</param>
        /// <returns>用户友好的下载文件名</returns>
        public static string GenerateDownloadFileName(string originalFileName, string outputFormat, bool includeConvertedSuffix = false)
        {
            if (string.IsNullOrEmpty(originalFileName))
                return $"converted_file.{outputFormat}";

            var nameWithoutExt = Path.GetFileNameWithoutExtension(originalFileName);
            var originalExt = Path.GetExtension(originalFileName).TrimStart('.');

            // 如果输出格式与原始格式相同，且不需要converted后缀，直接返回原始文件名
            if (!includeConvertedSuffix && string.Equals(originalExt, outputFormat, StringComparison.OrdinalIgnoreCase))
            {
                return originalFileName;
            }

            // 如果需要converted后缀或格式不同
            var suffix = includeConvertedSuffix ? "_converted" : "";
            return $"{nameWithoutExt}{suffix}.{outputFormat}";
        }

        /// <summary>
        /// 从服务器文件名中提取原始文件名部分
        /// </summary>
        /// <param name="serverFileName">服务器生成的文件名</param>
        /// <returns>提取的原始文件名部分</returns>
        public static string ExtractOriginalNameFromServerFileName(string serverFileName)
        {
            if (string.IsNullOrEmpty(serverFileName))
                return "unknown_file";

            var nameWithoutExt = Path.GetFileNameWithoutExtension(serverFileName);
            var extension = Path.GetExtension(serverFileName);

            // 移除时间戳和GUID模式: {name}_{timestamp}_{guid}
            // 例如: video_20241225_143022_a1b2c3d4 -> video
            var parts = nameWithoutExt.Split('_');
            if (parts.Length >= 3)
            {
                // 检查最后两部分是否是时间戳和GUID模式
                var lastPart = parts[^1]; // GUID部分
                var secondLastPart = parts[^2]; // 时间戳部分

                if (lastPart.Length == 8 && IsHexString(lastPart) &&
                    secondLastPart.Length == 15 && IsTimestampFormat(secondLastPart))
                {
                    // 移除最后两部分，重新组合原始名称
                    var originalParts = parts.Take(parts.Length - 2);
                    return string.Join("_", originalParts) + extension;
                }
            }

            // 移除"converted"标识: {name}_converted_{timestamp}
            // 例如: video_converted_20241225_143522 -> video
            if (nameWithoutExt.Contains("_converted_"))
            {
                var convertedIndex = nameWithoutExt.LastIndexOf("_converted_");
                if (convertedIndex > 0)
                {
                    return nameWithoutExt.Substring(0, convertedIndex) + extension;
                }
            }

            return serverFileName;
        }

        /// <summary>
        /// 检查字符串是否为十六进制格式
        /// </summary>
        private static bool IsHexString(string str)
        {
            return str.All(c => char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
        }

        /// <summary>
        /// 检查字符串是否为时间戳格式 (yyyyMMdd_HHmmss)
        /// </summary>
        private static bool IsTimestampFormat(string str)
        {
            return str.Length == 15 &&
                   str[8] == '_' &&
                   str.Substring(0, 8).All(char.IsDigit) &&
                   str.Substring(9).All(char.IsDigit);
        }
    }
}
