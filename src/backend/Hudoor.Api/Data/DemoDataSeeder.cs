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

        // 11. Teacher Diaa Nabil (أ. ضياء نبيل) - لغة عربية للثانوية الأزهرية
        var teacherDiaa = await context.Teachers.FirstOrDefaultAsync(t => t.Username == "diaa_nabil");
        if (teacherDiaa == null)
        {
            teacherDiaa = new Teacher
            {
                Id = Guid.NewGuid(),
                Username = "diaa_nabil",
                FullName = "أ. ضياء نبيل",
                Email = "diaa.nabil@hudoor.local",
                PhoneNumber = "01000000000",
                BrandName = "أ. ضياء نبيل - لغة عربية للثانوية الأزهرية",
                PasswordHash = passwordService.HashPassword("DiaaNabil2026!"),
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            await context.Teachers.AddAsync(teacherDiaa);
            await context.SaveChangesAsync();
            logger.LogInformation("Created Teacher: diaa_nabil / DiaaNabil2026!");
        }
        else
        {
            teacherDiaa.FullName = "أ. ضياء نبيل";
            teacherDiaa.BrandName = "أ. ضياء نبيل - لغة عربية للثانوية الأزهرية";
            teacherDiaa.PasswordHash = passwordService.HashPassword("DiaaNabil2026!");
            teacherDiaa.IsActive = true;
            await context.SaveChangesAsync();
        }

        // Academic Year for Diaa Nabil
        var diaaAcademicYear = await context.AcademicYears.FirstOrDefaultAsync(y => y.TeacherId == teacherDiaa.Id && y.Name.Contains("2025/2026"));
        if (diaaAcademicYear == null)
        {
            diaaAcademicYear = new AcademicYear
            {
                Id = Guid.NewGuid(),
                TeacherId = teacherDiaa.Id,
                Name = "العام الدراسي 2025/2026",
                StartDate = new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true,
                IsArchived = false,
                CreatedAtUtc = DateTime.UtcNow
            };
            await context.AcademicYears.AddAsync(diaaAcademicYear);
            await context.SaveChangesAsync();
        }
        else
        {
            diaaAcademicYear.Name = "العام الدراسي 2025/2026";
            diaaAcademicYear.IsActive = true;
            await context.SaveChangesAsync();
        }

        // Term for Diaa Nabil
        var diaaTerm = await context.Terms.FirstOrDefaultAsync(t => t.TeacherId == teacherDiaa.Id && t.Name.Contains("الثاني"));
        if (diaaTerm == null)
        {
            diaaTerm = new Term
            {
                Id = Guid.NewGuid(),
                TeacherId = teacherDiaa.Id,
                AcademicYearId = diaaAcademicYear.Id,
                Name = "الفصل الدراسي الثاني",
                StartDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            await context.Terms.AddAsync(diaaTerm);
            await context.SaveChangesAsync();
        }
        else
        {
            diaaTerm.Name = "الفصل الدراسي الثاني";
            diaaTerm.IsActive = true;
            await context.SaveChangesAsync();
        }

        // Clean any corrupted old subjects/groups for Diaa Nabil (e.g. from encoding glitches)
        var corruptedGroups = await context.Groups.Where(g => g.TeacherId == teacherDiaa.Id && g.Name.Contains("?")).ToListAsync();
        if (corruptedGroups.Count > 0)
        {
            context.Groups.RemoveRange(corruptedGroups);
            await context.SaveChangesAsync();
        }
        var corruptedSubjects = await context.Subjects.Where(s => s.TeacherId == teacherDiaa.Id && s.Name.Contains("?")).ToListAsync();
        if (corruptedSubjects.Count > 0)
        {
            context.Subjects.RemoveRange(corruptedSubjects);
            await context.SaveChangesAsync();
        }

        // Subjects for Diaa Nabil
        var sub1 = await context.Subjects.FirstOrDefaultAsync(s => s.TeacherId == teacherDiaa.Id && s.GradeLevel == "الصف الأول الثانوي");
        if (sub1 == null)
        {
            sub1 = new Subject
            {
                Id = Guid.NewGuid(),
                TeacherId = teacherDiaa.Id,
                Name = "اللغة العربية - الصف الأول الثانوي (أزهر)",
                GradeLevel = "الصف الأول الثانوي",
                IsArchived = false,
                CreatedAtUtc = DateTime.UtcNow
            };
            await context.Subjects.AddAsync(sub1);
        }
        else
        {
            sub1.Name = "اللغة العربية - الصف الأول الثانوي (أزهر)";
        }

        var sub2 = await context.Subjects.FirstOrDefaultAsync(s => s.TeacherId == teacherDiaa.Id && s.GradeLevel == "الصف الثاني الثانوي");
        if (sub2 == null)
        {
            sub2 = new Subject
            {
                Id = Guid.NewGuid(),
                TeacherId = teacherDiaa.Id,
                Name = "اللغة العربية - الصف الثاني الثانوي (أزهر)",
                GradeLevel = "الصف الثاني الثانوي",
                IsArchived = false,
                CreatedAtUtc = DateTime.UtcNow
            };
            await context.Subjects.AddAsync(sub2);
        }
        else
        {
            sub2.Name = "اللغة العربية - الصف الثاني الثانوي (أزهر)";
        }

        var sub3 = await context.Subjects.FirstOrDefaultAsync(s => s.TeacherId == teacherDiaa.Id && s.GradeLevel == "الصف الثالث الثانوي");
        if (sub3 == null)
        {
            sub3 = new Subject
            {
                Id = Guid.NewGuid(),
                TeacherId = teacherDiaa.Id,
                Name = "اللغة العربية - الصف الثالث الثانوي (أزهر)",
                GradeLevel = "الصف الثالث الثانوي",
                IsArchived = false,
                CreatedAtUtc = DateTime.UtcNow
            };
            await context.Subjects.AddAsync(sub3);
        }
        else
        {
            sub3.Name = "اللغة العربية - الصف الثالث الثانوي (أزهر)";
        }
        await context.SaveChangesAsync();

        // The 6 Groups (2 per Grade Level):
        var groupDefs = new[]
        {
            new { SubId = sub1.Id, Name = "أولى ثانوي - مجموعة (1)", Schedule = "السبت والثلاثاء 03:00 م - 05:00 م", Cap = 30 },
            new { SubId = sub1.Id, Name = "أولى ثانوي - مجموعة (2)", Schedule = "الأحد والأربعاء 03:00 م - 05:00 م", Cap = 30 },
            new { SubId = sub2.Id, Name = "تانية ثانوي - مجموعة (1)", Schedule = "السبت والثلاثاء 05:00 م - 07:00 م", Cap = 30 },
            new { SubId = sub2.Id, Name = "تانية ثانوي - مجموعة (2)", Schedule = "الأحد والأربعاء 05:00 م - 07:00 م", Cap = 30 },
            new { SubId = sub3.Id, Name = "تالتة ثانوي - مجموعة (1)", Schedule = "السبت والثلاثاء 07:00 م - 09:00 م", Cap = 35 },
            new { SubId = sub3.Id, Name = "تالتة ثانوي - مجموعة (2)", Schedule = "الأحد والأربعاء 07:00 م - 09:00 م", Cap = 35 }
        };

        foreach (var def in groupDefs)
        {
            var grp = await context.Groups.FirstOrDefaultAsync(g => g.TeacherId == teacherDiaa.Id && g.SubjectId == def.SubId && g.Name == def.Name);
            if (grp == null)
            {
                await context.Groups.AddAsync(new Group
                {
                    Id = Guid.NewGuid(),
                    TeacherId = teacherDiaa.Id,
                    SubjectId = def.SubId,
                    AcademicYearId = diaaAcademicYear.Id,
                    Name = def.Name,
                    ScheduleDescription = def.Schedule,
                    MaxCapacity = def.Cap,
                    IsArchived = false,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
            else
            {
                grp.ScheduleDescription = def.Schedule;
                grp.MaxCapacity = def.Cap;
            }
        }
        await context.SaveChangesAsync();

        // 12. Students for Teacher Diaa Nabil (48 realistic students without phone numbers)
        var diaaStudentsDefs = new[]
        {
            // الصف الأول الثانوي (أزهر) - مجموعة (1)
            new { Code = "HD-710001", Name = "عبد الرحمن أحمد الشريف", Grade = "الصف الأول الثانوي (أزهر)", GroupName = "أولى ثانوي - مجموعة (1)" },
            new { Code = "HD-710002", Name = "بلال محمود عبد الرحيم", Grade = "الصف الأول الثانوي (أزهر)", GroupName = "أولى ثانوي - مجموعة (1)" },
            new { Code = "HD-710003", Name = "يوسف إبراهيم الدسوقي", Grade = "الصف الأول الثانوي (أزهر)", GroupName = "أولى ثانوي - مجموعة (1)" },
            new { Code = "HD-710004", Name = "حمزة محمد الصاوي", Grade = "الصف الأول الثانوي (أزهر)", GroupName = "أولى ثانوي - مجموعة (1)" },
            new { Code = "HD-710005", Name = "سلمى حسام البدري", Grade = "الصف الأول الثانوي (أزهر)", GroupName = "أولى ثانوي - مجموعة (1)" },
            new { Code = "HD-710006", Name = "فاطمة حسن الألفي", Grade = "الصف الأول الثانوي (أزهر)", GroupName = "أولى ثانوي - مجموعة (1)" },
            new { Code = "HD-710007", Name = "آية مصطفى القاضي", Grade = "الصف الأول الثانوي (أزهر)", GroupName = "أولى ثانوي - مجموعة (1)" },
            new { Code = "HD-710008", Name = "مريم سامح الجوهري", Grade = "الصف الأول الثانوي (أزهر)", GroupName = "أولى ثانوي - مجموعة (1)" },

            // الصف الأول الثانوي (أزهر) - مجموعة (2)
            new { Code = "HD-710009", Name = "عمر خالد السعدني", Grade = "الصف الأول الثانوي (أزهر)", GroupName = "أولى ثانوي - مجموعة (2)" },
            new { Code = "HD-710010", Name = "أنس ياسر الشرقاوي", Grade = "الصف الأول الثانوي (أزهر)", GroupName = "أولى ثانوي - مجموعة (2)" },
            new { Code = "HD-710011", Name = "طارق مدحت البنهاوي", Grade = "الصف الأول الثانوي (أزهر)", GroupName = "أولى ثانوي - مجموعة (2)" },
            new { Code = "HD-710012", Name = "زياد عمرو الهواري", Grade = "الصف الأول الثانوي (أزهر)", GroupName = "أولى ثانوي - مجموعة (2)" },
            new { Code = "HD-710013", Name = "ملك عادل الباز", Grade = "الصف الأول الثانوي (أزهر)", GroupName = "أولى ثانوي - مجموعة (2)" },
            new { Code = "HD-710014", Name = "خديجة وليد النجار", Grade = "الصف الأول الثانوي (أزهر)", GroupName = "أولى ثانوي - مجموعة (2)" },
            new { Code = "HD-710015", Name = "جنى أحمد العوضي", Grade = "الصف الأول الثانوي (أزهر)", GroupName = "أولى ثانوي - مجموعة (2)" },
            new { Code = "HD-710016", Name = "هاجر سعيد غنيم", Grade = "الصف الأول الثانوي (أزهر)", GroupName = "أولى ثانوي - مجموعة (2)" },

            // الصف الثاني الثانوي (أزهر) - مجموعة (1)
            new { Code = "HD-710017", Name = "مصطفى حسام الدين", Grade = "الصف الثاني الثانوي (أزهر)", GroupName = "تانية ثانوي - مجموعة (1)" },
            new { Code = "HD-710018", Name = "إبراهيم كمال الشناوي", Grade = "الصف الثاني الثانوي (أزهر)", GroupName = "تانية ثانوي - مجموعة (1)" },
            new { Code = "HD-710019", Name = "معاذ أشرف البسيوني", Grade = "الصف الثاني الثانوي (أزهر)", GroupName = "تانية ثانوي - مجموعة (1)" },
            new { Code = "HD-710020", Name = "علي حازم الفيشاوي", Grade = "الصف الثاني الثانوي (أزهر)", GroupName = "تانية ثانوي - مجموعة (1)" },
            new { Code = "HD-710021", Name = "نور الدين تامر عفيفي", Grade = "الصف الثاني الثانوي (أزهر)", GroupName = "تانية ثانوي - مجموعة (1)" },
            new { Code = "HD-710022", Name = "رضوى أسامة زايد", Grade = "الصف الثاني الثانوي (أزهر)", GroupName = "تانية ثانوي - مجموعة (1)" },
            new { Code = "HD-710023", Name = "ياسمين هشام بركات", Grade = "الصف الثاني الثانوي (أزهر)", GroupName = "تانية ثانوي - مجموعة (1)" },
            new { Code = "HD-710024", Name = "هدى شريف العدوي", Grade = "الصف الثاني الثانوي (أزهر)", GroupName = "تانية ثانوي - مجموعة (1)" },

            // الصف الثاني الثانوي (أزهر) - مجموعة (2)
            new { Code = "HD-710025", Name = "أحمد ماجد الدمياطي", Grade = "الصف الثاني الثانوي (أزهر)", GroupName = "تانية ثانوي - مجموعة (2)" },
            new { Code = "HD-710026", Name = "كريم عماد المنشاوي", Grade = "الصف الثاني الثانوي (أزهر)", GroupName = "تانية ثانوي - مجموعة (2)" },
            new { Code = "HD-710027", Name = "سيف الدين رامي زهران", Grade = "الصف الثاني الثانوي (أزهر)", GroupName = "تانية ثانوي - مجموعة (2)" },
            new { Code = "HD-710028", Name = "عبد الله علاء النحاس", Grade = "الصف الثاني الثانوي (أزهر)", GroupName = "تانية ثانوي - مجموعة (2)" },
            new { Code = "HD-710029", Name = "نورهان عصام غالي", Grade = "الصف الثاني الثانوي (أزهر)", GroupName = "تانية ثانوي - مجموعة (2)" },
            new { Code = "HD-710030", Name = "منة الله طه شلبي", Grade = "الصف الثاني الثانوي (أزهر)", GroupName = "تانية ثانوي - مجموعة (2)" },
            new { Code = "HD-710031", Name = "ريم أيمن الخواجة", Grade = "الصف الثاني الثانوي (أزهر)", GroupName = "تانية ثانوي - مجموعة (2)" },
            new { Code = "HD-710032", Name = "سارة بهاء عزمي", Grade = "الصف الثاني الثانوي (أزهر)", GroupName = "تانية ثانوي - مجموعة (2)" },

            // الصف الثالث الثانوي (أزهر) - مجموعة (1)
            new { Code = "HD-710033", Name = "حسن وائل البحيري", Grade = "الصف الثالث الثانوي (أزهر)", GroupName = "تالتة ثانوي - مجموعة (1)" },
            new { Code = "HD-710034", Name = "محمود أشرف سويلم", Grade = "الصف الثالث الثانوي (أزهر)", GroupName = "تالتة ثانوي - مجموعة (1)" },
            new { Code = "HD-710035", Name = "يحيى هاني متولي", Grade = "الصف الثالث الثانوي (أزهر)", GroupName = "تالتة ثانوي - مجموعة (1)" },
            new { Code = "HD-710036", Name = "عبد العزيز عاطف زكي", Grade = "الصف الثالث الثانوي (أزهر)", GroupName = "تالتة ثانوي - مجموعة (1)" },
            new { Code = "HD-710037", Name = "أميرة حسام الفقي", Grade = "الصف الثالث الثانوي (أزهر)", GroupName = "تالتة ثانوي - مجموعة (1)" },
            new { Code = "HD-710038", Name = "تسنيم هاني عبد العليم", Grade = "الصف الثالث الثانوي (أزهر)", GroupName = "تالتة ثانوي - مجموعة (1)" },
            new { Code = "HD-710039", Name = "أروى أشرف قنديل", Grade = "الصف الثالث الثانوي (أزهر)", GroupName = "تالتة ثانوي - مجموعة (1)" },
            new { Code = "HD-710040", Name = "رنا وليد رضوان", Grade = "الصف الثالث الثانوي (أزهر)", GroupName = "تالتة ثانوي - مجموعة (1)" },

            // الصف الثالث الثانوي (أزهر) - مجموعة (2)
            new { Code = "HD-710041", Name = "مازن أسامة البغدادي", Grade = "الصف الثالث الثانوي (أزهر)", GroupName = "تالتة ثانوي - مجموعة (2)" },
            new { Code = "HD-710042", Name = "عمار ياسر السنباطي", Grade = "الصف الثالث الثانوي (أزهر)", GroupName = "تالتة ثانوي - مجموعة (2)" },
            new { Code = "HD-710043", Name = "فارس سامي بدران", Grade = "الصف الثالث الثانوي (أزهر)", GroupName = "تالتة ثانوي - مجموعة (2)" },
            new { Code = "HD-710044", Name = "حازم ناصر الجندي", Grade = "الصف الثالث الثانوي (أزهر)", GroupName = "تالتة ثانوي - مجموعة (2)" },
            new { Code = "HD-710045", Name = "شهد تامر العيسوي", Grade = "الصف الثالث الثانوي (أزهر)", GroupName = "تالتة ثانوي - مجموعة (2)" },
            new { Code = "HD-710046", Name = "رحمة إيهاب سليمان", Grade = "الصف الثالث الثانوي (أزهر)", GroupName = "تالتة ثانوي - مجموعة (2)" },
            new { Code = "HD-710047", Name = "حبيبة مصطفى خليل", Grade = "الصف الثالث الثانوي (أزهر)", GroupName = "تالتة ثانوي - مجموعة (2)" },
            new { Code = "HD-710048", Name = "ندى طارق الصيرفي", Grade = "الصف الثالث الثانوي (أزهر)", GroupName = "تالتة ثانوي - مجموعة (2)" }
        };

        var defaultDiaaStudentHash = passwordService.HashPassword("DiaaNabil2026!");
        foreach (var sDef in diaaStudentsDefs)
        {
            var student = await context.Students.FirstOrDefaultAsync(s => s.TeacherId == teacherDiaa.Id && s.StudentCode == sDef.Code);
            if (student == null)
            {
                student = new Student
                {
                    Id = Guid.NewGuid(),
                    TeacherId = teacherDiaa.Id,
                    StudentCode = sDef.Code,
                    FullName = sDef.Name,
                    PhoneNumber = null,
                    ParentPhoneNumber = null,
                    CurrentGrade = sDef.Grade,
                    PasswordHash = defaultDiaaStudentHash,
                    TokenVersion = 1,
                    MustChangePassword = false,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                };
                await context.Students.AddAsync(student);
            }
            else
            {
                student.FullName = sDef.Name;
                student.CurrentGrade = sDef.Grade;
                student.PhoneNumber = null;
                student.ParentPhoneNumber = null;
            }

            var targetGroup = await context.Groups.FirstOrDefaultAsync(g => g.TeacherId == teacherDiaa.Id && g.Name == sDef.GroupName);
            if (targetGroup != null)
            {
                var isEnrolled = await context.GroupEnrollments.AnyAsync(ge => ge.GroupId == targetGroup.Id && ge.StudentId == student.Id && ge.IsActive);
                if (!isEnrolled)
                {
                    await context.GroupEnrollments.AddAsync(new GroupEnrollment
                    {
                        Id = Guid.NewGuid(),
                        TeacherId = teacherDiaa.Id,
                        GroupId = targetGroup.Id,
                        StudentId = student.Id,
                        JoinedDateUtc = DateTime.UtcNow.AddDays(-15),
                        IsActive = true
                    });
                }
            }
        }
        await context.SaveChangesAsync();

        // 13. Historical 2-week Attendance Sessions for Diaa Nabil (Overall Attendance Rate = 78%)
        var existingDiaaSessions = await context.Sessions
            .Where(s => s.TeacherId == teacherDiaa.Id)
            .ToListAsync();

        if (existingDiaaSessions.Any())
        {
            var sIds = existingDiaaSessions.Select(s => s.Id).ToList();
            var existingRecords = await context.AttendanceRecords
                .Where(r => sIds.Contains(r.SessionId))
                .ToListAsync();
            context.AttendanceRecords.RemoveRange(existingRecords);
            context.Sessions.RemoveRange(existingDiaaSessions);
            await context.SaveChangesAsync();
        }

        var groupsList = await context.Groups
            .Include(g => g.Enrollments.Where(e => e.IsActive))
            .Where(g => g.TeacherId == teacherDiaa.Id && !g.IsArchived)
            .OrderBy(g => g.Name)
            .ToListAsync();

        var sessionTitles = new[]
        {
            "حصة (1): مدخل البلاغة وعلم البيان",
            "حصة (2): النحو - إعراب الفعل وبناء الأسماء",
            "حصة (3): شواهد النصوص وتطبيقات القرآن الكريم",
            "حصة (4): مراجعة دورية وتطبيقات شاملة"
        };

        var allNewRecords = new List<AttendanceRecord>();
        var baseDate = DateTime.UtcNow;

        // Group index targets for Present+Late count out of total records:
        // Group 0: 34 records -> 26 P+L (8 A/E) -> 76.5%
        // Group 1: 34 records -> 27 P+L (7 A/E) -> 79.4%
        // Group 2: 32 records -> 25 P+L (7 A/E) -> 78.1%
        // Group 3: 34 records -> 26 P+L (8 A/E) -> 76.5%
        // Group 4: 32 records -> 25 P+L (7 A/E) -> 78.1%
        // Group 5: 34 records -> 27 P+L (7 A/E) -> 79.4%
        // Total P+L = 156, Total records = 200 -> EXACTLY 78.0%!
        int[] targetPresents = [26, 27, 25, 26, 25, 27];

        for (int gIdx = 0; gIdx < groupsList.Count; gIdx++)
        {
            var grp = groupsList[gIdx];
            var enrolled = grp.Enrollments.Select(e => e.StudentId).ToList();
            bool isSatTue = grp.ScheduleDescription.Contains("السبت");
            int[] dayOffsets = isSatTue ? [12, 9, 5, 2] : [11, 8, 4, 1];

            var groupSessions = new List<Session>();
            for (int sIdx = 0; sIdx < 4; sIdx++)
            {
                var session = new Session
                {
                    Id = Guid.NewGuid(),
                    TeacherId = teacherDiaa.Id,
                    GroupId = grp.Id,
                    Title = sessionTitles[sIdx],
                    SessionDateUtc = baseDate.AddDays(-dayOffsets[sIdx]),
                    Status = "Closed",
                    QrSecuritySalt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                    CreatedAtUtc = baseDate.AddDays(-dayOffsets[sIdx])
                };
                groupSessions.Add(session);
            }
            await context.Sessions.AddRangeAsync(groupSessions);
            await context.SaveChangesAsync();

            var pairs = new List<(Guid StudentId, Session Session)>();
            for (int sIdx = 0; sIdx < groupSessions.Count; sIdx++)
            {
                var s = groupSessions[sIdx];
                int countToTake = (enrolled.Count > 8 && sIdx == 0) ? 7 : enrolled.Count;
                for (int stIdx = 0; stIdx < countToTake; stIdx++)
                {
                    pairs.Add((enrolled[stIdx], s));
                }
            }

            int groupTargetPresents = targetPresents[gIdx];
            int pAssigned = 0;

            for (int pIdx = 0; pIdx < pairs.Count; pIdx++)
            {
                var pair = pairs[pIdx];
                bool isAtRiskStudent = pair.StudentId == enrolled[0];
                string status;
                string method = (pIdx % 3 == 0) ? "Manual" : "QRCode";
                int? lateMinutes = null;
                string? note = null;

                if (isAtRiskStudent && (pIdx % 4 != 0))
                {
                    status = "Absent";
                }
                else if (pAssigned < groupTargetPresents)
                {
                    if (pAssigned % 20 == 0)
                    {
                        status = "Late";
                        lateMinutes = 15;
                    }
                    else
                    {
                        status = "Present";
                    }
                    pAssigned++;
                }
                else
                {
                    if (allNewRecords.Count(r => r.Status == "Excused") < 10 && pIdx % 2 == 0)
                    {
                        status = "Excused";
                        note = "إذن غياب مسبق من ولي الأمر";
                    }
                    else
                    {
                        status = "Absent";
                    }
                }

                allNewRecords.Add(new AttendanceRecord
                {
                    Id = Guid.NewGuid(),
                    TeacherId = teacherDiaa.Id,
                    SessionId = pair.Session.Id,
                    StudentId = pair.StudentId,
                    Status = status,
                    Method = method,
                    RecordedAtUtc = pair.Session.SessionDateUtc.AddMinutes(5),
                    MinutesLate = lateMinutes,
                    Notes = note
                });
            }
        }

        await context.AttendanceRecords.AddRangeAsync(allNewRecords);
        await context.SaveChangesAsync();

        logger.LogInformation("Hudoor Demo Data Seeding completed successfully (including Diaa Nabil students and 2-week attendance at 78%)!");
    }
}
