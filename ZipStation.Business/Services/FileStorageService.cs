using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using ZipStation.Business.Helpers;
using ZipStation.Models.Entities;

namespace ZipStation.Business.Services;

public interface IFileStorageService
{
    Task<string> UploadAsync(FileStorageSettings settings, string storageKey, Stream stream, string contentType);
    Task<Stream> DownloadAsync(FileStorageSettings settings, string storageKey);
    Task DeleteAsync(FileStorageSettings settings, string storageKey);
    string GeneratePresignedUrl(FileStorageSettings settings, string storageKey, TimeSpan expiry);
}

public class FileStorageService : IFileStorageService
{
    private readonly ILogger<FileStorageService> _logger;

    public FileStorageService(ILogger<FileStorageService> logger)
    {
        _logger = logger;
    }

    public async Task<string> UploadAsync(FileStorageSettings settings, string storageKey, Stream stream, string contentType)
    {
        using var client = CreateClient(settings);
        var request = new PutObjectRequest
        {
            BucketName = settings.BucketName,
            Key = storageKey,
            InputStream = stream,
            ContentType = contentType
        };
        await client.PutObjectAsync(request);
        _logger.LogInformation("Uploaded file {StorageKey} to bucket {Bucket}", storageKey, settings.BucketName);
        return storageKey;
    }

    public async Task<Stream> DownloadAsync(FileStorageSettings settings, string storageKey)
    {
        using var client = CreateClient(settings);
        var response = await client.GetObjectAsync(settings.BucketName, storageKey);
        var memoryStream = new MemoryStream();
        await response.ResponseStream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;
        return memoryStream;
    }

    public async Task DeleteAsync(FileStorageSettings settings, string storageKey)
    {
        using var client = CreateClient(settings);
        await client.DeleteObjectAsync(settings.BucketName, storageKey);
        _logger.LogInformation("Deleted file {StorageKey} from bucket {Bucket}", storageKey, settings.BucketName);
    }

    public string GeneratePresignedUrl(FileStorageSettings settings, string storageKey, TimeSpan expiry)
    {
        using var client = CreateClient(settings);
        var request = new GetPreSignedUrlRequest
        {
            BucketName = settings.BucketName,
            Key = storageKey,
            Expires = DateTime.UtcNow.Add(expiry)
        };
        return client.GetPreSignedURL(request);
    }

    private AmazonS3Client CreateClient(FileStorageSettings settings)
    {
        var keyId = EncryptionHelper.Decrypt(settings.KeyId);
        var appKey = EncryptionHelper.Decrypt(settings.AppKey);
        var config = new AmazonS3Config
        {
            ServiceURL = settings.Endpoint,
            ForcePathStyle = true
        };
        return new AmazonS3Client(keyId, appKey, config);
    }
}
