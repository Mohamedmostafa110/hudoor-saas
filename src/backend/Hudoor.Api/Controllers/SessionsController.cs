using System.Security.Cryptography;
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
[Route("api/teacher/sessions")]
[Authorize(Roles = Roles.Teacher)]
public class SessionsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly ITenantOwnershipValidator _ownershipValidator;
    private readonly IQrCodeService _qrCodeService;
    private readonly ILogger<SessionsController> _logger;

    public SessionsController(
        ApplicationDbContext context,
        ITenantContext tenantContext,
        ITenantOwnershipValidator ownershipValidator,
        IQrCodeService qrCodeService,
        ILogger<SessionsController> logger)
    {
        _context = context;
        _tenantContext = tenantContext;
        _ownershipValidator = ownershipValidator;
        _qrCodeService = qrCodeService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetSessions(
        [FromQuery] Guid? groupId = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var query = _context.Sessions
            .AsNoTracking()
            .Where(s => s.TeacherId == teacherId.Value);

        if (groupId.HasValue)
        {
            query = query.Where(s => s.GroupId == groupId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var trimmedStatus = status.Trim();
            query = query.Where(s => s.Status == trimmedStatus);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(s => s.SessionDateUtc >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(s => s.SessionDateUtc <= toDate.Value);
        }

        var sessions = await query
            .OrderByDescending(s => s.SessionDateUtc)
            .Select(s => new SessionSummaryDto
            {
                Id = s.Id,
                GroupId = s.GroupId,
                GroupName = s.Group.Name,
                SubjectName = s.Group.Subject.Name,
                AcademicYearName = s.Group.AcademicYear.Name,
                Title = s.Title,
                SessionDateUtc = s.SessionDateUtc,
                Status = s.Status,
                CreatedAtUtc = s.CreatedAtUtc,
                EnrolledStudentsCount = s.Group.Enrollments.Count(e => e.IsActive),
                PresentCount = s.AttendanceRecords.Count(r => r.Status == "Present"),
                LateCount = s.AttendanceRecords.Count(r => r.Status == "Late"),
                ExcusedCount = s.AttendanceRecords.Count(r => r.Status == "Excused"),
                AbsentCount = s.AttendanceRecords.Count(r => r.Status == "Absent"),
                UnrecordedCount = Math.Max(0, s.Group.Enrollments.Count(e => e.IsActive) - s.AttendanceRecords.Count)
            })
            .ToListAsync();

        return Ok(sessions);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetSessionById(Guid id)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var session = await _context.Sessions
            .AsNoTracking()
            .Include(s => s.Group)
                .ThenInclude(g => g.Subject)
            .Include(s => s.Group)
                .ThenInclude(g => g.AcademicYear)
            .Include(s => s.Group)
                .ThenInclude(g => g.Enrollments)
            .Include(s => s.AttendanceRecords)
            .FirstOrDefaultAsync(s => s.Id == id && s.TeacherId == teacherId.Value);

        if (session == null)
        {
            return NotFound(new { message = "Attendance session not found." });
        }

        var enrolledCount = session.Group.Enrollments.Count(e => e.IsActive);
        var result = new SessionDetailDto
        {
            Id = session.Id,
            GroupId = session.GroupId,
            GroupName = session.Group.Name,
            SubjectName = session.Group.Subject.Name,
            AcademicYearName = session.Group.AcademicYear.Name,
            Title = session.Title,
            SessionDateUtc = session.SessionDateUtc,
            Status = session.Status,
            CreatedAtUtc = session.CreatedAtUtc,
            EnrolledStudentsCount = enrolledCount,
            PresentCount = session.AttendanceRecords.Count(r => r.Status == "Present"),
            LateCount = session.AttendanceRecords.Count(r => r.Status == "Late"),
            ExcusedCount = session.AttendanceRecords.Count(r => r.Status == "Excused"),
            AbsentCount = session.AttendanceRecords.Count(r => r.Status == "Absent"),
            UnrecordedCount = Math.Max(0, enrolledCount - session.AttendanceRecords.Count)
        };

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateSession([FromBody] CreateSessionRequest request)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var isGroupOwned = await _ownershipValidator.ValidateGroupOwnershipAsync(request.GroupId, teacherId.Value);
        if (!isGroupOwned)
        {
            return NotFound(new { message = "Group not found." });
        }

        var group = await _context.Groups
            .Include(g => g.Subject)
            .Include(g => g.AcademicYear)
            .Include(g => g.Enrollments.Where(e => e.IsActive))
            .FirstOrDefaultAsync(g => g.Id == request.GroupId && g.TeacherId == teacherId.Value);

        if (group == null)
        {
            return NotFound(new { message = "Group not found." });
        }

        if (group.IsArchived)
        {
            return BadRequest(new { message = $"Cannot create attendance session for archived group '{group.Name}'." });
        }

        // Generate cryptographic salt for dynamic rotating QR check-in
        Span<byte> saltBytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(saltBytes);
        var qrSecuritySalt = Convert.ToHexString(saltBytes);

        var session = new Session
        {
            Id = Guid.NewGuid(),
            TeacherId = teacherId.Value,
            GroupId = group.Id,
            Title = request.Title.Trim(),
            SessionDateUtc = request.SessionDateUtc ?? DateTime.UtcNow,
            Status = request.Status ?? "Active",
            QrSecuritySalt = qrSecuritySalt,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.Sessions.Add(session);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Session {SessionId} ('{Title}') created for Group {GroupId} by Teacher {TeacherId}",
            session.Id, session.Title, group.Id, teacherId.Value);

        var response = new SessionDetailDto
        {
            Id = session.Id,
            GroupId = session.GroupId,
            GroupName = group.Name,
            SubjectName = group.Subject.Name,
            AcademicYearName = group.AcademicYear.Name,
            Title = session.Title,
            SessionDateUtc = session.SessionDateUtc,
            Status = session.Status,
            CreatedAtUtc = session.CreatedAtUtc,
            EnrolledStudentsCount = group.Enrollments.Count,
            PresentCount = 0,
            LateCount = 0,
            ExcusedCount = 0,
            AbsentCount = 0,
            UnrecordedCount = group.Enrollments.Count
        };

        return CreatedAtAction(nameof(GetSessionById), new { id = session.Id }, response);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateSession(Guid id, [FromBody] UpdateSessionRequest request)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var session = await _context.Sessions
            .Include(s => s.Group)
                .ThenInclude(g => g.Subject)
            .Include(s => s.Group)
                .ThenInclude(g => g.AcademicYear)
            .Include(s => s.Group)
                .ThenInclude(g => g.Enrollments)
            .Include(s => s.AttendanceRecords)
            .FirstOrDefaultAsync(s => s.Id == id && s.TeacherId == teacherId.Value);

        if (session == null)
        {
            return NotFound(new { message = "Attendance session not found." });
        }

        if (session.Status == "Archived")
        {
            return BadRequest(new { message = "Cannot modify an archived session." });
        }

        session.Title = request.Title.Trim();
        session.SessionDateUtc = request.SessionDateUtc;

        await _context.SaveChangesAsync();

        var enrolledCount = session.Group.Enrollments.Count(e => e.IsActive);
        var response = new SessionDetailDto
        {
            Id = session.Id,
            GroupId = session.GroupId,
            GroupName = session.Group.Name,
            SubjectName = session.Group.Subject.Name,
            AcademicYearName = session.Group.AcademicYear.Name,
            Title = session.Title,
            SessionDateUtc = session.SessionDateUtc,
            Status = session.Status,
            CreatedAtUtc = session.CreatedAtUtc,
            EnrolledStudentsCount = enrolledCount,
            PresentCount = session.AttendanceRecords.Count(r => r.Status == "Present"),
            LateCount = session.AttendanceRecords.Count(r => r.Status == "Late"),
            ExcusedCount = session.AttendanceRecords.Count(r => r.Status == "Excused"),
            AbsentCount = session.AttendanceRecords.Count(r => r.Status == "Absent"),
            UnrecordedCount = Math.Max(0, enrolledCount - session.AttendanceRecords.Count)
        };

        return Ok(response);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateSessionStatus(Guid id, [FromBody] UpdateSessionStatusRequest request)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var session = await _context.Sessions
            .Include(s => s.AttendanceRecords)
            .FirstOrDefaultAsync(s => s.Id == id && s.TeacherId == teacherId.Value);

        if (session == null)
        {
            return NotFound(new { message = "Attendance session not found." });
        }

        var oldStatus = session.Status;
        var newStatus = request.Status.Trim();

        if (oldStatus == "Archived" && newStatus != "Archived")
        {
            return BadRequest(new { message = "Cannot re-open an archived session." });
        }

        // Rule BR-ATT-061: When closing an Active session, auto-reconcile all unrecorded enrolled students as Absent
        int autoMarkedAbsentCount = 0;
        if (oldStatus == "Active" && newStatus == "Closed")
        {
            var enrolledStudentIds = await _context.GroupEnrollments
                .Where(e => e.GroupId == session.GroupId && e.IsActive && e.TeacherId == teacherId.Value)
                .Select(e => e.StudentId)
                .ToListAsync();

            var recordedStudentIds = session.AttendanceRecords
                .Select(r => r.StudentId)
                .ToHashSet();

            var missingStudentIds = enrolledStudentIds
                .Where(studentId => !recordedStudentIds.Contains(studentId))
                .ToList();

            var now = DateTime.UtcNow;
            foreach (var studentId in missingStudentIds)
            {
                var absentRecord = new AttendanceRecord
                {
                    Id = Guid.NewGuid(),
                    TeacherId = teacherId.Value,
                    SessionId = session.Id,
                    StudentId = studentId,
                    Status = "Absent",
                    Method = "Manual",
                    RecordedAtUtc = now,
                    Notes = "Auto-marked Absent upon session close."
                };
                _context.AttendanceRecords.Add(absentRecord);
                autoMarkedAbsentCount++;
            }
        }

        session.Status = newStatus;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Session {SessionId} status changed from {OldStatus} to {NewStatus} (AutoMarkedAbsent: {AbsentCount})",
            session.Id, oldStatus, newStatus, autoMarkedAbsentCount);

        return Ok(new
        {
            id = session.Id,
            previousStatus = oldStatus,
            status = session.Status,
            autoMarkedAbsentCount,
            message = $"Session status updated to '{session.Status}'."
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteSession(Guid id)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var session = await _context.Sessions
            .Include(s => s.AttendanceRecords)
            .FirstOrDefaultAsync(s => s.Id == id && s.TeacherId == teacherId.Value);

        if (session == null)
        {
            return NotFound(new { message = "Attendance session not found." });
        }

        if (session.AttendanceRecords.Any())
        {
            return BadRequest(new
            {
                message = "Cannot delete session with existing attendance records. Please archive the session instead to preserve historical records."
            });
        }

        _context.Sessions.Remove(session);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("{id:guid}/qr-token")]
    public async Task<IActionResult> GetSessionQrToken(Guid id)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var session = await _context.Sessions
            .FirstOrDefaultAsync(s => s.Id == id && s.TeacherId == teacherId.Value);

        if (session == null)
        {
            return NotFound(new { message = "Attendance session not found." });
        }

        if (session.Status != "Active")
        {
            return BadRequest(new { message = $"QR check-in is only available for Active sessions. Current status is '{session.Status}'." });
        }

        if (string.IsNullOrEmpty(session.QrSecuritySalt))
        {
            Span<byte> saltBytes = stackalloc byte[32];
            RandomNumberGenerator.Fill(saltBytes);
            session.QrSecuritySalt = Convert.ToHexString(saltBytes);
            await _context.SaveChangesAsync();
        }

        var (token, remainingSeconds, expiresAtUtc) = _qrCodeService.GenerateToken(session.Id, session.QrSecuritySalt);

        return Ok(new SessionQrTokenDto
        {
            SessionId = session.Id,
            QrToken = token,
            RemainingSeconds = remainingSeconds,
            ExpiresAtUtc = expiresAtUtc
        });
    }

    [HttpGet("{id:guid}/roster")]
    public async Task<IActionResult> GetSessionRoster(Guid id)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var session = await _context.Sessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id && s.TeacherId == teacherId.Value);

        if (session == null)
        {
            return NotFound(new { message = "Attendance session not found." });
        }

        // Get all active students enrolled in the session's group
        var enrollments = await _context.GroupEnrollments
            .AsNoTracking()
            .Include(e => e.Student)
            .Where(e => e.GroupId == session.GroupId && e.IsActive && e.TeacherId == teacherId.Value)
            .OrderBy(e => e.Student.FullName)
            .ToListAsync();

        // Get existing attendance records for this session
        var records = await _context.AttendanceRecords
            .AsNoTracking()
            .Where(r => r.SessionId == session.Id && r.TeacherId == teacherId.Value)
            .ToDictionaryAsync(r => r.StudentId);

        var roster = enrollments.Select(e =>
        {
            records.TryGetValue(e.StudentId, out var record);
            return new SessionRosterStudentDto
            {
                StudentId = e.Student.Id,
                StudentCode = e.Student.StudentCode,
                FullName = e.Student.FullName,
                PhoneNumber = e.Student.PhoneNumber,
                AttendanceRecordId = record?.Id,
                Status = record?.Status ?? "Unrecorded",
                Method = record?.Method,
                RecordedAtUtc = record?.RecordedAtUtc,
                MinutesLate = record?.MinutesLate,
                Notes = record?.Notes
            };
        }).ToList();

        return Ok(roster);
    }

    [HttpPost("{id:guid}/attendance")]
    public async Task<IActionResult> RecordStudentAttendance(Guid id, [FromBody] RecordAttendanceRequest request)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var session = await _context.Sessions
            .FirstOrDefaultAsync(s => s.Id == id && s.TeacherId == teacherId.Value);

        if (session == null)
        {
            return NotFound(new { message = "Attendance session not found." });
        }

        if (session.Status == "Archived")
        {
            return BadRequest(new { message = "Cannot record attendance for an archived session." });
        }

        // Validate student belongs to teacher and is enrolled in group
        var isEnrolled = await _context.GroupEnrollments
            .AnyAsync(e => e.GroupId == session.GroupId && e.StudentId == request.StudentId && e.IsActive && e.TeacherId == teacherId.Value);

        if (!isEnrolled)
        {
            return BadRequest(new { message = "Student is not actively enrolled in this session's group." });
        }

        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.Id == request.StudentId && s.TeacherId == teacherId.Value);

        if (student == null)
        {
            return NotFound(new { message = "Student not found." });
        }

        var existingRecord = await _context.AttendanceRecords
            .FirstOrDefaultAsync(r => r.SessionId == session.Id && r.StudentId == request.StudentId && r.TeacherId == teacherId.Value);

        if (existingRecord != null)
        {
            // Status modification: log to AttendanceAuditLog if status changed
            if (existingRecord.Status != request.Status)
            {
                var auditLog = new AttendanceAuditLog
                {
                    Id = Guid.NewGuid(),
                    AttendanceRecordId = existingRecord.Id,
                    PreviousStatus = existingRecord.Status,
                    NewStatus = request.Status,
                    ModifiedByTeacherId = teacherId.Value,
                    ModificationReason = request.Notes,
                    TimestampUtc = DateTime.UtcNow
                };
                _context.AttendanceAuditLogs.Add(auditLog);
            }

            existingRecord.Status = request.Status;
            existingRecord.MinutesLate = request.Status == "Late" ? request.MinutesLate : null;
            existingRecord.Notes = request.Notes?.Trim();
            existingRecord.RecordedAtUtc = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new AttendanceRecordResponseDto
            {
                Id = existingRecord.Id,
                SessionId = session.Id,
                StudentId = student.Id,
                StudentCode = student.StudentCode,
                StudentName = student.FullName,
                Status = existingRecord.Status,
                Method = existingRecord.Method,
                RecordedAtUtc = existingRecord.RecordedAtUtc,
                MinutesLate = existingRecord.MinutesLate,
                Notes = existingRecord.Notes
            });
        }
        else
        {
            // Create new record
            var newRecord = new AttendanceRecord
            {
                Id = Guid.NewGuid(),
                TeacherId = teacherId.Value,
                SessionId = session.Id,
                StudentId = student.Id,
                Status = request.Status,
                Method = "Manual",
                RecordedAtUtc = DateTime.UtcNow,
                MinutesLate = request.Status == "Late" ? request.MinutesLate : null,
                Notes = request.Notes?.Trim()
            };

            _context.AttendanceRecords.Add(newRecord);
            await _context.SaveChangesAsync();

            return StatusCode(StatusCodes.Status201Created, new AttendanceRecordResponseDto
            {
                Id = newRecord.Id,
                SessionId = session.Id,
                StudentId = student.Id,
                StudentCode = student.StudentCode,
                StudentName = student.FullName,
                Status = newRecord.Status,
                Method = newRecord.Method,
                RecordedAtUtc = newRecord.RecordedAtUtc,
                MinutesLate = newRecord.MinutesLate,
                Notes = newRecord.Notes
            });
        }
    }

    [HttpPost("{id:guid}/attendance/bulk")]
    public async Task<IActionResult> BulkRecordAttendance(Guid id, [FromBody] BulkRecordAttendanceRequest request)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var session = await _context.Sessions
            .FirstOrDefaultAsync(s => s.Id == id && s.TeacherId == teacherId.Value);

        if (session == null)
        {
            return NotFound(new { message = "Attendance session not found." });
        }

        if (session.Status == "Archived")
        {
            return BadRequest(new { message = "Cannot record attendance for an archived session." });
        }

        var enrolledStudentIds = await _context.GroupEnrollments
            .Where(e => e.GroupId == session.GroupId && e.IsActive && e.TeacherId == teacherId.Value)
            .Select(e => e.StudentId)
            .ToHashSetAsync();

        var existingRecords = await _context.AttendanceRecords
            .Where(r => r.SessionId == session.Id && r.TeacherId == teacherId.Value)
            .ToDictionaryAsync(r => r.StudentId);

        var now = DateTime.UtcNow;
        int createdCount = 0;
        int updatedCount = 0;

        foreach (var item in request.Records)
        {
            if (!enrolledStudentIds.Contains(item.StudentId))
            {
                continue; // Skip students not enrolled in this group
            }

            if (existingRecords.TryGetValue(item.StudentId, out var existing))
            {
                if (existing.Status != item.Status)
                {
                    var auditLog = new AttendanceAuditLog
                    {
                        Id = Guid.NewGuid(),
                        AttendanceRecordId = existing.Id,
                        PreviousStatus = existing.Status,
                        NewStatus = item.Status,
                        ModifiedByTeacherId = teacherId.Value,
                        ModificationReason = item.Notes ?? "Bulk attendance update",
                        TimestampUtc = now
                    };
                    _context.AttendanceAuditLogs.Add(auditLog);
                }

                existing.Status = item.Status;
                existing.MinutesLate = item.Status == "Late" ? item.MinutesLate : null;
                existing.Notes = item.Notes?.Trim();
                existing.RecordedAtUtc = now;
                updatedCount++;
            }
            else
            {
                var newRecord = new AttendanceRecord
                {
                    Id = Guid.NewGuid(),
                    TeacherId = teacherId.Value,
                    SessionId = session.Id,
                    StudentId = item.StudentId,
                    Status = item.Status,
                    Method = "Manual",
                    RecordedAtUtc = now,
                    MinutesLate = item.Status == "Late" ? item.MinutesLate : null,
                    Notes = item.Notes?.Trim()
                };
                _context.AttendanceRecords.Add(newRecord);
                existingRecords[item.StudentId] = newRecord;
                createdCount++;
            }
        }

        if (request.MarkRemainingAsAbsent)
        {
            foreach (var studentId in enrolledStudentIds)
            {
                if (!existingRecords.ContainsKey(studentId))
                {
                    var absentRecord = new AttendanceRecord
                    {
                        Id = Guid.NewGuid(),
                        TeacherId = teacherId.Value,
                        SessionId = session.Id,
                        StudentId = studentId,
                        Status = "Absent",
                        Method = "Manual",
                        RecordedAtUtc = now,
                        Notes = "Marked absent via bulk attendance submission."
                    };
                    _context.AttendanceRecords.Add(absentRecord);
                    createdCount++;
                }
            }
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            sessionId = session.Id,
            createdCount,
            updatedCount,
            totalProcessed = createdCount + updatedCount,
            message = "Bulk attendance processed successfully."
        });
    }

    [HttpGet("{id:guid}/audit-logs")]
    public async Task<IActionResult> GetSessionAuditLogs(Guid id)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var isSessionOwned = await _context.Sessions
            .AsNoTracking()
            .AnyAsync(s => s.Id == id && s.TeacherId == teacherId.Value);

        if (!isSessionOwned)
        {
            return NotFound(new { message = "Attendance session not found." });
        }

        var logs = await _context.AttendanceAuditLogs
            .AsNoTracking()
            .Where(log => log.AttendanceRecord.SessionId == id && log.AttendanceRecord.TeacherId == teacherId.Value)
            .OrderByDescending(log => log.TimestampUtc)
            .Select(log => new AttendanceAuditLogDto
            {
                Id = log.Id,
                AttendanceRecordId = log.AttendanceRecordId,
                PreviousStatus = log.PreviousStatus,
                NewStatus = log.NewStatus,
                ModifiedByTeacherId = log.ModifiedByTeacherId,
                ModificationReason = log.ModificationReason,
                TimestampUtc = log.TimestampUtc
            })
            .ToListAsync();

        return Ok(logs);
    }
}
