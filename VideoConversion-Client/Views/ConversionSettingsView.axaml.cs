using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using VideoConversion_Client.Models;

namespace VideoConversion_Client.Views
{
    public partial class ConversionSettingsView : UserControl
    {
        // 事件定义
        public event EventHandler<ConversionStartEventArgs>? ConversionStartRequested;

        public ConversionSettingsView()
        {
            InitializeComponent();
            InitializePresets();
            InitializeQualitySlider();

            // 在控件加载完成后初始化ComboBox
            this.Loaded += (s, e) => InitializeComboBoxes();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void InitializePresets()
        {
            var presetComboBox = this.FindControl<ComboBox>("PresetComboBox");
            if (presetComboBox != null)
            {
                var presets = ConversionPreset.GetAllPresets();
                foreach (var preset in presets)
                {
                    presetComboBox.Items.Add(preset.Name);
                }
                presetComboBox.SelectedIndex = 0;
            }
        }

        private void InitializeComboBoxes()
        {
            // 确保输出格式ComboBox有默认选中项
            var outputFormatComboBox = this.FindControl<ComboBox>("OutputFormatComboBox");
            if (outputFormatComboBox != null && outputFormatComboBox.SelectedIndex == -1)
            {
                outputFormatComboBox.SelectedIndex = 0; // 选择第一项 "MP4 (H.264)"
            }

            // 确保分辨率ComboBox有默认选中项
            var resolutionComboBox = this.FindControl<ComboBox>("ResolutionComboBox");
            if (resolutionComboBox != null && resolutionComboBox.SelectedIndex == -1)
            {
                resolutionComboBox.SelectedIndex = 0; // 选择第一项 "保持原始"
            }
        }

        private void InitializeQualitySlider()
        {
            var qualitySlider = this.FindControl<Slider>("QualitySlider");
            var qualityValue = this.FindControl<TextBlock>("QualityValue");
            if (qualitySlider != null && qualityValue != null)
            {
                qualitySlider.PropertyChanged += (s, e) =>
                {
                    if (e.Property.Name == "Value")
                    {
                        qualityValue.Text = ((int)qualitySlider.Value).ToString();
                    }
                };
            }
        }

        private void StartButton_Click(object? sender, RoutedEventArgs e)
        {
            var taskNameTextBox = this.FindControl<TextBox>("TaskNameTextBox");
            var presetComboBox = this.FindControl<ComboBox>("PresetComboBox");
            var outputFormatComboBox = this.FindControl<ComboBox>("OutputFormatComboBox");
            var resolutionComboBox = this.FindControl<ComboBox>("ResolutionComboBox");
            var qualitySlider = this.FindControl<Slider>("QualitySlider");

            var args = new ConversionStartEventArgs
            {
                TaskName = taskNameTextBox?.Text ?? "",
                Preset = presetComboBox?.SelectedItem?.ToString() ?? "Fast 1080p30",
                OutputFormat = GetSelectedComboBoxValue(outputFormatComboBox, "mp4"),
                Resolution = GetSelectedComboBoxValue(resolutionComboBox, ""),
                VideoQuality = ((int)(qualitySlider?.Value ?? 23)).ToString()
            };

            ConversionStartRequested?.Invoke(this, args);
        }

        private string GetSelectedComboBoxValue(ComboBox? comboBox, string defaultValue)
        {
            if (comboBox?.SelectedItem is ComboBoxItem item)
            {
                var content = item.Content?.ToString() ?? defaultValue;
                // 提取格式值（例如从"MP4 (H.264)"提取"mp4"）
                if (content.Contains("MP4"))
                    return content.Contains("H.265") ? "mp4_h265" : "mp4";
                if (content.Contains("WebM"))
                    return "webm";
                if (content.Contains("AVI"))
                    return "avi";
                if (content.Contains("4K"))
                    return "3840x2160";
                if (content.Contains("1080p"))
                    return "1920x1080";
                if (content.Contains("720p"))
                    return "1280x720";
                if (content.Contains("480p"))
                    return "854x480";
            }
            return defaultValue;
        }

        // 公共方法
        public void SetTaskName(string taskName)
        {
            var taskNameTextBox = this.FindControl<TextBox>("TaskNameTextBox");
            if (taskNameTextBox != null)
            {
                taskNameTextBox.Text = taskName;
            }
        }

        public void SetEnabled(bool enabled)
        {
            var startButton = this.FindControl<Button>("StartButton");
            if (startButton != null)
            {
                startButton.IsEnabled = enabled;
            }
        }

        public string GetTaskName()
        {
            var taskNameTextBox = this.FindControl<TextBox>("TaskNameTextBox");
            return taskNameTextBox?.Text ?? "";
        }
    }

    // 转换开始事件参数
    public class ConversionStartEventArgs : EventArgs
    {
        public string TaskName { get; set; } = "";
        public string Preset { get; set; } = "";
        public string OutputFormat { get; set; } = "";
        public string Resolution { get; set; } = "";
        public string VideoQuality { get; set; } = "";
    }
}
