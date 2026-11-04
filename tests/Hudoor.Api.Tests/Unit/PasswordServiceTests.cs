using System.Text.RegularExpressions;
using Hudoor.Api.Services;

namespace Hudoor.Api.Tests.Unit;

public class PasswordServiceTests
{
    private readonly PasswordService _service = new();

    [Fact]
    public void GenerateTemporaryPassword_ShouldFollowFormat_NeutralWordAndDigitsOnly()
    {
        // Act
        var tempPassword = _service.GenerateTemporaryPassword();

        // Assert
        Assert.NotNull(tempPassword);
        Assert.True(tempPassword.Length >= 7, $"Length should be >= 7 but was {tempPassword.Length}");

        // Only letters and digits, no symbols
        Assert.Matches("^[A-Za-z]+[2-9]{3}$", tempPassword);

        // Ambiguous digits check: strictly no '0' or '1'
        Assert.DoesNotContain("0", tempPassword);
        Assert.DoesNotContain("1", tempPassword);
    }

    [Fact]
    public void GenerateTemporaryPassword_ShouldProduceDistinctValues_HighEntropy()
    {
        var passwords = new HashSet<string>();
        for (var i = 0; i < 50; i++)
        {
            passwords.Add(_service.GenerateTemporaryPassword());
        }

        // 50 generation calls should result in high variety (at least 40 unique passwords out of 50)
        Assert.True(passwords.Count >= 40, $"Expected >= 40 distinct passwords, got {passwords.Count}");
    }

    [Fact]
    public void HashPassword_And_VerifyPassword_ShouldVerifyCorrectly()
    {
        var plain = "Falcon482";
        var hash = _service.HashPassword(plain);

        Assert.NotNull(hash);
        Assert.NotEqual(plain, hash);

        Assert.True(_service.VerifyPassword(hash, plain));
        Assert.False(_service.VerifyPassword(hash, "WrongPassword"));
        Assert.False(_service.VerifyPassword(hash, "falcon482")); // Case-sensitive
    }

    [Fact]
    public void HashPassword_ShouldProduceUniqueSaltsForSameInput()
    {
        var plain = "Cedar735";
        var hash1 = _service.HashPassword(plain);
        var hash2 = _service.HashPassword(plain);

        Assert.NotEqual(hash1, hash2);
        Assert.True(_service.VerifyPassword(hash1, plain));
        Assert.True(_service.VerifyPassword(hash2, plain));
    }

    [Theory]
    [InlineData("abc", false)] // too short
    [InlineData("123456", false)] // no letters
    [InlineData("abcdef", false)] // no digits
    [InlineData("Pass12", true)] // 6 chars, letters & digits
    [InlineData("Student99", true)]
    public void ValidateStudentPassword_ShouldEnforcePolicy(string password, bool expectedValid)
    {
        var (isValid, _) = _service.ValidateStudentPassword(password);
        Assert.Equal(expectedValid, isValid);
    }

    [Theory]
    [InlineData("pass1", false)] // too short (<8)
    [InlineData("pass1234", true)] // 8 chars, letters & digits
    [InlineData("Teacher2026", true)]
    [InlineData("12345678", false)] // no letters
    [InlineData("abcdefgh", false)] // no digits
    public void ValidateStaffPassword_ShouldEnforcePolicy(string password, bool expectedValid)
    {
        var (isValid, _) = _service.ValidateStaffPassword(password);
        Assert.Equal(expectedValid, isValid);
    }
}
