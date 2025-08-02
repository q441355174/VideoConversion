using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using VideoConversion_ClientTo.Presentation.ViewModels;
using VideoConversion_ClientTo.Infrastructure;

namespace VideoConversion_ClientTo.Views
{
    public partial class FileUploadView : UserControl
    {
        private FileUploadViewModel? _viewModel;

        public FileUploadView()
        {
            InitializeComponent();

            // 设置DataContext
            if (ServiceLocator.IsServiceRegistered<FileUploadViewModel>())
            {
                _viewModel = ServiceLocator.GetService<FileUploadViewModel>();
                DataContext = _viewModel;
            }
            else
            {
                // 如果服务未注册，创建一个临时实例
                _viewModel = new FileUploadViewModel();
                DataContext = _viewModel;
            }

            // 设置拖拽事件
            SetupDragAndDrop();
        }

        private void SetupDragAndDrop()
        {
            try
            {
                // 获取拖拽区域
                var emptyStateView = this.FindControl<Border>("EmptyStateView");
                var fileListView = this.FindControl<Grid>("FileListView");

                if (emptyStateView != null)
                {
                    SetupDragAndDropForElement(emptyStateView);
                }

                if (fileListView != null)
                {
                    SetupDragAndDropForElement(fileListView);
                }

                // 为整个控件设置拖拽
                SetupDragAndDropForElement(this);

                Utils.Logger.Info("FileUploadView", "✅ 拖拽功能已设置");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileUploadView", $"❌ 设置拖拽功能失败: {ex.Message}");
            }
        }

        private void SetupDragAndDropForElement(Control element)
        {
            DragDrop.SetAllowDrop(element, true);
            element.AddHandler(DragDrop.DragOverEvent, OnDragOver);
            element.AddHandler(DragDrop.DropEvent, OnDrop);
        }

        private void OnDragOver(object? sender, DragEventArgs e)
        {
            try
            {
                // 检查是否包含文件
                if (e.Data.Contains(DataFormats.Files))
                {
                    e.DragEffects = DragDropEffects.Copy;
                    e.Handled = true;

                    // 可以在这里添加视觉反馈
                    if (sender is Control control)
                    {
                        // 添加拖拽悬停效果
                        control.Opacity = 0.8;
                    }
                }
                else
                {
                    e.DragEffects = DragDropEffects.None;
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileUploadView", $"❌ 拖拽悬停处理失败: {ex.Message}");
            }
        }

        private async void OnDrop(object? sender, DragEventArgs e)
        {
            try
            {
                // 恢复透明度
                if (sender is Control control)
                {
                    control.Opacity = 1.0;
                }

                if (e.Data.Contains(DataFormats.Files))
                {
                    var files = e.Data.GetFiles();
                    if (files != null)
                    {
                        var filePaths = files.Select(f => f.Path.LocalPath).ToArray();
                        Utils.Logger.Info("FileUploadView", $"📁 拖拽文件: {filePaths.Length} 个");

                        // 调用ViewModel处理文件
                        if (_viewModel != null)
                        {
                            await _viewModel.HandleDroppedFilesAsync(filePaths);
                        }
                    }
                }

                e.Handled = true;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileUploadView", $"❌ 拖拽放置处理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新转换进度
        /// </summary>
        public void UpdateConversionProgress(string taskId, double progress, string status, double? fps = null, double? eta = null)
        {
            try
            {
                // 调用ViewModel更新进度
                _viewModel?.UpdateConversionProgress(taskId, progress, status, fps, eta);
                Utils.Logger.Debug("FileUploadView", $"📊 更新转换进度: {taskId} - {progress}%");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileUploadView", $"❌ 更新转换进度失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从转换设置更新目标信息 - 与原项目逻辑一致
        /// </summary>
        public void UpdateTargetInfoFromSettings()
        {
            try
            {
                // 这里可以调用ViewModel的方法来更新目标信息
                // _viewModel?.UpdateTargetInfoFromSettings();
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileUploadView", $"❌ 从转换设置更新目标信息失败: {ex.Message}");
            }
        }
    }
}
