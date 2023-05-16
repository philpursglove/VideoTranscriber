namespace VideoTranscriber.Models;

public class TranscriptElement
{
    public int Id { get; set; }
    public string Text { get; set; }
    public double Confidence { get; set; }
    public string StartTimeIndex { get; set; }
    public int SpeakerId { get; set; }
}

public class Speaker
{
    public int Id { get; set; }
    public string Name { get; set; }
}