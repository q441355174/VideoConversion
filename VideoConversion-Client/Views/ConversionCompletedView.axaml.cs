using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using System.Linq;

namespace VideoConversion_Client.Views
{
    public partial class ConversionCompletedView : UserControl
    {

        public ConversionCompletedView()
        {
            InitializeComponent();
            LoadCompletedFiles();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }



        private void SearchBtn_Click(object? sender, RoutedEventArgs e)
        {
            var searchBox = this.FindControl<TextBox>("SearchBox");
<<<<<<< HEAD
<<<<<<< HEAD
            var searchToggleBtn = this.FindControl<Button>("SearchToggleBtn");

            if (searchBox != null && searchToggleBtn != null)
=======
            if (searchBox != null)
>>>>>>> 7da76723b16f649c8e4abde3199589642f4de608
=======
            if (searchBox != null)
>>>>>>> 7da76723b16f649c8e4abde3199589642f4de608
            {
                searchBox.IsVisible = !searchBox.IsVisible;
                if (searchBox.IsVisible)
                {
                    searchBox.Focus();
<<<<<<< HEAD
<<<<<<< HEAD
                    searchToggleBtn.Content = "✕";
                    Avalonia.Controls.ToolTip.SetTip(searchToggleBtn, "关闭搜索");
                }
                else
                {
                    searchBox.Text = "";
                    searchToggleBtn.Content = "🔍";
                    Avalonia.Controls.ToolTip.SetTip(searchToggleBtn, "搜索文件");
                    // 清除搜索过滤，显示所有文件
                    FilterCompletedFiles("");
                }
            }
        }

        private void SearchBox_TextChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e)
        {
            if (sender is TextBox searchBox)
            {
                FilterCompletedFiles(searchBox.Text ?? "");
            }
        }

        private void FilterCompletedFiles(string searchText)
        {
            var completedContainer = this.FindControl<StackPanel>("CompletedFileListContainer");
            if (completedContainer == null) return;

            foreach (var child in completedContainer.Children)
            {
                if (child is Border border && border.Tag is string fileName)
                {
                    // 根据文件名进行过滤
                    bool isVisible = string.IsNullOrEmpty(searchText) ||
                                   fileName.Contains(searchText, StringComparison.OrdinalIgnoreCase);
                    border.IsVisible = isVisible;
                }
            }

            // 更新统计信息（只计算可见的文件）
            UpdateFilteredStats(searchText);
        }

        private void UpdateFilteredStats(string searchText)
        {
            var completedContainer = this.FindControl<StackPanel>("CompletedFileListContainer");
            var statsText = this.FindControl<TextBlock>("CompletedStatsText");

            if (completedContainer != null && statsText != null)
            {
                var visibleCount = completedContainer.Children
                    .OfType<Border>()
                    .Count(border => border.IsVisible);

                var totalCount = completedContainer.Children.Count;

                if (string.IsNullOrEmpty(searchText))
                {
                    statsText.Text = totalCount == 0 ? "0 项，0 GB" : $"{totalCount} 项，28.45 GB";
                }
                else
                {
                    statsText.Text = $"找到 {visibleCount} 项（共 {totalCount} 项）";
=======
>>>>>>> 7da76723b16f649c8e4abde3199589642f4de608
=======
>>>>>>> 7da76723b16f649c8e4abde3199589642f4de608
                }
            }
        }

        private void OpenFolderBtn_Click(object? sender, RoutedEventArgs e)
        {
            // 打开输出文件夹
            // System.Diagnostics.Process.Start("explorer.exe", outputFolderPath);
        }

