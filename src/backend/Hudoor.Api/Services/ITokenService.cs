using Hudoor.Api.Data.Entities;

namespace Hudoor.Api.Services;

public interface ITokenService
{
    (string Token, DateTime ExpiresAtUtc) GenerateTokenForPlatformAdmin(PlatformAdmin admin);
    (string Token, DateTime ExpiresAtUtc) GenerateTokenForTeacher(Teacher teacher);
    (string Token, DateTime ExpiresAtUtc) GenerateTokenForStudent(Student student);
}
