using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VideoConversion_Client.Models;
using VideoConversion_Client.ViewModels;
using VideoConversion_Client.Views;
using VideoConversion_Client.Views.SystemSetting;
using VideoConversion_Client.Services;

namespace VideoConversion_Client
{
    public partial class MainWindow : Window
    {
        #region 变量
        // ViewModel
        private MainWindowViewModel viewModel;
        private ServerStatusViewModel serverStatusViewModel;

        // View组件
        private FileUploadView fileUploadView;
        private ConversionCompletedView conversionCompletedView;

        // 服务器状态面板控件
        private Ellipse? serverStatusIndicator;
        private TextBlock? serverStatusText;
        private Ellipse? signalRStatusIndicator;
        // private TextBlock? signalRStatusText;
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
            viewModel = new MainWindowViewModel();
            DataContext = viewModel;

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

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void InitializeViewComponents()
        {
            // 获取View组件引用
            fileUploadView = this.FindControl<FileUploadView>("FileUploadView")!;
            conversionCompletedView = this.FindControl<ConversionCompletedView>("ConversionCompletedView")!;

            // 订阅ConversionCompletedView的导航事件
            conversionCompletedView.NavigateToUploadRequested += OnNavigateToUploadRequested;;

            // 获取服务器状态面板控件引用
            serverStatusIndicator = this.FindControl<Ellipse>("ServerStatusIndicator");
            serverStatusText = this.FindControl<TextBlock>("ServerStatusText");
            signalRStatusIndicator = this.FindControl<Ellipse>("SignalRStatusIndicator");
            // signalRStatusText = this.FindControl<TextBlock>("SignalRStatusText");
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

            // 连接转换进度事件
            viewModel.ConversionProgressUpdated += OnConversionProgressUpdated;
        }

        private void OnConversionProgressUpdated(string taskId, int progress, double? speed, double? eta)
        {
            // 转发转换进度到FileUploadView
            fileUploadView?.UpdateConversionProgress(taskId, progress, speed, eta);
        }

        private void InitializeServerStatusPanel()
        {
            // 创建服务器状态ViewModel
            var settingsService = Services.SystemSettingsService.Instance;
            var apiService = new Services.ApiService { BaseUrl = settingsService.GetServerAddress() };
            var signalRService = new Services.SignalRService(apiService.BaseUrl);

            serverStatusViewModel = new ServerStatusViewModel(apiService, signalRService);

            // 绑定事件
            SetupServerStatusEvents();

            // 设置按钮事件
            SetupServerStatusButtonEvents();

            // 开始监控
            _ = Task.Run(async () =>
            {
                await serverStatusViewModel.StartMonitoring();

                // 启动SignalR空间监控
                try
                {
                    await signalRService.JoinSpaceMonitoringAsync();
                    Utils.Logger.Info("MainWindow", "✅ 已加入SignalR空间监控组");
                }
                catch (Exception ex)
                {
                    Utils.Logger.Info("MainWindow", $"❌ 加入SignalR空间监控组失败: {ex.Message}");
                }
            });
        }

