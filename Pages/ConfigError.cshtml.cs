using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SmartGallery.Pages
{
    public class ConfigErrorModel : PageModel
    {
        public List<string> Errors { get; set; } = new();

        public void OnGet()
        {
            if (TempData["ConfigErrors"] is string errorsJson)
            {
                Errors = System.Text.Json.JsonSerializer.Deserialize<List<string>>(errorsJson) ?? new();
            }
        }
    }
}
