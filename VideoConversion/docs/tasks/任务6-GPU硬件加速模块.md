# 任务6：GPU硬件加速模块

## 📋 任务概述

实现GPU硬件加速检测和显示功能，为用户提供清晰的GPU状态信息和硬件加速能力展示。

## 🎯 任务目标

- [ ] 创建GPU信息显示界面
- [ ] 实现GPU检测逻辑
- [ ] 显示GPU状态和能力
- [ ] 处理GPU检测错误

## 📝 详细任务清单

### 步骤6.1：创建GPU信息显示界面

#### 任务清单
- [ ] 设计GPU信息卡片
- [ ] 添加GPU状态指示器
- [ ] 创建刷新按钮
- [ ] 实现加载状态显示

#### 实现代码
```html
<!-- GPU信息显示区域 -->
<div id="gpuInfoSection" class="card mb-4">
    <div class="card-header">
        <div class="d-flex justify-content-between align-items-center">
            <h6 class="mb-0">
                <i class="fas fa-microchip text-primary"></i>
                GPU硬件加速
            </h6>
            <button class="btn btn-outline-primary btn-sm" onclick="loadGpuInfo()">
                <i class="fas fa-redo"></i>
            </button>
        </div>
    </div>
    <div class="card-body">
        <div id="gpuInfo">
            <!-- GPU信息将通过JavaScript动态加载 -->
        </div>
    </div>
</div>
```

### 步骤6.2：实现GPU检测逻辑

#### 任务清单
- [ ] 实现GPU信息加载函数
- [ ] 添加GPU信息显示逻辑
- [ ] 处理GPU检测错误
- [ ] 创建GPU卡片生成器

