using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TourWebApp.Data.Models;

namespace TourWebApp.Services;

// ============================================================
// REQUEST
// ============================================================

public sealed class MatchingScoreRequest
{
    public int? IdDiemDen { get; set; }

    public List<int> IdSoThich { get; set; } = new();

    public decimal? NganSachToiDa { get; set; }

    public int? SoNgayToiDa { get; set; }

    public string? LoaiTour { get; set; }

    public int? SoKhach { get; set; }

    public DateOnly? NgayDuKien { get; set; }

    // MoiNguoi hoac CaNhom.
    // Null: giu cach tinh cu la moi nguoi.
    public string? LoaiNganSach { get; set; }

    public int TopN { get; set; } = 5;
}

// ============================================================
// RESULT
// ============================================================

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

    public string? TenLoaiTour { get; set; }

    public DateOnly? NgayKhoiHanhPhuHop { get; set; }

    public int? SoChoConLai { get; set; }
}

// ============================================================
// INTERFACE
// ============================================================

public interface IMatchingScoreService
{
    Task<List<MatchingScoreResult>> GoiYTourAsync(
        MatchingScoreRequest request,
        CancellationToken cancellationToken = default);
}

// ============================================================
// MATCHING SCORE SERVICE
// ============================================================

public sealed class MatchingScoreService : IMatchingScoreService
{
    private readonly ApplicationDbContext _db;

    private const int DiemToiDaSoThich = 50;
    private const int DiemToiDaDiemDen = 30;
    private const int DiemToiDaNganSach = 20;

    public MatchingScoreService(ApplicationDbContext db)
    {
        _db = db;
    }

    // ========================================================
    // 1. HAM GOI Y TOUR
    // ========================================================

