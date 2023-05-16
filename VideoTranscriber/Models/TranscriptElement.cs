﻿namespace VideoTranscriber.Models;

public class TranscriptElement
{
    public int Id { get; set; }
    public string Text { get; set; }
    public double Confidence { get; set; }
    public string StartTimeIndex { get; set; }
}