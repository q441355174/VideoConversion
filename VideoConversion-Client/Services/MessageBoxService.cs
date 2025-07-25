using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System.Threading.Tasks;

namespace VideoConversion_Client.Services
{
    /// <summary>
    /// 消息框类型枚举
    /// </summary>
    public enum MessageBoxType
    {
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// 统一的消息框服务
    /// </summary>
    public static class MessageBoxService
    {
        /// <summary>
        /// 显示消息框
        /// </summary>
        /// <param name="message">消息内容</param>
        /// <param name="title">标题</param>
        /// <param name="type">消息类型</param>
        /// <param name="owner">父窗口</param>
        public static async Task ShowAsync(string message, string title, MessageBoxType type, Window? owner = null)
        {
            var messageBox = CreateMessageBox(message, title, type);
            
            if (owner != null)
            {
                await messageBox.ShowDialog(owner);
            }
            else
            {
                messageBox.Show();
            }
        }

        /// <summary>
        /// 显示错误消息
        /// </summary>
        public static async Task ShowErrorAsync(string message, Window? owner = null)
        {
            await ShowAsync(message, "错误", MessageBoxType.Error, owner);
        }

        /// <summary>
        /// 显示成功消息
        /// </summary>
        public static async Task ShowSuccessAsync(string message, Window? owner = null)
        {
            await ShowAsync(message, "成功", MessageBoxType.Success, owner);
        }

        /// <summary>
        /// 显示警告消息
        /// </summary>
        public static async Task ShowWarningAsync(string message, Window? owner = null)
        {
            await ShowAsync(message, "警告", MessageBoxType.Warning, owner);
        }

        /// <summary>
        /// 显示信息消息
        /// </summary>
        public static async Task ShowInfoAsync(string message, Window? owner = null)
        {
            await ShowAsync(message, "信息", MessageBoxType.Info, owner);
        }

        /// <summary>
        /// 创建消息框窗口
        /// </summary>
        private static Window CreateMessageBox(string message, string title, MessageBoxType type)
        {
            var messageBox = new Window
            {
                Title = title,
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                ShowInTaskbar = false
            };

            var panel = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 15
            };

            // 添加图标和消息
            var headerPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10
            };

            // 根据类型添加图标
            var icon = new TextBlock
            {
                FontSize = 24,
                VerticalAlignment = VerticalAlignment.Center,
                Text = GetIconForType(type),
                Foreground = GetColorForType(type)
            };

            var messageText = new TextBlock
            {
                Text = message,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
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

        /// <summary>
        /// 获取消息类型对应的图标
        /// </summary>
        private static string GetIconForType(MessageBoxType type)
        {
            return type switch
            {
                MessageBoxType.Success => "✅",
                MessageBoxType.Error => "❌",
                MessageBoxType.Warning => "⚠️",
                MessageBoxType.Info => "ℹ️",
                _ => "ℹ️"
            };
        }

        /// <summary>
        /// 获取消息类型对应的颜色
        /// </summary>
        private static IBrush GetColorForType(MessageBoxType type)
        {
            return type switch
            {
                MessageBoxType.Success => new SolidColorBrush(Color.FromRgb(39, 174, 96)),   // #27ae60
                MessageBoxType.Error => new SolidColorBrush(Color.FromRgb(231, 76, 60)),     // #e74c3c
                MessageBoxType.Warning => new SolidColorBrush(Color.FromRgb(243, 156, 18)),  // #f39c12
                MessageBoxType.Info => new SolidColorBrush(Color.FromRgb(52, 152, 219)),     // #3498db
                _ => new SolidColorBrush(Color.FromRgb(52, 152, 219))
            };
        }
    }
}
