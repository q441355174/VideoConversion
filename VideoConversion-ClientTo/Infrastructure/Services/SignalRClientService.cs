using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using VideoConversion_ClientTo.Application.Interfaces;
using VideoConversion_ClientTo.Application.DTOs;

namespace VideoConversion_ClientTo.Infrastructure.Services
{
    /// <summary>
    /// STEP-7: 简化的SignalR客户端服务实现
    /// 职责: 与服务端进行实时通信
    /// </summary>
    public class SignalRClientService : ISignalRClient
    {
        private HubConnection? _connection;
        private string _hubUrl = "http://localhost:5065/conversionHub";

        public SignalRClientService()
        {
            Utils.Logger.Info("SignalRClientService", "✅ SignalR客户端服务已初始化");
        }

        #region 连接管理

        public async Task<bool> ConnectAsync()
        {
            try
            {
                if (_connection != null)
                {
                    await _connection.DisposeAsync();
                }

                _connection = new HubConnectionBuilder()
                    .WithUrl(_hubUrl)
                    .WithAutomaticReconnect()
                    .Build();

                // 注册事件处理器
                RegisterEventHandlers();

                // 连接事件
                _connection.Closed += OnConnectionClosed;
                _connection.Reconnecting += OnReconnecting;
                _connection.Reconnected += OnReconnected;

                await _connection.StartAsync();
                
                Utils.Logger.Info("SignalRClientService", "✅ SignalR连接成功");
                Connected?.Invoke(this, EventArgs.Empty);
                return true;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SignalRClientService", $"❌ SignalR连接失败: {ex.Message}");
                return false;
            }
        }

        public async Task DisconnectAsync()
        {
            try
            {
                if (_connection != null)
                {
                    await _connection.DisposeAsync();
                    _connection = null;
                    Utils.Logger.Info("SignalRClientService", "🔌 SignalR连接已断开");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SignalRClientService", $"❌ 断开SignalR连接失败: {ex.Message}");
            }
        }

        public bool IsConnected => _connection?.State == HubConnectionState.Connected;

        #endregion

        #region 群组管理

        public async Task JoinTaskGroupAsync(string taskId)
        {
            try
            {
                if (_connection != null && IsConnected)
                {
                    await _connection.InvokeAsync("JoinTaskGroup", taskId);
                    Utils.Logger.Debug("SignalRClientService", $"📥 已加入任务群组: {taskId}");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SignalRClientService", $"❌ 加入任务群组失败: {ex.Message}");
            }
        }

        public async Task LeaveTaskGroupAsync(string taskId)
        {
            try
            {
                if (_connection != null && IsConnected)
                {
                    await _connection.InvokeAsync("LeaveTaskGroup", taskId);
                    Utils.Logger.Debug("SignalRClientService", $"📤 已离开任务群组: {taskId}");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SignalRClientService", $"❌ 离开任务群组失败: {ex.Message}");
            }
        }

        public async Task JoinUserGroupAsync(string userId)
        {
            try
            {
                if (_connection != null && IsConnected)
                {
                    await _connection.InvokeAsync("JoinUserGroup", userId);
                    Utils.Logger.Debug("SignalRClientService", $"👤 已加入用户群组: {userId}");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SignalRClientService", $"❌ 加入用户群组失败: {ex.Message}");
            }
        }

        public async Task JoinSpaceMonitoringAsync()
        {
            try
            {
                if (_connection != null && IsConnected)
                {
                    await _connection.InvokeAsync("JoinSpaceMonitoring");
                    Utils.Logger.Info("SignalRClientService", "✅ 已加入空间监控群组");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SignalRClientService", $"❌ 加入空间监控群组失败: {ex.Message}");
            }
        }

        #endregion

        #region 事件处理

        private void RegisterEventHandlers()
        {
            if (_connection == null) return;

            // 任务进度更新
            _connection.On<ConversionProgressDto>("TaskProgressUpdated", (progress) =>
            {
                Utils.Logger.Debug("SignalRClientService", $"📊 任务进度更新: {progress.TaskId} - {progress.Progress}%");
                TaskProgressUpdated?.Invoke(this, progress);
            });

            // 任务状态更新
            _connection.On<TaskStatusUpdateDto>("TaskStatusUpdated", (status) =>
            {
                Utils.Logger.Debug("SignalRClientService", $"📋 任务状态更新: {status.TaskId} - {status.NewStatus}");
                TaskStatusUpdated?.Invoke(this, status);
            });

            // 任务完成
            _connection.On<TaskCompletedDto>("TaskCompleted", (completed) =>
            {
                Utils.Logger.Info("SignalRClientService", $"✅ 任务完成: {completed.TaskId} - {completed.TaskName}");
                TaskCompleted?.Invoke(this, completed);
            });

            // 任务删除
            _connection.On<string>("TaskDeleted", (taskId) =>
            {
                Utils.Logger.Info("SignalRClientService", $"🗑️ 任务删除: {taskId}");
                TaskDeleted?.Invoke(this, taskId);
            });

            // 磁盘空间状态更新
            _connection.On<DiskSpaceStatusDto>("DiskSpaceStatusUpdated", (diskSpace) =>
            {
                Utils.Logger.Debug("SignalRClientService", $"💾 磁盘空间更新: {diskSpace.UsagePercentage:F1}%");
                DiskSpaceStatusUpdated?.Invoke(this, diskSpace);
            });
        }

        private async Task OnConnectionClosed(Exception? exception)
        {
            var message = exception?.Message ?? "连接正常关闭";
            Utils.Logger.Warning("SignalRClientService", $"🔌 SignalR连接已关闭: {message}");
            Disconnected?.Invoke(this, message);
        }

        private async Task OnReconnecting(Exception? exception)
        {
            Utils.Logger.Info("SignalRClientService", "🔄 SignalR正在重连...");
            Reconnecting?.Invoke(this, EventArgs.Empty);
        }

        private async Task OnReconnected(string? connectionId)
        {
            Utils.Logger.Info("SignalRClientService", $"✅ SignalR重连成功: {connectionId}");
            Reconnected?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region 事件

        public event EventHandler<ConversionProgressDto>? TaskProgressUpdated;
        public event EventHandler<TaskStatusUpdateDto>? TaskStatusUpdated;
        public event EventHandler<TaskCompletedDto>? TaskCompleted;
        public event EventHandler<string>? TaskDeleted;
        public event EventHandler<DiskSpaceStatusDto>? DiskSpaceStatusUpdated;
        public event EventHandler<DiskSpaceDto>? DiskSpaceUpdated;

        public event EventHandler? Connected;
        public event EventHandler<string>? Disconnected;
        public event EventHandler? Reconnecting;
        public event EventHandler? Reconnected;

        #endregion

        public void Dispose()
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    if (_connection != null)
                    {
                        await _connection.DisposeAsync();
                    }
                }
                catch (Exception ex)
                {
                    Utils.Logger.Error("SignalRClientService", $"❌ 释放SignalR连接失败: {ex.Message}");
                }
            });
        }
    }
}
