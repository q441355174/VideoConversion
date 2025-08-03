using Avalonia.Controls;
using Avalonia.Interactivity;
using VideoConversion_ClientTo.Presentation.ViewModels.Dialogs;

namespace VideoConversion_ClientTo.Presentation.Views.Dialogs
{
    /// <summary>
    /// 磁盘空间配置对话框
    /// </summary>
    public partial class DiskSpaceConfigDialog : Window
    {
        public DiskSpaceConfigDialog()
        {
            InitializeComponent();
        }

        public DiskSpaceConfigDialog(DiskSpaceConfigViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            
            // 设置对话框结果处理
            if (DataContext is DiskSpaceConfigViewModel vm)
            {
                vm.DialogResult += OnDialogResult;
            }
        }

        private void OnDialogResult(bool? result)
        {
            Close(result);
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            // 清理事件订阅
            if (DataContext is DiskSpaceConfigViewModel vm)
            {
                vm.DialogResult -= OnDialogResult;
            }
            
            base.OnUnloaded(e);
        }
    }
}
