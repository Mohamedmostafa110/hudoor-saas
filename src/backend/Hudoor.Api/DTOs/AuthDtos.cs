using System.ComponentModel.DataAnnotations;

namespace Hudoor.Api.DTOs;

public class PlatformAdminLoginRequest
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class TeacherLoginRequest
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class StudentLoginRequest
{
    [Required]
    public string StudentCode { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public string Role { get; set; } = string.Empty;
    public string UsernameOrCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool? MustChangePassword { get; set; }
    public Guid? TeacherId { get; set; }
    public Guid? StudentId { get; set; }
}

public class CurrentUserResponse
{
    public Guid Id { get; set; }
    public string Role { get; set; } = string.Empty;
    public string UsernameOrCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public Guid? TeacherId { get; set; }
    public bool? MustChangePassword { get; set; }
}

public class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    public string NewPassword { get; set; } = string.Empty;
}

public class ResetStudentPasswordResponse
{
    public Guid StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string TemporaryPassword { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
}
