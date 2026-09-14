namespace DawwerOS.Business.DTOs.Staff;

public class SelectStoreResponseDto
{
    public string AccessToken { get; set; } = string.Empty;

    public string TokenType { get; set; } = "Bearer";

    public int ExpiresInMinutes { get; set; }

    public Guid StoreId { get; set; }

    public string StoreName { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;

    public List<string> EffectivePermissions { get; set; } = new();
}
