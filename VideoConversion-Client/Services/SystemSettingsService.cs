using System;
using System.Threading.Tasks;
using VideoConversion_Client.Models;
using VideoConversion_Client.Utils;

namespace VideoConversion_Client.Services
{
    /// <summary>
    /// 系统设置服务，用于管理应用程序设置
    /// </summary>
    public class SystemSettingsService
    {
        private static SystemSettingsService? _instance;
        private static readonly object _lock = new object();
        
        private SystemSettingsModel _currentSettings;

        public static SystemSettingsService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new SystemSettingsService();
                        }
                    }
                }
                return _instance;
            }
        }

        private SystemSettingsService()
        {
            _currentSettings = SystemSettingsModel.LoadSettings();
        }

        /// <summary>
        /// 当前设置
        /// </summary>
        public SystemSettingsModel CurrentSettings => _currentSettings;

        /// <summary>
        /// 设置变化事件
        /// </summary>
        public event EventHandler<SystemSettingsChangedEventArgs>? SettingsChanged;

        /// <summary>
        /// 更新设置
        /// </summary>
        public void UpdateSettings(SystemSettingsModel newSettings)
        {
            var oldSettings = _currentSettings.Clone();
            _currentSettings = newSettings.Clone();

            // 保存到数据库
            _currentSettings.SaveSettings();

            // 触发设置变化事件
            SettingsChanged?.Invoke(this, new SystemSettingsChangedEventArgs(oldSettings, _currentSettings));
        }

        /// <summary>
        /// 重新加载设置
        /// </summary>
        public void ReloadSettings()
        {
            var oldSettings = _currentSettings.Clone();
            _currentSettings = SystemSettingsModel.LoadSettings();

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
        /// 是否自动开始转换
        /// </summary>
        public bool ShouldAutoStartConversion()
        {
            return _currentSettings.AutoStartConversion;
        }

        /// <summary>
        /// 是否显示通知
        /// </summary>
        public bool ShouldShowNotifications()
        {
            return _currentSettings.ShowNotifications;
        }

        /// <summary>
        /// 获取默认输出路径
        /// </summary>
        public string GetDefaultOutputPath()
        {
            return _currentSettings.DefaultOutputPath;
        }

        /// <summary>
        /// 验证当前设置
        /// </summary>
        public bool ValidateCurrentSettings()
        {
            return _currentSettings.IsValidServerAddress() &&
                   _currentSettings.MaxConcurrentUploads > 0 &&
                   _currentSettings.MaxConcurrentDownloads > 0;
        }

        /// <summary>
        /// 重置为默认设置
        /// </summary>
        public void ResetToDefaults()
        {
            var oldSettings = _currentSettings.Clone();
            _currentSettings.ResetToDefaults();
            _currentSettings.SaveSettings();

            // 触发设置变化事件
            SettingsChanged?.Invoke(this, new SystemSettingsChangedEventArgs(oldSettings, _currentSettings));
        }

        /// <summary>
        /// 获取数据库信息
        /// </summary>
        public DatabaseInfo GetDatabaseInfo()
        {
            var dbService = DatabaseService.Instance;
            return new DatabaseInfo
            {
                DatabasePath = dbService.GetDatabasePath(),
                DatabaseSize = dbService.GetDatabaseSize(),
                IsConnected = dbService.TestConnection()
            };
        }

        /// <summary>
        /// 备份设置数据库
        /// </summary>
        public bool BackupSettings(string backupPath)
        {
            return SafeExecutor.Execute(
                () => DatabaseService.Instance.BackupDatabase(backupPath),
                "备份设置",
                false);
        }

        /// <summary>
        /// 恢复设置数据库
        /// </summary>
        public bool RestoreSettings(string backupPath)
        {
            return SafeExecutor.Execute(() =>
            {
                var dbService = DatabaseService.Instance;
                var success = dbService.RestoreDatabase(backupPath);

                if (success)
                {
                    // 重新加载设置
                    ReloadSettings();
                }

                return success;
            }, "恢复设置", false);
        }
    }

    /// <summary>
    /// 系统设置变化事件参数
    /// </summary>
    public class SystemSettingsChangedEventArgs : EventArgs
    {
        public SystemSettingsModel OldSettings { get; }
        public SystemSettingsModel NewSettings { get; }

        public SystemSettingsChangedEventArgs(SystemSettingsModel oldSettings, SystemSettingsModel newSettings)
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
            OldSettings.MaxConcurrentDownloads != NewSettings.MaxConcurrentDownloads;

        /// <summary>
        /// 其他设置是否发生变化
        /// </summary>
        public bool OtherSettingsChanged =>
            OldSettings.AutoStartConversion != NewSettings.AutoStartConversion ||
            OldSettings.ShowNotifications != NewSettings.ShowNotifications ||
            OldSettings.DefaultOutputPath != NewSettings.DefaultOutputPath;
    }

    /// <summary>
    /// 数据库信息
    /// </summary>
    public class DatabaseInfo
    {
        public string DatabasePath { get; set; } = "";
        public long DatabaseSize { get; set; }
        public bool IsConnected { get; set; }

        public string GetFormattedSize()
        {
            return FileSizeFormatter.FormatBytesAuto(DatabaseSize);
        }
    }
}
