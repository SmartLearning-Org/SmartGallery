using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartGallery.Models;
using SmartGallery.Services;

namespace SmartGallery.Pages
{
    public class IndexModel : PageModel
    {
        private readonly BlobImageService _images;
        public List<ImageItem> Items { get; private set; } = new();
        public string? ErrorMessage { get; set; }

        public IndexModel(BlobImageService images)
        {
            _images = images;
        }

        public async Task OnGetAsync()
        {
            try
            {
                Items = (await _images.ListAsync()).ToList();
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 403)
            {
                ErrorMessage = "Adgang nægtet til storage. Kontrollér at applikationen har de nødvendige rettigheder til at læse billeder.";
            }
            catch (Azure.RequestFailedException ex)
            {
                ErrorMessage = $"Kunne ikke hente billeder fra storage: {ex.Message}";
            }
            catch (System.Text.Json.JsonException ex)
            {
                ErrorMessage = $"Kunne ikke læse metadata for et eller flere billeder. Nogle billeder kan mangle: {ex.Message}";
            }
            catch (InvalidOperationException ex)
            {
                ErrorMessage = $"Fejl ved behandling af billeder: {ex.Message}";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Uventet fejl ved indlæsning af galleri: {ex.Message}";
            }
        }
    }
}