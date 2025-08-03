using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VideoConversion_ClientTo.ViewModels;
using VideoConversion_ClientTo.Domain.Models;
using VideoConversion_ClientTo.Infrastructure.Services;
using VideoConversion_ClientTo.Infrastructure;

namespace VideoConversion_ClientTo.Presentation.ViewModels
{
    /// <summary>
    /// 转换设置视图模型 - 完全按照Client项目逻辑重构，使用现代MVVM架构
    /// 保持Client项目的所有逻辑、事件处理、数据流转，但使用ClientTo的新架构实现
    /// </summary>
    public partial class ConversionSettingsViewModel : ViewModelBase, IDisposable
    {
        #region 私有字段 - 与Client项目一致

        private readonly ConversionSettingsService _settingsService;
        private ConversionSettings _currentSettings;

        #endregion

        #region 可观察属性 - 与Client项目UI控件完全对应

        // 🔑 预设选择 - 对应Client项目PresetCombo
        [ObservableProperty]
        private int _selectedPresetIndex = -1;

        // 🔑 基本设置 - 对应Client项目基本设置区域
        [ObservableProperty]
        private string _selectedOutputFormat = "mp4";

        [ObservableProperty]
        private string _selectedResolution = "保持原始";

        // 🔑 视频设置 - 对应Client项目视频设置区域
        [ObservableProperty]
        private string _selectedVideoCodec = "H.264 (CPU)";

        [ObservableProperty]
        private string _selectedFrameRate = "保持原始";

        [ObservableProperty]
        private string _selectedQualityMode = "恒定质量 (CRF)";

        [ObservableProperty]
        private double _crfQuality = 23; // CRF滑块值

        [ObservableProperty]
        private int _videoBitrate = 5000; // 视频比特率

        [ObservableProperty]
        private string _selectedEncodingPreset = "medium";

        [ObservableProperty]
        private string _selectedProfile = "auto";

        // 🔑 音频设置 - 对应Client项目音频设置区域
        [ObservableProperty]
        private string _selectedAudioCodec = "aac";

        [ObservableProperty]
        private string _selectedAudioQuality = "192";

        [ObservableProperty]
        private string _selectedAudioChannels = "原始";

        [ObservableProperty]
        private string _selectedSampleRate = "48000";

        [ObservableProperty]
        private double _audioVolume = 0; // 音量滑块值

        // 🔑 高级设置 - 对应Client项目高级设置区域
        [ObservableProperty]
        private string _selectedHardwareAcceleration = "auto";

        [ObservableProperty]
        private string _selectedPixelFormat = "auto";

        [ObservableProperty]
        private string _selectedColorSpace = "auto";

        [ObservableProperty]
        private bool _fastStart = true;

        [ObservableProperty]
        private bool _deinterlace = false;

        [ObservableProperty]
        private bool _twoPass = false;

        // 🔑 滤镜设置 - 对应Client项目滤镜设置区域
        [ObservableProperty]
        private string _selectedDenoise = "none";

        [ObservableProperty]
        private string _videoFilters = "";

        [ObservableProperty]
        private string _audioFilters = "";

        // 🔑 控件可见性 - 对应Client项目QualityModeCombo_SelectionChanged逻辑
        [ObservableProperty]
        private bool _isCrfMode = true; // CRF面板可见性

        [ObservableProperty]
        private bool _isBitrateMode = false; // 比特率面板可见性

        // 🔑 状态属性
        [ObservableProperty]
        private bool _settingsChanged = false;

        /// <summary>
        /// 保存完成后的回调
        /// </summary>
        public Action? OnSaveCompleted { get; set; }

        /// <summary>
        /// 是否已初始化
        /// </summary>
        private bool _isInitialized = false;

        // 🔑 设置摘要 - 与Client项目SettingsSummary一致
        [ObservableProperty]
        private string _settingsSummary = "";

        #endregion

        #region 集合属性 - 与Client项目ComboBox选项完全一致

        /// <summary>
        /// 预设选项 - 与Client项目PresetCombo完全一致
        /// </summary>
        public ObservableCollection<string> Presets { get; }

        /// <summary>
        /// 输出格式选项 - 与Client项目OutputFormatCombo完全一致
        /// </summary>
        public ObservableCollection<string> OutputFormats { get; }

        /// <summary>
        /// 分辨率选项 - 与Client项目ResolutionCombo完全一致
        /// </summary>
        public ObservableCollection<string> Resolutions { get; }

        /// <summary>
        /// 视频编码器选项 - 与Client项目VideoCodecCombo完全一致
        /// </summary>
        public ObservableCollection<string> VideoCodecs { get; }

        /// <summary>
        /// 帧率选项 - 与Client项目FrameRateCombo完全一致
        /// </summary>
        public ObservableCollection<string> FrameRates { get; }

        /// <summary>
        /// 质量模式选项 - 与Client项目QualityModeCombo完全一致
        /// </summary>
        public ObservableCollection<string> QualityModes { get; }

