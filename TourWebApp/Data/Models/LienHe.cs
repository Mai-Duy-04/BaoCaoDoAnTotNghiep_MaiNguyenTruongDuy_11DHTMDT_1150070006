using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TourWebApp.Data.Models;

[Table("LienHe")]
public partial class LienHe
{
    [Key]
    public int IdLienHe { get; set; }

    [Required]
    [StringLength(100)]
    public string HoTen { get; set; } = null!;

    [Required]
    [StringLength(100)]
    public string Email { get; set; } = null!;

    [StringLength(20)]
    public string? DienThoai { get; set; }

    [Required]
    public string NoiDung { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime NgayGui { get; set; }

    // Trang thai xu ly yeu cau tu van
    [Required]
    [StringLength(30)]
    public string TrangThai { get; set; } = "Chưa phản hồi";

    // Noi dung Admin phan hoi khach hang
    public string? NoiDungPhanHoi { get; set; }

    // Thoi gian Admin gui phan hoi
    [Column(TypeName = "datetime")]
    public DateTime? NgayPhanHoi { get; set; }
}
