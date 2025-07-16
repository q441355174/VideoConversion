# 视频转换工具 (VideoConversion)

基于HandBrake功能的Web视频转换应用程序，使用ASP.NET Core和FFmpeg构建。

## 功能特性

### 🎥 视频转换
- 支持多种输入格式：MP4, AVI, MOV, MKV, WMV, FLV, WebM, M4V, 3GP
- 多种输出格式：MP4, WebM, MP3（仅音频）
- 预设配置：类似HandBrake的转换预设
- 自定义设置：分辨率、质量、编解码器等

### 📊 实时监控
- 实时转换进度显示
- 转换速度和剩余时间估算
- SignalR实时通信
- 系统状态监控

### 📁 文件管理
- 安全的文件上传和验证
- 自动文件清理
- 下载转换后的文件
- 文件大小限制和格式验证

### 📈 转换历史
- 完整的转换历史记录
- 任务状态跟踪
- 搜索和筛选功能
- 批量操作支持

### 🔧 系统管理
- 健康检查API
- 详细的日志记录
- 错误处理和恢复
- 性能监控

## 技术栈

- **后端**: ASP.NET Core 8.0
- **数据库**: SQLite + SqlSugar ORM
- **视频处理**: Xabe.FFmpeg
- **实时通信**: SignalR
- **前端**: Bootstrap 5 + JavaScript
- **容器化**: Docker支持

## 快速开始

### 前置要求

1. .NET 8.0 SDK
2. FFmpeg（自动下载或手动安装）

### 安装和运行

1. **克隆项目**
   ```bash
   git clone <repository-url>
   cd VideoConversion
   ```

2. **还原依赖**
   ```bash
   dotnet restore
   ```

3. **运行应用程序**
   ```bash
   dotnet run
   ```

4. **访问应用程序**
   - 主页: http://localhost:5065
   - 转换历史: http://localhost:5065/History
   - 健康检查: http://localhost:5065/api/health

### Docker运行

```bash
docker build -t videoconversion .
docker run -p 8080:8080 videoconversion
```

## 使用指南

### 1. 视频转换

1. 在主页选择要转换的视频文件
2. 选择转换预设或自定义设置
3. 点击"开始转换"
4. 实时查看转换进度
5. 转换完成后下载文件

### 2. 预设配置

应用程序提供多种预设配置：

- **Fast 1080p30**: 快速转换，适合预览
- **High Quality 1080p**: 高质量输出
- **Web Optimized**: 网络优化版本
- **iPhone/iPad**: 移动设备优化
- **YouTube**: YouTube上传优化
- **Small Size**: 小文件大小

### 3. 高级设置

- **输出格式**: MP4, WebM, MP3
- **分辨率**: 自定义或预设分辨率
- **视频质量**: CRF值控制（18-30）
- **音频质量**: 比特率设置

### 4. 转换历史

- 查看所有转换任务
- 按状态筛选任务
- 搜索特定任务
- 下载已完成的文件
- 删除不需要的任务

## API文档

### 转换API

- `POST /api/conversion/start` - 开始转换任务
- `GET /api/conversion/status/{taskId}` - 获取任务状态
- `GET /api/conversion/recent` - 获取最近任务
- `GET /api/conversion/download/{taskId}` - 下载文件
- `POST /api/conversion/cancel/{taskId}` - 取消任务
- `DELETE /api/conversion/{taskId}` - 删除任务

### 健康检查API

- `GET /api/health` - 基本健康检查
- `GET /api/health/status` - 详细系统状态
- `GET /api/health/stats/today` - 今日统计
- `GET /api/health/diagnostics` - 系统诊断

## 配置说明

### appsettings.json

```json
{
  "VideoConversion": {
    "UploadPath": "uploads",
    "OutputPath": "outputs",
    "MaxFileSize": 2147483648,
    "AllowedExtensions": [".mp4", ".avi", ".mov", ".mkv", ".wmv", ".flv", ".webm", ".m4v", ".3gp"],
    "CleanupIntervalMinutes": 60,
    "MaxConcurrentConversions": 2
  }
}
```

### 环境变量

- `ASPNETCORE_ENVIRONMENT` - 运行环境
- `ASPNETCORE_URLS` - 监听地址

## 部署

### 生产环境部署

1. **发布应用程序**
   ```bash
   dotnet publish -c Release -o publish
   ```

2. **配置反向代理**（如Nginx）
   ```nginx
   server {
       listen 80;
       server_name your-domain.com;
       
       location / {
           proxy_pass http://localhost:5065;
           proxy_http_version 1.1;
           proxy_set_header Upgrade $http_upgrade;
           proxy_set_header Connection keep-alive;
           proxy_set_header Host $host;
           proxy_cache_bypass $http_upgrade;
       }
   }
   ```

3. **配置系统服务**
   ```bash
   sudo systemctl enable videoconversion
   sudo systemctl start videoconversion
   ```

### Docker部署

```bash
docker-compose up -d
```

## 监控和维护

### 日志文件

- 应用程序日志: `logs/videoconversion-*.log`
- 系统日志: 通过systemd查看

### 性能监控

- 访问 `/api/health/status` 查看系统状态
- 监控磁盘空间和内存使用
- 定期清理旧文件和任务记录

### 备份

- 数据库文件: `videoconversion.db`
- 上传文件: `uploads/` 目录
- 输出文件: `outputs/` 目录

## 故障排除

### 常见问题

1. **FFmpeg未找到**
   - 确保FFmpeg已安装或在PATH中
   - 检查FFmpeg许可证

2. **文件上传失败**
   - 检查文件大小限制
   - 验证文件格式支持

3. **转换失败**
   - 查看详细错误日志
   - 检查输入文件完整性

4. **性能问题**
   - 调整并发转换数量
   - 监控系统资源使用

## 贡献

欢迎提交Issue和Pull Request来改进这个项目。

## 许可证

本项目采用MIT许可证。

## 支持

如有问题或建议，请创建Issue或联系开发团队。
