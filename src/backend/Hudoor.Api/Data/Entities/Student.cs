using Hudoor.Api.Common;

namespace Hudoor.Api.Data.Entities;

public class Student : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TeacherId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public int TokenVersion { get; set; } = 1;
    public bool MustChangePassword { get; set; } = true;
    public DateTime? TemporaryPasswordExpiresAtUtc { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? ParentPhoneNumber { get; set; }
    public string? CurrentGrade { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigations
    public Teacher Teacher { get; set; } = null!;
    public ICollection<GroupEnrollment> Enrollments { get; set; } = new List<GroupEnrollment>();
    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
    public ICollection<PasswordResetAuditLog> PasswordResetAuditLogs { get; set; } = new List<PasswordResetAuditLog>();
}
