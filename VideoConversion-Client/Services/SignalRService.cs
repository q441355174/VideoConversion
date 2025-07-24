using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Threading.Tasks;
using VideoConversion_Client.Models;

namespace VideoConversion_Client.Services
{
    /// <summary>
    /// SignalR服务，用于实时接收转换进度和状态更新
    /// </summary>
    public class SignalRService
    {
        private HubConnection? _connection;
        private readonly string _baseUrl;

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
                };

                await _connection.StartAsync();
                Connected?.Invoke();
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
                    await _connection.InvokeAsync("JoinTaskGroup", taskId);
                }
                catch (Exception ex)
                {
                    Error?.Invoke($"加入任务组失败: {ex.Message}");
                }
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
                    var update = System.Text.Json.JsonSerializer.Deserialize<ProgressUpdateData>(json, new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (update != null)
                    {
                        ProgressUpdated?.Invoke(
                            update.TaskId ?? string.Empty,
                            update.Progress,
                            update.Message ?? string.Empty,
                            update.Speed,
                            update.RemainingSeconds
                        );
                    }
                }
                catch (Exception ex)
                {
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
        }

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
}
