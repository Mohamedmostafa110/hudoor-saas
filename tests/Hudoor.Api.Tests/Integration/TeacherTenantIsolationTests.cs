using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Hudoor.Api.Data.Entities;
using Hudoor.Api.DTOs;
using Hudoor.Api.Services;
using Hudoor.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hudoor.Api.Tests.Integration;

public class TeacherTenantIsolationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public TeacherTenantIsolationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ResetStudentPassword_ByOwnerTeacher_SucceedsAndGeneratesAuditLog()
    {
        using var scope = _factory.Services.CreateScope();
        var db = _factory.CreateDbContext();
        var pwd = scope.ServiceProvider.GetRequiredService<IPasswordService>();

        // Create Teacher T1
        var teacher = new Teacher
        {
            Id = Guid.NewGuid(),
            Username = "teacher_owner_1",
            PasswordHash = pwd.HashPassword("TeacherPass2026!"),
            FullName = "Owner Teacher",
            Email = "teacher_owner@test.com",
            IsActive = true
        };
        db.Teachers.Add(teacher);

        // Create Student belonging to T1
        var student = new Student
        {
            Id = Guid.NewGuid(),
            TeacherId = teacher.Id,
            StudentCode = "STD-RESET-OWNER",
            PasswordHash = pwd.HashPassword("InitialPass123"),
            FullName = "Student Of Owner",
            TokenVersion = 1,
            MustChangePassword = false,
            IsActive = true
        };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        // Login as Teacher T1
        var loginRes = await _client.PostAsJsonAsync("/api/auth/teacher/login", new TeacherLoginRequest
        {
            Username = "teacher_owner_1",
            Password = "TeacherPass2026!"
        });
        var auth = await loginRes.Content.ReadFromJsonAsync<AuthResponse>();

        // Reset student password
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/teacher/students/{student.Id}/reset-password");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var resetResult = await response.Content.ReadFromJsonAsync<ResetStudentPasswordResponse>();
        Assert.NotNull(resetResult);
        Assert.Equal(student.Id, resetResult.StudentId);
        Assert.Equal("STD-RESET-OWNER", resetResult.StudentCode);
        Assert.NotEmpty(resetResult.TemporaryPassword);
        Assert.Matches("^[A-Za-z]+[2-9]{3}$", resetResult.TemporaryPassword);

        // Verify DB updates using a fresh DbContext
        using var verifyDb = _factory.CreateDbContext();
        var updatedStudent = await verifyDb.Students.AsNoTracking().FirstAsync(s => s.Id == student.Id);
        Assert.Equal(2, updatedStudent.TokenVersion); // Atomic increment!
        Assert.True(updatedStudent.MustChangePassword);
        Assert.NotNull(updatedStudent.TemporaryPasswordExpiresAtUtc);

        // Verify Audit Log was generated (Rule BR-PWD-050)
        var audit = await verifyDb.PasswordResetAuditLogs.AsNoTracking().FirstOrDefaultAsync(a => a.StudentId == student.Id);
        Assert.NotNull(audit);
        Assert.Equal(teacher.Id, audit.TriggeredByTeacherId);
    }

    [Fact]
    public async Task ResetStudentPassword_ByDifferentTeacher_FailsWith404NotFound()
    {
        using var scope = _factory.Services.CreateScope();
        var db = _factory.CreateDbContext();
        var pwd = scope.ServiceProvider.GetRequiredService<IPasswordService>();

        // Create Teacher A and Teacher B
        var teacherA = new Teacher
        {
            Id = Guid.NewGuid(),
            Username = "teacher_isolation_a",
            PasswordHash = pwd.HashPassword("TeacherPass2026!"),
            FullName = "Teacher A",
            Email = "teacher_a@test.com",
            IsActive = true
        };
        var teacherB = new Teacher
        {
            Id = Guid.NewGuid(),
            Username = "teacher_isolation_b",
            PasswordHash = pwd.HashPassword("TeacherPass2026!"),
            FullName = "Teacher B",
            Email = "teacher_b@test.com",
            IsActive = true
        };
        db.Teachers.AddRange(teacherA, teacherB);

        // Student belongs to Teacher B
        var studentB = new Student
        {
            Id = Guid.NewGuid(),
            TeacherId = teacherB.Id,
            StudentCode = "STD-OF-TEACHER-B",
            PasswordHash = pwd.HashPassword("PassB123"),
            FullName = "Student Of B",
            TokenVersion = 1,
            MustChangePassword = false,
            IsActive = true
        };
        db.Students.Add(studentB);
        await db.SaveChangesAsync();

        // Login as Teacher A
        var loginResA = await _client.PostAsJsonAsync("/api/auth/teacher/login", new TeacherLoginRequest
        {
            Username = "teacher_isolation_a",
            Password = "TeacherPass2026!"
        });
        var authA = await loginResA.Content.ReadFromJsonAsync<AuthResponse>();

        // Teacher A attempts to reset Student B's password
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/teacher/students/{studentB.Id}/reset-password");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authA!.Token);

        var response = await _client.SendAsync(request);

        // Must return 404 Not Found to prevent tenant snooping / student existence enumeration
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // Verify Student B was NOT modified
        using var verifyDb = _factory.CreateDbContext();
        var dbStudentB = await verifyDb.Students.AsNoTracking().FirstAsync(s => s.Id == studentB.Id);
        Assert.Equal(1, dbStudentB.TokenVersion);
        Assert.False(dbStudentB.MustChangePassword);
    }
}
