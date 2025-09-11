using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LabelController : ControllerBase
    {
        private readonly LecturerMatcher _matcher;

        public LabelController(LecturerMatcher matcher)
        {
            _matcher = matcher;
        }

        [HttpPost("find-email")]
        public async Task<IActionResult> FindEmail([FromBody] System.Text.Json.JsonElement payload)
        {
            string? text = null;
            List<string>? lines = null;

            if (payload.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                text = payload.GetString();
            }
            else if (payload.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                if (payload.TryGetProperty("text", out var t) && t.ValueKind == System.Text.Json.JsonValueKind.String)
                    text = t.GetString();

                if (payload.TryGetProperty("lines", out var arr) && arr.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    lines = new List<string>();
                    foreach (var el in arr.EnumerateArray())
                        if (el.ValueKind == System.Text.Json.JsonValueKind.String)
                            lines.Add(el.GetString()!);
                }
            }

            if (string.IsNullOrWhiteSpace(text))
                return BadRequest("OCR text is empty. Please upload an image to generate OCR text.");

            var email = await _matcher.FindLecturerEmailAsync(text!, lines);
            return email == null
                   ? NotFound("Kein passendes Lecturer-Email gefunden.")
                   : Ok(new { email });
        }
    }
}