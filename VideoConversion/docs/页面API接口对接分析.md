# VideoConversion 页面API接口对接分析

## 📋 页面功能与API接口映射

### 🎮 GPU硬件加速功能

#### 页面需求
- GPU硬件检测和信息显示
- GPU性能实时监控
- 智能预设选择

#### 对应API接口 ✅
| 页面调用 | 控制器端点 | 状态 | 说明 |
|----------|------------|------|------|
| `GET /api/gpu/capabilities` | `GpuController.GetGpuCapabilities()` | ✅ 已实现 | 获取GPU能力信息 |
| `GET /api/gpu/detect` | `GpuController.DetectGpu()` | ✅ 已实现 | 检测GPU硬件 |
| `GET /api/gpu/performance` | `GpuController.GetGpuPerformance()` | ✅ 已实现 | 获取GPU性能数据 |

**额外可用接口**:
- `GET /api/gpu/recommended-encoder` - 获取推荐编码器
- `POST /api/gpu/refresh` - 刷新GPU检测缓存
- `POST /api/gpu/test-encoder` - 测试GPU编码器

### 📁 文件上传功能

#### 页面需求
- 普通文件上传
- 大文件上传支持
- 上传进度跟踪

#### 对应API接口 ✅
| 页面调用 | 控制器端点 | 状态 | 说明 |
|----------|------------|------|------|
| `POST /api/conversion/start` | `ConversionController.StartConversion()` | ✅ 已实现 | 普通文件上传并开始转换 |
| `POST /api/upload/large-file` | `UploadController.UploadLargeFileAndCreateTask()` | ✅ 已实现 | 大文件上传 |
| `GET /api/upload/progress/{uploadId}` | `UploadController.GetUploadProgress()` | ✅ 已实现 | 获取上传进度 |

**额外可用接口**:
- `POST /api/conversion/start-from-upload` - 从已上传文件开始转换

### 📋 任务管理功能

#### 页面需求
- 获取最近任务列表
- 任务状态监控
- 任务操作（取消、下载）

#### 对应API接口 ✅
| 页面调用 | 控制器端点 | 状态 | 说明 |
|----------|------------|------|------|
| `GET /api/conversion/recent` | `ConversionController.GetRecentTasks()` | ✅ 已实现 | 获取最近任务 |
| `POST /api/conversion/cancel/{taskId}` | `ConversionController.CancelTask()` | ✅ 已实现 | 取消任务 |
| `GET /api/conversion/download/{taskId}` | `ConversionController.DownloadFile()` | ✅ 已实现 | 下载文件 |

**额外可用接口**:
- `GET /api/conversion/presets` - 获取转换预设
- `GET /api/conversion/processes` - 获取运行进程
- `GET /api/conversion/is-running/{taskId}` - 检查任务是否运行
- `GET /api/conversion/task-details/{taskId}` - 获取任务详情
- `POST /api/conversion/cleanup` - 清理旧任务

### 🚨 错误处理功能

#### 页面需求
- 错误报告提交
- 用户反馈收集
- 错误统计查看

#### 对应API接口 ✅
| 页面调用 | 控制器端点 | 状态 | 说明 |
|----------|------------|------|------|
| `POST /api/errors/report` | `ErrorsController.ReportError()` | ✅ 已实现 | 报告错误 |
| `POST /api/errors/feedback` | `ErrorsController.SubmitFeedback()` | ✅ 已实现 | 提交用户反馈 |
| `GET /api/errors/statistics` | `ErrorsController.GetErrorStatistics()` | ✅ 已实现 | 获取错误统计 |

## 🔗 SignalR实时通信

### Hub方法 ✅
| 页面调用 | Hub方法 | 状态 | 说明 |
|----------|---------|------|------|
| `GetRecentTasks(count)` | `ConversionHub.GetRecentTasks()` | ✅ 已实现 | 获取最近任务 |
| `JoinTaskGroup(taskId)` | `ConversionHub.JoinTaskGroup()` | ✅ 已实现 | 加入任务组 |
| `LeaveTaskGroup(taskId)` | `ConversionHub.LeaveTaskGroup()` | ✅ 已实现 | 离开任务组 |
| `GetTaskStatus(taskId)` | `ConversionHub.GetTaskStatus()` | ✅ 已实现 | 获取任务状态 |
| `CancelTask(taskId)` | `ConversionHub.CancelTask()` | ✅ 已实现 | 取消任务 |

### 服务器推送事件 ✅
- `ProgressUpdate` - 进度更新
- `TaskStarted` - 任务开始
- `TaskCompleted` - 任务完成
- `TaskFailed` - 任务失败
- `TaskCancelled` - 任务取消
- `StatusUpdate` - 状态更新

## 📊 接口完整性分析

