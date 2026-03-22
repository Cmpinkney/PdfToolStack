namespace PdfToolkit.API.Configuration
{
    public class AzureStorageOptions
    {
        public const string SectionName = "AzureStorage";
        public string ConnectionString { get; set; }
            = string.Empty;
        public string ContainerName { get; set; }
            = "pdf-outputs";
        public int BlobExpiryHours { get; set; } = 1;
    }
}
