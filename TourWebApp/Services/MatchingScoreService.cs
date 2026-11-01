using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TourWebApp.Data.Models;

namespace TourWebApp.Services;

public sealed class MatchingScoreRequest
{
    public int? IdDiemDen { get; set; }
    public List<int> IdSoThich { get; set; } = new();
    public decimal? NganSachToiDa { get; set; }
    public int? SoNgayToiDa { get; set; }
    public int TopN { get; set; } = 5;
}

public sealed class MatchingScoreResult
{
    public int IdTour { get; set; }
    public string TenTour { get; set; } = "";
    public string DiaDiem { get; set; } = "";
    public string HinhAnh { get; set; } = "";
    public string ThoiGian { get; set; } = "";
    public decimal GiaBan { get; set; }
    public int? SoNgay { get; set; }

    public int DiemSoThich { get; set; }
    public int DiemDiemDen { get; set; }
    public int DiemNganSach { get; set; }
    public int MatchingScore { get; set; }

    public List<string> LyDoGoiY { get; set; } = new();
}

public interface IMatchingScoreService
{
    Task<List<MatchingScoreResult>> GoiYTourAsync(
        MatchingScoreRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class MatchingScoreService : IMatchingScoreService
{
    private readonly ApplicationDbContext _db;

    public MatchingScoreService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<MatchingScoreResult>> GoiYTourAsync(
        MatchingScoreRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.IdDiemDen is <= 0)
            throw new ArgumentException("Điểm đến không hợp lệ.");

        if (request.NganSachToiDa is <= 0)
            throw new ArgumentException("Ngân sách phải lớn hơn 0.");

        if (request.SoNgayToiDa is <= 0)
            throw new ArgumentException("Số ngày phải lớn hơn 0.");

        var soThichDaChon = (request.IdSoThich ?? new List<int>())
            .Where(id => id > 0)
            .Distinct()
            .ToHashSet();

        var soLuongKetQua = Math.Clamp(request.TopN, 1, 20);

        var tours = new Dictionary<int, TourMatchingData>();

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
                    SELECT
                        t.IdTour,
                        t.TenTour,
                        t.DiaDiem,
                        t.HinhAnh,
                        t.ThoiGian,
                        COALESCE(
                            NULLIF(t.GiaKhuyenMai, 0),
                            NULLIF(t.GiaGoc, 0)
                        ) AS GiaBan,
                        td.IdDiemDen,
                        td.VaiTro
                    FROM dbo.Tour AS t
                    LEFT JOIN dbo.AITourDiemDen AS td
                        ON td.IdTour = t.IdTour
                    WHERE t.TrangThai = 1
                      AND COALESCE(
                          NULLIF(t.GiaKhuyenMai, 0),
                          NULLIF(t.GiaGoc, 0)
                      ) > 0
                    ORDER BY t.IdTour;
                    """;

                await using var reader =
                    await command.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    var idTour = reader.GetInt32(0);

                    if (!tours.TryGetValue(idTour, out var tour))
                    {
                        var thoiGian = ReadText(reader, 4);

                        tour = new TourMatchingData
                        {
                            IdTour = idTour,
                            TenTour = ReadText(reader, 1),
                            DiaDiem = ReadText(reader, 2),
                            HinhAnh = ReadText(reader, 3),
                            ThoiGian = thoiGian,
                            SoNgay = ParseSoNgay(thoiGian),
                            GiaBan = reader.GetDecimal(5)
                        };

                        tours.Add(idTour, tour);
                    }

                    if (!reader.IsDBNull(6))
                    {
                        var vaiTro = ReadText(reader, 7);

                        if (string.Equals(
                            vaiTro.Trim(),
                            "DiemDen",
                            StringComparison.OrdinalIgnoreCase))
                        {
                            tour.DiemDenIds.Add(reader.GetInt32(6));
                        }
                    }
                }
            }

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    SELECT
                        ts.IdTour,
                        ts.IdSoThich
                    FROM dbo.AITourSoThich AS ts
                    INNER JOIN dbo.Tour AS t
                        ON t.IdTour = ts.IdTour
                    WHERE t.TrangThai = 1;
                    """;

                await using var reader =
                    await command.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    var idTour = reader.GetInt32(0);
                    var idSoThich = reader.GetInt32(1);

                    if (tours.TryGetValue(idTour, out var tour))
                    {
                        tour.SoThichIds.Add(idSoThich);
                    }
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

        var results = new List<MatchingScoreResult>();

        foreach (var tour in tours.Values)
        {

	if (request.IdDiemDen.HasValue &&
    !tour.DiemDenIds.Contains(request.IdDiemDen.Value))
{
    continue;
}
            if (request.NganSachToiDa.HasValue &&
                tour.GiaBan > request.NganSachToiDa.Value)
            {
                continue;
            }

            if (request.SoNgayToiDa.HasValue &&
                (!tour.SoNgay.HasValue ||
                 tour.SoNgay.Value > request.SoNgayToiDa.Value))
            {
                continue;
            }

            var lyDo = new List<string>();

            var diemSoThich = 0;

            if (soThichDaChon.Count > 0)
            {
                var soThichTrung = tour.SoThichIds
                    .Count(id => soThichDaChon.Contains(id));

                diemSoThich = (int)Math.Round(
                    50.0 * soThichTrung / soThichDaChon.Count,
                    MidpointRounding.AwayFromZero);

                if (soThichTrung > 0)
                {
                    lyDo.Add(
                        $"Phù hợp {soThichTrung}/{soThichDaChon.Count} sở thích đã chọn");
                }
            }

            var diemDiemDen = 0;

            if (request.IdDiemDen.HasValue &&
                tour.DiemDenIds.Contains(request.IdDiemDen.Value))
            {
                diemDiemDen = 30;
                lyDo.Add("Có điểm tham quan đúng với điểm đến mong muốn");
            }

            var diemNganSach = 0;

            if (request.NganSachToiDa.HasValue)
            {
                diemNganSach = 20;
                lyDo.Add("Giá tour nằm trong ngân sách");
            }

            if (request.SoNgayToiDa.HasValue && tour.SoNgay.HasValue)
            {
                lyDo.Add(
                    $"Thời gian {tour.SoNgay.Value} ngày phù hợp lịch trình");
            }

            if (lyDo.Count == 0)
            {
                lyDo.Add("Tour đang hoạt động và có giá bán hợp lệ");
            }

            var tongDiem =
                diemSoThich + diemDiemDen + diemNganSach;

            results.Add(new MatchingScoreResult
            {
                IdTour = tour.IdTour,
                TenTour = tour.TenTour,
                DiaDiem = tour.DiaDiem,
                HinhAnh = tour.HinhAnh,
                ThoiGian = tour.ThoiGian,
                GiaBan = tour.GiaBan,
                SoNgay = tour.SoNgay,
                DiemSoThich = diemSoThich,
                DiemDiemDen = diemDiemDen,
                DiemNganSach = diemNganSach,
                MatchingScore = tongDiem,
                LyDoGoiY = lyDo
            });
        }

        return results
            .OrderByDescending(t => t.MatchingScore)
            .ThenBy(t => t.GiaBan)
            .ThenBy(t => t.IdTour)
            .Take(soLuongKetQua)
            .ToList();
    }

    private static string ReadText(
        System.Data.Common.DbDataReader reader,
        int ordinal)
    {
        return reader.IsDBNull(ordinal)
            ? ""
            : reader.GetString(ordinal);
    }

    private static int? ParseSoNgay(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var normalized = text.Trim().Normalize(
            System.Text.NormalizationForm.FormC);

        var match = Regex.Match(
            normalized,
            @"(?<!\d)(\d+)\s*(?:ngày|ngay|n)(?![a-z])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (!match.Success)
            return null;

        if (!int.TryParse(
            match.Groups[1].Value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var days))
        {
            return null;
        }

        return days is >= 1 and <= 365 ? days : null;
    }

    private sealed class TourMatchingData
    {
        public int IdTour { get; set; }
        public string TenTour { get; set; } = "";
        public string DiaDiem { get; set; } = "";
        public string HinhAnh { get; set; } = "";
        public string ThoiGian { get; set; } = "";
        public decimal GiaBan { get; set; }
        public int? SoNgay { get; set; }

        public HashSet<int> DiemDenIds { get; } = new();
        public HashSet<int> SoThichIds { get; } = new();
    }
}
