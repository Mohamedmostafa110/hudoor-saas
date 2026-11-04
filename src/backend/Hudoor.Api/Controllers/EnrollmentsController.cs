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
public class EnrollmentsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly ITenantOwnershipValidator _ownershipValidator;
    private readonly ILogger<EnrollmentsController> _logger;

    public EnrollmentsController(
        ApplicationDbContext context,
        ITenantContext tenantContext,
        ITenantOwnershipValidator ownershipValidator,
        ILogger<EnrollmentsController> logger)
    {
        _context = context;
        _tenantContext = tenantContext;
        _ownershipValidator = ownershipValidator;
        _logger = logger;
    }

    [HttpGet("groups/{groupId:guid}/students")]
    public async Task<IActionResult> GetGroupStudents(Guid groupId, [FromQuery] bool? isActive = true)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var isGroupOwned = await _ownershipValidator.ValidateGroupOwnershipAsync(groupId, teacherId.Value);
        if (!isGroupOwned)
        {
            return NotFound(new { message = "Group not found." });
        }

        var query = _context.GroupEnrollments
            .AsNoTracking()
            .Include(e => e.Student)
            .Where(e => e.GroupId == groupId && e.TeacherId == teacherId.Value);

        if (isActive.HasValue)
        {
            query = query.Where(e => e.IsActive == isActive.Value);
        }

        var students = await query
            .OrderBy(e => e.Student.FullName)
            .Select(e => new GroupStudentDto
            {
                EnrollmentId = e.Id,
                StudentId = e.Student.Id,
                StudentCode = e.Student.StudentCode,
                FullName = e.Student.FullName,
                PhoneNumber = e.Student.PhoneNumber,
                ParentPhoneNumber = e.Student.ParentPhoneNumber,
                CurrentGrade = e.Student.CurrentGrade,
                JoinedDateUtc = e.JoinedDateUtc,
                LeftDateUtc = e.LeftDateUtc,
                IsActive = e.IsActive
            })
            .ToListAsync();

        return Ok(students);
    }

    [HttpPost("groups/{groupId:guid}/students")]
    public async Task<IActionResult> EnrollStudent(Guid groupId, [FromBody] EnrollStudentRequest request)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var isOwned = await _ownershipValidator.ValidateEnrollmentOwnershipAsync(groupId, request.StudentId, teacherId.Value);
        if (!isOwned)
        {
            return NotFound(new { message = "Group or student not found." });
        }

        var group = await _context.Groups
            .Include(g => g.Enrollments.Where(e => e.IsActive))
            .FirstOrDefaultAsync(g => g.Id == groupId && g.TeacherId == teacherId.Value);

        if (group == null)
        {
            return NotFound(new { message = "Group not found." });
        }

        if (group.IsArchived)
        {
            return BadRequest(new { message = $"Cannot enroll students into archived group '{group.Name}'." });
        }

        if (group.MaxCapacity.HasValue && group.Enrollments.Count >= group.MaxCapacity.Value)
        {
            return BadRequest(new { message = $"Group '{group.Name}' has reached its maximum capacity of {group.MaxCapacity.Value} students." });
        }

        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.Id == request.StudentId && s.TeacherId == teacherId.Value);

        if (student == null)
        {
            return NotFound(new { message = "Student not found." });
        }

        if (!student.IsActive)
        {
            return BadRequest(new { message = "Cannot enroll a deactivated student. Please activate the student account first." });
        }

        var alreadyEnrolled = await _context.GroupEnrollments
            .AnyAsync(e => e.GroupId == groupId && e.StudentId == request.StudentId && e.IsActive && e.TeacherId == teacherId.Value);

        if (alreadyEnrolled)
        {
            return Conflict(new { message = "Student is already actively enrolled in this group." });
        }

        var enrollment = new GroupEnrollment
        {
            Id = Guid.NewGuid(),
            TeacherId = teacherId.Value,
            GroupId = group.Id,
            StudentId = student.Id,
            JoinedDateUtc = DateTime.UtcNow,
            IsActive = true
        };

        _context.GroupEnrollments.Add(enrollment);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Student {StudentId} enrolled into Group {GroupId} by Teacher {TeacherId}",
            student.Id, group.Id, teacherId.Value);

        var response = new EnrollmentResponseDto
        {
            Id = enrollment.Id,
            GroupId = group.Id,
            GroupName = group.Name,
            StudentId = student.Id,
            StudentCode = student.StudentCode,
            StudentName = student.FullName,
            JoinedDateUtc = enrollment.JoinedDateUtc,
            IsActive = enrollment.IsActive
        };

        return Created($"/api/teacher/groups/{group.Id}/students", response);
    }

    [HttpDelete("groups/{groupId:guid}/students/{studentId:guid}")]
    public async Task<IActionResult> UnenrollStudent(Guid groupId, Guid studentId)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var enrollment = await _context.GroupEnrollments
            .FirstOrDefaultAsync(e => e.GroupId == groupId && e.StudentId == studentId && e.IsActive && e.TeacherId == teacherId.Value);

        if (enrollment == null)
        {
            return NotFound(new { message = "Active enrollment not found for this student in this group." });
        }

        enrollment.LeftDateUtc = DateTime.UtcNow;
        enrollment.IsActive = false;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Student {StudentId} unenrolled from Group {GroupId} by Teacher {TeacherId}",
            studentId, groupId, teacherId.Value);

        return Ok(new { message = "Student unenrolled successfully." });
    }

    [HttpPost("enrollments/transfer")]
    public async Task<IActionResult> TransferStudent([FromBody] TransferStudentRequest request)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        if (request.FromGroupId == request.ToGroupId)
        {
            return BadRequest(new { message = "Source and destination groups must be different." });
        }

        var isOwnershipValid = await _ownershipValidator.ValidateTransferOwnershipAsync(
            request.StudentId, request.FromGroupId, request.ToGroupId, teacherId.Value);

        if (!isOwnershipValid)
        {
            return NotFound(new { message = "Student or groups not found." });
        }

        var toGroup = await _context.Groups
            .Include(g => g.Enrollments.Where(e => e.IsActive))
            .FirstOrDefaultAsync(g => g.Id == request.ToGroupId && g.TeacherId == teacherId.Value);

        if (toGroup == null)
        {
            return NotFound(new { message = "Destination group not found." });
        }

        if (toGroup.IsArchived)
        {
            return BadRequest(new { message = $"Cannot transfer student to archived group '{toGroup.Name}'." });
        }

        if (toGroup.MaxCapacity.HasValue && toGroup.Enrollments.Count >= toGroup.MaxCapacity.Value)
        {
            return BadRequest(new { message = $"Destination group '{toGroup.Name}' has reached its maximum capacity of {toGroup.MaxCapacity.Value} students." });
        }

        var alreadyEnrolledInTarget = await _context.GroupEnrollments
            .AnyAsync(e => e.GroupId == request.ToGroupId && e.StudentId == request.StudentId && e.IsActive && e.TeacherId == teacherId.Value);

        if (alreadyEnrolledInTarget)
        {
            return Conflict(new { message = "Student is already actively enrolled in destination group." });
        }

        var currentEnrollment = await _context.GroupEnrollments
            .FirstOrDefaultAsync(e => e.GroupId == request.FromGroupId && e.StudentId == request.StudentId && e.IsActive && e.TeacherId == teacherId.Value);

        if (currentEnrollment == null)
        {
            return BadRequest(new { message = "Student does not have an active enrollment in the source group." });
        }

        // Close source enrollment
        var now = DateTime.UtcNow;
        currentEnrollment.LeftDateUtc = now;
        currentEnrollment.IsActive = false;

        // Open target enrollment
        var newEnrollment = new GroupEnrollment
        {
            Id = Guid.NewGuid(),
            TeacherId = teacherId.Value,
            GroupId = toGroup.Id,
            StudentId = request.StudentId,
            JoinedDateUtc = now,
            IsActive = true
        };

        _context.GroupEnrollments.Add(newEnrollment);
        await _context.SaveChangesAsync();

        var student = await _context.Students.FindAsync(request.StudentId);
        var fromGroup = await _context.Groups.FindAsync(request.FromGroupId);

        _logger.LogInformation("Student {StudentId} transferred from Group {FromGroupId} to Group {ToGroupId} by Teacher {TeacherId}",
            request.StudentId, request.FromGroupId, request.ToGroupId, teacherId.Value);

        var response = new TransferResponseDto
        {
            StudentId = request.StudentId,
            StudentName = student?.FullName ?? string.Empty,
            FromGroupId = request.FromGroupId,
            FromGroupName = fromGroup?.Name ?? string.Empty,
            ToGroupId = toGroup.Id,
            ToGroupName = toGroup.Name,
            TransferredAtUtc = now,
            NewEnrollmentId = newEnrollment.Id
        };

        return Ok(response);
    }
}
