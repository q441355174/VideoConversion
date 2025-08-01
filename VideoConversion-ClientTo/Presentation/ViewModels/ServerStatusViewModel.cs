using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using VideoConversion_ClientTo.Application.Interfaces;

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

            // 订阅SignalR事件
            _signalRClient.Connected += OnSignalRConnected;
            _signalRClient.Disconnected += OnSignalRDisconnected;
            _signalRClient.DiskSpaceUpdated += OnDiskSpaceUpdated;
            _signalRClient.TaskProgressUpdated += OnTaskProgressUpdated;
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
                // 检查服务器连接 - 这里需要实现一个简单的连接测试
                bool serverConnected = false;
                try
                {
                    var response = await _apiClient.GetAsync<object>("/api/health");
                    serverConnected = response.Success;
                }
                catch
                {
                    serverConnected = false;
                }

                IsServerConnected = serverConnected;
                ServerStatusText = serverConnected ? "已连接" : "连接失败";

                if (serverConnected)
                {
                    // 获取磁盘空间信息
                    await RefreshDiskSpace();

                    // 获取当前任务状态
                    await RefreshCurrentTaskStatus();
                }
                else
                {
                    // 重置状态
                    ResetStatusToDisconnected();
                }
            }
            catch (Exception ex)
            {
                IsServerConnected = false;
                ServerStatusText = $"检查失败: {ex.Message}";
                Utils.Logger.Error("ServerStatusViewModel", $"❌ 刷新服务器状态失败: {ex.Message}");
            }
        }

        #endregion

        #region 私有方法

        private async Task RefreshDiskSpace()
        {
            try
            {
                var response = await _apiClient.GetDiskSpaceAsync();
                if (response.Success && response.Data != null)
                {
                    var spaceInfo = response.Data;
                    UpdateDiskSpaceInfo(spaceInfo.UsedSpace, spaceInfo.TotalSpace, spaceInfo.AvailableSpace);
                }
                else
                {
                    Utils.Logger.Warning("ServerStatusViewModel", $"⚠️ 获取磁盘空间失败: {response.Message}");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Warning("ServerStatusViewModel", $"⚠️ 获取磁盘空间失败: {ex.Message}");
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
            }
        }

        private void UpdateDiskSpaceInfo(long usedSpace, long totalSpace, long availableSpace)
        {
            UsedSpaceText = FormatFileSize(usedSpace);
            TotalSpaceText = FormatFileSize(totalSpace);
            AvailableSpaceText = FormatFileSize(availableSpace);

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

        private void ResetStatusToDisconnected()
        {
            UsedSpaceText = "0 GB";
            TotalSpaceText = "未知";
            AvailableSpaceText = "未知";
            DiskUsagePercentage = 0;
            IsSpaceWarningVisible = false;
            HasActiveTask = false;
            ResetTaskStatus();
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

        private void OnTaskProgressUpdated(object? sender, Application.DTOs.ConversionProgressDto progress)
        {
            if (HasActiveTask && progress.TaskId == CurrentTaskName)
            {
                TaskProgress = progress.Progress;
                TaskProgressText = $"转换中... {progress.Progress}%";
                TaskSpeedText = progress.Speed?.ToString("0.0x") ?? "";
                
                if (progress.EstimatedRemainingSeconds.HasValue)
                {
                    var eta = TimeSpan.FromSeconds(progress.EstimatedRemainingSeconds.Value);
                    TaskETAText = $"预计剩余: {eta:mm\\:ss}";
                }
            }
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
