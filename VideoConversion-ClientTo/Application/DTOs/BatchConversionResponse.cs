using System.Collections.Generic;

namespace VideoConversion_ClientTo.Application.DTOs
{
    /// <summary>
    /// 批量转换响应 - 与Client项目一致
    /// </summary>
    public class BatchConversionResponse
    {
        /// <summary>
        /// 批次ID
        /// </summary>
        public string BatchId { get; set; } = "";

        /// <summary>
        /// 总文件数
        /// </summary>
        public int TotalFiles { get; set; }

        /// <summary>
        /// 成功数量
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// 转换结果列表
        /// </summary>
        public List<ConversionTaskResult> Results { get; set; } = new();
    }

    /// <summary>
    /// 转换任务结果
    /// </summary>
    public class ConversionTaskResult
    {
        /// <summary>
        /// 文件路径
        /// </summary>
        public string FilePath { get; set; } = "";

        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 任务ID
        /// </summary>
        public string? TaskId { get; set; }

        /// <summary>
        /// 消息
        /// </summary>
        public string Message { get; set; } = "";
    }

    /// <summary>
    /// 单文件转换响应
    /// </summary>
    public class StartConversionResponse
    {
        /// <summary>
        /// 任务ID
        /// </summary>
        public string TaskId { get; set; } = "";

        /// <summary>
        /// 消息
        /// </summary>
        public string Message { get; set; } = "";
    }

    /// <summary>
    /// 上传进度
    /// </summary>
    public class UploadProgress
    {
        /// <summary>
        /// 进度百分比
        /// </summary>
        public double Percentage { get; set; }

        /// <summary>
        /// 已上传字节数
        /// </summary>
        public long BytesUploaded { get; set; }

        /// <summary>
        /// 总字节数
        /// </summary>
        public long TotalBytes { get; set; }

        /// <summary>
        /// 上传速度 (bytes/second)
        /// </summary>
        public double Speed { get; set; }

        /// <summary>
        /// 预估剩余时间 (seconds)
        /// </summary>
        public double? EstimatedTimeRemaining { get; set; }
    }

    /// <summary>
    /// 批量上传进度 - 与Client项目一致
    /// </summary>
    public class BatchUploadProgress
    {
        /// <summary>
        /// 批次ID
        /// </summary>
        public string BatchId { get; set; } = "";

        /// <summary>
        /// 当前文件
        /// </summary>
        public string CurrentFile { get; set; } = "";

        /// <summary>
        /// 当前文件进度
        /// </summary>
        public double CurrentFileProgress { get; set; }

        /// <summary>
        /// 已完成文件数
        /// </summary>
        public int CompletedFiles { get; set; }

        /// <summary>
        /// 总文件数
        /// </summary>
        public int TotalFiles { get; set; }

        /// <summary>
        /// 总体进度
        /// </summary>
        public double OverallProgress { get; set; }

        /// <summary>
        /// 状态
        /// </summary>
        public string Status { get; set; } = "";

        /// <summary>
        /// 是否暂停
        /// </summary>
        public bool IsPaused { get; set; }

        /// <summary>
        /// 暂停原因
        /// </summary>
        public string PauseReason { get; set; } = "";
    }
}
