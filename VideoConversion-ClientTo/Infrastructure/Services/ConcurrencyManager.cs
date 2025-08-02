using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using VideoConversion_ClientTo.Infrastructure;

namespace VideoConversion_ClientTo.Infrastructure.Services
{
    /// <summary>
    /// 并发管理器 - 基于Client项目的完整实现
    /// </summary>
    public class ConcurrencyManager
    {
        private static ConcurrencyManager? _instance;
        private static readonly object _lock = new object();

        private SemaphoreSlim _uploadSemaphore;
        private SemaphoreSlim _downloadSemaphore;
        private readonly ConcurrentDictionary<string, TaskInfo> _activeTasks;

        // 默认并发限制
        private int _maxConcurrentUploads = 3;
        private int _maxConcurrentDownloads = 2;

        public static ConcurrencyManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new ConcurrencyManager();
                    }
                }
                return _instance;
            }
        }

        private ConcurrencyManager()
        {
            // 🔑 从系统设置服务获取并发限制 - 与Client项目完全一致
            var settingsService = SystemSettingsService.Instance;
            _maxConcurrentUploads = settingsService.GetMaxConcurrentUploads();
            _maxConcurrentDownloads = settingsService.GetMaxConcurrentDownloads();

            _uploadSemaphore = new SemaphoreSlim(_maxConcurrentUploads, _maxConcurrentUploads);
            _downloadSemaphore = new SemaphoreSlim(_maxConcurrentDownloads, _maxConcurrentDownloads);
            _activeTasks = new ConcurrentDictionary<string, TaskInfo>();

            // 🔑 监听设置变化 - 与Client项目完全一致
            settingsService.SettingsChanged += OnSettingsChanged;

            Utils.Logger.Info("ConcurrencyManager",
                $"✅ 并发管理器已初始化 - 上传: {_maxConcurrentUploads}, 下载: {_maxConcurrentDownloads}");
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
                
                Utils.Logger.Debug("ConcurrencyManager", 
                    $"🚀 开始上传任务: {taskId}, 当前上传任务数: {GetActiveUploadCount()}");
                
                return await uploadTask();
            }
            finally
            {
                _activeTasks.TryRemove(taskId, out _);
                _uploadSemaphore.Release();
                
                Utils.Logger.Debug("ConcurrencyManager", 
                    $"✅ 完成上传任务: {taskId}, 当前上传任务数: {GetActiveUploadCount()}");
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
                
                Utils.Logger.Debug("ConcurrencyManager", 
                    $"📥 开始下载任务: {taskId}, 当前下载任务数: {GetActiveDownloadCount()}");
                
                return await downloadTask();
            }
            finally
            {
                _activeTasks.TryRemove(taskId, out _);
                _downloadSemaphore.Release();
                
                Utils.Logger.Debug("ConcurrencyManager", 
                    $"✅ 完成下载任务: {taskId}, 当前下载任务数: {GetActiveDownloadCount()}");
            }
        }

        /// <summary>
        /// 执行转换任务（无并发限制，由服务端控制）
        /// </summary>
        public async Task<T> ExecuteConversionAsync<T>(string taskId, Func<Task<T>> conversionTask)
        {
            try
            {
                var taskInfo = new TaskInfo
                {
                    TaskId = taskId,
                    Type = TaskType.Conversion,
                    StartTime = DateTime.Now
                };
                
                _activeTasks.TryAdd(taskId, taskInfo);
                
                // 开始转换任务（移除日志）
                
                return await conversionTask();
            }
            finally
            {
                _activeTasks.TryRemove(taskId, out _);
                
                Utils.Logger.Debug("ConcurrencyManager", 
                    $"✅ 完成转换任务: {taskId}, 当前转换任务数: {GetActiveConversionCount()}");
            }
        }

        /// <summary>
        /// 更新并发设置
        /// </summary>
        public void UpdateConcurrencySettings(int maxUploads, int maxDownloads)
        {
            try
            {
                if (maxUploads > 0 && maxUploads != _maxConcurrentUploads)
                {
                    _maxConcurrentUploads = maxUploads;
                    
                    // 重新创建上传信号量
                    var oldUploadSemaphore = _uploadSemaphore;
                    _uploadSemaphore = new SemaphoreSlim(maxUploads, maxUploads);
                    oldUploadSemaphore.Dispose();
                    
                    Utils.Logger.Info("ConcurrencyManager", $"🔧 更新最大并发上传数: {maxUploads}");
                }

                if (maxDownloads > 0 && maxDownloads != _maxConcurrentDownloads)
                {
                    _maxConcurrentDownloads = maxDownloads;
                    
                    // 重新创建下载信号量
                    var oldDownloadSemaphore = _downloadSemaphore;
                    _downloadSemaphore = new SemaphoreSlim(maxDownloads, maxDownloads);
                    oldDownloadSemaphore.Dispose();
                    
                    Utils.Logger.Info("ConcurrencyManager", $"🔧 更新最大并发下载数: {maxDownloads}");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConcurrencyManager", $"❌ 更新并发设置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取活跃上传任务数量
        /// </summary>
        public int GetActiveUploadCount()
        {
            return CountTasksByType(TaskType.Upload);
        }

        /// <summary>
        /// 获取活跃下载任务数量
        /// </summary>
        public int GetActiveDownloadCount()
        {
            return CountTasksByType(TaskType.Download);
        }

        /// <summary>
        /// 获取活跃转换任务数量
        /// </summary>
        public int GetActiveConversionCount()
        {
            return CountTasksByType(TaskType.Conversion);
        }

        /// <summary>
        /// 获取总活跃任务数量
        /// </summary>
        public int GetTotalActiveTaskCount()
        {
            return _activeTasks.Count;
        }

        /// <summary>
        /// 按类型统计任务数量
        /// </summary>
        private int CountTasksByType(TaskType type)
        {
            int count = 0;
            foreach (var task in _activeTasks.Values)
            {
                if (task.Type == type)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 获取任务统计信息
        /// </summary>
        public ConcurrencyStatistics GetStatistics()
        {
            return new ConcurrencyStatistics
            {
                MaxConcurrentUploads = _maxConcurrentUploads,
                MaxConcurrentDownloads = _maxConcurrentDownloads,
                ActiveUploads = GetActiveUploadCount(),
                ActiveDownloads = GetActiveDownloadCount(),
                ActiveConversions = GetActiveConversionCount(),
                TotalActiveTasks = GetTotalActiveTaskCount(),
                AvailableUploadSlots = _uploadSemaphore.CurrentCount,
                AvailableDownloadSlots = _downloadSemaphore.CurrentCount
            };
        }

        /// <summary>
        /// 取消指定任务
        /// </summary>
        public bool CancelTask(string taskId)
        {
            try
            {
                if (_activeTasks.TryRemove(taskId, out var taskInfo))
                {
                    Utils.Logger.Info("ConcurrencyManager", $"🚫 任务已取消: {taskId} ({taskInfo.Type})");
                    
                    // 根据任务类型释放相应的信号量
                    switch (taskInfo.Type)
                    {
                        case TaskType.Upload:
                            _uploadSemaphore.Release();
                            break;
                        case TaskType.Download:
                            _downloadSemaphore.Release();
                            break;
                    }
                    
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConcurrencyManager", $"❌ 取消任务失败: {taskId} - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 清理所有活跃任务
        /// </summary>
        public void ClearAllTasks()
        {
            try
            {
                var taskCount = _activeTasks.Count;
                _activeTasks.Clear();
                
                // 重置信号量
                _uploadSemaphore.Dispose();
                _downloadSemaphore.Dispose();
                
                _uploadSemaphore = new SemaphoreSlim(_maxConcurrentUploads, _maxConcurrentUploads);
                _downloadSemaphore = new SemaphoreSlim(_maxConcurrentDownloads, _maxConcurrentDownloads);
                
                Utils.Logger.Info("ConcurrencyManager", $"🧹 已清理所有活跃任务，数量: {taskCount}");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConcurrencyManager", $"❌ 清理任务失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            try
            {
                ClearAllTasks();
                _uploadSemaphore?.Dispose();
                _downloadSemaphore?.Dispose();
                
                Utils.Logger.Info("ConcurrencyManager", "🗑️ 并发管理器资源已释放");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConcurrencyManager", $"❌ 释放资源失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理系统设置变化 - 与Client项目完全一致
        /// </summary>
        private void OnSettingsChanged(object? sender, SystemSettingsChangedEventArgs e)
        {
            if (e.ConcurrencySettingsChanged)
            {
                try
                {
                    // 重新创建信号量 - 与Client项目完全一致
                    var oldUploadSemaphore = _uploadSemaphore;
                    var oldDownloadSemaphore = _downloadSemaphore;

                    _maxConcurrentUploads = e.NewSettings.MaxConcurrentUploads;
                    _maxConcurrentDownloads = e.NewSettings.MaxConcurrentDownloads;

                    _uploadSemaphore = new SemaphoreSlim(_maxConcurrentUploads, _maxConcurrentUploads);
                    _downloadSemaphore = new SemaphoreSlim(_maxConcurrentDownloads, _maxConcurrentDownloads);

                    // 释放旧的信号量
                    oldUploadSemaphore?.Dispose();
                    oldDownloadSemaphore?.Dispose();

                    Utils.Logger.Info("ConcurrencyManager",
                        $"🔧 并发设置已更新 - 上传: {_maxConcurrentUploads}, 下载: {_maxConcurrentDownloads}");
                }
                catch (Exception ex)
                {
                    Utils.Logger.Error("ConcurrencyManager", $"❌ 更新并发设置失败: {ex.Message}");
                }
            }
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
        Download,
        Conversion
    }

    /// <summary>
    /// 并发统计信息
    /// </summary>
    public class ConcurrencyStatistics
    {
        public int MaxConcurrentUploads { get; set; }
        public int MaxConcurrentDownloads { get; set; }
        public int ActiveUploads { get; set; }
        public int ActiveDownloads { get; set; }
        public int ActiveConversions { get; set; }
        public int TotalActiveTasks { get; set; }
        public int AvailableUploadSlots { get; set; }
        public int AvailableDownloadSlots { get; set; }
    }
}
