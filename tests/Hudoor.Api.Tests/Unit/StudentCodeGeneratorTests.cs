using Hudoor.Api.Data;
using Hudoor.Api.Data.Entities;
using Hudoor.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Hudoor.Api.Tests.Unit;

public class StudentCodeGeneratorTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("StudentCodeGeneratorTestDb_" + Guid.NewGuid())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GenerateUniqueStudentCodeAsync_ShouldMatchExpectedFormatAndLength()
    {
        using var context = CreateInMemoryDbContext();
        var generator = new StudentCodeGenerator(context);

        var code = await generator.GenerateUniqueStudentCodeAsync();

        Assert.NotNull(code);
        Assert.StartsWith("HD-", code);
        Assert.Equal(9, code.Length); // "HD-" (3) + 6 characters = 9

        // Verify only unambiguous characters are used
        var suffix = code[3..];
        Assert.Matches("^[2-9A-HJ-KMNP-Z]{6}$", suffix);

        // Explicitly assert absence of ambiguous characters: 0, O, 1, I, L
        Assert.DoesNotContain("0", suffix);
        Assert.DoesNotContain("O", suffix);
        Assert.DoesNotContain("1", suffix);
        Assert.DoesNotContain("I", suffix);
        Assert.DoesNotContain("L", suffix);
    }

    [Fact]
    public async Task GenerateUniqueStudentCodeAsync_ShouldProduceDistinctCodes_HighEntropy()
    {
        using var context = CreateInMemoryDbContext();
        var generator = new StudentCodeGenerator(context);

        var codes = new HashSet<string>();
        for (var i = 0; i < 50; i++)
        {
            var code = await generator.GenerateUniqueStudentCodeAsync();
            codes.Add(code);
        }

        // 50 generation calls must all produce unique codes
        Assert.Equal(50, codes.Count);
    }

    [Fact]
    public async Task GenerateUniqueStudentCodeAsync_ShouldNotReturnExistingCodeInDatabase()
    {
        using var context = CreateInMemoryDbContext();
        var teacherId = Guid.NewGuid();

        // Seed a pre-existing code
        var existingCode = "HD-ABC234";
        context.Students.Add(new Student
        {
            Id = Guid.NewGuid(),
            TeacherId = teacherId,
            StudentCode = existingCode,
            FullName = "Existing Student"
        });
        await context.SaveChangesAsync();

        var generator = new StudentCodeGenerator(context);
        var generated = await generator.GenerateUniqueStudentCodeAsync();

        Assert.NotEqual(existingCode, generated);
    }
}
