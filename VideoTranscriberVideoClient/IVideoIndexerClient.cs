namespace VideoTranscriberVideoClient;

public interface IVideoIndexerClient
{
    Task<IndexingResult> GetVideoIndex(string videoIndexerId);
    Task SubmitVideoForIndexing(Uri videoUri, string videoName, Guid videoId, Uri callbackUri);
    Task<IndexingResult> IndexVideo(Uri videoUrl, string videoName, Guid videoGuid);
}