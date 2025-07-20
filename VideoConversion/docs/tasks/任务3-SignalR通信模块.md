# 任务3：SignalR通信模块

## 📋 任务概述

建立可靠的SignalR实时通信系统，实现客户端与服务器之间的双向通信，支持实时进度更新和状态同步。

## 🎯 任务目标

- [ ] 建立SignalR连接管理
- [ ] 实现自动重连机制
- [ ] 注册事件监听器
- [ ] 处理连接状态监控

## 📝 详细任务清单

### 步骤3.1：建立SignalR连接

#### 任务清单
- [ ] 配置SignalR连接参数
- [ ] 实现连接启动函数
- [ ] 添加连接状态监控
- [ ] 处理连接错误

#### 实现代码
```javascript
// SignalR连接初始化
function initializeSignalR() {
    console.log('📡 初始化SignalR连接...');
    
    // 设置连接状态事件
    setupConnectionEvents();
    
    // 启动连接
    startConnection();
    
    // 注册事件监听器
    registerSignalREvents();
}

// 启动SignalR连接
async function startConnection() {
    try {
        console.log('🔌 启动SignalR连接...');
        await connection.start();
        console.log("✅ SignalR连接成功");
        updateConnectionStatus('Connected');
        showAlert('success', 'SignalR连接已建立，可以实时接收转换进度');
        
        // 连接成功后加载最近任务
        loadRecentTasks();
    } catch (err) {
        console.error("❌ SignalR连接失败:", err);
        updateConnectionStatus('Failed');
        showAlert('danger', 'SignalR连接失败，进度更新可能不可用');
        
        // 5秒后重试连接
        setTimeout(startConnection, 5000);
    }
}

// 连接状态事件处理
function setupConnectionEvents() {
    // 重连中事件
    connection.onreconnecting((error) => {
        console.log("🔄 SignalR重连中...", error);
        updateConnectionStatus('Reconnecting');
        showAlert('warning', 'SignalR连接中断，正在重连...');
    });
    
    // 重连成功事件
    connection.onreconnected((connectionId) => {
        console.log("✅ SignalR重连成功:", connectionId);
        updateConnectionStatus('Connected');
        showAlert('success', 'SignalR连接已恢复');
        
        // 重连后重新加入任务组
        if (currentTaskId) {
            connection.invoke("JoinTaskGroup", currentTaskId).catch(err => {
                console.error("重新加入任务组失败:", err);
            });
        }
        
        // 刷新任务状态
        loadRecentTasks();
    });
    
    // 连接关闭事件
    connection.onclose((error) => {
        console.warn("⚠️ SignalR连接已关闭:", error);
        updateConnectionStatus('Disconnected');
        showAlert('warning', 'SignalR连接已断开');
    });
}
```

### 步骤3.2：注册SignalR事件监听器

#### 任务清单
- [ ] 注册进度更新事件
- [ ] 注册任务状态事件
- [ ] 注册上传进度事件
- [ ] 注册错误处理事件

#### 实现代码
```javascript
// 注册SignalR事件监听器
function registerSignalREvents() {
    console.log('📋 注册SignalR事件监听器...');
    
    // 监听进度更新
    connection.on("ProgressUpdate", function (data) {
        console.log("📊 收到进度更新:", data);
        updateProgress(data);
    });
    
    // 监听任务开始
    connection.on("TaskStarted", function (data) {
        console.log("🚀 任务开始:", data);
        handleTaskStarted(data);
    });
    
    // 监听任务完成
    connection.on("TaskCompleted", function (data) {
        console.log("✅ 任务完成:", data);
        handleTaskCompleted(data);
    });
    
    // 监听任务失败
    connection.on("TaskFailed", function (data) {
        console.log("❌ 任务失败:", data);
        handleTaskFailed(data);
    });
    
    // 监听上传进度
    connection.on("UploadProgress", function (data) {
        console.log("📤 上传进度:", data);
        updateUploadProgress(data);
    });
    
    // 监听上传完成
    connection.on("UploadCompleted", function (data) {
        console.log("✅ 上传完成:", data);
        showAlert('success', `文件上传完成: ${data.FilePath}`);
        hideUploadProgress();
    });
    
    // 监听上传失败
    connection.on("UploadFailed", function (data) {
        console.log("❌ 上传失败:", data);
        showAlert('danger', `上传失败: ${data.ErrorMessage}`);
        hideUploadProgress();
    });
    
    // 监听系统通知
    connection.on("SystemNotification", function (data) {
        console.log("📢 系统通知:", data);
        showAlert(data.type || 'info', data.message);
    });
    
    // 监听错误
    connection.on("Error", function (message) {
        console.error("❌ SignalR错误:", message);
        showAlert('danger', 'SignalR错误: ' + message);
    });
    
    // 监听任务取消完成
    connection.on("TaskCancelCompleted", function (data) {
        console.log("🚫 任务取消完成:", data);
        if (data.taskId === currentTaskId) {
            showAlert('success', '任务取消成功');
            hideCurrentTask();
            loadRecentTasks();
        }
    });
}
```

### 步骤3.3：实现连接状态管理

#### 任务清单
- [ ] 添加连接状态显示
- [ ] 实现连接重试机制
- [ ] 处理网络中断恢复
- [ ] 管理任务组加入/离开

