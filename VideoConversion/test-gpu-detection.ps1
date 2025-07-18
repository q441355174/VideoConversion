# GPU检测测试脚本
# 基于博客最佳实践的改进测试

Write-Host "🔍 开始GPU检测测试..." -ForegroundColor Green

# 1. 测试FFmpeg是否存在
Write-Host "`n📁 检查FFmpeg文件..." -ForegroundColor Yellow
$ffmpegPath = ".\ffmpeg\ffmpeg.exe"
if (Test-Path $ffmpegPath) {
    Write-Host "✅ FFmpeg文件存在: $ffmpegPath" -ForegroundColor Green
} else {
    Write-Host "❌ FFmpeg文件不存在: $ffmpegPath" -ForegroundColor Red
    exit 1
}

# 2. 测试FFmpeg版本
Write-Host "`n📋 检查FFmpeg版本..." -ForegroundColor Yellow
try {
    $versionOutput = & $ffmpegPath -version 2>&1
    $versionLine = ($versionOutput | Select-String "ffmpeg version").Line
    Write-Host "✅ $versionLine" -ForegroundColor Green
    
    # 检查编译选项中的GPU支持
    $configLine = ($versionOutput | Select-String "configuration:").Line
    if ($configLine -match "--enable-nvenc") {
        Write-Host "🎯 编译包含NVENC支持" -ForegroundColor Green
    }
    if ($configLine -match "--enable-cuda") {
        Write-Host "🎯 编译包含CUDA支持" -ForegroundColor Green
    }
    if ($configLine -match "--enable-qsv") {
        Write-Host "🎯 编译包含QSV支持" -ForegroundColor Green
    }
    if ($configLine -match "--enable-amf") {
        Write-Host "🎯 编译包含AMF支持" -ForegroundColor Green
    }
} catch {
    Write-Host "❌ 无法获取FFmpeg版本: $_" -ForegroundColor Red
}

# 3. 测试硬件加速方法
Write-Host "`n🔧 检查硬件加速方法..." -ForegroundColor Yellow
try {
    $hwaccelOutput = & $ffmpegPath -hwaccels 2>&1
    Write-Host "硬件加速方法:" -ForegroundColor Cyan
    $hwaccelOutput | ForEach-Object {
        if ($_ -match "^\s*\w+\s*$" -and $_ -notmatch "Hardware acceleration methods" -and $_ -notmatch "---") {
            $method = $_.Trim()
            if ($method) {
                Write-Host "  🟢 $method" -ForegroundColor Green
            }
        }
    }
} catch {
    Write-Host "❌ 无法获取硬件加速方法: $_" -ForegroundColor Red
}

# 4. 测试编码器列表
Write-Host "`n🎬 检查GPU编码器..." -ForegroundColor Yellow
try {
    $encoderOutput = & $ffmpegPath -encoders 2>&1
    $gpuEncoders = @()
    
    $encoderOutput | ForEach-Object {
        if ($_ -match "^\s*V[\.F][\.S][\.X][\.B][\.D]\s+(\S+)\s+(.+)$") {
            $encoderName = $matches[1]
            $description = $matches[2]
            
            if ($encoderName -match "(nvenc|qsv|amf|vaapi|cuda)") {
                $gpuEncoders += "$encoderName - $description"
            }
        }
    }
    
    if ($gpuEncoders.Count -gt 0) {
        Write-Host "发现GPU编码器:" -ForegroundColor Cyan
        $gpuEncoders | ForEach-Object {
            Write-Host "  🎯 $_" -ForegroundColor Green
        }
    } else {
        Write-Host "⚠️ 未发现GPU编码器" -ForegroundColor Yellow
    }
} catch {
    Write-Host "❌ 无法获取编码器列表: $_" -ForegroundColor Red
}

# 5. 测试NVIDIA GPU
Write-Host "`n🟢 测试NVIDIA NVENC..." -ForegroundColor Yellow
try {
    $testCmd = "$ffmpegPath -f lavfi -i testsrc=duration=2:size=320x240:rate=1 -c:v h264_nvenc -preset fast -b:v 100k -f null - -y"
    $testOutput = Invoke-Expression $testCmd 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ NVIDIA NVENC测试成功" -ForegroundColor Green
    } else {
        Write-Host "❌ NVIDIA NVENC测试失败" -ForegroundColor Red
        Write-Host "错误信息: $($testOutput | Select-String 'error|Error|ERROR' | Select-Object -First 3)" -ForegroundColor Red
    }
} catch {
    Write-Host "❌ NVIDIA NVENC测试异常: $_" -ForegroundColor Red
}

# 6. 调用API测试
Write-Host "`n🌐 测试GPU检测API..." -ForegroundColor Yellow
try {
    # 启动应用程序（如果未运行）
    $process = Get-Process -Name "VideoConversion" -ErrorAction SilentlyContinue
    if (-not $process) {
        Write-Host "⚠️ 应用程序未运行，请先启动应用程序" -ForegroundColor Yellow
    } else {
        # 测试API
        $apiUrl = "http://localhost:5065/api/gpu/refresh"
        $response = Invoke-RestMethod -Uri $apiUrl -Method POST -ErrorAction Stop
        
        if ($response.success) {
            Write-Host "✅ GPU检测API调用成功" -ForegroundColor Green
            Write-Host "检测结果: $($response.message)" -ForegroundColor Cyan
        } else {
            Write-Host "❌ GPU检测API调用失败: $($response.message)" -ForegroundColor Red
        }
        
        # 获取GPU能力信息
        $capabilitiesUrl = "http://localhost:5065/api/gpu/capabilities"
        $capabilities = Invoke-RestMethod -Uri $capabilitiesUrl -Method GET -ErrorAction Stop
        
        if ($capabilities.success) {
            Write-Host "GPU能力信息:" -ForegroundColor Cyan
            $caps = $capabilities.data
            Write-Host "  NVENC支持: $($caps.nvenc.supported)" -ForegroundColor $(if($caps.nvenc.supported) {"Green"} else {"Red"})
            Write-Host "  QSV支持: $($caps.qsv.supported)" -ForegroundColor $(if($caps.qsv.supported) {"Green"} else {"Red"})
            Write-Host "  AMF支持: $($caps.amf.supported)" -ForegroundColor $(if($caps.amf.supported) {"Green"} else {"Red"})
            Write-Host "  VAAPI支持: $($caps.vaapi.supported)" -ForegroundColor $(if($caps.vaapi.supported) {"Green"} else {"Red"})
        }
    }
} catch {
    Write-Host "❌ API测试失败: $_" -ForegroundColor Red
}

Write-Host "`n🎉 GPU检测测试完成！" -ForegroundColor Green
