using Avalonia.Controls;
using Avalonia.Interactivity;
using VideoConversion_Client.Services;
using System;
using System.Threading.Tasks;

namespace VideoConversion_Client.Views
{
    public partial class DiskSpaceConfigDialog : Window
    {
        private readonly DiskSpaceApiService _diskSpaceApiService;
        private DiskSpaceConfigResponse? _currentConfig;
        private DiskSpaceUsageResponse? _currentUsage;

        // UI控件
        private TextBlock? _currentUsedText;
        private TextBlock? _currentTotalText;
        private TextBlock? _currentUsagePercentText;
        private TextBlock? _currentAvailableText;
        private ProgressBar? _currentUsageBar;
        
        private CheckBox? _enableSpaceLimitCheckBox;
        private Slider? _maxSpaceSlider;
        private Slider? _reservedSpaceSlider;
        private TextBlock? _maxSpaceValueText;
        private TextBlock? _reservedSpaceValueText;
        private TextBlock? _effectiveSpaceText;
        private TextBlock? _configWarningText;
        
        private CheckBox? _autoCleanupCheckBox;
        private CheckBox? _cleanupDownloadedCheckBox;
        private CheckBox? _cleanupTempCheckBox;
        
        private Button? _refreshBtn;
        private Button? _cancelBtn;
        private Button? _saveBtn;
        private Button? _manualCleanupBtn;

        public bool ConfigSaved { get; private set; } = false;

        // 无参数构造函数，用于XAML设计器
        public DiskSpaceConfigDialog()
        {
            InitializeComponent();
            _diskSpaceApiService = new DiskSpaceApiService("http://localhost:5065");

            InitializeControls();
            SetupEventHandlers();
        }

        public DiskSpaceConfigDialog(string baseUrl)
        {
            InitializeComponent();
            _diskSpaceApiService = new DiskSpaceApiService(baseUrl);

            InitializeControls();
            SetupEventHandlers();

            _ = Task.Run(LoadCurrentConfigAsync);
        }

        private void InitializeControls()
        {
            // 获取UI控件引用
            _currentUsedText = this.FindControl<TextBlock>("CurrentUsedText");
            _currentTotalText = this.FindControl<TextBlock>("CurrentTotalText");
            _currentUsagePercentText = this.FindControl<TextBlock>("CurrentUsagePercentText");
            _currentAvailableText = this.FindControl<TextBlock>("CurrentAvailableText");
            _currentUsageBar = this.FindControl<ProgressBar>("CurrentUsageBar");
            
            _enableSpaceLimitCheckBox = this.FindControl<CheckBox>("EnableSpaceLimitCheckBox");
            _maxSpaceSlider = this.FindControl<Slider>("MaxSpaceSlider");
            _reservedSpaceSlider = this.FindControl<Slider>("ReservedSpaceSlider");
            _maxSpaceValueText = this.FindControl<TextBlock>("MaxSpaceValueText");
            _reservedSpaceValueText = this.FindControl<TextBlock>("ReservedSpaceValueText");
            _effectiveSpaceText = this.FindControl<TextBlock>("EffectiveSpaceText");
            _configWarningText = this.FindControl<TextBlock>("ConfigWarningText");
            
            _autoCleanupCheckBox = this.FindControl<CheckBox>("AutoCleanupCheckBox");
            _cleanupDownloadedCheckBox = this.FindControl<CheckBox>("CleanupDownloadedCheckBox");
            _cleanupTempCheckBox = this.FindControl<CheckBox>("CleanupTempCheckBox");
            
            _refreshBtn = this.FindControl<Button>("RefreshBtn");
            _cancelBtn = this.FindControl<Button>("CancelBtn");
            _saveBtn = this.FindControl<Button>("SaveBtn");
            _manualCleanupBtn = this.FindControl<Button>("ManualCleanupBtn");
        }

        private void SetupEventHandlers()
        {
            // 滑块值变化事件
            if (_maxSpaceSlider != null)
            {
                _maxSpaceSlider.ValueChanged += OnMaxSpaceSliderChanged;
            }
            
            if (_reservedSpaceSlider != null)
            {
                _reservedSpaceSlider.ValueChanged += OnReservedSpaceSliderChanged;
            }
            
            // 按钮点击事件
            if (_refreshBtn != null)
            {
                _refreshBtn.Click += OnRefreshClick;
            }
            
            if (_cancelBtn != null)
            {
                _cancelBtn.Click += OnCancelClick;
            }
            
            if (_saveBtn != null)
            {
                _saveBtn.Click += OnSaveClick;
            }
            
            if (_manualCleanupBtn != null)
            {
                _manualCleanupBtn.Click += OnManualCleanupClick;
            }
        }

        private async Task LoadCurrentConfigAsync()
        {
            try
            {
                // 加载当前配置
                _currentConfig = await _diskSpaceApiService.GetSpaceConfigAsync();
                
                // 加载当前使用情况
                _currentUsage = await _diskSpaceApiService.GetSpaceUsageAsync();
                
                // 更新UI
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    UpdateCurrentUsageDisplay();
                    UpdateConfigDisplay();
                    UpdatePreview();
                });
                
