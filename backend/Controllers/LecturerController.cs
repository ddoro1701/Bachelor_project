using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Backend.Controllers
{
    [ApiController]
    [Route("api/lecturer")]
    public class LecturerController : ControllerBase
    {
        private readonly LecturerService _service;

        public LecturerController(LecturerService service) { _service = service; }

        // GET: /api/lecturer/emails → Frontend erwartet string[]
        [HttpGet("emails")]
        public async Task<IActionResult> GetEmails()
        {
            try
            {
                var lecturers = await _service.GetAllLecturersAsync();
                var emails = lecturers
                    .Where(l => !string.IsNullOrWhiteSpace(l.Email))
                    .Select(l => l.Email)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                return Ok(emails);
            }
            catch (Exception ex)
            {
                return Problem(title: "Failed to load emails", detail: ex.Message, statusCode: 500);
            }
        }

        // POST: /api/lecturer/emails
        // Accepts: { FirstName, LastName, Email } or { Name, Email }
        [HttpPost("emails")]
        public async Task<IActionResult> AddOrUpdateLecturer([FromBody] JsonElement body)
        {
            try
            {
                if (!body.TryGetProperty("Email", out var e) || string.IsNullOrWhiteSpace(e.GetString()))
                    return BadRequest("Email required.");
                var email = e.GetString()!.Trim();

                string? first = body.TryGetProperty("FirstName", out var f) ? f.GetString() : null;
                string? last  = body.TryGetProperty("LastName",  out var l) ? l.GetString() : null;

                if ((string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(last)) &&
                    body.TryGetProperty("Name", out var n))
                {
                    var (fs, ls) = LecturerService.SplitFullName(n.GetString());
                    first ??= fs; last ??= ls;
                }

                var lecturer = new Lecturer
                {
                    Email = email,
                    FirstName = string.IsNullOrWhiteSpace(first) ? null : first!.Trim(),
                    LastName  = string.IsNullOrWhiteSpace(last)  ? null : last!.Trim()
                };

                await _service.UpsertAsync(lecturer);
                return Ok(lecturer);
            }
            catch (Exception ex)
            {
                return Problem(title: "Failed to upsert lecturer", detail: ex.Message, statusCode: 500);
            }
        }

        // DELETE: /api/lecturer/emails?email=...
        [HttpDelete("emails")]
        public async Task<IActionResult> DeleteByEmail([FromQuery] string email)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email)) return BadRequest("Email required.");
                var ok = await _service.DeleteByEmailAsync(email.Trim());
                return Ok(new { deleted = ok });
            }
            catch (Exception ex)
            {
                return Problem(title: "Failed to delete lecturer", detail: ex.Message, statusCode: 500);
            }
        }
    }
}