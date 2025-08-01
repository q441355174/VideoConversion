using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VideoConversion_ClientTo.Application.Interfaces;
using VideoConversion_ClientTo.Domain.Entities;
using VideoConversion_ClientTo.ViewModels;

namespace VideoConversion_ClientTo.Presentation.ViewModels
{
    /// <summary>
    /// 转换完成视图模型
    /// </summary>
    public partial class ConversionCompletedViewModel : ViewModelBase
    {
        private readonly IConversionTaskService _conversionTaskService;

        [ObservableProperty]
        private ObservableCollection<ConversionTask> _completedTasks = new();

        [ObservableProperty]
        private bool _isEmptyStateVisible = true;

        [ObservableProperty]
        private bool _isFileListVisible = false;

        [ObservableProperty]
        private bool _isSearchVisible = false;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string _statsText = "0 项，0 GB";

        [ObservableProperty]
        private bool _isLoading = false;

        public ConversionCompletedViewModel()
        {
            try
            {
                _conversionTaskService = Infrastructure.ServiceLocator.GetConversionTaskService();
                Utils.Logger.Info("ConversionCompletedViewModel", "✅ 转换完成视图模型已初始化");
                
                // 初始化时加载已完成的任务
                _ = LoadCompletedTasksAsync();
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionCompletedViewModel", $"❌ 初始化失败: {ex.Message}");
                throw;
            }
        }

        #region 命令

        [RelayCommand]
        private async Task RefreshAsync()
        {
            try
            {
                Utils.Logger.Info("ConversionCompletedViewModel", "🔄 刷新已完成任务列表");
                await LoadCompletedTasksAsync();
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionCompletedViewModel", $"❌ 刷新失败: {ex.Message}");
            }
        }

        [RelayCommand]
        private void ToggleSearch()
        {
            try
            {
                IsSearchVisible = !IsSearchVisible;
                if (!IsSearchVisible)
                {
                    SearchText = string.Empty;
                    // TODO: 重置搜索结果
                }
                Utils.Logger.Debug("ConversionCompletedViewModel", $"🔍 切换搜索状态: {IsSearchVisible}");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionCompletedViewModel", $"❌ 切换搜索失败: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task StartConversionAsync()
        {
            try
            {
                Utils.Logger.Info("ConversionCompletedViewModel", "🚀 导航到文件上传页面");
                // TODO: 实现导航到文件上传页面的逻辑
                // 这里可能需要通过事件或消息传递来通知主窗口切换视图
                await Task.Delay(100); // 模拟异步操作
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionCompletedViewModel", $"❌ 导航失败: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task ClearAllAsync()
        {
            try
            {
                Utils.Logger.Info("ConversionCompletedViewModel", "🗑️ 清空所有已完成任务");
                
                // 删除所有已完成的任务
                var tasksToDelete = CompletedTasks.ToList();
                foreach (var task in tasksToDelete)
                {
                    await _conversionTaskService.DeleteTaskAsync(task.Id);
                }
                
                CompletedTasks.Clear();
                UpdateUI();
                
                Utils.Logger.Info("ConversionCompletedViewModel", $"✅ 已清空 {tasksToDelete.Count} 个任务");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionCompletedViewModel", $"❌ 清空任务失败: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task DownloadFileAsync(ConversionTask task)
        {
            try
            {
                Utils.Logger.Info("ConversionCompletedViewModel", $"📥 下载文件: {task.Name}");
                
                var filePath = await _conversionTaskService.DownloadTaskFileAsync(task.Id);
                if (!string.IsNullOrEmpty(filePath))
                {
                    Utils.Logger.Info("ConversionCompletedViewModel", $"✅ 文件下载成功: {filePath}");
                }
                else
                {
                    Utils.Logger.Warning("ConversionCompletedViewModel", "⚠️ 文件下载失败");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionCompletedViewModel", $"❌ 下载文件失败: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task DeleteTaskAsync(ConversionTask task)
        {
            try
            {
                Utils.Logger.Info("ConversionCompletedViewModel", $"🗑️ 删除任务: {task.Name}");
                
                var success = await _conversionTaskService.DeleteTaskAsync(task.Id);
                if (success)
                {
                    CompletedTasks.Remove(task);
                    UpdateUI();
                    Utils.Logger.Info("ConversionCompletedViewModel", $"✅ 任务删除成功: {task.Name}");
                }
                else
                {
                    Utils.Logger.Warning("ConversionCompletedViewModel", $"⚠️ 任务删除失败: {task.Name}");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionCompletedViewModel", $"❌ 删除任务失败: {ex.Message}");
            }
        }

        #endregion

        #region 私有方法

        private async Task LoadCompletedTasksAsync()
        {
            try
            {
                IsLoading = true;
                Utils.Logger.Info("ConversionCompletedViewModel", "📋 加载已完成任务列表");
                
                var tasks = await _conversionTaskService.GetCompletedTasksAsync();
                
                CompletedTasks.Clear();
                foreach (var task in tasks)
                {
                    CompletedTasks.Add(task);
                }
                
                UpdateUI();
                Utils.Logger.Info("ConversionCompletedViewModel", $"✅ 已加载 {CompletedTasks.Count} 个已完成任务");
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionCompletedViewModel", $"❌ 加载已完成任务失败: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void UpdateUI()
        {
            var hasFiles = CompletedTasks.Count > 0;
            IsEmptyStateVisible = !hasFiles;
            IsFileListVisible = hasFiles;

            // 计算统计信息
            var totalCount = CompletedTasks.Count;
            var totalSize = CompletedTasks.Sum(t => t.SourceFile.FileSize);
            
            StatsText = $"{totalCount} 项，{FormatFileSize(totalSize)}";
        }

        private string FormatFileSize(long bytes)
        {
            if (bytes == 0) return "0 B";

            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double len = bytes;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 添加已完成的任务
        /// </summary>
        public void AddCompletedTask(ConversionTask task)
        {
            try
            {
                if (task.IsCompleted && !CompletedTasks.Contains(task))
                {
                    CompletedTasks.Insert(0, task); // 插入到列表开头
                    UpdateUI();
                    Utils.Logger.Info("ConversionCompletedViewModel", $"📁 添加已完成任务: {task.Name}");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionCompletedViewModel", $"❌ 添加已完成任务失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 搜索任务
        /// </summary>
        public void SearchTasks(string searchText)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchText))
                {
                    // 显示所有任务
                    _ = LoadCompletedTasksAsync();
                }
                else
                {
                    // TODO: 实现搜索逻辑
                    Utils.Logger.Debug("ConversionCompletedViewModel", $"🔍 搜索任务: {searchText}");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("ConversionCompletedViewModel", $"❌ 搜索任务失败: {ex.Message}");
            }
        }

        #endregion
    }
}
