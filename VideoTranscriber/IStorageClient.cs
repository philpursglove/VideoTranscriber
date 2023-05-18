namespace VideoTranscriber;

public interface IStorageClient
{
    public Task<Uri> UploadFile(string filename, Stream content, string folderName);
    public Task MoveToFolder(string filename, string targetFolder);
}