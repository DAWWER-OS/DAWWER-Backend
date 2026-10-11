namespace DawwerOS.Business.Common.Settings;

public class EmailSettings
{
    public const string SectionName = "EmailSettings";

    /// <summary>
    /// The SMTP host address (e.g. smtp.gmail.com, smtp-relay.brevo.com, smtp.sendgrid.net).
    /// </summary>
    public string SmtpHost { get; set; } = string.Empty;

    /// <summary>
    /// The SMTP server port (typically 587 for STARTTLS, 465 for SSL, or 25).
    /// </summary>
    public int SmtpPort { get; set; } = 587;

    /// <summary>
    /// Whether SSL/TLS is enabled for connections.
    /// </summary>
    public bool EnableSsl { get; set; } = true;

    /// <summary>
    /// The sender's email address shown to recipients.
    /// </summary>
    public string SenderEmail { get; set; } = string.Empty;

    /// <summary>
    /// The sender's display name shown to recipients.
    /// </summary>
    public string SenderName { get; set; } = "منصة دوّر - Dawwer";

    /// <summary>
    /// The username for SMTP authentication.
    /// </summary>
    public string SmtpUsername { get; set; } = string.Empty;

    /// <summary>
    /// The password or App Password for SMTP authentication.
    /// </summary>
    public string SmtpPassword { get; set; } = string.Empty;

    /// <summary>
    /// Master toggle for email dispatching.
    /// </summary>
    public bool EnableEmailSending { get; set; } = true;

    /// <summary>
    /// Connection and transmission timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 15;
}
