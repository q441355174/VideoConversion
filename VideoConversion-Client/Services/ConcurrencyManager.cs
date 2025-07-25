using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace VideoConversion_Client.Services
{
    /// <summary>
    /// 并发管理器 - 控制上传和下载的并发数量
    /// </summary>
    public class ConcurrencyManager
    {
        private static ConcurrencyManager? _instance;
        private static readonly object _lock = new object();

        private SemaphoreSlim _uploadSemaphore;
        private SemaphoreSlim _downloadSemaphore;
        private readonly ConcurrentDictionary<string, TaskInfo> _activeTasks;

        public static ConcurrencyManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new ConcurrencyManager();
                        }
                    }
                }
                return _instance;
            }
        }

        private ConcurrencyManager()
        {
            var settingsService = SystemSettingsService.Instance;
            _uploadSemaphore = new SemaphoreSlim(settingsService.GetMaxConcurrentUploads(), settingsService.GetMaxConcurrentUploads());
            _downloadSemaphore = new SemaphoreSlim(settingsService.GetMaxConcurrentDownloads(), settingsService.GetMaxConcurrentDownloads());
            _activeTasks = new ConcurrentDictionary<string, TaskInfo>();

            // 监听设置变化
            settingsService.SettingsChanged += OnSettingsChanged;
        }

        /// <summary>
        /// 执行上传任务（受并发限制）
        /// </summary>
        public async Task<T> ExecuteUploadAsync<T>(string taskId, Func<Task<T>> uploadTask)
        {
            await _uploadSemaphore.WaitAsync();
            
            try
            {
                var taskInfo = new TaskInfo
                {
                    TaskId = taskId,
                    Type = TaskType.Upload,
                    StartTime = DateTime.Now
                };
                
                _activeTasks.TryAdd(taskId, taskInfo);
                
                System.Diagnostics.Debug.WriteLine($"开始上传任务: {taskId}, 当前上传任务数: {GetActiveUploadCount()}");
                
                return await uploadTask();
            }
            finally
            {
                _activeTasks.TryRemove(taskId, out _);
                _uploadSemaphore.Release();
                
                System.Diagnostics.Debug.WriteLine($"完成上传任务: {taskId}, 当前上传任务数: {GetActiveUploadCount()}");
            }
        }

        /// <summary>
        /// 执行下载任务（受并发限制）
        /// </summary>
        public async Task<T> ExecuteDownloadAsync<T>(string taskId, Func<Task<T>> downloadTask)
        {
            await _downloadSemaphore.WaitAsync();
            
            try
            {
                var taskInfo = new TaskInfo
                {
                    TaskId = taskId,
                    Type = TaskType.Download,
                    StartTime = DateTime.Now
                };
                
                _activeTasks.TryAdd(taskId, taskInfo);
                
                System.Diagnostics.Debug.WriteLine($"开始下载任务: {taskId}, 当前下载任务数: {GetActiveDownloadCount()}");
                
                return await downloadTask();
            }
            finally
            {
                _activeTasks.TryRemove(taskId, out _);
                _downloadSemaphore.Release();
                
                System.Diagnostics.Debug.WriteLine($"完成下载任务: {taskId}, 当前下载任务数: {GetActiveDownloadCount()}");
            }
        }

        /// <summary>
        /// 获取当前活跃的上传任务数量
        /// </summary>
        public int GetActiveUploadCount()
        {
            var count = 0;
            foreach (var task in _activeTasks.Values)
            {
                if (task.Type == TaskType.Upload)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 获取当前活跃的下载任务数量
        /// </summary>
        public int GetActiveDownloadCount()
        {
            var count = 0;
            foreach (var task in _activeTasks.Values)
            {
                if (task.Type == TaskType.Download)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 获取并发限制信息
        /// </summary>
        public ConcurrencyInfo GetConcurrencyInfo()
        {
            return new ConcurrencyInfo
            {
                MaxUploads = _uploadSemaphore.CurrentCount + GetActiveUploadCount(),
                MaxDownloads = _downloadSemaphore.CurrentCount + GetActiveDownloadCount(),
                ActiveUploads = GetActiveUploadCount(),
                ActiveDownloads = GetActiveDownloadCount(),
                AvailableUploadSlots = _uploadSemaphore.CurrentCount,
                AvailableDownloadSlots = _downloadSemaphore.CurrentCount
            };
        }

        /// <summary>
        /// 处理设置变化
        /// </summary>
        private void OnSettingsChanged(object? sender, SystemSettingsChangedEventArgs e)
        {
            if (e.ConcurrencySettingsChanged)
            {
                // 重新创建信号量
                var oldUploadSemaphore = _uploadSemaphore;
                var oldDownloadSemaphore = _downloadSemaphore;

                _uploadSemaphore = new SemaphoreSlim(e.NewSettings.MaxConcurrentUploads, e.NewSettings.MaxConcurrentUploads);
                _downloadSemaphore = new SemaphoreSlim(e.NewSettings.MaxConcurrentDownloads, e.NewSettings.MaxConcurrentDownloads);

                // 释放旧的信号量
                oldUploadSemaphore?.Dispose();
                oldDownloadSemaphore?.Dispose();

                System.Diagnostics.Debug.WriteLine($"并发设置已更新 - 上传: {e.NewSettings.MaxConcurrentUploads}, 下载: {e.NewSettings.MaxConcurrentDownloads}");
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _uploadSemaphore?.Dispose();
            _downloadSemaphore?.Dispose();
        }
    }

    /// <summary>
    /// 任务信息
    /// </summary>
    public class TaskInfo
    {
        public string TaskId { get; set; } = "";
        public TaskType Type { get; set; }
        public DateTime StartTime { get; set; }
    }

    /// <summary>
    /// 任务类型
    /// </summary>
    public enum TaskType
    {
        Upload,
        Download
    }

    /// <summary>
    /// 并发信息
    /// </summary>
    public class ConcurrencyInfo
    {
        public int MaxUploads { get; set; }
        public int MaxDownloads { get; set; }
        public int ActiveUploads { get; set; }
        public int ActiveDownloads { get; set; }
        public int AvailableUploadSlots { get; set; }
        public int AvailableDownloadSlots { get; set; }

        public string GetSummary()
        {
            return $"上传: {ActiveUploads}/{MaxUploads}, 下载: {ActiveDownloads}/{MaxDownloads}";
        }
    }
}
