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
[Route("api/teacher/subjects")]
[Authorize(Roles = Roles.Teacher)]
public class SubjectsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly ITenantOwnershipValidator _ownershipValidator;

    public SubjectsController(
        ApplicationDbContext context,
        ITenantContext tenantContext,
        ITenantOwnershipValidator ownershipValidator)
    {
        _context = context;
        _tenantContext = tenantContext;
        _ownershipValidator = ownershipValidator;
    }

    [HttpGet]
    public async Task<IActionResult> GetSubjects([FromQuery] bool includeArchived = false)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var query = _context.Subjects
            .AsNoTracking()
            .Where(s => s.TeacherId == teacherId.Value);

        if (!includeArchived)
        {
            query = query.Where(s => !s.IsArchived);
        }

        var subjects = await query
            .OrderBy(s => s.Name)
            .Select(s => new SubjectResponseDto
            {
                Id = s.Id,
                Name = s.Name,
                GradeLevel = s.GradeLevel,
                IsArchived = s.IsArchived,
                GroupsCount = s.Groups.Count,
                CreatedAtUtc = s.CreatedAtUtc
            })
            .ToListAsync(HttpContext.RequestAborted);

        return Ok(subjects);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetSubjectById(Guid id)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var subject = await _context.Subjects
            .AsNoTracking()
            .Where(s => s.Id == id && s.TeacherId == teacherId.Value)
            .Select(s => new SubjectDetailResponseDto
            {
                Id = s.Id,
                Name = s.Name,
                GradeLevel = s.GradeLevel,
                IsArchived = s.IsArchived,
                CreatedAtUtc = s.CreatedAtUtc,
                Groups = s.Groups.Select(g => new SubjectGroupSummaryDto
                {
                    Id = g.Id,
                    Name = g.Name,
                    AcademicYearName = g.AcademicYear.Name,
                    ScheduleDescription = g.ScheduleDescription,
                    MaxCapacity = g.MaxCapacity,
                    IsArchived = g.IsArchived
                }).ToList()
            })
            .FirstOrDefaultAsync(HttpContext.RequestAborted);

        if (subject == null)
        {
            return NotFound(new { message = "Subject not found." });
        }

        return Ok(subject);
    }

    [HttpPost]
    public async Task<IActionResult> CreateSubject([FromBody] CreateSubjectRequest request)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var normalizedName = request.Name.Trim();

        // Check duplicate active subject name for this teacher
        var nameExists = await _context.Subjects
            .AnyAsync(s => s.TeacherId == teacherId.Value &&
                           !s.IsArchived &&
                           s.Name.ToLower() == normalizedName.ToLower(),
                      HttpContext.RequestAborted);

        if (nameExists)
        {
            return Conflict(new { message = "A subject with this name already exists." });
        }

        var subject = new Subject
        {
            Id = Guid.NewGuid(),
            TeacherId = teacherId.Value,
            Name = normalizedName,
            GradeLevel = request.GradeLevel?.Trim(),
            IsArchived = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.Subjects.Add(subject);
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        var response = new SubjectResponseDto
        {
            Id = subject.Id,
            Name = subject.Name,
            GradeLevel = subject.GradeLevel,
            IsArchived = subject.IsArchived,
            GroupsCount = 0,
            CreatedAtUtc = subject.CreatedAtUtc
        };

        return Created($"/api/teacher/subjects/{subject.Id}", response);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateSubject(Guid id, [FromBody] UpdateSubjectRequest request)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var subject = await _context.Subjects
            .FirstOrDefaultAsync(s => s.Id == id && s.TeacherId == teacherId.Value, HttpContext.RequestAborted);

        if (subject == null)
        {
            return NotFound(new { message = "Subject not found." });
        }

        var normalizedName = request.Name.Trim();

        // Check duplicate active subject name among other subjects of this teacher
        var nameConflict = await _context.Subjects
            .AnyAsync(s => s.TeacherId == teacherId.Value &&
                           s.Id != id &&
                           !s.IsArchived &&
                           s.Name.ToLower() == normalizedName.ToLower(),
                      HttpContext.RequestAborted);

        if (nameConflict)
        {
            return Conflict(new { message = "Another active subject with this name already exists." });
        }

        subject.Name = normalizedName;
        subject.GradeLevel = request.GradeLevel?.Trim();

        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        var groupsCount = await _context.Groups
            .CountAsync(g => g.SubjectId == id, HttpContext.RequestAborted);

        return Ok(new SubjectResponseDto
        {
            Id = subject.Id,
            Name = subject.Name,
            GradeLevel = subject.GradeLevel,
            IsArchived = subject.IsArchived,
            GroupsCount = groupsCount,
            CreatedAtUtc = subject.CreatedAtUtc
        });
    }

    [HttpPatch("{id:guid}/archive")]
    public async Task<IActionResult> ArchiveSubject(Guid id)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var subject = await _context.Subjects
            .FirstOrDefaultAsync(s => s.Id == id && s.TeacherId == teacherId.Value, HttpContext.RequestAborted);

        if (subject == null)
        {
            return NotFound(new { message = "Subject not found." });
        }

        subject.IsArchived = true;
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(new
        {
            id = subject.Id,
            name = subject.Name,
            isArchived = true,
            message = "Subject archived successfully."
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteSubject(Guid id)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var subject = await _context.Subjects
            .FirstOrDefaultAsync(s => s.Id == id && s.TeacherId == teacherId.Value, HttpContext.RequestAborted);

        if (subject == null)
        {
            return NotFound(new { message = "Subject not found." });
        }

        // Prevent hard deletion if groups are associated with this subject
        var hasGroups = await _context.Groups
            .AnyAsync(g => g.SubjectId == id, HttpContext.RequestAborted);

        if (hasGroups)
        {
            return BadRequest(new
            {
                message = "Cannot delete a subject with associated groups. Please archive it instead to preserve academic history."
            });
        }

        _context.Subjects.Remove(subject);
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(new { message = "Subject deleted successfully." });
    }
}
