using System.Text;
using Microsoft.AspNetCore.Mvc;
using VideoTranscriber.ViewModels;
using VideoTranscriberCore;
using VideoTranscriberData;
using VideoTranscriberStorage;

namespace VideoTranscriber.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ITranscriptionDataRepository _transcriptionDataRepository;
        private readonly IStorageClient _storageClient;

        public HomeController(ILogger<HomeController> logger, ITranscriptionDataRepository transcriptionDataRepository, 
            IStorageClient storageClient)
        {
            _logger = logger;
            _transcriptionDataRepository = transcriptionDataRepository;
            _storageClient = storageClient;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(HomeIndexModel model)
        {
            if (ModelState.IsValid)
            {
                var existingTranscripts = await _transcriptionDataRepository.GetAll();

                foreach (IFormFile file in Request.Form.Files)
                {
                    if (!existingTranscripts.Any(t => t.OriginalFilename == file.FileName))
                    {

                        Guid videoGuid = Guid.NewGuid();
                        TranscriptionData data = new TranscriptionData
                        {
                            OriginalFilename = file.FileName,
                            id = videoGuid,
                            ProjectName = model.ProjectName,
                            UploadDate = DateTime.UtcNow
                        };

                        await _transcriptionDataRepository.Add(data);

                        var videoUrl =
                            await _storageClient.UploadFile(file.FileName, file.OpenReadStream(), "toBeProcessed");

                    }
                }
                return RedirectToAction("Transcripts", "Home");
            }

            return View(model);
        }

        public async Task<IActionResult> ViewTranscript(Guid videoId)
        {
            TranscriptionData transcriptData =
                await _transcriptionDataRepository.Get(videoId);

            ViewTranscriptViewModel model = new ViewTranscriptViewModel()
            {
                Filename = transcriptData.OriginalFilename,
                Language = transcriptData.Language,
                Transcript = transcriptData.Transcript,
                Keywords = transcriptData.Keywords,
                Speakers = transcriptData.Speakers,
                VideoId = videoId
            };

            return View(model);
        }

        public async Task<IActionResult> Transcripts()
        {
            var transcripts = await _transcriptionDataRepository.GetAll();

            return View(transcripts.Where(t => t.Transcript != null && t.Transcript.Any()).ToList());
        }

        public async Task<IActionResult> TranscriptsForProject(string projectName)
        {
            var transcripts = await _transcriptionDataRepository.GetAll();

            return View("Transcripts", transcripts.Where(t => t.ProjectName == projectName));
        }

        public async Task<IActionResult> DownloadTranscript(Guid videoId)
        {
            TranscriptionData transcriptData =
                await _transcriptionDataRepository.Get(videoId);
            IEnumerable<TranscriptElement> elements =
                transcriptData.Transcript;
            IEnumerable<Speaker> speakers =
                transcriptData.Speakers;

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Start Time,Speaker,Text,Confidence");
            foreach (var element in elements)
            {
                builder.AppendLine(string.Join(",", element.StartTimeIndex,
                    speakers.First(s => s.Id == element.SpeakerId).Name, "\"" + element.Text.Replace("\"","'") + "\"", element.Confidence));
            }

            FileContentResult result = new FileContentResult(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv")
            {
                FileDownloadName = transcriptData.OriginalFilename + ".csv",
            };

            return result;
        }

        public async Task<IActionResult> EditSpeakers(Guid videoId)
        {
            TranscriptionData transcriptData =
                await _transcriptionDataRepository.Get(videoId);

            IEnumerable<Speaker> speakers =
                transcriptData.Speakers;

            EditSpeakersViewModel model = new EditSpeakersViewModel()
            {
                VideoId = videoId,
                Speakers = speakers
            };

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSpeakers(EditSpeakersViewModel model)
        {
            if (ModelState.IsValid)
            {
                TranscriptionData transcriptData =
                    await _transcriptionDataRepository.Get(model.VideoId);
                transcriptData.Speakers = model.Speakers;
                await _transcriptionDataRepository.Update(transcriptData);
                return RedirectToAction("ViewTranscript", new { videoId = model.VideoId });
            }

            return View(model);
        }
    }
}