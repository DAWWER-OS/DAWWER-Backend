using DawwerOS.Business.Common.Settings;
using DawwerOS.Business.Services.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace DawwerOS.Business.Services.Implementations;

public class EmailService : IEmailService
{
    private readonly EmailSettings _emailSettings;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        IOptions<EmailSettings> emailOptions,
        IHostEnvironment environment,
        ILogger<EmailService> logger)
    {
        _emailSettings = emailOptions.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task<bool> SendEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        if (!_emailSettings.EnableEmailSending)
        {
            _logger.LogInformation(
                "Email sending is disabled via configuration. Message to {ToEmail} was suppressed.",
                toEmail);
            return true;
        }

        if (string.IsNullOrWhiteSpace(toEmail))
        {
            _logger.LogWarning("Cannot send email: recipient address is empty.");
            return false;
        }

        // If SMTP configuration is missing, log actionable diagnosis
        if (string.IsNullOrWhiteSpace(_emailSettings.SmtpHost) ||
            string.IsNullOrWhiteSpace(_emailSettings.SmtpUsername))
        {
            _logger.LogWarning(
                "[SMTP_UNCONFIGURED] SMTP host or username is not configured. Email to {ToEmail} ('{Subject}') was not sent. Configure EmailSettings in appsettings.json or environment variables.",
                toEmail, subject);

            // In development, do not fail execution so local testing remains fluid
            return _environment.IsDevelopment();
        }

        try
        {
            var message = new MimeMessage();
            var senderEmail = !string.IsNullOrWhiteSpace(_emailSettings.SenderEmail)
                ? _emailSettings.SenderEmail
                : _emailSettings.SmtpUsername;

            message.From.Add(new MailboxAddress(_emailSettings.SenderName, senderEmail));
            message.To.Add(new MailboxAddress("", toEmail.Trim()));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = htmlBody
            };

            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            client.Timeout = Math.Max(5, _emailSettings.TimeoutSeconds) * 1000;

            // Determine SSL/TLS mode based on port and configuration
            var socketOptions = SecureSocketOptions.Auto;
            if (!_emailSettings.EnableSsl)
            {
                socketOptions = SecureSocketOptions.None;
            }
            else if (_emailSettings.SmtpPort == 465)
            {
                socketOptions = SecureSocketOptions.SslOnConnect;
            }
            else if (_emailSettings.SmtpPort == 587)
            {
                socketOptions = SecureSocketOptions.StartTls;
            }

            await client.ConnectAsync(_emailSettings.SmtpHost, _emailSettings.SmtpPort, socketOptions, cancellationToken);

            if (!string.IsNullOrWhiteSpace(_emailSettings.SmtpUsername) &&
                !string.IsNullOrWhiteSpace(_emailSettings.SmtpPassword))
            {
                await client.AuthenticateAsync(_emailSettings.SmtpUsername, _emailSettings.SmtpPassword, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation(
                "Email successfully sent to {ToEmail} with subject '{Subject}'",
                toEmail, subject);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send email to {ToEmail} with subject '{Subject}'. Error: {ErrorMessage}",
                toEmail, subject, ex.Message);

            return false;
        }
    }

    public async Task<bool> SendVerificationCodeEmailAsync(
        string toEmail,
        string recipientName,
        string verificationCode,
        int expiryMinutes = 15,
        CancellationToken cancellationToken = default)
    {
        var subject = $"{verificationCode} هو رمز تأكيد حسابك في منصة دوّر";
        var safeName = string.IsNullOrWhiteSpace(recipientName) ? "عزيزنا المشترك" : recipientName.Trim();
        var htmlBody = GenerateOtpEmailTemplate(
            title: "تأكيد وتفعيل الحساب",
            badgeText: "رمز التحقق OTP",
            greeting: $"مرحباً {safeName}،",
            introText: "شكراً لتسجيلك في منصة دوّر. لإكمال عملية إنشاء الحساب والتحقق من بريدك الإلكتروني، يرجى استخدام رمز التحقق التالي:",
            otpCode: verificationCode,
            expiryMinutes: expiryMinutes,
            securityNotice: "تنبيه أمني: رمز التحقق هذا مخصص لك فقط وصالح للاستخدام لمرة واحدة. لن يطلب منك فريق دوّر هذا الرمز مطلقاً.",
            callToActionText: "تأكيد الحساب في المنصة");

        return await SendEmailAsync(toEmail, subject, htmlBody, cancellationToken);
    }

    public async Task<bool> SendPasswordResetEmailAsync(
        string toEmail,
        string recipientName,
        string resetCode,
        int expiryMinutes = 15,
        CancellationToken cancellationToken = default)
    {
        var subject = $"{resetCode} هو رمز استعادة كلمة المرور في منصة دوّر";
        var safeName = string.IsNullOrWhiteSpace(recipientName) ? "عزيزنا المشترك" : recipientName.Trim();
        var htmlBody = GenerateOtpEmailTemplate(
            title: "استعادة كلمة المرور",
            badgeText: "إعادة تعيين كلمة المرور",
            greeting: $"مرحباً {safeName}،",
            introText: "لقد تلقينا طلباً لإعادة تعيين كلمة المرور الخاصة بحسابك في منصة دوّر. استخدم الرمز التالي لتعيين كلمة مرور جديدة:",
            otpCode: resetCode,
            expiryMinutes: expiryMinutes,
            securityNotice: "إذا لم تكن أنت من طلب إعادة تعيين كلمة المرور، يرجى تجاهل هذا البريد والتأكد من أمان حسابك فوراً.",
            callToActionText: "إعادة تعيين كلمة المرور");

        return await SendEmailAsync(toEmail, subject, htmlBody, cancellationToken);
    }

    private static string GenerateOtpEmailTemplate(
        string title,
        string badgeText,
        string greeting,
        string introText,
        string otpCode,
        int expiryMinutes,
        string securityNotice,
        string callToActionText)
    {
        return $@"<!DOCTYPE html>
<html lang=""ar"" dir=""rtl"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <title>{title} - دوّر</title>
  <style>
    body {{
      margin: 0;
      padding: 0;
      background-color: #f8f6f0;
      font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
      color: #334155;
      direction: rtl;
      text-align: right;
    }}
    .email-container {{
      max-width: 580px;
      margin: 32px auto;
      background-color: #ffffff;
      border-radius: 20px;
      overflow: hidden;
      box-shadow: 0 4px 20px rgba(0, 0, 0, 0.05);
      border: 1px solid #e2e8f0;
    }}
    .email-header {{
      background: linear-gradient(135deg, #143e27 0%, #1c5335 100%);
      padding: 36px 32px;
      text-align: center;
      color: #ffffff;
    }}
    .brand-title {{
      font-size: 26px;
      font-weight: 800;
      letter-spacing: -0.5px;
      margin: 0 0 6px 0;
      color: #ffffff;
    }}
    .brand-sub {{
      font-size: 13px;
      color: #a7f3d0;
      margin: 0;
      font-weight: 500;
    }}
    .email-body {{
      padding: 36px 32px;
    }}
    .badge {{
      display: inline-block;
      background-color: #edf5f0;
      color: #1c5335;
      font-size: 12px;
      font-weight: 700;
      padding: 6px 14px;
      border-radius: 9999px;
      border: 1px solid rgba(28, 83, 53, 0.2);
      margin-bottom: 16px;
    }}
    .greeting {{
      font-size: 18px;
      font-weight: 700;
      color: #1e293b;
      margin: 0 0 12px 0;
    }}
    .intro {{
      font-size: 14px;
      line-height: 1.7;
      color: #475569;
      margin: 0 0 28px 0;
    }}
    .otp-wrapper {{
      background-color: #edf5f0;
      border: 2px dashed #1c5335;
      border-radius: 16px;
      padding: 24px;
      text-align: center;
      margin: 0 0 24px 0;
    }}
    .otp-label {{
      font-size: 12px;
      font-weight: 600;
      color: #1c5335;
      margin-bottom: 8px;
      text-transform: uppercase;
      letter-spacing: 0.5px;
    }}
    .otp-code {{
      font-family: 'Courier New', Courier, monospace;
      font-size: 38px;
      font-weight: 800;
      letter-spacing: 10px;
      color: #1c5335;
      margin: 0;
      direction: ltr;
      display: inline-block;
    }}
    .expiry-note {{
      font-size: 12px;
      color: #64748b;
      margin-top: 10px;
      font-weight: 500;
    }}
    .expiry-badge {{
      color: #059669;
      font-weight: 700;
    }}
    .security-card {{
      background-color: #f8fafc;
      border: 1px solid #e2e8f0;
      border-right: 4px solid #f59e0b;
      border-radius: 10px;
      padding: 14px 18px;
      font-size: 12px;
      line-height: 1.6;
      color: #64748b;
      margin-bottom: 24px;
    }}
    .footer {{
      background-color: #f8fafc;
      border-top: 1px solid #e2e8f0;
      padding: 24px 32px;
      text-align: center;
      font-size: 12px;
      color: #94a3b8;
      line-height: 1.6;
    }}
  </style>
</head>
<body>
  <div class=""email-container"">
    <div class=""email-header"">
      <h1 class=""brand-title"">منصة دوّر</h1>
      <p class=""brand-sub"">النظام الذكي لإدارة المنتجات والمتاجر</p>
    </div>

    <div class=""email-body"">
      <div class=""badge"">{badgeText}</div>
      <h2 class=""greeting"">{greeting}</h2>
      <p class=""intro"">{introText}</p>

      <div class=""otp-wrapper"">
        <div class=""otp-label"">رمز التحقق الخاص بك</div>
        <div class=""otp-code"">{otpCode}</div>
        <div class=""expiry-note"">
          صلاحية هذا الرمز <span class=""expiry-badge"">{expiryMinutes} دقيقة</span> فقط
        </div>
      </div>

      <div class=""security-card"">
        {securityNotice}
      </div>
    </div>

    <div class=""footer"">
      <p style=""margin: 0 0 6px 0;"">تم إرسال هذا البريد الإلكتروني تلقائياً من نظام منصة دوّر.</p>
      <p style=""margin: 0;"">© 2026 منصة دوّر | جميع الحقوق محفوظة.</p>
    </div>
  </div>
</body>
</html>";
    }
}
