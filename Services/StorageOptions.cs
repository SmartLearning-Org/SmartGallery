namespace SmartGallery.Services
{
    public class StorageOptions
    {
        public bool UseManagedIdentity { get; set; } = false;
        public string? AccountName { get; set; }
        public string? ConnectionString { get; set; }
        public string ContainerName { get; set; } = "gallery";
    }
}