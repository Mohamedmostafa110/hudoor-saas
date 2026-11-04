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

public class AttendanceEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AttendanceEndpointsTests(CustomWebApplicationFactory factory)
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

    private async Task<(Group Group, Session Session, List<Student> Students)> SeedSessionWithStudentsAsync(Guid teacherId, int studentCount = 2)
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
            Title = "Class Session",
            SessionDateUtc = DateTime.UtcNow,
            Status = "Active",
            QrSecuritySalt = "salt123"
        };

        db.Subjects.Add(subject);
        db.AcademicYears.Add(year);
        db.Groups.Add(group);
        db.Sessions.Add(session);

        var students = new List<Student>();
        for (int i = 0; i < studentCount; i++)
        {
            var student = new Student
            {
                Id = Guid.NewGuid(),
                TeacherId = teacherId,
                StudentCode = "HD-" + Guid.NewGuid().ToString("N")[..6].ToUpper(),
                PasswordHash = "h",
                FullName = $"Student {i + 1}",
                CreatedAtUtc = DateTime.UtcNow
            };
            students.Add(student);
            db.Students.Add(student);
            db.GroupEnrollments.Add(new GroupEnrollment
            {
                Id = Guid.NewGuid(),
                TeacherId = teacherId,
                GroupId = group.Id,
                StudentId = student.Id,
                IsActive = true
            });
        }

        await db.SaveChangesAsync();

        return (group, session, students);
    }

    [Fact]
    public async Task RecordStudentAttendance_PresentAndLate_Success()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("att_t1_" + Guid.NewGuid().ToString("N")[..6]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (_, session, students) = await SeedSessionWithStudentsAsync(teacher.Id, 2);

        // Mark student 1 Present
        var res1 = await _client.PostAsJsonAsync($"/api/teacher/sessions/{session.Id}/attendance", new RecordAttendanceRequest
        {
            StudentId = students[0].Id,
            Status = "Present"
        });
        Assert.Equal(HttpStatusCode.Created, res1.StatusCode);
        var rec1 = await res1.Content.ReadFromJsonAsync<AttendanceRecordResponseDto>();
        Assert.Equal("Present", rec1!.Status);
        Assert.Equal("Manual", rec1.Method);

        // Mark student 2 Late with 15 minutes
        var res2 = await _client.PostAsJsonAsync($"/api/teacher/sessions/{session.Id}/attendance", new RecordAttendanceRequest
        {
            StudentId = students[1].Id,
            Status = "Late",
            MinutesLate = 15,
            Notes = "Traffic delay"
        });
        Assert.Equal(HttpStatusCode.Created, res2.StatusCode);
        var rec2 = await res2.Content.ReadFromJsonAsync<AttendanceRecordResponseDto>();
        Assert.Equal("Late", rec2!.Status);
        Assert.Equal(15, rec2.MinutesLate);
    }

    [Fact]
    public async Task BulkRecordAttendance_Success()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("att_t2_" + Guid.NewGuid().ToString("N")[..6]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (_, session, students) = await SeedSessionWithStudentsAsync(teacher.Id, 3);

        var request = new BulkRecordAttendanceRequest
        {
            Records = new List<RecordAttendanceItemRequest>
            {
                new() { StudentId = students[0].Id, Status = "Present" },
                new() { StudentId = students[1].Id, Status = "Late", MinutesLate = 10 }
            },
            MarkRemainingAsAbsent = true // Student 3 should be marked Absent!
        };

        var response = await _client.PostAsJsonAsync($"/api/teacher/sessions/{session.Id}/attendance/bulk", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var db = _factory.CreateDbContext();
        var records = await db.AttendanceRecords.Where(r => r.SessionId == session.Id).ToListAsync();
        Assert.Equal(3, records.Count);
        Assert.Equal("Present", records.First(r => r.StudentId == students[0].Id).Status);
        Assert.Equal("Late", records.First(r => r.StudentId == students[1].Id).Status);
        Assert.Equal("Absent", records.First(r => r.StudentId == students[2].Id).Status);
    }

    [Fact]
    public async Task UpdateAttendanceRecord_CreatesImmutableAuditLog()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("att_t3_" + Guid.NewGuid().ToString("N")[..6]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (_, session, students) = await SeedSessionWithStudentsAsync(teacher.Id, 1);

        // Record initially as Absent
        var createRes = await _client.PostAsJsonAsync($"/api/teacher/sessions/{session.Id}/attendance", new RecordAttendanceRequest
        {
            StudentId = students[0].Id,
            Status = "Absent"
        });
        var created = await createRes.Content.ReadFromJsonAsync<AttendanceRecordResponseDto>();

        // Now modify to Present (Flipping Absent <-> Present requires reason under Rule BR-AUD-081)
        var updateReq = new UpdateAttendanceRecordRequest
        {
            Status = "Present",
            ModificationReason = "Student arrived after medical appointment with certified note."
        };

        var updateRes = await _client.PutAsJsonAsync($"/api/teacher/attendance/{created!.Id}", updateReq);
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);

        // Verify Audit Log in DB
        using var db = _factory.CreateDbContext();
        var auditLogs = await db.AttendanceAuditLogs.Where(l => l.AttendanceRecordId == created.Id).ToListAsync();
        Assert.Single(auditLogs);
        Assert.Equal("Absent", auditLogs[0].PreviousStatus);
        Assert.Equal("Present", auditLogs[0].NewStatus);
        Assert.Equal(teacher.Id, auditLogs[0].ModifiedByTeacherId);
        Assert.Equal("Student arrived after medical appointment with certified note.", auditLogs[0].ModificationReason);
    }

    [Fact]
    public async Task UpdateAttendanceRecord_FlippingAbsentPresentWithoutReason_ReturnsBadRequest()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("att_t4_" + Guid.NewGuid().ToString("N")[..6]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (_, session, students) = await SeedSessionWithStudentsAsync(teacher.Id, 1);

        var createRes = await _client.PostAsJsonAsync($"/api/teacher/sessions/{session.Id}/attendance", new RecordAttendanceRequest
        {
            StudentId = students[0].Id,
            Status = "Absent"
        });
        var created = await createRes.Content.ReadFromJsonAsync<AttendanceRecordResponseDto>();

        // Attempt flipping to Present without required ModificationReason
        var updateReq = new UpdateAttendanceRecordRequest
        {
            Status = "Present",
            ModificationReason = null // Missing reason!
        };

        var updateRes = await _client.PutAsJsonAsync($"/api/teacher/attendance/{created!.Id}", updateReq);
        Assert.Equal(HttpStatusCode.BadRequest, updateRes.StatusCode);
    }

    [Fact]
    public async Task GetSessionRoster_Success()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("att_t5_" + Guid.NewGuid().ToString("N")[..6]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (_, session, students) = await SeedSessionWithStudentsAsync(teacher.Id, 2);

        // Mark only student 1
        await _client.PostAsJsonAsync($"/api/teacher/sessions/{session.Id}/attendance", new RecordAttendanceRequest
        {
            StudentId = students[0].Id,
            Status = "Present"
        });

        var roster = await _client.GetFromJsonAsync<List<SessionRosterStudentDto>>($"/api/teacher/sessions/{session.Id}/roster");
        Assert.NotNull(roster);
        Assert.Equal(2, roster.Count);

        var s1 = roster.First(s => s.StudentId == students[0].Id);
        Assert.Equal("Present", s1.Status);

        var s2 = roster.First(s => s.StudentId == students[1].Id);
        Assert.Equal("Unrecorded", s2.Status);
    }
}
