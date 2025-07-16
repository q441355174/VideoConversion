# 视频转换工具客户端

基于Avalonia UI的跨平台桌面客户端，用于连接视频转换服务器进行视频格式转换。

## 功能特性

### 🎥 视频转换
- 支持多种输入格式：MP4, AVI, MOV, MKV, WMV, FLV, WebM, M4V, 3GP
- 预设配置：类似HandBrake的转换预设
- 拖拽上传：支持直接拖拽文件到应用程序
- 自定义设置：分辨率、质量、编解码器等

### 📊 实时监控
- 实时转换进度显示
- 转换速度和剩余时间估算
- SignalR实时通信
- 系统通知支持

### 📁 文件管理
- 安全的文件上传
- 转换后文件下载
- 自定义下载路径
- 并发下载控制

### 📈 转换历史
- 完整的转换历史记录
- 任务状态跟踪
- 搜索和筛选功能
- 分页显示支持

### ⚙️ 设置管理
- 服务器地址配置
- 下载路径设置
- 通知开关
- 系统状态监控

## 技术栈

- **UI框架**: Avalonia UI 11.3.2
- **运行时**: .NET 8.0
- **架构模式**: MVVM (CommunityToolkit.Mvvm)
- **HTTP通信**: HttpClient
- **实时通信**: SignalR Client
- **依赖注入**: Microsoft.Extensions.DependencyInjection
- **日志记录**: Microsoft.Extensions.Logging

## 系统要求

### Windows
- Windows 10 版本 1809 或更高版本
- .NET 8.0 Runtime

### Linux
- 支持 .NET 8.0 的 Linux 发行版
- X11 或 Wayland 显示服务器

### macOS
- macOS 10.15 或更高版本
- .NET 8.0 Runtime

## 安装和运行

### 从源码构建

1. **克隆项目**
   ```bash
   git clone <repository-url>
   cd VideoConversion-Client
   ```

2. **安装依赖**
   ```bash
   dotnet restore
   ```

3. **构建项目**
   ```bash
   dotnet build
   ```

4. **运行应用**
   ```bash
   dotnet run
   ```

### 发布应用

#### Windows
```bash
dotnet publish -c Release -r win-x64 --self-contained
```

#### Linux
```bash
dotnet publish -c Release -r linux-x64 --self-contained
```

#### macOS
```bash
dotnet publish -c Release -r osx-x64 --self-contained
```

## 使用说明

### 首次使用

1. **配置服务器地址**
   - 打开"设置"页面
   - 输入视频转换服务器的地址（默认：http://localhost:5000）
   - 点击"测试连接"确认连接正常

2. **设置下载路径**
   - 在设置页面选择转换后文件的保存位置
   - 默认为系统下载文件夹

### 转换视频

1. **选择文件**
   - 点击"浏览"按钮选择视频文件
   - 或直接拖拽文件到应用程序窗口

2. **配置转换设置**
   - 输入任务名称（可选）
   - 选择转换预设

3. **开始转换**
   - 点击"开始转换"按钮
   - 实时查看转换进度

4. **下载结果**
   - 转换完成后，文件会自动下载到指定路径
   - 也可以在历史记录中手动下载

### 管理历史记录

1. **查看历史**
   - 切换到"历史记录"页面
   - 查看所有转换任务的状态

2. **搜索和筛选**
   - 使用搜索框查找特定任务
   - 按状态筛选任务

3. **任务操作**
   - 下载已完成的转换文件
   - 取消正在进行的任务
   - 删除不需要的任务记录

## 配置文件

应用程序设置保存在：
- **Windows**: `%APPDATA%\VideoConversion-Client\settings.json`
- **Linux**: `~/.config/VideoConversion-Client/settings.json`
- **macOS**: `~/Library/Application Support/VideoConversion-Client/settings.json`

## 故障排除

### 常见问题

1. **无法连接到服务器**
   - 检查服务器地址是否正确
   - 确认服务器正在运行
   - 检查防火墙设置

2. **文件上传失败**
   - 检查文件格式是否支持
   - 确认文件大小未超过限制
   - 检查网络连接

3. **转换失败**
   - 查看错误信息
   - 检查输入文件是否完整
   - 尝试不同的转换预设

4. **下载失败**
   - 检查下载路径是否有写入权限
   - 确认磁盘空间充足
   - 重试下载操作

### 日志文件

应用程序日志可以通过以下方式查看：
- 控制台输出（开发模式）
- 系统日志（发布模式）

## 开发说明

### 项目结构

```
VideoConversion-Client/
├── Models/              # 数据模型
├── ViewModels/          # 视图模型
├── Views/               # 用户界面
├── Services/            # 业务服务
├── Converters/          # 值转换器
├── App.axaml           # 应用程序资源
├── Program.cs          # 程序入口点
└── README.md           # 项目说明
```

### 添加新功能

1. 在相应的文件夹中创建新的类文件
2. 在Program.cs中注册新的服务
3. 更新相关的ViewModel和View
4. 添加必要的单元测试

## 许可证

本项目采用MIT许可证。详见LICENSE文件。

## 支持

如有问题或建议，请创建Issue或联系开发团队。
