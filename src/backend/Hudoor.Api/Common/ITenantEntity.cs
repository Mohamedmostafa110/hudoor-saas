namespace Hudoor.Api.Common;

/// <summary>
/// Marker interface for tenant-scoped entities owned by an independent teacher.
/// </summary>
public interface ITenantEntity
{
    Guid TeacherId { get; set; }
}