#### 实现代码
```javascript
// GPU模块初始化
function initializeGpuModule() {
    console.log('🖥️ 初始化GPU模块...');
    
    // 加载GPU信息
    loadGpuInfo();
}

// GPU信息加载
async function loadGpuInfo() {
    const gpuInfoDiv = document.getElementById('gpuInfo');
    if (!gpuInfoDiv) return;
    
    try {
        console.log('🔍 检测GPU硬件加速能力...');
        
        // 显示加载状态
        gpuInfoDiv.innerHTML = `
            <div class="text-center">
                <div class="spinner-border spinner-border-sm text-primary" role="status">
                    <span class="visually-hidden">检测中...</span>
                </div>
                <p class="mt-2 mb-0 text-muted">正在检测GPU硬件加速能力...</p>
            </div>
        `;
        
        const response = await fetch('/api/gpu/capabilities');
        const result = await response.json();
        
        if (result.success) {
            displayGpuInfo(result.data);
        } else {
            displayGpuError('获取GPU信息失败: ' + (result.message || '未知错误'));
        }
    } catch (error) {
        console.error('GPU信息加载失败:', error);
        displayGpuError('无法连接到GPU检测服务');
    }
}

// 显示GPU信息
function displayGpuInfo(gpuData) {
    const gpuInfoDiv = document.getElementById('gpuInfo');
    if (!gpuInfoDiv) return;
    
    console.log('📊 显示GPU信息:', gpuData);
    
    if (!gpuData.hasAnyGpuSupport) {
        // 无GPU支持的情况
        gpuInfoDiv.innerHTML = `
            <div class="text-center text-muted py-3">
                <i class="fas fa-exclamation-triangle fa-2x text-warning mb-3"></i>
                <h6>未检测到GPU硬件加速支持</h6>
                <p class="mb-2">系统将使用CPU进行视频转码</p>
                <small class="text-muted">
                    检测时间: ${new Date().toLocaleString()}
                </small>
            </div>
        `;
        return;
    }
    
    // 有GPU支持的情况
    let html = `
        <div class="alert alert-success mb-3">
            <strong><i class="fas fa-check-circle"></i> GPU硬件加速可用!</strong><br>
            <small>支持的GPU类型: ${getSupportedGpuTypes(gpuData)}</small>
        </div>
    `;
    
    // NVIDIA NVENC
    if (gpuData.nvidia && gpuData.nvidia.supported) {
        html += generateGpuCard('NVIDIA NVENC', gpuData.nvidia, 'success');
    }
    
    // Intel QSV
    if (gpuData.intel && gpuData.intel.supported) {
        html += generateGpuCard('Intel QSV', gpuData.intel, 'info');
    }
    
    // AMD AMF
    if (gpuData.amd && gpuData.amd.supported) {
        html += generateGpuCard('AMD AMF', gpuData.amd, 'warning');
    }
    
    html += `
        <div class="mt-3">
            <small class="text-muted">
                <i class="fas fa-clock"></i> 检测时间: ${new Date().toLocaleString()}
            </small>
        </div>
    `;
    
    gpuInfoDiv.innerHTML = html;
}

// 生成GPU卡片
function generateGpuCard(title, gpuInfo, variant) {
    const iconMap = {
        'NVIDIA NVENC': 'fas fa-microchip text-success',
        'Intel QSV': 'fas fa-microchip text-info',
        'AMD AMF': 'fas fa-microchip text-warning'
    };
    
    return `
        <div class="card mb-2 border-${variant}">
            <div class="card-body p-3">
                <h6 class="card-title">
                    <i class="${iconMap[title] || 'fas fa-microchip'}"></i> ${title}
                </h6>
                <div class="row">
                    <div class="col-6">
                        <small class="text-muted">设备数量</small><br>
                        <span class="fw-bold">${gpuInfo.deviceCount || 0}</span>
                    </div>
                    <div class="col-6">
                        <small class="text-muted">编码器数量</small><br>
                        <span class="fw-bold">${gpuInfo.encoders?.length || 0}</span>
                    </div>
                </div>
                ${gpuInfo.encoders && gpuInfo.encoders.length > 0 ? `
                    <div class="mt-2">
                        <small class="text-muted">支持的编码器:</small><br>
                        <div class="d-flex flex-wrap gap-1 mt-1">
                            ${gpuInfo.encoders.map(encoder => 
                                `<span class="badge bg-${variant}">${encoder}</span>`
                            ).join('')}
                        </div>
                    </div>
                ` : ''}
                ${gpuInfo.deviceName ? `
                    <div class="mt-2">
                        <small class="text-muted">设备名称:</small><br>
                        <span class="fw-bold">${gpuInfo.deviceName}</span>
                    </div>
                ` : ''}
                ${gpuInfo.driverVersion ? `
                    <div class="mt-1">
                        <small class="text-muted">驱动版本:</small>
                        <span class="fw-bold">${gpuInfo.driverVersion}</span>
                    </div>
                ` : ''}
            </div>
        </div>
    `;
}

// 获取支持的GPU类型列表
function getSupportedGpuTypes(gpuData) {
    const types = [];
    if (gpuData.nvidia?.supported) types.push('NVIDIA');
    if (gpuData.intel?.supported) types.push('Intel');
    if (gpuData.amd?.supported) types.push('AMD');
    return types.join(', ') || '无';
}

// 显示GPU错误信息
function displayGpuError(errorMessage) {
    const gpuInfoDiv = document.getElementById('gpuInfo');
    if (!gpuInfoDiv) return;
    
    gpuInfoDiv.innerHTML = `
        <div class="alert alert-warning">
            <h6><i class="fas fa-exclamation-triangle"></i> GPU信息加载失败</h6>
            <p class="mb-2">${errorMessage}</p>
            <button class="btn btn-outline-primary btn-sm" onclick="loadGpuInfo()">
                <i class="fas fa-redo"></i> 重新检测
            </button>
        </div>
    `;
}
```

### 步骤6.3：实现GPU状态监控

#### 任务清单
- [ ] 添加GPU状态定期检查
- [ ] 实现GPU温度监控
- [ ] 显示GPU使用率
- [ ] 处理GPU状态变化

