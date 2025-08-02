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
                // 直接创建ViewModel（简化实现）
                _viewModel = new SystemSettingsViewModel();
                DataContext = _viewModel;

                Utils.Logger.Info("SystemSettingsWindow", "✅ ViewModel已初始化");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SystemSettingsWindow", $"❌ ViewModel初始化失败: {ex.Message}");
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
        /// 窗口关闭事件处理
        /// </summary>
        private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
        {
            try
            {
                // 如果设置有变化，询问是否保存
                if (_viewModel?.SettingsChanged == true)
                {
                    e.Cancel = true; // 先取消关闭

                    var result = await ShowSaveConfirmationDialog();

                    switch (result)
                    {
                        case SaveDialogResult.Save:
                            await _viewModel.SaveSettingsCommand.ExecuteAsync(null);
                            Close(); // 保存后关闭
                            break;

                        case SaveDialogResult.DontSave:
                            Close(); // 不保存直接关闭
                            break;

                        case SaveDialogResult.Cancel:
                            // 取消关闭，什么都不做
                            break;
                    }
                }

                Utils.Logger.Info("SystemSettingsWindow", "🚪 系统设置窗口正在关闭");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SystemSettingsWindow", $"❌ 窗口关闭处理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示保存确认对话框
        /// </summary>
        private async Task<SaveDialogResult> ShowSaveConfirmationDialog()
        {
            try
            {
                // 这里可以使用自定义的对话框或者简单的MessageBox
                // 为了简化，我们使用一个简单的确认对话框

                var dialog = new Window
                {
                    Title = "保存设置",
                    Width = 400,
                    Height = 200,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    CanResize = false
                };

                var result = SaveDialogResult.Cancel;

                var panel = new StackPanel
                {
                    Margin = new Avalonia.Thickness(20),
                    Spacing = 20
                };

                panel.Children.Add(new TextBlock
                {
                    Text = "设置已更改，是否保存？",
                    FontSize = 14,
                    TextAlignment = Avalonia.Media.TextAlignment.Center
                });

                var buttonPanel = new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    Spacing = 10
                };

                var saveButton = new Button
                {
                    Content = "保存",
                    Width = 80,
                    Height = 32
                };
                saveButton.Click += (s, e) =>
                {
                    result = SaveDialogResult.Save;
                    dialog.Close();
                };

                var dontSaveButton = new Button
                {
                    Content = "不保存",
                    Width = 80,
                    Height = 32
                };
                dontSaveButton.Click += (s, e) =>
                {
                    result = SaveDialogResult.DontSave;
                    dialog.Close();
                };

                var cancelButton = new Button
                {
                    Content = "取消",
                    Width = 80,
                    Height = 32
                };
                cancelButton.Click += (s, e) =>
                {
                    result = SaveDialogResult.Cancel;
                    dialog.Close();
                };

                buttonPanel.Children.Add(saveButton);
                buttonPanel.Children.Add(dontSaveButton);
                buttonPanel.Children.Add(cancelButton);

                panel.Children.Add(buttonPanel);
                dialog.Content = panel;

                await dialog.ShowDialog(this);

                return result;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SystemSettingsWindow", $"❌ 显示保存确认对话框失败: {ex.Message}");
                return SaveDialogResult.Cancel;
            }
        }

        #endregion

        #region 辅助枚举

        /// <summary>
        /// 保存对话框结果
        /// </summary>
        private enum SaveDialogResult
        {
            Save,
            DontSave,
            Cancel
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

        #endregion
    }
}
