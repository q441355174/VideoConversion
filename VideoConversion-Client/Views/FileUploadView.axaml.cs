using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VideoConversion_Client.Models;

namespace VideoConversion_Client.Views
{
    public partial class FileUploadView : UserControl
    {
        public event EventHandler<EventArgs>? SettingsRequested;

        private bool _hasFiles = false;
        private bool _isConverting = false;
        private List<string> _selectedFiles = new List<string>();

        public FileUploadView()
        {
            InitializeComponent();
            UpdateViewState();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }



        // 更新视图状态
        private void UpdateViewState()
        {
            var emptyStateView = this.FindControl<Border>("EmptyStateView");
            var fileListView = this.FindControl<ScrollViewer>("FileListView");

            if (emptyStateView != null && fileListView != null)
            {
                if (_hasFiles)
                {
                    emptyStateView.IsVisible = false;
                    fileListView.IsVisible = true;
                }
                else
                {
                    emptyStateView.IsVisible = true;
                    fileListView.IsVisible = false;
                }
            }
        }

        // 文件拖拽区域点击事件
        private async void FileDropZone_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            await OpenFileDialog();
        }

        // 选择文件按钮点击事件
        private async void SelectFileBtn_Click(object? sender, RoutedEventArgs e)
        {
            await OpenFileDialog();
        }

        // 选择文件夹按钮点击事件
        private async void SelectFolderBtn_Click(object? sender, RoutedEventArgs e)
        {
            await OpenFolderDialog();
        }

