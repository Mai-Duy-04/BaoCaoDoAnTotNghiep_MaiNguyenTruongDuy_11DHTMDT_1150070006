
using System;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TourWebApp.Data.Models;
using TourWebApp.Models;

namespace TourWebApp.Controllers
{
    public class TaiKhoanController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IDataProtector _checkinProtector;
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<TaiKhoanController> _logger;
        private readonly IConfiguration _configuration;

        private static readonly PasswordHasher<TaiKhoan>
            PasswordHasher = new();

        private static readonly Regex PhoneRegex = new(
            @"^0[35789][0-9]{8}$",
            RegexOptions.Compiled);

        private const string ResetSuccessMessage =
            "Nếu email đã được đăng ký với HappyTrip, " +
            "bạn sẽ nhận được hướng dẫn đặt lại mật khẩu. " +
            "Vui lòng kiểm tra cả thư mục Spam.";

        public TaiKhoanController(
            ApplicationDbContext context,
            IDataProtectionProvider dataProtectionProvider,
            IOptions<EmailSettings> emailOptions,
            ILogger<TaiKhoanController> logger,
            IConfiguration configuration)
        {
            _context = context;

            _checkinProtector = dataProtectionProvider
                .CreateProtector("HappyTrip.CheckInQrToken.v1");

            _emailSettings = emailOptions.Value;
            _logger = logger;
            _configuration = configuration;
        }

        // ==========================================
        // 1. CAC HAM HO TRO
        // ==========================================

        private static string NormalizeEmail(string? email)
        {
            return (email ?? "").Trim().ToLowerInvariant();
        }

        private static string NormalizePhone(string? phone)
        {
            return (phone ?? "").Trim();
        }

        private static bool IsValidPhone(string? phone)
        {
            return !string.IsNullOrWhiteSpace(phone)
                && PhoneRegex.IsMatch(phone);
        }

