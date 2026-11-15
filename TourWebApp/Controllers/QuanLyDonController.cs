
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using ClosedXML.Excel;
using TourWebApp.Data.Models;
using TourWebApp.Models;
using TourWebApp.Services;

namespace TourWebApp.Controllers
{
    public class QuanLyDonController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IDataProtector _checkinProtector;
        private readonly ISeatInventoryService _seatInventory;

        private const string VoucherStatusHold = "GiuCho";
        private const string VoucherStatusUsed = "DaSuDung";
        private const string VoucherStatusCancelled = "DaHuy";

        public QuanLyDonController(
            ApplicationDbContext context,
            IDataProtectionProvider dataProtectionProvider,
            ISeatInventoryService seatInventory)
        {
            _context = context;

            _checkinProtector = dataProtectionProvider
                .CreateProtector("HappyTrip.CheckInQrToken.v1");

            _seatInventory = seatInventory;
        }

        private bool IsAdmin()
        {
            return HttpContext.Session.GetString("VaiTro") == "Admin";
        }

        private int? GetAdminId()
        {
            if (!IsAdmin()) return null;

            return HttpContext.Session.GetInt32("IdTaiKhoan");
        }

        // =====================================================
        // DANH SACH DON DAT TOUR
        // =====================================================

        [HttpGet]
        public IActionResult Index(
            string trangThai = "TatCa",
            DateTime? ngayDatTu = null,
            DateTime? ngayDatDen = null,
            string phuongThucTT = "TatCa",
            string? tuKhoa = null)
        {
            if (!IsAdmin())
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }

            ViewBag.PendingCashCount = _context.DonDatTours.Count(d =>
                d.TrangThai ==
                    BookingPaymentStatus.TrangThaiChoXacNhanTienMat
                && !d.DaThanhToan);

            var keyword = string.IsNullOrWhiteSpace(tuKhoa)
                ? null
                : tuKhoa.Trim();

            var query = BuildOrderQuery(
                trangThai,
                ngayDatTu,
                ngayDatDen,
                phuongThucTT,
                keyword);

            ViewBag.TrangThai = trangThai;
            ViewBag.NgayDatTu = ngayDatTu?.ToString("yyyy-MM-dd");
            ViewBag.NgayDatDen = ngayDatDen?.ToString("yyyy-MM-dd");
            ViewBag.PhuongThucTT = phuongThucTT;
            ViewBag.TuKhoa = keyword ?? string.Empty;

