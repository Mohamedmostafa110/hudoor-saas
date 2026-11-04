using Hudoor.Api.Common;
using Hudoor.Api.Data;
using Hudoor.Api.DTOs;
using Hudoor.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hudoor.Api.Controllers;

[ApiController]
[Route("api/student")]
[Authorize(Roles = Roles.Student)]
public class StudentController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IPasswordService _passwordService;
    private readonly ITokenService _tokenService;
    private readonly ITenantContext _tenantContext;

    public StudentController(
        ApplicationDbContext context,
        IPasswordService passwordService,
        ITokenService tokenService,
        ITenantContext tenantContext)
    {
        _context = context;
        _passwordService = passwordService;
        _tokenService = tokenService;
        _tenantContext = tenantContext;
    }

    [HttpPost("change-password")]
    [HttpPost("me/change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var studentId = _tenantContext.CurrentUserId;
        if (!studentId.HasValue)
        {
            return Unauthorized(new { message = "Student identity could not be verified." });
        }

        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.Id == studentId.Value, HttpContext.RequestAborted);

        if (student == null)
        {
            return NotFound(new { message = "Student not found." });
        }

        if (!_passwordService.VerifyPassword(student.PasswordHash, request.CurrentPassword))
        {
            return BadRequest(new { message = "Current password is incorrect." });
        }

        var (isValid, errorMessage) = _passwordService.ValidateStudentPassword(request.NewPassword);
        if (!isValid)
        {
            return BadRequest(new { message = errorMessage });
        }

        if (string.Equals(request.CurrentPassword, request.NewPassword, StringComparison.Ordinal))
        {
            return BadRequest(new { message = "New password must be different from the current password." });
        }

        // Atomic session invalidation & password update (Rule BR-PWD-049 & BR-PWD-051)
        student.PasswordHash = _passwordService.HashPassword(request.NewPassword);
        student.MustChangePassword = false;
        student.TemporaryPasswordExpiresAtUtc = null;
        student.TokenVersion++;

        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        // Issue new JWT with incremented TokenVersion and must_change_password = false
        var (token, expiresAtUtc) = _tokenService.GenerateTokenForStudent(student);

        return Ok(new
        {
            message = "Password updated successfully.",
            token,
            expiresAtUtc
        });
    }
}
