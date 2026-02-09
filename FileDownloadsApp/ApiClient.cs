using System.Net.Http.Json;

namespace FileDownloadApp;

public class ApiClient
{
    private readonly HttpClient _httpClient;

    public ApiClient(string baseUrl, string token)
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"JWT {token}");
    }

    public async Task<AssemblyGroupResponse?> GetAssemblyGroupAsync(string guid)
    {
        Console.WriteLine($"Fetching assembly group details for {guid}...");
        try
        {
            return await _httpClient.GetFromJsonAsync<AssemblyGroupResponse>($"api/public/assemblyGroups/{guid}");
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Error fetching assembly group: {ex.Message}");
            return null;
        }
    }

    public async Task DownloadFileAsync(string fileType, DownloadRequest request, string outputDirectory)
    {
        Console.WriteLine($"\nRequesting {fileType} download...");
        var endpoint = $"api/public/componentDesigns/download/{fileType}";
        
        try
        {
             var response = await _httpClient.PostAsJsonAsync(endpoint, request);

             if (response.IsSuccessStatusCode)
             {
                 var filename = response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? $"download_{DateTime.Now.Ticks}.{fileType}";
                 var filePath = Path.Combine(outputDirectory, filename);
                 
                 Console.WriteLine($"Downloading to {filePath}...");
                 using var stream = await response.Content.ReadAsStreamAsync();
                 using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                 await stream.CopyToAsync(fileStream);
                 Console.WriteLine($"Successfully downloaded {filename}");
             }
             else
             {
                 var errorContent = await response.Content.ReadAsStringAsync();
                 Console.WriteLine($"Failed to download {fileType}. Status: {response.StatusCode}. Details: {errorContent}");
             }
        }
        catch (Exception ex)
        {
             Console.WriteLine($"Exception during {fileType} download: {ex.Message}");
        }
    }
}
