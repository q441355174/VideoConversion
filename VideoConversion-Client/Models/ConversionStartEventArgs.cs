using System;

namespace VideoConversion_Client.Models
{
    public class ConversionStartEventArgs : EventArgs
    {
        public string FilePath { get; set; } = "";
        public ConversionSettings Settings { get; set; } = new ConversionSettings();
        public string TaskName { get; set; } = "";
        public string Preset { get; set; } = "";
        public string OutputFormat { get; set; } = "";
        public string Resolution { get; set; } = "";
        public string VideoQuality { get; set; } = "";

        public ConversionStartEventArgs(string filePath, ConversionSettings settings)
        {
            FilePath = filePath;
            Settings = settings;
            TaskName = System.IO.Path.GetFileNameWithoutExtension(filePath);
            Preset = "自定义";
            OutputFormat = "MP4";
            Resolution = settings.Resolution;
            VideoQuality = settings.Bitrate;
        }
    }
}
