using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TourWebApp.Data.Models;
using TourWebApp.Services;

namespace TourWebApp.Controllers;

public class AIRecommendationController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IMatchingScoreService _matchingService;

    public AIRecommendationController(
        ApplicationDbContext db,
        IMatchingScoreService matchingService)
    {
        _db = db;
        _matchingService = matchingService;
    }

    [HttpGet("/goi-y-tour")]
    public async Task<IActionResult> Index(
        [FromQuery] MatchingScoreRequest request,
        CancellationToken cancellationToken)
    {
        var model = new AIRecommendationPageViewModel
        {
            Request = request
        };

        var connection = _db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;

        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    SELECT IdDiemDen, TenDiemDen
                    FROM dbo.AIDiemDen
                    WHERE TrangThai = 1
                    ORDER BY TenDiemDen
                    """;

                await using var reader =
                    await command.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    model.DiemDens.Add(new AISelectionItem
                    {
                        Id = reader.GetInt32(0),
                        Ten = reader.GetString(1)
                    });
                }
            }

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    SELECT IdSoThich, TenSoThich
                    FROM dbo.AISoThich
                    WHERE TrangThai = 1
                    ORDER BY TenSoThich
                    """;

                await using var reader =
                    await command.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    model.SoThichs.Add(new AISelectionItem
                    {
                        Id = reader.GetInt32(0),
                        Ten = reader.GetString(1)
                    });
                }
            }
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }

        model.DaTimKiem = HttpContext.Request.Query.ContainsKey("tim");

        if (!model.DaTimKiem)
        {
            return View(model);
        }

        if (request.NganSachToiDa is <= 0 ||
            request.SoNgayToiDa is <= 0)
        {
            model.ThongBao = "Ngân sách và số ngày phải lớn hơn 0.";
            return View(model);
        }

        request.IdSoThich ??= new List<int>();

        if (!request.IdDiemDen.HasValue &&
            request.IdSoThich.Count == 0 &&
            !request.NganSachToiDa.HasValue)
        {
            model.ThongBao =
                "Vui lòng chọn ít nhất một tiêu chí: điểm đến, sở thích hoặc ngân sách.";

            return View(model);
        }

        var diemDenHopLe = !request.IdDiemDen.HasValue ||
            model.DiemDens.Any(x => x.Id == request.IdDiemDen.Value);

        var soThichHopLe = request.IdSoThich.All(id =>
            model.SoThichs.Any(x => x.Id == id));

        if (!diemDenHopLe || !soThichHopLe)
        {
            model.ThongBao =
                "Điểm đến hoặc sở thích không tồn tại trong danh mục hiện tại.";

            return View(model);
        }

        model.KetQua = await _matchingService.GoiYTourAsync(
            request,
            cancellationToken);

        if (model.KetQua.Count == 0)
        {
            model.ThongBao =
                "Chưa có tour đáp ứng các điều kiện đã chọn. Bạn thử tăng ngân sách hoặc số ngày nhé.";
        }

        return View(model);
    }
}

public sealed class AISelectionItem
{
    public int Id { get; set; }
    public string Ten { get; set; } = "";
}

public sealed class AIRecommendationPageViewModel
{
    public MatchingScoreRequest Request { get; set; } = new();

    public List<AISelectionItem> DiemDens { get; set; } = new();
    public List<AISelectionItem> SoThichs { get; set; } = new();
    public List<MatchingScoreResult> KetQua { get; set; } = new();

    public bool DaTimKiem { get; set; }
    public string? ThongBao { get; set; }
}