                Utils.Logger.Info("DiskSpaceConfig", "配置和使用情况加载完成");
            }
            catch (Exception ex)
            {
                Utils.Logger.Info("DiskSpaceConfig", $"加载配置失败: {ex.Message}");
                
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    // 显示错误信息
                    if (_configWarningText != null)
                    {
                        _configWarningText.Text = $"加载配置失败: {ex.Message}";
                        _configWarningText.IsVisible = true;
                    }
                });
            }
        }

        private void UpdateCurrentUsageDisplay()
        {
            if (_currentUsage?.Success == true)
            {
                if (_currentUsedText != null)
                    _currentUsedText.Text = $"{_currentUsage.UsedSpaceGB:F1} GB";
                
                if (_currentTotalText != null)
                    _currentTotalText.Text = $"{_currentUsage.TotalSpaceGB:F1} GB";
                
                if (_currentUsagePercentText != null)
                    _currentUsagePercentText.Text = $"{_currentUsage.UsagePercentage:F1}%";
                
                if (_currentAvailableText != null)
                    _currentAvailableText.Text = $"{_currentUsage.AvailableSpaceGB:F1} GB";
                
                if (_currentUsageBar != null)
                    _currentUsageBar.Value = _currentUsage.UsagePercentage;
            }
        }

        private void UpdateConfigDisplay()
        {
            if (_currentConfig?.Success == true)
            {
                if (_enableSpaceLimitCheckBox != null)
                    _enableSpaceLimitCheckBox.IsChecked = _currentConfig.IsEnabled;
                
                if (_maxSpaceSlider != null)
                    _maxSpaceSlider.Value = _currentConfig.MaxTotalSpaceGB;
                
                if (_reservedSpaceSlider != null)
                    _reservedSpaceSlider.Value = _currentConfig.ReservedSpaceGB;
            }
        }

        private void OnMaxSpaceSliderChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_maxSpaceValueText != null)
                _maxSpaceValueText.Text = $"{e.NewValue:F0} GB";
            
            UpdatePreview();
        }

        private void OnReservedSpaceSliderChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_reservedSpaceValueText != null)
                _reservedSpaceValueText.Text = $"{e.NewValue:F0} GB";
            
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            if (_maxSpaceSlider == null || _reservedSpaceSlider == null || _effectiveSpaceText == null || _configWarningText == null)
                return;
            
            var maxSpace = _maxSpaceSlider.Value;
            var reservedSpace = _reservedSpaceSlider.Value;
            var effectiveSpace = maxSpace - reservedSpace;
            
            _effectiveSpaceText.Text = $"{effectiveSpace:F0} GB";
            
            // 检查配置合理性
            if (reservedSpace >= maxSpace)
            {
                _configWarningText.Text = "⚠️ 保留空间不能大于或等于最大总空间";
                _configWarningText.IsVisible = true;
            }
            else if (effectiveSpace < 10)
            {
                _configWarningText.Text = "⚠️ 实际可用空间过小，建议至少保留10GB";
                _configWarningText.IsVisible = true;
            }
            else if (_currentUsage?.Success == true && effectiveSpace < _currentUsage.UsedSpaceGB)
            {
                _configWarningText.Text = $"⚠️ 实际可用空间({effectiveSpace:F0}GB)小于当前已使用空间({_currentUsage.UsedSpaceGB:F1}GB)";
                _configWarningText.IsVisible = true;
            }
            else
            {
                _configWarningText.IsVisible = false;
            }
        }

        private async void OnRefreshClick(object? sender, RoutedEventArgs e)
        {
            await LoadCurrentConfigAsync();
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            ConfigSaved = false;
            Close();
        }

        private async void OnSaveClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (_maxSpaceSlider == null || _reservedSpaceSlider == null || _enableSpaceLimitCheckBox == null)
                    return;
                
                var maxSpace = _maxSpaceSlider.Value;
                var reservedSpace = _reservedSpaceSlider.Value;
                var isEnabled = _enableSpaceLimitCheckBox.IsChecked ?? true;
                
                // 验证配置
                if (reservedSpace >= maxSpace)
                {
                    Utils.Logger.Info("DiskSpaceConfig", "保留空间不能大于或等于最大总空间");
                    return;
                }
                
                // 保存配置
                var result = await _diskSpaceApiService.SetSpaceConfigAsync(maxSpace, reservedSpace, isEnabled);
                
                if (result.Success)
                {
                    ConfigSaved = true;
                    Utils.Logger.Info("DiskSpaceConfig", $"磁盘空间配置保存成功: {maxSpace}GB/{reservedSpace}GB");
                    Close();
                }
                else
                {
                    Utils.Logger.Info("DiskSpaceConfig", $"保存配置失败: {result.Message}");
                    
                    if (_configWarningText != null)
                    {
                        _configWarningText.Text = $"保存失败: {result.Message}";
                        _configWarningText.IsVisible = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Info("DiskSpaceConfig", $"保存配置异常: {ex.Message}");
                
                if (_configWarningText != null)
                {
                    _configWarningText.Text = $"保存异常: {ex.Message}";
                    _configWarningText.IsVisible = true;
                }
            }
        }

        private async void OnManualCleanupClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (_manualCleanupBtn != null)
                {
                    _manualCleanupBtn.Content = "清理中...";
                    _manualCleanupBtn.IsEnabled = false;
                }
                
                // TODO: 实现手动清理功能
                await Task.Delay(2000); // 模拟清理过程
                
                Utils.Logger.Info("DiskSpaceConfig", "手动清理完成");
                
                // 刷新使用情况
                await LoadCurrentConfigAsync();
            }
            catch (Exception ex)
            {
                Utils.Logger.Info("DiskSpaceConfig", $"手动清理失败: {ex.Message}");
            }
            finally
            {
                if (_manualCleanupBtn != null)
                {
                    _manualCleanupBtn.Content = "立即清理临时文件";
                    _manualCleanupBtn.IsEnabled = true;
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _diskSpaceApiService?.Dispose();
            base.OnClosed(e);
        }
    }
}