### ✅ 完全对接的功能
1. **GPU硬件加速** - 3/3 接口已实现
2. **文件上传** - 3/3 接口已实现
3. **任务管理** - 3/3 核心接口已实现
4. **错误处理** - 3/3 接口已实现
5. **SignalR通信** - 5/5 Hub方法已实现

### 🎯 接口使用建议

#### 1. GPU功能优化
```javascript
// 推荐的GPU检测流程
async function initializeGPU() {
    // 1. 获取GPU能力
    const capabilities = await fetch('/api/gpu/capabilities');
    
    // 2. 检测具体GPU硬件
    const hardware = await fetch('/api/gpu/detect');
    
    // 3. 开始性能监控
    setInterval(async () => {
        const performance = await fetch('/api/gpu/performance');
        updateGPUMetrics(performance);
    }, 5000);
}
```

#### 2. 文件上传策略
```javascript
// 根据文件大小选择上传方式
async function uploadFile(file) {
    const maxNormalSize = 100 * 1024 * 1024; // 100MB
    
    if (file.size > maxNormalSize) {
        // 大文件上传
        return await fetch('/api/upload/large-file', {
            method: 'POST',
            body: formData
        });
    } else {
        // 普通上传
        return await fetch('/api/conversion/start', {
            method: 'POST',
            body: formData
        });
    }
}
```

#### 3. 任务状态监控
```javascript
// 结合SignalR和REST API的任务监控
class TaskMonitor {
    constructor() {
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl("/conversionHub")
            .build();
    }
    
    async monitorTask(taskId) {
        // 1. 加入任务组接收实时更新
        await this.connection.invoke("JoinTaskGroup", taskId);
        
        // 2. 监听实时事件
        this.connection.on("ProgressUpdate", (data) => {
            updateProgress(data);
        });
        
        // 3. 定期获取详细状态（备用）
        setInterval(async () => {
            const status = await this.connection.invoke("GetTaskStatus", taskId);
            updateTaskStatus(status);
        }, 10000);
    }
}
```

## 🔧 需要注意的技术细节

### 1. 错误处理
所有控制器都继承自`BaseApiController`，提供统一的错误处理：
```csharp
return await SafeExecuteAsync(
    async () => {
        // 业务逻辑
    },
    "操作描述",
    "成功消息"
);
```

### 2. 参数验证
控制器提供了内置的参数验证：
```csharp
if (string.IsNullOrWhiteSpace(taskId))
    return ValidationError("任务ID不能为空");
```

### 3. 文件大小限制
大文件上传有特殊配置：
```csharp
[RequestSizeLimit(32212254720)] // 30GB
[RequestFormLimits(MultipartBodyLengthLimit = 32212254720)]
```

## 🎉 总结

### ✅ 接口对接状态
- **完整性**: 100% - 所有页面功能都有对应的API接口
- **可用性**: 100% - 所有接口都已实现并可用
- **实时性**: 100% - SignalR提供完整的实时通信支持
- **错误处理**: 100% - 统一的错误处理和用户反馈机制

### 🚀 优势特点
1. **RESTful设计** - 清晰的资源路径和HTTP方法
2. **实时通信** - SignalR提供双向实时数据传输
3. **统一错误处理** - BaseApiController提供一致的错误响应
4. **完整验证** - 参数验证和业务逻辑验证
5. **性能优化** - 大文件上传和进度跟踪支持

### 📊 额外可用的API接口

#### TaskController (任务管理增强)
| 端点 | 方法 | 说明 |
|------|------|------|
| `/api/task/status/{taskId}` | GET | 获取任务状态（备用接口） |
| `/api/task/recent` | GET | 获取最近任务（备用接口） |
| `/api/task/list` | GET | 获取任务列表（支持分页） |
| `/api/task/cleanup` | POST | 清理旧任务 |

#### HealthController (系统监控)
| 端点 | 方法 | 说明 |
|------|------|------|
| `/api/health` | GET | 基本健康检查 |
| `/api/health/status` | GET | 详细系统状态 |
| `/api/health/stats/today` | GET | 今日统计 |
| `/api/health/diagnostics` | GET | 系统诊断 |

## 🔧 实际使用示例

### 1. 完整的文件上传和转换流程
```javascript
async function handleFileConversion(file, settings) {
    try {
        // 1. 检查GPU能力
        const gpuCapabilities = await fetch('/api/gpu/capabilities');
        const gpuData = await gpuCapabilities.json();

        // 2. 根据GPU能力调整设置
        if (gpuData.hasAnyGpuSupport) {
            settings.videoCodec = gpuData.supportedTypes.includes('NVIDIA') ? 'h264_nvenc' : 'libx264';
        }

        // 3. 开始上传和转换
        const formData = new FormData();
        formData.append('videoFile', file);
        formData.append('preset', settings.preset);
        formData.append('videoCodec', settings.videoCodec);

        const response = await fetch('/api/conversion/start', {
            method: 'POST',
            body: formData
        });

        const result = await response.json();

        // 4. 监控任务进度
        if (result.success) {
            monitorTask(result.data.taskId);
        }

        return result;
    } catch (error) {
        // 5. 错误报告
        await fetch('/api/errors/report', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                type: 'Conversion Error',
                message: error.message,
                timestamp: new Date().toISOString()
            })
        });
        throw error;
    }
}
```

