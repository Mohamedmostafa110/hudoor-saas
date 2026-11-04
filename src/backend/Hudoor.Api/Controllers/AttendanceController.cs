using Hudoor.Api.Common;
using Hudoor.Api.Data;
using Hudoor.Api.Data.Entities;
using Hudoor.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hudoor.Api.Controllers;

[ApiController]
[Route("api/teacher/attendance")]
[Authorize(Roles = Roles.Teacher)]
public class AttendanceController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<AttendanceController> _logger;

    public AttendanceController(
        ApplicationDbContext context,
        ITenantContext tenantContext,
        ILogger<AttendanceController> logger)
    {
        _context = context;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    [HttpPut("{attendanceRecordId:guid}")]
    public async Task<IActionResult> UpdateAttendanceRecord(Guid attendanceRecordId, [FromBody] UpdateAttendanceRecordRequest request)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var record = await _context.AttendanceRecords
            .Include(r => r.Student)
            .Include(r => r.Session)
            .FirstOrDefaultAsync(r => r.Id == attendanceRecordId && r.TeacherId == teacherId.Value);

        if (record == null)
        {
            return NotFound(new { message = "Attendance record not found." });
        }

        if (record.Session.Status == "Archived")
        {
            return BadRequest(new { message = "Cannot modify attendance records for an archived session." });
        }

        // Rule BR-AUD-081: Reason is mandatory if changing between Absent and Present or if modifying older than 24 hours
        var isStatusFlipped = (record.Status == "Absent" && request.Status == "Present") ||
                              (record.Status == "Present" && request.Status == "Absent");
        var isOlderThan24Hours = (DateTime.UtcNow - record.RecordedAtUtc).TotalHours > 24;

        if ((isStatusFlipped || isOlderThan24Hours) && string.IsNullOrWhiteSpace(request.ModificationReason))
        {
            return BadRequest(new
            {
                message = "A valid modification reason is required when changing attendance between Absent and Present, or when updating records older than 24 hours."
            });
        }

        var previousStatus = record.Status;
        if (previousStatus != request.Status)
        {
            var auditLog = new AttendanceAuditLog
            {
                Id = Guid.NewGuid(),
                AttendanceRecordId = record.Id,
                PreviousStatus = previousStatus,
                NewStatus = request.Status,
                ModifiedByTeacherId = teacherId.Value,
                ModificationReason = request.ModificationReason?.Trim(),
                TimestampUtc = DateTime.UtcNow
            };
            _context.AttendanceAuditLogs.Add(auditLog);
        }

        record.Status = request.Status;
        record.MinutesLate = request.Status == "Late" ? request.MinutesLate : null;
        record.Notes = request.Notes?.Trim();

        await _context.SaveChangesAsync();

        _logger.LogInformation("Attendance record {RecordId} status updated from {OldStatus} to {NewStatus} by Teacher {TeacherId}",
            record.Id, previousStatus, record.Status, teacherId.Value);

        var response = new AttendanceRecordResponseDto
        {
            Id = record.Id,
            SessionId = record.SessionId,
            StudentId = record.StudentId,
            StudentCode = record.Student.StudentCode,
            StudentName = record.Student.FullName,
            Status = record.Status,
            Method = record.Method,
            RecordedAtUtc = record.RecordedAtUtc,
            MinutesLate = record.MinutesLate,
            Notes = record.Notes
        };

        return Ok(response);
    }
}
