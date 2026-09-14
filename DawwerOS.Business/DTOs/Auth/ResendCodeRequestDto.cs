using System.ComponentModel.DataAnnotations;
using DawwerOS.DAL.Entities.Enums;

namespace DawwerOS.Business.DTOs.Auth;

public class ResendCodeRequestDto
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "A valid email address is required.")]
    public string Email { get; set; } = string.Empty;

    public VerificationCodeType Type { get; set; } = VerificationCodeType.EmailVerification;
}
