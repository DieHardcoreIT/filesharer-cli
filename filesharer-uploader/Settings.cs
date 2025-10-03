namespace filesharer_uploader;

public class ApiSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}

public class UploadSettings
{
    public int ConcurrentUploads { get; set; } = 4;
}
