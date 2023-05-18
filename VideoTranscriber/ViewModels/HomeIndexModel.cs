namespace VideoTranscriber.ViewModels;

public class HomeIndexModel
{
    public IFormFile VideoFile1 { get; set; }
    public IFormFile? VideoFile2 { get; set; }
    public IFormFile? VideoFile3 { get; set; }
    public IFormFile? VideoFile4 { get; set; }
    public IFormFile? VideoFile5 { get; set; }

    public string ProjectName { get; set; }
}