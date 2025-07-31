using VideoConversion.Services;
using VideoConversion.Hubs;
using VideoConversion.Middleware;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Http.Features;
using Serilog;
using System.Text;
using System.Text.Json;

// 🔑 设置控制台编码为UTF-8
try
{
    Console.OutputEncoding = Encoding.UTF8;
    Console.InputEncoding = Encoding.UTF8;
}
catch (Exception ex)
{
    Console.WriteLine($"设置控制台UTF-8编码失败: {ex.Message}");
}

var builder = WebApplication.CreateBuilder(args);

// 配置Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/videoconversion-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        fileSizeLimitBytes: 10 * 1024 * 1024, // 10MB
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // 配置JSON序列化选项以匹配客户端
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.WriteIndented = true;
    });

// 添加CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// 添加SignalR
builder.Services.AddSignalR();

// 添加WebSocket服务
builder.Services.AddWebSocketServices();
builder.Services.AddScoped<WebSocketNotificationService>();

// 注册新的优化服务（基础服务）
builder.Services.AddSingleton<FFmpegConfigurationService>();
builder.Services.AddScoped<NotificationService>();

// 注册自定义服务
builder.Services.AddScoped<DatabaseService>(); // 改为 Scoped 以支持 NotificationService 依赖
builder.Services.AddSingleton<FileService>();
builder.Services.AddSingleton<LoggingService>();
builder.Services.AddSingleton<GpuDetectionService>();
builder.Services.AddSingleton<FFmpegFormatDetectionService>();
builder.Services.AddScoped<GpuDeviceInfoService>();
builder.Services.AddScoped<VideoConversionService>();
builder.Services.AddScoped<ConversionTaskService>();
builder.Services.AddScoped<DiskSpaceService>(); // 磁盘空间管理服务
builder.Services.AddScoped<SpaceEstimationService>(); // 空间预估服务
builder.Services.AddScoped<BatchTaskSpaceControlService>(); // 批量任务空间控制服务
builder.Services.AddScoped<AdvancedFileCleanupService>(); // 高级文件清理服务
builder.Services.AddScoped<DownloadTrackingService>(); // 下载跟踪服务

// 注册后台服务（同时注册为单例以便从Hub访问）
builder.Services.AddSingleton<ConversionQueueService>();
builder.Services.AddHostedService<ConversionQueueService>(provider => provider.GetRequiredService<ConversionQueueService>());
builder.Services.AddHostedService<FileCleanupService>();

// 配置文件上传大小限制
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = builder.Configuration.GetValue<long>("VideoConversion:MaxFileSize", 32212254720);
});

// 配置Kestrel服务器选项 - 支持大文件上传
builder.Services.Configure<KestrelServerOptions>(options =>
{
    var maxFileSize = builder.Configuration.GetValue<long>("VideoConversion:MaxFileSize", 32212254720);
    var timeoutMinutes = builder.Configuration.GetValue<int>("VideoConversion:UploadTimeoutMinutes", 120); // 增加到2小时

    options.Limits.MaxRequestBodySize = maxFileSize;
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(timeoutMinutes);
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(timeoutMinutes);
    options.Limits.MaxRequestLineSize = 8192;
    options.Limits.MaxRequestHeadersTotalSize = 32768;

    // 增加连接超时设置
    options.Limits.MaxConcurrentConnections = 100;
    options.Limits.MaxConcurrentUpgradedConnections = 100;

    // 设置更长的请求体读取超时
    options.Limits.MinRequestBodyDataRate = new MinDataRate(bytesPerSecond: 240, gracePeriod: TimeSpan.FromSeconds(5));
    options.Limits.MinResponseDataRate = new MinDataRate(bytesPerSecond: 240, gracePeriod: TimeSpan.FromSeconds(5));
});

// 配置表单选项 - 优化大文件处理
builder.Services.Configure<FormOptions>(options =>
{
    var maxFileSize = builder.Configuration.GetValue<long>("VideoConversion:MaxFileSize", 32212254720);

    options.MultipartBodyLengthLimit = maxFileSize;
    options.ValueLengthLimit = int.MaxValue;
    options.ValueCountLimit = int.MaxValue;
    options.KeyLengthLimit = int.MaxValue;
    options.MemoryBufferThreshold = 1024 * 1024; // 1MB缓冲，避免大文件全部加载到内存
    options.MultipartHeadersLengthLimit = 16384;
    options.MultipartBoundaryLengthLimit = 128;
});

var app = builder.Build();

