using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;

namespace VideoTranscriberStorage;

public class AzureStorageClient : IStorageClient
{
    private readonly BlobContainerClient _containerClient;

    public AzureStorageClient(string connectionString, string containerName)
    {
        var blobServiceClient = new BlobServiceClient(connectionString);
        _containerClient = blobServiceClient.GetBlobContainerClient(containerName);
    }

    public async Task<Uri> UploadFile(string filename, Stream content, string folderName)
    {
        var blobClient = _containerClient.GetBlobClient(Path.Join(folderName, filename));
        var stream = await blobClient.OpenWriteAsync(true);
        await content.CopyToAsync(stream);
        stream.Close();
        
        return blobClient.Uri;
    }

    public async Task MoveToFolder(string filename, string targetFolder)
    {
        var sourceBlobClient = _containerClient.GetBlobClient(filename);

        filename = filename.Substring(filename.IndexOf("/") + 1);
        var destBlobClient = _containerClient.GetBlockBlobClient($"{targetFolder}/{filename}");
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

    public async Task<List<string>> GetFileNames(string folderName)
    {
        List<string> fileNames = new List<string>();

        // Get the list of blobs
        var files = _containerClient.GetBlobsAsync(prefix: "toBeProcessed");

        // For each blob
        //   Get the blob
        await foreach (var file in files)
        {
            if (file.Properties.ContentLength > 0)
            {
                fileNames.Add(file.Name);
            }
        }

        return fileNames;
    }

    public Task<Uri> GetFileUri(string fileName)
    {
        var blobClient = _containerClient.GetBlobClient(fileName);
        return Task.FromResult(blobClient.Uri);
    }
}