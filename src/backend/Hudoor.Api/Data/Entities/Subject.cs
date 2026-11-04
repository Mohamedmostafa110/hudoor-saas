using Hudoor.Api.Common;

namespace Hudoor.Api.Data.Entities;

public class Subject : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TeacherId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? GradeLevel { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigations
    public Teacher Teacher { get; set; } = null!;
    public ICollection<Group> Groups { get; set; } = new List<Group>();
}
