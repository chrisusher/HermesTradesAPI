using System.Globalization;
using System.Text;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Shared;
using Shared.Interfaces;

namespace Services.Azure;

public class BlobStorageService : IStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private BlobContainerClient? _blobContainerClient;

    public BlobStorageService(BlobServiceClient blobServiceClient)
    {
        _blobServiceClient = blobServiceClient;
    }

    public string FolderName { get; set; } = string.Empty;

    private BlobContainerClient BlobContainerClient
    {
        get
        {
            if (_blobContainerClient is null)
            {
                _blobContainerClient = _blobServiceClient.GetBlobContainerClient(FolderName);
            }
            return _blobContainerClient;
        }
    }

    public void Dispose()
    {
        _blobContainerClient = null;
    }

    public async Task<bool> FileExistsAsync(string path)
    {
        var containerClient = BlobContainerClient;

        var blobClient = containerClient.GetBlobClient(path);

        return await blobClient.ExistsAsync();
    }

    public async Task<(bool, string?)> FileExistsAsync(string prefix, DateTime date)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            throw new ArgumentException("Path cannot be null or empty.", nameof(prefix));
        }

        if (prefix.EndsWith('/'))
        {
            prefix = prefix.TrimEnd('/');
        }

        var latestFile = await GetLatestFileAsync(prefix, date);

        if (latestFile is null)
        {
            return (false, null);
        }

        return (true, latestFile.Name);
    }

    public async Task<List<string>> GetFilesAsync(string path)
    {
        var files = new List<string>();
        var updatedPath = path.EndsWith("/") ? path : $"{path}/";

        var blobs = BlobContainerClient.GetBlobsAsync(prefix: updatedPath);

        await foreach (var blob in blobs)
        {
            files.Add(blob.Name);
        }

        return files;
    }

    public async Task<T?> ReadFileAsync<T>(string path)
    {
        if (!await FileExistsAsync(path))
        {
            return default;
        }

        var blobClient = BlobContainerClient.GetBlobClient(path);
        using var stream = await blobClient.OpenReadAsync();

        return await JsonSerializer.DeserializeAsync<T>(stream, SharedCommon.JsonOptions);
    }

    public async Task<T?> ReadLatestFileAsync<T>(string folderPath, DateTime minDate)
    {
        var latestFiles = await GetLatestFileAsync(folderPath, minDate);

        if (latestFiles is null)
        {
            return default;
        }

        return await ReadFileAsync<T>(latestFiles.Name);
    }

    public async Task<T?> ReadLatestFileAsync<T>(string folderPath)
    {
        var latestFiles = await GetLatestFileAsync(folderPath);

        if (latestFiles is null)
        {
            return default;
        }

        return await ReadFileAsync<T>(latestFiles.Name);
    }

    public async Task SaveFileAsync<T>(T data, string path)
    {
        var containerClient = BlobContainerClient;

        var blobClient = containerClient.GetBlobClient(path);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data, SharedCommon.JsonOptions)));

        await blobClient.UploadAsync(stream, true);
    }

    private async Task<List<BlobItem>?> GetLatestFilesAsync(string folderPath, DateTime minDate)
    {
        var files = new List<BlobItem>();
        var blobs = BlobContainerClient.GetBlobsAsync(prefix: $"{folderPath}/{minDate:yyyy-MM-dd}/");

        await foreach (var blob in blobs)
        {
            files.Add(blob);
        }

        var latestFiles = files
            .Where(x =>
            {
                var datePart = x.Name.Split("/").Last();
                var fileName = Path.GetFileNameWithoutExtension(datePart);

                return DateTime.ParseExact(fileName, "yyyy-MM-dd-HH-mm-ss", CultureInfo.InvariantCulture) >= minDate;
            })
            .OrderByDescending(x =>
            {
                var datePart = x.Name.Split("/").Last();
                var fileName = Path.GetFileNameWithoutExtension(datePart);

                return DateTime.ParseExact(fileName, "yyyy-MM-dd-HH-mm-ss", CultureInfo.InvariantCulture);
            })
            .ToList();

        return latestFiles;
    }

    private async Task<BlobItem?> GetLatestFileAsync(string folderPath, DateTime minDate)
    {
        var files = new List<BlobItem>();
        var blobs = BlobContainerClient.GetBlobsAsync(prefix: $"{folderPath}/{minDate:yyyy-MM-dd}/");

        await foreach (var blob in blobs)
        {
            files.Add(blob);
        }

        var latestFile = files
            .Where(x =>
            {
                var datePart = x.Name.Split("/").Last();
                var fileName = Path.GetFileNameWithoutExtension(datePart);

                return DateTime.ParseExact(fileName, "yyyy-MM-dd-HH-mm-ss", CultureInfo.InvariantCulture) >= minDate;
            })
            .OrderByDescending(x =>
            {
                var datePart = x.Name.Split("/").Last();
                var fileName = Path.GetFileNameWithoutExtension(datePart);

                return DateTime.ParseExact(fileName, "yyyy-MM-dd-HH-mm-ss", CultureInfo.InvariantCulture);
            })
            .FirstOrDefault();

        return latestFile;
    }

    private async Task<BlobItem?> GetLatestFileAsync(string folderPath)
    {
        var files = new List<BlobItem>();
        var blobs = BlobContainerClient.GetBlobsAsync(prefix: $"{folderPath}/");

        await foreach (var blob in blobs)
        {
            files.Add(blob);
        }

        var latestFile = files
            .OrderByDescending(x =>
            {
                // Get Last Modified date
                return x.Properties.LastModified ?? DateTimeOffset.MinValue;
            })
            .FirstOrDefault();

        return latestFile;
    }
}
