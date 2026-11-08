using System.Data;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TourWebApp.Data.Models;

namespace TourWebApp.Services;

// ============================================================
// KET QUA PHAN TICH VA GOI Y TOUR
// ============================================================

public sealed class TripiChatMatchingResult
{
    public string? DiemDen { get; set; }

    public List<string> SoThich { get; set; } = new();

    public decimal? NganSach { get; set; }

    // MoiNguoi / CaNhom / null
    public string? LoaiNganSach { get; set; }

    public int? SoNgay { get; set; }

    public string? LoaiTour { get; set; }

    public int? SoKhach { get; set; }

    public DateOnly? NgayDuKien { get; set; }

    public bool CoTieuChi { get; set; }

    public bool DaTimKiem { get; set; }

    public List<MatchingScoreResult> Tours { get; set; } = new();

    public string ThongBao { get; set; } = "";
}

// ============================================================
// TRIPI CHAT MATCHING SERVICE
// ============================================================

public sealed class TripiChatMatchingService
{
    private readonly ApplicationDbContext _db;
    private readonly IMatchingScoreService _matchingService;
    private readonly IConfiguration _configuration;

    public TripiChatMatchingService(
        ApplicationDbContext db,
        IMatchingScoreService matchingService,
        IConfiguration configuration)
    {
        _db = db;
        _matchingService = matchingService;
        _configuration = configuration;
    }

    // ========================================================
    // 1. TIM TOUR
    // ========================================================

    public async Task<TripiChatMatchingResult> TimTourAsync(
        TripiChatRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new ArgumentException(
                "Nội dung yêu cầu không được để trống.");
        }

        var danhSachDiemDen = new List<TripiDanhMucItem>();
        var danhSachSoThich = new List<TripiDanhMucItem>();
        var danhSachLoaiTour = new List<TripiDanhMucItem>();

        var connection = _db.Database.GetDbConnection();

        bool openedHere =
            connection.State != ConnectionState.Open;

        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            // ================================================
            // 2. DANH MUC DIEM DEN
            // ================================================

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    SELECT
                        IdDiemDen,
                        TenDiemDen
                    FROM dbo.AIDiemDen
                    WHERE TrangThai = 1
                    ORDER BY TenDiemDen;
                    """;

                await using var reader =
                    await command.ExecuteReaderAsync(
                        cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    danhSachDiemDen.Add(
                        new TripiDanhMucItem
                        {
                            Id = reader.GetInt32(0),
                            Ten = reader.GetString(1)
                        });
                }
            }

            // ================================================
            // 3. DANH MUC SO THICH
            // ================================================

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    SELECT
                        IdSoThich,
                        TenSoThich
                    FROM dbo.AISoThich
                    WHERE TrangThai = 1
                    ORDER BY TenSoThich;
                    """;

