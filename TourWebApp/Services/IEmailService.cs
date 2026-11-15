namespace TourWebApp.Services;

public interface IEmailService
{
    Task SendPaymentSuccessEmailAsync(
        string toEmail,
        string customerName,
        string bookingCode,
        string tourName,
        DateTime? departureDate,
        decimal paidAmount
    );

    Task SendContactReplyEmailAsync(
        string toEmail,
        string customerName,
        string replyContent,
        string? customerPhone = null,
        string? destination = null
    );

    string BuildContactReplyHtml(
        string customerName,
        string replyContent,
        string headerImageUrl,
        string footerImageUrl,
        string postcardImageUrl,
        string stampImageUrl,
        string? customerEmail = null,
        string? customerPhone = null,
        string? destination = null
    );
}