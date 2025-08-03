using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VideoConversion_ClientTo.Application.DTOs;
using VideoConversion_ClientTo.Infrastructure;
using VideoConversion_ClientTo.Infrastructure.Services;
using VideoConversion_ClientTo.ViewModels;

namespace VideoConversion_ClientTo.Presentation.ViewModels.Dialogs
{
    /// <summary>
    /// 系统诊断信息对话框ViewModel
    /// </summary>
    public partial class SystemDiagnosticsViewModel : ViewModelBase
    {
        #region 私有字段

        private readonly ApiService _apiService;
        private readonly List<SystemDiagnosticDto> _allDiagnostics;

        #endregion

        #region 可观察属性

        [ObservableProperty]
        private ObservableCollection<SystemDiagnosticDto> _filteredDiagnostics = new();

        [ObservableProperty]
        private SystemDiagnosticDto? _selectedDiagnostic;

        [ObservableProperty]
        private string _selectedLevel = "全部";

        [ObservableProperty]
        private string _selectedCategory = "全部";

        [ObservableProperty]
        private string _statusText = "";

        #endregion

        #region 静态属性

        /// <summary>
        /// 级别选项
        /// </summary>
        public List<string> LevelOptions { get; } = new() { "全部", "Error", "Warning", "Info", "Debug" };

        /// <summary>
        /// 类别选项
        /// </summary>
        public List<string> CategoryOptions { get; private set; } = new() { "全部" };

        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数
        /// </summary>
        public SystemDiagnosticsViewModel(List<SystemDiagnosticDto> diagnostics)
        {
            _apiService = ServiceLocator.GetRequiredService<ApiService>();
            _allDiagnostics = diagnostics ?? new List<SystemDiagnosticDto>();

            // 初始化类别选项
            InitializeCategoryOptions();

            // 应用初始过滤
            ApplyFilter();

            // 更新状态文本
            UpdateStatusText();

            Utils.Logger.Info("SystemDiagnosticsViewModel", $"✅ 系统诊断ViewModel已初始化，共 {_allDiagnostics.Count} 条记录");
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 初始化类别选项
        /// </summary>
        private void InitializeCategoryOptions()
        {
            var categories = _allDiagnostics
                .Select(d => d.Category)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            CategoryOptions = new List<string> { "全部" };
            CategoryOptions.AddRange(categories);
        }

        /// <summary>
        /// 应用过滤器
        /// </summary>
        private void ApplyFilter()
        {
            var filtered = _allDiagnostics.AsEnumerable();

            // 级别过滤
            if (SelectedLevel != "全部")
            {
                filtered = filtered.Where(d => d.Level == SelectedLevel);
            }

            // 类别过滤
            if (SelectedCategory != "全部")
            {
                filtered = filtered.Where(d => d.Category == SelectedCategory);
            }

            // 按时间倒序排列
            var result = filtered
                .OrderByDescending(d => d.Timestamp)
                .ToList();

            FilteredDiagnostics.Clear();
            foreach (var item in result)
            {
                FilteredDiagnostics.Add(new SystemDiagnosticDisplayDto(item));
            }

            UpdateStatusText();
        }

        /// <summary>
        /// 更新状态文本
        /// </summary>
        private void UpdateStatusText()
        {
            var totalCount = _allDiagnostics.Count;
            var filteredCount = FilteredDiagnostics.Count;
            
            if (totalCount == filteredCount)
            {
                StatusText = $"共 {totalCount} 条诊断信息";
            }
            else
            {
                StatusText = $"显示 {filteredCount} / {totalCount} 条诊断信息";
            }
        }

        #endregion

        #region 属性变化处理

        /// <summary>
        /// 级别选择变化
        /// </summary>
        partial void OnSelectedLevelChanged(string value)
        {
            ApplyFilter();
        }

        /// <summary>
        /// 类别选择变化
        /// </summary>
        partial void OnSelectedCategoryChanged(string value)
        {
            ApplyFilter();
        }

        #endregion

        #region 命令

        /// <summary>
        /// 刷新命令
        /// </summary>
        [RelayCommand]
        private async Task RefreshAsync()
        {
            try
            {
                Utils.Logger.Info("SystemDiagnosticsViewModel", "🔄 刷新系统诊断信息");

                var response = await _apiService.GetSystemDiagnosticsAsync();
                if (response.Success && response.Data != null)
                {
                    _allDiagnostics.Clear();
                    _allDiagnostics.AddRange(response.Data);

                    // 重新初始化类别选项
                    InitializeCategoryOptions();
                    OnPropertyChanged(nameof(CategoryOptions));

                    // 重新应用过滤
                    ApplyFilter();

                    Utils.Logger.Info("SystemDiagnosticsViewModel", $"✅ 刷新成功，共 {response.Data.Count} 条记录");
                }
                else
                {
                    Utils.Logger.Warning("SystemDiagnosticsViewModel", $"⚠️ 刷新失败: {response.Message}");
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("SystemDiagnosticsViewModel", $"❌ 刷新异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 关闭命令
        /// </summary>
        [RelayCommand]
        private void Close()
        {
            // 对话框关闭逻辑由View处理
            Utils.Logger.Info("SystemDiagnosticsViewModel", "🚪 关闭系统诊断对话框");
        }

        #endregion
    }

    /// <summary>
    /// 系统诊断显示DTO - 扩展显示属性
    /// </summary>
    public class SystemDiagnosticDisplayDto : SystemDiagnosticDto
    {
        public SystemDiagnosticDisplayDto(SystemDiagnosticDto source)
        {
            Category = source.Category;
            Level = source.Level;
            Message = source.Message;
            Timestamp = source.Timestamp;
            Details = source.Details;
        }

        /// <summary>
        /// 格式化时间戳
        /// </summary>
        public string FormattedTimestamp => Timestamp.ToString("MM-dd HH:mm:ss");

        /// <summary>
        /// 级别颜色
        /// </summary>
        public string LevelColor => Level switch
        {
            "Error" => "#DC3545",
            "Warning" => "#FFC107",
            "Info" => "#17A2B8",
            "Debug" => "#6C757D",
            _ => "#6C757D"
        };
    }
}
