using System.ComponentModel.DataAnnotations;

namespace Hudoor.Api.DTOs;

public class RecordAttendanceRequest
{
    [Required(ErrorMessage = "StudentId is required.")]
    public Guid StudentId { get; set; }

    [Required(ErrorMessage = "Status is required.")]
    [RegularExpression("^(Present|Absent|Late|Excused)$", ErrorMessage = "Status must be 'Present', 'Absent', 'Late', or 'Excused'.")]
    public string Status { get; set; } = string.Empty;

    [Range(0, 300, ErrorMessage = "MinutesLate must be between 0 and 300.")]
    public int? MinutesLate { get; set; }

    [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
    public string? Notes { get; set; }
}

public class RecordAttendanceItemRequest
{
    [Required(ErrorMessage = "StudentId is required.")]
    public Guid StudentId { get; set; }

    [Required(ErrorMessage = "Status is required.")]
    [RegularExpression("^(Present|Absent|Late|Excused)$", ErrorMessage = "Status must be 'Present', 'Absent', 'Late', or 'Excused'.")]
    public string Status { get; set; } = string.Empty;

    [Range(0, 300, ErrorMessage = "MinutesLate must be between 0 and 300.")]
    public int? MinutesLate { get; set; }

    [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
    public string? Notes { get; set; }
}

public class BulkRecordAttendanceRequest
{
    [Required(ErrorMessage = "Records list cannot be null.")]
    public List<RecordAttendanceItemRequest> Records { get; set; } = new();

    public bool MarkRemainingAsAbsent { get; set; } = false;
}

public class UpdateAttendanceRecordRequest
{
    [Required(ErrorMessage = "Status is required.")]
    [RegularExpression("^(Present|Absent|Late|Excused)$", ErrorMessage = "Status must be 'Present', 'Absent', 'Late', or 'Excused'.")]
    public string Status { get; set; } = string.Empty;

    [Range(0, 300, ErrorMessage = "MinutesLate must be between 0 and 300.")]
    public int? MinutesLate { get; set; }

    [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
    public string? Notes { get; set; }

    [StringLength(500, ErrorMessage = "ModificationReason cannot exceed 500 characters.")]
    public string? ModificationReason { get; set; }
}

public class AttendanceRecordResponseDto
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public DateTime RecordedAtUtc { get; set; }
    public int? MinutesLate { get; set; }
    public string? Notes { get; set; }
}

public class AttendanceAuditLogDto
{
    public Guid Id { get; set; }
    public Guid AttendanceRecordId { get; set; }
    public string PreviousStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public Guid ModifiedByTeacherId { get; set; }
    public string? ModificationReason { get; set; }
    public DateTime TimestampUtc { get; set; }
}

public class SessionRosterStudentDto
{
    public Guid StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public Guid? AttendanceRecordId { get; set; }
    public string Status { get; set; } = "Unrecorded";
    public string? Method { get; set; }
    public DateTime? RecordedAtUtc { get; set; }
    public int? MinutesLate { get; set; }
    public string? Notes { get; set; }
}
