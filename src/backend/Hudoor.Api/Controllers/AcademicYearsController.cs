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
public class AcademicYearsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly ITenantOwnershipValidator _ownershipValidator;

    public AcademicYearsController(
        ApplicationDbContext context,
        ITenantContext tenantContext,
        ITenantOwnershipValidator ownershipValidator)
    {
        _context = context;
        _tenantContext = tenantContext;
        _ownershipValidator = ownershipValidator;
    }

    [HttpGet]
    public async Task<IActionResult> GetAcademicYears([FromQuery] bool includeArchived = false)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var query = _context.AcademicYears
            .AsNoTracking()
            .Where(ay => ay.TeacherId == teacherId.Value);

        if (!includeArchived)
        {
            query = query.Where(ay => !ay.IsArchived);
        }

        var years = await query
            .OrderByDescending(ay => ay.IsActive)
            .ThenByDescending(ay => ay.StartDate)
            .Select(ay => new AcademicYearResponseDto
            {
                Id = ay.Id,
                Name = ay.Name,
                StartDate = ay.StartDate,
                EndDate = ay.EndDate,
                IsActive = ay.IsActive,
                IsArchived = ay.IsArchived,
                TermsCount = ay.Terms.Count,
                GroupsCount = ay.Groups.Count,
                CreatedAtUtc = ay.CreatedAtUtc
            })
            .ToListAsync(HttpContext.RequestAborted);

        return Ok(years);
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActiveAcademicYear()
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var activeYear = await _context.AcademicYears
            .AsNoTracking()
            .Where(ay => ay.TeacherId == teacherId.Value && ay.IsActive && !ay.IsArchived)
            .Select(ay => new AcademicYearResponseDto
            {
                Id = ay.Id,
                Name = ay.Name,
                StartDate = ay.StartDate,
                EndDate = ay.EndDate,
                IsActive = ay.IsActive,
                IsArchived = ay.IsArchived,
                TermsCount = ay.Terms.Count,
                GroupsCount = ay.Groups.Count,
                CreatedAtUtc = ay.CreatedAtUtc
            })
            .FirstOrDefaultAsync(HttpContext.RequestAborted);

        if (activeYear == null)
        {
            return NotFound(new { message = "No active academic year found." });
        }

        return Ok(activeYear);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAcademicYearById(Guid id)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var year = await _context.AcademicYears
            .AsNoTracking()
            .Where(ay => ay.Id == id && ay.TeacherId == teacherId.Value)
            .Select(ay => new AcademicYearDetailResponseDto
            {
                Id = ay.Id,
                Name = ay.Name,
                StartDate = ay.StartDate,
                EndDate = ay.EndDate,
                IsActive = ay.IsActive,
                IsArchived = ay.IsArchived,
                GroupsCount = ay.Groups.Count,
                CreatedAtUtc = ay.CreatedAtUtc,
                Terms = ay.Terms.Select(t => new TermResponseDto
                {
                    Id = t.Id,
                    AcademicYearId = t.AcademicYearId,
                    Name = t.Name,
                    StartDate = t.StartDate,
                    EndDate = t.EndDate,
                    IsActive = t.IsActive,
                    CreatedAtUtc = t.CreatedAtUtc
                }).ToList()
            })
            .FirstOrDefaultAsync(HttpContext.RequestAborted);

        if (year == null)
        {
            return NotFound(new { message = "Academic year not found." });
        }

        return Ok(year);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAcademicYear([FromBody] CreateAcademicYearRequest request)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        if (request.EndDate <= request.StartDate)
        {
            return BadRequest(new { message = "EndDate must be greater than StartDate." });
        }

        // Rule BR-ACA-010: Exactly one Academic Year can be active at a time.
        // If SetAsActive is true, atomically deactivate any currently active academic year for this teacher.
        if (request.SetAsActive)
        {
            var currentlyActiveYears = await _context.AcademicYears
                .Where(ay => ay.TeacherId == teacherId.Value && ay.IsActive)
                .ToListAsync(HttpContext.RequestAborted);

            foreach (var active in currentlyActiveYears)
            {
                active.IsActive = false;
            }
        }

        var year = new AcademicYear
        {
            Id = Guid.NewGuid(),
            TeacherId = teacherId.Value,
            Name = request.Name.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = request.SetAsActive,
            IsArchived = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.AcademicYears.Add(year);
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        var response = new AcademicYearResponseDto
        {
            Id = year.Id,
            Name = year.Name,
            StartDate = year.StartDate,
            EndDate = year.EndDate,
            IsActive = year.IsActive,
            IsArchived = year.IsArchived,
            TermsCount = 0,
            GroupsCount = 0,
            CreatedAtUtc = year.CreatedAtUtc
        };

        return Created($"/api/teacher/academic-years/{year.Id}", response);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAcademicYear(Guid id, [FromBody] UpdateAcademicYearRequest request)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        if (request.EndDate <= request.StartDate)
        {
            return BadRequest(new { message = "EndDate must be greater than StartDate." });
        }

        var year = await _context.AcademicYears
            .FirstOrDefaultAsync(ay => ay.Id == id && ay.TeacherId == teacherId.Value, HttpContext.RequestAborted);

        if (year == null)
        {
            return NotFound(new { message = "Academic year not found." });
        }

        year.Name = request.Name.Trim();
        year.StartDate = request.StartDate;
        year.EndDate = request.EndDate;

        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        var termsCount = await _context.Terms.CountAsync(t => t.AcademicYearId == id, HttpContext.RequestAborted);
        var groupsCount = await _context.Groups.CountAsync(g => g.AcademicYearId == id, HttpContext.RequestAborted);

        return Ok(new AcademicYearResponseDto
        {
            Id = year.Id,
            Name = year.Name,
            StartDate = year.StartDate,
            EndDate = year.EndDate,
            IsActive = year.IsActive,
            IsArchived = year.IsArchived,
            TermsCount = termsCount,
            GroupsCount = groupsCount,
            CreatedAtUtc = year.CreatedAtUtc
        });
    }

    [HttpPatch("{id:guid}/activate")]
    public async Task<IActionResult> ActivateAcademicYear(Guid id)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var targetYear = await _context.AcademicYears
            .FirstOrDefaultAsync(ay => ay.Id == id && ay.TeacherId == teacherId.Value, HttpContext.RequestAborted);

        if (targetYear == null)
        {
            return NotFound(new { message = "Academic year not found." });
        }

        // Rule BR-ACA-010: Exactly one Academic Year can be active at any given time.
        // Atomically deactivate all other years belonging to this teacher
        var otherActiveYears = await _context.AcademicYears
            .Where(ay => ay.TeacherId == teacherId.Value && ay.Id != id && ay.IsActive)
            .ToListAsync(HttpContext.RequestAborted);

        foreach (var other in otherActiveYears)
        {
            other.IsActive = false;
        }

        targetYear.IsActive = true;
        targetYear.IsArchived = false;

        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(new
        {
            id = targetYear.Id,
            name = targetYear.Name,
            isActive = true,
            message = "Academic year activated successfully."
        });
    }

    [HttpPatch("{id:guid}/archive")]
    public async Task<IActionResult> ArchiveAcademicYear(Guid id)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var year = await _context.AcademicYears
            .FirstOrDefaultAsync(ay => ay.Id == id && ay.TeacherId == teacherId.Value, HttpContext.RequestAborted);

        if (year == null)
        {
            return NotFound(new { message = "Academic year not found." });
        }

        year.IsArchived = true;
        year.IsActive = false; // Cannot remain active if archived

        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(new
        {
            id = year.Id,
            name = year.Name,
            isArchived = true,
            isActive = false,
            message = "Academic year archived successfully."
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAcademicYear(Guid id)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var year = await _context.AcademicYears
            .FirstOrDefaultAsync(ay => ay.Id == id && ay.TeacherId == teacherId.Value, HttpContext.RequestAborted);

        if (year == null)
        {
            return NotFound(new { message = "Academic year not found." });
        }

        // Rule BR-ACA-011: Historical Academic Years cannot be hard-deleted if they contain associated terms, groups, or sessions.
        var hasTerms = await _context.Terms.AnyAsync(t => t.AcademicYearId == id, HttpContext.RequestAborted);
        var hasGroups = await _context.Groups.AnyAsync(g => g.AcademicYearId == id, HttpContext.RequestAborted);

        if (hasTerms || hasGroups)
        {
            return BadRequest(new
            {
                message = "Cannot delete an academic year with associated terms or groups. Please archive it instead to preserve academic history."
            });
        }

        _context.AcademicYears.Remove(year);
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(new { message = "Academic year deleted successfully." });
    }

    // --- Nested Terms Management ---

    [HttpGet("{yearId:guid}/terms")]
    public async Task<IActionResult> GetTermsByYear(Guid yearId)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var isYearOwned = await _ownershipValidator.ValidateAcademicYearOwnershipAsync(yearId, teacherId.Value, HttpContext.RequestAborted);
        if (!isYearOwned)
        {
            return NotFound(new { message = "Academic year not found." });
        }

        var terms = await _context.Terms
            .AsNoTracking()
            .Where(t => t.AcademicYearId == yearId && t.TeacherId == teacherId.Value)
            .OrderBy(t => t.StartDate)
            .Select(t => new TermResponseDto
            {
                Id = t.Id,
                AcademicYearId = t.AcademicYearId,
                Name = t.Name,
                StartDate = t.StartDate,
                EndDate = t.EndDate,
                IsActive = t.IsActive,
                CreatedAtUtc = t.CreatedAtUtc
            })
            .ToListAsync(HttpContext.RequestAborted);

        return Ok(terms);
    }

    [HttpPost("{yearId:guid}/terms")]
    public async Task<IActionResult> CreateTerm(Guid yearId, [FromBody] CreateTermRequest request)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var year = await _context.AcademicYears
            .FirstOrDefaultAsync(ay => ay.Id == yearId && ay.TeacherId == teacherId.Value, HttpContext.RequestAborted);

        if (year == null)
        {
            return NotFound(new { message = "Academic year not found." });
        }

        if (request.EndDate <= request.StartDate)
        {
            return BadRequest(new { message = "EndDate must be greater than StartDate." });
        }

        var term = new Term
        {
            Id = Guid.NewGuid(),
            TeacherId = teacherId.Value,
            AcademicYearId = yearId,
            Name = request.Name.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = request.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.Terms.Add(term);
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        var response = new TermResponseDto
        {
            Id = term.Id,
            AcademicYearId = term.AcademicYearId,
            Name = term.Name,
            StartDate = term.StartDate,
            EndDate = term.EndDate,
            IsActive = term.IsActive,
            CreatedAtUtc = term.CreatedAtUtc
        };

        return Created($"/api/teacher/terms/{term.Id}", response);
    }
}
