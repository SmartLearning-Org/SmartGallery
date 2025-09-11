namespace SmartGallery.Models
{
    public class ImageItem
    {
        public required string Id { get; set; }
        public required string Description { get; set; }
        public required string ImageUrl { get; set; }
        public DateTimeOffset UploadedAt { get; set; }
    }
}