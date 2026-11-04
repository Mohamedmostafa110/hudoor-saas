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

public class AcademicYearsEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AcademicYearsEndpointsTests(CustomWebApplicationFactory factory)
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
    public async Task CreateAcademicYear_WithValidData_Returns201AndAcademicYearDto()
    {
        var (_, token) = await CreateAndLoginTeacherAsync("teacher_ay_create");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/teacher/academic-years")
        {
            Content = JsonContent.Create(new CreateAcademicYearRequest
            {
                Name = "2025/2026",
                StartDate = new DateTime(2025, 9, 1),
                EndDate = new DateTime(2026, 6, 30),
                SetAsActive = true
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AcademicYearResponseDto>();
        Assert.NotNull(body);
        Assert.Equal("2025/2026", body.Name);
        Assert.True(body.IsActive);
        Assert.False(body.IsArchived);
    }

    [Fact]
    public async Task CreateAcademicYear_WhenEndDateBeforeStartDate_Returns400BadRequest()
    {
        var (_, token) = await CreateAndLoginTeacherAsync("teacher_ay_invalid_dates");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/teacher/academic-years")
        {
            Content = JsonContent.Create(new CreateAcademicYearRequest
            {
                Name = "Invalid Year",
                StartDate = new DateTime(2026, 6, 30),
                EndDate = new DateTime(2025, 9, 1) // End date before start date!
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SingleActiveAcademicYear_ActivatingNewYear_DeactivatesPreviousYearAtomically()
    {
        var (_, token) = await CreateAndLoginTeacherAsync("teacher_ay_single_active");

        // 1. Create Year 1 as active
        var req1 = new HttpRequestMessage(HttpMethod.Post, "/api/teacher/academic-years")
        {
            Content = JsonContent.Create(new CreateAcademicYearRequest
            {
                Name = "Year 2024/2025",
                StartDate = new DateTime(2024, 9, 1),
                EndDate = new DateTime(2025, 6, 30),
                SetAsActive = true
            })
        };
        req1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res1 = await _client.SendAsync(req1);
        var year1 = await res1.Content.ReadFromJsonAsync<AcademicYearResponseDto>();
        Assert.True(year1!.IsActive);

        // 2. Create Year 2 as NOT active
        var req2 = new HttpRequestMessage(HttpMethod.Post, "/api/teacher/academic-years")
        {
            Content = JsonContent.Create(new CreateAcademicYearRequest
            {
                Name = "Year 2025/2026",
                StartDate = new DateTime(2025, 9, 1),
                EndDate = new DateTime(2026, 6, 30),
                SetAsActive = false
            })
        };
        req2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res2 = await _client.SendAsync(req2);
        var year2 = await res2.Content.ReadFromJsonAsync<AcademicYearResponseDto>();
        Assert.False(year2!.IsActive);

        // 3. Activate Year 2
        var actReq = new HttpRequestMessage(HttpMethod.Patch, $"/api/teacher/academic-years/{year2.Id}/activate");
        actReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var actRes = await _client.SendAsync(actReq);
        Assert.Equal(HttpStatusCode.OK, actRes.StatusCode);

        // 4. Verify Year 1 is now inactive and Year 2 is active (Rule BR-ACA-010)
        using var db = _factory.CreateDbContext();
        var dbYear1 = await db.AcademicYears.FirstAsync(ay => ay.Id == year1.Id);
        var dbYear2 = await db.AcademicYears.FirstAsync(ay => ay.Id == year2.Id);

        Assert.False(dbYear1.IsActive);
        Assert.True(dbYear2.IsActive);
    }

    [Fact]
    public async Task ArchiveAcademicYear_SetsIsArchivedTrueAndIsActiveFalse()
    {
        var (_, token) = await CreateAndLoginTeacherAsync("teacher_ay_archive");

        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/teacher/academic-years")
        {
            Content = JsonContent.Create(new CreateAcademicYearRequest
            {
                Name = "Year To Archive",
                StartDate = new DateTime(2023, 9, 1),
                EndDate = new DateTime(2024, 6, 30),
                SetAsActive = true
            })
        };
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var createRes = await _client.SendAsync(createReq);
        var year = await createRes.Content.ReadFromJsonAsync<AcademicYearResponseDto>();

        var archiveReq = new HttpRequestMessage(HttpMethod.Patch, $"/api/teacher/academic-years/{year!.Id}/archive");
        archiveReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var archiveRes = await _client.SendAsync(archiveReq);
        Assert.Equal(HttpStatusCode.OK, archiveRes.StatusCode);

        using var db = _factory.CreateDbContext();
        var dbYear = await db.AcademicYears.FirstAsync(ay => ay.Id == year.Id);
        Assert.True(dbYear.IsArchived);
        Assert.False(dbYear.IsActive);
    }

    [Fact]
    public async Task DeleteAcademicYear_WithAssociatedTermsOrGroups_Returns400BadRequest()
    {
        var (teacher, token) = await CreateAndLoginTeacherAsync("teacher_ay_delete_prevent");

        using var db = _factory.CreateDbContext();
        var year = new AcademicYear
        {
            Id = Guid.NewGuid(),
            TeacherId = teacher.Id,
            Name = "Year With Term",
            StartDate = new DateTime(2025, 9, 1),
            EndDate = new DateTime(2026, 6, 30)
        };
        db.AcademicYears.Add(year);

        var term = new Term
        {
            Id = Guid.NewGuid(),
            TeacherId = teacher.Id,
            AcademicYearId = year.Id,
            Name = "Term 1",
            StartDate = new DateTime(2025, 9, 1),
            EndDate = new DateTime(2026, 1, 15)
        };
        db.Terms.Add(term);
        await db.SaveChangesAsync();

        // Attempt deletion -> Should be blocked per Rule BR-ACA-011
        var delReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/teacher/academic-years/{year.Id}");
        delReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var delRes = await _client.SendAsync(delReq);

        Assert.Equal(HttpStatusCode.BadRequest, delRes.StatusCode);
        var json = await delRes.Content.ReadAsStringAsync();
        Assert.Contains("archive it instead", json);
    }

    [Fact]
    public async Task DeleteAcademicYear_WhenEmpty_Returns200AndDeletes()
    {
        var (_, token) = await CreateAndLoginTeacherAsync("teacher_ay_delete_ok");

        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/teacher/academic-years")
        {
            Content = JsonContent.Create(new CreateAcademicYearRequest
            {
                Name = "Empty Year To Delete",
                StartDate = new DateTime(2028, 9, 1),
                EndDate = new DateTime(2029, 6, 30)
            })
        };
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var createRes = await _client.SendAsync(createReq);
        var year = await createRes.Content.ReadFromJsonAsync<AcademicYearResponseDto>();

        var delReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/teacher/academic-years/{year!.Id}");
        delReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var delRes = await _client.SendAsync(delReq);

        Assert.Equal(HttpStatusCode.OK, delRes.StatusCode);

        using var db = _factory.CreateDbContext();
        var exists = await db.AcademicYears.AnyAsync(ay => ay.Id == year.Id);
        Assert.False(exists);
    }

    [Fact]
    public async Task AcademicYear_CrossTenantIsolation_TeacherCannotAccessOrMutateOtherTeachersYear()
    {
        var (_, tokenA) = await CreateAndLoginTeacherAsync("teacher_ay_iso_a");
        var (_, tokenB) = await CreateAndLoginTeacherAsync("teacher_ay_iso_b");

        // Teacher A creates Year A
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/teacher/academic-years")
        {
            Content = JsonContent.Create(new CreateAcademicYearRequest
            {
                Name = "Teacher A Private Year",
                StartDate = new DateTime(2025, 9, 1),
                EndDate = new DateTime(2026, 6, 30)
            })
        };
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var createRes = await _client.SendAsync(createReq);
        var yearA = await createRes.Content.ReadFromJsonAsync<AcademicYearResponseDto>();

        // Teacher B attempts to view Teacher A's Year -> 404 Not Found
        var getReq = new HttpRequestMessage(HttpMethod.Get, $"/api/teacher/academic-years/{yearA!.Id}");
        getReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var getRes = await _client.SendAsync(getReq);
        Assert.Equal(HttpStatusCode.NotFound, getRes.StatusCode);

        // Teacher B attempts to activate Teacher A's Year -> 404 Not Found
        var actReq = new HttpRequestMessage(HttpMethod.Patch, $"/api/teacher/academic-years/{yearA.Id}/activate");
        actReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var actRes = await _client.SendAsync(actReq);
        Assert.Equal(HttpStatusCode.NotFound, actRes.StatusCode);

        // Teacher B attempts to add a Term to Teacher A's Year -> 404 Not Found (Cross-tenant prevention)
        var termReq = new HttpRequestMessage(HttpMethod.Post, $"/api/teacher/academic-years/{yearA.Id}/terms")
        {
            Content = JsonContent.Create(new CreateTermRequest
            {
                Name = "Cross-Tenant Term",
                StartDate = new DateTime(2025, 9, 1),
                EndDate = new DateTime(2026, 1, 15)
            })
        };
        termReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var termRes = await _client.SendAsync(termReq);
        Assert.Equal(HttpStatusCode.NotFound, termRes.StatusCode);
    }

    [Fact]
    public async Task Terms_CreateAndManage_UnderAcademicYear_Succeeds()
    {
        var (_, token) = await CreateAndLoginTeacherAsync("teacher_term_flow");

        // 1. Create Academic Year
        var yearReq = new HttpRequestMessage(HttpMethod.Post, "/api/teacher/academic-years")
        {
            Content = JsonContent.Create(new CreateAcademicYearRequest
            {
                Name = "Academic Year For Terms",
                StartDate = new DateTime(2025, 9, 1),
                EndDate = new DateTime(2026, 6, 30),
                SetAsActive = true
            })
        };
        yearReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var yearRes = await _client.SendAsync(yearReq);
        var year = await yearRes.Content.ReadFromJsonAsync<AcademicYearResponseDto>();

        // 2. Create Term 1 under Year
        var termReq = new HttpRequestMessage(HttpMethod.Post, $"/api/teacher/academic-years/{year!.Id}/terms")
        {
            Content = JsonContent.Create(new CreateTermRequest
            {
                Name = "Fall Term",
                StartDate = new DateTime(2025, 9, 1),
                EndDate = new DateTime(2026, 1, 15),
                IsActive = true
            })
        };
        termReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var termRes = await _client.SendAsync(termReq);
        Assert.Equal(HttpStatusCode.Created, termRes.StatusCode);
        var term = await termRes.Content.ReadFromJsonAsync<TermResponseDto>();
        Assert.NotNull(term);
        Assert.Equal("Fall Term", term.Name);

        // 3. Update Term
        var updateReq = new HttpRequestMessage(HttpMethod.Put, $"/api/teacher/terms/{term.Id}")
        {
            Content = JsonContent.Create(new UpdateTermRequest
            {
                Name = "Fall Term (Updated)",
                StartDate = new DateTime(2025, 9, 15),
                EndDate = new DateTime(2026, 1, 20)
            })
        };
        updateReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var updateRes = await _client.SendAsync(updateReq);
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);

        // 4. Update Term Status (Deactivate)
        var statusReq = new HttpRequestMessage(HttpMethod.Patch, $"/api/teacher/terms/{term.Id}/status")
        {
            Content = JsonContent.Create(new UpdateTermStatusRequest { IsActive = false })
        };
        statusReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var statusRes = await _client.SendAsync(statusReq);
        Assert.Equal(HttpStatusCode.OK, statusRes.StatusCode);

        // 5. Delete Term
        var delReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/teacher/terms/{term.Id}");
        delReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var delRes = await _client.SendAsync(delReq);
        Assert.Equal(HttpStatusCode.OK, delRes.StatusCode);
    }
}
