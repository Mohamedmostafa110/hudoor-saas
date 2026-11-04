using Hudoor.Api.Common;
using Hudoor.Api.Data;
using Hudoor.Api.DTOs;
using Hudoor.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hudoor.Api.Controllers;

[ApiController]
[Route("api/teacher/terms")]
[Authorize(Roles = Roles.Teacher)]
public class TermsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly ITenantOwnershipValidator _ownershipValidator;

    public TermsController(
        ApplicationDbContext context,
        ITenantContext tenantContext,
        ITenantOwnershipValidator ownershipValidator)
    {
        _context = context;
        _tenantContext = tenantContext;
        _ownershipValidator = ownershipValidator;
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateTerm(Guid id, [FromBody] UpdateTermRequest request)
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

        var term = await _context.Terms
            .FirstOrDefaultAsync(t => t.Id == id && t.TeacherId == teacherId.Value, HttpContext.RequestAborted);

        if (term == null)
        {
            return NotFound(new { message = "Term not found." });
        }

        term.Name = request.Name.Trim();
        term.StartDate = request.StartDate;
        term.EndDate = request.EndDate;

        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(new TermResponseDto
        {
            Id = term.Id,
            AcademicYearId = term.AcademicYearId,
            Name = term.Name,
            StartDate = term.StartDate,
            EndDate = term.EndDate,
            IsActive = term.IsActive,
            CreatedAtUtc = term.CreatedAtUtc
        });
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateTermStatus(Guid id, [FromBody] UpdateTermStatusRequest request)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var term = await _context.Terms
            .FirstOrDefaultAsync(t => t.Id == id && t.TeacherId == teacherId.Value, HttpContext.RequestAborted);

        if (term == null)
        {
            return NotFound(new { message = "Term not found." });
        }

        term.IsActive = request.IsActive;
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(new
        {
            id = term.Id,
            name = term.Name,
            isActive = term.IsActive,
            message = term.IsActive ? "Term activated." : "Term deactivated."
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteTerm(Guid id)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var term = await _context.Terms
            .FirstOrDefaultAsync(t => t.Id == id && t.TeacherId == teacherId.Value, HttpContext.RequestAborted);

        if (term == null)
        {
            return NotFound(new { message = "Term not found." });
        }

        _context.Terms.Remove(term);
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(new { message = "Term deleted successfully." });
    }
}
