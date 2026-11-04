namespace Hudoor.Api.Data.Entities;

/// <summary>
/// Immutable audit log capturing status modifications to an existing attendance record.
/// </summary>
public class AttendanceAuditLog
{
    public Guid Id { get; set; }
    public Guid AttendanceRecordId { get; set; }
    public string PreviousStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public Guid ModifiedByTeacherId { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string? ModificationReason { get; set; }

    // Navigations
    public AttendanceRecord AttendanceRecord { get; set; } = null!;
    public Teacher ModifiedByTeacher { get; set; } = null!;
}
