using System;
using System.Threading.Tasks;

namespace VideoConversion_Client.Utils
{
    /// <summary>
    /// 安全执行器 - 统一错误处理模式
    /// </summary>
    public static class SafeExecutor
    {
        /// <summary>
        /// 安全执行异步操作
        /// </summary>
        /// <typeparam name="T">返回类型</typeparam>
        /// <param name="operation">要执行的操作</param>
        /// <param name="operationName">操作名称（用于日志）</param>
        /// <param name="defaultValue">失败时的默认返回值</param>
        /// <param name="logError">是否记录错误日志</param>
        /// <returns>操作结果或默认值</returns>
        public static async Task<T> ExecuteAsync<T>(
            Func<Task<T>> operation,
            string operationName,
            T defaultValue = default(T),
            bool logError = true)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex)
            {
                if (logError)
                {
                    System.Diagnostics.Debug.WriteLine($"{operationName}失败: {ex.Message}");
                }
                return defaultValue;
            }
        }

        /// <summary>
        /// 安全执行同步操作
        /// </summary>
        /// <typeparam name="T">返回类型</typeparam>
        /// <param name="operation">要执行的操作</param>
        /// <param name="operationName">操作名称（用于日志）</param>
        /// <param name="defaultValue">失败时的默认返回值</param>
        /// <param name="logError">是否记录错误日志</param>
        /// <returns>操作结果或默认值</returns>
        public static T Execute<T>(
            Func<T> operation,
            string operationName,
            T defaultValue = default(T),
            bool logError = true)
        {
            try
            {
                return operation();
            }
            catch (Exception ex)
            {
                if (logError)
                {
                    System.Diagnostics.Debug.WriteLine($"{operationName}失败: {ex.Message}");
                }
                return defaultValue;
            }
        }

        /// <summary>
        /// 安全执行无返回值的异步操作
        /// </summary>
        /// <param name="operation">要执行的操作</param>
        /// <param name="operationName">操作名称（用于日志）</param>
        /// <param name="logError">是否记录错误日志</param>
        /// <returns>是否执行成功</returns>
        public static async Task<bool> ExecuteAsync(
            Func<Task> operation,
            string operationName,
            bool logError = true)
        {
            try
            {
                await operation();
                return true;
            }
            catch (Exception ex)
            {
                if (logError)
                {
                    System.Diagnostics.Debug.WriteLine($"{operationName}失败: {ex.Message}");
                }
                return false;
            }
        }

        /// <summary>
        /// 安全执行无返回值的同步操作
        /// </summary>
        /// <param name="operation">要执行的操作</param>
        /// <param name="operationName">操作名称（用于日志）</param>
        /// <param name="logError">是否记录错误日志</param>
        /// <returns>是否执行成功</returns>
        public static bool Execute(
            Action operation,
            string operationName,
            bool logError = true)
        {
            try
            {
                operation();
                return true;
            }
            catch (Exception ex)
            {
                if (logError)
                {
                    System.Diagnostics.Debug.WriteLine($"{operationName}失败: {ex.Message}");
                }
                return false;
            }
        }

        /// <summary>
        /// 安全执行操作并返回结果包装
        /// </summary>
        /// <typeparam name="T">返回类型</typeparam>
        /// <param name="operation">要执行的操作</param>
        /// <param name="operationName">操作名称</param>
        /// <returns>操作结果包装</returns>
        public static async Task<OperationResult<T>> ExecuteWithResultAsync<T>(
            Func<Task<T>> operation,
            string operationName)
        {
            try
            {
                var result = await operation();
                return OperationResult<T>.Success(result);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"{operationName}失败: {ex.Message}");
                return OperationResult<T>.Failure(ex.Message);
            }
        }

        /// <summary>
        /// 安全执行操作并返回结果包装（同步版本）
        /// </summary>
        /// <typeparam name="T">返回类型</typeparam>
        /// <param name="operation">要执行的操作</param>
        /// <param name="operationName">操作名称</param>
        /// <returns>操作结果包装</returns>
        public static OperationResult<T> ExecuteWithResult<T>(
            Func<T> operation,
            string operationName)
        {
            try
            {
                var result = operation();
                return OperationResult<T>.Success(result);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"{operationName}失败: {ex.Message}");
                return OperationResult<T>.Failure(ex.Message);
            }
        }

        /// <summary>
        /// 安全执行操作并返回简单结果
        /// </summary>
        /// <param name="operation">要执行的操作</param>
        /// <param name="operationName">操作名称</param>
        /// <returns>操作结果</returns>
        public static async Task<OperationResult> ExecuteWithResultAsync(
            Func<Task> operation,
            string operationName)
        {
            try
            {
                await operation();
                return OperationResult.Success();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"{operationName}失败: {ex.Message}");
                return OperationResult.Failure(ex.Message);
            }
        }

        /// <summary>
        /// 安全执行操作并返回简单结果（同步版本）
        /// </summary>
        /// <param name="operation">要执行的操作</param>
        /// <param name="operationName">操作名称</param>
        /// <returns>操作结果</returns>
        public static OperationResult ExecuteWithResult(
            Action operation,
            string operationName)
        {
            try
            {
                operation();
                return OperationResult.Success();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"{operationName}失败: {ex.Message}");
                return OperationResult.Failure(ex.Message);
            }
        }
    }

    /// <summary>
    /// 操作结果包装类
    /// </summary>
    /// <typeparam name="T">结果类型</typeparam>
    public class OperationResult<T>
    {
        public bool IsSuccess { get; private set; }
        public T? Value { get; private set; }
        public string? ErrorMessage { get; private set; }

        private OperationResult(bool isSuccess, T? value, string? errorMessage)
        {
            IsSuccess = isSuccess;
            Value = value;
            ErrorMessage = errorMessage;
        }

        public static OperationResult<T> Success(T value)
        {
            return new OperationResult<T>(true, value, null);
        }

        public static OperationResult<T> Failure(string errorMessage)
        {
            return new OperationResult<T>(false, default(T), errorMessage);
        }
    }

    /// <summary>
    /// 简单操作结果包装类
    /// </summary>
    public class OperationResult
    {
        public bool IsSuccess { get; private set; }
        public string? ErrorMessage { get; private set; }

        private OperationResult(bool isSuccess, string? errorMessage)
        {
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
        }

        public static OperationResult Success()
        {
            return new OperationResult(true, null);
        }

        public static OperationResult Failure(string errorMessage)
        {
            return new OperationResult(false, errorMessage);
        }
    }
}
