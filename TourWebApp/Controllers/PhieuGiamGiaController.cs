
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using TourWebApp.Data.Models;

namespace TourWebApp.Controllers
{
    public class PhieuGiamGiaController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PhieuGiamGiaController(ApplicationDbContext context)
        {
            _context = context;
        }

        private bool IsAdmin() => HttpContext.Session.GetString("VaiTro") == "Admin";

        public IActionResult Index(string? trangThai = "tatca")
        {
            if (!IsAdmin()) return RedirectToAction("DangNhap", "TaiKhoan");

            var query = _context.PhieuGiamGias
                .Include(x => x.IdTourNavigation)
                .Include(x => x.IdLoaiTourNavigation)
                .AsQueryable();

            if (trangThai == "active")
            {
                query = query.Where(x => x.TrangThai);
            }
            else if (trangThai == "inactive")
            {
                query = query.Where(x => !x.TrangThai);
            }

            var ds = query
                .OrderByDescending(x => x.NgayTao)
                .ToList();

            ViewBag.TrangThai = trangThai ?? "tatca";
            return View(ds);
        }

        [HttpGet]
        public IActionResult Create()
        {
            if (!IsAdmin()) return RedirectToAction("DangNhap", "TaiKhoan");

            var model = new PhieuGiamGia
            {
                LoaiGiam = "PhanTram",
                PhamViApDung = "TatCa",
                TrangThai = true,
                DonToiThieu = 0,
                DaSuDung = 0
            };

            NapDuLieuDropdown();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(PhieuGiamGia model)
        {
            if (!IsAdmin()) return RedirectToAction("DangNhap", "TaiKhoan");

            ChuanHoaPhieu(model);

            if (!KiemTraHopLe(model, false))
            {
                NapDuLieuDropdown(model.IdTour, model.IdLoaiTour);
                return View(model);
            }

            model.DaSuDung = 0;
            model.NgayTao = DateTime.Now;
            model.NgayCapNhat = DateTime.Now;

            _context.PhieuGiamGias.Add(model);
            _context.SaveChanges();

            TempData["Success"] = "Da tao phieu giam gia.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            if (!IsAdmin()) return RedirectToAction("DangNhap", "TaiKhoan");

            var phieu = _context.PhieuGiamGias
                .FirstOrDefault(x => x.IdPhieuGiamGia == id);

            if (phieu == null) return NotFound();

            NapDuLieuDropdown(phieu.IdTour, phieu.IdLoaiTour);
            return View(phieu);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(PhieuGiamGia model)
        {
            if (!IsAdmin()) return RedirectToAction("DangNhap", "TaiKhoan");

            var phieu = _context.PhieuGiamGias
                .FirstOrDefault(x => x.IdPhieuGiamGia == model.IdPhieuGiamGia);

            if (phieu == null) return NotFound();

            ChuanHoaPhieu(model);

            if (!KiemTraHopLe(model, true))
            {
                NapDuLieuDropdown(model.IdTour, model.IdLoaiTour);
                return View(model);
            }

            if (model.TongLuotSuDung.HasValue &&
                model.TongLuotSuDung.Value < phieu.DaSuDung)
            {
                ModelState.AddModelError(
                    nameof(model.TongLuotSuDung),
                    "Tong luot su dung khong duoc nho hon so luot da dung."
                );

                NapDuLieuDropdown(model.IdTour, model.IdLoaiTour);
                return View(model);
            }

            phieu.MaPhieu = model.MaPhieu;
            phieu.TenPhieu = model.TenPhieu;
            phieu.LoaiGiam = model.LoaiGiam;
            phieu.GiaTriGiam = model.GiaTriGiam;
            phieu.DonToiThieu = model.DonToiThieu;
            phieu.GiamToiDa = model.GiamToiDa;
            phieu.NgayBatDau = model.NgayBatDau;
            phieu.NgayKetThuc = model.NgayKetThuc;
            phieu.TongLuotSuDung = model.TongLuotSuDung;
            phieu.LuotToiDaMoiTaiKhoan = model.LuotToiDaMoiTaiKhoan;
            phieu.PhamViApDung = model.PhamViApDung;
            phieu.IdTour = model.IdTour;
            phieu.IdLoaiTour = model.IdLoaiTour;
            phieu.TrangThai = model.TrangThai;
            phieu.MoTa = model.MoTa;
            phieu.NgayCapNhat = DateTime.Now;

            _context.SaveChanges();

            TempData["Success"] = "Da cap nhat phieu giam gia.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DoiTrangThai(int id)
        {
            if (!IsAdmin()) return RedirectToAction("DangNhap", "TaiKhoan");

            var phieu = _context.PhieuGiamGias
                .FirstOrDefault(x => x.IdPhieuGiamGia == id);

            if (phieu == null) return NotFound();

            phieu.TrangThai = !phieu.TrangThai;
            phieu.NgayCapNhat = DateTime.Now;

            _context.SaveChanges();

            TempData["Success"] = "Da cap nhat trang thai phieu.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            if (!IsAdmin()) return RedirectToAction("DangNhap", "TaiKhoan");

            var phieu = _context.PhieuGiamGias
                .FirstOrDefault(x => x.IdPhieuGiamGia == id);

            if (phieu == null) return NotFound();

            var daPhatSinhDon = _context.DonDatTours
                .Any(x => x.IdPhieuGiamGia == id);

            var daPhatSinhLichSu = _context.PhieuGiamGiaSuDungs
                .Any(x => x.IdPhieuGiamGia == id);

            var daLuuTrongViKhach = _context.PhieuGiamGiaTaiKhoans
                .Any(x => x.IdPhieuGiamGia == id);

            if (daPhatSinhDon ||
                daPhatSinhLichSu ||
                daLuuTrongViKhach)
            {
                phieu.TrangThai = false;
                phieu.NgayCapNhat = DateTime.Now;

                _context.SaveChanges();

                TempData["Error"] =
                    "Phiếu giảm giá đã có dữ liệu liên quan đến " +
                    "đơn đặt tour, lịch sử sử dụng hoặc ví khách hàng. " +
                    "Hệ thống đã chuyển phiếu sang trạng thái " +
                    "ngừng hoạt động để bảo toàn dữ liệu.";

                return RedirectToAction(nameof(Index));
            }

            try
            {
                _context.PhieuGiamGias.Remove(phieu);
                _context.SaveChanges();

                TempData["Success"] =
                    "Đã xóa vĩnh viễn phiếu giảm giá chưa có dữ liệu liên quan.";

                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is SqlException sqlEx &&
                      sqlEx.Number == 547)
            {
                TempData["Error"] =
                    "Không thể xóa vĩnh viễn phiếu giảm giá vì " +
                    "đang có dữ liệu liên kết trong hệ thống. " +
                    "Vui lòng tạm khóa phiếu để bảo toàn dữ liệu.";

                return RedirectToAction(nameof(Index));
            }
        }

        private void NapDuLieuDropdown(
            int? idTour = null,
            int? idLoaiTour = null)
        {
            ViewBag.DsTour = _context.Tours
                .OrderBy(x => x.TenTour)
                .Select(x => new
                {
                    x.IdTour,
                    x.TenTour,
                    Selected = idTour.HasValue &&
                               idTour.Value == x.IdTour
                })
                .ToList();

            ViewBag.DsLoaiTour = _context.LoaiTours
                .OrderBy(x => x.TenLoai)
                .Select(x => new
                {
                    x.IdLoaiTour,
                    x.TenLoai,
                    Selected = idLoaiTour.HasValue &&
                               idLoaiTour.Value == x.IdLoaiTour
                })
                .ToList();
        }

        private void ChuanHoaPhieu(PhieuGiamGia model)
        {
            model.MaPhieu = (model.MaPhieu ?? string.Empty)
                .Trim()
                .ToUpperInvariant();

            model.TenPhieu = (model.TenPhieu ?? string.Empty)
                .Trim();

            model.MoTa = model.MoTa?.Trim();

            model.LoaiGiam = string.IsNullOrWhiteSpace(model.LoaiGiam)
                ? "PhanTram"
                : model.LoaiGiam.Trim();

            model.PhamViApDung =
                string.IsNullOrWhiteSpace(model.PhamViApDung)
                    ? "TatCa"
                    : model.PhamViApDung.Trim();

            if (model.DonToiThieu < 0)
            {
                model.DonToiThieu = 0;
            }

            if (model.GiamToiDa.HasValue &&
                model.GiamToiDa.Value <= 0)
            {
                model.GiamToiDa = null;
            }

            if (model.TongLuotSuDung.HasValue &&
                model.TongLuotSuDung.Value <= 0)
            {
                model.TongLuotSuDung = null;
            }

            if (model.LuotToiDaMoiTaiKhoan.HasValue &&
                model.LuotToiDaMoiTaiKhoan.Value <= 0)
            {
                model.LuotToiDaMoiTaiKhoan = null;
            }

            if (model.PhamViApDung.Equals(
                "TatCa",
                StringComparison.OrdinalIgnoreCase))
            {
                model.IdTour = null;
                model.IdLoaiTour = null;
            }
            else if (model.PhamViApDung.Equals(
                "Tour",
                StringComparison.OrdinalIgnoreCase))
            {
                model.IdLoaiTour = null;
            }
            else if (model.PhamViApDung.Equals(
                "LoaiTour",
                StringComparison.OrdinalIgnoreCase))
            {
                model.IdTour = null;
            }
        }

        private bool KiemTraHopLe(
            PhieuGiamGia model,
            bool isEdit)
        {
            if (string.IsNullOrWhiteSpace(model.MaPhieu))
            {
                ModelState.AddModelError(
                    nameof(model.MaPhieu),
                    "Ma phieu khong duoc de trong."
                );
            }

            if (string.IsNullOrWhiteSpace(model.TenPhieu))
            {
                ModelState.AddModelError(
                    nameof(model.TenPhieu),
                    "Ten phieu khong duoc de trong."
                );
            }

            if (model.GiaTriGiam <= 0)
            {
                ModelState.AddModelError(
                    nameof(model.GiaTriGiam),
                    "Gia tri giam phai lon hon 0."
                );
            }

            if (model.LoaiGiam.Equals(
                    "PhanTram",
                    StringComparison.OrdinalIgnoreCase) &&
                model.GiaTriGiam > 100)
            {
                ModelState.AddModelError(
                    nameof(model.GiaTriGiam),
                    "Giam theo phan tram phai <= 100."
                );
            }

            if (model.NgayBatDau.HasValue &&
                model.NgayKetThuc.HasValue &&
                model.NgayBatDau.Value > model.NgayKetThuc.Value)
            {
                ModelState.AddModelError(
                    nameof(model.NgayKetThuc),
                    "Ngay ket thuc phai lon hon hoac bang ngay bat dau."
                );
            }

            if (model.PhamViApDung.Equals(
                    "Tour",
                    StringComparison.OrdinalIgnoreCase) &&
                !model.IdTour.HasValue)
            {
                ModelState.AddModelError(
                    nameof(model.IdTour),
                    "Vui long chon tour khi pham vi ap dung la Tour."
                );
            }

            if (model.PhamViApDung.Equals(
                    "LoaiTour",
                    StringComparison.OrdinalIgnoreCase) &&
                !model.IdLoaiTour.HasValue)
            {
                ModelState.AddModelError(
                    nameof(model.IdLoaiTour),
                    "Vui long chon loai tour khi pham vi ap dung la LoaiTour."
                );
            }

            var maTrung = _context.PhieuGiamGias.Any(x =>
                x.MaPhieu == model.MaPhieu &&
                (!isEdit ||
                 x.IdPhieuGiamGia != model.IdPhieuGiamGia));

            if (maTrung)
            {
                ModelState.AddModelError(
                    nameof(model.MaPhieu),
                    "Ma phieu da ton tai."
                );
            }

            if (model.TongLuotSuDung.HasValue &&
                model.LuotToiDaMoiTaiKhoan.HasValue &&
                model.LuotToiDaMoiTaiKhoan.Value >
                model.TongLuotSuDung.Value)
            {
                ModelState.AddModelError(
                    nameof(model.LuotToiDaMoiTaiKhoan),
                    "Luot toi da moi tai khoan khong duoc lon hon tong luot su dung."
                );
            }

            return ModelState.IsValid;
        }
    }
}
