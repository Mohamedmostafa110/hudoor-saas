using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Hudoor.Api.Common;
using Hudoor.Api.Data.Entities;
using Hudoor.Api.DTOs;
using Hudoor.Api.Services;
using Hudoor.Api.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Hudoor.Api.Tests.Integration;

public class AuthEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PlatformAdminLogin_WithValidCredentials_Returns200AndToken()
    {
        // Admin is seeded by AdminBootstrapService
        var response = await _client.PostAsJsonAsync("/api/auth/platform-admin/login", new PlatformAdminLoginRequest
        {
            Username = "admin",
            Password = "AdminSecure2026!"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.NotEmpty(body.Token);
        Assert.Equal(Roles.PlatformAdmin, body.Role);
        Assert.Equal("admin", body.UsernameOrCode);
    }

    [Fact]
    public async Task PlatformAdminLogin_WithInvalidPassword_Returns401GenericError()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/platform-admin/login", new PlatformAdminLoginRequest
        {
            Username = "admin",
            Password = "WrongPassword999!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid credentials.", json);
    }

    [Fact]
    public async Task TeacherLogin_WithValidCredentials_Returns200AndToken()
    {
        using var scope = _factory.Services.CreateScope();
        var db = _factory.CreateDbContext();
        var pwd = scope.ServiceProvider.GetRequiredService<IPasswordService>();

        var teacher = new Teacher
        {
            Id = Guid.NewGuid(),
            Username = "teacher_test_1",
            PasswordHash = pwd.HashPassword("TeacherPass2026!"),
            FullName = "Mr. Test Teacher",
            Email = "teacher1@test.com",
            IsActive = true
        };
        db.Teachers.Add(teacher);
        await db.SaveChangesAsync();

        var response = await _client.PostAsJsonAsync("/api/auth/teacher/login", new TeacherLoginRequest
        {
            Username = "teacher_test_1",
            Password = "TeacherPass2026!"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.Equal(Roles.Teacher, body.Role);
        Assert.Equal(teacher.Id, body.TeacherId);
    }

    [Fact]
    public async Task TeacherLogin_WhenTeacherDeactivated_Returns403Forbidden()
    {
        using var scope = _factory.Services.CreateScope();
        var db = _factory.CreateDbContext();
        var pwd = scope.ServiceProvider.GetRequiredService<IPasswordService>();

        var teacher = new Teacher
        {
            Id = Guid.NewGuid(),
            Username = "inactive_teacher",
            PasswordHash = pwd.HashPassword("TeacherPass2026!"),
            FullName = "Inactive Teacher",
            Email = "inactive_teacher@test.com",
            IsActive = false
        };
        db.Teachers.Add(teacher);
        await db.SaveChangesAsync();

        var response = await _client.PostAsJsonAsync("/api/auth/teacher/login", new TeacherLoginRequest
        {
            Username = "inactive_teacher",
            Password = "TeacherPass2026!"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("Account is deactivated.", json);
    }

    [Fact]
    public async Task StudentLogin_WithValidCredentials_Returns200AndToken()
    {
        using var scope = _factory.Services.CreateScope();
        var db = _factory.CreateDbContext();
        var pwd = scope.ServiceProvider.GetRequiredService<IPasswordService>();

        var teacher = new Teacher
        {
            Id = Guid.NewGuid(),
            Username = "teacher_for_student",
            PasswordHash = pwd.HashPassword("TeacherPass2026!"),
            FullName = "Teacher For Student",
            Email = "teacher_stud@test.com",
            IsActive = true
        };
        db.Teachers.Add(teacher);

        var student = new Student
        {
            Id = Guid.NewGuid(),
            TeacherId = teacher.Id,
            StudentCode = "STD-10001",
            PasswordHash = pwd.HashPassword("Falcon482"),
            FullName = "Tariq Ali",
            MustChangePassword = true,
            TemporaryPasswordExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            IsActive = true
        };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        var response = await _client.PostAsJsonAsync("/api/auth/student/login", new StudentLoginRequest
        {
            StudentCode = "STD-10001",
            Password = "Falcon482"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.Equal(Roles.Student, body.Role);
        Assert.Equal("STD-10001", body.UsernameOrCode);
        Assert.True(body.MustChangePassword);
        Assert.Equal(teacher.Id, body.TeacherId);
        Assert.Equal(student.Id, body.StudentId);
    }

    [Fact]
    public async Task StudentLogin_WhenTemporaryPasswordExpired_Returns401ExpiredMessage()
    {
        using var scope = _factory.Services.CreateScope();
        var db = _factory.CreateDbContext();
        var pwd = scope.ServiceProvider.GetRequiredService<IPasswordService>();

        var teacher = new Teacher
        {
            Id = Guid.NewGuid(),
            Username = "teacher_expired_test",
            PasswordHash = pwd.HashPassword("TeacherPass2026!"),
            FullName = "Teacher Expired",
            Email = "expired_teacher@test.com",
            IsActive = true
        };
        db.Teachers.Add(teacher);

        var student = new Student
        {
            Id = Guid.NewGuid(),
            TeacherId = teacher.Id,
            StudentCode = "STD-EXPIRED-1",
            PasswordHash = pwd.HashPassword("Swift924"),
            FullName = "Expired Student",
            MustChangePassword = true,
            TemporaryPasswordExpiresAtUtc = DateTime.UtcNow.AddDays(-1), // Expired!
            IsActive = true
        };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        var response = await _client.PostAsJsonAsync("/api/auth/student/login", new StudentLoginRequest
        {
            StudentCode = "STD-EXPIRED-1",
            Password = "Swift924"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("Temporary password has expired", json);
    }

    [Fact]
    public async Task StudentLogin_WhenTeacherIsDeactivated_Returns403Forbidden()
    {
        using var scope = _factory.Services.CreateScope();
        var db = _factory.CreateDbContext();
        var pwd = scope.ServiceProvider.GetRequiredService<IPasswordService>();

        var teacher = new Teacher
        {
            Id = Guid.NewGuid(),
            Username = "teacher_deactivated_std",
            PasswordHash = pwd.HashPassword("TeacherPass2026!"),
            FullName = "Teacher Inactive",
            Email = "teacher_inact@test.com",
            IsActive = false // Deactivated teacher!
        };
        db.Teachers.Add(teacher);

        var student = new Student
        {
            Id = Guid.NewGuid(),
            TeacherId = teacher.Id,
            StudentCode = "STD-TEACHER-INACTIVE",
            PasswordHash = pwd.HashPassword("Cedar735"),
            FullName = "Student Under Deactivated Teacher",
            MustChangePassword = false,
            IsActive = true
        };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        var response = await _client.PostAsJsonAsync("/api/auth/student/login", new StudentLoginRequest
        {
            StudentCode = "STD-TEACHER-INACTIVE",
            Password = "Cedar735"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("Teacher account is deactivated", json);
    }

    [Fact]
    public async Task GetCurrentUser_WhenUnauthenticated_Returns401()
    {
        var response = await _client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentUser_WhenAuthenticatedAsTeacher_ReturnsTeacherProfile()
    {
        using var scope = _factory.Services.CreateScope();
        var db = _factory.CreateDbContext();
        var pwd = scope.ServiceProvider.GetRequiredService<IPasswordService>();

        var teacher = new Teacher
        {
            Id = Guid.NewGuid(),
            Username = "teacher_me_check",
            PasswordHash = pwd.HashPassword("TeacherPass2026!"),
            FullName = "Profile Teacher",
            Email = "profile_teacher@test.com",
            IsActive = true
        };
        db.Teachers.Add(teacher);
        await db.SaveChangesAsync();

        var loginRes = await _client.PostAsJsonAsync("/api/auth/teacher/login", new TeacherLoginRequest
        {
            Username = "teacher_me_check",
            Password = "TeacherPass2026!"
        });
        var auth = await loginRes.Content.ReadFromJsonAsync<AuthResponse>();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var me = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.NotNull(me);
        Assert.Equal(Roles.Teacher, me.Role);
        Assert.Equal("teacher_me_check", me.UsernameOrCode);
        Assert.Equal("Profile Teacher", me.FullName);
        Assert.Equal(teacher.Id, me.TeacherId);
    }
}
