﻿using VideoTranscriberCore;

namespace VideoTranscriberData;

public interface ITranscriptionDataRepository
{
    public Task<TranscriptionData> Get(Guid videoId);
    public Task<IEnumerable<TranscriptionData>> GetAll();
    public Task Add(TranscriptionData transcriptionData);
    public Task Update(TranscriptionData transcriptionData);
    public Task<TranscriptionData> Get(string filename);

}