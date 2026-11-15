using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TourWebApp.Data.Models;
using TourWebApp.Helpers;
using TourWebApp.ViewModels;
using TourWebApp.Models.ViewModels;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Layout.Borders;
using iText.Kernel.Colors;
using iText.IO.Image;
using iText.Kernel.Font;
using iText.IO.Font;
using System.Data;

namespace TourWebApp.Controllers
{
    public class QuanTriController : Controller
    {
        private readonly ApplicationDbContext _context;

        public QuanTriController(ApplicationDbContext context)
        {
            _context = context;
        }

       
        private bool LaAdmin()
        {
            var vaiTro = HttpContext.Session.GetString("VaiTro");
            return vaiTro == "Admin";
        }

        private IActionResult NeuKhongPhaiAdmin()
        {
            
            return RedirectToAction("DangNhap", "TaiKhoan");
        }
    
        public IActionResult Dashboard(
            string period = "month",
            DateTime? selectedDate = null)
        {
            if (!LaAdmin())
                return NeuKhongPhaiAdmin();

            var today = DateTime.Today;
            var baseDate = selectedDate?.Date ?? today;

            period = (period ?? "month").Trim().ToLowerInvariant();

            if (!new[] { "day", "week", "month", "quarter", "year" }
                .Contains(period))
            {
                period = "month";
            }

            DateTime start;
            DateTime end;
            DateTime previousStart;
            DateTime previousEnd;
            string filterTitle;

            // ==================================================
            // 1. BO LOC THOI GIAN
            // ==================================================

            switch (period)
            {
                case "day":
                {
                    start = baseDate;
                    end = start.AddDays(1);

                    previousStart = start.AddDays(-1);
                    previousEnd = start;

                    filterTitle = $"Ngày {baseDate:dd/MM/yyyy}";
                    break;
                }

                case "week":
                {
                    int offset = ((int)baseDate.DayOfWeek + 6) % 7;

                    start = baseDate.AddDays(-offset);
                    end = start.AddDays(7);

                    previousStart = start.AddDays(-7);
                    previousEnd = start;

                    filterTitle =
                        $"Tuần {start:dd/MM} - {end.AddDays(-1):dd/MM/yyyy}";
                    break;
                }

                case "quarter":
                {
                    int quarter = (baseDate.Month - 1) / 3 + 1;
                    int firstMonth = (quarter - 1) * 3 + 1;

                    start = new DateTime(baseDate.Year, firstMonth, 1);
                    end = start.AddMonths(3);

                    previousStart = start.AddMonths(-3);
                    previousEnd = start;

                    filterTitle = $"Quý {quarter}/{baseDate.Year}";
                    break;
                }

                case "year":
                {
                    start = new DateTime(baseDate.Year, 1, 1);
                    end = start.AddYears(1);

                    previousStart = start.AddYears(-1);
                    previousEnd = start;

                    filterTitle = $"Năm {baseDate.Year}";
                    break;
                }

                default:
                {
                    period = "month";

                    start = new DateTime(
                        baseDate.Year,
                        baseDate.Month,
                        1);

                    end = start.AddMonths(1);

                    previousStart = start.AddMonths(-1);
                    previousEnd = start;

                    filterTitle = $"Tháng {baseDate:MM/yyyy}";
                    break;
                }
            }

            ViewBag.Period = period;
            ViewBag.FilterTitle = filterTitle;
            ViewBag.SelectedDate = baseDate.ToString("yyyy-MM-dd");
            ViewBag.SelectedDateText = baseDate.ToString("dd/MM/yyyy");

            // ==================================================
            // 2. LAY DON DAT TOUR
            // ==================================================

            // Doanh thu duoc nhom theo NGAY DAT.
            // Chi tinh don da thanh toan va chua bi huy.
            // Chua phai doanh thu thuan sau hoan tien.

            var orders = _context.DonDatTours
                .AsNoTracking()
                .Where(d =>
                    d.TrangThai != "DaHuy" &&
                    d.TrangThai != "Đã hủy" &&
                    d.NgayDat >= previousStart &&
                    d.NgayDat < end)
                .Select(d => new
                {
                    d.IdDon,
                    d.IdTour,
                    d.IdLich,
                    d.NgayDat,
                    d.DaThanhToan,
                    d.TrangThai,
                    d.NguoiLon,
                    d.TreEm,
                    d.TreNho,
                    DoanhThu = d.TongTienSauGiam ?? d.TongTien
                })
                .ToList();

            var currentOrders = orders
                .Where(d =>
                    d.NgayDat >= start &&
                    d.NgayDat < end)
                .ToList();

            var previousOrders = orders
                .Where(d =>
                    d.NgayDat >= previousStart &&
                    d.NgayDat < previousEnd)
                .ToList();

            // ==================================================
            // 3. KPI THUC TE
            // ==================================================

            decimal revenue = currentOrders
                .Where(d => d.DaThanhToan)
                .Sum(d => d.DoanhThu);

            decimal previousRevenue = previousOrders
                .Where(d => d.DaThanhToan)
                .Sum(d => d.DoanhThu);

            int orderCount = currentOrders.Count;
            int previousOrderCount = previousOrders.Count;

            int guestCount = currentOrders.Sum(d =>
                d.NguoiLon + d.TreEm + d.TreNho);

            int previousGuestCount = previousOrders.Sum(d =>
                d.NguoiLon + d.TreEm + d.TreNho);

            int activeTourCount = _context.Tours
                .AsNoTracking()
                .Count(t => t.TrangThai);

            ViewBag.TongDoanhThu = revenue;
            ViewBag.TongDon = orderCount;
            ViewBag.TongKhach = guestCount;
            ViewBag.TourDangBan = activeTourCount;

            ViewBag.PhanTramDoanhThu =
                TinhPhanTram(revenue, previousRevenue);

            ViewBag.PhanTramDon =
                TinhPhanTram(orderCount, previousOrderCount);

            ViewBag.PhanTramKhach =
                TinhPhanTram(guestCount, previousGuestCount);

            // Khong su dung ty le 5.2% gan co dinh.
            ViewBag.PhanTramTour = null;

            // ==================================================
            // 4. BIEU DO DOANH THU + SO DON
            // ==================================================

            var revenueLabels = new List<string>();
            var revenueValues = new List<decimal>();

            var guestLabels = new List<string>();
            var guestValues = new List<int>();

            var orderLabels = new List<string>();
            var orderValues = new List<int>();

            void AddBucket(
                DateTime bucketStart,
                DateTime bucketEnd,
                string label)
            {
                var bucket = currentOrders
                    .Where(d =>
                        d.NgayDat >= bucketStart &&
                        d.NgayDat < bucketEnd)
                    .ToList();

                decimal bucketRevenue = bucket
                    .Where(d => d.DaThanhToan)
                    .Sum(d => d.DoanhThu);

                int bucketGuests = bucket.Sum(d =>
                    d.NguoiLon + d.TreEm + d.TreNho);

                int bucketOrders = bucket.Count;

                revenueLabels.Add(label);
                revenueValues.Add(bucketRevenue);

                guestLabels.Add(label);
                guestValues.Add(bucketGuests);

                orderLabels.Add(label);
                orderValues.Add(bucketOrders);
            }

            if (period == "day")
            {
                for (int hour = 0; hour < 24; hour++)
                {
                    var bucketStart = start.AddHours(hour);

                    AddBucket(
                        bucketStart,
                        bucketStart.AddHours(1),
                        $"{hour:00}:00");
                }
            }
            else if (period == "quarter" || period == "year")
            {
                var bucketStart = start;

                while (bucketStart < end)
                {
                    AddBucket(
                        bucketStart,
                        bucketStart.AddMonths(1),
                        bucketStart.ToString("MM/yyyy"));

                    bucketStart = bucketStart.AddMonths(1);
                }
            }
            else
            {
                var bucketStart = start;

                while (bucketStart < end)
                {
                    AddBucket(
                        bucketStart,
                        bucketStart.AddDays(1),
                        bucketStart.ToString("dd/MM"));

                    bucketStart = bucketStart.AddDays(1);
                }
            }

            ViewBag.LabelDoanhThu = revenueLabels;
            ViewBag.DataDoanhThu = revenueValues;

            ViewBag.LabelKhach = guestLabels;
            ViewBag.DataKhach = guestValues;

            ViewBag.DashboardOrderLabelsJson =
                System.Text.Json.JsonSerializer.Serialize(orderLabels);

            ViewBag.DashboardOrderValuesJson =
                System.Text.Json.JsonSerializer.Serialize(orderValues);

            // ==================================================
            // 5. THONG TIN TOUR VA LOAI TOUR
            // ==================================================

            var tours = _context.Tours
                .AsNoTracking()
                .Include(t => t.HinhTours)
                .ToList();

            var tourLookup = tours.ToDictionary(t => t.IdTour);

            var typeLookup = _context.LoaiTours
                .AsNoTracking()
                .ToDictionary(t => t.IdLoaiTour, t => t.TenLoai);

            var paidOrders = currentOrders
                .Where(d => d.DaThanhToan)
                .ToList();

            // ==================================================
            // 6. DONUT - DOANH THU THEO LOAI TOUR
            // ==================================================

            var categoryRevenue = paidOrders
                .Where(d => tourLookup.ContainsKey(d.IdTour))
                .GroupBy(d =>
                {
                    var tour = tourLookup[d.IdTour];

                    return typeLookup.TryGetValue(
                        tour.IdLoaiTour,
                        out var typeName)
                            ? typeName
                            : "Chưa phân loại";
                })
                .Select(g => new
                {
                    name = g.Key,
                    revenue = g.Sum(x => x.DoanhThu)
                })
                .Where(x => x.revenue > 0)
                .OrderByDescending(x => x.revenue)
                .ToList();

            ViewBag.DashboardCategoryRevenueJson =
                System.Text.Json.JsonSerializer.Serialize(
                    categoryRevenue);

            // ==================================================
            // 7. HIEU QUA THEO DIEM DEN
            // ==================================================

            string DestinationName(string? raw)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    return "Chưa xác định";

                var value = raw.Trim();

                // Co duong di dang: TP.HCM -> Da Lat
                // Hoac TP.HCM → Da Lat.
                // Uu tien lay diem den cuoi cung.

                var parts = value.Split(
                    new[] { "→", "->", "➜" },
                    StringSplitOptions.RemoveEmptyEntries);

                return parts.Length > 0
                    ? parts[^1].Trim()
                    : value;
            }