        // 打开文件对话框
        private async Task OpenFileDialog()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "选择视频文件",
                AllowMultiple = true,
                FileTypeFilter = new[]
                {
                    new Avalonia.Platform.Storage.FilePickerFileType("视频文件")
                    {
                        Patterns = new[] { "*.mp4", "*.avi", "*.mov", "*.mkv", "*.wmv", "*.flv", "*.webm", "*.m4v", "*.3gp" }
                    }
                }
            });

            if (files.Count > 0)
            {
                foreach (var file in files)
                {
                    AddFile(file.Path.LocalPath);
                }
            }
        }

        // 打开文件夹对话框
        private async Task OpenFolderDialog()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                Title = "选择包含视频文件的文件夹",
                AllowMultiple = false
            });

            if (folders.Count > 0)
            {
                var folder = folders[0];
                var videoFiles = Directory.GetFiles(folder.Path.LocalPath, "*.*", SearchOption.AllDirectories)
                    .Where(file => new[] { ".mp4", ".avi", ".mov", ".mkv", ".wmv", ".flv", ".webm", ".m4v", ".3gp" }
                        .Contains(Path.GetExtension(file).ToLower()))
                    .ToArray();

                foreach (var file in videoFiles)
                {
                    AddFile(file);
                }
            }
        }

        // 添加文件到列表
        private void AddFile(string filePath)
        {
            if (!_selectedFiles.Contains(filePath))
            {
                _selectedFiles.Add(filePath);
                CreateFileItem(filePath);

                if (!_hasFiles)
                {
                    _hasFiles = true;
                    UpdateViewState();
                }
            }
        }

        // 创建文件项控件
        private void CreateFileItem(string filePath)
        {
            var fileListContainer = this.FindControl<StackPanel>("FileListContainer");
            if (fileListContainer == null) return;

            var fileName = Path.GetFileName(filePath);
            var fileInfo = new FileInfo(filePath);

            // 创建文件项的UI元素
            var fileItemBorder = CreateFileItemUI(
                fileName,
                Path.GetExtension(filePath).TrimStart('.').ToUpper(),
                "1920*1080", // 实际应该读取视频分辨率
                FormatFileSize(fileInfo.Length),
                "03:15:21", // 实际应该读取视频时长
                filePath
            );

            fileListContainer.Children.Add(fileItemBorder);
        }

        // 创建文件项UI
        private Border CreateFileItemUI(string fileName, string format, string resolution, string size, string duration, string filePath)
        {
            var border = new Border
            {
                Background = Avalonia.Media.Brushes.White,
                BorderBrush = Avalonia.Media.Brush.Parse("#e0e0e0"),
                BorderThickness = new Avalonia.Thickness(1),
                CornerRadius = new Avalonia.CornerRadius(8),
                Padding = new Avalonia.Thickness(15),
                Margin = new Avalonia.Thickness(0, 5),
                Tag = filePath // 用于标识文件路径
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition(120, GridUnitType.Pixel));
            grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            // 缩略图
            var thumbnailBorder = new Border
            {
                Background = Avalonia.Media.Brush.Parse("#f0f0f0"),
                CornerRadius = new Avalonia.CornerRadius(6),
                Width = 100,
                Height = 70
            };
            Grid.SetColumn(thumbnailBorder, 0);

            // 文件信息
            var infoPanel = CreateFileInfoPanel(fileName, format, resolution, size, duration);
            Grid.SetColumn(infoPanel, 1);

            // 操作按钮
            var actionPanel = CreateActionPanel(filePath);
            Grid.SetColumn(actionPanel, 2);

            // 设置和转换按钮
            var settingsPanel = CreateSettingsPanel(filePath);
            Grid.SetColumn(settingsPanel, 3);

            grid.Children.Add(thumbnailBorder);
            grid.Children.Add(infoPanel);
            grid.Children.Add(actionPanel);
            grid.Children.Add(settingsPanel);

            border.Child = grid;
            return border;
        }

        // 创建文件信息面板
        private StackPanel CreateFileInfoPanel(string fileName, string format, string resolution, string size, string duration)
        {
            var panel = new StackPanel
            {
                Margin = new Avalonia.Thickness(15, 0),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            // 文件名
            var fileNameText = new TextBlock
            {
                Text = fileName,
                FontSize = 16,
                FontWeight = Avalonia.Media.FontWeight.Medium,
                Foreground = Avalonia.Media.Brush.Parse("#333"),
                TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis
            };
            panel.Children.Add(fileNameText);

            // 源文件信息行1
            var sourceInfo1 = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 20,
                Margin = new Avalonia.Thickness(0, 8, 0, 0)
            };

            var formatPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 5 };
            formatPanel.Children.Add(new CheckBox { IsChecked = true });
            formatPanel.Children.Add(new TextBlock { Text = format, FontSize = 12, Foreground = Avalonia.Media.Brush.Parse("#666") });

            var resolutionPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 5 };
            resolutionPanel.Children.Add(new CheckBox { IsChecked = true });
            resolutionPanel.Children.Add(new TextBlock { Text = resolution, FontSize = 12, Foreground = Avalonia.Media.Brush.Parse("#666") });

            sourceInfo1.Children.Add(formatPanel);
            sourceInfo1.Children.Add(resolutionPanel);
            panel.Children.Add(sourceInfo1);

            // 源文件信息行2
            var sourceInfo2 = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 20,
                Margin = new Avalonia.Thickness(0, 5, 0, 0)
            };

            var sizePanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 5 };
            sizePanel.Children.Add(new CheckBox { IsChecked = true });
            sizePanel.Children.Add(new TextBlock { Text = size, FontSize = 12, Foreground = Avalonia.Media.Brush.Parse("#666") });

            var durationPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 5 };
            durationPanel.Children.Add(new CheckBox { IsChecked = true });
            durationPanel.Children.Add(new TextBlock { Text = duration, FontSize = 12, Foreground = Avalonia.Media.Brush.Parse("#666") });

            sourceInfo2.Children.Add(sizePanel);
            sourceInfo2.Children.Add(durationPanel);
            panel.Children.Add(sourceInfo2);

            // 转换箭头和目标信息
            var targetInfo = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 20,
                Margin = new Avalonia.Thickness(0, 10, 0, 0)
            };

            targetInfo.Children.Add(new TextBlock { Text = "→", FontSize = 16, Foreground = Avalonia.Media.Brush.Parse("#9b59b6"), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });

            var targetFormatPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 5 };
            targetFormatPanel.Children.Add(new CheckBox { IsChecked = true });
            targetFormatPanel.Children.Add(new TextBlock { Text = format, FontSize = 12, Foreground = Avalonia.Media.Brush.Parse("#666") });

            var targetResolutionPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 5 };
            targetResolutionPanel.Children.Add(new CheckBox { IsChecked = true });
            targetResolutionPanel.Children.Add(new TextBlock { Text = resolution, FontSize = 12, Foreground = Avalonia.Media.Brush.Parse("#666") });

            targetInfo.Children.Add(targetFormatPanel);
            targetInfo.Children.Add(targetResolutionPanel);
            panel.Children.Add(targetInfo);

            return panel;
        }

        // 创建操作按钮面板
        private StackPanel CreateActionPanel(string filePath)
        {
            var panel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 10,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
            };

            var deleteBtn = new Button
            {
                Content = "✕",
                Background = Avalonia.Media.Brushes.Transparent,
                Foreground = Avalonia.Media.Brush.Parse("#999"),
                BorderThickness = new Avalonia.Thickness(0),
                Padding = new Avalonia.Thickness(8)
            };
            deleteBtn.Click += (s, e) => RemoveFile(filePath);

            var copyBtn = new Button
            {
                Content = "📋",
                Background = Avalonia.Media.Brushes.Transparent,
                Foreground = Avalonia.Media.Brush.Parse("#999"),
                BorderThickness = new Avalonia.Thickness(0),
                Padding = new Avalonia.Thickness(8)
            };

            var moreBtn = new Button
            {
                Content = "⋯",
                Background = Avalonia.Media.Brushes.Transparent,
                Foreground = Avalonia.Media.Brush.Parse("#999"),
                BorderThickness = new Avalonia.Thickness(0),
                Padding = new Avalonia.Thickness(8)
            };

            panel.Children.Add(deleteBtn);
            panel.Children.Add(copyBtn);
            panel.Children.Add(moreBtn);

            return panel;
        }

        // 创建设置面板
        private StackPanel CreateSettingsPanel(string filePath)
        {
            var panel = new StackPanel
            {
                Spacing = 10,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Margin = new Avalonia.Thickness(15, 0, 0, 0)
            };

            var encoderCombo = new ComboBox
            {
                MinWidth = 120,
                SelectedIndex = 0
            };
            encoderCombo.Items.Add("无字幕");
            encoderCombo.Items.Add("内嵌字幕");
            encoderCombo.Items.Add("外挂字幕");

            var audioEncoderCombo = new ComboBox
            {
                MinWidth = 120,
                SelectedIndex = 0
            };
            audioEncoderCombo.Items.Add("Advanced Audio Coding");
            audioEncoderCombo.Items.Add("MP3");
            audioEncoderCombo.Items.Add("FLAC");

            var settingsBtn = new Button
            {
                Content = "⚙️ 设置",
                Background = Avalonia.Media.Brushes.Transparent,
                BorderBrush = Avalonia.Media.Brush.Parse("#e0e0e0"),
                BorderThickness = new Avalonia.Thickness(1),
                Padding = new Avalonia.Thickness(10, 6),
                CornerRadius = new Avalonia.CornerRadius(4)
            };
            settingsBtn.Click += (s, e) => ShowSettingsDialog();

            var convertBtn = new Button
            {
                Content = "转换",
                Background = Avalonia.Media.Brush.Parse("#9b59b6"),
                Foreground = Avalonia.Media.Brushes.White,
                Padding = new Avalonia.Thickness(20, 8),
                CornerRadius = new Avalonia.CornerRadius(20)
            };
            convertBtn.Click += (s, e) => StartConversion();

            panel.Children.Add(encoderCombo);
            panel.Children.Add(audioEncoderCombo);
            panel.Children.Add(settingsBtn);
            panel.Children.Add(convertBtn);

            return panel;
        }

        // 移除文件
        private void RemoveFile(string filePath)
        {
            _selectedFiles.Remove(filePath);

            var fileListContainer = this.FindControl<StackPanel>("FileListContainer");
            if (fileListContainer != null)
            {
                // 查找要删除的Border元素
                Border? itemToRemove = null;
                foreach (var child in fileListContainer.Children)
                {
                    if (child is Border border && border.Tag?.ToString() == filePath)
                    {
                        itemToRemove = border;
                        break;
                    }
                }

                if (itemToRemove != null)
                {
                    fileListContainer.Children.Remove(itemToRemove);
                }
            }

            if (_selectedFiles.Count == 0)
            {
                _hasFiles = false;
                UpdateViewState();
            }
        }

        // 格式化文件大小
        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        // 设置按钮点击事件
        private void SettingsBtn_Click(object? sender, RoutedEventArgs e)
        {
            ShowSettingsDialog();
        }

        private async void ShowSettingsDialog()
        {
            var settingsWindow = new ConversionSettingsWindow(new ConversionSettings());

            // 获取主窗口作为父窗口
            var mainWindow = TopLevel.GetTopLevel(this) as Window;

            if (mainWindow != null)
            {
                var result = await settingsWindow.ShowDialog<ConversionSettings?>(mainWindow);
                if (result != null)
                {
                    // 处理设置结果
                }
            }
        }

        // 转换按钮点击事件
        private void ConvertBtn_Click(object? sender, RoutedEventArgs e)
        {
            StartConversion();
        }

        // 转换全部按钮点击事件
        private void ConvertAllBtn_Click(object? sender, RoutedEventArgs e)
        {
            StartConversion();
        }

        private void StartConversion()
        {
            // 开始转换逻辑
            // 这里应该切换到转换进度显示
        }
    }
}
