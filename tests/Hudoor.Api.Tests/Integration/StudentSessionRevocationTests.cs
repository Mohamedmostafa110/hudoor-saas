using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Hudoor.Api.Common;
using Hudoor.Api.Data.Entities;
using Hudoor.Api.DTOs;
using Hudoor.Api.Services;
using Hudoor.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hudoor.Api.Tests.Integration;

public class StudentSessionRevocationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public StudentSessionRevocationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task StudentSession_IsInstantlyRevoked_WhenTeacherResetsPassword()
    {
        using var scope = _factory.Services.CreateScope();
        var db = _factory.CreateDbContext();
        var pwd = scope.ServiceProvider.GetRequiredService<IPasswordService>();

        // Create teacher and student
        var teacher = new Teacher
        {
            Id = Guid.NewGuid(),
            Username = "teacher_session_test",
            PasswordHash = pwd.HashPassword("TeacherPass2026!"),
            FullName = "Session Test Teacher",
            Email = "session_t@test.com",
            IsActive = true
        };
        db.Teachers.Add(teacher);

        var student = new Student
        {
            Id = Guid.NewGuid(),
            TeacherId = teacher.Id,
            StudentCode = "STD-REVOKE-1",
            PasswordHash = pwd.HashPassword("InitialPass123"),
            FullName = "Revoke Target Student",
            TokenVersion = 1,
            MustChangePassword = false,
            IsActive = true
        };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        // 1. Student logs in
        var studentLoginRes = await _client.PostAsJsonAsync("/api/auth/student/login", new StudentLoginRequest
        {
            StudentCode = "STD-REVOKE-1",
            Password = "InitialPass123"
        });
        var studentAuth = await studentLoginRes.Content.ReadFromJsonAsync<AuthResponse>();
        var oldStudentToken = studentAuth!.Token;

        // 2. Student calls /api/auth/me with active token -> 200 OK
        var req1 = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        req1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", oldStudentToken);
        var res1 = await _client.SendAsync(req1);
        Assert.Equal(HttpStatusCode.OK, res1.StatusCode);

        // 3. Teacher logs in and resets student's password
        var teacherLoginRes = await _client.PostAsJsonAsync("/api/auth/teacher/login", new TeacherLoginRequest
        {
            Username = "teacher_session_test",
            Password = "TeacherPass2026!"
        });
        var teacherAuth = await teacherLoginRes.Content.ReadFromJsonAsync<AuthResponse>();

        var resetReq = new HttpRequestMessage(HttpMethod.Post, $"/api/teacher/students/{student.Id}/reset-password");
        resetReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacherAuth!.Token);
        var resetRes = await _client.SendAsync(resetReq);
        Assert.Equal(HttpStatusCode.OK, resetRes.StatusCode);
        var resetData = await resetRes.Content.ReadFromJsonAsync<ResetStudentPasswordResponse>();

        // 4. Student calls /api/auth/me IMMEDIATELY with old token
        // Database TokenVersion is now 2, but old token has tv=1 -> MUST be rejected with 401 Unauthorized!
        var req2 = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        req2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", oldStudentToken);
        var res2 = await _client.SendAsync(req2);

        Assert.Equal(HttpStatusCode.Unauthorized, res2.StatusCode);
        var res2Json = await res2.Content.ReadAsStringAsync();
        Assert.Contains("SESSION_REVOKED", res2Json);

        // 5. Student logs in with the new temporary password
        var tempLoginRes = await _client.PostAsJsonAsync("/api/auth/student/login", new StudentLoginRequest
        {
            StudentCode = "STD-REVOKE-1",
            Password = resetData!.TemporaryPassword
        });
        Assert.Equal(HttpStatusCode.OK, tempLoginRes.StatusCode);
        var tempAuth = await tempLoginRes.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.True(tempAuth!.MustChangePassword);
    }

    [Fact]
    public async Task StudentSession_EnforcesPasswordChange_AndInvalidatesPriorTokenOnChange()
    {
        using var scope = _factory.Services.CreateScope();
        var db = _factory.CreateDbContext();
        var pwd = scope.ServiceProvider.GetRequiredService<IPasswordService>();

        var teacher = new Teacher
        {
            Id = Guid.NewGuid(),
            Username = "teacher_pwd_change_flow",
            PasswordHash = pwd.HashPassword("TeacherPass2026!"),
            FullName = "Teacher Flow",
            Email = "flow_t@test.com",
            IsActive = true
        };
        db.Teachers.Add(teacher);

        var student = new Student
        {
            Id = Guid.NewGuid(),
            TeacherId = teacher.Id,
            StudentCode = "STD-PWD-CHANGE",
            PasswordHash = pwd.HashPassword("TempPass735"),
            FullName = "Flow Student",
            TokenVersion = 5,
            MustChangePassword = true, // Must change password!
            TemporaryPasswordExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            IsActive = true
        };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        // 1. Student logs in with temporary password
        var loginRes = await _client.PostAsJsonAsync("/api/auth/student/login", new StudentLoginRequest
        {
            StudentCode = "STD-PWD-CHANGE",
            Password = "TempPass735"
        });
        var auth = await loginRes.Content.ReadFromJsonAsync<AuthResponse>();
        var tempToken = auth!.Token;

        // 2. Change password successfully
        var changeReq = new HttpRequestMessage(HttpMethod.Post, "/api/student/change-password")
        {
            Content = JsonContent.Create(new ChangePasswordRequest
            {
                CurrentPassword = "TempPass735",
                NewPassword = "MyPermanentPassword99!"
            })
        };
        changeReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tempToken);
        var changeRes = await _client.SendAsync(changeReq);
        Assert.Equal(HttpStatusCode.OK, changeRes.StatusCode);

        // 3. Verify prior token (with TokenVersion 5) is rejected on next request
        var oldTokenReq = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        oldTokenReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tempToken);
        var oldTokenRes = await _client.SendAsync(oldTokenReq);
        Assert.Equal(HttpStatusCode.Unauthorized, oldTokenRes.StatusCode);

        // 4. Verify student can log in with new permanent password
        var newLoginRes = await _client.PostAsJsonAsync("/api/auth/student/login", new StudentLoginRequest
        {
            StudentCode = "STD-PWD-CHANGE",
            Password = "MyPermanentPassword99!"
        });
        Assert.Equal(HttpStatusCode.OK, newLoginRes.StatusCode);
        var newAuth = await newLoginRes.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.False(newAuth!.MustChangePassword);
    }

    [Fact]
    public async Task StudentSession_IsRevokedImmediately_WhenTeacherIsDeactivated()
    {
        using var scope = _factory.Services.CreateScope();
        var db = _factory.CreateDbContext();
        var pwd = scope.ServiceProvider.GetRequiredService<IPasswordService>();

        var teacher = new Teacher
        {
            Id = Guid.NewGuid(),
            Username = "teacher_to_deactivate",
            PasswordHash = pwd.HashPassword("TeacherPass2026!"),
            FullName = "Teacher To Deactivate",
            Email = "deact_t@test.com",
            IsActive = true
        };
        db.Teachers.Add(teacher);

        var student = new Student
        {
            Id = Guid.NewGuid(),
            TeacherId = teacher.Id,
            StudentCode = "STD-UNDER-TEACHER-DEACT",
            PasswordHash = pwd.HashPassword("StudentPass123"),
            FullName = "Student Under Deact Teacher",
            TokenVersion = 1,
            MustChangePassword = false,
            IsActive = true
        };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        // 1. Student logs in
        var loginRes = await _client.PostAsJsonAsync("/api/auth/student/login", new StudentLoginRequest
        {
            StudentCode = "STD-UNDER-TEACHER-DEACT",
            Password = "StudentPass123"
        });
        var auth = await loginRes.Content.ReadFromJsonAsync<AuthResponse>();

        // Verify session works
        var testReq1 = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        testReq1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        var testRes1 = await _client.SendAsync(testReq1);
        Assert.Equal(HttpStatusCode.OK, testRes1.StatusCode);

        // 2. Deactivate teacher in DB
        var teacherInDb = await db.Teachers.FirstAsync(t => t.Id == teacher.Id);
        teacherInDb.IsActive = false;
        await db.SaveChangesAsync();

        // 3. Student makes immediate next request
        // Middleware checks Teacher.IsActive directly against SQL Server -> 401 Unauthorized!
        var testReq2 = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        testReq2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        var testRes2 = await _client.SendAsync(testReq2);

        Assert.Equal(HttpStatusCode.Unauthorized, testRes2.StatusCode);
    }
}
