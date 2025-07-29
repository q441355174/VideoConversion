using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using VideoConversion_Client.Services;
using VideoConversion_Client.Utils;

namespace VideoConversion_Client.ViewModels
{
    /// <summary>
    /// 服务器状态面板的ViewModel
    /// </summary>
    public class ServerStatusViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _apiService;
        private readonly SignalRService _signalRService;
        private readonly DiskSpaceApiService _diskSpaceApiService;
        private readonly System.Timers.Timer _refreshTimer;

        public event PropertyChangedEventHandler? PropertyChanged;

        #region 服务器连接状态
        private bool _isServerConnected = false;
        public bool IsServerConnected
        {
            get => _isServerConnected;
            set => SetProperty(ref _isServerConnected, value);
        }

        private bool _isSignalRConnected = false;
        public bool IsSignalRConnected
        {
            get => _isSignalRConnected;
            set => SetProperty(ref _isSignalRConnected, value);
        }

        private string _serverStatusText = "检测中...";
        public string ServerStatusText
        {
            get => _serverStatusText;
            set => SetProperty(ref _serverStatusText, value);
        }

        private string _signalRStatusText = "未连接";
        public string SignalRStatusText
        {
            get => _signalRStatusText;
            set => SetProperty(ref _signalRStatusText, value);
        }
        #endregion

        #region 磁盘空间状态
        private long _usedSpace = 0;
        public long UsedSpace
        {
            get => _usedSpace;
            set => SetProperty(ref _usedSpace, value);
        }

        private long _totalSpace = 100L * 1024 * 1024 * 1024; // 默认100GB
        public long TotalSpace
        {
            get => _totalSpace;
            set => SetProperty(ref _totalSpace, value);
        }

        private long _availableSpace = 100L * 1024 * 1024 * 1024;
        public long AvailableSpace
        {
            get => _availableSpace;
            set => SetProperty(ref _availableSpace, value);
        }

        public string UsedSpaceText => FormatBytes(UsedSpace);
        public string TotalSpaceText => FormatBytes(TotalSpace);
        public string AvailableSpaceText => FormatBytes(AvailableSpace);
        public double DiskUsagePercentage => TotalSpace > 0 ? (double)UsedSpace / TotalSpace * 100 : 0;

        private bool _isSpaceWarningVisible = false;
        public bool IsSpaceWarningVisible
        {
            get => _isSpaceWarningVisible;
            set => SetProperty(ref _isSpaceWarningVisible, value);
        }

        private string _spaceWarningText = "";
        public string SpaceWarningText
        {
            get => _spaceWarningText;
            set => SetProperty(ref _spaceWarningText, value);
        }
        #endregion

        #region 当前任务状态
        private bool _hasActiveTask = false;
        public bool HasActiveTask
        {
            get => _hasActiveTask;
            set => SetProperty(ref _hasActiveTask, value);
        }

        private string _currentTaskName = "";
        public string CurrentTaskName
        {
            get => _currentTaskName;
            set => SetProperty(ref _currentTaskName, value);
        }

        private string _currentFileName = "";
        public string CurrentFileName
        {
            get => _currentFileName;
            set => SetProperty(ref _currentFileName, value);
        }

        private int _taskProgress = 0;
        public int TaskProgress
        {
            get => _taskProgress;
            set => SetProperty(ref _taskProgress, value);
        }

        private string _taskProgressText = "";
        public string TaskProgressText
        {
            get => _taskProgressText;
            set => SetProperty(ref _taskProgressText, value);
        }

        private string _taskSpeedText = "";
        public string TaskSpeedText
        {
            get => _taskSpeedText;
            set => SetProperty(ref _taskSpeedText, value);
        }

        private string _taskETAText = "";
        public string TaskETAText
        {
            get => _taskETAText;
            set => SetProperty(ref _taskETAText, value);
        }
        #endregion

        #region 批量任务状态
        private bool _hasBatchTask = false;
        public bool HasBatchTask
        {
            get => _hasBatchTask;
            set => SetProperty(ref _hasBatchTask, value);
        }