        private void LoadCompletedFiles()
        {
            var completedContainer = this.FindControl<StackPanel>("CompletedFileListContainer");
            if (completedContainer == null) return;

            // 清空现有项目
            completedContainer.Children.Clear();

<<<<<<< HEAD
<<<<<<< HEAD
            // 添加示例文件用于测试搜索功能
=======
            // 添加示例完成文件（与设计图一致）
>>>>>>> 7da76723b16f649c8e4abde3199589642f4de608
=======
            // 添加示例完成文件（与设计图一致）
>>>>>>> 7da76723b16f649c8e4abde3199589642f4de608
            var sampleFiles = new[]
            {
                new { Name = "FC2PPV-4649081 (2)", Format = "MP4", Resolution = "1920*1080", Size = "1.12 GB", Duration = "51:09" },
                new { Name = "FC2PPV-4647933 (2)", Format = "MP4", Resolution = "1920*1080", Size = "1.18 GB", Duration = "54:00" },
                new { Name = "FC2PPV-4647709 (2)", Format = "MP4", Resolution = "1920*1080", Size = "1.34 GB", Duration = "01:01:24" },
                new { Name = "FC2PPV-4647352-1 (2)", Format = "MP4", Resolution = "1920*1080", Size = "1.64 GB", Duration = "01:14:50" },
<<<<<<< HEAD
<<<<<<< HEAD
                new { Name = "FC2PPV-4647381-1 (2)", Format = "MP4", Resolution = "1920*1080", Size = "1.45 GB", Duration = "01:08:30" },
                new { Name = "测试视频文件", Format = "AVI", Resolution = "1280*720", Size = "0.85 GB", Duration = "45:30" },
                new { Name = "会议录像", Format = "MOV", Resolution = "1920*1080", Size = "2.1 GB", Duration = "01:30:15" }
=======
                new { Name = "FC2PPV-4647381-1 (2)", Format = "MP4", Resolution = "1920*1080", Size = "1.45 GB", Duration = "01:08:30" }
>>>>>>> 7da76723b16f649c8e4abde3199589642f4de608
=======
                new { Name = "FC2PPV-4647381-1 (2)", Format = "MP4", Resolution = "1920*1080", Size = "1.45 GB", Duration = "01:08:30" }
>>>>>>> 7da76723b16f649c8e4abde3199589642f4de608
            };

            foreach (var file in sampleFiles)
            {
                var completedItem = CreateCompletedFileItemUI(file.Name, file.Format, file.Resolution, file.Size, file.Duration);
                completedContainer.Children.Add(completedItem);
            }

<<<<<<< HEAD
<<<<<<< HEAD
            // 更新统计信息和空状态显示
            UpdateCompletedStats();
            UpdateEmptyStateVisibility();
=======
            // 更新统计信息
            UpdateCompletedStats();
>>>>>>> 7da76723b16f649c8e4abde3199589642f4de608
=======
            // 更新统计信息
            UpdateCompletedStats();
>>>>>>> 7da76723b16f649c8e4abde3199589642f4de608
        }

