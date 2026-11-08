
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TourWebApp.Data.Models;
using TourWebApp.Models.ViewModels;
using System.Text.RegularExpressions;

namespace TourWebApp.Controllers
{
    public class TourController : Controller
    {
        private readonly ApplicationDbContext _db;

        public TourController(ApplicationDbContext db)
        {
            _db = db;
        }

        // =====================================================
        // LOC TOUR THEO CHU DE
        // =====================================================

        private List<int> GetTourIdsByChuDe(string chuDe)
        {
            string[] maSoThich = chuDe.Trim().ToLowerInvariant() switch
            {
                "bien" => new[]
                {
                    "BIEN"
                },

                "nui" => new[]
                {
                    "NUI"
                },

                "vanhoa" => new[]
                {
                    "VANHOA",
                    "LICHSU"
                },

                "thanhpho" => new[]
                {
                    "THANHPHO"
                },

                "khampha" => new[]
                {
                    "CHECKIN",
                    "MAOHIEM",
                    "SINHTHAI"
                },

                _ => Array.Empty<string>()
            };

            if (maSoThich.Length == 0)
            {
                return new List<int>();
            }

            var tourIds = new HashSet<int>();

            foreach (var ma in maSoThich)
            {
                var ids = _db.Database.SqlQuery<int>(
                    $"""
                    SELECT DISTINCT ts.IdTour AS Value
                    FROM dbo.AITourSoThich AS ts
                    INNER JOIN dbo.AISoThich AS st
                        ON st.IdSoThich = ts.IdSoThich
                    WHERE st.MaSoThich = {ma}
                      AND st.TrangThai = 1
                    """
                ).ToList();

                foreach (var id in ids)
                {
                    tourIds.Add(id);
                }
            }

            return tourIds.ToList();
        }

        // =====================================================
        // HELPER: SO NGAY
        // =====================================================

        private int ExtractNgay(string? thoiGian)
        {
            if (string.IsNullOrWhiteSpace(thoiGian))
            {
                return 0;
            }

            var match = Regex.Match(thoiGian, "\\d+");

            if (match.Success &&
                int.TryParse(match.Value, out var soNgay))
            {
                return soNgay;
            }

            return 0;
        }

        // =====================================================
        // HELPER: GIA TIEN
        // =====================================================

        private decimal? ParseMoney(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var digitsOnly = new string(
                raw.Where(char.IsDigit).ToArray()
            );

            if (string.IsNullOrWhiteSpace(digitsOnly))
            {
                return null;
            }

            if (decimal.TryParse(digitsOnly, out var value))
            {
                return value;
            }

            return null;
        }

        // =====================================================
        // HELPER: NGAY DI
        // =====================================================

        private DateOnly? ParseNgayDi(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            if (DateOnly.TryParse(raw, out var dateOnly))
            {
                return dateOnly;
            }

            if (DateTime.TryParse(raw, out var dateTime))
            {
                return DateOnly.FromDateTime(dateTime);
            }

            return null;
        }

        // =====================================================
        // LICH KHOI HANH GAN NHAT
        // =====================================================

        private LichKhoiHanh? GetNextLich(int idTour)
        {
            return _db.LichKhoiHanhs
                .Where(l =>
                    l.IdTour == idTour &&
                    l.NgayKhoiHanh >=
                        DateOnly.FromDateTime(DateTime.Today))
                .OrderBy(l => l.SoChoConLai > 0 ? 0 : 1)
                .ThenBy(l => l.NgayKhoiHanh)
                .ThenBy(l => l.GioKhoiHanh)
                .FirstOrDefault();
        }

        // =====================================================
        // DANH SACH TAT CA TOUR
        // =====================================================