        private void SetupServerStatusEvents()
        {
            if (serverStatusViewModel == null) return;

            // 监听属性变化
            serverStatusViewModel.PropertyChanged += (s, e) =>
            {
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    UpdateServerStatusUI();
                });
            };
        }

        private void SetupServerStatusButtonEvents()
        {
            if (refreshSpaceBtn != null)
                refreshSpaceBtn.Click += async (s, e) => await serverStatusViewModel?.RefreshServerStatus()!;

            if (serverSettingsBtn != null)
                serverSettingsBtn.Click += SystemSettingsBtn_Click;
        }

        private void UpdateServerStatusUI()
        {
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

            // if (signalRStatusText != null)
            //     signalRStatusText.Text = serverStatusViewModel.SignalRStatusText;

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

        private void SetupEventHandlers()
        {
            // ViewModel属性变化事件
            viewModel.PropertyChanged += OnViewModelPropertyChanged;

            // 转换设置变化事件
            Services.ConversionSettingsService.Instance.SettingsChanged += OnConversionSettingsChanged;
        }

        private void InitializeConversionSettings()
        {
            try
            {
                // 显式初始化转换设置服务，确保在程序运行期间始终存在
                Services.ConversionSettingsService.Initialize();

                var settingsService = Services.ConversionSettingsService.Instance;

                // 记录初始化状态
                System.Diagnostics.Debug.WriteLine($"转换设置服务已初始化并将在程序运行期间持续存在");
                System.Diagnostics.Debug.WriteLine($"当前设置: {settingsService.CurrentSettings.VideoCodec}, {settingsService.CurrentSettings.Resolution}");

                UpdateStatus($"⚙️ 转换设置已加载: {settingsService.GetFormattedResolution()}, {settingsService.CurrentSettings.VideoCodec}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"初始化转换设置服务失败: {ex.Message}");
                UpdateStatus($"❌ 加载转换设置失败: {ex.Message}");
            }
        }

        private void InitializeViewState()
        {
            // 默认显示文件上传界面
            SwitchToFileUploadView();
        }

        // 切换按钮事件处理方法
        public void ConvertingStatusBtn_Click(object? sender, RoutedEventArgs e)
        {
            SwitchToFileUploadView();
        }

        private void CompletedStatusBtn_Click(object? sender, RoutedEventArgs e)
        {
            SwitchToCompletedView();
        }

        // 系统设置按钮点击事件
        private async void SystemSettingsBtn_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var settingsWindow = new SystemSettingsWindow();
                await settingsWindow.ShowDialog(this);

                // 如果设置有变化，更新应用配置
                if (settingsWindow.SettingsChanged)
                {
                    await ApplyNewSettings(settingsWindow.Settings);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"打开系统设置失败: {ex.Message}");
                UpdateStatus($"❌ 打开设置失败: {ex.Message}");
            }
        }

        // 应用新的设置
        private async Task ApplyNewSettings(SystemSettingsModel newSettings)
        {
            try
            {
                // 通过ViewModel应用新设置，这会触发自动重连等逻辑
                if (viewModel != null)
                {
                    viewModel.ApplySettings(newSettings);
                    UpdateStatus("✅ 设置已保存并应用");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"应用新设置失败: {ex.Message}");
                UpdateStatus($"❌ 应用设置失败: {ex.Message}");
            }
        }




        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(MainWindowViewModel.StatusText):
                    UpdateStatus(viewModel.StatusText);
                    break;
                case nameof(MainWindowViewModel.IsConnectedToServer):
                    UpdateConnectionIndicator(viewModel.IsConnectedToServer);
                    break;
            }
        }

        private void OnConversionSettingsChanged(object? sender, Services.ConversionSettingsChangedEventArgs e)
        {
            try
            {
                // 通知文件上传视图更新转换后的预估值
                fileUploadView?.UpdateTargetInfoFromSettings();

                // 更新状态显示
                UpdateStatus($"⚙️ 转换设置已更新: {e.NewSettings.Resolution}, {e.NewSettings.VideoCodec}");

                System.Diagnostics.Debug.WriteLine($"转换设置已变化: {e.NewSettings.VideoCodec}, {e.NewSettings.Resolution}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"处理转换设置变化失败: {ex.Message}");
            }
        }



        // 界面切换方法
        void SwitchToFileUploadView()
        {
            // 切换页面显示
            fileUploadView.IsVisible = true;
            conversionCompletedView.IsVisible = false;

            // 更新按钮状态
            UpdateButtonStates(true);

            UpdateStatus("📁 文件上传界面");
        }

        private void SwitchToCompletedView()
        {
            // 切换页面显示
            fileUploadView.IsVisible = false;
            conversionCompletedView.IsVisible = true;

            // 更新按钮状态
            UpdateButtonStates(false);

            UpdateStatus("✅ 转换完成界面");
        }

        /// <summary>
        /// 处理从ConversionCompletedView请求导航到上传页面的事件
        /// </summary>
        private void OnNavigateToUploadRequested(object? sender, EventArgs e)
        {
            // 切换到上传页面
            ConvertingStatusBtn_Click(null, new RoutedEventArgs());
        }

        // 更新切换按钮的状态
        private void UpdateButtonStates(bool isConvertingActive)
        {
            var convertingBtn = this.FindControl<Button>("ConvertingStatusBtn");
            var completedBtn = this.FindControl<Button>("CompletedStatusBtn");

            if (convertingBtn != null && completedBtn != null)
            {
                if (isConvertingActive)
                {
                    // 正在转换按钮激活
                    convertingBtn.Background = Avalonia.Media.Brush.Parse("#9b59b6");
                    convertingBtn.Foreground = Avalonia.Media.Brushes.White;

                    // 转换完成按钮非激活
                    completedBtn.Background = Avalonia.Media.Brush.Parse("#f0f0f0");
                    completedBtn.Foreground = Avalonia.Media.Brush.Parse("#666");
                }
                else
                {
                    // 正在转换按钮非激活
                    convertingBtn.Background = Avalonia.Media.Brush.Parse("#f0f0f0");
                    convertingBtn.Foreground = Avalonia.Media.Brush.Parse("#666");

                    // 转换完成按钮激活
                    completedBtn.Background = Avalonia.Media.Brush.Parse("#9b59b6");
                    completedBtn.Foreground = Avalonia.Media.Brushes.White;
                }
            }
        }

        // 辅助方法
        private void UpdateStatus(string status)
        {
            var statusText = this.FindControl<TextBlock>("StatusText");
            if (statusText != null)
            {
                statusText.Text = status;
            }
        }

        private void UpdateConnectionIndicator(bool connected)
        {
            var indicator = this.FindControl<Border>("ConnectionIndicator");
            var statusText = this.FindControl<TextBlock>("ConnectionStatusText");

            if (indicator != null)
            {
                indicator.Background = connected ?
                    Avalonia.Media.Brushes.Green :
                    Avalonia.Media.Brushes.Red;
            }

            if (statusText != null)
            {
                statusText.Text = connected ?
                    $"SignalR连接: 已连接 ({viewModel.ServerUrl})" :
                    $"SignalR连接: 连接失败: 由于目标计算机积极拒绝，无法连接。 ({viewModel.ServerUrl})";
            }
        }


        private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
        {
            try
            {
                // 清理ViewModel
                await viewModel.CleanupAsync();

                // 清理服务器状态监控
                if (serverStatusViewModel != null)
                {
                    serverStatusViewModel.StopMonitoring();
                }

                // 清理转换设置服务
                Services.ConversionSettingsService.Instance.Cleanup();

                System.Diagnostics.Debug.WriteLine("程序关闭，所有服务已清理");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清理资源失败: {ex.Message}");
            }
        }

        #region 服务器状态面板按钮事件

        private async void ConfigSpaceBtn_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                UpdateStatus("🔧 打开磁盘空间配置...");

                var settingsService = Services.SystemSettingsService.Instance;
                var baseUrl = settingsService.GetServerAddress();
                var configDialog = new Views.DiskSpaceConfigDialog(baseUrl);

                // 设置对话框的所有者为当前窗口
                var result = await configDialog.ShowDialog<bool?>(this);

                if (configDialog.ConfigSaved)
                {
                    UpdateStatus("✅ 磁盘空间配置已保存");

                    // 刷新服务器状态
                    if (serverStatusViewModel != null)
                    {
                        await serverStatusViewModel.RefreshServerStatus();
                    }

                    Utils.Logger.Info("MainWindow", "磁盘空间配置已更新，服务器状态已刷新");
                }
                else
                {
                    UpdateStatus("📋 磁盘空间配置已取消");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"❌ 打开空间配置失败: {ex.Message}");
                Utils.Logger.Info("MainWindow", $"打开磁盘空间配置对话框失败: {ex.Message}");
            }
        }

        private async void CleanupFilesBtn_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                // TODO: 执行文件清理
                UpdateStatus("🗑️ 文件清理功能开发中...");
            }
            catch (Exception ex)
            {
                UpdateStatus($"❌ 文件清理失败: {ex.Message}");
            }
        }

        private async void ViewLogsBtn_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                // TODO: 打开日志查看器
                UpdateStatus("📋 日志查看功能开发中...");
            }
            catch (Exception ex)
            {
                UpdateStatus($"❌ 打开日志失败: {ex.Message}");
            }
        }

        #endregion
    }
}
          