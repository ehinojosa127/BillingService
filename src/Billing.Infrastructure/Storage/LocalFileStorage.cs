using Billing.Application.Abstractions;
using Billing.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Billing.Infrastructure.Storage;

public sealed class LocalFileStorage(IOptions<StorageOptions> options) : IFileStorage
{
    public async Task<StoredFile> SaveAsync(string key, string fileName, string contentType, byte[] content, CancellationToken cancellationToken)
    {
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, content, cancellationToken);
        return new StoredFile(key, fileName, contentType, content);
    }

    public async Task<StoredFile?> GetAsync(string key, CancellationToken cancellationToken)
    {
        var path = Resolve(key);
        if (!File.Exists(path))
        {
            return null;
        }

        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        return new StoredFile(key, Path.GetFileName(path), GuessContentType(path), bytes);
    }

    private string Resolve(string key)
    {
        var safe = key.Replace('\\', '/').TrimStart('/');
        return Path.GetFullPath(Path.Combine(options.Value.Root, safe));
    }

    private static string GuessContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".pdf" => "application/pdf",
        ".zip" => "application/zip",
        _ => "application/octet-stream"
    };
}
