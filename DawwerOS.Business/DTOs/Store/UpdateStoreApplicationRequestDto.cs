using System.ComponentModel.DataAnnotations;

namespace DawwerOS.Business.DTOs.Store;

public class UpdateStoreApplicationRequestDto
{
    [Required(ErrorMessage = "Store name is required.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Store name must be between 2 and 150 characters.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
    public string? Description { get; set; }

    [StringLength(100, ErrorMessage = "Commercial registration number cannot exceed 100 characters.")]
    public string? CommercialRegistrationNumber { get; set; }

    [StringLength(100, ErrorMessage = "Tax number cannot exceed 100 characters.")]
    public string? TaxNumber { get; set; }

    [Phone(ErrorMessage = "Invalid phone number format.")]
    [StringLength(30, ErrorMessage = "Phone number cannot exceed 30 characters.")]
    public string? PhoneNumber { get; set; }

    [EmailAddress(ErrorMessage = "Invalid email format.")]
    [StringLength(256, ErrorMessage = "Email cannot exceed 256 characters.")]
    public string? Email { get; set; }

    [StringLength(300, ErrorMessage = "Address cannot exceed 300 characters.")]
    public string? Address { get; set; }

    [StringLength(100, ErrorMessage = "City cannot exceed 100 characters.")]
    public string? City { get; set; }

    [Range(-90.0, 90.0, ErrorMessage = "Latitude must be between -90 and 90.")]
    public decimal? Latitude { get; set; }

    [Range(-180.0, 180.0, ErrorMessage = "Longitude must be between -180 and 180.")]
    public decimal? Longitude { get; set; }

    [Url(ErrorMessage = "Invalid logo URL.")]
    [StringLength(500, ErrorMessage = "Logo URL cannot exceed 500 characters.")]
    public string? LogoUrl { get; set; }

    [Url(ErrorMessage = "Invalid cover image URL.")]
    [StringLength(500, ErrorMessage = "Cover image URL cannot exceed 500 characters.")]
    public string? CoverImageUrl { get; set; }
}
