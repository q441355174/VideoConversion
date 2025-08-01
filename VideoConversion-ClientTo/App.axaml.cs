using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
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

            Utils.Logger.Info("App", "✅ 应用程序依赖注入配置完成");

            desktop.MainWindow = new MainWindow
            {
                DataContext = ServiceLocator.GetRequiredService<MainWindowViewModel>(),
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
}