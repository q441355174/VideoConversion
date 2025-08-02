using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using VideoConversion_ClientTo.Domain.ValueObjects;
using VideoConversion_ClientTo.Domain.Models;
using VideoConversion_ClientTo.Presentation.ViewModels;
using VideoConversion_ClientTo.Infrastructure;

namespace VideoConversion_ClientTo.Views
{
    /// <summary>
    /// 转换设置窗口 - 融合Client项目的完整实现
    /// </summary>
    public partial class ConversionSettingsWindow : Window
    {
        private readonly ConversionSettingsViewModel _viewModel;
        public bool SettingsChanged { get; private set; } = false;

        public ConversionSettingsWindow()
        {
            InitializeComponent();

            // 创建并设置ViewModel
            _viewModel = new ConversionSettingsViewModel();
            DataContext = _viewModel;

            // 设置事件处理
            SetupEventHandlers();

            // 设置窗口关闭事件 - 与Client项目一致
            Closing += OnWindowClosing;

            Utils.Logger.Info("ConversionSettingsWindow", "✅ 转换设置窗口已初始化");
        }

        public ConversionSettingsWindow(ConversionSettings? currentSettings) : this()
        {
            // 兼容性构造函数 - 与Client项目完全一致
            if (currentSettings != null)
            {
                Utils.Logger.Info("ConversionSettingsWindow", "📁 使用提供的设置初始化窗口");
                // 注意：由于我们使用全局设置服务，这里不需要额外操作
                // currentSettings 参数保留是为了兼容性
            }
        }



        private void SetupEventHandlers()
        {
            try
            {
                // 按钮事件 - 使用Command绑定，这里只处理窗口关闭
                if (SaveButton != null)
                {
                    SaveButton.Click += (s, e) =>
                    {
                        _viewModel.SaveSettingsCommand.Execute(null);
                        SettingsChanged = true;
                        Close();
                    };
                }

                if (CancelButton != null)
                {
                    CancelButton.Click += (s, e) =>
                    {
                        _viewModel.CancelCommand.Execute(null);
                        SettingsChanged = false;
                        Close();
                    };
                }

                // 预设变化事件
                if (PresetCombo != null)
                {
                    PresetCombo.SelectionChanged += (s, e) =>
                    {
                        _viewModel.ApplyPresetCommand.Execute(null);
                    };
                }

                // 质量模式变化事件
                if (QualityModeCombo != null)
                {
                    QualityModeCombo.SelectionChanged += (s, e) =>
                    {
                        _viewModel.UpdateQualityModeCommand.Execute(null);
                        UpdateQualityModeVisibility();
                    };
                }

                // 滑块事件 - 与Client项目一致
                if (CrfQualitySlider != null)
                {
                    CrfQualitySlider.ValueChanged += (s, e) =>
                    {
                        // 更新CRF值显示文本
                        var crfValueText = this.FindControl<TextBlock>("CrfValueText");
                        if (crfValueText != null)
                        {
                            crfValueText.Text = ((int)e.NewValue).ToString();
                        }
                    };
                }

                if (AudioVolumeSlider != null)
                {
                    AudioVolumeSlider.ValueChanged += (s, e) =>
                    {
                        // 更新音量值显示文本
                        var volumeValueText = this.FindControl<TextBlock>("VolumeValueText");
                        if (volumeValueText != null)
                        {
                            volumeValueText.Text = $"{e.NewValue:F1} dB";
                        }
                    };
                }

                // 事件处理器设置完成（移除Debug日志）
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionSettingsWindow", $"❌ 设置事件处理器失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新质量模式控件可见性
        /// </summary>
        private void UpdateQualityModeVisibility()
        {
            try
            {
                var isCrfMode = _viewModel.IsCrfMode;

                // 更新控件可见性
                if (CrfQualityPanel != null) CrfQualityPanel.IsVisible = isCrfMode;
                if (BitratePanel != null) BitratePanel.IsVisible = !isCrfMode;

                // 更新标签可见性
                var crfLabel = this.FindControl<TextBlock>("CrfLabel");
                var bitrateLabel = this.FindControl<TextBlock>("BitrateLabel");
                if (crfLabel != null) crfLabel.IsVisible = isCrfMode;
                if (bitrateLabel != null) bitrateLabel.IsVisible = !isCrfMode;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionSettingsWindow", $"❌ 更新质量模式可见性失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取当前设置摘要
        /// </summary>
        public string GetSettingsSummary()
        {
            return _viewModel.SettingsSummary;
        }

        /// <summary>
        /// 验证设置有效性
        /// </summary>
        public bool ValidateSettings()
        {
            try
            {
                // 基本验证逻辑
                return !string.IsNullOrEmpty(_viewModel.SettingsSummary);
            }
            catch
            {
                return false;
            }
        }

        #region 生命周期管理 - 与Client项目一致

        /// <summary>
        /// 窗口关闭事件处理 - 与SystemSettingsWindow一致
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
                            _viewModel.SaveSettingsCommand.Execute(null);
                            SettingsChanged = true;
                            Close(); // 保存后关闭
                            break;

                        case SaveDialogResult.DontSave:
                            SettingsChanged = false;
                            Close(); // 不保存直接关闭
                            break;

                        case SaveDialogResult.Cancel:
                            // 取消关闭，什么都不做
                            break;
                    }
                }

                Utils.Logger.Info("ConversionSettingsWindow", "🚪 转换设置窗口正在关闭");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionSettingsWindow", $"❌ 窗口关闭处理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示保存确认对话框
        /// </summary>
        private async Task<SaveDialogResult> ShowSaveConfirmationDialog()
        {
            try
            {
                var messageBoxService = new Infrastructure.Services.MessageBoxService();

                // 创建自定义确认对话框
                var dialog = new Window
                {
                    Title = "保存设置",
                    Width = 400,
                    Height = 200,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    CanResize = false
                };

                var result = SaveDialogResult.Cancel;

                // 简化版对话框 - 实际项目中应该使用专门的对话框控件
                var confirmResult = await messageBoxService.ShowConfirmAsync(
                    "转换设置已修改，是否保存？",
                    "保存设置",
                    this);

                return confirmResult ? SaveDialogResult.Save : SaveDialogResult.DontSave;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionSettingsWindow", $"❌ 显示保存确认对话框失败: {ex.Message}");
                return SaveDialogResult.Cancel;
            }
        }

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

                Utils.Logger.Info("ConversionSettingsWindow", "🧹 转换设置窗口资源已清理");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionSettingsWindow", $"❌ 资源清理失败: {ex.Message}");
            }
            finally
            {
                base.OnClosed(e);
            }
        }

        #endregion
    }

    /// <summary>
    /// 保存对话框结果枚举
    /// </summary>
    public enum SaveDialogResult
    {
        Save,
        DontSave,
        Cancel
    }
}