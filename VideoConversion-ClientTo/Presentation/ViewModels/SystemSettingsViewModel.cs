using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using VideoConversion_ClientTo.Infrastructure;
using VideoConversion_ClientTo.Infrastructure.Services;
using VideoConversion_ClientTo.Infrastructure.Data;
using VideoConversion_ClientTo.ViewModels;

namespace VideoConversion_ClientTo.Presentation.ViewModels
{
    /// <summary>
    /// 系统设置窗口ViewModel - 基于新框架的MVVM实现
    /// </summary>
    public partial class SystemSettingsViewModel : ViewModelBase
    {
        #region 私有字段

        // 🔑 现代架构：依赖注入的服务 - 与ClientTo架构一致
        private readonly ApiService _apiService;
        private readonly DatabaseService _databaseService;
        private readonly LocalDbContext _dbContext;

        #endregion

        #region 可观察属性

        [ObservableProperty]
        private string _serverAddress = "";

        [ObservableProperty]
        private int _maxConcurrentUploads = 3;

        [ObservableProperty]
        private int _maxConcurrentDownloads = 3;

        [ObservableProperty]
        private int _maxConcurrentChunks = 4;

        [ObservableProperty]
        private bool _autoStartConversion = false;

        [ObservableProperty]
        private bool _showNotifications = true;

        [ObservableProperty]
        private string _defaultOutputPath = "";

        [ObservableProperty]
        private bool _isTestingConnection = false;

        [ObservableProperty]
        private string _connectionStatus = "未测试";

        [ObservableProperty]
        private string _connectionStatusColor = "#808080";

        [ObservableProperty]
        private string _databasePath = "";

        [ObservableProperty]
        private string _databaseStatus = "未知";

        [ObservableProperty]
        private string _databaseSize = "";

        [ObservableProperty]
        private string _serverVersion = "未知";

        [ObservableProperty]
        private string _ffmpegVersion = "未知";

        [ObservableProperty]
        private string _hardwareAcceleration = "未知";

        [ObservableProperty]
        private string _uptime = "未知";

        [ObservableProperty]
        private bool _settingsChanged = false;

        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数 - 使用依赖注入 - 与ClientTo现代架构一致
        /// </summary>
        public SystemSettingsViewModel()
        {
            // 🔑 使用ServiceLocator获取服务 - 与ClientTo架构一致
            _apiService = ServiceLocator.GetRequiredService<ApiService>();
            _dbContext = ServiceLocator.GetRequiredService<LocalDbContext>();
            _databaseService = new DatabaseService(_dbContext);

            // 初始化默认值
            InitializeDefaultValues();

            // 异步加载设置
            _ = LoadSettingsAsync();

            Utils.Logger.Info("SystemSettingsViewModel", "✅ 系统设置ViewModel已初始化（现代架构）");
        }

        /// <summary>
        /// 初始化默认值
        /// </summary>
        private void InitializeDefaultValues()
        {
            ServerAddress = "http://localhost:5065";
            MaxConcurrentUploads = 3;
            MaxConcurrentDownloads = 3;
            MaxConcurrentChunks = 4;
            AutoStartConversion = false;
            ShowNotifications = true;
            DefaultOutputPath = "";

            ConnectionStatus = "未测试";
            ConnectionStatusColor = "#808080";
            DatabasePath = "VideoConversion.db";
            DatabaseStatus = "未知";
            DatabaseSize = "";
            ServerVersion = "未知";
            FfmpegVersion = "未知";
            HardwareAcceleration = "未知";
            Uptime = "未知";
        }

        #endregion

        #region 简化的方法实现

