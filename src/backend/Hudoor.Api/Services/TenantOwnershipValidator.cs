using Hudoor.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Hudoor.Api.Services;

public class TenantOwnershipValidator : ITenantOwnershipValidator
{
    private readonly ApplicationDbContext _dbContext;

    public TenantOwnershipValidator(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> ValidateAcademicYearOwnershipAsync(Guid academicYearId, Guid teacherId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.AcademicYears
            .AsNoTracking()
            .AnyAsync(ay => ay.Id == academicYearId && ay.TeacherId == teacherId, cancellationToken);
    }

    public async Task<bool> ValidateTermOwnershipAsync(Guid termId, Guid teacherId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Terms
            .AsNoTracking()
            .AnyAsync(t => t.Id == termId && t.TeacherId == teacherId, cancellationToken);
    }

    public async Task<bool> ValidateSubjectOwnershipAsync(Guid subjectId, Guid teacherId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Subjects
            .AsNoTracking()
            .AnyAsync(s => s.Id == subjectId && s.TeacherId == teacherId, cancellationToken);
    }

    public async Task<bool> ValidateGroupOwnershipAsync(Guid groupId, Guid teacherId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Groups
            .AsNoTracking()
            .AnyAsync(g => g.Id == groupId && g.TeacherId == teacherId, cancellationToken);
    }

    public async Task<bool> ValidateStudentOwnershipAsync(Guid studentId, Guid teacherId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Students
            .AsNoTracking()
            .AnyAsync(s => s.Id == studentId && s.TeacherId == teacherId, cancellationToken);
    }

    public async Task<bool> ValidateGroupCreationOwnershipAsync(Guid subjectId, Guid academicYearId, Guid teacherId, CancellationToken cancellationToken = default)
    {
        var isSubjectOwned = await ValidateSubjectOwnershipAsync(subjectId, teacherId, cancellationToken);
        if (!isSubjectOwned)
        {
            return false;
        }

        var isYearOwned = await ValidateAcademicYearOwnershipAsync(academicYearId, teacherId, cancellationToken);
        return isYearOwned;
    }

    public async Task<bool> ValidateEnrollmentOwnershipAsync(Guid groupId, Guid studentId, Guid teacherId, CancellationToken cancellationToken = default)
    {
        var isGroupOwned = await ValidateGroupOwnershipAsync(groupId, teacherId, cancellationToken);
        if (!isGroupOwned)
        {
            return false;
        }

        var isStudentOwned = await ValidateStudentOwnershipAsync(studentId, teacherId, cancellationToken);
        return isStudentOwned;
    }

    public async Task<bool> ValidateTransferOwnershipAsync(Guid studentId, Guid fromGroupId, Guid toGroupId, Guid teacherId, CancellationToken cancellationToken = default)
    {
        var isStudentOwned = await ValidateStudentOwnershipAsync(studentId, teacherId, cancellationToken);
        if (!isStudentOwned)
        {
            return false;
        }

        var isFromGroupOwned = await ValidateGroupOwnershipAsync(fromGroupId, teacherId, cancellationToken);
        if (!isFromGroupOwned)
        {
            return false;
        }

        var isToGroupOwned = await ValidateGroupOwnershipAsync(toGroupId, teacherId, cancellationToken);
        return isToGroupOwned;
    }
}
