using Hudoor.Api.Common;

namespace Hudoor.Api.Data.Entities;

/// <summary>
/// Represents the attendance status of an individual student for a specific session.
/// </summary>
public class AttendanceRecord : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TeacherId { get; set; }
    public Guid SessionId { get; set; }
    public Guid StudentId { get; set; }
    public string Status { get; set; } = "Present"; // Present, Absent, Late, Excused
    public string Method { get; set; } = "Manual";  // Manual, QRCode
    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;
    public int? MinutesLate { get; set; }
    public string? Notes { get; set; }

    // Navigations
    public Teacher Teacher { get; set; } = null!;
    public Session Session { get; set; } = null!;
    public Student Student { get; set; } = null!;
    public ICollection<AttendanceAuditLog> AuditLogs { get; set; } = new List<AttendanceAuditLog>();
}
