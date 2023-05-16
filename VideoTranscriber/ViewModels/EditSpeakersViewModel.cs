using VideoTranscriber.Models;

namespace VideoTranscriber.ViewModels
{
    public class EditSpeakersViewModel
    {
        public Guid VideoId { get; set; }
        public IEnumerable<Speaker> Speakers { get; set; }
    }
}