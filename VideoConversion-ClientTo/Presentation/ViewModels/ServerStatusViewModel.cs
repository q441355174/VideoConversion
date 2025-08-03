using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using VideoConversion_ClientTo.Application.Interfaces;
using VideoConversion_ClientTo.Application.DTOs;

namespace VideoConversion_ClientTo.Presentation.ViewModels
{
    /// <summary>
    /// 服务器状态视图模型
    /// 职责: 管理服务器连接状态、磁盘空间、任务监控等
    /// </summary>
    public partial class ServerStatusViewModel : ObservableObject
    {
        private readonly IApiClient _apiClient;
        private readonly ISignalRClient _signalRClient;
        private Timer? _statusTimer;
        private bool _isMonitoring = false;

        public ServerStatusViewModel(IApiClient apiClient, ISignalRClient signalRClient)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _signalRClient = signalRClient ?? throw new ArgumentNullException(nameof(signalRClient));

            // 初始化服务器URL
            ServerUrl = _apiClient.BaseUrl ?? "未知";

            // 🔧 初始化磁盘空间状态为未连接状态
            InitializeDisconnectedState();

            // 🔧 设置完整的SignalR事件监听（与Client项目一致）
            SetupSignalREvents();

            Utils.Logger.Info("ServerStatusViewModel", "📡 服务器状态ViewModel已初始化");