        public IActionResult TatCa(
            string? diadiem,
            string? ngaydi,
            int? songay,
            string? giamin,
            string? giamax,
            string? phuongtien,
            int? idLoaiTour,
            bool? concho,
            string? sort,
            string? chuDe,
            int page = 1)
        {
            int pageSize = 9;

            var today = DateOnly.FromDateTime(
                DateTime.Today
            );

            var query = _db.Tours
                .Where(t => t.TrangThai == true)
                .AsQueryable();

            // Loc theo chu de

            if (!string.IsNullOrWhiteSpace(chuDe))
            {
                var normalizedChuDe = chuDe
                    .Trim()
                    .ToLowerInvariant();

                var tourIds = GetTourIdsByChuDe(
                    normalizedChuDe
                );

                query = query.Where(t =>
                    tourIds.Contains(t.IdTour)
                );
            }

            // Tim theo ten tour, ma tour va dia diem

            if (!string.IsNullOrWhiteSpace(diadiem))
            {
                string keyword = diadiem.Trim();

                query = query.Where(t =>
                    t.TenTour.Contains(keyword) ||
                    (
                        t.MaTour != null &&
                        t.MaTour.Contains(keyword)
                    ) ||
                    (
                        t.DiaDiem != null &&
                        t.DiaDiem.Contains(keyword)
                    )
                );
            }

            // Loc ngay khoi hanh

            var ngayDiParsed = ParseNgayDi(ngaydi);

            if (ngayDiParsed.HasValue)
            {
                var nd = ngayDiParsed.Value;

                query = query.Where(t =>
                    _db.LichKhoiHanhs.Any(l =>
                        l.IdTour == t.IdTour &&
                        l.NgayKhoiHanh == nd
                    )
                );
            }

            // Loc phuong tien

            if (!string.IsNullOrWhiteSpace(phuongtien))
            {
                string pt = phuongtien
                    .Trim()
                    .ToLower();

                query = query.Where(t =>
                    t.PhuongTien != null &&
                    t.PhuongTien.ToLower().Contains(pt)
                );
            }

            // Loc loai tour

            if (idLoaiTour.HasValue &&
                idLoaiTour.Value > 0)
            {
                query = query.Where(t =>
                    t.IdLoaiTour == idLoaiTour.Value
                );
            }

            // Chi hien thi tour con cho

            if (concho == true)
            {
                query = query.Where(t =>
                    _db.LichKhoiHanhs.Any(l =>
                        l.IdTour == t.IdTour &&
                        l.NgayKhoiHanh >= today &&
                        l.SoChoConLai > 0
                    )
                );
            }

            // Loc ngan sach

            var giaMin = ParseMoney(giamin);
            var giaMax = ParseMoney(giamax);

            if (giaMin.HasValue)
            {
                query = query.Where(t =>
                    (t.GiaKhuyenMai ?? t.GiaGoc ?? 0)
                    >= giaMin.Value
                );
            }

            if (giaMax.HasValue)
            {
                query = query.Where(t =>
                    (t.GiaKhuyenMai ?? t.GiaGoc ?? 0)
                    <= giaMax.Value
                );
            }

            // Loc so ngay trong memory

            var filteredTours = query.ToList();

            if (songay.HasValue &&
                songay.Value > 0)
            {
                filteredTours = filteredTours
                    .Where(t =>
                        ExtractNgay(t.ThoiGian)
                        == songay.Value
                    )
                    .ToList();
            }

            // Sap xep

            filteredTours = (sort ?? string.Empty) switch
            {
                "gia_tang" => filteredTours
                    .OrderBy(t =>
                        t.GiaKhuyenMai ??
                        t.GiaGoc ??
                        decimal.MaxValue)
                    .ToList(),

                "gia_giam" => filteredTours
                    .OrderByDescending(t =>
                        t.GiaKhuyenMai ??
                        t.GiaGoc ??
                        0)
                    .ToList(),

                "ten" => filteredTours
                    .OrderBy(t => t.TenTour)
                    .ToList(),

                _ => filteredTours
                    .OrderByDescending(t => t.LuotXem)
                    .ToList()
            };

            // Phan trang

            if (page < 1)
            {
                page = 1;
            }

            int totalItems = filteredTours.Count;

            int totalPages = (int)Math.Ceiling(
                (double)totalItems / pageSize
            );

            if (totalPages == 0)
            {
                totalPages = 1;
            }

            if (page > totalPages)
            {
                page = totalPages;
            }

            var tours = filteredTours
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            // Du lieu bo loc

            ViewBag.LoaiTours = _db.LoaiTours
                .OrderBy(x => x.TenLoai)
                .ToList();

            ViewBag.PhuongTienList = _db.Tours
                .Where(t =>
                    t.TrangThai &&
                    t.PhuongTien != null &&
                    t.PhuongTien.Trim() != ""
                )
                .Select(t => t.PhuongTien!.Trim())
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            // Lich khoi hanh

            ViewBag.LichGanNhat = tours.ToDictionary(
                t => t.IdTour,
                t => GetNextLich(t.IdTour)
            );

            // Danh sach yeu thich

            var idTaiKhoan = HttpContext.Session
                .GetInt32("IdTaiKhoan");

            ViewBag.WishlistIds = new HashSet<int>();

            if (idTaiKhoan.HasValue)
            {
                try
                {
                    ViewBag.WishlistIds = _db.WishlistTours
                        .Where(x =>
                            x.IdTaiKhoan == idTaiKhoan.Value
                        )
                        .Select(x => x.IdTour)
                        .ToHashSet();
                }
                catch (SqlException ex)
                    when (ex.Number == 208)
                {
                    ViewBag.WishlistIds = new HashSet<int>();
                }
            }

            return View(tours);
        }