            string TourImage(Tour tour)
            {
                var firstImage = tour.HinhTours
                    .Where(h => !string.IsNullOrWhiteSpace(h.UrlHinh))
                    .OrderBy(h => h.ThuTu ?? 999)
                    .FirstOrDefault();

                return TourImagePathHelper.ToTourImageSrc(
                    firstImage?.UrlHinh ?? tour.HinhAnh);
            }

            var destinations = paidOrders
                .Where(d => tourLookup.ContainsKey(d.IdTour))
                .GroupBy(d =>
                    DestinationName(
                        tourLookup[d.IdTour].DiaDiem))
                .Select(g =>
                {
                    var representativeOrder = g
                        .OrderByDescending(x => x.DoanhThu)
                        .First();

                    var representativeTour =
                        tourLookup[representativeOrder.IdTour];

                    return new
                    {
                        name = g.Key,
                        revenue = g.Sum(x => x.DoanhThu),
                        guests = g.Sum(x =>
                            x.NguoiLon + x.TreEm + x.TreNho),
                        imageUrl = TourImage(representativeTour)
                    };
                })
                .Where(x => x.revenue > 0)
                .OrderByDescending(x => x.revenue)
                .Take(5)
                .ToList();

            ViewBag.DashboardDestinationsJson =
                System.Text.Json.JsonSerializer.Serialize(
                    destinations);

            // ==================================================
            // 8. LICH KHOI HANH SAP TOI
            // ==================================================

            var todayDate = DateOnly.FromDateTime(today);

            var upcomingSchedules = _context.LichKhoiHanhs
                .AsNoTracking()
                .Include(l => l.IdTourNavigation)
                .ThenInclude(t => t.HinhTours)
                .Where(l =>
                    l.NgayKhoiHanh >= todayDate &&
                    l.IdTourNavigation.TrangThai &&
                    l.TrangThai != "DaHuy" &&
                    l.TrangThai != "Đã hủy")
                .OrderBy(l => l.NgayKhoiHanh)
                .ThenBy(l => l.GioKhoiHanh)
                .Take(3)
                .ToList();

            var upcomingIds = upcomingSchedules
                .Select(l => l.IdLich)
                .ToList();

            var guestsBySchedule = _context.DonDatTours
                .AsNoTracking()
                .Where(d =>
                    upcomingIds.Contains(d.IdLich) &&
                    d.TrangThai != "DaHuy" &&
                    d.TrangThai != "Đã hủy")
                .GroupBy(d => d.IdLich)
                .Select(g => new
                {
                    IdLich = g.Key,
                    Guests = g.Sum(x =>
                        x.NguoiLon + x.TreEm + x.TreNho)
                })
                .ToDictionary(x => x.IdLich, x => x.Guests);

            var upcoming = upcomingSchedules
                .Select(l =>
                {
                    var tour = l.IdTourNavigation;

                    var guestTotal = guestsBySchedule.TryGetValue(
                        l.IdLich,
                        out var count)
                            ? count
                            : 0;

                    return new
                    {
                        idLich = l.IdLich,
                        tourName = tour.TenTour,
                        destination = DestinationName(tour.DiaDiem),
                        imageUrl = TourImage(tour),
                        departureDate =
                            l.NgayKhoiHanh.ToString("dd/MM/yyyy"),
                        day = l.NgayKhoiHanh.Day,
                        month = l.NgayKhoiHanh.Month,
                        guests = guestTotal,
                        daysRemaining =
                            l.NgayKhoiHanh.DayNumber -
                            todayDate.DayNumber,
                        remainingSeats = l.SoChoConLai
                    };
                })
                .ToList();

            ViewBag.DashboardUpcomingJson =
                System.Text.Json.JsonSerializer.Serialize(
                    upcoming);

            // ==================================================
            // 9. TOP 3 TOUR
            // ==================================================

            // Giu dung logic hien tai:
            // xep theo so nguoi da dat, sau do luot xem.

            ViewBag.TopTour = tours
                .Where(t => t.TrangThai)
                .OrderByDescending(t => t.SoNguoiDaDat)
                .ThenByDescending(t => t.LuotXem)
                .ThenBy(t => t.IdTour)
                .Take(3)
                .ToList();

            // ==================================================
            // 10. TRA VE VIEW
            // ==================================================

            return View();
        }

        private decimal TinhPhanTram(decimal hienTai, decimal kyTruoc)
        {
            if (kyTruoc == 0)
            {
                return hienTai > 0 ? 100 : 0;
            }

            return Math.Round(((hienTai - kyTruoc) / kyTruoc) * 100, 1);
        }

        private decimal TinhPhanTram(int hienTai, int kyTruoc)
        {
            if (kyTruoc == 0)
            {
                return hienTai > 0 ? 100 : 0;
            }

            return Math.Round(((decimal)(hienTai - kyTruoc) / kyTruoc) * 100, 1);
        }

        // ========== DANH SÁCH TOUR ==========
        [HttpGet]
        public IActionResult QuanLyTour(int? idLoaiTour, string? tuKhoa)
        {
            if (!LaAdmin()) return NeuKhongPhaiAdmin();

            
            var query = _context.Tours
                                .Include(t => t.IdLoaiTourNavigation)
                                .AsQueryable();

            if (idLoaiTour.HasValue && idLoaiTour > 0)
            {
                query = query.Where(t => t.IdLoaiTour == idLoaiTour);
            }

            if (!string.IsNullOrWhiteSpace(tuKhoa))
            {
                tuKhoa = tuKhoa.Trim();
                query = query.Where(t =>
                    t.TenTour.Contains(tuKhoa) ||
                    (t.DiaDiem ?? "").Contains(tuKhoa));
            }

            var vm = new QuanLyTourViewModel
            {
                Tours = query
                    .OrderByDescending(t => t.IdTour)
                    .ToList(),

                LoaiTours = _context.LoaiTours
                    .OrderBy(x => x.TenLoai)
                    .ToList(),

                IdLoaiTour = idLoaiTour,
                TuKhoa = tuKhoa
            };

            return View(vm);
        }

       // ========== FORM THÊM TOUR ==========
        // GET
        [HttpGet]
        public IActionResult ThemTour()
        {
            if (!LaAdmin()) return NeuKhongPhaiAdmin();

            ViewBag.LoaiTours = _context.LoaiTours.OrderBy(x => x.TenLoai).ToList();
            return View(new Tour());
        }

