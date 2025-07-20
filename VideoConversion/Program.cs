using VideoConversion.Services;
using VideoConversion.Hubs;
using VideoConversion.Middleware;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Http.Features;
using Serilog;

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
builder.Services.AddControllers();

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

// 注册新的优化服务（基础服务）
builder.Services.AddSingleton<FFmpegConfigurationService>();
builder.Services.AddScoped<NotificationService>();

// 注册自定义服务
builder.Services.AddScoped<DatabaseService>(); // 改为 Scoped 以支持 NotificationService 依赖
builder.Services.AddSingleton<FileService>();
builder.Services.AddSingleton<LoggingService>();
builder.Services.AddSingleton<GpuDetectionService>();
builder.Services.AddScoped<GpuPerformanceService>();
builder.Services.AddScoped<GpuDeviceInfoService>();
builder.Services.AddScoped<VideoConversionService>();
builder.Services.AddScoped<ConversionTaskService>();

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
    var timeoutMinutes = builder.Configuration.GetValue<int>("VideoConversion:UploadTimeoutMinutes", 60);

    options.Limits.MaxRequestBodySize = maxFileSize;
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(timeoutMinutes);
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(timeoutMinutes);
    options.Limits.MaxRequestLineSize = 8192;
    options.Limits.MaxRequestHeadersTotalSize = 32768;
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

app.UseRouting();

// 启用CORS
app.UseCors("AllowAll");

app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

// 映射SignalR Hub
app.MapHub<ConversionHub>("/conversionHub");

app.Run();
