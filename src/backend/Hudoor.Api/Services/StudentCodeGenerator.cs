using System.Security.Cryptography;
using Hudoor.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Hudoor.Api.Services;

public class StudentCodeGenerator : IStudentCodeGenerator
{
    private readonly ApplicationDbContext _dbContext;

    // 31 unambiguous alphanumeric characters: excludes 0, O, 1, I, L
    private static readonly char[] UnambiguousChars =
    [
        '2', '3', '4', '5', '6', '7', '8', '9',
        'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H',
        'J', 'K', 'M', 'N', 'P', 'Q', 'R', 'S',
        'T', 'U', 'V', 'W', 'X', 'Y', 'Z'
    ];

    private const string CodePrefix = "HD-";
    private const int SuffixLength = 6;
    private const int MaxCollisionRetries = 10;

    public StudentCodeGenerator(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<string> GenerateUniqueStudentCodeAsync(CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt < MaxCollisionRetries; attempt++)
        {
            var code = GenerateCandidateCode();

            // Check global uniqueness across the entire platform in SQL Server
            var exists = await _dbContext.Students
                .AsNoTracking()
                .AnyAsync(s => s.StudentCode == code, cancellationToken);

            if (!exists)
            {
                return code;
            }
        }

        throw new InvalidOperationException("Failed to generate a unique student code after multiple attempts. Please retry.");
    }

    private static string GenerateCandidateCode()
    {
        Span<char> buffer = stackalloc char[SuffixLength];
        for (var i = 0; i < SuffixLength; i++)
        {
            var index = RandomNumberGenerator.GetInt32(UnambiguousChars.Length);
            buffer[i] = UnambiguousChars[index];
        }

        return $"{CodePrefix}{buffer.ToString()}";
    }
}