        // =====================================================
        // POST: THEM TOUR + GAN CHU DE SO THICH
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("ThemTour")]
        public IActionResult ThemTour(
            Tour model,
            IFormFile? HinhAnhFile,
            List<IFormFile>? HinhAnhFiles,
            List<string>? MaSoThichs)
        {
            if (!LaAdmin())
                return NeuKhongPhaiAdmin();

            // ================================================
            // 1. CHUAN HOA DANH SACH SO THICH
            // ================================================

            var maSoThichDaChon = (MaSoThichs ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // ================================================
            // 2. KIEM TRA THONG TIN TOUR
            // ================================================

            if (model.IdLoaiTour == 0)
            {
                ModelState.AddModelError(
                    "IdLoaiTour",
                    "Vui lòng chọn loại tour."
                );
            }

            if (string.IsNullOrWhiteSpace(model.PhuongTien))
            {
                ModelState.AddModelError(
                    "PhuongTien",
                    "Vui lòng chọn phương tiện."
                );
            }

            if (string.IsNullOrWhiteSpace(model.MaTour))
            {
                ModelState.AddModelError(
                    "MaTour",
                    "Vui lòng nhập mã tour."
                );
            }
            else
            {
                model.MaTour = model.MaTour.Trim();

                bool maTrung = _context.Tours.Any(
                    t => t.MaTour == model.MaTour
                );

                if (maTrung)
                {
                    ModelState.AddModelError(
                        "MaTour",
                        "Mã tour đã tồn tại."
                    );
                }
            }

            if (string.IsNullOrWhiteSpace(model.TenTour))
            {
                ModelState.AddModelError(
                    "TenTour",
                    "Vui lòng nhập tên tour."
                );
            }

            if (model.GiaGoc.HasValue &&
                model.GiaGoc.Value < 0)
            {
                ModelState.AddModelError(
                    "GiaGoc",
                    "Giá tour không được âm."
                );
            }

            if (model.PhanTramGiam.HasValue &&
                (model.PhanTramGiam.Value < 0 ||
                 model.PhanTramGiam.Value > 100))
            {
                ModelState.AddModelError(
                    "PhanTramGiam",
                    "Phần trăm giảm phải từ 0 đến 100."
                );
            }

            // ================================================
            // 3. KIEM TRA CHU DE
            // ================================================

            if (maSoThichDaChon.Count == 0)
            {
                ModelState.AddModelError(
                    "MaSoThichs",
                    "Vui lòng chọn ít nhất một chủ đề du lịch."
                );
            }

            var idSoThichHopLe = new List<int>();

            foreach (string ma in maSoThichDaChon)
            {
                // Truy van tham so hoa, khong noi chuoi SQL.
                // Chi chap nhan so thich dang hoat dong.

                var ids = _context.Database.SqlQuery<int>(
                    $"""
                    SELECT st.IdSoThich AS Value
                    FROM dbo.AISoThich AS st
                    WHERE st.MaSoThich = {ma}
                      AND st.TrangThai = 1
                    """
                ).ToList();

                if (ids.Count == 0)
                {
                    ModelState.AddModelError(
                        "MaSoThichs",
                        $"Chủ đề '{ma}' không tồn tại hoặc đã ngừng hoạt động."
                    );

                    continue;
                }

                idSoThichHopLe.AddRange(ids);
            }

            idSoThichHopLe = idSoThichHopLe
                .Distinct()
                .ToList();

            if (!ModelState.IsValid)
            {
                ViewBag.LoaiTours = _context.LoaiTours
                    .OrderBy(x => x.TenLoai)
                    .ToList();

                return View(model);
            }

            // ================================================
            // 4. KHOI TAO THONG TIN KINH DOANH
            // ================================================

            model.LuotXem = 0;
            model.SoNguoiDaDat = 0;
            model.TrangThai = true;

            int phanTramGiam = model.PhanTramGiam ?? 0;

            decimal giaGoc = model.GiaGoc ?? 0;

            decimal giaKhuyenMai =
                giaGoc - (giaGoc * phanTramGiam / 100m);

            // Lam tron ve VND.
            giaKhuyenMai = Math.Round(
                giaKhuyenMai,
                0,
                MidpointRounding.AwayFromZero
            );

            model.GiaKhuyenMai = giaKhuyenMai;

            // ================================================
            // 5. GIU NGUYEN QUY TAC GIA HIEN TAI
            // ================================================

            decimal giaNguoiLon = giaKhuyenMai;

            decimal giaTreEm = Math.Round(
                giaNguoiLon * 0.5m,
                0,
                MidpointRounding.AwayFromZero
            );

            decimal giaEmBe = 0m;

            model.TourGiaChiTiets =
                new List<TourGiaChiTiet>
                {
                    new TourGiaChiTiet
                    {
                        DoiTuong = TourPriceAudience.Adult,
                        Gia = giaNguoiLon,
                        GhiChu = "Giá người lớn theo giá bán tour."
                    },

                    new TourGiaChiTiet
                    {
                        DoiTuong = TourPriceAudience.Child,
                        Gia = giaTreEm,
                        GhiChu = "50% giá người lớn."
                    },

                    new TourGiaChiTiet
                    {
                        DoiTuong = TourPriceAudience.Infant,
                        Gia = giaEmBe,
                        GhiChu = "Miễn phí."
                    }
                };

            // ================================================
            // 6. CHUAN BI DANH SACH ANH
            // ================================================

            var uploadedImagePaths = new List<string>();

            var uploadFiles = new List<IFormFile>();

            if (HinhAnhFile != null &&
                HinhAnhFile.Length > 0)
            {
                uploadFiles.Add(HinhAnhFile);
            }

            if (HinhAnhFiles != null)
            {
                uploadFiles.AddRange(
                    HinhAnhFiles.Where(x => x.Length > 0)
                );
            }

            string uploadDirectory = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                "img",
                "tours"
            );

            // ================================================
            // 7. TRANSACTION: TOUR + GIA + CHU DE
            // ================================================

            using var transaction =
                _context.Database.BeginTransaction();

            try
            {
                Directory.CreateDirectory(uploadDirectory);

                int thuTu = 1;

                foreach (var imageFile in uploadFiles)
                {
                    if (string.IsNullOrWhiteSpace(
                        imageFile.ContentType
                    ) ||
                        !imageFile.ContentType.StartsWith(
                            "image/",
                            StringComparison.OrdinalIgnoreCase
                        ))
                    {
                        throw new InvalidOperationException(
                            "Tệp tải lên không phải hình ảnh."
                        );
                    }

                    string extension = Path.GetExtension(
                        imageFile.FileName
                    ).ToLowerInvariant();

                    if (!new[]
                    {
                        ".jpg",
                        ".jpeg",
                        ".png",
                        ".webp"
                    }.Contains(extension))
                    {
                        throw new InvalidOperationException(
                            "Chỉ hỗ trợ ảnh JPG, PNG hoặc WEBP."
                        );
                    }

                    string fileName =
                        Guid.NewGuid().ToString("N") +
                        extension;

                    string path = Path.Combine(
                        uploadDirectory,
                        fileName
                    );

                    using (var stream = new FileStream(
                        path,
                        FileMode.CreateNew
                    ))
                    {
                        // Ghi nhan truoc de co the don dep neu loi.
                        uploadedImagePaths.Add(path);

                        imageFile.CopyTo(stream);
                    }

                    if (string.IsNullOrWhiteSpace(model.HinhAnh))
                    {
                        model.HinhAnh = fileName;
                    }

                    model.HinhTours.Add(
                        new HinhTour
                        {
                            UrlHinh = fileName,
                            ThuTu = thuTu++
                        }
                    );
                }

                // --------------------------------------------
                // 7.1. LUU TOUR, BANG GIA VA ANH
                // --------------------------------------------

                _context.Tours.Add(model);

                _context.SaveChanges();

                int idTourMoi = model.IdTour;

                if (idTourMoi <= 0)
                {
                    throw new InvalidOperationException(
                        "Không lấy được mã định danh tour mới."
                    );
                }

                // --------------------------------------------
                // 7.2. GAN SO THICH CHO TOUR
                // --------------------------------------------

                foreach (int idSoThich in idSoThichHopLe)
                {
                    _context.Database.ExecuteSqlInterpolated(
                        $"""
                        INSERT INTO dbo.AITourSoThich
                            (IdTour, IdSoThich)
                        VALUES
                            ({idTourMoi}, {idSoThich})
                        """
                    );
                }

                // Chi commit khi ca hai phan da thanh cong.
                transaction.Commit();

                TempData["ThongBao"] =
                    "Đã thêm tour và lưu các chủ đề du lịch thành công.";

                return RedirectToAction("QuanLyTour");
            }
            catch (Exception)
            {
                transaction.Rollback();

                // SQL da rollback thi don dep anh tren dia.
                foreach (string path in uploadedImagePaths)
                {
                    try
                    {
                        if (System.IO.File.Exists(path))
                        {
                            System.IO.File.Delete(path);
                        }
                    }
                    catch (IOException)
                    {
                        // Khong che mat loi luu tour ban dau.
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Khong che mat loi luu tour ban dau.
                    }
                }

                ModelState.AddModelError(
                    string.Empty,
                    "Không thể lưu tour hoặc các chủ đề du lịch. " +
                    "Vui lòng kiểm tra dữ liệu và thử lại."
                );

                ViewBag.LoaiTours = _context.LoaiTours
                    .OrderBy(x => x.TenLoai)
                    .ToList();

                return View(model);
            }
        }

        // ========== FORM SỬA TOUR ==========
        [HttpGet]
        public IActionResult SuaTour(int id)
        {
            if (!LaAdmin())
                return NeuKhongPhaiAdmin();

            var tour = _context.Tours
                .Include(t => t.HinhTours)
                .Include(t => t.TourGiaChiTiets)
                .FirstOrDefault(t => t.IdTour == id);

            if (tour == null)
                return NotFound();

            // Danh sách loại tour.
            ViewBag.LoaiTours = _context.LoaiTours
                .OrderBy(x => x.TenLoai)
                .ToList();

            // Bảng giá hiện tại.
            ViewBag.GiaNguoiLon = tour.TourGiaChiTiets
                .FirstOrDefault(g =>
                    TourPriceAudience.IsAdult(g.DoiTuong))
                ?.Gia ?? 0m;

            ViewBag.GiaTreEm = tour.TourGiaChiTiets
                .FirstOrDefault(g =>
                    TourPriceAudience.IsChild(g.DoiTuong))
                ?.Gia ?? 0m;

            ViewBag.GiaEmBe = tour.TourGiaChiTiets
                .FirstOrDefault(g =>
                    TourPriceAudience.IsInfant(g.DoiTuong))
                ?.Gia ?? 0m;

            // Lấy mã sở thích đã gắn cho tour.
            // Không cần tạo Entity mới cho bảng AITourSoThich.
            var selectedCodes = _context.Database
                .SqlQuery<string>(
                    $"""
                    SELECT s.MaSoThich AS Value
                    FROM dbo.AITourSoThich AS ts
                    INNER JOIN dbo.AISoThich AS s
                        ON s.IdSoThich = ts.IdSoThich
                    WHERE ts.IdTour = {id}
                    """
                )
                .ToList();

            ViewBag.SelectedMaSoThichs = selectedCodes;

            return View(tour);
        }

        // ========== CHI TIET TOUR ==========
        [HttpGet]
        public IActionResult ChiTietTour(int id)
        {
            if (!LaAdmin())
                return NeuKhongPhaiAdmin();

            var tour = _context.Tours
                .Include(t => t.IdLoaiTourNavigation)
                .Include(t => t.HinhTours)
                .Include(t => t.LichKhoiHanhs)
                .FirstOrDefault(t => t.IdTour == id);

            if (tour == null)
                return NotFound();

            return View(tour);
        }
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SuaTour(
            Tour model,
            List<IFormFile>? HinhAnhFiles,
            List<int>? XoaHinhIds,
            decimal? GiaNguoiLon,
            decimal? GiaTreEm,
            decimal? GiaEmBe,
            List<string>? MaSoThichs,
            string? CheDoGia)
        {
            if (!LaAdmin())
                return NeuKhongPhaiAdmin();

            var tour = _context.Tours
                .FirstOrDefault(t => t.IdTour == model.IdTour);

            if (tour == null)
                return NotFound();

            var existingImages = _context.HinhTours
                .Where(x => x.IdTour == tour.IdTour)
                .OrderBy(x => x.ThuTu)
                .ThenBy(x => x.IdHinh)
                .ToList();

            // =====================================================
            // 1. KIEM TRA CHE DO GIA
            // =====================================================

            var cheDoGia = (CheDoGia ?? "").Trim();

            if (cheDoGia != "TuDong" &&
                cheDoGia != "TuyChinh")
            {
                ModelState.AddModelError(
                    "CheDoGia",
                    "Vui lòng chọn chế độ tính giá hợp lệ."
                );
            }

            // Giá gốc và phần trăm giảm.
            if (!model.GiaGoc.HasValue ||
                model.GiaGoc.Value < 0)
            {
                ModelState.AddModelError(
                    "GiaGoc",
                    "Giá gốc phải lớn hơn hoặc bằng 0."
                );
            }

            var phanTramGiam = model.PhanTramGiam ?? 0m;

            if (phanTramGiam < 0 || phanTramGiam > 100)
            {
                ModelState.AddModelError(
                    "PhanTramGiam",
                    "Phần trăm giảm phải từ 0 đến 100."
                );
            }

            decimal giaSauGiam = 0m;

            if (model.GiaGoc.HasValue &&
                model.GiaGoc.Value >= 0 &&
                phanTramGiam >= 0 &&
                phanTramGiam <= 100)
            {
                giaSauGiam = Math.Round(
                    model.GiaGoc.Value *
                    (100m - phanTramGiam) / 100m,
                    0,
                    MidpointRounding.AwayFromZero
                );
            }

            // =====================================================
            // 2. TINH GIA THEO CHE DO
            // =====================================================

            decimal giaNguoiLon = 0m;
            decimal giaTreEm = 0m;
            decimal giaEmBe = 0m;

            if (cheDoGia == "TuDong")
            {
                // Không tin giá tự tính từ JavaScript.
                // Máy chủ tính lại để lưu thống nhất.
                giaNguoiLon = giaSauGiam;

                giaTreEm = Math.Round(
                    giaNguoiLon * 0.5m,
                    0,
                    MidpointRounding.AwayFromZero
                );

                giaEmBe = 0m;

                // Giá tính lại được dùng để hiển thị
                // nếu Controller phải trả về form.
                ModelState.Remove("GiaNguoiLon");
                ModelState.Remove("GiaTreEm");
                ModelState.Remove("GiaEmBe");
            }
            else if (cheDoGia == "TuyChinh")
            {
                if (!GiaNguoiLon.HasValue ||
                    GiaNguoiLon.Value < 0)
                {
                    ModelState.AddModelError(
                        "GiaNguoiLon",
                        "Giá người lớn không hợp lệ."
                    );
                }

                if (!GiaTreEm.HasValue ||
                    GiaTreEm.Value < 0)
                {
                    ModelState.AddModelError(
                        "GiaTreEm",
                        "Giá trẻ em không hợp lệ."
                    );
                }

                if (!GiaEmBe.HasValue ||
                    GiaEmBe.Value < 0)
                {
                    ModelState.AddModelError(
                        "GiaEmBe",
                        "Giá em bé không hợp lệ."
                    );
                }

                giaNguoiLon = GiaNguoiLon ?? 0m;
                giaTreEm = GiaTreEm ?? 0m;
                giaEmBe = GiaEmBe ?? 0m;
            }

            // =====================================================
            // 3. KIEM TRA THONG TIN TOUR
            // =====================================================

            if (string.IsNullOrWhiteSpace(model.TenTour))
            {
                ModelState.AddModelError(
                    "TenTour",
                    "Vui lòng nhập tên tour."
                );
            }

            if (!_context.LoaiTours.Any(
                x => x.IdLoaiTour == model.IdLoaiTour))
            {
                ModelState.AddModelError(
                    "IdLoaiTour",
                    "Loại tour không hợp lệ."
                );
            }

            if (string.IsNullOrWhiteSpace(model.PhuongTien))
            {
                ModelState.AddModelError(
                    "PhuongTien",
                    "Vui lòng chọn phương tiện."
                );
            }

            // =====================================================
            // 4. CHUAN HOA CHU DE DU LICH
            // =====================================================

            var selectedCodes = (MaSoThichs ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var selectedTopicIds = new HashSet<int>();

            if (selectedCodes.Count == 0)
            {
                ModelState.AddModelError(
                    "MaSoThichs",
                    "Vui lòng chọn ít nhất một chủ đề du lịch."
                );
            }

            foreach (var code in selectedCodes)
            {
                var ids = _context.Database
                    .SqlQuery<int>(
                        $"""
                        SELECT IdSoThich AS Value
                        FROM dbo.AISoThich
                        WHERE MaSoThich = {code}
                        AND TrangThai = 1
                        """
                    )
                    .ToList();

                if (ids.Count == 0)
                {
                    ModelState.AddModelError(
                        "MaSoThichs",
                        $"Chủ đề {code} không hợp lệ."
                    );

                    continue;
                }

                foreach (var id in ids)
                {
                    selectedTopicIds.Add(id);
                }
            }

            // =====================================================
            // 5. KIEM TRA HINH ANH
            // =====================================================

            var uploadFiles = (HinhAnhFiles ?? new List<IFormFile>())
                .Where(x => x.Length > 0)
                .ToList();

            const long maxFileSize = 8 * 1024 * 1024;

            var allowedExtensions = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase)
            {
                ".jpg", ".jpeg", ".png", ".webp"
            };

            var allowedMimeTypes = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase)
            {
                "image/jpeg", "image/png", "image/webp"
            };

            foreach (var file in uploadFiles)
            {
                var extension = Path.GetExtension(file.FileName);

                if (!allowedExtensions.Contains(extension) ||
                    !allowedMimeTypes.Contains(file.ContentType))
                {
                    ModelState.AddModelError(
                        "HinhAnhFiles",
                        "Chỉ hỗ trợ ảnh JPG, PNG và WEBP."
                    );
                }

                if (file.Length > maxFileSize)
                {
                    ModelState.AddModelError(
                        "HinhAnhFiles",
                        "Mỗi ảnh không được vượt quá 8 MB."
                    );
                }
            }

            // =====================================================
            // 6. TRA VE FORM KHI DU LIEU KHONG HOP LE
            // =====================================================

            IActionResult ReturnEditView(Tour viewModel)
            {
                ViewBag.LoaiTours = _context.LoaiTours
                    .OrderBy(x => x.TenLoai)
                    .ToList();

                ViewBag.GiaNguoiLon = giaNguoiLon;
                ViewBag.GiaTreEm = giaTreEm;
                ViewBag.GiaEmBe = giaEmBe;

                ViewBag.SelectedMaSoThichs = selectedCodes;

                viewModel.HinhTours = existingImages;
                viewModel.MaTour = tour.MaTour;
                viewModel.HinhAnh = tour.HinhAnh;
                viewModel.TrangThai = tour.TrangThai;

                return View(viewModel);
            }

            if (!ModelState.IsValid)
            {
                return ReturnEditView(model);
            }

            // =====================================================
            // 7. CHUAN BI ANH CAN THEM, XOA
            // =====================================================

            var deleteIdSet = (XoaHinhIds ?? new List<int>())
                .ToHashSet();

            var imagesToRemove = existingImages
                .Where(x => deleteIdSet.Contains(x.IdHinh))
                .ToList();

            var imagesToKeep = existingImages
                .Where(x => !deleteIdSet.Contains(x.IdHinh))
                .ToList();

            var uploadedImagePaths = new List<string>();
            var newImageNames = new List<string>();

            var uploadDirectory = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                "img",
                "tours"
            );

            var nextOrder = imagesToKeep
                .Select(x => x.ThuTu ?? 0)
                .DefaultIfEmpty(0)
                .Max() + 1;

            // =====================================================
            // 8. TRANSACTION LUU TOUR
            // =====================================================

            using var transaction =
                _context.Database.BeginTransaction();

            try
            {
                // ---------------------------------------------
                // 8.1. THONG TIN TOUR
                // ---------------------------------------------

                tour.TenTour = model.TenTour.Trim();
                tour.IdLoaiTour = model.IdLoaiTour;
                tour.DiaDiem = model.DiaDiem;
                tour.ThoiGian = model.ThoiGian;
                tour.PhuongTien = model.PhuongTien;
                tour.MoTa = model.MoTa;

                // Không đổi mã tour và trạng thái bán
                // trong form chỉnh sửa thông tin.
                tour.GiaGoc = model.GiaGoc;
                tour.PhanTramGiam = model.PhanTramGiam;
                tour.GiaKhuyenMai = giaSauGiam;

                // ---------------------------------------------
                // 8.2. XOA CAC BAN GHI ANH DUOC CHON
                // ---------------------------------------------

                if (imagesToRemove.Count > 0)
                {
                    _context.HinhTours.RemoveRange(
                        imagesToRemove
                    );
                }

                // ---------------------------------------------
                // 8.3. UPLOAD ANH MOI
                // ---------------------------------------------

                Directory.CreateDirectory(uploadDirectory);

                foreach (var file in uploadFiles)
                {
                    var extension = Path.GetExtension(
                        file.FileName
                    ).ToLowerInvariant();

                    var fileName =
                        Guid.NewGuid().ToString("N") + extension;

                    var physicalPath = Path.Combine(
                        uploadDirectory,
                        fileName
                    );

                    // Ghi nhận để dọn file nếu rollback.
                    uploadedImagePaths.Add(physicalPath);

                    using (var stream = new FileStream(
                        physicalPath,
                        FileMode.CreateNew))
                    {
                        file.CopyTo(stream);
                    }

                    newImageNames.Add(fileName);

                    _context.HinhTours.Add(
                        new HinhTour
                        {
                            IdTour = tour.IdTour,
                            UrlHinh = fileName,
                            ThuTu = nextOrder++
                        }
                    );
                }

                // ---------------------------------------------
                // 8.4. ANH DAI DIEN
                // ---------------------------------------------

                var remainingImages = imagesToKeep
                    .Select(x =>
                        TourImagePathHelper
                            .NormalizeTourImagePath(x.UrlHinh)
                    )
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Concat(newImageNames)
                    .ToList();

                var selectedThumbnail =
                    TourImagePathHelper.NormalizeTourImagePath(
                        Request.Form["Thumbnail"].ToString()
                    );

                var oldThumbnail =
                    TourImagePathHelper.NormalizeTourImagePath(
                        tour.HinhAnh
                    );

                if (!string.IsNullOrWhiteSpace(selectedThumbnail) &&
                    remainingImages.Contains(
                        selectedThumbnail,
                        StringComparer.OrdinalIgnoreCase))
                {
                    tour.HinhAnh = selectedThumbnail;
                }
                else if (
                    !string.IsNullOrWhiteSpace(oldThumbnail) &&
                    remainingImages.Contains(
                        oldThumbnail,
                        StringComparer.OrdinalIgnoreCase))
                {
                    tour.HinhAnh = oldThumbnail;
                }
                else
                {
                    tour.HinhAnh = remainingImages.FirstOrDefault();
                }

                // ---------------------------------------------
                // 8.5. XOA BANG GIA CU
                // ---------------------------------------------

                var oldPrices = _context.TourGiaChiTiets
                    .Where(x => x.IdTour == tour.IdTour)
                    .ToList();

                _context.TourGiaChiTiets.RemoveRange(
                    oldPrices
                );

                // Xóa giá cũ trước khi thêm lại,
                // tránh xung đột khóa duy nhất.
                _context.SaveChanges();

                // ---------------------------------------------
                // 8.6. LUU GIA THEO DOI TUONG
                // ---------------------------------------------

                _context.TourGiaChiTiets.AddRange(
                    new TourGiaChiTiet
                    {
                        IdTour = tour.IdTour,
                        DoiTuong = TourPriceAudience.Adult,
                        Gia = giaNguoiLon,
                        GhiChu = cheDoGia == "TuDong"
                            ? "Giá tự động"
                            : "Giá tùy chỉnh"
                    },
                    new TourGiaChiTiet
                    {
                        IdTour = tour.IdTour,
                        DoiTuong = TourPriceAudience.Child,
                        Gia = giaTreEm,
                        GhiChu = cheDoGia == "TuDong"
                            ? "Giá tự động - 50% người lớn"
                            : "Giá tùy chỉnh"
                    },
                    new TourGiaChiTiet
                    {
                        IdTour = tour.IdTour,
                        DoiTuong = TourPriceAudience.Infant,
                        Gia = giaEmBe,
                        GhiChu = cheDoGia == "TuDong"
                            ? "Giá tự động - miễn phí"
                            : "Giá tùy chỉnh"
                    }
                );

                _context.SaveChanges();

                // ---------------------------------------------
                // 8.7. DONG BO CHU DE TOUR
                // ---------------------------------------------

                var oldTopicIds = _context.Database
                    .SqlQuery<int>(
                        $"""
                        SELECT IdSoThich AS Value
                        FROM dbo.AITourSoThich
                        WHERE IdTour = {tour.IdTour}
                        """
                    )
                    .ToList()
                    .ToHashSet();

                foreach (var oldId in oldTopicIds)
                {
                    if (!selectedTopicIds.Contains(oldId))
                    {
                        _context.Database.ExecuteSqlInterpolated(
                            $"""
                            DELETE FROM dbo.AITourSoThich
                            WHERE IdTour = {tour.IdTour}
                            AND IdSoThich = {oldId}
                            """
                        );
                    }
                }

                foreach (var newId in selectedTopicIds)
                {
                    if (!oldTopicIds.Contains(newId))
                    {
                        _context.Database.ExecuteSqlInterpolated(
                            $"""
                            INSERT INTO dbo.AITourSoThich
                                (IdTour, IdSoThich)
                            VALUES ({tour.IdTour}, {newId})
                            """
                        );
                    }
                }

                // ---------------------------------------------
                // 8.8. COMMIT
                // ---------------------------------------------

                transaction.Commit();

                // Chỉ dọn file ảnh cũ sau khi SQL commit.
                try
                {
                    DeleteUnsharedTourImages(
                        imagesToRemove.Select(x => x.UrlHinh)
                    );
                }
                catch (IOException)
                {
                    // SQL đã lưu thành công.
                }
                catch (UnauthorizedAccessException)
                {
                    // SQL đã lưu thành công.
                }

                TempData["ThongBao"] =
                    "Đã cập nhật tour, giá bán, chủ đề " +
                    "và hình ảnh thành công.";

                return RedirectToAction("QuanLyTour");
            }
            catch (Exception)
            {
                // =================================================
                // 9. ROLLBACK
                // =================================================

                transaction.Rollback();

                foreach (var path in uploadedImagePaths)
                {
                    try
                    {
                        if (System.IO.File.Exists(path))
                        {
                            System.IO.File.Delete(path);
                        }
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }

                _context.ChangeTracker.Clear();

                var currentTour = _context.Tours
                    .Include(x => x.HinhTours)
                    .FirstOrDefault(x => x.IdTour == model.IdTour);

                ViewBag.LoaiTours = _context.LoaiTours
                    .OrderBy(x => x.TenLoai)
                    .ToList();

                ViewBag.GiaNguoiLon = giaNguoiLon;
                ViewBag.GiaTreEm = giaTreEm;
                ViewBag.GiaEmBe = giaEmBe;

                ViewBag.SelectedMaSoThichs = selectedCodes;

                ModelState.AddModelError(
                    string.Empty,
                    "Không thể cập nhật tour. " +
                    "Vui lòng kiểm tra thông tin và thử lại."
                );

                return View(currentTour ?? model);
            }
        }
        private void DeleteUnsharedTourImages(IEnumerable<string> imagePaths)
        {
            foreach (var imagePath in imagePaths)
            {
                var normalizedPath = TourImagePathHelper.NormalizeTourImagePath(imagePath);
                if (string.IsNullOrWhiteSpace(normalizedPath))
                    continue;

                var fileNameOnly = Path.GetFileNameWithoutExtension(normalizedPath);
                if (!Guid.TryParse(fileNameOnly, out _))
                    continue;

                var isStillUsed = _context.HinhTours
                    .AsEnumerable()
                    .Any(x => TourImagePathHelper.NormalizeTourImagePath(x.UrlHinh)
                        .Equals(normalizedPath, StringComparison.OrdinalIgnoreCase))
                    || _context.Tours
                        .AsEnumerable()
                        .Any(x => TourImagePathHelper.NormalizeTourImagePath(x.HinhAnh)
                            .Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));

                if (isStillUsed)
                    continue;

                var physicalPath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot",
                    "img",
                    "tours",
                    normalizedPath.Replace('/', Path.DirectorySeparatorChar));

                if (System.IO.File.Exists(physicalPath))
                    System.IO.File.Delete(physicalPath);
            }
        }

      // NGƯNG BÁN
        [HttpPost]
        public IActionResult NgungBanTour(int id)
        {
            if (!LaAdmin()) return NeuKhongPhaiAdmin();

            var tour = _context.Tours.FirstOrDefault(t => t.IdTour == id);
            if (tour == null) return NotFound();

            tour.TrangThai = false;
            _context.SaveChanges();

            TempData["ThongBao"] = "🚫 Đã ngưng bán tour";
            return RedirectToAction("QuanLyTour");
        }
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult XoaTour(int id)
        {
            if (!LaAdmin()) return NeuKhongPhaiAdmin();

            var tour = _context.Tours
                .Include(t => t.HinhTours)
                .Include(t => t.TourGiaChiTiets)
                .Include(t => t.LichKhoiHanhs)
                .FirstOrDefault(t => t.IdTour == id);

            if (tour == null) return NotFound();

            _context.HinhTours.RemoveRange(tour.HinhTours);
            _context.TourGiaChiTiets.RemoveRange(tour.TourGiaChiTiets);
            _context.LichKhoiHanhs.RemoveRange(tour.LichKhoiHanhs);

            _context.Tours.Remove(tour);
            _context.SaveChanges();

            TempData["ThongBao"] = "✅ Đã xóa tour";
            return RedirectToAction("QuanLyTour");
        }

        // MỞ BÁN LẠI
       [HttpPost]
        public IActionResult MoLaiTour(int id)
        {
            if (!LaAdmin()) return NeuKhongPhaiAdmin();

            var tour = _context.Tours.FirstOrDefault(t => t.IdTour == id);
            if (tour == null) return NotFound();

            tour.TrangThai = true;
            _context.SaveChanges();

            TempData["ThongBao"] = "✅ Đã mở bán lại tour";
            return RedirectToAction("QuanLyTour");
        }
       
        // FORM THÊM LỊCH
        [HttpGet]
        public IActionResult ThemLich(int idTour)
        {
            if (!LaAdmin()) return NeuKhongPhaiAdmin();

            ViewBag.IdTour = idTour;
            return View();
        }
      
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ThemLich(LichKhoiHanh model)
        {
            if (!LaAdmin())
                return NeuKhongPhaiAdmin();

            IActionResult BaoLoi(string message)
            {
                TempData["Error"] = message;
                return RedirectToAction("DanhSachLich");
            }

            // Kiểm tra tour.
            if (model.IdTour <= 0)
                return BaoLoi("Vui lòng chọn tour cần thêm lịch.");

            bool tourTonTai = _context.Tours
                .Any(t => t.IdTour == model.IdTour);

            if (!tourTonTai)
                return BaoLoi("Tour được chọn không tồn tại.");

            // Kiểm tra sức chứa.
            if (model.SoChoToiDa <= 0)
                return BaoLoi("Số chỗ tối đa phải lớn hơn 0.");

            // Kiểm tra ngày và giờ.
            var homNay = DateOnly.FromDateTime(DateTime.Now);
            var gioHienTai = TimeOnly.FromDateTime(DateTime.Now);

            if (model.NgayKhoiHanh < homNay)
                return BaoLoi(
                    "Không thể tạo lịch khởi hành trong quá khứ."
                );

            if (!model.GioKhoiHanh.HasValue)
                return BaoLoi("Vui lòng nhập giờ khởi hành.");

            if (model.NgayKhoiHanh == homNay &&
                model.GioKhoiHanh.Value <= gioHienTai)
            {
                return BaoLoi(
                    "Giờ khởi hành phải lớn hơn thời gian hiện tại."
                );
            }

            // Một tour không được có hai lịch cùng ngày.
            bool daCoLich = _context.LichKhoiHanhs
                .Any(l =>
                    l.IdTour == model.IdTour &&
                    l.NgayKhoiHanh == model.NgayKhoiHanh);

            if (daCoLich)
                return BaoLoi(
                    "Tour này đã có lịch khởi hành vào ngày đã chọn."
                );

            // Không lấy số chỗ còn lại do trình duyệt gửi lên.
            var lichMoi = new LichKhoiHanh
            {
                IdTour = model.IdTour,
                NgayKhoiHanh = model.NgayKhoiHanh,
                GioKhoiHanh = model.GioKhoiHanh,
                SoChoToiDa = model.SoChoToiDa,
                SoChoConLai = model.SoChoToiDa,
                TrangThai = "Còn chỗ"
            };

            try
            {
                _context.LichKhoiHanhs.Add(lichMoi);
                _context.SaveChanges();

                TempData["ThongBao"] =
                    "Đã thêm lịch khởi hành thành công.";    
                return RedirectToAction("DanhSachLich");
            }
            catch (DbUpdateException)
            {
                TempData["Error"] =
                    "Không thể thêm lịch. Vui lòng kiểm tra " +
                    "lịch trùng hoặc ràng buộc dữ liệu.";

                return RedirectToAction("DanhSachLich");
            }
        }
        // ================= SỬA LỊCH KHỞI HÀNH =================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SuaLich(LichKhoiHanh model)
        {
            if (!LaAdmin())
                return NeuKhongPhaiAdmin();

            var lich = _context.LichKhoiHanhs
                .FirstOrDefault(l => l.IdLich == model.IdLich);

            if (lich == null)
            {
                TempData["Error"] =
                    "Không tìm thấy lịch khởi hành.";

                return RedirectToAction("DanhSachLich");
            }

            IActionResult BaoLoi(string message)
            {
                TempData["Error"] = message;

                return RedirectToAction(
                    "DanhSachLich",
                    new { idTour = lich.IdTour }
                );
            }

            var homNay = DateOnly.FromDateTime(DateTime.Now);
            var gioHienTai = TimeOnly.FromDateTime(DateTime.Now);

            // Không sửa lịch đã qua.
            if (lich.NgayKhoiHanh < homNay)
                return BaoLoi(
                    "Không thể chỉnh sửa lịch đã qua ngày khởi hành."
                );

            // Giữ mọi lịch đã từng phát sinh đơn hàng,
            // kể cả đơn đã hủy.
            bool daTungCoDon = _context.DonDatTours
                .Any(d => d.IdLich == lich.IdLich);

            if (daTungCoDon)
                return BaoLoi(
                    "Lịch đã có lịch sử đặt tour, không thể sửa trực tiếp."
                );

            // Tính số chỗ đang bị chiếm từ dữ liệu cũ
            // TRƯỚC KHI cập nhật sức chứa.
            int soChoDaGiu = Math.Max(
                0,
                lich.SoChoToiDa - lich.SoChoConLai
            );

            // Phòng trường hợp dữ liệu giữ chỗ tồn tại
            // nhưng chưa có bản ghi đơn tương ứng.
            if (soChoDaGiu > 0)
                return BaoLoi(
                    "Lịch đang có chỗ được giữ. " +
                    "Không thể thay đổi lịch khi chưa xử lý số chỗ này."
                );

            if (model.SoChoToiDa <= 0)
                return BaoLoi(
                    "Số chỗ tối đa phải lớn hơn 0."
                );

            if (!model.GioKhoiHanh.HasValue)
                return BaoLoi(
                    "Vui lòng nhập giờ khởi hành."
                );

            if (model.NgayKhoiHanh < homNay)
                return BaoLoi(
                    "Ngày khởi hành mới không được nằm trong quá khứ."
                );

            if (model.NgayKhoiHanh == homNay &&
                model.GioKhoiHanh.Value <= gioHienTai)
            {
                return BaoLoi(
                    "Giờ khởi hành phải lớn hơn thời gian hiện tại."
                );
            }

            bool trungLich = _context.LichKhoiHanhs
                .Any(l =>
                    l.IdTour == lich.IdTour &&
                    l.IdLich != lich.IdLich &&
                    l.NgayKhoiHanh == model.NgayKhoiHanh);

            if (trungLich)
                return BaoLoi(
                    "Tour đã có lịch khác vào ngày được chọn."
                );

            // Cập nhật sau khi kiểm tra toàn bộ điều kiện.
            lich.NgayKhoiHanh = model.NgayKhoiHanh;
            lich.GioKhoiHanh = model.GioKhoiHanh;
            lich.SoChoToiDa = model.SoChoToiDa;

            // Dùng số chỗ đã giữ từ trạng thái cũ.
            lich.SoChoConLai =
                model.SoChoToiDa - soChoDaGiu;

            lich.TrangThai = lich.SoChoConLai <= 0
                ? "Hết chỗ"
                : lich.SoChoConLai <= 5
                    ? "Sắp hết chỗ"
                    : "Còn chỗ";

            try
            {
                _context.SaveChanges();

                TempData["ThongBao"] =
                    "Đã cập nhật lịch khởi hành thành công.";

                return RedirectToAction(
                    "DanhSachLich",
                    new { idTour = lich.IdTour }
                );
            }
            catch (DbUpdateException)
            {
                return BaoLoi(
                    "Không thể cập nhật lịch do ràng buộc dữ liệu."
                );
            }
        }
      
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult XoaLich(int id)
        {
            if (!LaAdmin())
                return NeuKhongPhaiAdmin();

            var lich = _context.LichKhoiHanhs
                .FirstOrDefault(l => l.IdLich == id);

            if (lich == null)
            {
                TempData["Error"] =
                    "Lịch khởi hành không tồn tại.";

                return RedirectToAction("DanhSachLich");
            }

            IActionResult BaoLoi(string message)
            {
                TempData["Error"] = message;

                return RedirectToAction(
                    "DanhSachLich",
                    new { idTour = lich.IdTour }
                );
            }

            // Kiểm tra tất cả đơn hàng,
            // không phân biệt trạng thái.
            bool daTungCoDon = _context.DonDatTours
                .Any(d => d.IdLich == id);

            if (daTungCoDon)
            {
                return BaoLoi(
                    "Không thể xóa lịch đã có lịch sử đặt tour, " +
                    "kể cả đơn hàng đã hủy."
                );
            }

            // Không xóa lịch còn chỗ đang bị giữ.
            int soChoDaGiu = Math.Max(
                0,
                lich.SoChoToiDa - lich.SoChoConLai
            );

            if (soChoDaGiu > 0)
            {
                return BaoLoi(
                    "Lịch đang có số chỗ được giữ, " +
                    "cần kiểm tra dữ liệu trước khi xóa."
                );
            }

            // Kiểm tra DanhSachCho nếu bảng tồn tại.
            // Câu SQL chỉ đọc, không thay đổi dữ liệu.
            int soKhachCho;

            try
            {
                soKhachCho = _context.Database
                    .SqlQuery<int>(
                        $"""
                        IF OBJECT_ID(N'dbo.DanhSachCho', N'U') IS NULL
                            SELECT CAST(0 AS int) AS Value;
                        ELSE
                            SELECT COUNT(*) AS Value
                            FROM dbo.DanhSachCho
                            WHERE IdLich = {id};
                        """
                    )
                    .ToList()
                    .FirstOrDefault();
            }
            catch (Exception)
            {
                return BaoLoi(
                    "Không thể xác minh danh sách chờ. " +
                    "Hệ thống chưa thực hiện xóa lịch."
                );
            }

            if (soKhachCho > 0)
            {
                return BaoLoi(
                    "Lịch có dữ liệu danh sách chờ, " +
                    "không thể xóa trực tiếp."
                );
            }

            try
            {
                _context.LichKhoiHanhs.Remove(lich);
                _context.SaveChanges();

                TempData["ThongBao"] =
                    "Đã xóa lịch khởi hành thành công.";

                return RedirectToAction("DanhSachLich");
            }
            catch (DbUpdateException)
            {
                return BaoLoi(
                    "Lịch đang được liên kết với dữ liệu khác. " +
                    "Không thể xóa để bảo vệ lịch sử hệ thống."
                );
            }
        }
        // ================= DANH SÁCH TẤT CẢ LỊCH (sidebar) =================
        public IActionResult DanhSachLich(int? idTour)
        {
            if (!LaAdmin()) return NeuKhongPhaiAdmin();

            var query = _context.LichKhoiHanhs
            .Include(l => l.IdTourNavigation)
            .Include(l => l.DonDatTours)   // ✅ BẮT BUỘC PHẢI CÓ DÒNG NÀY
            .AsQueryable();


            if (idTour != null)
            {
                query = query.Where(l => l.IdTour == idTour);
            }

            ViewBag.Tours = _context.Tours.ToList();
            ViewBag.IdTour = idTour;
            ViewBag.WaitlistCounts = LaySoLuongDanhSachCho();

            return View(query.OrderByDescending(l => l.NgayKhoiHanh).ToList());
        }

        public IActionResult DanhSachCho(int? idLich)
        {
            if (!LaAdmin()) return NeuKhongPhaiAdmin();

            var result = new List<DanhSachChoAdminVM>();
            var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open) connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
