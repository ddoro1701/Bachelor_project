using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PackageController : ControllerBase
    {
        private readonly PackageService _packageService;
        private readonly EmailService _emailService;

        public PackageController(PackageService packageService, EmailService emailService)
        {
            _packageService = packageService;
            _emailService = emailService;
        }

        // GET: /api/package/all
        [HttpGet("all")]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var list = await _packageService.GetAllAsync();
                return Ok(list ?? new List<Package>());
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"GetAll failed: {ex}");
                return Ok(new List<Package>()); // nie 500 an UI
            }
        }

        // GET: /api/package/lookup?token=...
        [HttpGet("lookup")]
        public async Task<IActionResult> LookupByToken([FromQuery] string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return BadRequest("token required.");
            var pkg = await _packageService.GetByQrTokenAsync(token);
            if (pkg == null) return NotFound("invalid token");
            if (pkg.QrExpiresAt.HasValue && pkg.QrExpiresAt.Value < DateTime.UtcNow)
                return Unauthorized("token expired");

            return Ok(new
            {
                id = pkg.Id,
                lecturerEmail = pkg.LecturerEmail,
                lecturerFirstName = pkg.LecturerFirstName,
                lecturerLastName = pkg.LecturerLastName,
                itemCount = pkg.ItemCount,
                shippingProvider = pkg.ShippingProvider,
                additionalInfo = pkg.AdditionalInfo,
                collectionDate = pkg.CollectionDate,
                status = pkg.Status,
                imageUrl = pkg.ImageUrl
            });
        }

        // POST: /api/package/send-email  ← this must be POST (not GET)
        [HttpPost("send-email")]
        public async Task<IActionResult> SendEmailToLecturer([FromBody] Package package)
        {
            if (package == null || string.IsNullOrWhiteSpace(package.LecturerEmail))
                return BadRequest("Missing required package data.");

            // normalize date to UTC
            if (package.CollectionDate.HasValue)
            {
                var dt = package.CollectionDate.Value;
                if (dt.Kind == DateTimeKind.Unspecified) dt = DateTime.SpecifyKind(dt, DateTimeKind.Local);
                package.CollectionDate = dt.ToUniversalTime();
            }
            else package.CollectionDate = DateTime.UtcNow;

            // optional: absolute Bild-URL
            string? absoluteImageUrl = null;
            if (!string.IsNullOrWhiteSpace(package.ImageUrl))
            {
                absoluteImageUrl = Uri.TryCreate(package.ImageUrl, UriKind.Absolute, out var abs)
                    ? abs.ToString()
                    : $"{Request.Scheme}://{Request.Host}{package.ImageUrl}";
            }

            // QR vorbereiten
            package.QrToken ??= Guid.NewGuid().ToString("N");
            package.QrExpiresAt ??= DateTime.UtcNow.AddDays(14);

            var receptionUrl = $"{Request.Scheme}://{Request.Host}/reception.html?token={package.QrToken}";
            var qrDataUri = QrCodeService.ToDataUri(receptionUrl);

            await _packageService.CreateAsync(package);

            await _emailService.SendPackageEmailAsync(
                toEmail: package.LecturerEmail,
                subject: "Package details",
                itemCount: package.ItemCount,
                shippingProvider: package.ShippingProvider ?? string.Empty,
                additionalInfo: package.AdditionalInfo ?? string.Empty,
                collectionDateUtc: package.CollectionDate!.Value,
                imageUrl: absoluteImageUrl,
                qrLink: receptionUrl,
                qrImageDataUri: qrDataUri
            );

            return Ok(new { message = "Package record created successfully." });
        }
    }
}