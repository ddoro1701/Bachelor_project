using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using WebApplication1.Services;

namespace WebApplication1.Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ImageController : ControllerBase
    {
        private readonly string subscriptionKey;
        private readonly string endpoint;
        private readonly bool isStudentAccount;
        private readonly ImagePreprocessor _pre;
        private readonly IWebHostEnvironment _env;

        public ImageController(IConfiguration configuration, ImagePreprocessor pre, IWebHostEnvironment env)
        {
            subscriptionKey = configuration["AzureComputerVision:SubscriptionKey"];
            endpoint = configuration["AzureComputerVision:Endpoint"];
            isStudentAccount = bool.Parse(configuration["AzureAccount:IsStudentAccount"]);
            _pre = pre;
            _env = env;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadImage([FromForm] IFormFile image)
        {
            if (image == null || image.Length == 0)
                return BadRequest("No image uploaded.");

            // 1) Unverarbeitetes Bild speichern (wwwroot/uploads/{guid}.ext)
            var uploadsRoot = Path.Combine(_env.WebRootPath, "uploads");
            Directory.CreateDirectory(uploadsRoot);
            var ext = Path.GetExtension(image.FileName);
            if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";
            var fileId = Guid.NewGuid().ToString("N");
            var rawFileName = fileId + ext.ToLowerInvariant();
            var rawDiskPath = Path.Combine(uploadsRoot, rawFileName);
            var rawPublicUrl = "/uploads/" + rawFileName;

            await using (var rawFs = System.IO.File.Create(rawDiskPath))
            {
                await image.CopyToAsync(rawFs);
            }

            // 2) Für OCR normalisieren/croppen (aus der gespeicherten Datei laden)
            await using var outputStream = new MemoryStream();
            using (var inputImage = await Image.LoadAsync<Rgba32>(rawDiskPath))
            {
                // optionales Resize (Student Account) + Normalize + AutoCrop
                const int maxWidth = 1024, maxHeight = 768;
                if (isStudentAccount && (inputImage.Width > maxWidth || inputImage.Height > maxHeight))
                {
                    inputImage.Mutate(x => x.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(maxWidth, maxHeight) }));
                }
                _pre.Normalize(inputImage);
                _pre.AutoCrop(inputImage, backgroundThreshold: 245, padding: 12);

                await inputImage.SaveAsJpegAsync(outputStream);
            }
            outputStream.Seek(0, SeekOrigin.Begin);

            // 3) Azure OCR auf das verarbeitete Bild
            var client = new ComputerVisionClient(new ApiKeyServiceClientCredentials(subscriptionKey)) { Endpoint = endpoint };
            var result = await client.ReadInStreamAsync(outputStream);
            var operationId = result.OperationLocation.Split('/').Last();

            ReadOperationResult readResult;
            do
            {
                await Task.Delay(800);
                readResult = await client.GetReadResultAsync(Guid.Parse(operationId));
            } while (readResult.Status == OperationStatusCodes.Running || readResult.Status == OperationStatusCodes.NotStarted);

            if (readResult.Status == OperationStatusCodes.Failed)
                return BadRequest("Failed to process the image.");

            var lines = readResult.AnalyzeResult.ReadResults.SelectMany(r => r.Lines).Select(l => l.Text).ToList();
            var text = string.Join(" ", lines);

            // 4) Response inkl. URL zum unverarbeiteten Bild
            return Ok(new { text, lines, rawImageUrl = rawPublicUrl });
        }
    }
}