### 2. 实时任务监控
```javascript
class TaskMonitor {
    constructor() {
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl("/conversionHub")
            .build();
        this.setupEventHandlers();
    }

    setupEventHandlers() {
        // 进度更新
        this.connection.on("ProgressUpdate", (data) => {
            this.updateProgress(data.taskId, data.progress, data.speed);
        });

        // 任务完成
        this.connection.on("TaskCompleted", (data) => {
            this.showCompletionNotification(data);
        });

        // 任务失败
        this.connection.on("TaskFailed", (data) => {
            this.showErrorNotification(data);
        });
    }

    async startMonitoring(taskId) {
        await this.connection.start();
        await this.connection.invoke("JoinTaskGroup", taskId);
    }

    async stopMonitoring(taskId) {
        await this.connection.invoke("LeaveTaskGroup", taskId);
    }
}
```

### 3. GPU性能监控
```javascript
class GPUMonitor {
    constructor() {
        this.isMonitoring = false;
        this.monitoringInterval = null;
    }

    async startMonitoring() {
        if (this.isMonitoring) return;

        this.isMonitoring = true;
        this.monitoringInterval = setInterval(async () => {
            try {
                const response = await fetch('/api/gpu/performance');
                const data = await response.json();

                if (data.success) {
                    this.updateGPUMetrics(data.data);
                }
            } catch (error) {
                console.error('GPU监控失败:', error);
            }
        }, 5000);
    }

    stopMonitoring() {
        if (this.monitoringInterval) {
            clearInterval(this.monitoringInterval);
            this.monitoringInterval = null;
        }
        this.isMonitoring = false;
    }

    updateGPUMetrics(gpuData) {
        gpuData.forEach((gpu, index) => {
            const card = document.querySelector(`#gpu-card-${index}`);
            if (card) {
                card.querySelector('.gpu-usage').textContent = `${gpu.usage}%`;
                card.querySelector('.gpu-temperature').textContent = `${gpu.temperature}°C`;
                card.querySelector('.gpu-memory').textContent =
                    `${gpu.memoryUsed}MB / ${gpu.memoryTotal}MB`;
            }
        });
    }
}
```

## 🎯 最佳实践建议

### 1. 错误处理策略
```javascript
// 统一的API调用封装
async function apiCall(url, options = {}) {
    try {
        const response = await fetch(url, {
            headers: {
                'Content-Type': 'application/json',
                ...options.headers
            },
            ...options
        });

        const data = await response.json();

        if (!response.ok) {
            throw new Error(data.message || `HTTP ${response.status}`);
        }

        return data;
    } catch (error) {
        // 自动错误报告
        await reportError(error, url);
        throw error;
    }
}
```

### 2. 缓存策略
```javascript
// GPU信息缓存
class GPUCache {
    constructor() {
        this.cache = new Map();
        this.cacheTimeout = 5 * 60 * 1000; // 5分钟
    }

    async getGPUCapabilities() {
        const cacheKey = 'gpu-capabilities';
        const cached = this.cache.get(cacheKey);

        if (cached && Date.now() - cached.timestamp < this.cacheTimeout) {
            return cached.data;
        }

        const data = await apiCall('/api/gpu/capabilities');
        this.cache.set(cacheKey, {
            data,
            timestamp: Date.now()
        });

        return data;
    }
}
```

### 3. 进度跟踪
```javascript
// 上传进度跟踪
async function uploadWithProgress(file, onProgress) {
    const formData = new FormData();
    formData.append('videoFile', file);

    return new Promise((resolve, reject) => {
        const xhr = new XMLHttpRequest();

        xhr.upload.addEventListener('progress', (e) => {
            if (e.lengthComputable) {
                const progress = (e.loaded / e.total) * 100;
                onProgress(progress);
            }
        });

        xhr.addEventListener('load', () => {
            if (xhr.status === 200) {
                resolve(JSON.parse(xhr.responseText));
            } else {
                reject(new Error(`Upload failed: ${xhr.status}`));
            }
        });

        xhr.addEventListener('error', () => {
            reject(new Error('Upload failed'));
        });

        xhr.open('POST', '/api/conversion/start');
        xhr.send(formData);
    });
}
```

页面与后端API接口已完全对接，所有功能都有相应的服务端支持！🎯
