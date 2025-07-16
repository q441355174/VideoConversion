using VideoConversion.Services;
using VideoConversion.Hubs;
using VideoConversion.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers();

// 添加SignalR
builder.Services.AddSignalR();

// 注册自定义服务
builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddSingleton<FileService>();
builder.Services.AddSingleton<LoggingService>();
builder.Services.AddScoped<VideoConversionService>();

// 注册后台服务
builder.Services.AddHostedService<ConversionQueueService>();
builder.Services.AddHostedService<FileCleanupService>();

// 配置文件上传大小限制
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = builder.Configuration.GetValue<long>("VideoConversion:MaxFileSize", 2147483648);
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
app.UseExceptionHandling();

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

// 映射SignalR Hub
app.MapHub<ConversionHub>("/conversionHub");

app.Run();
