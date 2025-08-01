using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VideoConversion_ClientTo.ViewModels;
using VideoConversion_ClientTo.Domain.ValueObjects;

namespace VideoConversion_ClientTo.Presentation.ViewModels
{
    /// <summary>
    /// 转换设置视图模型
    /// </summary>
    public partial class ConversionSettingsViewModel : ViewModelBase
    {
        [ObservableProperty]
        private int _selectedPresetIndex = 0;

        [ObservableProperty]
        private int _outputFormatIndex = 0;

        [ObservableProperty]
        private int _resolutionIndex = 1; // 默认1080p

        [ObservableProperty]
        private int _videoCodecIndex = 0; // 默认H.264

        [ObservableProperty]
        private double _videoQuality = 23; // 默认CRF 23

        [ObservableProperty]
        private int _audioCodecIndex = 0; // 默认AAC

        [ObservableProperty]
        private int _audioBitrateIndex = 1; // 默认192k

        [ObservableProperty]
        private int _encodingPresetIndex = 5; // 默认medium

        [ObservableProperty]
        private int _hardwareAccelIndex = 0; // 默认自动检测

        [ObservableProperty]
        private bool _twoPassEncoding = false;

        public ConversionSettingsViewModel()
        {
            Utils.Logger.Info("ConversionSettingsViewModel", "✅ 转换设置视图模型已初始化");
            LoadDefaultSettings();
        }

        #region 命令

