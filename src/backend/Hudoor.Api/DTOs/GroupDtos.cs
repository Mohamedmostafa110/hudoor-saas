using System.ComponentModel.DataAnnotations;

namespace Hudoor.Api.DTOs;

public class CreateGroupRequest
{
    [Required]
    public Guid SubjectId { get; set; }

    [Required]
    public Guid AcademicYearId { get; set; }

    [Required]
    [StringLength(150, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [StringLength(300)]
    public string? ScheduleDescription { get; set; }

    [Range(1, 1000)]
    public int? MaxCapacity { get; set; }
}

public class UpdateGroupRequest
{
    [Required]
    [StringLength(150, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [StringLength(300)]
    public string? ScheduleDescription { get; set; }

    [Range(1, 1000)]
    public int? MaxCapacity { get; set; }
}

public class GroupResponseDto
{
    public Guid Id { get; set; }
    public Guid SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public Guid AcademicYearId { get; set; }
    public string AcademicYearName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ScheduleDescription { get; set; }
    public int? MaxCapacity { get; set; }
    public int EnrolledStudentsCount { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class GroupDetailResponseDto
{
    public Guid Id { get; set; }
    public Guid SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public Guid AcademicYearId { get; set; }
    public string AcademicYearName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ScheduleDescription { get; set; }
    public int? MaxCapacity { get; set; }
    public int EnrolledStudentsCount { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public List<GroupEnrolledStudentSummaryDto> EnrolledStudents { get; set; } = new();
}

public class GroupEnrolledStudentSummaryDto
{
    public Guid StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public DateTime JoinedDateUtc { get; set; }
}
