using Microsoft.AspNetCore.Mvc;
using TourWebApp.Data;
using TourWebApp.Data.Models;
using TourWebApp.Models.ViewModels.Travel;

namespace TourWebApp.Controllers;

public class TravelController : Controller
{
    private readonly ApplicationDbContext _db;

    public TravelController(ApplicationDbContext db)
    {
        _db = db;
    }

    public IActionResult Index()
    {
        var wishlistDestinationSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var userId = HttpContext.Session.GetInt32("IdTaiKhoan");
        if (userId.HasValue)
        {
            var wishlistTours = _db.WishlistTours
                .Where(x => x.IdTaiKhoan == userId.Value)
                .Select(x => x.IdTourNavigation)
                .Where(t => t != null)
                .ToList();

            foreach (var tour in wishlistTours)
            {
                var text = $"{tour!.TenTour} {tour.DiaDiem}".ToLowerInvariant();

                if (text.Contains("phú quốc") || text.Contains("phu quoc")) wishlistDestinationSlugs.Add("phu-quoc");
                if (text.Contains("vũng tàu") || text.Contains("vung tau")) wishlistDestinationSlugs.Add("vung-tau");
                if (text.Contains("đà lạt") || text.Contains("da lat")) wishlistDestinationSlugs.Add("da-lat");
                if (text.Contains("cần thơ") || text.Contains("can tho")) wishlistDestinationSlugs.Add("can-tho");
                if (text.Contains("hồ chí minh") || text.Contains("ho chi minh") || text.Contains("sài gòn") || text.Contains("sai gon")) wishlistDestinationSlugs.Add("sai-gon");
                if (text.Contains("miền tây") || text.Contains("mien tay")) wishlistDestinationSlugs.Add("mien-tay");
            }
        }

        ViewBag.WishlistDestinationSlugs = wishlistDestinationSlugs;

        var model = new TravelHomeViewModel
        {
            WhyTravelWithUs = TravelContentData.WhyTravelWithUs,
           FeaturedDestinations = TravelContentData.Destinations.ToList(),
            PopularTours = TravelContentData.Tours.Take(3).ToList(),
            GuidePosts = TravelContentData.GuidePosts,
            Testimonials = TravelContentData.Testimonials
        };

        return View(model);
    }

    public IActionResult Destinations(string filter = TravelContentData.AllDestinationsFilter)
    {
        var normalizedFilter = TravelContentData.DestinationFilters
            .FirstOrDefault(x => x.Equals(filter, StringComparison.OrdinalIgnoreCase)) ?? TravelContentData.AllDestinationsFilter;

        var model = new TravelDestinationsViewModel
        {
            Filters = TravelContentData.DestinationFilters,
            Items = TravelContentData.GetDestinations(normalizedFilter),
            SelectedFilter = normalizedFilter
        };

        return View(model);
    }

    public IActionResult Tours(string filter = TravelContentData.AllToursFilter)
    {
        var normalizedFilter = TravelContentData.TourFilters
            .FirstOrDefault(x => x.Equals(filter, StringComparison.OrdinalIgnoreCase)) ?? TravelContentData.AllToursFilter;

        var model = new TravelToursViewModel
        {
            Filters = TravelContentData.TourFilters,
            Items = TravelContentData.GetTours(normalizedFilter),
            SelectedFilter = normalizedFilter
        };

        return View(model);
    }

    public IActionResult DestinationDetail(string slug)
    {
        var item = TravelContentData.GetDestinationBySlug(slug);
        if (item is null)
        {
            return NotFound();
        }

        // Ưu tiên điều hướng sang trang chi tiết tour thật (Tour/ChiTiet) nếu tìm được tour phù hợp.
        var normalizedSlug = slug.Trim().ToLower();
        var candidateTours = _db.Tours
            .Where(t => t.TrangThai == true)
            .ToList();

        var matchedTour = candidateTours.FirstOrDefault(t =>
            (!string.IsNullOrWhiteSpace(t.TenTour) && t.TenTour.ToLower().Contains(normalizedSlug.Replace("-", " "))) ||
            (!string.IsNullOrWhiteSpace(t.DiaDiem) && t.DiaDiem.ToLower().Contains(item.Name.ToLower())));

        if (matchedTour is not null)
        {
            return RedirectToAction("ChiTiet", "Tour", new { id = matchedTour.IdTour });
        }

        var model = new TravelDestinationDetailViewModel
        {
            Item = item,
            RelatedDestinations = TravelContentData.GetRelatedDestinations(slug)
        };

        return View(model);
    }

    public IActionResult TourDetail(string slug)
    {
        var item = TravelContentData.GetTourBySlug(slug);
        if (item is null)
        {
            return NotFound();
        }

        // Ưu tiên điều hướng sang trang chi tiết tour thật (Tour/ChiTiet) để dùng đầy đủ logic DB.
        var normalizedSlug = slug.Trim().ToLower();
        var slugKeywords = normalizedSlug
            .Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(k => k.Length >= 2)
            .ToList();

        var candidateTours = _db.Tours
            .Where(t => t.TrangThai == true)
            .ToList();

        var matchedTour = candidateTours
            .Select(t => new
            {
                Tour = t,
                NameLower = (t.TenTour ?? string.Empty).ToLower(),
                LocationLower = (t.DiaDiem ?? string.Empty).ToLower()
            })
            .Select(x => new
            {
                x.Tour,
                Score = slugKeywords.Count(k => x.NameLower.Contains(k) || x.LocationLower.Contains(k))
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Tour.LuotXem)
            .Select(x => x.Tour)
            .FirstOrDefault();

        if (matchedTour is not null)
        {
            return RedirectToAction("ChiTiet", "Tour", new { id = matchedTour.IdTour });
        }

        var model = new TravelTourDetailViewModel
        {
            Item = item,
            RelatedTours = TravelContentData.GetRelatedTours(slug)
        };

        return View(model);
    }

    public IActionResult About()
    {
        var model = new TravelAboutViewModel
        {
            TeamMembers = TravelContentData.TeamMembers,
            Stats = TravelContentData.AboutStats
        };

        return View(model);
    }

    [HttpGet]
    public IActionResult Contact()
    {
        return View(new TravelContactViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Contact(TravelContactViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.IsSubmitted = false;
            return View(model);
        }

        Console.WriteLine($"[HappyTrip Contact] Name: {model.Form.Name} | Email: {model.Form.Email} | Destination: {model.Form.Destination} | Message: {model.Form.Message}");

        return View(new TravelContactViewModel
        {
            IsSubmitted = true,
            SuccessMessage = "Cảm ơn bạn. HappyTrip đã nhận thông tin và sẽ liên hệ tư vấn sớm nhất."
        });
    }
}
