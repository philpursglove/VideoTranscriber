using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;

namespace VideoTranscriberStorage;

public class AzureStorageClient : IStorageClient
{
    private readonly string _connectionString;
    private readonly string _containerName;

    public AzureStorageClient(string connectionString, string containerName)
    {
        _connectionString = connectionString;
        _containerName = containerName;
    }

    public async Task<Uri> UploadFile(string filename, Stream content, string folderName)
    {
        var blobServiceClient = new BlobServiceClient(_connectionString);
        var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);
        var blobClient = containerClient.GetBlobClient(Path.Join(folderName, filename));
        var stream = await blobClient.OpenWriteAsync(true);
        await content.CopyToAsync(stream);
        stream.Close();
        
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