        /// <summary>
        /// 编码预设选项 - 与Client项目EncodingPresetCombo完全一致
        /// </summary>
        public ObservableCollection<string> EncodingPresets { get; }

        /// <summary>
        /// 配置文件选项 - 与Client项目ProfileCombo完全一致
        /// </summary>
        public ObservableCollection<string> Profiles { get; }

        /// <summary>
        /// 音频编码器选项 - 与Client项目AudioCodecCombo完全一致
        /// </summary>
        public ObservableCollection<string> AudioCodecs { get; }

        /// <summary>
        /// 音频质量选项 - 与Client项目AudioQualityCombo完全一致
        /// </summary>
        public ObservableCollection<string> AudioQualities { get; }

        /// <summary>
        /// 音频声道选项 - 与Client项目AudioChannelsCombo完全一致
        /// </summary>
        public ObservableCollection<string> AudioChannels { get; }

        /// <summary>
        /// 采样率选项 - 与Client项目SampleRateCombo完全一致
        /// </summary>
        public ObservableCollection<string> SampleRates { get; }

        /// <summary>
        /// 硬件加速选项 - 与Client项目HardwareAccelerationCombo完全一致
        /// </summary>
        public ObservableCollection<string> HardwareAccelerations { get; }

        /// <summary>
        /// 像素格式选项 - 与Client项目PixelFormatCombo完全一致
        /// </summary>
        public ObservableCollection<string> PixelFormats { get; }

        /// <summary>
        /// 色彩空间选项 - 与Client项目ColorSpaceCombo完全一致
        /// </summary>
        public ObservableCollection<string> ColorSpaces { get; }

        /// <summary>
        /// 降噪选项 - 与Client项目DenoiseCombo完全一致
        /// </summary>
        public ObservableCollection<string> DenoiseOptions { get; }

        #endregion

        #region 构造函数 - 完全按照Client项目ConversionSettingsWindow()逻辑

        /// <summary>
        /// 构造函数 - 完全按照Client项目ConversionSettingsWindow()逻辑
        /// </summary>
        public ConversionSettingsViewModel()
        {
            // 🔑 获取全局转换设置服务实例 - 与Client项目完全一致
            _settingsService = ConversionSettingsService.Instance;

            // 🔑 初始化集合 - 使用ConversionOptions结构化选项替代硬编码
            Presets = new ObservableCollection<string>(ConversionOptions.GetPresetOptions());
            OutputFormats = new ObservableCollection<string>(ConversionOptions.GetOutputFormatDisplayNames());
            Resolutions = new ObservableCollection<string>(ConversionOptions.GetResolutionOptions());
            VideoCodecs = new ObservableCollection<string>(ConversionOptions.GetVideoCodecOptions());
            QualityModes = new ObservableCollection<string>(ConversionOptions.GetQualityModeOptions());
            FrameRates = new ObservableCollection<string>(GetClientFrameRateOptions());
            EncodingPresets = new ObservableCollection<string>(ConversionOptions.GetEncodingPresetOptions());
            Profiles = new ObservableCollection<string>(GetClientProfileOptions());
            AudioCodecs = new ObservableCollection<string>(ConversionOptions.GetAudioCodecOptions());
            AudioQualities = new ObservableCollection<string>(GetClientAudioQualityOptions());
            AudioChannels = new ObservableCollection<string>(GetClientAudioChannelOptions());
            SampleRates = new ObservableCollection<string>(GetClientSampleRateOptions());
            HardwareAccelerations = new ObservableCollection<string>(GetClientHardwareAccelOptions());
            PixelFormats = new ObservableCollection<string>(GetClientPixelFormatOptions());
            ColorSpaces = new ObservableCollection<string>(GetClientColorSpaceOptions());
            DenoiseOptions = new ObservableCollection<string>(GetClientDenoiseOptions());

            // 🔑 初始化控件可见性（默认CRF模式）- 与Client项目InitializeControlVisibility()一致
            InitializeControlVisibility();

            // 🔑 加载当前设置 - 与Client项目LoadCurrentSettings()一致
            LoadCurrentSettings();

            // 转换设置ViewModel初始化完成（移除日志）
        }

        #endregion

        #region 初始化方法 - 完全按照Client项目方法逻辑

        /// <summary>
        /// 初始化控件可见性（默认CRF模式）- 与Client项目InitializeControlVisibility()完全一致
        /// </summary>
        private void InitializeControlVisibility()
        {
            // 默认显示CRF控件，隐藏比特率控件 - 与Client项目一致
            IsCrfMode = true;
            IsBitrateMode = false;
        }