        private static bool IsValidEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email) ||
                email.Length > 100)
            {
                return false;
            }

            try
            {
                var address = new MailAddress(email);

                return string.Equals(
                    address.Address,
                    email,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsIdentityHash(string? value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.StartsWith(
                    "AQAAAA",
                    StringComparison.Ordinal)
                && value.Length >= 70;
        }

        private static bool VerifyPassword(
            TaiKhoan user,
            string? password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return false;
            }

            var storedPassword = user.MatKhau ?? "";

            if (IsIdentityHash(storedPassword))
            {
                try
                {
                    return PasswordHasher.VerifyHashedPassword(
                        user,
                        storedPassword,
                        password) != PasswordVerificationResult.Failed;
                }
                catch
                {
                    return false;
                }
            }

            // Ho tro tai khoan cu con luu mat khau thuong.
            // Sau khi dang nhap dung se nang cap sang hash.
            var oldPassword = SHA256.HashData(
                Encoding.UTF8.GetBytes(storedPassword));

            var suppliedPassword = SHA256.HashData(
                Encoding.UTF8.GetBytes(password));

            return CryptographicOperations.FixedTimeEquals(
                oldPassword,
                suppliedPassword);
        }

        private static string HashPassword(
            TaiKhoan user,
            string password)
        {
            return PasswordHasher.HashPassword(
                user,
                password);
        }

        private static bool IsValidResetToken(string? token)
        {
            return !string.IsNullOrWhiteSpace(token)
                && token.Length == 64
                && token.All(Uri.IsHexDigit);
        }

        private static string HashResetToken(string token)
        {
            return Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(token)));
        }

        private void LoadProfileOrders(int userId)
        {
            ViewBag.DonCuaToi = _context.DonDatTours
                .Include(d => d.IdTourNavigation)
                .Where(d => d.IdTaiKhoan == userId)
                .OrderByDescending(d => d.NgayDat)
                .ToList();
        }

        private string? GetTrustedResetBaseUrl()
        {
            var configured =
                _configuration["HappyTrip:PublicBaseUrl"];

            if (!string.IsNullOrWhiteSpace(configured) &&
                Uri.TryCreate(
                    configured,
                    UriKind.Absolute,
                    out var configuredUri))
            {
                if (configuredUri.Scheme == Uri.UriSchemeHttps &&
                    !configuredUri.IsLoopback &&
                    string.IsNullOrEmpty(configuredUri.UserInfo))
                {
                    return configuredUri.GetLeftPart(
                        UriPartial.Authority);
                }

                if (configuredUri.IsLoopback &&
                    (configuredUri.Scheme == Uri.UriSchemeHttp ||
                     configuredUri.Scheme == Uri.UriSchemeHttps))
                {
                    return configuredUri.GetLeftPart(
                        UriPartial.Authority);
                }
            }

            // Chi su dung Host tu request khi dang test localhost.
            var host = Request.Host.Host;

            if (host.Equals(
                    "localhost",
                    StringComparison.OrdinalIgnoreCase) ||
                host == "127.0.0.1")
            {
                return $"{Request.Scheme}://{Request.Host}";
            }

            return null;
        }

        // ==========================================
        // 2. DANG KY
        // ==========================================

        [HttpGet]
        public IActionResult DangKy()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DangKy(
            string HoTen,
            string Email,
            string SoDienThoai,
            string MatKhau,
            string MatKhauNhapLai)
        {
            var name = (HoTen ?? "").Trim();
            var email = NormalizeEmail(Email);
            var phone = NormalizePhone(SoDienThoai);

            if (string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(phone) ||
                string.IsNullOrWhiteSpace(MatKhau))
            {
                ViewBag.ThongBao =
                    "Vui lòng nhập đầy đủ thông tin.";
                return View();
            }

            if (name.Length > 100)
            {
                ViewBag.ThongBao =
                    "Họ và tên không được vượt quá 100 ký tự.";
                return View();
            }

            if (!IsValidEmail(email))
            {
                ViewBag.ThongBao =
                    "Địa chỉ email không hợp lệ.";
                return View();
            }

            if (!IsValidPhone(phone))
            {
                ViewBag.ThongBao =
                    "Số điện thoại phải có đúng 10 chữ số " +
                    "và bắt đầu bằng 03, 05, 07, 08 hoặc 09.";
                return View();
            }

            if (MatKhau.Length < 8 ||
                MatKhau.Length > 100)
            {
                ViewBag.ThongBao =
                    "Mật khẩu phải từ 8 đến 100 ký tự.";
                return View();
            }

            if (MatKhau != MatKhauNhapLai)
            {
                ViewBag.ThongBao =
                    "Mật khẩu xác nhận không khớp.";
                return View();
            }

            if (await _context.TaiKhoans.AnyAsync(
                    x => x.Email.ToLower() == email))
            {
                ViewBag.ThongBao =
                    "Email này đã được đăng ký.";
                return View();
            }

            if (await _context.TaiKhoans.AnyAsync(
                    x => x.SoDienThoai == phone))
            {
                ViewBag.ThongBao =
                    "Số điện thoại này đã được sử dụng.";
                return View();
            }

            var user = new TaiKhoan
            {
                HoTen = name,
                Email = email,
                SoDienThoai = phone,
                VaiTro = "User",
                TrangThai = true,
                NgayTao = DateTime.Now
            };

            user.MatKhau = HashPassword(
                user,
                MatKhau);

            try
            {
                _context.TaiKhoans.Add(user);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Dang ky gap loi rang buoc du lieu.");

                ViewBag.ThongBao =
                    "Email hoặc số điện thoại đã được sử dụng.";

                return View();
            }

            TempData["ToastSuccess"] =
                "Đăng ký thành công. Vui lòng đăng nhập.";

            return RedirectToAction(nameof(DangNhap));
        }

        // ==========================================
        // 3. DANG NHAP
        // ==========================================

        [HttpGet]
        public IActionResult DangNhap(string? returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;

            if (TempData["ThongBao"] is string message &&
                !string.IsNullOrWhiteSpace(message))
            {
                TempData["ToastInfo"] = message;
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DangNhap(
            string Email,
            string MatKhau,
            string? returnUrl)
        {
            var email = NormalizeEmail(Email);

            var user = await _context.TaiKhoans
                .FirstOrDefaultAsync(x =>
                    x.Email.ToLower() == email &&
                    x.TrangThai);

            if (user == null ||
                !VerifyPassword(user, MatKhau))
            {
                ViewBag.ThongBao =
                    "Email hoặc mật khẩu không đúng.";

                ViewBag.ReturnUrl = returnUrl;

                return View();
            }

            // Nang cap mat khau cu sang Identity Hash.
            if (!IsIdentityHash(user.MatKhau))
            {
                user.MatKhau = HashPassword(
                    user,
                    MatKhau);

                await _context.SaveChangesAsync();
            }

            HttpContext.Session.Clear();

            HttpContext.Session.SetInt32(
                "IdTaiKhoan",
                user.IdTaiKhoan);

            HttpContext.Session.SetString(
                "HoTen",
                user.HoTen);

            HttpContext.Session.SetString(
                "VaiTro",
                user.VaiTro);

            TempData["ToastSuccess"] =
                $"Đăng nhập thành công. Xin chào {user.HoTen}.";

            if (!string.IsNullOrWhiteSpace(returnUrl) &&
                Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            if (user.VaiTro == "Admin")
            {
                return RedirectToAction(
                    "Dashboard",
                    "QuanTri");
            }

            if (TempData["ReturnUrl"] is string savedUrl &&
                Url.IsLocalUrl(savedUrl))
            {
                return LocalRedirect(savedUrl);
            }

            return RedirectToAction(
                "Index",
                "Travel");
        }

        // ==========================================
        // 4. QUEN MAT KHAU QUA GMAIL
        // ==========================================

        [HttpGet]
        public IActionResult QuenMatKhau()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuenMatKhau(
            string Email)
        {
            var email = NormalizeEmail(Email);

            if (!IsValidEmail(email))
            {
                ViewBag.ThongBao =
                    "Vui lòng nhập địa chỉ email hợp lệ.";
                return View();
            }

            var user = await _context.TaiKhoans
                .FirstOrDefaultAsync(x =>
                    x.Email.ToLower() == email &&
                    x.TrangThai);

            // Khong tiet lo email co tai khoan hay khong.
            if (user == null)
            {
                ViewBag.ThongBaoThanhCong =
                    ResetSuccessMessage;

                return View();
            }

            var now = DateTime.UtcNow;

            // Gioi han gui: toi da 1 yeu cau/phut,
            // toi da 5 yeu cau/gio cho mot tai khoan.
            // Can bo sung gioi han theo IP khi deploy cong khai.
            var recentCount = await _context.Database
                .SqlQuery<int>(
                    $"""
                    SELECT COUNT(*) AS Value
                    FROM dbo.YeuCauDatLaiMatKhau
                    WHERE IdTaiKhoan = {user.IdTaiKhoan}
                      AND NgayTao >= {now.AddHours(-1)}
                    """)
                .SingleAsync();

            var sentWithinMinute = await _context.Database
                .SqlQuery<int>(
                    $"""
                    SELECT COUNT(*) AS Value
                    FROM dbo.YeuCauDatLaiMatKhau
                    WHERE IdTaiKhoan = {user.IdTaiKhoan}
                      AND NgayTao >= {now.AddMinutes(-1)}
                    """)
                .SingleAsync();

            if (recentCount >= 5 ||
                sentWithinMinute > 0)
            {
                ViewBag.ThongBaoThanhCong =
                    ResetSuccessMessage;

                return View();
            }

            var baseUrl = GetTrustedResetBaseUrl();

            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                _logger.LogWarning(
                    "Chua co URL tin cay de gui link dat lai mat khau.");

                ViewBag.ThongBaoThanhCong =
                    ResetSuccessMessage;

                return View();
            }

            // Token 256-bit ngau nhien.
            var token = Convert.ToHexString(
                RandomNumberGenerator.GetBytes(32));

            var tokenHash = HashResetToken(token);

            var expiresAt = now.AddMinutes(15);

            var resetUrl = baseUrl.TrimEnd('/') +
                "/TaiKhoan/DatLaiMatKhau?token=" +
                Uri.EscapeDataString(token);

            try
            {
                // Chi luu hash, khong luu token goc.
                await _context.Database
                    .ExecuteSqlInterpolatedAsync(
                        $"""
                        INSERT INTO dbo.YeuCauDatLaiMatKhau
                            (
                                IdTaiKhoan,
                                TokenHash,
                                NgayTao,
                                NgayHetHan
                            )
                        VALUES
                            (
                                {user.IdTaiKhoan},
                                {tokenHash},
                                {now},
                                {expiresAt}
                            )
                        """);

                await SendPasswordResetEmailAsync(
                    user.Email,
                    user.HoTen,
                    resetUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Khong the gui email khoi phuc tai khoan.");
            }

            ViewBag.ThongBaoThanhCong =
                ResetSuccessMessage;

            return View();
        }

        // ==========================================
        // 5. GUI EMAIL DAT LAI MAT KHAU
        // ==========================================

        private async Task SendPasswordResetEmailAsync(
            string toEmail,
            string customerName,
            string resetUrl)
        {
            var safeName = WebUtility.HtmlEncode(
                customerName);

            var safeUrl = WebUtility.HtmlEncode(
                resetUrl);

            var html = $$"""
            <!DOCTYPE html>
            <html lang="vi">
            <head>
                <meta charset="UTF-8">
                <meta name="viewport"
                      content="width=device-width,initial-scale=1">
                <title>HappyTrip - Đặt lại mật khẩu</title>
            </head>

            <body style="
                margin:0;
                padding:20px 10px;
                background:#edf3e9;
                font-family:Arial,Helvetica,sans-serif;">

                <table role="presentation"
                       width="100%"
                       cellpadding="0"
                       cellspacing="0"
                       border="0"
                       style="border-collapse:collapse;">

                    <tr>
                        <td align="center">

                            <table role="presentation"
                                   width="560"
                                   cellpadding="0"
                                   cellspacing="0"
                                   border="0"
                                   style="
                                       width:100%;
                                       max-width:560px;
                                       background:#fffdf8;
                                       border-collapse:collapse;">

                                <tr>
                                    <td align="center"
                                        style="
                                            background:#126842;
                                            padding:27px 20px;
                                            color:#ffffff;">

                                        <div style="
                                            font-size:27px;
                                            font-weight:bold;">
                                            HappyTrip
                                        </div>

                                        <div style="
                                            margin-top:8px;
                                            font-size:11px;
                                            color:#d5e9d4;">
                                            KHÁM PHÁ VIỆT NAM CÙNG BẠN
                                        </div>
                                    </td>
                                </tr>

                                <tr>
                                    <td align="center"
                                        style="padding:27px 23px 12px;">

                                        <div style="
                                            color:#187247;
                                            font-size:12px;
                                            font-weight:bold;">
                                            BẢO MẬT TÀI KHOẢN
                                        </div>

                                        <h1 style="
                                            margin:13px 0 5px;
                                            font-size:25px;
                                            color:#15583a;">
                                            Đặt lại mật khẩu
                                        </h1>
                                    </td>
                                </tr>

                                <tr>
                                    <td style="
                                        padding:15px 28px;
                                        color:#3f5949;
                                        font-size:14px;
                                        line-height:1.9;">

                                        Xin chào
                                        <strong>{{safeName}}</strong>,

                                        <p>
                                            HappyTrip đã nhận được yêu cầu
                                            đặt lại mật khẩu tài khoản
                                            của bạn.
                                        </p>

                                        <p>
                                            Vui lòng nhấn nút bên dưới
                                            để tạo mật khẩu mới.
                                        </p>
                                    </td>
                                </tr>

                                <tr>
                                    <td align="center"
                                        style="padding:10px 20px 28px;">

                                        <a href="{{safeUrl}}"
                                           style="
                                               display:inline-block;
                                               background:#187447;
                                               color:#ffffff;
                                               padding:15px 25px;
                                               border-radius:9px;
                                               text-decoration:none;
                                               font-size:14px;
                                               font-weight:bold;">
                                            ĐẶT LẠI MẬT KHẨU
                                        </a>

                                    </td>
                                </tr>

                                <tr>
                                    <td style="
                                        padding:20px 27px;
                                        background:#fff4d9;
                                        color:#765c30;
                                        font-size:13px;
                                        line-height:1.9;">

                                        <strong>
                                            Lưu ý bảo mật
                                        </strong>

                                        <p style="margin:8px 0;">
                                            Liên kết có hiệu lực
                                            trong <strong>15 phút</strong>
                                            và chỉ được dùng một lần.
                                        </p>

                                        <p style="margin:0;">
                                            Nếu bạn không yêu cầu
                                            đặt lại mật khẩu,
                                            hãy bỏ qua email này.
                                        </p>

                                    </td>
                                </tr>

                                <tr>
                                    <td align="center"
                                        style="
                                            padding:23px 20px;
                                            background:#e5f0e2;
                                            color:#476a51;
                                            font-size:12px;
                                            line-height:1.8;">

                                        <strong>
                                            HappyTrip Customer Support
                                        </strong>

                                        <br>

                                        Đi để yêu thêm Việt Nam.
                                    </td>
                                </tr>

                            </table>
                        </td>
                    </tr>
                </table>

            </body>
            </html>
            """;

            var plainText = $"""
                Xin chào {customerName},

                HappyTrip đã nhận được yêu cầu đặt lại mật khẩu.

                Truy cập liên kết sau:
                {resetUrl}

                Liên kết có hiệu lực trong 15 phút
                và chỉ được sử dụng một lần.

                Nếu không yêu cầu, vui lòng bỏ qua email này.

                HappyTrip Customer Support
                """;

            using var client = new SmtpClient(
                _emailSettings.SmtpHost,
                _emailSettings.SmtpPort)
            {
                EnableSsl = _emailSettings.EnableSsl,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(
                    _emailSettings.Username,
                    _emailSettings.Password),
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 30000
            };

            using var message = new MailMessage
            {
                From = new MailAddress(
                    _emailSettings.SenderEmail,
                    _emailSettings.SenderName),

                Subject =
                    "[HappyTrip] Yêu cầu đặt lại mật khẩu",

                SubjectEncoding = Encoding.UTF8,
                BodyEncoding = Encoding.UTF8
            };

            message.To.Add(
                new MailAddress(toEmail));

            message.AlternateViews.Add(
                AlternateView.CreateAlternateViewFromString(
                    plainText,
                    Encoding.UTF8,
                    MediaTypeNames.Text.Plain));

            message.AlternateViews.Add(
                AlternateView.CreateAlternateViewFromString(
                    html,
                    Encoding.UTF8,
                    MediaTypeNames.Text.Html));

            await client.SendMailAsync(message);
        }

        // ==========================================
        // 6. DAT LAI MAT KHAU - GET
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> DatLaiMatKhau(
            string? token)
        {
            Response.Headers["Referrer-Policy"] =
                "no-referrer";

            Response.Headers["Cache-Control"] =
                "no-store";

            if (!IsValidResetToken(token))
            {
                return View("DatLaiMatKhauHetHan");
            }

            var hash = HashResetToken(token!);
            var now = DateTime.UtcNow;

            var userId = await _context.Database
                .SqlQuery<int>(
                    $"""
                    SELECT r.IdTaiKhoan AS Value
                    FROM dbo.YeuCauDatLaiMatKhau AS r
                    INNER JOIN dbo.TaiKhoan AS t
                        ON t.IdTaiKhoan = r.IdTaiKhoan
                    WHERE r.TokenHash = {hash}
                      AND r.NgaySuDung IS NULL
                      AND r.NgayHetHan > {now}
                      AND t.TrangThai = 1
                    """)
                .FirstOrDefaultAsync();

            if (userId <= 0)
            {
                return View("DatLaiMatKhauHetHan");
            }

            ViewBag.Token = token;

            return View();
        }

        // ==========================================
        // 7. DAT LAI MAT KHAU - POST
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DatLaiMatKhau(
            string token,
            string MatKhauMoi,
            string NhapLaiMatKhauMoi)
        {
            Response.Headers["Referrer-Policy"] =
                "no-referrer";

            Response.Headers["Cache-Control"] =
                "no-store";

            if (!IsValidResetToken(token))
            {
                return View("DatLaiMatKhauHetHan");
            }

            ViewBag.Token = token;

            if (string.IsNullOrWhiteSpace(MatKhauMoi) ||
                MatKhauMoi.Length < 8 ||
                MatKhauMoi.Length > 100)
            {
                ViewBag.ThongBao =
                    "Mật khẩu phải từ 8 đến 100 ký tự.";

                return View();
            }

            if (MatKhauMoi != NhapLaiMatKhauMoi)
            {
                ViewBag.ThongBao =
                    "Mật khẩu xác nhận không khớp.";

                return View();
            }

            var hash = HashResetToken(token);
            var now = DateTime.UtcNow;

            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync();

            // Tim tai khoan cua token.
            var userId = await _context.Database
                .SqlQuery<int>(
                    $"""
                    SELECT IdTaiKhoan AS Value
                    FROM dbo.YeuCauDatLaiMatKhau
                    WHERE TokenHash = {hash}
                      AND NgaySuDung IS NULL
                      AND NgayHetHan > {now}
                    """)
                .FirstOrDefaultAsync();

            if (userId <= 0)
            {
                await transaction.RollbackAsync();

                return View("DatLaiMatKhauHetHan");
            }

            var user = await _context.TaiKhoans
                .FirstOrDefaultAsync(x =>
                    x.IdTaiKhoan == userId &&
                    x.TrangThai);

            if (user == null)
            {
                await transaction.RollbackAsync();

                return View("DatLaiMatKhauHetHan");
            }

            // Atomic UPDATE: neu token da duoc su dung
            // boi yeu cau khac, affected se bang 0.
            var affected = await _context.Database
                .ExecuteSqlInterpolatedAsync(
                    $"""
                    UPDATE dbo.YeuCauDatLaiMatKhau
                    SET NgaySuDung = {now}
                    WHERE TokenHash = {hash}
                      AND NgaySuDung IS NULL
                      AND NgayHetHan > {now}
                    """);

            if (affected != 1)
            {
                await transaction.RollbackAsync();

                return View("DatLaiMatKhauHetHan");
            }

            user.MatKhau = HashPassword(
                user,
                MatKhauMoi);

            await _context.SaveChangesAsync();

            // Huy tat ca token chua su dung cua tai khoan.
            await _context.Database
                .ExecuteSqlInterpolatedAsync(
                    $"""
                    UPDATE dbo.YeuCauDatLaiMatKhau
                    SET NgaySuDung = {now}
                    WHERE IdTaiKhoan = {userId}
                      AND NgaySuDung IS NULL
                    """);

            await transaction.CommitAsync();

            TempData["ToastSuccess"] =
                "Đặt lại mật khẩu thành công. " +
                "Vui lòng đăng nhập bằng mật khẩu mới.";

            return RedirectToAction(
                nameof(DangNhap));
        }

        // ==========================================
        // 8. HO SO - GET
        // ==========================================

        [HttpGet]
        public IActionResult HoSo()
        {
            var userId = HttpContext.Session
                .GetInt32("IdTaiKhoan");

            if (userId == null)
            {
                return RedirectToAction(nameof(DangNhap));
            }

            var user = _context.TaiKhoans
                .FirstOrDefault(x =>
                    x.IdTaiKhoan == userId);

            if (user == null)
            {
                return RedirectToAction(nameof(DangNhap));
            }

            LoadProfileOrders(userId.Value);

            return View(user);
        }

        // ==========================================
        // 9. CAP NHAT HO SO
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> HoSo(TaiKhoan model)
        {
            var userId = HttpContext.Session
                .GetInt32("IdTaiKhoan");

            if (userId == null ||
                model.IdTaiKhoan != userId.Value)
            {
                return RedirectToAction(nameof(DangNhap));
            }

            var user = await _context.TaiKhoans
                .FirstOrDefaultAsync(x =>
                    x.IdTaiKhoan == userId.Value);

            if (user == null)
            {
                return NotFound();
            }

            var name = (model.HoTen ?? "").Trim();
            var phone = NormalizePhone(model.SoDienThoai);
            var address = (model.DiaChi ?? "").Trim();

            // Khach cu co the chua co SDT.
            // Neu co nhap thi bat buoc dung 10 so.
            if (string.IsNullOrWhiteSpace(name) ||
                name.Length > 100)
            {
                ViewBag.ThongBao =
                    "Họ tên phải có từ 1 đến 100 ký tự.";

                LoadProfileOrders(userId.Value);
                return View(user);
            }

            if (!string.IsNullOrEmpty(phone) &&
                !IsValidPhone(phone))
            {
                ViewBag.ThongBao =
                    "Số điện thoại phải có đúng 10 chữ số hợp lệ.";

                LoadProfileOrders(userId.Value);
                return View(user);
            }

            if (address.Length > 255)
            {
                ViewBag.ThongBao =
                    "Địa chỉ không được vượt quá 255 ký tự.";

                LoadProfileOrders(userId.Value);
                return View(user);
            }

            if (!string.IsNullOrEmpty(phone))
            {
                var phoneExists =
                    await _context.TaiKhoans.AnyAsync(x =>
                        x.IdTaiKhoan != userId.Value &&
                        x.SoDienThoai == phone);

                if (phoneExists)
                {
                    ViewBag.ThongBao =
                        "Số điện thoại đã được sử dụng.";

                    LoadProfileOrders(userId.Value);
                    return View(user);
                }
            }

            user.HoTen = name;
            user.SoDienThoai =
                string.IsNullOrEmpty(phone)
                    ? null
                    : phone;

            user.DiaChi =
                string.IsNullOrEmpty(address)
                    ? null
                    : address;

            try
            {
                await _context.SaveChangesAsync();
                HttpContext.Session.SetString("HoTen", user.HoTen);

                ViewBag.ThongBao =
                    "Cập nhật hồ sơ thành công.";
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Cap nhat ho so vi pham rang buoc SQL.");

                ViewBag.ThongBao =
                    "Số điện thoại đã tồn tại hoặc dữ liệu không hợp lệ.";
            }

            LoadProfileOrders(userId.Value);

            return View(user);
        }

        // ==========================================
        // 10. DOI MAT KHAU TRONG HO SO
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DoiMatKhau(
            int idTaiKhoan,
            string matKhauHienTai,
            string matKhauMoi,
            string xacNhanMatKhau)
        {
            var userId = HttpContext.Session
                .GetInt32("IdTaiKhoan");

            if (userId == null ||
                idTaiKhoan != userId.Value)
            {
                return RedirectToAction(nameof(DangNhap));
            }

            var user = await _context.TaiKhoans
                .FirstOrDefaultAsync(x =>
                    x.IdTaiKhoan == idTaiKhoan);

            if (user == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(matKhauHienTai) ||
                string.IsNullOrWhiteSpace(matKhauMoi) ||
                string.IsNullOrWhiteSpace(xacNhanMatKhau))
            {
                ViewBag.LoiMatKhau =
                    "Vui lòng nhập đầy đủ thông tin.";
            }
            else if (!VerifyPassword(
                user,
                matKhauHienTai))
            {
                ViewBag.LoiMatKhau =
                    "Mật khẩu hiện tại không đúng.";
            }
            else if (matKhauMoi.Length < 8 ||
                     matKhauMoi.Length > 100)
            {
                ViewBag.LoiMatKhau =
                    "Mật khẩu mới phải từ 8 đến 100 ký tự.";
            }
            else if (matKhauMoi != xacNhanMatKhau)
            {
                ViewBag.LoiMatKhau =
                    "Xác nhận mật khẩu không khớp.";
            }
            else
            {
                user.MatKhau = HashPassword(
                    user,
                    matKhauMoi);

                await _context.SaveChangesAsync();

                // Huy token reset cu sau khi doi mat khau.
                var now = DateTime.UtcNow;

                await _context.Database
                    .ExecuteSqlInterpolatedAsync(
                        $"""
                        UPDATE dbo.YeuCauDatLaiMatKhau
                        SET NgaySuDung = {now}
                        WHERE IdTaiKhoan = {idTaiKhoan}
                          AND NgaySuDung IS NULL
                        """);

                ViewBag.ThongBaoMatKhau =
                    "Đổi mật khẩu thành công.";
            }

            LoadProfileOrders(userId.Value);

            return View("HoSo", user);
        }

        // ==========================================
        // 11. CHI TIET DON + QR CHECK-IN
        // ==========================================

        public IActionResult ChiTietDon(int idDon)
        {
            int? userId = HttpContext.Session
                .GetInt32("IdTaiKhoan");

            if (userId == null)
            {
                return RedirectToAction(nameof(DangNhap));
            }

            var don = _context.DonDatTours
                .Include(d => d.IdTourNavigation)
                .Include(d => d.IdLichNavigation)
                .Include(d => d.IdTaiKhoanNavigation)
                .FirstOrDefault(d =>
                    d.IdDon == idDon &&
                    d.IdTaiKhoan == userId);

            if (don == null)
            {
                return RedirectToAction(nameof(DonCuaToi));
            }

            var payload =
                $"{don.IdDon}|{don.MaBooking}|" +
                $"{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

            ViewBag.CheckInToken =
                _checkinProtector.Protect(payload);

            return View(don);
        }

        // ==========================================
        // 12. DON CUA TOI
        // ==========================================

        public IActionResult DonCuaToi()
        {
            int? userId = HttpContext.Session
                .GetInt32("IdTaiKhoan");

            if (userId == null)
            {
                return RedirectToAction(nameof(DangNhap));
            }

            var dsDon = _context.DonDatTours
                .Include(d => d.IdTourNavigation)
                .Include(d => d.IdLichNavigation)
                .Where(d => d.IdTaiKhoan == userId)
                .OrderByDescending(d => d.NgayDat)
                .ToList();

            return View(dsDon);
        }

        // ==========================================
        // 13. KHO PHIEU GIAM GIA
        // ==========================================

        [HttpGet]
        public IActionResult KhoPhieuGiamGia()
        {
            int? userId = HttpContext.Session
                .GetInt32("IdTaiKhoan");

            if (userId == null)
            {
                return RedirectToAction(
                    nameof(DangNhap),
                    new
                    {
                        returnUrl = "/TaiKhoan/KhoPhieuGiamGia"
                    });
            }

            var now = DateTime.Now;

            var khoDaLuu = _context.PhieuGiamGiaTaiKhoans
                .Include(x => x.IdPhieuGiamGiaNavigation)
                    .ThenInclude(x => x.IdTourNavigation)
                .Include(x => x.IdPhieuGiamGiaNavigation)
                    .ThenInclude(x => x.IdLoaiTourNavigation)
                .Where(x =>
                    x.IdTaiKhoan == userId &&
                    x.TrangThai)
                .OrderByDescending(x => x.NgayLuu)
                .ToList();

            var daLuuIds = khoDaLuu
                .Select(x => x.IdPhieuGiamGia)
                .Distinct()
                .ToList();

            var dsPhieuCoTheLuu = _context.PhieuGiamGias
                .Include(x => x.IdTourNavigation)
                .Include(x => x.IdLoaiTourNavigation)
                .Where(x =>
                    x.TrangThai &&
                    (!x.NgayBatDau.HasValue ||
                     x.NgayBatDau.Value <= now) &&
                    (!x.NgayKetThuc.HasValue ||
                     x.NgayKetThuc.Value >= now))
                .OrderBy(x =>
                    x.NgayKetThuc ?? DateTime.MaxValue)
                .ThenByDescending(x => x.GiaTriGiam)
                .ToList();

            ViewBag.DanhSachPhieuCoTheLuu =
                dsPhieuCoTheLuu;

            ViewBag.DaLuuIds = daLuuIds;

            return View(khoDaLuu);
        }

        // ==========================================
        // 14. LUU PHIEU GIAM GIA
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult LuuPhieuGiamGia(
            int idPhieuGiamGia)
        {
            int? userId = HttpContext.Session
                .GetInt32("IdTaiKhoan");

            if (userId == null)
            {
                return RedirectToAction(
                    nameof(DangNhap),
                    new
                    {
                        returnUrl = "/TaiKhoan/KhoPhieuGiamGia"
                    });
            }

            var now = DateTime.Now;

            var phieu = _context.PhieuGiamGias
                .FirstOrDefault(x =>
                    x.IdPhieuGiamGia == idPhieuGiamGia);

            if (phieu == null)
            {
                TempData["Error"] =
                    "Không tìm thấy phiếu giảm giá.";

                return RedirectToAction(
                    nameof(KhoPhieuGiamGia));
            }

            if (!phieu.TrangThai ||
                (phieu.NgayBatDau.HasValue &&
                 phieu.NgayBatDau.Value > now) ||
                (phieu.NgayKetThuc.HasValue &&
                 phieu.NgayKetThuc.Value < now))
            {
                TempData["Error"] =
                    "Phiếu giảm giá không còn hiệu lực.";

                return RedirectToAction(
                    nameof(KhoPhieuGiamGia));
            }

            var existing = _context.PhieuGiamGiaTaiKhoans
                .FirstOrDefault(x =>
                    x.IdTaiKhoan == userId &&
                    x.IdPhieuGiamGia == idPhieuGiamGia);

            if (existing != null)
            {
                if (existing.TrangThai)
                {
                    TempData["Info"] =
                        "Phiếu này đã có trong kho của bạn.";

                    return RedirectToAction(
                        nameof(KhoPhieuGiamGia));
                }

                existing.TrangThai = true;
                existing.NgayLuu = now;
            }
            else
            {
                _context.PhieuGiamGiaTaiKhoans.Add(
                    new PhieuGiamGiaTaiKhoan
                    {
                        IdTaiKhoan = userId.Value,
                        IdPhieuGiamGia = idPhieuGiamGia,
                        NgayLuu = now,
                        TrangThai = true
                    });
            }

            _context.SaveChanges();

            TempData["Success"] =
                "Đã lưu phiếu giảm giá vào kho.";

            return RedirectToAction(
                nameof(KhoPhieuGiamGia));
        }

        // ==========================================
        // 15. BO LUU PHIEU GIAM GIA
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult BoLuuPhieuGiamGia(
            int idPhieuGiamGia)
        {
            int? userId = HttpContext.Session
                .GetInt32("IdTaiKhoan");

            if (userId == null)
            {
                return RedirectToAction(
                    nameof(DangNhap),
                    new
                    {
                        returnUrl = "/TaiKhoan/KhoPhieuGiamGia"
                    });
            }

            var existing = _context.PhieuGiamGiaTaiKhoans
                .FirstOrDefault(x =>
                    x.IdTaiKhoan == userId &&
                    x.IdPhieuGiamGia == idPhieuGiamGia &&
                    x.TrangThai);

            if (existing != null)
            {
                existing.TrangThai = false;
                _context.SaveChanges();

                TempData["Success"] =
                    "Đã bỏ phiếu khỏi kho của bạn.";
            }
            else
            {
                TempData["Info"] =
                    "Phiếu này không có trong kho của bạn.";
            }

            return RedirectToAction(
                nameof(KhoPhieuGiamGia));
        }

        // ==========================================
        // 16. DANG XUAT
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DangXuat()
        {
            HttpContext.Session.Clear();

            return RedirectToAction(nameof(DangNhap));
        }
    }
}
