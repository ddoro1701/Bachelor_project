using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/package")]
    public class PackageController : ControllerBase
    {
        private readonly PackageService _packageService;
        private readonly EmailService _emailService;
        private readonly LecturerService _lecturerService; // NEW

        public PackageController(PackageService packageService, EmailService emailService, LecturerService lecturerService)
        {
            _packageService = packageService;
            _emailService = emailService;
            _lecturerService = lecturerService; // NEW
        }

        // GET: /api/package/all
        [HttpGet("all")]
        public async Task<IActionResult> GetAll()
        {
            var list = await _packageService.GetAllAsync(); // let exceptions bubble
            return Ok(list ?? new List<Package>());
        }

        // GET: /api/package/debug
        [HttpGet("debug")]
        public async Task<IActionResult> DebugInfo()
        {
            var count = await _packageService.CountAsync();
            var firstId = await _packageService.FirstIdAsync();
            return Ok(new
            {
                database = _packageService.DatabaseName,
                collection = _packageService.CollectionName,
                count,
                firstId
            });
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

        // POST: /api/package/send-email
        [HttpPost("send-email")]
        public async Task<IActionResult> SendEmail([FromBody] Package package)
        {
            if (package == null || string.IsNullOrWhiteSpace(package.LecturerEmail))
                return BadRequest("Missing required package data.");

            if (package.CollectionDate.HasValue)
            {
                var dt = package.CollectionDate.Value;
                if (dt.Kind == DateTimeKind.Unspecified) dt = DateTime.SpecifyKind(dt, DateTimeKind.Local);
                package.CollectionDate = dt.ToUniversalTime();
            }
            else
            {
                package.CollectionDate = DateTime.UtcNow;
            }

            string? absoluteImageUrl = null;
            if (!string.IsNullOrWhiteSpace(package.ImageUrl))
            {
                absoluteImageUrl = Uri.TryCreate(package.ImageUrl, UriKind.Absolute, out var abs)
                    ? abs.ToString()
                    : $"{Request.Scheme}://{Request.Host}{package.ImageUrl}";
            }

            package.QrToken ??= Guid.NewGuid().ToString("N");
            package.QrExpiresAt ??= DateTime.UtcNow.AddDays(14);

            var receptionUrl = $"{Request.Scheme}://{Request.Host}/reception.html?token={package.QrToken}";
            var qrDataUri = QrCodeService.ToDataUri(receptionUrl);

            if (string.IsNullOrWhiteSpace(package.Status))
                package.Status = "Received";
            if (package.CollectionDate == null)
                package.CollectionDate = DateTime.UtcNow;

            // Enrich with lecturer first/last name using email
            var lecturer = await _lecturerService.GetByEmailAsync(package.LecturerEmail!);
            if (lecturer != null)
            {
                package.LecturerFirstName ??= lecturer.FirstName;
                package.LecturerLastName  ??= lecturer.LastName;
            }

            await _packageService.CreateAsync(package);

            try
            {
                await _emailService.SendPackageEmailAsync(
                    toEmail: package.LecturerEmail,
                    subject: "Package arrived at reception!",
                    itemCount: package.ItemCount,
                    shippingProvider: package.ShippingProvider,
                    additionalInfo: package.AdditionalInfo ?? string.Empty,
                    collectionDateUtc: package.CollectionDate!.Value,   // ensure UTC above
                    receptionUrl: receptionUrl,
                    qrDataUri: qrDataUri,
                    imageUrl: absoluteImageUrl
                );
            }
            catch (Exception ex)
            {
                // surface failure to client so UI doesn't show "sent" when it wasn't
                return StatusCode(500, $"Email send failed: {ex.Message}");
            }

            return Ok(new
            {
                id = package.Id,
                lecturerEmail = package.LecturerEmail,
                lecturerFirstName = package.LecturerFirstName,
                lecturerLastName = package.LecturerLastName,
                status = package.Status
            });
        }

        // PUT/POST: /api/package/update-status
        [HttpPut("update-status")]
        [HttpPost("update-status")]
        public async Task<IActionResult> UpdateStatus([FromBody] UpdateStatusRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Id) || string.IsNullOrWhiteSpace(req.Status))
                return BadRequest("id and status required");

            var ok = await _packageService.UpdateStatusAsync(req.Id.Trim(), req.Status.Trim(), req.ViaQr);
            if (!ok) return NotFound("package not found");

            return Ok(new { id = req.Id, status = req.Status });
        }

        // DELETE/POST: /api/package/delete-collected
        [HttpDelete("delete-collected")]
        [HttpPost("delete-collected")]
        public async Task<IActionResult> DeleteCollected([FromBody] DeleteCollectedRequest req)
        {
            if (req?.Ids == null || req.Ids.Count == 0)
                return BadRequest("ids required");

            var deleted = await _packageService.DeleteCollectedAsync(req.Ids);
            return Ok(new { deleted });
        }
    }
}