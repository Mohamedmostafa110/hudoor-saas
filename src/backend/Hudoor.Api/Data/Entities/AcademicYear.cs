using Hudoor.Api.Common;

namespace Hudoor.Api.Data.Entities;

public class AcademicYear : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TeacherId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigations
    public Teacher Teacher { get; set; } = null!;
    public ICollection<Term> Terms { get; set; } = new List<Term>();
    public ICollection<Group> Groups { get; set; } = new List<Group>();
}
