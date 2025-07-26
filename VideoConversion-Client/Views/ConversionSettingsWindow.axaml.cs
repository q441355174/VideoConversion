using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using VideoConversion_Client.Models;

namespace VideoConversion_Client.Views
{
    public partial class ConversionSettingsWindow : Window
    {
        private readonly Services.ConversionSettingsService _settingsService;
        public bool SettingsChanged { get; private set; } = false;

        public ConversionSettingsWindow()
        {
            InitializeComponent();

            // 获取全局转换设置服务实例
            _settingsService = Services.ConversionSettingsService.Instance;

            // 加载当前设置
            LoadCurrentSettings();
        }

        public ConversionSettingsWindow(ConversionSettings currentSettings) : this()
        {
            // 由于我们直接使用全局设置服务，这里不需要额外操作
            // currentSettings 参数保留是为了兼容性
        }

        private void LoadCurrentSettings()
        {
            // 从全局设置服务加载当前设置
            var currentSettings = _settingsService.CurrentSettings;
            LoadSettings(currentSettings);

            System.Diagnostics.Debug.WriteLine($"转码设置窗口已加载当前设置: {currentSettings.VideoCodec}, {currentSettings.Resolution}");
        }

        private void LoadSettings(ConversionSettings settings)
        {
            // 设置视频编码器
            SetComboBoxValue(VideoCodecCombo, settings.VideoCodec);

            // 设置分辨率
            SetComboBoxValue(ResolutionCombo, settings.Resolution);

            // 设置帧率
            SetComboBoxValue(FrameRateCombo, settings.FrameRate);

            // 设置码率
            SetComboBoxValue(BitrateCombo, settings.Bitrate);

            // 设置音频编码器
            SetComboBoxValue(AudioCodecCombo, settings.AudioCodec); 

            // 设置音频比特率
            SetComboBoxValue(AudioBitrateCombo, settings.AudioQuality);

            // 设置音频轨道
            SetComboBoxValue(AudioTrackCombo, "自动");

            // 设置采样率
            SetComboBoxValue(SampleRateCombo, "自动");
        }

        private void SetComboBoxValue(ComboBox? comboBox, string? value)
        {
            if (comboBox == null || string.IsNullOrEmpty(value))
                return;

            foreach (ComboBoxItem item in comboBox.Items)
            {
                if (item.Content?.ToString() == value)
                {
                    comboBox.SelectedItem = item;
                    break;
                }
            }
        }

        private string GetComboBoxValue(ComboBox? comboBox)
        {
            if (comboBox?.SelectedItem is ComboBoxItem item)
            {
                return item.Content?.ToString() ?? "自动";
            }
            return "自动";
        }

        private ConversionSettings GetCurrentSettings()
        {
            return new ConversionSettings
            {
                VideoCodec = GetComboBoxValue(VideoCodecCombo),
                Resolution = GetComboBoxValue(ResolutionCombo),
                FrameRate = GetComboBoxValue(FrameRateCombo),
                Bitrate = GetComboBoxValue(BitrateCombo),
                AudioCodec = GetComboBoxValue(AudioCodecCombo),
                AudioQuality = GetComboBoxValue(AudioBitrateCombo),
                HardwareAcceleration = "自动",
                Threads = "自动"
            };
        }

        private void ApplyButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                // 获取当前UI中的设置
                var newSettings = GetCurrentSettings();

                // 直接更新全局设置服务
                _settingsService.UpdateSettings(newSettings);

                SettingsChanged = true;

                System.Diagnostics.Debug.WriteLine($"转码设置已应用到全局服务: {newSettings.VideoCodec}, {newSettings.Resolution}");

                Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"应用转码设置失败: {ex.Message}");
            }
        }

        private void CancelButton_Click(object? sender, RoutedEventArgs e)
        {
            SettingsChanged = false;
            Close();
        }
    }
}
