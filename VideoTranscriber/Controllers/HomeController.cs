using System.Text;
using Microsoft.AspNetCore.Mvc;
using VideoTranscriber.Models;
using VideoTranscriber.ViewModels;

namespace VideoTranscriber.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ITranscriptionDataRepository _transcriptionDataRepository;
        private readonly IStorageClient _storageClient;
        private readonly VideoIndexerClient _videoIndexerClient;

        public HomeController(ILogger<HomeController> logger, ITranscriptionDataRepository transcriptionDataRepository, 
            IStorageClient storageClient, VideoIndexerClient videoIndexerClient)
        {
            _logger = logger;
            _transcriptionDataRepository = transcriptionDataRepository;
            _storageClient = storageClient;
            _videoIndexerClient = videoIndexerClient;
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
                Guid videoGuid = Guid.NewGuid();
                TranscriptionData data = new TranscriptionData
                {
                    OriginalFilename = model.VideoFile.FileName,
                    id = videoGuid,
                    ProjectName = model.ProjectName,
                    UploadDate = DateTime.UtcNow
                };

                await _transcriptionDataRepository.Add(data);

                var videoUrl =
                    await _storageClient.UploadFile(model.VideoFile.FileName, model.VideoFile.OpenReadStream());

                DateTime startTime = DateTime.UtcNow;

                IndexingResult indexResult = await _videoIndexerClient.IndexVideo(videoUrl, model.VideoFile.FileName);

                DateTime endTime = DateTime.UtcNow;

                TranscriptionData updateData = await _transcriptionDataRepository.Get(videoGuid);
                updateData.Language = indexResult.Language;
                updateData.Transcript = indexResult.Transcript;
                updateData.Duration = indexResult.Duration;
                updateData.SpeakerCount = indexResult.SpeakerCount;
                updateData.Confidence = indexResult.Confidence;
                updateData.Keywords = indexResult.Keywords;
                updateData.Speakers = indexResult.Speakers;
                updateData.IndexDuration = endTime - startTime;
                await _transcriptionDataRepository.Update(updateData);

                await _storageClient.MoveToFolder(model.VideoFile.FileName, "processed");

                return RedirectToAction("ViewTranscript", "Home", new { videoId = videoGuid });
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

            return View(transcripts.Where(t => t.Transcript.Any()));
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