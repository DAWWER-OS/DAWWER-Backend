namespace DawwerOS.Business.Services.Interfaces;

public interface IEmailService
{
    /// <summary>
    /// Sends a general HTML email message to a recipient.
    /// </summary>
    Task<bool> SendEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispatches an account verification OTP code email with branded template.
    /// </summary>
    Task<bool> SendVerificationCodeEmailAsync(
        string toEmail,
        string recipientName,
        string verificationCode,
        int expiryMinutes = 15,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispatches a password reset OTP code email with branded template.
    /// </summary>
    Task<bool> SendPasswordResetEmailAsync(
        string toEmail,
        string recipientName,
        string resetCode,
        int expiryMinutes = 15,
        CancellationToken cancellationToken = default);
}
