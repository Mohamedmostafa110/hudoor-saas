namespace Hudoor.Api.DTOs;

public class TeacherAnalyticsSummaryDto
{
    public int TotalStudents { get; set; }
    public int ActiveStudents { get; set; }
    public int TotalGroups { get; set; }
    public int TotalSessions { get; set; }
    public double OverallAttendanceRate { get; set; }
    public int TotalPresentCount { get; set; }
    public int TotalLateCount { get; set; }
    public int TotalExcusedCount { get; set; }
    public int TotalAbsentCount { get; set; }
    public int AtRiskStudentsCount { get; set; }
    public List<GroupAttendanceSummaryDto> GroupSummaries { get; set; } = new();
}

public class GroupAttendanceSummaryDto
{
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public int EnrolledStudentsCount { get; set; }
    public int TotalSessionsCount { get; set; }
    public double AttendanceRate { get; set; }
    public int PresentCount { get; set; }
    public int LateCount { get; set; }
    public int ExcusedCount { get; set; }
    public int AbsentCount { get; set; }
}

public class AtRiskStudentDto
{
    public Guid StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? ParentPhoneNumber { get; set; }
    public string? CurrentGrade { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public int TotalExpectedSessions { get; set; }
    public int AttendedSessions { get; set; }
    public int AbsentSessions { get; set; }
    public double AttendanceRate { get; set; }
    public DateTime? LastAttendedDateUtc { get; set; }
}

public class GroupAttendanceMatrixDto
{
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public List<MatrixSessionHeaderDto> Sessions { get; set; } = new();
    public List<MatrixStudentRowDto> Students { get; set; } = new();
}

public class MatrixSessionHeaderDto
{
    public Guid SessionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime SessionDateUtc { get; set; }
}

public class MatrixStudentRowDto
{
    public Guid StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public Dictionary<Guid, string> SessionStatuses { get; set; } = new();
    public int TotalPresent { get; set; }
    public int TotalLate { get; set; }
    public int TotalExcused { get; set; }
    public int TotalAbsent { get; set; }
    public double AttendanceRate { get; set; }
}
