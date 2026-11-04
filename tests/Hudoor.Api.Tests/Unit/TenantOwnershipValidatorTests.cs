using Hudoor.Api.Data;
using Hudoor.Api.Data.Entities;
using Hudoor.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Hudoor.Api.Tests.Unit;

public class TenantOwnershipValidatorTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("TenantOwnershipValidatorTestDb_" + Guid.NewGuid())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task ValidateAcademicYearOwnershipAsync_ShouldReturnTrueForOwner_AndFalseForOther()
    {
        using var context = CreateInMemoryDbContext();
        var teacher1Id = Guid.NewGuid();
        var teacher2Id = Guid.NewGuid();

        var year = new AcademicYear
        {
            Id = Guid.NewGuid(),
            TeacherId = teacher1Id,
            Name = "2025/2026",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddMonths(9)
        };
        context.AcademicYears.Add(year);
        await context.SaveChangesAsync();

        var validator = new TenantOwnershipValidator(context);

        Assert.True(await validator.ValidateAcademicYearOwnershipAsync(year.Id, teacher1Id));
        Assert.False(await validator.ValidateAcademicYearOwnershipAsync(year.Id, teacher2Id));
        Assert.False(await validator.ValidateAcademicYearOwnershipAsync(Guid.NewGuid(), teacher1Id));
    }

    [Fact]
    public async Task ValidateGroupCreationOwnershipAsync_ShouldPreventCrossTenantLinkage()
    {
        using var context = CreateInMemoryDbContext();
        var teacherAId = Guid.NewGuid();
        var teacherBId = Guid.NewGuid();

        var yearA = new AcademicYear { Id = Guid.NewGuid(), TeacherId = teacherAId, Name = "Year A" };
        var yearB = new AcademicYear { Id = Guid.NewGuid(), TeacherId = teacherBId, Name = "Year B" };

        var subjectA = new Subject { Id = Guid.NewGuid(), TeacherId = teacherAId, Name = "Physics" };
        var subjectB = new Subject { Id = Guid.NewGuid(), TeacherId = teacherBId, Name = "Chemistry" };

        context.AcademicYears.AddRange(yearA, yearB);
        context.Subjects.AddRange(subjectA, subjectB);
        await context.SaveChangesAsync();

        var validator = new TenantOwnershipValidator(context);

        // Case 1: Both subject and academic year belong to Teacher A -> Valid
        Assert.True(await validator.ValidateGroupCreationOwnershipAsync(subjectA.Id, yearA.Id, teacherAId));

        // Case 2: Subject belongs to Teacher B, but Teacher A tries to create a group with it -> REJECTED!
        Assert.False(await validator.ValidateGroupCreationOwnershipAsync(subjectB.Id, yearA.Id, teacherAId));

        // Case 3: Academic year belongs to Teacher B, but Teacher A tries to create a group with it -> REJECTED!
        Assert.False(await validator.ValidateGroupCreationOwnershipAsync(subjectA.Id, yearB.Id, teacherAId));

        // Case 4: Both belong to Teacher B, but Teacher A attempts to claim -> REJECTED!
        Assert.False(await validator.ValidateGroupCreationOwnershipAsync(subjectB.Id, yearB.Id, teacherAId));
    }

    [Fact]
    public async Task ValidateEnrollmentOwnershipAsync_ShouldPreventCrossTenantEnrollment()
    {
        using var context = CreateInMemoryDbContext();
        var teacher1Id = Guid.NewGuid();
        var teacher2Id = Guid.NewGuid();

        var group1 = new Group { Id = Guid.NewGuid(), TeacherId = teacher1Id, Name = "Group 1" };
        var student1 = new Student { Id = Guid.NewGuid(), TeacherId = teacher1Id, StudentCode = "HD-AAA111", FullName = "Student 1" };
        var student2 = new Student { Id = Guid.NewGuid(), TeacherId = teacher2Id, StudentCode = "HD-BBB222", FullName = "Student 2" };

        context.Groups.Add(group1);
        context.Students.AddRange(student1, student2);
        await context.SaveChangesAsync();

        var validator = new TenantOwnershipValidator(context);

        // Teacher 1 enrolls Teacher 1's student in Teacher 1's group -> Valid
        Assert.True(await validator.ValidateEnrollmentOwnershipAsync(group1.Id, student1.Id, teacher1Id));

        // Teacher 1 attempts to enroll Teacher 2's student into Teacher 1's group -> REJECTED!
        Assert.False(await validator.ValidateEnrollmentOwnershipAsync(group1.Id, student2.Id, teacher1Id));
    }

    [Fact]
    public async Task ValidateTransferOwnershipAsync_ShouldEnsureStudentAndBothGroupsBelongToSameTeacher()
    {
        using var context = CreateInMemoryDbContext();
        var teacher1Id = Guid.NewGuid();
        var teacher2Id = Guid.NewGuid();

        var student1 = new Student { Id = Guid.NewGuid(), TeacherId = teacher1Id, StudentCode = "HD-STU001", FullName = "Student 1" };
        var groupA = new Group { Id = Guid.NewGuid(), TeacherId = teacher1Id, Name = "Group A" };
        var groupB = new Group { Id = Guid.NewGuid(), TeacherId = teacher1Id, Name = "Group B" };
        var foreignGroup = new Group { Id = Guid.NewGuid(), TeacherId = teacher2Id, Name = "Group Other" };

        context.Students.Add(student1);
        context.Groups.AddRange(groupA, groupB, foreignGroup);
        await context.SaveChangesAsync();

        var validator = new TenantOwnershipValidator(context);

        // Valid transfer within Teacher 1's domain: Group A -> Group B
        Assert.True(await validator.ValidateTransferOwnershipAsync(student1.Id, groupA.Id, groupB.Id, teacher1Id));

        // Invalid transfer: Target group belongs to Teacher 2 -> REJECTED!
        Assert.False(await validator.ValidateTransferOwnershipAsync(student1.Id, groupA.Id, foreignGroup.Id, teacher1Id));

        // Invalid transfer: Source group belongs to Teacher 2 -> REJECTED!
        Assert.False(await validator.ValidateTransferOwnershipAsync(student1.Id, foreignGroup.Id, groupB.Id, teacher1Id));
    }
}
