using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using VideoConversion_Client.Models;
using VideoConversion_Client.Services;
using VideoConversion_Client.Views;

namespace VideoConversion_Client.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly ApiService apiService;
        private readonly SignalRService signalRService;

        private string _statusText = "就绪 - 请选择视频文件开始转换";
        private bool _isConnectedToServer = false;
        private string? _currentTaskId = null; 
        private DateTime? _currentTaskStartTime = null;

        public MainWindowViewModel()
        {
            apiService = new ApiService();
            signalRService = new SignalRService(apiService.BaseUrl);
            
            ConversionTasks = new ObservableCollection<ConversionTask>();
            
            InitializeServices();
        }

        // 属性
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public bool IsConnectedToServer
        {
            get => _isConnectedToServer;
            set => SetProperty(ref _isConnectedToServer, value);
        }

        public string? CurrentTaskId
        {
            get => _currentTaskId;
            set => SetProperty(ref _currentTaskId, value);
        }

        public ObservableCollection<ConversionTask> ConversionTasks { get; }

        public string ServerUrl => apiService.BaseUrl.Replace("http://", "").Replace("https://", "");

        // 初始化服务
        private async void InitializeServices()
        {
            try
            {
                // 设置SignalR事件处理
                signalRService.Connected += () =>
                {
                    IsConnectedToServer = true;
                    StatusText = "✅ 已连接到服务器";
                };

                signalRService.Disconnected += () =>
                {
                    IsConnectedToServer = false;
                    StatusText = "❌ 与服务器断开连接";
                };

                signalRService.ProgressUpdated += OnProgressUpdated;
                signalRService.StatusUpdated += OnStatusUpdated;
                signalRService.TaskCompleted += OnTaskCompleted;
                signalRService.Error += OnSignalRError;

                // 尝试连接
                await signalRService.ConnectAsync();
                
                // 加载最近任务
                await LoadRecentTasks();
            }
            catch (Exception ex)
            {
                StatusText = $"❌ 初始化失败: {ex.Message}";
            }
        }

        // SignalR事件处理
        private void OnProgressUpdated(string taskId, int progress, string message, double? speed, int? remainingSeconds)
        {
            if (CurrentTaskId == taskId)
            {
                var speedText = speed.HasValue ? $" - {speed.Value:F1}x" : "";
                var timeText = remainingSeconds.HasValue ? 
                    $" - 剩余: {TimeSpan.FromSeconds(remainingSeconds.Value):hh\\:mm\\:ss}" : "";
                
                StatusText = $"📊 转换进度: {progress}%{speedText}{timeText}";
            }

            // 更新任务列表中的进度
            var task = GetTaskById(taskId);
            if (task != null)
            {
                task.Progress = progress;
                task.ConversionSpeed = speed;
                task.EstimatedTimeRemaining = remainingSeconds;
            }
        }

        private void OnStatusUpdated(string taskId, string status, string? message)
        {
            var task = GetTaskById(taskId);
            if (task != null)
            {
                if (Enum.TryParse<ConversionStatus>(status, out var statusEnum))
                {
                    task.Status = statusEnum;
                }
            }

            if (CurrentTaskId == taskId)
            {
                StatusText = $"📋 任务状态: {message ?? status}";
            }
        }

        private void OnTaskCompleted(string taskId, string status, bool success, string? outputPath)
        {
            var task = GetTaskById(taskId);
            if (task != null)
            {
                task.Status = success ? ConversionStatus.Completed : ConversionStatus.Failed;
                task.Progress = success ? 100 : task.Progress;
                task.CompletedAt = DateTime.Now;
                task.OutputPath = outputPath;
            }

            if (CurrentTaskId == taskId)
            {
                StatusText = success ? 
                    $"✅ 转换完成: {Path.GetFileName(outputPath ?? "")}" : 
                    $"❌ 转换失败: {status}";
                
                if (success)
                {
                    CurrentTaskId = null;
                    _currentTaskStartTime = null;
                }
            }
        }

        private void OnSignalRError(string error)
        {
            StatusText = $"❌ SignalR错误: {error}";
        }

        // 业务方法
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                StatusText = "🔗 正在测试服务器连接...";
                var connected = await apiService.TestConnectionAsync();
                
                if (connected)
                {
                    StatusText = "✅ 服务器连接测试成功";
                    if (!IsConnectedToServer)
                    {
                        await signalRService.ConnectAsync();
                    }
                }
                else
                {
                    StatusText = "❌ 服务器连接失败 - 请检查地址和服务器状态";
                }
                
                return connected;
            }
            catch (Exception ex)
            {
                StatusText = $"❌ 连接测试失败: {ex.Message}";
                return false;
            }
        }

        public async Task<bool> StartConversionAsync(string filePath, ConversionStartEventArgs settings)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    StatusText = "⚠️ 选择的文件不存在";
                    return false;
                }

                var preset = ConversionPreset.GetPresetByName(settings.Preset);
                if (preset == null)
                {
                    StatusText = "⚠️ 无效的转换预设";
                    return false;
                }

                // 准备转换请求
                var request = new StartConversionRequest
                {
                    TaskName = settings.TaskName,
                    Preset = settings.Preset,
                    OutputFormat = settings.OutputFormat,
                    Resolution = settings.Resolution,
                    VideoCodec = preset.VideoCodec,
                    AudioCodec = preset.AudioCodec,
                    VideoQuality = settings.VideoQuality,
                    AudioQuality = preset.AudioQuality,
                    FrameRate = preset.FrameRate
                };

                _currentTaskStartTime = DateTime.Now;
                StatusText = $"🚀 开始转换: {settings.TaskName} (预设: {settings.Preset})";

                // 调用API开始转换
                var response = await apiService.StartConversionAsync(filePath, request);

                if (response.Success && response.Data != null)
                {
                    CurrentTaskId = response.Data.TaskId;
                    StatusText = $"✅ 转换任务已创建: {response.Data.TaskName}";

                    // 加入SignalR任务组以接收进度更新
                    if (!string.IsNullOrEmpty(CurrentTaskId))
                    {
                        await signalRService.JoinTaskGroupAsync(CurrentTaskId);
                    }

                    // 创建新的任务对象并添加到列表
                    var newTask = new ConversionTask
                    {
                        Id = CurrentTaskId ?? Guid.NewGuid().ToString(),
                        TaskName = settings.TaskName,
                        OriginalFileName = Path.GetFileName(filePath),
                        Status = ConversionStatus.Pending,
                        Progress = 0,
                        CreatedAt = DateTime.Now,
                        StartedAt = DateTime.Now
                    };

                    ConversionTasks.Insert(0, newTask);
                    return true;
                }
                else
                {
                    StatusText = $"❌ 启动转换失败: {response.Message}";
                    return false;
                }
            }
            catch (Exception ex)
            {
                StatusText = $"❌ 转换失败: {ex.Message}";
                return false;
            }
        }

        public async Task LoadRecentTasks()
        {
            try
            {
                var response = await apiService.GetRecentTasksAsync(10);
                if (response.Success && response.Data != null)
                {
                    ConversionTasks.Clear();
                    foreach (var task in response.Data)
                    {
                        ConversionTasks.Add(task);
                    }
                }
            }
            catch (Exception ex)
            {
                StatusText = $"❌ 加载历史任务失败: {ex.Message}";
            }
        }

        public async Task<bool> CancelTaskAsync(string taskId)
        {
            try
            {
                var response = await apiService.CancelTaskAsync(taskId);
                if (response.Success)
                {
                    StatusText = "✅ 任务已取消";
                    if (CurrentTaskId == taskId)
                    {
                        CurrentTaskId = null;
                        _currentTaskStartTime = null;
                    }
                    return true;
                }
                else
                {
                    StatusText = $"❌ 取消任务失败: {response.Message}";
                    return false;
                }
            }
            catch (Exception ex)
            {
                StatusText = $"❌ 取消任务失败: {ex.Message}";
                return false;
            }
        }

        // 辅助方法
        private ConversionTask? GetTaskById(string taskId)
        {
            foreach (var task in ConversionTasks)
            {
                if (task.Id == taskId)
                    return task;
            }
            return null;
        }

        // 清理资源
        public async Task CleanupAsync()
        {
            try
            {
                await signalRService.DisconnectAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清理资源失败: {ex.Message}");
            }
        }
    }
}
