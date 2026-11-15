
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using TourWebApp.Models;

namespace TourWebApp.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;
    private readonly IWebHostEnvironment _environment;

    private const string SupportEmail = "aoi27072004@gmail.com";
    private const string SupportPhone = "+84 938 179 170";
    private const string SupportArea = "Bình Thạnh, TP.HCM";

    public EmailService(
        IOptions<EmailSettings> options,
        ILogger<EmailService> logger,
        IWebHostEnvironment environment)
    {
        _settings = options.Value;
        _logger = logger;
        _environment = environment;
    }

    // ==================================================
    // 1. EMAIL THANH TOAN - GIU NGUYEN
    // ==================================================
    public async Task SendPaymentSuccessEmailAsync(
        string toEmail,
        string customerName,
        string bookingCode,
        string tourName,
        DateTime? departureDate,
        decimal paidAmount)
    {
        if (string.IsNullOrWhiteSpace(toEmail) ||
            string.IsNullOrWhiteSpace(_settings.SmtpHost) ||
            string.IsNullOrWhiteSpace(_settings.SenderEmail))
        {
            _logger.LogWarning(
                "Bo qua email thanh toan vi thieu SMTP.");
            return;
        }

        var validEmail = new MailAddress(toEmail.Trim()).Address;

        var name = string.IsNullOrWhiteSpace(customerName)
            ? "Quý khách"
            : customerName.Trim();

        var departure = departureDate.HasValue
            ? departureDate.Value.ToString("dd/MM/yyyy")
            : "Theo lịch trình tour";

        var subject =
            $"[HappyTrip] Thanh toán thành công - {bookingCode}";

        var plainText = $"""
            Xin chào {name},

            HappyTrip xác nhận đã nhận thanh toán thành công
            cho đơn đặt tour của bạn.

            Mã booking: {bookingCode}
            Tour: {tourName}
            Ngày khởi hành: {departure}
            Số tiền đã thanh toán: {paidAmount:N0} đ

            LƯU Ý TRƯỚC CHUYẾN ĐI

            1. Kiểm tra thời gian và địa điểm tập trung.
            2. Có mặt đúng giờ theo hướng dẫn của tour.
            3. Chuẩn bị giấy tờ tùy thân cần thiết.
            4. Đăng nhập HappyTrip để xem QR Check-in.
            5. Liên hệ bộ phận hỗ trợ nếu cần thay đổi thông tin.

            Cảm ơn bạn đã lựa chọn HappyTrip.
            Chúc bạn có một chuyến đi an toàn và đáng nhớ!

            HappyTrip Customer Support
            Hotline: {SupportPhone}
            """;

        var html = BuildPaymentSuccessHtml(
            name,
            bookingCode,
            tourName,
            departureDate,
            paidAmount);

        await SendEmailInternalAsync(
            validEmail,
            subject,
            plainText,
            html,
            includeBrandImages: true);

        _logger.LogInformation(
            "SMTP da chap nhan email thanh toan cho booking {BookingCode}.",
            bookingCode);
    }
    
    private string BuildPaymentSuccessHtml(
        string customerName,
        string bookingCode,
        string tourName,
        DateTime? departureDate,
        decimal paidAmount)
    {
        // Ma hoa du lieu de an toan khi dua vao HTML.
        var name = Encode(customerName);
        var booking = Encode(bookingCode);
        var tour = Encode(tourName);

        var departure = departureDate.HasValue
            ? departureDate.Value.ToString("dd/MM/yyyy")
            : "Theo lịch trình tour";

        var amount = Encode(paidAmount.ToString("N0") + " đ");

        var supportPhone = Encode(SupportPhone);
        var supportEmail = Encode(SupportEmail);

        return $$"""
        <!DOCTYPE html>
        <html lang="vi">
        <head>
            <meta charset="UTF-8">
            <meta name="viewport"
                content="width=device-width,initial-scale=1.0">
            <title>HappyTrip - Xác nhận thanh toán</title>
        </head>

        <body style="
            margin:0;
            padding:0;
            background:#eaf1e9;
            font-family:Arial,Helvetica,sans-serif;
            color:#254333;">

            <div style="
                display:none;
                font-size:1px;
                color:#eaf1e9;">
                Đơn đặt tour {{booking}} đã thanh toán thành công.
            </div>

            <table role="presentation"
                cellpadding="0"
                cellspacing="0"
                border="0"
                width="100%"
                style="
                    width:100%;
                    border-collapse:collapse;
                    background:#eaf1e9;">

                <tr>
                    <td align="center"
                        style="padding:20px 9px;">

                        <table role="presentation"
                            width="640"
                            cellpadding="0"
                            cellspacing="0"
                            border="0"
                            style="
                                width:100%;
                                max-width:640px;
                                border-collapse:collapse;
                                background:#fffdf7;">

                            <!-- BANNER DU LICH -->
                            <tr>
                                <td style="padding:0;">
                                    <img src="cid:happytrip-header"
                                        alt="HappyTrip - Hành trình Việt Nam"
                                        width="640"
                                        style="
                                            display:block;
                                            width:100%;
                                            height:auto;
                                            border:0;">
                                </td>
                            </tr>

                            <!-- LOI CHUC MUNG -->
                            <tr>
                                <td align="center"
                                    style="padding:27px 20px 15px;">

                                    <div style="
                                        display:inline-block;
                                        background:#e0f1e2;
                                        color:#147345;
                                        border-radius:24px;
                                        padding:10px 17px;
                                        font-size:12px;
                                        font-weight:bold;">
                                        &#10003; THANH TOÁN THÀNH CÔNG
                                    </div>

                                    <h1 style="
                                        margin:17px 0 9px;
                                        color:#10553a;
                                        font-size:27px;
                                        line-height:1.35;
                                        font-weight:bold;">
                                        Hành trình đã sẵn sàng!
                                    </h1>

                                    <p style="
                                        margin:0;
                                        color:#728575;
                                        font-size:14px;
                                        line-height:1.8;">
                                        Một chuyến đi đáng nhớ đang chờ đón bạn.
                                    </p>

                                </td>
                            </tr>

                            <!-- LOI CHAO -->
                            <tr>
                                <td style="padding:13px 27px 20px;">

                                    <h2 style="
                                        margin:0 0 12px;
                                        color:#17563d;
                                        font-size:20px;
                                        line-height:1.5;">
                                        Xin chào {{name}},
                                    </h2>

                                    <p style="
                                        margin:0;
                                        color:#455e4d;
                                        font-size:13px;
                                        line-height:1.9;">
                                        HappyTrip vui mừng thông báo
                                        <strong>đơn đặt tour của bạn đã
                                        được thanh toán thành công</strong>
                                        và ghi nhận trên hệ thống.

                                        Cảm ơn bạn đã lựa chọn HappyTrip
                                        đồng hành trong hành trình sắp tới!
                                    </p>

                                </td>
                            </tr>

                            <!-- BOOKING PASS -->
                            <tr>
                                <td style="padding:0 26px 22px;">

                                    <table role="presentation"
                                        width="100%"
                                        cellpadding="0"
                                        cellspacing="0"
                                        border="0"
                                        style="
                                            width:100%;
                                            background:#fff0c9;
                                            border:1px dashed #cfad66;">

                                        <tr>
                                            <td align="center"
                                                style="padding:19px 13px;">

                                                <div style="
                                                    color:#80652e;
                                                    font-size:10px;
                                                    letter-spacing:2px;
                                                    font-weight:bold;">
                                                    HAPPYTRIP BOOKING PASS
                                                </div>

                                                <div style="
                                                    margin-top:8px;
                                                    color:#10553a;
                                                    font-size:22px;
                                                    line-height:1.5;
                                                    font-weight:bold;
                                                    overflow-wrap:anywhere;">
                                                    {{booking}}
                                                </div>

                                                <div style="
                                                    margin-top:6px;
                                                    color:#826b3d;
                                                    font-size:11px;">
                                                    Vui lòng lưu lại mã đặt tour này.
                                                </div>

                                            </td>
                                        </tr>

                                    </table>

                                </td>
                            </tr>

                            <!-- CARD TOUR + POSTCARD -->
                            <tr>
                                <td style="padding:0 25px 17px;">

                                    <table role="presentation"
                                        width="100%"
                                        cellpadding="0"
                                        cellspacing="0"
                                        border="0"
                                        style="width:100%;">

                                        <tr>
                                            <td valign="top"
                                                width="62%"
                                                style="padding-right:9px;">

                                                <table role="presentation"
                                                    width="100%"
                                                    cellpadding="0"
                                                    cellspacing="0"
                                                    border="0"
                                                    style="
                                                        width:100%;
                                                        background:#f3f8ef;
                                                        border:1px solid #d9e7d6;">

                                                    <tr>
                                                        <td style="
                                                            padding:13px;
                                                            background:#1e734e;
                                                            color:white;
                                                            font-size:13px;
                                                            font-weight:bold;">
                                                            THÔNG TIN HÀNH TRÌNH
                                                        </td>
                                                    </tr>

                                                    <tr>
                                                        <td style="padding:15px 12px;">

                                                            <div style="
                                                                color:#18533c;
                                                                font-size:15px;
                                                                font-weight:bold;
                                                                line-height:1.65;
                                                                overflow-wrap:anywhere;">
                                                                {{tour}}
                                                            </div>

                                                            <div style="
                                                                height:1px;
                                                                margin:14px 0;
                                                                background:#d8e5d6;">
                                                            </div>

                                                            <div style="
                                                                color:#788979;
                                                                font-size:11px;">
                                                                Ngày khởi hành
                                                            </div>

                                                            <div style="
                                                                margin-top:5px;
                                                                color:#214c36;
                                                                font-size:14px;
                                                                font-weight:bold;">
                                                                {{departure}}
                                                            </div>

                                                        </td>
                                                    </tr>

                                                </table>

                                            </td>

                                            <td valign="top"
                                                align="center"
                                                width="38%"
                                                style="padding-left:5px;">

                                                <table role="presentation"
                                                    width="100%"
                                                    cellpadding="0"
                                                    cellspacing="0"
                                                    border="0"
                                                    style="
                                                        width:100%;
                                                        background:#ffffff;
                                                        border:1px solid #e6e4d9;">

                                                    <tr>
                                                        <td align="center"
                                                            style="padding:8px;">

                                                            <img src="cid:happytrip-postcard"
                                                                alt="Postcard du lịch HappyTrip"
                                                                width="185"
                                                                style="
                                                                    display:block;
                                                                    width:100%;
                                                                    height:auto;
                                                                    border:0;">

                                                            <div style="
                                                                padding:10px 0 3px;
                                                                color:#236e4b;
                                                                font-size:12px;
                                                                font-style:italic;
                                                                font-weight:bold;">
                                                                Let's explore!
                                                            </div>

                                                        </td>
                                                    </tr>

                                                </table>

                                            </td>
                                        </tr>

                                    </table>

                                </td>
                            </tr>

                            <!-- SO TIEN -->
                            <tr>
                                <td style="padding:0 25px 19px;">

                                    <table role="presentation"
                                        width="100%"
                                        cellpadding="0"
                                        cellspacing="0"
                                        border="0"
                                        style="
                                            width:100%;
                                            background:#e5f1e2;">

                                        <tr>
                                            <td valign="middle"
                                                style="padding:17px 14px;">

                                                <div style="
                                                    color:#56775e;
                                                    font-size:11px;
                                                    font-weight:bold;">
                                                    SỐ TIỀN ĐÃ THANH TOÁN
                                                </div>

                                                <div style="
                                                    margin-top:7px;
                                                    color:#117247;
                                                    font-size:25px;
                                                    line-height:1.4;
                                                    font-weight:bold;">
                                                    {{amount}}
                                                </div>

                                            </td>

                                            <td align="right"
                                                valign="middle"
                                                width="79"
                                                style="padding:10px 13px;">

                                                <img src="cid:happytrip-stamp"
                                                    width="65"
                                                    alt="Dấu xác nhận HappyTrip"
                                                    style="
                                                        display:block;
                                                        width:65px;
                                                        height:auto;
                                                        border:0;">

                                            </td>
                                        </tr>

                                    </table>

                                </td>
                            </tr>

                            <!-- NHAC NHO TRUOC CHUYEN DI -->
                            <tr>
                                <td style="padding:0 25px 20px;">

                                    <table role="presentation"
                                        width="100%"
                                        cellpadding="0"
                                        cellspacing="0"
                                        border="0"
                                        style="
                                            width:100%;
                                            background:#fff8e8;
                                            border-left:4px solid #e0b352;">

                                        <tr>
                                            <td style="padding:19px 18px;">

                                                <h2 style="
                                                    margin:0 0 13px;
                                                    color:#735420;
                                                    font-size:17px;">
                                                    LƯU Ý TRƯỚC CHUYẾN ĐI
                                                </h2>

                                                <p style="
                                                    margin:0 0 11px;
                                                    color:#514c3f;
                                                    font-size:12px;
                                                    line-height:1.8;">
                                                    <strong>1. Kiểm tra lịch trình:</strong>
                                                    Xem lại ngày khởi hành,
                                                    địa điểm và thời gian
                                                    tập trung theo hướng dẫn
                                                    của tour.
                                                </p>

                                                <p style="
                                                    margin:0 0 11px;
                                                    color:#514c3f;
                                                    font-size:12px;
                                                    line-height:1.8;">
                                                    <strong>2. Đến đúng giờ:</strong>
                                                    Có mặt tại điểm tập trung
                                                    theo thời gian được thông báo
                                                    để không ảnh hưởng lịch trình.
                                                </p>

                                                <p style="
                                                    margin:0 0 11px;
                                                    color:#514c3f;
                                                    font-size:12px;
                                                    line-height:1.8;">
                                                    <strong>3. Giấy tờ cần thiết:</strong>
                                                    Chuẩn bị CCCD, hộ chiếu
                                                    hoặc giấy tờ khác theo
                                                    yêu cầu của tour.
                                                </p>

                                                <p style="
                                                    margin:0;
                                                    color:#514c3f;
                                                    font-size:12px;
                                                    line-height:1.8;">
                                                    <strong>4. Vé điện tử:</strong>
                                                    Đăng nhập HappyTrip,
                                                    mở đơn đặt tour của bạn
                                                    và chuẩn bị mã QR Check-in.
                                                </p>

                                            </td>
                                        </tr>

                                    </table>

                                </td>
                            </tr>

                            <!-- HUONG DAN XEM VE -->
                            <tr>
                                <td align="center"
                                    style="padding:5px 28px 25px;">

                                    <div style="
                                        display:inline-block;
                                        padding:12px 17px;
                                        background:#1b704b;
                                        color:#ffffff;
                                        font-size:13px;
                                        font-weight:bold;">
                                        VÉ ĐIỆN TỬ & QR CHECK-IN
                                    </div>

                                    <p style="
                                        margin:13px 0 0;
                                        color:#718474;
                                        font-size:12px;
                                        line-height:1.8;">
                                        Vui lòng đăng nhập website HappyTrip
                                        để xem đơn đặt tour và QR Check-in.

                                        Không cần in email này
                                        nếu đã chuẩn bị vé điện tử.
                                    </p>

                                </td>
                            </tr>

                            <!-- CHU KY + HO TRO -->
                            <tr>
                                <td style="
                                    background:#e4efe3;
                                    padding:20px 25px;">

                                    <p style="
                                        margin:0 0 9px;
                                        color:#15583b;
                                        font-size:15px;
                                        font-weight:bold;">
                                        HappyTrip Customer Support
                                    </p>

                                    <p style="
                                        margin:0;
                                        color:#4c6b55;
                                        font-size:12px;
                                        line-height:1.85;">
                                        Cần hỗ trợ về giờ tập trung,
                                        lịch trình hoặc thông tin đặt tour?
                                        Hãy liên hệ với HappyTrip.
                                        <br>
                                        Hotline: <strong>{{supportPhone}}</strong>
                                        <br>
                                        Email: {{supportEmail}}
                                    </p>

                                    <p style="
                                        margin:14px 0 0;
                                        color:#316a4b;
                                        font-size:12px;
                                        font-style:italic;">
                                        Chúc bạn một hành trình thật đáng nhớ!
                                    </p>

                                </td>
                            </tr>

                            <tr>
                                <td align="center"
                                    style="
                                        padding:13px;
                                        background:#153e30;
                                        color:#e9d8a9;
                                        font-size:10px;">
                                    © 2026 HappyTrip · Đi để yêu thêm Việt Nam
                                </td>
                            </tr>

                        </table>

                    </td>
                </tr>

            </table>

        </body>
        </html>
        """;
    }

    // ==================================================
    // 2. GUI EMAIL PHAN HOI TU VAN
    // ==================================================

    public async Task SendContactReplyEmailAsync(
        string toEmail,
        string customerName,
        string replyContent,
        string? customerPhone = null,
        string? destination = null)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            throw new ArgumentException(
                "Email người nhận không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(replyContent))
        {
            throw new ArgumentException(
                "Nội dung phản hồi không được để trống.");
        }

        var content = replyContent.Trim();

        if (content.Length < 10 || content.Length > 5000)
        {
            throw new ArgumentException(
                "Nội dung phản hồi phải từ 10 đến 5000 ký tự.");
        }

        var validEmail =
            new MailAddress(toEmail.Trim()).Address;

        var name = string.IsNullOrWhiteSpace(customerName)
            ? "Quý khách"
            : customerName.Trim();

        var subject =
            "[HappyTrip] Phản hồi yêu cầu tư vấn du lịch";

        var plainText = $"""
            HAPPYTRIP - PHẢN HỒI YÊU CẦU TƯ VẤN

            Họ tên khách hàng: {name}
            Email khách hàng: {validEmail}
            Số điện thoại khách hàng: {FormatOptional(customerPhone)}
            Điểm đến mong muốn: {FormatOptional(destination)}

            NỘI DUNG PHẢN HỒI

            {content}

            Nếu cần hỗ trợ thêm, bạn có thể phản hồi
            trực tiếp email này.

            TRUNG TÂM HỖ TRỢ DU LỊCH HAPPYTRIP
            Email: {SupportEmail}
            Hotline: {SupportPhone}
            Khu vực: {SupportArea}
            """;

        var html = BuildContactReplyHtml(
            customerName: name,
            replyContent: content,
            headerImageUrl: "cid:happytrip-header",
            footerImageUrl: "",
            postcardImageUrl: "cid:happytrip-postcard",
            stampImageUrl: "cid:happytrip-stamp",
            customerEmail: validEmail,
            customerPhone: customerPhone,
            destination: destination);

        await SendEmailInternalAsync(
            validEmail,
            subject,
            plainText,
            html,
            includeBrandImages: true);

        _logger.LogInformation(
            "SMTP da chap nhan email phan hoi HappyTrip.");
    }

    // ==================================================
    // 3. HAM HO TRO GIAO DIEN
    // ==================================================

    private static string FormatOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "Không cung cấp"
            : value.Trim();
    }

    private static string Encode(string? value)
    {
        return WebUtility.HtmlEncode(value ?? "");
    }

    private static string NormalizeText(string? value)
    {
        return (value ?? string.Empty)
            .Trim()
            .Replace("\r\n", "\n")
            .Replace("\r", "\n");
    }

    // Chi nhan dien loi chao o DAU noi dung.
    private static bool HasOpeningGreeting(string text)
    {
        var lines = NormalizeText(text)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var firstLine = lines.FirstOrDefault()?.Trim() ?? "";

        return firstLine.StartsWith(
                   "Xin chào",
                   StringComparison.OrdinalIgnoreCase)
            || firstLine.StartsWith(
                   "Kính gửi",
                   StringComparison.OrdinalIgnoreCase)
            || firstLine.StartsWith(
                   "Chào bạn",
                   StringComparison.OrdinalIgnoreCase);
    }

    // Chi xem la chu ky neu nam o CUOI noi dung.
    // Khong nham voi cau "doi ngu HappyTrip se ho tro".
    private static bool HasClosingSignature(string text)
    {
        var lines = NormalizeText(text)
            .Split(
                '\n',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

        if (lines.Length == 0)
            return false;

        var lastLine = lines[^1]
            .Trim()
            .TrimEnd('.', ',', '!', ' ');

        return lastLine.Equals(
                   "Đội ngũ chăm sóc khách hàng HappyTrip",
                   StringComparison.OrdinalIgnoreCase)
            || lastLine.Equals(
                   "Đội ngũ HappyTrip",
                   StringComparison.OrdinalIgnoreCase)
            || lastLine.Equals(
                   "Bộ phận chăm sóc khách hàng HappyTrip",
                   StringComparison.OrdinalIgnoreCase)
            || lastLine.Equals(
                   "Trân trọng",
                   StringComparison.OrdinalIgnoreCase);
    }

    // Giu nguyen cac doan Admin nhap.
    // Chi giam khoang cach hien thi trong HTML.
    private static string BuildReplyParagraphs(string content)
    {
        var normalized = NormalizeText(content);

        if (string.IsNullOrWhiteSpace(normalized))
            return "";

        var paragraphs = Regex.Split(
            normalized,
            @"\n[ \t]*\n+");

        var builder = new StringBuilder();

        foreach (var paragraph in paragraphs)
        {
            var trimmed = paragraph.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            var safeText = Encode(trimmed)
                .Replace("\n", "<br />");

            builder.Append(
                """
                <p style="
                    margin:0 0 11px;
                    padding:0;
                    color:#304739;
                    font-family:Arial,Helvetica,sans-serif;
                    font-size:13px;
                    line-height:1.75;
                    overflow-wrap:anywhere;
                    word-break:normal;">
                """
            );

            builder.Append(safeText);
            builder.Append("</p>");
        }

        return builder.ToString();
    }

    // ==================================================
    // 4. GIAO DIEN EMAIL HTML
    // ==================================================

    public string BuildContactReplyHtml(
        string customerName,
        string replyContent,
        string headerImageUrl,
        string footerImageUrl,
        string postcardImageUrl,
        string stampImageUrl,
        string? customerEmail = null,
        string? customerPhone = null,
        string? destination = null)
    {
        // Giu tham so de tuong thich voi Controller.
        // Ban compact khong su dung anh footer cu.
        _ = footerImageUrl;

        var name = Encode(FormatOptional(customerName));
        var email = Encode(FormatOptional(customerEmail));
        var phone = Encode(FormatOptional(customerPhone));
        var place = Encode(FormatOptional(destination));

        var rawContent = NormalizeText(replyContent);
        var contentHtml = BuildReplyParagraphs(rawContent);

        var header = Encode(headerImageUrl);
        var postcard = Encode(postcardImageUrl);
        var stamp = Encode(stampImageUrl);

        var showGreeting =
            !HasOpeningGreeting(rawContent);

        var showSignature =
            !HasClosingSignature(rawContent);

        var greetingBlock = showGreeting
            ? $"""
              <tr>
                  <td style="
                      padding:0 25px 8px;
                      color:#0c6040;
                      font-family:Arial,Helvetica,sans-serif;
                      font-size:22px;
                      font-weight:bold;
                      line-height:1.4;">
                      Xin chào {name},
                  </td>
              </tr>
              """
            : "";

        var signatureBlock = showSignature
            ? """
              <tr>
                  <td style="
                      padding:5px 25px 19px;
                      font-family:Arial,Helvetica,sans-serif;
                      font-size:13px;
                      color:#26533c;
                      line-height:1.65;">

                      <table role="presentation"
                             cellpadding="0"
                             cellspacing="0"
                             border="0">
                          <tr>
                              <td valign="middle"
                                  style="
                                      padding-right:10px;
                                      color:#247d50;
                                      font-size:21px;">
                                  ✈
                              </td>
                              <td valign="middle"
                                  style="
                                      color:#26533c;
                                      font-size:13px;
                                      line-height:1.65;">
                                  Trân trọng,<br>
                                  <strong style="color:#0c6040;">
                                      Đội ngũ chăm sóc khách hàng HappyTrip
                                  </strong>
                              </td>
                          </tr>
                      </table>
                  </td>
              </tr>
              """
            : "";

        return $$"""
        <!DOCTYPE html>
        <html lang="vi">
        <head>
            <meta charset="UTF-8">
            <meta name="viewport"
                  content="width=device-width,initial-scale=1.0">
            <title>HappyTrip - Phản hồi yêu cầu tư vấn</title>
        </head>

        <body style="
            margin:0;
            padding:0;
            background-color:#edf3ed;
            font-family:Arial,Helvetica,sans-serif;
            color:#273f32;">

            <table role="presentation"
                   width="100%"
                   cellpadding="0"
                   cellspacing="0"
                   border="0"
                   style="
                       width:100%;
                       border-collapse:collapse;
                       background-color:#edf3ed;">

                <tr>
                    <td align="center"
                        style="padding:14px 8px;">

                        <table role="presentation"
                               width="640"
                               cellpadding="0"
                               cellspacing="0"
                               border="0"
                               style="
                                   width:100%;
                                   max-width:640px;
                                   border-collapse:collapse;
                                   background-color:#fffdf9;">

                            <!-- HEADER -->
                            <tr>
                                <td style="padding:0;">
                                    <img src="{{header}}"
                                         width="640"
                                         alt="HappyTrip - Đồng hành cùng mọi chuyến đi"
                                         style="
                                             display:block;
                                             width:100%;
                                             height:auto;
                                             border:0;">
                                </td>
                            </tr>

                            <!-- BADGE VA DAU BUU DIEN -->
                            <tr>
                                <td style="padding:17px 25px 8px;">

                                    <table role="presentation"
                                           width="100%"
                                           cellpadding="0"
                                           cellspacing="0"
                                           border="0"
                                           style="
                                               width:100%;
                                               border-collapse:collapse;">

                                        <tr>
                                            <td valign="middle"
                                                style="padding:0;">

                                                <table role="presentation"
                                                       cellpadding="0"
                                                       cellspacing="0"
                                                       border="0">
                                                    <tr>
                                                        <td style="
                                                            padding:9px 14px;
                                                            background-color:#fff0c4;
                                                            color:#76551c;
                                                            font-size:12px;
                                                            font-weight:bold;
                                                            border-radius:25px;">
                                                            ✉ PHẢN HỒI YÊU CẦU TƯ VẤN
                                                        </td>
                                                    </tr>
                                                </table>

                                            </td>

                                            <td width="80"
                                                align="right"
                                                valign="middle"
                                                style="padding:0 0 0 5px;">

                                                <img src="{{stamp}}"
                                                     width="76"
                                                     alt="Dấu bưu điện HappyTrip"
                                                     style="
                                                         display:block;
                                                         width:76px;
                                                         max-width:100%;
                                                         height:auto;
                                                         border:0;">

                                            </td>
                                        </tr>
                                    </table>
                                </td>
                            </tr>

                            <!-- LOI CHAO -->
                            {{greetingBlock}}

                            <!-- MO DAU -->
                            <tr>
                                <td style="
                                    padding:0 25px 12px;
                                    color:#435648;
                                    font-size:13px;
                                    line-height:1.7;">
                                    Cảm ơn bạn đã liên hệ với
                                    <strong>HappyTrip</strong>.
                                    Chúng mình đã tiếp nhận yêu cầu
                                    và gửi đến bạn thông tin phản hồi.
                                </td>
                            </tr>

                            <!-- NHAN VANG -->
                            <tr>
                                <td style="padding:0 25px 14px;">
                                    <table role="presentation"
                                           cellpadding="0"
                                           cellspacing="0"
                                           border="0">
                                        <tr>
                                            <td width="52"
                                                height="4"
                                                style="
                                                    background-color:#edc34d;
                                                    border-radius:4px;
                                                    font-size:0;">
                                                &nbsp;
                                            </td>
                                        </tr>
                                    </table>
                                </td>
                            </tr>

                            <!-- BANG THONG TIN + POSTCARD -->
                            <tr>
                                <td style="padding:0 21px 17px;">

                                    <table role="presentation"
                                           width="100%"
                                           cellpadding="0"
                                           cellspacing="0"
                                           border="0"
                                           style="
                                               width:100%;
                                               border-collapse:collapse;">

                                        <tr>
                                            <!-- BANG KHACH -->
                                            <td width="59%"
                                                valign="middle"
                                                style="
                                                    width:59%;
                                                    padding-right:9px;">

                                                <table role="presentation"
                                                       width="100%"
                                                       cellpadding="0"
                                                       cellspacing="0"
                                                       border="0"
                                                       style="
                                                           width:100%;
                                                           background-color:#ffffff;
                                                           border:1px solid #d6e5d6;
                                                           border-collapse:separate;
                                                           border-spacing:0;
                                                           border-radius:12px;">

                                                    <tr>
                                                        <td style="
                                                            padding:12px;
                                                            background-color:#e8f1e5;
                                                            color:#0c6040;
                                                            font-size:12px;
                                                            font-weight:bold;
                                                            border-radius:11px 11px 0 0;">
                                                            THÔNG TIN NGƯỜI GỬI YÊU CẦU
                                                        </td>
                                                    </tr>

                                                    <tr>
                                                        <td style="padding:8px 11px 10px;">

                                                            <table role="presentation"
                                                                   width="100%"
                                                                   cellpadding="0"
                                                                   cellspacing="0"
                                                                   border="0"
                                                                   style="
                                                                       width:100%;
                                                                       border-collapse:collapse;">

                                                                <tr>
                                                                    <td width="40%"
                                                                        valign="top"
                                                                        style="
                                                                            padding:8px 3px;
                                                                            border-bottom:1px solid #e6ece6;
                                                                            color:#647566;
                                                                            font-size:11px;">
                                                                        Họ và tên
                                                                    </td>
                                                                    <td valign="top"
                                                                        style="
                                                                            padding:8px 3px;
                                                                            border-bottom:1px solid #e6ece6;
                                                                            color:#213e2e;
                                                                            font-size:11px;
                                                                            font-weight:bold;
                                                                            overflow-wrap:anywhere;">
                                                                        {{name}}
                                                                    </td>
                                                                </tr>

                                                                <tr>
                                                                    <td valign="top"
                                                                        style="
                                                                            padding:8px 3px;
                                                                            border-bottom:1px solid #e6ece6;
                                                                            color:#647566;
                                                                            font-size:11px;">
                                                                        Email khách
                                                                    </td>
                                                                    <td valign="top"
                                                                        style="
                                                                            padding:8px 3px;
                                                                            border-bottom:1px solid #e6ece6;
                                                                            color:#213e2e;
                                                                            font-size:11px;
                                                                            overflow-wrap:anywhere;
                                                                            word-break:break-word;">
                                                                        {{email}}
                                                                    </td>
                                                                </tr>

                                                                <tr>
                                                                    <td valign="top"
                                                                        style="
                                                                            padding:8px 3px;
                                                                            border-bottom:1px solid #e6ece6;
                                                                            color:#647566;
                                                                            font-size:11px;">
                                                                        SĐT khách
                                                                    </td>
                                                                    <td valign="top"
                                                                        style="
                                                                            padding:8px 3px;
                                                                            border-bottom:1px solid #e6ece6;
                                                                            color:#213e2e;
                                                                            font-size:11px;
                                                                            font-weight:bold;">
                                                                        {{phone}}
                                                                    </td>
                                                                </tr>

                                                                <tr>
                                                                    <td valign="top"
                                                                        style="
                                                                            padding:8px 3px 2px;
                                                                            color:#647566;
                                                                            font-size:11px;">
                                                                        Điểm đến
                                                                    </td>
                                                                    <td valign="top"
                                                                        style="
                                                                            padding:8px 3px 2px;
                                                                            color:#213e2e;
                                                                            font-size:11px;
                                                                            overflow-wrap:anywhere;">
                                                                        {{place}}
                                                                    </td>
                                                                </tr>

                                                            </table>
                                                        </td>
                                                    </tr>
                                                </table>
                                            </td>

                                            <!-- POSTCARD -->
                                            <td width="41%"
                                                valign="middle"
                                                align="center"
                                                style="
                                                    width:41%;
                                                    padding-left:5px;">

                                                <table role="presentation"
                                                       width="100%"
                                                       cellpadding="0"
                                                       cellspacing="0"
                                                       border="0"
                                                       style="
                                                           width:100%;
                                                           max-width:212px;
                                                           background-color:#ffffff;
                                                           border:1px solid #e3e1d9;
                                                           border-collapse:separate;
                                                           border-spacing:0;
                                                           border-radius:10px;
                                                           box-shadow:0 5px 12px rgba(28,65,43,0.12);
                                                           transform:rotate(-3deg);">

                                                    <tr>
                                                        <td align="center"
                                                            style="
                                                                padding:0;
                                                                height:12px;
                                                                font-size:0;">
                                                            <span style="
                                                                display:inline-block;
                                                                width:57px;
                                                                height:12px;
                                                                background-color:#f5dfa4;">
                                                                &nbsp;
                                                            </span>
                                                        </td>
                                                    </tr>

                                                    <tr>
                                                        <td style="padding:7px 8px 0;">
                                                            <img src="{{postcard}}"
                                                                 width="190"
                                                                 alt="Khám phá Việt Nam"
                                                                 style="
                                                                     display:block;
                                                                     width:100%;
                                                                     height:auto;
                                                                     border:0;
                                                                     border-radius:4px;">
                                                        </td>
                                                    </tr>

                                                    <tr>
                                                        <td align="center"
                                                            style="
                                                                padding:9px 4px 11px;
                                                                color:#245d42;
                                                                font-family:Georgia,serif;
                                                                font-style:italic;
                                                                font-size:12px;">
                                                            Khám phá Việt Nam ♡
                                                        </td>
                                                    </tr>
                                                </table>

                                            </td>
                                        </tr>

                                    </table>
                                </td>
                            </tr>

                            <!-- KHUNG PHAN HOI -->
                            <tr>
                                <td style="padding:0 25px 13px;">

                                    <table role="presentation"
                                           width="100%"
                                           cellpadding="0"
                                           cellspacing="0"
                                           border="0"
                                           style="
                                               width:100%;
                                               background-color:#f7faf5;
                                               border:1px solid #d4e5d5;
                                               border-collapse:separate;
                                               border-spacing:0;
                                               border-radius:12px;">

                                        <tr>
                                            <td style="
                                                padding:12px 16px;
                                                background-color:#21764e;
                                                color:#ffffff;
                                                font-size:13px;
                                                font-weight:bold;
                                                border-radius:11px 11px 0 0;">
                                                NỘI DUNG PHẢN HỒI TỪ HAPPYTRIP
                                            </td>
                                        </tr>

                                        <tr>
                                            <td style="
                                                padding:16px 17px 6px;
                                                background-color:#f7faf5;
                                                border-radius:0 0 11px 11px;
                                                vertical-align:top;">
                                                {{contentHtml}}
                                            </td>
                                        </tr>

                                    </table>
                                </td>
                            </tr>

                            <!-- LOI KET -->
                            <tr>
                                <td style="
                                    padding:0 25px 10px;
                                    color:#49604f;
                                    font-size:12px;
                                    line-height:1.7;">
                                    Nếu cần thêm thông tin,
                                    bạn có thể phản hồi trực tiếp
                                    email này để HappyTrip tiếp tục hỗ trợ.
                                </td>
                            </tr>

                            <!-- CHU KY -->
                            {{signatureBlock}}

                            <!-- FOOTER DUY NHAT -->
                            <tr>
                                <td style="
                                    padding:17px 24px 13px;
                                    background-color:#e6f0e5;
                                    border-top:1px solid #cfded0;">

                                    <table role="presentation"
                                           width="100%"
                                           cellpadding="0"
                                           cellspacing="0"
                                           border="0"
                                           style="
                                               width:100%;
                                               border-collapse:collapse;">

                                        <tr>
                                            <!-- THUONG HIEU -->
                                            <td width="36%"
                                                valign="middle"
                                                style="
                                                    padding:3px 11px 3px 0;
                                                    border-right:1px solid #cbdcca;">

                                                <div style="
                                                    font-size:21px;
                                                    font-weight:bold;
                                                    line-height:1.25;
                                                    color:#247d4a;">
                                                    Happy<span style="color:#d4a72f;">Trip</span>
                                                </div>

                                                <div style="
                                                    padding-top:4px;
                                                    color:#245d40;
                                                    font-size:10px;
                                                    line-height:1.5;
                                                    font-weight:bold;">
                                                    TRUNG TÂM HỖ TRỢ DU LỊCH
                                                </div>
                                            </td>

                                            <!-- LIEN HE -->
                                            <td width="64%"
                                                valign="middle"
                                                style="
                                                    padding:3px 0 3px 14px;
                                                    font-size:11px;
                                                    line-height:1.85;
                                                    color:#28553b;">

                                                <div>
                                                    <strong>Email:</strong>
                                                    <a href="mailto:aoi27072004@gmail.com"
                                                       style="
                                                           color:#195b3d;
                                                           text-decoration:none;">
                                                        aoi27072004@gmail.com
                                                    </a>
                                                </div>

                                                <div>
                                                    <strong>Hotline:</strong>
                                                    <a href="tel:+84938179170"
                                                       style="
                                                           color:#195b3d;
                                                           text-decoration:none;">
                                                        +84 938 179 170
                                                    </a>
                                                </div>

                                                <div>
                                                    <strong>Khu vực:</strong>
                                                    Bình Thạnh, TP.HCM
                                                </div>
                                            </td>

                                        </tr>
                                    </table>

                                    <!-- COPYRIGHT -->
                                    <table role="presentation"
                                           width="100%"
                                           cellpadding="0"
                                           cellspacing="0"
                                           border="0"
                                           style="
                                               width:100%;
                                               border-collapse:collapse;
                                               margin-top:11px;">

                                        <tr>
                                            <td align="center"
                                                style="
                                                    padding-top:9px;
                                                    border-top:1px solid #cbdcca;
                                                    color:#54775e;
                                                    font-size:10px;
                                                    line-height:1.4;">
                                                © 2026 HappyTrip · Đi để yêu thêm Việt Nam
                                            </td>
                                        </tr>
                                    </table>

                                </td>
                            </tr>

                        </table>
                    </td>
                </tr>
            </table>
        </body>
        </html>
        """;
    }

    // ==================================================
    // 5. GUI EMAIL QUA SMTP
    // ==================================================

    private async Task SendEmailInternalAsync(
        string toEmail,
        string subject,
        string plainTextBody,
        string? htmlBody = null,
        bool includeBrandImages = false)
    {
        ValidateSmtpSettings();

        try
        {
            using var client = new SmtpClient(
                _settings.SmtpHost,
                _settings.SmtpPort)
            {
                EnableSsl = _settings.EnableSsl,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(
                    _settings.Username,
                    _settings.Password),
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 30000
            };

            using var message = new MailMessage
            {
                From = new MailAddress(
                    _settings.SenderEmail,
                    _settings.SenderName),
                Subject = subject,
                SubjectEncoding = Encoding.UTF8,
                BodyEncoding = Encoding.UTF8,
                Body = plainTextBody,
                IsBodyHtml = false
            };

            message.To.Add(new MailAddress(toEmail));

            if (!string.IsNullOrWhiteSpace(htmlBody))
            {
                var textView =
                    AlternateView.CreateAlternateViewFromString(
                        plainTextBody,
                        Encoding.UTF8,
                        MediaTypeNames.Text.Plain);

                var htmlView =
                    AlternateView.CreateAlternateViewFromString(
                        htmlBody,
                        Encoding.UTF8,
                        MediaTypeNames.Text.Html);

                if (includeBrandImages)
                {
                    var webRoot = _environment.WebRootPath;

                    if (string.IsNullOrWhiteSpace(webRoot))
                    {
                        throw new InvalidOperationException(
                            "Không tìm thấy thư mục wwwroot.");
                    }

                    var folder = Path.Combine(
                        webRoot,
                        "img",
                        "email");

                    AddImage(
                        htmlView,
                        Path.Combine(
                            folder,
                            "happytrip-email-header.png"),
                        "happytrip-header");

                    AddImage(
                        htmlView,
                        Path.Combine(
                            folder,
                            "happytrip-email-postcard.png"),
                        "happytrip-postcard");

                    AddImage(
                        htmlView,
                        Path.Combine(
                            folder,
                            "happytrip-email-stamp.png"),
                        "happytrip-stamp");
                }

                message.AlternateViews.Add(textView);
                message.AlternateViews.Add(htmlView);
            }

            await client.SendMailAsync(message);
        }
        catch (SmtpException ex)
        {
            _logger.LogError(
                ex,
                "Gui email qua SMTP that bai.");

            throw new InvalidOperationException(
                "Không thể gửi email. Vui lòng kiểm tra SMTP.",
                ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Xay ra loi khi gui email.");

            throw;
        }
    }

    // ==================================================
    // 6. NHUNG ANH CID
    // ==================================================

    private static void AddImage(
        AlternateView htmlView,
        string imagePath,
        string contentId)
    {
        if (!File.Exists(imagePath))
        {
            throw new FileNotFoundException(
                $"Không tìm thấy ảnh email: {imagePath}");
        }

        var resource = new LinkedResource(
            imagePath,
            MediaTypeNames.Image.Png)
        {
            ContentId = contentId,
            TransferEncoding = TransferEncoding.Base64
        };

        htmlView.LinkedResources.Add(resource);
    }

    // ==================================================
    // 7. KIEM TRA SMTP
    // ==================================================

    private void ValidateSmtpSettings()
    {
        if (string.IsNullOrWhiteSpace(_settings.SmtpHost) ||
            string.IsNullOrWhiteSpace(_settings.SenderEmail) ||
            string.IsNullOrWhiteSpace(_settings.Username) ||
            string.IsNullOrWhiteSpace(_settings.Password))
        {
            throw new InvalidOperationException(
                "Chưa cấu hình đầy đủ SMTP cho HappyTrip.");
        }

        if (_settings.SmtpPort < 1 ||
            _settings.SmtpPort > 65535)
        {
            throw new InvalidOperationException(
                "Cổng SMTP không hợp lệ.");
        }
    }
}