        // =====================================================
        // TOUR THEO LOAI
        // =====================================================

        public IActionResult TheoLoai(int id)
        {
            var loai = _db.LoaiTours
                .FirstOrDefault(l => l.IdLoaiTour == id);

            ViewBag.TenLoai = loai?.TenLoai ?? "Tour";

            var tours = _db.Tours
                .Where(t =>
                    t.TrangThai == true &&
                    t.IdLoaiTour == id
                )
                .OrderByDescending(t => t.LuotXem)
                .ToList();

            ViewBag.LichGanNhat = tours.ToDictionary(
                t => t.IdTour,
                t => GetNextLich(t.IdTour)
            );

            return View(tours);
        }

        // =====================================================
        // GOI Y DIA DIEM CU
        // =====================================================

        [HttpGet]
        public IActionResult GoiyDiaDiem(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return Json(new List<string>());
            }

            keyword = keyword.ToLower();

            var results = _db.Tours
                .Where(t =>
                    t.DiaDiem != null &&
                    t.DiaDiem.ToLower().Contains(keyword)
                )
                .Select(t => t.DiaDiem)
                .Distinct()
                .Take(10)
                .ToList();

            return Json(results);
        }

        // =====================================================
        // CHI TIET TOUR
        // =====================================================

        public IActionResult ChiTiet(
            int id,
            string? sortCmt)
        {
            var tour = _db.Tours
                .Include(t => t.HinhTours)
                .Include(t => t.LichKhoiHanhs)
                .Include(t => t.TourGiaChiTiets)
                .FirstOrDefault(t => t.IdTour == id);

            if (tour == null)
            {
                return NotFound();
            }

            // Tang luot xem

            tour.LuotXem++;

            _db.SaveChanges();

            // Lich khoi hanh gan nhat

            ViewBag.LichGanNhat = _db.LichKhoiHanhs
                .Where(l =>
                    l.IdTour == id &&
                    l.NgayKhoiHanh >=
                        DateOnly.FromDateTime(DateTime.Today)
                )
                .OrderBy(l => l.SoChoConLai > 0 ? 0 : 1)
                .ThenBy(l => l.NgayKhoiHanh)
                .ThenBy(l => l.GioKhoiHanh)
                .FirstOrDefault();

            // Gia theo doi tuong

            ViewBag.GiaNguoiLon = tour.TourGiaChiTiets
                .FirstOrDefault(x =>
                    TourPriceAudience.IsAdult(x.DoiTuong)
                );

            ViewBag.GiaTreEm = tour.TourGiaChiTiets
                .FirstOrDefault(x =>
                    TourPriceAudience.IsChild(x.DoiTuong)
                );

            ViewBag.GiaEmBe = tour.TourGiaChiTiets
                .FirstOrDefault(x =>
                    TourPriceAudience.IsInfant(x.DoiTuong)
                );

            // Tour lien quan

            ViewBag.TourLienQuan = _db.Tours
                .Where(t =>
                    t.IdLoaiTour == tour.IdLoaiTour &&
                    t.IdTour != id
                )
                .OrderByDescending(t => t.LuotXem)
                .Take(3)
                .ToList();

            // Binh luan

            var binhLuansQuery = _db.BinhLuanTours
                .Include(b => b.IdTaiKhoanNavigation)
                .Where(b =>
                    b.IdTour == id &&
                    b.HienThi
                );

            if (sortCmt == "old")
            {
                binhLuansQuery = binhLuansQuery
                    .OrderBy(b => b.NgayBL);
            }
            else
            {
                binhLuansQuery = binhLuansQuery
                    .OrderByDescending(b => b.NgayBL);
            }

            var binhLuans = binhLuansQuery.ToList();

            ViewBag.BinhLuanTour = binhLuans;
            ViewBag.BinhLuanCount = binhLuans.Count;
            ViewBag.SortCmt = sortCmt;

            // Trang thai yeu thich

            var idTaiKhoan = HttpContext.Session
                .GetInt32("IdTaiKhoan");

            ViewBag.WishlistIds = new HashSet<int>();

            if (idTaiKhoan.HasValue)
            {
                try
                {
                    ViewBag.WishlistIds = _db.WishlistTours
                        .Where(x =>
                            x.IdTaiKhoan == idTaiKhoan.Value
                        )
                        .Select(x => x.IdTour)
                        .ToHashSet();
                }
                catch (SqlException ex)
                    when (ex.Number == 208)
                {
                    ViewBag.WishlistIds = new HashSet<int>();
                }
            }

            return View(tour);
        }

