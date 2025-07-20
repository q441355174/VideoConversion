# VideoConversion Docker 配置文件
# 基于 .NET 8.0 的视频转换应用程序

# 基础运行时镜像
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# 安装 FFmpeg 和必要的依赖
RUN apt-get update && apt-get install -y \
    ffmpeg \
    wget \
    curl \
    mediainfo \
    && rm -rf /var/lib/apt/lists/*

# 验证 FFmpeg 安装
RUN ffmpeg -version


# 此阶段用于生成服务项目
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["VideoConversion.csproj", "."]
RUN dotnet restore "./VideoConversion.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "./VideoConversion.csproj" -c $BUILD_CONFIGURATION -o /app/build

# 此阶段用于发布要复制到最终阶段的服务项目
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./VideoConversion.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# 最终运行时镜像
FROM base AS final
WORKDIR /app

# 复制发布的应用程序
COPY --from=publish /app/publish .

# 创建必要的目录
RUN mkdir -p /app/uploads /app/outputs /app/temp_uploads /app/logs

# 设置目录权限
RUN chmod 755 /app/uploads /app/outputs /app/temp_uploads /app/logs

# 创建非root用户（注释掉，因为基础镜像已经处理了用户）
# RUN groupadd -r appuser && useradd -r -g appuser appuser
# RUN chown -R appuser:appuser /app
# USER appuser

# 设置环境变量
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true

# 健康检查
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:8080/api/health || exit 1

# 启动应用程序
ENTRYPOINT ["dotnet", "VideoConversion.dll"]