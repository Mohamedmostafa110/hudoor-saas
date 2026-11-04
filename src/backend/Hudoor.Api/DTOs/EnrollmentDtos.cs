using System.ComponentModel.DataAnnotations;

namespace Hudoor.Api.DTOs;

public class EnrollStudentRequest
{
    [Required(ErrorMessage = "StudentId is required.")]
    public Guid StudentId { get; set; }
}

public class TransferStudentRequest
{
    [Required(ErrorMessage = "StudentId is required.")]
    public Guid StudentId { get; set; }

    [Required(ErrorMessage = "FromGroupId is required.")]
    public Guid FromGroupId { get; set; }

    [Required(ErrorMessage = "ToGroupId is required.")]
    public Guid ToGroupId { get; set; }
}

public class GroupStudentDto
{
    public Guid EnrollmentId { get; set; }
    public Guid StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? ParentPhoneNumber { get; set; }
    public string? CurrentGrade { get; set; }
    public DateTime JoinedDateUtc { get; set; }
    public DateTime? LeftDateUtc { get; set; }
    public bool IsActive { get; set; }
}

public class EnrollmentResponseDto
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public Guid StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public DateTime JoinedDateUtc { get; set; }
    public DateTime? LeftDateUtc { get; set; }
    public bool IsActive { get; set; }
}

public class TransferResponseDto
{
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public Guid FromGroupId { get; set; }
    public string FromGroupName { get; set; } = string.Empty;
    public Guid ToGroupId { get; set; }
    public string ToGroupName { get; set; } = string.Empty;
    public DateTime TransferredAtUtc { get; set; }
    public Guid NewEnrollmentId { get; set; }
}
