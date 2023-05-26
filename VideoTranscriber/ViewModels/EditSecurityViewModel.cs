namespace VideoTranscriber.ViewModels;

public class EditSecurityViewModel
{
    public Guid VideoId { get;  set; }
    public string? Filename { get;  set; }
    public string? Owner { get;  set; }
    public string SecurityGroup { get;  set; }
}