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
[Route("api/teacher/groups")]
[Authorize(Roles = Roles.Teacher)]
public class GroupsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly ITenantOwnershipValidator _ownershipValidator;

    public GroupsController(
        ApplicationDbContext context,
        ITenantContext tenantContext,
        ITenantOwnershipValidator ownershipValidator)
    {
        _context = context;
        _tenantContext = tenantContext;
        _ownershipValidator = ownershipValidator;
    }

    [HttpGet]
    public async Task<IActionResult> GetGroups(
        [FromQuery] Guid? academicYearId = null,
        [FromQuery] Guid? subjectId = null,
        [FromQuery] bool includeArchived = false)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var query = _context.Groups
            .AsNoTracking()
            .Where(g => g.TeacherId == teacherId.Value);

        if (academicYearId.HasValue)
        {
            query = query.Where(g => g.AcademicYearId == academicYearId.Value);
        }

        if (subjectId.HasValue)
        {
            query = query.Where(g => g.SubjectId == subjectId.Value);
        }

        if (!includeArchived)
        {
            query = query.Where(g => !g.IsArchived);
        }

        var groups = await query
            .OrderBy(g => g.Name)
            .Select(g => new GroupResponseDto
            {
                Id = g.Id,
                SubjectId = g.SubjectId,
                SubjectName = g.Subject.Name,
                AcademicYearId = g.AcademicYearId,
                AcademicYearName = g.AcademicYear.Name,
                Name = g.Name,
                ScheduleDescription = g.ScheduleDescription,
                MaxCapacity = g.MaxCapacity,
                EnrolledStudentsCount = g.Enrollments.Count(e => e.IsActive),
                IsArchived = g.IsArchived,
                CreatedAtUtc = g.CreatedAtUtc
            })
            .ToListAsync(HttpContext.RequestAborted);

        return Ok(groups);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetGroupById(Guid id)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var group = await _context.Groups
            .AsNoTracking()
            .Where(g => g.Id == id && g.TeacherId == teacherId.Value)
            .Select(g => new GroupDetailResponseDto
            {
                Id = g.Id,
                SubjectId = g.SubjectId,
                SubjectName = g.Subject.Name,
                AcademicYearId = g.AcademicYearId,
                AcademicYearName = g.AcademicYear.Name,
                Name = g.Name,
                ScheduleDescription = g.ScheduleDescription,
                MaxCapacity = g.MaxCapacity,
                EnrolledStudentsCount = g.Enrollments.Count(e => e.IsActive),
                IsArchived = g.IsArchived,
                CreatedAtUtc = g.CreatedAtUtc,
                EnrolledStudents = g.Enrollments
                    .Where(e => e.IsActive)
                    .OrderBy(e => e.Student.FullName)
                    .Select(e => new GroupEnrolledStudentSummaryDto
                    {
                        StudentId = e.StudentId,
                        StudentCode = e.Student.StudentCode,
                        FullName = e.Student.FullName,
                        PhoneNumber = e.Student.PhoneNumber,
                        JoinedDateUtc = e.JoinedDateUtc
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(HttpContext.RequestAborted);

        if (group == null)
        {
            return NotFound(new { message = "Group not found." });
        }

        return Ok(group);
    }

    [HttpPost]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupRequest request)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        // Strict cross-tenant validation: ensure both Subject and AcademicYear belong to this teacher
        var isCreationOwned = await _ownershipValidator.ValidateGroupCreationOwnershipAsync(
            request.SubjectId,
            request.AcademicYearId,
            teacherId.Value,
            HttpContext.RequestAborted);

        if (!isCreationOwned)
        {
            return NotFound(new { message = "Specified Subject or Academic Year was not found." });
        }

        // Prevent creating groups in an archived academic year
        var academicYear = await _context.AcademicYears
            .AsNoTracking()
            .FirstAsync(ay => ay.Id == request.AcademicYearId, HttpContext.RequestAborted);

        if (academicYear.IsArchived)
        {
            return BadRequest(new { message = "Cannot create groups in an archived academic year." });
        }

        var normalizedName = request.Name.Trim();

        // Check duplicate active group name under same subject and academic year
        var nameConflict = await _context.Groups
            .AnyAsync(g => g.TeacherId == teacherId.Value &&
                           g.SubjectId == request.SubjectId &&
                           g.AcademicYearId == request.AcademicYearId &&
                           !g.IsArchived &&
                           g.Name.ToLower() == normalizedName.ToLower(),
                      HttpContext.RequestAborted);

        if (nameConflict)
        {
            return Conflict(new { message = "A group with this name already exists for this subject in the specified academic year." });
        }

        var group = new Group
        {
            Id = Guid.NewGuid(),
            TeacherId = teacherId.Value,
            SubjectId = request.SubjectId,
            AcademicYearId = request.AcademicYearId,
            Name = normalizedName,
            ScheduleDescription = request.ScheduleDescription?.Trim(),
            MaxCapacity = request.MaxCapacity,
            IsArchived = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.Groups.Add(group);
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        var subject = await _context.Subjects
            .AsNoTracking()
            .FirstAsync(s => s.Id == request.SubjectId, HttpContext.RequestAborted);

        var response = new GroupResponseDto
        {
            Id = group.Id,
            SubjectId = group.SubjectId,
            SubjectName = subject.Name,
            AcademicYearId = group.AcademicYearId,
            AcademicYearName = academicYear.Name,
            Name = group.Name,
            ScheduleDescription = group.ScheduleDescription,
            MaxCapacity = group.MaxCapacity,
            EnrolledStudentsCount = 0,
            IsArchived = group.IsArchived,
            CreatedAtUtc = group.CreatedAtUtc
        };

        return Created($"/api/teacher/groups/{group.Id}", response);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateGroup(Guid id, [FromBody] UpdateGroupRequest request)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var group = await _context.Groups
            .Include(g => g.Subject)
            .Include(g => g.AcademicYear)
            .FirstOrDefaultAsync(g => g.Id == id && g.TeacherId == teacherId.Value, HttpContext.RequestAborted);

        if (group == null)
        {
            return NotFound(new { message = "Group not found." });
        }

        var activeEnrolledCount = await _context.GroupEnrollments
            .CountAsync(e => e.GroupId == id && e.IsActive, HttpContext.RequestAborted);

        if (request.MaxCapacity.HasValue && request.MaxCapacity.Value < activeEnrolledCount)
        {
            return BadRequest(new
            {
                message = $"MaxCapacity ({request.MaxCapacity.Value}) cannot be less than current active enrolled students count ({activeEnrolledCount})."
            });
        }

        var normalizedName = request.Name.Trim();

        var nameConflict = await _context.Groups
            .AnyAsync(g => g.TeacherId == teacherId.Value &&
                           g.Id != id &&
                           g.SubjectId == group.SubjectId &&
                           g.AcademicYearId == group.AcademicYearId &&
                           !g.IsArchived &&
                           g.Name.ToLower() == normalizedName.ToLower(),
                      HttpContext.RequestAborted);

        if (nameConflict)
        {
            return Conflict(new { message = "Another active group with this name already exists for this subject in the specified academic year." });
        }

        group.Name = normalizedName;
        group.ScheduleDescription = request.ScheduleDescription?.Trim();
        group.MaxCapacity = request.MaxCapacity;

        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(new GroupResponseDto
        {
            Id = group.Id,
            SubjectId = group.SubjectId,
            SubjectName = group.Subject.Name,
            AcademicYearId = group.AcademicYearId,
            AcademicYearName = group.AcademicYear.Name,
            Name = group.Name,
            ScheduleDescription = group.ScheduleDescription,
            MaxCapacity = group.MaxCapacity,
            EnrolledStudentsCount = activeEnrolledCount,
            IsArchived = group.IsArchived,
            CreatedAtUtc = group.CreatedAtUtc
        });
    }

    [HttpPatch("{id:guid}/archive")]
    public async Task<IActionResult> ArchiveGroup(Guid id)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var group = await _context.Groups
            .FirstOrDefaultAsync(g => g.Id == id && g.TeacherId == teacherId.Value, HttpContext.RequestAborted);

        if (group == null)
        {
            return NotFound(new { message = "Group not found." });
        }

        group.IsArchived = true;
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(new
        {
            id = group.Id,
            name = group.Name,
            isArchived = true,
            message = "Group archived successfully."
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteGroup(Guid id)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var group = await _context.Groups
            .FirstOrDefaultAsync(g => g.Id == id && g.TeacherId == teacherId.Value, HttpContext.RequestAborted);

        if (group == null)
        {
            return NotFound(new { message = "Group not found." });
        }

        // Rule BR-ACA-023: Deleting a Group is only permitted if zero sessions and zero enrollments exist
        var hasSessions = await _context.Sessions
            .AnyAsync(s => s.GroupId == id, HttpContext.RequestAborted);

        var hasEnrollments = await _context.GroupEnrollments
            .AnyAsync(ge => ge.GroupId == id, HttpContext.RequestAborted);

        if (hasSessions || hasEnrollments)
        {
            return BadRequest(new
            {
                message = "Cannot delete a group with associated sessions or student enrollments. Please archive it instead to preserve academic history."
            });
        }

        _context.Groups.Remove(group);
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(new { message = "Group deleted successfully." });
    }
}
