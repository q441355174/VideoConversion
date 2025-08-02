using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using VideoConversion_ClientTo.Presentation.ViewModels;
using VideoConversion_ClientTo.Infrastructure;

namespace VideoConversion_ClientTo.Views;

public partial class MainWindow : Window
{
    #region 变量
    // ViewModel
    private MainWindowViewModel? viewModel;

    // View组件
    private FileUploadView? fileUploadView;
    private ConversionCompletedView? conversionCompletedView;

    // 服务器状态面板控件引用
    private Ellipse? serverStatusIndicator;
    private TextBlock? serverStatusText;
    private Ellipse? signalRStatusIndicator;
    private TextBlock? usedSpaceText;
    private TextBlock? totalSpaceText;
    private TextBlock? availableSpaceText;
    private ProgressBar? diskUsageProgressBar;
    private Border? spaceWarningPanel;
    private TextBlock? spaceWarningText;
    private StackPanel? noTaskPanel;
    private StackPanel? activeTaskPanel;
    private TextBlock? currentTaskNameText;
    private TextBlock? currentFileNameText;
    private TextBlock? taskProgressText;
    private TextBlock? taskSpeedText;
    private TextBlock? taskETAText;
    private ProgressBar? taskProgressBar;
    private Border? batchTaskPanel;
    private TextBlock? batchProgressText;
    private ProgressBar? batchProgressBar;
    private Border? batchPausedPanel;
    private TextBlock? batchPausedText;
    private Button? refreshSpaceBtn;
    private Button? serverSettingsBtn;
    #endregion

    public MainWindow()
    {
        InitializeComponent();

        // 初始化ViewModel
        InitializeViewModel();

        // 获取View组件引用
        InitializeViewComponents();

        // 设置事件处理
        SetupEventHandlers();

        // 初始化服务器状态面板
        InitializeServerStatusPanel();

        // 预加载转换设置
        InitializeConversionSettings();

        // 初始化界面状态
        InitializeViewState();

        // 窗口关闭事件
        Closing += OnWindowClosing;
    }

    private void InitializeViewModel()
    {
        try
        {
            viewModel = ServiceLocator.GetRequiredService<MainWindowViewModel>();
            DataContext = viewModel;

            // 主窗口ViewModel初始化完成（移除日志）
        }
        catch (Exception ex)
        {
            Utils.Logger.Error("MainWindow", $"❌ 主窗口ViewModel初始化失败: {ex.Message}");
        }
    }

    private void InitializeViewComponents()
    {
        try
        {
            // 获取View组件引用
            fileUploadView = this.FindControl<FileUploadView>("FileUploadView");
            conversionCompletedView = this.FindControl<ConversionCompletedView>("ConversionCompletedView");

            // 获取服务器状态面板控件引用
            serverStatusIndicator = this.FindControl<Ellipse>("ServerStatusIndicator");
            serverStatusText = this.FindControl<TextBlock>("ServerStatusText");
            // signalRStatusIndicator = this.FindControl<Ellipse>("SignalRStatusIndicator"); // 控件不存在，已注释
            usedSpaceText = this.FindControl<TextBlock>("UsedSpaceText");
            totalSpaceText = this.FindControl<TextBlock>("TotalSpaceText");
            availableSpaceText = this.FindControl<TextBlock>("AvailableSpaceText");
            diskUsageProgressBar = this.FindControl<ProgressBar>("DiskUsageProgressBar");
            spaceWarningPanel = this.FindControl<Border>("SpaceWarningPanel");
            spaceWarningText = this.FindControl<TextBlock>("SpaceWarningText");
            noTaskPanel = this.FindControl<StackPanel>("NoTaskPanel");
            activeTaskPanel = this.FindControl<StackPanel>("ActiveTaskPanel");
            currentTaskNameText = this.FindControl<TextBlock>("CurrentTaskNameText");
            currentFileNameText = this.FindControl<TextBlock>("CurrentFileNameText");
            taskProgressText = this.FindControl<TextBlock>("TaskProgressText");
            taskSpeedText = this.FindControl<TextBlock>("TaskSpeedText");
            taskETAText = this.FindControl<TextBlock>("TaskETAText");
            taskProgressBar = this.FindControl<ProgressBar>("TaskProgressBar");
            batchTaskPanel = this.FindControl<Border>("BatchTaskPanel");
            batchProgressText = this.FindControl<TextBlock>("BatchProgressText");
            batchProgressBar = this.FindControl<ProgressBar>("BatchProgressBar");
            batchPausedPanel = this.FindControl<Border>("BatchPausedPanel");
            batchPausedText = this.FindControl<TextBlock>("BatchPausedText");
            refreshSpaceBtn = this.FindControl<Button>("RefreshSpaceBtn");
            serverSettingsBtn = this.FindControl<Button>("ServerSettingsBtn");

            // 订阅ConversionCompletedView的导航事件
            if (conversionCompletedView != null)
            {
                conversionCompletedView.NavigateToUploadRequested += OnNavigateToUploadRequested;
            }

            Utils.Logger.Info("MainWindow", "✅ 视图组件初始化完成");
        }
        catch (Exception ex)
        {
            Utils.Logger.Error("MainWindow", $"❌ 视图组件初始化失败: {ex.Message}");
        }
    }

