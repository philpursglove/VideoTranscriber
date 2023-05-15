using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using System.Reflection;

namespace VideoTranscriber;

public interface IStorageClient
{
    public Task<Uri> UploadFile(string filename, Stream content);
    public Task MoveToFolder(string filename, string targetFolder);
}

public class AzureStorageClient : IStorageClient
{
    private readonly string _connectionString;
    private readonly string _containerName;

    public AzureStorageClient(string connectionString, string containerName)
    {
        _connectionString = connectionString;
        _containerName = containerName;
    }

    public async Task<Uri> UploadFile(string filename, Stream content)
    {
        var blobServiceClient = new BlobServiceClient(_connectionString);
        var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);
        var blobClient = containerClient.GetBlobClient(filename);
        await blobClient.UploadAsync(content, true);

        return blobClient.Uri;
    }

    public async Task MoveToFolder(string filename, string targetFolder)
    {
        var blobServiceClient = new BlobServiceClient(_connectionString);
        var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);
        var sourceBlobClient = containerClient.GetBlobClient(filename);
        var destBlobClient = containerClient.GetBlockBlobClient($"{targetFolder}/{filename}");
        var copy = destBlobClient.StartCopyFromUriAsync(sourceBlobClient.Uri);
        while (true)
        {
            copy.Wait();
            if (copy.Status == TaskStatus.RanToCompletion)
            {
                break;
            }
            Thread.Sleep(1000);
        }

        await sourceBlobClient.DeleteAsync();
    }
}