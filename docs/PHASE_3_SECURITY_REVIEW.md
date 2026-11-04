# تقرير المراجعة الأمنية والمعمارية — المرحلة الثالثة (Phase 3 Security & Architecture Review)
**مشروع SaaS لإدارة الحضور (Hudoor SaaS)**  
**تاريخ المراجعة:** 3 نوفمبر 2026  
**النطاق:** المصادقة، الصلاحيات، عزل المستأجرين، إلغاء الجلسات اللحظي، وإدارة كلمات المرور.

---

## 1. نظرة عامة والملخص التنفيذي (Executive Summary)

تم إجراء مراجعة أمنية ومعمارية دقيقة وشاملة للمرحلة الثالثة (Phase 3) الخاصة بأساسيات الأمان والمصادقة في مشروع Hudoor. لم يتم تعديل الكود أو تغيير المعمارية أو إعادة إنشاء قاعدة البيانات أثناء هذه المراجعة؛ والهدف هو فحص الكود والاختبارات سطرًا بسطر وتقديم تقييم موضوعي مدعوم بالأدلة الصريحة.

### لوحة النتائج (Verdict Dashboard)

| المحور | الموضوع المفحوص | النتيجة | الملاحظة الجوهرية |
| :--- | :--- | :---: | :--- |
| **1** | **Authentication & JWT** | **PASS** | تشفير PBKDF2 قوي، توقيع HMAC-SHA256 (256-bit)، والتحقق الصارم من الصلاحيات وامتناع تسريب كلمات المرور. |
| **2** | **Student TokenVersion** | **PASS** | تحقق مباشر ولحظي من SQL Server لكل طلب طالب محمي دون أي كاش، وذرية كاملة لعمليات الإلغاء. |
| **3** | **Tenant Isolation & Schema** | **WARNING** | العزل محقق برمجيًا في Phase 3، ولكن العلاقات في الـ Schema تفتقر لـ Composite Foreign Keys لمنع الربط العرضي مستقبلاً. |
| **4** | **PlatformAdmin Bootstrap** | **WARNING** | آلية التأسيس لا تستبدل الأدمن الموجود، ولكن يوجد Fallback لكلمات مرور افتراضية صريحة في الكود والإعدادات. |
| **5** | **Rate Limiting** | **PASS** (مع قيود) | الحظر مؤقت (15 دقيقة) وحد الـ IP مراعٍ لبيئة الفصول، مع قيود مرتبطة بالتخزين في الذاكرة (In-Memory). |
| **6** | **Database & Tests** | **PASS** (مع توثيق) | تم إثبات أن الاختبارات تعمل على EF Core InMemory فقط ولا تختبر قيود SQL Server الفيزيائية. |
| **7** | **Dependency Security** | **PASS** | تمت ترقية حزمة `System.Security.Cryptography.Pkcs` إلى 10.0.14، وصفر ثغرات عبر الحل بالكامل. |

---

## 2. الملفات التي تمت مراجعتها (Reviewed Files)

1. `src/backend/Hudoor.Api/Services/PasswordService.cs`
2. `src/backend/Hudoor.Api/Services/JwtTokenService.cs`
3. `src/backend/Hudoor.Api/Services/RateLimiterService.cs`
4. `src/backend/Hudoor.Api/Services/AdminBootstrapService.cs`
5. `src/backend/Hudoor.Api/Security/StudentTokenValidationMiddleware.cs`
6. `src/backend/Hudoor.Api/Common/TenantContext.cs`
7. `src/backend/Hudoor.Api/Controllers/AuthController.cs`
8. `src/backend/Hudoor.Api/Controllers/PlatformAdminController.cs`
9. `src/backend/Hudoor.Api/Controllers/TeacherController.cs`
10. `src/backend/Hudoor.Api/Controllers/StudentController.cs`
11. `src/backend/Hudoor.Api/Program.cs`
12. `src/backend/Hudoor.Api/appsettings.json` و `appsettings.Development.json`
13. `src/backend/Hudoor.Api/Data/Entities/*.cs` (جميع الكيانات الـ 13)
14. `src/backend/Hudoor.Api/Data/Configurations/*.cs` (جميع ملفات تكوين EF Core)
15. `tests/Hudoor.Api.Tests/Infrastructure/CustomWebApplicationFactory.cs`
16. `tests/Hudoor.Api.Tests/Unit/PasswordServiceTests.cs`
17. `tests/Hudoor.Api.Tests/Integration/*.cs` (جميع ملفات الاختبارات التكاملية)