        private string _batchProgressText = "";
        public string BatchProgressText
        {
            get => _batchProgressText;
            set => SetProperty(ref _batchProgressText, value);
        }

        private int _batchProgress = 0;
        public int BatchProgress
        {
            get => _batchProgress;
            set => SetProperty(ref _batchProgress, value);
        }

        private bool _isBatchPaused = false;
        public bool IsBatchPaused
        {
            get => _isBatchPaused;
            set => SetProperty(ref _isBatchPaused, value);
        }

        private string _batchPausedText = "";
        public string BatchPausedText
        {
            get => _batchPausedText;
            set => SetProperty(ref _batchPausedText, value);
        }
        #endregion

        #region 系统信息
        private string _serverVersion = "v1.0.0";
        public string ServerVersion
        {
            get => _serverVersion;
            set => SetProperty(ref _serverVersion, value);
        }

        private string _ffmpegVersion = "未知";
        public string FFmpegVersion
        {
            get => _ffmpegVersion;
            set => SetProperty(ref _ffmpegVersion, value);
        }

        private string _hardwareAcceleration = "未知";
        public string HardwareAcceleration
        {
            get => _hardwareAcceleration;
            set => SetProperty(ref _hardwareAcceleration, value);
        }

        private string _uptime = "未知";
        public string Uptime
        {
            get => _uptime;
            set => SetProperty(ref _uptime, value);
        }
        #endregion

        public ServerStatusViewModel(ApiService apiService, SignalRService signalRService)
        {
            _apiService = apiService;
            _signalRService = signalRService;
            _diskSpaceApiService = new DiskSpaceApiService(apiService.BaseUrl);

            // 设置定时刷新
            _refreshTimer = new System.Timers.Timer(30000); // 30秒刷新一次
            _refreshTimer.Elapsed += async (s, e) => await RefreshServerStatus();
            _refreshTimer.AutoReset = true;

            // 监听SignalR事件
            SetupSignalREvents();
        }

        private void SetupSignalREvents()
        {
            _signalRService.Connected += () =>
            {
                IsSignalRConnected = true;
                SignalRStatusText = "已连接";
            };

            _signalRService.Disconnected += () =>
            {
                IsSignalRConnected = false;
                SignalRStatusText = "未连接";
            };

            _signalRService.ProgressUpdated += OnProgressUpdated;
            _signalRService.StatusUpdated += OnStatusUpdated;

            // 磁盘空间事件监听
            _signalRService.DiskSpaceUpdated += OnDiskSpaceUpdated;
            _signalRService.SpaceReleased += OnSpaceReleased;
            _signalRService.SpaceWarning += OnSpaceWarning;
            _signalRService.SpaceConfigChanged += OnSpaceConfigChanged;

            // 监听活动任务响应
            _signalRService.RegisterHandler("ActiveTasks", (data) => OnActiveTasksReceived(data));
        }

        private void OnProgressUpdated(string taskId, int progress, string message, double? speed, int? remainingSeconds)
        {
            HasActiveTask = true;
            TaskProgress = progress;
            TaskProgressText = $"{message} {progress}%";
            TaskSpeedText = speed.HasValue ? $"{speed.Value:F1}x" : "";
            TaskETAText = remainingSeconds.HasValue ? $"预计剩余: {FormatTime(remainingSeconds.Value)}" : "";
        }

        private void OnStatusUpdated(string taskId, string status, string? message)
        {
            if (status == "Completed" || status == "Failed" || status == "Cancelled")
            {
                HasActiveTask = false;
                TaskProgress = 0;
                TaskProgressText = "";
                TaskSpeedText = "";
                TaskETAText = "";
            }
        }

        public async Task StartMonitoring()
        {
            await RefreshServerStatus();
            _refreshTimer.Start();
        }

        public void StopMonitoring()
        {
            _refreshTimer.Stop();
        }

