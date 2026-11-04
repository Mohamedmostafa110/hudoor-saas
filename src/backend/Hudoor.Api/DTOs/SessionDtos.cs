using System.ComponentModel.DataAnnotations;

namespace Hudoor.Api.DTOs;

public class CreateSessionRequest
{
    [Required(ErrorMessage = "GroupId is required.")]
    public Guid GroupId { get; set; }

    [Required(ErrorMessage = "Session title is required.")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 200 characters.")]
    public string Title { get; set; } = string.Empty;

    public DateTime? SessionDateUtc { get; set; }

    [RegularExpression("^(Draft|Active)$", ErrorMessage = "Initial status must be either 'Draft' or 'Active'.")]
    public string? Status { get; set; }
}

public class UpdateSessionRequest
{
    [Required(ErrorMessage = "Session title is required.")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 200 characters.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Session date is required.")]
    public DateTime SessionDateUtc { get; set; }
}

public class UpdateSessionStatusRequest
{
    [Required(ErrorMessage = "Status is required.")]
    [RegularExpression("^(Draft|Active|Closed|Archived)$", ErrorMessage = "Status must be 'Draft', 'Active', 'Closed', or 'Archived'.")]
    public string Status { get; set; } = string.Empty;
}

public class SessionSummaryDto
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public string AcademicYearName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime SessionDateUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }

    // Aggregate Metrics
    public int EnrolledStudentsCount { get; set; }
    public int PresentCount { get; set; }
    public int LateCount { get; set; }
    public int ExcusedCount { get; set; }
    public int AbsentCount { get; set; }
    public int UnrecordedCount { get; set; }
}

public class SessionDetailDto : SessionSummaryDto
{
    public bool IsQrCheckInAvailable => Status == "Active";
}

public class SessionQrTokenDto
{
    public Guid SessionId { get; set; }
    public string QrToken { get; set; } = string.Empty;
    public int RemainingSeconds { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}