---

## 3. التدقيق التفصيلي حسب المحاور المطلوبة

### المحور الأول: Authentication & JWT
**النتيجة: PASS**

1. **فحص `PasswordService.cs`**:
   - يستخدم `Microsoft.AspNetCore.Identity.PasswordHasher<object>` الافتراضي لنظام ASP.NET Core، والذي يعتمد معيار PBKDF2 مع HMAC-SHA512 وتوليد Salt عشوائي لكل مستخدم بطول 128-bit و 100,000 تكرار تشفيري.
   - تم التحقق من اختبارات الملح (`HashPassword_ShouldProduceUniqueSaltsForSameInput`): إدخال نفس كلمة المرور ينتج عنه هاش مختلف تمامًا في كل مرة، والتحقق ينجح بدقة.
   - توليد كلمات المرور المؤقتة للطلاب:
     - يستخدم `RandomNumberGenerator.GetInt32` (CSPRNG آمن تشفيريًا).
     - يعتمد الصيغة المطلوبة: كلمة حيادية من قاموس محدد (20 كلمة مثل `Falcon`, `Cedar`, `Swift`) + 3 أرقام.
     - الأرقام مستخرجة حصرًا من المصفوفة `['2', '3', '4', '5', '6', '7', '8', '9']`، مما يضمن خلو الأرقام بنسبة 100% من الأرقام الملتبسة (`0` و `1`).
     - التحقق من سياسة كلمات المرور: الحد الأدنى للطالب 6 محارف تشمل حروفًا وأرقامًا، وللموظفين (Teacher/Admin) 8 محارف تشمل حروفًا وأرقامًا.

2. **فحص `JwtTokenService.cs` و `Program.cs`**:
   - **مفتاح التوقيع (Signing Key)**: يتم التحقق برمجيًا من أن طول المفتاح لا يقل عن 256 بت (32 بايت) وإلا يفشل التطبيق فورًا:
     ```csharp
     if (Encoding.UTF8.GetByteCount(keyString) < 32)
         throw new InvalidOperationException("Jwt:Key must be at least 256 bits (32 bytes) long.");
     ```
   - **الخوارزمية المستخدمة**: `SecurityAlgorithms.HmacSha256`.
   - **التحقق من التوقيع والـ Claims**:
     - `ValidateIssuerSigningKey = true`
     - `ValidateIssuer = true` (مع `ValidIssuer = "Hudoor.Api"`)
     - `ValidateAudience = true` (مع `ValidAudience = "Hudoor.Client"`)
     - `ValidateLifetime = true`
     - `ClockSkew = TimeSpan.FromSeconds(30)` (محدد بدقة 30 ثانية بدلاً من الافتراضي المتساهل 5 دقائق).
   - **Role Claims و Authorization Policies**:
     - يتم تضمين الدور عبر `ClaimTypes.Role` بالقيم المعيارية الثابتة (`PlatformAdmin`, `Teacher`, `Student`).
     - تم ربط الـ Controllers بسمات الحماية الصريحة:
       - `[Authorize(Roles = Roles.PlatformAdmin)]` على `PlatformAdminController`
       - `[Authorize(Roles = Roles.Teacher)]` على `TeacherController`
       - `[Authorize(Roles = Roles.Student)]` على `StudentController`
     - أثبتت الاختبارات التكاملية نجاح منع الدخول غير المصرح به وإرجاع `401 Unauthorized` للمستخدم غير المسجل و `403 Forbidden` للدور غير المسموح له.

3. **حماية سرية كلمات المرور وعدم تسريبها في الـ Logs والـ Responses**:
   - تم فحص كود الـ DTOs والـ Controllers والـ Logging:
     - كائن `TeacherResponseDto` لا يحتوي على خاصية `PasswordHash` مطلقًا.
     - مسار `GET /api/auth/me` يُرجع `CurrentUserResponse` الخالي تمامًا من الهاش.
     - استجابة إنشاء المدرس واستجابة تسجيل الدخول لا تعيد أي قيمة للهاش.
     - استجابة `POST /api/teacher/students/{id}/reset-password` تعيد فقط كلمة المرور المؤقتة الجديدة المخصصة للعرض لمرة واحدة للشارح/المدرس، مع تخزين الهاش مباشرة في قاعدة البيانات.
     - سجلات الـ Logging في `AdminBootstrapService` تسجل فقط اسم المستخدم (`Username`) ولا تطبع كلمة المرور.

