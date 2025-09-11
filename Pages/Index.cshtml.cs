using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartGallery.Models;
using SmartGallery.Services;

namespace SmartGallery.Pages
{
    public class IndexModel : PageModel
    {
        private readonly BlobImageService _images;
        public List<ImageItem> Items { get; private set; } = new();

        public IndexModel(BlobImageService images)
        {
            _images = images;
        }

        public async Task OnGetAsync()
        {
            Items = (await _images.ListAsync()).ToList();
        }
    }
}