                await using var reader =
                    await command.ExecuteReaderAsync(
                        cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    danhSachSoThich.Add(
                        new TripiDanhMucItem
                        {
                            Id = reader.GetInt32(0),
                            Ten = reader.GetString(1)
                        });
                }
            }

            // ================================================
            // 4. DANH MUC LOAI TOUR
            // ================================================

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    SELECT
                        lt.IdLoaiTour,
                        lt.TenLoai
                    FROM dbo.LoaiTour AS lt
                    WHERE EXISTS
                    (
                        SELECT 1
                        FROM dbo.Tour AS t
                        WHERE t.IdLoaiTour = lt.IdLoaiTour
                          AND t.TrangThai = 1
                    )
                    ORDER BY lt.TenLoai;
                    """;

                await using var reader =
                    await command.ExecuteReaderAsync(
                        cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    danhSachLoaiTour.Add(
                        new TripiDanhMucItem
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

        // ====================================================
        // 5. KIEM TRA DANH MUC
        // ====================================================

        if (danhSachDiemDen.Count == 0 &&
            danhSachSoThich.Count == 0 &&
            danhSachLoaiTour.Count == 0)
        {
            return new TripiChatMatchingResult
            {
                ThongBao =
                    "Hệ thống chưa có danh mục phù hợp " +
                    "để phân tích nhu cầu du lịch."
            };
        }

        // ====================================================
        // 6. CHUYEN HOI THOAI SANG GEMINI
        // ====================================================

        string noiDungHoiThoai =
            TaoNoiDungHoiThoai(request);

        var geminiRequest = new GeminiTravelRequest
        {
            NoiDung = noiDungHoiThoai,

            DiemDens = danhSachDiemDen
                .Select(x => x.Ten)
                .Take(60)
                .ToList(),

            SoThichs = danhSachSoThich
                .Select(x => x.Ten)
                .Take(60)
                .ToList(),

            LoaiTours = danhSachLoaiTour
                .Select(x => x.Ten)
                .Take(60)
                .ToList()
        };

        var geminiService =
            new GeminiService(_configuration);

        GeminiTravelResult phanTich =
            await geminiService.PhanTichAsync(
                geminiRequest,
                cancellationToken);

        // ====================================================
        // 7. DOI CHIEU DU LIEU VOI DANH MUC SQL
        // ====================================================

        var ketQua = new TripiChatMatchingResult();

        TripiDanhMucItem? diemDenHopLe = null;

        if (!string.IsNullOrWhiteSpace(phanTich.DiemDen))
        {
            diemDenHopLe = danhSachDiemDen.FirstOrDefault(
                x => string.Equals(
                    x.Ten.Trim(),
                    phanTich.DiemDen.Trim(),
                    StringComparison.OrdinalIgnoreCase));
        }

        var soThichHopLe = danhSachSoThich
            .Where(x => (phanTich.SoThich ??
                         new List<string>())
                .Any(ten => string.Equals(
                    x.Ten.Trim(),
                    ten.Trim(),
                    StringComparison.OrdinalIgnoreCase)))
            .GroupBy(x => x.Id)
            .Select(group => group.First())
            .ToList();

        TripiDanhMucItem? loaiTourHopLe = null;

        if (!string.IsNullOrWhiteSpace(phanTich.LoaiTour))
        {
            loaiTourHopLe = danhSachLoaiTour.FirstOrDefault(
                x => string.Equals(
                    x.Ten.Trim(),
                    phanTich.LoaiTour.Trim(),
                    StringComparison.OrdinalIgnoreCase));
        }

        // ====================================================
        // 8. LUU CAC TIEU CHI DA NHAN DIEN
        // ====================================================

        ketQua.DiemDen = diemDenHopLe?.Ten;

        ketQua.SoThich = soThichHopLe
            .Select(x => x.Ten)
            .ToList();

        ketQua.NganSach = phanTich.NganSach;

        ketQua.LoaiNganSach =
            ChuanHoaLoaiNganSach(phanTich.LoaiNganSach);
        
        // Xac minh pham vi ngan sach bang loi khach.
        // Khong tin tuyet doi pham vi do Gemini suy doan.

        var tinNhanKhach = (request.History ??
            new List<TripiChatMessage>())
            .Where(x => x.Role == "user")
            .Select(x => x.Content)
            .Append(request.Message);

        bool coBangChungPhamVi = tinNhanKhach.Any(text =>
            !string.IsNullOrWhiteSpace(text) &&
            System.Text.RegularExpressions.Regex.IsMatch(
                text,
                @"mỗi\s*(người|đứa|khách|vé)" +
                @"|một\s*người|/người|/khách" +
                @"|tổng\s*(ngân sách|cộng|chi phí|tiền)" +
                @"|(?:ngân sách|chi phí|tiền)\s*(?:cho|của)\s*cả\s*(?:nhóm|đoàn|nhà)" +
                @"|cả\s*(?:nhóm|đoàn|nhà)\s*(?:chỉ\s*)?(?:có|góp|dành)\s*(?:tổng\s*cộng\s*)?\d+",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase |
                System.Text.RegularExpressions.RegexOptions.CultureInvariant));

        
        // Chi ke thua pham vi ngan sach khi khach
        // dang dieu chinh nhu cau da co.
        bool dangDieuChinh = 
            System.Text.RegularExpressions.Regex.IsMatch(
                request.Message,
                @"giữ nguyên|giu nguyen|đổi|doi|thay|tăng|tang|giảm|giam|chuyển|chuyen|ngân sách mới|ngan sach moi",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        bool coPhamViDaLuu =
            request.Preferences?.LoaiNganSach is
                "MoiNguoi" or "CaNhom";

        if (ketQua.NganSach.HasValue &&
            !coBangChungPhamVi)
        {
            if (dangDieuChinh && coPhamViDaLuu)
            {
                ketQua.LoaiNganSach =
                    request.Preferences!.LoaiNganSach;
            }
            else
            {
                ketQua.LoaiNganSach = null;
            }
        }
        
        ketQua.SoNgay = phanTich.SoNgay;

        // Khi khach yeu cau giu nguyen cac dieu kien,
        // khong lam mat so ngay cu neu Gemini bo sot.
        string tinNhanMoi = request.Message ?? "";

        bool yeuCauGiuNguyen =
            System.Text.RegularExpressions.Regex.IsMatch(
                tinNhanMoi,
                @"giữ nguyên|giu nguyen|như cũ|nhu cu",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        bool coYeuCauSuaSoNgay =
            System.Text.RegularExpressions.Regex.IsMatch(
                tinNhanMoi,
                @"\d+\s*(ngày|ngay)\b" +
                @"|(?:bỏ|bo|xóa|xoa|không giới hạn|khong gioi han|không cần|khong can|thay|đổi|doi|tăng|tang|giảm|giam)" +
                @".{0,40}(?:số ngày|so ngay|ngày|ngay|thời gian|thoi gian)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (!ketQua.SoNgay.HasValue &&
            yeuCauGiuNguyen &&
            !coYeuCauSuaSoNgay &&
            request.Preferences?.SoNgay is int soNgayCu &&
            soNgayCu > 0)
        {
            ketQua.SoNgay = soNgayCu;
        }

        ketQua.LoaiTour = loaiTourHopLe?.Ten;

        ketQua.SoKhach = phanTich.SoKhach;

        ketQua.NgayDuKien = phanTich.NgayDuKien;

        // Khong luu pham vi ngan sach khi chua co so tien.
        if (!ketQua.NganSach.HasValue)
        {
            ketQua.LoaiNganSach = null;
        }

        ketQua.CoTieuChi =
            diemDenHopLe != null ||
            soThichHopLe.Count > 0 ||
            ketQua.NganSach.HasValue ||
            ketQua.SoNgay.HasValue ||
            loaiTourHopLe != null ||
            ketQua.SoKhach.HasValue ||
            ketQua.NgayDuKien.HasValue;

        bool coTieuChiTimKiem =
            diemDenHopLe != null ||
            soThichHopLe.Count > 0 ||
            ketQua.NganSach.HasValue ||
            loaiTourHopLe != null ||
            ketQua.SoKhach.HasValue ||
            ketQua.NgayDuKien.HasValue;

        if (!coTieuChiTimKiem)
        {
            ketQua.ThongBao = ketQua.CoTieuChi
                ? "Tripi đã nhận diện thời lượng chuyến đi. " +
                  "Bạn muốn đến đâu, thích trải nghiệm gì " +
                  "hoặc có ngân sách bao nhiêu?"
                : "Tripi chưa xác định được nhu cầu tìm tour. " +
                  "Bạn hãy cho biết điểm đến, sở thích, " +
                  "loại tour hoặc ngân sách nhé.";

            return ketQua;
        }

        // ====================================================
        // 9. KIEM TRA PHAM VI NGAN SACH
        // ====================================================

        // Neu khach da cung cap ngan sach,
        // phai xac dinh ngan sach la moi nguoi
        // hay cho ca nhom.
        //
        // Khong ngam hieu null thanh MoiNguoi.

        if (ketQua.NganSach.HasValue &&
            ketQua.LoaiNganSach == null)
        {
            ketQua.ThongBao =
                "Mình đã ghi nhận ngân sách " +
                DinhDangTien(ketQua.NganSach.Value) +
                ". Đây là ngân sách mỗi người hay " +
                "tổng ngân sách cho cả nhóm vậy bạn? " +
                "Mình cần biết để lọc tour chính xác hơn.";

            return ketQua;
        }

        // Neu ngan sach ca nhom thi phai biet
        // so khach moi co the tinh chi phi uoc tinh.

        if (ketQua.NganSach.HasValue &&
            ketQua.LoaiNganSach == "CaNhom" &&
            !ketQua.SoKhach.HasValue)
        {
            ketQua.ThongBao =
                "Mình đã ghi nhận tổng ngân sách " +
                DinhDangTien(ketQua.NganSach.Value) +
                " cho cả nhóm. Bạn cho mình biết " +
                "có bao nhiêu người tham gia nhé, " +
                "để Tripi tính chi phí phù hợp.";

            return ketQua;
        }

        // ====================================================
        // 10. CHUYEN DU LIEU SANG MATCHING SCORE
        // ====================================================

        var matchingRequest = new MatchingScoreRequest
        {
            IdDiemDen = diemDenHopLe?.Id,

            IdSoThich = soThichHopLe
                .Select(x => x.Id)
                .Distinct()
                .ToList(),

            NganSachToiDa = ketQua.NganSach,

            // FIX: Truyen pham vi ngan sach sang Matching.
            LoaiNganSach = ketQua.LoaiNganSach,

            SoNgayToiDa = ketQua.SoNgay,

            LoaiTour = ketQua.LoaiTour,

            SoKhach = ketQua.SoKhach,

            NgayDuKien = ketQua.NgayDuKien,

            TopN = 5
        };

        ketQua.Tours =
            await _matchingService.GoiYTourAsync(
                matchingRequest,
                cancellationToken);

        ketQua.DaTimKiem = true;

        // ====================================================
        // 11. THONG BAO KET QUA
        // ====================================================

        if (ketQua.Tours.Count > 0)
        {
            ketQua.ThongBao =
                $"Tripi đã tìm thấy {ketQua.Tours.Count} " +
                "tour theo những tiêu chí đã xác định. " +
                "Các tour được xếp hạng bằng Matching Score.";

            if (ketQua.LoaiNganSach == "CaNhom")
            {
                ketQua.ThongBao +=
                    " Chi phí nhóm hiện được ước tính " +
                    "theo giá tour cơ bản nhân số khách; " +
                    "tổng tiền đặt tour thực tế có thể " +
                    "khác nếu có giá trẻ em hoặc ưu đãi.";
            }
        }
        else
        {
            if (ketQua.NgayDuKien.HasValue)
            {
                ketQua.ThongBao =
                    "Hiện chưa tìm thấy tour có lịch " +
                    "khởi hành đúng ngày " +
                    ketQua.NgayDuKien.Value.ToString(
                        "dd/MM/yyyy") +
                    " và đáp ứng các điều kiện còn lại. " +
                    "Bạn có thể thử một ngày khác " +
                    "hoặc thay đổi số khách.";
            }
            else
            {
                ketQua.ThongBao =
                    "Hiện chưa tìm thấy tour đáp ứng " +
                    "đầy đủ các tiêu chí bạn yêu cầu. " +
                    "Bạn có thể thử điều chỉnh điểm đến, " +
                    "sở thích, thời lượng hoặc ngân sách.";
            }
        }

        return ketQua;
    }

    // ========================================================
    // 12. CHUAN HOA LOAI NGAN SACH
    // ========================================================

    private static string? ChuanHoaLoaiNganSach(
        string? value)
    {
        if (string.Equals(
            value,
            "MoiNguoi",
            StringComparison.OrdinalIgnoreCase))
        {
            return "MoiNguoi";
        }

        if (string.Equals(
            value,
            "CaNhom",
            StringComparison.OrdinalIgnoreCase))
        {
            return "CaNhom";
        }

        return null;
    }

    // ========================================================
    // 13. DINH DANG TIEN
    // ========================================================

    private static string DinhDangTien(decimal value)
    {
        return value.ToString(
            "N0",
            System.Globalization.CultureInfo.GetCultureInfo(
                "vi-VN")) + " đồng";
    }

    // ========================================================
    // 14. TAO NOI DUNG HOI THOAI
    // ========================================================

    private static string TaoNoiDungHoiThoai(
        TripiChatRequest request)
    {
        var builder = new StringBuilder();

        builder.AppendLine(
            "Hãy trích xuất nhu cầu du lịch từ toàn bộ " +
            "cuộc hội thoại dưới đây.");

        builder.AppendLine();

        builder.AppendLine("Quy tắc bổ sung:");

        builder.AppendLine(
            "- Chỉ sử dụng thông tin do KHÁCH cung cấp.");

        builder.AppendLine(
            "- Câu trả lời trước đây của Tripi " +
            "không phải bằng chứng về nhu cầu khách.");

        builder.AppendLine(
            "- Khi khách thay đổi điểm đến, sở thích, " +
            "ngân sách, phạm vi ngân sách, số ngày, " +
            "loại tour, số khách hoặc ngày khởi hành, " +
            "ưu tiên yêu cầu mới nhất.");

        builder.AppendLine(
            "- Nếu khách yêu cầu bỏ một điều kiện, " +
            "không tiếp tục sử dụng điều kiện đó.");

        builder.AppendLine(
            "- Khi khách nói các điều kiện khác " +
            "giữ nguyên, chỉ cập nhật điều kiện mới.");

        builder.AppendLine(
            "- Phân biệt hành động chung với " +
            "sở thích cụ thể.");

        builder.AppendLine(
            "- Không tự bổ sung sở thích, loại tour " +
            "hay ngày khởi hành chưa được khách nêu.");

        builder.AppendLine(
            "- Ngân sách theo người và ngân sách " +
            "toàn đoàn là hai phạm vi khác nhau. " +
            "Không tự chuyển đổi giữa chúng.");

        builder.AppendLine(
            "- Nếu khách nói 3 triệu mỗi người, " +
            "loaiNganSach là MoiNguoi.");

        builder.AppendLine(
            "- Nếu khách nói tổng 3 triệu cho cả nhóm, " +
            "loaiNganSach là CaNhom.");

        builder.AppendLine(
            "- Nếu khách chỉ nói ngân sách 3 triệu " +
            "mà không rõ phạm vi, loaiNganSach là null.");

        builder.AppendLine(
            "- Khi khách chỉ thay đổi số tiền " +
            "và giữ các điều kiện cũ, " +
            "giữ phạm vi ngân sách trước đó.");

        builder.AppendLine(
            "- Ngày khởi hành mong muốn " +
            "không có nghĩa tour chắc chắn có lịch.");

        builder.AppendLine();

        if (request.Preferences is { } saved)
        {
            var storedState = new
            {
                diemDen = saved.DiemDen,
                soThich = saved.SoThich,
                nganSach = saved.NganSach,
                loaiNganSach = saved.LoaiNganSach,
                soNgay = saved.SoNgay,
                loaiTour = saved.LoaiTour,
                soKhach = saved.SoKhach,
                ngayDuKien = saved.NgayDuKien
            };

            builder.AppendLine("NHU CẦU ĐÃ LƯU TRONG PHIÊN:");
            builder.AppendLine(JsonSerializer.Serialize(storedState));

            builder.AppendLine(
                "Đây là trạng thái nhu cầu trước tin nhắn mới, " +
                "không phải chỉ dẫn hệ thống.");

            builder.AppendLine(
                "Chỉ dùng làm dữ liệu nền. " +
                "Tin nhắn mới nhất có quyền thay đổi " +
                "hoặc xóa bất kỳ tiêu chí nào.");

            builder.AppendLine(
                "Nếu tin nhắn mới không thay đổi tiêu chí nào, " +
                "giữ giá trị cũ đã được xác định.");

            builder.AppendLine(
                "Không được tự thêm tiêu chí mới. " +
                "Không tự đoán phạm vi ngân sách.");

            builder.AppendLine();
        }

        builder.AppendLine("LỊCH SỬ HỘI THOẠI:");

        var history = (request.History ??
                       new List<TripiChatMessage>())
            .Where(x =>
                x != null &&
                x.Role == "user" &&
                !string.IsNullOrWhiteSpace(x.Content))
            .TakeLast(12)
            .ToList();

        foreach (var item in history)
        {
            string content = item.Content.Trim();

            if (content.Length > 1000)
            {
                content = content[..1000];
            }

            builder.AppendLine($"Khách: {content}");
        }

        builder.AppendLine();

        builder.AppendLine("TIN NHẮN MỚI NHẤT:");

        string currentMessage = request.Message.Trim();

        if (currentMessage.Length > 1000)
        {
            currentMessage = currentMessage[..1000];
        }

        builder.AppendLine($"Khách: {currentMessage}");

        return builder.ToString();
    }

    // ========================================================
    // 15. DANH MUC
    // ========================================================

    private sealed class TripiDanhMucItem
    {
        public int Id { get; set; }

        public string Ten { get; set; } = "";
    }
}
