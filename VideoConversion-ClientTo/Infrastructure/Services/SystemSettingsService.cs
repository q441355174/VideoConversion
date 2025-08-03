using System;
using System.Threading.Tasks;
using VideoConversion_ClientTo.Infrastructure;

namespace VideoConversion_ClientTo.Infrastructure.Services
{
    /// <summary>
    /// 系统设置服务 - 与Client项目完全一致的实现
    /// </summary>
    public class SystemSettingsService
    {
        private static SystemSettingsService? _instance;
        private static readonly object _lock = new object();
        
        private readonly IDatabaseService _databaseService;
        private SystemSettings _currentSettings;

        public static SystemSettingsService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new SystemSettingsService();
                    }
                }
                return _instance;
            }
        }

        private SystemSettingsService()
        {
            _databaseService = ServiceLocator.GetRequiredService<IDatabaseService>();
            _currentSettings = LoadSettingsAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// 当前设置
        /// </summary>
        public SystemSettings CurrentSettings => _currentSettings;

        /// <summary>
        /// 设置变化事件
        /// </summary>
        public event EventHandler<SystemSettingsChangedEventArgs>? SettingsChanged;

        /// <summary>
        /// 更新设置
        /// </summary>
        public async Task UpdateSettingsAsync(SystemSettings newSettings)
        {
            var oldSettings = _currentSettings.Clone();
            _currentSettings = newSettings.Clone();

            // 保存到数据库
            await SaveSettingsAsync(newSettings);

            // 触发设置变化事件
            SettingsChanged?.Invoke(this, new SystemSettingsChangedEventArgs(oldSettings, _currentSettings));
        }

        /// <summary>
        /// 重新加载设置
        /// </summary>
        public async Task ReloadSettingsAsync()
        {
            var oldSettings = _currentSettings.Clone();
            _currentSettings = await LoadSettingsAsync();

            // 触发设置变化事件
            SettingsChanged?.Invoke(this, new SystemSettingsChangedEventArgs(oldSettings, _currentSettings));
        }

        /// <summary>
        /// 获取服务器地址
        /// </summary>
        public string GetServerAddress()
        {
            return _currentSettings.ServerAddress;
        }

        /// <summary>
        /// 获取最大并发上传数
        /// </summary>
        public int GetMaxConcurrentUploads()
        {
            return _currentSettings.MaxConcurrentUploads;
        }

        /// <summary>
        /// 获取最大并发下载数
        /// </summary>
        public int GetMaxConcurrentDownloads()
        {
            return _currentSettings.MaxConcurrentDownloads;
        }

        /// <summary>
        /// 获取最大分片并发数
        /// </summary>
        public int GetMaxConcurrentChunks()
        {
            return _currentSettings.MaxConcurrentChunks;
        }

        /// <summary>
        /// 从数据库加载设置
        /// </summary>
        private async Task<SystemSettings> LoadSettingsAsync()
        {
            try
            {
                var serverAddress = await _databaseService.GetSettingAsync("ServerAddress") ?? "http://localhost:5065";
                
                var maxUploadsStr = await _databaseService.GetSettingAsync("MaxConcurrentUploads");
                var maxUploads = int.TryParse(maxUploadsStr, out var uploads) ? uploads : 3;
                
                var maxDownloadsStr = await _databaseService.GetSettingAsync("MaxConcurrentDownloads");
                var maxDownloads = int.TryParse(maxDownloadsStr, out var downloads) ? downloads : 2;

                var maxChunksStr = await _databaseService.GetSettingAsync("MaxConcurrentChunks");
                var maxChunks = int.TryParse(maxChunksStr, out var chunks) ? chunks : 4;

                return new SystemSettings
                {
                    ServerAddress = serverAddress,
                    MaxConcurrentUploads = maxUploads,
                    MaxConcurrentDownloads = maxDownloads,
                    MaxConcurrentChunks = maxChunks
                };
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SystemSettingsService", $"加载系统设置失败: {ex.Message}");
                return SystemSettings.CreateDefault();
            }
        }

        /// <summary>
        /// 保存设置到数据库
        /// </summary>
        private async Task SaveSettingsAsync(SystemSettings settings)
        {
            try
            {
                await _databaseService.SetSettingAsync("ServerAddress", settings.ServerAddress);
                await _databaseService.SetSettingAsync("MaxConcurrentUploads", settings.MaxConcurrentUploads.ToString());
                await _databaseService.SetSettingAsync("MaxConcurrentDownloads", settings.MaxConcurrentDownloads.ToString());
                await _databaseService.SetSettingAsync("MaxConcurrentChunks", settings.MaxConcurrentChunks.ToString());
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SystemSettingsService", $"保存系统设置失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 系统设置模型
    /// </summary>
    public class SystemSettings
    {
        public string ServerAddress { get; set; } = "http://localhost:5065";
        public int MaxConcurrentUploads { get; set; } = 3;
        public int MaxConcurrentDownloads { get; set; } = 2;
        public int MaxConcurrentChunks { get; set; } = 4; // 分片上传并发数

        public static SystemSettings CreateDefault()
        {
            return new SystemSettings();
        }

        public SystemSettings Clone()
        {
            return new SystemSettings
            {
                ServerAddress = this.ServerAddress,
                MaxConcurrentUploads = this.MaxConcurrentUploads,
                MaxConcurrentDownloads = this.MaxConcurrentDownloads,
                MaxConcurrentChunks = this.MaxConcurrentChunks
            };
        }
    }

    /// <summary>
    /// 系统设置变化事件参数
    /// </summary>
    public class SystemSettingsChangedEventArgs : EventArgs
    {
        public SystemSettings OldSettings { get; }
        public SystemSettings NewSettings { get; }

        public SystemSettingsChangedEventArgs(SystemSettings oldSettings, SystemSettings newSettings)
        {
            OldSettings = oldSettings;
            NewSettings = newSettings;
        }

        /// <summary>
        /// 服务器地址是否发生变化
        /// </summary>
        public bool ServerAddressChanged => OldSettings.ServerAddress != NewSettings.ServerAddress;

        /// <summary>
        /// 并发设置是否发生变化
        /// </summary>
        public bool ConcurrencySettingsChanged =>
            OldSettings.MaxConcurrentUploads != NewSettings.MaxConcurrentUploads ||
            OldSettings.MaxConcurrentDownloads != NewSettings.MaxConcurrentDownloads ||
            OldSettings.MaxConcurrentChunks != NewSettings.MaxConcurrentChunks;
    }
}
