using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VideoConversion_Client.Services;
using VideoConversion_Client.Models;

namespace VideoConversion_Client.Views.SystemSetting
{
    public partial class SystemSettingsWindow : Window
    {
        private SystemSettingsModel _settings;
        private SystemSettingsModel _originalSettings;
        private ApiService? _apiService;

        public SystemSettingsModel Settings => _settings;
        public bool SettingsChanged { get; private set; }

        public SystemSettingsWindow() : this(SystemSettingsModel.LoadSettings())
        {
        }

        public SystemSettingsWindow(SystemSettingsModel settings)
        {
            InitializeComponent();
            _originalSettings = settings;
            _settings = settings.Clone(); // 创建副本用于编辑

            InitializeControls();
            LoadSettingsToUI();
            LoadDatabaseInfo();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void InitializeControls()
        {
            // 绑定滑块值变化事件
            var maxUploadsSlider = this.FindControl<Slider>("MaxUploadsSlider");
            var maxDownloadsSlider = this.FindControl<Slider>("MaxDownloadsSlider");
            var maxUploadsValueText = this.FindControl<TextBlock>("MaxUploadsValueText");
            var maxDownloadsValueText = this.FindControl<TextBlock>("MaxDownloadsValueText");

            if (maxUploadsSlider != null && maxUploadsValueText != null)
            {
                maxUploadsSlider.ValueChanged += (s, e) =>
                {
                    var value = (int)maxUploadsSlider.Value;
                    maxUploadsValueText.Text = value.ToString();
                    _settings.MaxConcurrentUploads = value;
                };
            }

            if (maxDownloadsSlider != null && maxDownloadsValueText != null)
            {
                maxDownloadsSlider.ValueChanged += (s, e) =>
                {
                    var value = (int)maxDownloadsSlider.Value;
                    maxDownloadsValueText.Text = value.ToString();
                    _settings.MaxConcurrentDownloads = value;
                };
            }

            // 绑定文本框变化事件
            var serverAddressTextBox = this.FindControl<TextBox>("ServerAddressTextBox");
            if (serverAddressTextBox != null)
            {
                serverAddressTextBox.TextChanged += (s, e) =>
                {
                    _settings.ServerAddress = serverAddressTextBox.Text ?? "";
                };
            }

            // 绑定复选框变化事件
            var autoStartCheckBox = this.FindControl<CheckBox>("AutoStartConversionCheckBox");
            var showNotificationsCheckBox = this.FindControl<CheckBox>("ShowNotificationsCheckBox");

            if (autoStartCheckBox != null)
            {
                autoStartCheckBox.IsCheckedChanged += (s, e) =>
                {
                    _settings.AutoStartConversion = autoStartCheckBox.IsChecked ?? false;
                };
            }

            if (showNotificationsCheckBox != null)
            {
                showNotificationsCheckBox.IsCheckedChanged += (s, e) =>
                {
                    _settings.ShowNotifications = showNotificationsCheckBox.IsChecked ?? false;
                };
            }

            var defaultOutputPathTextBox = this.FindControl<TextBox>("DefaultOutputPathTextBox");
            if (defaultOutputPathTextBox != null)
            {
                defaultOutputPathTextBox.TextChanged += (s, e) =>
                {
                    _settings.DefaultOutputPath = defaultOutputPathTextBox.Text ?? "";
                };
            }
        }

        private void LoadSettingsToUI()
        {
            // 加载服务器地址
            var serverAddressTextBox = this.FindControl<TextBox>("ServerAddressTextBox");
            if (serverAddressTextBox != null)
            {
                serverAddressTextBox.Text = _settings.ServerAddress;
            }

            // 加载并发设置
            var maxUploadsSlider = this.FindControl<Slider>("MaxUploadsSlider");
            var maxDownloadsSlider = this.FindControl<Slider>("MaxDownloadsSlider");
            var maxUploadsValueText = this.FindControl<TextBlock>("MaxUploadsValueText");
            var maxDownloadsValueText = this.FindControl<TextBlock>("MaxDownloadsValueText");

            if (maxUploadsSlider != null && maxUploadsValueText != null)
            {
                maxUploadsSlider.Value = _settings.MaxConcurrentUploads;
                maxUploadsValueText.Text = _settings.MaxConcurrentUploads.ToString();
            }

            if (maxDownloadsSlider != null && maxDownloadsValueText != null)
            {
                maxDownloadsSlider.Value = _settings.MaxConcurrentDownloads;
                maxDownloadsValueText.Text = _settings.MaxConcurrentDownloads.ToString();
            }

            // 加载其他设置
            var autoStartCheckBox = this.FindControl<CheckBox>("AutoStartConversionCheckBox");
            var showNotificationsCheckBox = this.FindControl<CheckBox>("ShowNotificationsCheckBox");
            var defaultOutputPathTextBox = this.FindControl<TextBox>("DefaultOutputPathTextBox");

            if (autoStartCheckBox != null)
            {
                autoStartCheckBox.IsChecked = _settings.AutoStartConversion;
            }

            if (showNotificationsCheckBox != null)
            {
                showNotificationsCheckBox.IsChecked = _settings.ShowNotifications;
            }

            if (defaultOutputPathTextBox != null)
            {
                defaultOutputPathTextBox.Text = _settings.DefaultOutputPath;
            }
        }

        private async void TestConnectionBtn_Click(object? sender, RoutedEventArgs e)
        {
            var testBtn = sender as Button;
            var statusPanel = this.FindControl<StackPanel>("ConnectionStatusPanel");
            var statusIndicator = this.FindControl<Border>("ConnectionStatusIndicator");
            var statusText = this.FindControl<TextBlock>("ConnectionStatusText");

            if (testBtn == null || statusPanel == null || statusIndicator == null || statusText == null)
                return;

            try
            {
                // 显示测试中状态
                statusPanel.IsVisible = true;
                statusIndicator.Background = Avalonia.Media.Brushes.Orange;
                statusText.Text = "正在测试连接...";
                testBtn.IsEnabled = false;
                testBtn.Content = "测试中...";

                // 创建临时API服务进行测试
                _apiService?.Dispose();
                _apiService = new ApiService { BaseUrl = _settings.ServerAddress };

                var isConnected = await _apiService.TestConnectionAsync();

                if (isConnected)
                {
                    statusIndicator.Background = Avalonia.Media.Brushes.Green;
                    statusText.Text = "连接成功";
                }
                else
                {
                    statusIndicator.Background = Avalonia.Media.Brushes.Red;
                    statusText.Text = "连接失败";
                }
            }
            catch (Exception ex)
            {
                statusIndicator.Background = Avalonia.Media.Brushes.Red;
                statusText.Text = $"连接错误: {ex.Message}";
            }
            finally
            {
                testBtn.IsEnabled = true;
                testBtn.Content = "测试连接";
            }
        }

        private async void BrowseOutputPathBtn_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel == null) return;

                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "选择默认输出文件夹",
                    AllowMultiple = false
                });

                if (folders.Count > 0)
                {
                    var selectedPath = folders[0].Path.LocalPath;
                    var defaultOutputPathTextBox = this.FindControl<TextBox>("DefaultOutputPathTextBox");
                    if (defaultOutputPathTextBox != null)
                    {
                        defaultOutputPathTextBox.Text = selectedPath;
                        _settings.DefaultOutputPath = selectedPath;
                    }
                }
            }
            catch (Exception ex)
            {
                // 显示错误消息
                System.Diagnostics.Debug.WriteLine($"选择文件夹失败: {ex.Message}");
            }
        }

        private void ResetBtn_Click(object? sender, RoutedEventArgs e)
        {
            _settings.ResetToDefaults();
            LoadSettingsToUI();
        }

        private void CancelBtn_Click(object? sender, RoutedEventArgs e)
        {
            SettingsChanged = false;
            Close();
        }

        private async void SaveBtn_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                // 验证设置
                if (!_settings.IsValidServerAddress())
                {
                    await MessageBoxService.ShowErrorAsync("服务器地址格式不正确，请输入有效的HTTP或HTTPS地址。", this);
                    return;
                }

                // 保存设置
                _settings.SaveSettings();
                SettingsChanged = true;
                Close();
            }
            catch (Exception ex)
            {
                await MessageBoxService.ShowErrorAsync($"保存设置失败: {ex.Message}", this);
            }
        }



        private void LoadDatabaseInfo()
        {
            try
            {
                var settingsService = Services.SystemSettingsService.Instance;
                var dbInfo = settingsService.GetDatabaseInfo();

                var databasePathTextBox = this.FindControl<TextBox>("DatabasePathTextBox");
                var databaseStatusIndicator = this.FindControl<Border>("DatabaseStatusIndicator");
                var databaseStatusText = this.FindControl<TextBlock>("DatabaseStatusText");
                var databaseSizeText = this.FindControl<TextBlock>("DatabaseSizeText");

                if (databasePathTextBox != null)
                {
                    databasePathTextBox.Text = dbInfo.DatabasePath;
                }

                if (databaseStatusIndicator != null && databaseStatusText != null)
                {
                    if (dbInfo.IsConnected)
                    {
                        databaseStatusIndicator.Background = Avalonia.Media.Brushes.Green;
                        databaseStatusText.Text = "连接正常";
                    }
                    else
                    {
                        databaseStatusIndicator.Background = Avalonia.Media.Brushes.Red;
                        databaseStatusText.Text = "连接失败";
                    }
                }

                if (databaseSizeText != null)
                {
                    databaseSizeText.Text = $"({dbInfo.GetFormattedSize()})";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载数据库信息失败: {ex.Message}");
            }
        }

        private async void OpenDatabaseFolderBtn_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var settingsService = Services.SystemSettingsService.Instance;
                var dbInfo = settingsService.GetDatabaseInfo();
                var folderPath = Path.GetDirectoryName(dbInfo.DatabasePath);

                if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = folderPath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    await MessageBoxService.ShowErrorAsync("数据库文件夹不存在", this);
                }
            }
            catch (Exception ex)
            {
                await MessageBoxService.ShowErrorAsync($"打开数据库文件夹失败: {ex.Message}", this);
            }
        }

        private async void BackupDatabaseBtn_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel == null) return;

                var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "备份数据库",
                    DefaultExtension = "db",
                    SuggestedFileName = $"VideoConversion_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.db",
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType("数据库文件") { Patterns = new[] { "*.db" } },
                        new FilePickerFileType("所有文件") { Patterns = new[] { "*" } }
                    }
                });

                if (file != null)
                {
                    var settingsService = Services.SystemSettingsService.Instance;
                    var success = settingsService.BackupSettings(file.Path.LocalPath);

                    if (success)
                    {
                        await MessageBoxService.ShowSuccessAsync("数据库备份成功！", this);
                    }
                    else
                    {
                        await MessageBoxService.ShowErrorAsync("数据库备份失败", this);
                    }
                }
            }
            catch (Exception ex)
            {
                await MessageBoxService.ShowErrorAsync($"备份数据库失败: {ex.Message}", this);
            }
        }

        private async void RestoreDatabaseBtn_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel == null) return;

                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "选择备份文件",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("数据库文件") { Patterns = new[] { "*.db" } },
                        new FilePickerFileType("所有文件") { Patterns = new[] { "*" } }
                    }
                });

                if (files.Count > 0)
                {
                    var settingsService = Services.SystemSettingsService.Instance;
                    var success = settingsService.RestoreSettings(files[0].Path.LocalPath);

                    if (success)
                    {
                        await MessageBoxService.ShowSuccessAsync("数据库恢复成功！设置已重新加载。", this);

                        // 重新加载设置到UI
                        _settings = settingsService.CurrentSettings.Clone();
                        LoadSettingsToUI();
                        LoadDatabaseInfo();
                    }
                    else
                    {
                        await MessageBoxService.ShowErrorAsync("数据库恢复失败", this);
                    }
                }
            }
            catch (Exception ex)
            {
                await MessageBoxService.ShowErrorAsync($"恢复数据库失败: {ex.Message}", this);
            }
        }

        #region 系统管理按钮事件

        private async void RefreshSystemInfoBtn_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (_apiService == null)
                {
                    var settingsService = Services.SystemSettingsService.Instance;
                    _apiService = new Services.ApiService { BaseUrl = settingsService.GetServerAddress() };
                }

                // 从服务器获取系统信息
                var response = await _apiService.GetSystemStatusAsync();

                if (response.Success && response.Data != null)
                {
                    // 更新UI显示
                    var serverVersionText = this.FindControl<TextBlock>("ServerVersionText");
                    var ffmpegVersionText = this.FindControl<TextBlock>("FFmpegVersionText");
                    var hardwareAccelText = this.FindControl<TextBlock>("HardwareAccelText");
                    var uptimeText = this.FindControl<TextBlock>("UptimeText");

                    if (serverVersionText != null)
                        serverVersionText.Text = response.Data.ServerVersion;

                    if (ffmpegVersionText != null)
                        ffmpegVersionText.Text = response.Data.FFmpegVersion;

                    if (hardwareAccelText != null)
                        hardwareAccelText.Text = response.Data.HardwareAcceleration;

                    if (uptimeText != null)
                        uptimeText.Text = FormatUptime(response.Data.Uptime);

                    await MessageBoxService.ShowSuccessAsync("系统信息已刷新", this);
                }
                else
                {
                    await MessageBoxService.ShowErrorAsync($"获取系统信息失败: {response.Message}", this);
                }
            }
            catch (Exception ex)
            {
                await MessageBoxService.ShowErrorAsync($"刷新系统信息失败: {ex.Message}", this);
            }
        }

        private async void ConfigSpaceBtn_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var settingsService = Services.SystemSettingsService.Instance;
                var baseUrl = settingsService.GetServerAddress();
                var configDialog = new Views.DiskSpaceConfigDialog(baseUrl);

                await configDialog.ShowDialog(this);

                if (configDialog.ConfigSaved)
                {
                    await MessageBoxService.ShowSuccessAsync("磁盘空间配置已保存", this);
                }
            }
            catch (Exception ex)
            {
                await MessageBoxService.ShowErrorAsync($"打开空间配置失败: {ex.Message}", this);
            }
        }

        private async void CleanupFilesBtn_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                // 确认对话框
                var result = await MessageBoxService.ShowConfirmAsync(
                    "确定要清理临时文件吗？\n\n这将删除所有临时文件和缓存，释放磁盘空间。",
                    this);

                if (!result) return;

                if (_apiService == null)
                {
                    var settingsService = Services.SystemSettingsService.Instance;
                    _apiService = new Services.ApiService { BaseUrl = settingsService.GetServerAddress() };
                }

                // 执行文件清理
                var response = await _apiService.CleanupFilesAsync("temp");

                if (response.Success && response.Data != null)
                {
                    var message = $"清理完成！\n\n" +
                                 $"删除文件数: {response.Data.DeletedFiles}\n" +
                                 $"释放空间: {FormatBytes(response.Data.FreedSpace)}";

                    await MessageBoxService.ShowSuccessAsync(message, this);
                }
                else
                {
                    await MessageBoxService.ShowErrorAsync($"文件清理失败: {response.Message}", this);
                }
            }
            catch (Exception ex)
            {
                await MessageBoxService.ShowErrorAsync($"文件清理失败: {ex.Message}", this);
            }
        }

        private async void ViewLogsBtn_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (_apiService == null)
                {
                    var settingsService = Services.SystemSettingsService.Instance;
                    _apiService = new Services.ApiService { BaseUrl = settingsService.GetServerAddress() };
                }

                // 获取系统诊断信息
                var response = await _apiService.GetSystemDiagnosticsAsync();

                if (response.Success && response.Data != null)
                {
                    var diagnosticsText = string.Join("\n",
                        response.Data.Select(d => $"[{d.Level.ToUpper()}] {d.Name}: {d.Status}"));

                    await MessageBoxService.ShowInfoAsync($"系统诊断信息:\n\n{diagnosticsText}", this);
                }
                else
                {
                    await MessageBoxService.ShowErrorAsync($"获取系统诊断信息失败: {response.Message}", this);
                }
            }
            catch (Exception ex)
            {
                await MessageBoxService.ShowErrorAsync($"获取系统诊断信息失败: {ex.Message}", this);
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 格式化运行时间
        /// </summary>
        private string FormatUptime(TimeSpan uptime)
        {
            if (uptime.TotalDays >= 1)
                return $"{(int)uptime.TotalDays}天 {uptime.Hours}小时";
            else if (uptime.TotalHours >= 1)
                return $"{uptime.Hours}小时 {uptime.Minutes}分钟";
            else
                return $"{uptime.Minutes}分钟";
        }

        /// <summary>
        /// 格式化字节大小
        /// </summary>
        private string FormatBytes(long bytes)
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

        protected override void OnClosed(EventArgs e)
        {
            _apiService?.Dispose();
            base.OnClosed(e);
        }
    }
}
