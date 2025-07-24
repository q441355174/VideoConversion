using Microsoft.AspNetCore.Mvc.RazorPages;

namespace VideoConversion.Pages
{
    public class HistoryModel : PageModel
    {
        private readonly ILogger<HistoryModel> _logger;

        public HistoryModel(ILogger<HistoryModel> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
            // 页面逻辑主要在前端JavaScript中处理 
        }
    }
}