#### 实现代码
```javascript
// GPU状态监控
class GpuMonitor {
    constructor() {
        this.isMonitoring = false;
        this.monitorInterval = null;
        this.updateInterval = 5000; // 5秒更新一次
    }
    
    // 开始监控
    startMonitoring() {
        if (this.isMonitoring) return;
        
        console.log('🔍 开始GPU状态监控...');
        this.isMonitoring = true;
        
        this.monitorInterval = setInterval(() => {
            this.updateGpuStatus();
        }, this.updateInterval);
        
        // 立即执行一次
        this.updateGpuStatus();
    }
    
    // 停止监控
    stopMonitoring() {
        if (!this.isMonitoring) return;
        
        console.log('⏹️ 停止GPU状态监控');
        this.isMonitoring = false;
        
        if (this.monitorInterval) {
            clearInterval(this.monitorInterval);
            this.monitorInterval = null;
        }
    }
    
    // 更新GPU状态
    async updateGpuStatus() {
        try {
            const response = await fetch('/api/gpu/status');
            const result = await response.json();
            
            if (result.success) {
                this.displayGpuStatus(result.data);
            }
        } catch (error) {
            console.error('获取GPU状态失败:', error);
        }
    }
    
    // 显示GPU状态
    displayGpuStatus(statusData) {
        // 更新GPU使用率
        this.updateGpuUsage(statusData);
        
        // 更新GPU温度
        this.updateGpuTemperature(statusData);
        
        // 更新GPU内存使用
        this.updateGpuMemory(statusData);
    }
    
    // 更新GPU使用率
    updateGpuUsage(statusData) {
        if (!statusData.usage) return;
        
        const usageElements = document.querySelectorAll('.gpu-usage');
        usageElements.forEach(element => {
            const gpuType = element.dataset.gpuType;
            const usage = statusData.usage[gpuType];
            
            if (usage !== undefined) {
                element.textContent = `${usage}%`;
                
                // 更新使用率颜色
                element.className = `gpu-usage badge ${this.getUsageClass(usage)}`;
            }
        });
    }
    
    // 更新GPU温度
    updateGpuTemperature(statusData) {
        if (!statusData.temperature) return;
        
        const tempElements = document.querySelectorAll('.gpu-temperature');
        tempElements.forEach(element => {
            const gpuType = element.dataset.gpuType;
            const temp = statusData.temperature[gpuType];
            
            if (temp !== undefined) {
                element.textContent = `${temp}°C`;
                
                // 更新温度颜色
                element.className = `gpu-temperature badge ${this.getTemperatureClass(temp)}`;
            }
        });
    }
    
    // 更新GPU内存使用
    updateGpuMemory(statusData) {
        if (!statusData.memory) return;
        
        const memoryElements = document.querySelectorAll('.gpu-memory');
        memoryElements.forEach(element => {
            const gpuType = element.dataset.gpuType;
            const memory = statusData.memory[gpuType];
            
            if (memory) {
                const usedGB = (memory.used / 1024 / 1024 / 1024).toFixed(1);
                const totalGB = (memory.total / 1024 / 1024 / 1024).toFixed(1);
                element.textContent = `${usedGB}GB / ${totalGB}GB`;
            }
        });
    }
    
    // 获取使用率样式类
    getUsageClass(usage) {
        if (usage < 30) return 'bg-success';
        if (usage < 70) return 'bg-warning';
        return 'bg-danger';
    }
    
    // 获取温度样式类
    getTemperatureClass(temp) {
        if (temp < 60) return 'bg-success';
        if (temp < 80) return 'bg-warning';
        return 'bg-danger';
    }
}

// 创建GPU监控实例
const gpuMonitor = new GpuMonitor();

// 在页面可见时开始监控
document.addEventListener('visibilitychange', function() {
    if (document.hidden) {
        gpuMonitor.stopMonitoring();
    } else {
        gpuMonitor.startMonitoring();
    }
});

// 页面卸载时停止监控
window.addEventListener('beforeunload', function() {
    gpuMonitor.stopMonitoring();
});
```

### 步骤6.4：实现GPU性能建议

#### 任务清单
- [ ] 分析GPU性能数据
- [ ] 生成性能建议
- [ ] 显示优化提示
- [ ] 处理性能警告