---

### المحور الثاني: Student TokenVersion & Session Revocation
**النتيجة: PASS**

1. **موقع الـ Middleware في Pipeline**:
   - تم فحص `Program.cs` (الأسطر 74-76):
     ```csharp
     app.UseAuthentication();
     app.UseMiddleware<StudentTokenValidationMiddleware>();
     app.UseAuthorization();
     ```
   - الـ Middleware يقع مباشرة بعد التحقق من صحة التوكن التشفيرية (`UseAuthentication`) وقبل فلترة الصلاحيات (`UseAuthorization`). هذا يضمن أن هوية المستخدم (`context.User`) متاحة ومحققة، ولكن لم يُسمح له بعد بالوصول للـ Endpoints.

2. **إثبات التحقق المباشر من SQL Server لكل Request محمي**:
   - داخل `StudentTokenValidationMiddleware.cs`:
     ```csharp
     var studentState = await dbContext.Students
         .AsNoTracking()
         .Where(s => s.Id == studentId)
         .Select(s => new
         {
             s.TokenVersion,
             s.IsActive,
             TeacherIsActive = s.Teacher.IsActive
         })
         .FirstOrDefaultAsync(context.RequestAborted);
     ```
   - **الدليل على انعدام الكاش**: الاستعلام يُنفذ مباشرة عبر الـ DbContext بواسطة `AsNoTracking()` بالاعتماد على الفهرس الرئيسي (Primary Key Index على `Student.Id`).
   - لا يوجد Redis، ولا يوجد `IMemoryCache`، ولا يوجد تخزين مؤقت للـ TokenVersion.
   - إذا تم تغيير كلمة مرور الطالب أو قام المدرس بعمل Reset، تزداد القيمة في الجدول مباشرة، فيفشل الـ Claim `tv` فورًا في الطلب التالي مباشرة ويرجع `401 Unauthorized` (`SESSION_REVOKED`).

3. **ذرية العمليات (Atomicity)**:
   - في `TeacherController.ResetStudentPassword`:
     يتم تعديل `PasswordHash`، و`MustChangePassword = true`، و`TemporaryPasswordExpiresAtUtc`، وزيادة `TokenVersion++`، وإضافة `PasswordResetAuditLog`، ثم حفظها جميعًا عبر:
     ```csharp
     await _context.SaveChangesAsync(HttpContext.RequestAborted);
     ```
   - في `StudentController.ChangePassword`:
     يتم تحديث الهاش وتصفير الـ MustChangePassword وزيادة `TokenVersion++` وحفظها في نداء واحد لـ `SaveChangesAsync`.
   - يقوم EF Core تلقائيًا بتغليف `SaveChangesAsync` داخل Database Transaction واحدة؛ فإذا فشل أي قيد، تتراجع العملية بالكامل.

4. **اختبار إبطال التوكن القديم**:
   - أثبت الاختبار `StudentSession_IsInstantlyRevoked_WhenTeacherResetsPassword` والاختبار `StudentSession_EnforcesPasswordChange_AndInvalidatesPriorTokenOnChange` أن الطلب التالي مباشرة بالتوكن السابق يُرفض فورًا بـ `401 Unauthorized` ورمز الخطأ `SESSION_REVOKED`.

5. **ملاحظة فنية معمارية (Minor Edge Case)**:
   - فحص السطر 66 في `StudentTokenValidationMiddleware.cs`:
     `var isAllowed = path.EndsWith("/api/student/change-password") || ...`
   - إذا أرسل العميل الرابط مع Trailing Slash (مثل `/api/student/change-password/`)، فإن `EndsWith` ستفشل ويتم منعه بـ `403`. يُوصى باستخدام `path.TrimEnd('/')`.

---

