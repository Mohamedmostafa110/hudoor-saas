using Hudoor.Api.Common;
using Hudoor.Api.Data;
using Hudoor.Api.DTOs;
using Hudoor.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hudoor.Api.Controllers;

[ApiController]
[Route("api/teacher/reports")]
[Authorize(Roles = Roles.Teacher)]
public class ReportsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly ITenantOwnershipValidator _ownershipValidator;
    private readonly IExportService _exportService;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(
        ApplicationDbContext context,
        ITenantContext tenantContext,
        ITenantOwnershipValidator ownershipValidator,
        IExportService exportService,
        ILogger<ReportsController> logger)
    {
        _context = context;
        _tenantContext = tenantContext;
        _ownershipValidator = ownershipValidator;
        _exportService = exportService;
        _logger = logger;
    }

    [HttpGet("analytics")]
    public async Task<IActionResult> GetTeacherAnalytics()
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var totalStudents = await _context.Students
            .CountAsync(s => s.TeacherId == teacherId.Value);

        var activeStudents = await _context.Students
            .CountAsync(s => s.TeacherId == teacherId.Value && s.IsActive);

        var groups = await _context.Groups
            .AsNoTracking()
            .Include(g => g.Subject)
            .Include(g => g.Enrollments.Where(e => e.IsActive))
            .Include(g => g.Sessions)
                .ThenInclude(s => s.AttendanceRecords)
            .Where(g => g.TeacherId == teacherId.Value && !g.IsArchived)
            .ToListAsync();

        int totalSessions = 0;
        int totalPresent = 0;
        int totalLate = 0;
        int totalExcused = 0;
        int totalAbsent = 0;

        var groupSummaries = new List<GroupAttendanceSummaryDto>();

        foreach (var group in groups)
        {
            var sessions = group.Sessions.ToList();
            totalSessions += sessions.Count;

            var records = sessions.SelectMany(s => s.AttendanceRecords).ToList();
            var present = records.Count(r => r.Status == "Present");
            var late = records.Count(r => r.Status == "Late");
            var excused = records.Count(r => r.Status == "Excused");
            var absent = records.Count(r => r.Status == "Absent");

            totalPresent += present;
            totalLate += late;
            totalExcused += excused;
            totalAbsent += absent;

            var groupTotalRecords = records.Count;
            var groupRate = groupTotalRecords > 0
                ? Math.Round((double)(present + late) / groupTotalRecords * 100.0, 1)
                : 100.0;

            groupSummaries.Add(new GroupAttendanceSummaryDto
            {
                GroupId = group.Id,
                GroupName = group.Name,
                SubjectName = group.Subject.Name,
                EnrolledStudentsCount = group.Enrollments.Count,
                TotalSessionsCount = sessions.Count,
                AttendanceRate = groupRate,
                PresentCount = present,
                LateCount = late,
                ExcusedCount = excused,
                AbsentCount = absent
            });
        }

        var grandTotal = totalPresent + totalLate + totalExcused + totalAbsent;
        var overallRate = grandTotal > 0
            ? Math.Round((double)(totalPresent + totalLate) / grandTotal * 100.0, 1)
            : 100.0;

        // Count at-risk students (< 75% attendance or >= 3 absences)
        var studentRecords = await _context.AttendanceRecords
            .AsNoTracking()
            .Where(r => r.TeacherId == teacherId.Value)
            .GroupBy(r => r.StudentId)
            .Select(g => new
            {
                StudentId = g.Key,
                Total = g.Count(),
                PresentAndLate = g.Count(r => r.Status == "Present" || r.Status == "Late"),
                Absent = g.Count(r => r.Status == "Absent")
            })
            .ToListAsync();

        var atRiskCount = studentRecords.Count(s =>
            (s.Total > 0 && ((double)s.PresentAndLate / s.Total * 100.0) < 75.0) || s.Absent >= 3);

        var result = new TeacherAnalyticsSummaryDto
        {
            TotalStudents = totalStudents,
            ActiveStudents = activeStudents,
            TotalGroups = groups.Count,
            TotalSessions = totalSessions,
            OverallAttendanceRate = overallRate,
            TotalPresentCount = totalPresent,
            TotalLateCount = totalLate,
            TotalExcusedCount = totalExcused,
            TotalAbsentCount = totalAbsent,
            AtRiskStudentsCount = atRiskCount,
            GroupSummaries = groupSummaries
        };

        return Ok(result);
    }

    [HttpGet("at-risk")]
    public async Task<IActionResult> GetAtRiskStudents(
        [FromQuery] double threshold = 75.0,
        [FromQuery] int minAbsences = 3,
        [FromQuery] Guid? groupId = null)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var query = _context.Students
            .AsNoTracking()
            .Include(s => s.Enrollments.Where(e => e.IsActive))
                .ThenInclude(e => e.Group)
            .Include(s => s.AttendanceRecords)
            .Where(s => s.TeacherId == teacherId.Value && s.IsActive);

        if (groupId.HasValue)
        {
            query = query.Where(s => s.Enrollments.Any(e => e.GroupId == groupId.Value && e.IsActive));
        }

        var students = await query.ToListAsync();
        var atRiskList = new List<AtRiskStudentDto>();

        foreach (var student in students)
        {
            var records = student.AttendanceRecords.ToList();
            if (groupId.HasValue)
            {
                var groupSessionIds = await _context.Sessions
                    .Where(sess => sess.GroupId == groupId.Value)
                    .Select(sess => sess.Id)
                    .ToListAsync();
                records = records.Where(r => groupSessionIds.Contains(r.SessionId)).ToList();
            }

            var total = records.Count;
            var attended = records.Count(r => r.Status == "Present" || r.Status == "Late");
            var absent = records.Count(r => r.Status == "Absent");

            var rate = total > 0 ? Math.Round((double)attended / total * 100.0, 1) : 100.0;

            if ((total > 0 && rate < threshold) || absent >= minAbsences)
            {
                var primaryEnrollment = student.Enrollments.FirstOrDefault();
                var lastAttended = records
                    .Where(r => r.Status == "Present" || r.Status == "Late")
                    .OrderByDescending(r => r.RecordedAtUtc)
                    .Select(r => (DateTime?)r.RecordedAtUtc)
                    .FirstOrDefault();

                atRiskList.Add(new AtRiskStudentDto
                {
                    StudentId = student.Id,
                    StudentCode = student.StudentCode,
                    FullName = student.FullName,
                    PhoneNumber = student.PhoneNumber,
                    ParentPhoneNumber = student.ParentPhoneNumber,
                    CurrentGrade = student.CurrentGrade,
                    GroupName = primaryEnrollment?.Group.Name ?? "Unassigned",
                    TotalExpectedSessions = total,
                    AttendedSessions = attended,
                    AbsentSessions = absent,
                    AttendanceRate = rate,
                    LastAttendedDateUtc = lastAttended
                });
            }
        }

        atRiskList = atRiskList.OrderBy(s => s.AttendanceRate).ToList();
        return Ok(atRiskList);
    }

    [HttpGet("groups/{groupId:guid}/matrix")]
    public async Task<IActionResult> GetGroupAttendanceMatrix(
        Guid groupId,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
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

        var group = await _context.Groups
            .AsNoTracking()
            .Include(g => g.Subject)
            .FirstOrDefaultAsync(g => g.Id == groupId && g.TeacherId == teacherId.Value);

        if (group == null)
        {
            return NotFound(new { message = "Group not found." });
        }

        var sessionsQuery = _context.Sessions
            .AsNoTracking()
            .Where(s => s.GroupId == groupId && s.TeacherId == teacherId.Value);

        if (fromDate.HasValue)
        {
            sessionsQuery = sessionsQuery.Where(s => s.SessionDateUtc >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            sessionsQuery = sessionsQuery.Where(s => s.SessionDateUtc <= toDate.Value);
        }

        var sessions = await sessionsQuery
            .OrderBy(s => s.SessionDateUtc)
            .Select(s => new MatrixSessionHeaderDto
            {
                SessionId = s.Id,
                Title = s.Title,
                SessionDateUtc = s.SessionDateUtc
            })
            .ToListAsync();

        var sessionIds = sessions.Select(s => s.SessionId).ToList();

        var enrollments = await _context.GroupEnrollments
            .AsNoTracking()
            .Include(e => e.Student)
            .Where(e => e.GroupId == groupId && e.TeacherId == teacherId.Value && e.IsActive)
            .OrderBy(e => e.Student.FullName)
            .ToListAsync();

        var records = await _context.AttendanceRecords
            .AsNoTracking()
            .Where(r => sessionIds.Contains(r.SessionId) && r.TeacherId == teacherId.Value)
            .ToListAsync();

        var recordsByStudent = records.GroupBy(r => r.StudentId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(r => r.SessionId, r => r.Status));

        var studentRows = new List<MatrixStudentRowDto>();

        foreach (var enrollment in enrollments)
        {
            var student = enrollment.Student;
            recordsByStudent.TryGetValue(student.Id, out var studentRecordsMap);
            studentRecordsMap ??= new Dictionary<Guid, string>();

            int present = 0;
            int late = 0;
            int excused = 0;
            int absent = 0;

            var statusMap = new Dictionary<Guid, string>();
            foreach (var session in sessions)
            {
                if (studentRecordsMap.TryGetValue(session.SessionId, out var status))
                {
                    statusMap[session.SessionId] = status;
                    switch (status)
                    {
                        case "Present": present++; break;
                        case "Late": late++; break;
                        case "Excused": excused++; break;
                        case "Absent": absent++; break;
                    }
                }
                else
                {
                    statusMap[session.SessionId] = "-"; // unrecorded
                }
            }

            var totalRecorded = present + late + excused + absent;
            var rate = totalRecorded > 0
                ? Math.Round((double)(present + late) / totalRecorded * 100.0, 1)
                : 100.0;

            studentRows.Add(new MatrixStudentRowDto
            {
                StudentId = student.Id,
                StudentCode = student.StudentCode,
                FullName = student.FullName,
                SessionStatuses = statusMap,
                TotalPresent = present,
                TotalLate = late,
                TotalExcused = excused,
                TotalAbsent = absent,
                AttendanceRate = rate
            });
        }

        var result = new GroupAttendanceMatrixDto
        {
            GroupId = group.Id,
            GroupName = group.Name,
            SubjectName = group.Subject.Name,
            Sessions = sessions,
            Students = studentRows
        };

        return Ok(result);
    }

    [HttpGet("export/group-attendance")]
    public async Task<IActionResult> ExportGroupAttendance(
        [FromQuery] Guid groupId,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
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

        var group = await _context.Groups
            .Include(g => g.Subject)
            .FirstOrDefaultAsync(g => g.Id == groupId && g.TeacherId == teacherId.Value);

        if (group == null)
        {
            return NotFound(new { message = "Group not found." });
        }

        var sessionsQuery = _context.Sessions.Where(s => s.GroupId == groupId && s.TeacherId == teacherId.Value);
        if (fromDate.HasValue) sessionsQuery = sessionsQuery.Where(s => s.SessionDateUtc >= fromDate.Value);
        if (toDate.HasValue) sessionsQuery = sessionsQuery.Where(s => s.SessionDateUtc <= toDate.Value);
        var sessionIds = await sessionsQuery.Select(s => s.Id).ToListAsync();

        var enrollments = await _context.GroupEnrollments
            .Include(e => e.Student)
            .Where(e => e.GroupId == groupId && e.TeacherId == teacherId.Value && e.IsActive)
            .OrderBy(e => e.Student.FullName)
            .ToListAsync();

        var records = await _context.AttendanceRecords
            .Where(r => sessionIds.Contains(r.SessionId) && r.TeacherId == teacherId.Value)
            .ToListAsync();

        var headers = new[]
        {
            "كود الطالب (Code)",
            "اسم الطالب (Name)",
            "الصف (Grade)",
            "رقم الهاتف (Phone)",
            "رقم ولي الأمر (Parent Phone)",
            "إجمالي الجلسات (Sessions)",
            "حضور (Present)",
            "تأخير (Late)",
            "معذور (Excused)",
            "غياب (Absent)",
            "نسبة الحضور (Attendance Rate %)"
        };

        var rows = new List<string[]>();
        foreach (var e in enrollments)
        {
            var student = e.Student;
            var stRecords = records.Where(r => r.StudentId == student.Id).ToList();

            int present = stRecords.Count(r => r.Status == "Present");
            int late = stRecords.Count(r => r.Status == "Late");
            int excused = stRecords.Count(r => r.Status == "Excused");
            int absent = stRecords.Count(r => r.Status == "Absent");
            int total = stRecords.Count;
            var rate = total > 0 ? Math.Round((double)(present + late) / total * 100.0, 1) : 100.0;

            rows.Add(new[]
            {
                student.StudentCode,
                student.FullName,
                student.CurrentGrade ?? string.Empty,
                student.PhoneNumber ?? string.Empty,
                student.ParentPhoneNumber ?? string.Empty,
                total.ToString(),
                present.ToString(),
                late.ToString(),
                excused.ToString(),
                absent.ToString(),
                $"{rate}%"
            });
        }

        var csvBytes = _exportService.GenerateCsv(headers, rows);
        var filename = $"{group.Name}_Attendance_{DateTime.UtcNow:yyyyMMdd}.csv";

        return File(csvBytes, "text/csv; charset=utf-8", filename);
    }

    [HttpGet("export/session-attendance")]
    public async Task<IActionResult> ExportSessionAttendance([FromQuery] Guid sessionId)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var session = await _context.Sessions
            .Include(s => s.Group)
            .Include(s => s.AttendanceRecords)
                .ThenInclude(r => r.Student)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.TeacherId == teacherId.Value);

        if (session == null)
        {
            return NotFound(new { message = "Session not found." });
        }

        var headers = new[]
        {
            "كود الطالب (Code)",
            "اسم الطالب (Name)",
            "الحالة (Status)",
            "طريقة التسجيل (Method)",
            "دقائق التأخير (Minutes Late)",
            "وقت التسجيل (Recorded At UTC)",
            "ملاحظات (Notes)"
        };

        var rows = session.AttendanceRecords
            .OrderBy(r => r.Student.FullName)
            .Select(r => new[]
            {
                r.Student.StudentCode,
                r.Student.FullName,
                r.Status,
                r.Method,
                r.MinutesLate?.ToString() ?? "0",
                r.RecordedAtUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                r.Notes ?? string.Empty
            });

        var csvBytes = _exportService.GenerateCsv(headers, rows);
        var filename = $"{session.Title}_{session.SessionDateUtc:yyyyMMdd}.csv";

        return File(csvBytes, "text/csv; charset=utf-8", filename);
    }

    [HttpGet("export/students")]
    public async Task<IActionResult> ExportStudentsRoster([FromQuery] Guid? groupId = null, [FromQuery] string? grade = null)
    {
        var teacherId = _tenantContext.CurrentTeacherId;
        if (!teacherId.HasValue)
        {
            return Unauthorized(new { message = "Teacher identity could not be verified." });
        }

        var query = _context.Students
            .AsNoTracking()
            .Include(s => s.Enrollments.Where(e => e.IsActive))
                .ThenInclude(e => e.Group)
            .Where(s => s.TeacherId == teacherId.Value);

        if (groupId.HasValue)
        {
            query = query.Where(s => s.Enrollments.Any(e => e.GroupId == groupId.Value && e.IsActive));
        }

        if (!string.IsNullOrWhiteSpace(grade))
        {
            query = query.Where(s => s.CurrentGrade == grade.Trim());
        }

        var students = await query.OrderBy(s => s.FullName).ToListAsync();

        var headers = new[]
        {
            "كود الطالب (Code)",
            "اسم الطالب (Name)",
            "الصف (Grade)",
            "المجموعة (Group)",
            "رقم الهاتف (Phone)",
            "رقم ولي الأمر (Parent Phone)",
            "الحالة (Active)",
            "تاريخ الإنشاء (Created At UTC)"
        };

        var rows = students.Select(s => new[]
        {
            s.StudentCode,
            s.FullName,
            s.CurrentGrade ?? string.Empty,
            s.Enrollments.FirstOrDefault()?.Group.Name ?? "غير مسجل",
            s.PhoneNumber ?? string.Empty,
            s.ParentPhoneNumber ?? string.Empty,
            s.IsActive ? "نشط" : "معطل",
            s.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm:ss")
        });

        var csvBytes = _exportService.GenerateCsv(headers, rows);
        return File(csvBytes, "text/csv; charset=utf-8", "Students_Roster.csv");
    }
}
