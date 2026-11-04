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
[Route("api/platform-admin")]
[Route("api/admin")]
[Authorize(Roles = Roles.PlatformAdmin)]
public class PlatformAdminController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IPasswordService _passwordService;

    public PlatformAdminController(ApplicationDbContext context, IPasswordService passwordService)
    {
        _context = context;
        _passwordService = passwordService;
    }

    [HttpGet("teachers")]
    public async Task<IActionResult> GetTeachers()
    {
        var teachers = await _context.Teachers
            .OrderByDescending(t => t.CreatedAtUtc)
            .Select(t => new TeacherResponseDto
            {
                Id = t.Id,
                Username = t.Username,
                FullName = t.FullName,
                Email = t.Email,
                PhoneNumber = t.PhoneNumber,
                BrandName = t.BrandName,
                IsActive = t.IsActive,
                CreatedAtUtc = t.CreatedAtUtc
            })
            .ToListAsync(HttpContext.RequestAborted);

        return Ok(teachers);
    }

    [HttpPost("teachers")]
    public async Task<IActionResult> CreateTeacher([FromBody] CreateTeacherRequest request)
    {
        var username = request.Username.Trim();
        var email = request.Email.Trim().ToLowerInvariant();

        var (isValidPassword, passwordError) = _passwordService.ValidateStaffPassword(request.Password);
        if (!isValidPassword)
        {
            return BadRequest(new { message = passwordError });
        }

        // Check username uniqueness across Teachers and Admins
        var usernameExists = await _context.Teachers.AnyAsync(t => t.Username == username, HttpContext.RequestAborted)
            || await _context.PlatformAdmins.AnyAsync(a => a.Username == username, HttpContext.RequestAborted);

        if (usernameExists)
        {
            return Conflict(new { message = "Username is already in use." });
        }

        var emailExists = await _context.Teachers.AnyAsync(t => t.Email == email, HttpContext.RequestAborted);
        if (emailExists)
        {
            return Conflict(new { message = "Email is already in use by another teacher." });
        }

        var teacher = new Teacher
        {
            Id = Guid.NewGuid(),
            Username = username,
            PasswordHash = _passwordService.HashPassword(request.Password),
            FullName = request.FullName.Trim(),
            Email = email,
            PhoneNumber = request.PhoneNumber?.Trim(),
            BrandName = request.BrandName?.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.Teachers.Add(teacher);
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        var response = new TeacherResponseDto
        {
            Id = teacher.Id,
            Username = teacher.Username,
            FullName = teacher.FullName,
            Email = teacher.Email,
            PhoneNumber = teacher.PhoneNumber,
            BrandName = teacher.BrandName,
            IsActive = teacher.IsActive,
            CreatedAtUtc = teacher.CreatedAtUtc
        };

        return Created($"/api/platform-admin/teachers/{teacher.Id}", response);
    }

    [HttpPatch("teachers/{id:guid}/status")]
    public async Task<IActionResult> UpdateTeacherStatus(Guid id, [FromBody] UpdateTeacherStatusRequest request)
    {
        var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.Id == id, HttpContext.RequestAborted);
        if (teacher == null)
        {
            return NotFound(new { message = "Teacher not found." });
        }

        teacher.IsActive = request.IsActive;
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(new
        {
            teacher.Id,
            teacher.Username,
            teacher.IsActive,
            message = teacher.IsActive ? "Teacher account activated." : "Teacher account deactivated."
        });
    }
}
