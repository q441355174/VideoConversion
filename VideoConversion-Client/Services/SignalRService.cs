using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VideoConversion_Client.Services
{
    /// <summary>
    /// SignalR服务，用于实时接收转换进度和状态更新
    /// </summary>
    public class SignalRService
    {
        private HubConnection? _connection;
        private readonly string _baseUrl;
        private readonly Dictionary<string, List<Action<System.Text.Json.JsonElement>>> _dynamicHandlers = new();

        public bool IsConnected => _connection?.State == HubConnectionState.Connected;

        // 事件
        public event Action<string, int, string, double?, int?>? ProgressUpdated;
        public event Action<string, string, string?>? StatusUpdated;
        public event Action<string, string, bool, string?>? TaskCompleted;
        public event Action<string, string>? SystemNotification;
        public event Action<string>? TaskNotFound;
        public event Action<string>? Error;
        public event Action? Connected;
        public event Action? Disconnected;

        // 磁盘空间监控事件
        public event Action<DiskSpaceStatus>? DiskSpaceUpdated;
        public event Action<SpaceReleaseNotification>? SpaceReleased;
        public event Action<SpaceWarningNotification>? SpaceWarning;
        public event Action<DiskSpaceConfigNotification>? SpaceConfigChanged;

        // 批量任务控制事件
        public event Action<BatchTaskControlNotification>? BatchTaskPaused;
        public event Action<BatchTaskControlNotification>? BatchTaskResumed;

        public SignalRService(string baseUrl = "http://localhost:5065")
        {
            _baseUrl = baseUrl;
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
                    Disconnected?.Invoke();
                    if (error != null)
                    {
                        Error?.Invoke($"连接断开: {error.Message}");
                    }
                };

                _connection.Reconnected += async (connectionId) =>
                {
                    Connected?.Invoke();
                    // 重连后重新加入空间监控组
                    await JoinSpaceMonitoringAsync();
                };

                await _connection.StartAsync();
                Connected?.Invoke();

                // 连接成功后自动加入空间监控组
                await JoinSpaceMonitoringAsync();

                return true;
            }
            catch (Exception ex)
            {
                Error?.Invoke($"连接失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public async Task DisconnectAsync()
        {
            if (_connection != null)
            {
                await _connection.DisposeAsync();
                _connection = null;
                Disconnected?.Invoke();
            }
        }

        /// <summary>
        /// 加入任务组以接收特定任务的更新
        /// </summary>
        public async Task JoinTaskGroupAsync(string taskId)
        {
            if (_connection?.State == HubConnectionState.Connected)
            {
                try
                {
                    Utils.Logger.Info("SignalR", $"🔗 加入任务组: {taskId}");
                    await _connection.InvokeAsync("JoinTaskGroup", taskId);
                    Utils.Logger.Info("SignalR", $"✅ 成功加入任务组: {taskId}");
                }
                catch (Exception ex)
                {
                    Utils.Logger.Info("SignalR", $"❌ 加入任务组失败: {taskId}, 错误: {ex.Message}");
                    Error?.Invoke($"加入任务组失败: {ex.Message}");
                }
            }
            else
            {
                Utils.Logger.Info("SignalR", $"⚠️ SignalR未连接，无法加入任务组: {taskId}");
            }
        }

        /// <summary>
        /// 离开任务组
        /// </summary>
        public async Task LeaveTaskGroupAsync(string taskId)
        {
            if (_connection?.State == HubConnectionState.Connected)
            {
                try
                {
                    await _connection.InvokeAsync("LeaveTaskGroup", taskId);
                }
                catch (Exception ex)
                {
                    Error?.Invoke($"离开任务组失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 获取任务状态
        /// </summary>
        public async Task GetTaskStatusAsync(string taskId)
        {
            if (_connection?.State == HubConnectionState.Connected)
            {
                try
                {
                    await _connection.InvokeAsync("GetTaskStatus", taskId);
                }
                catch (Exception ex)
                {
                    Error?.Invoke($"获取任务状态失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 获取所有活动任务
        /// </summary>
        public async Task GetActiveTasksAsync()
        {
            if (_connection?.State == HubConnectionState.Connected)
            {
                try
                {
                    await _connection.InvokeAsync("GetActiveTasks");
                }
                catch (Exception ex)
                {
                    Error?.Invoke($"获取活动任务失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 取消任务
        /// </summary>
        public async Task CancelTaskAsync(string taskId)
        {
            if (_connection?.State == HubConnectionState.Connected)
            {
                try
                {
                    await _connection.InvokeAsync("CancelTask", taskId);
                }
                catch (Exception ex)
                {
                    Error?.Invoke($"取消任务失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 注册SignalR事件处理器
        /// </summary>
        private void RegisterEventHandlers()
        {
            if (_connection == null) return;

            // 进度更新
            _connection.On<object>("ProgressUpdate", (data) =>
            {
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(data);
                    Utils.Logger.Info("SignalR", $"📊 收到进度更新: {json}");

                    var update = System.Text.Json.JsonSerializer.Deserialize<ProgressUpdateData>(json, new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (update != null)
                    {
                        Utils.Logger.Info("SignalR", $"🔄 转发进度更新: TaskId={update.TaskId}, Progress={update.Progress}%, Speed={update.Speed:F2}x");

                        ProgressUpdated?.Invoke(
                            update.TaskId ?? string.Empty,
                            update.Progress,
                            update.Message ?? string.Empty,
                            update.Speed,
                            update.RemainingSeconds
                        );
                    }
                    else
                    {
                        Utils.Logger.Info("SignalR", "⚠️ 进度更新数据解析为null");
                    }
                }
                catch (Exception ex)
                {
                    Utils.Logger.Info("SignalR", $"❌ 处理进度更新失败: {ex.Message}");
                    Error?.Invoke($"处理进度更新失败: {ex.Message}");
                }
            });

            // 状态更新
            _connection.On<object>("StatusUpdate", (data) =>
            {
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(data);
                    var update = System.Text.Json.JsonSerializer.Deserialize<StatusUpdateData>(json, new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (update != null)
                    {
                        StatusUpdated?.Invoke(
                            update.TaskId ?? string.Empty,
                            update.Status ?? string.Empty,
                            update.ErrorMessage
                        );
                    }
                }
                catch (Exception ex)
                {
                    Error?.Invoke($"处理状态更新失败: {ex.Message}");
                }
            });

            // 任务完成
            _connection.On<object>("TaskCompleted", (data) =>
            {
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(data);
                    var update = System.Text.Json.JsonSerializer.Deserialize<TaskCompletedData>(json, new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (update != null)
                    {
                        TaskCompleted?.Invoke(
                            update.TaskId ?? string.Empty,
                            update.TaskName ?? string.Empty,
                            update.Success,
                            update.ErrorMessage
                        );
                    }
                }
                catch (Exception ex)
                {
                    Error?.Invoke($"处理任务完成失败: {ex.Message}");
                }
            });

            // 系统通知
            _connection.On<object>("SystemNotification", (data) =>
            {
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(data);
                    var update = System.Text.Json.JsonSerializer.Deserialize<SystemNotificationData>(json, new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (update != null)
                    {
                        SystemNotification?.Invoke(
                            update.Message ?? string.Empty,
                            update.Type ?? "info"
                        );
                    }
                }
                catch (Exception ex)
                {
                    Error?.Invoke($"处理系统通知失败: {ex.Message}");
                }
            });

            // 任务未找到
            _connection.On<string>("TaskNotFound", (taskId) =>
            {
                TaskNotFound?.Invoke(taskId);
            });

            // 错误
            _connection.On<string>("Error", (message) =>
            {
                Error?.Invoke(message);
            });

            // 任务状态响应
            _connection.On<object>("TaskStatus", (data) =>
            {
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(data);
                    var taskStatus = System.Text.Json.JsonSerializer.Deserialize<TaskStatusData>(json, new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (taskStatus != null)
                    {
                        // 可以触发一个专门的任务状态事件
                        StatusUpdated?.Invoke(
                            taskStatus.TaskId ?? string.Empty,
                            taskStatus.Status ?? string.Empty,
                            taskStatus.ErrorMessage
                        );

                        if (taskStatus.Progress.HasValue)
                        {
                            ProgressUpdated?.Invoke(
                                taskStatus.TaskId ?? string.Empty,
                                taskStatus.Progress.Value,
                                "状态更新",
                                taskStatus.ConversionSpeed,
                                taskStatus.EstimatedTimeRemaining
                            );
                        }
                    }
                }
                catch (Exception ex)
                {
                    Error?.Invoke($"处理任务状态失败: {ex.Message}");
                }
            });

            // 磁盘空间状态更新
            _connection.On<object>("DiskSpaceUpdate", (data) =>
            {
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(data);
                    Utils.Logger.Info("SignalR", $"💾 收到磁盘空间更新: {json}");

                    var spaceStatus = System.Text.Json.JsonSerializer.Deserialize<DiskSpaceStatus>(json, new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (spaceStatus != null)
                    {
                        Utils.Logger.Info("SignalR", $"📊 磁盘空间状态: 已用={spaceStatus.UsedSpaceGB:F2}GB, 可用={spaceStatus.AvailableSpaceGB:F2}GB, 使用率={spaceStatus.UsagePercentage:F1}%");
                        DiskSpaceUpdated?.Invoke(spaceStatus);
                    }
                }
                catch (Exception ex)
                {
                    Utils.Logger.Info("SignalR", $"❌ 处理磁盘空间更新失败: {ex.Message}");
                    Error?.Invoke($"处理磁盘空间更新失败: {ex.Message}");
                }
            });

            // 空间释放通知
            _connection.On<object>("SpaceReleased", (data) =>
            {
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(data);
                    var notification = System.Text.Json.JsonSerializer.Deserialize<SpaceReleaseNotification>(json, new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (notification != null)
                    {
                        Utils.Logger.Info("SignalR", $"🗑️ 空间释放通知: {notification.ReleasedMB:F2}MB, 原因: {notification.Reason}");
                        SpaceReleased?.Invoke(notification);
                    }
                }
                catch (Exception ex)
                {
                    Utils.Logger.Info("SignalR", $"❌ 处理空间释放通知失败: {ex.Message}");
                    Error?.Invoke($"处理空间释放通知失败: {ex.Message}");
                }
            });

            // 空间警告通知
            _connection.On<object>("SpaceWarning", (data) =>
            {
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(data);
                    var warning = System.Text.Json.JsonSerializer.Deserialize<SpaceWarningNotification>(json, new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (warning != null)
                    {
                        Utils.Logger.Info("SignalR", $"⚠️ 空间警告: {warning.Message}, 使用率: {warning.UsagePercentage:F1}%");
                        SpaceWarning?.Invoke(warning);
                    }
                }
                catch (Exception ex)
                {
                    Utils.Logger.Info("SignalR", $"❌ 处理空间警告失败: {ex.Message}");
                    Error?.Invoke($"处理空间警告失败: {ex.Message}");
                }
            });

            // 空间配置变更通知
            _connection.On<object>("SpaceConfigChanged", (data) =>
            {
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(data);
                    var configNotification = System.Text.Json.JsonSerializer.Deserialize<DiskSpaceConfigNotification>(json, new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (configNotification != null)
                    {
                        Utils.Logger.Info("SignalR", $"⚙️ 空间配置变更: 最大={configNotification.MaxTotalSpaceGB:F2}GB, 保留={configNotification.ReservedSpaceGB:F2}GB");
                        SpaceConfigChanged?.Invoke(configNotification);
                    }
                }
                catch (Exception ex)
                {
                    Utils.Logger.Info("SignalR", $"❌ 处理空间配置变更失败: {ex.Message}");
                    Error?.Invoke($"处理空间配置变更失败: {ex.Message}");
                }
            });

            // 批量任务暂停通知
            _connection.On<object>("BatchTaskPaused", (data) =>
            {
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(data);
                    var notification = System.Text.Json.JsonSerializer.Deserialize<BatchTaskControlNotification>(json, new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (notification != null)
                    {
                        Utils.Logger.Info("SignalR", $"⏸️ 批量任务暂停: {notification.BatchId}, 原因: {notification.Reason}");
                        BatchTaskPaused?.Invoke(notification);
                    }
                }
                catch (Exception ex)
                {
                    Utils.Logger.Info("SignalR", $"❌ 处理批量任务暂停通知失败: {ex.Message}");
                    Error?.Invoke($"处理批量任务暂停通知失败: {ex.Message}");
                }
            });
        }

        #region 磁盘空间监控方法

        /// <summary>
        /// 加入空间监控组
        /// </summary>
        public async Task JoinSpaceMonitoringAsync()
        {
            if (_connection?.State == HubConnectionState.Connected)
            {
                try
                {
                    Utils.Logger.Info("SignalR", "🔗 加入空间监控组");
                    await _connection.InvokeAsync("JoinSpaceMonitoring");
                    Utils.Logger.Info("SignalR", "✅ 成功加入空间监控组");
                }
                catch (Exception ex)
                {
                    Utils.Logger.Info("SignalR", $"❌ 加入空间监控组失败: {ex.Message}");
                    Error?.Invoke($"加入空间监控组失败: {ex.Message}");
                }
            }
            else
            {
                Utils.Logger.Info("SignalR", "⚠️ SignalR未连接，无法加入空间监控组");
            }
        }

        /// <summary>
        /// 离开空间监控组
        /// </summary>
        public async Task LeaveSpaceMonitoringAsync()
        {
            if (_connection?.State == HubConnectionState.Connected)
            {
                try
                {
                    await _connection.InvokeAsync("LeaveSpaceMonitoring");
                    Utils.Logger.Info("SignalR", "✅ 已离开空间监控组");
                }
                catch (Exception ex)
                {
                    Utils.Logger.Info("SignalR", $"❌ 离开空间监控组失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 加入批量任务组
        /// </summary>
        public async Task JoinBatchTaskGroupAsync(string batchId)
        {
            if (_connection?.State == HubConnectionState.Connected)
            {
                try
                {
                    Utils.Logger.Info("SignalR", $"🔗 加入批量任务组: {batchId}");
                    await _connection.InvokeAsync("JoinBatchTaskGroup", batchId);
                    Utils.Logger.Info("SignalR", $"✅ 成功加入批量任务组: {batchId}");
                }
                catch (Exception ex)
                {
                    Utils.Logger.Info("SignalR", $"❌ 加入批量任务组失败: {batchId}, 错误: {ex.Message}");
                    Error?.Invoke($"加入批量任务组失败: {ex.Message}");
                }
            }
            else
            {
                Utils.Logger.Info("SignalR", $"⚠️ SignalR未连接，无法加入批量任务组: {batchId}");
            }
        }

        /// <summary>
        /// 离开批量任务组
        /// </summary>
        public async Task LeaveBatchTaskGroupAsync(string batchId)
        {
            if (_connection?.State == HubConnectionState.Connected)
            {
                try
                {
                    await _connection.InvokeAsync("LeaveBatchTaskGroup", batchId);
                    Utils.Logger.Info("SignalR", $"✅ 已离开批量任务组: {batchId}");
                }
                catch (Exception ex)
                {
                    Utils.Logger.Info("SignalR", $"❌ 离开批量任务组失败: {batchId}, 错误: {ex.Message}");
                }
            }
        }

        #endregion

        public void Dispose()
        {
            _ = Task.Run(async () =>
            {
                if (_connection != null)
                {
                    await _connection.DisposeAsync();
                }
            });
        }

        /// <summary>
        /// 注册动态事件处理器
        /// </summary>
        public void RegisterHandler(string eventName, Action<System.Text.Json.JsonElement> handler)
        {
            if (!_dynamicHandlers.ContainsKey(eventName))
            {
                _dynamicHandlers[eventName] = new List<Action<System.Text.Json.JsonElement>>();

                // 在SignalR连接上注册事件
                if (_connection != null)
                {
                    _connection.On<System.Text.Json.JsonElement>(eventName, (data) =>
                    {
                        if (_dynamicHandlers.ContainsKey(eventName))
                        {
                            foreach (var h in _dynamicHandlers[eventName])
                            {
                                try
                                {
                                    h.Invoke(data);
                                }
                                catch (Exception ex)
                                {
                                    Error?.Invoke($"处理事件 {eventName} 时出错: {ex.Message}");
                                }
                            }
                        }
                    });
                }
            }

            _dynamicHandlers[eventName].Add(handler);
        }

        /// <summary>
        /// 移除动态事件处理器
        /// </summary>
        public void UnregisterHandler(string eventName, Action<System.Text.Json.JsonElement> handler)
        {
            if (_dynamicHandlers.ContainsKey(eventName))
            {
                _dynamicHandlers[eventName].Remove(handler);

                if (_dynamicHandlers[eventName].Count == 0)
                {
                    _dynamicHandlers.Remove(eventName);
                }
            }
        }
    }

    // SignalR数据模型
    public class ProgressUpdateData
    {
        public string? TaskId { get; set; }
        public int Progress { get; set; }
        public string? Message { get; set; }
        public double? Speed { get; set; }
        public int? RemainingSeconds { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class StatusUpdateData
    {
        public string? TaskId { get; set; }
        public string? Status { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class TaskCompletedData
    {
        public string? TaskId { get; set; }
        public string? TaskName { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class SystemNotificationData
    {
        public string? Message { get; set; }
        public string? Type { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class TaskStatusData
    {
        public string? TaskId { get; set; }
        public string? Status { get; set; }
        public int? Progress { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int? EstimatedTimeRemaining { get; set; }
        public double? ConversionSpeed { get; set; }
        public double? Duration { get; set; }
        public double? CurrentTime { get; set; }
    }

    #region 磁盘空间相关数据模型

    /// <summary>
    /// 磁盘空间状态
    /// </summary>
    public class DiskSpaceStatus
    {
        public double TotalSpaceGB { get; set; }
        public double UsedSpaceGB { get; set; }
        public double AvailableSpaceGB { get; set; }
        public double ReservedSpaceGB { get; set; }
        public double UsagePercentage { get; set; }
        public bool HasSufficientSpace { get; set; }
        public DateTime UpdateTime { get; set; }
    }

    /// <summary>
    /// 空间释放通知
    /// </summary>
    public class SpaceReleaseNotification
    {
        public long ReleasedBytes { get; set; }
        public double ReleasedMB { get; set; }
        public string? Reason { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// 空间警告通知
    /// </summary>
    public class SpaceWarningNotification
    {
        public string? Message { get; set; }
        public double UsagePercentage { get; set; }
        public double AvailableSpaceGB { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// 磁盘空间配置变更通知
    /// </summary>
    public class DiskSpaceConfigNotification
    {
        public double MaxTotalSpaceGB { get; set; }
        public double ReservedSpaceGB { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// 批量任务控制通知
    /// </summary>
    public class BatchTaskControlNotification
    {
        public string? BatchId { get; set; }
        public string? Action { get; set; } // Pause, Resume, Continue
        public string? Reason { get; set; }
        public double RequiredSpaceGB { get; set; }
        public double AvailableSpaceGB { get; set; }
        public DateTime Timestamp { get; set; }
    }

    #endregion
}
