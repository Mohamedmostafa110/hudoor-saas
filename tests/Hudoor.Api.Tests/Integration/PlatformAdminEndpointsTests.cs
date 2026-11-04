using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Hudoor.Api.Common;
using Hudoor.Api.DTOs;
using Hudoor.Api.Tests.Infrastructure;

namespace Hudoor.Api.Tests.Integration;

public class PlatformAdminEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public PlatformAdminEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<string> GetAdminTokenAsync()
    {
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/platform-admin/login", new PlatformAdminLoginRequest
        {
            Username = "admin",
            Password = "AdminSecure2026!"
        });
        loginResponse.EnsureSuccessStatusCode();
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        return auth!.Token;
    }

    [Fact]
    public async Task CreateTeacher_AsPlatformAdmin_Returns201AndTeacherDtoWithoutPassword()
    {
        var adminToken = await GetAdminTokenAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/platform-admin/teachers")
        {
            Content = JsonContent.Create(new CreateTeacherRequest
            {
                Username = "new_math_teacher",
                Password = "TeacherPass2026!",
                FullName = "Ahmed Mansour",
                Email = "ahmed.mansour@example.com",
                PhoneNumber = "+201001234567",
                BrandName = "Al-Najah Center"
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<TeacherResponseDto>();
        Assert.NotNull(body);
        Assert.Equal("new_math_teacher", body.Username);
        Assert.Equal("Ahmed Mansour", body.FullName);
        Assert.Equal("ahmed.mansour@example.com", body.Email);
        Assert.True(body.IsActive);

        // Security check: Verify no password hash is returned in response
        var rawJson = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("PasswordHash", rawJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateTeacher_WithoutAdminRole_Returns401Or403()
    {
        // Without token -> 401
        var unauthResponse = await _client.PostAsJsonAsync("/api/platform-admin/teachers", new CreateTeacherRequest
        {
            Username = "unauth_teacher",
            Password = "TeacherPass2026!",
            FullName = "Unauth Teacher",
            Email = "unauth@example.com"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, unauthResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateTeacherStatus_DeactivatesAndReactivatesTeacher()
    {
        var adminToken = await GetAdminTokenAsync();

        // 1. Create a teacher
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/platform-admin/teachers")
        {
            Content = JsonContent.Create(new CreateTeacherRequest
            {
                Username = "status_toggle_teacher",
                Password = "TeacherPass2026!",
                FullName = "Status Teacher",
                Email = "status.teacher@example.com"
            })
        };
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var createResponse = await _client.SendAsync(createRequest);
        var teacher = await createResponse.Content.ReadFromJsonAsync<TeacherResponseDto>();

        // 2. Deactivate teacher
        var deactivateRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/platform-admin/teachers/{teacher!.Id}/status")
        {
            Content = JsonContent.Create(new UpdateTeacherStatusRequest { IsActive = false })
        };
        deactivateRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var deactResponse = await _client.SendAsync(deactivateRequest);
        Assert.Equal(HttpStatusCode.OK, deactResponse.StatusCode);

        // 3. Verify teacher cannot log in
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/teacher/login", new TeacherLoginRequest
        {
            Username = "status_toggle_teacher",
            Password = "TeacherPass2026!"
        });
        Assert.Equal(HttpStatusCode.Forbidden, loginResponse.StatusCode);

        // 4. Reactivate teacher
        var reactivateRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/platform-admin/teachers/{teacher.Id}/status")
        {
            Content = JsonContent.Create(new UpdateTeacherStatusRequest { IsActive = true })
        };
        reactivateRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var reactResponse = await _client.SendAsync(reactivateRequest);
        Assert.Equal(HttpStatusCode.OK, reactResponse.StatusCode);

        // 5. Verify teacher can log in again
        var loginAgainResponse = await _client.PostAsJsonAsync("/api/auth/teacher/login", new TeacherLoginRequest
        {
            Username = "status_toggle_teacher",
            Password = "TeacherPass2026!"
        });
        Assert.Equal(HttpStatusCode.OK, loginAgainResponse.StatusCode);
    }
}
