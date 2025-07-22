using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using VideoConversion_Client.Models;

namespace VideoConversion_Client.Views
{
    public partial class CurrentTaskView : UserControl
    {
        // 事件定义
        public event EventHandler<string>? TaskCancelRequested;
        public event EventHandler<string>? TaskRefreshRequested;
        public event EventHandler<string>? TaskDownloadRequested;

        private string? currentTaskId;
        private DateTime? taskStartTime;

        public CurrentTaskView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        // 按钮事件处理
        private void CancelTaskButton_Click(object? sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(currentTaskId))
            {
                TaskCancelRequested?.Invoke(this, currentTaskId);
            }
        }

        private void RefreshTaskButton_Click(object? sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(currentTaskId))
            {
                TaskRefreshRequested?.Invoke(this, currentTaskId);
            }
        }

        private void DownloadTaskButton_Click(object? sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(currentTaskId))
            {
                TaskDownloadRequested?.Invoke(this, currentTaskId);
            }
        }

        // 公共方法
        public void ShowTask(ConversionTask task)
        {
            currentTaskId = task.Id;
            taskStartTime = task.StartedAt ?? DateTime.Now;

            var currentTaskSection = this.FindControl<Border>("CurrentTaskSection");
            var currentTaskName = this.FindControl<TextBlock>("CurrentTaskName");
            var currentTaskIdText = this.FindControl<TextBlock>("CurrentTaskId");
            var originalFileName = this.FindControl<TextBlock>("OriginalFileName");
            var outputFormat = this.FindControl<TextBlock>("OutputFormat");

            if (currentTaskSection != null)
                currentTaskSection.IsVisible = true;

            if (currentTaskName != null)
                currentTaskName.Text = task.TaskName;

            if (currentTaskIdText != null)
                currentTaskIdText.Text = $"ID: {task.Id}";

            if (originalFileName != null)
                originalFileName.Text = task.OriginalFileName;

            if (outputFormat != null)
                outputFormat.Text = "MP4"; // 可以从任务信息中获取

            UpdateProgress(task.Progress, task.ConversionSpeed, task.EstimatedTimeRemaining);
            UpdateStatus(task.Status.ToString());
        }

        public void HideTask()
        {
            var currentTaskSection = this.FindControl<Border>("CurrentTaskSection");
            if (currentTaskSection != null)
                currentTaskSection.IsVisible = false;

            currentTaskId = null;
            taskStartTime = null;
        }

        public void UpdateProgress(int progress, double? speed = null, int? remainingSeconds = null)
        {
            var conversionProgressBar = this.FindControl<ProgressBar>("ConversionProgressBar");
            var conversionProgressText = this.FindControl<TextBlock>("ConversionProgressText");
            var conversionSpeed = this.FindControl<TextBlock>("ConversionSpeed");
            var remainingTime = this.FindControl<TextBlock>("RemainingTime");
            var elapsedTime = this.FindControl<TextBlock>("ElapsedTime");

            if (conversionProgressBar != null)
                conversionProgressBar.Value = progress;

            if (conversionProgressText != null)
                conversionProgressText.Text = $"{progress}%";

            if (conversionSpeed != null)
                conversionSpeed.Text = speed.HasValue ? $"{speed.Value:F1}x" : "-";

            if (remainingTime != null)
                remainingTime.Text = remainingSeconds.HasValue ? 
                    TimeSpan.FromSeconds(remainingSeconds.Value).ToString(@"hh\:mm\:ss") : "-";

            if (elapsedTime != null && taskStartTime.HasValue)
            {
                var elapsed = DateTime.Now - taskStartTime.Value;
                elapsedTime.Text = elapsed.ToString(@"hh\:mm\:ss");
            }
        }

        public void UpdateStatus(string status)
        {
            var taskStatus = this.FindControl<TextBlock>("TaskStatus");
            var cancelButton = this.FindControl<Button>("CancelTaskButton");
            var refreshButton = this.FindControl<Button>("RefreshTaskButton");
            var downloadButton = this.FindControl<Button>("DownloadTaskButton");

            if (taskStatus != null)
                taskStatus.Text = GetStatusText(status);

            // 根据状态更新按钮可见性
            if (cancelButton != null)
                cancelButton.IsVisible = status == "Pending" || status == "Converting";

            if (refreshButton != null)
                refreshButton.IsVisible = status == "Failed" || status == "Cancelled";

            if (downloadButton != null)
                downloadButton.IsVisible = status == "Completed";
        }

        public void SetFileSize(string fileSize)
        {
            var fileSizeText = this.FindControl<TextBlock>("FileSize");
            if (fileSizeText != null)
                fileSizeText.Text = fileSize;
        }

        public void SetOutputFormat(string format)
        {
            var outputFormat = this.FindControl<TextBlock>("OutputFormat");
            if (outputFormat != null)
                outputFormat.Text = format;
        }

        private string GetStatusText(string status)
        {
            return status switch
            {
                "Pending" => "等待中...",
                "Converting" => "转换中...",
                "Completed" => "转换完成",
                "Failed" => "转换失败",
                "Cancelled" => "已取消",
                _ => status
            };
        }

        // 属性
        public string? TaskId => currentTaskId;
        public new bool IsVisible => this.FindControl<Border>("CurrentTaskSection")?.IsVisible ?? false;
    }
}
