using TourWebApp.Models.Travel;

namespace TourWebApp.Data;

public static class TravelContentData
{
    public const string AllDestinationsFilter = "Tất cả";
    public const string AllToursFilter = "Tất cả";

    public static readonly IReadOnlyList<string> DestinationFilters =
        new[] { AllDestinationsFilter, "Biển", "Núi", "Văn hóa", "Thành phố", "Khám phá" };

    public static readonly IReadOnlyList<string> TourFilters =
        new[] { AllToursFilter, "Khám phá", "Biển", "Văn hóa", "Nghỉ dưỡng" };

    public static readonly IReadOnlyList<FeatureItem> WhyTravelWithUs =
        new List<FeatureItem>
        {
            new() { IconClass = "fa-solid fa-route", Title = "Lịch trình tối ưu", Description = "Mỗi lịch trình được HappyTrip thiết kế rõ ràng, cân bằng giữa tham quan, nghỉ ngơi và trải nghiệm thực tế." },
            new() { IconClass = "fa-solid fa-user-group", Title = "Hướng dẫn viên bản địa", Description = "Đội ngũ hướng dẫn viên giàu kinh nghiệm, am hiểu địa phương và luôn đồng hành xuyên suốt chuyến đi." },
            new() { IconClass = "fa-solid fa-calendar-check", Title = "Đặt tour linh hoạt", Description = "Tư vấn theo nhu cầu thật, hỗ trợ đổi lịch hợp lý và minh bạch mọi chi phí trước khi xác nhận." },
            new() { IconClass = "fa-solid fa-headset", Title = "Hỗ trợ nhanh 24/7", Description = "Từ lúc lên kế hoạch đến khi kết thúc hành trình, HappyTrip luôn sẵn sàng hỗ trợ khi bạn cần." }
        };

    public static readonly IReadOnlyList<DestinationItem> Destinations =
        new List<DestinationItem>
        {
            new()
            {
                Slug = "phu-quoc",
                Name = "Phú Quốc",
                Country = "Việt Nam",
                Category = "Biển",
                Description = "Đảo ngọc với biển xanh trong vắt, những bãi cát trắng và khung cảnh hoàng hôn tuyệt đẹp.",
                PriceFrom = 3850000m,
                ImageUrl = "/img/tours/phuquoc.jpg"
            },
            new()
            {
                Slug = "vung-tau",
                Name = "Vũng Tàu",
                Country = "Việt Nam",
                Category = "Biển",
                Description = "Thành phố biển nổi bật với cung đường ven biển, ngọn hải đăng và làn gió đại dương.",
                PriceFrom = 1050000m,
                ImageUrl = "/img/tours/vungtau.jpg"
            },
            new()
            {
                Slug = "da-lat",
                Name = "Đà Lạt",
                Country = "Việt Nam",
                Category = "Núi",
                Description = "Thành phố sương mù với rừng thông xanh, những đồi hoa và bầu không khí mát lành.",
                PriceFrom = 2890000m,
                ImageUrl = "/img/tours/dalat.jpg"
            },
            new()
            {
                Slug = "can-tho",
                Name = "Cần Thơ",
                Country = "Việt Nam",
                Category = "Văn hóa",
                Description = "Chợ nổi buổi sớm, những dòng sông hiền hòa và nét văn hóa đặc trưng của miền Tây.",
                PriceFrom = 1490000m,
                ImageUrl = "/img/tours/cantho.jpg"
            },
            new()
            {
                Slug = "sai-gon",
                Name = "TP. Hồ Chí Minh",
                Country = "Việt Nam",
                Category = "Thành phố",
                Description = "Đô thị năng động với kiến trúc lịch sử, những con phố nhộn nhịp và ẩm thực phong phú.",
                PriceFrom = 1290000m,
                ImageUrl = "/img/tours/saigon-city.jpg"
            },
            new()
            {
                Slug = "mien-tay",
                Name = "Miền Tây sông nước",
                Country = "Việt Nam",
                Category = "Khám phá",
                Description = "Miệt vườn xanh mát, những chuyến đò bình dị và nét đẹp mộc mạc của vùng sông nước.",
                PriceFrom = 1590000m,
                ImageUrl = "/img/home/mientay.jpg"
            },
            new()
            {
                Slug = "ben-tre",
                Name = "Bến Tre",
                Country = "Việt Nam",
                Category = "Văn hóa",
                Description = "Xứ dừa thanh bình với những con rạch nhỏ, vườn cây xanh mát và cuộc sống miền quê gần gũi.",
                PriceFrom = 0m,
                ImageUrl = "/img/tours/bentre.jpg"
            },
            new()
            {
                Slug = "phan-thiet",
                Name = "Phan Thiết",
                Country = "Việt Nam",
                Category = "Biển",
                Description = "Vùng biển đầy nắng gió với đồi cát trải dài, làng chài bình dị và khung cảnh thiên nhiên đặc sắc.",
                PriceFrom = 0m,
                ImageUrl = "/img/tours/phanthiet.jpg"
            }
        };

