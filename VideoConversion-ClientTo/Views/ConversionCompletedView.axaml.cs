using System;
using Avalonia.Controls;
using VideoConversion_ClientTo.Presentation.ViewModels;
using VideoConversion_ClientTo.Infrastructure;

namespace VideoConversion_ClientTo.Views
{
    public partial class ConversionCompletedView : UserControl
    {
        /// <summary>
        /// 请求导航到上传页面事件
        /// </summary>
        public event EventHandler? NavigateToUploadRequested;

        public ConversionCompletedView()
        {
            InitializeComponent();

            // 设置DataContext
            if (ServiceLocator.IsServiceRegistered<ConversionCompletedViewModel>())
            {
                DataContext = ServiceLocator.GetService<ConversionCompletedViewModel>();
            }
            else
            {
                // 如果服务未注册，创建一个临时实例
                DataContext = new ConversionCompletedViewModel();
            }
        }

        /// <summary>
        /// 触发导航到上传页面事件
        /// </summary>
        public void RequestNavigateToUpload()
        {
            NavigateToUploadRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
