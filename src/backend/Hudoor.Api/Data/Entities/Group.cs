using Hudoor.Api.Common;

namespace Hudoor.Api.Data.Entities;

public class Group : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TeacherId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid AcademicYearId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ScheduleDescription { get; set; }
    public int? MaxCapacity { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigations
    public Teacher Teacher { get; set; } = null!;
    public Subject Subject { get; set; } = null!;
    public AcademicYear AcademicYear { get; set; } = null!;
    public ICollection<GroupEnrollment> Enrollments { get; set; } = new List<GroupEnrollment>();
    public ICollection<Session> Sessions { get; set; } = new List<Session>();
}
