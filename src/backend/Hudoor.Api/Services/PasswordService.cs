using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;

namespace Hudoor.Api.Services;

public class PasswordService : IPasswordService
{
    private readonly PasswordHasher<object> _hasher = new();
    private static readonly object DummyUser = new();

    // Curated neutral words: pleasant, easy to read, pronounceable, strictly avoiding personal info
    private static readonly string[] NeutralWords =
    [
        "Falcon", "Cedar", "Swift", "Atlas", "Beacon",
        "Summit", "Silver", "Haven", "River", "Opal",
        "Breeze", "Canyon", "Timber", "Harbor", "Solar",
        "Orion", "Jasper", "Willow", "Cosmo", "Zenith"
    ];

    // Allowed digits excluding ambiguous characters '0' (looks like 'O') and '1' (looks like 'I' / 'l')
    private static readonly char[] UnambiguousDigits = ['2', '3', '4', '5', '6', '7', '8', '9'];

    public string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        return _hasher.HashPassword(DummyUser, password);
    }

    public bool VerifyPassword(string hashedPassword, string providedPassword)
    {
        if (string.IsNullOrWhiteSpace(hashedPassword) || string.IsNullOrWhiteSpace(providedPassword))
        {
            return false;
        }

        var result = _hasher.VerifyHashedPassword(DummyUser, hashedPassword, providedPassword);
        return result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded;
    }

    /// <summary>
    /// Generates a human-friendly temporary password using a cryptographically secure random number generator (CSPRNG).
    /// Format: NeutralWord + 3 unambiguous digits (e.g., Falcon482, Cedar735, Swift924).
    /// Strictly letters and numbers only, avoiding ambiguous characters (0, O, 1, I, l).
    /// </summary>
    public string GenerateTemporaryPassword()
    {
        var wordIndex = RandomNumberGenerator.GetInt32(NeutralWords.Length);
        var word = NeutralWords[wordIndex];

        Span<char> suffix = stackalloc char[3];
        for (var i = 0; i < 3; i++)
        {
            var digitIndex = RandomNumberGenerator.GetInt32(UnambiguousDigits.Length);
            suffix[i] = UnambiguousDigits[digitIndex];
        }

        return $"{word}{suffix.ToString()}";
    }

    public (bool IsValid, string? ErrorMessage) ValidateStudentPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return (false, "Password cannot be empty.");
        }

        if (password.Length < 6)
        {
            return (false, "Student password must be at least 6 characters long.");
        }

        var hasLetter = password.Any(char.IsLetter);
        var hasDigit = password.Any(char.IsDigit);

        if (!hasLetter || !hasDigit)
        {
            return (false, "Password must contain both letters and digits.");
        }

        return (true, null);
    }

    public (bool IsValid, string? ErrorMessage) ValidateStaffPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return (false, "Password cannot be empty.");
        }

        if (password.Length < 8)
        {
            return (false, "Staff password must be at least 8 characters long.");
        }

        var hasLetter = password.Any(char.IsLetter);
        var hasDigit = password.Any(char.IsDigit);

        if (!hasLetter || !hasDigit)
        {
            return (false, "Staff password must contain both letters and digits.");
        }

        return (true, null);
    }
}
