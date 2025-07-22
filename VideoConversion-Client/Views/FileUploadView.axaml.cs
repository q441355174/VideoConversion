using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace VideoConversion_Client.Views
{
    public partial class FileUploadView : UserControl
    {
        // 事件定义
        public event EventHandler<string>? FileSelected;
        public event EventHandler? FileCleared;

        private string? selectedFilePath = null;

        public FileUploadView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        // 拖拽事件处理
        private void FileDropZone_DragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data.Contains(DataFormats.Files))
            {
                e.DragEffects = DragDropEffects.Copy;
                var dropZone = sender as Border;
                if (dropZone != null)
                {
                    dropZone.Background = Avalonia.Media.Brushes.LightBlue;
                }
            }
            else
            {
                e.DragEffects = DragDropEffects.None;
            }
        }

        private void FileDropZone_DragLeave(object? sender, DragEventArgs e)
        {
            var dropZone = sender as Border;
            if (dropZone != null)
            {
                dropZone.Background = Avalonia.Media.Brush.Parse("#f8f9fa");
            }
        }

        private async void FileDropZone_Drop(object? sender, DragEventArgs e)
        {
            var dropZone = sender as Border;
            if (dropZone != null)
            {
                dropZone.Background = Avalonia.Media.Brush.Parse("#f8f9fa");
            }

            if (e.Data.Contains(DataFormats.Files))
            {
                var files = e.Data.GetFiles();
                var file = files?.FirstOrDefault();
                
                if (file != null)
                {
                    var filePath = file.Path.LocalPath;
                    await HandleFileSelection(filePath);
                }
            }
        }

        // 文件选择事件
        private async void BrowseButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel == null) return;

                var options = new FilePickerOpenOptions
                {
                    Title = "选择视频文件",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("视频文件")
                        {
                            Patterns = new[] { "*.mp4", "*.avi", "*.mov", "*.mkv", "*.wmv", "*.flv", "*.webm", "*.m4v", "*.3gp", "*.mpg", "*.mpeg", "*.ts", "*.mts" }
                        },
                        FilePickerFileTypes.All
                    }
                };

                var result = await topLevel.StorageProvider.OpenFilePickerAsync(options);
                var file = result?.FirstOrDefault();

                if (file != null)
                {
                    var filePath = file.Path.LocalPath;
                    await HandleFileSelection(filePath);
                }
            }
            catch (Exception ex)
            {
                // 可以通过事件通知父组件错误
                System.Diagnostics.Debug.WriteLine($"文件选择失败: {ex.Message}");
            }
        }

        // 清除文件选择
        private void ClearFileButton_Click(object? sender, RoutedEventArgs e)
        {
            ClearFileSelection();
            FileCleared?.Invoke(this, EventArgs.Empty);
        }

        // 文件处理方法
        private async Task HandleFileSelection(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return;
                }

                // 检查文件格式
                var extension = Path.GetExtension(filePath).ToLower();
                var supportedExtensions = new[] { ".mp4", ".avi", ".mov", ".mkv", ".wmv", ".flv", ".webm", ".m4v", ".3gp", ".mpg", ".mpeg", ".ts", ".mts" };
                
                if (!supportedExtensions.Contains(extension))
                {
                    return;
                }

                selectedFilePath = filePath;

                // 更新UI显示
                var selectedFileInfo = this.FindControl<Border>("SelectedFileInfo");
                var selectedFileName = this.FindControl<TextBlock>("SelectedFileName");
                var selectedFileSize = this.FindControl<TextBlock>("SelectedFileSize");

                if (selectedFileInfo != null)
                    selectedFileInfo.IsVisible = true;

                if (selectedFileName != null)
                    selectedFileName.Text = Path.GetFileName(filePath);

                if (selectedFileSize != null)
                    selectedFileSize.Text = GetFileSize(filePath);

                // 通知父组件文件已选择
                FileSelected?.Invoke(this, filePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"文件处理失败: {ex.Message}");
            }
        }

        // 清除文件选择
        public void ClearFileSelection()
        {
            selectedFilePath = null;
            
            var selectedFileInfo = this.FindControl<Border>("SelectedFileInfo");
            if (selectedFileInfo != null)
                selectedFileInfo.IsVisible = false;
        }

        // 获取文件大小
        private string GetFileSize(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                var sizeInMB = fileInfo.Length / (1024.0 * 1024.0);
                return sizeInMB > 1024 ? $"{sizeInMB / 1024:F1} GB" : $"{sizeInMB:F1} MB";
            }
            catch
            {
                return "未知大小";
            }
        }

        // 公共属性
        public string? SelectedFilePath => selectedFilePath;
    }
}
