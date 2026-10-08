using System.ComponentModel.DataAnnotations;

namespace DawwerOS.Business.DTOs.Store;

public class CreateStoreApplicationRequestDto
{
    private string _name = string.Empty;
    private string? _description;
    private string? _commercialRegistrationNumber;
    private string? _taxNumber;
    private string? _phoneNumber;
    private string? _email;
    private string? _address;
    private string? _city;
    private string? _logoUrl;
    private string? _coverImageUrl;

    [Required(ErrorMessage = "Store name is required.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Store name must be between 2 and 150 characters.")]
    public string Name
    {
        get => _name;
        set => _name = value?.Trim() ?? string.Empty;
    }

    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
    public string? Description
    {
        get => _description;
        set => _description = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    [StringLength(100, ErrorMessage = "Commercial registration number cannot exceed 100 characters.")]
    public string? CommercialRegistrationNumber
    {
        get => _commercialRegistrationNumber;
        set => _commercialRegistrationNumber = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    [StringLength(100, ErrorMessage = "Tax number cannot exceed 100 characters.")]
    public string? TaxNumber
    {
        get => _taxNumber;
        set => _taxNumber = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    [Phone(ErrorMessage = "Invalid phone number format.")]
    [StringLength(30, ErrorMessage = "Phone number cannot exceed 30 characters.")]
    public string? PhoneNumber
    {
        get => _phoneNumber;
        set => _phoneNumber = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    [EmailAddress(ErrorMessage = "Invalid email format.")]
    [StringLength(256, ErrorMessage = "Email cannot exceed 256 characters.")]
    public string? Email
    {
        get => _email;
        set => _email = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    [StringLength(300, ErrorMessage = "Address cannot exceed 300 characters.")]
    public string? Address
    {
        get => _address;
        set => _address = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    [StringLength(100, ErrorMessage = "City cannot exceed 100 characters.")]
    public string? City
    {
        get => _city;
        set => _city = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    [Range(-90.0, 90.0, ErrorMessage = "Latitude must be between -90 and 90.")]
    public decimal? Latitude { get; set; }

    [Range(-180.0, 180.0, ErrorMessage = "Longitude must be between -180 and 180.")]
    public decimal? Longitude { get; set; }

    [Url(ErrorMessage = "Invalid logo URL.")]
    [StringLength(500, ErrorMessage = "Logo URL cannot exceed 500 characters.")]
    public string? LogoUrl
    {
        get => _logoUrl;
        set => _logoUrl = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    [Url(ErrorMessage = "Invalid cover image URL.")]
    [StringLength(500, ErrorMessage = "Cover image URL cannot exceed 500 characters.")]
    public string? CoverImageUrl
    {
        get => _coverImageUrl;
        set => _coverImageUrl = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
