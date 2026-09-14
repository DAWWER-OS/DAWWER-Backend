using System.ComponentModel.DataAnnotations;
using DawwerOS.DAL.Entities.Enums;

namespace DawwerOS.Business.DTOs.Auth;

public class VerifyCodeRequestDto
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "A valid email address is required.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Verification code is required.")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Verification code must be exactly 6 digits.")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Verification code must consist of digits only.")]
    public string Code { get; set; } = string.Empty;

    public VerificationCodeType Type { get; set; } = VerificationCodeType.EmailVerification;
}
