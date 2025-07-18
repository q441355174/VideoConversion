# 测试GPU参数构建脚本
Write-Host "🧪 测试GPU参数构建..." -ForegroundColor Green

# 模拟一个GPU转换任务的参数
$VideoCodec = "h264_nvenc"
$QualityMode = "CRF"
$VideoQuality = "23"
$OutputFormat = "mp4"
$InputFile = "test.mkv"
$OutputFile = "test_output.mp4"

Write-Host "`n📋 测试任务参数:" -ForegroundColor Yellow
Write-Host "  VideoCodec: $VideoCodec" -ForegroundColor Cyan
Write-Host "  QualityMode: $QualityMode" -ForegroundColor Cyan
Write-Host "  VideoQuality: $VideoQuality" -ForegroundColor Cyan

# 根据代码逻辑构建预期的FFmpeg参数
Write-Host "`n🔧 预期的GPU参数:" -ForegroundColor Yellow

# 硬件加速参数
Write-Host "  硬件加速参数:" -ForegroundColor Cyan
Write-Host "    -hwaccel cuda" -ForegroundColor Green
Write-Host "    -hwaccel_output_format cuda" -ForegroundColor Green
Write-Host "    -extra_hw_frames 3" -ForegroundColor Green

# 编码器特定参数
Write-Host "  NVENC编码器参数:" -ForegroundColor Cyan
Write-Host "    -c:v h264_nvenc" -ForegroundColor Green
Write-Host "    -preset p4" -ForegroundColor Green
Write-Host "    -profile:v high" -ForegroundColor Green
Write-Host "    -level 4.1" -ForegroundColor Green
Write-Host "    -rc constqp" -ForegroundColor Green
Write-Host "    -cq 23" -ForegroundColor Green
Write-Host "    -spatial_aq 1" -ForegroundColor Green
Write-Host "    -temporal_aq 1" -ForegroundColor Green
Write-Host "    -rc-lookahead 20" -ForegroundColor Green
Write-Host "    -surfaces 32" -ForegroundColor Green
Write-Host "    -bf 3" -ForegroundColor Green

# 完整的预期命令
Write-Host "`n🎯 完整的预期FFmpeg命令:" -ForegroundColor Yellow
$expectedCommand = @"
ffmpeg.exe 
-hwaccel cuda 
-hwaccel_output_format cuda 
-extra_hw_frames 3 
-i "test.mkv" 
-c:v h264_nvenc 
-preset p4 
-profile:v high 
-level 4.1 
-rc constqp 
-cq 23 
-spatial_aq 1 
-temporal_aq 1 
-rc-lookahead 20 
-surfaces 32 
-bf 3 
-c:a aac 
-b:a 128k 
-f mp4 
"test_output.mp4"
"@

Write-Host $expectedCommand -ForegroundColor Green

Write-Host "`n💡 关键检查点:" -ForegroundColor Yellow
Write-Host "  1. 是否包含 -hwaccel cuda" -ForegroundColor Cyan
Write-Host "  2. 是否包含 -c:v h264_nvenc" -ForegroundColor Cyan
Write-Host "  3. 是否包含 NVENC 优化参数" -ForegroundColor Cyan
Write-Host "  4. 是否正确设置 CRF 质量参数" -ForegroundColor Cyan

Write-Host "`n🔍 如果实际命令与预期不符，可能的原因:" -ForegroundColor Yellow
Write-Host "  ❌ GPU检测失败，回退到CPU编码" -ForegroundColor Red
Write-Host "  ❌ 预设配置错误，VideoCodec不是h264_nvenc" -ForegroundColor Red
Write-Host "  ❌ BuildFFmpegArguments方法逻辑错误" -ForegroundColor Red
Write-Host "  ❌ 硬件加速参数构建失败" -ForegroundColor Red

Write-Host "`n✅ 测试完成！请启动一个实际的转换任务来验证。" -ForegroundColor Green
