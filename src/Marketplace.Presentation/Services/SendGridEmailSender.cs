using System.Net;
using Marketplace.Data;
using Microsoft.AspNetCore.Identity;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace Marketplace.Presentation.Services;

public class SendGridEmailSender : IEmailSender<ApplicationUser>
{
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly string _fromEmail;
    private readonly string _fromName;

    public SendGridEmailSender(
        IConfiguration configuration,
        string fromEmail = "noreply@marketplace.local",
        string fromName = "Marketplace")
    {
        _apiKey = configuration.GetValue<string>("SendGrid:ApiKey") ?? string.Empty;
        _baseUrl = configuration.GetValue<string>("App:BaseUrl") ?? "https://localhost:5001";
        _fromEmail = fromEmail;
        _fromName = fromName;
    }

    public async Task SendEmailAsync(ApplicationUser user, string subject, string htmlMessage)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            return;
        }

        var client = new SendGridClient(_apiKey);
        var msg = new SendGridMessage
        {
            From = new EmailAddress(_fromEmail, _fromName),
            Subject = subject,
            HtmlContent = htmlMessage,
        };
        msg.AddTo(new EmailAddress(user.Email));
        await client.SendEmailAsync(msg);
    }

    public async Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationToken)
    {
        var callbackUrl = $"{_baseUrl}/Auth/ConfirmEmail?userId={user.Id}&token={Uri.EscapeDataString(confirmationToken)}";
        await SendEmailAsync(
            user,
            "Confirm your email",
            $"<p>Please confirm your email by <a href=\"{callbackUrl}\">clicking here</a>.</p>");
    }

    public async Task SendPasswordResetLinkAsync(ApplicationUser user, string resetToken, string callbackUrl)
    {
        await SendEmailAsync(
            user,
            "Reset your password",
            $"<p>Reset your password by <a href=\"{callbackUrl}\">clicking here</a>.</p>");
    }

    public async Task SendPasswordResetCodeAsync(ApplicationUser user, string resetCode, string callbackUrl)
    {
        await SendEmailAsync(
            user,
            "Reset your password",
            $"<p>Your password reset code is: {resetCode}</p>");
    }
}