        /// <summary>
        /// 异步加载设置 - 从数据库加载真实设置 - 与Client项目一致
        /// </summary>
        private async Task LoadSettingsAsync()
        {
            try
            {
                Utils.Logger.Info("SystemSettingsViewModel", "📖 开始从数据库加载系统设置");

                // 🔑 从数据库加载设置 - 与Client项目SystemSettingsModel.LoadSettings()一致
                ServerAddress = await _databaseService.GetSettingAsync("ServerAddress") ?? "http://localhost:5065";

                var maxUploadsStr = await _databaseService.GetSettingAsync("MaxConcurrentUploads");
                MaxConcurrentUploads = int.TryParse(maxUploadsStr, out var maxUploads) ? maxUploads : 3;

                var maxDownloadsStr = await _databaseService.GetSettingAsync("MaxConcurrentDownloads");
                MaxConcurrentDownloads = int.TryParse(maxDownloadsStr, out var maxDownloads) ? maxDownloads : 3;

                var maxChunksStr = await _databaseService.GetSettingAsync("MaxConcurrentChunks");
                MaxConcurrentChunks = int.TryParse(maxChunksStr, out var maxChunks) ? maxChunks : 4;

                var autoStartStr = await _databaseService.GetSettingAsync("AutoStartConversion");
                AutoStartConversion = bool.TryParse(autoStartStr, out var autoStart) ? autoStart : false;

                var showNotificationsStr = await _databaseService.GetSettingAsync("ShowNotifications");
                ShowNotifications = bool.TryParse(showNotificationsStr, out var showNotifications) ? showNotifications : true;

                DefaultOutputPath = await _databaseService.GetSettingAsync("DefaultOutputPath") ?? "";

                // 🔑 加载数据库信息 - 与Client项目一致
                await LoadDatabaseInfoAsync();

                Utils.Logger.Info("SystemSettingsViewModel", "✅ 系统设置已从数据库加载完成");
                Utils.Logger.Debug("SystemSettingsViewModel", $"设置详情: 服务器={ServerAddress}, 上传并发={MaxConcurrentUploads}, 下载并发={MaxConcurrentDownloads}");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SystemSettingsViewModel", $"❌ 从数据库加载设置失败: {ex.Message}");

                // 加载失败时保持默认值
                Utils.Logger.Info("SystemSettingsViewModel", "🔄 使用默认设置");
            }
        }

        /// <summary>
        /// 加载数据库信息 - 与Client项目LoadDatabaseInfo()一致
        /// </summary>
        private async Task LoadDatabaseInfoAsync()
        {
            try
            {
                // 获取数据库路径
                DatabasePath = "VideoConversion.db"; // 简化实现

                // 检查数据库连接状态
                var canConnect = await _dbContext.Database.CanConnectAsync();
                DatabaseStatus = canConnect ? "连接正常" : "连接失败";

                // 获取数据库文件大小
                if (File.Exists(DatabasePath))
                {
                    var fileInfo = new FileInfo(DatabasePath);
                    DatabaseSize = $"({FormatFileSize(fileInfo.Length)})";
                }
                else
                {
                    DatabaseSize = "(文件不存在)";
                }

                Utils.Logger.Debug("SystemSettingsViewModel", $"数据库信息: 路径={DatabasePath}, 状态={DatabaseStatus}, 大小={DatabaseSize}");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SystemSettingsViewModel", $"❌ 加载数据库信息失败: {ex.Message}");
                DatabaseStatus = "获取失败";
                DatabaseSize = "";
            }
        }

        #endregion

        #region 命令实现

