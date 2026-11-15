using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TourWebApp.Data.Models;

[Table("DonDatTour")]
[Index("TrangThai", Name = "IX_Don_TrangThai")]
[Index("MaBooking", Name = "UX_Don_MaBooking", IsUnique = true)]
public partial class DonDatTour
{
    [Key]
    public int IdDon { get; set; }

    [StringLength(20)]
    public string MaBooking { get; set; } = null!;

    public int IdTaiKhoan { get; set; }

    public int IdTour { get; set; }

    public int IdLich { get; set; }

    public int? IdPhieuGiamGia { get; set; }

    public int NguoiLon { get; set; }

    public int TreEm { get; set; }

    public int TreNho { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime NgayDat { get; set; }

    [Column(TypeName = "decimal(18, 0)")]
    public decimal TongTien { get; set; }

    [StringLength(50)]
    public string? MaPhieuGiamGia { get; set; }

    [Column(TypeName = "decimal(18, 0)")]
    public decimal? SoTienGiam { get; set; }

    [Column(TypeName = "decimal(18, 0)")]
    public decimal? TongTienSauGiam { get; set; }

    [StringLength(30)]
    public string TrangThai { get; set; } = null!;

    public string? GhiChu { get; set; }

    [StringLength(255)]
    public string? DiaChiLienHe { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? NgayHuy { get; set; }

    public bool DaThanhToan { get; set; } = false;

    public string? TrangThaiThanhToan { get; set; }

    public string? MaGiaoDich { get; set; }

    public DateTime? NgayThanhToan { get; set; }

    public DateTime? HanThanhToan { get; set; }

    public string? PhuongThucTT { get; set; }

    [ForeignKey("IdLich")]
    [InverseProperty("DonDatTours")]
    public virtual LichKhoiHanh IdLichNavigation { get; set; } = null!;

    [ForeignKey("IdPhieuGiamGia")]
    [InverseProperty("DonDatTours")]
    public virtual PhieuGiamGia? IdPhieuGiamGiaNavigation { get; set; }

    [ForeignKey("IdTaiKhoan")]
    [InverseProperty("DonDatTours")]
    public virtual TaiKhoan IdTaiKhoanNavigation { get; set; } = null!;

    [ForeignKey("IdTour")]
    [InverseProperty("DonDatTours")]
    public virtual Tour IdTourNavigation { get; set; } = null!;

    [InverseProperty("IdDonNavigation")]
    public virtual ICollection<ThongBao> ThongBaos { get; set; } = new List<ThongBao>();

    [InverseProperty("IdDonNavigation")]
    public virtual ICollection<PhieuGiamGiaSuDung> PhieuGiamGiaSuDungs { get; set; } = new List<PhieuGiamGiaSuDung>();
}
