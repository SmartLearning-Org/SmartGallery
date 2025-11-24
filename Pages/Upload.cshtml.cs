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
        public new IFormFile? File { get; set; }

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

            try
            {
                await using var stream = File.OpenReadStream();
                await _images.UploadAsync(stream, File.FileName, File.ContentType, Description ?? string.Empty);

                Message = "Billedet er uploadet.";
                return RedirectToPage("/Index");
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return Page();
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 403)
            {
                ModelState.AddModelError("", "Adgang nægtet til storage. Kontrollér at applikationen har de nødvendige rettigheder.");
                return Page();
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 413 || ex.ErrorCode == "RequestBodyTooLarge")
            {
                ModelState.AddModelError("File", "Filen er for stor. Maksimal størrelse er 25MB.");
                return Page();
            }
            catch (Azure.RequestFailedException ex) when (ex.ErrorCode == "InsufficientAccountPermissions" || ex.ErrorCode == "AccountStorageQuotaExceeded")
            {
                ModelState.AddModelError("", "Storage kontoen har ikke mere plads. Kontakt administrator.");
                return Page();
            }
            catch (Azure.RequestFailedException ex)
            {
                ModelState.AddModelError("", $"Azure Storage fejl: {ex.Message}");
                return Page();
            }
            catch (IOException ex)
            {
                ModelState.AddModelError("File", $"Kunne ikke læse filen: {ex.Message}");
                return Page();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Uventet fejl ved upload: {ex.Message}");
                return Page();
            }
        }
    }
}