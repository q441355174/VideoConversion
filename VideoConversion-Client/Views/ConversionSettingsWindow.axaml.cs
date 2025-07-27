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

        // UI控件引用将由Avalonia自动生成，无需手动声明

        public ConversionSettingsWindow()
        {
            InitializeComponent();

            // 获取全局转换设置服务实例
            _settingsService = Services.ConversionSettingsService.Instance;

            // 初始化控件可见性（默认CRF模式）
            InitializeControlVisibility();

            // 设置事件处理
            SetupEventHandlers();

            // 加载当前设置
            LoadCurrentSettings();
        }

        public ConversionSettingsWindow(ConversionSettings currentSettings) : this()
        {
            // 由于我们直接使用全局设置服务，这里不需要额外操作
            // currentSettings 参数保留是为了兼容性
        }



        private void InitializeControlVisibility()
        {
            // 默认显示CRF控件，隐藏比特率控件
            CrfQualityPanel.IsVisible = true;
            BitratePanel.IsVisible = false;

            // 查找并控制标签的显示
            var crfLabel = this.FindControl<TextBlock>("CrfLabel");
            var bitrateLabel = this.FindControl<TextBlock>("BitrateLabel");
            if (crfLabel != null) crfLabel.IsVisible = true;
            if (bitrateLabel != null) bitrateLabel.IsVisible = false;
        }

        private void SetupEventHandlers()
        {
            // 预设选择变化事件
            PresetCombo.SelectionChanged += PresetCombo_SelectionChanged;

            // 质量模式变化事件
            QualityModeCombo.SelectionChanged += QualityModeCombo_SelectionChanged;

            // CRF滑块变化事件
            CrfQualitySlider.ValueChanged += CrfQualitySlider_ValueChanged;

            // 音量滑块变化事件
            AudioVolumeSlider.ValueChanged += AudioVolumeSlider_ValueChanged;

            // 按钮事件
            SaveButton.Click += SaveButton_Click;
            CancelButton.Click += CancelButton_Click;
        }

        private void QualityModeCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (QualityModeCombo.SelectedItem is ComboBoxItem item)
            {
                // 使用Tag值来判断模式
                var mode = item.Tag?.ToString() ?? item.Content?.ToString();
                var isCrf = mode == "CRF";

                // 显示/隐藏相关控件
                CrfQualityPanel.IsVisible = isCrf;
                BitratePanel.IsVisible = !isCrf;

                // 查找并控制标签的显示
                var crfLabel = this.FindControl<TextBlock>("CrfLabel");
                var bitrateLabel = this.FindControl<TextBlock>("BitrateLabel");
                if (crfLabel != null) crfLabel.IsVisible = isCrf;
                if (bitrateLabel != null) bitrateLabel.IsVisible = !isCrf;
            }
        }

        private void CrfQualitySlider_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            CrfValueText.Text = ((int)e.NewValue).ToString();
        }

        private void AudioVolumeSlider_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            VolumeValueText.Text = $"{e.NewValue:F1} dB";
        }

        private void PresetCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (PresetCombo.SelectedItem is ComboBoxItem item)
            {
                var presetName = item.Content?.ToString();
                ApplyPreset(presetName);
            }
        }

        private void ApplyPreset(string? presetName)
        {
            if (string.IsNullOrEmpty(presetName)) return;

            ConversionSettings presetSettings;

            switch (presetName)
            {
                case "GPU Fast 1080p (NVENC)":
                    presetSettings = CreateNvencFastPreset();
                    break;
                case "GPU High Quality 1080p (NVENC)":
                    presetSettings = CreateNvencHighQualityPreset();
                    break;
                case "GPU 4K Ultra (NVENC)":
                    presetSettings = CreateNvenc4KPreset();
                    break;
                case "GPU Fast 1080p (QSV)":
                    presetSettings = CreateQsvFastPreset();
                    break;
                case "GPU High Quality 1080p (QSV)":
                    presetSettings = CreateQsvHighQualityPreset();
                    break;
                case "GPU Fast 1080p (AMF)":
                    presetSettings = CreateAmfFastPreset();
                    break;
                case "GPU High Quality 1080p (AMF)":
                    presetSettings = CreateAmfHighQualityPreset();
                    break;
                case "CPU Standard 1080p":
                    presetSettings = CreateCpuStandardPreset();
                    break;
                case "CPU High Quality 1080p":
                    presetSettings = CreateCpuHighQualityPreset();
                    break;
                case "Bitrate Mode Demo":
                    presetSettings = CreateBitratePreset();
                    break;
                default:
                    return; // 未知预设，不做任何操作
            }

            // 应用预设设置到UI
            LoadSettings(presetSettings);
        }

        private void LoadCurrentSettings()
        {
            // 从全局设置服务加载当前设置
            var currentSettings = _settingsService.CurrentSettings;

            // 调试输出：显示加载的设置值
            System.Diagnostics.Debug.WriteLine("=== 转码设置窗口加载设置 ===");
            System.Diagnostics.Debug.WriteLine($"OutputFormat: {currentSettings.OutputFormat}");
            System.Diagnostics.Debug.WriteLine($"Resolution: {currentSettings.Resolution}");
            System.Diagnostics.Debug.WriteLine($"VideoCodec: {currentSettings.VideoCodec}");
            System.Diagnostics.Debug.WriteLine($"EncodingPreset: {currentSettings.EncodingPreset}");
            System.Diagnostics.Debug.WriteLine($"QualityMode: {currentSettings.QualityMode}");
            System.Diagnostics.Debug.WriteLine($"VideoQuality: {currentSettings.VideoQuality}");
            System.Diagnostics.Debug.WriteLine($"FrameRate: {currentSettings.FrameRate}");
            System.Diagnostics.Debug.WriteLine($"AudioCodec: {currentSettings.AudioCodec}");
            System.Diagnostics.Debug.WriteLine($"AudioQuality: {currentSettings.AudioQuality}");
            System.Diagnostics.Debug.WriteLine($"SampleRate: {currentSettings.SampleRate}");
            System.Diagnostics.Debug.WriteLine($"HardwareAcceleration: {currentSettings.HardwareAcceleration}");

            LoadSettings(currentSettings);

            System.Diagnostics.Debug.WriteLine("转码设置窗口已完成设置加载");
        }

        private void LoadSettings(ConversionSettings settings)
        {
            // 基本设置
            SetComboBoxValue(OutputFormatCombo, settings.OutputFormat);
            SetComboBoxValue(ResolutionCombo, settings.Resolution);

            // 视频设置
            SetComboBoxValue(VideoCodecCombo, settings.VideoCodec);
            SetComboBoxValue(EncodingPresetCombo, settings.EncodingPreset);
            SetComboBoxValue(QualityModeCombo, settings.QualityMode);
            SetComboBoxValue(FrameRateCombo, settings.FrameRate);
            SetComboBoxValue(ProfileCombo, settings.Profile);

            // CRF质量值和视频比特率
            if (!string.IsNullOrEmpty(settings.VideoQuality))
            {
                if (int.TryParse(settings.VideoQuality, out int value))
                {
                    // 根据质量模式设置不同的控件
                    if (settings.QualityMode == "CRF")
                    {
                        CrfQualitySlider.Value = value;
                    }
                    else if (settings.QualityMode == "Bitrate")
                    {
                        VideoBitrateInput.Value = value;
                    }
                    else
                    {
                        // 默认设置CRF
                        CrfQualitySlider.Value = value;
                    }
                }
            }

            // 音频设置
            SetComboBoxValue(AudioCodecCombo, settings.AudioCodec);
            SetComboBoxValue(AudioQualityModeCombo, settings.AudioQualityMode);
            SetComboBoxValue(AudioQualityCombo, settings.AudioQuality);
            SetComboBoxValue(AudioChannelsCombo, settings.AudioChannels);
            SetComboBoxValue(SampleRateCombo, settings.SampleRate);

            // 音量调整
            if (!string.IsNullOrEmpty(settings.AudioVolume))
            {
                if (double.TryParse(settings.AudioVolume, out double volumeValue))
                {
                    AudioVolumeSlider.Value = volumeValue;
                }
            }

            // 高级设置
            SetComboBoxValue(HardwareAccelerationCombo, settings.HardwareAcceleration);
            SetComboBoxValue(PixelFormatCombo, settings.PixelFormat);
            SetComboBoxValue(ColorSpaceCombo, settings.ColorSpace);

            // 复选框设置
            FastStartCheckBox.IsChecked = settings.FastStart;
            DeinterlaceCheckBox.IsChecked = settings.Deinterlace;
            SetComboBoxValue(TwoPassCombo, settings.TwoPass ? "true" : "false");

            // 滤镜设置
            SetComboBoxValue(DenoiseCombo, settings.Denoise);
            VideoFiltersTextBox.Text = settings.VideoFilters ?? "";
            AudioFiltersTextBox.Text = settings.AudioFilters ?? "";

            // 触发质量模式变化事件以显示正确的面板
            QualityModeCombo_SelectionChanged(null, null!);
        }

        private void SetComboBoxValue(ComboBox? comboBox, string? value)
        {
            if (comboBox == null || string.IsNullOrEmpty(value))
            {
                System.Diagnostics.Debug.WriteLine($"SetComboBoxValue: ComboBox为null或value为空 - {comboBox?.Name}, value: {value}");
                return;
            }

            bool found = false;
            foreach (ComboBoxItem item in comboBox.Items)
            {
                // 优先匹配Tag，如果没有Tag则匹配Content
                var tagValue = item.Tag?.ToString();
                var contentValue = item.Content?.ToString();

                if (tagValue == value || (string.IsNullOrEmpty(tagValue) && contentValue == value))
                {
                    comboBox.SelectedItem = item;
                    found = true;
                    System.Diagnostics.Debug.WriteLine($"SetComboBoxValue: 成功设置 {comboBox.Name} = {value} (匹配: {(tagValue == value ? "Tag" : "Content")})");
                    break;
                }
            }

            if (!found)
            {
                System.Diagnostics.Debug.WriteLine($"SetComboBoxValue: 未找到匹配项 {comboBox.Name} = {value}");
                // 列出所有可用选项
                System.Diagnostics.Debug.WriteLine($"可用选项:");
                foreach (ComboBoxItem item in comboBox.Items)
                {
                    System.Diagnostics.Debug.WriteLine($"  - Content: {item.Content}, Tag: {item.Tag}");
                }
            }
        }

        private string GetComboBoxValue(ComboBox? comboBox)
        {
            if (comboBox?.SelectedItem is ComboBoxItem item)
            {
                // 优先返回Tag值，如果没有Tag则返回Content
                var tagValue = item.Tag?.ToString();
                if (!string.IsNullOrEmpty(tagValue))
                {
                    return tagValue;
                }
                return item.Content?.ToString() ?? "自动";
            }
            return "自动";
        }

        private string GetVideoQualityValue()
        {
            var qualityMode = GetComboBoxValue(QualityModeCombo);

            if (qualityMode == "CRF")
            {
                return ((int)CrfQualitySlider.Value).ToString();
            }
            else if (qualityMode == "Bitrate")
            {
                return ((int)VideoBitrateInput.Value).ToString();
            }
            else
            {
                // 默认返回CRF值
                return ((int)CrfQualitySlider.Value).ToString();
            }
        }

        private ConversionSettings GetCurrentSettings()
        {
            return new ConversionSettings
            {
                // 基本设置
                OutputFormat = GetComboBoxValue(OutputFormatCombo),
                Resolution = GetComboBoxValue(ResolutionCombo),

                // 视频设置
                VideoCodec = GetComboBoxValue(VideoCodecCombo),
                EncodingPreset = GetComboBoxValue(EncodingPresetCombo),
                QualityMode = GetComboBoxValue(QualityModeCombo),
                FrameRate = GetComboBoxValue(FrameRateCombo),
                VideoQuality = GetVideoQualityValue(),
                Profile = GetComboBoxValue(ProfileCombo),

                // 音频设置
                AudioCodec = GetComboBoxValue(AudioCodecCombo),
                AudioQualityMode = GetComboBoxValue(AudioQualityModeCombo),
                AudioQuality = GetComboBoxValue(AudioQualityCombo),
                AudioChannels = GetComboBoxValue(AudioChannelsCombo),
                SampleRate = GetComboBoxValue(SampleRateCombo),
                AudioVolume = AudioVolumeSlider.Value.ToString(),

                // 高级设置
                HardwareAcceleration = GetComboBoxValue(HardwareAccelerationCombo),
                PixelFormat = GetComboBoxValue(PixelFormatCombo),
                ColorSpace = GetComboBoxValue(ColorSpaceCombo),
                FastStart = FastStartCheckBox.IsChecked ?? true,
                Deinterlace = DeinterlaceCheckBox.IsChecked ?? false,
                TwoPass = GetComboBoxValue(TwoPassCombo) == "true",

                // 滤镜设置
                Denoise = GetComboBoxValue(DenoiseCombo),
                VideoFilters = VideoFiltersTextBox.Text,
                AudioFilters = AudioFiltersTextBox.Text,

                // 任务设置
                Priority = 0,
                MaxRetries = 3
            };
        }

        private void SaveButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                // 获取当前UI中的设置
                var newSettings = GetCurrentSettings();

                // 直接更新全局设置服务
                _settingsService.UpdateSettings(newSettings);

                SettingsChanged = true;

                System.Diagnostics.Debug.WriteLine($"转码设置已保存到全局服务: {newSettings.VideoCodec}, {newSettings.Resolution}");

                Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存转码设置失败: {ex.Message}");
                // 可以在这里显示错误消息给用户
            }
        }

        private void CancelButton_Click(object? sender, RoutedEventArgs e)
        {
            SettingsChanged = false;
            Close();
        }

        #region 预设创建方法

        private ConversionSettings CreateNvencFastPreset()
        {
            return new ConversionSettings
            {
                OutputFormat = "mp4",
                Resolution = "1080p (1920x1080)",
                VideoCodec = "H.264 (NVIDIA)",
                FrameRate = "保持原始",
                QualityMode = "恒定质量 (CRF)",
                VideoQuality = "23",
                EncodingPreset = "快",
                Profile = "High",
                AudioCodec = "AAC (推荐)",
                AudioQuality = "128 kbps (标准)",
                AudioChannels = "保持原始",
                SampleRate = "48 kHz (DVD质量)",
                AudioVolume = "0",
                HardwareAcceleration = "NVIDIA NVENC",
                PixelFormat = "YUV420P (标准)",
                ColorSpace = "BT.709 (HD)",
                FastStart = true,
                Deinterlace = false,
                TwoPass = false,
                Denoise = "无",
                VideoFilters = "",
                AudioFilters = "",
                Priority = 0,
                MaxRetries = 3
            };
        }

        private ConversionSettings CreateNvencHighQualityPreset()
        {
            return new ConversionSettings
            {
                OutputFormat = "mp4",
                Resolution = "1080p (1920x1080)",
                VideoCodec = "H.264 (NVIDIA)",
                FrameRate = "保持原始",
                QualityMode = "恒定质量 (CRF)",
                VideoQuality = "18",
                EncodingPreset = "慢",
                Profile = "High",
                AudioCodec = "AAC (推荐)",
                AudioQuality = "256 kbps (很高质量)",
                AudioChannels = "保持原始",
                SampleRate = "48 kHz (DVD质量)",
                AudioVolume = "0",
                HardwareAcceleration = "NVIDIA NVENC",
                PixelFormat = "YUV420P (标准)",
                ColorSpace = "BT.709 (HD)",
                FastStart = true,
                Deinterlace = false,
                TwoPass = false,
                Denoise = "高质量降噪",
                VideoFilters = "",
                AudioFilters = "",
                Priority = 0,
                MaxRetries = 3
            };
        }

        private ConversionSettings CreateNvenc4KPreset()
        {
            return new ConversionSettings
            {
                OutputFormat = "mp4",
                Resolution = "4K (3840x2160)",
                VideoCodec = "H.265/HEVC (NVIDIA)",
                FrameRate = "保持原始",
                QualityMode = "恒定质量 (CRF)",
                VideoQuality = "20",
                EncodingPreset = "中等 (推荐)",
                Profile = "Main",
                AudioCodec = "AAC (推荐)",
                AudioQuality = "320 kbps (最高质量)",
                AudioChannels = "保持原始",
                SampleRate = "48 kHz (DVD质量)",
                AudioVolume = "0",
                HardwareAcceleration = "NVIDIA NVENC",
                PixelFormat = "YUV420P 10-bit",
                ColorSpace = "BT.2020 (4K HDR)",
                FastStart = true,
                Deinterlace = false,
                TwoPass = false,
                Denoise = "BM3D降噪 (最佳)",
                VideoFilters = "",
                AudioFilters = "",
                Priority = 0,
                MaxRetries = 3
            };
        }

        private ConversionSettings CreateQsvFastPreset()
        {
            return new ConversionSettings
            {
                OutputFormat = "mp4",
                Resolution = "1080p (1920x1080)",
                VideoCodec = "H.264 (Intel QSV)",
                FrameRate = "保持原始",
                QualityMode = "恒定质量 (CRF)",
                VideoQuality = "23",
                EncodingPreset = "快",
                Profile = "High",
                AudioCodec = "AAC (推荐)",
                AudioQuality = "128 kbps (标准)",
                AudioChannels = "保持原始",
                SampleRate = "48 kHz (DVD质量)",
                AudioVolume = "0",
                HardwareAcceleration = "Intel Quick Sync",
                PixelFormat = "YUV420P (标准)",
                ColorSpace = "BT.709 (HD)",
                FastStart = true,
                Deinterlace = false,
                TwoPass = false,
                Denoise = "无",
                VideoFilters = "",
                AudioFilters = "",
                Priority = 0,
                MaxRetries = 3
            };
        }

        private ConversionSettings CreateQsvHighQualityPreset()
        {
            return new ConversionSettings
            {
                OutputFormat = "mp4",
                Resolution = "1080p (1920x1080)",
                VideoCodec = "H.264 (Intel QSV)",
                FrameRate = "保持原始",
                QualityMode = "恒定质量 (CRF)",
                VideoQuality = "18",
                EncodingPreset = "慢",
                Profile = "High",
                AudioCodec = "AAC (推荐)",
                AudioQuality = "256 kbps (很高质量)",
                AudioChannels = "保持原始",
                SampleRate = "48 kHz (DVD质量)",
                AudioVolume = "0",
                HardwareAcceleration = "Intel Quick Sync",
                PixelFormat = "YUV420P (标准)",
                ColorSpace = "BT.709 (HD)",
                FastStart = true,
                Deinterlace = false,
                TwoPass = false,
                Denoise = "高质量降噪",
                VideoFilters = "",
                AudioFilters = "",
                Priority = 0,
                MaxRetries = 3
            };
        }

        private ConversionSettings CreateAmfFastPreset()
        {
            return new ConversionSettings
            {
                OutputFormat = "mp4",
                Resolution = "1080p (1920x1080)",
                VideoCodec = "H.264 (AMD AMF)",
                FrameRate = "保持原始",
                QualityMode = "恒定质量 (CRF)",
                VideoQuality = "23",
                EncodingPreset = "快",
                Profile = "High",
                AudioCodec = "AAC (推荐)",
                AudioQuality = "128 kbps (标准)",
                AudioChannels = "保持原始",
                SampleRate = "48 kHz (DVD质量)",
                AudioVolume = "0",
                HardwareAcceleration = "AMD AMF",
                PixelFormat = "YUV420P (标准)",
                ColorSpace = "BT.709 (HD)",
                FastStart = true,
                Deinterlace = false,
                TwoPass = false,
                Denoise = "无",
                VideoFilters = "",
                AudioFilters = "",
                Priority = 0,
                MaxRetries = 3
            };
        }

        private ConversionSettings CreateAmfHighQualityPreset()
        {
            return new ConversionSettings
            {
                OutputFormat = "mp4",
                Resolution = "1080p (1920x1080)",
                VideoCodec = "H.264 (AMD AMF)",
                FrameRate = "保持原始",
                QualityMode = "恒定质量 (CRF)",
                VideoQuality = "18",
                EncodingPreset = "慢",
                Profile = "High",
                AudioCodec = "AAC (推荐)",
                AudioQuality = "256 kbps (很高质量)",
                AudioChannels = "保持原始",
                SampleRate = "48 kHz (DVD质量)",
                AudioVolume = "0",
                HardwareAcceleration = "AMD AMF",
                PixelFormat = "YUV420P (标准)",
                ColorSpace = "BT.709 (HD)",
                FastStart = true,
                Deinterlace = false,
                TwoPass = false,
                Denoise = "高质量降噪",
                VideoFilters = "",
                AudioFilters = "",
                Priority = 0,
                MaxRetries = 3
            };
        }

        private ConversionSettings CreateCpuStandardPreset()
        {
            return new ConversionSettings
            {
                OutputFormat = "mp4",
                Resolution = "原始",
                VideoCodec = "libx264",
                FrameRate = "原始",
                QualityMode = "CRF",
                VideoQuality = "23",
                EncodingPreset = "medium",
                Profile = "auto",
                AudioCodec = "aac",
                AudioQuality = "192",
                AudioChannels = "原始",
                SampleRate = "48000",
                AudioVolume = "0",
                HardwareAcceleration = "auto",
                PixelFormat = "auto",
                ColorSpace = "auto",
                FastStart = true,
                Deinterlace = false,
                TwoPass = false,
                Denoise = "none",
                VideoFilters = "",
                AudioFilters = "",
                Priority = 0,
                MaxRetries = 3
            };
        }

        private ConversionSettings CreateCpuHighQualityPreset()
        {
            return new ConversionSettings
            {
                OutputFormat = "mp4",
                Resolution = "保持原始",
                VideoCodec = "H.264 (CPU)",
                FrameRate = "保持原始",
                QualityMode = "恒定质量 (CRF)",
                VideoQuality = "18",
                EncodingPreset = "很慢 (最高质量)",
                Profile = "High",
                AudioCodec = "AAC (推荐)",
                AudioQuality = "320 kbps (最高质量)",
                AudioChannels = "保持原始",
                SampleRate = "48 kHz (DVD质量)",
                AudioVolume = "0",
                HardwareAcceleration = "自动检测",
                PixelFormat = "YUV420P (标准)",
                ColorSpace = "BT.709 (HD)",
                FastStart = true,
                Deinterlace = false,
                TwoPass = true,
                Denoise = "BM3D降噪 (最佳)",
                VideoFilters = "",
                AudioFilters = "",
                Priority = 0,
                MaxRetries = 3
            };
        }

        private ConversionSettings CreateBitratePreset()
        {
            return new ConversionSettings
            {
                OutputFormat = "mp4",
                Resolution = "1920x1080",
                VideoCodec = "libx264",
                FrameRate = "30",
                QualityMode = "Bitrate",
                VideoQuality = "5000", // 5000 kbps
                EncodingPreset = "medium",
                Profile = "high",
                AudioCodec = "aac",
                AudioQuality = "192",
                AudioChannels = "2",
                SampleRate = "48000",
                AudioVolume = "0",
                HardwareAcceleration = "none",
                PixelFormat = "yuv420p",
                ColorSpace = "bt709",
                FastStart = true,
                Deinterlace = false,
                TwoPass = false,
                Denoise = "none",
                VideoFilters = "",
                AudioFilters = "",
                Priority = 0,
                MaxRetries = 3
            };
        }

        #endregion
    }
}
