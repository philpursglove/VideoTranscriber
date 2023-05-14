using Azure;
using Azure.Data.Tables;

public class TranscriptionData : ITableEntity
{
    public string PartitionKey { get; set; }
    public string RowKey { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string Language { get; set; }

    public string Transcript { get; set; }

    public string OriginalFilename { get; set; }

    public Guid VideoId { get; set; }

    public Guid BatchId { get; set; }

    public string Duration { get; set; }
}