### المحور الثالث: Tenant Isolation & Database Relational Boundaries
**النتيجة: WARNING (تحذير معماري هام للمراحل القادمة)**

تم فحص جميع الجداول والكيانات الـ 13 وعلاقاتها في EF Core لتحديد هل يمكن لمدرس ربط بياناته ببيانات مدرس آخر:

1. **العلاقات المباشرة المحمية في Phase 3**:
   - `Teacher` و `Student`: معزول تمامًا بحقل `Student.TeacherId`.
   - مسار إعادة تعيين كلمة المرور يتحقق من:
     ```csharp
     _context.Students.FirstOrDefaultAsync(s => s.Id == id && s.TeacherId == teacherId.Value)
     ```
     إذا كان الطالب تابعًا لمدرس آخر، يُرجع `404 Not Found` حمايةً للخصوصية ومنعًا لاكتشاف وجود الطلاب. تم إثبات ذلك باختبار `ResetStudentPassword_ByDifferentTeacher_FailsWith404NotFound`.

2. **تحليل العلاقات العرضية المعرضة لمخاطر العزل في المراحل القادمة (Critical Architecture Finding)**:
   عند فحص هيكل قاعدة البيانات المصمم في المرحلة 2، تم اكتشاف ما يلي:
   
   - **العلاقة بين `AcademicYear` و `Term`**:
     - `Term` يحتوي على `TeacherId` و `AcademicYearId`.
     - القيد على مستوى قاعدة البيانات: `FK_Terms_AcademicYears_AcademicYearId` يربط فقط بـ `AcademicYears.Id`.
     - **الثغرة المحتملة مستقبلاً**: لا يوجد Composite Foreign Key على `(AcademicYearId, TeacherId)`. إذا قام مطور في Phase 4 بإنشاء `Term` وأخذ `AcademicYearId` من الطلب دون التحقق من أن العام الأكاديمي يملكه نفس المدرس، يمكن لـ Teacher A إنشاء Term يتبع AcademicYear الخاص بـ Teacher B!
   
   - **العلاقة بين `Subject` و `Group` و `AcademicYear`**:
     - `Group` يحتوي على `TeacherId` و `SubjectId` و `AcademicYearId`.
     - القيود: `FK_Groups_Subjects_SubjectId` و `FK_Groups_AcademicYears_AcademicYearId`.
     - **الثغرة المحتملة مستقبلاً**: قاعدة البيانات تسمح لمجموعة تابعة للمدرس A بأن ترتبط بـ Subject يملكه المدرس B، طالما أن الـ ID موجود!
   
   - **العلاقة في `GroupEnrollment`**:
     - يحتوي على `TeacherId` و `GroupId` و `StudentId`.
     - **الثغرة المحتملة مستقبلاً**: ما لم تتحقق طبقة الـ API في Phase 4 من أن `Group.TeacherId == Student.TeacherId == CurrentTeacherId`، فإن قاعدة البيانات تقبل تسجيل طالب من المدرس B في مجموعة المدرس A!
   
   - **العلاقة في `Session` و `AttendanceRecord`**:
     - يحتوي `AttendanceRecord` على `TeacherId` و `SessionId` و `StudentId`.
     - **الثغرة المحتملة مستقبلاً**: لا يوجد قيد في SQL Server يمنع إدراج سجل حضور لجلسة يملكها المدرس A مع طالب يملكه المدرس B.

> [!WARNING]
> **خلاصة تدقيق العزل**:
> إن العلاقات على مستوى الـ Foreign Keys صالحة رياضيًا وعلائقيًا، ولكنها **غير كافية بمفردها لضمان Tenant Ownership**. الحماية الحالية في المرحلة 3 تعتمد بالكامل على التحقق الصارم في الـ Application Layer عبر `ITenantContext`. يجب توثيق قاعدة صارمة لجميع الـ Controllers في Phase 4 وما بعدها تقضي بالتحقق من ملكية الكيانات المرتبطة قبل الحفظ، أو تعزيز الـ Schema بمفاتيح أجنبية مركبة `Composite Foreign Keys` تشمل `TeacherId`.

---

### المحور الرابع: PlatformAdmin Bootstrap
**النتيجة: WARNING (تحذير أمني)**

