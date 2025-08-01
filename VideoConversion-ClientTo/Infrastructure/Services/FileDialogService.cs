using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace VideoConversion_ClientTo.Infrastructure.Services
{
    /// <summary>
    /// 文件对话框服务
    /// 职责: 处理文件选择和文件夹选择
    /// </summary>
    public interface IFileDialogService
    {
        Task<IEnumerable<string>> SelectVideoFilesAsync();
        Task<IEnumerable<string>> SelectFolderAsync();
        Task<string?> SelectSaveLocationAsync(string defaultFileName);
        string[] GetSupportedVideoExtensions();
        bool IsVideoFile(string filePath);
    }

    public class FileDialogService : IFileDialogService
    {
        private readonly string[] _videoExtensions = new[]
        {
            ".mp4", ".avi", ".mov", ".mkv", ".wmv", ".flv", ".webm", 
            ".m4v", ".3gp", ".mpg", ".mpeg", ".ts", ".mts", ".m2ts"
        };

        public async Task<IEnumerable<string>> SelectVideoFilesAsync()
        {
            try
            {
                var topLevel = TopLevel.GetTopLevel(App.Current?.ApplicationLifetime is 
                    Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop 
                    ? desktop.MainWindow : null);

                if (topLevel == null)
                {
                    Utils.Logger.Warning("FileDialogService", "⚠️ 无法获取顶级窗口");
                    return Array.Empty<string>();
                }

                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "选择视频文件",
                    AllowMultiple = true,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("视频文件")
                        {
                            Patterns = _videoExtensions.Select(ext => $"*{ext}").ToArray(),
                            AppleUniformTypeIdentifiers = new[] { "public.movie" },
                            MimeTypes = new[] { "video/*" }
                        },
                        FilePickerFileTypes.All
                    }
                });

                var filePaths = files.Select(f => f.Path.LocalPath).ToList();
                Utils.Logger.Info("FileDialogService", $"✅ 用户选择了 {filePaths.Count} 个文件");
                
                return filePaths;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileDialogService", $"❌ 文件选择失败: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        public async Task<IEnumerable<string>> SelectFolderAsync()
        {
            try
            {
                var topLevel = TopLevel.GetTopLevel(App.Current?.ApplicationLifetime is 
                    Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop 
                    ? desktop.MainWindow : null);

                if (topLevel == null)
                {
                    Utils.Logger.Warning("FileDialogService", "⚠️ 无法获取顶级窗口");
                    return Array.Empty<string>();
                }

                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "选择包含视频文件的文件夹",
                    AllowMultiple = false
                });

                if (folders.Count == 0)
                {
                    return Array.Empty<string>();
                }

                var folderPath = folders[0].Path.LocalPath;
                Utils.Logger.Info("FileDialogService", $"📂 用户选择了文件夹: {folderPath}");

                // 递归扫描文件夹中的视频文件
                var videoFiles = ScanFolderForVideoFiles(folderPath);
                Utils.Logger.Info("FileDialogService", $"✅ 在文件夹中找到 {videoFiles.Count} 个视频文件");

                return videoFiles;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileDialogService", $"❌ 文件夹选择失败: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        public async Task<string?> SelectSaveLocationAsync(string defaultFileName)
        {
            try
            {
                var topLevel = TopLevel.GetTopLevel(App.Current?.ApplicationLifetime is 
                    Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop 
                    ? desktop.MainWindow : null);

                if (topLevel == null)
                {
                    Utils.Logger.Warning("FileDialogService", "⚠️ 无法获取顶级窗口");
                    return null;
                }

                var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "选择保存位置",
                    SuggestedFileName = defaultFileName,
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType("视频文件")
                        {
                            Patterns = _videoExtensions.Select(ext => $"*{ext}").ToArray()
                        }
                    }
                });

                if (file != null)
                {
                    var filePath = file.Path.LocalPath;
                    Utils.Logger.Info("FileDialogService", $"💾 用户选择保存位置: {filePath}");
                    return filePath;
                }

                return null;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileDialogService", $"❌ 保存位置选择失败: {ex.Message}");
                return null;
            }
        }

        private List<string> ScanFolderForVideoFiles(string folderPath)
        {
            var videoFiles = new List<string>();

            try
            {
                // 获取当前文件夹中的视频文件
                var files = Directory.GetFiles(folderPath)
                    .Where(file => _videoExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                    .ToList();

                videoFiles.AddRange(files);

                // 递归扫描子文件夹
                var subDirectories = Directory.GetDirectories(folderPath);
                foreach (var subDir in subDirectories)
                {
                    try
                    {
                        var subFiles = ScanFolderForVideoFiles(subDir);
                        videoFiles.AddRange(subFiles);
                    }
                    catch (Exception ex)
                    {
                        Utils.Logger.Warning("FileDialogService", $"⚠️ 扫描子文件夹失败 {subDir}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("FileDialogService", $"❌ 扫描文件夹失败 {folderPath}: {ex.Message}");
            }

            return videoFiles;
        }

        /// <summary>
        /// 验证文件是否为支持的视频格式
        /// </summary>
        public bool IsVideoFile(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return _videoExtensions.Contains(extension);
        }

        /// <summary>
        /// 获取支持的视频格式列表
        /// </summary>
        public string[] GetSupportedVideoExtensions()
        {
            return _videoExtensions.ToArray();
        }
    }
}