// 添加请求日志中间件 - 记录所有HTTP请求
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    var requestId = Guid.NewGuid().ToString("N")[..8];

    logger.LogInformation("[{RequestId}] 🌐 === HTTP请求开始 ===", requestId);
    logger.LogInformation("[{RequestId}] 方法: {Method}", requestId, context.Request.Method);
    logger.LogInformation("[{RequestId}] 路径: {Path}", requestId, context.Request.Path);
    logger.LogInformation("[{RequestId}] 查询: {Query}", requestId, context.Request.QueryString);
    logger.LogInformation("[{RequestId}] 客户端IP: {ClientIP}", requestId, context.Connection.RemoteIpAddress);
    logger.LogInformation("[{RequestId}] User-Agent: {UserAgent}", requestId, context.Request.Headers.UserAgent.ToString());
    logger.LogInformation("[{RequestId}] Content-Type: {ContentType}", requestId, context.Request.ContentType ?? "null");
    logger.LogInformation("[{RequestId}] Content-Length: {ContentLength}", requestId, context.Request.ContentLength?.ToString() ?? "null");

    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

    try
    {
        await next();
        stopwatch.Stop();

        logger.LogInformation("[{RequestId}] ✅ 请求完成: {StatusCode} ({ElapsedMs}ms)",
            requestId, context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
    }
    catch (Exception ex)
    {
        stopwatch.Stop();
        logger.LogError(ex, "[{RequestId}] ❌ 请求异常: ({ElapsedMs}ms)",
            requestId, stopwatch.ElapsedMilliseconds);
        throw;
    }

    logger.LogInformation("[{RequestId}] 🌐 === HTTP请求结束 ===", requestId);
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

// 使用全局异常处理中间件
app.UseGlobalExceptionHandling();

app.UseHttpsRedirection();
app.UseStaticFiles();

// 启用WebSocket支持
app.UseWebSockets();

// 使用WebSocket中间件
app.UseWebSocketMiddleware();

// 添加请求日志中间件
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("🌐 收到请求: {Method} {Path} from {RemoteIP}",
        context.Request.Method,
        context.Request.Path,
        context.Connection.RemoteIpAddress);

    if (context.Request.Path.StartsWithSegments("/api/upload"))
    {
        logger.LogInformation("🚨 上传相关请求: {Method} {Path} {QueryString}",
            context.Request.Method,
            context.Request.Path,
            context.Request.QueryString);
        logger.LogInformation("🚨 Content-Type: {ContentType}", context.Request.ContentType);
        logger.LogInformation("🚨 Content-Length: {ContentLength}", context.Request.ContentLength);
    }

    await next();
});

app.UseRouting();

// 启用CORS
app.UseCors("AllowAll");

app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

// 映射SignalR Hub
app.MapHub<ConversionHub>("/conversionHub");

// 添加启动日志
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("🚀 === VideoConversion服务启动 ===");
logger.LogInformation("🌐 监听地址: {Urls}", string.Join(", ", app.Urls));
logger.LogInformation("🔧 环境: {Environment}", app.Environment.EnvironmentName);
logger.LogInformation("📁 内容根目录: {ContentRoot}", app.Environment.ContentRootPath);
logger.LogInformation("⚙️ 配置信息:");
logger.LogInformation("   - 最大文件大小: {MaxFileSize} bytes", builder.Configuration.GetValue<long>("VideoConversion:MaxFileSize", 32212254720));
logger.LogInformation("   - 上传路径: {UploadsPath}", builder.Configuration.GetValue<string>("VideoConversion:UploadsPath", "uploads"));
logger.LogInformation("   - 分片大小: {ChunkSize} bytes", builder.Configuration.GetValue<int>("VideoConversion:ChunkSize", 2 * 1024 * 1024));
// 初始化下载跟踪数据库
using (var scope = app.Services.CreateScope())
{
    try
    {
        // 确保主数据库已经初始化
        var databaseService = scope.ServiceProvider.GetRequiredService<DatabaseService>();
        var db = databaseService.GetDatabaseAsync();

        // 测试数据库连接
        await db.Ado.GetDataTableAsync("SELECT 1");

        var downloadTrackingService = scope.ServiceProvider.GetRequiredService<DownloadTrackingService>();
        await downloadTrackingService.InitializeDatabaseAsync();
        logger.LogInformation("📊 下载跟踪数据库初始化完成");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ 下载跟踪数据库初始化失败");
        // 不抛出异常，允许应用继续启动
    }
}

logger.LogInformation("✅ 服务启动完成，等待客户端连接...");

app.Run();
