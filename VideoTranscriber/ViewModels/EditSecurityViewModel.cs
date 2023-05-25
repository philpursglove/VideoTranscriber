namespace VideoTranscriber.ViewModels;

public class EditSecurityViewModel
{
    public Guid VideoId { get; internal set; }
    public string Filename { get; internal set; }
    public string Owner { get; internal set; }
    public string SecurityGroup { get; internal set; }
}