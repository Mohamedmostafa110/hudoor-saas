namespace Hudoor.Api.Data.Entities;

public class Teacher
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? BrandName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigations
    public ICollection<AcademicYear> AcademicYears { get; set; } = new List<AcademicYear>();
    public ICollection<Term> Terms { get; set; } = new List<Term>();
    public ICollection<Subject> Subjects { get; set; } = new List<Subject>();
    public ICollection<Group> Groups { get; set; } = new List<Group>();
    public ICollection<Student> Students { get; set; } = new List<Student>();
    public ICollection<GroupEnrollment> Enrollments { get; set; } = new List<GroupEnrollment>();
    public ICollection<Session> Sessions { get; set; } = new List<Session>();
    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
}
