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

public class EnrollmentsEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public EnrollmentsEndpointsTests(CustomWebApplicationFactory factory)
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

    private async Task<(Subject Subject, AcademicYear Year, Group Group)> SeedGroupAsync(Guid teacherId, int? maxCapacity = null, bool isArchived = false)
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
            Name = "Group " + Guid.NewGuid().ToString("N")[..6],
            MaxCapacity = maxCapacity,
            IsArchived = isArchived
        };

        db.Subjects.Add(subject);
        db.AcademicYears.Add(year);
        db.Groups.Add(group);
        await db.SaveChangesAsync();

        return (subject, year, group);
    }

    private async Task<Student> SeedStudentAsync(Guid teacherId, bool isActive = true)
    {
        using var db = _factory.CreateDbContext();
        var student = new Student
        {
            Id = Guid.NewGuid(),
            TeacherId = teacherId,
            StudentCode = "HD-" + Guid.NewGuid().ToString("N")[..6].ToUpper(),
            PasswordHash = "hash",
            FullName = "Student " + Guid.NewGuid().ToString("N")[..6],
            IsActive = isActive,
            MustChangePassword = true,
            TokenVersion = 1,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Students.Add(student);
        await db.SaveChangesAsync();
        return student;
    }

    [Fact]
    public async Task EnrollStudent_Success()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("enr_t1_" + Guid.NewGuid().ToString("N")[..6]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (_, _, group) = await SeedGroupAsync(teacher.Id);
        var student = await SeedStudentAsync(teacher.Id);

        var response = await _client.PostAsJsonAsync($"/api/teacher/groups/{group.Id}/students", new EnrollStudentRequest
        {
            StudentId = student.Id
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<EnrollmentResponseDto>();
        Assert.NotNull(created);
        Assert.Equal(group.Id, created.GroupId);
        Assert.Equal(student.Id, created.StudentId);
        Assert.True(created.IsActive);

        using var db = _factory.CreateDbContext();
        var enrollmentInDb = await db.GroupEnrollments.FindAsync(created.Id);
        Assert.NotNull(enrollmentInDb);
        Assert.Equal(teacher.Id, enrollmentInDb.TeacherId);
        Assert.True(enrollmentInDb.IsActive);
        Assert.Null(enrollmentInDb.LeftDateUtc);
    }

    [Fact]
    public async Task EnrollStudent_CrossTenantEntities_ReturnsNotFound()
    {
        var (teacher1, _) = await CreateAndLoginTeacherAsync("enr_t2a_" + Guid.NewGuid().ToString("N")[..6]);
        var (teacher2, token2) = await CreateAndLoginTeacherAsync("enr_t2b_" + Guid.NewGuid().ToString("N")[..6]);

        var (_, _, teacher1Group) = await SeedGroupAsync(teacher1.Id);
        var teacher1Student = await SeedStudentAsync(teacher1.Id);
        var teacher2Student = await SeedStudentAsync(teacher2.Id);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token2);

        // Case 1: Teacher 2 tries to enroll Teacher 1's student into Teacher 2's group
        var res1 = await _client.PostAsJsonAsync($"/api/teacher/groups/{teacher1Group.Id}/students", new EnrollStudentRequest
        {
            StudentId = teacher2Student.Id
        });
        Assert.Equal(HttpStatusCode.NotFound, res1.StatusCode);

        // Case 2: Teacher 2 tries to enroll Teacher 2's student into Teacher 1's group
        var (_, _, teacher2Group) = await SeedGroupAsync(teacher2.Id);
        var res2 = await _client.PostAsJsonAsync($"/api/teacher/groups/{teacher2Group.Id}/students", new EnrollStudentRequest
        {
            StudentId = teacher1Student.Id
        });
        Assert.Equal(HttpStatusCode.NotFound, res2.StatusCode);
    }

    [Fact]
    public async Task EnrollStudent_AlreadyEnrolled_ReturnsConflict()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("enr_t3_" + Guid.NewGuid().ToString("N")[..6]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (_, _, group) = await SeedGroupAsync(teacher.Id);
        var student = await SeedStudentAsync(teacher.Id);

        // First enrollment
        var res1 = await _client.PostAsJsonAsync($"/api/teacher/groups/{group.Id}/students", new EnrollStudentRequest
        {
            StudentId = student.Id
        });
        Assert.Equal(HttpStatusCode.Created, res1.StatusCode);

        // Duplicate active enrollment
        var res2 = await _client.PostAsJsonAsync($"/api/teacher/groups/{group.Id}/students", new EnrollStudentRequest
        {
            StudentId = student.Id
        });
        Assert.Equal(HttpStatusCode.Conflict, res2.StatusCode);
    }

    [Fact]
    public async Task EnrollStudent_GroupAtMaxCapacity_ReturnsBadRequest()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("enr_t4_" + Guid.NewGuid().ToString("N")[..6]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (_, _, group) = await SeedGroupAsync(teacher.Id, maxCapacity: 1);
        var student1 = await SeedStudentAsync(teacher.Id);
        var student2 = await SeedStudentAsync(teacher.Id);

        // 1st enrolls
        var res1 = await _client.PostAsJsonAsync($"/api/teacher/groups/{group.Id}/students", new EnrollStudentRequest
        {
            StudentId = student1.Id
        });
        Assert.Equal(HttpStatusCode.Created, res1.StatusCode);

        // 2nd exceeds capacity
        var res2 = await _client.PostAsJsonAsync($"/api/teacher/groups/{group.Id}/students", new EnrollStudentRequest
        {
            StudentId = student2.Id
        });
        Assert.Equal(HttpStatusCode.BadRequest, res2.StatusCode);
    }

    [Fact]
    public async Task EnrollStudent_InactiveStudent_ReturnsBadRequest()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("enr_t5_" + Guid.NewGuid().ToString("N")[..6]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (_, _, group) = await SeedGroupAsync(teacher.Id);
        var inactiveStudent = await SeedStudentAsync(teacher.Id, isActive: false);

        var res = await _client.PostAsJsonAsync($"/api/teacher/groups/{group.Id}/students", new EnrollStudentRequest
        {
            StudentId = inactiveStudent.Id
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task GetGroupStudents_Success()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("enr_t6_" + Guid.NewGuid().ToString("N")[..6]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (_, _, group) = await SeedGroupAsync(teacher.Id);
        var student1 = await SeedStudentAsync(teacher.Id);
        var student2 = await SeedStudentAsync(teacher.Id);

        await _client.PostAsJsonAsync($"/api/teacher/groups/{group.Id}/students", new EnrollStudentRequest { StudentId = student1.Id });
        await _client.PostAsJsonAsync($"/api/teacher/groups/{group.Id}/students", new EnrollStudentRequest { StudentId = student2.Id });

        var listRes = await _client.GetFromJsonAsync<List<GroupStudentDto>>($"/api/teacher/groups/{group.Id}/students");
        Assert.NotNull(listRes);
        Assert.Equal(2, listRes.Count);
        Assert.Contains(listRes, s => s.StudentId == student1.Id);
        Assert.Contains(listRes, s => s.StudentId == student2.Id);
    }

    [Fact]
    public async Task GetGroupStudents_CrossTenant_ReturnsNotFound()
    {
        var (teacher1, _) = await CreateAndLoginTeacherAsync("enr_t7a_" + Guid.NewGuid().ToString("N")[..6]);
        var (_, token2) = await CreateAndLoginTeacherAsync("enr_t7b_" + Guid.NewGuid().ToString("N")[..6]);

        var (_, _, teacher1Group) = await SeedGroupAsync(teacher1.Id);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token2);
        var res = await _client.GetAsync($"/api/teacher/groups/{teacher1Group.Id}/students");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task UnenrollStudent_Success()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("enr_t8_" + Guid.NewGuid().ToString("N")[..6]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (_, _, group) = await SeedGroupAsync(teacher.Id);
        var student = await SeedStudentAsync(teacher.Id);

        await _client.PostAsJsonAsync($"/api/teacher/groups/{group.Id}/students", new EnrollStudentRequest { StudentId = student.Id });

        var unenrollRes = await _client.DeleteAsync($"/api/teacher/groups/{group.Id}/students/{student.Id}");
        Assert.Equal(HttpStatusCode.OK, unenrollRes.StatusCode);

        using var db = _factory.CreateDbContext();
        var enrollment = await db.GroupEnrollments.FirstOrDefaultAsync(e => e.GroupId == group.Id && e.StudentId == student.Id);
        Assert.NotNull(enrollment);
        Assert.False(enrollment.IsActive);
        Assert.NotNull(enrollment.LeftDateUtc);
    }

    [Fact]
    public async Task TransferStudent_Success()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("enr_t9_" + Guid.NewGuid().ToString("N")[..6]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (_, _, groupA) = await SeedGroupAsync(teacher.Id);
        var (_, _, groupB) = await SeedGroupAsync(teacher.Id);
        var student = await SeedStudentAsync(teacher.Id);

        // Enroll in Group A
        await _client.PostAsJsonAsync($"/api/teacher/groups/{groupA.Id}/students", new EnrollStudentRequest { StudentId = student.Id });

        // Transfer from Group A to Group B
        var transferReq = new TransferStudentRequest
        {
            StudentId = student.Id,
            FromGroupId = groupA.Id,
            ToGroupId = groupB.Id
        };

        var transferRes = await _client.PostAsJsonAsync("/api/teacher/enrollments/transfer", transferReq);
        Assert.Equal(HttpStatusCode.OK, transferRes.StatusCode);

        var transfer = await transferRes.Content.ReadFromJsonAsync<TransferResponseDto>();
        Assert.NotNull(transfer);
        Assert.Equal(groupA.Id, transfer.FromGroupId);
        Assert.Equal(groupB.Id, transfer.ToGroupId);

        using var db = _factory.CreateDbContext();
        // Check Group A enrollment is closed
        var oldEnrollment = await db.GroupEnrollments.FirstOrDefaultAsync(e => e.GroupId == groupA.Id && e.StudentId == student.Id);
        Assert.NotNull(oldEnrollment);
        Assert.False(oldEnrollment.IsActive);
        Assert.NotNull(oldEnrollment.LeftDateUtc);

        // Check Group B enrollment is active
        var newEnrollment = await db.GroupEnrollments.FirstOrDefaultAsync(e => e.GroupId == groupB.Id && e.StudentId == student.Id);
        Assert.NotNull(newEnrollment);
        Assert.True(newEnrollment.IsActive);
        Assert.Null(newEnrollment.LeftDateUtc);
    }

    [Fact]
    public async Task TransferStudent_SameGroup_ReturnsBadRequest()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("enr_t10_" + Guid.NewGuid().ToString("N")[..6]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (_, _, group) = await SeedGroupAsync(teacher.Id);
        var student = await SeedStudentAsync(teacher.Id);

        var transferReq = new TransferStudentRequest
        {
            StudentId = student.Id,
            FromGroupId = group.Id,
            ToGroupId = group.Id
        };

        var res = await _client.PostAsJsonAsync("/api/teacher/enrollments/transfer", transferReq);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task TransferStudent_NotCurrentlyEnrolledInSource_ReturnsBadRequest()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("enr_t11_" + Guid.NewGuid().ToString("N")[..6]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (_, _, groupA) = await SeedGroupAsync(teacher.Id);
        var (_, _, groupB) = await SeedGroupAsync(teacher.Id);
        var student = await SeedStudentAsync(teacher.Id);

        // Student is not enrolled in groupA
        var transferReq = new TransferStudentRequest
        {
            StudentId = student.Id,
            FromGroupId = groupA.Id,
            ToGroupId = groupB.Id
        };

        var res = await _client.PostAsJsonAsync("/api/teacher/enrollments/transfer", transferReq);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task TransferStudent_TargetGroupAtCapacity_ReturnsBadRequest()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("enr_t12_" + Guid.NewGuid().ToString("N")[..6]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (_, _, groupA) = await SeedGroupAsync(teacher.Id);
        var (_, _, groupB) = await SeedGroupAsync(teacher.Id, maxCapacity: 1); // target capacity = 1
        var student1 = await SeedStudentAsync(teacher.Id);
        var student2 = await SeedStudentAsync(teacher.Id);

        // Enroll student 1 in group A
        await _client.PostAsJsonAsync($"/api/teacher/groups/{groupA.Id}/students", new EnrollStudentRequest { StudentId = student1.Id });

        // Enroll student 2 in group B (filling its capacity)
        await _client.PostAsJsonAsync($"/api/teacher/groups/{groupB.Id}/students", new EnrollStudentRequest { StudentId = student2.Id });

        // Try to transfer student 1 to full group B
        var transferReq = new TransferStudentRequest
        {
            StudentId = student1.Id,
            FromGroupId = groupA.Id,
            ToGroupId = groupB.Id
        };

        var res = await _client.PostAsJsonAsync("/api/teacher/enrollments/transfer", transferReq);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task TransferStudent_CrossTenantTargetGroup_ReturnsNotFound()
    {
        var (teacher1, _) = await CreateAndLoginTeacherAsync("enr_t13a_" + Guid.NewGuid().ToString("N")[..6]);
        var (teacher2, token2) = await CreateAndLoginTeacherAsync("enr_t13b_" + Guid.NewGuid().ToString("N")[..6]);

        var (_, _, teacher1Group) = await SeedGroupAsync(teacher1.Id);
        var (_, _, teacher2Group) = await SeedGroupAsync(teacher2.Id);
        var teacher2Student = await SeedStudentAsync(teacher2.Id);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token2);

        // Teacher 2 enrolls their student in their group
        await _client.PostAsJsonAsync($"/api/teacher/groups/{teacher2Group.Id}/students", new EnrollStudentRequest { StudentId = teacher2Student.Id });

        // Teacher 2 tries to transfer their student into Teacher 1's group
        var transferReq = new TransferStudentRequest
        {
            StudentId = teacher2Student.Id,
            FromGroupId = teacher2Group.Id,
            ToGroupId = teacher1Group.Id
        };

        var res = await _client.PostAsJsonAsync("/api/teacher/enrollments/transfer", transferReq);
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
