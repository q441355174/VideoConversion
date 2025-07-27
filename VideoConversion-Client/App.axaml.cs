using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace VideoConversion_Client
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            // 初始化日志系统
            Utils.Logger.Info("App", "应用程序启动");
            Utils.Logger.Info("App", $"日志目录: {Utils.Logger.GetLogDirectory()}");

            // 清理30天前的旧日志
            Utils.Logger.CleanupOldLogs(30);

            // 初始化转码设置服务
            Services.ConversionSettingsService.Initialize();
            Utils.Logger.Info("App", "转码设置服务已初始化");

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow();
                Utils.Logger.Info("App", "主窗口已创建");

                // 在窗口创建后异步初始化FilePreprocessor
                _ = Task.Run(async () =>
                {
                    try
                    {
                        Utils.Logger.Info("App", "开始初始化FilePreprocessor...");

                        // 初始化FilePreprocessor
                        await Utils.FilePreprocessor.InitializeAsync();

                        Utils.Logger.Info("App", "FilePreprocessor初始化完成");
                    }
                    catch (Exception ex)
                    {
                        Utils.Logger.Error("App", "FilePreprocessor初始化失败", ex);
                    }
                });

                // 注册应用程序退出事件
                desktop.Exit += (sender, e) =>
                {
                    Utils.Logger.Info("App", "应用程序正在退出");
                    Utils.Logger.Flush();
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}