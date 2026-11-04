using Hudoor.Api.Common;

namespace Hudoor.Api.Data.Entities;

public class Term : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TeacherId { get; set; }
    public Guid AcademicYearId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigations
    public Teacher Teacher { get; set; } = null!;
    public AcademicYear AcademicYear { get; set; } = null!;
}