#### 实现代码
```javascript
// 连接状态管理
function manageConnectionState() {
    // 添加连接状态显示元素
    addConnectionStatusIndicator();
    
    // 定期检查连接状态
    setInterval(checkConnectionHealth, 30000); // 每30秒检查一次
}

// 添加连接状态指示器
function addConnectionStatusIndicator() {
    const statusHtml = `
        <div class="position-fixed top-0 end-0 p-3" style="z-index: 1050;">
            <div class="d-flex align-items-center">
                <small class="text-muted me-2">连接状态:</small>
                <span id="connectionStatus" class="badge bg-secondary">未连接</span>
            </div>
        </div>
    `;
    
    document.body.insertAdjacentHTML('beforeend', statusHtml);
}

// 检查连接健康状态
function checkConnectionHealth() {
    if (connection.state === signalR.HubConnectionState.Disconnected) {
        console.log('🔍 检测到连接断开，尝试重连...');
        startConnection();
    }
}

// 任务组管理
async function joinTaskGroup(taskId) {
    if (!taskId || connection.state !== signalR.HubConnectionState.Connected) {
        return;
    }
    
    try {
        await connection.invoke("JoinTaskGroup", taskId);
        console.log(`✅ 已加入任务组: ${taskId}`);
    } catch (error) {
        console.error(`❌ 加入任务组失败: ${taskId}`, error);
    }
}

async function leaveTaskGroup(taskId) {
    if (!taskId || connection.state !== signalR.HubConnectionState.Connected) {
        return;
    }
    
    try {
        await connection.invoke("LeaveTaskGroup", taskId);
        console.log(`✅ 已离开任务组: ${taskId}`);
    } catch (error) {
        console.error(`❌ 离开任务组失败: ${taskId}`, error);
    }
}
```

### 步骤3.4：实现SignalR方法调用

#### 任务清单
- [ ] 实现服务器方法调用
- [ ] 添加调用错误处理
- [ ] 实现超时处理
- [ ] 添加重试机制

#### 实现代码
```javascript
// SignalR方法调用封装
class SignalRInvoker {
    static async invoke(methodName, ...args) {
        if (connection.state !== signalR.HubConnectionState.Connected) {
            throw new Error('SignalR连接未建立');
        }
        
        try {
            console.log(`📞 调用SignalR方法: ${methodName}`, args);
            const result = await connection.invoke(methodName, ...args);
            console.log(`✅ SignalR方法调用成功: ${methodName}`, result);
            return result;
        } catch (error) {
            console.error(`❌ SignalR方法调用失败: ${methodName}`, error);
            throw error;
        }
    }
    
    static async invokeWithTimeout(methodName, timeout = 30000, ...args) {
        return Promise.race([
            this.invoke(methodName, ...args),
            new Promise((_, reject) => 
                setTimeout(() => reject(new Error('SignalR调用超时')), timeout)
            )
        ]);
    }
    
    static async invokeWithRetry(methodName, maxRetries = 3, ...args) {
        let lastError;
        
        for (let i = 0; i < maxRetries; i++) {
            try {
                return await this.invoke(methodName, ...args);
            } catch (error) {
                lastError = error;
                console.warn(`SignalR方法调用重试 ${i + 1}/${maxRetries}: ${methodName}`);
                
                if (i < maxRetries - 1) {
                    await new Promise(resolve => setTimeout(resolve, 1000 * (i + 1)));
                }
            }
        }
        
        throw lastError;
    }
}

// 常用SignalR方法调用
async function getTaskStatus(taskId) {
    try {
        return await SignalRInvoker.invoke("GetTaskStatus", taskId);
    } catch (error) {
        showAlert('warning', '获取任务状态失败: ' + error.message);
    }
}

async function cancelTask(taskId) {
    try {
        return await SignalRInvoker.invoke("CancelTask", taskId);
    } catch (error) {
        showAlert('danger', '取消任务失败: ' + error.message);
    }
}

async function requestTaskList() {
    try {
        return await SignalRInvoker.invoke("GetRecentTasks", 10);
    } catch (error) {
        console.error('获取任务列表失败:', error);
    }
}
```

## ✅ 验收标准

### 功能验收
- [ ] SignalR连接建立成功
- [ ] 自动重连机制正常
- [ ] 事件监听器正确注册
- [ ] 方法调用功能正常

### 稳定性验收
- [ ] 网络中断后自动恢复
- [ ] 长时间连接保持稳定
- [ ] 错误处理机制完善
- [ ] 内存泄漏检查通过

### 性能验收
- [ ] 连接建立时间合理
- [ ] 事件响应及时
- [ ] 资源占用适中
- [ ] 并发处理能力

## 🔗 依赖关系

### 前置依赖
- 任务1：基础架构搭建
- SignalR Hub服务端实现

### 后续任务
- 任务4：转换设置模块
- 任务5：任务管理模块

## 📊 预估工时

- **开发时间**: 3-4小时
- **测试时间**: 2小时
- **总计**: 5-6小时

## 🚨 注意事项

1. **连接稳定性**: 确保在各种网络环境下连接稳定
2. **错误恢复**: 完善的错误处理和自动恢复机制
3. **性能优化**: 避免频繁的连接重试影响性能
4. **安全考虑**: 验证SignalR连接的安全性

## 📝 完成标记

- [ ] 步骤3.1完成
- [ ] 步骤3.2完成
- [ ] 步骤3.3完成
- [ ] 步骤3.4完成
- [ ] 验收测试通过
- [ ] 代码提交完成

**完成时间**: ___________  
**开发者**: ___________  
**审核者**: ___________
