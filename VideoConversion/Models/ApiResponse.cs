namespace VideoConversion.Models
{
    /// <summary>
    /// 统一API响应格式
    /// </summary>
    public class ApiResponse
    {
        /// <summary>
        /// 操作是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 响应消息
        /// </summary>
        public string Message { get; set; } = "";

        /// <summary>
        /// 响应时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 错误代码（可选）
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>
        /// 创建成功响应
        /// </summary>
        public static ApiResponse CreateSuccess(string message = "操作成功")
        {
            return new ApiResponse
            {
                Success = true,
                Message = message,
                Timestamp = DateTime.Now
            };
        }

        /// <summary>
        /// 创建错误响应
        /// </summary>
        public static ApiResponse CreateError(string message, string? errorCode = null)
        {
            return new ApiResponse
            {
                Success = false,
                Message = message,
                ErrorCode = errorCode,
                Timestamp = DateTime.Now
            };
        }
    }

    /// <summary>
    /// 带数据的API响应格式
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    public class ApiResponse<T> : ApiResponse
    {
        /// <summary>
        /// 响应数据
        /// </summary>
        public T? Data { get; set; }

        /// <summary>
        /// 创建成功响应（带数据）
        /// </summary>
        public static ApiResponse<T> CreateSuccess(T data, string message = "操作成功")
        {
            return new ApiResponse<T>
            {
                Success = true,
                Message = message,
                Data = data,
                Timestamp = DateTime.Now
            };
        }

        /// <summary>
        /// 创建错误响应（带数据）
        /// </summary>
        public static ApiResponse<T> CreateError(string message, T? data = default, string? errorCode = null)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Message = message,
                Data = data,
                ErrorCode = errorCode,
                Timestamp = DateTime.Now
            };
        }
    }

    /// <summary>
    /// 分页响应格式
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    public class PagedApiResponse<T> : ApiResponse<IEnumerable<T>>
    {
        /// <summary>
        /// 分页信息
        /// </summary>
        public PaginationInfo Pagination { get; set; } = new();

        /// <summary>
        /// 创建分页成功响应
        /// </summary>
        public static PagedApiResponse<T> CreateSuccess(
            IEnumerable<T> data, 
            int page, 
            int pageSize, 
            int totalCount,
            string message = "获取数据成功")
        {
            return new PagedApiResponse<T>
            {
                Success = true,
                Message = message,
                Data = data,
                Pagination = new PaginationInfo
                {
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                },
                Timestamp = DateTime.Now
            };
        }
    }

    /// <summary>
    /// 分页信息
    /// </summary>
    public class PaginationInfo
    {
        /// <summary>
        /// 当前页码
        /// </summary>
        public int Page { get; set; }

        /// <summary>
        /// 每页大小
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// 总记录数
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// 总页数
        /// </summary>
        public int TotalPages { get; set; }

        /// <summary>
        /// 是否有上一页
        /// </summary>
        public bool HasPreviousPage => Page > 1;

        /// <summary>
        /// 是否有下一页
        /// </summary>
        public bool HasNextPage => Page < TotalPages;
    }

    /// <summary>
    /// 任务状态响应
    /// </summary>
    public class TaskStatusResponse
    {
        public string TaskId { get; set; } = "";
        public string Status { get; set; } = "";
        public int Progress { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? OutputFileName { get; set; }
        public long? OutputFileSize { get; set; }
        public TimeSpan? ProcessingTime { get; set; }
    }

    /// <summary>
    /// 文件上传响应
    /// </summary>
    public class FileUploadResponse
    {
        public string FileName { get; set; } = "";
        public string FilePath { get; set; } = "";
        public long FileSize { get; set; }
        public string ContentType { get; set; } = "";
        public string UploadId { get; set; } = "";
        public DateTime UploadTime { get; set; }
    }

    /// <summary>
    /// 系统状态响应
    /// </summary>
    public class SystemStatusResponse
    {
        public string Status { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Components { get; set; } = new();
        public Dictionary<string, string> Versions { get; set; } = new();
        public SystemMetrics Metrics { get; set; } = new();
    }

    /// <summary>
    /// 系统指标
    /// </summary>
    public class SystemMetrics
    {
        public double CpuUsage { get; set; }
        public long MemoryUsage { get; set; }
        public long MemoryTotal { get; set; }
        public long DiskUsage { get; set; }
        public long DiskTotal { get; set; }
        public int ActiveTasks { get; set; }
        public int QueuedTasks { get; set; }
        public TimeSpan Uptime { get; set; }
    }
}
