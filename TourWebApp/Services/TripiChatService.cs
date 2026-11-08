using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TourWebApp.Services;

// ============================================================
// REQUEST / RESPONSE
// ============================================================

public sealed class TripiChatMessage
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = "";
}

public sealed class TripiTravelPreferences
{
    public string? DiemDen { get; set; }

    public List<string> SoThich { get; set; } = new();

    public decimal? NganSach { get; set; }

    public string? LoaiNganSach { get; set; }

    public int? SoNgay { get; set; }

    public string? LoaiTour { get; set; }

    public int? SoKhach { get; set; }

    public string? NgayDuKien { get; set; }
}


public sealed class TripiChatRequest
{
    public string Message { get; set; } = "";

    public List<TripiChatMessage> History { get; set; } = new();

    public TripiTravelPreferences? Preferences { get; set; }
}

public sealed class TripiChatResponse
{
    public string Reply { get; set; } = "";
    public string Intent { get; set; } = "general";
    public bool CanSearchTours { get; set; }
}

// ============================================================
// TRIPI CHAT SERVICE
// ============================================================

public sealed class TripiChatService
{
    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(90)
    };

    private readonly IConfiguration _configuration;

    private const int MaxHistoryMessages = 12;
    private const int MaxMessageLength = 1000;

    private const int MaxAttempts = 3;
    private const int FallbackAttempts = 2;

    private static readonly HashSet<string> ValidIntents =
        new(StringComparer.Ordinal)
        {
            "general",
            "search_tour",
            "tour_info",
            "compare_tours",
            "refine"
        };

    public TripiChatService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    // ========================================================
    // MAIN
    // ========================================================

    public async Task<TripiChatResponse> TraLoiAsync(
        TripiChatRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null ||
            string.IsNullOrWhiteSpace(request.Message))
        {
            throw new ArgumentException(
                "Bạn hãy nhập nội dung muốn hỏi Tripi nhé.");
        }

        string userMessage = request.Message.Trim();

        if (userMessage.Length > MaxMessageLength)
        {
            throw new ArgumentException(
                "Tin nhắn không được vượt quá 1000 ký tự.");
        }

        string? apiKey =
            _configuration["Gemini:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Chưa cấu hình Gemini API key.");
        }

        string model =
            _configuration["Gemini:Model"]
            ?? "gemini-2.5-flash";

        string fallbackModel =
            _configuration["Gemini:FallbackModel"]
            ?? "gemini-2.5-flash-lite";

        if (string.Equals(
                model,
                fallbackModel,
                StringComparison.OrdinalIgnoreCase))
        {
            fallbackModel = "";
        }

        var previousMessages =
            (request.History ?? new List<TripiChatMessage>())
            .Where(message =>
                message != null &&
                !string.IsNullOrWhiteSpace(message.Content) &&
                (message.Role == "user" ||
                 message.Role == "model") &&
                message.Content.Length <= MaxMessageLength)
            .TakeLast(MaxHistoryMessages)
            .ToList();

        // ====================================================
        // PERSONALITY + INTENT PROMPT
        // ====================================================

        string systemPrompt = """
            BẠN LÀ AI?

            Bạn là Tripi, trợ lý AI của website
            du lịch HappyTrip tại Việt Nam.

            Vai trò chính:
            - Trò chuyện tự nhiên với khách hàng.
            - Hiểu nhu cầu du lịch.
            - Hỗ trợ tìm kiếm và lựa chọn tour.
            - Giải thích thông tin dựa trên dữ liệu
              thực tế của hệ thống HappyTrip.

            PHONG CÁCH GIAO TIẾP:

            Thân thiện, tự nhiên, tinh tế,
            chuyên nghiệp và đáng tin cậy.

            Mặc định:
            - Xưng "mình" hoặc "Tripi".
            - Gọi khách là "bạn".
            - Trả lời bằng tiếng Việt.
            - Gần gũi nhưng không suồng sã.
            - Tránh trả lời như văn bản hành chính.
            - Không quảng cáo trong mọi phản hồi.
            - Không lặp lại câu chào mỗi lượt.
            - Không bắt buộc kết thúc bằng câu hỏi.
            - Không cố chuyển mọi cuộc trò chuyện
              sang hoạt động tìm hoặc đặt tour.

            Khi khách nói chuyện thân mật,
            có thể đáp lại gần gũi tương ứng.

            Nếu khách dùng các từ như:
            "bà", "tui", "bestie", "hong",
            "hông", "má ơi", "nè"...

            Có thể đáp lại tự nhiên bằng:
            "bà ơi", "nè", "tui", "hihi"...
            khi phù hợp.

            Không bắt chước mọi tiếng lóng.
            Không lạm dụng emoji.
            Không tự đoán tuổi hoặc giới tính.

            Nếu khách đang khó chịu:
            - Bình tĩnh.
            - Không tranh cãi.
            - Tôn trọng cảm xúc khách.
            - Tập trung giải quyết vấn đề.

            ===================================================
            1. HIỂU ĐÚNG TÌNH HUỐNG
            ===================================================

            Không phải tin nhắn nào cũng là
            yêu cầu tư vấn du lịch.

            Khách chào hỏi:
            Hãy chào hỏi lại.

            Khách kể chuyện:
            Hãy lắng nghe và tiếp chuyện.

            Khách đang đùa:
            Có thể đáp lại dí dỏm, vừa phải.

            Khách hỏi kiến thức:
            Hãy trả lời rõ ràng và chính xác.

            Khách chỉ chia sẻ sở thích hoặc
            nơi sinh sống:
            Không tự tìm tour.

            Khách yêu cầu tìm tour:
            Chuyển sang quy trình tìm tour.

            Khách hỏi về một tour:
            Chuyển sang quy trình tra cứu tour.

            Ví dụ:

            Khách:
            "Hello bestie, nay bà khỏe hong?"

            Trả lời phù hợp:
            "Hellooo bà nè! 😄 Mình vẫn ổn.
            Nay bà có chuyện gì vui hong?"

            Không hỏi khách có muốn đặt tour.

            Khách:
            "Tui ở Cái Bè, Tiền Giang á."

            Có thể đáp:
            "À, Cái Bè nè! Khu vực đó nổi tiếng
            với cảnh miệt vườn và sông nước lắm."

            Không tự đề xuất tour Cái Bè.

            Các ví dụ chỉ định hướng phong cách,
            không phải câu trả lời cố định.

            ===================================================
            2. GHI NHỚ HỘI THOẠI
            ===================================================

            Sử dụng lịch sử được cung cấp để
            hiểu các lượt trò chuyện.

            Có thể ghi nhớ:
            - Địa phương khách đã nói.
            - Điểm đến khách quan tâm.
            - Sở thích du lịch.
            - Ngân sách.
            - Thời lượng.
            - Số khách.
            - Ngày khởi hành mong muốn.
            - Loại hình du lịch.
            - Tour đang được thảo luận.

            Nếu khách hỏi lại thông tin cũ,
            trả lời dựa trên lịch sử thực tế.

            Không khẳng định nhớ thông tin
            không có trong lịch sử.

            Khi khách đổi điều kiện:
            - Thông tin mới nhất được ưu tiên.
            - Giữ điều kiện không thay đổi.
            - Không tự tạo thêm điều kiện.

            ===================================================
            3. PHÂN LOẠI Ý ĐỊNH
            ===================================================

            Chỉ dùng đúng 5 giá trị intent:

            general
            search_tour
            tour_info
            compare_tours
            refine

            GENERAL:

            Áp dụng khi:
            - Chào hỏi.
            - Trò chuyện thường ngày.
            - Chia sẻ cảm xúc.
            - Hỏi kiến thức chung.
            - Kể chuyện quê quán.
            - Chia sẻ sở thích.
            - Xin lời khuyên chung.

            Không tự tìm tour.

            SEARCH_TOUR:

            Khi khách chủ động yêu cầu:
            - Tìm tour.
            - Gợi ý tour.
            - Đề xuất tour.
            - Liệt kê tour.
            - Xem danh sách tour.
            - Tư vấn lựa chọn tour.
            - Tìm tour để đặt.

            Ví dụ:
            "Tìm tour Tây Ninh cho mình."
            "Gợi ý tour biển dưới 3 triệu."
            "Có tour nào đi Côn Đảo không?"
            "Cho mình xem các tour phù hợp."

            Chỉ xác nhận nhu cầu.
            Không tự bịa kết quả tìm kiếm.

            TOUR_INFO:

            Khi khách hỏi thông tin cụ thể
            về một tour đang được thảo luận.

            Ví dụ:
            "Tour này có gì nổi bật?"
            "Tour có bao gồm ăn uống không?"
            "Lịch trình ngày đầu ra sao?"
            "Tour này có chính sách hủy thế nào?"

            Không tự tạo thông tin tour.

            COMPARE_TOURS:

            Khi khách muốn so sánh
            hai tour hoặc nhiều tour.

            Ví dụ:
            "So sánh Vũng Tàu với Côn Đảo."
            "Tour nào tiết kiệm hơn?"
            "Trong hai tour này nên chọn tour nào?"

            Không tự khẳng định giá hoặc
            thông tin chưa có dữ liệu.

            REFINE:

            Khi khách muốn điều chỉnh
            một yêu cầu tìm tour trước đó.

            Ví dụ:
            "Giảm ngân sách còn 2 triệu."
            "Đổi sang Vũng Tàu nhé."
            "Chỉ đi tối đa 2 ngày thôi."
            "Đổi thành 4 người nha."
            "Khởi hành ngày 20/12/2026."
            "Chuyển sang tour nghỉ dưỡng."
            "Tìm lại theo ngân sách mới."

            Nếu chưa có yêu cầu tìm tour
            trước đó, không tự tìm lại
            chỉ vì khách chia sẻ sở thích.

            ===================================================
            4. DỮ LIỆU VÀ ĐỘ TIN CẬY
            ===================================================

            Bạn chưa được cung cấp toàn bộ
            dữ liệu tour HappyTrip trong bước này.

            Không tự sáng tạo:
            - Mã tour hoặc tên tour.
            - Giá bán và khuyến mãi.
            - Lịch trình.
            - Ngày khởi hành.
            - Số chỗ còn lại.
            - Chính sách hoàn/hủy.
            - Dịch vụ bao gồm.
            - Điểm Matching Score.
            - Trạng thái đặt tour hoặc thanh toán.

            Khi khách yêu cầu tìm tour:
            Phân loại search_tour để hệ thống
            chạy Matching Score.

            Khi hỏi thông tin tour:
            Phân loại tour_info để hệ thống
            truy xuất dữ liệu và RAG.

            Không tự nhận mình đã đi du lịch
            hoặc trực tiếp sử dụng dịch vụ.

            Không yêu cầu khách cung cấp
            mật khẩu hoặc thông tin thẻ.

            ===================================================
            5. CHẤT LƯỢNG PHẢN HỒI
            ===================================================

            Trước khi trả lời:
            - Hiểu khách đang muốn điều gì.
            - Xem lại ngữ cảnh cần thiết.
            - Tránh lặp ý.
            - Không giả định dữ liệu còn thiếu.
            - Không ép khách tìm tour.
            - Không tự nhận đã thực hiện
              truy vấn hoặc đặt tour.

            Trả lời đúng trọng tâm.
            Ngắn gọn khi chỉ cần câu ngắn.
            Giải thích thêm khi cần thiết.

            ===================================================
            6. ĐỊNH DẠNG ĐẦU RA
            ===================================================

            Chỉ trả về một JSON hợp lệ.

            JSON phải có hai trường:

            reply:
            Câu trả lời tiếng Việt tự nhiên.

            intent:
            Một trong 5 giá trị:
            general, search_tour, tour_info,
            compare_tours, refine.

            Không thêm trường khác.
            Không bọc trong Markdown.
            """;

        // ====================================================
        // HISTORY
        // ====================================================

        var contents = new List<object>();

        foreach (TripiChatMessage message in previousMessages)
        {
            contents.Add(new
            {
                role = message.Role,
                parts = new[]
                {
                    new
                    {
                        text = message.Content.Trim()
                    }
                }
            });
        }

        contents.Add(new
        {
            role = "user",
            parts = new[]
            {
                new
                {
                    text = userMessage
                }
            }
        });

        // ====================================================
        // REQUEST PAYLOAD
        // ====================================================

        var payload = new
        {
            systemInstruction = new
            {
                parts = new[]
                {
                    new
                    {
                        text = systemPrompt
                    }
                }
            },

            contents,

            generationConfig = new
            {
                responseMimeType = "application/json",

                temperature = 0.65,

                maxOutputTokens = 1400,

                responseSchema = new
                {
                    type = "OBJECT",

                    properties = new
                    {
                        reply = new
                        {
                            type = "STRING"
                        },

                        intent = new
                        {
                            type = "STRING",

                            @enum = new[]
                            {
                                "general",
                                "search_tour",
                                "tour_info",
                                "compare_tours",
                                "refine"
                            }
                        }
                    },

                    required = new[]
                    {
                        "reply",
                        "intent"
                    }
                }
            }
        };

        // ====================================================
        // CALL GEMINI WITH RETRY + FALLBACK
        // ====================================================

        using JsonDocument responseDocument =
            await SendGeminiRequestAsync(
                model,
                fallbackModel,
                apiKey,
                payload,
                cancellationToken);

        string responseText =
            ExtractResponseText(responseDocument);

        using JsonDocument parsed =
            JsonDocument.Parse(responseText);

        JsonElement result = parsed.RootElement;

        string reply = ReadString(
            result,
            "reply");

        string intent = ReadString(
            result,
            "intent");

        if (string.IsNullOrWhiteSpace(reply))
        {
            throw new InvalidOperationException(
                "Tripi chưa tạo được câu trả lời.");
        }

        if (!ValidIntents.Contains(intent))
        {
            intent = "general";
        }

        // ====================================================
        // CONFIRM SEARCH PERMISSION
        // ====================================================

        bool explicitSearch =
            HasExplicitSearchRequest(userMessage);

        // Nhận diện cách nói tự nhiên:
        // "Mình muốn đi Tây Ninh 2 ngày, tầm 3 triệu..."
        // Không bắt khách phải nói chính xác "tìm tour".
        string normalizedMessage = Normalize(userMessage);

        bool wantsTravel = Regex.IsMatch(
            normalizedMessage,
            @"\b(muon di|du dinh di|muon du lich|can chuyen di|len ke hoach di)\b");

        bool hasTravelCriteria = Regex.IsMatch(
            normalizedMessage,
            @"\b\d+\s*(ngay|dem|nguoi|khach)\b|\b\d+(?:[.,]\d+)?\s*(trieu|tr|nghin|k)\b|\b\d{1,2}\s+\d{1,2}(?:\s+\d{4})?\b");

        bool naturalSearch =
            wantsTravel && hasTravelCriteria;

        bool previousSearch =
            previousMessages.Any(message =>
                message.Role == "user" &&
                (
                    HasExplicitSearchRequest(message.Content) ||
                    (
                        Regex.IsMatch(
                            Normalize(message.Content),
                            @"\b(muon di|du dinh di|muon du lich|can chuyen di|len ke hoach di)\b") &&
                        Regex.IsMatch(
                            Normalize(message.Content),
                            @"\b\d+\s*(ngay|dem|nguoi|khach)\b|\b\d+(?:[.,]\d+)?\s*(trieu|tr|nghin|k)\b")
                    )
                ));

        // Da co nhu cau tim tour duoc luu tu phien truoc.
        // Khong phu thuoc hoan toan vao 12 tin nhan gan nhat.
        bool hasStoredSearch =
            request.Preferences is { } saved &&
            (
                !string.IsNullOrWhiteSpace(saved.DiemDen) ||
                (saved.SoThich?.Count ?? 0) > 0 ||
                saved.NganSach.HasValue ||
                saved.SoNgay.HasValue ||
                !string.IsNullOrWhiteSpace(saved.LoaiTour) ||
                saved.SoKhach.HasValue ||
                !string.IsNullOrWhiteSpace(saved.NgayDuKien)
            );

        previousSearch = previousSearch || hasStoredSearch;

        bool explicitRefinement =
            HasExplicitRefinement(userMessage);

        // Ưu tiên yêu cầu tìm tour rõ ràng.
        // Không phủ nhận kết quả Gemini nếu Gemini
        // đã phân loại search_tour.
        if (explicitSearch || naturalSearch)
        {
            intent = "search_tour";
        }

        // ====================================================
        // KHACH CHUA BIET DI DAU - TU VAN TRUOC KHI TIM TOUR
        // ====================================================

        bool dangPhanVanDiemDen =
            Regex.IsMatch(
                normalizedMessage,
                @"\b(chua biet|khong biet|phan van|chua quyet dinh)\b")
            &&
            Regex.IsMatch(
                normalizedMessage,
                @"\b(di dau|noi nao|dia diem|diem den|di du lich|di choi|di bien|di nui)\b");

        // Khach chua biet di dau thi khong tu dong
        // chuyen sang Matching Score, tru khi khach
        // da chu dong yeu cau tim tour ro rang.
        if (dangPhanVanDiemDen &&
            !explicitSearch &&
            !naturalSearch &&
            !explicitRefinement)
        {
            intent = "general";

            // Neu Gemini da soan cau tra loi theo huong
            // yeu cau tim tour, thay bang loi tu van.
            if (string.IsNullOrWhiteSpace(reply) ||
                Regex.IsMatch(
                    Normalize(reply),
                    @"chua xac dinh duoc nhu cau|hay cho biet diem den|can them tieu chi"))
            {
                reply =
                    "Được chứ! 🦊 Nếu bạn chưa biết nên đi đâu, " +
                    "mình sẽ cùng bạn tìm một điểm đến phù hợp nhé.\n\n" +
                    "Bạn thích đi biển để nghỉ dưỡng, " +
                    "khám phá thiên nhiên hay tham quan " +
                    "những địa điểm văn hóa, lịch sử?\n\n" +
                    "Mình có thể dựa vào sở thích của bạn " +
                    "để tư vấn trước, chưa cần chọn tour ngay đâu!";
            }
        }

        // Khi khách thay đổi điều kiện của chuyến đi
        // đã yêu cầu tìm trước đó, tiếp tục tìm lại.
        // Không tự tìm khi chỉ đang trò chuyện.
        bool canSearch =
            intent == "search_tour" ||
            (
                intent == "refine" &&
                previousSearch &&
                explicitRefinement
            );

        return new TripiChatResponse
        {
            Reply = reply,
            Intent = intent,
            CanSearchTours = canSearch
        };
    }

    // ========================================================
    // SEARCH INTENT
    // ========================================================

    private static bool HasExplicitSearchRequest(
        string text)
    {
        string normalized = Normalize(text);

        string[] phrases =
        {
            "tim tour",
            "tim giup",
            "tim cho minh",
            "tim kiem tour",
            "goi y tour",
            "goi y vai tour",
            "goi y chuyen di",
            "goi y hanh trinh",
            "de xuat tour",
            "tu van tour",
            "chon tour",
            "xem tour",
            "danh sach tour",
            "co tour nao",
            "tour nao phu hop",
            "cho xem tour",
            "muon dat tour",
            "dat tour di",
            "tim lai tour",
            "loc tour",
            "tour phu hop voi minh",
            "tour phu hop cho minh"
        };

        if (phrases.Any(phrase =>
            normalized.Contains(
                phrase,
                StringComparison.Ordinal)))
        {
            return true;
        }

        bool mentionsTour =
            Regex.IsMatch(
                normalized,
                @"\btour\b");

        bool asksForSuggestions =
            Regex.IsMatch(
                normalized,
                @"\b(tim|goi y|de xuat|tu van|liet ke)\b");

        return mentionsTour && asksForSuggestions;
    }

    // ========================================================
    // REFINE INTENT
    // ========================================================

    private static bool HasExplicitRefinement(
        string text)
    {
        string normalized = Normalize(text);

        string[] phrases =
        {
            "doi ngan sach",
            "doi diem den",
            "doi thanh",
            "thay doi",
            "giam ngan sach",
            "tang ngan sach",
            "ngan sach giam",
            "ngan sach tang",
            "tim lai",
            "loc lai",
            "thay bang",
            "bo dieu kien",
            "chi di toi da",
            "chi muon di",
            "van muon di",
            "ngan sach con",
            "ngan sach moi",
            "ngan hon",
            "it ngay hon",
            "re hon",
            "cao hon",
            "duoi 2 trieu thoi",
            "2 trieu thoi",
            "3 trieu thoi",

            // Bổ sung nhận diện thay đổi nhu cầu.
            "doi ngay",
            "doi lich",
            "doi so nguoi",
            "doi so khach",
            "doi loai tour",
            "doi tour",
            "chuyen sang",
            "doi sang",
            "khoi hanh ngay",
            "di ngay",
            "ngay khoi hanh",
            "them nguoi",
            "bot nguoi",
            "them khach",
            "bot khach"
        };

        if (phrases.Any(phrase =>
            normalized.Contains(
                phrase,
                StringComparison.Ordinal)))
        {
            return true;
        }

        bool expressesChange =
            Regex.IsMatch(
                normalized,
                @"\b(thoi|nhe|nha|con|toi da|chi|giam|tang|doi|thanh|sang)\b");

        bool mentionsAmount =
            Regex.IsMatch(
                normalized,
                @"\b\d+(?:[.,]\d+)?\s*(?:trieu|tr|k|nghin|dong)\b");

        if (mentionsAmount && expressesChange)
        {
            return true;
        }

        bool mentionsDays =
            Regex.IsMatch(
                normalized,
                @"\b\d+\s*ngay\b");

        if (mentionsDays && expressesChange)
        {
            return true;
        }

        bool mentionsGuests =
            Regex.IsMatch(
                normalized,
                @"\b\d+\s*(?:nguoi|khach)\b");

        if (mentionsGuests && expressesChange)
        {
            return true;
        }

        bool mentionsSpecificDate =
            Regex.IsMatch(
                normalized,
                @"\b\d{1,2}[/-]\d{1,2}(?:[/-]\d{4})?\b");

        if (mentionsSpecificDate &&
            Regex.IsMatch(
                normalized,
                @"\b(ngay|di|khoi hanh|doi|sang)\b"))
        {
            return true;
        }

        return false;
    }

    // ========================================================
    // NORMALIZE VIETNAMESE
    // ========================================================

    private static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        string decomposed = text
            .Trim()
            .ToLowerInvariant()
            .Replace('đ', 'd')
            .Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder();

        foreach (char character in decomposed)
        {
            UnicodeCategory category =
                CharUnicodeInfo.GetUnicodeCategory(
                    character);

            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        string normalized =
            builder.ToString()
                .Normalize(NormalizationForm.FormC);

        normalized = Regex.Replace(
            normalized,
            @"[^a-z0-9]+",
            " ");

        return Regex.Replace(
            normalized,
            @"\s+",
            " ").Trim();
    }

    // ========================================================
    // EXTRACT GEMINI RESPONSE
    // ========================================================

    private static string ExtractResponseText(
        JsonDocument document)
    {
        JsonElement root = document.RootElement;

        if (!root.TryGetProperty(
                "candidates",
                out JsonElement candidates) ||
            candidates.ValueKind != JsonValueKind.Array ||
            candidates.GetArrayLength() == 0)
        {
            throw new InvalidOperationException(
                "Gemini chưa trả về câu trả lời.");
        }

        JsonElement candidate = candidates[0];

        if (!candidate.TryGetProperty(
                "content",
                out JsonElement content) ||
            !content.TryGetProperty(
                "parts",
                out JsonElement parts) ||
            parts.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException(
                "Gemini trả về nội dung chưa hợp lệ.");
        }

        var texts = new List<string>();

        foreach (JsonElement part in parts.EnumerateArray())
        {
            if (part.TryGetProperty(
                    "text",
                    out JsonElement value) &&
                value.ValueKind == JsonValueKind.String)
            {
                string? text = value.GetString();

                if (!string.IsNullOrWhiteSpace(text))
                {
                    texts.Add(text);
                }
            }
        }

        if (texts.Count == 0)
        {
            throw new InvalidOperationException(
                "Gemini chưa tạo được câu trả lời.");
        }

        return string.Join("\n", texts).Trim();
    }

    private static string ReadString(
        JsonElement root,
        string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty(
                propertyName,
                out JsonElement value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return "";
        }

        return value.GetString()?.Trim() ?? "";
    }

    // ========================================================
    // GEMINI ENDPOINT
    // ========================================================

    private static string BuildEndpoint(string model)
    {
        return
            "https://generativelanguage.googleapis.com/" +
            "v1beta/models/" +
            Uri.EscapeDataString(model) +
            ":generateContent";
    }

    // ========================================================
    // GEMINI HTTP ERROR
    // ========================================================

    private sealed class GeminiHttpException : Exception
    {
        public HttpStatusCode StatusCode { get; }

        public GeminiHttpException(
            HttpStatusCode statusCode,
            string message)
            : base(message)
        {
            StatusCode = statusCode;
        }
    }

    // ========================================================
    // GEMINI REQUEST: PRIMARY + FALLBACK
    // ========================================================

    private static async Task<JsonDocument>
        SendGeminiRequestAsync(
            string primaryModel,
            string fallbackModel,
            string apiKey,
            object payload,
            CancellationToken cancellationToken)
    {
        try
        {
            // Bước 1: Thử model chính.
            return await SendToModelAsync(
                primaryModel,
                apiKey,
                payload,
                MaxAttempts,
                cancellationToken);
        }
        catch (GeminiHttpException ex) when (
            !string.IsNullOrWhiteSpace(fallbackModel) &&
            ex.StatusCode is
                HttpStatusCode.ServiceUnavailable or
                HttpStatusCode.BadGateway or
                HttpStatusCode.GatewayTimeout or
                HttpStatusCode.InternalServerError)
        {
            // Bước 2: Chỉ dùng model dự phòng khi
            // model chính gặp lỗi dịch vụ.
            //
            // Không tự chuyển model với lỗi:
            // 400, 401, 403, 404 hoặc 429.

            try
            {
                return await SendToModelAsync(
                    fallbackModel,
                    apiKey,
                    payload,
                    FallbackAttempts,
                    cancellationToken);
            }
            catch (GeminiHttpException fallbackError)
            {
                throw new InvalidOperationException(
                    $"Gemini chính ({primaryModel}) lỗi HTTP " +
                    $"{(int)ex.StatusCode}; " +
                    $"model dự phòng ({fallbackModel}) lỗi HTTP " +
                    $"{(int)fallbackError.StatusCode}: " +
                    fallbackError.Message,
                    fallbackError);
            }
        }
        catch (GeminiHttpException ex)
        {
            throw new InvalidOperationException(
                $"Gemini API lỗi HTTP " +
                $"{(int)ex.StatusCode}: " +
                ex.Message,
                ex);
        }
    }

    // ========================================================
    // SEND WITH RETRY
    // ========================================================

    private static async Task<JsonDocument>
        SendToModelAsync(
            string model,
            string apiKey,
            object payload,
            int maxAttempts,
            CancellationToken cancellationToken)
    {
        string endpoint = BuildEndpoint(model);

        for (int attempt = 1;
             attempt <= maxAttempts;
             attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var request =
                new HttpRequestMessage(
                    HttpMethod.Post,
                    endpoint);

            request.Headers.Add(
                "x-goog-api-key",
                apiKey);

            request.Content =
                JsonContent.Create(payload);

            HttpResponseMessage response;

            try
            {
                response = await Client.SendAsync(
                    request,
                    cancellationToken);
            }
            catch (HttpRequestException)
                when (attempt < maxAttempts)
            {
                await Task.Delay(
                    GetRetryDelay(attempt, null),
                    cancellationToken);

                continue;
            }

            using (response)
            {
                if (response.IsSuccessStatusCode)
                {
                    await using Stream stream =
                        await response.Content.ReadAsStreamAsync(
                            cancellationToken);

                    return await JsonDocument.ParseAsync(
                        stream,
                        cancellationToken: cancellationToken);
                }

                bool retryable =
                    response.StatusCode is
                        HttpStatusCode.TooManyRequests or
                        HttpStatusCode.InternalServerError or
                        HttpStatusCode.BadGateway or
                        HttpStatusCode.ServiceUnavailable or
                        HttpStatusCode.GatewayTimeout;

                if (retryable && attempt < maxAttempts)
                {
                    await Task.Delay(
                        GetRetryDelay(attempt, response),
                        cancellationToken);

                    continue;
                }

                string errorBody =
                    await response.Content.ReadAsStringAsync(
                        cancellationToken);

                throw new GeminiHttpException(
                    response.StatusCode,
                    GetGoogleErrorMessage(errorBody));
            }
        }

        throw new InvalidOperationException(
            "Không thể kết nối Gemini sau các lần thử.");
    }

    // ========================================================
    // EXPONENTIAL BACKOFF + JITTER
    // ========================================================

    private static TimeSpan GetRetryDelay(
        int attempt,
        HttpResponseMessage? response)
    {
        double seconds = Math.Min(
            Math.Pow(2, attempt - 1),
            8);

        var retryAfter =
            response?.Headers.RetryAfter;

        if (retryAfter?.Delta is TimeSpan delta &&
            delta > TimeSpan.Zero)
        {
            seconds = Math.Clamp(
                delta.TotalSeconds,
                1,
                12);
        }
        else if (
            retryAfter?.Date is DateTimeOffset date &&
            date > DateTimeOffset.UtcNow)
        {
            seconds = Math.Clamp(
                (date - DateTimeOffset.UtcNow)
                    .TotalSeconds,
                1,
                12);
        }

        // Jitter: tránh các request thử lại
        // cùng một thời điểm khi API quá tải.

        return TimeSpan.FromSeconds(
            seconds +
            Random.Shared.Next(100, 501) / 1000.0);
    }

    // ========================================================
    // PARSE GOOGLE ERROR
    // ========================================================

    private static string GetGoogleErrorMessage(
        string errorBody)
    {
        try
        {
            using JsonDocument document =
                JsonDocument.Parse(errorBody);

            if (document.RootElement.TryGetProperty(
                    "error",
                    out JsonElement error) &&
                error.TryGetProperty(
                    "message",
                    out JsonElement message) &&
                message.ValueKind == JsonValueKind.String)
            {
                string? value =
                    message.GetString();

                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }
        catch (JsonException)
        {
            // Google có thể trả lỗi không phải JSON.
        }

        return "Không có thông tin chi tiết từ Google.";
    }
}
