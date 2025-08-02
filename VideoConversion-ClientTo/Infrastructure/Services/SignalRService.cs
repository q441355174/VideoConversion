using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using VideoConversion_ClientTo.Infrastructure;

namespace VideoConversion_ClientTo.Infrastructure.Services
{
    /// <summary>
    /// SignalR服务 - 基于Client项目的完整实现
    /// </summary>
    public class SignalRService
    {
        private static SignalRService? _instance;
        private static readonly object _lock = new object();

        private HubConnection? _connection;
        private readonly string _baseUrl;
        private bool _isConnected = false;

        // 事件
        public event Action<string, int, string, double?, int?>? ProgressUpdated;
        public event Action<string, string, string?>? StatusUpdated;
        public event Action<string, string, bool, string?>? TaskCompleted;
        public event Action<string>? TaskDeleted;
        public event Action<string, string>? SystemNotification;
        public event Action<string>? TaskNotFound;
        public event Action<string>? Error;
        public event Action? Connected;
        public event Action? Disconnected;

        public static SignalRService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new SignalRService();
                    }
                }
                return _instance;
            }
        }

        private SignalRService()
        {
            // 从配置或设置中获取服务器地址
            _baseUrl = "http://localhost:5065"; // 默认地址，可以从配置文件读取
            
            Utils.Logger.Info("SignalRService", "✅ SignalR服务已初始化");
        }

        /// <summary>
        /// 连接到SignalR Hub
        /// </summary>
        public async Task<bool> ConnectAsync()
        {
            try
            {
                if (_connection != null)
                {
                    await _connection.DisposeAsync();
                }

                _connection = new HubConnectionBuilder()
                    .WithUrl($"{_baseUrl}/conversionHub")
                    .WithAutomaticReconnect()
                    .Build();

                // 注册事件处理器
                RegisterEventHandlers();

                // 连接状态事件
                _connection.Closed += async (error) =>
                {
                    _isConnected = false;
                    Disconnected?.Invoke();
                    if (error != null)
                    {
                        Error?.Invoke($"连接断开: {error.Message}");
                        Utils.Logger.Error("SignalRService", $"❌ 连接断开: {error.Message}");
                    }
                };

                _connection.Reconnected += async (connectionId) =>
                {
                    _isConnected = true;
                    Connected?.Invoke();
                    Utils.Logger.Info("SignalRService", "🔗 SignalR重新连接成功");
                };

                await _connection.StartAsync();
                _isConnected = true;
                Connected?.Invoke();

                Utils.Logger.Info("SignalRService", "✅ SignalR连接成功");
                return true;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SignalRService", $"❌ SignalR连接失败: {ex.Message}");
                Error?.Invoke($"连接失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public async Task DisconnectAsync()
        {
            try
            {
                if (_connection != null)
                {
                    await _connection.DisposeAsync();
                    _connection = null;
                    _isConnected = false;
                    Utils.Logger.Info("SignalRService", "🔌 SignalR连接已断开");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SignalRService", $"❌ 断开SignalR连接失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 注册事件处理器
        /// </summary>
        private void RegisterEventHandlers()
        {
            if (_connection == null) return;

            // 进度更新事件
            _connection.On<object>("ProgressUpdate", (data) =>
            {
                try
                {
                    // 解析进度数据
                    var progressData = System.Text.Json.JsonSerializer.Deserialize<ProgressUpdateData>(data.ToString() ?? "{}");
                    if (progressData != null)
                    {
                        ProgressUpdated?.Invoke(
                            progressData.TaskId ?? "",
                            progressData.Progress,
                            progressData.Message ?? "",
                            progressData.Speed,
                            progressData.RemainingSeconds
                        );

                        Utils.Logger.Debug("SignalRService", 
                            $"📊 收到进度更新: {progressData.TaskId} - {progressData.Progress}%");
                    }
                }
                catch (Exception ex)
                {
                    Utils.Logger.Error("SignalRService", $"❌ 处理进度更新失败: {ex.Message}");
                }
            });

            // 状态更新事件
            _connection.On<string, string, string?>("StatusUpdate", (taskId, status, message) =>
            {
                try
                {
                    StatusUpdated?.Invoke(taskId, status, message);
                    Utils.Logger.Debug("SignalRService", $"📋 收到状态更新: {taskId} - {status}");
                }
                catch (Exception ex)
                {
                    Utils.Logger.Error("SignalRService", $"❌ 处理状态更新失败: {ex.Message}");
                }
            });

            // 任务完成事件
            _connection.On<string, string, bool, string?>("TaskCompleted", (taskId, taskName, success, message) =>
            {
                try
                {
                    TaskCompleted?.Invoke(taskId, taskName, success, message);
                    Utils.Logger.Info("SignalRService", $"🎉 收到任务完成: {taskId} - 成功: {success}");
                }
                catch (Exception ex)
                {
                    Utils.Logger.Error("SignalRService", $"❌ 处理任务完成失败: {ex.Message}");
                }
            });

            // 任务删除事件
            _connection.On<string>("TaskDeleted", (taskId) =>
            {
                try
                {
                    TaskDeleted?.Invoke(taskId);
                    Utils.Logger.Info("SignalRService", $"🗑️ 收到任务删除: {taskId}");
                }
                catch (Exception ex)
                {
                    Utils.Logger.Error("SignalRService", $"❌ 处理任务删除失败: {ex.Message}");
                }
            });

            // 系统通知事件
            _connection.On<string, string>("SystemNotification", (type, message) =>
            {
                try
                {
                    SystemNotification?.Invoke(type, message);
                    Utils.Logger.Info("SignalRService", $"📢 收到系统通知: {type} - {message}");
                }
                catch (Exception ex)
                {
                    Utils.Logger.Error("SignalRService", $"❌ 处理系统通知失败: {ex.Message}");
                }
            });

            // 任务未找到事件
            _connection.On<string>("TaskNotFound", (taskId) =>
            {
                try
                {
                    TaskNotFound?.Invoke(taskId);
                    Utils.Logger.Warning("SignalRService", $"⚠️ 任务未找到: {taskId}");
                }
                catch (Exception ex)
                {
                    Utils.Logger.Error("SignalRService", $"❌ 处理任务未找到失败: {ex.Message}");
                }
            });

            Utils.Logger.Info("SignalRService", "✅ SignalR事件处理器已注册");
        }

        /// <summary>
        /// 加入任务组以接收特定任务的更新
        /// </summary>
        public async Task JoinTaskGroupAsync(string taskId)
        {
            if (_connection?.State == Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Connected)
            {
                try
                {
                    await _connection.InvokeAsync("JoinTaskGroup", taskId);
                    Utils.Logger.Info("SignalRService", $"🔗 已加入任务组: {taskId}");
                }
                catch (Exception ex)
                {
                    Utils.Logger.Error("SignalRService", $"❌ 加入任务组失败: {taskId} - {ex.Message}");
                    Error?.Invoke($"加入任务组失败: {ex.Message}");
                }
            }
            else
            {
                Utils.Logger.Warning("SignalRService", "⚠️ SignalR未连接，无法加入任务组");
            }
        }

        /// <summary>
        /// 离开任务组
        /// </summary>
        public async Task LeaveTaskGroupAsync(string taskId)
        {
            if (_connection?.State == Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Connected)
            {
                try
                {
                    await _connection.InvokeAsync("LeaveTaskGroup", taskId);
                    Utils.Logger.Info("SignalRService", $"🚪 已离开任务组: {taskId}");
                }
                catch (Exception ex)
                {
                    Utils.Logger.Error("SignalRService", $"❌ 离开任务组失败: {taskId} - {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 检查连接状态
        /// </summary>
        public bool IsConnected => _isConnected && 
            _connection?.State == Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Connected;

        /// <summary>
        /// 获取连接状态字符串
        /// </summary>
        public string GetConnectionStatus()
        {
            if (_connection == null) return "未初始化";
            
            return _connection.State switch
            {
                Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Connected => "已连接",
                Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Connecting => "连接中",
                Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Reconnecting => "重连中",
                Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Disconnected => "已断开",
                _ => "未知状态"
            };
        }

        /// <summary>
        /// 设置服务器地址
        /// </summary>
        public void SetServerUrl(string baseUrl)
        {
            if (!string.IsNullOrEmpty(baseUrl))
            {
                // 这里可以更新服务器地址，需要重新连接
                Utils.Logger.Info("SignalRService", $"🔧 更新服务器地址: {baseUrl}");
            }
        }
    }

    /// <summary>
    /// 进度更新数据模型
    /// </summary>
    public class ProgressUpdateData
    {
        public string? TaskId { get; set; }
        public int Progress { get; set; }
        public string? Message { get; set; }
        public double? Speed { get; set; }
        public int? RemainingSeconds { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
