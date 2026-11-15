
using System.ComponentModel.DataAnnotations;
using TourWebApp.Models.Travel;

namespace TourWebApp.Models.ViewModels.Travel;

public class ContactFormInput
{
    [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
    [StringLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [StringLength(100, ErrorMessage = "Email tối đa 100 ký tự.")]
    public string Email { get; set; } = string.Empty;

    [StringLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự.")]
    [RegularExpression(
        @"^\+?[0-9\s.\-()]{9,20}$",
        ErrorMessage = "Số điện thoại không đúng định dạng.")]
    public string? Phone { get; set; }

    [StringLength(100, ErrorMessage = "Điểm đến tối đa 100 ký tự.")]
    public string Destination { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập nội dung cần tư vấn.")]
    [StringLength(
        2000,
        MinimumLength = 10,
        ErrorMessage = "Nội dung phải từ 10 đến 2000 ký tự.")]
    public string Message { get; set; } = string.Empty;
}

public class TravelHomeViewModel
{
    public required IReadOnlyList<FeatureItem> WhyTravelWithUs { get; set; }
    public required IReadOnlyList<DestinationItem> FeaturedDestinations { get; set; }
    public required IReadOnlyList<TourPackageItem> PopularTours { get; set; }
    public required IReadOnlyList<GuidePostItem> GuidePosts { get; set; }
    public required IReadOnlyList<TestimonialItem> Testimonials { get; set; }
}

public class TravelDestinationsViewModel
{
    public required IReadOnlyList<string> Filters { get; set; }
    public required IReadOnlyList<DestinationItem> Items { get; set; }
    public required string SelectedFilter { get; set; }
}

public class TravelToursViewModel
{
    public required IReadOnlyList<string> Filters { get; set; }
    public required IReadOnlyList<TourPackageItem> Items { get; set; }
    public required string SelectedFilter { get; set; }
}

public class TravelDestinationDetailViewModel
{
    public required DestinationItem Item { get; set; }
    public required IReadOnlyList<DestinationItem> RelatedDestinations { get; set; }
}

public class TravelTourDetailViewModel
{
    public required TourPackageItem Item { get; set; }
    public required IReadOnlyList<TourPackageItem> RelatedTours { get; set; }
}

public class TravelAboutViewModel
{
    public required IReadOnlyList<TeamMemberItem> TeamMembers { get; set; }
    public required IReadOnlyList<StatItem> Stats { get; set; }
}

public class TravelContactViewModel
{
    public ContactFormInput Form { get; set; } = new();
    public bool IsSubmitted { get; set; }
    public string SuccessMessage { get; set; } = string.Empty;
}
