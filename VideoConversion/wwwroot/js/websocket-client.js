/**
 * WebSocket客户端管理器
 * 提供WebSocket连接管理、消息处理、重连机制等功能
 */
class WebSocketClient {
    constructor(url = null) {
        this.url = url || this.getWebSocketUrl();
        this.socket = null;
        this.connectionId = null;
        this.isConnected = false;
        this.reconnectAttempts = 0;
        this.maxReconnectAttempts = 5;
        this.reconnectInterval = 3000; // 3秒
        this.pingInterval = 30000; // 30秒
        this.pingTimer = null;
        this.messageHandlers = new Map();
        this.eventListeners = new Map();
        
        // 绑定方法上下文
        this.onOpen = this.onOpen.bind(this);
        this.onMessage = this.onMessage.bind(this);
        this.onClose = this.onClose.bind(this);
        this.onError = this.onError.bind(this);
    }

    /**
     * 获取WebSocket URL
     */
    getWebSocketUrl() {
        const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
        const host = window.location.host;
        return `${protocol}//${host}/ws`;
    }

    /**
     * 连接WebSocket
     */
    async connect() {
        if (this.socket && this.socket.readyState === WebSocket.OPEN) {
            console.log('WebSocket已经连接');
            return;
        }

        try {
            console.log('正在连接WebSocket:', this.url);
            this.socket = new WebSocket(this.url);
            
            this.socket.onopen = this.onOpen;
            this.socket.onmessage = this.onMessage;
            this.socket.onclose = this.onClose;
            this.socket.onerror = this.onError;

        } catch (error) {
            console.error('WebSocket连接失败:', error);
            this.scheduleReconnect();
        }
    }

    /**
     * 断开WebSocket连接
     */
    disconnect() {
        console.log('断开WebSocket连接');
        this.isConnected = false;
        this.reconnectAttempts = 0;
        
        if (this.pingTimer) {
            clearInterval(this.pingTimer);
            this.pingTimer = null;
        }

        if (this.socket) {
            this.socket.close(1000, 'Client disconnect');
            this.socket = null;
        }
    }

    /**
     * 连接打开事件处理
     */
    onOpen(event) {
        console.log('WebSocket连接已建立');
        this.isConnected = true;
        this.reconnectAttempts = 0;
        
        // 开始心跳检测
        this.startPing();
        
        // 触发连接事件
        this.emit('connected', { event });
    }

    /**
     * 消息接收事件处理
     */
    onMessage(event) {
        try {
            const message = JSON.parse(event.data);
            console.log('收到WebSocket消息:', message);

            // 处理连接确认消息
            if (message.type === 'Connect' && message.connectionId) {
                this.connectionId = message.connectionId;
                console.log('WebSocket连接ID:', this.connectionId);
            }

            // 处理Pong消息
            if (message.type === 'Pong') {
                console.log('收到Pong消息，连接正常');
                return;
            }

            // 调用消息处理器
            this.handleMessage(message);
            
            // 触发消息事件
            this.emit('message', message);

        } catch (error) {
            console.error('解析WebSocket消息失败:', error, event.data);
        }
    }

    /**
     * 连接关闭事件处理
     */
    onClose(event) {
        console.log('WebSocket连接已关闭:', event.code, event.reason);
        this.isConnected = false;
        this.connectionId = null;
        
        if (this.pingTimer) {
            clearInterval(this.pingTimer);
            this.pingTimer = null;
        }

        // 触发断开事件
        this.emit('disconnected', { event });

        // 如果不是主动断开，尝试重连
        if (event.code !== 1000) {
            this.scheduleReconnect();
        }
    }

    /**
     * 错误事件处理
     */
    onError(event) {
        console.error('WebSocket错误:', event);
        this.emit('error', { event });
    }

    /**
     * 发送消息
     */
    send(message) {
        if (!this.isConnected || !this.socket) {
            console.warn('WebSocket未连接，无法发送消息:', message);
            return false;
        }

        try {
            const messageStr = typeof message === 'string' ? message : JSON.stringify(message);
            this.socket.send(messageStr);
            console.log('发送WebSocket消息:', message);
            return true;
        } catch (error) {
            console.error('发送WebSocket消息失败:', error);
            return false;
        }
    }

