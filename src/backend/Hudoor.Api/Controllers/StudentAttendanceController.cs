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
[Route("api/student/attendance")]
[Authorize(Roles = Roles.Student)]
public class StudentAttendanceController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly IQrCodeService _qrCodeService;
    private readonly ILogger<StudentAttendanceController> _logger;

    public StudentAttendanceController(
        ApplicationDbContext context,
        ITenantContext tenantContext,
        IQrCodeService qrCodeService,
        ILogger<StudentAttendanceController> logger)
    {
        _context = context;
        _tenantContext = tenantContext;
        _qrCodeService = qrCodeService;
        _logger = logger;
    }

    [HttpPost("check-in")]
    public async Task<IActionResult> CheckIn([FromBody] StudentCheckInRequest request)
    {
        var studentId = _tenantContext.CurrentUserId;
        if (!studentId.HasValue)
        {
            return Unauthorized(new { message = "Student identity could not be verified." });
        }

        var sessionId = _qrCodeService.ExtractSessionIdFromToken(request.QrToken);
        if (!sessionId.HasValue)
        {
            return BadRequest(new { message = "Invalid QR attendance token format." });
        }

        var session = await _context.Sessions
            .Include(s => s.Group)
                .ThenInclude(g => g.Subject)
            .FirstOrDefaultAsync(s => s.Id == sessionId.Value);

        if (session == null)
        {
            return NotFound(new { message = "Attendance session not found." });
        }

        if (session.Status != "Active")
        {
            return BadRequest(new
            {
                message = $"Attendance check-in is not open for this session. Current session status is '{session.Status}'."
            });
        }

        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.Id == studentId.Value);

        if (student == null || student.TeacherId != session.TeacherId)
        {
            return Forbid();
        }

        // Validate that student is actively enrolled in the session's group
        var isEnrolled = await _context.GroupEnrollments
            .AnyAsync(e => e.GroupId == session.GroupId && e.StudentId == student.Id && e.IsActive);

        if (!isEnrolled)
        {
            return BadRequest(new
            {
                message = "You are not enrolled in the group for this attendance session."
            });
        }

        // Validate QR Token Cryptographic Integrity and Freshness
        var (isValid, errorMessage) = _qrCodeService.ValidateToken(
            request.QrToken, session.Id, session.QrSecuritySalt ?? string.Empty);

        if (!isValid)
        {
            return BadRequest(new { message = errorMessage });
        }

        // Prevent Duplicate Check-In (Rule BR-ATT-072)
        var existingRecord = await _context.AttendanceRecords
            .FirstOrDefaultAsync(r => r.SessionId == session.Id && r.StudentId == student.Id);

        if (existingRecord != null)
        {
            return Conflict(new
            {
                message = "You have already checked in for this attendance session.",
                sessionId = session.Id,
                sessionTitle = session.Title,
                status = existingRecord.Status,
                recordedAtUtc = existingRecord.RecordedAtUtc
            });
        }

        var now = DateTime.UtcNow;
        var attendanceRecord = new AttendanceRecord
        {
            Id = Guid.NewGuid(),
            TeacherId = session.TeacherId,
            SessionId = session.Id,
            StudentId = student.Id,
            Status = "Present",
            Method = "QRCode",
            RecordedAtUtc = now
        };

        _context.AttendanceRecords.Add(attendanceRecord);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Student {StudentCode} ({StudentId}) checked into Session {SessionId} via QR Code",
            student.StudentCode, student.Id, session.Id);

        return Ok(new StudentCheckInResponseDto
        {
            SessionId = session.Id,
            SessionTitle = session.Title,
            GroupName = session.Group.Name,
            SubjectName = session.Group.Subject.Name,
            RecordedAtUtc = now,
            Status = attendanceRecord.Status,
            Method = attendanceRecord.Method,
            Message = "Attendance recorded successfully! You are marked Present."
        });
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetAttendanceHistory(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var studentId = _tenantContext.CurrentUserId;
        if (!studentId.HasValue)
        {
            return Unauthorized(new { message = "Student identity could not be verified." });
        }

        var query = _context.AttendanceRecords
            .AsNoTracking()
            .Include(r => r.Session)
                .ThenInclude(s => s.Group)
                    .ThenInclude(g => g.Subject)
            .Where(r => r.StudentId == studentId.Value);

        if (fromDate.HasValue)
        {
            query = query.Where(r => r.Session.SessionDateUtc >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(r => r.Session.SessionDateUtc <= toDate.Value);
        }

        var history = await query
            .OrderByDescending(r => r.Session.SessionDateUtc)
            .Select(r => new StudentAttendanceHistoryDto
            {
                AttendanceRecordId = r.Id,
                SessionId = r.SessionId,
                SessionTitle = r.Session.Title,
                GroupName = r.Session.Group.Name,
                SubjectName = r.Session.Group.Subject.Name,
                SessionDateUtc = r.Session.SessionDateUtc,
                Status = r.Status,
                Method = r.Method,
                MinutesLate = r.MinutesLate,
                RecordedAtUtc = r.RecordedAtUtc
            })
            .ToListAsync();

        return Ok(history);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetAttendanceSummary()
    {
        var studentId = _tenantContext.CurrentUserId;
        if (!studentId.HasValue)
        {
            return Unauthorized(new { message = "Student identity could not be verified." });
        }

        var records = await _context.AttendanceRecords
            .AsNoTracking()
            .Where(r => r.StudentId == studentId.Value)
            .ToListAsync();

        var totalSessions = records.Count;
        var presentCount = records.Count(r => r.Status == "Present");
        var lateCount = records.Count(r => r.Status == "Late");
        var excusedCount = records.Count(r => r.Status == "Excused");
        var absentCount = records.Count(r => r.Status == "Absent");

        var percentage = totalSessions > 0
            ? Math.Round((double)(presentCount + lateCount) / totalSessions * 100.0, 1)
            : 100.0;

        return Ok(new StudentAttendanceSummaryDto
        {
            TotalSessions = totalSessions,
            PresentCount = presentCount,
            LateCount = lateCount,
            ExcusedCount = excusedCount,
            AbsentCount = absentCount,
            AttendancePercentage = percentage
        });
    }
}
