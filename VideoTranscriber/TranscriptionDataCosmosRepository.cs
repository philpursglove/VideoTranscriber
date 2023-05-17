﻿using Microsoft.Azure.Cosmos;

namespace VideoTranscriber;

public class TranscriptionDataCosmosRepository : ITranscriptionDataRepository
{
    private readonly Container _container;

    public TranscriptionDataCosmosRepository(string connectionString)
    {
        CosmosClient cosmosClient = new CosmosClient(connectionString);
        Database database = cosmosClient.GetDatabase("transcriptions");
        _container = database.GetContainer("transcriptions");
    }

    public async Task<TranscriptionData> Get(Guid videoId)
    {
        return await _container.ReadItemAsync<TranscriptionData>(videoId.ToString(),
            new PartitionKey(videoId.ToString()));
    }

    public async Task<IEnumerable<TranscriptionData>> GetAll()
    {
        List<TranscriptionData> results = new List<TranscriptionData>();
        var query = new QueryDefinition("SELECT * FROM transcriptions");

        using FeedIterator<TranscriptionData> feed = _container.GetItemQueryIterator<TranscriptionData>(query);

        while (feed.HasMoreResults)
        {
            FeedResponse<TranscriptionData> response = await feed.ReadNextAsync();
            foreach (TranscriptionData transcriptionData in response)
            {
                results.Add(transcriptionData);
            }
        }

        return results;
    }

    public async Task Add(TranscriptionData transcriptionData)
    {
        await _container.CreateItemAsync(transcriptionData);
    }

    public async Task Update(TranscriptionData transcriptionData)
    {
        await _container.UpsertItemAsync(transcriptionData, new PartitionKey(transcriptionData.id.ToString()));
    }
}