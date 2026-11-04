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
[Route("api/teacher/academic-years")]
[Authorize(Roles = Roles.Teacher)]
public class RolloverController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly ITenantOwnershipValidator _ownershipValidator;
    private readonly ILogger<RolloverController> _logger;

    public RolloverController(
        ApplicationDbContext context,
        ITenantContext tenantContext,
        ITenantOwnershipValidator ownershipValidator,
        ILogger<RolloverController> logger)
    {
        _context = context;
        _tenantContext = tenantContext;
        _ownershipValidator = ownershipValidator;
        _logger = logger;
    }

    [HttpGet("{id:guid}/rollover-summary")]
    public async Task<IActionResult> GetRolloverSummary(Guid id)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var isYearOwned = await _ownershipValidator.ValidateAcademicYearOwnershipAsync(id, teacherId.Value);
        if (!isYearOwned)
        {
            return NotFound(new { message = "Academic year not found." });
        }

        var year = await _context.AcademicYears
            .AsNoTracking()
            .Include(ay => ay.Groups)
                .ThenInclude(g => g.Enrollments.Where(e => e.IsActive))
                    .ThenInclude(e => e.Student)
            .FirstOrDefaultAsync(ay => ay.Id == id && ay.TeacherId == teacherId.Value);

        if (year == null)
        {
            return NotFound(new { message = "Academic year not found." });
        }

        var groups = year.Groups.ToList();
        var allEnrolledStudents = groups
            .SelectMany(g => g.Enrollments)
            .Where(e => e.IsActive)
            .Select(e => e.Student)
            .DistinctBy(s => s.Id)
            .ToList();

        var gradeCounts = allEnrolledStudents
            .GroupBy(s => string.IsNullOrWhiteSpace(s.CurrentGrade) ? "غير محدد" : s.CurrentGrade)
            .Select(g => new GradeStudentsCountDto
            {
                Grade = g.Key,
                StudentCount = g.Count()
            })
            .OrderBy(g => g.Grade)
            .ToList();

        var result = new RolloverSummaryDto
        {
            CurrentAcademicYearId = year.Id,
            CurrentAcademicYearName = year.Name,
            StartDate = year.StartDate,
            EndDate = year.EndDate,
            TotalGroups = groups.Count,
            TotalEnrolledStudents = allEnrolledStudents.Count,
            StudentsByGrade = gradeCounts
        };

        return Ok(result);
    }

    [HttpPost("rollover")]
    public async Task<IActionResult> ExecuteRollover([FromBody] ExecuteRolloverRequest request)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        if (request.StartDate >= request.EndDate)
        {
            return BadRequest(new { message = "New academic year start date must be before end date." });
        }

        // Find active academic year to rollover from
        var currentActiveYear = await _context.AcademicYears
            .FirstOrDefaultAsync(ay => ay.TeacherId == teacherId.Value && ay.IsActive);

        if (currentActiveYear == null)
        {
            return BadRequest(new { message = "No active academic year found to roll over from." });
        }

        // Validate all students in progression belong to this teacher
        var studentIds = request.StudentProgressions.Select(p => p.StudentId).Distinct().ToList();
        var students = await _context.Students
            .Include(s => s.Enrollments.Where(e => e.IsActive))
            .Where(s => studentIds.Contains(s.Id) && s.TeacherId == teacherId.Value)
            .ToDictionaryAsync(s => s.Id);

        if (students.Count != studentIds.Count)
        {
            return BadRequest(new { message = "One or more students in progression were not found or belong to another teacher." });
        }

        // Validate target groups if specified
        var targetGroupIds = request.StudentProgressions
            .Where(p => p.TargetGroupId.HasValue)
            .Select(p => p.TargetGroupId!.Value)
            .Distinct()
            .ToList();

        foreach (var targetGroupId in targetGroupIds)
        {
            var isGroupOwned = await _ownershipValidator.ValidateGroupOwnershipAsync(targetGroupId, teacherId.Value);
            if (!isGroupOwned)
            {
                return BadRequest(new { message = $"Specified target group '{targetGroupId}' not found or belongs to another teacher." });
            }
        }

        // Execute in atomic transaction (Rule BR-ROL-090: Zero data loss guarantee)
        await using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync()
            : null;

        // 1. Archive current active year
        currentActiveYear.IsActive = false;
        currentActiveYear.IsArchived = true;

        // 2. Create and activate new academic year
        var newYear = new AcademicYear
        {
            Id = Guid.NewGuid(),
            TeacherId = teacherId.Value,
            Name = request.NewAcademicYearName.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = true,
            IsArchived = false,
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.AcademicYears.Add(newYear);

        // 3. Process student progressions
        int promotedCount = 0;
        int repeatedCount = 0;
        int departedCount = 0;
        var now = DateTime.UtcNow;

        foreach (var item in request.StudentProgressions)
        {
            var student = students[item.StudentId];

            // Close active group enrollments from prior academic year
            foreach (var activeEnrollment in student.Enrollments.Where(e => e.IsActive))
            {
                activeEnrollment.IsActive = false;
                activeEnrollment.LeftDateUtc = now;
            }

            switch (item.Action)
            {
                case "Promote":
                    if (!string.IsNullOrWhiteSpace(item.NewGrade))
                    {
                        student.CurrentGrade = item.NewGrade.Trim();
                    }
                    if (item.TargetGroupId.HasValue)
                    {
                        var newEnrollment = new GroupEnrollment
                        {
                            Id = Guid.NewGuid(),
                            TeacherId = teacherId.Value,
                            GroupId = item.TargetGroupId.Value,
                            StudentId = student.Id,
                            JoinedDateUtc = now,
                            IsActive = true
                        };
                        _context.GroupEnrollments.Add(newEnrollment);
                    }
                    promotedCount++;
                    break;

                case "Repeat":
                    if (item.TargetGroupId.HasValue)
                    {
                        var newEnrollment = new GroupEnrollment
                        {
                            Id = Guid.NewGuid(),
                            TeacherId = teacherId.Value,
                            GroupId = item.TargetGroupId.Value,
                            StudentId = student.Id,
                            JoinedDateUtc = now,
                            IsActive = true
                        };
                        _context.GroupEnrollments.Add(newEnrollment);
                    }
                    repeatedCount++;
                    break;

                case "Depart":
                    student.IsActive = false;
                    student.TokenVersion++; // revoke any active sessions
                    departedCount++;
                    break;
            }
        }

        await _context.SaveChangesAsync();
        if (transaction != null)
        {
            await transaction.CommitAsync();
        }

        _logger.LogInformation("Rollover executed by Teacher {TeacherId} from Year {OldYearId} to {NewYearId} (Promoted: {P}, Repeated: {R}, Departed: {D})",
            teacherId.Value, currentActiveYear.Id, newYear.Id, promotedCount, repeatedCount, departedCount);

        var response = new RolloverResultDto
        {
            PreviousAcademicYearId = currentActiveYear.Id,
            PreviousAcademicYearName = currentActiveYear.Name,
            NewAcademicYearId = newYear.Id,
            NewAcademicYearName = newYear.Name,
            PromotedCount = promotedCount,
            RepeatedCount = repeatedCount,
            DepartedCount = departedCount,
            TotalProcessed = promotedCount + repeatedCount + departedCount,
            ExecutedAtUtc = now,
            Message = "Academic year rollover executed successfully. All historical sessions and attendance records remain preserved."
        };

        return Ok(response);
    }
}
