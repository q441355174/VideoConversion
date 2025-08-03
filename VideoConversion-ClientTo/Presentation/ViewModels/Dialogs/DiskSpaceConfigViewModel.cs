using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using VideoConversion_ClientTo.Application.DTOs;
using VideoConversion_ClientTo.Infrastructure;
using VideoConversion_ClientTo.Infrastructure.Services;
using VideoConversion_ClientTo.ViewModels;

namespace VideoConversion_ClientTo.Presentation.ViewModels.Dialogs
{
    /// <summary>
    /// 磁盘空间配置对话框ViewModel
    /// </summary>
    public partial class DiskSpaceConfigViewModel : ViewModelBase
    {
        #region 私有字段

        private readonly ApiService _apiService;
        private readonly DiskSpaceConfigDto _originalConfig;

        #endregion

        #region 可观察属性

        // 空间配置
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(EffectiveSpaceText))]
        [NotifyPropertyChangedFor(nameof(ConfigWarningText))]
        [NotifyPropertyChangedFor(nameof(HasConfigWarning))]
        private bool _enableSpaceLimit = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FormattedMaxSpace))]
        [NotifyPropertyChangedFor(nameof(EffectiveSpaceText))]
        [NotifyPropertyChangedFor(nameof(ConfigWarningText))]
        [NotifyPropertyChangedFor(nameof(HasConfigWarning))]
        private double _maxSpaceGB = 100;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FormattedReservedSpace))]
        [NotifyPropertyChangedFor(nameof(EffectiveSpaceText))]
        [NotifyPropertyChangedFor(nameof(ConfigWarningText))]
        [NotifyPropertyChangedFor(nameof(HasConfigWarning))]
        private double _reservedSpaceGB = 5;

        // 自动清理
        [ObservableProperty]
        private bool _autoCleanup = false;

        [ObservableProperty]
        private bool _cleanupDownloaded = true;

        [ObservableProperty]
        private bool _cleanupTemp = true;

        // 磁盘状态
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FormattedAvailableSpace))]
        [NotifyPropertyChangedFor(nameof(DiskUsagePercentage))]
        [NotifyPropertyChangedFor(nameof(FormattedUsagePercentage))]
        private long _availableSpace = 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FormattedTotalSpace))]
        [NotifyPropertyChangedFor(nameof(DiskUsagePercentage))]
        [NotifyPropertyChangedFor(nameof(FormattedUsagePercentage))]
        [NotifyPropertyChangedFor(nameof(ConfigWarningText))]
        [NotifyPropertyChangedFor(nameof(HasConfigWarning))]
        private long _totalSpace = 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FormattedUsedSpace))]
        [NotifyPropertyChangedFor(nameof(DiskUsagePercentage))]
        [NotifyPropertyChangedFor(nameof(FormattedUsagePercentage))]
        private long _usedSpace = 0;

        #endregion

        #region 计算属性

        /// <summary>
        /// 格式化可用空间
        /// </summary>
        public string FormattedAvailableSpace => FormatFileSize(AvailableSpace);

        /// <summary>
        /// 格式化总空间
        /// </summary>
        public string FormattedTotalSpace => FormatFileSize(TotalSpace);

        /// <summary>
        /// 磁盘使用百分比
        /// </summary>
        public double DiskUsagePercentage => 
            TotalSpace > 0 ? (double)(TotalSpace - AvailableSpace) / TotalSpace * 100 : 0;

        /// <summary>
        /// 格式化使用百分比
        /// </summary>
        public string FormattedUsagePercentage => $"{DiskUsagePercentage:F1}%";

        /// <summary>
        /// 格式化的已用空间
        /// </summary>
        public string FormattedUsedSpace => FormatFileSize(UsedSpace);

        /// <summary>
        /// 格式化的最大空间
        /// </summary>
        public string FormattedMaxSpace => $"{MaxSpaceGB:F0} GB";

        /// <summary>
        /// 格式化的保留空间
        /// </summary>
        public string FormattedReservedSpace => $"{ReservedSpaceGB:F0} GB";

        /// <summary>
        /// 效果空间文本
        /// </summary>
        public string EffectiveSpaceText
        {
            get
            {
                if (!EnableSpaceLimit) return "未启用空间限制";
                var effectiveSpace = MaxSpaceGB - ReservedSpaceGB;
                return $"有效可用空间: {effectiveSpace:F0} GB (最大 {MaxSpaceGB:F0} GB - 保留 {ReservedSpaceGB:F0} GB)";
            }
        }

        /// <summary>
        /// 配置警告文本
        /// </summary>
        public string ConfigWarningText
        {
            get
            {
                if (!EnableSpaceLimit) return "";

                var effectiveSpace = MaxSpaceGB - ReservedSpaceGB;
                var totalSpaceGB = TotalSpace / (1024.0 * 1024 * 1024);
                var usedSpaceGB = UsedSpace / (1024.0 * 1024 * 1024);

                // 检查配置合理性（与Client项目一致）
                if (ReservedSpaceGB >= MaxSpaceGB)
                    return "⚠️ 保留空间不能大于或等于最大总空间";
                else if (effectiveSpace < 10)
                    return "⚠️ 实际可用空间过小，建议至少保留10GB";
                else if (TotalSpace > 0 && effectiveSpace < usedSpaceGB)
                    return $"⚠️ 实际可用空间({effectiveSpace:F0}GB)小于当前已使用空间({usedSpaceGB:F1}GB)";
                else if (MaxSpaceGB > totalSpaceGB)
                    return "⚠️ 最大空间超过了磁盘总容量";

                return "";
            }
        }

        /// <summary>
        /// 是否有配置警告
        /// </summary>
        public bool HasConfigWarning => !string.IsNullOrEmpty(ConfigWarningText);

        #endregion

        #region 事件

        /// <summary>
        /// 对话框结果事件
        /// </summary>
        public event Action<bool?>? DialogResult;

        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数
        /// </summary>
        public DiskSpaceConfigViewModel(DiskSpaceConfigDto config)
        {
            _apiService = ServiceLocator.GetRequiredService<ApiService>();
            _originalConfig = config;

            // 初始化配置
            LoadConfiguration(config);

            // 加载当前磁盘状态和配置
            _ = LoadCurrentDataAsync();

            Utils.Logger.Info("DiskSpaceConfigViewModel", "✅ 磁盘空间配置ViewModel已初始化");
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 加载配置
        /// </summary>
        private void LoadConfiguration(DiskSpaceConfigDto config)
        {
            ReservedSpaceGB = config.MinFreeSpace / (1024.0 * 1024.0 * 1024.0); // 转换为GB
            AutoCleanup = config.AutoCleanup;
            // 其他配置项根据需要添加
        }

        /// <summary>
        /// 加载当前数据（磁盘状态和配置）
        /// </summary>
        private async Task LoadCurrentDataAsync()
        {
            try
            {
                if (_apiService == null)
                {
                    Utils.Logger.Warning("DiskSpaceConfigViewModel", "⚠️ ApiService为null，无法加载数据");
                    return;
                }

                // 并行加载磁盘状态和服务器配置
                var diskTask = LoadDiskStatusAsync();
                var configTask = LoadServerConfigAsync();

                await Task.WhenAll(diskTask, configTask);

                Utils.Logger.Info("DiskSpaceConfigViewModel", "✅ 当前数据加载完成");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("DiskSpaceConfigViewModel", $"❌ 加载当前数据失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载服务器配置
        /// </summary>
        private async Task LoadServerConfigAsync()
        {
            try
            {
                if (_apiService == null) return;

                Utils.Logger.Info("DiskSpaceConfigViewModel", "🔍 加载服务器磁盘配置");

                // 🔧 直接调用服务器API获取原始配置数据
                var url = $"{_apiService.BaseUrl}/api/diskspace/config";
                var httpClient = new HttpClient();
                var response = await httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Utils.Logger.Debug("DiskSpaceConfigViewModel", $"服务器返回配置: {content}");

                    // 解析服务器返回的原始格式
                    using var document = JsonDocument.Parse(content);
                    var root = document.RootElement;

                    if (root.TryGetProperty("success", out var successProp) && successProp.GetBoolean() &&
                        root.TryGetProperty("data", out var dataProp))
                    {
                        // 🔧 正确解析服务器返回的配置格式
                        var maxTotalSpaceGB = dataProp.TryGetProperty("maxTotalSpaceGB", out var maxProp) ? maxProp.GetDouble() : 100.0;
                        var reservedSpaceGB = dataProp.TryGetProperty("reservedSpaceGB", out var reservedProp) ? reservedProp.GetDouble() : 5.0;
                        var isEnabled = dataProp.TryGetProperty("isEnabled", out var enabledProp) && enabledProp.GetBoolean();

                        // 更新界面属性
                        MaxSpaceGB = maxTotalSpaceGB;
                        ReservedSpaceGB = reservedSpaceGB;
                        EnableSpaceLimit = isEnabled;
                        AutoCleanup = isEnabled; // 暂时使用相同的值

                        Utils.Logger.Info("DiskSpaceConfigViewModel", $"✅ 服务器配置加载成功: 最大{MaxSpaceGB}GB, 保留{ReservedSpaceGB}GB, 启用{EnableSpaceLimit}");
                    }
                    else
                    {
                        Utils.Logger.Warning("DiskSpaceConfigViewModel", "⚠️ 服务器返回格式错误");
                        SetDefaultValues();
                    }
                }
                else
                {
                    Utils.Logger.Warning("DiskSpaceConfigViewModel", $"⚠️ 获取服务器配置失败: {response.StatusCode}");
                    SetDefaultValues();
                }

                httpClient.Dispose();
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("DiskSpaceConfigViewModel", $"❌ 加载服务器配置失败: {ex.Message}");
                // 使用默认值
                SetDefaultValues();
            }
        }

        /// <summary>
        /// 设置默认值
        /// </summary>
        private void SetDefaultValues()
        {
            MaxSpaceGB = 100;
            ReservedSpaceGB = 5;
            EnableSpaceLimit = true;
            AutoCleanup = false;
            CleanupDownloaded = true;
            CleanupTemp = true;
        }

        /// <summary>
        /// 加载磁盘状态
        /// </summary>
        private async Task LoadDiskStatusAsync()
        {
            try
            {
                var response = await _apiService.GetSystemStatusAsync();
                if (response.Success && response.Data != null)
                {
                    AvailableSpace = response.Data.AvailableDiskSpace;
                    TotalSpace = response.Data.TotalDiskSpace;
                    UsedSpace = TotalSpace - AvailableSpace; // 计算已用空间

                    Utils.Logger.Info("DiskSpaceConfigViewModel", $"✅ 磁盘状态加载成功: {FormattedUsedSpace}/{FormattedTotalSpace}");
                }
                else
                {
                    Utils.Logger.Warning("DiskSpaceConfigViewModel", $"⚠️ 获取磁盘状态失败: {response.Message}");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("DiskSpaceConfigViewModel", $"❌ 加载磁盘状态失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 格式化文件大小
        /// </summary>
        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        /// <summary>
        /// 创建配置DTO
        /// </summary>
        private DiskSpaceConfigDto CreateConfigDto()
        {
            return new DiskSpaceConfigDto
            {
                MinFreeSpace = (long)(ReservedSpaceGB * 1024 * 1024 * 1024), // 转换为字节
                AutoCleanup = AutoCleanup
            };
        }

        #endregion

        #region 命令

        /// <summary>
        /// 刷新命令
        /// </summary>
        [RelayCommand]
        private async Task RefreshAsync()
        {
            try
            {
                Utils.Logger.Info("DiskSpaceConfigViewModel", "🔄 刷新磁盘空间信息");
                await LoadDiskStatusAsync();
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("DiskSpaceConfigViewModel", $"❌ 刷新失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 手动清理命令
        /// </summary>
        [RelayCommand]
        private async Task ManualCleanupAsync()
        {
            try
            {
                Utils.Logger.Info("DiskSpaceConfigViewModel", "🧹 执行手动清理");
                // TODO: 实现手动清理功能
                Utils.Logger.Info("DiskSpaceConfigViewModel", "💡 手动清理功能待实现");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("DiskSpaceConfigViewModel", $"❌ 手动清理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 浏览清理路径命令
        /// </summary>
        [RelayCommand]
        private async Task BrowseCleanupPathAsync()
        {
            try
            {
                // TODO: 实现文件夹选择对话框
                Utils.Logger.Info("DiskSpaceConfigViewModel", "📁 浏览清理路径功能待实现");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("DiskSpaceConfigViewModel", $"❌ 浏览路径失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存命令
        /// </summary>
        [RelayCommand]
        private async Task SaveAsync()
        {
            try
            {
                Utils.Logger.Info("DiskSpaceConfigViewModel", "💾 保存磁盘空间配置");

                // 验证配置（与Client项目一致）
                if (ReservedSpaceGB >= MaxSpaceGB)
                {
                    Utils.Logger.Warning("DiskSpaceConfigViewModel", "⚠️ 保留空间不能大于或等于最大总空间");
                    return;
                }

                // 🔧 使用正确的API调用，与Client项目一致
                Utils.Logger.Info("DiskSpaceConfigViewModel", $"📤 保存配置: 最大{MaxSpaceGB}GB, 保留{ReservedSpaceGB}GB, 启用{EnableSpaceLimit}");

                var response = await _apiService.SetSpaceConfigAsync(MaxSpaceGB, ReservedSpaceGB, EnableSpaceLimit);

                if (response.Success)
                {
                    Utils.Logger.Info("DiskSpaceConfigViewModel", "✅ 磁盘空间配置保存成功");

                    // 🔧 保存成功后重新从服务器加载最新配置
                    Utils.Logger.Info("DiskSpaceConfigViewModel", "🔄 重新加载服务器配置以确保界面显示最新状态");
                    await LoadServerConfigAsync();

                    DialogResult?.Invoke(true);
                }
                else
                {
                    Utils.Logger.Warning("DiskSpaceConfigViewModel", $"⚠️ 保存配置失败: {response.Message}");
                    // TODO: 显示错误消息给用户
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("DiskSpaceConfigViewModel", $"❌ 保存配置异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 取消命令
        /// </summary>
        [RelayCommand]
        private void Cancel()
        {
            Utils.Logger.Info("DiskSpaceConfigViewModel", "🚫 取消磁盘空间配置");
            DialogResult?.Invoke(false);
        }

        #endregion
    }
}
