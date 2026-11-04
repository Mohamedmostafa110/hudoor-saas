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

public class RolloverEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public RolloverEndpointsTests(CustomWebApplicationFactory factory)
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

    private async Task<(AcademicYear Year, Group Group, List<Student> Students, Session Session)> SeedYearWithDataAsync(Guid teacherId)
    {
        using var db = _factory.CreateDbContext();
        var year = new AcademicYear
        {
            Id = Guid.NewGuid(),
            TeacherId = teacherId,
            Name = "Academic Year 2025/2026",
            StartDate = new DateTime(2025, 9, 1),
            EndDate = new DateTime(2026, 6, 30),
            IsActive = true,
            IsArchived = false
        };
        var subject = new Subject
        {
            Id = Guid.NewGuid(),
            TeacherId = teacherId,
            Name = "Mathematics " + Guid.NewGuid().ToString("N")[..6],
            GradeLevel = "1st Secondary"
        };
        var group = new Group
        {
            Id = Guid.NewGuid(),
            TeacherId = teacherId,
            SubjectId = subject.Id,
            AcademicYearId = year.Id,
            Name = "Math Group 101"
        };
        var session = new Session
        {
            Id = Guid.NewGuid(),
            TeacherId = teacherId,
            GroupId = group.Id,
            Title = "Final Calculus Session",
            SessionDateUtc = DateTime.UtcNow.AddMonths(-1),
            Status = "Closed"
        };

        db.AcademicYears.Add(year);
        db.Subjects.Add(subject);
        db.Groups.Add(group);
        db.Sessions.Add(session);

        var students = new List<Student>();
        for (int i = 1; i <= 3; i++)
        {
            var student = new Student
            {
                Id = Guid.NewGuid(),
                TeacherId = teacherId,
                StudentCode = "HD-" + Guid.NewGuid().ToString("N")[..6].ToUpper(),
                PasswordHash = "h",
                FullName = $"Student {i}",
                CurrentGrade = "1st Secondary",
                IsActive = true,
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
            db.AttendanceRecords.Add(new AttendanceRecord
            {
                Id = Guid.NewGuid(),
                TeacherId = teacherId,
                SessionId = session.Id,
                StudentId = student.Id,
                Status = "Present",
                Method = "QRCode",
                RecordedAtUtc = DateTime.UtcNow.AddMonths(-1)
            });
        }

        await db.SaveChangesAsync();
        return (year, group, students, session);
    }

    [Fact]
    public async Task GetRolloverSummary_Success()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("roll_t1_" + Guid.NewGuid().ToString("N")[..6]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (year, _, students, _) = await SeedYearWithDataAsync(teacher.Id);

        var response = await _client.GetAsync($"/api/teacher/academic-years/{year.Id}/rollover-summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var summary = await response.Content.ReadFromJsonAsync<RolloverSummaryDto>();
        Assert.NotNull(summary);
        Assert.Equal(year.Id, summary.CurrentAcademicYearId);
        Assert.Equal(1, summary.TotalGroups);
        Assert.Equal(students.Count, summary.TotalEnrolledStudents);
        Assert.Single(summary.StudentsByGrade);
        Assert.Equal("1st Secondary", summary.StudentsByGrade[0].Grade);
        Assert.Equal(3, summary.StudentsByGrade[0].StudentCount);
    }

    [Fact]
    public async Task GetRolloverSummary_CrossTenant_ReturnsNotFound()
    {
        var (teacher1, _) = await CreateAndLoginTeacherAsync("roll_t2a_" + Guid.NewGuid().ToString("N")[..6]);
        var (_, token2) = await CreateAndLoginTeacherAsync("roll_t2b_" + Guid.NewGuid().ToString("N")[..6]);

        var (year1, _, _, _) = await SeedYearWithDataAsync(teacher1.Id);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token2);
        var response = await _client.GetAsync($"/api/teacher/academic-years/{year1.Id}/rollover-summary");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ExecuteRollover_Success_PreservesAllHistoricalData()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("roll_t3_" + Guid.NewGuid().ToString("N")[..6]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (year, group, students, session) = await SeedYearWithDataAsync(teacher.Id);

        // Create a new target group for next year
        using var db = _factory.CreateDbContext();
        var targetGroup = new Group
        {
            Id = Guid.NewGuid(),
            TeacherId = teacher.Id,
            SubjectId = group.SubjectId,
            AcademicYearId = year.Id, // initially temporary or will be updated
            Name = "Advanced Math 201"
        };
        db.Groups.Add(targetGroup);
        await db.SaveChangesAsync();

        var request = new ExecuteRolloverRequest
        {
            NewAcademicYearName = "Academic Year 2026/2027",
            StartDate = new DateTime(2026, 9, 1),
            EndDate = new DateTime(2027, 6, 30),
            StudentProgressions = new List<StudentProgressionItemRequest>
            {
                // Student 1: Promote to 2nd Secondary & enroll in target group
                new()
                {
                    StudentId = students[0].Id,
                    Action = "Promote",
                    NewGrade = "2nd Secondary",
                    TargetGroupId = targetGroup.Id
                },
                // Student 2: Repeat 1st Secondary & enroll in target group
                new()
                {
                    StudentId = students[1].Id,
                    Action = "Repeat",
                    TargetGroupId = targetGroup.Id
                },
                // Student 3: Depart
                new()
                {
                    StudentId = students[2].Id,
                    Action = "Depart"
                }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/teacher/academic-years/rollover", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<RolloverResultDto>();
        Assert.NotNull(result);
        Assert.Equal(year.Id, result.PreviousAcademicYearId);
        Assert.Equal(1, result.PromotedCount);
        Assert.Equal(1, result.RepeatedCount);
        Assert.Equal(1, result.DepartedCount);
        Assert.Equal(3, result.TotalProcessed);

        // Verification in database
        using var verifyDb = _factory.CreateDbContext();

        // 1. Check previous year is archived and inactive
        var oldYearInDb = await verifyDb.AcademicYears.FindAsync(year.Id);
        Assert.NotNull(oldYearInDb);
        Assert.False(oldYearInDb.IsActive);
        Assert.True(oldYearInDb.IsArchived);

        // 2. Check new year is active and unarchived
        var newYearInDb = await verifyDb.AcademicYears.FindAsync(result.NewAcademicYearId);
        Assert.NotNull(newYearInDb);
        Assert.True(newYearInDb.IsActive);
        Assert.False(newYearInDb.IsArchived);
        Assert.Equal("Academic Year 2026/2027", newYearInDb.Name);

        // 3. Check Student 1 is promoted to 2nd Secondary and enrolled in target group
        var s1 = await verifyDb.Students.Include(s => s.Enrollments).FirstOrDefaultAsync(s => s.Id == students[0].Id);
        Assert.NotNull(s1);
        Assert.Equal("2nd Secondary", s1.CurrentGrade);
        Assert.True(s1.IsActive);
        Assert.Contains(s1.Enrollments, e => e.GroupId == targetGroup.Id && e.IsActive);

        // 4. Check Student 2 repeated (grade unchanged) and enrolled in target group
        var s2 = await verifyDb.Students.Include(s => s.Enrollments).FirstOrDefaultAsync(s => s.Id == students[1].Id);
        Assert.NotNull(s2);
        Assert.Equal("1st Secondary", s2.CurrentGrade);
        Assert.True(s2.IsActive);
        Assert.Contains(s2.Enrollments, e => e.GroupId == targetGroup.Id && e.IsActive);

        // 5. Check Student 3 departed (inactive)
        var s3 = await verifyDb.Students.FindAsync(students[2].Id);
        Assert.NotNull(s3);
        Assert.False(s3.IsActive);

        // 6. RULE BR-ROL-090: Zero data loss guarantee:
        // All historical sessions and attendance records are completely intact!
        var sessionInDb = await verifyDb.Sessions.Include(s => s.AttendanceRecords).FirstOrDefaultAsync(s => s.Id == session.Id);
        Assert.NotNull(sessionInDb);
        Assert.Equal(3, sessionInDb.AttendanceRecords.Count);
        Assert.All(sessionInDb.AttendanceRecords, r => Assert.Equal("Present", r.Status));
    }

    [Fact]
    public async Task ExecuteRollover_CrossTenantStudent_ReturnsBadRequest()
    {
        var (teacher1, _) = await CreateAndLoginTeacherAsync("roll_t4a_" + Guid.NewGuid().ToString("N")[..6]);
        var (teacher2, token2) = await CreateAndLoginTeacherAsync("roll_t4b_" + Guid.NewGuid().ToString("N")[..6]);

        var (year1, _, students1, _) = await SeedYearWithDataAsync(teacher1.Id);
        var (year2, _, _, _) = await SeedYearWithDataAsync(teacher2.Id);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token2);

        // Teacher 2 tries to rollover their year but includes Teacher 1's student
        var request = new ExecuteRolloverRequest
        {
            NewAcademicYearName = "Year 2026/2027",
            StartDate = new DateTime(2026, 9, 1),
            EndDate = new DateTime(2027, 6, 30),
            StudentProgressions = new List<StudentProgressionItemRequest>
            {
                new()
                {
                    StudentId = students1[0].Id, // Belongs to Teacher 1!
                    Action = "Promote"
                }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/teacher/academic-years/rollover", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
