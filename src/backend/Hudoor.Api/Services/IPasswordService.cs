namespace Hudoor.Api.Services;

public interface IPasswordService
{
    string HashPassword(string password);
    bool VerifyPassword(string hashedPassword, string providedPassword);
    string GenerateTemporaryPassword();
    (bool IsValid, string? ErrorMessage) ValidateStudentPassword(string password);
    (bool IsValid, string? ErrorMessage) ValidateStaffPassword(string password);
}
