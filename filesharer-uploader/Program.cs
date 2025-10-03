using Microsoft.Extensions.Configuration;

namespace filesharer_uploader;

public static class Program
{
    private static readonly HttpClient HttpClient = new();

    public static async Task Main(string[] args)
    {
        // Load the configuration from appsettings.json
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", false)
            .Build();

        // Get the API settings and upload settings from the configuration
        var apiSettings = config.GetSection("ApiSettings").Get<ApiSettings>();
        var uploadSettings = config.GetSection("UploadSettings").Get<UploadSettings>();

        if (apiSettings == null || uploadSettings == null || string.IsNullOrEmpty(apiSettings.ApiKey))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(
                "Error: Configuration (ApiSettings/UploadSettings) in appsettings.json is missing or invalid.");
            Console.ReadKey();
            Console.ResetColor();
            return;
        }
        
        // Fix user input errors
        if (apiSettings.BaseUrl.EndsWith("/"))
        {
            apiSettings.BaseUrl = apiSettings.BaseUrl.TrimEnd('/');
        }

        // Check whether a file path was passed as an argument
        if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
        {
            Console.WriteLine("filesharer-uploader.exe \"C:\\Path\\to\\File.zip\"");
            Console.ReadKey();
            return;
        }

        var filePath = args[0];
        if (!File.Exists(filePath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: The file was not found: {filePath}");
            Console.ReadKey();
            Console.ResetColor();
            return;
        }

        try
        {
            // Initialize the uploader and upload the file
            var uploader = new ApiUploader(HttpClient, apiSettings, uploadSettings);
            await uploader.UploadFileAsync(filePath);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nAn unexpected error has occurred: {ex.Message}");
        }
        finally
        {
            Console.ResetColor();
            Console.WriteLine("\nPress any key to close the window.");
            Console.ReadKey();
        }
    }
}