    public async Task<List<MatchingScoreResult>> GoiYTourAsync(
        MatchingScoreRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // ====================================================
        // 2. VALIDATE
        // ====================================================

        if (request.IdDiemDen is <= 0)
        {
            throw new ArgumentException(
                "Điểm đến không hợp lệ.");
        }

        if (request.NganSachToiDa is <= 0)
        {
            throw new ArgumentException(
                "Ngân sách phải lớn hơn 0.");
        }

        if (request.SoNgayToiDa is <= 0)
        {
            throw new ArgumentException(
                "Số ngày phải lớn hơn 0.");
        }

        if (request.SoKhach is <= 0)
        {
            throw new ArgumentException(
                "Số khách phải lớn hơn 0.");
        }

        int topN = Math.Clamp(
            request.TopN,
            1,
            20);

        HashSet<int> soThichDaChon =
            (request.IdSoThich ?? new List<int>())
                .Where(id => id > 0)
                .ToHashSet();

        bool coSoThich =
            soThichDaChon.Count > 0;

        bool coLoaiTour =
            !string.IsNullOrWhiteSpace(request.LoaiTour);

        bool coNgay =
            request.NgayDuKien.HasValue;

        bool coSoKhach =
            request.SoKhach.HasValue;

        // Chi yeu cau lich thuc te khi can xac nhan
        // ngay hoac so cho.
        //
        // Neu khach chi hoi tim tour, he thong van
        // cho phep kham pha danh muc tour.
        // Tuy nhien KHONG khang dinh tour co lich
        // trong tuong lai khi chua kiem tra.

        bool canLocTheoLich =
            coNgay;

        DateOnly homNay = DateOnly.FromDateTime(
            DateTime.UtcNow.AddHours(7));

        // Khong co lich khoi hanh qua khu.

        if (request.NgayDuKien.HasValue &&
            request.NgayDuKien.Value < homNay)
        {
            return new List<MatchingScoreResult>();
        }

        string loaiNganSach =
            NormalizeText(request.LoaiNganSach);

        bool nganSachCaNhom =
            loaiNganSach == "canhom";

        // Neu ngan sach la cua ca nhom,
        // can biet so nguoi moi co the
        // uoc tinh tong chi phi.
        //
        // Khong tu gia dinh nhom co bao nhieu nguoi.

        if (nganSachCaNhom &&
            request.NganSachToiDa.HasValue &&
            !request.SoKhach.HasValue)
        {
            return new List<MatchingScoreResult>();
        }

        string? loaiTourYeuCau =
            request.LoaiTour?.Trim();

        // ====================================================
        // 3. LOAD DU LIEU
        // ====================================================

        var tours =
            new Dictionary<int, TourMatchingData>();

        DbConnection connection =
            _db.Database.GetDbConnection();

        bool openedHere =
            connection.State != ConnectionState.Open;

        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            // ================================================
            // 3.1. TOUR + DIEM DEN + LOAI TOUR
            // ================================================

            await using (DbCommand command =
                connection.CreateCommand())
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
                        td.VaiTro,
                        t.IdLoaiTour,
                        lt.TenLoai
                    FROM dbo.Tour AS t
                    LEFT JOIN dbo.AITourDiemDen AS td
                        ON td.IdTour = t.IdTour
                    LEFT JOIN dbo.LoaiTour AS lt
                        ON lt.IdLoaiTour = t.IdLoaiTour
                    WHERE t.TrangThai = 1
                      AND COALESCE(
                          NULLIF(t.GiaKhuyenMai, 0),
                          NULLIF(t.GiaGoc, 0)
                      ) > 0
                    ORDER BY t.IdTour;
                    """;

                await using DbDataReader reader =
                    await command.ExecuteReaderAsync(
                        cancellationToken);

                while (await reader.ReadAsync(
                    cancellationToken))
                {
                    int idTour =
                        reader.GetInt32(0);

                    if (!tours.TryGetValue(
                        idTour,
                        out TourMatchingData? tour))
                    {
                        string thoiGian =
                            ReadText(reader, 4);

                        tour = new TourMatchingData
                        {
                            IdTour = idTour,

                            TenTour =
                                ReadText(reader, 1),

                            DiaDiem =
                                ReadText(reader, 2),

                            HinhAnh =
                                ReadText(reader, 3),

                            ThoiGian = thoiGian,

                            SoNgay =
                                ParseSoNgay(thoiGian),

                            GiaBan =
                                reader.GetDecimal(5),

                            IdLoaiTour =
                                reader.IsDBNull(8)
                                    ? 0
                                    : reader.GetInt32(8),

                            TenLoaiTour =
                                ReadText(reader, 9)
                        };

                        tours.Add(idTour, tour);
                    }

                    if (!reader.IsDBNull(6))
                    {
                        string vaiTro =
                            ReadText(reader, 7);

                        if (IsDiemDenRole(vaiTro))
                        {
                            tour.DiemDenIds.Add(
                                reader.GetInt32(6));
                        }
                    }
                }
            }

            // ================================================
            // 3.2. TOUR + SO THICH
            // ================================================

            await using (DbCommand command =
                connection.CreateCommand())
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

                await using DbDataReader reader =
                    await command.ExecuteReaderAsync(
                        cancellationToken);

                while (await reader.ReadAsync(
                    cancellationToken))
                {
                    int idTour =
                        reader.GetInt32(0);

                    int idSoThich =
                        reader.GetInt32(1);

                    if (tours.TryGetValue(
                        idTour,
                        out TourMatchingData? tour))
                    {
                        tour.SoThichIds.Add(idSoThich);
                    }
                }
            }

            // ================================================
            // 3.3. LICH KHOI HANH
            // ================================================

            if (canLocTheoLich)
            {
                await using DbCommand command =
                    connection.CreateCommand();

                command.CommandText = """
                    SELECT
                        l.IdTour,
                        l.IdLich,
                        l.NgayKhoiHanh,
                        l.SoChoToiDa,
                        l.SoChoConLai,
                        l.TrangThai
                    FROM dbo.LichKhoiHanh AS l
                    INNER JOIN dbo.Tour AS t
                        ON t.IdTour = l.IdTour
                    WHERE t.TrangThai = 1
                      AND l.SoChoToiDa > 0
                      AND l.SoChoConLai > 0
                      AND l.SoChoConLai <= l.SoChoToiDa
                      AND l.NgayKhoiHanh >= @NgayHomNay
                      AND l.TrangThai IN (
                          N'Đang mở',
                          N'Còn chỗ',
                          N'Sắp hết chỗ'
                      )
                    ORDER BY
                        l.IdTour,
                        l.NgayKhoiHanh,
                        l.IdLich;
                    """;

                DbParameter parameter =
                    command.CreateParameter();

                parameter.ParameterName =
                    "@NgayHomNay";

                parameter.DbType =
                    DbType.Date;

                parameter.Value =
                    homNay.ToDateTime(TimeOnly.MinValue);

                command.Parameters.Add(parameter);

                await using DbDataReader reader =
                    await command.ExecuteReaderAsync(
                        cancellationToken);

                while (await reader.ReadAsync(
                    cancellationToken))
                {
                    int idTour =
                        reader.GetInt32(0);

                    if (!tours.TryGetValue(
                        idTour,
                        out TourMatchingData? tour))
                    {
                        continue;
                    }

                    DateOnly? ngayKhoiHanh =
                        ReadDateOnly(reader, 2);

                    if (!ngayKhoiHanh.HasValue)
                    {
                        continue;
                    }

                    if (ngayKhoiHanh.Value < homNay)
                    {
                        continue;
                    }

                    int soChoToiDa =
                        reader.GetInt32(3);

                    int soChoConLai =
                        reader.GetInt32(4);

                    if (soChoToiDa <= 0 ||
                        soChoConLai <= 0 ||
                        soChoConLai > soChoToiDa)
                    {
                        continue;
                    }

                    tour.LichKhoiHanhs.Add(
                        new LichMatchingData
                        {
                            IdLich =
                                reader.GetInt32(1),

                            NgayKhoiHanh =
                                ngayKhoiHanh.Value,

                            SoChoConLai =
                                soChoConLai,

                            TrangThai =
                                ReadText(reader, 5)
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

        // ====================================================
        // 4. FILTER + MATCHING SCORE
        // ====================================================

        var results =
            new List<MatchingScoreResult>();

        foreach (TourMatchingData tour in tours.Values)
        {
            // ================================================
            // 4.1. DIEM DEN
            // ================================================

            if (request.IdDiemDen.HasValue &&
                !tour.DiemDenIds.Contains(
                    request.IdDiemDen.Value))
            {
                continue;
            }

            // ================================================
            // 4.2. NGAN SACH
            // ================================================

            if (request.NganSachToiDa.HasValue)
            {
                decimal giaCanSoSanh =
                    tour.GiaBan;

                if (nganSachCaNhom)
                {
                    // Gia tour dang dung la gia mot nguoi.
                    // Uoc tinh nhom theo so khach.
                    //
                    // Luu y: neu nhom co tre em,
                    // can lay them gia theo do tuoi
                    // tu nghiep vu dat tour de tinh chinh xac.

                    int soKhach =
                        request.SoKhach!.Value;

                    giaCanSoSanh =
                        tour.GiaBan * soKhach;
                }

                if (giaCanSoSanh >
                    request.NganSachToiDa.Value)
                {
                    continue;
                }
            }

            // ================================================
            // 4.3. SO NGAY
            // ================================================

            if (request.SoNgayToiDa.HasValue)
            {
                if (!tour.SoNgay.HasValue ||
                    tour.SoNgay.Value >
                        request.SoNgayToiDa.Value)
                {
                    continue;
                }
            }

            // ================================================
            // 4.4. SO THICH
            // ================================================

            int soThichTrung =
                tour.SoThichIds.Count(
                    id => soThichDaChon.Contains(id));

            if (coSoThich &&
                soThichTrung == 0)
            {
                continue;
            }

            // ================================================
            // 4.5. LOAI TOUR
            // ================================================

            if (coLoaiTour &&
                !string.Equals(
                    NormalizeText(tour.TenLoaiTour),
                    NormalizeText(loaiTourYeuCau),
                    StringComparison.Ordinal))
            {
                continue;
            }

            // ================================================
            // 4.6. LICH + SO KHACH
            // ================================================

            LichMatchingData? lichPhuHop = null;

            if (canLocTheoLich)
            {
                IEnumerable<LichMatchingData> lichHopLe =
                    tour.LichKhoiHanhs;

                if (request.NgayDuKien.HasValue)
                {
                    DateOnly ngayYeuCau =
                        request.NgayDuKien.Value;

                    lichHopLe = lichHopLe.Where(
                        lich =>
                            lich.NgayKhoiHanh ==
                            ngayYeuCau);
                }

                if (request.SoKhach.HasValue)
                {
                    int soKhach =
                        request.SoKhach.Value;

                    lichHopLe = lichHopLe.Where(
                        lich =>
                            lich.SoChoConLai >= soKhach);
                }

                lichPhuHop = lichHopLe
                    .OrderBy(
                        lich => lich.NgayKhoiHanh)
                    .ThenBy(
                        lich => lich.IdLich)
                    .FirstOrDefault();

                if (lichPhuHop is null)
                {
                    continue;
                }
            }

            // ================================================
            // 5. TINH MATCHING SCORE
            // ================================================
            //
            // SO THICH: 50
            // DIEM DEN: 30
            // NGAN SACH: 20
            //
            // SO NGAY, LOAI TOUR, LICH, SO KHACH
            // LA DIEU KIEN LOC.
            //
            // KHONG TU THEM TRONG SO MOI.

            var lyDo =
                new List<string>();

            // 5.1. SO THICH - TOI DA 50

            int diemSoThich = 0;

            if (soThichDaChon.Count > 0)
            {
                diemSoThich = (int)Math.Round(
                    (double)DiemToiDaSoThich *
                    soThichTrung /
                    soThichDaChon.Count,
                    MidpointRounding.AwayFromZero);

                if (soThichTrung > 0)
                {
                    lyDo.Add(
                        $"Phù hợp {soThichTrung}/" +
                        $"{soThichDaChon.Count} " +
                        "sở thích đã chọn");
                }
            }

            // 5.2. DIEM DEN - TOI DA 30

            int diemDiemDen = 0;

            if (request.IdDiemDen.HasValue &&
                tour.DiemDenIds.Contains(
                    request.IdDiemDen.Value))
            {
                diemDiemDen =
                    DiemToiDaDiemDen;

                lyDo.Add(
                    "Có điểm tham quan đúng với " +
                    "điểm đến mong muốn");
            }

            // 5.3. NGAN SACH - TOI DA 20

            int diemNganSach = 0;

            if (request.NganSachToiDa.HasValue)
            {
                diemNganSach =
                    DiemToiDaNganSach;

                lyDo.Add(
                    nganSachCaNhom
                        ? "Chi phí nhóm ước tính " +
                          "nằm trong ngân sách"
                        : "Giá tour nằm trong " +
                          "ngân sách mỗi người");
            }

            // ================================================
            // 5.4. GIAI THICH CAC DIEU KIEN LOC
            // ================================================

            if (request.SoNgayToiDa.HasValue &&
                tour.SoNgay.HasValue)
            {
                lyDo.Add(
                    $"Thời gian {tour.SoNgay.Value} ngày " +
                    "đáp ứng giới hạn chuyến đi");
            }

            if (coLoaiTour)
            {
                lyDo.Add(
                    $"Thuộc loại tour {tour.TenLoaiTour}");
            }

            if (lichPhuHop is not null)
            {
                string ngay =
                    lichPhuHop.NgayKhoiHanh.ToString(
                        "dd/MM/yyyy",
                        CultureInfo.InvariantCulture);

                lyDo.Add(
                    $"Có lịch khởi hành ngày {ngay}");

                if (request.SoKhach.HasValue)
                {
                    lyDo.Add(
                        $"Lịch còn " +
                        $"{lichPhuHop.SoChoConLai} chỗ, " +
                        $"đáp ứng {request.SoKhach.Value} khách");
                }
            }

            if (lyDo.Count == 0)
            {
                lyDo.Add(
                    "Tour đang hoạt động " +
                    "và có giá bán hợp lệ");
            }

            int tongDiem =
                diemSoThich +
                diemDiemDen +
                diemNganSach;

            // ================================================
            // 6. TAO KET QUA
            // ================================================

            results.Add(
                new MatchingScoreResult
                {
                    IdTour = tour.IdTour,

                    TenTour = tour.TenTour,

                    DiaDiem = tour.DiaDiem,

                    HinhAnh = tour.HinhAnh,

                    ThoiGian = tour.ThoiGian,

                    GiaBan = tour.GiaBan,

                    SoNgay = tour.SoNgay,

                    DiemSoThich =
                        diemSoThich,

                    DiemDiemDen =
                        diemDiemDen,

                    DiemNganSach =
                        diemNganSach,

                    MatchingScore =
                        tongDiem,

                    LyDoGoiY =
                        lyDo,

                    TenLoaiTour =
                        tour.TenLoaiTour,

                    NgayKhoiHanhPhuHop =
                        lichPhuHop?.NgayKhoiHanh,

                    SoChoConLai =
                        lichPhuHop?.SoChoConLai
                });
        }

        // ====================================================
        // 7. RANKING TOP-N
        // ====================================================

        return results
            .OrderByDescending(
                tour => tour.MatchingScore)
            .ThenBy(
                tour => tour.GiaBan)
            .ThenBy(
                tour => tour.IdTour)
            .Take(topN)
            .ToList();
    }

    // ========================================================
    // HELPERS
    // ========================================================

    private static bool IsDiemDenRole(
        string? value)
    {
        return NormalizeText(value) == "diemden";
    }

    private static string NormalizeText(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        string normalized = value
            .Trim()
            .ToLowerInvariant()
            .Replace('đ', 'd')
            .Normalize(
                NormalizationForm.FormD);

        var builder = new StringBuilder();

        foreach (char character in normalized)
        {
            UnicodeCategory category =
                CharUnicodeInfo.GetUnicodeCategory(
                    character);

            if (category !=
                UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return Regex.Replace(
                builder.ToString()
                    .Normalize(
                        NormalizationForm.FormC),
                @"[^a-z0-9]+",
                "")
            .Trim();
    }

    private static string ReadText(
        DbDataReader reader,
        int ordinal)
    {
        return reader.IsDBNull(ordinal)
            ? ""
            : Convert.ToString(
                reader.GetValue(ordinal),
                CultureInfo.InvariantCulture) ?? "";
    }

    private static DateOnly? ReadDateOnly(
        DbDataReader reader,
        int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        object value =
            reader.GetValue(ordinal);

        if (value is DateOnly dateOnly)
        {
            return dateOnly;
        }

        if (value is DateTime dateTime)
        {
            return DateOnly.FromDateTime(
                dateTime);
        }

        if (DateOnly.TryParse(
            Convert.ToString(
                value,
                CultureInfo.InvariantCulture),
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out DateOnly parsed))
        {
            return parsed;
        }

        return null;
    }

    // ========================================================
    // PARSE SO NGAY
    // ========================================================

    private static int? ParseSoNgay(
        string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        string normalized = text
            .Trim()
            .Normalize(
                NormalizationForm.FormC);

        // Ho tro:
        // 2 ngay 1 dem
        // 2 ngày 1 đêm
        // 2N1D
        // 2N1Đ
        // 3 ngay
        // 1 ngày

        Match match = Regex.Match(
            normalized,
            @"(?<!\d)(\d+)\s*(?:ngày|ngay|n)(?![a-z])",
            RegexOptions.IgnoreCase |
            RegexOptions.CultureInvariant);

        if (!match.Success)
        {
            return null;
        }

        if (!int.TryParse(
            match.Groups[1].Value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out int days))
        {
            return null;
        }

        return days is >= 1 and <= 365
            ? days
            : null;
    }

    // ========================================================
    // INTERNAL DATA
    // ========================================================

    private sealed class TourMatchingData
    {
        public int IdTour { get; set; }

        public string TenTour { get; set; } = "";

        public string DiaDiem { get; set; } = "";

        public string HinhAnh { get; set; } = "";

        public string ThoiGian { get; set; } = "";

        public decimal GiaBan { get; set; }

        public int? SoNgay { get; set; }

        public int IdLoaiTour { get; set; }

        public string TenLoaiTour { get; set; } = "";

        public HashSet<int> DiemDenIds { get; } = new();

        public HashSet<int> SoThichIds { get; } = new();

        public List<LichMatchingData> LichKhoiHanhs { get; }
            = new();
    }

    private sealed class LichMatchingData
    {
        public int IdLich { get; set; }

        public DateOnly NgayKhoiHanh { get; set; }

        public int SoChoConLai { get; set; }

        public string TrangThai { get; set; } = "";
    }
}
