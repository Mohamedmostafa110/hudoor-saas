using System.ComponentModel.DataAnnotations;

namespace Hudoor.Api.DTOs;

public class CreateStudentRequest
{
    [Required(ErrorMessage = "Student full name is required.")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Full name must be between 3 and 200 characters.")]
    public string FullName { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Invalid phone number format.")]
    [StringLength(50, ErrorMessage = "Phone number cannot exceed 50 characters.")]
    public string? PhoneNumber { get; set; }

    [Phone(ErrorMessage = "Invalid parent phone number format.")]
    [StringLength(50, ErrorMessage = "Parent phone number cannot exceed 50 characters.")]
    public string? ParentPhoneNumber { get; set; }

    [StringLength(100, ErrorMessage = "Grade cannot exceed 100 characters.")]
    public string? CurrentGrade { get; set; }

    [StringLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters.")]
    public string? Notes { get; set; }

    public Guid? InitialGroupId { get; set; }
}

public class UpdateStudentRequest
{
    [Required(ErrorMessage = "Student full name is required.")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Full name must be between 3 and 200 characters.")]
    public string FullName { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Invalid phone number format.")]
    [StringLength(50, ErrorMessage = "Phone number cannot exceed 50 characters.")]
    public string? PhoneNumber { get; set; }

    [Phone(ErrorMessage = "Invalid parent phone number format.")]
    [StringLength(50, ErrorMessage = "Parent phone number cannot exceed 50 characters.")]
    public string? ParentPhoneNumber { get; set; }

    [StringLength(100, ErrorMessage = "Grade cannot exceed 100 characters.")]
    public string? CurrentGrade { get; set; }

    [StringLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters.")]
    public string? Notes { get; set; }
}

public class UpdateStudentStatusRequest
{
    public bool IsActive { get; set; }
}

public class StudentSummaryDto
{
    public Guid Id { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? ParentPhoneNumber { get; set; }
    public string? CurrentGrade { get; set; }
    public bool IsActive { get; set; }
    public bool MustChangePassword { get; set; }
    public int EnrolledGroupsCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class StudentDetailDto
{
    public Guid Id { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? ParentPhoneNumber { get; set; }
    public string? CurrentGrade { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTime? TemporaryPasswordExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public List<StudentEnrollmentSummaryDto> Enrollments { get; set; } = new();
}

public class StudentEnrollmentSummaryDto
{
    public Guid EnrollmentId { get; set; }
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public string AcademicYearName { get; set; } = string.Empty;
    public DateTime JoinedDateUtc { get; set; }
    public DateTime? LeftDateUtc { get; set; }
    public bool IsActive { get; set; }
}

public class StudentCreatedResponseDto
{
    public Guid Id { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string TemporaryPassword { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? CurrentGrade { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public Guid? InitialGroupId { get; set; }
    public string? InitialGroupName { get; set; }
}

public class RegenerateCodeResponseDto
{
    public Guid StudentId { get; set; }
    public string OldStudentCode { get; set; } = string.Empty;
    public string NewStudentCode { get; set; } = string.Empty;
    public int TokenVersion { get; set; }
}