IF OBJECT_ID(N'dbo.DanhSachCho', N'U') IS NOT NULL
BEGIN
    SELECT ds.IdDanhSachCho, ds.IdLich, t.TenTour, l.NgayKhoiHanh,
           tk.HoTen, tk.Email, tk.SoDienThoai, ds.SoKhach, ds.NgayDangKy, ds.TrangThai
    FROM dbo.DanhSachCho ds
    INNER JOIN dbo.LichKhoiHanh l ON l.IdLich = ds.IdLich
    INNER JOIN dbo.Tour t ON t.IdTour = l.IdTour
    INNER JOIN dbo.TaiKhoan tk ON tk.IdTaiKhoan = ds.IdTaiKhoan
    WHERE (@IdLich IS NULL OR ds.IdLich = @IdLich)
    ORDER BY CASE WHEN ds.TrangThai = N'Đang chờ' THEN 0 ELSE 1 END, ds.NgayDangKy;
END";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@IdLich";
            parameter.Value = idLich.HasValue ? idLich.Value : DBNull.Value;
            command.Parameters.Add(parameter);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new DanhSachChoAdminVM
                {
                    IdDanhSachCho = reader.GetInt32(0),
                    IdLich = reader.GetInt32(1),
                    TenTour = reader.GetString(2),
                    NgayKhoiHanh = DateOnly.FromDateTime(reader.GetDateTime(3)),
                    HoTen = reader.GetString(4),
                    Email = reader.GetString(5),
                    SoDienThoai = reader.IsDBNull(6) ? null : reader.GetString(6),
                    SoKhach = reader.GetInt32(7),
                    NgayDangKy = reader.GetDateTime(8),
                    TrangThai = reader.GetString(9)
                });
            }

            ViewBag.IdLich = idLich;
            return View(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CapNhatDanhSachCho(int id, string trangThai)
        {
            if (!LaAdmin()) return NeuKhongPhaiAdmin();

            var allowed = new[] { "Đang chờ", "Đã thông báo", "Đã liên hệ", "Đã đặt", "Đã hủy" };
            if (!allowed.Contains(trangThai)) return BadRequest();

            _context.Database.ExecuteSqlInterpolated($@"
IF OBJECT_ID(N'dbo.DanhSachCho', N'U') IS NOT NULL
    UPDATE dbo.DanhSachCho SET TrangThai = {trangThai} WHERE IdDanhSachCho = {id};");

            TempData["ThongBao"] = "Đã cập nhật trạng thái danh sách chờ.";
            return RedirectToAction(nameof(DanhSachCho));
        }

        private Dictionary<int, int> LaySoLuongDanhSachCho()
        {
            var result = new Dictionary<int, int>();
            var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open) connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
IF OBJECT_ID(N'dbo.DanhSachCho', N'U') IS NOT NULL
    SELECT IdLich, COUNT(*) FROM dbo.DanhSachCho
    WHERE TrangThai IN (N'Đang chờ', N'Đã thông báo', N'Đã liên hệ') GROUP BY IdLich;";
            using var reader = command.ExecuteReader();
            while (reader.Read()) result[reader.GetInt32(0)] = reader.GetInt32(1);
            return result;
        }

        [HttpGet]
        public IActionResult ExportHoaDon(int idDon)
        {
            if (!LaAdmin()) return NeuKhongPhaiAdmin();

            var don = _context.DonDatTours
                .Include(x => x.IdTaiKhoanNavigation)
                .Include(x => x.IdTourNavigation)
                .Include(x => x.IdLichNavigation)
                .FirstOrDefault(x => x.IdDon == idDon);

            if (don == null) return NotFound();

            using (var stream = new MemoryStream())
            {
                var writer = new PdfWriter(stream);
                var pdf = new PdfDocument(writer);
                var doc = new Document(pdf, iText.Kernel.Geom.PageSize.A4);

                // Lề cho gọn
                doc.SetMargins(30, 30, 30, 30);

               // FONT TIẾNG VIỆT
                var fontPathRegular = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/fonts/Roboto-Regular.ttf");
                var fontPathBold    = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/fonts/Roboto-Bold.ttf");

                var fontNormal = PdfFontFactory.CreateFont(fontPathRegular, PdfEncodings.IDENTITY_H);
                var fontBold   = PdfFontFactory.CreateFont(fontPathBold,    PdfEncodings.IDENTITY_H);


                doc.SetFont(fontNormal);

                // ======== MÀU CHỦ ĐẠO ========
                Color primary   = new DeviceRgb(13, 110, 253);
                Color darkText  = new DeviceRgb(33, 37, 41);
                Color lightGray = new DeviceRgb(248, 249, 250);

                // ======== KHUNG NGOÀI ========
                var outerTable = new Table(1).UseAllAvailableWidth();
                outerTable.SetBorder(new SolidBorder(primary, 2));
                outerTable.SetBackgroundColor(ColorConstants.WHITE);
                outerTable.SetPadding(15);

                // ================== HEADER (LOGO + MÃ BOOKING) ==================
                var headerTable = new Table(new float[] { 3, 2 }).UseAllAvailableWidth();

                // Logo (nếu có file)
                var logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "img", "logo-happytrip.png");
                Cell cellLogo;
                if (System.IO.File.Exists(logoPath))
                {
                    var imgData = ImageDataFactory.Create(logoPath);
                    var img = new Image(imgData).SetMaxHeight(50).SetAutoScale(true);
                    cellLogo = new Cell().Add(img)
                                        .SetBorder(Border.NO_BORDER)
                                        .SetVerticalAlignment(iText.Layout.Properties.VerticalAlignment.MIDDLE);
                }
                else
                {
                    // fallback chỉ hiện chữ
                    cellLogo = new Cell()
                        .Add(new Paragraph("HappyTrip Travel")
                            .SetFont(fontBold)
                            .SetFontSize(18)
                            .SetFontColor(primary))
                        .SetBorder(Border.NO_BORDER)
                        .SetVerticalAlignment(iText.Layout.Properties.VerticalAlignment.MIDDLE);
                }

                headerTable.AddCell(cellLogo);

                // Mã booking + tiêu đề
                var rightHeader = new Cell()
                    .SetBorder(Border.NO_BORDER)
                    .SetTextAlignment(TextAlignment.RIGHT);

                rightHeader.Add(new Paragraph("HÓA ĐƠN ĐẶT TOUR")
                    .SetFont(fontBold)
                    .SetFontSize(18)
                    .SetFontColor(darkText));

                rightHeader.Add(new Paragraph($"Mã Booking: {don.MaBooking}")
                    .SetFont(fontBold)
                    .SetFontSize(12)
                    .SetFontColor(primary));

                rightHeader.Add(new Paragraph($"Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm}")
                    .SetFontSize(9)
                    .SetFontColor(ColorConstants.GRAY));

                headerTable.AddCell(rightHeader);

                outerTable.AddCell(new Cell().Add(headerTable)
                                            .SetBorder(Border.NO_BORDER));

                // ================== THÔNG TIN KHÁCH + TRẠNG THÁI ==================
                var infoTop = new Table(new float[] { 3, 2 }).UseAllAvailableWidth();
                infoTop.SetMarginTop(10);

                // Cột khách hàng
                var cellCustomer = new Cell().SetBorder(Border.NO_BORDER);
                cellCustomer.Add(new Paragraph("THÔNG TIN KHÁCH HÀNG")
                    .SetFont(fontBold)
                    .SetFontSize(11)
                    .SetFontColor(primary));

                var kh = don.IdTaiKhoanNavigation;
                cellCustomer.Add(new Paragraph($"{kh.HoTen}")
                    .SetFont(fontBold)
                    .SetFontSize(11));
                cellCustomer.Add(new Paragraph($"Email: {kh.Email}").SetFontSize(10));
                cellCustomer.Add(new Paragraph($"Số điện thoại: {kh.SoDienThoai}").SetFontSize(10));

                infoTop.AddCell(cellCustomer);

                // Cột trạng thái + tổng tiền mini
                var cellStatus = new Cell()
                    .SetBorder(Border.NO_BORDER)
                    .SetTextAlignment(TextAlignment.RIGHT);

                string labelTrangThai = don.TrangThai;
                Color statusColor = primary;
                if (don.TrangThai == "Đã hủy")
                    statusColor = ColorConstants.RED;
                else if (don.TrangThai == "Chờ duyệt")
                    statusColor = ColorConstants.ORANGE;

                cellStatus.Add(new Paragraph("TRẠNG THÁI ĐƠN")
                    .SetFont(fontBold)
                    .SetFontSize(10)
                    .SetFontColor(ColorConstants.GRAY));

                cellStatus.Add(new Paragraph(labelTrangThai)
                    .SetFont(fontBold)
                    .SetFontSize(12)
                    .SetFontColor(statusColor));

                cellStatus.Add(new Paragraph("\nTỔNG TIỀN")
                    .SetFont(fontBold)
                    .SetFontSize(10)
                    .SetFontColor(ColorConstants.GRAY));

                cellStatus.Add(new Paragraph($"{don.TongTien:N0} đ")
                    .SetFont(fontBold)
                    .SetFontSize(14)
                    .SetFontColor(darkText));

                infoTop.AddCell(cellStatus);

                outerTable.AddCell(new Cell().Add(infoTop)
                                            .SetBorder(Border.NO_BORDER));

                // ================== BLOCK THÔNG TIN TOUR ==================
                var tourBlock = new Table(1).UseAllAvailableWidth();
                tourBlock.SetMarginTop(15);

                // tiêu đề block
                tourBlock.AddCell(
                    new Cell()
                        .Add(new Paragraph("THÔNG TIN TOUR")
                            .SetFont(fontBold)
                            .SetFontSize(11)
                            .SetFontColor(ColorConstants.WHITE))
                        .SetBackgroundColor(primary)
                        .SetBorder(Border.NO_BORDER)
                        .SetPadding(6)
                );

                var tourInner = new Table(new float[] { 1, 2 }).UseAllAvailableWidth();
                tourInner.SetBackgroundColor(lightGray);
                tourInner.SetPadding(8);

                void AddRow(string label, string value)
                {
                    tourInner.AddCell(
                        new Cell()
                            .Add(new Paragraph(label).SetFontSize(10).SetFont(fontBold))
                            .SetBorder(Border.NO_BORDER)
                    );
                    tourInner.AddCell(
                        new Cell()
                            .Add(new Paragraph(value).SetFontSize(10))
                            .SetBorder(Border.NO_BORDER)
                    );
                }

                AddRow("Tour:", don.IdTourNavigation.TenTour);
                AddRow("Khởi hành:",
                    $"{don.IdLichNavigation.NgayKhoiHanh:dd/MM/yyyy} - {don.IdLichNavigation.GioKhoiHanh:hh\\:mm}");
                AddRow("Người lớn:", don.NguoiLon.ToString());
                AddRow("Trẻ em:", don.TreEm.ToString());
                AddRow("Trẻ nhỏ:", don.TreNho.ToString());

                tourBlock.AddCell(new Cell().Add(tourInner).SetBorder(Border.NO_BORDER));

                outerTable.AddCell(new Cell().Add(tourBlock).SetBorder(Border.NO_BORDER));

                // ================== BLOCK THANH TOÁN ==================
                var payBlock = new Table(1).UseAllAvailableWidth();
                payBlock.SetMarginTop(15);

                payBlock.AddCell(
                    new Cell()
                        .Add(new Paragraph("THANH TOÁN")
                            .SetFont(fontBold)
                            .SetFontSize(11)
                            .SetFontColor(ColorConstants.WHITE))
                        .SetBackgroundColor(primary)
                        .SetBorder(Border.NO_BORDER)
                        .SetPadding(6)
                );

                var payInner = new Table(new float[] { 1, 2 }).UseAllAvailableWidth();
                payInner.SetBackgroundColor(lightGray);
                payInner.SetPadding(8);

                AddRowPayment("Tổng tiền:", $"{don.TongTien:N0} đ");
                AddRowPayment("Trạng thái thanh toán:", don.TrangThai);

                void AddRowPayment(string label, string value)
                {
                    payInner.AddCell(
                        new Cell()
                            .Add(new Paragraph(label).SetFontSize(10).SetFont(fontBold))
                            .SetBorder(Border.NO_BORDER)
                    );
                    payInner.AddCell(
                        new Cell()
                            .Add(new Paragraph(value).SetFontSize(10))
                            .SetBorder(Border.NO_BORDER)
                    );
                }

                payBlock.AddCell(new Cell().Add(payInner).SetBorder(Border.NO_BORDER));

                outerTable.AddCell(new Cell().Add(payBlock).SetBorder(Border.NO_BORDER));

                // ================== FOOTER ==================
                var footer = new Paragraph("Cảm ơn bạn đã sử dụng dịch vụ HappyTrip!\nHotline hỗ trợ: 1900 1234 • Website: happytrip.vn")
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontSize(9)
                    .SetFontColor(ColorConstants.GRAY)
                    .SetMarginTop(20);

                outerTable.AddCell(new Cell().Add(footer)
                                            .SetBorder(Border.NO_BORDER));

                // Thêm khung ngoài vào document
                doc.Add(outerTable);

                doc.Close();

                return File(
                    stream.ToArray(),
                    "application/pdf",
                    $"HoaDon_{don.MaBooking}.pdf"
                );
            }
        }

    }
}
