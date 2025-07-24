using Avalonia.Controls;
using Avalonia.Interactivity;
using VideoConversion_Client.Models;

namespace VideoConversion_Client.Views
{
    public partial class ConversionSettingsWindow : Window
    {
        public ConversionSettings? Settings { get; private set; }
        public bool DialogResult { get; private set; } = false;

        public ConversionSettingsWindow()
        {
            InitializeComponent();
            LoadCurrentSettings();
        }

        public ConversionSettingsWindow(ConversionSettings currentSettings) : this()
        {
            LoadSettings(currentSettings);
        }

        private void LoadCurrentSettings()
        {
            // 加载默认设置
            var defaultSettings = new ConversionSettings();
            LoadSettings(defaultSettings);
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
            Settings = GetCurrentSettings();
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object? sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