        // 创建完成文件项UI
        private Border CreateCompletedFileItemUI(string fileName, string format, string resolution, string size, string duration)
        {
            var border = new Border
            {
                Background = Avalonia.Media.Brushes.White,
                BorderBrush = Avalonia.Media.Brush.Parse("#e0e0e0"),
                BorderThickness = new Avalonia.Thickness(1),
                CornerRadius = new Avalonia.CornerRadius(8),
                Padding = new Avalonia.Thickness(15),
                Margin = new Avalonia.Thickness(0, 5),
                Tag = fileName // 用于标识文件名
            };

            var grid = new Grid();
            // 添加列定义
            grid.ColumnDefinitions.Add(new ColumnDefinition(80, GridUnitType.Pixel));
            grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            // 缩略图
            var thumbnailBorder = new Border
            {
                Background = Avalonia.Media.Brush.Parse("#f0f0f0"),
                CornerRadius = new Avalonia.CornerRadius(6),
                Width = 60,
                Height = 45
            };
            Grid.SetColumn(thumbnailBorder, 0);

            // 文件名
            var namePanel = new StackPanel
            {
                Margin = new Avalonia.Thickness(15, 0),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            var fileNameText = new TextBlock
            {
                Text = fileName,
                FontSize = 14,
                FontWeight = Avalonia.Media.FontWeight.Medium,
                Foreground = Avalonia.Media.Brush.Parse("#333"),
                TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis
            };
            var editIcon = new TextBlock
            {
                Text = "✏️",
                FontSize = 12,
                Foreground = Avalonia.Media.Brush.Parse("#999"),
                Margin = new Avalonia.Thickness(0, 2, 0, 0)
            };
            namePanel.Children.Add(fileNameText);
            namePanel.Children.Add(editIcon);
            Grid.SetColumn(namePanel, 1);

            // 格式
            var formatPanel = CreateInfoColumn("📄", format);
            Grid.SetColumn(formatPanel, 2);

            // 分辨率
            var resolutionPanel = CreateInfoColumn("📐", resolution);
            Grid.SetColumn(resolutionPanel, 3);

            // 文件大小
            var sizePanel = CreateInfoColumn("💾", size);
            Grid.SetColumn(sizePanel, 4);

            // 时长
            var durationPanel = CreateInfoColumn("⏱️", duration);
            Grid.SetColumn(durationPanel, 5);

            // 删除按钮
            var deleteBtn = new Button
            {
                Content = "✕",
                Background = Avalonia.Media.Brushes.Transparent,
                Foreground = Avalonia.Media.Brush.Parse("#999"),
                BorderThickness = new Avalonia.Thickness(0),
                Padding = new Avalonia.Thickness(8),
                Margin = new Avalonia.Thickness(10, 0, 5, 0)
            };
            deleteBtn.Click += (s, e) => RemoveCompletedFile(fileName);
            Grid.SetColumn(deleteBtn, 6);

            // 文件夹按钮
            var folderBtn = new Button
            {
                Content = "📁",
                Background = Avalonia.Media.Brushes.Transparent,
                Foreground = Avalonia.Media.Brush.Parse("#999"),
                BorderThickness = new Avalonia.Thickness(0),
                Padding = new Avalonia.Thickness(8),
                Margin = new Avalonia.Thickness(5, 0)
            };
            folderBtn.Click += (s, e) => OpenFileFolder(fileName);
            Grid.SetColumn(folderBtn, 7);

            // 更多选项按钮
            var moreBtn = new Button
            {
                Content = "⋯",
                Background = Avalonia.Media.Brushes.Transparent,
                Foreground = Avalonia.Media.Brush.Parse("#999"),
                BorderThickness = new Avalonia.Thickness(0),
                Padding = new Avalonia.Thickness(8),
                Margin = new Avalonia.Thickness(5, 0, 0, 0)
            };
            Grid.SetColumn(moreBtn, 8);

            grid.Children.Add(thumbnailBorder);
            grid.Children.Add(namePanel);
            grid.Children.Add(formatPanel);
            grid.Children.Add(resolutionPanel);
            grid.Children.Add(sizePanel);
            grid.Children.Add(durationPanel);
            grid.Children.Add(deleteBtn);
            grid.Children.Add(folderBtn);
            grid.Children.Add(moreBtn);

            border.Child = grid;
            return border;
        }

        // 创建信息列
        private StackPanel CreateInfoColumn(string icon, string text)
        {
            var panel = new StackPanel
            {
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Margin = new Avalonia.Thickness(10, 0)
            };

            var iconText = new TextBlock
            {
                Text = icon,
                FontSize = 12,
                Foreground = Avalonia.Media.Brush.Parse("#666"),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };

            var valueText = new TextBlock
            {
                Text = text,
                FontSize = 12,
                Foreground = Avalonia.Media.Brush.Parse("#666"),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };

            panel.Children.Add(iconText);
            panel.Children.Add(valueText);

            return panel;
        }

        private void RemoveCompletedFile(string fileName)
        {
            var completedContainer = this.FindControl<StackPanel>("CompletedFileListContainer");
            if (completedContainer != null)
            {
                // 查找要删除的Border元素
                Border? itemToRemove = null;
                foreach (var child in completedContainer.Children)
                {
                    if (child is Border border && border.Tag?.ToString() == fileName)
                    {
                        itemToRemove = border;
                        break;
                    }
                }

                if (itemToRemove != null)
                {
                    completedContainer.Children.Remove(itemToRemove);
                    UpdateCompletedStats();
<<<<<<< HEAD
<<<<<<< HEAD
                    UpdateEmptyStateVisibility();
=======
>>>>>>> 7da76723b16f649c8e4abde3199589642f4de608
=======
>>>>>>> 7da76723b16f649c8e4abde3199589642f4de608
                }
            }
        }

        private void OpenFileFolder(string fileName)
        {
            // 实际应该打开文件所在的文件夹
            // System.Diagnostics.Process.Start("explorer.exe", "/select," + filePath);
        }

        private void UpdateCompletedStats()
        {
            var completedContainer = this.FindControl<StackPanel>("CompletedFileListContainer");
            var statsText = this.FindControl<TextBlock>("CompletedStatsText");
<<<<<<< HEAD
<<<<<<< HEAD
            var clearButton = this.FindControl<Button>("ClearListButton");
            var searchBox = this.FindControl<TextBox>("SearchBox");

            if (completedContainer != null && statsText != null)
            {
                var count = completedContainer.Children.Count;

                // 如果正在搜索，使用过滤后的统计
                if (searchBox != null && searchBox.IsVisible && !string.IsNullOrEmpty(searchBox.Text))
                {
                    UpdateFilteredStats(searchBox.Text);
                }
                else
                {
                    // 正常统计
                    if (count == 0)
                    {
                        statsText.Text = "0 项，0 GB";
                        if (clearButton != null)
                            clearButton.IsVisible = false;
                    }
                    else
                    {
                        statsText.Text = $"{count} 项，28.45 GB";
                        if (clearButton != null)
                            clearButton.IsVisible = true;
                    }
                }
            }
        }

        private void UpdateEmptyStateVisibility()
        {
            var completedContainer = this.FindControl<StackPanel>("CompletedFileListContainer");
            var emptyStatePanel = this.FindControl<StackPanel>("EmptyStatePanel");
            var fileListScrollViewer = this.FindControl<ScrollViewer>("FileListScrollViewer");

            if (completedContainer != null && emptyStatePanel != null && fileListScrollViewer != null)
            {
                bool hasFiles = completedContainer.Children.Count > 0;
                emptyStatePanel.IsVisible = !hasFiles;
                fileListScrollViewer.IsVisible = hasFiles;
            }
        }

        // 新增的事件处理方法
        private void StartConversionBtn_Click(object? sender, RoutedEventArgs e)
        {
            // 切换到文件上传视图
            // 这里需要与主窗口通信，切换到上传文件的标签页
            if (Parent?.Parent?.Parent is MainWindow mainWindow)
            {
                // 假设主窗口有切换到上传页面的方法
                // mainWindow.SwitchToUploadView();
            }
        }

        private void ClearListBtn_Click(object? sender, RoutedEventArgs e)
        {
            var completedContainer = this.FindControl<StackPanel>("CompletedFileListContainer");
            if (completedContainer != null)
            {
                completedContainer.Children.Clear();
                UpdateCompletedStats();
                UpdateEmptyStateVisibility();
            }
        }

        private void OpenOutputFolderBtn_Click(object? sender, RoutedEventArgs e)
        {
            // 打开输出文件夹
            try
            {
                var outputPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "VideoConversion", "Output");
                if (System.IO.Directory.Exists(outputPath))
                {
                    System.Diagnostics.Process.Start("explorer.exe", outputPath);
                }
                else
                {
                    // 如果文件夹不存在，创建它
                    System.IO.Directory.CreateDirectory(outputPath);
                    System.Diagnostics.Process.Start("explorer.exe", outputPath);
                }
            }
            catch (Exception ex)
            {
                // 处理错误，可以显示消息框或日志
                System.Diagnostics.Debug.WriteLine($"打开输出文件夹失败: {ex.Message}");
=======
=======
>>>>>>> 7da76723b16f649c8e4abde3199589642f4de608
            
            if (completedContainer != null && statsText != null)
            {
                var count = completedContainer.Children.Count;
                // 这里应该计算实际的总大小，现在使用示例数据
                statsText.Text = $"{count} 项，28.45 GB";
<<<<<<< HEAD
>>>>>>> 7da76723b16f649c8e4abde3199589642f4de608
=======
>>>>>>> 7da76723b16f649c8e4abde3199589642f4de608
            }
        }
    }
}
