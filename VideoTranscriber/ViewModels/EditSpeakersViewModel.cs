using VideoTranscriber.Models;
using VideoTranscriberCore;

namespace VideoTranscriber.ViewModels
{
    public class EditSpeakersViewModel
    {
        public Guid VideoId { get; set; }
        public IEnumerable<Speaker> Speakers { get; set; }

        public string Filename { get; set; }
    }
}