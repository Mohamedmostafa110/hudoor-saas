using System.ComponentModel.DataAnnotations;

namespace Hudoor.Api.DTOs;

public class RolloverSummaryDto
{
    public Guid CurrentAcademicYearId { get; set; }
    public string CurrentAcademicYearName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalGroups { get; set; }
    public int TotalEnrolledStudents { get; set; }
    public List<GradeStudentsCountDto> StudentsByGrade { get; set; } = new();
}

public class GradeStudentsCountDto
{
    public string Grade { get; set; } = string.Empty;
    public int StudentCount { get; set; }
}

public class ExecuteRolloverRequest
{
    [Required(ErrorMessage = "New academic year name is required.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Name must be between 3 and 100 characters.")]
    public string NewAcademicYearName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Start date is required.")]
    public DateTime StartDate { get; set; }

    [Required(ErrorMessage = "End date is required.")]
    public DateTime EndDate { get; set; }

    public List<StudentProgressionItemRequest> StudentProgressions { get; set; } = new();
}

public class StudentProgressionItemRequest
{
    [Required(ErrorMessage = "StudentId is required.")]
    public Guid StudentId { get; set; }

    [Required(ErrorMessage = "Action is required.")]
    [RegularExpression("^(Promote|Repeat|Depart)$", ErrorMessage = "Action must be 'Promote', 'Repeat', or 'Depart'.")]
    public string Action { get; set; } = string.Empty;

    public string? NewGrade { get; set; }
    public Guid? TargetGroupId { get; set; }
}

public class RolloverResultDto
{
    public Guid PreviousAcademicYearId { get; set; }
    public string PreviousAcademicYearName { get; set; } = string.Empty;
    public Guid NewAcademicYearId { get; set; }
    public string NewAcademicYearName { get; set; } = string.Empty;
    public int PromotedCount { get; set; }
    public int RepeatedCount { get; set; }
    public int DepartedCount { get; set; }
    public int TotalProcessed { get; set; }
    public DateTime ExecutedAtUtc { get; set; }
    public string Message { get; set; } = "Academic year rollover executed successfully without data loss.";
}
