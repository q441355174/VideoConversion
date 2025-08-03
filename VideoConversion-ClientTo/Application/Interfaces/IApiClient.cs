using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VideoConversion_ClientTo.Application.DTOs;

namespace VideoConversion_ClientTo.Application.Interfaces
{
    /// <summary>
    /// STEP-5: API客户端接口
    /// 职责: 定义与服务端通信的操作
    /// </summary>
    public interface IApiClient
    {
        #region 基础属性

        /// <summary>
        /// 服务器基础URL
        /// </summary>
        string? BaseUrl { get; }

        #endregion

        #region 连接测试

        /// <summary>
        /// 测试服务器连接
        /// </summary>
        Task<bool> TestConnectionAsync();

        #endregion

        #region 基础HTTP操作

        /// <summary>
        /// GET请求
        /// </summary>
        Task<ApiResponseDto<T>> GetAsync<T>(string endpoint);

        /// <summary>
        /// POST请求
        /// </summary>
        Task<ApiResponseDto<T>> PostAsync<T>(string endpoint, object? data = null);

        /// <summary>
        /// PUT请求
        /// </summary>
        Task<ApiResponseDto<T>> PutAsync<T>(string endpoint, object? data = null);

        /// <summary>
        /// DELETE请求
        /// </summary>
        Task<ApiResponseDto<T>> DeleteAsync<T>(string endpoint);

        #endregion

        #region 任务相关API

        /// <summary>
        /// 获取活跃任务列表
        /// </summary>
        Task<ApiResponseDto<List<ConversionTaskDto>>> GetActiveTasksAsync();

        /// <summary>
        /// 获取已完成的任务列表
        /// </summary>
        Task<ApiResponseDto<List<ConversionTaskDto>>> GetCompletedTasksAsync(int page = 1, int pageSize = 50);

        /// <summary>
        /// 获取任务详情
        /// </summary>
        Task<ApiResponseDto<ConversionTaskDto>> GetTaskAsync(string taskId);

        /// <summary>
        /// 开始转换任务
        /// </summary>
        Task<ApiResponseDto<StartConversionResponseDto>> StartConversionAsync(StartConversionRequestDto request);

        /// <summary>
        /// 取消任务
        /// </summary>
        Task<ApiResponseDto<object>> CancelTaskAsync(string taskId);

        /// <summary>
        /// 删除任务
        /// </summary>
        Task<ApiResponseDto<object>> DeleteTaskAsync(string taskId);

        #endregion

        #region 文件操作API

        /// <summary>
        /// 下载文件
        /// </summary>
        Task<ApiResponseDto<string>> DownloadFileAsync(string taskId);

        /// <summary>
        /// 检查磁盘空间
        /// </summary>
        Task<ApiResponseDto<SpaceCheckResponseDto>> CheckSpaceAsync(long requiredBytes);

        /// <summary>
        /// 获取磁盘空间信息
        /// </summary>
        Task<ApiResponseDto<DiskSpaceDto>> GetDiskSpaceAsync();

        #endregion

        #region 配置

        /// <summary>
        /// 设置基础URL
        /// </summary>
        void SetBaseUrl(string baseUrl);

        /// <summary>
        /// 设置超时时间
        /// </summary>
        void SetTimeout(TimeSpan timeout);

        /// <summary>
        /// 设置请求头
        /// </summary>
        void SetHeader(string name, string value);

        #endregion
    }

    /// <summary>
    /// STEP-5: 实时通信客户端接口
    /// 职责: 定义SignalR实时通信操作
    /// </summary>
    public interface ISignalRClient
    {
        #region 连接管理

        /// <summary>
        /// 连接到服务器
        /// </summary>
        Task<bool> ConnectAsync();

        /// <summary>
        /// 断开连接
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// 检查连接状态
        /// </summary>
        bool IsConnected { get; }

        #endregion

        #region 事件订阅

        /// <summary>
        /// 任务进度更新事件
        /// </summary>
        event EventHandler<ConversionProgressDto>? TaskProgressUpdated;

        /// <summary>
        /// 任务状态更新事件
        /// </summary>
        event EventHandler<TaskStatusUpdateDto>? TaskStatusUpdated;

        /// <summary>
        /// 任务完成事件
        /// </summary>
        event EventHandler<TaskCompletedDto>? TaskCompleted;

        /// <summary>
        /// 任务删除事件
        /// </summary>
        event EventHandler<string>? TaskDeleted;

        /// <summary>
        /// 磁盘空间状态更新事件
        /// </summary>
        event EventHandler<DiskSpaceStatusDto>? DiskSpaceStatusUpdated;

        /// <summary>
        /// 磁盘空间更新事件
        /// </summary>
        event EventHandler<DiskSpaceDto>? DiskSpaceUpdated;

        #endregion

        #region 群组管理

        /// <summary>
        /// 加入任务群组
        /// </summary>
        Task JoinTaskGroupAsync(string taskId);

        /// <summary>
        /// 离开任务群组
        /// </summary>
        Task LeaveTaskGroupAsync(string taskId);

        /// <summary>
        /// 加入用户群组
        /// </summary>
        Task JoinUserGroupAsync(string userId);

        /// <summary>
        /// 加入空间监控群组
        /// </summary>
        Task JoinSpaceMonitoringAsync();

        #endregion

        #region 连接事件

        /// <summary>
        /// 连接成功事件
        /// </summary>
        event EventHandler? Connected;

        /// <summary>
        /// 连接断开事件
        /// </summary>
        event EventHandler<string>? Disconnected;

        /// <summary>
        /// 重连事件
        /// </summary>
        event EventHandler? Reconnecting;

        /// <summary>
        /// 重连成功事件
        /// </summary>
        event EventHandler? Reconnected;

        #endregion
    }
}
