using System;
using Microsoft.Extensions.DependencyInjection;
using VideoConversion_ClientTo.Application.Interfaces;

namespace VideoConversion_ClientTo.Infrastructure
{
    /// <summary>
    /// STEP-8: 简化的服务定位器
    /// 职责: 在不支持依赖注入的地方提供服务访问
    /// </summary>
    public static class ServiceLocator
    {
        private static IServiceProvider? _serviceProvider;
        private static bool _isInitialized = false;

        /// <summary>
        /// 初始化服务定位器
        /// </summary>
        public static void Initialize(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _isInitialized = true;
            Utils.Logger.Info("ServiceLocator", "✅ 服务定位器初始化完成");
        }

        /// <summary>
        /// 获取服务实例
        /// </summary>
        public static T GetService<T>() where T : class
        {
            if (!_isInitialized || _serviceProvider == null)
            {
                throw new InvalidOperationException("服务定位器未初始化，请先调用 Initialize 方法");
            }

            try
            {
                var service = _serviceProvider.GetService<T>();
                if (service == null)
                {
                    throw new InvalidOperationException($"服务 {typeof(T).Name} 未注册");
                }
                return service;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ServiceLocator", $"❌ 获取服务失败 {typeof(T).Name}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 获取必需的服务实例
        /// </summary>
        public static T GetRequiredService<T>() where T : class
        {
            if (!_isInitialized || _serviceProvider == null)
            {
                throw new InvalidOperationException("服务定位器未初始化，请先调用 Initialize 方法");
            }

            try
            {
                return _serviceProvider.GetRequiredService<T>();
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ServiceLocator", $"❌ 获取必需服务失败 {typeof(T).Name}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 创建服务作用域
        /// </summary>
        public static IServiceScope CreateScope()
        {
            if (!_isInitialized || _serviceProvider == null)
            {
                throw new InvalidOperationException("服务定位器未初始化，请先调用 Initialize 方法");
            }

            return _serviceProvider.CreateScope();
        }

        /// <summary>
        /// 检查服务是否已注册
        /// </summary>
        public static bool IsServiceRegistered<T>()
        {
            if (!_isInitialized || _serviceProvider == null)
            {
                return false;
            }

            try
            {
                return _serviceProvider.GetService<T>() != null;
            }
            catch
            {
                return false;
            }
        }

        #region 便捷方法

        /// <summary>
        /// 获取转换任务服务
        /// </summary>
        public static IConversionTaskService GetConversionTaskService()
        {
            return GetRequiredService<IConversionTaskService>();
        }

        /// <summary>
        /// 获取API客户端
        /// </summary>
        public static IApiClient GetApiClient()
        {
            return GetRequiredService<IApiClient>();
        }

        /// <summary>
        /// 获取SignalR客户端
        /// </summary>
        public static ISignalRClient GetSignalRClient()
        {
            return GetRequiredService<ISignalRClient>();
        }

        #endregion
    }
}