            return View(query.ToList());
        }

        // =====================================================
        // XUAT EXCEL
        // =====================================================

        [HttpGet]
        public IActionResult ExportExcel(
            string trangThai = "TatCa",
            DateTime? ngayDatTu = null,
            DateTime? ngayDatDen = null,
            string phuongThucTT = "TatCa",
            string? tuKhoa = null)
        {
            if (!IsAdmin())
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }

            var keyword = string.IsNullOrWhiteSpace(tuKhoa)
                ? null
                : tuKhoa.Trim();

            var danhSachDon = BuildOrderQuery(
                    trangThai,
                    ngayDatTu,
                    ngayDatDen,
                    phuongThucTT,
                    keyword)
                .AsNoTracking()
                .ToList();

            using var workbook = new XLWorkbook();

            var worksheet = workbook.Worksheets.Add("DonDatTour");

            string[] headers =
            {
                "STT",
                "Ma booking",
                "Khach hang",
                "Tour",
                "Ngay dat",
                "Phuong thuc TT",
                "Tong goc",
                "Tong thanh toan",
                "Trang thai"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(1, i + 1).Value = headers[i];
            }

            var headerRange = worksheet.Range(
                1, 1, 1, headers.Length);

            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor =
                XLColor.LightGray;

            for (int i = 0; i < danhSachDon.Count; i++)
            {
                var don = danhSachDon[i];
                int row = i + 2;

                decimal thanhTien = Math.Max(
                    0m,
                    don.TongTienSauGiam ??
                    (don.TongTien - (don.SoTienGiam ?? 0m)));

                worksheet.Cell(row, 1).Value = i + 1;
                worksheet.Cell(row, 2).Value = don.MaBooking;

                worksheet.Cell(row, 3).Value =
                    don.IdTaiKhoanNavigation?.HoTen
                    ?? "Chua cap nhat";

                worksheet.Cell(row, 4).Value =
                    don.IdTourNavigation?.TenTour
                    ?? "Khong xac dinh";

                worksheet.Cell(row, 5).Value = don.NgayDat;

                worksheet.Cell(row, 6).Value =
                    don.PhuongThucTT ?? string.Empty;

                worksheet.Cell(row, 7).Value = don.TongTien;
                worksheet.Cell(row, 8).Value = thanhTien;

                worksheet.Cell(row, 9).Value =
                    GetTrangThaiHienThi(don);
            }

            worksheet.Column(5).Style.DateFormat.Format =
                "dd/MM/yyyy HH:mm";

            worksheet.Column(7).Style.NumberFormat.Format = "#,##0";
            worksheet.Column(8).Style.NumberFormat.Format = "#,##0";

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();

            workbook.SaveAs(stream);

            string fileName =
                $"DanhSachDon_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        // =====================================================
        // CHI TIET DON
        // =====================================================

        [HttpGet]
        public IActionResult ChiTiet(int id)
        {
            if (!IsAdmin())
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }

            var don = _context.DonDatTours
                .Include(d => d.IdTourNavigation)
                .Include(d => d.IdLichNavigation)
                .Include(d => d.IdTaiKhoanNavigation)
                .Include(d => d.IdPhieuGiamGiaNavigation)
                .FirstOrDefault(d => d.IdDon == id);

            if (don == null) return NotFound();

            ViewBag.DaCheckIn = DaCheckIn(id);

            return View(don);
        }

        // =====================================================
        // QUET QR - HIEN THI THE VE
        // =====================================================

        [HttpGet]
        public IActionResult ScanCheckIn(string token)
        {
            if (!IsAdmin())
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                TempData["Error"] = "Ma QR khong hop le.";

                return RedirectToAction(nameof(Index));
            }

            if (!TryDecodeCheckInToken(
                token,
                out var idDon,
                out var maBooking))
            {
                TempData["Error"] =
                    "Ma QR khong hop le hoac da bi thay doi.";

                return RedirectToAction(nameof(Index));
            }

            var don = _context.DonDatTours
                .Include(d => d.IdTourNavigation)
                .Include(d => d.IdLichNavigation)
                .Include(d => d.IdTaiKhoanNavigation)
                .FirstOrDefault(d =>
                    d.IdDon == idDon &&
                    d.MaBooking == maBooking);

            if (don == null)
            {
                TempData["Error"] =
                    "Khong tim thay don dat tour tu ma QR.";

                return RedirectToAction(nameof(Index));
            }

            ViewBag.CheckInToken = token;
            ViewBag.DaCheckIn = DaCheckIn(don.IdDon);

            // Quet QR chi doc du lieu, khong tu ghi Check-in.
            return View(don);
        }

        // =====================================================
        // XAC NHAN CHECK-IN CA DON DAT TOUR
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult XacNhanCheckIn(
            int idDon,
            string? token)
        {
            int? adminId = GetAdminId();

            if (!adminId.HasValue || adminId.Value <= 0)
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }

            // Bat buoc xac minh token QR, khong chi tin idDon
            // duoc gui tu giao dien.
            if (string.IsNullOrWhiteSpace(token) ||
                !TryDecodeCheckInToken(
                    token,
                    out var tokenIdDon,
                    out var tokenMaBooking))
            {
                TempData["Error"] =
                    "Token Check-in khong hop le.";

                return RedirectToAction(nameof(Index));
            }

            if (tokenIdDon != idDon)
            {
                TempData["Error"] =
                    "Thong tin don khong khop ma QR.";

                return RedirectToAction(nameof(Index));
            }

            var don = _context.DonDatTours
                .FirstOrDefault(d =>
                    d.IdDon == idDon &&
                    d.MaBooking == tokenMaBooking);

            if (don == null)
            {
                TempData["Error"] =
                    "Khong tim thay don dat tour hop le.";

                return RedirectToAction(nameof(Index));
            }

            string scanUrl = Url.Action(
                nameof(ScanCheckIn),
                "QuanLyDon",
                new { token }) ?? "/QuanLyDon";

            if (don.TrangThai == BookingPaymentStatus.TrangThaiDaHuy)
            {
                TempData["Error"] =
                    "Don da huy, khong the Check-in.";

                return Redirect(scanUrl);
            }

            if (!don.DaThanhToan)
            {
                TempData["Error"] =
                    "Don chua thanh toan thanh cong.";

                return Redirect(scanUrl);
            }

            // Du lieu cu dung HoanTat de danh dau Check-in.
            // Khong tu Check-in lai nhung don nay, tranh
            // ghi nhan trung khi chuyen sang bang moi.
            if (don.TrangThai == "HoanTat" &&
                !DaCheckIn(don.IdDon))
            {
                TempData["Error"] =
                    "Don dang o trang thai HoanTat cu. " +
                    "Can doi chieu lich su Check-in truoc khi ghi nhan.";

                return Redirect(scanUrl);
            }

            if (DaCheckIn(don.IdDon))
            {
                TempData["Info"] =
                    "Don nay da duoc Check-in truoc do.";

                return Redirect(scanUrl);
            }

            int soKhach =
                don.NguoiLon +
                don.TreEm +
                don.TreNho;

            if (soKhach <= 0)
            {
                TempData["Error"] =
                    "So luong khach khong hop le.";

                return Redirect(scanUrl);
            }

            try
            {
                // Cau lenh INSERT nguyen tu:
                // UPDLOCK + HOLDLOCK ket hop UNIQUE(IdDon)
                // ngan hai lan xac nhan tao hai ban ghi.
                int affected = _context.Database.ExecuteSqlInterpolated($@"
INSERT INTO dbo.DonDatTourCheckIn
    (IdDon, NgayCheckIn, SoKhachCheckIn, IdNguoiXacNhan)
SELECT
    {don.IdDon},
    SYSDATETIME(),
    {soKhach},
    {adminId.Value}
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.DonDatTourCheckIn WITH (UPDLOCK, HOLDLOCK)
    WHERE IdDon = {don.IdDon}
);
");

                if (affected == 0)
                {
                    TempData["Info"] =
                        "Don nay da Check-in. Khong ghi nhan trung.";
                }
                else
                {
                    TempData["Success"] =
                        "Check-in thanh cong cho toan bo " +
                        soKhach +
                        " khach trong don " +
                        don.MaBooking +
                        ".";

                    // KHONG gan don.TrangThai = "HoanTat".
                    // Check-in va hoan tat tour la hai nghiep vu khac nhau.
                }
            }
            catch (SqlException ex)
                when (ex.Number == 2601 || ex.Number == 2627)
            {
                TempData["Info"] =
                    "Don nay da duoc Check-in truoc do.";
            }

            return Redirect(scanUrl);
        }

        // =====================================================
        // KIEM TRA CHECK-IN TU SQL
        // Khong can them DbSet vao ApplicationDbContext.
        // =====================================================

        private bool DaCheckIn(int idDon)
        {
            int count = _context.Database
                .SqlQueryRaw<int>(
                    @"SELECT COUNT(*) AS [Value]
                      FROM dbo.DonDatTourCheckIn
                      WHERE IdDon = {0}",
                    idDon)
                .Single();

            return count > 0;
        }

        // =====================================================
        // DUYET DON
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Duyet(int id)
        {
            if (!IsAdmin())
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }

            var don = _context.DonDatTours.Find(id);

            if (don == null) return NotFound();

            if (don.DaThanhToan)
            {
                TempData["Error"] =
                    "Don da thanh toan, khong can duyet.";

                return RedirectToAction("ChiTiet", new { id });
            }

            if (don.TrangThai == BookingPaymentStatus.TrangThaiDaHuy)
            {
                TempData["Error"] =
                    "Khong the duyet don da huy.";

                return RedirectToAction("ChiTiet", new { id });
            }

            don.TrangThai = "DaDuyet";

            _context.SaveChanges();

            TempData["Success"] = "Da duyet don.";

            return RedirectToAction("ChiTiet", new { id });
        }

        // =====================================================
        // HUY DON CHUA THANH TOAN
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Huy(int id, string? ghiChu)
        {
            if (!IsAdmin())
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }

            var don = _context.DonDatTours
                .FirstOrDefault(x => x.IdDon == id);

            if (don == null) return NotFound();

            if (don.DaThanhToan || DaCheckIn(don.IdDon))
            {
                TempData["Error"] =
                    "Don da thanh toan hoac Check-in, khong the huy theo luong nay.";

                return RedirectToAction("ChiTiet", new { id });
            }

            if (don.TrangThai == BookingPaymentStatus.TrangThaiDaHuy)
            {
                TempData["Info"] =
                    "Don da duoc huy truoc do.";

                return RedirectToAction("ChiTiet", new { id });
            }

            don.TrangThai = BookingPaymentStatus.TrangThaiDaHuy;
            don.GhiChu = ghiChu;
            don.NgayHuy = DateTime.Now;
            don.TrangThaiThanhToan = "DaHuy";

            ThuHoiLuotPhieuNeuCan(
                don,
                "Admin huy don",
                xoaBanGhiSuDung: false);

            _context.SaveChanges();

            _seatInventory
                .SynchronizeAsync(don.IdLich, don.IdTour)
                .GetAwaiter()
                .GetResult();

            TempData["Success"] = "Da huy don.";

            return RedirectToAction("ChiTiet", new { id });
        }

        // =====================================================
        // XAC NHAN THU TIEN MAT
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult XacNhanDonChoThuTienMat(int id)
        {
            if (!IsAdmin())
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }

            var don = _context.DonDatTours
                .Include(x => x.IdLichNavigation)
                .Include(x => x.IdTourNavigation)
                .FirstOrDefault(x => x.IdDon == id);

            if (don == null) return NotFound();

            if (don.DaThanhToan)
            {
                TempData["Info"] =
                    "Don da duoc xac nhan thanh toan truoc do.";

                return RedirectToAction("ChiTiet", new { id });
            }

            if (don.TrangThai == BookingPaymentStatus.TrangThaiDaHuy)
            {
                TempData["Error"] =
                    "Don da huy, khong the xac nhan.";

                return RedirectToAction("ChiTiet", new { id });
            }

            bool dungPhuongThuc = string.Equals(
                don.PhuongThucTT,
                BookingPaymentStatus.PhuongThucTienMat,
                StringComparison.OrdinalIgnoreCase);

            bool dungTrangThai =
                don.TrangThai ==
                BookingPaymentStatus.TrangThaiChoXacNhanTienMat;

            if (!dungPhuongThuc || !dungTrangThai)
            {
                TempData["Error"] =
                    "Chi xac nhan duoc don dang cho thu tien mat.";

                return RedirectToAction("ChiTiet", new { id });
            }

            don.DaThanhToan = true;

            don.TrangThai =
                BookingPaymentStatus.TrangThaiThanhToanThanhCong;

            don.TrangThaiThanhToan =
                BookingPaymentStatus.TrangThaiTtThanhToanThanhCong;

            don.NgayThanhToan = DateTime.Now;

            don.PhuongThucTT =
                BookingPaymentStatus.PhuongThucTienMat;

            if (don.IdPhieuGiamGia.HasValue)
            {
                var suDung = _context.PhieuGiamGiaSuDungs
                    .Where(x =>
                        x.IdDon == don.IdDon &&
                        x.IdPhieuGiamGia ==
                            don.IdPhieuGiamGia.Value)
                    .OrderByDescending(x => x.IdSuDung)
                    .FirstOrDefault();

                if (suDung != null &&
                    suDung.TrangThai != VoucherStatusUsed)
                {
                    suDung.TrangThai = VoucherStatusUsed;
                    suDung.ThoiDiemSuDung = DateTime.Now;
                    suDung.GhiChu =
                        "Admin xac nhan thu tien mat";
                }
            }

            _context.SaveChanges();

            _seatInventory
                .SynchronizeAsync(don.IdLich, don.IdTour)
                .GetAwaiter()
                .GetResult();

            TempData["Success"] =
                "Da xac nhan don thu tien mat thanh cong.";

            return RedirectToAction("ChiTiet", new { id });
        }

        // =====================================================
        // XOA DON CHUA THANH TOAN
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Xoa(int id)
        {
            if (!IsAdmin())
            {
                return RedirectToAction("DangNhap", "TaiKhoan");
            }

            var don = _context.DonDatTours
                .FirstOrDefault(x => x.IdDon == id);

            if (don == null) return NotFound();

            if (don.DaThanhToan || DaCheckIn(don.IdDon))
            {
                TempData["Error"] =
                    "Khong the xoa don da thanh toan hoac da Check-in.";

                return RedirectToAction(nameof(Index));
            }

            // Khong xoa khi ma giao dich da duoc tao:
            // Can doi chieu trang thai thanh toan truoc.
            if (string.Equals(
                    don.PhuongThucTT,
                    "VNPAY",
                    StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(don.MaGiaoDich))
            {
                TempData["Error"] =
                    "Don co giao dich VNPAY. " +
                    "Can doi chieu ket qua truoc khi xoa.";

                return RedirectToAction(
                    "ChiTiet",
                    new { id });
            }

            ThuHoiLuotPhieuNeuCan(
                don,
                "Admin xoa don",
                xoaBanGhiSuDung: true);

            var thongBaos = _context.ThongBaos
                .Where(tb => tb.IdDon == id)
                .ToList();

            if (thongBaos.Any())
            {
                _context.ThongBaos.RemoveRange(thongBaos);
            }

            var idLich = don.IdLich;
            var idTour = don.IdTour;

            _context.DonDatTours.Remove(don);
            _context.SaveChanges();

            _seatInventory
                .SynchronizeAsync(idLich, idTour)
                .GetAwaiter()
                .GetResult();

            TempData["Success"] =
                "Da xoa don chua thanh toan.";

            return RedirectToAction(nameof(Index));
        }

        // =====================================================
        // TRUY VAN DANH SACH VA BO LOC
        // =====================================================

        private IQueryable<DonDatTour> BuildOrderQuery(
            string trangThai,
            DateTime? ngayDatTu,
            DateTime? ngayDatDen,
            string phuongThucTT,
            string? tuKhoa)
        {
            var query = _context.DonDatTours
                .Include(d => d.IdTourNavigation)
                .Include(d => d.IdLichNavigation)
                .Include(d => d.IdTaiKhoanNavigation)
                .AsQueryable();

            switch (trangThai)
            {
                case "ChoDuyet":
                    query = query.Where(d =>
                        d.TrangThai == "ChoDuyet");
                    break;

                case "DaDuyet":
                    query = query.Where(d =>
                        d.TrangThai == "DaDuyet");
                    break;

                case "DaHuy":
                    query = query.Where(d =>
                        d.TrangThai ==
                            BookingPaymentStatus.TrangThaiDaHuy);
                    break;

                case "DaThanhToan":
                    query = query.Where(d => d.DaThanhToan);
                    break;

                case "ChuaThanhToan":
                    query = query.Where(d => !d.DaThanhToan);
                    break;

                case "ChoXacNhanTienMat":
                    query = query.Where(d =>
                        d.TrangThai ==
                            BookingPaymentStatus.TrangThaiChoXacNhanTienMat);
                    break;
            }

            var tuNgay = ngayDatTu?.Date;
            var denNgay = ngayDatDen?.Date;

            if (tuNgay.HasValue &&
                denNgay.HasValue &&
                tuNgay > denNgay)
            {
                var tam = tuNgay;
                tuNgay = denNgay;
                denNgay = tam;
            }

            if (tuNgay.HasValue)
            {
                query = query.Where(d =>
                    d.NgayDat >= tuNgay.Value);
            }

            if (denNgay.HasValue)
            {
                var denNgayBaoGom = denNgay.Value.AddDays(1);

                query = query.Where(d =>
                    d.NgayDat < denNgayBaoGom);
            }

            if (!string.IsNullOrWhiteSpace(phuongThucTT)
                && phuongThucTT != "TatCa")
            {
                query = query.Where(d =>
                    d.PhuongThucTT == phuongThucTT);
            }

            if (!string.IsNullOrWhiteSpace(tuKhoa))
            {
                query = query.Where(d =>
                    d.MaBooking.Contains(tuKhoa) ||
                    d.IdTaiKhoanNavigation.HoTen.Contains(tuKhoa));
            }

            return query.OrderByDescending(d => d.NgayDat);
        }

        // =====================================================
        // TRANG THAI XUAT EXCEL
        // =====================================================

        private static string GetTrangThaiHienThi(DonDatTour don)
        {
            if (don.TrangThai ==
                BookingPaymentStatus.TrangThaiDaHuy)
            {
                return "Da huy";
            }

            if (don.TrangThai == "HoanTat")
            {
                return "Hoan tat";
            }

            if (don.DaThanhToan)
            {
                return "Da thanh toan";
            }

            if (don.TrangThai ==
                BookingPaymentStatus.TrangThaiChoXacNhanTienMat)
            {
                return "Cho xac nhan tien mat";
            }

            if (don.HanThanhToan.HasValue &&
                don.HanThanhToan.Value < DateTime.Now)
            {
                return "Het han thanh toan";
            }

            return "Cho thanh toan";
        }

        // =====================================================
        // PHIEU GIAM GIA
        // =====================================================

        private void ThuHoiLuotPhieuNeuCan(
            DonDatTour don,
            string ghiChu,
            bool xoaBanGhiSuDung)
        {
            if (!don.IdPhieuGiamGia.HasValue)
            {
                return;
            }

            var lichSuDung = _context.PhieuGiamGiaSuDungs
                .Where(x => x.IdDon == don.IdDon)
                .ToList();

            if (!lichSuDung.Any())
            {
                return;
            }

            bool canTruLuot = lichSuDung.Any(x =>
                x.TrangThai == VoucherStatusHold);

            if (canTruLuot)
            {
                var phieu = _context.PhieuGiamGias
                    .FirstOrDefault(x =>
                        x.IdPhieuGiamGia ==
                        don.IdPhieuGiamGia.Value);

                if (phieu != null && phieu.DaSuDung > 0)
                {
                    phieu.DaSuDung -= 1;
                    phieu.NgayCapNhat = DateTime.Now;
                }
            }

            if (xoaBanGhiSuDung)
            {
                _context.PhieuGiamGiaSuDungs
                    .RemoveRange(lichSuDung);

                return;
            }

            foreach (var item in lichSuDung)
            {
                if (item.TrangThai == VoucherStatusUsed ||
                    item.TrangThai == VoucherStatusCancelled)
                {
                    continue;
                }

                item.TrangThai = VoucherStatusCancelled;
                item.ThoiDiemSuDung = DateTime.Now;
                item.GhiChu = ghiChu;
            }
        }

        // =====================================================
        // GIAI MA TOKEN QR
        // Tuong thich ca payload 2 va 3 thanh phan
        // =====================================================

        private bool TryDecodeCheckInToken(
            string token,
            out int idDon,
            out string maBooking)
        {
            idDon = 0;
            maBooking = string.Empty;

            try
            {
                string raw = _checkinProtector.Unprotect(token);

                string[] parts = raw.Split('|');

                if (parts.Length != 2 && parts.Length != 3)
                {
                    return false;
                }

                if (!int.TryParse(parts[0], out idDon) ||
                    idDon <= 0)
                {
                    return false;
                }

                maBooking = parts[1];

                if (string.IsNullOrWhiteSpace(maBooking))
                {
                    return false;
                }

                if (parts.Length == 3 &&
                    !long.TryParse(parts[2], out _))
                {
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
