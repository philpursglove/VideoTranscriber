namespace VideoTranscriberStorage;

public interface IStorageClient
{
    public Task<Uri> UploadFile(string filename, Stream content, string folderName);
    public Task MoveToFolder(string filename, string targetFolder);
    public Task<List<string>> GetFileNames(string folderName);
    public Task<Uri> GetFileUri(string fileName);
}