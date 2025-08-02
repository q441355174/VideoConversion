using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using VideoConversion_ClientTo.Application.Interfaces;
using VideoConversion_ClientTo.Infrastructure.Services;
using VideoConversion_ClientTo.Infrastructure.Data;
using VideoConversion_ClientTo.Presentation.ViewModels;

namespace VideoConversion_ClientTo.Infrastructure
{
    /// <summary>
    /// STEP-8: 依赖注入配置扩展
    /// 职责: 简化的服务注册配置
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 添加视频转换客户端服务
        /// </summary>
        public static IServiceCollection AddVideoConversionServices(this IServiceCollection services)
        {
            // 注册应用服务
            services.AddScoped<IConversionTaskService, ConversionTaskService>();
            services.AddSingleton<IApiClient, ApiClientService>(); // 🔑 改为单例以支持ChunkedUploadService实时控制
            services.AddScoped<ISignalRClient, SignalRClientService>();

            // 注册基础设施服务
            services.AddScoped<IFileDialogService, FileDialogService>();
            services.AddScoped<IFilePreprocessorService, FilePreprocessorService>();
            services.AddScoped<IMessageBoxService, MessageBoxService>();
            services.AddScoped<IDatabaseService, DatabaseService>();

            // 注册ViewModels
            services.AddTransient<MainWindowViewModel>();
            services.AddTransient<FileUploadViewModel>();
            services.AddTransient<ConversionCompletedViewModel>();
            services.AddTransient<ConversionSettingsViewModel>();
            services.AddTransient<ServerStatusViewModel>();

            Utils.Logger.Info("ServiceCollectionExtensions", "✅ 视频转换服务已注册");
            return services;
        }

        /// <summary>
        /// 添加HTTP客户端服务
        /// </summary>
        public static IServiceCollection AddHttpClientServices(this IServiceCollection services)
        {
            services.AddHttpClient("VideoConversionApi", client =>
            {
                client.Timeout = TimeSpan.FromMinutes(30);
                client.DefaultRequestHeaders.Add("User-Agent", "VideoConversion-ClientTo/1.0");
            });

            Utils.Logger.Info("ServiceCollectionExtensions", "✅ HTTP客户端服务已注册");
            return services;
        }

        /// <summary>
        /// 添加数据库服务
        /// </summary>
        public static IServiceCollection AddDatabaseServices(this IServiceCollection services)
        {
            // 配置SQLite数据库
            services.AddDbContext<LocalDbContext>(options =>
            {
                var appDataPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
                var appFolder = System.IO.Path.Combine(appDataPath, "VideoConversion-ClientTo");

                if (!System.IO.Directory.Exists(appFolder))
                {
                    System.IO.Directory.CreateDirectory(appFolder);
                }

                var dbPath = System.IO.Path.Combine(appFolder, "VideoConversion.db");
                options.UseSqlite($"Data Source={dbPath}");
            });

            Utils.Logger.Info("ServiceCollectionExtensions", "✅ 数据库服务已注册");
            return services;
        }

        /// <summary>
        /// 添加配置服务
        /// </summary>
        public static IServiceCollection AddConfigurationServices(this IServiceCollection services)
        {
            // 这里可以添加配置相关的服务
            Utils.Logger.Info("ServiceCollectionExtensions", "✅ 配置服务已注册");
            return services;
        }
    }
}
