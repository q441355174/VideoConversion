using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;

namespace VideoConversion_Client.Models
{
    /// <summary>
    /// 系统设置模型
    /// </summary>
    public class SystemSettingsModel : INotifyPropertyChanged
    {
        private string _serverAddress = "http://localhost:5065";
        private int _maxConcurrentUploads = 3;
        private int _maxConcurrentDownloads = 3;
        private bool _autoStartConversion = true;
        private bool _showNotifications = true;
        private string _defaultOutputPath = "";

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// 服务器地址
        /// </summary>
        public string ServerAddress
        {
            get => _serverAddress;
            set
            {
                if (_serverAddress != value)
                {
                    _serverAddress = value;
                    OnPropertyChanged(nameof(ServerAddress));
                }
            }
        }

        /// <summary>
        /// 最大同时上传数量
        /// </summary>
        public int MaxConcurrentUploads
        {
            get => _maxConcurrentUploads;
            set
            {
                if (_maxConcurrentUploads != value && value > 0 && value <= 10)
                {
                    _maxConcurrentUploads = value;
                    OnPropertyChanged(nameof(MaxConcurrentUploads));
                }
            }
        }

        /// <summary>
        /// 最大同时下载数量
        /// </summary>
        public int MaxConcurrentDownloads
        {
            get => _maxConcurrentDownloads;
            set
            {
                if (_maxConcurrentDownloads != value && value > 0 && value <= 10)
                {
                    _maxConcurrentDownloads = value;
                    OnPropertyChanged(nameof(MaxConcurrentDownloads));
                }
            }
        }

        /// <summary>
        /// 自动开始转换
        /// </summary>
        public bool AutoStartConversion
        {
            get => _autoStartConversion;
            set
            {
                if (_autoStartConversion != value)
                {
                    _autoStartConversion = value;
                    OnPropertyChanged(nameof(AutoStartConversion));
                }
            }
        }

        /// <summary>
        /// 显示通知
        /// </summary>
        public bool ShowNotifications
        {
            get => _showNotifications;
            set
            {
                if (_showNotifications != value)
                {
                    _showNotifications = value;
                    OnPropertyChanged(nameof(ShowNotifications));
                }
            }
        }

        /// <summary>
        /// 默认输出路径
        /// </summary>
        public string DefaultOutputPath
        {
            get => _defaultOutputPath;
            set
            {
                if (_defaultOutputPath != value)
                {
                    _defaultOutputPath = value ?? "";
                    OnPropertyChanged(nameof(DefaultOutputPath));
                }
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 验证服务器地址格式
        /// </summary>
        public bool IsValidServerAddress()
        {
            try
            {
                var uri = new Uri(ServerAddress);
                return uri.Scheme == "http" || uri.Scheme == "https";
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 保存设置到数据库
        /// </summary>
        public void SaveSettings()
        {
            try
            {
                var entity = SystemSettingsEntity.FromModel(this);
                var dbService = VideoConversion_Client.Services.DatabaseService.Instance;
                dbService.SaveSystemSettings(entity);

                System.Diagnostics.Debug.WriteLine("设置已保存到数据库");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存设置到数据库失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从数据库加载设置
        /// </summary>
        public static SystemSettingsModel LoadSettings()
        {
            try
            {
                var dbService = VideoConversion_Client.Services.DatabaseService.Instance;
                var entity = dbService.GetSystemSettings();

                if (entity != null)
                {
                    var model = entity.ToModel();
                    System.Diagnostics.Debug.WriteLine("从数据库加载设置成功");
                    return model;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("数据库中没有设置记录，使用默认设置");
                    var defaultSettings = new SystemSettingsModel();
                    defaultSettings.SaveSettings(); // 保存默认设置到数据库
                    return defaultSettings;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"从数据库加载设置失败: {ex.Message}");
                return new SystemSettingsModel();
            }
        }

        /// <summary>
        /// 重置为默认设置
        /// </summary>
        public void ResetToDefaults()
        {
            ServerAddress = "http://localhost:5065";
            MaxConcurrentUploads = 3;
            MaxConcurrentDownloads = 3;
            AutoStartConversion = true;
            ShowNotifications = true;
            DefaultOutputPath = "";
        }

        /// <summary>
        /// 创建设置的副本
        /// </summary>
        public SystemSettingsModel Clone()
        {
            return new SystemSettingsModel
            {
                ServerAddress = this.ServerAddress,
                MaxConcurrentUploads = this.MaxConcurrentUploads,
                MaxConcurrentDownloads = this.MaxConcurrentDownloads,
                AutoStartConversion = this.AutoStartConversion,
                ShowNotifications = this.ShowNotifications,
                DefaultOutputPath = this.DefaultOutputPath
            };
        }
    }
}