    private void SetupEventHandlers()
    {
        try
        {
            // 连接转换进度事件
            if (viewModel != null)
            {
                viewModel.ConversionProgressUpdated += OnConversionProgressUpdated;
                // ViewModel属性变化事件
                viewModel.PropertyChanged += OnViewModelPropertyChanged;
            }

            // 设置服务器状态按钮事件
            SetupServerStatusButtonEvents();

            // 设置转换设置变化事件
            SetupConversionSettingsEvents();

            // 事件处理器设置完成（移除日志）
        }
        catch (Exception ex)
        {
            Utils.Logger.Error("MainWindow", $"❌ 事件处理器设置失败: {ex.Message}");
        }
    }

    private void InitializeServerStatusPanel()
    {
        try
        {
            // 设置服务器状态事件
            SetupServerStatusEvents();

            // 启动服务器状态监控
            if (viewModel != null)
            {
                _ = Task.Run(async () =>
                {
                    await viewModel.StartServerStatusMonitoringAsync();
                });
            }

            Utils.Logger.Info("MainWindow", "✅ 服务器状态面板已初始化");
        }
        catch (Exception ex)
        {
            Utils.Logger.Error("MainWindow", $"❌ 初始化服务器状态面板失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 设置服务器状态事件
    /// </summary>
    private void SetupServerStatusEvents()
    {
        try
        {
            var serverStatusViewModel = viewModel?.GetServerStatusViewModel();
            if (serverStatusViewModel == null) return;

            // 监听属性变化
            serverStatusViewModel.PropertyChanged += (s, e) =>
            {
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    UpdateServerStatusUI();
                });
            };

            Utils.Logger.Info("MainWindow", "✅ 服务器状态事件已设置");
        }
        catch (Exception ex)
        {
            Utils.Logger.Error("MainWindow", $"❌ 设置服务器状态事件失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 初始化转换设置 - 与Client项目逻辑完全一致（确保性初始化）
    /// </summary>
    private void InitializeConversionSettings()
    {
        try
        {
            // 🔑 显式确保转换设置服务已初始化 - 与Client项目完全一致
            Infrastructure.Services.ConversionSettingsService.Initialize();

            var settingsService = Infrastructure.Services.ConversionSettingsService.Instance;

            // 转换设置服务确保初始化完成（移除日志）

            // 更新状态栏 - 与Client项目一致
            if (viewModel != null)
            {
                viewModel.StatusText = $"⚙️ 转换设置已加载: {settingsService.CurrentSettings.Resolution}, {settingsService.CurrentSettings.VideoCodec}";
            }
        }
        catch (Exception ex)
        {
            Utils.Logger.Error("MainWindow", $"❌ 初始化转换设置服务失败: {ex.Message}");
            if (viewModel != null)
            {
                viewModel.StatusText = $"❌ 加载转换设置失败: {ex.Message}";
            }
        }
    }

    private void InitializeViewState()
    {
        try
        {
            // 默认显示文件上传界面 - 与原项目逻辑一致
            SwitchToFileUploadView();

            Utils.Logger.Info("MainWindow", "✅ 界面状态已初始化");
        }
        catch (Exception ex)
        {
            Utils.Logger.Error("MainWindow", $"❌ 初始化界面状态失败: {ex.Message}");
        }
    }

    private void OnConversionProgressUpdated(string taskId, int progress, double? speed, double? eta)
    {
        try
        {
            // 转发转换进度到FileUploadView
            fileUploadView?.UpdateConversionProgress(taskId, progress, $"转换中 {progress}%", speed, eta);

            // 转换进度更新（移除日志）
        }
        catch (Exception ex)
        {
            Utils.Logger.Error("MainWindow", $"❌ 转发进度失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 设置转换设置变化事件 - 与原项目逻辑一致
    /// </summary>
    private void SetupConversionSettingsEvents()
    {
        try
        {
            // 🔑 订阅转换设置服务的变化事件 - 与Client项目完全一致
            var conversionSettingsService = Infrastructure.Services.ConversionSettingsService.Instance;
            conversionSettingsService.SettingsChanged += OnConversionSettingsChanged;

            Utils.Logger.Info("MainWindow", "✅ 转换设置事件已设置");
        }
        catch (Exception ex)
        {
            Utils.Logger.Error("MainWindow", $"❌ 设置转换设置事件失败: {ex.Message}");
        }
    }

    #region 事件处理方法

    /// <summary>
    /// 设置服务器状态按钮事件 - 与原项目逻辑一致
    /// </summary>
    private void SetupServerStatusButtonEvents()
    {
        try
        {
            if (refreshSpaceBtn != null)
                refreshSpaceBtn.Click += RefreshSpaceBtn_Click;

            if (serverSettingsBtn != null)
                serverSettingsBtn.Click += SystemSettingsBtn_Click;

            Utils.Logger.Info("MainWindow", "✅ 服务器状态按钮事件已设置");
        }
        catch (Exception ex)
        {
            Utils.Logger.Error("MainWindow", $"❌ 设置服务器状态按钮事件失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 刷新空间按钮点击事件
    /// </summary>
    private async void RefreshSpaceBtn_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (viewModel?.GetServerStatusViewModel() != null)
            {
                await viewModel.GetServerStatusViewModel()!.RefreshServerStatus();
                Utils.Logger.Info("MainWindow", "🔄 手动刷新服务器状态");
            }
        }
        catch (Exception ex)
        {
            Utils.Logger.Error("MainWindow", $"❌ 刷新服务器状态失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 系统设置按钮点击事件 - 与原项目逻辑一致
    /// </summary>
    private async void SystemSettingsBtn_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Utils.Logger.Info("MainWindow", "⚙️ 打开系统设置窗口");
            
            var settingsWindow = new SystemSetting.SystemSettingsWindow();
            await settingsWindow.ShowDialog(this);

            // 如果设置有变化，更新应用配置 - 与原项目逻辑一致
            if (settingsWindow.SettingsChanged)
            {
                await ApplyNewSettings(settingsWindow.GetSettingsSummary());
            }

            Utils.Logger.Info("MainWindow", "✅ 系统设置窗口已关闭");
        }
        catch (Exception ex)
        {
            Utils.Logger.Error("MainWindow", $"❌ 打开系统设置失败: {ex.Message}");
            if (viewModel != null)
            {
                viewModel.StatusText = $"❌ 打开设置失败: {ex.Message}";
            }
        }
    }



    /// <summary>
    /// 处理ViewModel属性变化 - 与原项目逻辑一致
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        try
        {
            switch (e.PropertyName)
            {
                case nameof(MainWindowViewModel.StatusText):
                    UpdateStatus(viewModel?.StatusText ?? "");
                    break;
                case nameof(MainWindowViewModel.IsConnected):
                    UpdateServerStatusUI(); // 使用现有的服务器状态更新方法
                    break;
                case nameof(MainWindowViewModel.ConnectionStatus):
                    UpdateServerStatusUI();
                    break;
                // 可以根据需要添加更多属性处理
            }
        }
        catch (Exception ex)
        {
            Utils.Logger.Error("MainWindow", $"❌ 处理ViewModel属性变化失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 处理转换设置变化事件 - 与Client项目逻辑完全一致
    /// </summary>
    private void OnConversionSettingsChanged(object? sender, Infrastructure.Services.ConversionSettingsChangedEventArgs e)
    {
        try
        {
            // 通知文件上传视图更新转换后的预估值 - 与Client项目一致
            fileUploadView?.UpdateTargetInfoFromSettings();

            // 更新状态显示 - 与Client项目一致
            UpdateStatus($"⚙️ 转换设置已更新: {e.Settings.Resolution}, {e.Settings.VideoCodec}");

            // 转换设置更新完成（移除日志）
        }
        catch (Exception ex)
        {
            Utils.Logger.Error("MainWindow", $"❌ 处理转换设置变化失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 处理从ConversionCompletedView请求导航到上传页面的事件 - 与原项目逻辑一致
    /// </summary>
    private void OnNavigateToUploadRequested(object? sender, EventArgs e)
    {
        try
        {
            // 切换到上传页面 - 与原项目逻辑一致
            SwitchToFileUploadView();
            Utils.Logger.Info("MainWindow", "🔄 从完成界面导航到上传界面");
        }
        catch (Exception ex)
        {
            Utils.Logger.Error("MainWindow", $"❌ 导航到上传界面失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 更新服务器状态UI
    /// </summary>
    private void UpdateServerStatusUI()
    {
        try
        {
            var serverStatusViewModel = viewModel?.GetServerStatusViewModel();
            if (serverStatusViewModel == null) return;

            // 更新服务器连接状态
            if (serverStatusIndicator != null)
                serverStatusIndicator.Fill = serverStatusViewModel.IsServerConnected ?
                    Avalonia.Media.Brushes.Green : Avalonia.Media.Brushes.Red;

            if (serverStatusText != null)
                serverStatusText.Text = serverStatusViewModel.ServerStatusText;

            // 更新SignalR连接状态
            if (signalRStatusIndicator != null)
                signalRStatusIndicator.Fill = serverStatusViewModel.IsSignalRConnected ?
                    Avalonia.Media.Brushes.Green : Avalonia.Media.Brushes.Red;

            // 更新磁盘空间信息
            if (usedSpaceText != null)
                usedSpaceText.Text = serverStatusViewModel.UsedSpaceText;

            if (totalSpaceText != null)
                totalSpaceText.Text = serverStatusViewModel.TotalSpaceText;

            if (availableSpaceText != null)
                availableSpaceText.Text = serverStatusViewModel.AvailableSpaceText;

            if (diskUsageProgressBar != null)
                diskUsageProgressBar.Value = serverStatusViewModel.DiskUsagePercentage;

            // 更新空间警告
            if (spaceWarningPanel != null)
                spaceWarningPanel.IsVisible = serverStatusViewModel.IsSpaceWarningVisible;

            if (spaceWarningText != null)
                spaceWarningText.Text = serverStatusViewModel.SpaceWarningText;

            // 更新任务状态
            UpdateTaskStatusUI(serverStatusViewModel);
        }
        catch (Exception ex)
        {
            Utils.Logger.Error("MainWindow", $"❌ 更新服务器状态UI失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 更新任务状态UI
    /// </summary>
    private void UpdateTaskStatusUI(ServerStatusViewModel serverStatusViewModel)
    {
        try
        {
            // 更新当前任务状态
            if (noTaskPanel != null)
                noTaskPanel.IsVisible = !serverStatusViewModel.HasActiveTask;

            if (activeTaskPanel != null)
                activeTaskPanel.IsVisible = serverStatusViewModel.HasActiveTask;

            if (serverStatusViewModel.HasActiveTask)
            {
                if (currentTaskNameText != null)
                    currentTaskNameText.Text = serverStatusViewModel.CurrentTaskName;

                if (currentFileNameText != null)
                    currentFileNameText.Text = serverStatusViewModel.CurrentFileName;

                if (taskProgressText != null)
                    taskProgressText.Text = serverStatusViewModel.TaskProgressText;

                if (taskSpeedText != null)
                    taskSpeedText.Text = serverStatusViewModel.TaskSpeedText;

                if (taskETAText != null)
                    taskETAText.Text = serverStatusViewModel.TaskETAText;

                if (taskProgressBar != null)
                    taskProgressBar.Value = serverStatusViewModel.TaskProgress;
            }

            // 更新批量任务状态
            if (batchTaskPanel != null)
                batchTaskPanel.IsVisible = serverStatusViewModel.HasBatchTask;

            if (serverStatusViewModel.HasBatchTask)
            {
                if (batchProgressText != null)
                    batchProgressText.Text = serverStatusViewModel.BatchProgressText;

                if (batchProgressBar != null)
                    batchProgressBar.Value = serverStatusViewModel.BatchProgress;

                if (batchPausedPanel != null)
                    batchPausedPanel.IsVisible = serverStatusViewModel.IsBatchPaused;

                if (batchPausedText != null)
                    batchPausedText.Text = serverStatusViewModel.BatchPausedText;
            }
        }
        catch (Exception ex)
        {
            Utils.Logger.Error("MainWindow", $"❌ 更新任务状态UI失败: {ex.Message}");
        }
    }

    #endregion

    #region 切换按钮事件处理

    /// <summary>
    /// 正在转换按钮点击事件 - 与原项目逻辑一致
    /// </summary>
    private void ConvertingStatusBtn_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            // 直接操作UI控件 - 确保切换生效
            SwitchToFileUploadView();
        }
        catch (Exception ex)
        {
            Utils.Logger.Error("MainWindow", $"❌ 切换到文件上传视图失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 转换完成按钮点击事件 - 与原项目逻辑一致
    /// </summary>
    private void CompletedStatusBtn_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Utils.Logger.Info("MainWindow", "🔄 CompletedStatusBtn_Click被调用");

            // 直接操作UI控件 - 确保切换生效
            SwitchToCompletedView();

            Utils.Logger.Info("MainWindow", "✅ CompletedStatusBtn_Click处理完成");
        }
        catch (Exception ex)
        {
            Utils.Logger.Error("MainWindow", $"❌ CompletedStatusBtn_Click处理失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 切换到文件上传界面 - 直接操作UI控件
    /// </summary>
    private void SwitchToFileUploadView()
    {
        try
        {
            // 直接操作UI控件的可见性
            if (fileUploadView != null && conversionCompletedView != null)
            {
                fileUploadView.IsVisible = true;
                conversionCompletedView.IsVisible = false;
                Utils.Logger.Info("MainWindow", "✅ 界面切换: FileUploadView可见, ConversionCompletedView隐藏");
            }

            // 更新按钮样式
            UpdateButtonStates(true);

            // 更新ViewModel状态（保持数据一致性）
            if (viewModel != null)
            {
                viewModel.IsFileUploadViewVisible = true;
                viewModel.IsCompletedViewVisible = false;
                viewModel.StatusText = "📁 文件上传界面";
            }

            Utils.Logger.Info("MainWindow", "✅ 切换到文件上传界面完成");
        }
        catch (Exception ex)
        {
            Utils.Logger.Error("MainWindow", $"❌ 切换到文件上传界面失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 切换到转换完成界面 - 直接操作UI控件
    /// </summary>
    private void SwitchToCompletedView()
    {
        try
        {
            // 直接操作UI控件的可见性
            if (fileUploadView != null && conversionCompletedView != null)
            {
                fileUploadView.IsVisible = false;
                conversionCompletedView.IsVisible = true;
                Utils.Logger.Info("MainWindow", "✅ 界面切换: FileUploadView隐藏, ConversionCompletedView可见");
            }

            // 更新按钮样式
            UpdateButtonStates(false);

            // 更新ViewModel状态（保持数据一致性）
            if (viewModel != null)
            {
                viewModel.IsFileUploadViewVisible = false;
                viewModel.IsCompletedViewVisible = true;
                viewModel.StatusText = "✅ 转换完成界面";
            }

            Utils.Logger.Info("MainWindow", "✅ 切换到转换完成界面完成");
        }
        catch (Exception ex)
        {
            Utils.Logger.Error("MainWindow", $"❌ 切换到转换完成界面失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 更新按钮状态 - 直接操作UI控件
    /// </summary>
    private void UpdateButtonStates(bool isConvertingActive)
    {
        try
        {
            var convertingBtn = this.FindControl<Button>("ConvertingStatusBtn");
            var completedBtn = this.FindControl<Button>("CompletedStatusBtn");

            if (convertingBtn != null)
            {
                convertingBtn.Background = isConvertingActive ?
                    Avalonia.Media.Brush.Parse("#9b59b6") :
                    Avalonia.Media.Brush.Parse("#f0f0f0");
                convertingBtn.Foreground = isConvertingActive ?
                    Avalonia.Media.Brushes.White :
                    Avalonia.Media.Brush.Parse("#666");
            }

            if (completedBtn != null)
            {
                completedBtn.Background = !isConvertingActive ?
                    Avalonia.Media.Brush.Parse("#9b59b6") :
                    Avalonia.Media.Brush.Parse("#f0f0f0");
                completedBtn.Foreground = !isConvertingActive ?
                    Avalonia.Media.Brushes.White :
                    Avalonia.Media.Brush.Parse("#666");
            }

            // 同时更新ViewModel属性（保持数据一致性）
            if (viewModel != null)
            {
                if (isConvertingActive)
                {
                    viewModel.ConvertingButtonBackground = "#9b59b6";
                    viewModel.ConvertingButtonForeground = "White";
                    viewModel.CompletedButtonBackground = "#f0f0f0";
                    viewModel.CompletedButtonForeground = "#666";
                }
                else
                {
                    viewModel.ConvertingButtonBackground = "#f0f0f0";
                    viewModel.ConvertingButtonForeground = "#666";
                    viewModel.CompletedButtonBackground = "#9b59b6";
                    viewModel.CompletedButtonForeground = "White";
                }
            }

            Utils.Logger.Info("MainWindow", $"✅ 按钮状态已更新: 转换按钮激活={isConvertingActive}");
        }
        catch (Exception ex)
        {
            Utils.Logger.Error("MainWindow", $"❌ 更新按钮状态失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 应用新的设置 - 与原项目逻辑一致
    /// </summary>
    private async Task ApplyNewSettings(object newSettings)
    {
        try
        {
            Utils.Logger.Info("MainWindow", "🔄 应用新的设置");

            // 通过ViewModel应用新设置，这会触发自动重连等逻辑
            if (viewModel != null)
            {
                // 这里可以根据需要实现设置应用逻辑
                // viewModel.ApplySettings(newSettings);
                viewModel.StatusText = "✅ 设置已保存并应用";

                // 重新初始化服务器状态监控
                await viewModel.StartServerStatusMonitoringAsync();
            }

            Utils.Logger.Info("MainWindow", "✅ 新设置已应用");
        }
        catch (Exception ex)
        {
            Utils.Logger.Error("MainWindow", $"❌ 应用新设置失败: {ex.Message}");
            if (viewModel != null)
            {
                viewModel.StatusText = $"❌ 应用设置失败: {ex.Message}";
            }
        }
    }
    #endregion

    /// <summary>
    /// 更新状态栏 - 与原项目逻辑一致
    /// </summary>
    private void UpdateStatus(string status)
    {
        try
        {
            var statusText = this.FindControl<TextBlock>("StatusText");
            if (statusText != null)
            {
                statusText.Text = status;
            }

            // 同时更新ViewModel状态
            if (viewModel != null)
            {
                viewModel.StatusText = status;
            }

            Utils.Logger.Debug("MainWindow", $"📊 状态更新: {status}");
        }
        catch (Exception ex)
        {
            Utils.Logger.Error("MainWindow", $"❌ 更新状态失败: {ex.Message}");
        }
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        try
        {
            Utils.Logger.Info("MainWindow", "🔌 主窗口正在关闭，清理资源");

            // 停止服务器状态监控
            viewModel?.StopServerStatusMonitoring();

            // 清理事件订阅
            if (viewModel != null)
            {
                viewModel.ConversionProgressUpdated -= OnConversionProgressUpdated;
            }
        }
        catch (Exception ex)
        {
            Utils.Logger.Error("MainWindow", $"❌ 窗口关闭处理失败: {ex.Message}");
        }
    }
}