        // =====================================================
        // THEM BINH LUAN TOUR
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ThemBinhLuanTour(
            int idTour,
            string noiDung)
        {
            var idTK = HttpContext.Session
                .GetInt32("IdTaiKhoan");

            if (idTK == null)
            {
                return RedirectToAction(
                    "DangNhap",
                    "TaiKhoan",
                    new
                    {
                        returnUrl = Url.Action(
                            "ChiTiet",
                            "Tour",
                            new { id = idTour }
                        )
                    }
                );
            }

            noiDung = (noiDung ?? "").Trim();

            if (string.IsNullOrWhiteSpace(noiDung))
            {
                return RedirectToAction(
                    "ChiTiet",
                    new { id = idTour }
                );
            }

            var tk = _db.TaiKhoans
                .FirstOrDefault(x =>
                    x.IdTaiKhoan == idTK.Value
                );

            if (tk == null)
            {
                return RedirectToAction(
                    "DangNhap",
                    "TaiKhoan"
                );
            }

            var bl = new BinhLuanTour
            {
                IdTour = idTour,
                IdBaiViet = null,
                IdTaiKhoan = idTK.Value,
                Ten = tk.HoTen,
                Email = tk.Email,
                DienThoai = tk.SoDienThoai,
                NoiDung = noiDung,
                NgayBL = DateTime.Now,
                HienThi = true,
                PhanHoiAdm = null
            };

            _db.BinhLuanTours.Add(bl);
            _db.SaveChanges();

            return RedirectToAction(
                "ChiTiet",
                new { id = idTour }
            );
        }

        // =====================================================
        // GOI Y TOUR TRUC TIEP
        // =====================================================

        [HttpGet]
        public IActionResult GoiYTour(string? keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return Json(Array.Empty<object>());
            }

