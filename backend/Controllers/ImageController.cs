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

        public ImageController(IConfiguration configuration, ImagePreprocessor pre)
        {
            subscriptionKey = configuration["AzureComputerVision:SubscriptionKey"];
            endpoint = configuration["AzureComputerVision:Endpoint"];
            isStudentAccount = bool.Parse(configuration["AzureAccount:IsStudentAccount"]);
            _pre = pre;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadImage([FromForm] IFormFile image)
        {
            if (image == null || image.Length == 0)
                return BadRequest("No image uploaded.");

            using (var inputStream = image.OpenReadStream())
            using (var outputStream = new MemoryStream())
            {
                var img = await Image.LoadAsync<Rgba32>(inputStream);

                // Resize for student accounts to keep quota in check
                const int maxWidth = 1024;
                const int maxHeight = 768;
                if (isStudentAccount && (img.Width > maxWidth || img.Height > maxHeight))
                {
                    img.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Mode = ResizeMode.Max,
                        Size = new Size(maxWidth, maxHeight)
                    }));
                }

                // Normalize + Auto-crop
                _pre.Normalize(img);
                _pre.AutoCrop(img, backgroundThreshold: 245, padding: 12);

                // Save processed image to stream
                await img.SaveAsJpegAsync(outputStream);
                outputStream.Seek(0, SeekOrigin.Begin);

                var client = new ComputerVisionClient(new ApiKeyServiceClientCredentials(subscriptionKey))
                {
                    Endpoint = endpoint
                };

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

                var text = string.Join(" ", readResult.AnalyzeResult.ReadResults
                    .SelectMany(r => r.Lines)
                    .Select(l => l.Text));

                return Ok(new { text });
            }
        }
    }
}