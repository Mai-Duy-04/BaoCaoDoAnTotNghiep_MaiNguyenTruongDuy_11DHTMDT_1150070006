using Microsoft.EntityFrameworkCore;
using TourWebApp.Data.Models;
using TourWebApp.Models;
using TourWebApp.Services;
using VNPAY.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ISeatInventoryService, SeatInventoryService>();
builder.Services.AddScoped<IMatchingScoreService, MatchingScoreService>();


var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:DefaultConnection");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString)
);


builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var vnpayConfig = builder.Configuration.GetSection("VNPAY");
builder.Services.AddVnpayClient(config =>
{
    config.TmnCode = vnpayConfig["TmnCode"] ?? string.Empty;
    config.HashSecret = vnpayConfig["HashSecret"] ?? string.Empty;
    config.CallbackUrl = vnpayConfig["CallbackUrl"] ?? string.Empty;
    config.BaseUrl = vnpayConfig["BaseUrl"] ?? "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
    config.Version = vnpayConfig["Version"] ?? "2.1.0";
    config.OrderType = vnpayConfig["OrderType"] ?? "other";
});

builder.Services.AddHostedService<CodOrderExpiryService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();


app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "travel-home",
    pattern: "",
    defaults: new { controller = "Travel", action = "Index" });

app.MapControllerRoute(
    name: "travel-destinations",
    pattern: "destinations",
    defaults: new { controller = "Travel", action = "Destinations" });

app.MapControllerRoute(
    name: "travel-destination-detail",
    pattern: "destinations/{slug}",
    defaults: new { controller = "Travel", action = "DestinationDetail" });

app.MapControllerRoute(
    name: "travel-tours",
    pattern: "tours",
    defaults: new { controller = "Tour", action = "TatCa" });

app.MapControllerRoute(
    name: "travel-tour-detail",
    pattern: "tours/{slug}",
    defaults: new { controller = "Travel", action = "TourDetail" });

app.MapControllerRoute(
    name: "travel-about",
    pattern: "about",
    defaults: new { controller = "Travel", action = "About" });

app.MapControllerRoute(
    name: "travel-contact",
    pattern: "contact",
    defaults: new { controller = "Travel", action = "Contact" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Travel}/{action=Index}/{id?}"
);

app.Run();
