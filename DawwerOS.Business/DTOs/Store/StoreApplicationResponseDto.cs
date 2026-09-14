namespace DawwerOS.Business.DTOs.Store;

public class StoreApplicationResponseDto
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public string? OwnerName { get; set; }

    public string? OwnerEmail { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? CommercialRegistrationNumber { get; set; }

    public string? TaxNumber { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Email { get; set; }

    public string? Address { get; set; }

    public string? City { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public string? LogoUrl { get; set; }

    public string? CoverImageUrl { get; set; }

    public string VerificationStatus { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? InformationRequestMessage { get; set; }

    public string? RejectionReason { get; set; }

    public string? SuspensionReason { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public DateTime? SuspendedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public List<StoreDocumentResponseDto> Documents { get; set; } = new();
}
