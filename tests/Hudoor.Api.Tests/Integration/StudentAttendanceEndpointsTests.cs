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

public class StudentAttendanceEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public StudentAttendanceEndpointsTests(CustomWebApplicationFactory factory)
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

    private async Task<(Student Student, string Token)> SeedAndLoginStudentAsync(Guid teacherId, string studentCode, string password)
    {
        using var scope = _factory.Services.CreateScope();
        var db = _factory.CreateDbContext();
        var pwd = scope.ServiceProvider.GetRequiredService<IPasswordService>();

        var student = new Student
        {
            Id = Guid.NewGuid(),
            TeacherId = teacherId,
            StudentCode = studentCode,
            PasswordHash = pwd.HashPassword(password),
            FullName = $"Student {studentCode}",
            IsActive = true,
            MustChangePassword = false,
            TokenVersion = 1,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        var loginRes = await _client.PostAsJsonAsync("/api/auth/student/login", new StudentLoginRequest
        {
            StudentCode = studentCode,
            Password = password
        });
        loginRes.EnsureSuccessStatusCode();
        var auth = await loginRes.Content.ReadFromJsonAsync<AuthResponse>();

        return (student, auth!.Token);
    }

    private async Task<(Group Group, Session Session)> SeedGroupAndSessionAsync(Guid teacherId, string status = "Active")
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
        var session = new Session
        {
            Id = Guid.NewGuid(),
            TeacherId = teacherId,
            GroupId = group.Id,
            Title = "Active Class Session",
            SessionDateUtc = DateTime.UtcNow,
            Status = status,
            QrSecuritySalt = "qr_salt_" + Guid.NewGuid().ToString("N")
        };

        db.Subjects.Add(subject);
        db.AcademicYears.Add(year);
        db.Groups.Add(group);
        db.Sessions.Add(session);
        await db.SaveChangesAsync();

        return (group, session);
    }

    private async Task EnrollStudentAsync(Guid teacherId, Guid groupId, Guid studentId)
    {
        using var db = _factory.CreateDbContext();
        db.GroupEnrollments.Add(new GroupEnrollment
        {
            Id = Guid.NewGuid(),
            TeacherId = teacherId,
            GroupId = groupId,
            StudentId = studentId,
            IsActive = true
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task StudentCheckIn_ValidToken_Success()
    {
        var (teacher, _) = await CreateAndLoginTeacherAsync("st_att_t1_" + Guid.NewGuid().ToString("N")[..6]);
        var (group, session) = await SeedGroupAndSessionAsync(teacher.Id, "Active");

        var studentCode = "HD-" + Guid.NewGuid().ToString("N")[..6].ToUpper();
        var (student, studentToken) = await SeedAndLoginStudentAsync(teacher.Id, studentCode, "StudentPass2026!");
        await EnrollStudentAsync(teacher.Id, group.Id, student.Id);

        // Generate live QR token using QrCodeService
        using var scope = _factory.Services.CreateScope();
        var qrService = scope.ServiceProvider.GetRequiredService<IQrCodeService>();
        var (token, _, _) = qrService.GenerateToken(session.Id, session.QrSecuritySalt!);

        // Student scans QR code and submits check-in
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var response = await _client.PostAsJsonAsync("/api/student/attendance/check-in", new StudentCheckInRequest
        {
            QrToken = token
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<StudentCheckInResponseDto>();
        Assert.NotNull(result);
        Assert.Equal(session.Id, result.SessionId);
        Assert.Equal("Present", result.Status);
        Assert.Equal("QRCode", result.Method);

        // Verify in DB
        using var db = _factory.CreateDbContext();
        var record = await db.AttendanceRecords.FirstOrDefaultAsync(r => r.SessionId == session.Id && r.StudentId == student.Id);
        Assert.NotNull(record);
        Assert.Equal("Present", record.Status);
        Assert.Equal("QRCode", record.Method);
    }

    [Fact]
    public async Task StudentCheckIn_DuplicateCheckIn_ReturnsConflict()
    {
        var (teacher, _) = await CreateAndLoginTeacherAsync("st_att_t2_" + Guid.NewGuid().ToString("N")[..6]);
        var (group, session) = await SeedGroupAndSessionAsync(teacher.Id, "Active");

        var studentCode = "HD-" + Guid.NewGuid().ToString("N")[..6].ToUpper();
        var (student, studentToken) = await SeedAndLoginStudentAsync(teacher.Id, studentCode, "StudentPass2026!");
        await EnrollStudentAsync(teacher.Id, group.Id, student.Id);

        using var scope = _factory.Services.CreateScope();
        var qrService = scope.ServiceProvider.GetRequiredService<IQrCodeService>();
        var (token, _, _) = qrService.GenerateToken(session.Id, session.QrSecuritySalt!);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);

        // First check-in
        var res1 = await _client.PostAsJsonAsync("/api/student/attendance/check-in", new StudentCheckInRequest { QrToken = token });
        Assert.Equal(HttpStatusCode.OK, res1.StatusCode);

        // Duplicate check-in -> 409 Conflict
        var res2 = await _client.PostAsJsonAsync("/api/student/attendance/check-in", new StudentCheckInRequest { QrToken = token });
        Assert.Equal(HttpStatusCode.Conflict, res2.StatusCode);
    }

    [Fact]
    public async Task StudentCheckIn_ExpiredToken_ReturnsBadRequest()
    {
        var (teacher, _) = await CreateAndLoginTeacherAsync("st_att_t3_" + Guid.NewGuid().ToString("N")[..6]);
        var (group, session) = await SeedGroupAndSessionAsync(teacher.Id, "Active");

        var studentCode = "HD-" + Guid.NewGuid().ToString("N")[..6].ToUpper();
        var (student, studentToken) = await SeedAndLoginStudentAsync(teacher.Id, studentCode, "StudentPass2026!");
        await EnrollStudentAsync(teacher.Id, group.Id, student.Id);

        using var scope = _factory.Services.CreateScope();
        var qrService = scope.ServiceProvider.GetRequiredService<IQrCodeService>();

        // Token generated 60 seconds in the past
        var oldTime = DateTime.UtcNow.AddSeconds(-60);
        var (oldToken, _, _) = qrService.GenerateToken(session.Id, session.QrSecuritySalt!, oldTime);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var response = await _client.PostAsJsonAsync("/api/student/attendance/check-in", new StudentCheckInRequest
        {
            QrToken = oldToken
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task StudentCheckIn_NotEnrolledInGroup_ReturnsBadRequest()
    {
        var (teacher, _) = await CreateAndLoginTeacherAsync("st_att_t4_" + Guid.NewGuid().ToString("N")[..6]);
        var (_, session) = await SeedGroupAndSessionAsync(teacher.Id, "Active");

        var studentCode = "HD-" + Guid.NewGuid().ToString("N")[..6].ToUpper();
        var (_, studentToken) = await SeedAndLoginStudentAsync(teacher.Id, studentCode, "StudentPass2026!");
        // Note: student is NOT enrolled in session's group

        using var scope = _factory.Services.CreateScope();
        var qrService = scope.ServiceProvider.GetRequiredService<IQrCodeService>();
        var (token, _, _) = qrService.GenerateToken(session.Id, session.QrSecuritySalt!);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var response = await _client.PostAsJsonAsync("/api/student/attendance/check-in", new StudentCheckInRequest
        {
            QrToken = token
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task StudentCheckIn_ClosedSession_ReturnsBadRequest()
    {
        var (teacher, _) = await CreateAndLoginTeacherAsync("st_att_t5_" + Guid.NewGuid().ToString("N")[..6]);
        var (group, session) = await SeedGroupAndSessionAsync(teacher.Id, "Closed"); // Closed session

        var studentCode = "HD-" + Guid.NewGuid().ToString("N")[..6].ToUpper();
        var (student, studentToken) = await SeedAndLoginStudentAsync(teacher.Id, studentCode, "StudentPass2026!");
        await EnrollStudentAsync(teacher.Id, group.Id, student.Id);

        using var scope = _factory.Services.CreateScope();
        var qrService = scope.ServiceProvider.GetRequiredService<IQrCodeService>();
        var (token, _, _) = qrService.GenerateToken(session.Id, session.QrSecuritySalt!);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var response = await _client.PostAsJsonAsync("/api/student/attendance/check-in", new StudentCheckInRequest
        {
            QrToken = token
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task StudentAttendance_HistoryAndSummary_Success()
    {
        var (teacher, _) = await CreateAndLoginTeacherAsync("st_att_t6_" + Guid.NewGuid().ToString("N")[..6]);
        var (group, session1) = await SeedGroupAndSessionAsync(teacher.Id, "Active");
        var (_, session2) = await SeedGroupAndSessionAsync(teacher.Id, "Active");

        var studentCode = "HD-" + Guid.NewGuid().ToString("N")[..6].ToUpper();
        var (student, studentToken) = await SeedAndLoginStudentAsync(teacher.Id, studentCode, "StudentPass2026!");
        await EnrollStudentAsync(teacher.Id, group.Id, student.Id);

        // Seed 2 attendance records in DB: 1 Present, 1 Late
        using var db = _factory.CreateDbContext();
        db.AttendanceRecords.Add(new AttendanceRecord
        {
            Id = Guid.NewGuid(),
            TeacherId = teacher.Id,
            SessionId = session1.Id,
            StudentId = student.Id,
            Status = "Present",
            Method = "QRCode",
            RecordedAtUtc = DateTime.UtcNow.AddDays(-1)
        });
        db.AttendanceRecords.Add(new AttendanceRecord
        {
            Id = Guid.NewGuid(),
            TeacherId = teacher.Id,
            SessionId = session2.Id,
            StudentId = student.Id,
            Status = "Late",
            MinutesLate = 10,
            Method = "Manual",
            RecordedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);

        // 1. History
        var history = await _client.GetFromJsonAsync<List<StudentAttendanceHistoryDto>>("/api/student/attendance/history");
        Assert.NotNull(history);
        Assert.Equal(2, history.Count);

        // 2. Summary
        var summary = await _client.GetFromJsonAsync<StudentAttendanceSummaryDto>("/api/student/attendance/summary");
        Assert.NotNull(summary);
        Assert.Equal(2, summary.TotalSessions);
        Assert.Equal(1, summary.PresentCount);
        Assert.Equal(1, summary.LateCount);
        Assert.Equal(0, summary.AbsentCount);
        Assert.Equal(100.0, summary.AttendancePercentage); // (1 Present + 1 Late) / 2 = 100%
    }
}
