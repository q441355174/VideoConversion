using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using VideoConversion_ClientTo.Presentation.ViewModels;
using VideoConversion_ClientTo.Infrastructure;

namespace VideoConversion_ClientTo.Views.SystemSetting
{
    /// <summary>
    /// 系统设置窗口 - 基于新框架的MVVM实现
    /// </summary>
    public partial class SystemSettingsWindow : Window
    {
        #region 私有字段

        private SystemSettingsViewModel? _viewModel;

        #endregion

        #region 公共属性

        /// <summary>
        /// 设置是否已更改
        /// </summary>
        public bool SettingsChanged => _viewModel?.SettingsChanged ?? false;

        #endregion

        #region 构造函数

        public SystemSettingsWindow()
        {
            InitializeComponent();
            InitializeViewModel();
            SetupEventHandlers();

            Utils.Logger.Info("SystemSettingsWindow", "✅ 系统设置窗口已初始化");
        }

        #endregion

        #region 初始化方法

        /// <summary>
        /// 初始化ViewModel
        /// </summary>
        private void InitializeViewModel()
        {
            try
            {
                Utils.Logger.Info("SystemSettingsWindow", "🔄 开始初始化ViewModel");

                // 直接创建ViewModel（简化实现）
                _viewModel = new SystemSettingsViewModel();

                if (_viewModel == null)
                {
                    Utils.Logger.Error("SystemSettingsWindow", "❌ ViewModel创建失败，返回null");
                    return;
                }

                DataContext = _viewModel;
                Utils.Logger.Info("SystemSettingsWindow", "✅ DataContext已设置");

                // 等待数据加载完成后再显示界面
                _ = InitializeDataAsync();

                Utils.Logger.Info("SystemSettingsWindow", "✅ ViewModel已初始化");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SystemSettingsWindow", $"❌ ViewModel初始化失败: {ex.Message}");
                Utils.Logger.Error("SystemSettingsWindow", $"❌ 堆栈跟踪: {ex.StackTrace}");

                // 创建一个最小的ViewModel作为备用
                try
                {
                    _viewModel = CreateFallbackViewModel();
                    DataContext = _viewModel;
                    Utils.Logger.Info("SystemSettingsWindow", "✅ 已创建备用ViewModel");
                }
                catch (Exception fallbackEx)
                {
                    Utils.Logger.Error("SystemSettingsWindow", $"❌ 备用ViewModel创建也失败: {fallbackEx.Message}");
                }
            }
        }

