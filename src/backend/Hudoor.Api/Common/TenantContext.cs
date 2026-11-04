using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Hudoor.Api.Common;

public class TenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public Guid? CurrentUserId
    {
        get
        {
            var sub = User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? User?.FindFirstValue("sub");
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    public Guid? CurrentTeacherId
    {
        get
        {
            var teacherIdClaim = User?.FindFirstValue("teacher_id");
            return Guid.TryParse(teacherIdClaim, out var id) ? id : null;
        }
    }

    public string? Role => User?.FindFirstValue(ClaimTypes.Role) ?? User?.FindFirstValue("role");

    public bool IsPlatformAdmin => string.Equals(Role, Roles.PlatformAdmin, StringComparison.OrdinalIgnoreCase);

    public bool IsTeacher => string.Equals(Role, Roles.Teacher, StringComparison.OrdinalIgnoreCase);

    public bool IsStudent => string.Equals(Role, Roles.Student, StringComparison.OrdinalIgnoreCase);
}