1. **التحقق من عدم استبدال حساب أدمن موجود**:
   - **PASS**: الأسطر 36-39 في `AdminBootstrapService.cs`:
     ```csharp
     if (await context.PlatformAdmins.AnyAsync(cancellationToken))
     {
         return;
     }
     ```
     إذا كان هناك أي أدمن مسجل في قاعدة البيانات، تتوقف الخدمة فورًا ولا يتم تعديل أو استبدال أي بيانات.

2. **التحقق من بيانات الاعتماد الافتراضية (Default Credentials)**:
   - **FAIL / WARNING أمني**:
     في `AdminBootstrapService.cs` (الأسطر 41-48):
     ```csharp
     var username = _configuration["Bootstrap:AdminUsername"]
         ?? Environment.GetEnvironmentVariable("HUDOOR_ADMIN_BOOTSTRAP_USERNAME")
         ?? "admin";

     var password = _configuration["Bootstrap:AdminPassword"]
         ?? Environment.GetEnvironmentVariable("HUDOOR_ADMIN_BOOTSTRAP_PASSWORD")
         ?? "AdminSecure2026!";
     ```
     وفي `appsettings.json`:
     ```json
     "Bootstrap": {
       "AdminUsername": "admin",
       "AdminPassword": "AdminSecure2026!",
       "AdminEmail": "admin@hudoor.local",
       "AdminFullName": "Platform Administrator"
     }
     ```
   - **المخاطرة الأمنية**:
     وجود قيم افتراضية ثابتة (`AdminSecure2026!`) مضمنة في الكود وملف الإعدادات يُعد مخالفة أمنية صريحة (CWE-1392: Use of Default Credentials). إذا تم نشر النظام دون تكوين متغيرات بيئة، سينشأ حساب PlatformAdmin بكلمة مرور معروفة للعامة!
   - **السلوك عند غياب بيانات الاعتماد**:
     بدلاً من تخطي التأسيس أو إيقاف التطبيق، يرجع الكود تلقائيًا إلى كلمة المرور الافتراضية.
   - **طباعة البيانات في السجلات**:
     **PASS**: الكود يطبع فقط اسم المستخدم `Username` ولا يطبع كلمة المرور إطلاقًا.

---

### المحور الخامس: Rate Limiting & Anti-Brute-Force
**النتيجة: PASS (مع قيود تشغيلية موثقة)**

1. **فحص حظر الحسابات (Account Lockout)**:
   - المفتاح: `admin:{username}`، `teacher:{username}`، `student:{studentcode}`.
   - الشرط: 5 محاولات فاشلة متتالية تفرض حظرًا لمدة 15 دقيقة (`AccountLockoutDuration = TimeSpan.FromMinutes(15)`).
   - بعد انقضاء الـ 15 دقيقة، يتم تصفير العداد والرفع التلقائي للحظر (`state.LockedUntilUtc = null`). الحظر **مؤقت حتمًا ولا يؤدي إلى قفل دائم للحساب**.
   - **مخاطرة الـ Denial-of-Service**: نظرًا لأن الحظر يستند إلى كود الطالب، فإن أي شخص يعرف كود طالب يستطيع إرسال 5 محاولات خاطئة كل 15 دقيقة لحرمان الطالب من تسجيل الدخول. هذا السلوك موثق في `SECURITY_REQUIREMENTS.md` ومقبول في الـ MVP كحماية ضد كسر كلمات المرور، ولكن يجب تعزيزه في مراحل لاحقة بحلول إضافية (مثل CAPTCHA).

2. **فحص حظر الـ IP لبيئة الفصول الدراسية (Classroom-Aware IP Limiting)**:
   - الحد الأقصى: 100 طلب في الدقيقة لكل عنوان IP (`MaxIpRequestsPerMinute = 100`, `IpWindowDuration = 1 Minute`).
   - هذا السقف يتسع لدفعة من 50 إلى 80 طالبًا يسجلون دخولهم في نفس اللحظة عبر راوتر الفصل دون التعرض لحظر جماعي لشبكة المدرسة.

