using System.Security.Cryptography;
using Hudoor.Api.Data.Entities;
using Hudoor.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hudoor.Api.Data;

public static class DemoDataSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DemoDataSeeder");

        logger.LogInformation("Starting Hudoor Demo Data Seeding...");

        // 1. Platform Admin
        var admin = await context.PlatformAdmins.FirstOrDefaultAsync();
        if (admin == null)
        {
            admin = new PlatformAdmin
            {
                Id = Guid.NewGuid(),
                Username = "Mohamed1652006#",
                Email = "mohamedmostafa_1652006@outlook.com",
                FullName = "م. محمد مصطفى (مدير المنصة)",
                PasswordHash = passwordService.HashPassword("MoMs1652006$@"),
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            await context.PlatformAdmins.AddAsync(admin);
            await context.SaveChangesAsync();
            logger.LogInformation("Created Admin: Mohamed1652006# / MoMs1652006$@");
        }
        else
        {
            admin.Username = "Mohamed1652006#";
            admin.FullName = "م. محمد مصطفى (مدير المنصة)";
            admin.Email = "mohamedmostafa_1652006@outlook.com";
            admin.PasswordHash = passwordService.HashPassword("MoMs1652006$@");
            admin.IsActive = true;
            await context.SaveChangesAsync();
            logger.LogInformation("Updated Admin: Mohamed1652006# / MoMs1652006$@");
        }

        // 2. Demo Teacher
        var teacher = await context.Teachers.FirstOrDefaultAsync(t => t.Username == "teacher_demo");
        if (teacher == null)
        {
            teacher = new Teacher
            {
                Id = Guid.NewGuid(),
                Username = "teacher_demo",
                FullName = "أ. محمد مصطفى",
                Email = "mohamed.mostafa@hudoor.local",
                PhoneNumber = "01012345678",
                BrandName = "أكاديمية التفوق في الفيزياء",
                PasswordHash = passwordService.HashPassword("TeacherPass2026!"),
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            await context.Teachers.AddAsync(teacher);
            await context.SaveChangesAsync();
            logger.LogInformation("Created Teacher: teacher_demo / TeacherPass2026!");
        }

        // 3. Academic Year
        var academicYear = await context.AcademicYears.FirstOrDefaultAsync(y => y.TeacherId == teacher.Id && y.Name == "العام الدراسي 2025/2026");
        if (academicYear == null)
        {
            academicYear = new AcademicYear
            {
                Id = Guid.NewGuid(),
                TeacherId = teacher.Id,
                Name = "العام الدراسي 2025/2026",
                StartDate = new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true,
                IsArchived = false,
                CreatedAtUtc = DateTime.UtcNow
            };
            await context.AcademicYears.AddAsync(academicYear);
            await context.SaveChangesAsync();
        }

        // 4. Term
        var term = await context.Terms.FirstOrDefaultAsync(t => t.TeacherId == teacher.Id && t.Name == "الفصل الدراسي الثاني");
        if (term == null)
        {
            term = new Term
            {
                Id = Guid.NewGuid(),
                TeacherId = teacher.Id,
                AcademicYearId = academicYear.Id,
                Name = "الفصل الدراسي الثاني",
                StartDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            await context.Terms.AddAsync(term);
            await context.SaveChangesAsync();
        }

        // 5. Subject
        var subject = await context.Subjects.FirstOrDefaultAsync(s => s.TeacherId == teacher.Id && s.Name == "الفيزياء للثانوية العامة");
        if (subject == null)
        {
            subject = new Subject
            {
                Id = Guid.NewGuid(),
                TeacherId = teacher.Id,
                Name = "الفيزياء للثانوية العامة",
                GradeLevel = "الصف الثالث الثانوي",
                IsArchived = false,
                CreatedAtUtc = DateTime.UtcNow
            };
            await context.Subjects.AddAsync(subject);
            await context.SaveChangesAsync();
        }

        // 6. Groups
        var group1 = await context.Groups.FirstOrDefaultAsync(g => g.TeacherId == teacher.Id && g.Name.Contains("مجموعة العباقرة"));
        if (group1 == null)
        {
            group1 = new Group
            {
                Id = Guid.NewGuid(),
                TeacherId = teacher.Id,
                SubjectId = subject.Id,
                AcademicYearId = academicYear.Id,
                Name = "مجموعة العباقرة (السبت والثلاثاء)",
                ScheduleDescription = "السبت والثلاثاء 05:00 م - 07:00 م",
                MaxCapacity = 35,
                IsArchived = false,
                CreatedAtUtc = DateTime.UtcNow
            };
            await context.Groups.AddAsync(group1);
        }

        var group2 = await context.Groups.FirstOrDefaultAsync(g => g.TeacherId == teacher.Id && g.Name.Contains("مجموعة المتفوقين"));
        if (group2 == null)
        {
            group2 = new Group
            {
                Id = Guid.NewGuid(),
                TeacherId = teacher.Id,
                SubjectId = subject.Id,
                AcademicYearId = academicYear.Id,
                Name = "مجموعة المتفوقين (الأحد والأربعاء)",
                ScheduleDescription = "الأحد والأربعاء 06:00 م - 08:00 م",
                MaxCapacity = 30,
                IsArchived = false,
                CreatedAtUtc = DateTime.UtcNow
            };
            await context.Groups.AddAsync(group2);
        }
        await context.SaveChangesAsync();

        // 7. Students
        var studentDefs = new[]
        {
            new { Code = "HD-849201", Name = "أحمد علي محمود", Phone = "01098765432", Parent = "01234567890", MustChange = false },
            new { Code = "HD-392104", Name = "سارة إبراهيم حسن", Phone = "01123456789", Parent = "01023456789", MustChange = false },
            new { Code = "HD-581932", Name = "عمر طارق عبد الله", Phone = "01512345678", Parent = "01198765432", MustChange = false },
            new { Code = "HD-724185", Name = "مريم خالد يوسف", Phone = "01298765432", Parent = "01598765432", MustChange = false },
            new { Code = "HD-910243", Name = "يوسف مصطفى كمال", Phone = "01055556666", Parent = "01255556666", MustChange = true }
        };

        var studentList = new List<Student>();
        var defaultStudentHash = passwordService.HashPassword("StudentPass2026!");

        foreach (var def in studentDefs)
        {
            var student = await context.Students.FirstOrDefaultAsync(s => s.StudentCode == def.Code);
            if (student == null)
            {
                student = new Student
                {
                    Id = Guid.NewGuid(),
                    TeacherId = teacher.Id,
                    StudentCode = def.Code,
                    FullName = def.Name,
                    PhoneNumber = def.Phone,
                    ParentPhoneNumber = def.Parent,
                    CurrentGrade = "الصف الثالث الثانوي",
                    PasswordHash = defaultStudentHash,
                    TokenVersion = 1,
                    MustChangePassword = def.MustChange,
                    TemporaryPasswordExpiresAtUtc = def.MustChange ? DateTime.UtcNow.AddDays(7) : null,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                };
                await context.Students.AddAsync(student);
            }
            studentList.Add(student);
        }
        await context.SaveChangesAsync();

        // 8. Group Enrollments
        foreach (var s in studentList)
        {
            if (!await context.GroupEnrollments.AnyAsync(ge => ge.GroupId == group1.Id && ge.StudentId == s.Id && ge.IsActive))
            {
                await context.GroupEnrollments.AddAsync(new GroupEnrollment
                {
                    Id = Guid.NewGuid(),
                    TeacherId = teacher.Id,
                    GroupId = group1.Id,
                    StudentId = s.Id,
                    JoinedDateUtc = DateTime.UtcNow.AddDays(-30),
                    IsActive = true
                });
            }
        }

        // Student 2 also in Group 2
        if (!await context.GroupEnrollments.AnyAsync(ge => ge.GroupId == group2.Id && ge.StudentId == studentList[1].Id && ge.IsActive))
        {
            await context.GroupEnrollments.AddAsync(new GroupEnrollment
            {
                Id = Guid.NewGuid(),
                TeacherId = teacher.Id,
                GroupId = group2.Id,
                StudentId = studentList[1].Id,
                JoinedDateUtc = DateTime.UtcNow.AddDays(-20),
                IsActive = true
            });
        }
        await context.SaveChangesAsync();

        // 9. Past Closed Session (for Analytics & Reports)
        var closedSession = await context.Sessions.FirstOrDefaultAsync(s => s.GroupId == group1.Id && s.Title.Contains("التمهيدية"));
        if (closedSession == null)
        {
            closedSession = new Session
            {
                Id = Guid.NewGuid(),
                TeacherId = teacher.Id,
                GroupId = group1.Id,
                Title = "الحصة التمهيدية: الميكانيكا وقوانين الحركة",
                SessionDateUtc = DateTime.UtcNow.AddDays(-4),
                Status = "Closed",
                QrSecuritySalt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                CreatedAtUtc = DateTime.UtcNow.AddDays(-4)
            };
            await context.Sessions.AddAsync(closedSession);
            await context.SaveChangesAsync();

            // Records: 1 Present QR, 2 Present Manual, 3 Late, 4 Excused, 5 Absent
            await context.AttendanceRecords.AddRangeAsync(
                new AttendanceRecord
                {
                    Id = Guid.NewGuid(),
                    TeacherId = teacher.Id,
                    SessionId = closedSession.Id,
                    StudentId = studentList[0].Id,
                    Status = "Present",
                    Method = "QRCode",
                    RecordedAtUtc = closedSession.SessionDateUtc.AddMinutes(5)
                },
                new AttendanceRecord
                {
                    Id = Guid.NewGuid(),
                    TeacherId = teacher.Id,
                    SessionId = closedSession.Id,
                    StudentId = studentList[1].Id,
                    Status = "Present",
                    Method = "Manual",
                    RecordedAtUtc = closedSession.SessionDateUtc.AddMinutes(10)
                },
                new AttendanceRecord
                {
                    Id = Guid.NewGuid(),
                    TeacherId = teacher.Id,
                    SessionId = closedSession.Id,
                    StudentId = studentList[2].Id,
                    Status = "Late",
                    MinutesLate = 15,
                    Method = "Manual",
                    Notes = "تأخر بسبب المواصلات",
                    RecordedAtUtc = closedSession.SessionDateUtc.AddMinutes(15)
                },
                new AttendanceRecord
                {
                    Id = Guid.NewGuid(),
                    TeacherId = teacher.Id,
                    SessionId = closedSession.Id,
                    StudentId = studentList[3].Id,
                    Status = "Excused",
                    Method = "Manual",
                    Notes = "إذن غياب مسبق من ولي الأمر",
                    RecordedAtUtc = closedSession.SessionDateUtc
                },
                new AttendanceRecord
                {
                    Id = Guid.NewGuid(),
                    TeacherId = teacher.Id,
                    SessionId = closedSession.Id,
                    StudentId = studentList[4].Id,
                    Status = "Absent",
                    Method = "Manual",
                    Notes = "غياب بدون عذر",
                    RecordedAtUtc = closedSession.SessionDateUtc
                }
            );
            await context.SaveChangesAsync();
        }

        // 10. Active Live Session (Ready for QR Projection & Fast-Tap Marking)
        var activeSession = await context.Sessions.FirstOrDefaultAsync(s => s.GroupId == group1.Id && s.Status == "Active");
        if (activeSession == null)
        {
            activeSession = new Session
            {
                Id = Guid.NewGuid(),
                TeacherId = teacher.Id,
                GroupId = group1.Id,
                Title = "الحصة 1: مقدمة المتجهات وقوانين الحركة",
                SessionDateUtc = DateTime.UtcNow,
                Status = "Active",
                QrSecuritySalt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                CreatedAtUtc = DateTime.UtcNow
            };
            await context.Sessions.AddAsync(activeSession);
            await context.SaveChangesAsync();

            // Mark Student 1 & 2 as present, leave 3, 4, 5 for live test
            await context.AttendanceRecords.AddRangeAsync(
                new AttendanceRecord
                {
                    Id = Guid.NewGuid(),
                    TeacherId = teacher.Id,
                    SessionId = activeSession.Id,
                    StudentId = studentList[0].Id,
                    Status = "Present",
                    Method = "Manual",
                    RecordedAtUtc = DateTime.UtcNow.AddMinutes(-10)
                },
                new AttendanceRecord
                {
                    Id = Guid.NewGuid(),
                    TeacherId = teacher.Id,
                    SessionId = activeSession.Id,
                    StudentId = studentList[1].Id,
                    Status = "Present",
                    Method = "QRCode",
                    RecordedAtUtc = DateTime.UtcNow.AddMinutes(-5)
                }
            );
            await context.SaveChangesAsync();
        }

        logger.LogInformation("Hudoor Demo Data Seeding completed successfully!");
    }
}
