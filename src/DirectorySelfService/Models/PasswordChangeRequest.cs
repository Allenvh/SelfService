using System.ComponentModel.DataAnnotations;

namespace DirectorySelfService.Models;

public sealed class PasswordChangeRequest
{
    [Required(ErrorMessage = "Enter your username or UPN.")]
    [StringLength(256, MinimumLength = 2)]
    [Display(Name = "Username or UPN")]
    public string Username { get; init; } = string.Empty;

    [Required(ErrorMessage = "Enter your current password.")]
    [DataType(DataType.Password)]
    [Display(Name = "Current password")]
    public string CurrentPassword { get; init; } = string.Empty;

    [Required(ErrorMessage = "Enter a new password.")]
    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string NewPassword { get; init; } = string.Empty;

    [Required(ErrorMessage = "Confirm your new password.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "The new passwords do not match.")]
    [Display(Name = "Confirm new password")]
    public string ConfirmNewPassword { get; init; } = string.Empty;
}