3. **التنفيذ الفعلي على Endpoints تسجيل الدخول**:
   - تم فحص `AuthController.cs`:
     يتم استدعاء `_rateLimiter.IsIpRateLimited` و `_rateLimiter.IsAccountLockedOut` و `_rateLimiter.ApplyProgressiveDelayAsync` في جميع مسارات تسجيل الدخول الثلاثة (`platform-admin/login`, `teacher/login`, `student/login`).
   - رسائل الخطأ عامة تمامًا: `"Invalid credentials."` ولا توضح هل الحساب موجود أم غير موجود.

4. **القيد المعماري الحالي**:
   - التخزين في الذاكرة عبر `ConcurrentDictionary`: في حال تشغيل التطبيق في بيئة ذات خوادم متعددة (Load-Balanced / Multi-Instance)، لن تتم مشاركة عدادات الحظر بين النسخ ما لم يتم نقلها إلى مخزن موزع (مثل Redis في مراحل ما بعد الـ MVP).

---

### المحور السادس: Database & Integration Test Coverage
**النتيجة: PASS (مع الشفافية الكاملة حول حدود الاختبارات)**

1. **نوع قاعدة البيانات المستخدمة في Integration Tests**:
   - **تم التأكد بنسبة 100%**: الاختبارات الحالية في `tests/Hudoor.Api.Tests` تستخدم حزمة **`Microsoft.EntityFrameworkCore.InMemory`** حصريًا، عبر التهيئة في `CustomWebApplicationFactory.cs`:
     ```csharp
     services.AddDbContext<ApplicationDbContext>(options =>
     {
         options.UseInMemoryDatabase(_dbName);
     });
     ```
   - **الاختبارات الحالية لا تتحقق من SQL Server الحقيقي**؛ بل تعمل داخل الذاكرة لضمان سرعة التنفيذ وعزل كل اختبار بقاعدة بيانات جديدة.

2. **الفروق الجوهرية والقيود التي لا تغطيها قاعدة الـ InMemory**:
   - `InMemoryDatabase` **لا تفرض** قيود الـ Foreign Keys (`DeleteBehavior.Restrict`).
   - `InMemoryDatabase` **لا تفرض** القيود الفريدة للـ Unique Indexes (مثل `IX_Students_StudentCode` أو الفهرس المركب على `AttendanceRecords`).
   - `InMemoryDatabase` **لا تختبر** سلوك الـ Deadlocks والـ Concurrency على مستوى جداول SQL Server الحقيقية.
   - **ما تؤكده الاختبارات بنجاح تام**:
     صحة مسارات الـ Controllers، خط أنابيب الـ Middleware، توليد وفك تشفير توكنات الـ JWT، فرض سياسات الـ RBAC، لوجيك عزل المستأجر في الاستعلامات، والتحقق المباشر من زيادة `TokenVersion`.

3. **السيناريوهات الأمنية غير المغطاة حاليًا في الاختبارات**:
   - محاولة إدخال `StudentCode` مكرر تحت ضغط متزامن (Race Condition).
   - سقوط اتصال قاعدة البيانات أثناء تنفيذ `StudentTokenValidationMiddleware` والتحقق من طريقة تعامل السيرفر معه (Graceful Degradation vs 500 Error).
   - اختبار هجوم Brute Force مكثف ضد الـ IP Rate Limiter للتحقق من إرجاع `429 Too Many Requests`.

---

### المحور السابع: Dependency Security Audit
**النتيجة: PASS**

1. **الحزم المعرضة للمخاطر وتاريخ ترقيتها**:
   - الحزمة المتأثرة سابقًا: `System.Security.Cryptography.Pkcs (9.0.11)` المستدعاة بشكل غير مباشر بواسطة `Microsoft.Data.SqlClient`.
   - الثغرات الموثقة:
     - **GHSA-846f-f764-76mx** (Moderate - Denial of Service)
     - **GHSA-2fmv-rv2c-59x7** (High - Remote Code Execution / Information Disclosure)
   - الحل المطبق: تثبيت مباشر للإصدار الآمن المتوافق مع .NET 10:
     ```xml
     <PackageReference Include="System.Security.Cryptography.Pkcs" Version="10.0.14" />
     ```
2. **التحقق الحالي**:
   - تم تشغيل الأمر: `dotnet list package --vulnerable --include-transitive`.
   - النتيجة الصريحة:
     ```text
     The given project `Hudoor.Api` has no vulnerable packages given the current sources.
     The given project `Hudoor.Api.Tests` has no vulnerable packages given the current sources.
     ```