    public static readonly IReadOnlyList<TourPackageItem> Tours =
        new List<TourPackageItem>
        {
            new() { Slug = "phu-quoc-3n2d", Name = "Phú Quốc 3N2Đ - Combo vé + khách sạn", Category = "Biển", Duration = "3 ngày 2 đêm", Rating = 4.9, Price = 3850000m, Description = "Trọn gói nghỉ dưỡng, xe đưa đón và lịch trình tham quan linh hoạt theo nhu cầu.", ImageUrl = "/img/tours/phuquoc.jpg" },
            new() { Slug = "vung-tau-1-ngay", Name = "Vũng Tàu 1 ngày - Tắm biển", Category = "Biển", Duration = "1 ngày", Rating = 4.7, Price = 1050000m, Description = "Lựa chọn tiết kiệm thời gian, phù hợp đi cuối tuần cùng gia đình hoặc nhóm bạn.", ImageUrl = "/img/tours/vungtau.jpg" },
            new() { Slug = "chau-doc-tra-su", Name = "Châu Đốc - Miếu Bà Chúa Xứ - Rừng Tràm Trà Sư", Category = "Văn hóa", Duration = "2 ngày 1 đêm", Rating = 4.8, Price = 890000m, Description = "Khám phá văn hóa bản địa, ẩm thực đặc sản và cảnh sắc thiên nhiên miền biên giới.", ImageUrl = "/img/tours/mientay.jpg" },
            new() { Slug = "da-lat-san-may", Name = "Đà Lạt săn mây - Đồi thông", Category = "Nghỉ dưỡng", Duration = "3 ngày 2 đêm", Rating = 4.8, Price = 2990000m, Description = "Trải nghiệm không khí se lạnh, quán cà phê view đẹp và lịch trình nghỉ dưỡng nhẹ nhàng.", ImageUrl = "/img/tours/dalat1.jpg" },
            new() { Slug = "can-tho-cho-noi", Name = "Cần Thơ - Chợ nổi Cái Răng", Category = "Văn hóa", Duration = "2 ngày 1 đêm", Rating = 4.7, Price = 1490000m, Description = "Đi thuyền chợ nổi, ăn sáng trên sông và ghé các làng nghề truyền thống đặc sắc.", ImageUrl = "/img/tours/cantho.jpg" },
            new() { Slug = "sai-gon-mien-tay", Name = "Sài Gòn - Miền Tây trải nghiệm", Category = "Khám phá", Duration = "2 ngày 1 đêm", Rating = 4.6, Price = 1390000m, Description = "Kết hợp city tour Sài Gòn và hành trình miệt vườn để đổi gió chỉ trong 2 ngày.", ImageUrl = "/img/tours/saigon.jpg" }
        };

    public static readonly IReadOnlyList<GuidePostItem> GuidePosts =
        new List<GuidePostItem>
        {
            new() { Slug = "phu-quoc-thoi-diem-dep", Title = "Thời điểm đẹp nhất để đi Phú Quốc", Summary = "Gợi ý mùa nắng đẹp, lịch trình 3N2Đ và các điểm check-in phù hợp từng nhóm khách.", DateLabel = "06/05/2026", ImageUrl = "/img/blog/blog1.jpg" },
            new() { Slug = "an-gi-o-can-tho", Title = "Ăn gì ở Cần Thơ trong 24 giờ?", Summary = "Danh sách món ngon miền Tây dễ ăn, giá hợp lý và địa chỉ được khách HappyTrip đánh giá cao.", DateLabel = "28/04/2026", ImageUrl = "/img/blog/blog2.jpg" },
            new() { Slug = "checklist-tour-3n2d", Title = "Checklist hành lý cho tour 3 ngày 2 đêm", Summary = "Chuẩn bị gọn nhẹ nhưng đầy đủ: trang phục, giấy tờ, phụ kiện và mẹo chống quên đồ.", DateLabel = "16/04/2026", ImageUrl = "/img/blog/blog3.jpg" }
        };

