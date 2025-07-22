using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using VideoConversion_Client.Models;

namespace VideoConversion_Client.Views
{
    public partial class RecentTasksView : UserControl
    {
        // 事件定义
        public event EventHandler? RefreshRequested;
        public event EventHandler<ConversionTask>? TaskSelected;

        private List<ConversionTask> tasks = new List<ConversionTask>();

        public RecentTasksView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void RefreshRecentTasksButton_Click(object? sender, RoutedEventArgs e)
        {
            RefreshRequested?.Invoke(this, EventArgs.Empty);
        }

        // 公共方法
        public void UpdateTasks(IEnumerable<ConversionTask> newTasks)
        {
            tasks = newTasks.ToList();
            RefreshDisplay();
        }

        public void AddTask(ConversionTask task)
        {
            tasks.Insert(0, task);
            if (tasks.Count > 10)
            {
                tasks = tasks.Take(10).ToList();
            }
            RefreshDisplay();
        }

        public void UpdateTask(ConversionTask updatedTask)
        {
            var existingTask = tasks.FirstOrDefault(t => t.Id == updatedTask.Id);
            if (existingTask != null)
            {
                var index = tasks.IndexOf(existingTask);
                tasks[index] = updatedTask;
                RefreshDisplay();
            }
        }

        private void RefreshDisplay()
        {
            var recentTasksPanel = this.FindControl<StackPanel>("RecentTasksPanel");
            var emptyState = this.FindControl<Border>("EmptyState");
            
            if (recentTasksPanel == null) return;

            // 清除现有内容（除了空状态）
            var itemsToRemove = recentTasksPanel.Children
                .Where(child => child != emptyState)
                .ToList();
            
            foreach (var item in itemsToRemove)
            {
                recentTasksPanel.Children.Remove(item);
            }

            if (tasks.Count == 0)
            {
                // 显示空状态
                if (emptyState != null)
                    emptyState.IsVisible = true;
            }
            else
            {
                // 隐藏空状态
                if (emptyState != null)
                    emptyState.IsVisible = false;

                // 添加任务项
                foreach (var task in tasks)
                {
                    var taskItem = CreateTaskItem(task);
                    recentTasksPanel.Children.Add(taskItem);
                }
            }
        }

        private Border CreateTaskItem(ConversionTask task)
        {
            var border = new Border
            {
                Classes = { "info-panel" },
                Margin = new Avalonia.Thickness(0, 0, 0, 8),
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
            };

            // 添加点击事件
            border.Tapped += (s, e) => TaskSelected?.Invoke(this, task);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var leftPanel = new StackPanel();
            
            leftPanel.Children.Add(new TextBlock
            {
                Text = task.TaskName,
                FontWeight = Avalonia.Media.FontWeight.Bold,
                FontSize = 13
            });
            
            leftPanel.Children.Add(new TextBlock
            {
                Text = task.OriginalFileName,
                FontSize = 11,
                Foreground = Avalonia.Media.Brush.Parse("#7f8c8d")
            });

            leftPanel.Children.Add(new TextBlock
            {
                Text = $"创建时间: {task.CreatedAt:MM-dd HH:mm}",
                FontSize = 10,
                Foreground = Avalonia.Media.Brush.Parse("#95a5a6")
            });

            var rightPanel = new StackPanel
            {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
            };
            
            var statusColor = GetStatusColor(task.Status);
            rightPanel.Children.Add(new TextBlock
            {
                Text = task.StatusText,
                FontSize = 11,
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Foreground = statusColor
            });
            
            rightPanel.Children.Add(new TextBlock
            {
                Text = task.ProgressText,
                FontSize = 11,
                Foreground = Avalonia.Media.Brush.Parse("#7f8c8d"),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
            });

            if (task.ConversionSpeed.HasValue && task.Status == ConversionStatus.Converting)
            {
                rightPanel.Children.Add(new TextBlock
                {
                    Text = $"{task.ConversionSpeed.Value:F1}x",
                    FontSize = 10,
                    Foreground = Avalonia.Media.Brush.Parse("#27ae60"),
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
                });
            }

            Grid.SetColumn(leftPanel, 0);
            Grid.SetColumn(rightPanel, 1);
            
            grid.Children.Add(leftPanel);
            grid.Children.Add(rightPanel);
            
            border.Child = grid;
            return border;
        }

        private Avalonia.Media.IBrush GetStatusColor(ConversionStatus status)
        {
            return status switch
            {
                ConversionStatus.Pending => Avalonia.Media.Brush.Parse("#f39c12"),
                ConversionStatus.Converting => Avalonia.Media.Brush.Parse("#3498db"),
                ConversionStatus.Completed => Avalonia.Media.Brush.Parse("#27ae60"),
                ConversionStatus.Failed => Avalonia.Media.Brush.Parse("#e74c3c"),
                ConversionStatus.Cancelled => Avalonia.Media.Brush.Parse("#95a5a6"),
                _ => Avalonia.Media.Brush.Parse("#7f8c8d")
            };
        }

        // 属性
        public int TaskCount => tasks.Count;
        public IReadOnlyList<ConversionTask> Tasks => tasks.AsReadOnly();
    }
}
