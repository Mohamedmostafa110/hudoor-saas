namespace Hudoor.Api.Common;

/// <summary>
/// Provides authoritative tenant and identity information extracted strictly from validated claims.
/// Client-supplied tenant identifiers from query strings or JSON bodies are never used.
/// </summary>
public interface ITenantContext
{
    Guid? CurrentUserId { get; }
    Guid? CurrentTeacherId { get; }
    string? Role { get; }
    bool IsAuthenticated { get; }
    bool IsPlatformAdmin { get; }
    bool IsTeacher { get; }
    bool IsStudent { get; }
}
