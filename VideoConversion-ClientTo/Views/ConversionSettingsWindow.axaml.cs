using System;
using Avalonia.Controls;
using VideoConversion_ClientTo.Presentation.ViewModels;
using VideoConversion_ClientTo.Infrastructure;

namespace VideoConversion_ClientTo.Views
{
    public partial class ConversionSettingsWindow : Window
    {
        /// <summary>
        /// 设置是否已更改
        /// </summary>
        public bool SettingsChanged { get; set; } = false;

        /// <summary>
        /// 设置对象
        /// </summary>
        public object? Settings { get; set; }

        public ConversionSettingsWindow()
        {
            InitializeComponent();
            
            // 设置DataContext
            try
            {
                if (ServiceLocator.IsServiceRegistered<ConversionSettingsViewModel>())
                {
                    DataContext = ServiceLocator.GetService<ConversionSettingsViewModel>();
                }
                else
                {
                    // 如果服务未注册，创建一个临时实例
                    DataContext = new ConversionSettingsViewModel();
                }
                
                Utils.Logger.Info("ConversionSettingsWindow", "✅ 转换设置窗口初始化完成");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionSettingsWindow", $"❌ 转换设置窗口初始化失败: {ex.Message}");
            }
        }
    }
}
