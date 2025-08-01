using System;

namespace VideoConversion_ClientTo.Domain.ValueObjects
{
    /// <summary>
    /// STEP-1: 值对象 - 文件信息
    /// 职责: 封装文件相关信息和验证
    /// </summary>
    public class FileInfo : IEquatable<FileInfo>
    {
        private readonly string _filePath;
        private readonly long _fileSize;
        private readonly string _fileName;

        private FileInfo(string filePath, long fileSize)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            if (fileSize < 0)
                throw new ArgumentException("File size cannot be negative", nameof(fileSize));

            _filePath = filePath;
            _fileSize = fileSize;
            _fileName = System.IO.Path.GetFileName(filePath);
        }

        public static FileInfo Create(string filePath, long fileSize)
        {
            return new FileInfo(filePath, fileSize);
        }

        public string FilePath => _filePath;
        public long FileSize => _fileSize;
        public string FileName => _fileName;
        public string FileExtension => System.IO.Path.GetExtension(_filePath);

        // UI绑定属性
        public string FormattedSize => GetFormattedSize();

        // 业务方法
        public string GetFormattedSize()
        {
            if (_fileSize == 0) return "0 B";

            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double len = _fileSize;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        public bool IsVideoFile()
        {
            var videoExtensions = new[] { ".mp4", ".avi", ".mov", ".mkv", ".wmv", ".flv", ".webm", ".m4v", ".3gp" };
            return Array.Exists(videoExtensions, ext =>
                string.Equals(FileExtension, ext, StringComparison.OrdinalIgnoreCase));
        }

        // 相等性比较
        public bool Equals(FileInfo? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return _filePath == other._filePath && _fileSize == other._fileSize;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as FileInfo);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_filePath, _fileSize);
        }

        public override string ToString()
        {
            return $"{_fileName} ({GetFormattedSize()})";
        }

        // 操作符重载
        public static bool operator ==(FileInfo? left, FileInfo? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(FileInfo? left, FileInfo? right)
        {
            return !Equals(left, right);
        }
    }
}
