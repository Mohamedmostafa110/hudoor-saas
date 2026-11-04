using System.ComponentModel.DataAnnotations;

namespace Hudoor.Api.DTOs;

public class CreateSubjectRequest
{
    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [StringLength(100)]
    public string? GradeLevel { get; set; }
}

public class UpdateSubjectRequest
{
    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [StringLength(100)]
    public string? GradeLevel { get; set; }
}

public class SubjectResponseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? GradeLevel { get; set; }
    public bool IsArchived { get; set; }
    public int GroupsCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class SubjectDetailResponseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? GradeLevel { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public List<SubjectGroupSummaryDto> Groups { get; set; } = new();
}

public class SubjectGroupSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AcademicYearName { get; set; } = string.Empty;
    public string? ScheduleDescription { get; set; }
    public int? MaxCapacity { get; set; }
    public bool IsArchived { get; set; }
}
