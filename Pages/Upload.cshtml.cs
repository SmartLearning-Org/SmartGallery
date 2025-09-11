using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartGallery.Services;

namespace SmartGallery.Pages
{
    public class UploadModel : PageModel
    {
        private readonly BlobImageService _images;
        public UploadModel(BlobImageService images) => _images = images;

        [BindProperty]
        public IFormFile? File { get; set; }

        [BindProperty]
        public string Description { get; set; } = string.Empty;

        [TempData]
        public string? Message { get; set; }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            if (File is null || File.Length == 0)
            {
                ModelState.AddModelError("File", "Vælg en billedfil.");
                return Page();
            }
            if (!BlobImageService.IsSupportedContentType(File.ContentType))
            {
                ModelState.AddModelError("File", "Kun billeder er tilladt (jpg, png, gif, webp).");
                return Page();
            }

            await using var stream = File.OpenReadStream();
            await _images.UploadAsync(stream, File.FileName, File.ContentType, Description ?? string.Empty);

            Message = "Billedet er uploadet.";
            return RedirectToPage("/Index");
        }
    }
}