    /**
     * 发送Ping消息
     */
    ping() {
        this.send({
            type: 'Ping',
            timestamp: new Date().toISOString()
        });
    }

    /**
     * 开始心跳检测
     */
    startPing() {
        if (this.pingTimer) {
            clearInterval(this.pingTimer);
        }

        this.pingTimer = setInterval(() => {
            if (this.isConnected) {
                this.ping();
            }
        }, this.pingInterval);
    }

    /**
     * 加入组
     */
    joinGroup(groupName) {
        return this.send({
            type: 'JoinGroup',
            groupName: groupName,
            timestamp: new Date().toISOString()
        });
    }

    /**
     * 离开组
     */
    leaveGroup(groupName) {
        return this.send({
            type: 'LeaveGroup',
            groupName: groupName,
            timestamp: new Date().toISOString()
        });
    }

    /**
     * 发送自定义消息
     */
    sendCustomMessage(action, payload = null) {
        return this.send({
            type: 'CustomMessage',
            action: action,
            payload: payload,
            timestamp: new Date().toISOString()
        });
    }

    /**
     * 注册消息处理器
     */
    onMessageType(messageType, handler) {
        if (!this.messageHandlers.has(messageType)) {
            this.messageHandlers.set(messageType, []);
        }
        this.messageHandlers.get(messageType).push(handler);
    }

    /**
     * 移除消息处理器
     */
    offMessageType(messageType, handler) {
        if (this.messageHandlers.has(messageType)) {
            const handlers = this.messageHandlers.get(messageType);
            const index = handlers.indexOf(handler);
            if (index > -1) {
                handlers.splice(index, 1);
            }
        }
    }

    /**
     * 处理收到的消息
     */
    handleMessage(message) {
        const messageType = message.type;
        if (this.messageHandlers.has(messageType)) {
            const handlers = this.messageHandlers.get(messageType);
            handlers.forEach(handler => {
                try {
                    handler(message);
                } catch (error) {
                    console.error('消息处理器执行失败:', error);
                }
            });
        }
    }

    /**
     * 注册事件监听器
     */
    on(eventName, listener) {
        if (!this.eventListeners.has(eventName)) {
            this.eventListeners.set(eventName, []);
        }
        this.eventListeners.get(eventName).push(listener);
    }

    /**
     * 移除事件监听器
     */
    off(eventName, listener) {
        if (this.eventListeners.has(eventName)) {
            const listeners = this.eventListeners.get(eventName);
            const index = listeners.indexOf(listener);
            if (index > -1) {
                listeners.splice(index, 1);
            }
        }
    }

    /**
     * 触发事件
     */
    emit(eventName, data) {
        if (this.eventListeners.has(eventName)) {
            const listeners = this.eventListeners.get(eventName);
            listeners.forEach(listener => {
                try {
                    listener(data);
                } catch (error) {
                    console.error('事件监听器执行失败:', error);
                }
            });
        }
    }

    /**
     * 安排重连
     */
    scheduleReconnect() {
        if (this.reconnectAttempts >= this.maxReconnectAttempts) {
            console.error('WebSocket重连次数已达上限，停止重连');
            this.emit('reconnectFailed');
            return;
        }

        this.reconnectAttempts++;
        const delay = this.reconnectInterval * Math.pow(2, this.reconnectAttempts - 1); // 指数退避
        
        console.log(`WebSocket将在 ${delay}ms 后进行第 ${this.reconnectAttempts} 次重连`);
        
        setTimeout(() => {
            if (!this.isConnected) {
                this.connect();
            }
        }, delay);
    }

    /**
     * 获取连接状态
     */
    getConnectionState() {
        if (!this.socket) return 'Disconnected';
        
        switch (this.socket.readyState) {
            case WebSocket.CONNECTING: return 'Connecting';
            case WebSocket.OPEN: return 'Connected';
            case WebSocket.CLOSING: return 'Closing';
            case WebSocket.CLOSED: return 'Disconnected';
            default: return 'Unknown';
        }
    }

    /**
     * 获取连接信息
     */
    getConnectionInfo() {
        return {
            url: this.url,
            connectionId: this.connectionId,
            isConnected: this.isConnected,
            state: this.getConnectionState(),
            reconnectAttempts: this.reconnectAttempts
        };
    }
}

// 导出WebSocket客户端类
window.WebSocketClient = WebSocketClient;