3. **توافق الحزم**:
   - جميع حزم الـ EF Core و Authentication ومحرك الاختبارات متوافقة وموحدة على إصدار الصيانة `.NET 10.0.14`.
   - لا توجد أي تحذيرات بناء (0 Warnings, 0 Errors).

---

## 4. نتائج تشغيل الاختبارات والبناء (Build & Test Execution Log)

### مخرجات بناء الحل (Dotnet Build)
```text
dotnet build
  Determining projects to restore...
  All projects are up-to-date for restore.
  Hudoor.Api -> D:\AttendedanceSaaS\src\backend\Hudoor.Api\bin\Debug\net10.0\Hudoor.Api.dll
  Hudoor.Api.Tests -> D:\AttendedanceSaaS\tests\Hudoor.Api.Tests\bin\Debug\net10.0\Hudoor.Api.Tests.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)
```

### مخرجات الاختبارات الآلية (Dotnet Test)
```text
dotnet test
Test run for D:\AttendedanceSaaS\tests\Hudoor.Api.Tests\bin\Debug\net10.0\Hudoor.Api.Tests.dll (.NETCoreApp,Version=v10.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:    31, Skipped:     0, Total:    31, Duration: 4 s - Hudoor.Api.Tests.dll (net10.0)
```

جميع الاختبارات الـ 31 الموزعة على وحدات التشفير، المصادقة، عزل المستأجرين، صلاحيات الأدمن، وإبطال التوكن اللحظي تعمل بنجاح 100%.

---

## 5. توصيات الإصلاح والتحسين مرتبة حسب الأولوية (Prioritized Recommendations)

### الأولوية القصوى (High Priority - قبل إطلاق الإنتاج)
1. **معالجة الـ Default Credentials في `AdminBootstrapService`**:
   - إزالة القيمة الافتراضية الثابتة `"AdminSecure2026!"` من `AdminBootstrapService.cs` و `appsettings.json`.
   - تعديل المنطق البرمجي بحيث: إذا لم تتوفر بيانات الأدمن عبر متغيرات البيئة (`HUDOOR_ADMIN_BOOTSTRAP_*`) أو الإعدادات الخاصة بالإنتاج، يتم **تخطي عملية التأسيس بالكامل مع تسجيل Warning في الـ Logs**، بدلاً من استخدام كلمة سر افتراضية معلنة.

### الأولوية المتوسطة (Medium Priority - مع بداية Phase 4)
2. **قواعد التحقق الصارم لملكية الكيانات (Cross-Entity Tenant Validation)**:
   - عند تنفيذ الـ Endpoints الخاصة بإدارة المواد، المجموعات، والتسجيل في المرحلة 4، يجب وضع قاعدة معمارية صارمة:
     *عند إنشاء Group: التأكد برمجيًا من أن `Subject.TeacherId == CurrentTeacherId` و `AcademicYear.TeacherId == CurrentTeacherId`.*
     *عند تسجيل طالب في مجموعة: التأكد برمجيًا من أن `Group.TeacherId == Student.TeacherId == CurrentTeacherId`.*
3. **معالجة الـ Trailing Slash في Middleware التوكن**:
   - استبدال `path.EndsWith(...)` بـ `path.TrimEnd('/').EndsWith(...)` لتفادي حظر الطلاب الذين ترسل متصفحاتهم الرابط بشرطة مائلة ختامية أثناء تغيير كلمة المرور الإلزامية.

### الأولوية المنخفضة (Low Priority - للتحسين المستقبلي)
4. **اختبارات تكامل مخصصة لقاعدة بيانات SQL Server الحقيقية**:
   - إعداد مشروع اختبارات أو Test Fixture اختياري يختبر الـ Unique Indexes على SQL Server المحلي للتأكد من سلوك محرك قاعدة البيانات تحت ظروف التزامن (Concurrency).
5. **مقاومة حجب الحسابات المتعمد (Account DoS Mitigation)**:
   - دراسة إدخال CAPTCHA عند تكرار الفشل بدلاً من الحظر الكامل لـ 15 دقيقة لكود الطالب.

---
**نهاية تقرير المراجعة الأمنية والمعمارية للمرحلة الثالثة.**
