namespace Hudoor.Api.Data.Entities;

/// <summary>
/// Immutable audit log capturing teacher-initiated password reset events for students.
/// </summary>
public class PasswordResetAuditLog
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid TriggeredByTeacherId { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string? ClientIpAddress { get; set; }

    // Navigations
    public Student Student { get; set; } = null!;
    public Teacher TriggeredByTeacher { get; set; } = null!;
}
