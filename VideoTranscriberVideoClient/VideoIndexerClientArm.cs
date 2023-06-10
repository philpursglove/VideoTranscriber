using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoTranscriberVideoClient
{
    public class VideoIndexerClientArm : VideoIndexerClientBase, IVideoIndexerClient
    {
        public Task<IndexingResult> GetVideoIndex(string videoIndexerId)
        {
            throw new NotImplementedException();
        }

        public Task<IndexingResult> IndexVideo(Uri videoUrl, string videoName, Guid videoGuid)
        {
            throw new NotImplementedException();
        }

        public Task SubmitVideoForIndexing(Uri videoUri, string videoName, Guid videoId, Uri callbackUri)
        {
            throw new NotImplementedException();
        }
    }
}
