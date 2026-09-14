using DawwerOS.DAL.Entities.Enums;

namespace DawwerOS.DAL.Entities;

public class Store : BaseEntity
{
    public Guid OwnerId { get; set; }

    public User Owner { get; set; } = null!;

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

    public StoreVerificationStatus VerificationStatus { get; set; } = StoreVerificationStatus.Draft;

    public StoreStatus Status { get; set; } = StoreStatus.Inactive;

    public string? RejectionReason { get; set; }

    public string? InformationRequestMessage { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public Guid? ReviewedById { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public DateTime? SuspendedAt { get; set; }

    public string? SuspensionReason { get; set; }

    public ICollection<StoreDocument> Documents { get; set; } = new List<StoreDocument>();

    public ICollection<StoreStaff> StaffMembers { get; set; } = new List<StoreStaff>();

    public ICollection<StoreRole> CustomRoles { get; set; } = new List<StoreRole>();
}
