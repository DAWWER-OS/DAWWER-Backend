using System.ComponentModel.DataAnnotations;

namespace DawwerOS.Business.DTOs.Auth;

public class ForgotPasswordRequestDto
{
    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Invalid email address format.")]
    [StringLength(150, ErrorMessage = "Email must not exceed 150 characters.")]
    public string Email { get; set; } = string.Empty;
}
