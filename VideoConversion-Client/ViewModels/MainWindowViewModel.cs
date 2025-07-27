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
        private SignalRService signalRService;

        // 转换进度更新事件
        public event Action<string, int, double?, double?>? ConversionProgressUpdated;

        private string _statusText = "就绪 - 请选择视频文件开始转换";
        private bool _isConnectedToServer = false;
        private string? _currentTaskId = null; 
        private DateTime? _currentTaskStartTime = null;

        public MainWindowViewModel()
        {
            // 使用系统设置服务获取服务器地址
            var settingsService = Services.SystemSettingsService.Instance;
            apiService = new ApiService { BaseUrl = settingsService.GetServerAddress() };
            signalRService = new SignalRService(apiService.BaseUrl);

            ConversionTasks = new ObservableCollection<ConversionTask>();

            // 监听设置变化
            settingsService.SettingsChanged += OnSystemSettingsChanged;

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

        /// <summary>
        /// 获取并发状态信息
        /// </summary>
        public string ConcurrencyStatus
        {
            get
            {
                var concurrencyInfo = ConcurrencyManager.Instance.GetConcurrencyInfo();
                return concurrencyInfo.GetSummary();
            }
        }

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

                // 添加Web端兼容的事件处理
                SetupWebCompatibleEvents();

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

            // 转发转换进度到FileUploadView
            ConversionProgressUpdated?.Invoke(taskId, progress, speed, remainingSeconds.HasValue ? remainingSeconds.Value : (double?)null);
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

                    // 显示转换完成通知
                    var fileName = Path.GetFileName(outputPath ?? task?.TaskName ?? "未知文件");
                    ShowNotification("转换完成", $"文件 '{fileName}' 转换成功");
                }
                else
                {
                    // 显示转换失败通知
                    var fileName = task?.TaskName ?? "未知文件";
                    ShowNotification("转换失败", $"文件 '{fileName}' 转换失败: {status}");
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

                // 准备转换请求（与ConversionTask模型对应）
                var request = new StartConversionRequest
                {
                    // 基本信息
                    TaskName = settings.TaskName,
                    Preset = settings.Preset,

                    // 基本设置
                    OutputFormat = settings.OutputFormat,
                    Resolution = settings.Resolution,
                    CustomWidth = settings.CustomWidth,
                    CustomHeight = settings.CustomHeight,
                    AspectRatio = settings.AspectRatio,

                    // 视频设置
                    VideoCodec = settings.VideoCodec ?? preset.VideoCodec,
                    FrameRate = settings.FrameRate ?? preset.FrameRate,
                    QualityMode = settings.QualityMode ?? "crf",
                    VideoQuality = settings.VideoQuality ?? preset.VideoQuality,
                    VideoBitrate = settings.VideoBitrate,
                    EncodingPreset = settings.EncodingPreset ?? "medium",
                    Profile = settings.Profile,

                    // 音频设置
                    AudioCodec = settings.AudioCodec ?? preset.AudioCodec,
                    AudioChannels = settings.AudioChannels ?? "2",
                    AudioQualityMode = settings.AudioQualityMode ?? "bitrate",
                    AudioQuality = settings.AudioQuality ?? preset.AudioQuality,
                    AudioBitrate = settings.AudioBitrate,
                    CustomAudioBitrateValue = settings.CustomAudioBitrateValue,
                    AudioQualityValue = settings.AudioQualityValue,
                    SampleRate = settings.SampleRate ?? "48000",
                    AudioVolume = settings.AudioVolume,

                    // 高级选项
                    StartTime = settings.StartTime,
                    EndTime = settings.EndTime,
                    Duration = settings.Duration,
                    DurationLimit = settings.DurationLimit,
                    Deinterlace = settings.Deinterlace,
                    Denoise = settings.Denoise,
                    ColorSpace = settings.ColorSpace ?? "bt709",
                    PixelFormat = settings.PixelFormat ?? "yuv420p",
                    CustomParams = settings.CustomParams,
                    CustomParameters = settings.CustomParameters,
                    HardwareAcceleration = settings.HardwareAcceleration ?? "auto",
                    VideoFilters = settings.VideoFilters,
                    AudioFilters = settings.AudioFilters,

                    // 任务设置
                    Priority = settings.Priority,
                    MaxRetries = settings.MaxRetries,
                    Tags = settings.Tags,
                    Notes = settings.Notes,

                    // 编码选项
                    TwoPass = settings.TwoPass,
                    FastStart = settings.FastStart,
                    CopyTimestamps = settings.CopyTimestamps
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

                    // 检查是否需要显示通知
                    var settingsService = Services.SystemSettingsService.Instance;
                    if (settingsService.ShouldShowNotifications())
                    {
                        ShowNotification("转换开始", $"任务 '{response.Data.TaskName}' 已开始转换");
                    }

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

        /// <summary>
        /// 加入SignalR任务组
        /// </summary>
        public async Task JoinTaskGroupAsync(string taskId)
        {
            try
            {
                Utils.Logger.Info("MainWindowViewModel", $"🔗 加入SignalR任务组: {taskId}");
                await signalRService.JoinTaskGroupAsync(taskId);
            }
            catch (Exception ex)
            {
                Utils.Logger.Info("MainWindowViewModel", $"❌ 加入SignalR任务组失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 设置与Web端兼容的SignalR事件处理
        /// </summary>
        private void SetupWebCompatibleEvents()
        {
            // 注册Web端的上传相关事件
            signalRService.RegisterHandler("UploadStarted", (data) =>
            {
                StatusText = $"📤 开始上传文件";
            });

            signalRService.RegisterHandler("UploadProgress", (data) =>
            {
                StatusText = $"📤 文件上传中...";
            });

            signalRService.RegisterHandler("UploadCompleted", (data) =>
            {
                StatusText = $"✅ 文件上传完成";
            });

            signalRService.RegisterHandler("UploadFailed", (data) =>
            {
                StatusText = $"❌ 文件上传失败";
            });
        }

        /// <summary>
        /// 开始文件转换
        /// </summary>
        public async Task<bool> StartFileConversionAsync(string filePath, Models.StartConversionRequest request)
        {
            try
            {
                StatusText = $"📤 准备转换: {Path.GetFileName(filePath)}";

                var result = await apiService.StartConversionAsync(filePath, request);

                if (result.Success && result.Data != null)
                {
                    CurrentTaskId = result.Data.TaskId;
                    StatusText = $"🎬 转换已启动: {result.Data.TaskName}";
                    return true;
                }
                else
                {
                    StatusText = $"❌ 转换启动失败: {result.Message}";
                    return false;
                }
            }
            catch (Exception ex)
            {
                StatusText = $"❌ 转换启动异常: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// 处理系统设置变化
        /// </summary>
        private async void OnSystemSettingsChanged(object? sender, Services.SystemSettingsChangedEventArgs e)
        {
            try
            {
                // 如果服务器地址发生变化，需要重新连接
                if (e.ServerAddressChanged)
                {
                    StatusText = "🔄 服务器地址已更改，正在重新连接...";

                    // 更新API服务的基础URL
                    apiService.BaseUrl = e.NewSettings.ServerAddress;

                    // 重新连接SignalR
                    await signalRService.DisconnectAsync();
                    signalRService = new SignalRService(e.NewSettings.ServerAddress);
                    SetupWebCompatibleEvents();
                    await signalRService.ConnectAsync();

                    StatusText = $"✅ 已重新连接到服务器: {e.NewSettings.ServerAddress}";
                }

                // 如果并发设置发生变化，可以在这里处理
                if (e.ConcurrencySettingsChanged)
                {
                    StatusText = $"⚙️ 并发设置已更新 - 上传:{e.NewSettings.MaxConcurrentUploads}, 下载:{e.NewSettings.MaxConcurrentDownloads}";
                }

                // 如果其他设置发生变化
                if (e.OtherSettingsChanged)
                {
                    StatusText = "⚙️ 应用设置已更新";
                }
            }
            catch (Exception ex)
            {
                StatusText = $"❌ 应用新设置失败: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"应用系统设置变化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取当前系统设置
        /// </summary>
        public Models.SystemSettingsModel GetCurrentSettings()
        {
            return Services.SystemSettingsService.Instance.CurrentSettings;
        }

        /// <summary>
        /// 应用新的系统设置
        /// </summary>
        public void ApplySettings(Models.SystemSettingsModel newSettings)
        {
            Services.SystemSettingsService.Instance.UpdateSettings(newSettings);
        }

        // 清理资源
        public async Task CleanupAsync()
        {
            try
            {
                // 取消设置变化监听
                Services.SystemSettingsService.Instance.SettingsChanged -= OnSystemSettingsChanged;

                await signalRService.DisconnectAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清理资源失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示通知
        /// </summary>
        private void ShowNotification(string title, string message)
        {
            try
            {
                var settingsService = Services.SystemSettingsService.Instance;
                if (!settingsService.ShouldShowNotifications())
                    return;

                // 简单的调试输出通知，实际项目中可以使用系统通知
                System.Diagnostics.Debug.WriteLine($"📢 通知: {title} - {message}");

                // 可以在这里集成真正的通知系统，比如：
                // - Windows Toast 通知
                // - 应用内通知栏
                // - 系统托盘通知

                // 暂时在状态栏显示通知信息
                var originalStatus = StatusText;
                StatusText = $"📢 {title}: {message}";

                // 3秒后恢复原状态
                Task.Delay(3000).ContinueWith(_ =>
                {
                    if (StatusText.StartsWith("📢"))
                    {
                        StatusText = originalStatus;
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示通知失败: {ex.Message}");
            }
        }
    }
}
