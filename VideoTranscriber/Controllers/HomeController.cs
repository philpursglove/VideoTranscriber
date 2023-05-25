using System.Text;
using Aspose.Words;
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
                foreach (IFormFile file in Request.Form.Files)
                {
                    var username = HttpContext.User.Identity.Name.Replace("AzureAD\\", string.Empty).ToLowerInvariant();

                    if (_transcriptionDataRepository.Get(file.FileName) == null)
                    {
                        Guid videoGuid = Guid.NewGuid();
                        TranscriptionData data = new TranscriptionData
                        {
                            OriginalFilename = file.FileName,
                            id = videoGuid,
                            ProjectName = model.ProjectName,
                            UploadDate = DateTime.UtcNow,
                            Owner = username
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

            var username = HttpContext.User.Identity.Name.Replace("AzureAD\\", string.Empty).ToLowerInvariant();

            if (transcriptData.Owner.ToLowerInvariant() == username || transcriptData.SecurityGroup.ToLowerInvariant().Contains(username))
            {
                ViewTranscriptViewModel model = new ViewTranscriptViewModel()
                {
                    Filename = transcriptData.OriginalFilename,
                    Language = transcriptData.Language,
                    Transcript = transcriptData.Transcript,
                    Keywords = transcriptData.Keywords,
                    Speakers = transcriptData.Speakers,
                    VideoId = videoId,
                    UserIsFileOwner = transcriptData.Owner.ToLowerInvariant() == username
                };

                return View(model);
            }

            return new ForbidResult();
        }

        public async Task<IActionResult> Transcripts()
        {
            var transcripts = await _transcriptionDataRepository.GetAll();

            var username = HttpContext.User.Identity.Name.Replace("AzureAD\\", string.Empty).ToLowerInvariant();

            return View(transcripts.Where(t => t.Transcript != null && t.Transcript.Any() && (t.Owner.ToLowerInvariant() == username || t.SecurityGroup.ToLowerInvariant().Contains(username))).ToList());
        }

        public async Task<IActionResult> TranscriptsForProject(string projectName)
        {
            var transcripts = await _transcriptionDataRepository.GetAll();

            return View("Transcripts", transcripts.Where(t => t.ProjectName == projectName));
        }

        public async Task<IActionResult> MyTranscripts()
        {
            var transcripts = await _transcriptionDataRepository.GetAll();

            var username = HttpContext.User.Identity.Name.Replace("AzureAD\\", string.Empty).ToLowerInvariant();

            return View("Transcripts", transcripts.Where(t => t.Owner.ToLowerInvariant() == username));
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

        public async Task<IActionResult> DownloadWord(Guid videoId, bool includeTimestamps = false)
        {
            TranscriptionData transcriptData =
                await _transcriptionDataRepository.Get(videoId);
            IEnumerable<TranscriptElement> elements =
                transcriptData.Transcript;
            IEnumerable<Speaker> speakers =
                transcriptData.Speakers;

            Document doc = new Document();
            DocumentBuilder builder = new DocumentBuilder(doc);
            builder.Writeln($"File name: {transcriptData.OriginalFilename}");

            builder.Bold = true;
            builder.Write("Moderator questions in Bold, ");
            builder.Bold = false;
            builder.Write("Respondents in Regular text.");
            builder.Writeln();
            builder.InsertHorizontalRule();

            foreach (var element in elements)
            {
                var speakerName = speakers.First(s => s.Id == element.SpeakerId).Name;
                builder.Bold = speakerName.ToLowerInvariant().Contains("moderator");

                builder.Writeln(includeTimestamps
                    ? $"{element.StartTimeIndex}{ControlChar.Tab} {speakerName}: {element.Text}"
                    : $"{speakerName}: {element.Text}");
            }

            Stream docStream = new MemoryStream();
            doc.Save(docStream, SaveFormat.Docx);
            docStream.Position = 0;
            FileStreamResult result = new FileStreamResult(docStream,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
            result.FileDownloadName = transcriptData.OriginalFilename + ".docx";

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
                Speakers = speakers,
                Filename = transcriptData.OriginalFilename,
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

        public async Task<IActionResult> EditSecurity(Guid videoId)
        {
            TranscriptionData transcriptData =
                await _transcriptionDataRepository.Get(videoId);

            var username = HttpContext.User.Identity.Name.Replace("AzureAD\\", string.Empty).ToLowerInvariant();

            if (transcriptData.Owner.ToLowerInvariant() == username)
            {
                EditSecurityViewModel model = new EditSecurityViewModel()
                {
                    VideoId = videoId,
                    Filename = transcriptData.OriginalFilename,
                    Owner = transcriptData.Owner,
                    SecurityGroup = transcriptData.SecurityGroup
                };

                return View(model);
            }

            return new ForbidResult();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSecurity(EditSecurityViewModel model)
        {
            if (ModelState.IsValid)
            {
                TranscriptionData transcriptData =
                    await _transcriptionDataRepository.Get(model.VideoId);
                transcriptData.SecurityGroup = model.SecurityGroup;
                await _transcriptionDataRepository.Update(transcriptData);
                return RedirectToAction("ViewTranscript", new { videoId = model.VideoId });
            }

            return View(model);
        }
        
    }
}