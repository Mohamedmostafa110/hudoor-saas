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

public class StudentsEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public StudentsEndpointsTests(CustomWebApplicationFactory factory)
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

    private async Task<(Subject Subject, AcademicYear Year, Group Group)> SeedGroupAsync(Guid teacherId, int? maxCapacity = null)
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
            MaxCapacity = maxCapacity
        };

        db.Subjects.Add(subject);
        db.AcademicYears.Add(year);
        db.Groups.Add(group);
        await db.SaveChangesAsync();

        return (subject, year, group);
    }

    [Fact]
    public async Task CreateStudent_WithoutInitialGroup_Success()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("stu_t1_" + Guid.NewGuid().ToString("N")[..6]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new CreateStudentRequest
        {
            FullName = "Ahmed Mahmoud",
            PhoneNumber = "+201011112222",
            ParentPhoneNumber = "+201033334444",
            CurrentGrade = "1st Secondary",
            Notes = "Excels at chemistry"
        };

        var response = await _client.PostAsJsonAsync("/api/teacher/students", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<StudentCreatedResponseDto>();
        Assert.NotNull(created);
        Assert.StartsWith("HD-", created.StudentCode);
        Assert.Equal(9, created.StudentCode.Length); // HD- + 6 characters
        Assert.False(string.IsNullOrWhiteSpace(created.TemporaryPassword));
        Assert.Equal("Ahmed Mahmoud", created.FullName);
        Assert.Null(created.InitialGroupId);

        using var db = _factory.CreateDbContext();
        var studentInDb = await db.Students.Include(s => s.Enrollments).FirstOrDefaultAsync(s => s.Id == created.Id);
        Assert.NotNull(studentInDb);
        Assert.Equal(teacher.Id, studentInDb.TeacherId);
        Assert.True(studentInDb.MustChangePassword);
        Assert.Equal(1, studentInDb.TokenVersion);
        Assert.True(studentInDb.IsActive);
        Assert.Empty(studentInDb.Enrollments);
    }

    [Fact]
    public async Task CreateStudent_WithInitialGroup_Success()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("stu_t2_" + Guid.NewGuid().ToString("N")[..6]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (_, _, group) = await SeedGroupAsync(teacher.Id);

        var request = new CreateStudentRequest
        {
            FullName = "Sara Mostafa",
            PhoneNumber = "+201055556666",
            CurrentGrade = "1st Secondary",
            InitialGroupId = group.Id
        };

        var response = await _client.PostAsJsonAsync("/api/teacher/students", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<StudentCreatedResponseDto>();
        Assert.NotNull(created);
        Assert.Equal(group.Id, created.InitialGroupId);
        Assert.Equal(group.Name, created.InitialGroupName);

        using var db = _factory.CreateDbContext();
        var enrollment = await db.GroupEnrollments.FirstOrDefaultAsync(e => e.StudentId == created.Id && e.GroupId == group.Id);
        Assert.NotNull(enrollment);
        Assert.Equal(teacher.Id, enrollment.TeacherId);
        Assert.True(enrollment.IsActive);
    }

    [Fact]
    public async Task CreateStudent_CrossTenantInitialGroup_ReturnsNotFound()
    {
        var (teacher1, _) = await CreateAndLoginTeacherAsync("stu_t3a_" + Guid.NewGuid().ToString("N")[..6]);
        var (teacher2, token2) = await CreateAndLoginTeacherAsync("stu_t3b_" + Guid.NewGuid().ToString("N")[..6]);

        var (_, _, teacher1Group) = await SeedGroupAsync(teacher1.Id);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token2);

        var request = new CreateStudentRequest
        {
            FullName = "Infiltrator Student",
            InitialGroupId = teacher1Group.Id // Cross-tenant reference
        };

        var response = await _client.PostAsJsonAsync("/api/teacher/students", request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateStudent_GroupAtMaxCapacity_ReturnsBadRequest()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("stu_t4_" + Guid.NewGuid().ToString("N")[..6]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Seed group with MaxCapacity = 1
        var (_, _, group) = await SeedGroupAsync(teacher.Id, maxCapacity: 1);

        // Student 1 creation (fills capacity)
        var res1 = await _client.PostAsJsonAsync("/api/teacher/students", new CreateStudentRequest
        {
            FullName = "First Student",
            InitialGroupId = group.Id
        });
        Assert.Equal(HttpStatusCode.Created, res1.StatusCode);

        // Student 2 creation (exceeds capacity)
        var res2 = await _client.PostAsJsonAsync("/api/teacher/students", new CreateStudentRequest
        {
            FullName = "Second Student",
            InitialGroupId = group.Id
        });
        Assert.Equal(HttpStatusCode.BadRequest, res2.StatusCode);
    }

    [Fact]
    public async Task GetStudents_Filtering_Success()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("stu_t5_" + Guid.NewGuid().ToString("N")[..6]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (_, _, group) = await SeedGroupAsync(teacher.Id);

        // Create 2 students
        await _client.PostAsJsonAsync("/api/teacher/students", new CreateStudentRequest
        {
            FullName = "Tariq Ali",
            PhoneNumber = "+201110001111",
            CurrentGrade = "Grade 10",
            InitialGroupId = group.Id
        });

        await _client.PostAsJsonAsync("/api/teacher/students", new CreateStudentRequest
        {
            FullName = "Mona Hassan",
            PhoneNumber = "+201120002222",
            CurrentGrade = "Grade 11"
        });

        // 1. All teacher students
        var allRes = await _client.GetFromJsonAsync<List<StudentSummaryDto>>("/api/teacher/students");
        Assert.NotNull(allRes);
        Assert.Equal(2, allRes.Count);

        // 2. Filter by search (name)
        var searchRes = await _client.GetFromJsonAsync<List<StudentSummaryDto>>("/api/teacher/students?search=Tariq");
        Assert.NotNull(searchRes);
        Assert.Single(searchRes);
        Assert.Equal("Tariq Ali", searchRes[0].FullName);

        // 3. Filter by grade
        var gradeRes = await _client.GetFromJsonAsync<List<StudentSummaryDto>>("/api/teacher/students?grade=Grade 11");
        Assert.NotNull(gradeRes);
        Assert.Single(gradeRes);
        Assert.Equal("Mona Hassan", gradeRes[0].FullName);

        // 4. Filter by group
        var groupRes = await _client.GetFromJsonAsync<List<StudentSummaryDto>>($"/api/teacher/students?groupId={group.Id}");
        Assert.NotNull(groupRes);
        Assert.Single(groupRes);
        Assert.Equal("Tariq Ali", groupRes[0].FullName);
    }

    [Fact]
    public async Task GetStudentById_CrossTenant_ReturnsNotFound()
    {
        var (teacher1, token1) = await CreateAndLoginTeacherAsync("stu_t6a_" + Guid.NewGuid().ToString("N")[..6]);
        var (teacher2, token2) = await CreateAndLoginTeacherAsync("stu_t6b_" + Guid.NewGuid().ToString("N")[..6]);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token1);
        var createRes = await _client.PostAsJsonAsync("/api/teacher/students", new CreateStudentRequest
        {
            FullName = "Teacher1 Student"
        });
        var created = await createRes.Content.ReadFromJsonAsync<StudentCreatedResponseDto>();

        // Teacher 2 tries to GET Teacher 1's student
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token2);
        var getRes = await _client.GetAsync($"/api/teacher/students/{created!.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getRes.StatusCode);
    }

    [Fact]
    public async Task UpdateStudent_Success()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("stu_t7_" + Guid.NewGuid().ToString("N")[..6]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var createRes = await _client.PostAsJsonAsync("/api/teacher/students", new CreateStudentRequest
        {
            FullName = "Original Name",
            CurrentGrade = "Grade 9"
        });
        var created = await createRes.Content.ReadFromJsonAsync<StudentCreatedResponseDto>();

        var updateReq = new UpdateStudentRequest
        {
            FullName = "Updated Name",
            PhoneNumber = "+201200000000",
            CurrentGrade = "Grade 10",
            Notes = "Updated notes"
        };

        var updateRes = await _client.PutAsJsonAsync($"/api/teacher/students/{created!.Id}", updateReq);
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);

        var updated = await updateRes.Content.ReadFromJsonAsync<StudentDetailDto>();
        Assert.NotNull(updated);
        Assert.Equal("Updated Name", updated.FullName);
        Assert.Equal("Grade 10", updated.CurrentGrade);
        Assert.Equal("Updated notes", updated.Notes);
    }

    [Fact]
    public async Task UpdateStudentStatus_Deactivate_RevokesSessions()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("stu_t8_" + Guid.NewGuid().ToString("N")[..6]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var createRes = await _client.PostAsJsonAsync("/api/teacher/students", new CreateStudentRequest
        {
            FullName = "Status Student"
        });
        var created = await createRes.Content.ReadFromJsonAsync<StudentCreatedResponseDto>();

        // Deactivate student
        var patchRes = await _client.PatchAsJsonAsync($"/api/teacher/students/{created!.Id}/status", new UpdateStudentStatusRequest
        {
            IsActive = false
        });
        Assert.Equal(HttpStatusCode.OK, patchRes.StatusCode);

        using var db = _factory.CreateDbContext();
        var studentInDb = await db.Students.FindAsync(created.Id);
        Assert.NotNull(studentInDb);
        Assert.False(studentInDb.IsActive);
        Assert.Equal(2, studentInDb.TokenVersion); // Incremented to revoke JWTs
    }

    [Fact]
    public async Task RegenerateStudentCode_Success()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("stu_t9_" + Guid.NewGuid().ToString("N")[..6]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var createRes = await _client.PostAsJsonAsync("/api/teacher/students", new CreateStudentRequest
        {
            FullName = "Regen Student"
        });
        var created = await createRes.Content.ReadFromJsonAsync<StudentCreatedResponseDto>();
        var originalCode = created!.StudentCode;

        var regenRes = await _client.PostAsync($"/api/teacher/students/{created.Id}/regenerate-code", null);
        Assert.Equal(HttpStatusCode.OK, regenRes.StatusCode);

        var regen = await regenRes.Content.ReadFromJsonAsync<RegenerateCodeResponseDto>();
        Assert.NotNull(regen);
        Assert.Equal(originalCode, regen.OldStudentCode);
        Assert.NotEqual(originalCode, regen.NewStudentCode);
        Assert.StartsWith("HD-", regen.NewStudentCode);
        Assert.Equal(2, regen.TokenVersion); // Incremented

        using var db = _factory.CreateDbContext();
        var studentInDb = await db.Students.FindAsync(created.Id);
        Assert.NotNull(studentInDb);
        Assert.Equal(regen.NewStudentCode, studentInDb.StudentCode);
        Assert.Equal(2, studentInDb.TokenVersion);
    }

    [Fact]
    public async Task DeleteStudent_WithEnrollment_ReturnsBadRequest()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("stu_t10_" + Guid.NewGuid().ToString("N")[..6]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (_, _, group) = await SeedGroupAsync(teacher.Id);

        var createRes = await _client.PostAsJsonAsync("/api/teacher/students", new CreateStudentRequest
        {
            FullName = "Enrolled Student",
            InitialGroupId = group.Id
        });
        var created = await createRes.Content.ReadFromJsonAsync<StudentCreatedResponseDto>();

        // Try to hard delete enrolled student
        var deleteRes = await _client.DeleteAsync($"/api/teacher/students/{created!.Id}");
        Assert.Equal(HttpStatusCode.BadRequest, deleteRes.StatusCode);

        // Verify still exists
        using var db = _factory.CreateDbContext();
        var student = await db.Students.FindAsync(created.Id);
        Assert.NotNull(student);
    }

    [Fact]
    public async Task DeleteStudent_WithoutHistory_ReturnsNoContent()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("stu_t11_" + Guid.NewGuid().ToString("N")[..6]);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var createRes = await _client.PostAsJsonAsync("/api/teacher/students", new CreateStudentRequest
        {
            FullName = "Mistake Student"
        });
        var created = await createRes.Content.ReadFromJsonAsync<StudentCreatedResponseDto>();

        var deleteRes = await _client.DeleteAsync($"/api/teacher/students/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);

        using var db = _factory.CreateDbContext();
        var student = await db.Students.FindAsync(created.Id);
        Assert.Null(student);
    }
}