    public static readonly IReadOnlyList<TestimonialItem> Testimonials =
        new List<TestimonialItem>
        {
            new() { Name = "Minh Anh", Title = "Tour gia đình Phú Quốc", Quote = "Lịch trình rõ ràng, xe đón đúng giờ, khách sạn sạch đẹp. Cả nhà đi rất thoải mái.", AvatarUrl = "/img/icons/user-1.png", Rating = 5 },
            new() { Name = "Quốc Bảo", Title = "Tour nhóm Vũng Tàu", Quote = "Tư vấn nhanh, báo giá minh bạch, HDV nhiệt tình. Nhóm mình rất hài lòng.", AvatarUrl = "/img/icons/user-2.png", Rating = 5 },
            new() { Name = "Ngọc Trâm", Title = "Tour Cần Thơ - Chợ nổi", Quote = "Đúng kiểu trải nghiệm miền Tây mình muốn, đồ ăn ngon và điểm tham quan hợp lý.", AvatarUrl = "/img/icons/user-3.png", Rating = 5 }
        };

    public static readonly IReadOnlyList<TeamMemberItem> TeamMembers =
        new List<TeamMemberItem>
        {
            new() { Name = "Nguyễn Thảo My", Role = "Điều phối tour", Bio = "Chịu trách nhiệm tối ưu lịch trình, phối hợp vận hành và đảm bảo trải nghiệm trọn vẹn cho khách.", ImageUrl = "/img/icons/user.png" },
            new() { Name = "Trần Quốc Đạt", Role = "Quản lý vận hành", Bio = "Theo sát từng khâu đặt dịch vụ, xe đưa đón và hỗ trợ xử lý nhanh trong suốt hành trình.", ImageUrl = "/img/icons/user-1.png" },
            new() { Name = "Lê Hoàng Anh", Role = "Chuyên viên điểm đến", Bio = "Tư vấn tuyến tour phù hợp ngân sách, sở thích và mùa du lịch đẹp nhất của từng khu vực.", ImageUrl = "/img/icons/user-2.png" },
            new() { Name = "Phạm Ngọc Trâm", Role = "Hướng dẫn viên trưởng", Bio = "Đồng hành cùng đoàn với phong cách thân thiện, nhiều kiến thức địa phương và xử lý tình huống tốt.", ImageUrl = "/img/icons/user-3.png" }
        };

    public static readonly IReadOnlyList<StatItem> AboutStats =
        new List<StatItem>
        {
            new() { Value = "10K+", Label = "Khách đã phục vụ" },
            new() { Value = "50+", Label = "Điểm đến nội địa" },
            new() { Value = "120+", Label = "Lịch trình đã thiết kế" },
            new() { Value = "4.9", Label = "Điểm đánh giá trung bình" }
        };

    public static IReadOnlyList<DestinationItem> GetDestinations(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter) || filter.Equals(AllDestinationsFilter, StringComparison.OrdinalIgnoreCase))
        {
            return Destinations;
        }

        return Destinations
            .Where(x => x.Category.Equals(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public static IReadOnlyList<TourPackageItem> GetTours(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter) || filter.Equals(AllToursFilter, StringComparison.OrdinalIgnoreCase))
        {
            return Tours;
        }

        return Tours
            .Where(x => x.Category.Equals(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public static DestinationItem? GetDestinationBySlug(string slug)
    {
        return Destinations.FirstOrDefault(
            x => x.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
    }

    public static TourPackageItem? GetTourBySlug(string slug)
    {
        return Tours.FirstOrDefault(
            x => x.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<DestinationItem> GetRelatedDestinations(string currentSlug, int take = 3)
    {
        return Destinations
            .Where(x => !x.Slug.Equals(currentSlug, StringComparison.OrdinalIgnoreCase))
            .Take(take)
            .ToList();
    }

    public static IReadOnlyList<TourPackageItem> GetRelatedTours(string currentSlug, int take = 3)
    {
        return Tours
            .Where(x => !x.Slug.Equals(currentSlug, StringComparison.OrdinalIgnoreCase))
            .Take(take)
            .ToList();
    }
}
