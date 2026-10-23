using System.Net;
using System.Net.Mail;
using System.Text;
using DareCare.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace DareCare.Api.Services;

public interface IAccountEmailService
{
    bool IsConfigured { get; }
    Task SendVerificationAsync(ApplicationUser user, CancellationToken ct);
    Task SendPasswordResetAsync(ApplicationUser user, CancellationToken ct);
}

public sealed class AccountEmailService(
    IConfiguration config,
    UserManager<ApplicationUser> users,
    IWebHostEnvironment environment,
    DevelopmentEmailOutbox outbox) : IAccountEmailService
{
    private bool HasSmtp => !string.IsNullOrWhiteSpace(config["Smtp:Host"]) &&
                            !string.IsNullOrWhiteSpace(config["Smtp:FromEmail"]);
    public bool IsConfigured => HasSmtp || environment.IsDevelopment();

    public async Task SendVerificationAsync(ApplicationUser user, CancellationToken ct)
    {
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        await SendAsync(user.Email!, "Verify your DareCare email",
            $"Your DareCare email verification code:\n{Encode(token)}\n\n" +
            "Enter this code in the app's verification form. If you did not register, ignore this message.", ct);
    }

    public async Task SendPasswordResetAsync(ApplicationUser user, CancellationToken ct)
    {
        var token = await users.GeneratePasswordResetTokenAsync(user);
        await SendAsync(user.Email!, "DareCare password setup / reset",
            $"Your DareCare password setup or reset code:\n{Encode(token)}\n\n" +
            "Enter this code in the app's reset-password form. If you did not request this, ignore this message.", ct);
    }

    private static string Encode(string token) =>
        WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    private async Task SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        if (!HasSmtp && environment.IsDevelopment())
        {
            outbox.Add(to, subject, body);
            return;
        }
        if (!HasSmtp) throw new InvalidOperationException("SMTP email delivery is not configured.");
        using var message = new MailMessage(config["Smtp:FromEmail"]!, to, subject, body);
        using var smtp = new SmtpClient(config["Smtp:Host"]!, config.GetValue<int>("Smtp:Port", 587))
        {
            EnableSsl = config.GetValue<bool>("Smtp:EnableSsl", true),
            UseDefaultCredentials = false
        };
        if (!string.IsNullOrEmpty(config["Smtp:Username"]))
            smtp.Credentials = new NetworkCredential(config["Smtp:Username"], config["Smtp:Password"]);
        await smtp.SendMailAsync(message, ct);
    }
}
