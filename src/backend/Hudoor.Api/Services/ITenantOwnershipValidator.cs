namespace Hudoor.Api.Services;

public interface ITenantOwnershipValidator
{
    Task<bool> ValidateAcademicYearOwnershipAsync(Guid academicYearId, Guid teacherId, CancellationToken cancellationToken = default);
    Task<bool> ValidateTermOwnershipAsync(Guid termId, Guid teacherId, CancellationToken cancellationToken = default);
    Task<bool> ValidateSubjectOwnershipAsync(Guid subjectId, Guid teacherId, CancellationToken cancellationToken = default);
    Task<bool> ValidateGroupOwnershipAsync(Guid groupId, Guid teacherId, CancellationToken cancellationToken = default);
    Task<bool> ValidateStudentOwnershipAsync(Guid studentId, Guid teacherId, CancellationToken cancellationToken = default);
    Task<bool> ValidateGroupCreationOwnershipAsync(Guid subjectId, Guid academicYearId, Guid teacherId, CancellationToken cancellationToken = default);
    Task<bool> ValidateEnrollmentOwnershipAsync(Guid groupId, Guid studentId, Guid teacherId, CancellationToken cancellationToken = default);
    Task<bool> ValidateTransferOwnershipAsync(Guid studentId, Guid fromGroupId, Guid toGroupId, Guid teacherId, CancellationToken cancellationToken = default);
}
