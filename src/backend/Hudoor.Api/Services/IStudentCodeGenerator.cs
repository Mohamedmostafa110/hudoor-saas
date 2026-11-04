namespace Hudoor.Api.Services;

public interface IStudentCodeGenerator
{
    /// <summary>
    /// Generates a globally unique, high-entropy student code formatted with a prefix and unambiguous characters.
    /// Example format: HD-7W49KB (excluding 0, O, 1, I, L).
    /// </summary>
    Task<string> GenerateUniqueStudentCodeAsync(CancellationToken cancellationToken = default);
}
