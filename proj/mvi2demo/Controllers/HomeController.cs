using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using mvi2demo.Models;
using mvi2demo.InferenceTools;
using mvi2demo.Services;

namespace mvi2demo.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IBufferedFileUploadService _bufferedFileUploadService;
        private readonly IWebHostEnvironment _env;

        public HomeController(ILogger<HomeController> logger,
            IBufferedFileUploadService bufferedFileUploadService,
            IWebHostEnvironment env)
        {
            _logger = logger;
            _bufferedFileUploadService = bufferedFileUploadService;
            _env = env;
        }

        public IActionResult Index()
        {
            ViewBag.MidiLink = "";
            return View();
        }

        [HttpPost]
        public async Task<ActionResult> Index(IFormFile file)
        {
            (var succeded, var inputFilePath) = await _bufferedFileUploadService.UploadFile(file);
            if (succeded)
            {
                ViewBag.Message = $"File Uploaded!"; // \nSaved in {inputFilePath}";
                var mvi = new MidiVelocityInfer();
                bool result = mvi.Inference(inputFilePath, out string outputFilePath);
                CopyToWebDir(outputFilePath, _env.WebRootPath, out string outputFileWebPath);
                string outputFileUrl = GetOutputFileUrl(outputFileWebPath, ""); // Replace with your own base url

                if (result)
                {
                    ViewBag.InferenceMessage = $"Inference Finished!";
                    ViewBag.MidiLink = outputFileUrl;
                }
                else
                {
                    ViewBag.InferenceMessage = "Inference Failed";
                }
            }
            else
            {
                ViewBag.Message = "File Upload Failed";
            }
            return View();
        }

        private string GetOutputFileUrl(string outputFileWebPath, string baseUrl = "")
        {
            if (baseUrl.Equals("")) baseUrl = MyHttpContext.AppBaseUrl;
            string folderName = new DirectoryInfo(outputFileWebPath).Parent.Name;
            string fileName = Path.GetFileName(outputFileWebPath);

            var url = UrlCombineLib.UrlCombine.Combine(baseUrl, "output", folderName, fileName);
            return url;
        }

        private static void CopyToWebDir(string outputFilePath, string webRootPath, out string outputFileWebPath)
        {
            string folderName = new DirectoryInfo(outputFilePath).Parent.Name;
            string webDir = Path.Combine(webRootPath, "output", folderName);
            if (!Directory.Exists(webDir))
            {
                Directory.CreateDirectory(webDir);
            }
            outputFileWebPath = Path.Combine(webDir, Path.GetFileName(outputFilePath));
            System.IO.File.Copy(outputFilePath, outputFileWebPath, true);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}