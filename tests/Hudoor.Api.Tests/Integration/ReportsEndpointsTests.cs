using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Hudoor.Api.Data.Entities;
using Hudoor.Api.DTOs;
using Hudoor.Api.Services;
using Hudoor.Api.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Hudoor.Api.Tests.Integration;

public class ReportsEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ReportsEndpointsTests(CustomWebApplicationFactory factory)
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

    private async Task<(Group Group, Session Session, Student Student)> SeedDataAsync(Guid teacherId)
    {
        using var db = _factory.CreateDbContext();
        var subject = new Subject { Id = Guid.NewGuid(), TeacherId = teacherId, Name = "Physics", GradeLevel = "1st Secondary" };
        var year = new AcademicYear { Id = Guid.NewGuid(), TeacherId = teacherId, Name = "2025/2026", StartDate = new DateTime(2025, 9, 1), EndDate = new DateTime(2026, 6, 30), IsActive = true };
        var group = new Group { Id = Guid.NewGuid(), TeacherId = teacherId, SubjectId = subject.Id, AcademicYearId = year.Id, Name = "Physics Group A" };
        var session = new Session { Id = Guid.NewGuid(), TeacherId = teacherId, GroupId = group.Id, Title = "Session 1", SessionDateUtc = DateTime.UtcNow, Status = "Closed" };
        var student = new Student { Id = Guid.NewGuid(), TeacherId = teacherId, StudentCode = "HD-" + Guid.NewGuid().ToString("N")[..6].ToUpper(), PasswordHash = "h", FullName = "أحمد مصطفى", CurrentGrade = "1st Secondary", IsActive = true, CreatedAtUtc = DateTime.UtcNow };

        db.Subjects.Add(subject);
        db.AcademicYears.Add(year);
        db.Groups.Add(group);
        db.Sessions.Add(session);
        db.Students.Add(student);
        db.GroupEnrollments.Add(new GroupEnrollment { Id = Guid.NewGuid(), TeacherId = teacherId, GroupId = group.Id, StudentId = student.Id, IsActive = true });
        db.AttendanceRecords.Add(new AttendanceRecord { Id = Guid.NewGuid(), TeacherId = teacherId, SessionId = session.Id, StudentId = student.Id, Status = "Present", Method = "QRCode", RecordedAtUtc = DateTime.UtcNow });

        await db.SaveChangesAsync();
        return (group, session, student);
    }

    [Fact]
    public async Task GetTeacherAnalytics_Success()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("rep_t1_" + Guid.NewGuid().ToString("N")[..6]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await SeedDataAsync(teacher.Id);

        var analytics = await _client.GetFromJsonAsync<TeacherAnalyticsSummaryDto>("/api/teacher/reports/analytics");
        Assert.NotNull(analytics);
        Assert.True(analytics.TotalStudents >= 1);
        Assert.True(analytics.TotalSessions >= 1);
        Assert.True(analytics.TotalPresentCount >= 1);
        Assert.Equal(100.0, analytics.OverallAttendanceRate);
        Assert.Single(analytics.GroupSummaries);
    }

    [Fact]
    public async Task GetAtRiskStudents_Success()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("rep_t2_" + Guid.NewGuid().ToString("N")[..6]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var db = _factory.CreateDbContext();
        var subject = new Subject { Id = Guid.NewGuid(), TeacherId = teacher.Id, Name = "Math", GradeLevel = "1st Secondary" };
        var year = new AcademicYear { Id = Guid.NewGuid(), TeacherId = teacher.Id, Name = "2025/2026", StartDate = new DateTime(2025, 9, 1), EndDate = new DateTime(2026, 6, 30), IsActive = true };
        var group = new Group { Id = Guid.NewGuid(), TeacherId = teacher.Id, SubjectId = subject.Id, AcademicYearId = year.Id, Name = "Math Group" };
        var s1 = new Session { Id = Guid.NewGuid(), TeacherId = teacher.Id, GroupId = group.Id, Title = "S1", SessionDateUtc = DateTime.UtcNow.AddDays(-3), Status = "Closed" };
        var s2 = new Session { Id = Guid.NewGuid(), TeacherId = teacher.Id, GroupId = group.Id, Title = "S2", SessionDateUtc = DateTime.UtcNow.AddDays(-2), Status = "Closed" };
        var s3 = new Session { Id = Guid.NewGuid(), TeacherId = teacher.Id, GroupId = group.Id, Title = "S3", SessionDateUtc = DateTime.UtcNow.AddDays(-1), Status = "Closed" };
        var student = new Student { Id = Guid.NewGuid(), TeacherId = teacher.Id, StudentCode = "HD-" + Guid.NewGuid().ToString("N")[..6].ToUpper(), PasswordHash = "h", FullName = "Absent Student", IsActive = true, CreatedAtUtc = DateTime.UtcNow };

        db.Subjects.Add(subject);
        db.AcademicYears.Add(year);
        db.Groups.Add(group);
        db.Sessions.AddRange(s1, s2, s3);
        db.Students.Add(student);
        db.GroupEnrollments.Add(new GroupEnrollment { Id = Guid.NewGuid(), TeacherId = teacher.Id, GroupId = group.Id, StudentId = student.Id, IsActive = true });
        // 3 consecutive absences
        db.AttendanceRecords.Add(new AttendanceRecord { Id = Guid.NewGuid(), TeacherId = teacher.Id, SessionId = s1.Id, StudentId = student.Id, Status = "Absent", Method = "Manual" });
        db.AttendanceRecords.Add(new AttendanceRecord { Id = Guid.NewGuid(), TeacherId = teacher.Id, SessionId = s2.Id, StudentId = student.Id, Status = "Absent", Method = "Manual" });
        db.AttendanceRecords.Add(new AttendanceRecord { Id = Guid.NewGuid(), TeacherId = teacher.Id, SessionId = s3.Id, StudentId = student.Id, Status = "Absent", Method = "Manual" });
        await db.SaveChangesAsync();

        var atRiskList = await _client.GetFromJsonAsync<List<AtRiskStudentDto>>("/api/teacher/reports/at-risk?minAbsences=3");
        Assert.NotNull(atRiskList);
        Assert.Contains(atRiskList, s => s.StudentId == student.Id);
        Assert.Equal(3, atRiskList.First(s => s.StudentId == student.Id).AbsentSessions);
    }

    [Fact]
    public async Task GetGroupAttendanceMatrix_Success()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("rep_t3_" + Guid.NewGuid().ToString("N")[..6]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (group, session, student) = await SeedDataAsync(teacher.Id);

        var matrix = await _client.GetFromJsonAsync<GroupAttendanceMatrixDto>($"/api/teacher/reports/groups/{group.Id}/matrix");
        Assert.NotNull(matrix);
        Assert.Equal(group.Id, matrix.GroupId);
        Assert.Single(matrix.Sessions);
        Assert.Single(matrix.Students);

        var row = matrix.Students[0];
        Assert.Equal(student.Id, row.StudentId);
        Assert.Equal("Present", row.SessionStatuses[session.Id]);
        Assert.Equal(1, row.TotalPresent);
    }

    [Fact]
    public async Task ExportGroupAttendance_CsvWithUtf8Bom_Success()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("rep_t4_" + Guid.NewGuid().ToString("N")[..6]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (group, _, student) = await SeedDataAsync(teacher.Id);

        var response = await _client.GetAsync($"/api/teacher/reports/export/group-attendance?groupId={group.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv; charset=utf-8", response.Content.Headers.ContentType?.ToString());

        var bytes = await response.Content.ReadAsByteArrayAsync();
        // Check UTF-8 BOM
        Assert.True(bytes.Length >= 3);
        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xBF, bytes[2]);

        var content = Encoding.UTF8.GetString(bytes[3..]);
        Assert.Contains(student.FullName, content);
        Assert.Contains("كود الطالب", content);
    }

    [Fact]
    public async Task ExportSessionAttendance_Success()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("rep_t5_" + Guid.NewGuid().ToString("N")[..6]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (_, session, student) = await SeedDataAsync(teacher.Id);

        var response = await _client.GetAsync($"/api/teacher/reports/export/session-attendance?sessionId={session.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        var content = Encoding.UTF8.GetString(bytes[3..]);
        Assert.Contains(student.FullName, content);
        Assert.Contains("Present", content);
    }

    [Fact]
    public async Task ExportStudents_Success()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("rep_t6_" + Guid.NewGuid().ToString("N")[..6]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (_, _, student) = await SeedDataAsync(teacher.Id);

        var response = await _client.GetAsync("/api/teacher/reports/export/students");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        var content = Encoding.UTF8.GetString(bytes[3..]);
        Assert.Contains(student.FullName, content);
        Assert.Contains("اسم الطالب", content);
    }

    [Fact]
    public async Task GetGroupAttendanceMatrix_CrossTenantGroup_ReturnsNotFound()
    {
        var (teacher1, _) = await CreateAndLoginTeacherAsync("rep_t7a_" + Guid.NewGuid().ToString("N")[..6]);
        var (_, token2) = await CreateAndLoginTeacherAsync("rep_t7b_" + Guid.NewGuid().ToString("N")[..6]);

        var (teacher1Group, _, _) = await SeedDataAsync(teacher1.Id);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token2);
        var response = await _client.GetAsync($"/api/teacher/reports/groups/{teacher1Group.Id}/matrix");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
