using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using VideoConversion_ClientTo.Application.DTOs;
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
        private readonly IDatabaseService _databaseService;

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

        #region 服务器状态属性

        [ObservableProperty]
        private bool _isServerOnline = false;

        [ObservableProperty]
        private bool _canAccessServerFeatures = false;

        [ObservableProperty]
        private string _serverFeatureStatusText = "检查服务器连接状态...";

        [ObservableProperty]
        private long _availableDiskSpace = 0;

        [ObservableProperty]
        private long _totalDiskSpace = 0;

        [ObservableProperty]
        private int _activeTasks = 0;

        [ObservableProperty]
        private int _queuedTasks = 0;

        [ObservableProperty]
        private double _cpuUsage = 0;

        [ObservableProperty]
        private double _memoryUsage = 0;



        #region 诊断信息属性

        [ObservableProperty]
        private ObservableCollection<SystemDiagnosticDisplayDto> _recentDiagnostics = new();

        #endregion

        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数 - 使用依赖注入 - 与ClientTo现代架构一致
        /// </summary>
        public SystemSettingsViewModel()
        {
            try
            {
                Utils.Logger.Info("SystemSettingsViewModel", "🔄 开始初始化SystemSettingsViewModel");

                // 🔑 首先初始化默认值，确保属性不为null
                InitializeDefaultValues();
                Utils.Logger.Info("SystemSettingsViewModel", "✅ 默认值已初始化");

                // 🔑 尝试获取服务 - 与ClientTo架构一致
                try
                {
                    _apiService = ServiceLocator.GetRequiredService<ApiService>();
                    Utils.Logger.Info("SystemSettingsViewModel", "✅ ApiService已获取");
                }
                catch (Exception apiEx)
                {
                    Utils.Logger.Warning("SystemSettingsViewModel", $"⚠️ ApiService获取失败: {apiEx.Message}");
                    _apiService = null;
                }

                try
                {
                    _databaseService = ServiceLocator.GetRequiredService<IDatabaseService>();
                    Utils.Logger.Info("SystemSettingsViewModel", "✅ DatabaseService已获取");
                }
                catch (Exception dbEx)
                {
                    Utils.Logger.Warning("SystemSettingsViewModel", $"⚠️ DatabaseService获取失败: {dbEx.Message}");
                    _databaseService = null;
                }

                // 同步加载基础设置，异步加载服务器相关数据
                LoadBasicSettings();
                _ = LoadServerDataAsync();

                Utils.Logger.Info("SystemSettingsViewModel", "✅ 系统设置ViewModel已初始化（现代架构）");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SystemSettingsViewModel", $"❌ ViewModel初始化失败: {ex.Message}");
                Utils.Logger.Error("SystemSettingsViewModel", $"❌ 堆栈跟踪: {ex.StackTrace}");

                // 确保至少有默认值
                try
                {
                    InitializeDefaultValues();
                    Utils.Logger.Info("SystemSettingsViewModel", "✅ 已设置备用默认值");
                }
                catch (Exception fallbackEx)
                {
                    Utils.Logger.Error("SystemSettingsViewModel", $"❌ 设置备用默认值也失败: {fallbackEx.Message}");
                }
            }
        }

        /// <summary>
        /// 初始化默认值
        /// </summary>
        private void InitializeDefaultValues()
        {
            // 基础设置默认值
            ServerAddress = "http://localhost:5065";
            MaxConcurrentUploads = 3;
            MaxConcurrentDownloads = 3;
            MaxConcurrentChunks = 4;
            AutoStartConversion = false;
            ShowNotifications = true;
            DefaultOutputPath = "";

            // 连接状态默认值
            ConnectionStatus = "未测试";
            ConnectionStatusColor = "#808080";
            IsTestingConnection = false;

            // 数据库状态默认值
            DatabasePath = "VideoConversion.db";
            DatabaseStatus = "检查中...";
            DatabaseSize = "";

            // 🔑 服务器信息默认值 - 未连接时显示未知状态
            ServerVersion = "未连接";
            FfmpegVersion = "未连接";
            HardwareAcceleration = "未连接";
            Uptime = "未连接";

            // 🔑 服务器状态默认值 - 确保界面正确显示
            IsServerOnline = false;
            CanAccessServerFeatures = false;
            ServerFeatureStatusText = "🔌 服务器未连接 - 请设置服务器地址并测试连接";

            // 🔑 系统监控默认值 - 未连接时显示0值
            AvailableDiskSpace = 0;
            TotalDiskSpace = 0;
            ActiveTasks = 0;
            QueuedTasks = 0;
            CpuUsage = 0.0;
            MemoryUsage = 0.0;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 完整初始化 - 供外部调用
        /// </summary>
        public async Task CompleteInitializationAsync()
        {
            try
            {
                Utils.Logger.Info("SystemSettingsViewModel", "🔄 开始完整初始化");

                // 确保数据库连接正常
                try
                {
                    await _databaseService.InitializeAsync();
                    DatabaseStatus = "连接正常";
                }
                catch (Exception ex)
                {
                    DatabaseStatus = $"连接失败: {ex.Message}";
                    Utils.Logger.Warning("SystemSettingsViewModel", $"⚠️ 数据库连接失败: {ex.Message}");
                }

                // 如果服务器可访问，刷新服务器信息
                if (CanAccessServerFeatures)
                {
                    await RefreshSystemInfoAsync();
                }

                Utils.Logger.Info("SystemSettingsViewModel", "✅ 完整初始化完成");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SystemSettingsViewModel", $"❌ 完整初始化失败: {ex.Message}");
            }
        }

        #endregion

        #region 简化的方法实现

        /// <summary>
        /// 加载基础设置 - 使用默认值，异步更新
        /// </summary>
        private void LoadBasicSettings()
        {
            try
            {
                Utils.Logger.Info("SystemSettingsViewModel", "📖 加载基础设置（使用默认值）");

                // 🔑 立即设置默认值，确保界面有数据显示
                ServerAddress = "http://localhost:5065";
                MaxConcurrentUploads = 3;
                MaxConcurrentDownloads = 3;
                MaxConcurrentChunks = 4;
                AutoStartConversion = false;
                ShowNotifications = true;
                DefaultOutputPath = "";

                // 设置数据库基础信息
                LoadBasicDatabaseInfo();

                // 🔑 只有在数据库服务可用时才异步加载真实设置
                if (_databaseService != null)
                {
                    _ = LoadRealSettingsAsync();
                }
                else
                {
                    Utils.Logger.Warning("SystemSettingsViewModel", "⚠️ 数据库服务不可用，使用默认设置");
                }

                Utils.Logger.Info("SystemSettingsViewModel", "✅ 基础设置已加载（默认值）");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SystemSettingsViewModel", $"❌ 加载基础设置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 异步加载真实设置
        /// </summary>
        private async Task LoadRealSettingsAsync()
        {
            try
            {
                Utils.Logger.Info("SystemSettingsViewModel", "📖 异步加载真实设置");

                // 从数据库异步加载设置
                var serverAddress = await _databaseService.GetSettingAsync("ServerAddress");
                if (!string.IsNullOrEmpty(serverAddress))
                    ServerAddress = serverAddress;

                var maxUploadsStr = await _databaseService.GetSettingAsync("MaxConcurrentUploads");
                if (int.TryParse(maxUploadsStr, out var maxUploads))
                    MaxConcurrentUploads = maxUploads;

                var maxDownloadsStr = await _databaseService.GetSettingAsync("MaxConcurrentDownloads");
                if (int.TryParse(maxDownloadsStr, out var maxDownloads))
                    MaxConcurrentDownloads = maxDownloads;

                var maxChunksStr = await _databaseService.GetSettingAsync("MaxConcurrentChunks");
                if (int.TryParse(maxChunksStr, out var maxChunks))
                    MaxConcurrentChunks = maxChunks;

                var autoStartStr = await _databaseService.GetSettingAsync("AutoStartConversion");
                if (bool.TryParse(autoStartStr, out var autoStart))
                    AutoStartConversion = autoStart;

                var showNotificationsStr = await _databaseService.GetSettingAsync("ShowNotifications");
                if (bool.TryParse(showNotificationsStr, out var showNotifications))
                    ShowNotifications = showNotifications;

                var defaultPath = await _databaseService.GetSettingAsync("DefaultOutputPath");
                if (!string.IsNullOrEmpty(defaultPath))
                    DefaultOutputPath = defaultPath;

                Utils.Logger.Info("SystemSettingsViewModel", "✅ 真实设置已异步加载完成");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SystemSettingsViewModel", $"❌ 异步加载真实设置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 异步加载服务器相关数据
        /// </summary>
        private async Task LoadServerDataAsync()
        {
            try
            {
                Utils.Logger.Info("SystemSettingsViewModel", "🌐 异步加载服务器数据");

                // 等待一小段时间，确保UI完全加载
                await Task.Delay(100);

                // 测试服务器连接
                Utils.Logger.Info("SystemSettingsViewModel", $"🔗 开始自动连接测试，服务器地址: {ServerAddress}");
                await TestConnectionAsync();

                Utils.Logger.Info("SystemSettingsViewModel", $"✅ 服务器数据已异步加载完成，连接状态: {ConnectionStatus}");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SystemSettingsViewModel", $"❌ 异步加载服务器数据失败: {ex.Message}");
                Utils.Logger.Error("SystemSettingsViewModel", $"❌ 堆栈跟踪: {ex.StackTrace}");

                // 确保连接状态被正确设置
                ConnectionStatus = $"自动连接失败: {ex.Message}";
                ConnectionStatusColor = "#FF0000";
                UpdateServerConnectionStatus(false);
            }
        }

        /// <summary>
        /// 加载基础数据库信息
        /// </summary>
        private void LoadBasicDatabaseInfo()
        {
            try
            {
                // 🔑 获取正确的数据库路径 - 与SqlSugarDatabaseService一致
                var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                var fullDbPath = Path.Combine(appDirectory, "VideoConversion.db");
                DatabasePath = fullDbPath;

                // 检查数据库文件
                if (File.Exists(fullDbPath))
                {
                    var fileInfo = new FileInfo(fullDbPath);
                    DatabaseSize = $"({FormatFileSize(fileInfo.Length)})";
                    DatabaseStatus = "文件存在";
                    Utils.Logger.Debug("SystemSettingsViewModel", $"数据库文件找到: {fullDbPath}, 大小: {fileInfo.Length} bytes");
                }
                else
                {
                    DatabaseSize = "(文件不存在)";
                    DatabaseStatus = "文件不存在";
                    Utils.Logger.Warning("SystemSettingsViewModel", $"数据库文件不存在: {fullDbPath}");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SystemSettingsViewModel", $"❌ 加载基础数据库信息失败: {ex.Message}");
                DatabaseStatus = "获取失败";
                DatabaseSize = "";
                DatabasePath = "未知";
            }
        }

        /// <summary>
        /// 异步加载设置 - 从数据库加载真实设置 - 与Client项目一致（保留兼容性）
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

                // 检查数据库连接状态 - 使用SqlSugar
                try
                {
                    await _databaseService.InitializeAsync();
                    DatabaseStatus = "连接正常";
                }
                catch
                {
                    DatabaseStatus = "连接失败";
                }

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
        /// 增强的连接测试命令 - 同时更新服务器状态
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
                    UpdateServerConnectionStatus(false);
                    return;
                }

                if (!ServerAddress.StartsWith("http://") && !ServerAddress.StartsWith("https://"))
                {
                    ConnectionStatus = "服务器地址格式不正确";
                    ConnectionStatusColor = "#FF0000"; // Red
                    UpdateServerConnectionStatus(false);
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
                    UpdateServerConnectionStatus(true);

                    // 自动刷新系统信息
                    await RefreshSystemInfoAsync();

                    Utils.Logger.Info("SystemSettingsViewModel", "✅ 服务器连接测试成功");
                }
                else
                {
                    ConnectionStatus = $"连接失败: HTTP {(int)response.StatusCode}";
                    ConnectionStatusColor = "#FF0000"; // Red
                    UpdateServerConnectionStatus(false);
                    Utils.Logger.Warning("SystemSettingsViewModel", $"⚠️ 服务器连接失败: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                ConnectionStatus = $"连接错误: {ex.Message}";
                ConnectionStatusColor = "#FF0000"; // Red
                UpdateServerConnectionStatus(false);
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
                await _databaseService.SetSettingAsync("MaxConcurrentChunks", MaxConcurrentChunks.ToString());
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
        /// 刷新系统信息命令 - 完整实现
        /// </summary>
        [RelayCommand]
        private async Task RefreshSystemInfoAsync()
        {
            try
            {
                Utils.Logger.Info("SystemSettingsViewModel", "🔄 开始刷新系统信息");

                // 🔑 检查ApiService是否可用
                if (_apiService == null)
                {
                    Utils.Logger.Warning("SystemSettingsViewModel", "⚠️ ApiService不可用，无法刷新系统信息");

                    // 设置离线状态的默认值
                    ServerVersion = "离线模式 - ApiService未初始化";
                    FfmpegVersion = "离线模式";
                    HardwareAcceleration = "离线模式";
                    Uptime = "离线模式";
                    AvailableDiskSpace = 0;
                    TotalDiskSpace = 0;
                    ActiveTasks = 0;
                    QueuedTasks = 0;
                    CpuUsage = 0.0;
                    MemoryUsage = 0.0;

                    // 显示用户友好的提示
                    ConnectionStatus = "ApiService未初始化，请重新打开设置窗口";
                    ConnectionStatusColor = "#FFA500"; // Orange

                    return;
                }

                var response = await _apiService.GetSystemStatusAsync();
                if (response.Success && response.Data != null)
                {
                    var status = response.Data;
                    ServerVersion = status.ServerVersion;
                    FfmpegVersion = status.FfmpegVersion;
                    HardwareAcceleration = status.HardwareAcceleration;
                    Uptime = status.Uptime;
                    AvailableDiskSpace = status.AvailableDiskSpace;
                    TotalDiskSpace = status.TotalDiskSpace;
                    ActiveTasks = status.ActiveTasks;
                    QueuedTasks = status.QueuedTasks;
                    CpuUsage = status.CpuUsage;
                    MemoryUsage = status.MemoryUsage;

                    Utils.Logger.Info("SystemSettingsViewModel", "✅ 系统信息刷新成功");
                    Utils.Logger.Debug("SystemSettingsViewModel", $"服务器版本: {ServerVersion}, FFmpeg版本: {FfmpegVersion}");
                }
                else
                {
                    Utils.Logger.Warning("SystemSettingsViewModel", $"⚠️ 获取系统信息失败: {response?.Message ?? "未知错误"}");
                }

                // 同时加载诊断信息（保留部分现代化功能）
                await LoadRecentDiagnosticsAsync();
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SystemSettingsViewModel", $"❌ 刷新系统信息异常: {ex.Message}");
                Utils.Logger.Error("SystemSettingsViewModel", $"❌ 堆栈跟踪: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// 配置磁盘空间命令 - 恢复对话框模式，与Client项目一致
        /// </summary>
        [RelayCommand]
        private async Task ConfigureDiskSpaceAsync()
        {
            try
            {
                Utils.Logger.Info("SystemSettingsViewModel", "⚙️ 打开磁盘空间配置对话框");

                // 🔑 检查ApiService是否可用
                if (_apiService == null)
                {
                    Utils.Logger.Warning("SystemSettingsViewModel", "⚠️ ApiService不可用，无法配置磁盘空间");

                    // 显示用户友好的提示（这里可以添加MessageBox或其他UI提示）
                    Utils.Logger.Info("SystemSettingsViewModel", "💡 提示：请确保服务器连接正常后再使用此功能");
                    return;
                }

                // 获取当前配置
                var configResponse = await _apiService.GetDiskSpaceConfigAsync();
                if (!configResponse.Success || configResponse.Data == null)
                {
                    Utils.Logger.Warning("SystemSettingsViewModel", $"⚠️ 无法获取当前磁盘配置: {configResponse.Message}");
                    // 使用默认配置
                    var defaultConfig = new Application.DTOs.DiskSpaceConfigDto
                    {
                        MinFreeSpace = 10L * 1024 * 1024 * 1024, // 10GB
                        AutoCleanup = false,
                        CleanupIntervalHours = 24,
                        MaxFileAgeHours = 168,
                        CleanupPath = ""
                    };
                    configResponse = Application.DTOs.ApiResponseDto<Application.DTOs.DiskSpaceConfigDto>.CreateSuccess(defaultConfig);
                }

                // 创建并显示磁盘空间配置对话框 - 与Client项目一致
                var viewModel = new Presentation.ViewModels.Dialogs.DiskSpaceConfigViewModel(configResponse.Data!);
                var dialog = new Presentation.Views.Dialogs.DiskSpaceConfigDialog(viewModel);

                // 获取主窗口作为父窗口
                var mainWindow = App.Current?.ApplicationLifetime is
                    Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                    ? desktop.MainWindow : null;

                var result = await dialog.ShowDialog<bool?>(mainWindow);

                if (result == true)
                {
                    Utils.Logger.Info("SystemSettingsViewModel", "✅ 磁盘空间配置已保存");
                    // 刷新系统信息以获取最新状态
                    await RefreshSystemInfoAsync();
                }
                else
                {
                    Utils.Logger.Info("SystemSettingsViewModel", "🚫 磁盘空间配置已取消");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SystemSettingsViewModel", $"❌ 配置磁盘空间异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 查看系统日志命令 - 与Client项目一致，使用MessageBox显示
        /// </summary>
        [RelayCommand]
        private async Task ViewSystemLogsAsync()
        {
            try
            {
                Utils.Logger.Info("SystemSettingsViewModel", "📋 获取系统诊断日志");

                // 🔑 检查ApiService是否可用
                if (_apiService == null)
                {
                    Utils.Logger.Warning("SystemSettingsViewModel", "⚠️ ApiService不可用，无法获取系统日志");

                    // 显示用户友好的提示
                    Utils.Logger.Info("SystemSettingsViewModel", "💡 提示：请确保服务器连接正常后再使用此功能");
                    return;
                }

                var response = await _apiService.GetSystemDiagnosticsAsync();
                if (response.Success && response.Data != null)
                {
                    // 与Client项目一致的显示方式
                    var diagnosticsText = string.Join("\n",
                        response.Data.Select(d => $"[{d.Level.ToUpper()}] {d.Category}: {d.Message}"));

                    // TODO: 实现MessageBox服务或使用简单对话框
                    Utils.Logger.Info("SystemSettingsViewModel", $"📊 系统诊断信息:\n{diagnosticsText}");

                    // 同时更新内联显示（保留部分现代化功能）
                    await LoadRecentDiagnosticsAsync();

                    Utils.Logger.Info("SystemSettingsViewModel", $"✅ 诊断信息获取成功，共 {response.Data.Count} 条记录");
                }
                else
                {
                    Utils.Logger.Warning("SystemSettingsViewModel", $"⚠️ 获取诊断信息失败: {response.Message}");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SystemSettingsViewModel", $"❌ 查看系统日志异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载最近的诊断信息用于内联显示
        /// </summary>
        private async Task LoadRecentDiagnosticsAsync()
        {
            try
            {
                // 🔑 检查ApiService是否可用
                if (_apiService == null)
                {
                    Utils.Logger.Warning("SystemSettingsViewModel", "⚠️ ApiService不可用，无法加载诊断信息");
                    return;
                }

                var response = await _apiService.GetSystemDiagnosticsAsync();
                if (response.Success && response.Data != null)
                {
                    var recentDiagnostics = response.Data
                        .OrderByDescending(d => d.Timestamp)
                        .Take(5) // 只显示最近5条
                        .Select(d => new SystemDiagnosticDisplayDto(d))
                        .ToList();

                    RecentDiagnostics.Clear();
                    foreach (var diagnostic in recentDiagnostics)
                    {
                        RecentDiagnostics.Add(diagnostic);
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SystemSettingsViewModel", $"❌ 加载诊断信息异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 清理文件命令 - 与Client项目完全一致的完整实现
        /// </summary>
        [RelayCommand]
        private async Task CleanupFilesAsync()
        {
            try
            {
                Utils.Logger.Info("SystemSettingsViewModel", "🧹 开始文件清理");

                // 🔧 创建清理请求 - 与Client项目一致的默认配置
                var cleanupRequest = new ManualCleanupRequest
                {
                    CleanupTempFiles = true,        // 清理临时文件
                    CleanupDownloadedFiles = false, // 不清理已下载文件（用户可能还需要）
                    CleanupOrphanFiles = true,      // 清理孤儿文件
                    CleanupFailedTasks = true,      // 清理失败任务文件
                    CleanupLogFiles = false,        // 不清理日志文件（用于调试）
                    IgnoreRetention = false         // 遵守保留时间限制
                };

                Utils.Logger.Info("SystemSettingsViewModel", $"🧹 清理配置: {cleanupRequest}");

                // 调用API执行清理
                var response = await _apiService.TriggerManualCleanupAsync(cleanupRequest);

                if (response.Success && response.Data != null)
                {
                    var result = response.Data;
                    var message = $"清理完成！释放空间: {result.FormattedTotalSize}, 清理文件: {result.TotalCleanedFiles}个";

                    Utils.Logger.Info("SystemSettingsViewModel", $"✅ {message}");

                    // 刷新系统信息以显示最新的磁盘使用情况
                    await RefreshSystemInfoAsync();

                    // TODO: 显示成功消息给用户
                    // await ShowSuccessMessageAsync(message);
                }
                else
                {
                    var errorMessage = $"文件清理失败: {response.Message}";
                    Utils.Logger.Warning("SystemSettingsViewModel", $"⚠️ {errorMessage}");

                    // TODO: 显示错误消息给用户
                    // await ShowErrorMessageAsync(errorMessage);
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SystemSettingsViewModel", $"❌ 文件清理异常: {ex.Message}");

                // TODO: 显示错误消息给用户
                // await ShowErrorMessageAsync($"文件清理失败: {ex.Message}");
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

        partial void OnMaxConcurrentChunksChanged(int value)
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

        #region 智能状态管理

        /// <summary>
        /// 更新服务器连接状态
        /// </summary>
        private void UpdateServerConnectionStatus(bool isConnected)
        {
            IsServerOnline = isConnected;
            CanAccessServerFeatures = isConnected;

            ServerFeatureStatusText = isConnected
                ? "🟢 服务器在线 - 所有功能可用"
                : "🔴 服务器离线 - 仅本地设置可用";

            // 通知状态相关属性变化
            OnPropertyChanged(nameof(ServerStatusText));
            OnPropertyChanged(nameof(ServerStatusColor));
            OnPropertyChanged(nameof(FormattedDiskSpace));
            OnPropertyChanged(nameof(TaskStatusDisplay));
            OnPropertyChanged(nameof(FormattedCpuUsage));
            OnPropertyChanged(nameof(FormattedMemoryUsage));

            // 刷新命令可执行状态
            RefreshSystemInfoCommand.NotifyCanExecuteChanged();
            ViewSystemLogsCommand.NotifyCanExecuteChanged();

            Utils.Logger.Info("SystemSettingsViewModel", $"🔄 服务器状态更新: {(isConnected ? "在线" : "离线")}");
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

        /// <summary>
        /// 格式化磁盘空间显示
        /// </summary>
        public string FormattedDiskSpace =>
            TotalDiskSpace > 0
                ? $"{FormatFileSize(AvailableDiskSpace)} / {FormatFileSize(TotalDiskSpace)}"
                : "未知";

        /// <summary>
        /// 磁盘使用百分比
        /// </summary>
        public double DiskUsagePercentage =>
            TotalDiskSpace > 0
                ? (double)(TotalDiskSpace - AvailableDiskSpace) / TotalDiskSpace * 100
                : 0;

        /// <summary>
        /// CPU使用率显示
        /// </summary>
        public string FormattedCpuUsage => $"{CpuUsage:F1}%";

        /// <summary>
        /// 内存使用率显示
        /// </summary>
        public string FormattedMemoryUsage => $"{MemoryUsage:F1}%";

        /// <summary>
        /// 任务状态显示
        /// </summary>
        public string TaskStatusDisplay => $"活跃: {ActiveTasks} | 队列: {QueuedTasks}";

        /// <summary>
        /// 服务器状态显示文本
        /// </summary>
        public string ServerStatusText => IsServerOnline ? "在线" : "离线";

        /// <summary>
        /// 服务器状态颜色
        /// </summary>
        public string ServerStatusColor => IsServerOnline ? "#28a745" : "#6c757d";

        #endregion

        #endregion
    }

    /// <summary>
    /// 系统诊断显示DTO - 扩展显示属性
    /// </summary>
    public class SystemDiagnosticDisplayDto : SystemDiagnosticDto
    {
        public SystemDiagnosticDisplayDto(SystemDiagnosticDto source)
        {
            Category = source.Category;
            Level = source.Level;
            Message = source.Message;
            Timestamp = source.Timestamp;
            Details = source.Details;
        }

        /// <summary>
        /// 格式化时间戳
        /// </summary>
        public string FormattedTimestamp => Timestamp.ToString("MM-dd HH:mm:ss");

        /// <summary>
        /// 级别颜色
        /// </summary>
        public string LevelColor => Level switch
        {
            "Error" => "#DC3545",
            "Warning" => "#FFC107",
            "Info" => "#17A2B8",
            "Debug" => "#6C757D",
            _ => "#6C757D"
        };
    }
}
