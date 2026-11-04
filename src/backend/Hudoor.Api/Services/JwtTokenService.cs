using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Hudoor.Api.Common;
using Hudoor.Api.Data.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Hudoor.Api.Services;

public class JwtTokenService : ITokenService
{
    private readonly IConfiguration _configuration;
    private readonly SymmetricSecurityKey _signingKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expiryMinutes;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
        var keyString = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key is not configured.");

        if (Encoding.UTF8.GetByteCount(keyString) < 32)
        {
            throw new InvalidOperationException("Jwt:Key must be at least 256 bits (32 bytes) long.");
        }

        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
        _issuer = _configuration["Jwt:Issuer"] ?? "Hudoor.Api";
        _audience = _configuration["Jwt:Audience"] ?? "Hudoor.Client";
        _expiryMinutes = int.TryParse(_configuration["Jwt:ExpiryMinutes"], out var minutes) ? minutes : 120;
    }

    public (string Token, DateTime ExpiresAtUtc) GenerateTokenForPlatformAdmin(PlatformAdmin admin)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, admin.Id.ToString()),
            new(ClaimTypes.NameIdentifier, admin.Id.ToString()),
            new(ClaimTypes.Name, admin.Username),
            new(ClaimTypes.Role, Roles.PlatformAdmin),
            new("admin_id", admin.Id.ToString())
        };

        return BuildToken(claims);
    }

    public (string Token, DateTime ExpiresAtUtc) GenerateTokenForTeacher(Teacher teacher)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, teacher.Id.ToString()),
            new(ClaimTypes.NameIdentifier, teacher.Id.ToString()),
            new(ClaimTypes.Name, teacher.Username),
            new(ClaimTypes.Role, Roles.Teacher),
            new("teacher_id", teacher.Id.ToString())
        };

        return BuildToken(claims);
    }

    public (string Token, DateTime ExpiresAtUtc) GenerateTokenForStudent(Student student)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, student.Id.ToString()),
            new(ClaimTypes.NameIdentifier, student.Id.ToString()),
            new(ClaimTypes.Name, student.StudentCode),
            new(ClaimTypes.Role, Roles.Student),
            new("student_id", student.Id.ToString()),
            new("teacher_id", student.TeacherId.ToString()),
            new("tv", student.TokenVersion.ToString()),
            new("must_change_password", student.MustChangePassword.ToString().ToLowerInvariant())
        };

        return BuildToken(claims);
    }

    private (string Token, DateTime ExpiresAtUtc) BuildToken(IEnumerable<Claim> claims)
    {
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(_expiryMinutes);
        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAtUtc,
            Issuer = _issuer,
            Audience = _audience,
            SigningCredentials = credentials
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(tokenDescriptor);
        return (handler.WriteToken(token), expiresAtUtc);
    }
}