        /// <summary>
        /// 加载当前设置 - 与Client项目LoadCurrentSettings()完全一致
        /// </summary>
        private void LoadCurrentSettings()
        {
            // 🔧 延迟初始化设置，确保UI属性完全初始化后再加载
            _ = Task.Run(async () =>
            {
                try
                {
                    // 等待一小段时间确保UI完全初始化
                    await Task.Delay(50);

                    // 🔑 从全局设置服务加载当前设置 - 与Client项目一致
                    _currentSettings = await _settingsService.GetSettingsAsync();

                    // 🔧 在UI线程上加载设置
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        // 🔑 加载设置到UI - 与Client项目LoadSettings()一致
                        LoadSettings(_currentSettings);

                        // 🔑 更新设置摘要
                        UpdateSettingsSummary();

                        // 🔧 标记为已初始化
                        _isInitialized = true;

                        Utils.Logger.Debug("ConversionSettingsViewModel", "✅ 转码设置窗口已完成设置加载");
                    });
                }
                catch (Exception ex)
                {
                    Utils.Logger.Error("ConversionSettingsViewModel", $"❌ 加载当前设置失败: {ex.Message}");

                    // 🔧 加载失败时使用默认设置
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        LoadDefaultSettings();
                        _isInitialized = true;
                    });
                }
            });
        }

        /// <summary>
        /// 更新设置摘要 - 生成当前设置的简要描述（带防抖）
        /// </summary>
        private void UpdateSettingsSummary()
        {
            try
            {
                // 防抖处理 - 避免频繁更新
                _summaryUpdateTimer?.Stop();
                _summaryUpdateTimer = new System.Timers.Timer(300); // 300ms延迟
                _summaryUpdateTimer.Elapsed += (s, e) =>
                {
                    _summaryUpdateTimer?.Stop();

                    try
                    {
                        var qualityInfo = IsCrfMode ? $"CRF {CrfQuality}" : $"{VideoBitrate} kbps";
                        SettingsSummary = $"{SelectedVideoCodec} | {SelectedResolution} | {qualityInfo} | {SelectedAudioCodec}";
                    }
                    catch (Exception ex)
                    {
                        Utils.Logger.Error("ConversionSettingsViewModel", $"❌ 更新设置摘要失败: {ex.Message}");
                        SettingsSummary = "设置摘要生成失败";
                    }
                };
                _summaryUpdateTimer.Start();
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionSettingsViewModel", $"❌ 设置摘要更新定时器失败: {ex.Message}");
                // 降级到直接更新
                var qualityInfo = IsCrfMode ? $"CRF {CrfQuality}" : $"{VideoBitrate} kbps";
                SettingsSummary = $"{SelectedVideoCodec} | {SelectedResolution} | {qualityInfo} | {SelectedAudioCodec}";
            }
        }

        private System.Timers.Timer? _summaryUpdateTimer;

        /// <summary>
        /// 加载设置到UI - 完全按照Client项目LoadSettings()逻辑
        /// </summary>
        private void LoadSettings(ConversionSettings settings)
        {
            try
            {
                // 🔑 基本设置 - 与Client项目SetComboBoxValue()逻辑一致
                SelectedOutputFormat = SetComboBoxValue(OutputFormats, settings.OutputFormat);
                SelectedResolution = SetComboBoxValue(Resolutions, settings.Resolution);

                // 🔑 视频设置 - 与Client项目一致
                SelectedVideoCodec = SetComboBoxValue(VideoCodecs, settings.VideoCodec);
                SelectedEncodingPreset = SetComboBoxValue(EncodingPresets, settings.EncodingPreset);
                SelectedQualityMode = SetComboBoxValue(QualityModes, settings.QualityMode);
                SelectedFrameRate = SetComboBoxValue(FrameRates, settings.FrameRate);
                SelectedProfile = SetComboBoxValue(Profiles, settings.Profile);

                // 🔑 CRF质量值和视频比特率 - 与Client项目完全一致
                if (!string.IsNullOrEmpty(settings.VideoQuality))
                {
                    if (int.TryParse(settings.VideoQuality, out int value))
                    {
                        // 根据质量模式设置不同的控件 - 与Client项目逻辑一致
                        if (settings.QualityMode == "恒定质量 (CRF)")
                        {
                            CrfQuality = value;
                        }
                        else if (settings.QualityMode == "恒定比特率")
                        {
                            VideoBitrate = value;
                        }
                        else
                        {
                            // 默认设置CRF
                            CrfQuality = value;
                        }
                    }
                }

                // 🔑 音频设置 - 与Client项目一致
                SelectedAudioCodec = SetComboBoxValue(AudioCodecs, settings.AudioCodec);
                SelectedAudioQuality = SetComboBoxValue(AudioQualities, settings.AudioQuality);
                SelectedAudioChannels = SetComboBoxValue(AudioChannels, settings.AudioChannels);
                SelectedSampleRate = SetComboBoxValue(SampleRates, settings.SampleRate);

                // 🔑 音量调整 - 与Client项目一致
                if (!string.IsNullOrEmpty(settings.AudioVolume))
                {
                    if (double.TryParse(settings.AudioVolume, out double volumeValue))
                    {
                        AudioVolume = volumeValue;
                    }
                }

                // 🔑 高级设置 - 与Client项目一致
                SelectedHardwareAcceleration = SetComboBoxValue(HardwareAccelerations, settings.HardwareAcceleration);
                SelectedPixelFormat = SetComboBoxValue(PixelFormats, settings.PixelFormat);
                SelectedColorSpace = SetComboBoxValue(ColorSpaces, settings.ColorSpace);

                // 🔑 复选框设置 - 与Client项目一致
                FastStart = settings.FastStart;
                Deinterlace = settings.Deinterlace;
                TwoPass = settings.TwoPass;

                // 🔑 滤镜设置 - 与Client项目一致
                SelectedDenoise = SetComboBoxValue(DenoiseOptions, settings.Denoise);
                VideoFilters = settings.VideoFilters ?? "";
                AudioFilters = settings.AudioFilters ?? "";

                // 🔑 触发质量模式变化以显示正确的面板 - 与Client项目QualityModeCombo_SelectionChanged一致
                OnQualityModeChanged();

                Utils.Logger.Debug("ConversionSettingsViewModel", "✅ 设置已加载到UI");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionSettingsViewModel", $"❌ 加载设置到UI失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载默认设置 - 当从服务加载失败时使用
        /// </summary>
        private void LoadDefaultSettings()
        {
            try
            {
                Utils.Logger.Info("ConversionSettingsViewModel", "🔧 加载默认转码设置");

                var defaultSettings = new ConversionSettings
                {
                    // 基本设置 - 使用结构化选项的显示名称
                    OutputFormat = "MP4 (推荐)",
                    Resolution = "保持原始",

                    // 视频设置 - 使用结构化选项
                    VideoCodec = "H.264 (CPU)",
                    FrameRate = "保持原始",
                    QualityMode = "恒定质量 (CRF)",
                    VideoQuality = "23",
                    EncodingPreset = "中等 (推荐)",
                    Profile = "High",

                    // 音频设置 - 使用结构化选项
                    AudioCodec = "AAC (推荐)",
                    AudioChannels = "保持原始",
                    AudioQuality = "192 kbps (高质量)",
                    SampleRate = "48 kHz (DVD质量)",
                    AudioVolume = "0",

                    // 高级设置
                    HardwareAcceleration = "自动检测",
                    PixelFormat = "YUV420P (标准)",
                    ColorSpace = "BT.709 (HD)",
                    FastStart = true,
                    Deinterlace = false,
                    TwoPass = false,

                    // 滤镜设置
                    Denoise = "无",
                    VideoFilters = "",
                    AudioFilters = ""
                };

                LoadSettings(defaultSettings);
                Utils.Logger.Info("ConversionSettingsViewModel", "✅ 默认转码设置已加载");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionSettingsViewModel", $"❌ 加载默认设置失败: {ex.Message}");
            }
        }

        #endregion

        #region 命令 - 完全按照Client项目事件处理逻辑

        /// <summary>
        /// 保存设置命令 - 完全按照Client项目SaveButton_Click逻辑
        /// </summary>
        [RelayCommand]
        public async Task SaveSettingsAsync()
        {
            try
            {
                // 🔧 检查是否已初始化
                if (!_isInitialized)
                {
                    Utils.Logger.Warning("ConversionSettingsViewModel", "⚠️ 设置尚未初始化完成，无法保存");
                    return;
                }

                // 🔑 验证设置有效性
                if (!ValidateSettings())
                {
                    Utils.Logger.Warning("ConversionSettingsViewModel", "⚠️ 设置验证失败，无法保存");
                    return;
                }

                // 🔑 获取当前UI中的设置 - 与Client项目GetCurrentSettings()一致
                var newSettings = GetCurrentSettings();

                // 🔧 异步保存到设置服务
                await _settingsService.SaveSettingsAsync(newSettings);

                // 🔑 更新当前设置引用
                _currentSettings = newSettings;

                // 🔑 更新设置摘要
                UpdateSettingsSummary();

                SettingsChanged = true;

                Utils.Logger.Info("ConversionSettingsViewModel", $"✅ 转码设置已保存: {newSettings.VideoCodec}, {newSettings.Resolution}");

                // 🔧 保存完成后调用回调关闭窗口
                OnSaveCompleted?.Invoke();
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionSettingsViewModel", $"❌ 保存转码设置失败: {ex.Message}");
                // TODO: 显示错误消息给用户
            }
        }

        /// <summary>
        /// 验证设置有效性
        /// </summary>
        private bool ValidateSettings()
        {
            try
            {
                // 验证CRF质量值范围
                if (IsCrfMode && (CrfQuality < 0 || CrfQuality > 51))
                {
                    Utils.Logger.Warning("ConversionSettingsViewModel", "⚠️ CRF质量值必须在0-51之间");
                    return false;
                }

                // 验证视频比特率范围
                if (!IsCrfMode && (VideoBitrate < 100 || VideoBitrate > 50000))
                {
                    Utils.Logger.Warning("ConversionSettingsViewModel", "⚠️ 视频比特率必须在100-50000 kbps之间");
                    return false;
                }

                // 验证音量范围
                if (AudioVolume < -30 || AudioVolume > 30)
                {
                    Utils.Logger.Warning("ConversionSettingsViewModel", "⚠️ 音量调整必须在-30到+30 dB之间");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionSettingsViewModel", $"❌ 设置验证失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 取消命令 - 完全按照Client项目CancelButton_Click逻辑
        /// </summary>
        [RelayCommand]
        public void Cancel()
        {
            SettingsChanged = false;
            // 用户取消设置修改（移除日志）
        }

        /// <summary>
        /// 应用预设命令 - 完全按照Client项目PresetCombo_SelectionChanged逻辑
        /// </summary>
        [RelayCommand]
        public void ApplyPreset()
        {
            try
            {
                if (SelectedPresetIndex >= 0 && SelectedPresetIndex < Presets.Count)
                {
                    var presetName = Presets[SelectedPresetIndex];

                    // 🔑 直接应用预设 - 与Client项目ApplyPreset()一致
                    ApplyPresetInternal(presetName);

                    // 预设应用完成（移除日志）
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionSettingsViewModel", $"❌ 应用预设失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新质量模式命令 - 完全按照Client项目QualityModeCombo_SelectionChanged逻辑
        /// </summary>
        [RelayCommand]
        public void UpdateQualityMode()
        {
            OnQualityModeChanged();
        }

        #endregion

        #region 辅助方法 - 完全按照Client项目方法逻辑

        /// <summary>
        /// 设置ComboBox值 - 完全按照Client项目SetComboBoxValue()逻辑
        /// </summary>
        private string SetComboBoxValue(ObservableCollection<string> collection, string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                Utils.Logger.Debug("ConversionSettingsViewModel", $"SetComboBoxValue: value为空，返回第一项");
                return collection.FirstOrDefault() ?? "";
            }

            // 🔑 查找完全匹配的项 - 与Client项目逻辑一致
            var exactMatch = collection.FirstOrDefault(item => item == value);
            if (exactMatch != null)
            {
                Utils.Logger.Debug("ConversionSettingsViewModel", $"SetComboBoxValue: 成功设置 = {value} (完全匹配)");
                return exactMatch;
            }

            // 🔑 查找包含该值的项 - 与Client项目逻辑一致
            var containsMatch = collection.FirstOrDefault(item => item.Contains(value));
            if (containsMatch != null)
            {
                Utils.Logger.Debug("ConversionSettingsViewModel", $"SetComboBoxValue: 成功设置 = {value} (包含匹配: {containsMatch})");
                return containsMatch;
            }

            // 🔑 如果都没找到，返回第一项 - 与Client项目逻辑一致
            Utils.Logger.Debug("ConversionSettingsViewModel", $"SetComboBoxValue: 未找到匹配项 = {value}，返回第一项");
            return collection.FirstOrDefault() ?? "";
        }

        /// <summary>
        /// 质量模式变化处理 - 完全按照Client项目QualityModeCombo_SelectionChanged逻辑
        /// </summary>
        private void OnQualityModeChanged()
        {
            // 🔑 使用中文描述来判断模式 - 与Client项目逻辑一致
            var isCrf = SelectedQualityMode == "恒定质量 (CRF)";

            // 🔑 显示/隐藏相关控件 - 与Client项目一致
            IsCrfMode = isCrf;
            IsBitrateMode = !isCrf;

            Utils.Logger.Debug("ConversionSettingsViewModel", $"质量模式变化: {SelectedQualityMode}, CRF模式: {IsCrfMode}");
        }

        /// <summary>
        /// 获取当前UI中的设置 - 完全按照Client项目GetCurrentSettings()逻辑
        /// </summary>
        private ConversionSettings GetCurrentSettings()
        {
            return new ConversionSettings
            {
                // 🔑 基本设置 - 与Client项目一致
                OutputFormat = SelectedOutputFormat,
                Resolution = SelectedResolution,

                // 🔑 视频设置 - 与Client项目一致
                VideoCodec = SelectedVideoCodec,
                FrameRate = SelectedFrameRate,
                QualityMode = SelectedQualityMode,
                VideoQuality = IsCrfMode ? ((int)CrfQuality).ToString() : VideoBitrate.ToString(),
                EncodingPreset = SelectedEncodingPreset,
                Profile = SelectedProfile,

                // 🔑 音频设置 - 与Client项目一致
                AudioCodec = SelectedAudioCodec,
                AudioQuality = SelectedAudioQuality,
                AudioChannels = SelectedAudioChannels,
                SampleRate = SelectedSampleRate,
                AudioVolume = AudioVolume.ToString(),

                // 🔑 高级设置 - 与Client项目一致
                HardwareAcceleration = SelectedHardwareAcceleration,
                PixelFormat = SelectedPixelFormat,
                ColorSpace = SelectedColorSpace,
                FastStart = FastStart,
                Deinterlace = Deinterlace,
                TwoPass = TwoPass,

                // 🔑 滤镜设置 - 与Client项目一致
                Denoise = SelectedDenoise,
                VideoFilters = VideoFilters,
                AudioFilters = AudioFilters,

                // 🔑 任务设置 - 与Client项目一致
                Priority = 0,
                MaxRetries = 3
            };
        }

        /// <summary>
        /// 应用预设内部方法 - 完全按照Client项目ApplyPreset()逻辑
        /// </summary>
        private void ApplyPresetInternal(string? presetName)
        {
            if (string.IsNullOrEmpty(presetName)) return;

            ConversionSettings presetSettings;

            // 🔑 预设选择逻辑 - 与Client项目switch语句完全一致
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

            // 🔑 应用预设设置到UI - 与Client项目LoadSettings()一致
            LoadSettings(presetSettings);

            // 🔑 更新设置摘要
            UpdateSettingsSummary();
        }

        #endregion

        #region 预设创建方法 - 完全按照Client项目预设方法

        /// <summary>
        /// 创建NVENC快速预设 - 与Client项目CreateNvencFastPreset()一致
        /// </summary>
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

        /// <summary>
        /// 创建NVENC高质量预设 - 与Client项目CreateNvencHighQualityPreset()一致
        /// </summary>
        private ConversionSettings CreateNvencHighQualityPreset()
        {
            return new ConversionSettings
            {
                OutputFormat = "mp4",
                Resolution = "1920x1080",
                VideoCodec = "h264_nvenc",
                FrameRate = "原始",
                QualityMode = "CRF",
                VideoQuality = "18",
                EncodingPreset = "slow",
                Profile = "high",
                AudioCodec = "aac",
                AudioQuality = "256",
                AudioChannels = "原始",
                SampleRate = "48000",
                AudioVolume = "0",
                HardwareAcceleration = "nvenc",
                PixelFormat = "yuv420p",
                ColorSpace = "bt709",
                FastStart = true,
                Deinterlace = false,
                TwoPass = false,
                Denoise = "hqdn3d",
                VideoFilters = "",
                AudioFilters = "",
                Priority = 0,
                MaxRetries = 3
            };
        }

        /// <summary>
        /// 创建NVENC 4K预设 - 与Client项目CreateNvenc4KPreset()一致
        /// </summary>
        private ConversionSettings CreateNvenc4KPreset()
        {
            return new ConversionSettings
            {
                OutputFormat = "mp4",
                Resolution = "3840x2160",
                VideoCodec = "hevc_nvenc",
                FrameRate = "原始",
                QualityMode = "CRF",
                VideoQuality = "20",
                EncodingPreset = "medium",
                Profile = "main",
                AudioCodec = "aac",
                AudioQuality = "320",
                AudioChannels = "原始",
                SampleRate = "48000",
                AudioVolume = "0",
                HardwareAcceleration = "nvenc",
                PixelFormat = "yuv420p10le",
                ColorSpace = "bt2020",
                FastStart = true,
                Deinterlace = false,
                TwoPass = false,
                Denoise = "bm3d",
                VideoFilters = "",
                AudioFilters = "",
                Priority = 0,
                MaxRetries = 3
            };
        }

        /// <summary>
        /// 创建QSV快速预设 - 与Client项目CreateQsvFastPreset()一致
        /// </summary>
        private ConversionSettings CreateQsvFastPreset()
        {
            return new ConversionSettings
            {
                OutputFormat = "mp4",
                Resolution = "1920x1080",
                VideoCodec = "h264_qsv",
                FrameRate = "原始",
                QualityMode = "CRF",
                VideoQuality = "23",
                EncodingPreset = "fast",
                Profile = "high",
                AudioCodec = "aac",
                AudioQuality = "128",
                AudioChannels = "原始",
                SampleRate = "48000",
                AudioVolume = "0",
                HardwareAcceleration = "qsv",
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

        /// <summary>
        /// 创建QSV高质量预设 - 与Client项目CreateQsvHighQualityPreset()一致
        /// </summary>
        private ConversionSettings CreateQsvHighQualityPreset()
        {
            return new ConversionSettings
            {
                OutputFormat = "mp4",
                Resolution = "1920x1080",
                VideoCodec = "h264_qsv",
                FrameRate = "原始",
                QualityMode = "CRF",
                VideoQuality = "18",
                EncodingPreset = "slow",
                Profile = "high",
                AudioCodec = "aac",
                AudioQuality = "256",
                AudioChannels = "原始",
                SampleRate = "48000",
                AudioVolume = "0",
                HardwareAcceleration = "qsv",
                PixelFormat = "yuv420p",
                ColorSpace = "bt709",
                FastStart = true,
                Deinterlace = false,
                TwoPass = false,
                Denoise = "hqdn3d",
                VideoFilters = "",
                AudioFilters = "",
                Priority = 0,
                MaxRetries = 3
            };
        }

        /// <summary>
        /// 创建AMF快速预设 - 与Client项目CreateAmfFastPreset()一致
        /// </summary>
        private ConversionSettings CreateAmfFastPreset()
        {
            return new ConversionSettings
            {
                OutputFormat = "mp4",
                Resolution = "1920x1080",
                VideoCodec = "h264_amf",
                FrameRate = "原始",
                QualityMode = "CRF",
                VideoQuality = "23",
                EncodingPreset = "fast",
                Profile = "high",
                AudioCodec = "aac",
                AudioQuality = "128",
                AudioChannels = "原始",
                SampleRate = "48000",
                AudioVolume = "0",
                HardwareAcceleration = "amf",
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

        /// <summary>
        /// 创建AMF高质量预设 - 与Client项目CreateAmfHighQualityPreset()一致
        /// </summary>
        private ConversionSettings CreateAmfHighQualityPreset()
        {
            return new ConversionSettings
            {
                OutputFormat = "mp4",
                Resolution = "1920x1080",
                VideoCodec = "h264_amf",
                FrameRate = "原始",
                QualityMode = "CRF",
                VideoQuality = "18",
                EncodingPreset = "slow",
                Profile = "high",
                AudioCodec = "aac",
                AudioQuality = "256",
                AudioChannels = "原始",
                SampleRate = "48000",
                AudioVolume = "0",
                HardwareAcceleration = "amf",
                PixelFormat = "yuv420p",
                ColorSpace = "bt709",
                FastStart = true,
                Deinterlace = false,
                TwoPass = false,
                Denoise = "hqdn3d",
                VideoFilters = "",
                AudioFilters = "",
                Priority = 0,
                MaxRetries = 3
            };
        }

        /// <summary>
        /// 创建CPU标准预设 - 与Client项目CreateCpuStandardPreset()一致
        /// </summary>
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

        /// <summary>
        /// 创建CPU高质量预设 - 与Client项目CreateCpuHighQualityPreset()一致
        /// </summary>
        private ConversionSettings CreateCpuHighQualityPreset()
        {
            return new ConversionSettings
            {
                OutputFormat = "mp4",
                Resolution = "原始",
                VideoCodec = "libx264",
                FrameRate = "原始",
                QualityMode = "CRF",
                VideoQuality = "18",
                EncodingPreset = "veryslow",
                Profile = "high",
                AudioCodec = "aac",
                AudioQuality = "320",
                AudioChannels = "原始",
                SampleRate = "48000",
                AudioVolume = "0",
                HardwareAcceleration = "auto",
                PixelFormat = "auto",
                ColorSpace = "auto",
                FastStart = true,
                Deinterlace = false,
                TwoPass = true,
                Denoise = "bm3d",
                VideoFilters = "",
                AudioFilters = "",
                Priority = 0,
                MaxRetries = 3
            };
        }

        /// <summary>
        /// 创建比特率模式预设 - 与Client项目CreateBitratePreset()一致
        /// </summary>
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

        #region 选项获取方法 - 完全按照Client项目ComboBox选项

        /// <summary>
        /// 获取Client项目预设选项 - 与Client项目PresetCombo完全一致
        /// </summary>
        private List<string> GetClientPresetOptions()
        {
            return new List<string>
            {
                "GPU Fast 1080p (NVENC)",
                "GPU High Quality 1080p (NVENC)",
                "GPU 4K Ultra (NVENC)",
                "GPU Fast 1080p (QSV)",
                "GPU High Quality 1080p (QSV)",
                "GPU Fast 1080p (AMF)",
                "GPU High Quality 1080p (AMF)",
                "CPU Standard 1080p",
                "CPU High Quality 1080p"
                // ❌ 移除了"Bitrate Mode Demo"，Client项目没有这个选项
            };
        }

        /// <summary>
        /// 获取输出格式选项 - 与Client项目OutputFormatCombo完全一致
        /// </summary>
        private List<string> GetClientOutputFormatOptions()
        {
            return new List<string> { "mp4", "avi", "mov", "mkv", "webm", "flv", "m4v" };
        }

        /// <summary>
        /// 获取分辨率选项 - 与Client项目ResolutionCombo完全一致
        /// </summary>
        private List<string> GetClientResolutionOptions()
        {
            return new List<string>
            {
                "保持原始", "4K (3840x2160)", "1080p (1920x1080)", "720p (1280x720)", "480p (854x480)", "360p (640x360)"
            };
        }

        /// <summary>
        /// 获取视频编码器选项 - 与Client项目VideoCodecCombo完全一致
        /// </summary>
        private List<string> GetClientVideoCodecOptions()
        {
            return new List<string>
            {
                "H.264 (CPU)", "H.265/HEVC (CPU)", "H.264 (NVIDIA)", "H.265/HEVC (NVIDIA)",
                "H.264 (Intel QSV)", "H.265/HEVC (Intel QSV)", "H.264 (AMD AMF)", "H.265/HEVC (AMD AMF)"
            };
        }

        /// <summary>
        /// 获取质量模式选项 - 与Client项目QualityModeCombo完全一致
        /// </summary>
        private List<string> GetClientQualityModeOptions()
        {
            return new List<string> { "恒定质量 (CRF)", "恒定比特率" };
        }

        /// <summary>
        /// 获取帧率选项 - 与Client项目FrameRateCombo完全一致
        /// </summary>
        private List<string> GetClientFrameRateOptions()
        {
            return new List<string> { "保持原始", "24", "25", "30", "50", "60" };
        }

        /// <summary>
        /// 获取编码预设选项 - 与Client项目EncodingPresetCombo完全一致
        /// </summary>
        private List<string> GetClientEncodingPresetOptions()
        {
            return new List<string>
            {
                "超快 (最低质量)", "很快", "快", "中等 (推荐)", "慢", "很慢 (最高质量)"
            };
        }

        /// <summary>
        /// 获取配置文件选项 - 与Client项目ProfileCombo完全一致
        /// </summary>
        private List<string> GetClientProfileOptions()
        {
            return new List<string> { "auto", "baseline", "main", "high" };
        }

        /// <summary>
        /// 获取音频编码器选项 - 与Client项目AudioCodecCombo完全一致
        /// </summary>
        private List<string> GetClientAudioCodecOptions()
        {
            return new List<string> { "AAC (推荐)", "MP3", "Opus", "Vorbis", "AC3", "EAC3" };
        }

        /// <summary>
        /// 获取音频质量选项 - 与Client项目AudioQualityCombo完全一致
        /// </summary>
        private List<string> GetClientAudioQualityOptions()
        {
            return new List<string>
            {
                "96 kbps (低质量)", "128 kbps (标准)", "192 kbps (高质量)",
                "256 kbps (很高质量)", "320 kbps (最高质量)"
            };
        }

        /// <summary>
        /// 获取音频声道选项 - 与Client项目AudioChannelsCombo完全一致
        /// </summary>
        private List<string> GetClientAudioChannelOptions()
        {
            return new List<string> { "保持原始", "单声道", "立体声", "5.1环绕声" };
        }

        /// <summary>
        /// 获取采样率选项 - 与Client项目SampleRateCombo完全一致
        /// </summary>
        private List<string> GetClientSampleRateOptions()
        {
            return new List<string>
            {
                "22 kHz (电话质量)", "44.1 kHz (CD质量)", "48 kHz (DVD质量)", "96 kHz (高保真)"
            };
        }

        /// <summary>
        /// 获取硬件加速选项 - 与Client项目HardwareAccelerationCombo完全一致
        /// </summary>
        private List<string> GetClientHardwareAccelOptions()
        {
            return new List<string> { "自动检测", "NVIDIA NVENC", "Intel Quick Sync", "AMD AMF", "无" };
        }

        /// <summary>
        /// 获取像素格式选项 - 与Client项目PixelFormatCombo完全一致
        /// </summary>
        private List<string> GetClientPixelFormatOptions()
        {
            return new List<string> { "YUV420P (标准)", "YUV420P 10-bit", "YUV444P", "RGB24" };
        }

        /// <summary>
        /// 获取色彩空间选项 - 与Client项目ColorSpaceCombo完全一致
        /// </summary>
        private List<string> GetClientColorSpaceOptions()
        {
            return new List<string> { "自动", "BT.709 (HD)", "BT.2020 (4K HDR)", "BT.601 (SD)" };
        }

        /// <summary>
        /// 获取降噪选项 - 与Client项目DenoiseCombo完全一致
        /// </summary>
        private List<string> GetClientDenoiseOptions()
        {
            return new List<string> { "无", "高质量降噪", "非局部均值降噪", "BM3D降噪 (最佳)" };
        }



        #endregion

        #region 属性变化通知 - 用于更新设置摘要

        /// <summary>
        /// CRF质量值变化时更新摘要
        /// </summary>
        partial void OnCrfQualityChanged(double value)
        {
            UpdateSettingsSummary();
        }

        /// <summary>
        /// 视频比特率变化时更新摘要
        /// </summary>
        partial void OnVideoBitrateChanged(int value)
        {
            UpdateSettingsSummary();
        }

        /// <summary>
        /// 视频编码器变化时更新摘要
        /// </summary>
        partial void OnSelectedVideoCodecChanged(string value)
        {
            UpdateSettingsSummary();
        }

        /// <summary>
        /// 分辨率变化时更新摘要
        /// </summary>
        partial void OnSelectedResolutionChanged(string value)
        {
            UpdateSettingsSummary();
        }

        /// <summary>
        /// 音频编码器变化时更新摘要
        /// </summary>
        partial void OnSelectedAudioCodecChanged(string value)
        {
            UpdateSettingsSummary();
        }

        /// <summary>
        /// 质量模式变化时更新摘要
        /// </summary>
        partial void OnSelectedQualityModeChanged(string value)
        {
            OnQualityModeChanged();
            UpdateSettingsSummary();
        }

        #endregion

        #region IDisposable实现 - 资源清理

        private bool _disposed = false;

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源的具体实现
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    try
                    {
                        // 清理定时器
                        _summaryUpdateTimer?.Stop();
                        _summaryUpdateTimer?.Dispose();
                        _summaryUpdateTimer = null;

                        // ViewModel资源清理完成（移除日志）
                    }
                    catch (Exception ex)
                    {
                        Utils.Logger.Error("ConversionSettingsViewModel", $"❌ ViewModel资源清理失败: {ex.Message}");
                    }
                }

                _disposed = true;
            }
        }

        #endregion
    }
}
