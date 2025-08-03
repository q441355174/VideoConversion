using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VideoConversion_ClientTo.Application.Interfaces;
using VideoConversion_ClientTo.Domain.Entities;
using VideoConversion_ClientTo.ViewModels;

namespace VideoConversion_ClientTo.Presentation.ViewModels
{
    /// <summary>
    /// STEP-3: 简化的主窗口视图模型
    /// 职责: 主界面的数据绑定和命令处理
    /// </summary>
    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly IConversionTaskService _conversionTaskService;
        private readonly ISignalRClient _signalRClient;
        private readonly IApiClient _apiClient;
        private ServerStatusViewModel? _serverStatusViewModel;

        // 🔑 窗口状态管理 - 防止重复打开
        private VideoConversion_ClientTo.Views.SystemSetting.SystemSettingsWindow? _currentSettingsWindow;

        // 进度转发事件
        public event Action<string, int, double?, double?>? ConversionProgressUpdated;

        [ObservableProperty]
        private string _greeting = "欢迎使用视频转换客户端";

        [ObservableProperty]
        private bool _isConnected = false;

        [ObservableProperty]
        private string _connectionStatus = "检测中...";

        [ObservableProperty]
        private ObservableCollection<ConversionTask> _tasks = new();

        [ObservableProperty]
        private ConversionTask? _selectedTask;

        [ObservableProperty]
        private bool _isLoading = false;

        // 界面切换相关
        [ObservableProperty]
        private bool _isFileUploadViewVisible = true;

        [ObservableProperty]
        private bool _isCompletedViewVisible = false;

        // 按钮样式属性 - 默认状态：正在转换按钮激活
        [ObservableProperty]
        private string _convertingButtonBackground = "#9b59b6";

        [ObservableProperty]
        private string _convertingButtonForeground = "White";

        [ObservableProperty]
        private string _completedButtonBackground = "#f0f0f0";

        [ObservableProperty]
        private string _completedButtonForeground = "#666";

        // 服务器状态相关
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
        private string _spaceWarningText = "磁盘空间不足";

        // 当前任务状态 - 这些属性将从ServerStatusViewModel同步
        [ObservableProperty]
        private bool _isNoTaskVisible = true;

        [ObservableProperty]
        private bool _isActiveTaskVisible = false;

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

        // 批量任务状态
        [ObservableProperty]
        private bool _isBatchTaskVisible = false;

        [ObservableProperty]
        private string _batchProgressText = "2/5 文件完成";

        [ObservableProperty]
        private double _batchProgress = 40;

        [ObservableProperty]
        private bool _isBatchPausedVisible = false;

        [ObservableProperty]
        private string _batchPausedText = "因空间不足暂停";

        [ObservableProperty]
        private string _statusText = "就绪 - 请选择视频文件开始转换";

