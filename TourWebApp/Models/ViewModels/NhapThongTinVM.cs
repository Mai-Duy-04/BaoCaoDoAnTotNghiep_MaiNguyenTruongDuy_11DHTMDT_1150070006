using System;
using System.ComponentModel.DataAnnotations;

namespace TourWebApp.Models.ViewModels
{
    public class NhapThongTinVM
    {
        public const string PaymentMethodVnpay = "VNPAY";
        public const string PaymentMethodCash = "TienMat";

        // ========================================
        // THONG TIN TOUR VA LICH KHOI HANH
        // ========================================

        public int IdTour { get; set; }

        public int IdLich { get; set; }

        public string TenTour { get; set; } = string.Empty;

        public DateTime NgayKhoiHanh { get; set; }

        // ========================================
        // SO LUONG HANH KHACH
        // ========================================

        public int NguoiLon { get; set; }

        public int TreEm { get; set; }

        public int EmBe { get; set; }

        public int TongKhach => NguoiLon + TreEm + EmBe;

        // ========================================
        // GIA TOUR
        // Chi hien thi tren form.
        // Controller phai tinh lai tu CSDL.
        // ========================================

        public decimal GiaNguoiLon { get; set; }

        public decimal GiaTreEm { get; set; }

        public decimal GiaEmBe { get; set; }

        // ========================================
        // THANH TIEN VA PHIEU GIAM GIA
        // ========================================

        public decimal TongTien { get; set; }

        public decimal SoTienGiam { get; set; }

        public decimal TongTienSauGiam { get; set; }

        public string? MaPhieuGiamGia { get; set; }

        // ========================================
        // THONG TIN NGUOI DAI DIEN DAT TOUR
        // ========================================

        [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
        public string HoTen { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
        public string SDT { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập email.")]
        [EmailAddress(ErrorMessage = "Địa chỉ email không hợp lệ.")]
        public string Email { get; set; } = string.Empty;

        // Dia chi lien he khong bat buoc
        [StringLength(
            255,
            ErrorMessage = "Địa chỉ liên hệ không được vượt quá 255 ký tự.")]
        public string? DiaChiLienHe { get; set; }

        // ========================================
        // GHI CHU
        // ========================================

        public string GhiChu { get; set; } = string.Empty;

        // ========================================
        // PHUONG THUC THANH TOAN
        // ========================================

        public string PhuongThucThanhToan { get; set; }
            = PaymentMethodVnpay;
    }
}
