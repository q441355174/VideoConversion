// VideoConversion 站点通用 JavaScript 文件
// 提供全站通用的功能和工具函数

(function() {
    'use strict';

    // 全局配置
    window.VideoConversion = window.VideoConversion || {};
    
    // 工具函数
    window.VideoConversion.Utils = {
        
        /**
         * 格式化文件大小
         * @param {number} bytes - 字节数
         * @returns {string} 格式化后的文件大小
         */
        formatFileSize: function(bytes) {
            if (bytes === 0) return '0 Bytes';
            
            const k = 1024;
            const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
            const i = Math.floor(Math.log(bytes) / Math.log(k));
            
            return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
        },

        /**
         * 格式化时间
         * @param {number} seconds - 秒数
         * @returns {string} 格式化后的时间
         */
        formatTime: function(seconds) {
            if (!seconds || seconds < 0) return '00:00:00';
            
            const hours = Math.floor(seconds / 3600);
            const minutes = Math.floor((seconds % 3600) / 60);
            const secs = Math.floor(seconds % 60);
            
            return [hours, minutes, secs]
                .map(val => val.toString().padStart(2, '0'))
                .join(':');
        },

        /**
         * 格式化日期时间
         * @param {string|Date} dateTime - 日期时间
         * @returns {string} 格式化后的日期时间
         */
        formatDateTime: function(dateTime) {
            if (!dateTime) return '-';
            
            const date = new Date(dateTime);
            if (isNaN(date.getTime())) return '-';
            
            return date.toLocaleString('zh-CN', {
                year: 'numeric',
                month: '2-digit',
                day: '2-digit',
                hour: '2-digit',
                minute: '2-digit',
                second: '2-digit'
            });
        },

        /**
         * 显示通知消息
         * @param {string} message - 消息内容
         * @param {string} type - 消息类型 (success, error, warning, info)
         * @param {number} duration - 显示时长（毫秒）
         */
        showNotification: function(message, type = 'info', duration = 3000) {
            // 创建通知元素
            const notification = document.createElement('div');
            notification.className = `alert alert-${type === 'error' ? 'danger' : type} alert-dismissible fade show position-fixed`;
            notification.style.cssText = 'top: 20px; right: 20px; z-index: 9999; min-width: 300px;';
            
            notification.innerHTML = `
                ${message}
                <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
            `;
            
            // 添加到页面
            document.body.appendChild(notification);
            
            // 自动移除
            if (duration > 0) {
                setTimeout(() => {
                    if (notification.parentNode) {
                        notification.remove();
                    }
                }, duration);
            }
        },

        /**
         * 复制文本到剪贴板
         * @param {string} text - 要复制的文本
         * @returns {Promise<boolean>} 是否成功
         */
        copyToClipboard: async function(text) {
            try {
                if (navigator.clipboard && window.isSecureContext) {
                    await navigator.clipboard.writeText(text);
                    return true;
                } else {
                    // 降级方案
                    const textArea = document.createElement('textarea');
                    textArea.value = text;
                    textArea.style.position = 'fixed';
                    textArea.style.left = '-999999px';
                    textArea.style.top = '-999999px';
                    document.body.appendChild(textArea);
                    textArea.focus();
                    textArea.select();
                    const result = document.execCommand('copy');
                    textArea.remove();
                    return result;
                }
            } catch (error) {
                console.error('复制到剪贴板失败:', error);
                return false;
            }
        },

        /**
         * 防抖函数
         * @param {Function} func - 要防抖的函数
         * @param {number} wait - 等待时间（毫秒）
         * @returns {Function} 防抖后的函数
         */
        debounce: function(func, wait) {
            let timeout;
            return function executedFunction(...args) {
                const later = () => {
                    clearTimeout(timeout);
                    func(...args);
                };
                clearTimeout(timeout);
                timeout = setTimeout(later, wait);
            };
        },

        /**
         * 节流函数
         * @param {Function} func - 要节流的函数
         * @param {number} limit - 限制时间（毫秒）
         * @returns {Function} 节流后的函数
         */
        throttle: function(func, limit) {
            let inThrottle;
            return function() {
                const args = arguments;
                const context = this;
                if (!inThrottle) {
                    func.apply(context, args);
                    inThrottle = true;
                    setTimeout(() => inThrottle = false, limit);
                }
            };
        }
    };

    // 页面加载完成后的初始化
    document.addEventListener('DOMContentLoaded', function() {
        
        // 初始化工具提示
        if (typeof bootstrap !== 'undefined' && bootstrap.Tooltip) {
            const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
            tooltipTriggerList.map(function (tooltipTriggerEl) {
                return new bootstrap.Tooltip(tooltipTriggerEl);
            });
        }

        // 初始化弹出框
        if (typeof bootstrap !== 'undefined' && bootstrap.Popover) {
            const popoverTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="popover"]'));
            popoverTriggerList.map(function (popoverTriggerEl) {
                return new bootstrap.Popover(popoverTriggerEl);
            });
        }

        // 添加复制按钮功能
        document.addEventListener('click', function(e) {
            if (e.target.classList.contains('copy-btn') || e.target.closest('.copy-btn')) {
                const btn = e.target.classList.contains('copy-btn') ? e.target : e.target.closest('.copy-btn');
                const text = btn.dataset.copyText || btn.textContent;
                
                VideoConversion.Utils.copyToClipboard(text).then(success => {
                    if (success) {
                        VideoConversion.Utils.showNotification('已复制到剪贴板', 'success', 2000);
                    } else {
                        VideoConversion.Utils.showNotification('复制失败', 'error', 2000);
                    }
                });
            }
        });

        // 添加确认删除功能
        document.addEventListener('click', function(e) {
            if (e.target.classList.contains('confirm-delete') || e.target.closest('.confirm-delete')) {
                const btn = e.target.classList.contains('confirm-delete') ? e.target : e.target.closest('.confirm-delete');
                const message = btn.dataset.confirmMessage || '确定要删除吗？';
                
                if (!confirm(message)) {
                    e.preventDefault();
                    e.stopPropagation();
                    return false;
                }
            }
        });

        // 自动隐藏警告消息（排除GPU面板和永久性消息）
        setTimeout(function() {
            const alerts = document.querySelectorAll('.alert:not(.alert-permanent):not(#gpuInfo .alert)');
            alerts.forEach(function(alert) {
                // 只隐藏临时消息，不隐藏GPU面板中的结果
                if (!alert.closest('#gpuInfo')) {
                    if (alert.classList.contains('fade')) {
                        alert.classList.remove('show');
                        setTimeout(() => alert.remove(), 150);
                    } else {
                        alert.remove();
                    }
                }
            });
        }, 5000);

        console.log('VideoConversion site.js 已加载');
    });

})();
