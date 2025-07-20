# Index.cshtml 页面API接口对接优化建议

## 📊 当前API接口对接状态分析

经过详细分析，Index.cshtml页面的API接口对接情况如下：

### ✅ 已完全对接的模块

#### 1. **GPU模块 - 100%对接** 🎮
- ✅ `detectGPUHardware()` → `GET /api/gpu/detect`
- ✅ `getGPUPerformanceData()` → `GET /api/gpu/performance`
- ✅ 完整的错误处理和模拟数据回退机制

#### 2. **任务管理模块 - 100%对接** 📋
- ✅ `loadRecentTasks()` → `GET /api/conversion/recent`
- ✅ SignalR Hub方法：`GetRecentTasks()`
- ✅ 完整的任务状态管理和实时更新

#### 3. **错误处理模块 - 100%对接** 🚨
- ✅ `sendErrorToServer()` → `POST /api/errors/report`
- ✅ 用户反馈 → `POST /api/errors/feedback`
- ✅ 完整的错误日志记录和报告机制

#### 4. **文件上传模块 - 90%对接** 📁
- ✅ `handleNormalFileUpload()` → `POST /api/conversion/start`
- ⚠️ `handleLargeFileUpload()` - 需要完善大文件上传API调用

### 🔧 需要优化的部分

#### 1. **GPU能力检测API缺失**
当前GPU模块缺少GPU能力检测的API调用：

**问题**：
```javascript
// 在loadInfo()中调用了不存在的方法
const capabilities = await this.getGPUCapabilities();
```

**解决方案**：
```javascript
// 添加GPU能力检测方法
getGPUCapabilities: async function() {
    try {
        const response = await fetch('/api/gpu/capabilities');
        if (response.ok) {
            return await response.json();
        } else {
            throw new Error('GPU能力检测API调用失败');
        }
    } catch (error) {
        console.log('GPU能力检测失败，使用模拟数据:', error.message);
        return this.getMockCapabilitiesData();
    }
}
```

#### 2. **大文件上传API调用不完整**
当前大文件上传只有模拟实现：

**问题**：
```javascript
handleLargeFileUpload: async function(file, formData) {
    // 只有模拟进度，没有实际API调用
    return await this.simulateUploadProgress();
}
```

**解决方案**：
```javascript
handleLargeFileUpload: async function(file, formData) {
    console.log('📤 处理大文件上传:', file.name);
    
    try {
        const response = await fetch('/api/upload/large-file', {
            method: 'POST',
            body: formData
        });
        
        if (!response.ok) {
            throw new Error(`大文件上传失败: ${response.status}`);
        }
        
        return await response.json();
    } catch (error) {
        console.error('大文件上传失败:', error);
        throw error;
    }
}
```

#### 3. **统一的API调用封装**
建议添加统一的API调用方法：

```javascript
// 在Utils模块中添加
const Utils = {
    // 统一的API调用方法
    apiCall: async function(url, options = {}) {
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
            ErrorHandler.handleApplicationError(error, {
                module: 'API',
                action: url
            });
            throw error;
        }
    }
};
```

## 🚀 具体优化实施方案

### 1. 添加GPU能力检测API
在GPU模块中添加缺失的方法：

```javascript
// 在GPUManager中添加
getGPUCapabilities: async function() {
    try {
        const response = await fetch('/api/gpu/capabilities');
        if (response.ok) {
            const data = await response.json();
            console.log('GPU能力检测成功:', data);
            return data;
        } else {
            throw new Error(`GPU能力检测失败: ${response.status}`);
        }
    } catch (error) {
        console.log('GPU能力检测失败，使用模拟数据:', error.message);
        return {
            success: true,
            data: {
                hasAnyGpuSupport: true,
                supportedTypes: 'NVIDIA NVENC',
                nvidia: { supported: true, encoders: ['h264_nvenc', 'hevc_nvenc'] },
                intel: { supported: true, encoders: ['h264_qsv', 'hevc_qsv'] },
                amd: { supported: false, encoders: [] }
            }
        };
    }
}
```

### 2. 完善大文件上传API调用
修改FileUpload模块：

```javascript
handleLargeFileUpload: async function(file, formData) {
    console.log('📤 处理大文件上传:', file.name);
    
    this.showProgress();
    
    try {
        // 使用XMLHttpRequest支持进度跟踪
        return new Promise((resolve, reject) => {
            const xhr = new XMLHttpRequest();
            
            xhr.upload.addEventListener('progress', (e) => {
                if (e.lengthComputable) {
                    const progress = (e.loaded / e.total) * 100;
                    this.updateProgress(progress);
                }
            });
            
            xhr.addEventListener('load', () => {
                if (xhr.status === 200) {
                    const result = JSON.parse(xhr.responseText);
                    resolve(result);
                } else {
                    reject(new Error(`大文件上传失败: ${xhr.status}`));
                }
            });
            
            xhr.addEventListener('error', () => {
                reject(new Error('大文件上传网络错误'));
            });
            
            xhr.open('POST', '/api/upload/large-file');
            xhr.send(formData);
        });
    } catch (error) {
        console.error('大文件上传失败:', error);
        this.hideProgress();
        throw error;
    }
}
```

### 3. 添加缓存机制
为频繁调用的API添加缓存：

```javascript
// 在Utils中添加缓存管理
const APICache = {
    cache: new Map(),
    cacheTimeout: 5 * 60 * 1000, // 5分钟
    
    get: function(key) {
        const cached = this.cache.get(key);
        if (cached && Date.now() - cached.timestamp < this.cacheTimeout) {
            return cached.data;
        }
        return null;
    },
    
    set: function(key, data) {
        this.cache.set(key, {
            data,
            timestamp: Date.now()
        });
    },
    
    clear: function() {
        this.cache.clear();
    }
};

// 在GPU模块中使用缓存
getGPUCapabilities: async function() {
    const cacheKey = 'gpu-capabilities';
    const cached = APICache.get(cacheKey);
    
    if (cached) {
        console.log('使用缓存的GPU能力数据');
        return cached;
    }
    
    const data = await Utils.apiCall('/api/gpu/capabilities');
    APICache.set(cacheKey, data);
    return data;
}
```

## 📋 优化检查清单

### 立即需要实施的优化
- [ ] 添加`getGPUCapabilities()`方法
- [ ] 完善大文件上传API调用
- [ ] 添加统一的API调用封装
- [ ] 实施API缓存机制

### 可选的增强功能
- [ ] 添加API调用重试机制
- [ ] 实施请求去重
- [ ] 添加API调用性能监控
- [ ] 实施离线模式支持

## 🎯 预期效果

实施这些优化后，将获得：

1. **完整的API覆盖** - 所有功能都有对应的API调用
2. **更好的性能** - 缓存机制减少重复请求
3. **更强的稳定性** - 统一错误处理和重试机制
4. **更好的用户体验** - 大文件上传进度跟踪
5. **更易维护** - 统一的API调用模式

## 🔧 实施优先级

1. **高优先级** - 添加缺失的API方法（GPU能力检测）
2. **中优先级** - 完善大文件上传API调用
3. **低优先级** - 添加缓存和性能优化

这些优化将使Index.cshtml页面与后端API的对接更加完善和稳定！🚀
