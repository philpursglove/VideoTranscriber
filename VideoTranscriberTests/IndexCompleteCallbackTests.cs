using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.Extensions.Primitives;
using NSubstitute;
using NUnit.Framework;
using VideoTranscriberData;
using VideoTranscriberFunctions;
using VideoTranscriberStorage;
using VideoTranscriberVideoClient;

namespace VideoTranscriberTests.Functions;

[TestFixture]
public class IndexCompleteCallbackTests
{
    private IVideoIndexerClient _videoIndexerClient;
    private ITranscriptionDataRepository _repository;
    private IStorageClient _storageClient;

    private IndexCompleteCallback _indexCompleteCallback;

    [SetUp]
    public void Setup()
    {
        _videoIndexerClient = Substitute.For<IVideoIndexerClient>();
        _repository = Substitute.For<ITranscriptionDataRepository>();
        _storageClient = Substitute.For<IStorageClient>();

        _indexCompleteCallback = new IndexCompleteCallback(_videoIndexerClient, _repository, _storageClient);
    }

    [Test]
    public async Task WhenVideoIsNotProcessedNothingElseHappens()
    {
        HttpRequest request = Substitute.For<HttpRequest>();
        request.Query.Returns(new QueryCollection(new Dictionary<string, StringValues>
        {
            {"id", "1234"},
            {"state", "processing"}
        }));

        await _indexCompleteCallback.Run(request, null);

        await _videoIndexerClient.DidNotReceive().GetVideoIndex(Arg.Any<string>());
    }
}