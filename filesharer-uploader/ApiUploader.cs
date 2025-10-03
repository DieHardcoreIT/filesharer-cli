using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace filesharer_uploader;

public class ApiUploader
{
    private readonly ApiSettings _apiSettings;
    private readonly HttpClient _httpClient;
    private readonly UploadSettings _uploadSettings;

    public ApiUploader(HttpClient httpClient, ApiSettings apiSettings, UploadSettings uploadSettings)
    {
        _httpClient = httpClient;
        _apiSettings = apiSettings;
        _uploadSettings = uploadSettings;
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiSettings.ApiKey);
        
        // Set a timeout of 2 hours for big files
        _httpClient.Timeout = TimeSpan.FromHours(2); 
    }

    public async Task UploadFileAsync(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        Console.WriteLine($"Preparing: {fileInfo.Name}");
        Console.WriteLine($"Size: {fileInfo.Length / (1024.0 * 1024.0):F2} MB");

        // 1. Generate Hash of the file (we do this so the server can check if the file was successfully uploaded)
        Console.WriteLine("Calculate hash from file...");
        var fileHash = await CalculateSha256Async(filePath);
        Console.WriteLine($"Hash: {fileHash}");

        // 2. Initiate upload session with server
        // In this process the server will generate a unique upload ID and return the chunk size
        Console.WriteLine("\nInitialize upload session...");
        var initiateRequest = new { fileName = fileInfo.Name, fileSize = fileInfo.Length, fileHash, expiry = "1d" };
        var initiateContent =
            new StringContent(JsonSerializer.Serialize(initiateRequest), Encoding.UTF8, "application/json");
        var initiateResponse = await _httpClient.PostAsync($"{_apiSettings.BaseUrl}/api/v1/upload/initiate", initiateContent);
        initiateResponse.EnsureSuccessStatusCode();

        var initiateResult =
            JsonSerializer.Deserialize<JsonElement>(await initiateResponse.Content.ReadAsStringAsync());
        var uploadId = initiateResult.GetProperty("uploadId").GetString()!;
        var chunkSize = initiateResult.GetProperty("chunkSize").GetInt32();
        var totalChunks = (int)Math.Ceiling((double)fileInfo.Length / chunkSize);
        Console.WriteLine($"Session started. Upload ID: {uploadId}, Chunks: {totalChunks}");

        // 3. Upload chunks in one go
        Console.WriteLine(
            $"\nUpload {totalChunks} chunks with {_uploadSettings.ConcurrentUploads} simultaneous streams...");
        var stopwatch = Stopwatch.StartNew();
        var uploadedChunks = 0;

        var chunkNumbers = Enumerable.Range(1, totalChunks);
        await Parallel.ForEachAsync(chunkNumbers,
            new ParallelOptions { MaxDegreeOfParallelism = _uploadSettings.ConcurrentUploads },
            async (chunkNumber, cancellationToken) =>
            {
                await UploadChunkAsync(filePath, uploadId, chunkNumber, chunkSize);
                var progress = Interlocked.Increment(ref uploadedChunks);
                var percentage = (double)progress / totalChunks * 100;
                Console.WriteLine($"Chunk {progress}/{totalChunks} ({percentage:F2}%) uploaded.");
            });

        stopwatch.Stop();
        Console.WriteLine($"\nAll chunks uploaded in {stopwatch.Elapsed.TotalSeconds:F2} seconds.");

        // 4. Finalize upload
        // In this process the server will check if all chunks were uploaded successfully and if so, it will return the download link (and other metadata)
        Console.WriteLine("Finalize upload...");
        var finalizeRequest = new { uploadId, totalChunks };
        var finalizeContent =
            new StringContent(JsonSerializer.Serialize(finalizeRequest), Encoding.UTF8, "application/json");
        var finalizeResponse = await _httpClient.PostAsync($"{_apiSettings.BaseUrl}/api/v1/upload/finalize", finalizeContent);

        if (!finalizeResponse.IsSuccessStatusCode)
        {
            var error = await finalizeResponse.Content.ReadAsStringAsync();
            throw new Exception($"Finalization failed: {error}");
        }

        var finalizeResult =
            JsonSerializer.Deserialize<JsonElement>(await finalizeResponse.Content.ReadAsStringAsync());
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n--- UPLOAD SUCCESSFUL ---");
        Console.WriteLine($"File name: {finalizeResult.GetProperty("fileName").GetString()}");
        Console.WriteLine($"Download link: {finalizeResult.GetProperty("link").GetString()}");
        Console.WriteLine($"Will be deleted on: {finalizeResult.GetProperty("deleteDate").GetString()}");
        Console.WriteLine("--------------------------");
        Console.ResetColor();
    }

    private async Task UploadChunkAsync(string filePath, string uploadId, int chunkNumber, int chunkSize)
    {
        await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var buffer = new byte[chunkSize];
        fileStream.Position = (long)(chunkNumber - 1) * chunkSize;
        var bytesRead = await fileStream.ReadAsync(buffer.AsMemory(0, chunkSize));

        var chunkContent = new ByteArrayContent(buffer, 0, bytesRead);
        using var formData = new MultipartFormDataContent();
        formData.Add(new StringContent(uploadId), "uploadId");
        formData.Add(new StringContent(chunkNumber.ToString()), "chunkNumber");
        formData.Add(chunkContent, "chunk", "chunk");

        var response = await _httpClient.PostAsync($"{_apiSettings.BaseUrl}/api/v1/upload/chunk", formData);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<string> CalculateSha256Async(string filePath)
    {
        using var sha256 = SHA256.Create();
        await using var fileStream = File.OpenRead(filePath);
        var hash = await sha256.ComputeHashAsync(fileStream);
        return Convert.ToHexStringLower(hash);
    }
}