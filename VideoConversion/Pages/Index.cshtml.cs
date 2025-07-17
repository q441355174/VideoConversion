using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VideoConversion.Models;
using VideoConversion.Services;

namespace VideoConversion.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly FileService _fileService;

        public IndexModel(ILogger<IndexModel> logger, FileService fileService)
        {
            _logger = logger;
            _fileService = fileService;
        }

        public List<ConversionPreset> ConversionPresets { get; set; } = new();
        public string[] SupportedExtensions { get; set; } = Array.Empty<string>();
        public string MaxFileSizeFormatted { get; set; } = string.Empty;
        public long MaxFileSize { get; set; }

        public void OnGet()
        {
            // 获取转换预设
            ConversionPresets = ConversionPreset.GetAllPresets();

            // 获取支持的文件扩展名
            SupportedExtensions = _fileService.GetSupportedExtensions();

            // 获取最大文件大小
            MaxFileSize = _fileService.GetMaxFileSize();

            // 格式化最大文件大小
            MaxFileSizeFormatted = FileService.FormatFileSize(MaxFileSize);
        }
    }
}
