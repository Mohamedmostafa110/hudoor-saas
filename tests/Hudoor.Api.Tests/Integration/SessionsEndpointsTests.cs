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

public class SessionsEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SessionsEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(Teacher Teacher, string Token)> CreateAndLoginTeacherAsync(string username)
    {
        using var scope = _factory.Services.CreateScope();
        var db = _factory.CreateDbContext();
        var pwd = scope.ServiceProvider.GetRequiredService<IPasswordService>();

        var teacher = new Teacher
        {
            Id = Guid.NewGuid(),
            Username = username,
            PasswordHash = pwd.HashPassword("TeacherPass2026!"),
            FullName = $"Teacher {username}",
            Email = $"{username}@test.com",
            IsActive = true
        };
        db.Teachers.Add(teacher);
        await db.SaveChangesAsync();

        var loginRes = await _client.PostAsJsonAsync("/api/auth/teacher/login", new TeacherLoginRequest
        {
            Username = username,
            Password = "TeacherPass2026!"
        });
        loginRes.EnsureSuccessStatusCode();
        var auth = await loginRes.Content.ReadFromJsonAsync<AuthResponse>();

        return (teacher, auth!.Token);
    }

    private async Task<(Subject Subject, AcademicYear Year, Group Group)> SeedGroupAsync(Guid teacherId)
    {
        using var db = _factory.CreateDbContext();
        var subject = new Subject
        {
            Id = Guid.NewGuid(),
            TeacherId = teacherId,
            Name = "Subject " + Guid.NewGuid().ToString("N")[..6],
            GradeLevel = "1st Secondary"
        };
        var year = new AcademicYear
        {
            Id = Guid.NewGuid(),
            TeacherId = teacherId,
            Name = "Year " + Guid.NewGuid().ToString("N")[..6],
            StartDate = new DateTime(2025, 9, 1),
            EndDate = new DateTime(2026, 6, 30),
            IsActive = true
        };
        var group = new Group
        {
            Id = Guid.NewGuid(),
            TeacherId = teacherId,
            SubjectId = subject.Id,
            AcademicYearId = year.Id,
            Name = "Group " + Guid.NewGuid().ToString("N")[..6]
        };

        db.Subjects.Add(subject);
        db.AcademicYears.Add(year);
        db.Groups.Add(group);
        await db.SaveChangesAsync();

        return (subject, year, group);
    }

    [Fact]
    public async Task CreateSession_Success()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("ses_t1_" + Guid.NewGuid().ToString("N")[..6]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (_, _, group) = await SeedGroupAsync(teacher.Id);

        var request = new CreateSessionRequest
        {
            GroupId = group.Id,
            Title = "Week 1 - Vectors Introduction",
            Status = "Active"
        };

        var response = await _client.PostAsJsonAsync("/api/teacher/sessions", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<SessionDetailDto>();
        Assert.NotNull(created);
        Assert.Equal("Week 1 - Vectors Introduction", created.Title);
        Assert.Equal("Active", created.Status);
        Assert.Equal(group.Id, created.GroupId);

        using var db = _factory.CreateDbContext();
        var sessionInDb = await db.Sessions.FindAsync(created.Id);
        Assert.NotNull(sessionInDb);
        Assert.False(string.IsNullOrWhiteSpace(sessionInDb.QrSecuritySalt));
    }

    [Fact]
    public async Task CreateSession_CrossTenantGroup_ReturnsNotFound()
    {
        var (teacher1, _) = await CreateAndLoginTeacherAsync("ses_t2a_" + Guid.NewGuid().ToString("N")[..6]);
        var (_, token2) = await CreateAndLoginTeacherAsync("ses_t2b_" + Guid.NewGuid().ToString("N")[..6]);

        var (_, _, teacher1Group) = await SeedGroupAsync(teacher1.Id);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token2);

        var response = await _client.PostAsJsonAsync("/api/teacher/sessions", new CreateSessionRequest
        {
            GroupId = teacher1Group.Id,
            Title = "Unauthorized Session"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSessions_FilteredByGroupAndStatus_Success()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("ses_t3_" + Guid.NewGuid().ToString("N")[..6]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (_, _, group1) = await SeedGroupAsync(teacher.Id);
        var (_, _, group2) = await SeedGroupAsync(teacher.Id);

        await _client.PostAsJsonAsync("/api/teacher/sessions", new CreateSessionRequest
        {
            GroupId = group1.Id,
            Title = "Session 1 Group 1",
            Status = "Active"
        });

        await _client.PostAsJsonAsync("/api/teacher/sessions", new CreateSessionRequest
        {
            GroupId = group2.Id,
            Title = "Session 1 Group 2",
            Status = "Draft"
        });

        // Query group 1
        var list1 = await _client.GetFromJsonAsync<List<SessionSummaryDto>>($"/api/teacher/sessions?groupId={group1.Id}");
        Assert.NotNull(list1);
        Assert.Single(list1);
        Assert.Equal("Session 1 Group 1", list1[0].Title);

        // Query status Draft
        var draftList = await _client.GetFromJsonAsync<List<SessionSummaryDto>>("/api/teacher/sessions?status=Draft");
        Assert.NotNull(draftList);
        Assert.Contains(draftList, s => s.Title == "Session 1 Group 2");
    }

    [Fact]
    public async Task UpdateSessionStatus_CloseActiveSession_AutoReconcilesAbsentStudents()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("ses_t4_" + Guid.NewGuid().ToString("N")[..6]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (_, _, group) = await SeedGroupAsync(teacher.Id);

        // Enroll 2 students in group
        using var db = _factory.CreateDbContext();
        var s1 = new Student
        {
            Id = Guid.NewGuid(),
            TeacherId = teacher.Id,
            StudentCode = "HD-" + Guid.NewGuid().ToString("N")[..6].ToUpper(),
            PasswordHash = "h",
            FullName = "Student 1",
            CreatedAtUtc = DateTime.UtcNow
        };
        var s2 = new Student
        {
            Id = Guid.NewGuid(),
            TeacherId = teacher.Id,
            StudentCode = "HD-" + Guid.NewGuid().ToString("N")[..6].ToUpper(),
            PasswordHash = "h",
            FullName = "Student 2",
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Students.AddRange(s1, s2);
        db.GroupEnrollments.Add(new GroupEnrollment { Id = Guid.NewGuid(), TeacherId = teacher.Id, GroupId = group.Id, StudentId = s1.Id, IsActive = true });
        db.GroupEnrollments.Add(new GroupEnrollment { Id = Guid.NewGuid(), TeacherId = teacher.Id, GroupId = group.Id, StudentId = s2.Id, IsActive = true });
        await db.SaveChangesAsync();

        // Create Active session
        var createRes = await _client.PostAsJsonAsync("/api/teacher/sessions", new CreateSessionRequest
        {
            GroupId = group.Id,
            Title = "Live Lecture",
            Status = "Active"
        });
        var session = await createRes.Content.ReadFromJsonAsync<SessionDetailDto>();

        // Student 1 is marked present
        await _client.PostAsJsonAsync($"/api/teacher/sessions/{session!.Id}/attendance", new RecordAttendanceRequest
        {
            StudentId = s1.Id,
            Status = "Present"
        });

        // Now Close the session -> Rule BR-ATT-061: Student 2 must be auto-marked Absent!
        var patchRes = await _client.PatchAsJsonAsync($"/api/teacher/sessions/{session.Id}/status", new UpdateSessionStatusRequest
        {
            Status = "Closed"
        });
        Assert.Equal(HttpStatusCode.OK, patchRes.StatusCode);

        using var verifyDb = _factory.CreateDbContext();
        var records = await verifyDb.AttendanceRecords.Where(r => r.SessionId == session.Id).ToListAsync();
        Assert.Equal(2, records.Count);

        var s1Record = records.FirstOrDefault(r => r.StudentId == s1.Id);
        Assert.NotNull(s1Record);
        Assert.Equal("Present", s1Record.Status);

        var s2Record = records.FirstOrDefault(r => r.StudentId == s2.Id);
        Assert.NotNull(s2Record);
        Assert.Equal("Absent", s2Record.Status); // Auto-marked!
    }

    [Fact]
    public async Task GetSessionQrToken_ActiveSession_ReturnsTokenAndMetadata()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("ses_t5_" + Guid.NewGuid().ToString("N")[..6]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (_, _, group) = await SeedGroupAsync(teacher.Id);

        var createRes = await _client.PostAsJsonAsync("/api/teacher/sessions", new CreateSessionRequest
        {
            GroupId = group.Id,
            Title = "QR Session",
            Status = "Active"
        });
        var session = await createRes.Content.ReadFromJsonAsync<SessionDetailDto>();

        var qrRes = await _client.GetAsync($"/api/teacher/sessions/{session!.Id}/qr-token");
        Assert.Equal(HttpStatusCode.OK, qrRes.StatusCode);

        var qrDto = await qrRes.Content.ReadFromJsonAsync<SessionQrTokenDto>();
        Assert.NotNull(qrDto);
        Assert.Equal(session.Id, qrDto.SessionId);
        Assert.False(string.IsNullOrWhiteSpace(qrDto.QrToken));
        Assert.InRange(qrDto.RemainingSeconds, 1, 20);
    }

    [Fact]
    public async Task GetSessionQrToken_ClosedSession_ReturnsBadRequest()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("ses_t6_" + Guid.NewGuid().ToString("N")[..6]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (_, _, group) = await SeedGroupAsync(teacher.Id);

        var createRes = await _client.PostAsJsonAsync("/api/teacher/sessions", new CreateSessionRequest
        {
            GroupId = group.Id,
            Title = "Closed QR Session",
            Status = "Draft"
        });
        var session = await createRes.Content.ReadFromJsonAsync<SessionDetailDto>();

        var qrRes = await _client.GetAsync($"/api/teacher/sessions/{session!.Id}/qr-token");
        Assert.Equal(HttpStatusCode.BadRequest, qrRes.StatusCode);
    }

    [Fact]
    public async Task DeleteSession_WithRecords_ReturnsBadRequest()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("ses_t7_" + Guid.NewGuid().ToString("N")[..6]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (_, _, group) = await SeedGroupAsync(teacher.Id);

        using var db = _factory.CreateDbContext();
        var s = new Student
        {
            Id = Guid.NewGuid(),
            TeacherId = teacher.Id,
            StudentCode = "HD-" + Guid.NewGuid().ToString("N")[..6].ToUpper(),
            PasswordHash = "h",
            FullName = "Student",
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Students.Add(s);
        db.GroupEnrollments.Add(new GroupEnrollment { Id = Guid.NewGuid(), TeacherId = teacher.Id, GroupId = group.Id, StudentId = s.Id, IsActive = true });
        await db.SaveChangesAsync();

        var createRes = await _client.PostAsJsonAsync("/api/teacher/sessions", new CreateSessionRequest
        {
            GroupId = group.Id,
            Title = "Session To Delete",
            Status = "Active"
        });
        var session = await createRes.Content.ReadFromJsonAsync<SessionDetailDto>();

        // Record attendance
        await _client.PostAsJsonAsync($"/api/teacher/sessions/{session!.Id}/attendance", new RecordAttendanceRequest
        {
            StudentId = s.Id,
            Status = "Present"
        });

        // Try deleting
        var deleteRes = await _client.DeleteAsync($"/api/teacher/sessions/{session.Id}");
        Assert.Equal(HttpStatusCode.BadRequest, deleteRes.StatusCode);
    }
}
