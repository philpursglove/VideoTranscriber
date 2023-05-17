using Azure;
using Azure.Data.Tables;

namespace VideoTranscriber;

//public class TranscriptionDataTableRepository : ITranscriptionDataRepository
//{
//    private readonly TableClient _tableClient;
//    private readonly string _partitionKey = "Transcriptions";
//    public TranscriptionDataTableRepository(string storageAccountName, string storageAccountKey, Uri tableUri)
//    {
//        TableServiceClient tableServiceClient =
//            new TableServiceClient(tableUri, new TableSharedKeyCredential(storageAccountName, storageAccountKey));
//        _tableClient = tableServiceClient.GetTableClient("Transcriptions");
//        _tableClient.CreateIfNotExists();
//    }

//    public async Task<TranscriptionData> Get(Guid videoId)
//    {
//        return await _tableClient.GetEntityAsync<TranscriptionData>(_partitionKey, videoId.ToString());
//    }

//    public async Task<IEnumerable<TranscriptionData>> GetAll()
//    {
//        IList<TranscriptionData> transcriptionData = new List<TranscriptionData>();
//        var transcripts = _tableClient.QueryAsync<TranscriptionData>(td => td.PartitionKey == _partitionKey);

//        await foreach (var transcript in transcripts)
//        {
//            transcriptionData.Add(transcript);
//        }

//        return transcriptionData;
//    }

//    public async Task Add(TranscriptionData transcriptionData)
//    {
//        await _tableClient.AddEntityAsync(transcriptionData);
//    }

//    public async Task Update(TranscriptionData transcriptionData)
//    {
//        await _tableClient.UpdateEntityAsync(transcriptionData, ETag.All);
//    }
//}