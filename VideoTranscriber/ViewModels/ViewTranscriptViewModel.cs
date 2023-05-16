using VideoTranscriber.Models;

namespace VideoTranscriber.ViewModels;

public class ViewTranscriptViewModel
{
    public string Filename { get; set; }
    public string Language { get; set; }
    public IEnumerable<TranscriptElement> Transcript { get; set; }
    public IEnumerable<string> Keywords { get; set; }
    public IEnumerable<Speaker> Speakers { get; set; }
    public Guid VideoId { get; set; }
}