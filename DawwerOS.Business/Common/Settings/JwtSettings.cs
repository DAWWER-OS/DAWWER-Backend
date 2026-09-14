namespace DawwerOS.Business.Common.Settings;

public class JwtSettings
{
    public const string SectionName = "JwtSettings";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public int DurationInMinutes { get; set; } = 60;
    public int RefreshTokenDurationInDays { get; set; } = 7;
}
