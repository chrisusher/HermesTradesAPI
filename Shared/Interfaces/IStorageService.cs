namespace Shared.Interfaces;

public interface IStorageService : IDisposable
{
    public string FolderName { get; set; }

    public Task<bool> FileExistsAsync(string path);

    /// <summary>
    /// Checks if a file exists based on a prefix and a specific date.
    /// </summary>
    /// <param name="prefix">The prefix to search for in the file name.</param>
    /// <param name="date">The date associated with the file.</param>
    /// <returns>A tuple containing a boolean indicating existence and an optional string with the file path.</returns>
    public Task<(bool, string?)> FileExistsAsync(string prefix, DateTime date);

    public Task<List<string>> GetFilesAsync(string path);

    public Task<T?> ReadFileAsync<T>(string path);

    public Task<T?> ReadLatestFileAsync<T>(string folderPath, DateTime quoteDate);

    public Task<T?> ReadLatestFileAsync<T>(string folderPath);

    public Task SaveFileAsync<T>(T data, string path);
}
