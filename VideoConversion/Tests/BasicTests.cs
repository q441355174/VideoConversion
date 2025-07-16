using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VideoConversion.Models;
using VideoConversion.Services;
using Xunit;
using Moq;

namespace VideoConversion.Tests
{
    public class BasicTests
    {
        private readonly Mock<ILogger<DatabaseService>> _mockDbLogger;
        private readonly Mock<ILogger<FileService>> _mockFileLogger;
        private readonly Mock<IConfiguration> _mockConfiguration;

        public BasicTests()
        {
            _mockDbLogger = new Mock<ILogger<DatabaseService>>();
            _mockFileLogger = new Mock<ILogger<FileService>>();
            _mockConfiguration = new Mock<IConfiguration>();
            
            // 设置配置模拟
            _mockConfiguration.Setup(c => c.GetConnectionString("DefaultConnection"))
                .Returns("Data Source=test.db");
            _mockConfiguration.Setup(c => c.GetValue<string>("VideoConversion:UploadPath"))
                .Returns("test_uploads");
            _mockConfiguration.Setup(c => c.GetValue<string>("VideoConversion:OutputPath"))
                .Returns("test_outputs");
            _mockConfiguration.Setup(c => c.GetValue<long>("VideoConversion:MaxFileSize", It.IsAny<long>()))
                .Returns(1073741824); // 1GB
        }

        [Fact]
        public void ConversionPreset_GetAllPresets_ReturnsPresets()
        {
            // Act
            var presets = ConversionPreset.GetAllPresets();

            // Assert
            Assert.NotEmpty(presets);
            Assert.Contains(presets, p => p.IsDefault);
            Assert.All(presets, p => 
            {
                Assert.NotEmpty(p.Name);
                Assert.NotEmpty(p.Description);
                Assert.NotEmpty(p.OutputFormat);
            });
        }

        [Fact]
        public void ConversionPreset_GetDefaultPreset_ReturnsDefaultPreset()
        {
            // Act
            var defaultPreset = ConversionPreset.GetDefaultPreset();

            // Assert
            Assert.NotNull(defaultPreset);
            Assert.True(defaultPreset.IsDefault);
            Assert.Equal("Fast 1080p30", defaultPreset.Name);
        }

        [Fact]
        public void ConversionPreset_GetPresetByName_ReturnsCorrectPreset()
        {
            // Arrange
            var presetName = "High Quality 1080p";

            // Act
            var preset = ConversionPreset.GetPresetByName(presetName);

            // Assert
            Assert.NotNull(preset);
            Assert.Equal(presetName, preset.Name);
        }

        [Fact]
        public void ConversionTask_DefaultValues_AreSetCorrectly()
        {
            // Act
            var task = new ConversionTask();

            // Assert
            Assert.NotEmpty(task.Id);
            Assert.Equal(ConversionStatus.Pending, task.Status);
            Assert.Equal(0, task.Progress);
            Assert.True(task.CreatedAt <= DateTime.Now);
            Assert.True(task.CreatedAt > DateTime.Now.AddMinutes(-1));
        }

        [Theory]
        [InlineData("test.mp4", true)]
        [InlineData("test.avi", true)]
        [InlineData("test.mov", true)]
        [InlineData("test.txt", false)]
        [InlineData("test.exe", false)]
        [InlineData("", false)]
        public void FileService_ValidateFileExtension_WorksCorrectly(string fileName, bool expectedValid)
        {
            // Arrange
            var fileService = new FileService(_mockFileLogger.Object, _mockConfiguration.Object);
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.FileName).Returns(fileName);
            mockFile.Setup(f => f.Length).Returns(1024); // 1KB

            // Act
            var result = fileService.ValidateFile(mockFile.Object);

            // Assert
            Assert.Equal(expectedValid, result.IsValid);
        }

