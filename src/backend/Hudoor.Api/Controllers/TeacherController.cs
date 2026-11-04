using Hudoor.Api.Common;
using Hudoor.Api.Data;
using Hudoor.Api.Data.Entities;
using Hudoor.Api.DTOs;
using Hudoor.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hudoor.Api.Controllers;

[ApiController]
[Route("api/teacher")]
[Authorize(Roles = Roles.Teacher)]
public class TeacherController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IPasswordService _passwordService;
    private readonly ITokenService _tokenService;
    private readonly ITenantContext _tenantContext;

    public TeacherController(
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

    [HttpPost("students/{id:guid}/reset-password")]
    public async Task<IActionResult> ResetStudentPassword(Guid id)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        // Enforce strict multi-tenant isolation:
        // Query student strictly by id AND TeacherId. A teacher cannot see or reset another teacher's student.
        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.Id == id && s.TeacherId == teacherId.Value, HttpContext.RequestAborted);

        if (student == null)
        {
            return NotFound(new { message = "Student not found." });
        }

        // Generate easy-to-read, CSPRNG temporary password (Rule BR-PWD-047)
        var tempPassword = _passwordService.GenerateTemporaryPassword();
        var hashedTempPassword = _passwordService.HashPassword(tempPassword);

        // Atomic update: increment TokenVersion to invalidate prior student sessions (Rule BR-PWD-049)
        student.PasswordHash = hashedTempPassword;
        student.MustChangePassword = true;
        student.TemporaryPasswordExpiresAtUtc = DateTime.UtcNow.AddDays(7);
        student.TokenVersion++;

        // Audit Logging (Rule BR-PWD-050)
        var auditLog = new PasswordResetAuditLog
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            TriggeredByTeacherId = teacherId.Value,
            TimestampUtc = DateTime.UtcNow,
            ClientIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        };

        _context.PasswordResetAuditLogs.Add(auditLog);
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(new ResetStudentPasswordResponse
        {
            StudentId = student.Id,
            StudentCode = student.StudentCode,
            TemporaryPassword = tempPassword,
            ExpiresAtUtc = student.TemporaryPasswordExpiresAtUtc.Value
        });
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var teacher = await _context.Teachers
            .FirstOrDefaultAsync(t => t.Id == teacherId.Value, HttpContext.RequestAborted);

        if (teacher == null)
        {
            return NotFound(new { message = "Teacher not found." });
        }

        if (!_passwordService.VerifyPassword(teacher.PasswordHash, request.CurrentPassword))
        {
            return BadRequest(new { message = "Current password is incorrect." });
        }

        var (isValid, errorMessage) = _passwordService.ValidateStaffPassword(request.NewPassword);
        if (!isValid)
        {
            return BadRequest(new { message = errorMessage });
        }

        teacher.PasswordHash = _passwordService.HashPassword(request.NewPassword);
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        var (token, expiresAtUtc) = _tokenService.GenerateTokenForTeacher(teacher);

        return Ok(new
        {
            message = "Password changed successfully.",
            token,
            expiresAtUtc
        });
    }
}