        [RelayCommand]
        private async Task ResetToDefaultAsync()
        {
            try
            {
                Utils.Logger.Info("ConversionSettingsViewModel", "🔄 重置为默认设置");
                LoadDefaultSettings();
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionSettingsViewModel", $"❌ 重置设置失败: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task CancelAsync()
        {
            try
            {
                Utils.Logger.Info("ConversionSettingsViewModel", "❌ 取消设置");
                // TODO: 关闭窗口
                await Task.Delay(100); // 模拟异步操作
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionSettingsViewModel", $"❌ 取消操作失败: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task OkAsync()
        {
            try
            {
                Utils.Logger.Info("ConversionSettingsViewModel", "✅ 确认设置");
                
                // 保存设置
                var parameters = CreateConversionParameters();
                Utils.Logger.Info("ConversionSettingsViewModel", $"📋 转换参数: {parameters}");
                
                // TODO: 保存设置并关闭窗口
                await Task.Delay(100); // 模拟异步操作
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionSettingsViewModel", $"❌ 确认设置失败: {ex.Message}");
            }
        }

        #endregion

        #region 私有方法

        private void LoadDefaultSettings()
        {
            try
            {
                SelectedPresetIndex = 0; // Fast 1080p30
                OutputFormatIndex = 0; // MP4
                ResolutionIndex = 1; // 1080p
                VideoCodecIndex = 0; // H.264
                VideoQuality = 23; // CRF 23
                AudioCodecIndex = 0; // AAC
                AudioBitrateIndex = 1; // 192k
                EncodingPresetIndex = 5; // medium
                HardwareAccelIndex = 0; // 自动检测
                TwoPassEncoding = false;
                
                Utils.Logger.Debug("ConversionSettingsViewModel", "📋 默认设置已加载");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionSettingsViewModel", $"❌ 加载默认设置失败: {ex.Message}");
            }
        }

        private ConversionParameters CreateConversionParameters()
        {
            try
            {
                var outputFormat = GetOutputFormat();
                var resolution = GetResolution();
                var videoCodec = GetVideoCodec();
                var audioCodec = GetAudioCodec();
                var videoQualityStr = VideoQuality.ToString();
                var audioQualityStr = GetAudioBitrate();
                var preset = GetEncodingPreset();

                return ConversionParameters.Create(
                    outputFormat, resolution, videoCodec, audioCodec,
                    videoQualityStr, audioQualityStr, preset);
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionSettingsViewModel", $"❌ 创建转换参数失败: {ex.Message}");
                return ConversionParameters.CreateDefault();
            }
        }

        private string GetOutputFormat()
        {
            return OutputFormatIndex switch
            {
                0 => "mp4",
                1 => "avi",
                2 => "mov",
                3 => "mkv",
                4 => "webm",
                _ => "mp4"
            };
        }

        private string GetResolution()
        {
            return ResolutionIndex switch
            {
                0 => "3840x2160",
                1 => "1920x1080",
                2 => "1280x720",
                3 => "854x480",
                4 => "640x360",
                5 => "custom", // 自定义
                _ => "1920x1080"
            };
        }

        private string GetVideoCodec()
        {
            return VideoCodecIndex switch
            {
                0 => "libx264",
                1 => "libx265",
                2 => "libvpx-vp9",
                3 => "libvpx",
                _ => "libx264"
            };
        }

        private string GetAudioCodec()
        {
            return AudioCodecIndex switch
            {
                0 => "aac",
                1 => "mp3",
                2 => "opus",
                3 => "vorbis",
                _ => "aac"
            };
        }

        private string GetAudioBitrate()
        {
            return AudioBitrateIndex switch
            {
                0 => "128k",
                1 => "192k",
                2 => "256k",
                3 => "320k",
                _ => "192k"
            };
        }

        private string GetEncodingPreset()
        {
            return EncodingPresetIndex switch
            {
                0 => "ultrafast",
                1 => "superfast",
                2 => "veryfast",
                3 => "faster",
                4 => "fast",
                5 => "medium",
                6 => "slow",
                7 => "slower",
                8 => "veryslow",
                _ => "medium"
            };
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 获取当前转换参数
        /// </summary>
        public ConversionParameters GetCurrentParameters()
        {
            return CreateConversionParameters();
        }

        /// <summary>
        /// 设置转换参数
        /// </summary>
        public void SetParameters(ConversionParameters parameters)
        {
            try
            {
                // 根据参数设置界面值
                SetOutputFormatIndex(parameters.OutputFormat);
                SetResolutionIndex(parameters.Resolution);
                SetVideoCodecIndex(parameters.VideoCodec);
                SetAudioCodecIndex(parameters.AudioCodec);
                
                if (double.TryParse(parameters.VideoQuality, out var quality))
                {
                    VideoQuality = quality;
                }
                
                SetAudioBitrateIndex(parameters.AudioQuality);
                SetEncodingPresetIndex(parameters.Preset);
                
                Utils.Logger.Debug("ConversionSettingsViewModel", "📋 转换参数已设置");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionSettingsViewModel", $"❌ 设置转换参数失败: {ex.Message}");
            }
        }

        private void SetOutputFormatIndex(string format)
        {
            OutputFormatIndex = format.ToLower() switch
            {
                "mp4" => 0,
                "avi" => 1,
                "mov" => 2,
                "mkv" => 3,
                "webm" => 4,
                _ => 0
            };
        }

        private void SetResolutionIndex(string resolution)
        {
            ResolutionIndex = resolution switch
            {
                "3840x2160" => 0,
                "1920x1080" => 1,
                "1280x720" => 2,
                "854x480" => 3,
                "640x360" => 4,
                _ => 1
            };
        }

        private void SetVideoCodecIndex(string codec)
        {
            VideoCodecIndex = codec switch
            {
                "libx264" => 0,
                "libx265" => 1,
                "libvpx-vp9" => 2,
                "libvpx" => 3,
                _ => 0
            };
        }

        private void SetAudioCodecIndex(string codec)
        {
            AudioCodecIndex = codec switch
            {
                "aac" => 0,
                "mp3" => 1,
                "opus" => 2,
                "vorbis" => 3,
                _ => 0
            };
        }

        private void SetAudioBitrateIndex(string bitrate)
        {
            AudioBitrateIndex = bitrate switch
            {
                "128k" => 0,
                "192k" => 1,
                "256k" => 2,
                "320k" => 3,
                _ => 1
            };
        }

        private void SetEncodingPresetIndex(string preset)
        {
            EncodingPresetIndex = preset switch
            {
                "ultrafast" => 0,
                "superfast" => 1,
                "veryfast" => 2,
                "faster" => 3,
                "fast" => 4,
                "medium" => 5,
                "slow" => 6,
                "slower" => 7,
                "veryslow" => 8,
                _ => 5
            };
        }

        #endregion
    }
}