        /// <summary>
        /// 异步初始化数据
        /// </summary>
        private async Task InitializeDataAsync()
        {
            try
            {
                if (_viewModel != null)
                {
                    Utils.Logger.Info("SystemSettingsWindow", "🔄 开始数据初始化");

                    // 等待基础数据加载完成
                    await Task.Delay(50); // 给ViewModel一点时间完成基础初始化

                    // 检查ViewModel是否有CompleteInitializationAsync方法
                    if (_viewModel.GetType().GetMethod("CompleteInitializationAsync") != null)
                    {
                        // 执行完整初始化
                        await _viewModel.CompleteInitializationAsync();
                    }
                    else
                    {
                        Utils.Logger.Warning("SystemSettingsWindow", "⚠️ ViewModel没有CompleteInitializationAsync方法");
                    }

                    Utils.Logger.Info("SystemSettingsWindow", "✅ 数据初始化完成");
                }
                else
                {
                    Utils.Logger.Error("SystemSettingsWindow", "❌ ViewModel为null，无法初始化数据");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SystemSettingsWindow", $"❌ 数据初始化失败: {ex.Message}");
                Utils.Logger.Error("SystemSettingsWindow", $"❌ 堆栈跟踪: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// 设置事件处理器
        /// </summary>
        private void SetupEventHandlers()
        {
            try
            {
                // 窗口关闭事件
                Closing += OnWindowClosing;

                // 取消按钮事件（需要保留，用于关闭窗口）
                var cancelBtn = this.FindControl<Button>("CancelBtn");
                if (cancelBtn != null)
                {
                    cancelBtn.Click += CancelBtn_Click;
                }

                Utils.Logger.Info("SystemSettingsWindow", "✅ 事件处理器已设置");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SystemSettingsWindow", $"❌ 设置事件处理器失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 取消按钮点击事件
        /// </summary>
        private void CancelBtn_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                Utils.Logger.Info("SystemSettingsWindow", "🚪 用户点击取消按钮");
                Close();
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SystemSettingsWindow", $"❌ 取消按钮处理失败: {ex.Message}");
            }
        }

        #endregion

        #region 事件处理方法

        /// <summary>
        /// 窗口关闭事件处理 - 简化逻辑，直接关闭
        /// </summary>
        private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
        {
            try
            {
                // 🔧 简化逻辑：直接关闭，不提示保存
                Utils.Logger.Info("SystemSettingsWindow", "🚪 系统设置窗口正在关闭");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SystemSettingsWindow", $"❌ 窗口关闭处理失败: {ex.Message}");
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 获取当前设置是否已更改
        /// </summary>
        public bool HasSettingsChanged()
        {
            return _viewModel?.SettingsChanged ?? false;
        }

        /// <summary>
        /// 手动保存设置
        /// </summary>
        public async Task<bool> SaveSettingsAsync()
        {
            if (_viewModel != null)
            {
                await _viewModel.SaveSettingsCommand.ExecuteAsync(null);
                return _viewModel.SettingsChanged;
            }
            return false;
        }

        #endregion

        #region 资源清理

        /// <summary>
        /// 窗口关闭时的资源清理
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // 清理ViewModel资源
                if (_viewModel is IDisposable disposableViewModel)
                {
                    disposableViewModel.Dispose();
                }

                Utils.Logger.Info("SystemSettingsWindow", "🧹 系统设置窗口资源已清理");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SystemSettingsWindow", $"❌ 资源清理失败: {ex.Message}");
            }
            finally
            {
                base.OnClosed(e);
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 获取设置摘要信息
        /// </summary>
        public string GetSettingsSummary()
        {
            try
            {
                if (_viewModel == null)
                    return "设置摘要获取失败";

                return $"服务器: {_viewModel.ServerAddress}, " +
                       $"并发上传: {_viewModel.MaxConcurrentUploads}, " +
                       $"并发下载: {_viewModel.MaxConcurrentDownloads}, " +
                       $"自动转换: {(_viewModel.AutoStartConversion ? "是" : "否")}, " +
                       $"显示通知: {(_viewModel.ShowNotifications ? "是" : "否")}";
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SystemSettingsWindow", $"❌ 获取设置摘要失败: {ex.Message}");
                return "设置摘要获取失败";
            }
        }

        /// <summary>
        /// 创建备用ViewModel（当正常创建失败时使用）
        /// </summary>
        private SystemSettingsViewModel CreateFallbackViewModel()
        {
            Utils.Logger.Info("SystemSettingsWindow", "🔧 创建备用ViewModel");

            // 创建一个最简单的ViewModel，不依赖任何服务
            var fallbackViewModel = new SystemSettingsViewModel();

            // 手动设置默认值，确保UI有数据显示
            fallbackViewModel.ServerAddress = "http://localhost:5065";
            fallbackViewModel.MaxConcurrentUploads = 3;
            fallbackViewModel.MaxConcurrentDownloads = 3;
            fallbackViewModel.MaxConcurrentChunks = 4;
            fallbackViewModel.AutoStartConversion = false;
            fallbackViewModel.ShowNotifications = true;
            fallbackViewModel.DefaultOutputPath = "";

            // 设置连接状态
            fallbackViewModel.ConnectionStatus = "未测试";
            fallbackViewModel.ConnectionStatusColor = "#808080";
            fallbackViewModel.IsTestingConnection = false;

            // 设置数据库状态
            fallbackViewModel.DatabasePath = "VideoConversion.db";
            fallbackViewModel.DatabaseStatus = "离线模式";
            fallbackViewModel.DatabaseSize = "未知";

            // 设置服务器信息
            fallbackViewModel.ServerVersion = "离线模式";
            fallbackViewModel.FfmpegVersion = "离线模式";
            fallbackViewModel.HardwareAcceleration = "离线模式";
            fallbackViewModel.Uptime = "离线模式";

            Utils.Logger.Info("SystemSettingsWindow", "✅ 备用ViewModel创建完成");
            return fallbackViewModel;
        }

        #endregion
    }
}