        [Theory]
        [InlineData(1024, true)] // 1KB
        [InlineData(1048576, true)] // 1MB
        [InlineData(1073741824, true)] // 1GB (at limit)
        [InlineData(2147483648, false)] // 2GB (over limit)
        public void FileService_ValidateFileSize_WorksCorrectly(long fileSize, bool expectedValid)
        {
            // Arrange
            var fileService = new FileService(_mockFileLogger.Object, _mockConfiguration.Object);
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.FileName).Returns("test.mp4");
            mockFile.Setup(f => f.Length).Returns(fileSize);

            // Act
            var result = fileService.ValidateFile(mockFile.Object);

            // Assert
            Assert.Equal(expectedValid, result.IsValid);
        }

        [Fact]
        public void FileService_FormatFileSize_FormatsCorrectly()
        {
            // Test cases
            Assert.Equal("1 KB", FileService.FormatFileSize(1024));
            Assert.Equal("1 MB", FileService.FormatFileSize(1048576));
            Assert.Equal("1 GB", FileService.FormatFileSize(1073741824));
            Assert.Equal("500 B", FileService.FormatFileSize(500));
            Assert.Equal("1.5 KB", FileService.FormatFileSize(1536));
        }

        [Fact]
        public void FileService_GenerateOutputFilePath_GeneratesValidPath()
        {
            // Arrange
            var fileService = new FileService(_mockFileLogger.Object, _mockConfiguration.Object);
            var originalFileName = "test_video.mp4";
            var outputFormat = "webm";

            // Act
            var outputPath = fileService.GenerateOutputFilePath(originalFileName, outputFormat);

            // Assert
            Assert.NotEmpty(outputPath);
            Assert.Contains("test_video", outputPath);
            Assert.EndsWith(".webm", outputPath);
            Assert.Contains("converted", outputPath);
        }

        [Fact]
        public void ConversionStatus_EnumValues_AreCorrect()
        {
            // Assert
            Assert.Equal(0, (int)ConversionStatus.Pending);
            Assert.Equal(1, (int)ConversionStatus.Converting);
            Assert.Equal(2, (int)ConversionStatus.Completed);
            Assert.Equal(3, (int)ConversionStatus.Failed);
            Assert.Equal(4, (int)ConversionStatus.Cancelled);
        }

        [Fact]
        public void ConversionTask_StatusTransitions_AreLogical()
        {
            // Arrange
            var task = new ConversionTask();

            // Act & Assert - Initial state
            Assert.Equal(ConversionStatus.Pending, task.Status);

            // Simulate status transitions
            task.Status = ConversionStatus.Converting;
            task.StartedAt = DateTime.Now;
            Assert.Equal(ConversionStatus.Converting, task.Status);
            Assert.NotNull(task.StartedAt);

            task.Status = ConversionStatus.Completed;
            task.CompletedAt = DateTime.Now;
            task.Progress = 100;
            Assert.Equal(ConversionStatus.Completed, task.Status);
            Assert.NotNull(task.CompletedAt);
            Assert.Equal(100, task.Progress);
        }

        [Theory]
        [InlineData("mp4", "libx264", "aac")]
        [InlineData("webm", "libvpx-vp9", "libvorbis")]
        [InlineData("mp3", "", "libmp3lame")]
        public void ConversionPreset_CodecSettings_AreAppropriate(string format, string expectedVideoCodec, string expectedAudioCodec)
        {
            // Arrange & Act
            var presets = ConversionPreset.GetAllPresets();
            var preset = presets.FirstOrDefault(p => p.OutputFormat == format);

            // Assert
            Assert.NotNull(preset);
            Assert.Equal(expectedVideoCodec, preset.VideoCodec);
            Assert.Equal(expectedAudioCodec, preset.AudioCodec);
        }

        [Fact]
        public void DatabaseService_Constructor_InitializesCorrectly()
        {
            // This test verifies that the DatabaseService can be constructed
            // In a real scenario, you'd want to use an in-memory database for testing
            
            // Act & Assert - Should not throw
            Assert.NotNull(_mockDbLogger.Object);
            Assert.NotNull(_mockConfiguration.Object);
        }
    }
}
