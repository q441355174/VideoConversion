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
        private ConversionCompletedView conversionCompletedView;
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

            // 初始化界面状态
            InitializeViewState();

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
            conversionCompletedView = this.FindControl<ConversionCompletedView>("ConversionCompletedView")!;
        }

        private void SetupEventHandlers()
        {
            // ViewModel属性变化事件
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void InitializeViewState()
        {
            // 默认显示文件上传界面
            SwitchToFileUploadView();
        }

        // 切换按钮事件处理方法
        private void ConvertingStatusBtn_Click(object? sender, RoutedEventArgs e)
        {
            SwitchToFileUploadView();
        }

        private void CompletedStatusBtn_Click(object? sender, RoutedEventArgs e)
        {
            SwitchToCompletedView();
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



        // 界面切换方法
        private void SwitchToFileUploadView()
        {
            // 切换页面显示
            fileUploadView.IsVisible = true;
            conversionCompletedView.IsVisible = false;

            // 更新按钮状态
            UpdateButtonStates(true);

            UpdateStatus("📁 文件上传界面");
        }

        private void SwitchToCompletedView()
        {
            // 切换页面显示
            fileUploadView.IsVisible = false;
            conversionCompletedView.IsVisible = true;

            // 更新按钮状态
            UpdateButtonStates(false);

            UpdateStatus("✅ 转换完成界面");
        }

        // 更新切换按钮的状态
        private void UpdateButtonStates(bool isConvertingActive)
        {
            var convertingBtn = this.FindControl<Button>("ConvertingStatusBtn");
            var completedBtn = this.FindControl<Button>("CompletedStatusBtn");

            if (convertingBtn != null && completedBtn != null)
            {
                if (isConvertingActive)
                {
                    // 正在转换按钮激活
                    convertingBtn.Background = Avalonia.Media.Brush.Parse("#9b59b6");
                    convertingBtn.Foreground = Avalonia.Media.Brushes.White;

                    // 转换完成按钮非激活
                    completedBtn.Background = Avalonia.Media.Brush.Parse("#f0f0f0");
                    completedBtn.Foreground = Avalonia.Media.Brush.Parse("#666");
                }
                else
                {
                    // 正在转换按钮非激活
                    convertingBtn.Background = Avalonia.Media.Brush.Parse("#f0f0f0");
                    convertingBtn.Foreground = Avalonia.Media.Brush.Parse("#666");

                    // 转换完成按钮激活
                    completedBtn.Background = Avalonia.Media.Brush.Parse("#9b59b6");
                    completedBtn.Foreground = Avalonia.Media.Brushes.White;
                }
            }
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

            if (indicator != null)
            {
                indicator.Background = connected ?
                    Avalonia.Media.Brushes.Green :
                    Avalonia.Media.Brushes.Red;
            }

            if (statusText != null)
            {
                statusText.Text = connected ?
                    $"SignalR连接: 已连接 ({viewModel.ServerUrl})" :
                    $"SignalR连接: 连接失败: 由于目标计算机积极拒绝，无法连接。 ({viewModel.ServerUrl})";
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
          