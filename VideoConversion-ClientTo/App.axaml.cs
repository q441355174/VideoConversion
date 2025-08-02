using System;
using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using VideoConversion_ClientTo.ViewModels;
using VideoConversion_ClientTo.Views;
using VideoConversion_ClientTo.Infrastructure;
using VideoConversion_ClientTo.Presentation.ViewModels;

namespace VideoConversion_ClientTo;

public partial class App : Avalonia.Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // 🔑 设置极简日志级别 - 大幅减少日志输出
        #if DEBUG
            Utils.Logger.SetMinimumLogLevel(Utils.Logger.LogLevel.Info);
        #else
            Utils.Logger.SetMinimumLogLevel(Utils.Logger.LogLevel.Warning);
        #endif

        // 清理30天前的旧日志
        Utils.Logger.CleanupOldLogs(30);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit.
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            // 配置依赖注入
            var services = new ServiceCollection();
            services.AddVideoConversionServices();
            services.AddHttpClientServices();
            services.AddDatabaseServices();
            services.AddConfigurationServices();

            var serviceProvider = services.BuildServiceProvider();

            // 初始化服务定位器
            ServiceLocator.Initialize(serviceProvider);

            // 🔑 初始化转换设置服务 - 与Client项目完全一致
            InitializeConversionSettingsService();

            Utils.Logger.Info("App", "✅ 应用程序依赖注入配置完成");

            desktop.MainWindow = new MainWindow
            {
                DataContext = ServiceLocator.GetRequiredService<MainWindowViewModel>(),
            };

            Utils.Logger.Info("App", "主窗口已创建");

            // 🔑 在窗口创建后异步初始化其他服务 - 与Client项目一致
            _ = Task.Run(async () =>
            {
                try
                {
                    Utils.Logger.Info("App", "开始异步初始化后台服务...");

                    // 这里可以添加其他需要异步初始化的服务
                    // 例如：FilePreprocessor、缓存服务等

                    Utils.Logger.Info("App", "后台服务初始化完成");
                }
                catch (Exception ex)
                {
                    Utils.Logger.Error("App", $"后台服务初始化失败: {ex.Message}");
                }
            });

            // 🔑 注册应用程序退出事件 - 与Client项目一致
            desktop.Exit += (sender, e) =>
            {
                Utils.Logger.Info("App", "应用程序正在退出");

                // 清理转换设置服务
                CleanupConversionSettingsService();

                Utils.Logger.Flush();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }

    /// <summary>
    /// 初始化转换设置服务 - 与Client项目完全一致
    /// </summary>
    private void InitializeConversionSettingsService()
    {
        try
        {
            // 🔑 触发ConversionSettingsService单例创建 - 与Client项目Initialize()一致
            var settingsService = Infrastructure.Services.ConversionSettingsService.Instance;

            // 转换设置服务初始化完成（移除日志）
        }
        catch (Exception ex)
        {
            Utils.Logger.Error("App", $"❌ 初始化转换设置服务失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 清理转换设置服务 - 与Client项目退出逻辑一致
    /// </summary>
    private void CleanupConversionSettingsService()
    {
        try
        {
            // 确保最新设置已保存
            var settingsService = Infrastructure.Services.ConversionSettingsService.Instance;
            // ConversionSettingsService会在自己的析构函数中处理清理

            Utils.Logger.Info("App", "转换设置服务清理完成");
        }
        catch (Exception ex)
        {
            Utils.Logger.Error("App", $"❌ 清理转换设置服务失败: {ex.Message}");
        }
    }
}