        public MainWindowViewModel(
            IConversionTaskService conversionTaskService,
            ISignalRClient signalRClient,
            IApiClient apiClient)
        {
            _conversionTaskService = conversionTaskService ?? throw new ArgumentNullException(nameof(conversionTaskService));
            _signalRClient = signalRClient ?? throw new ArgumentNullException(nameof(signalRClient));
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));

            // 订阅SignalR事件
            _signalRClient.Connected += OnSignalRConnected;
            _signalRClient.Disconnected += OnSignalRDisconnected;
            _signalRClient.TaskProgressUpdated += OnTaskProgressUpdated;
            _signalRClient.TaskCompleted += OnTaskCompleted;

            // 初始化服务器状态监控
            InitializeServerStatusMonitoring();

            // 设置转换设置变化事件
            SetupConversionSettingsEvents();

            Utils.Logger.Info("MainWindowViewModel", "✅ 主窗口视图模型已初始化");

            // 初始化连接
            _ = InitializeAsync();
        }

        #region 命令

        [RelayCommand]
        private async Task ConnectToServerAsync()
        {
            try
            {
                IsLoading = true;
                ConnectionStatus = "正在连接...";
                
                // 开始连接服务器（移除日志）
                
                var connected = await _signalRClient.ConnectAsync();
                if (connected)
                {
                    IsConnected = true;
                    ConnectionStatus = "已连接";
                    // 服务器连接成功（移除日志）
                    
                    // 加载任务列表
                    await LoadTasksAsync();
                }
                else
                {
                    IsConnected = false;
                    ConnectionStatus = "连接失败";
                    Utils.Logger.Error("MainWindowViewModel", "❌ 服务器连接失败");
                }
            }
            catch (Exception ex)
            {
                IsConnected = false;
                ConnectionStatus = "连接错误";
                Utils.Logger.Error("MainWindowViewModel", $"❌ 连接服务器时发生错误: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task DisconnectFromServerAsync()
        {
            try
            {
                Utils.Logger.Info("MainWindowViewModel", "🔌 断开服务器连接");
                await _signalRClient.DisconnectAsync();
                IsConnected = false;
                ConnectionStatus = "已断开";
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("MainWindowViewModel", $"❌ 断开连接时发生错误: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task LoadTasksAsync()
        {
            try
            {
                IsLoading = true;
                Utils.Logger.Info("MainWindowViewModel", "📋 加载任务列表");
                
                var tasks = await _conversionTaskService.GetAllTasksAsync();
                
                Tasks.Clear();
                foreach (var task in tasks)
                {
                    Tasks.Add(task);
                }
                
                Utils.Logger.Info("MainWindowViewModel", $"✅ 已加载 {Tasks.Count} 个任务");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("MainWindowViewModel", $"❌ 加载任务列表失败: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            await LoadTasksAsync();
        }

        [RelayCommand]
        private async Task CreateTestTaskAsync()
        {
            try
            {
                Utils.Logger.Info("MainWindowViewModel", "🧪 创建测试任务");

                var parameters = Domain.ValueObjects.ConversionParameters.CreateDefault();
                var task = await _conversionTaskService.CreateTaskAsync(
                    $"测试任务 {DateTime.Now:HH:mm:ss}",
                    @"C:\temp\test.mp4",
                    1024 * 1024 * 100, // 100MB
                    parameters);

                Tasks.Add(task);
                Utils.Logger.Info("MainWindowViewModel", $"✅ 测试任务创建成功: {task.Id}");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("MainWindowViewModel", $"❌ 创建测试任务失败: {ex.Message}");
            }
        }

        [RelayCommand]
        private void ShowConvertingView()
        {
            try
            {
                Utils.Logger.Info("MainWindowViewModel", "🔄 ShowConvertingView被调用");

                IsFileUploadViewVisible = true;
                IsCompletedViewVisible = false;

                // 更新按钮样式 - 与原项目逻辑一致
                UpdateButtonStates(true);

                // 更新状态栏 - 与原项目逻辑一致
                UpdateStatus("📁 文件上传界面");

                Utils.Logger.Info("MainWindowViewModel", "✅ 切换到正在转换视图完成");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("MainWindowViewModel", $"❌ 切换视图失败: {ex.Message}");
            }
        }

        [RelayCommand]
        private void ShowCompletedView()
        {
            try
            {
                Utils.Logger.Info("MainWindowViewModel", "🔄 ShowCompletedView被调用");

                IsFileUploadViewVisible = false;
                IsCompletedViewVisible = true;

                // 更新按钮样式 - 与原项目逻辑一致
                UpdateButtonStates(false);

                // 更新状态栏 - 与原项目逻辑一致
                UpdateStatus("✅ 转换完成界面");

                Utils.Logger.Info("MainWindowViewModel", "✅ 切换到转换完成视图完成");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("MainWindowViewModel", $"❌ 切换视图失败: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task OpenSystemSettingsAsync()
        {
            // 🔑 统一使用OpenServerSettingsAsync方法，避免重复代码和重复窗口问题
            await OpenServerSettingsAsync();
        }

        [RelayCommand]
        private async Task OpenServerSettingsAsync()
        {
            try
            {
                Utils.Logger.Info("MainWindowViewModel", "⚙️ 打开服务器设置");

                // 🔑 检查是否已有窗口打开 - 防止重复打开
                if (_currentSettingsWindow != null)
                {
                    Utils.Logger.Info("MainWindowViewModel", "⚠️ 系统设置窗口已打开，激活现有窗口");

                    // 激活现有窗口并置顶
                    _currentSettingsWindow.Activate();
                    _currentSettingsWindow.Topmost = true;
                    _currentSettingsWindow.Topmost = false; // 重置Topmost以正常显示
                    return;
                }

                // 🔑 创建并显示系统设置窗口 - 与Client项目逻辑一致
                _currentSettingsWindow = new VideoConversion_ClientTo.Views.SystemSetting.SystemSettingsWindow();

                if (_currentSettingsWindow == null)
                {
                    Utils.Logger.Error("MainWindowViewModel", "❌ 系统设置窗口创建失败");
                    return;
                }

                // 🔑 设置窗口关闭事件 - 清理引用
                _currentSettingsWindow.Closed += (s, e) =>
                {
                    _currentSettingsWindow = null;
                    Utils.Logger.Info("MainWindowViewModel", "🔄 系统设置窗口已关闭，清理引用");
                };

                // 🔑 保存窗口引用，防止在ShowDialog期间被清空
                var settingsWindow = _currentSettingsWindow;

                // 获取主窗口
                if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
                {
                    await settingsWindow.ShowDialog(desktop.MainWindow);

                    // 🔑 检查设置是否有变化 - 使用保存的引用，防止空引用
                    if (settingsWindow?.SettingsChanged == true)
                    {
                        Utils.Logger.Info("MainWindowViewModel", "📝 服务器设置已更改，刷新相关状态");

                        // 更新状态显示
                        UpdateStatus("⚙️ 服务器设置已更新");

                        // 🔑 重新启动服务器状态监控以应用新设置
                        await StartServerStatusMonitoringAsync();
                    }
                    else
                    {
                        Utils.Logger.Info("MainWindowViewModel", "ℹ️ 服务器设置未更改");
                    }
                }
                else
                {
                    settingsWindow.Show();
                }

                Utils.Logger.Info("MainWindowViewModel", "✅ 服务器设置窗口操作完成");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("MainWindowViewModel", $"❌ 打开服务器设置失败: {ex.Message}");
                Utils.Logger.Error("MainWindowViewModel", $"❌ 堆栈跟踪: {ex.StackTrace}");

                // 🔑 异常时清理窗口引用
                _currentSettingsWindow = null;

                // 更新状态显示
                UpdateStatus("❌ 打开设置窗口失败");
            }
        }

        [RelayCommand]
        private async Task RefreshSpaceAsync()
        {
            try
            {
                Utils.Logger.Info("MainWindowViewModel", "🔄 手动刷新服务器状态和磁盘空间");

                // 通过ServerStatusViewModel刷新服务器状态
                if (_serverStatusViewModel != null)
                {
                    await _serverStatusViewModel.RefreshServerStatus();
                    Utils.Logger.Info("MainWindowViewModel", "✅ 服务器状态刷新完成");
                }
                else
                {
                    Utils.Logger.Warning("MainWindowViewModel", "⚠️ ServerStatusViewModel未初始化");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("MainWindowViewModel", $"❌ 刷新磁盘空间失败: {ex.Message}");
            }
        }

        #endregion

        #region 事件处理

        private void OnSignalRConnected(object? sender, EventArgs e)
        {
            IsConnected = true;
            ConnectionStatus = "已连接";
            // SignalR连接成功（移除日志）
        }

        private void OnSignalRDisconnected(object? sender, string message)
        {
            IsConnected = false;
            ConnectionStatus = $"连接断开: {message}";
            Utils.Logger.Warning("MainWindowViewModel", $"⚠️ SignalR连接断开: {message}");
        }

        private void OnTaskProgressUpdated(object? sender, Application.DTOs.ConversionProgressDto progress)
        {
            // 更新对应任务的进度
            var task = Tasks.FirstOrDefault(t => t.Id.Value == progress.TaskId);
            if (task != null)
            {
                // 这里需要更新任务进度，但由于领域模型的封装，需要通过服务来更新
                Utils.Logger.Debug("MainWindowViewModel", $"📊 任务进度更新: {progress.TaskId} - {progress.Progress}%");
            }

            // 转发进度到FileUploadView
            ForwardConversionProgress(progress.TaskId, progress.Progress, progress.Speed, progress.EstimatedRemainingSeconds);
        }

        private void OnTaskCompleted(object? sender, Application.DTOs.TaskCompletedDto completed)
        {
            Utils.Logger.Info("MainWindowViewModel", $"✅ 任务完成通知: {completed.TaskName}");
            // 刷新任务列表
            _ = LoadTasksAsync();
        }

        private void OnServerStatusPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            try
            {
                // 当服务器状态属性变化时，同步UI状态
                if (_serverStatusViewModel != null)
                {
                    switch (e.PropertyName)
                    {
                        case nameof(ServerStatusViewModel.IsServerConnected):
                            IsConnected = _serverStatusViewModel.IsServerConnected;
                            ConnectionStatus = _serverStatusViewModel.IsServerConnected ? "已连接" : "未连接";
                            break;
                        case nameof(ServerStatusViewModel.UsedSpaceText):
                            UsedSpaceText = _serverStatusViewModel.UsedSpaceText;
                            break;
                        case nameof(ServerStatusViewModel.TotalSpaceText):
                            TotalSpaceText = _serverStatusViewModel.TotalSpaceText;
                            break;
                        case nameof(ServerStatusViewModel.AvailableSpaceText):
                            AvailableSpaceText = _serverStatusViewModel.AvailableSpaceText;
                            break;
                        case nameof(ServerStatusViewModel.DiskUsagePercentage):
                            DiskUsagePercentage = _serverStatusViewModel.DiskUsagePercentage;
                            break;
                        case nameof(ServerStatusViewModel.IsSpaceWarningVisible):
                            IsSpaceWarningVisible = _serverStatusViewModel.IsSpaceWarningVisible;
                            break;
                        case nameof(ServerStatusViewModel.SpaceWarningText):
                            SpaceWarningText = _serverStatusViewModel.SpaceWarningText;
                            break;
                        case nameof(ServerStatusViewModel.HasActiveTask):
                            IsNoTaskVisible = !_serverStatusViewModel.HasActiveTask;
                            IsActiveTaskVisible = _serverStatusViewModel.HasActiveTask;
                            break;
                        case nameof(ServerStatusViewModel.CurrentTaskName):
                            CurrentTaskName = _serverStatusViewModel.CurrentTaskName;
                            break;
                        case nameof(ServerStatusViewModel.CurrentFileName):
                            CurrentFileName = _serverStatusViewModel.CurrentFileName;
                            break;
                        case nameof(ServerStatusViewModel.TaskProgressText):
                            TaskProgressText = _serverStatusViewModel.TaskProgressText;
                            break;
                        case nameof(ServerStatusViewModel.TaskSpeedText):
                            TaskSpeedText = _serverStatusViewModel.TaskSpeedText;
                            break;
                        case nameof(ServerStatusViewModel.TaskETAText):
                            TaskETAText = _serverStatusViewModel.TaskETAText;
                            break;
                        case nameof(ServerStatusViewModel.TaskProgress):
                            TaskProgress = _serverStatusViewModel.TaskProgress;
                            break;
                        case nameof(ServerStatusViewModel.HasBatchTask):
                            IsBatchTaskVisible = _serverStatusViewModel.HasBatchTask;
                            break;
                        case nameof(ServerStatusViewModel.BatchProgressText):
                            BatchProgressText = _serverStatusViewModel.BatchProgressText;
                            break;
                        case nameof(ServerStatusViewModel.BatchProgress):
                            BatchProgress = _serverStatusViewModel.BatchProgress;
                            break;
                        case nameof(ServerStatusViewModel.IsBatchPaused):
                            IsBatchPausedVisible = _serverStatusViewModel.IsBatchPaused;
                            break;
                        case nameof(ServerStatusViewModel.BatchPausedText):
                            BatchPausedText = _serverStatusViewModel.BatchPausedText;
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("MainWindowViewModel", $"❌ 处理服务器状态变化失败: {ex.Message}");
            }
        }

        #endregion

        #region 界面状态管理

        /// <summary>
        /// 更新切换按钮的状态 - 与原项目逻辑一致
        /// </summary>
        private void UpdateButtonStates(bool isConvertingActive)
        {
            if (isConvertingActive)
            {
                // 正在转换按钮激活
                ConvertingButtonBackground = "#9b59b6";
                ConvertingButtonForeground = "White";

                // 转换完成按钮非激活
                CompletedButtonBackground = "#f0f0f0";
                CompletedButtonForeground = "#666";
            }
            else
            {
                // 正在转换按钮非激活
                ConvertingButtonBackground = "#f0f0f0";
                ConvertingButtonForeground = "#666";

                // 转换完成按钮激活
                CompletedButtonBackground = "#9b59b6";
                CompletedButtonForeground = "White";
            }
        }

        /// <summary>
        /// 更新状态栏 - 与原项目逻辑一致
        /// </summary>
        private void UpdateStatus(string status)
        {
            try
            {
                StatusText = status;
                Utils.Logger.Debug("MainWindowViewModel", $"📊 状态更新: {status}");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("MainWindowViewModel", $"❌ 更新状态失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 设置转换设置变化事件 - 与原项目逻辑一致
        /// </summary>
        private void SetupConversionSettingsEvents()
        {
            try
            {
                // 这里可以订阅转换设置服务的变化事件
                // 由于新架构中可能没有ConversionSettingsService，我们先预留接口
                Utils.Logger.Info("MainWindowViewModel", "✅ 转换设置事件已设置");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("MainWindowViewModel", $"❌ 设置转换设置事件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理转换设置变化 - 与原项目逻辑一致
        /// </summary>
        private void OnConversionSettingsChanged(object? sender, EventArgs e)
        {
            try
            {
                // 更新状态显示
                UpdateStatus("⚙️ 转换设置已更新");

                Utils.Logger.Info("MainWindowViewModel", "✅ 转换设置变化已处理");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("MainWindowViewModel", $"❌ 处理转换设置变化失败: {ex.Message}");
            }
        }

        #endregion

        #region 服务器状态管理

        /// <summary>
        /// 初始化服务器状态监控
        /// </summary>
        private void InitializeServerStatusMonitoring()
        {
            try
            {
                _serverStatusViewModel = new ServerStatusViewModel(_apiClient, _signalRClient);

                // 订阅属性变化事件
                _serverStatusViewModel.PropertyChanged += OnServerStatusPropertyChanged;

                Utils.Logger.Info("MainWindowViewModel", "✅ 服务器状态监控已初始化");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("MainWindowViewModel", $"❌ 初始化服务器状态监控失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 开始服务器状态监控
        /// </summary>
        public async Task StartServerStatusMonitoringAsync()
        {
            try
            {
                if (_serverStatusViewModel != null)
                {
                    await _serverStatusViewModel.StartMonitoring();

                    // 尝试加入SignalR空间监控组
                    try
                    {
                        await _signalRClient.JoinSpaceMonitoringAsync();
                        Utils.Logger.Info("MainWindowViewModel", "✅ 已加入SignalR空间监控组");
                    }
                    catch (Exception ex)
                    {
                        Utils.Logger.Warning("MainWindowViewModel", $"⚠️ 加入SignalR空间监控组失败: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("MainWindowViewModel", $"❌ 启动服务器状态监控失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 停止服务器状态监控
        /// </summary>
        public void StopServerStatusMonitoring()
        {
            try
            {
                _serverStatusViewModel?.StopMonitoring();
                Utils.Logger.Info("MainWindowViewModel", "⏹️ 服务器状态监控已停止");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("MainWindowViewModel", $"❌ 停止服务器状态监控失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取服务器状态ViewModel
        /// </summary>
        public ServerStatusViewModel? GetServerStatusViewModel()
        {
            return _serverStatusViewModel;
        }

        #endregion

        #region 进度转发

        /// <summary>
        /// 转发转换进度到FileUploadView
        /// </summary>
        private void ForwardConversionProgress(string taskId, int progress, double? speed, double? eta)
        {
            try
            {
                ConversionProgressUpdated?.Invoke(taskId, progress, speed, eta);
                Utils.Logger.Debug("MainWindowViewModel", $"📊 转发进度: {taskId} - {progress}%");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("MainWindowViewModel", $"❌ 转发进度失败: {ex.Message}");
            }
        }

        #endregion

        #region 私有方法

        private async Task InitializeAsync()
        {
            try
            {
                // 初始化主窗口（移除日志）
                
                // 自动连接到服务器
                await ConnectToServerAsync();
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("MainWindowViewModel", $"❌ 初始化失败: {ex.Message}");
            }
        }

        #endregion
    }
}
