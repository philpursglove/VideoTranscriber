using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Azure.Storage.Blobs;
using VideoTranscriber.Models;

namespace VideoTranscriber.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly string _connString;

        public HomeController(ILogger<HomeController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _connString = configuration.GetConnectionString("VideoTranscriberStorageAccount");
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel {RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier});
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Index(HomeIndexModel model)
        {
            if (ModelState.IsValid)
            {
                var blobServiceClient = new BlobServiceClient(_connString);
            }

            return RedirectToAction(nameof(Index));
        }
    }
}