using System.ComponentModel.DataAnnotations;

namespace Hudoor.Api.DTOs;

public class StudentCheckInRequest
{
    [Required(ErrorMessage = "QR token is required.")]
    public string QrToken { get; set; } = string.Empty;
}

public class StudentCheckInResponseDto
{
    public Guid SessionId { get; set; }
    public string SessionTitle { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public DateTime RecordedAtUtc { get; set; }
    public string Status { get; set; } = "Present";
    public string Method { get; set; } = "QRCode";
    public string Message { get; set; } = "Attendance checked in successfully!";
}

public class StudentAttendanceHistoryDto
{
    public Guid AttendanceRecordId { get; set; }
    public Guid SessionId { get; set; }
    public string SessionTitle { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public DateTime SessionDateUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public int? MinutesLate { get; set; }
    public DateTime RecordedAtUtc { get; set; }
}

public class StudentAttendanceSummaryDto
{
    public int TotalSessions { get; set; }
    public int PresentCount { get; set; }
    public int LateCount { get; set; }
    public int ExcusedCount { get; set; }
    public int AbsentCount { get; set; }
    public double AttendancePercentage { get; set; }
}