            keyword = keyword.Trim();

            if (keyword.Length > 100)
            {
                keyword = keyword[..100];
            }

            var tours = _db.Tours
                .AsNoTracking()
                .Where(t =>
                    t.TrangThai == true &&
                    (
                        t.TenTour.Contains(keyword) ||
                        (
                            t.MaTour != null &&
                            t.MaTour.Contains(keyword)
                        ) ||
                        (
                            t.DiaDiem != null &&
                            t.DiaDiem.Contains(keyword)
                        )
                    )
                )
                .OrderByDescending(t => t.LuotXem)
                .Take(8)
                .Select(t => new
                {
                    t.HinhAnh,
                    t.IdTour,
                    t.TenTour,
                    t.DiaDiem,
                    Gia = t.GiaKhuyenMai ?? t.GiaGoc
                })
                .ToList();

            var result = tours.Select(t => new
            {
                idTour = t.IdTour,
                tenTour = t.TenTour,
                diaDiem = t.DiaDiem ?? "",
                gia = t.Gia.HasValue
                    ? t.Gia.Value.ToString("N0") + "đ"
                    : "Liên hệ",
                hinhAnh = TourWebApp.Helpers
                    .TourImagePathHelper
                    .ToTourImageSrc(t.HinhAnh),
                url = Url.Action(
                    "ChiTiet",
                    "Tour",
                    new { id = t.IdTour }
                )
            });

            return Json(result);
        }

        // =====================================================
        // TRANG INDEX
        // =====================================================

        public IActionResult Index()
        {
            return RedirectToAction("TatCa");
        }

        // =====================================================
        // SO SANH TOUR
        // =====================================================

        [HttpGet]
        public IActionResult SoSanh(string? ids)
        {
            var parsedIds = (ids ?? string.Empty)
                .Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries
                )
                .Select(x =>
                    int.TryParse(x, out var id)
                        ? id
                        : 0
                )
                .Where(id => id > 0)
                .Distinct()
                .Take(4)
                .ToList();

            if (parsedIds.Count < 2)
            {
                TempData["Error"] =
                    "Vui lòng chọn ít nhất 2 tour để so sánh.";

                return RedirectToAction(nameof(TatCa));
            }

            var tours = _db.Tours
                .Where(t =>
                    t.TrangThai &&
                    parsedIds.Contains(t.IdTour)
                )
                .Include(t => t.TourGiaChiTiets)
                .ToList();

            // Lay lich khoi hanh tuong lai

            var lichMap = _db.LichKhoiHanhs
                .Where(l =>
                    parsedIds.Contains(l.IdTour) &&
                    l.NgayKhoiHanh >=
                        DateOnly.FromDateTime(DateTime.Today)
                )
                .GroupBy(l => l.IdTour)
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .OrderBy(x => x.NgayKhoiHanh)
                        .ThenBy(x => x.GioKhoiHanh)
                        .FirstOrDefault()
                );

            var result = tours
                .OrderBy(t =>
                    parsedIds.IndexOf(t.IdTour)
                )
                .Select(t => new TourCompareVM
                {
                    IdTour = t.IdTour,
                    MaTour = t.MaTour,
                    TenTour = t.TenTour,
                    DiaDiem = t.DiaDiem,
                    ThoiGian = t.ThoiGian,
                    PhuongTien = t.PhuongTien,

                    GiaNguoiLon = t.TourGiaChiTiets
                        .FirstOrDefault(x =>
                            TourPriceAudience.IsAdult(
                                x.DoiTuong
                            )
                        )?.Gia
                        ?? t.GiaKhuyenMai
                        ?? t.GiaGoc,

                    SoChoConLai = lichMap.TryGetValue(
                        t.IdTour,
                        out var lich
                    )
                        ? (lich?.SoChoConLai ?? 0)
                        : 0,

                    HinhAnh = t.HinhAnh,
                    LuotXem = t.LuotXem
                })
                .ToList();

            return View(result);
        }
    }
}
