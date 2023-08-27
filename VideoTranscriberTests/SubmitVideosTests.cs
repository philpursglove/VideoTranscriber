using NSubstitute;
using VideoTranscriberData;
using VideoTranscriberFunctions;
using VideoTranscriberStorage;
using VideoTranscriberVideoClient;

namespace VideoTranscriberTests
{
    public class SubmitVideosTests
    {
        private IVideoIndexerClient _videoIndexerClient;
        private ITranscriptionDataRepository _repository;
        private IStorageClient _storageClient;
        private ConfigValues _configValues;
        private SubmitVideos _submitVideos;

        [SetUp]
        public void Setup()
        {
            _videoIndexerClient = Substitute.For<IVideoIndexerClient>();
            _repository = Substitute.For<ITranscriptionDataRepository>();
            _storageClient = Substitute.For<IStorageClient>();
            _configValues = Substitute.For<ConfigValues>();

            _submitVideos = new SubmitVideos(_videoIndexerClient, _repository, _storageClient, _configValues);
        }

        [Test]
        public async Task WhenNoFilesAvailableNothingElseHappens()
        {
            _storageClient.GetFileNames("toBeProcessed").Returns(new List<string>());

            await _submitVideos.Run(null, null);

            _videoIndexerClient.DidNotReceive().SubmitVideoForIndexing(Arg.Any<Uri>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<System.Uri>());
        }
    }
}