        public async Task RefreshServerStatus()
        {
            try
            {
                // 显示检测状态
                ServerStatusText = "检测中...";
                Utils.Logger.Info("ServerStatus", "开始检测服务器连接状态");

                // 测试服务器连接
                var connected = await _apiService.TestConnectionAsync();
                IsServerConnected = connected;

                if (connected)
                {
                    ServerStatusText = "已连接";
                    Utils.Logger.Info("ServerStatus", "✅ 服务器连接成功");

                    // 获取磁盘空间信息
                    await RefreshDiskSpaceInfo();

                    // 获取系统信息
                    await RefreshSystemInfo();
                }
                else
                {
                    ServerStatusText = "未连接";
                    Utils.Logger.Info("ServerStatus", "❌ 服务器连接失败");

                    // 重置相关状态
                    ResetServerDependentStates();
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Info("ServerStatus", $"刷新服务器状态失败: {ex.Message}");
                IsServerConnected = false;
                ServerStatusText = "连接失败";
                ResetServerDependentStates();
            }
        }

        /// <summary>
        /// 重置依赖服务器的状态
        /// </summary>
        private void ResetServerDependentStates()
        {
            // 重置磁盘空间信息
            UsedSpace = 0;
            TotalSpace = 100L * 1024 * 1024 * 1024; // 默认100GB
            AvailableSpace = 100L * 1024 * 1024 * 1024;
            IsSpaceWarningVisible = false;
            SpaceWarningText = "";

            // 重置任务状态
            HasActiveTask = false;
            HasBatchTask = false;
            CurrentTaskName = "";
            CurrentFileName = "";
            TaskProgress = 0;
            TaskProgressText = "";
            TaskSpeedText = "";
            TaskETAText = "";
            BatchProgressText = "";
            BatchProgress = 0;
            IsBatchPaused = false;
            BatchPausedText = "";
        }

        private async Task RefreshDiskSpaceInfo()
        {
            try
            {
                var spaceUsage = await _diskSpaceApiService.GetSpaceUsageAsync();
                if (spaceUsage?.Success == true)
                {
                    // 更新空间信息（转换为字节）
                    UsedSpace = (long)(spaceUsage.UsedSpaceGB * 1024 * 1024 * 1024);
                    TotalSpace = (long)(spaceUsage.TotalSpaceGB * 1024 * 1024 * 1024);
                    AvailableSpace = (long)(spaceUsage.AvailableSpaceGB * 1024 * 1024 * 1024);

                    // 检查空间警告
                    var usagePercentage = spaceUsage.UsagePercentage;
                    if (usagePercentage > 90)
                    {
                        IsSpaceWarningVisible = true;
                        SpaceWarningText = "磁盘空间严重不足";
                    }
                    else if (usagePercentage > 80)
                    {
                        IsSpaceWarningVisible = true;
                        SpaceWarningText = "磁盘空间不足";
                    }
                    else
                    {
                        IsSpaceWarningVisible = false;
                    }

                    Utils.Logger.Info("ServerStatus", $"磁盘空间信息已更新: 已用={spaceUsage.UsedSpaceGB:F2}GB, 可用={spaceUsage.AvailableSpaceGB:F2}GB");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Info("ServerStatus", $"刷新磁盘空间信息失败: {ex.Message}");
            }
        }

        private async Task RefreshSystemInfo()
        {
            try
            {
                // 获取活动任务状态
                await RefreshActiveTasksInfo();

                // 获取系统信息
                var response = await _apiService.GetSystemStatusAsync();
                if (response.Success && response.Data != null)
                {
                    ServerVersion = response.Data.ServerVersion;
                    FFmpegVersion = response.Data.FFmpegVersion;
                    HardwareAcceleration = response.Data.HardwareAcceleration;
                    Uptime = FormatUptime(response.Data.Uptime);

                    Utils.Logger.Info("ServerStatus", "系统信息已更新");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Info("ServerStatus", $"刷新系统信息失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 刷新活动任务信息
        /// </summary>
        private async Task RefreshActiveTasksInfo()
        {
            try
            {
                // 通过SignalR获取活动任务
                if (_signalRService.IsConnected)
                {
                    await _signalRService.GetActiveTasksAsync();
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Info("ServerStatus", $"获取活动任务失败: {ex.Message}");
                // 如果获取失败，重置任务状态
                HasActiveTask = false;
                HasBatchTask = false;
            }
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:F1} {sizes[order]}";
        }

        private string FormatTime(int seconds)
        {
            var time = TimeSpan.FromSeconds(seconds);
            if (time.TotalHours >= 1)
                return $"{(int)time.TotalHours}小时{time.Minutes}分钟";
            else if (time.TotalMinutes >= 1)
                return $"{time.Minutes}分{time.Seconds}秒";
            else
                return $"{time.Seconds}秒";
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #region 磁盘空间事件处理

        /// <summary>
        /// 处理磁盘空间状态更新
        /// </summary>
        private void OnDiskSpaceUpdated(Services.DiskSpaceStatus spaceStatus)
        {
            try
            {
                // 更新空间信息（转换为字节）
                UsedSpace = (long)(spaceStatus.UsedSpaceGB * 1024 * 1024 * 1024);
                TotalSpace = (long)(spaceStatus.TotalSpaceGB * 1024 * 1024 * 1024);
                AvailableSpace = (long)(spaceStatus.AvailableSpaceGB * 1024 * 1024 * 1024);

                // 检查空间警告
                if (spaceStatus.UsagePercentage > 90)
                {
                    IsSpaceWarningVisible = true;
                    SpaceWarningText = "磁盘空间严重不足";
                }
                else if (spaceStatus.UsagePercentage > 80)
                {
                    IsSpaceWarningVisible = true;
                    SpaceWarningText = "磁盘空间不足";
                }
                else
                {
                    IsSpaceWarningVisible = false;
                }

                Utils.Logger.Info("ServerStatus", $"实时磁盘空间更新: 已用={spaceStatus.UsedSpaceGB:F2}GB, 可用={spaceStatus.AvailableSpaceGB:F2}GB, 使用率={spaceStatus.UsagePercentage:F1}%");
            }
            catch (Exception ex)
            {
                Utils.Logger.Info("ServerStatus", $"处理磁盘空间更新失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理空间释放通知
        /// </summary>
        private void OnSpaceReleased(Services.SpaceReleaseNotification notification)
        {
            try
            {
                Utils.Logger.Info("ServerStatus", $"收到空间释放通知: {notification.ReleasedMB:F2}MB, 原因: {notification.Reason}");

                // 可以在这里触发空间信息刷新
                _ = Task.Run(async () => await RefreshDiskSpaceInfo());
            }
            catch (Exception ex)
            {
                Utils.Logger.Info("ServerStatus", $"处理空间释放通知失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理空间警告通知
        /// </summary>
        private void OnSpaceWarning(Services.SpaceWarningNotification warning)
        {
            try
            {
                IsSpaceWarningVisible = true;
                SpaceWarningText = warning.Message ?? "磁盘空间不足";

                Utils.Logger.Info("ServerStatus", $"收到空间警告: {warning.Message}, 使用率: {warning.UsagePercentage:F1}%");
            }
            catch (Exception ex)
            {
                Utils.Logger.Info("ServerStatus", $"处理空间警告失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理空间配置变更通知
        /// </summary>
        private void OnSpaceConfigChanged(Services.DiskSpaceConfigNotification configNotification)
        {
            try
            {
                // 更新总空间配置
                TotalSpace = (long)(configNotification.MaxTotalSpaceGB * 1024 * 1024 * 1024);

                Utils.Logger.Info("ServerStatus", $"收到空间配置变更: 最大={configNotification.MaxTotalSpaceGB:F2}GB, 保留={configNotification.ReservedSpaceGB:F2}GB");

                // 刷新空间使用情况
                _ = Task.Run(async () => await RefreshDiskSpaceInfo());
            }
            catch (Exception ex)
            {
                Utils.Logger.Info("ServerStatus", $"处理空间配置变更失败: {ex.Message}");
            }
        }

        #endregion

        #region 活动任务处理

        /// <summary>
        /// 处理活动任务响应
        /// </summary>
        private void OnActiveTasksReceived(System.Text.Json.JsonElement data)
        {
            try
            {
                var json = data.GetRawText();
                var tasks = System.Text.Json.JsonSerializer.Deserialize<List<ActiveTaskInfo>>(json, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (tasks != null && tasks.Any())
                {
                    UpdateTaskStatusFromActiveTasks(tasks);
                }
                else
                {
                    // 没有活动任务
                    HasActiveTask = false;
                    HasBatchTask = false;
                    CurrentTaskName = "";
                    CurrentFileName = "";
                    TaskProgress = 0;
                    TaskProgressText = "";
                    TaskSpeedText = "";
                    TaskETAText = "";
                    BatchProgressText = "";
                    BatchProgress = 0;
                    IsBatchPaused = false;
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Info("ServerStatus", $"处理活动任务响应失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 根据活动任务更新状态
        /// </summary>
        private void UpdateTaskStatusFromActiveTasks(List<ActiveTaskInfo> tasks)
        {
            // 统计任务状态
            var convertingTasks = tasks.Where(t => t.Status == "Converting").ToList();
            var pendingTasks = tasks.Where(t => t.Status == "Pending").ToList();
            var allTasks = tasks.ToList();

            // 更新当前任务状态
            if (convertingTasks.Any())
            {
                var currentTask = convertingTasks.First();
                HasActiveTask = true;
                CurrentTaskName = currentTask.TaskName ?? "转换任务";
                CurrentFileName = currentTask.OriginalFileName ?? "";
                TaskProgress = currentTask.Progress;
                TaskProgressText = $"正在转换 {currentTask.Progress}%";

                // 如果有速度和时间信息，更新它们
                if (currentTask.ConversionSpeed.HasValue)
                {
                    TaskSpeedText = $"{currentTask.ConversionSpeed.Value:F1}x";
                }
                if (currentTask.EstimatedTimeRemaining.HasValue)
                {
                    TaskETAText = $"预计剩余: {FormatTime(currentTask.EstimatedTimeRemaining.Value)}";
                }
            }
            else
            {
                HasActiveTask = false;
                CurrentTaskName = "";
                CurrentFileName = "";
                TaskProgress = 0;
                TaskProgressText = "";
                TaskSpeedText = "";
                TaskETAText = "";
            }

            // 更新批量任务状态
            if (allTasks.Count > 1)
            {
                HasBatchTask = true;
                var completedCount = tasks.Count(t => t.Status == "Completed");
                var totalCount = allTasks.Count;
                BatchProgress = totalCount > 0 ? (completedCount * 100 / totalCount) : 0;
                BatchProgressText = $"{completedCount}/{totalCount} 文件完成";

                // 检查是否有暂停的任务
                IsBatchPaused = tasks.Any(t => t.Status == "Paused");
                if (IsBatchPaused)
                {
                    BatchPausedText = "因空间不足暂停";
                }
            }
            else
            {
                HasBatchTask = false;
                BatchProgressText = "";
                BatchProgress = 0;
                IsBatchPaused = false;
                BatchPausedText = "";
            }
        }

        /// <summary>
        /// 格式化运行时间
        /// </summary>
        private string FormatUptime(TimeSpan uptime)
        {
            if (uptime.TotalDays >= 1)
                return $"{(int)uptime.TotalDays}天 {uptime.Hours}小时";
            else if (uptime.TotalHours >= 1)
                return $"{uptime.Hours}小时 {uptime.Minutes}分钟";
            else
                return $"{uptime.Minutes}分钟";
        }

        #endregion

        public void Dispose()
        {
            _refreshTimer?.Stop();
            _refreshTimer?.Dispose();
            _diskSpaceApiService?.Dispose();
        }
    }

    /// <summary>
    /// 活动任务信息
    /// </summary>
    public class ActiveTaskInfo
    {
        public string? TaskId { get; set; }
        public string? TaskName { get; set; }
        public string? OriginalFileName { get; set; }
        public string? Status { get; set; }
        public int Progress { get; set; }
        public double? ConversionSpeed { get; set; }
        public int? EstimatedTimeRemaining { get; set; }
        public DateTime? StartedAt { get; set; }
        public string? InputFormat { get; set; }
        public string? OutputFormat { get; set; }
    }
}
