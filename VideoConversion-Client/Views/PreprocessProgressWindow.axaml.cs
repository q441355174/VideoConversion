using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace VideoConversion_Client.Views
{
    public partial class PreprocessProgressWindow : Window
    {
        private CancellationTokenSource? _cancellationTokenSource;
        private int _totalFiles = 0;
        private int _processedFiles = 0;

        public PreprocessProgressWindow()
        {
            InitializeComponent();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// 获取取消令牌
        /// </summary>
        public CancellationToken CancellationToken => _cancellationTokenSource?.Token ?? CancellationToken.None;

        /// <summary>
        /// 是否已取消
        /// </summary>
        public bool IsCancelled => _cancellationTokenSource?.IsCancellationRequested ?? false;

        /// <summary>
        /// 初始化进度显示
        /// </summary>
        public void InitializeProgress(IEnumerable<string> filePaths)
        {
            Dispatcher.UIThread.Post(() =>
            {
                _totalFiles = filePaths.Count();
                _processedFiles = 0;

                UpdateProgress();
            });
        }

        /// <summary>
        /// 更新文件处理状态
        /// </summary>
        public void UpdateFileStatus(string filePath, string status, double progress = -1)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var statusText = this.FindControl<TextBlock>("StatusText");
                var currentFileText = this.FindControl<TextBlock>("CurrentFileText");

                if (statusText != null)
                {
                    statusText.Text = status;
                }

                if (currentFileText != null)
                {
                    var fileName = System.IO.Path.GetFileName(filePath);
                    currentFileText.Text = fileName;
                }
            });
        }

        /// <summary>
        /// 标记文件处理完成
        /// </summary>
        public void MarkFileCompleted(string filePath, bool success)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (success)
                {
                    _processedFiles++;
                }

                UpdateProgress();

                // 检查是否全部完成
                if (_processedFiles >= _totalFiles)
                {
                    MarkAllCompleted();
                }
            });
        }

        /// <summary>
        /// 更新进度显示
        /// </summary>
        private void UpdateProgress()
        {
            var progressText = this.FindControl<TextBlock>("ProgressText");
            var progressBar = this.FindControl<ProgressBar>("ProgressBar");

            if (progressText != null)
            {
                progressText.Text = $"{_processedFiles}/{_totalFiles}";
            }

            if (progressBar != null)
            {
                var progressPercentage = _totalFiles > 0 ? (double)_processedFiles / _totalFiles * 100 : 0;
                progressBar.Value = progressPercentage;
            }
        }

        /// <summary>
        /// 标记全部完成
        /// </summary>
        private void MarkAllCompleted()
        {
            var statusText = this.FindControl<TextBlock>("StatusText");
            var currentFileText = this.FindControl<TextBlock>("CurrentFileText");
            var cancelButton = this.FindControl<Button>("CancelButton");

            if (statusText != null)
            {
                statusText.Text = "处理完成";
            }

            if (currentFileText != null)
            {
                currentFileText.Text = $"共处理 {_processedFiles} 个文件";
            }

            if (cancelButton != null)
            {
                cancelButton.Content = "关闭";
            }
        }

        /// <summary>
        /// 取消/关闭按钮点击
        /// </summary>
        private void CancelButton_Click(object? sender, RoutedEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            Close();
        }

        /// <summary>
        /// 窗口关闭时清理资源
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            base.OnClosed(e);
        }
    }
}
