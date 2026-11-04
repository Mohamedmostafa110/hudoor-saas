using Hudoor.Api.Common;

namespace Hudoor.Api.Data.Entities;

/// <summary>
/// Represents an on-demand attendance session conducted by a teacher.
/// </summary>
public class Session : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TeacherId { get; set; }
    public Guid GroupId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime SessionDateUtc { get; set; }
    public string Status { get; set; } = "Active"; // Draft, Active, Closed, Archived
    public string? QrSecuritySalt { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigations
    public Teacher Teacher { get; set; } = null!;
    public Group Group { get; set; } = null!;
    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
}