            // 🔧 初始化时主动获取服务端最新数据
            _ = Task.Run(async () =>
            {
                try
                {
                    Utils.Logger.Info("ServerStatusViewModel", "🔄 初始化时获取服务端最新数据");
                    await RefreshServerStatus();
                }
                catch (Exception ex)
                {
                    Utils.Logger.Warning("ServerStatusViewModel", $"⚠️ 初始化获取服务端数据失败: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 初始化断开连接状态
        /// </summary>
        private void InitializeDisconnectedState()
        {
            // 服务器连接状态
            IsServerConnected = false;
            IsSignalRConnected = false;
            ServerStatusText = "未连接";

            // 磁盘空间状态
            UsedSpaceText = "未知";
            TotalSpaceText = "未知";
            AvailableSpaceText = "未知";
            DiskUsagePercentage = 0;
            IsSpaceWarningVisible = false;
            SpaceWarningText = "";

            // 任务状态
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

        #region 属性

        [ObservableProperty]
        private bool _isServerConnected = false;

        [ObservableProperty]
        private bool _isSignalRConnected = false;

        [ObservableProperty]
        private string _serverStatusText = "检测中...";

        [ObservableProperty]
        private string _signalRStatusText = "未连接";

        [ObservableProperty]
        private string _serverUrl = "未知";

        [ObservableProperty]
        private string _usedSpaceText = "0 GB";

        [ObservableProperty]
        private string _totalSpaceText = "100 GB";

        [ObservableProperty]
        private string _availableSpaceText = "100 GB";

        [ObservableProperty]
        private double _diskUsagePercentage = 0;

        [ObservableProperty]
        private bool _isSpaceWarningVisible = false;

        [ObservableProperty]
        private string _spaceWarningText = "";

        [ObservableProperty]
        private bool _hasActiveTask = false;

        [ObservableProperty]
        private string _currentTaskName = "";

        [ObservableProperty]
        private string _currentFileName = "";

        [ObservableProperty]
        private string _taskProgressText = "";

        [ObservableProperty]
        private string _taskSpeedText = "";

        [ObservableProperty]
        private string _taskETAText = "";

        [ObservableProperty]
        private double _taskProgress = 0;

        [ObservableProperty]
        private bool _hasBatchTask = false;

        [ObservableProperty]
        private string _batchProgressText = "";

        [ObservableProperty]
        private double _batchProgress = 0;

        [ObservableProperty]
        private bool _isBatchPaused = false;

        [ObservableProperty]
        private string _batchPausedText = "";

        #endregion

        #region 公共方法

        /// <summary>
        /// 开始监控服务器状态
        /// </summary>
        public async Task StartMonitoring()
        {
            if (_isMonitoring) return;

            try
            {
                _isMonitoring = true;
                Utils.Logger.Info("ServerStatusViewModel", "🔄 开始服务器状态监控");

                // 立即检查一次状态
                await RefreshServerStatus();

                // 启动定时器，每30秒检查一次
                _statusTimer = new Timer(async _ => await RefreshServerStatus(), 
                    null, TimeSpan.Zero, TimeSpan.FromSeconds(30));

                // 尝试连接SignalR
                if (!IsSignalRConnected)
                {
                    await _signalRClient.ConnectAsync();
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ServerStatusViewModel", $"❌ 启动监控失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 停止监控
        /// </summary>
        public void StopMonitoring()
        {
            try
            {
                _isMonitoring = false;
                _statusTimer?.Dispose();
                _statusTimer = null;

                Utils.Logger.Info("ServerStatusViewModel", "⏹️ 服务器状态监控已停止");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ServerStatusViewModel", $"❌ 停止监控失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 刷新服务器状态
        /// </summary>
        public async Task RefreshServerStatus()
        {
            try
            {
                // 显示检测状态
                ServerStatusText = "检测中...";
                Utils.Logger.Debug("ServerStatusViewModel", "🔍 开始检测服务器连接状态");

                // 🔧 使用与Client项目一致的连接测试方法
                bool serverConnected = await TestServerConnection();
                IsServerConnected = serverConnected;

                if (serverConnected)
                {
                    ServerStatusText = "已连接";
                    Utils.Logger.Debug("ServerStatusViewModel", "✅ 服务器连接成功");

                    // 🔧 按照Client项目的顺序获取信息
                    await RefreshDiskSpace();
                    await RefreshSystemInfo();
                    await RefreshCurrentTaskStatus();
                }
                else
                {
                    ServerStatusText = "未连接";
                    Utils.Logger.Debug("ServerStatusViewModel", "❌ 服务器连接失败");
                    ResetStatusToDisconnected();
                }
            }
            catch (Exception ex)
            {
                IsServerConnected = false;
                ServerStatusText = "连接失败";
                Utils.Logger.Error("ServerStatusViewModel", $"❌ 刷新服务器状态失败: {ex.Message}");
                ResetStatusToDisconnected();
            }
        }

        /// <summary>
        /// 测试服务器连接（与Client项目一致）
        /// </summary>
        private async Task<bool> TestServerConnection()
        {
            try
            {
                // 🔧 使用与Client项目完全一致的连接测试方法
                var response = await _apiClient.TestConnectionAsync();
                return response;
            }
            catch (Exception ex)
            {
                Utils.Logger.Debug("ServerStatusViewModel", $"连接测试失败: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region 私有方法

        private async Task RefreshDiskSpace()
        {
            try
            {
                // 🔧 使用与Client项目一致的API调用
                var response = await _apiClient.GetDiskSpaceAsync();
                if (response.Success && response.Data != null)
                {
                    var spaceInfo = response.Data;

                    // 🔧 直接使用ApiClientService处理后的数据
                    // ApiClientService已经将TotalSpace计算为用户可用总空间（总空间-保留空间）
                    var usedSpace = spaceInfo.UsedSpace;
                    var userTotalSpace = spaceInfo.TotalSpace; // 这已经是总空间-保留空间
                    var availableSpace = spaceInfo.AvailableSpace;

                    // 更新磁盘空间信息
                    UpdateDiskSpaceInfo(usedSpace, userTotalSpace, availableSpace);

                    // 🔧 添加与Client项目一致的空间警告检查
                    CheckSpaceWarning(usedSpace, userTotalSpace);

                    Utils.Logger.Debug("ServerStatusViewModel", $"✅ 磁盘空间更新: 已用{FormatBytes(usedSpace)}/用户总计{FormatBytes(userTotalSpace)}/可用{FormatBytes(availableSpace)}");
                }
                else
                {
                    Utils.Logger.Warning("ServerStatusViewModel", $"⚠️ 获取磁盘空间失败: {response.Message}");
                    // 🔧 失败时显示未知状态，而不是0值
                    UsedSpaceText = "未知";
                    TotalSpaceText = "未知";
                    AvailableSpaceText = "未知";
                    DiskUsagePercentage = 0;
                    IsSpaceWarningVisible = false;
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Warning("ServerStatusViewModel", $"⚠️ 获取磁盘空间失败: {ex.Message}");
                // 🔧 异常时显示未知状态，而不是0值
                UsedSpaceText = "未知";
                TotalSpaceText = "未知";
                AvailableSpaceText = "未知";
                DiskUsagePercentage = 0;
                IsSpaceWarningVisible = false;
            }
        }

        /// <summary>
        /// 检查空间警告（与Client项目一致）
        /// </summary>
        private void CheckSpaceWarning(long usedSpace, long totalSpace)
        {
            if (totalSpace <= 0) return;

            var usagePercentage = (double)usedSpace / totalSpace * 100;

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
                SpaceWarningText = "";
            }
        }

        /// <summary>
        /// 刷新系统信息（与Client项目一致）
        /// </summary>
        private async Task RefreshSystemInfo()
        {
            try
            {
                // 🔧 暂时跳过系统信息获取，因为API方法不存在
                // TODO: 实现GetSystemStatusAsync方法或使用其他方式获取系统信息
                Utils.Logger.Debug("ServerStatusViewModel", "📋 系统信息刷新跳过（API方法待实现）");

                await Task.CompletedTask; // 避免编译器警告
            }
            catch (Exception ex)
            {
                Utils.Logger.Warning("ServerStatusViewModel", $"⚠️ 刷新系统信息失败: {ex.Message}");
            }
        }

        private async Task RefreshCurrentTaskStatus()
        {
            try
            {
                var response = await _apiClient.GetActiveTasksAsync();
                if (response.Success && response.Data?.Any() == true)
                {
                    var currentTask = response.Data.First();
                    HasActiveTask = true;
                    CurrentTaskName = currentTask.TaskName ?? "";
                    CurrentFileName = currentTask.SourceFileName ?? "";
                    TaskProgress = currentTask.Progress;
                    TaskProgressText = $"转换中... {currentTask.Progress}%";
                    TaskSpeedText = currentTask.Speed?.ToString("0.0x") ?? "";

                    if (currentTask.EstimatedRemainingSeconds.HasValue)
                    {
                        var eta = TimeSpan.FromSeconds(currentTask.EstimatedRemainingSeconds.Value);
                        TaskETAText = $"预计剩余: {eta:mm\\:ss}";
                    }
                }
                else
                {
                    HasActiveTask = false;
                    ResetTaskStatus();
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Warning("ServerStatusViewModel", $"⚠️ 获取任务状态失败: {ex.Message}");
                HasActiveTask = false;
                ResetTaskStatus();
            }
        }

        /// <summary>
        /// 重置状态到断开连接状态（与Client项目一致）
        /// </summary>
        private void ResetStatusToDisconnected()
        {
            // 🔧 重置磁盘空间信息为未知状态，而不是默认值
            UsedSpaceText = "未知";
            TotalSpaceText = "未知";
            AvailableSpaceText = "未知";
            DiskUsagePercentage = 0;
            IsSpaceWarningVisible = false;
            SpaceWarningText = "";

            // 🔧 重置任务状态（与Client项目一致）
            HasActiveTask = false;
            HasBatchTask = false;
            CurrentTaskName = "";
            CurrentFileName = "";
            TaskProgress = 0;
            TaskProgressText = "";
            TaskSpeedText = "";
            TaskETAText = "";

            // 🔧 重置批量任务状态（与Client项目一致）
            BatchProgressText = "";
            BatchProgress = 0;
            IsBatchPaused = false;
            BatchPausedText = "";

            Utils.Logger.Debug("ServerStatusViewModel", "🔄 状态已重置到断开连接状态");
        }

        private void UpdateDiskSpaceInfo(long usedSpace, long totalSpace, long availableSpace)
        {
            UsedSpaceText = FormatBytes(usedSpace);
            TotalSpaceText = FormatBytes(totalSpace);
            AvailableSpaceText = FormatBytes(availableSpace);

            if (totalSpace > 0)
            {
                DiskUsagePercentage = (double)usedSpace / totalSpace * 100;
                
                // 检查空间警告
                var usagePercentage = DiskUsagePercentage;
                if (usagePercentage > 90)
                {
                    IsSpaceWarningVisible = true;
                    SpaceWarningText = "磁盘空间不足，请及时清理";
                }
                else if (usagePercentage > 80)
                {
                    IsSpaceWarningVisible = true;
                    SpaceWarningText = "磁盘空间使用率较高";
                }
                else
                {
                    IsSpaceWarningVisible = false;
                    SpaceWarningText = "";
                }
            }
        }

        private void ResetTaskStatus()
        {
            CurrentTaskName = "";
            CurrentFileName = "";
            TaskProgress = 0;
            TaskProgressText = "";
            TaskSpeedText = "";
            TaskETAText = "";
        }

        private string FormatFileSize(long bytes)
        {
            if (bytes == 0) return "0 B";

            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double len = bytes;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        #endregion

        #region 事件处理

        private void OnSignalRConnected(object? sender, EventArgs e)
        {
            IsSignalRConnected = true;
            SignalRStatusText = "已连接";
            Utils.Logger.Info("ServerStatusViewModel", "✅ SignalR连接成功");
        }

        private void OnSignalRDisconnected(object? sender, string reason)
        {
            IsSignalRConnected = false;
            SignalRStatusText = $"断开连接: {reason}";
            Utils.Logger.Warning("ServerStatusViewModel", $"⚠️ SignalR连接断开: {reason}");
        }

        private void OnDiskSpaceUpdated(object? sender, Application.DTOs.DiskSpaceDto spaceInfo)
        {
            UpdateDiskSpaceInfo(spaceInfo.UsedSpace, spaceInfo.TotalSpace, spaceInfo.AvailableSpace);
        }

        /// <summary>
        /// 设置SignalR事件监听（与Client项目一致）
        /// </summary>
        private void SetupSignalREvents()
        {
            // 🔧 连接状态事件
            _signalRClient.Connected += (sender, e) =>
            {
                IsSignalRConnected = true;
                Utils.Logger.Info("ServerStatusViewModel", "✅ SignalR连接成功");
            };

            _signalRClient.Disconnected += (sender, message) =>
            {
                IsSignalRConnected = false;
                Utils.Logger.Warning("ServerStatusViewModel", $"⚠️ SignalR连接断开: {message}");
            };

            // 🔧 任务相关事件（使用现有的事件）
            _signalRClient.TaskProgressUpdated += OnTaskProgressUpdated;
            _signalRClient.TaskCompleted += OnTaskCompleted;

            // 🔧 磁盘空间更新事件
            _signalRClient.DiskSpaceUpdated += OnDiskSpaceUpdated;

            Utils.Logger.Debug("ServerStatusViewModel", "📡 SignalR事件监听已设置");
        }

        /// <summary>
        /// 处理任务进度更新（与现有SignalR事件一致）
        /// </summary>
        private void OnTaskProgressUpdated(object? sender, ConversionProgressDto progress)
        {
            if (progress != null)
            {
                HasActiveTask = true;
                TaskProgress = Math.Max(0, Math.Min(100, progress.Progress));
                TaskProgressText = $"{progress.Status} {progress.Progress}%";
                TaskSpeedText = progress.Speed.HasValue ? $"{progress.Speed.Value:F1}x" : "";
                TaskETAText = progress.EstimatedRemainingSeconds.HasValue ?
                    $"预计剩余: {FormatTime((int)progress.EstimatedRemainingSeconds.Value)}" : "";

                // 更新当前任务信息
                CurrentTaskName = progress.TaskName;
                CurrentFileName = progress.TaskName; // 使用TaskName作为文件名

                Utils.Logger.Debug("ServerStatusViewModel", $"📊 任务进度更新: {progress.Progress}% - {progress.Status}");
            }
        }

        /// <summary>
        /// 处理任务完成（与现有SignalR事件一致）
        /// </summary>
        private void OnTaskCompleted(object? sender, TaskCompletedDto completed)
        {
            if (completed != null)
            {
                HasActiveTask = false;
                ResetTaskStatus();

                Utils.Logger.Info("ServerStatusViewModel", $"✅ 任务完成: {completed.TaskId} - {(completed.IsSuccess ? "成功" : "失败")}");
            }
        }

        /// <summary>
        /// 处理磁盘空间更新（与现有SignalR事件一致）
        /// </summary>
        private void OnDiskSpaceUpdated(object? sender, object spaceStatus)
        {
            try
            {
                // TODO: 解析磁盘空间状态并更新UI
                Utils.Logger.Debug("ServerStatusViewModel", "💾 收到磁盘空间更新");

                // 触发磁盘空间刷新
                _ = Task.Run(RefreshDiskSpace);
            }
            catch (Exception ex)
            {
                Utils.Logger.Warning("ServerStatusViewModel", $"⚠️ 处理磁盘空间更新失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 格式化时间（与Client项目一致）
        /// </summary>
        private string FormatTime(int seconds)
        {
            var timeSpan = TimeSpan.FromSeconds(seconds);
            if (timeSpan.TotalHours >= 1)
            {
                return $"{timeSpan:h\\:mm\\:ss}";
            }
            else
            {
                return $"{timeSpan:mm\\:ss}";
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 格式化字节大小
        /// </summary>
        private static string FormatBytes(long bytes)
        {
            if (bytes == 0) return "0 B";

            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;

            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:F1} {sizes[order]}";
        }

        #endregion

        #region 清理

        public void Dispose()
        {
            StopMonitoring();
            
            // 取消订阅事件
            _signalRClient.Connected -= OnSignalRConnected;
            _signalRClient.Disconnected -= OnSignalRDisconnected;
            _signalRClient.DiskSpaceUpdated -= OnDiskSpaceUpdated;
            _signalRClient.TaskProgressUpdated -= OnTaskProgressUpdated;
        }

        #endregion
    }
}
