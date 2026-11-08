using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using TourWebApp.Data.Models;
using TourWebApp.Helpers;
using TourWebApp.Services;

namespace TourWebApp.Controllers;

// ============================================================
// API REQUEST
// ============================================================

public sealed class TripiChatApiRequest
{
    public string Message { get; set; } = "";

    public List<TripiChatMessage> History { get; set; } = new();

    public int? IdTourDangXem { get; set; }
    public TripiTravelPreferences? Preferences { get; set; }
}

// ============================================================
// TRIPI CHAT CONTROLLER
// ============================================================

[ApiController]
[Route("api/tripi-chat")]
public sealed class TripiChatController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TripiChatController> _logger;
    private readonly ApplicationDbContext _db;
    private readonly IMatchingScoreService _matchingService;

    private const int MaxMessageLength = 1000;
    private const int MaxHistoryMessages = 12;
    private const int MaxRequestCharacters = 16000;

    public TripiChatController(
        IConfiguration configuration,
        ILogger<TripiChatController> logger,
        ApplicationDbContext db,
        IMatchingScoreService matchingService)
    {
        _configuration = configuration;
        _logger = logger;
        _db = db;
        _matchingService = matchingService;
    }

    // ========================================================
    // 1. NHAN TIN NHAN
    // ========================================================

    [HttpPost("gui-tin-nhan")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(32768)]
    [Consumes("application/json")]
    public async Task<IActionResult> GuiTinNhan(
        [FromBody] TripiChatApiRequest? apiRequest,
        CancellationToken cancellationToken)
    {
        // ====================================================
        // 2. KIEM TRA REQUEST
        // ====================================================

        if (apiRequest == null ||
            string.IsNullOrWhiteSpace(apiRequest.Message))
        {
            return BadRequest(new
            {
                message = "Bạn hãy nhập nội dung muốn hỏi Tripi nhé."
            });
        }

        apiRequest.Message = apiRequest.Message.Trim();

        if (apiRequest.Message.Length > MaxMessageLength)
        {
            return BadRequest(new
            {
                message =
                    "Mỗi tin nhắn không được vượt quá 1000 ký tự."
            });
        }

        apiRequest.History ??= new List<TripiChatMessage>();

        if (apiRequest.History.Count > MaxHistoryMessages)
        {
            return BadRequest(new
            {
                message =
                    "Lịch sử hội thoại quá dài. " +
                    "Vui lòng tải lại trang."
            });
        }

        if (apiRequest.IdTourDangXem.HasValue &&
            apiRequest.IdTourDangXem.Value <= 0)
        {
            return BadRequest(new
            {
                message = "Mã tour đang xem không hợp lệ."
            });
        }

        int totalCharacters = apiRequest.Message.Length;

        foreach (TripiChatMessage item in apiRequest.History)
        {
            if (item == null ||
                string.IsNullOrWhiteSpace(item.Content) ||
                item.Content.Length > MaxMessageLength ||
                (item.Role != "user" && item.Role != "model"))
            {
                return BadRequest(new
                {
                    message = "Lịch sử hội thoại không hợp lệ."
                });
            }

            totalCharacters += item.Content.Length;
        }

        if (totalCharacters > MaxRequestCharacters)
        {
            return BadRequest(new
            {
                message =
                    "Nội dung hội thoại vượt quá giới hạn."
            });
        }

        var savedPreferences = apiRequest.Preferences;

        if (savedPreferences != null)
        {
            bool invalidPreferences =
                (savedPreferences.DiemDen?.Length ?? 0) > 200 ||
                (savedPreferences.LoaiTour?.Length ?? 0) > 200 ||
                (savedPreferences.NgayDuKien?.Length ?? 0) > 30 ||
                (savedPreferences.SoThich?.Count ?? 0) > 20 ||
                (savedPreferences.SoThich?.Any(x =>
                    string.IsNullOrWhiteSpace(x) ||
                    x.Length > 100) ?? false) ||
                (savedPreferences.NganSach.HasValue &&
                    (savedPreferences.NganSach.Value < 0 ||
                    savedPreferences.NganSach.Value > 1000000000m)) ||
                (savedPreferences.SoNgay.HasValue &&
                    (savedPreferences.SoNgay.Value < 1 ||
                    savedPreferences.SoNgay.Value > 365)) ||
                (savedPreferences.SoKhach.HasValue &&
                    (savedPreferences.SoKhach.Value < 1 ||
                    savedPreferences.SoKhach.Value > 1000)) ||
                (!string.IsNullOrEmpty(savedPreferences.LoaiNganSach) &&
                    savedPreferences.LoaiNganSach != "MoiNguoi" &&
                    savedPreferences.LoaiNganSach != "CaNhom");

            if (!string.IsNullOrWhiteSpace(savedPreferences.NgayDuKien))
            {
                invalidPreferences |=
                    !DateOnly.TryParseExact(
                        savedPreferences.NgayDuKien,
                        "yyyy-MM-dd",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None,
                        out _);
            }

            if (invalidPreferences)
            {
                return BadRequest(new
                {
                    message = "Dữ liệu nhu cầu du lịch không hợp lệ."
                });
            }
        }

        
        var chatRequest = new TripiChatRequest
        {
            Message = apiRequest.Message,
            History = apiRequest.History,
            Preferences = savedPreferences
        };


        // ====================================================
        // 3. GEMINI PHAN LOAI Y DINH
        // ====================================================

        TripiChatResponse chatResult;

        try
        {
            // Yeu cau tim tour ro rang:
            // Khong can goi Gemini lan 1 de phan loai.
            // Gemini lan 2 van phan tich nhu cau thuc te.

            if (LaYeuCauTimTourRoRang(apiRequest.Message))
            {
                chatResult = new TripiChatResponse
                {
                    Intent = "search_tour",
                    CanSearchTours = true,
                    Reply = ""
                };
            }
            else
            {
                var chatService = new TripiChatService(
                    _configuration);

                chatResult = await chatService.TraLoiAsync(
                    chatRequest,
                    cancellationToken);
            }

            _logger.LogInformation(
                "[Tripi] Intent={Intent}; CanSearch={CanSearch}; " +
                "HistoryCount={HistoryCount}",
                chatResult.Intent,
                chatResult.CanSearchTours,
                chatRequest.History.Count);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(
                ex,
                "[Tripi] Gemini chat request timed out.");

            return StatusCode(504, new
            {
                message =
                    "Tripi phản hồi quá lâu. " +
                    "Bạn vui lòng thử lại nhé."
            });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(
                ex,
                "[Tripi] Cannot connect to Gemini chat.");

            return StatusCode(503, new
            {
                message =
                    "Tripi chưa kết nối được với Gemini. " +
                    "Bạn vui lòng thử lại nhé."
            });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "[Tripi] Gemini chat returned invalid JSON.");

            return StatusCode(502, new
            {
                message =
                    "Tripi chưa xử lý được phản hồi AI. " +
                    "Bạn vui lòng thử lại nhé."
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                ex,
                "[Tripi] Gemini chat failed.");

            return StatusCode(503, new
            {
                message =
                    "Tripi đang gặp sự cố khi tạo câu trả lời. " +
                    "Bạn vui lòng thử lại nhé."
            });
        }

        // ====================================================
        // 4. CAU HOI CHI TIET TOUR -> RAG
        // ====================================================

        if (chatResult.Intent == "tour_info")
        {
            return await TraLoiThongTinTourAsync(
                apiRequest,
                cancellationToken);
        }

        // ====================================================
        // 5. TIM TOUR THEO Y DINH
        // ====================================================

        bool shouldSearchTours =
            chatResult.CanSearchTours &&
            (chatResult.Intent == "search_tour" ||
             chatResult.Intent == "refine");

        if (shouldSearchTours)
        {
            return await TimTourTrongChatAsync(
                chatRequest,
                chatResult,
                cancellationToken);
        }

        // ====================================================
        // 6. HOI THOAI THONG THUONG
        // ====================================================

        string generalReply = chatResult.Reply;

        if (chatResult.Intent == "compare_tours")
        {
            generalReply =
                "Mình có thể giúp bạn xem những tiêu chí " +
                "nên so sánh giữa các tour. " +
                "Phần đối chiếu dữ liệu trực tiếp giữa " +
                "nhiều tour hiện chưa được kết nối, " +
                "nên mình chưa muốn đưa ra kết luận " +
                "thiếu căn cứ. Bạn đang cân nhắc " +
                "những tour nào vậy?";
        }

        return Ok(new
        {
            reply = generalReply,
            intent = chatResult.Intent,
            canSearchTours = false,

            preferences = (object?)null,

            searchPerformed = false,
            tours = Array.Empty<object>(),
            searchMessage = "",

            activeTourId = apiRequest.IdTourDangXem,

            ragHasEvidence = false,
            ragSources = Array.Empty<object>()
        });
    }

    // ========================================================
    // 7. RAG - TRA LOI THONG TIN TOUR
    // ========================================================

    private async Task<IActionResult> TraLoiThongTinTourAsync(
        TripiChatApiRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.IdTourDangXem.HasValue)
        {
            return Ok(new
            {
                reply =
                    "Mình muốn kiểm tra đúng tour mà bạn " +
                    "đang hỏi. Bạn hãy chọn " +
                    "'Hỏi về tour này' ở một hành trình " +
                    "bên cạnh nhé. Sau đó mình sẽ " +
                    "tra cứu thông tin HappyTrip.",

                intent = "tour_info",
                canSearchTours = false,
                preferences = (object?)null,

                searchPerformed = false,
                tours = Array.Empty<object>(),
                searchMessage = "",

                activeTourId = (int?)null,

                ragHasEvidence = false,
                ragSources = Array.Empty<object>()
            });
        }

        int idTour = request.IdTourDangXem.Value;

        try
        {
            var ragService = new TripiRagService(
                _db,
                _configuration);

            TripiRagResult ragResult =
                await ragService.TraLoiVeTourAsync(
                    idTour,
                    request.Message,
                    cancellationToken);

            _logger.LogInformation(
                "[Tripi RAG] TourId={TourId}; " +
                "HasEvidence={HasEvidence}; SourceCount={SourceCount}",
                idTour,
                ragResult.HasEvidence,
                ragResult.Sources.Count);

            return Ok(new
            {
                reply = ragResult.Reply,
                intent = "tour_info",
                canSearchTours = false,

                preferences = (object?)null,

                searchPerformed = false,
                tours = Array.Empty<object>(),
                searchMessage = "",

                activeTourId = idTour,

                ragHasEvidence = ragResult.HasEvidence,

                ragSources = ragResult.Sources.Select(s => new
                {
                    idTaiLieu = s.IdTaiLieu,
                    idTour = s.IdTour,
                    tieuDe = s.TieuDe,
                    phienBan = s.PhienBan
                }).ToList()
            });
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (
            ex is InvalidOperationException ||
            ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is JsonException)
        {
            _logger.LogWarning(
                ex,
                "[Tripi RAG] Failed for TourId={TourId}",
                idTour);

            return Ok(new
            {
                reply =
                    "Mình chưa truy xuất được tài liệu " +
                    "của tour này lúc này. " +
                    "Bạn có thể xem trang chi tiết tour " +
                    "hoặc hỏi lại mình sau nhé.",

                intent = "tour_info",
                canSearchTours = false,

                preferences = (object?)null,

                searchPerformed = false,
                tours = Array.Empty<object>(),
                searchMessage = "",

                activeTourId = idTour,

                ragHasEvidence = false,
                ragSources = Array.Empty<object>()
            });
        }
    }

    // ========================================================
    // 8. MATCHING SCORE - TIM TOUR
    // ========================================================

    private async Task<IActionResult> TimTourTrongChatAsync(
        TripiChatRequest request,
        TripiChatResponse chatResult,
        CancellationToken cancellationToken)
    {
        try
        {
            var matchingService = new TripiChatMatchingService(
                _db,
                _matchingService,
                _configuration);

            TripiChatMatchingResult matchingResult =
                await matchingService.TimTourAsync(
                    request,
                    cancellationToken);

            // =================================================
            // 9. LOG CAC TIEU CHI THUC TE
            // =================================================
            //
            // Khong ghi nguyen tin nhan khach vao log.
            // Chi ghi cac tieu chi can de debug.

            _logger.LogInformation(
                "[Tripi Matching] " +
                "Destination={Destination}; " +
                "InterestCount={InterestCount}; " +
                "Budget={Budget}; " +
                "BudgetScope={BudgetScope}; " +
                "Days={Days}; " +
                "TourType={TourType}; " +
                "Guests={Guests}; " +
                "DepartureDate={DepartureDate}; " +
                "SearchPerformed={SearchPerformed}; " +
                "TourCount={TourCount}",
                matchingResult.DiemDen,
                matchingResult.SoThich.Count,
                matchingResult.NganSach,
                matchingResult.LoaiNganSach,
                matchingResult.SoNgay,
                matchingResult.LoaiTour,
                matchingResult.SoKhach,
                matchingResult.NgayDuKien,
                matchingResult.DaTimKiem,
                matchingResult.Tours.Count);

            // =================================================
            // 10. TAO CAU TRA LOI DONG BO VOI KET QUA
            // =================================================

            string reply;

            if (matchingResult.DaTimKiem)
            {
                int total = matchingResult.Tours.Count;

                if (total == 0)
                {
                    // FIX:
                    // Khong bo qua thong bao chi tiet
                    // tu Matching Service nua.

                    reply =
                        !string.IsNullOrWhiteSpace(
                            matchingResult.ThongBao)
                            ? matchingResult.ThongBao
                            : "Mình đã tìm theo các điều kiện " +
                              "bạn chia sẻ, nhưng hiện chưa " +
                              "có tour nào phù hợp.";
                }
                else
                {
                    var strongerMatches = matchingResult.Tours
                        .Where(t => t.MatchingScore >= 50)
                        .OrderByDescending(
                            t => t.MatchingScore)
                        .ToList();

                    if (strongerMatches.Count > 0)
                    {
                        var highlighted = strongerMatches
                            .Take(2)
                            .ToList();

                        string names = string.Join(
                            " và ",
                            highlighted.Select(
                                t => $"**{t.TenTour}**"));

                        reply =
                            "Mình vừa xem qua các tour " +
                            "HappyTrip theo nhu cầu của " +
                            "bạn rồi! 🌿 " +
                            $"Trong {total} lựa chọn " +
                            $"được trả về, {names} " +
                            "có điểm phù hợp nổi bật hơn " +
                            "theo các tiêu chí hiện tại.";

                        if (total > strongerMatches.Count)
                        {
                            reply +=
                                "\n\nMột số tour khác vẫn " +
                                "được hiển thị để bạn tham khảo, " +
                                "nhưng mức độ khớp nhu cầu thấp hơn.";
                        }

                        reply +=
                            "\n\nBạn muốn tìm hiểu kỹ hơn " +
                            "về hành trình nào trước?";
                    }
                    else
                    {
                        // Khi chi co it tieu chi,
                        // khong goi diem thap la tour kem
                        // chat luong hoac khong phu hop.

                        reply =
                            $"Mình tìm được {total} tour " +
                            "trong dữ liệu HappyTrip. " +
                            "Hiện các tour mới khớp " +
                            "một phần tiêu chí bạn đã cung cấp, " +
                            "nên điểm Matching Score " +
                            "chưa cao. Bạn có thể xem " +
                            "các lựa chọn bên cạnh hoặc " +
                            "bổ sung sở thích và ngân sách " +
                            "để mình xếp hạng sát hơn nhé.";
                    }
                }
            }
            else
            {
                // Chua tim:
                // Uu tien yeu cau lam ro tu Matching.
                //
                // Vi du: ngan sach moi nguoi hay ca nhom?

                reply =
                    !string.IsNullOrWhiteSpace(
                        matchingResult.ThongBao)
                        ? matchingResult.ThongBao
                        : chatResult.Reply;
            }

            // Giu hanh vi hien tai:
            // backend tra tour dau tien lam tour goi y.
            // Neu khach muon doi tour, JS van cho phep
            // bam "Hoi ve tour nay".

            int? activeTourId = matchingResult.Tours
                .FirstOrDefault()?.IdTour;

            // =================================================
            // 11. RESPONSE
            // =================================================

            return Ok(new
            {
                reply,

                intent = chatResult.Intent,

                canSearchTours =
                    matchingResult.DaTimKiem,

                // FIX: Tra du ca 8 truong.
                preferences = new
                {
                    diemDen = matchingResult.DiemDen,

                    soThich = matchingResult.SoThich,

                    nganSach = matchingResult.NganSach,

                    loaiNganSach =
                        matchingResult.LoaiNganSach,

                    soNgay = matchingResult.SoNgay,

                    loaiTour = matchingResult.LoaiTour,

                    soKhach = matchingResult.SoKhach,

                    ngayDuKien =
                        matchingResult.NgayDuKien?
                            .ToString("yyyy-MM-dd")
                },

                searchPerformed =
                    matchingResult.DaTimKiem,

                tours = matchingResult.Tours.Select(t => new
                {
                    idTour = t.IdTour,
                    tenTour = t.TenTour,
                    diaDiem = t.DiaDiem,

                    hinhAnh =
                        TourImagePathHelper.ToTourImageSrc(
                            t.HinhAnh),

                    thoiGian = t.ThoiGian,
                    giaBan = t.GiaBan,
                    soNgay = t.SoNgay,

                    matchingScore = t.MatchingScore,

                    diemSoThich = t.DiemSoThich,
                    diemDiemDen = t.DiemDiemDen,
                    diemNganSach = t.DiemNganSach,

                    lyDoGoiY = t.LyDoGoiY,

                    tenLoaiTour = t.TenLoaiTour,

                    ngayKhoiHanhPhuHop =
                        t.NgayKhoiHanhPhuHop?
                            .ToString("yyyy-MM-dd"),

                    soChoConLai = t.SoChoConLai
                }).ToList(),

                searchMessage =
                    matchingResult.ThongBao,

                activeTourId,

                ragHasEvidence = false,

                ragSources = Array.Empty<object>()
            });
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (
            ex is InvalidOperationException ||
            ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is JsonException)
        {
            _logger.LogWarning(
                ex,
                "[Tripi Matching] Integration failed.");

            return Ok(new
            {
                reply =
                    "Mình đã hiểu yêu cầu của bạn, " +
                    "nhưng hiện chưa truy xuất được " +
                    "danh sách tour HappyTrip. " +
                    "Bạn có thể thử tìm lại hoặc " +
                    "tiếp tục trò chuyện cùng mình nhé.",

                intent = chatResult.Intent,

                canSearchTours = false,

                preferences = (object?)null,

                searchPerformed = false,

                tours = Array.Empty<object>(),

                searchMessage =
                    "Chưa thể tìm tour lúc này.",

                activeTourId = (int?)null,

                ragHasEvidence = false,

                ragSources = Array.Empty<object>()
            });
        }
    }
        private static bool LaYeuCauTimTourRoRang(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        string text = message.Trim().ToLowerInvariant();

        // Khong kich hoat khi khach phu dinh yeu cau.
        if (text.Contains("không tìm tour") ||
            text.Contains("đừng tìm tour") ||
            text.Contains("không cần tìm tour"))
        {
            return false;
        }

        string[] yeuCauTimTour =
        {
            "tìm tour",
            "gợi ý tour",
            "đề xuất tour",
            "tư vấn tour",
            "có tour nào",
            "tour nào phù hợp",
            "xem danh sách tour",
            "lọc tour"
        };

        if (yeuCauTimTour.Any(
            phrase => text.Contains(
                phrase,
                StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // Cach noi tu nhien co nhu cau cu the.
        bool muonDi =
            text.Contains("muốn đi ") ||
            text.Contains("tính đi ") ||
            text.Contains("dự định đi ");

        bool coThongTinChuyenDi =
            System.Text.RegularExpressions.Regex.IsMatch(
                text,
                @"\d+\s*(ngày|đêm|người|khách)" +
                @"|\d+(?:[.,]\d+)?\s*(triệu|tr)");

        return muonDi && coThongTinChuyenDi;
    }
}
