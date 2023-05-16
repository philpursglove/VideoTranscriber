﻿namespace VideoTranscriber.Models;

public class IndexingResult
{
    public string Language { get; set; }
    public IEnumerable<TranscriptElement> Transcript { get; set; }
    public string Duration { get; set; }
    public int SpeakerCount { get; set; }
    public double Confidence { get; set; }
    public IEnumerable<string> Keywords { get; set; }
}