        /// <summary>
        /// 测试连接命令 - 使用真实的API服务 - 与Client项目TestConnectionBtn_Click一致
        /// </summary>
        [RelayCommand]
        private async Task TestConnectionAsync()
        {
            try
            {
                IsTestingConnection = true;
                ConnectionStatus = "正在测试连接...";
                ConnectionStatusColor = "#FFA500"; // Orange

                // 🔑 验证服务器地址格式 - 与Client项目IsValidServerAddress()一致
                if (string.IsNullOrWhiteSpace(ServerAddress))
                {
                    ConnectionStatus = "服务器地址不能为空";
                    ConnectionStatusColor = "#FF0000"; // Red
                    return;
                }

                if (!ServerAddress.StartsWith("http://") && !ServerAddress.StartsWith("https://"))
                {
                    ConnectionStatus = "服务器地址格式不正确";
                    ConnectionStatusColor = "#FF0000"; // Red
                    return;
                }

                Utils.Logger.Info("SystemSettingsViewModel", $"🔗 开始测试连接: {ServerAddress}");

                // 🔑 使用HTTP客户端进行连接测试 - 与Client项目一致
                using var httpClient = new System.Net.Http.HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(10);

                var response = await httpClient.GetAsync($"{ServerAddress}/api/health");
                var isConnected = response.IsSuccessStatusCode;

                if (isConnected)
                {
                    ConnectionStatus = "连接成功";
                    ConnectionStatusColor = "#008000"; // Green
                    Utils.Logger.Info("SystemSettingsViewModel", "✅ 服务器连接测试成功");
                }
                else
                {
                    ConnectionStatus = $"连接失败: HTTP {(int)response.StatusCode}";
                    ConnectionStatusColor = "#FF0000"; // Red
                    Utils.Logger.Warning("SystemSettingsViewModel", $"⚠️ 服务器连接失败: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                ConnectionStatus = $"连接错误: {ex.Message}";
                ConnectionStatusColor = "#FF0000"; // Red
                Utils.Logger.Error("SystemSettingsViewModel", $"❌ 连接测试异常: {ex.Message}");
            }
            finally
            {
                IsTestingConnection = false;
            }
        }

        /// <summary>
        /// 浏览输出路径命令 - 使用真实的文件夹选择对话框
        /// </summary>
        [RelayCommand]
        private async Task BrowseOutputPathAsync()
        {
            try
            {
                // 🔑 使用真实的文件夹选择对话框 - 与Client项目一致
                Utils.Logger.Info("SystemSettingsViewModel", "📁 打开文件夹选择对话框");

                // 获取当前活动窗口
                var mainWindow = App.Current?.ApplicationLifetime is
                    Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                    ? desktop.MainWindow : null;

                if (mainWindow == null)
                {
                    Utils.Logger.Warning("SystemSettingsViewModel", "⚠️ 无法获取主窗口");
                    return;
                }

                var topLevel = TopLevel.GetTopLevel(mainWindow);
                if (topLevel?.StorageProvider == null)
                {
                    Utils.Logger.Warning("SystemSettingsViewModel", "⚠️ 无法获取StorageProvider");
                    return;
                }

                var options = new Avalonia.Platform.Storage.FolderPickerOpenOptions
                {
                    Title = "选择默认输出文件夹",
                    AllowMultiple = false
                };

                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(options);

                if (folders.Count > 0)
                {
                    var selectedPath = folders[0].Path.LocalPath;
                    DefaultOutputPath = selectedPath;
                    MarkSettingsChanged();
                    Utils.Logger.Info("SystemSettingsViewModel", $"✅ 输出路径已更新: {selectedPath}");
                }
                else
                {
                    Utils.Logger.Info("SystemSettingsViewModel", "🚫 用户取消了文件夹选择");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SystemSettingsViewModel", $"❌ 选择输出路径失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 重置设置命令
        /// </summary>
        [RelayCommand]
        private async Task ResetSettingsAsync()
        {
            try
            {
                // 简化实现 - 重置为默认值
                await Task.Delay(100);

                ServerAddress = "http://localhost:5065";
                MaxConcurrentUploads = 3;
                MaxConcurrentDownloads = 3;
                MaxConcurrentChunks = 4;
                AutoStartConversion = false;
                ShowNotifications = true;
                DefaultOutputPath = "";

                MarkSettingsChanged();
                Utils.Logger.Info("SystemSettingsViewModel", "🔄 设置已重置为默认值");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SystemSettingsViewModel", $"❌ 重置设置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存设置命令 - 使用真实的数据库持久化 - 与Client项目SaveBtn_Click一致
        /// </summary>
        [RelayCommand]
        private async Task<bool> SaveSettingsAsync()
        {
            try
            {
                // 🔑 验证设置 - 与Client项目_settings.IsValidServerAddress()一致
                if (!IsValidServerAddress(ServerAddress))
                {
                    Utils.Logger.Warning("SystemSettingsViewModel", "⚠️ 服务器地址格式不正确");
                    return false;
                }

                if (MaxConcurrentUploads < 1 || MaxConcurrentUploads > 10)
                {
                    Utils.Logger.Warning("SystemSettingsViewModel", "⚠️ 并发上传数必须在1-10之间");
                    return false;
                }

                if (MaxConcurrentDownloads < 1 || MaxConcurrentDownloads > 10)
                {
                    Utils.Logger.Warning("SystemSettingsViewModel", "⚠️ 并发下载数必须在1-10之间");
                    return false;
                }

                if (MaxConcurrentChunks < 1 || MaxConcurrentChunks > 8)
                {
                    Utils.Logger.Warning("SystemSettingsViewModel", "⚠️ 分片并发数必须在1-8之间");
                    return false;
                }

                Utils.Logger.Info("SystemSettingsViewModel", "💾 开始保存系统设置到数据库");

                // 🔑 通过SystemSettingsService保存 - 与Client项目完全一致
                var newSettings = new Infrastructure.Services.SystemSettings
                {
                    ServerAddress = ServerAddress,
                    MaxConcurrentUploads = MaxConcurrentUploads,
                    MaxConcurrentDownloads = MaxConcurrentDownloads,
                    MaxConcurrentChunks = MaxConcurrentChunks
                };

                await Infrastructure.Services.SystemSettingsService.Instance.UpdateSettingsAsync(newSettings);

                // 保存其他设置到数据库
                await _databaseService.SetSettingAsync("AutoStartConversion", AutoStartConversion.ToString());
                await _databaseService.SetSettingAsync("ShowNotifications", ShowNotifications.ToString());
                await _databaseService.SetSettingAsync("DefaultOutputPath", DefaultOutputPath ?? "");

                SettingsChanged = true;
                Utils.Logger.Info("SystemSettingsViewModel", "✅ 系统设置已成功保存到数据库");
                Utils.Logger.Debug("SystemSettingsViewModel", $"保存的设置: 服务器={ServerAddress}, 上传并发={MaxConcurrentUploads}, 下载并发={MaxConcurrentDownloads}");

                return true;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SystemSettingsViewModel", $"❌ 保存设置失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 打开数据库文件夹命令
        /// </summary>
        [RelayCommand]
        private async Task OpenDatabaseFolderAsync()
        {
            try
            {
                await Task.Delay(100);
                Utils.Logger.Info("SystemSettingsViewModel", "📂 打开数据库文件夹（简化实现）");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SystemSettingsViewModel", $"❌ 打开数据库文件夹失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 备份数据库命令
        /// </summary>
        [RelayCommand]
        private async Task BackupDatabaseAsync()
        {
            try
            {
                await Task.Delay(100);
                Utils.Logger.Info("SystemSettingsViewModel", "💾 数据库备份（简化实现）");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SystemSettingsViewModel", $"❌ 备份数据库失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 恢复数据库命令
        /// </summary>
        [RelayCommand]
        private async Task RestoreDatabaseAsync()
        {
            try
            {
                await Task.Delay(100);
                Utils.Logger.Info("SystemSettingsViewModel", "🔄 数据库恢复（简化实现）");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SystemSettingsViewModel", $"❌ 恢复数据库失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 刷新系统信息命令
        /// </summary>
        [RelayCommand]
        private async Task RefreshSystemInfoAsync()
        {
            try
            {
                await Task.Delay(100);
                Utils.Logger.Info("SystemSettingsViewModel", "🔄 系统信息已刷新（简化实现）");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SystemSettingsViewModel", $"❌ 刷新系统信息失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 清理文件命令
        /// </summary>
        [RelayCommand]
        private async Task CleanupFilesAsync()
        {
            try
            {
                await Task.Delay(100);
                Utils.Logger.Info("SystemSettingsViewModel", "🧹 文件清理（简化实现）");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SystemSettingsViewModel", $"❌ 文件清理失败: {ex.Message}");
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 标记设置已更改
        /// </summary>
        private void MarkSettingsChanged()
        {
            SettingsChanged = true;
        }

        /// <summary>
        /// 验证服务器地址格式
        /// </summary>
        private bool IsValidServerAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return false;

            return Uri.TryCreate(address, UriKind.Absolute, out var uri) &&
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        #endregion

        #region 属性变化处理

        /// <summary>
        /// 属性变化时的处理
        /// </summary>
        partial void OnServerAddressChanged(string value)
        {
            MarkSettingsChanged();
        }

        partial void OnMaxConcurrentUploadsChanged(int value)
        {
            MarkSettingsChanged();
        }

        partial void OnMaxConcurrentDownloadsChanged(int value)
        {
            MarkSettingsChanged();
        }

        partial void OnAutoStartConversionChanged(bool value)
        {
            MarkSettingsChanged();
        }

        partial void OnShowNotificationsChanged(bool value)
        {
            MarkSettingsChanged();
        }

        partial void OnDefaultOutputPathChanged(string value)
        {
            MarkSettingsChanged();
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 格式化文件大小 - 与Client项目一致
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

        #endregion
    }
}
