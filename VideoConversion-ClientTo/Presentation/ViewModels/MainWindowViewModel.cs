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
                
                Utils.Logger.Info("MainWindowViewModel", "🔌 开始连接服务器");
                
                var connected = await _signalRClient.ConnectAsync();
                if (connected)
                {
                    IsConnected = true;
                    ConnectionStatus = "已连接";
                    Utils.Logger.Info("MainWindowViewModel", "✅ 服务器连接成功");
                    
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
            try
            {
                Utils.Logger.Info("MainWindowViewModel", "⚙️ 打开系统设置");

                // 创建并显示转换设置窗口
                var settingsWindow = new Views.ConversionSettingsWindow();

                // 获取主窗口
                if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
                {
                    await settingsWindow.ShowDialog(desktop.MainWindow);
                }
                else
                {
                    settingsWindow.Show();
                }

                Utils.Logger.Info("MainWindowViewModel", "✅ 系统设置窗口已关闭");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("MainWindowViewModel", $"❌ 打开系统设置失败: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task OpenServerSettingsAsync()
        {
            try
            {
                Utils.Logger.Info("MainWindowViewModel", "⚙️ 打开服务器设置");

                // 创建并显示转换设置窗口（这里可以创建专门的服务器设置窗口）
                var settingsWindow = new Views.ConversionSettingsWindow();

                // 获取主窗口
                if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
                {
                    await settingsWindow.ShowDialog(desktop.MainWindow);
                }
                else
                {
                    settingsWindow.Show();
                }

                Utils.Logger.Info("MainWindowViewModel", "✅ 服务器设置窗口已关闭");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("MainWindowViewModel", $"❌ 打开服务器设置失败: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task RefreshSpaceAsync()
        {
            try
            {
                Utils.Logger.Info("MainWindowViewModel", "🔄 刷新磁盘空间信息");
                // TODO: 实现刷新磁盘空间信息
                await Task.Delay(100); // 模拟异步操作
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
            Utils.Logger.Info("MainWindowViewModel", "✅ SignalR连接成功");
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
                Utils.Logger.Info("MainWindowViewModel", "🚀 初始化主窗口");
                
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
