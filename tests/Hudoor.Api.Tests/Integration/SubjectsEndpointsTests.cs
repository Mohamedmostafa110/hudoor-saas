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

public class SubjectsEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SubjectsEndpointsTests(CustomWebApplicationFactory factory)
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

    [Fact]
    public async Task CreateSubject_WithValidData_Returns201AndSubjectDto()
    {
        var (_, token) = await CreateAndLoginTeacherAsync("teacher_subj_create");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/teacher/subjects")
        {
            Content = JsonContent.Create(new CreateSubjectRequest
            {
                Name = "Physics - 1st Secondary",
                GradeLevel = "1st Secondary"
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<SubjectResponseDto>();
        Assert.NotNull(body);
        Assert.Equal("Physics - 1st Secondary", body.Name);
        Assert.Equal("1st Secondary", body.GradeLevel);
        Assert.False(body.IsArchived);
        Assert.Equal(0, body.GroupsCount);
    }

    [Fact]
    public async Task CreateSubject_WhenDuplicateActiveNameExistsForSameTeacher_Returns409Conflict()
    {
        var (_, token) = await CreateAndLoginTeacherAsync("teacher_subj_dup");

        var req1 = new HttpRequestMessage(HttpMethod.Post, "/api/teacher/subjects")
        {
            Content = JsonContent.Create(new CreateSubjectRequest
            {
                Name = "Mathematics - Algebra",
                GradeLevel = "2nd Secondary"
            })
        };
        req1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res1 = await _client.SendAsync(req1);
        Assert.Equal(HttpStatusCode.Created, res1.StatusCode);

        // Attempt to create another active subject with the exact same name
        var req2 = new HttpRequestMessage(HttpMethod.Post, "/api/teacher/subjects")
        {
            Content = JsonContent.Create(new CreateSubjectRequest
            {
                Name = "  mathematics - algebra  ", // Case-insensitive and trimmed
                GradeLevel = "Different Grade"
            })
        };
        req2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res2 = await _client.SendAsync(req2);

        Assert.Equal(HttpStatusCode.Conflict, res2.StatusCode);
    }

    [Fact]
    public async Task GetSubjects_FiltersArchivedByDefault_AndIncludesWhenRequested()
    {
        var (_, token) = await CreateAndLoginTeacherAsync("teacher_subj_list");

        // 1. Create active subject
        var reqActive = new HttpRequestMessage(HttpMethod.Post, "/api/teacher/subjects")
        {
            Content = JsonContent.Create(new CreateSubjectRequest { Name = "Active Subject" })
        };
        reqActive.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await _client.SendAsync(reqActive);

        // 2. Create archived subject
        var reqArchived = new HttpRequestMessage(HttpMethod.Post, "/api/teacher/subjects")
        {
            Content = JsonContent.Create(new CreateSubjectRequest { Name = "Archived Subject" })
        };
        reqArchived.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resArchived = await _client.SendAsync(reqArchived);
        var createdArchived = await resArchived.Content.ReadFromJsonAsync<SubjectResponseDto>();

        var patchReq = new HttpRequestMessage(HttpMethod.Patch, $"/api/teacher/subjects/{createdArchived!.Id}/archive");
        patchReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await _client.SendAsync(patchReq);

        // 3. Query without includeArchived -> only active
        var getReq1 = new HttpRequestMessage(HttpMethod.Get, "/api/teacher/subjects");
        getReq1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var getRes1 = await _client.SendAsync(getReq1);
        var list1 = await getRes1.Content.ReadFromJsonAsync<List<SubjectResponseDto>>();
        Assert.Contains(list1!, s => s.Name == "Active Subject");
        Assert.DoesNotContain(list1!, s => s.Name == "Archived Subject");

        // 4. Query with includeArchived=true -> includes archived
        var getReq2 = new HttpRequestMessage(HttpMethod.Get, "/api/teacher/subjects?includeArchived=true");
        getReq2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var getRes2 = await _client.SendAsync(getReq2);
        var list2 = await getRes2.Content.ReadFromJsonAsync<List<SubjectResponseDto>>();
        Assert.Contains(list2!, s => s.Name == "Active Subject");
        Assert.Contains(list2!, s => s.Name == "Archived Subject");
    }

    [Fact]
    public async Task UpdateSubject_WithValidData_UpdatesAndReturns200()
    {
        var (_, token) = await CreateAndLoginTeacherAsync("teacher_subj_update");

        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/teacher/subjects")
        {
            Content = JsonContent.Create(new CreateSubjectRequest
            {
                Name = "Chemistry Initial",
                GradeLevel = "Grade 10"
            })
        };
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var createRes = await _client.SendAsync(createReq);
        var subject = await createRes.Content.ReadFromJsonAsync<SubjectResponseDto>();

        var updateReq = new HttpRequestMessage(HttpMethod.Put, $"/api/teacher/subjects/{subject!.Id}")
        {
            Content = JsonContent.Create(new UpdateSubjectRequest
            {
                Name = "Chemistry Updated",
                GradeLevel = "Grade 11"
            })
        };
        updateReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var updateRes = await _client.SendAsync(updateReq);
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);

        var updatedBody = await updateRes.Content.ReadFromJsonAsync<SubjectResponseDto>();
        Assert.Equal("Chemistry Updated", updatedBody!.Name);
        Assert.Equal("Grade 11", updatedBody.GradeLevel);
    }

    [Fact]
    public async Task DeleteSubject_WithAssociatedGroups_Returns400BadRequest()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("teacher_subj_del_prevent");

        using var db = _factory.CreateDbContext();

        var subject = new Subject
        {
            Id = Guid.NewGuid(),
            TeacherId = teacher.Id,
            Name = "Biology For Groups"
        };
        db.Subjects.Add(subject);

        var year = new AcademicYear
        {
            Id = Guid.NewGuid(),
            TeacherId = teacher.Id,
            Name = "Year For Subject Test",
            StartDate = new DateTime(2025, 9, 1),
            EndDate = new DateTime(2026, 6, 30)
        };
        db.AcademicYears.Add(year);

        var group = new Group
        {
            Id = Guid.NewGuid(),
            TeacherId = teacher.Id,
            SubjectId = subject.Id,
            AcademicYearId = year.Id,
            Name = "Group Alpha"
        };
        db.Groups.Add(group);
        await db.SaveChangesAsync();

        // Attempt deletion -> blocked because of associated groups
        var delReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/teacher/subjects/{subject.Id}");
        delReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var delRes = await _client.SendAsync(delReq);

        Assert.Equal(HttpStatusCode.BadRequest, delRes.StatusCode);
        var json = await delRes.Content.ReadAsStringAsync();
        Assert.Contains("archive it instead", json);
    }

    [Fact]
    public async Task DeleteSubject_WhenEmpty_DeletesAndReturns200()
    {
        var (_, token) = await CreateAndLoginTeacherAsync("teacher_subj_del_ok");

        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/teacher/subjects")
        {
            Content = JsonContent.Create(new CreateSubjectRequest
            {
                Name = "Subject To Delete"
            })
        };
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var createRes = await _client.SendAsync(createReq);
        var subject = await createRes.Content.ReadFromJsonAsync<SubjectResponseDto>();

        var delReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/teacher/subjects/{subject!.Id}");
        delReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var delRes = await _client.SendAsync(delReq);

        Assert.Equal(HttpStatusCode.OK, delRes.StatusCode);

        using var db = _factory.CreateDbContext();
        var exists = await db.Subjects.AnyAsync(s => s.Id == subject.Id);
        Assert.False(exists);
    }

    [Fact]
    public async Task Subject_CrossTenantIsolation_TeacherCannotAccessOrMutateOtherTeachersSubject()
    {
        var (_, tokenA) = await CreateAndLoginTeacherAsync("teacher_subj_iso_a");
        var (_, tokenB) = await CreateAndLoginTeacherAsync("teacher_subj_iso_b");

        // Teacher A creates Subject A
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/teacher/subjects")
        {
            Content = JsonContent.Create(new CreateSubjectRequest
            {
                Name = "Teacher A Private Subject"
            })
        };
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var createRes = await _client.SendAsync(createReq);
        var subjectA = await createRes.Content.ReadFromJsonAsync<SubjectResponseDto>();

        // Teacher B attempts to get Teacher A's subject -> 404 Not Found
        var getReq = new HttpRequestMessage(HttpMethod.Get, $"/api/teacher/subjects/{subjectA!.Id}");
        getReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var getRes = await _client.SendAsync(getReq);
        Assert.Equal(HttpStatusCode.NotFound, getRes.StatusCode);

        // Teacher B attempts to update Teacher A's subject -> 404 Not Found
        var updateReq = new HttpRequestMessage(HttpMethod.Put, $"/api/teacher/subjects/{subjectA.Id}")
        {
            Content = JsonContent.Create(new UpdateSubjectRequest { Name = "Tampered Subject" })
        };
        updateReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var updateRes = await _client.SendAsync(updateReq);
        Assert.Equal(HttpStatusCode.NotFound, updateRes.StatusCode);

        // Teacher B attempts to archive Teacher A's subject -> 404 Not Found
        var archiveReq = new HttpRequestMessage(HttpMethod.Patch, $"/api/teacher/subjects/{subjectA.Id}/archive");
        archiveReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var archiveRes = await _client.SendAsync(archiveReq);
        Assert.Equal(HttpStatusCode.NotFound, archiveRes.StatusCode);

        // Teacher B attempts to delete Teacher A's subject -> 404 Not Found
        var delReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/teacher/subjects/{subjectA.Id}");
        delReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var delRes = await _client.SendAsync(delReq);
        Assert.Equal(HttpStatusCode.NotFound, delRes.StatusCode);
    }
}
