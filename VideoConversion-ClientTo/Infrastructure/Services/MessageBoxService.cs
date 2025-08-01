using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;

namespace VideoConversion_ClientTo.Infrastructure.Services
{
    /// <summary>
    /// 消息框类型
    /// </summary>
    public enum MessageBoxType
    {
        Info,
        Warning,
        Error,
        Success
    }

    /// <summary>
    /// 统一的消息框服务
    /// </summary>
    public interface IMessageBoxService
    {
        Task ShowAsync(string message, string title, MessageBoxType type, Window? owner = null);
        Task ShowErrorAsync(string message, Window? owner = null);
        Task ShowWarningAsync(string message, Window? owner = null);
        Task ShowInfoAsync(string message, Window? owner = null);
        Task ShowSuccessAsync(string message, Window? owner = null);
        Task<bool> ShowConfirmAsync(string message, string title, Window? owner = null);
    }

    public class MessageBoxService : IMessageBoxService
    {
        /// <summary>
        /// 显示消息框
        /// </summary>
        public async Task ShowAsync(string message, string title, MessageBoxType type, Window? owner = null)
        {
            var messageBox = CreateMessageBox(message, title, type);
            
            if (owner != null)
            {
                await messageBox.ShowDialog(owner);
            }
            else
            {
                var mainWindow = GetMainWindow();
                if (mainWindow != null)
                {
                    await messageBox.ShowDialog(mainWindow);
                }
                else
                {
                    messageBox.Show();
                }
            }
        }

        /// <summary>
        /// 显示错误消息
        /// </summary>
        public async Task ShowErrorAsync(string message, Window? owner = null)
        {
            await ShowAsync(message, "错误", MessageBoxType.Error, owner);
        }

        /// <summary>
        /// 显示警告消息
        /// </summary>
        public async Task ShowWarningAsync(string message, Window? owner = null)
        {
            await ShowAsync(message, "警告", MessageBoxType.Warning, owner);
        }

        /// <summary>
        /// 显示信息消息
        /// </summary>
        public async Task ShowInfoAsync(string message, Window? owner = null)
        {
            await ShowAsync(message, "信息", MessageBoxType.Info, owner);
        }

        /// <summary>
        /// 显示成功消息
        /// </summary>
        public async Task ShowSuccessAsync(string message, Window? owner = null)
        {
            await ShowAsync(message, "成功", MessageBoxType.Success, owner);
        }

        /// <summary>
        /// 显示确认对话框
        /// </summary>
        public async Task<bool> ShowConfirmAsync(string message, string title, Window? owner = null)
        {
            var confirmBox = CreateConfirmBox(message, title);

            if (owner != null)
            {
                var result = await confirmBox.ShowDialog<bool?>(owner);
                return result == true;
            }
            else
            {
                var mainWindow = GetMainWindow();
                if (mainWindow != null)
                {
                    var result = await confirmBox.ShowDialog<bool?>(mainWindow);
                    return result == true;
                }
                else
                {
                    confirmBox.Show();
                    return false; // 无法获取结果
                }
            }
        }

        private Window? GetMainWindow()
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.MainWindow;
            }
            return null;
        }

        private Window CreateMessageBox(string message, string title, MessageBoxType type)
        {
            var messageBox = new Window
            {
                Title = title,
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                ShowInTaskbar = false,
                Background = Brushes.White
            };

            var panel = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 20
            };

            // 创建图标和消息的水平面板
            var headerPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 15,
                VerticalAlignment = VerticalAlignment.Center
            };

            // 添加图标
            var icon = new TextBlock
            {
                Text = GetIconForType(type),
                FontSize = 24,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = GetColorForType(type)
            };

            var messageText = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 320
            };

            headerPanel.Children.Add(icon);
            headerPanel.Children.Add(messageText);
            panel.Children.Add(headerPanel);

            // 添加确定按钮
            var okButton = new Button
            {
                Content = "确定",
                HorizontalAlignment = HorizontalAlignment.Center,
                Padding = new Avalonia.Thickness(20, 8),
                Background = GetColorForType(type),
                Foreground = Brushes.White,
                BorderThickness = new Avalonia.Thickness(0),
                CornerRadius = new Avalonia.CornerRadius(4),
                MinWidth = 80
            };

            okButton.Click += (s, e) => messageBox.Close();
            panel.Children.Add(okButton);

            messageBox.Content = panel;
            return messageBox;
        }

        private Window CreateConfirmBox(string message, string title)
        {
            var confirmBox = new Window
            {
                Title = title,
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                ShowInTaskbar = false,
                Background = Brushes.White
            };

            var panel = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 20
            };

            // 创建图标和消息的水平面板
            var headerPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 15,
                VerticalAlignment = VerticalAlignment.Center
            };

            // 添加问号图标
            var icon = new TextBlock
            {
                Text = "❓",
                FontSize = 24,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.Orange
            };

            var messageText = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 320
            };

            headerPanel.Children.Add(icon);
            headerPanel.Children.Add(messageText);
            panel.Children.Add(headerPanel);

            // 添加按钮面板
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 10
            };

            var yesButton = new Button
            {
                Content = "确定",
                Padding = new Avalonia.Thickness(20, 8),
                Background = Brushes.DodgerBlue,
                Foreground = Brushes.White,
                BorderThickness = new Avalonia.Thickness(0),
                CornerRadius = new Avalonia.CornerRadius(4),
                MinWidth = 80
            };

            var noButton = new Button
            {
                Content = "取消",
                Padding = new Avalonia.Thickness(20, 8),
                Background = Brushes.Gray,
                Foreground = Brushes.White,
                BorderThickness = new Avalonia.Thickness(0),
                CornerRadius = new Avalonia.CornerRadius(4),
                MinWidth = 80
            };

            yesButton.Click += (s, e) => confirmBox.Close(true);
            noButton.Click += (s, e) => confirmBox.Close(false);

            buttonPanel.Children.Add(yesButton);
            buttonPanel.Children.Add(noButton);
            panel.Children.Add(buttonPanel);

            confirmBox.Content = panel;
            return confirmBox;
        }

        private string GetIconForType(MessageBoxType type)
        {
            return type switch
            {
                MessageBoxType.Info => "ℹ️",
                MessageBoxType.Warning => "⚠️",
                MessageBoxType.Error => "❌",
                MessageBoxType.Success => "✅",
                _ => "ℹ️"
            };
        }

        private IBrush GetColorForType(MessageBoxType type)
        {
            return type switch
            {
                MessageBoxType.Info => Brushes.DodgerBlue,
                MessageBoxType.Warning => Brushes.Orange,
                MessageBoxType.Error => Brushes.Red,
                MessageBoxType.Success => Brushes.Green,
                _ => Brushes.DodgerBlue
            };
        }
    }
}
