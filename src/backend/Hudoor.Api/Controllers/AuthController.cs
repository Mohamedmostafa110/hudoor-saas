using System.Security.Claims;
using Hudoor.Api.Common;
using Hudoor.Api.Data;
using Hudoor.Api.DTOs;
using Hudoor.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hudoor.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IPasswordService _passwordService;
    private readonly ITokenService _tokenService;
    private readonly IRateLimiterService _rateLimiter;
    private readonly ITenantContext _tenantContext;

    public AuthController(
        ApplicationDbContext context,
        IPasswordService passwordService,
        ITokenService tokenService,
        IRateLimiterService rateLimiter,
        ITenantContext tenantContext)
    {
        _context = context;
        _passwordService = passwordService;
        _tokenService = tokenService;
        _rateLimiter = rateLimiter;
        _tenantContext = tenantContext;
    }

    [HttpPost("platform-admin/login")]
    [HttpPost("admin/login")]
    public async Task<IActionResult> PlatformAdminLogin([FromBody] PlatformAdminLoginRequest request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (_rateLimiter.IsIpRateLimited(ip))
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { message = "Too many requests. Please try again later." });
        }

        var accountKey = $"admin:{request.Username.Trim().ToLowerInvariant()}";
        if (_rateLimiter.IsAccountLockedOut(accountKey, out var remaining))
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                message = $"Too many failed login attempts. Account temporarily locked for {Math.Ceiling(remaining.TotalMinutes)} minute(s)."
            });
        }

        await _rateLimiter.ApplyProgressiveDelayAsync(accountKey, HttpContext.RequestAborted);

        var admin = await _context.PlatformAdmins
            .FirstOrDefaultAsync(a => a.Username == request.Username.Trim(), HttpContext.RequestAborted);

        if (admin == null || !_passwordService.VerifyPassword(admin.PasswordHash, request.Password))
        {
            _rateLimiter.RecordFailedAttempt(accountKey, ip);
            return Unauthorized(new { message = "Invalid credentials." });
        }

        if (!admin.IsActive)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Account is deactivated." });
        }

        _rateLimiter.ResetAccount(accountKey);

        var (token, expiresAtUtc) = _tokenService.GenerateTokenForPlatformAdmin(admin);

        return Ok(new AuthResponse
        {
            Token = token,
            ExpiresAtUtc = expiresAtUtc,
            Role = Roles.PlatformAdmin,
            UsernameOrCode = admin.Username,
            FullName = admin.FullName
        });
    }

    [HttpPost("teacher/login")]
    public async Task<IActionResult> TeacherLogin([FromBody] TeacherLoginRequest request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (_rateLimiter.IsIpRateLimited(ip))
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { message = "Too many requests. Please try again later." });
        }

        var accountKey = $"teacher:{request.Username.Trim().ToLowerInvariant()}";
        if (_rateLimiter.IsAccountLockedOut(accountKey, out var remaining))
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                message = $"Too many failed login attempts. Account temporarily locked for {Math.Ceiling(remaining.TotalMinutes)} minute(s)."
            });
        }

        await _rateLimiter.ApplyProgressiveDelayAsync(accountKey, HttpContext.RequestAborted);

        var teacher = await _context.Teachers
            .FirstOrDefaultAsync(t => t.Username == request.Username.Trim(), HttpContext.RequestAborted);

        if (teacher == null || !_passwordService.VerifyPassword(teacher.PasswordHash, request.Password))
        {
            _rateLimiter.RecordFailedAttempt(accountKey, ip);
            return Unauthorized(new { message = "Invalid credentials." });
        }

        if (!teacher.IsActive)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Account is deactivated." });
        }

        _rateLimiter.ResetAccount(accountKey);

        var (token, expiresAtUtc) = _tokenService.GenerateTokenForTeacher(teacher);

        return Ok(new AuthResponse
        {
            Token = token,
            ExpiresAtUtc = expiresAtUtc,
            Role = Roles.Teacher,
            UsernameOrCode = teacher.Username,
            FullName = teacher.FullName,
            TeacherId = teacher.Id
        });
    }

    [HttpPost("student/login")]
    public async Task<IActionResult> StudentLogin([FromBody] StudentLoginRequest request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (_rateLimiter.IsIpRateLimited(ip))
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { message = "Too many requests. Please try again later." });
        }

        var accountKey = $"student:{request.StudentCode.Trim().ToUpperInvariant()}";
        if (_rateLimiter.IsAccountLockedOut(accountKey, out var remaining))
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                message = $"Too many failed login attempts. Account temporarily locked for {Math.Ceiling(remaining.TotalMinutes)} minute(s)."
            });
        }

        await _rateLimiter.ApplyProgressiveDelayAsync(accountKey, HttpContext.RequestAborted);

        var student = await _context.Students
            .Include(s => s.Teacher)
            .FirstOrDefaultAsync(s => s.StudentCode == request.StudentCode.Trim(), HttpContext.RequestAborted);

        if (student == null || !_passwordService.VerifyPassword(student.PasswordHash, request.Password))
        {
            _rateLimiter.RecordFailedAttempt(accountKey, ip);
            return Unauthorized(new { message = "Invalid credentials." });
        }

        if (!student.IsActive)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Student account is deactivated." });
        }

        if (!student.Teacher.IsActive)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Teacher account is deactivated." });
        }

        // Temporary password expiry check (Rule BR-PWD-049)
        if (student.MustChangePassword && student.TemporaryPasswordExpiresAtUtc.HasValue && student.TemporaryPasswordExpiresAtUtc.Value < DateTime.UtcNow)
        {
            return Unauthorized(new { message = "Temporary password has expired. Please ask your teacher to reset your password." });
        }

        _rateLimiter.ResetAccount(accountKey);

        var (token, expiresAtUtc) = _tokenService.GenerateTokenForStudent(student);

        return Ok(new AuthResponse
        {
            Token = token,
            ExpiresAtUtc = expiresAtUtc,
            Role = Roles.Student,
            UsernameOrCode = student.StudentCode,
            FullName = student.FullName,
            MustChangePassword = student.MustChangePassword,
            TeacherId = student.TeacherId,
            StudentId = student.Id
        });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var role = _tenantContext.Role;
        var userId = _tenantContext.CurrentUserId;

        if (!userId.HasValue || string.IsNullOrEmpty(role))
        {
            return Unauthorized(new { message = "User identity could not be resolved from token." });
        }

        if (role == Roles.PlatformAdmin)
        {
            var admin = await _context.PlatformAdmins
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == userId.Value, HttpContext.RequestAborted);

            if (admin == null) return NotFound(new { message = "Admin not found." });

            return Ok(new CurrentUserResponse
            {
                Id = admin.Id,
                Role = Roles.PlatformAdmin,
                UsernameOrCode = admin.Username,
                FullName = admin.FullName,
                Email = admin.Email
            });
        }

        if (role == Roles.Teacher)
        {
            var teacher = await _context.Teachers
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == userId.Value, HttpContext.RequestAborted);

            if (teacher == null) return NotFound(new { message = "Teacher not found." });

            return Ok(new CurrentUserResponse
            {
                Id = teacher.Id,
                Role = Roles.Teacher,
                UsernameOrCode = teacher.Username,
                FullName = teacher.FullName,
                Email = teacher.Email,
                TeacherId = teacher.Id
            });
        }

        if (role == Roles.Student)
        {
            var student = await _context.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == userId.Value, HttpContext.RequestAborted);

            if (student == null) return NotFound(new { message = "Student not found." });

            return Ok(new CurrentUserResponse
            {
                Id = student.Id,
                Role = Roles.Student,
                UsernameOrCode = student.StudentCode,
                FullName = student.FullName,
                TeacherId = student.TeacherId,
                MustChangePassword = student.MustChangePassword
            });
        }

        return Forbid();
    }
}
