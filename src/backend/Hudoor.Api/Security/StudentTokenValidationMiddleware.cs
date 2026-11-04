using System.Security.Claims;
using Hudoor.Api.Common;
using Hudoor.Api.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Hudoor.Api.Security;

public class StudentTokenValidationMiddleware
{
    private readonly RequestDelegate _next;

    public StudentTokenValidationMiddleware(RequestDelegate _next)
    {
        this._next = _next;
    }

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext)
    {
        var user = context.User;

        if (user.Identity?.IsAuthenticated == true && user.IsInRole(Roles.Student))
        {
            var studentIdClaim = user.FindFirstValue("student_id") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
            var tvClaim = user.FindFirstValue("tv");

            if (!Guid.TryParse(studentIdClaim, out var studentId) || !int.TryParse(tvClaim, out var claimTokenVersion))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "INVALID_TOKEN",
                    message = "Student token is missing required claims."
                });
                return;
            }

            // Direct SQL Server TokenVersion validation against the database (zero stale in-memory cache)
            var studentState = await dbContext.Students
                .AsNoTracking()
                .Where(s => s.Id == studentId)
                .Select(s => new
                {
                    s.TokenVersion,
                    s.IsActive,
                    TeacherIsActive = s.Teacher.IsActive
                })
                .FirstOrDefaultAsync(context.RequestAborted);

            if (studentState == null || !studentState.IsActive || !studentState.TeacherIsActive || studentState.TokenVersion != claimTokenVersion)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "SESSION_REVOKED",
                    message = "Your session has been invalidated or expired. Please log in again."
                });
                return;
            }

            // Mandatory password change enforcement: if must_change_password claim is true, student can only access change-password and me
            var mustChangePasswordClaim = user.FindFirstValue("must_change_password");
            if (string.Equals(mustChangePasswordClaim, "true", StringComparison.OrdinalIgnoreCase))
            {
                var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
                var isAllowed = path.EndsWith("/api/student/change-password") ||
                                path.EndsWith("/api/student/me/change-password") ||
                                path.EndsWith("/api/auth/me");

                if (!isAllowed)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "PASSWORD_CHANGE_REQUIRED",
                        message = "You are required to change your temporary password before accessing other features."
                    });
                    return;
                }
            }
        }

        await _next(context);
    }
}