#### 实现代码
```javascript
// GPU性能分析器
class GpuPerformanceAnalyzer {
    constructor() {
        this.performanceData = [];
        this.maxDataPoints = 60; // 保留最近60个数据点
    }
    
    // 添加性能数据
    addPerformanceData(data) {
        this.performanceData.push({
            timestamp: Date.now(),
            ...data
        });
        
        // 保持数据点数量限制
        if (this.performanceData.length > this.maxDataPoints) {
            this.performanceData.shift();
        }
        
        // 分析性能并生成建议
        this.analyzePerformance();
    }
    
    // 分析性能
    analyzePerformance() {
        if (this.performanceData.length < 5) return;
        
        const recent = this.performanceData.slice(-5);
        const avgUsage = recent.reduce((sum, data) => sum + (data.usage || 0), 0) / recent.length;
        const avgTemp = recent.reduce((sum, data) => sum + (data.temperature || 0), 0) / recent.length;
        
        // 生成建议
        const suggestions = this.generateSuggestions(avgUsage, avgTemp);
        
        if (suggestions.length > 0) {
            this.displaySuggestions(suggestions);
        }
    }
    
    // 生成建议
    generateSuggestions(avgUsage, avgTemp) {
        const suggestions = [];
        
        // 高使用率建议
        if (avgUsage > 90) {
            suggestions.push({
                type: 'warning',
                title: 'GPU使用率过高',
                message: 'GPU使用率持续超过90%，可能影响转换性能。建议降低并发任务数量。',
                icon: 'exclamation-triangle'
            });
        }
        
        // 高温度建议
        if (avgTemp > 80) {
            suggestions.push({
                type: 'danger',
                title: 'GPU温度过高',
                message: 'GPU温度超过80°C，建议检查散热系统或降低工作负载。',
                icon: 'thermometer-full'
            });
        }
        
        // 低使用率建议
        if (avgUsage < 20) {
            suggestions.push({
                type: 'info',
                title: 'GPU利用率较低',
                message: 'GPU使用率较低，可以考虑提高转换质量设置或增加并发任务。',
                icon: 'info-circle'
            });
        }
        
        return suggestions;
    }
    
    // 显示建议
    displaySuggestions(suggestions) {
        const suggestionsContainer = document.getElementById('gpuSuggestions');
        if (!suggestionsContainer) return;
        
        let html = '';
        suggestions.forEach(suggestion => {
            html += `
                <div class="alert alert-${suggestion.type} alert-dismissible fade show mb-2">
                    <i class="fas fa-${suggestion.icon}"></i>
                    <strong>${suggestion.title}</strong><br>
                    ${suggestion.message}
                    <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
                </div>
            `;
        });
        
        suggestionsContainer.innerHTML = html;
    }
}

// 创建性能分析器实例
const gpuAnalyzer = new GpuPerformanceAnalyzer();

// 扩展GPU监控以包含性能分析
const originalDisplayGpuStatus = GpuMonitor.prototype.displayGpuStatus;
GpuMonitor.prototype.displayGpuStatus = function(statusData) {
    // 调用原始方法
    originalDisplayGpuStatus.call(this, statusData);
    
    // 添加性能分析
    if (statusData.nvidia?.usage !== undefined) {
        gpuAnalyzer.addPerformanceData({
            usage: statusData.nvidia.usage,
            temperature: statusData.nvidia.temperature
        });
    }
};

// 添加GPU建议显示区域到HTML
function addGpuSuggestionsContainer() {
    const gpuInfoSection = document.getElementById('gpuInfoSection');
    if (gpuInfoSection && !document.getElementById('gpuSuggestions')) {
        const suggestionsHtml = `
            <div class="mt-3">
                <h6 class="text-muted">
                    <i class="fas fa-lightbulb"></i> 性能建议
                </h6>
                <div id="gpuSuggestions"></div>
            </div>
        `;
        
        const cardBody = gpuInfoSection.querySelector('.card-body');
        if (cardBody) {
            cardBody.insertAdjacentHTML('beforeend', suggestionsHtml);
        }
    }
}

// 在GPU模块初始化时添加建议容器
const originalInitializeGpuModule = initializeGpuModule;
initializeGpuModule = function() {
    originalInitializeGpuModule();
    addGpuSuggestionsContainer();
    
    // 开始GPU监控
    setTimeout(() => {
        gpuMonitor.startMonitoring();
    }, 2000);
};
```

## ✅ 验收标准

### 功能验收
- [ ] GPU信息正确检测和显示
- [ ] GPU状态实时更新
- [ ] 性能建议准确有效
- [ ] 错误处理完善

### 用户体验验收
- [ ] 界面清晰易懂
- [ ] 加载状态明确
- [ ] 刷新功能正常
- [ ] 建议提示有用

### 性能验收
- [ ] 检测速度合理
- [ ] 监控不影响性能
- [ ] 内存使用适中
- [ ] 定时器正确清理

## 🔗 依赖关系

### 前置依赖
- 任务1：基础架构搭建
- GPU检测API服务端实现

### 后续任务
- 任务7：错误处理和优化
- 任务8：测试和部署

## 📊 预估工时

- **开发时间**: 3-4小时
- **测试时间**: 1-2小时
- **总计**: 4-6小时

## 🚨 注意事项

1. **兼容性**: 确保支持各种GPU类型和驱动版本
2. **性能影响**: GPU监控不应影响转换性能
3. **错误处理**: 完善的GPU检测失败处理
4. **用户体验**: 清晰的GPU状态和建议显示

## 📝 完成标记

- [ ] 步骤6.1完成
- [ ] 步骤6.2完成
- [ ] 步骤6.3完成
- [ ] 步骤6.4完成
- [ ] 验收测试通过
- [ ] 代码提交完成

**完成时间**: ___________  
**开发者**: ___________  
**审核者**: ___________
