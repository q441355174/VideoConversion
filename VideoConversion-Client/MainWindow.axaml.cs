using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VideoConversion_Client.Models;
using VideoConversion_Client.ViewModels;
using VideoConversion_Client.Views;

namespace VideoConversion_Client
{
    public partial class MainWindow : Window
    {
        // ViewModel
        private MainWindowViewModel viewModel;

        // View组件
        private FileUploadView fileUploadView;
        private ConversionSettingsView conversionSettingsView;
        private CurrentTaskView currentTaskView;
        private RecentTasksView recentTasksView;

        public MainWindow()
        {
            InitializeComponent();

            // 初始化ViewModel
            viewModel = new MainWindowViewModel();
            DataContext = viewModel;

            // 获取View组件引用
            InitializeViewComponents();

            // 设置事件处理
            SetupEventHandlers();

            // 窗口关闭事件
            Closing += OnWindowClosing;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void InitializeViewComponents()
        {
            // 获取View组件引用
            fileUploadView = this.FindControl<FileUploadView>("FileUploadView")!;
            conversionSettingsView = this.FindControl<ConversionSettingsView>("ConversionSettingsView")!;
            currentTaskView = this.FindControl<CurrentTaskView>("CurrentTaskView")!;
            recentTasksView = this.FindControl<RecentTasksView>("RecentTasksView")!;
        }

        private void SetupEventHandlers()
        {
            // 文件上传View事件
            fileUploadView.FileSelected += OnFileSelected;
            fileUploadView.FileCleared += OnFileCleared;

            // 转换设置View事件
            conversionSettingsView.ConversionStartRequested += OnConversionStartRequested;

            // 当前任务View事件
            currentTaskView.TaskCancelRequested += OnTaskCancelRequested;
            currentTaskView.TaskRefreshRequested += OnTaskRefreshRequested;
            currentTaskView.TaskDownloadRequested += OnTaskDownloadRequested;

            // 最近任务View事件
            recentTasksView.RefreshRequested += OnRecentTasksRefreshRequested;
            recentTasksView.TaskSelected += OnTaskSelected;

            // ViewModel属性变化事件
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        // 事件处理方法
        private void OnFileSelected(object? sender, string filePath)
        {
            // 自动设置任务名称
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            conversionSettingsView.SetTaskName(fileName);

            UpdateStatus($"✅ 已选择文件: {Path.GetFileName(filePath)}");
        }

        private void OnFileCleared(object? sender, EventArgs e)
        {
            conversionSettingsView.SetTaskName("");
            UpdateStatus("📂 请选择视频文件");
        }

        private async void OnConversionStartRequested(object? sender, ConversionStartEventArgs e)
        {
            if (string.IsNullOrEmpty(fileUploadView.SelectedFilePath))
            {
                UpdateStatus("⚠️ 请先选择视频文件");
                return;
            }

            if (string.IsNullOrEmpty(e.TaskName))
            {
                UpdateStatus("⚠️ 请输入任务名称");
                return;
            }

            conversionSettingsView.SetEnabled(false);
            var success = await viewModel.StartConversionAsync(fileUploadView.SelectedFilePath, e);

            if (success)
            {
                // 显示当前任务
                var task = viewModel.ConversionTasks.FirstOrDefault();
                if (task != null)
                {
                    currentTaskView.ShowTask(task);
                    currentTaskView.SetFileSize(GetFileSize(fileUploadView.SelectedFilePath));
                    currentTaskView.SetOutputFormat(e.OutputFormat.ToUpper());
                }

                // 更新最近任务列表
                recentTasksView.UpdateTasks(viewModel.ConversionTasks);
            }

            conversionSettingsView.SetEnabled(true);
        }

        private async void OnTaskCancelRequested(object? sender, string taskId)
        {
            var success = await viewModel.CancelTaskAsync(taskId);
            if (success)
            {
                currentTaskView.HideTask();
            }
        }

        private async void OnTaskRefreshRequested(object? sender, string taskId)
        {
            // 刷新任务状态的逻辑可以在这里实现
            UpdateStatus("🔄 任务状态已刷新");
        }

        private async void OnTaskDownloadRequested(object? sender, string taskId)
        {
            try
            {
                var options = new FolderPickerOpenOptions
                {
                    Title = "选择保存位置"
                };

                var result = await StorageProvider.OpenFolderPickerAsync(options);
                var folder = result?.FirstOrDefault();

                if (folder != null)
                {
                    var savePath = Path.Combine(folder.Path.LocalPath, $"converted_{taskId}.mp4");
                    // 这里需要实现下载逻辑
                    UpdateStatus($"✅ 文件已下载到: {savePath}");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"❌ 下载失败: {ex.Message}");
            }
        }

        private async void OnRecentTasksRefreshRequested(object? sender, EventArgs e)
        {
            await viewModel.LoadRecentTasks();
            recentTasksView.UpdateTasks(viewModel.ConversionTasks);
        }

        private void OnTaskSelected(object? sender, ConversionTask task)
        {
            // 显示选中的任务详情
            currentTaskView.ShowTask(task);
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(MainWindowViewModel.StatusText):
                    UpdateStatus(viewModel.StatusText);
                    break;
                case nameof(MainWindowViewModel.IsConnectedToServer):
                    UpdateConnectionIndicator(viewModel.IsConnectedToServer);
                    break;
            }
        }

        // 测试连接按钮事件
        private async void TestConnectionButton_Click(object? sender, RoutedEventArgs e)
        {
            await viewModel.TestConnectionAsync();
        }

        // 辅助方法
        private void UpdateStatus(string status)
        {
            var statusText = this.FindControl<TextBlock>("StatusText");
            if (statusText != null)
            {
                statusText.Text = status;
            }
        }

        private void UpdateConnectionIndicator(bool connected)
        {
            var indicator = this.FindControl<Border>("ConnectionIndicator");
            var statusText = this.FindControl<TextBlock>("ConnectionStatusText");
            var serverUrl = this.FindControl<TextBlock>("ServerUrl");

            if (indicator != null)
            {
                indicator.Background = connected ?
                    Avalonia.Media.Brushes.Green :
                    Avalonia.Media.Brushes.Red;
            }

            if (statusText != null)
            {
                statusText.Text = connected ? "已连接" : "未连接";
            }

            if (serverUrl != null)
            {
                serverUrl.Text = viewModel.ServerUrl;
            }
        }

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

        private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
        {
            try
            {
                await viewModel.CleanupAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清理资源失败: {ex.Message}");
            }
        }
    }
}
          