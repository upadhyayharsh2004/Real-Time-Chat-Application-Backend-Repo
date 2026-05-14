namespace ConnectHub.Media.Services;

/// <summary>
/// AzureBlobOptions — strongly-typed config for Azure Blob Storage.
/// Injected via IOptions&lt;AzureBlobOptions&gt; into MediaService.
/// Bound from "AzureBlob" section in appsettings.json.
/// </summary>
public class AzureBlobOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string ContainerName { get; set; } = "connecthub-media";
    /// <summary>SAS token expiry in hours (default 1)</summary>
    public int SasExpiryHours { get; set; } = 1;
    /// <summary>Max file size in MB (default 100)</summary>
    public int MaxFileSizeMb { get; set; } = 100;
    /// <summary>Allowed content types</summary>
    public string[] AllowedContentTypes { get; set; } = new[]
    {
        "image/jpeg", "image/png", "image/gif", "image/webp",
        "video/mp4", "video/webm",
        "audio/mpeg", "audio/ogg", "audio/wav",
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "text/plain"
    };
}
