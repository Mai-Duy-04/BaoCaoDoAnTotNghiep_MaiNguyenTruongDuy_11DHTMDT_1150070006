using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TourWebApp.Data.Models;
using TourWebApp.Services;

namespace TourWebApp.Controllers;

public class QuanLyLienHeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly ILogger<QuanLyLienHeController> _logger;

    public QuanLyLienHeController(
        ApplicationDbContext context,
        IEmailService emailService,
        ILogger<QuanLyLienHeController> logger)
    {
        _context = context;
        _emailService = emailService;
        _logger = logger;
    }

    private bool LaAdmin()
    {
        return HttpContext.Session.GetString("VaiTro") == "Admin";
    }

    private IActionResult NeuKhongPhaiAdmin()
    {
        return RedirectToAction("DangNhap", "TaiKhoan");
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? trangThai)
    {
        if (!LaAdmin())
            return NeuKhongPhaiAdmin();

        var query = _context.LienHes
            .AsNoTracking()
            .AsQueryable();

        if (trangThai == "Chưa phản hồi" ||
            trangThai == "Đã phản hồi")
        {
            query = query.Where(x => x.TrangThai == trangThai);
        }

        var danhSach = await query
            .OrderBy(x => x.TrangThai == "Đã phản hồi")
            .ThenByDescending(x => x.NgayGui)
            .ToListAsync();

        ViewBag.TrangThai = trangThai;

        ViewBag.TongYeuCau =
            await _context.LienHes.CountAsync();

        ViewBag.ChuaPhanHoi =
            await _context.LienHes.CountAsync(
                x => x.TrangThai == "Chưa phản hồi");

        ViewBag.DaPhanHoi =
            await _context.LienHes.CountAsync(
                x => x.TrangThai == "Đã phản hồi");

        return View(danhSach);
    }

    [HttpGet]
    public async Task<IActionResult> ChiTiet(int id)
    {
        if (!LaAdmin())
            return NeuKhongPhaiAdmin();

        var lienHe = await _context.LienHes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IdLienHe == id);

        if (lienHe == null)
            return NotFound();

        return View(lienHe);
    }

    private static string? LayDiemDen(string? noiDung)
    {
        if (string.IsNullOrWhiteSpace(noiDung))
            return null;

        const string prefix = "Điểm đến mong muốn:";

        var normalized = noiDung
            .Replace("\r\n", "\n")
            .Replace("\r", "\n");

        var firstLine = normalized
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?.Trim();

        if (string.IsNullOrWhiteSpace(firstLine))
            return null;

        if (!firstLine.StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var destination = firstLine
            .Substring(prefix.Length)
            .Trim();

        return string.IsNullOrWhiteSpace(destination)
            ? null
            : destination;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> XemTruocEmail(
        int id,
        string? noiDungPhanHoi)
    {
        if (!LaAdmin())
            return NeuKhongPhaiAdmin();

        var lienHe = await _context.LienHes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IdLienHe == id);

        if (lienHe == null)
            return NotFound();

        if (lienHe.TrangThai == "Đã phản hồi")
        {
            return BadRequest("Yêu cầu này đã được phản hồi.");
        }

        if (string.IsNullOrWhiteSpace(noiDungPhanHoi) ||
            noiDungPhanHoi.Trim().Length < 10 ||
            noiDungPhanHoi.Trim().Length > 5000)
        {
            return BadRequest(
                "Nội dung phản hồi phải từ 10 đến 5000 ký tự.");
        }

        var headerUrl = Url.Content("~/img/email/happytrip-email-header.png");
        var footerUrl = Url.Content("~/img/email/happytrip-email-footer.png");
        var postcardUrl = Url.Content("~/img/email/happytrip-email-postcard.png");
        var stampUrl = Url.Content("~/img/email/happytrip-email-stamp.png");

        var html = _emailService.BuildContactReplyHtml(
            customerName: lienHe.HoTen,
            replyContent: noiDungPhanHoi.Trim(),
            headerImageUrl: headerUrl!,
            footerImageUrl: footerUrl!,
            postcardImageUrl: postcardUrl!,
            stampImageUrl: stampUrl!,
            customerEmail: lienHe.Email,
            customerPhone: lienHe.DienThoai,
            destination: LayDiemDen(lienHe.NoiDung)
        );

        Response.Headers["Cache-Control"] = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Security-Policy"] =
            "default-src 'none'; " +
            "img-src 'self' data:; " +
            "style-src 'unsafe-inline'; " +
            "base-uri 'none'; " +
            "form-action 'none'";

        return Content(html, "text/html; charset=utf-8");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuiPhanHoi(
        int id,
        string? noiDungPhanHoi)
    {
        if (!LaAdmin())
            return NeuKhongPhaiAdmin();

        var lienHe = await _context.LienHes
            .FirstOrDefaultAsync(x => x.IdLienHe == id);

        if (lienHe == null)
            return NotFound();

        if (lienHe.TrangThai == "Đã phản hồi")
        {
            TempData["Loi"] =
                "Yêu cầu này đã được phản hồi trước đó.";

            return RedirectToAction(nameof(ChiTiet), new { id });
        }

        if (string.IsNullOrWhiteSpace(noiDungPhanHoi))
        {
            TempData["Loi"] =
                "Vui lòng nhập nội dung phản hồi.";

            return RedirectToAction(nameof(ChiTiet), new { id });
        }

        noiDungPhanHoi = noiDungPhanHoi.Trim();

        if (noiDungPhanHoi.Length < 10 ||
            noiDungPhanHoi.Length > 5000)
        {
            TempData["Loi"] =
                "Nội dung phản hồi phải từ 10 đến 5000 ký tự.";

            return RedirectToAction(nameof(ChiTiet), new { id });
        }

        try
        {
            await _emailService.SendContactReplyEmailAsync(
                toEmail: lienHe.Email,
                customerName: lienHe.HoTen,
                replyContent: noiDungPhanHoi,
                customerPhone: lienHe.DienThoai,
                destination: LayDiemDen(lienHe.NoiDung)
            );

            lienHe.NoiDungPhanHoi = noiDungPhanHoi;
            lienHe.NgayPhanHoi = DateTime.Now;
            lienHe.TrangThai = "Đã phản hồi";

            await _context.SaveChangesAsync();

            TempData["ThanhCong"] =
                "Đã gửi email phản hồi và lưu kết quả thành công.";
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Loi gui hoac luu phan hoi lien he ID {IdLienHe}",
                id);

            TempData["Loi"] =
                "Có lỗi khi gửi hoặc lưu phản hồi. " +
                "Vui lòng kiểm tra nhật ký và hộp thư trước khi gửi lại.";
        }

        return RedirectToAction(nameof(ChiTiet), new { id });
    }
}