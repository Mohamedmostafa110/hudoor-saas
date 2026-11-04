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

public class GroupsEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public GroupsEndpointsTests(CustomWebApplicationFactory factory)
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

    private async Task<(Subject Subject, AcademicYear Year)> SeedSubjectAndYearAsync(Guid teacherId, bool isYearArchived = false)
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
            IsActive = !isYearArchived,
            IsArchived = isYearArchived
        };
        db.Subjects.Add(subject);
        db.AcademicYears.Add(year);
        await db.SaveChangesAsync();

        return (subject, year);
    }

    [Fact]
    public async Task CreateGroup_WithValidSubjectAndYear_Returns201AndGroupDto()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("teacher_grp_create");
        var (subject, year) = await SeedSubjectAndYearAsync(teacher.Id);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/teacher/groups")
        {
            Content = JsonContent.Create(new CreateGroupRequest
            {
                SubjectId = subject.Id,
                AcademicYearId = year.Id,
                Name = "Group Alpha",
                ScheduleDescription = "Sundays & Wednesdays 4:00 PM",
                MaxCapacity = 30
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<GroupResponseDto>();
        Assert.NotNull(body);
        Assert.Equal("Group Alpha", body.Name);
        Assert.Equal(subject.Id, body.SubjectId);
        Assert.Equal(year.Id, body.AcademicYearId);
        Assert.Equal(30, body.MaxCapacity);
        Assert.Equal(0, body.EnrolledStudentsCount);
        Assert.False(body.IsArchived);
    }

    [Fact]
    public async Task CreateGroup_WhenSubjectBelongsToAnotherTeacher_Returns404NotFound()
    {
        var (teacherA, tokenA) = await CreateAndLoginTeacherAsync("teacher_grp_iso_a");
        var (teacherB, _) = await CreateAndLoginTeacherAsync("teacher_grp_iso_b");

        var (subjectB, _) = await SeedSubjectAndYearAsync(teacherB.Id); // Owned by B!
        var (_, yearA) = await SeedSubjectAndYearAsync(teacherA.Id);    // Owned by A

        // Teacher A attempts to link Teacher B's subject -> REJECTED!
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/teacher/groups")
        {
            Content = JsonContent.Create(new CreateGroupRequest
            {
                SubjectId = subjectB.Id,
                AcademicYearId = yearA.Id,
                Name = "Exploit Group"
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateGroup_WhenAcademicYearBelongsToAnotherTeacher_Returns404NotFound()
    {
        var (teacherA, tokenA) = await CreateAndLoginTeacherAsync("teacher_grp_iso_a2");
        var (teacherB, _) = await CreateAndLoginTeacherAsync("teacher_grp_iso_b2");

        var (subjectA, _) = await SeedSubjectAndYearAsync(teacherA.Id); // Owned by A
        var (_, yearB) = await SeedSubjectAndYearAsync(teacherB.Id);    // Owned by B!

        // Teacher A attempts to link Teacher B's academic year -> REJECTED!
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/teacher/groups")
        {
            Content = JsonContent.Create(new CreateGroupRequest
            {
                SubjectId = subjectA.Id,
                AcademicYearId = yearB.Id,
                Name = "Exploit Group 2"
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateGroup_WhenAcademicYearIsArchived_Returns400BadRequest()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("teacher_grp_arch_yr");
        var (subject, year) = await SeedSubjectAndYearAsync(teacher.Id, isYearArchived: true);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/teacher/groups")
        {
            Content = JsonContent.Create(new CreateGroupRequest
            {
                SubjectId = subject.Id,
                AcademicYearId = year.Id,
                Name = "Group In Archived Year"
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("archived academic year", json);
    }

    [Fact]
    public async Task CreateGroup_WhenDuplicateActiveNameExistsInSameSubjectAndYear_Returns409Conflict()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("teacher_grp_dup");
        var (subject, year) = await SeedSubjectAndYearAsync(teacher.Id);

        var req1 = new HttpRequestMessage(HttpMethod.Post, "/api/teacher/groups")
        {
            Content = JsonContent.Create(new CreateGroupRequest
            {
                SubjectId = subject.Id,
                AcademicYearId = year.Id,
                Name = "Group Saturday 10AM"
            })
        };
        req1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res1 = await _client.SendAsync(req1);
        Assert.Equal(HttpStatusCode.Created, res1.StatusCode);

        // Attempt duplicate
        var req2 = new HttpRequestMessage(HttpMethod.Post, "/api/teacher/groups")
        {
            Content = JsonContent.Create(new CreateGroupRequest
            {
                SubjectId = subject.Id,
                AcademicYearId = year.Id,
                Name = "  group saturday 10am  "
            })
        };
        req2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res2 = await _client.SendAsync(req2);
        Assert.Equal(HttpStatusCode.Conflict, res2.StatusCode);
    }

    [Fact]
    public async Task UpdateGroup_WhenCapacityBelowCurrentEnrolledStudents_Returns400BadRequest()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("teacher_grp_capacity");
        var (subject, year) = await SeedSubjectAndYearAsync(teacher.Id);

        using var db = _factory.CreateDbContext();
        var group = new Group
        {
            Id = Guid.NewGuid(),
            TeacherId = teacher.Id,
            SubjectId = subject.Id,
            AcademicYearId = year.Id,
            Name = "Capacity Group",
            MaxCapacity = 20
        };
        db.Groups.Add(group);

        // Add 5 active enrolled students
        for (var i = 0; i < 5; i++)
        {
            var student = new Student
            {
                Id = Guid.NewGuid(),
                TeacherId = teacher.Id,
                StudentCode = $"HD-CAP{i}{Guid.NewGuid():N}"[..9],
                FullName = $"Student {i}"
            };
            db.Students.Add(student);
            db.GroupEnrollments.Add(new GroupEnrollment
            {
                Id = Guid.NewGuid(),
                TeacherId = teacher.Id,
                GroupId = group.Id,
                StudentId = student.Id,
                IsActive = true
            });
        }
        await db.SaveChangesAsync();

        // Attempt to update MaxCapacity to 3 (which is less than current 5 active students)
        var updateReq = new HttpRequestMessage(HttpMethod.Put, $"/api/teacher/groups/{group.Id}")
        {
            Content = JsonContent.Create(new UpdateGroupRequest
            {
                Name = "Capacity Group",
                MaxCapacity = 3 // Invalid! Less than 5
            })
        };
        updateReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var updateRes = await _client.SendAsync(updateReq);

        Assert.Equal(HttpStatusCode.BadRequest, updateRes.StatusCode);
        var json = await updateRes.Content.ReadAsStringAsync();
        Assert.Contains("current active enrolled students count", json);
    }

    [Fact]
    public async Task DeleteGroup_WhenHasEnrollmentsOrSessions_Returns400BadRequest()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("teacher_grp_del_block");
        var (subject, year) = await SeedSubjectAndYearAsync(teacher.Id);

        using var db = _factory.CreateDbContext();
        var group = new Group
        {
            Id = Guid.NewGuid(),
            TeacherId = teacher.Id,
            SubjectId = subject.Id,
            AcademicYearId = year.Id,
            Name = "Group With Session"
        };
        db.Groups.Add(group);

        var session = new Session
        {
            Id = Guid.NewGuid(),
            TeacherId = teacher.Id,
            GroupId = group.Id,
            Title = "First Class",
            SessionDateUtc = DateTime.UtcNow
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();

        // Attempt deletion -> blocked per Rule BR-ACA-023
        var delReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/teacher/groups/{group.Id}");
        delReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var delRes = await _client.SendAsync(delReq);

        Assert.Equal(HttpStatusCode.BadRequest, delRes.StatusCode);
        var json = await delRes.Content.ReadAsStringAsync();
        Assert.Contains("archive it instead", json);
    }

    [Fact]
    public async Task DeleteGroup_WhenEmpty_DeletesAndReturns200()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("teacher_grp_del_ok");
        var (subject, year) = await SeedSubjectAndYearAsync(teacher.Id);

        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/teacher/groups")
        {
            Content = JsonContent.Create(new CreateGroupRequest
            {
                SubjectId = subject.Id,
                AcademicYearId = year.Id,
                Name = "Empty Group To Delete"
            })
        };
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var createRes = await _client.SendAsync(createReq);
        var group = await createRes.Content.ReadFromJsonAsync<GroupResponseDto>();

        var delReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/teacher/groups/{group!.Id}");
        delReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var delRes = await _client.SendAsync(delReq);

        Assert.Equal(HttpStatusCode.OK, delRes.StatusCode);

        using var db = _factory.CreateDbContext();
        var exists = await db.Groups.AnyAsync(g => g.Id == group.Id);
        Assert.False(exists);
    }

    [Fact]
    public async Task Group_CrossTenantIsolation_TeacherCannotAccessOrMutateOtherTeachersGroup()
    {
        var (teacherA, tokenA) = await CreateAndLoginTeacherAsync("teacher_grp_iso_read_a");
        var (teacherB, tokenB) = await CreateAndLoginTeacherAsync("teacher_grp_iso_read_b");

        var (subjectA, yearA) = await SeedSubjectAndYearAsync(teacherA.Id);

        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/teacher/groups")
        {
            Content = JsonContent.Create(new CreateGroupRequest
            {
                SubjectId = subjectA.Id,
                AcademicYearId = yearA.Id,
                Name = "Teacher A Private Group"
            })
        };
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var createRes = await _client.SendAsync(createReq);
        var groupA = await createRes.Content.ReadFromJsonAsync<GroupResponseDto>();

        // Teacher B attempts to view Teacher A's group -> 404 Not Found
        var getReq = new HttpRequestMessage(HttpMethod.Get, $"/api/teacher/groups/{groupA!.Id}");
        getReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var getRes = await _client.SendAsync(getReq);
        Assert.Equal(HttpStatusCode.NotFound, getRes.StatusCode);

        // Teacher B attempts to update Teacher A's group -> 404 Not Found
        var updateReq = new HttpRequestMessage(HttpMethod.Put, $"/api/teacher/groups/{groupA.Id}")
        {
            Content = JsonContent.Create(new UpdateGroupRequest { Name = "Tampered Group" })
        };
        updateReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var updateRes = await _client.SendAsync(updateReq);
        Assert.Equal(HttpStatusCode.NotFound, updateRes.StatusCode);

        // Teacher B attempts to archive Teacher A's group -> 404 Not Found
        var archiveReq = new HttpRequestMessage(HttpMethod.Patch, $"/api/teacher/groups/{groupA.Id}/archive");
        archiveReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var archiveRes = await _client.SendAsync(archiveReq);
        Assert.Equal(HttpStatusCode.NotFound, archiveRes.StatusCode);

        // Teacher B attempts to delete Teacher A's group -> 404 Not Found
        var delReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/teacher/groups/{groupA.Id}");
        delReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var delRes = await _client.SendAsync(delReq);
        Assert.Equal(HttpStatusCode.NotFound, delRes.StatusCode);
    }
}
