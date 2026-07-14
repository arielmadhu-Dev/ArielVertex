namespace ArielVertex.Application.Abstractions;

public record StoredFile(string StorageKey, string OriginalName, string ContentType, long SizeBytes);

/// <summary>
/// Private file storage boundary (spec 6.4 / §9 file security). Files live outside the web root
/// and are only ever reached through authorized download endpoints — the storage key is never
/// returned to clients. Swap LocalFileStorage for Azure Blob / S3 in production without touching
/// callers.
/// </summary>
public interface IFileStorage
{
    /// <summary>Validates and persists an uploaded file; returns its opaque storage key + metadata.</summary>
    Task<StoredFile> SaveAsync(Stream content, string originalFileName, string? contentType, CancellationToken ct = default);

    /// <summary>Opens a stored file for streaming, or null if the key is unknown/invalid.</summary>
    Task<(Stream stream, string contentType, string fileName)?> OpenAsync(string storageKey, string fileName, CancellationToken ct = default);

    void Delete(string storageKey);
}

/// <summary>Thrown when an upload fails validation (extension / size / empty).</summary>
public class FileValidationException : Exception
{
    public FileValidationException(string message) : base(message) { }
}
