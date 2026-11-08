using Microsoft.AspNetCore.Mvc;
using TourWebApp.Services;

namespace TourWebApp.Controllers;

[Route("api/gemini")]
public sealed class GeminiController : Controller
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeminiController> _logger;

    public GeminiController(
        IConfiguration configuration,
        ILogger<GeminiController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("phan-tich")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> PhanTich(
        [FromBody] GeminiTravelRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null
            || string.IsNullOrWhiteSpace(request.NoiDung)
            || request.NoiDung.Length > 1000)
        {
            return BadRequest(new
            {
                message = "Vui lòng nhập yêu cầu từ 1 đến 1000 ký tự."
            });
        }

        request.DiemDens = (request.DiemDens ?? new())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Take(60)
            .Select(x => x.Length > 80 ? x[..80] : x)
            .ToList();

        request.SoThichs = (request.SoThichs ?? new())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Take(60)
            .Select(x => x.Length > 80 ? x[..80] : x)
            .ToList();

        try
        {
            var service = new GeminiService(_configuration);

            var result = await service.PhanTichAsync(
                request,
                cancellationToken);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                "Gemini analysis failed: {Error}",
                ex.Message);

            return StatusCode(503, new
            {
                message = ex.Message
            });
        }
        catch (HttpRequestException)
        {
            return StatusCode(503, new
            {
                message = "Không thể kết nối Gemini. Vui lòng thử lại."
            });
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(504, new
            {
                message = "Gemini phản hồi quá lâu. Vui lòng thử lại."
            });
        }
        catch (System.Text.Json.JsonException)
        {
            return StatusCode(502, new
            {
                message = "Gemini trả về dữ liệu chưa hợp lệ."
            });
        }
    }
}
