
using Microsoft.AspNetCore.Mvc;

namespace TourWebApp.Controllers;

[Route("tripi-chat")]
public sealed class TripiChatPageController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Trò chuyện cùng Tripi | HappyTrip";
        ViewData["Description"] =
            "Trò chuyện cùng Tripi, trợ lý du lịch AI của HappyTrip. " +
            "Khám phá các tour phù hợp với sở thích, ngân sách và thời gian của bạn.";

        return View();
    }
}
