using Hudoor.Api.Common;

namespace Hudoor.Api.Data.Entities;

/// <summary>
/// Represents a student's enrollment in a group for an academic year,
/// modeling enrollment history separately from permanent student identity.
/// </summary>
public class GroupEnrollment : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TeacherId { get; set; }
    public Guid GroupId { get; set; }
    public Guid StudentId { get; set; }
    public DateTime JoinedDateUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LeftDateUtc { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigations
    public Teacher Teacher { get; set; } = null!;
    public Group Group { get; set; } = null!;
    public Student Student { get; set; } = null!;
}
