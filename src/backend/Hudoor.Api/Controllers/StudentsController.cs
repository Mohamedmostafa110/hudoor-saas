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
[Route("api/teacher/students")]
[Authorize(Roles = Roles.Teacher)]
public class StudentsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly ITenantOwnershipValidator _ownershipValidator;
    private readonly IStudentCodeGenerator _studentCodeGenerator;
    private readonly IPasswordService _passwordService;
    private readonly ILogger<StudentsController> _logger;

    public StudentsController(
        ApplicationDbContext context,
        ITenantContext tenantContext,
        ITenantOwnershipValidator ownershipValidator,
        IStudentCodeGenerator studentCodeGenerator,
        IPasswordService passwordService,
        ILogger<StudentsController> logger)
    {
        _context = context;
        _tenantContext = tenantContext;
        _ownershipValidator = ownershipValidator;
        _studentCodeGenerator = studentCodeGenerator;
        _passwordService = passwordService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetStudents(
        [FromQuery] string? search = null,
        [FromQuery] string? grade = null,
        [FromQuery] Guid? groupId = null,
        [FromQuery] bool? isActive = null)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var query = _context.Students
            .AsNoTracking()
            .Where(s => s.TeacherId == teacherId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmedSearch = search.Trim();
            query = query.Where(s =>
                s.FullName.Contains(trimmedSearch) ||
                s.StudentCode.Contains(trimmedSearch) ||
                (s.PhoneNumber != null && s.PhoneNumber.Contains(trimmedSearch)) ||
                (s.ParentPhoneNumber != null && s.ParentPhoneNumber.Contains(trimmedSearch)));
        }

        if (!string.IsNullOrWhiteSpace(grade))
        {
            var trimmedGrade = grade.Trim();
            query = query.Where(s => s.CurrentGrade != null && s.CurrentGrade == trimmedGrade);
        }

        if (groupId.HasValue)
        {
            query = query.Where(s => s.Enrollments.Any(e => e.GroupId == groupId.Value && e.IsActive));
        }

        if (isActive.HasValue)
        {
            query = query.Where(s => s.IsActive == isActive.Value);
        }

        var students = await query
            .OrderBy(s => s.FullName)
            .Select(s => new StudentSummaryDto
            {
                Id = s.Id,
                StudentCode = s.StudentCode,
                FullName = s.FullName,
                PhoneNumber = s.PhoneNumber,
                ParentPhoneNumber = s.ParentPhoneNumber,
                CurrentGrade = s.CurrentGrade,
                IsActive = s.IsActive,
                MustChangePassword = s.MustChangePassword,
                EnrolledGroupsCount = s.Enrollments.Count(e => e.IsActive),
                CreatedAtUtc = s.CreatedAtUtc
            })
            .ToListAsync();

        return Ok(students);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetStudentById(Guid id)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var student = await _context.Students
            .AsNoTracking()
            .Include(s => s.Enrollments)
                .ThenInclude(e => e.Group)
                    .ThenInclude(g => g.Subject)
            .Include(s => s.Enrollments)
                .ThenInclude(e => e.Group)
                    .ThenInclude(g => g.AcademicYear)
            .FirstOrDefaultAsync(s => s.Id == id && s.TeacherId == teacherId.Value);

        if (student == null)
        {
            return NotFound(new { message = "Student not found." });
        }

        var result = new StudentDetailDto
        {
            Id = student.Id,
            StudentCode = student.StudentCode,
            FullName = student.FullName,
            PhoneNumber = student.PhoneNumber,
            ParentPhoneNumber = student.ParentPhoneNumber,
            CurrentGrade = student.CurrentGrade,
            Notes = student.Notes,
            IsActive = student.IsActive,
            MustChangePassword = student.MustChangePassword,
            TemporaryPasswordExpiresAtUtc = student.TemporaryPasswordExpiresAtUtc,
            CreatedAtUtc = student.CreatedAtUtc,
            Enrollments = student.Enrollments
                .OrderByDescending(e => e.JoinedDateUtc)
                .Select(e => new StudentEnrollmentSummaryDto
                {
                    EnrollmentId = e.Id,
                    GroupId = e.GroupId,
                    GroupName = e.Group.Name,
                    SubjectName = e.Group.Subject.Name,
                    AcademicYearName = e.Group.AcademicYear.Name,
                    JoinedDateUtc = e.JoinedDateUtc,
                    LeftDateUtc = e.LeftDateUtc,
                    IsActive = e.IsActive
                })
                .ToList()
        };

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateStudent([FromBody] CreateStudentRequest request)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        Group? initialGroup = null;
        if (request.InitialGroupId.HasValue)
        {
            var isGroupOwned = await _ownershipValidator.ValidateGroupOwnershipAsync(request.InitialGroupId.Value, teacherId.Value);
            if (!isGroupOwned)
            {
                return NotFound(new { message = "Specified initial group was not found." });
            }

            initialGroup = await _context.Groups
                .Include(g => g.Enrollments.Where(e => e.IsActive))
                .FirstOrDefaultAsync(g => g.Id == request.InitialGroupId.Value && g.TeacherId == teacherId.Value);

            if (initialGroup == null)
            {
                return NotFound(new { message = "Specified initial group was not found." });
            }

            if (initialGroup.IsArchived)
            {
                return BadRequest(new { message = $"Cannot enroll student into archived group '{initialGroup.Name}'." });
            }

            if (initialGroup.MaxCapacity.HasValue && initialGroup.Enrollments.Count >= initialGroup.MaxCapacity.Value)
            {
                return BadRequest(new { message = $"Group '{initialGroup.Name}' has reached its maximum capacity of {initialGroup.MaxCapacity.Value} students." });
            }
        }

        var studentCode = await _studentCodeGenerator.GenerateUniqueStudentCodeAsync();
        var tempPassword = _passwordService.GenerateTemporaryPassword();
        var passwordHash = _passwordService.HashPassword(tempPassword);

        var student = new Student
        {
            Id = Guid.NewGuid(),
            TeacherId = teacherId.Value,
            StudentCode = studentCode,
            PasswordHash = passwordHash,
            TokenVersion = 1,
            MustChangePassword = true,
            TemporaryPasswordExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            FullName = request.FullName.Trim(),
            PhoneNumber = request.PhoneNumber?.Trim(),
            ParentPhoneNumber = request.ParentPhoneNumber?.Trim(),
            CurrentGrade = request.CurrentGrade?.Trim(),
            Notes = request.Notes?.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        if (initialGroup != null)
        {
            var enrollment = new GroupEnrollment
            {
                Id = Guid.NewGuid(),
                TeacherId = teacherId.Value,
                GroupId = initialGroup.Id,
                StudentId = student.Id,
                JoinedDateUtc = DateTime.UtcNow,
                IsActive = true
            };
            student.Enrollments.Add(enrollment);
        }

        _context.Students.Add(student);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Student {StudentCode} provisioned for Teacher {TeacherId} with initial group {GroupId}",
            studentCode, teacherId.Value, initialGroup?.Id);

        var response = new StudentCreatedResponseDto
        {
            Id = student.Id,
            StudentCode = student.StudentCode,
            TemporaryPassword = tempPassword,
            FullName = student.FullName,
            CurrentGrade = student.CurrentGrade,
            ExpiresAtUtc = student.TemporaryPasswordExpiresAtUtc,
            InitialGroupId = initialGroup?.Id,
            InitialGroupName = initialGroup?.Name
        };

        return CreatedAtAction(nameof(GetStudentById), new { id = student.Id }, response);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateStudent(Guid id, [FromBody] UpdateStudentRequest request)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var student = await _context.Students
            .Include(s => s.Enrollments)
                .ThenInclude(e => e.Group)
                    .ThenInclude(g => g.Subject)
            .Include(s => s.Enrollments)
                .ThenInclude(e => e.Group)
                    .ThenInclude(g => g.AcademicYear)
            .FirstOrDefaultAsync(s => s.Id == id && s.TeacherId == teacherId.Value);

        if (student == null)
        {
            return NotFound(new { message = "Student not found." });
        }

        student.FullName = request.FullName.Trim();
        student.PhoneNumber = request.PhoneNumber?.Trim();
        student.ParentPhoneNumber = request.ParentPhoneNumber?.Trim();
        student.CurrentGrade = request.CurrentGrade?.Trim();
        student.Notes = request.Notes?.Trim();

        await _context.SaveChangesAsync();

        var result = new StudentDetailDto
        {
            Id = student.Id,
            StudentCode = student.StudentCode,
            FullName = student.FullName,
            PhoneNumber = student.PhoneNumber,
            ParentPhoneNumber = student.ParentPhoneNumber,
            CurrentGrade = student.CurrentGrade,
            Notes = student.Notes,
            IsActive = student.IsActive,
            MustChangePassword = student.MustChangePassword,
            TemporaryPasswordExpiresAtUtc = student.TemporaryPasswordExpiresAtUtc,
            CreatedAtUtc = student.CreatedAtUtc,
            Enrollments = student.Enrollments
                .OrderByDescending(e => e.JoinedDateUtc)
                .Select(e => new StudentEnrollmentSummaryDto
                {
                    EnrollmentId = e.Id,
                    GroupId = e.GroupId,
                    GroupName = e.Group.Name,
                    SubjectName = e.Group.Subject.Name,
                    AcademicYearName = e.Group.AcademicYear.Name,
                    JoinedDateUtc = e.JoinedDateUtc,
                    LeftDateUtc = e.LeftDateUtc,
                    IsActive = e.IsActive
                })
                .ToList()
        };

        return Ok(result);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStudentStatus(Guid id, [FromBody] UpdateStudentStatusRequest request)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.Id == id && s.TeacherId == teacherId.Value);

        if (student == null)
        {
            return NotFound(new { message = "Student not found." });
        }

        student.IsActive = request.IsActive;

        // If student is deactivated, increment TokenVersion to immediately revoke any active JWTs
        if (!request.IsActive)
        {
            student.TokenVersion++;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            id = student.Id,
            isActive = student.IsActive,
            message = student.IsActive ? "Student account activated." : "Student account deactivated and active sessions revoked."
        });
    }

    [HttpPost("{id:guid}/regenerate-code")]
    public async Task<IActionResult> RegenerateStudentCode(Guid id)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.Id == id && s.TeacherId == teacherId.Value);

        if (student == null)
        {
            return NotFound(new { message = "Student not found." });
        }

        var oldCode = student.StudentCode;
        var newCode = await _studentCodeGenerator.GenerateUniqueStudentCodeAsync();

        student.StudentCode = newCode;
        // Invalidate active sessions since login identifier changed
        student.TokenVersion++;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Student {OldCode} regenerated to new code {NewCode} for Teacher {TeacherId}",
            oldCode, newCode, teacherId.Value);

        return Ok(new RegenerateCodeResponseDto
        {
            StudentId = student.Id,
            OldStudentCode = oldCode,
            NewStudentCode = newCode,
            TokenVersion = student.TokenVersion
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteStudent(Guid id)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var student = await _context.Students
            .Include(s => s.Enrollments)
            .Include(s => s.AttendanceRecords)
            .FirstOrDefaultAsync(s => s.Id == id && s.TeacherId == teacherId.Value);

        if (student == null)
        {
            return NotFound(new { message = "Student not found." });
        }

        // BR-STU-020: Prohibit deleting student if attendance records or enrollment history exists
        if (student.AttendanceRecords.Any() || student.Enrollments.Any())
        {
            return BadRequest(new
            {
                message = "Cannot delete student because academic or attendance history exists. Please deactivate the student account instead."
            });
        }

        _context.Students.Remove(student);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
