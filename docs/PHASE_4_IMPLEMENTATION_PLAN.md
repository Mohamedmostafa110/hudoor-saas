# خطة تنفيذ المرحلة الرابعة: إدارة المجال الأكاديمي والمدرسين (Phase 4 Implementation Plan)
**مشروع SaaS لإدارة الحضور (Hudoor SaaS)**  
**الدور:** كبير مهندسي برمجيات .NET (Senior .NET Software Architect)  
**الحالة:** وثيقة معمارية تخطيطية معتمدة — بانتظار الموافقة قبل التنفيذ  
**الملف:** `docs/PHASE_4_IMPLEMENTATION_PLAN.md`

---

## 1. تقييم المعمارية الحالية (Existing Architecture Assessment)

### 1.1 البنية البرمجية الحالية (Current Software Stack)
- **منصة العمل:** .NET 10 (C#) بنمط **Modular Monolith** معتمد على المتحكمات (Controller-based Web API).
- **قاعدة البيانات:** Microsoft SQL Server 2022 (`localhost`, `HudoorDb`) عبر Entity Framework Core 10.0.14.
- **إدارة الهوية والتفويض:**
  - مصادقة هجينة معتمدة على JWT Bearer Tokens بتوقيع متماثل (HMAC-SHA256) ومدعمة بصلاحيات صريحة (`PlatformAdmin`, `Teacher`, `Student`).
  - عزل المستأجرين محقق عبر سياق عمل مخصص ومحقون في الحاوية (`ITenantContext` / `TenantContext`) يستخرج `CurrentTeacherId` و `CurrentUserId` حصرياً من ادعاءات التوكن المعتمدة (`ClaimsPrincipal`)، مع تجاهل تام لأي معرف يُرسل عبر الـ Body أو الـ Query String.
  - آلية التحقق المباشر من `TokenVersion` للطلاب عبر `StudentTokenValidationMiddleware` مباشرة من SQL Server دون كاش لضمان الإلغاء اللحظي للجلسات.
  - آلية Rate Limiting متعددة المستويات في الذاكرة لحماية مسارات تسجيل الدخول.
- **حالة الاختبارات:**
  - 35 اختباراً آلياً (وحدات وتكامل) تغطي بنسبة 100% عمليات المصادقة، تشفير كلمات المرور، عزل المدرسين، إلغاء جلسات الطلاب، وتهيئة الأدمن الآمنة.

### 1.2 حالة الكيانات وقاعدة البيانات في المرحلة الحالية
الجداول الأساسية الستة الخاصة بنطاق المرحلة الرابعة تم إنشاؤها بالفعل في ترحيل قاعدة البيانات الأولي (`InitialCreate`):
1. `AcademicYears` (الأعوام الأكاديمية)
2. `Terms` (الفصول/الفترات الدراسية)
3. `Subjects` (المواد الدراسية)
4. `Groups` (المجموعات الدراسية)
5. `Students` (الطلاب)
6. `GroupEnrollments` (سجلات تسجيل ونقل الطلاب بالمجموعات)

جميع هذه الجداول تحتوي بالفعل على حقل `TeacherId` كمفتاح أجنبي مرتبط بجدول `Teachers` بقيد تقييد الحذف (`DeleteBehavior.Restrict`).

---

## 2. نطاق المرحلة الرابعة (Phase 4 Scope)

تشمل المرحلة الرابعة بناء الخدمات ومنطق الأعمال ومسارات الـ API للوظائف التالية:
1. **إدارة الأعوام الأكاديمية (Academic Years)**: إنشاء، تحديث، تفعيل عام دراسي واحد فقط نشط لكل مدرس، والأرشفة الآمنة.
2. **إدارة الفصول الدراسية (Terms)**: تنظيم الفصول التابعة للعام الدراسي، تحديد التواريخ، والتفعيل.
3. **إدارة المواد (Subjects)**: إنشاء المواد والمستويات التعليمية المرتبطة بها وأرشفتها.
4. **إدارة المجموعات (Groups)**: ربط المجموعات بمادة محددة وعام دراسي نشط، تحديد المواعيد والسعة القصوى.
5. **إدارة وتأسيس الطلاب (Student Provisioning & Management)**: إنشاء ملفات الطلاب، التوليد الآلي لكود طالب فريد عالمياً (`StudentCode`)، التوليد الآلي لكلمة مرور مؤقتة عبر CSPRNG، التعديل، والتعطيل/الأرشفة.
6. **تسجيل ونقل الطلاب (Group Enrollment & Transfers)**: تسجيل الطلاب بالمجموعات، فرض حدود السعة القصوى، فك التسجيل، وإدارة التحويل بين المجموعات مع الحفاظ الصارم على السجل التاريخي للحضور.

---

## 3. تقييم الكيانات والتعديلات المطلوبة (Entity & Relational Assessment)

### 3.1 فحص الكيانات الحالية
بعد فحص الكيانات في `src/backend/Hudoor.Api/Data/Entities/`:
- **`AcademicYear`**: يمتلك الخصائص الأساسية (`Id`, `TeacherId`, `Name`, `StartDate`, `EndDate`, `IsActive`, `IsArchived`, `CreatedAtUtc`). **مكتمل**.
- **`Term`**: يمتلك (`Id`, `TeacherId`, `AcademicYearId`, `Name`, `StartDate`, `EndDate`, `IsActive`, `CreatedAtUtc`). **مكتمل**.
- **`Subject`**: يمتلك (`Id`, `TeacherId`, `Name`, `GradeLevel`, `IsArchived`, `CreatedAtUtc`). **مكتمل**.
- **`Group`**: يمتلك (`Id`, `TeacherId`, `SubjectId`, `AcademicYearId`, `Name`, `ScheduleDescription`, `MaxCapacity`, `IsArchived`, `CreatedAtUtc`). **مكتمل**.
- **`Student`**: يمتلك (`Id`, `TeacherId`, `StudentCode`, `PasswordHash`, `TokenVersion`, `MustChangePassword`, `TemporaryPasswordExpiresAtUtc`, `FullName`, `PhoneNumber`, `ParentPhoneNumber`, `CurrentGrade`, `Notes`, `IsActive`, `CreatedAtUtc`). **مكتمل**.
- **`GroupEnrollment`**: يمتلك (`Id`, `TeacherId`, `GroupId`, `StudentId`, `JoinedDateUtc`, `LeftDateUtc`, `IsActive`). **مكتمل**.

### 3.2 الخلاصة المعمارية للكيانات
**لا توجد حاجة لتعديل كود الكيانات أو إضافة حقول جديدة في المرحلة 4**. الكيانات الحالية تغطي كافة متطلبات النطاق الأكاديمي. المطلوب فقط هو إضافة القيود المنطقية والفهارس (Indexes) وقواعد التحقق في طبقة التطبيق.

---

## 4. استراتيجية عزل المستأجرين وسلامة العلاقات (Tenant Isolation Strategy)

### 4.1 مشكلة العلاقات العرضية بين المستأجرين (Cross-Tenant Relational Risk)
كما كشفت المراجعة الأمنية في المرحلة الثالثة (`PHASE_3_SECURITY_REVIEW.md`):
- القيود المفروضة في SQL Server حالياً هي قيود أجنبية بسيطة (Simple Foreign Keys)، مثل `FK_Groups_Subjects_SubjectId` التي تتحقق فقط من وجود الـ `SubjectId` في جدول `Subjects`.
- **المخاطرة المعمارية**: إذا قام مدرس (T1) بإرسال `SubjectId` يملكه مدرس آخر (T2)، فإن SQL Server سيقبل الربط طالما المعرف موجود في الجدول!

### 4.2 استراتيجية الحل المعتمدة: التحقق الصارم في طبقة النطاق (Application-Layer Ownership Validator)
للحفاظ على قاعدة البيانات الحالية دون الحاجة إلى تعديلات جذرية تفرض Composite Foreign Keys مركبة تتطلب ترحيلات واسعة:
1. **حظر الاعتماد على مدخلات العميل**: استخراج `CurrentTeacherId` دائماً من ادعاءات التوكن المحققة عبر `ITenantContext`.
2. **خدمة التحقق من الملكية المشتركة (`ITenantOwnershipValidator`)**:
   تأسيس خدمة مركزية تفحص أي علاقة تجميعية قبل الإنشاء أو التعديل:
   - **عند إنشاء Term**: التحقق من أن `AcademicYear.TeacherId == CurrentTeacherId`.
   - **عند إنشاء Group**: التحقق المزدوج من أن `Subject.TeacherId == CurrentTeacherId` **و** `AcademicYear.TeacherId == CurrentTeacherId`.
   - **عند تسجيل طالب بمجموعة**: التحقق من أن `Group.TeacherId == CurrentTeacherId` **و** `Student.TeacherId == CurrentTeacherId`.
   - **عند نقل طالب بين مجموعتين**: التحقق من أن `Student`, `FromGroup`, `ToGroup` جميعها تنتمي حصراً إلى `CurrentTeacherId`.
3. **سياسة الاستجابة عند انتهاك الملكية**:
   إرجاع `404 Not Found` فوراً (بدلاً من 403 Forbidden) لعدم تسريب أي معلومات عن وجود الكيانات التابعة لمدرسين آخرين.

---

## 5. قواعد الأعمال وسلامة البيانات التاريخية (Business Rules & Historical Data)

1. **العام الأكاديمي النشط (Rule BR-ACA-010)**:
   - يُسمح للمدرس بإنشاء عدة أعوام، ولكن **عام واحد فقط** يمكن أن يحمل `IsActive = true` في نفس اللحظة.
   - عند تفعيل عام دراسي (`PATCH /activate`)، يتم إلغاء تفعيل أي عام دراسي نشط حالياً لنفس المدرس ضمن معاملة ذرية واحدة (Single Transaction).
2. **منع الحذف الإتلافي وحماية السجلات التاريخية (Rule BR-ACA-011, BR-ACA-023)**:
   - **الأعوام الدراسية والمجموعات**: يُمنع الحذف النهائي (`Hard Delete`) إذا كان العام أو المجموعة مرتبطاً بجلسات حضور (`Sessions`) أو سجلات تسجيل. في هذه الحالة يُسمح فقط بالأرشفة (`IsArchived = true`).
   - **الطلاب**: لا يُحذف الطالب نهائياً إذا كان لديه أي سجل حضور تاريخي؛ بل يُعطل حسابه (`IsActive = false`) وتُغلق تسجيلاته الفعالة.
3. **سلامة التحويل بين المجموعات (Rule BR-STU-031)**:
   - عند نقل طالب من المجموعة A إلى المجموعة B:
     - إغلاق سجل المجموعة A: `LeftDateUtc = UtcNow`, `IsActive = false`.
     - فتح سجل جديد في المجموعة B: `JoinedDateUtc = UtcNow`, `IsActive = true`.
     - سجلات حضور الطالب السابقة تظل مرتبطة بجلسات المجموعة A السابقة دون أي تعديل أو نقل رجعي.
4. **توليد كود الطالب (Rule BR-COD-040, BR-COD-041)**:
   - كود الطالب فريد عالمياً عبر المنصة بالكامل لضمان نجاح صفحة الدخول الموحدة دون تحديد المدرس.
   - الصيغة المعتمدة للتوليد الآلي: بادئة المنصة مع أرقام وحروف غير ملتبسة عالية العشوائية عبر CSPRNG (مثال: `HD-74928`).
5. **توليد كلمة المرور الأولية (Rule BR-PWD-047)**:
   - عند إنشاء الطالب، يولد النظام تلقائياً كلمة مرور مؤقتة عبر `IPasswordService.GenerateTemporaryPassword()` بصيغة الكلمات الحيادية (مثال: `Falcon482`).
   - يتم تخزين الهاش فوراً وتعيين `MustChangePassword = true` وصلاحية 7 أيام، وتُعاد كلمة المرور المؤقتة للـ Teacher مرة واحدة فقط في استجابة الإنشاء لعرضها على الشاشة.

---

## 6. واجهات برمجة التطبيقات (API Endpoints Specification)

جميع هذه المسارات محمية بالكامل وتتطلب توكن معتمد بدور `Teacher` (`[Authorize(Roles = Roles.Teacher)]`):

### 6.1 إدارة الأعوام الأكاديمية والفصول الدراسية (`AcademicYearsController`)
| الطريقة | المسار (Endpoint) | الوصف | قيود الأعمال |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/teacher/academic-years` | استعراض أعوام المدرس الأكاديمية | دعم فلترة `includeArchived=false` |
| `GET` | `/api/teacher/academic-years/active` | استرجاع العام الدراسي النشط حالياً | إرجاع 404 إذا لم يوجد عام نشط |
| `GET` | `/api/teacher/academic-years/{id}` | تفاصيل عام محدد مع إحصائيات المجموعات | قصر البحث على معرف المدرس |
| `POST` | `/api/teacher/academic-years` | إنشاء عام دراسي جديد | التحقق من تواريخ البداية والنهاية |
| `PUT` | `/api/teacher/academic-years/{id}` | تعديل بيانات العام الدراسي | التحقق من ملكية المدرس للعام |
| `PATCH` | `/api/teacher/academic-years/{id}/activate` | تعيين هذا العام كعام دراسي نشط | إلغاء تنشيط الأعوام الأخرى ذرياً |
| `PATCH` | `/api/teacher/academic-years/{id}/archive` | أرشفة العام الدراسي | تحويل `IsArchived = true`, `IsActive = false` |
| `DELETE` | `/api/teacher/academic-years/{id}` | حذف العام الدراسي | مسموح فقط إذا كان عدد الفصول والمجموعات 0 |
| `GET` | `/api/teacher/academic-years/{yearId}/terms` | استعراض فصول العام الدراسي | التأكد من ملكية المدرس للعام |
| `POST` | `/api/teacher/academic-years/{yearId}/terms` | إنشاء فصل دراسي جديد تابع للعام | التحقق من وقوع تواريخ الفصل ضمن العام |
| `PUT` | `/api/teacher/terms/{termId}` | تعديل فصل دراسي | قصر التعديل على فصول المدرس |
| `PATCH` | `/api/teacher/terms/{termId}/status` | تنشيط/تعطيل فصل دراسي | تعديل `IsActive` |
| `DELETE` | `/api/teacher/terms/{termId}` | حذف فصل دراسي | مسموح فقط في حال عدم وجود جلسات حضور |

### 6.2 إدارة المواد الدراسية (`SubjectsController`)
| الطريقة | المسار (Endpoint) | الوصف | قيود الأعمال |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/teacher/subjects` | استعراض المواد الدراسية للمدرس | دعم خيار `includeArchived` |
| `GET` | `/api/teacher/subjects/{id}` | تفاصيل مادة دراسية ومجموعاتها | التحقق من الملكية |
| `POST` | `/api/teacher/subjects` | إضافة مادة دراسية جديدة | الاسم، والمرحلة الدراسية |
| `PUT` | `/api/teacher/subjects/{id}` | تعديل مادة دراسية | التحقق من الملكية وعدم التكرار |
| `PATCH` | `/api/teacher/subjects/{id}/archive` | أرشفة مادة دراسية | منع استخدامها في مجموعات جديدة |
| `DELETE` | `/api/teacher/subjects/{id}` | حذف مادة دراسية | مسموح فقط إذا كان عدد المجموعات المرتبطة 0 |

### 6.3 إدارة المجموعات الدراسية (`GroupsController`)
| الطريقة | المسار (Endpoint) | الوصف | قيود الأعمال |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/teacher/groups` | استعراض المجموعات مع فلاتر | تصفية حسب العام الدراسي والمادة |
| `GET` | `/api/teacher/groups/{id}` | تفاصيل المجموعة وعدد الطلاب والسعة | التحقق من الملكية |
| `POST` | `/api/teacher/groups` | إنشاء مجموعة دراسية جديدة | التحقق الإلزامي من ملكية المادة والعام الدراسي |
| `PUT` | `/api/teacher/groups/{id}` | تعديل بيانات المجموعة والمواعيد | التحقق من الملكية |
| `PATCH` | `/api/teacher/groups/{id}/archive` | أرشفة مجموعة دراسية | تحويل `IsArchived = true` |
| `DELETE` | `/api/teacher/groups/{id}` | حذف مجموعة دراسية | مسموح فقط إذا لم تنعقد بها أي جلسات حضور |

### 6.4 إدارة الطلاب وتأسيس الحسابات (`StudentsController`)
| الطريقة | المسار (Endpoint) | الوصف | قيود الأعمال |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/teacher/students` | استعراض وبحث الطلاب (Roster) | دعم التصفية بالاسم، الكود، المجموعة، والصف |
| `GET` | `/api/teacher/students/{id}` | استعراض ملف الطالب الأكاديمي | عرض المجموعات المسجل بها وحالة الحساب |
| `POST` | `/api/teacher/students` | تسجيل طالب جديد وتوليد بياناته | توليد تلقائي لـ StudentCode و TempPassword |
| `PUT` | `/api/teacher/students/{id}` | تحديث بيانات الطالب الشخصية | تعديل الاسم، الهواتف، الصف، والملاحظات |
| `PATCH` | `/api/teacher/students/{id}/status` | تنشيط / تعطيل حساب الطالب | تعطيل الحساب يلغي جلساته الحالية فوراً |
| `POST` | `/api/teacher/students/{id}/regenerate-code` | إعادة توليد كود الطالب إذا تسرب | يغير الكود ويزيد `TokenVersion` لإلغاء الجلسات |

*(ملاحظة: مسار إعادة تعيين كلمة المرور `POST /api/teacher/students/{id}/reset-password` منجز ومختبر بالكامل في المرحلة 3).*

### 6.5 تسجيل ونقل الطلاب (`EnrollmentsController`)
| الطريقة | المسار (Endpoint) | الوصف | قيود الأعمال |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/teacher/groups/{groupId}/students` | قائمة الطلاب المسجلين حالياً بالمجموعة | التأكد من ملكية المجموعة للمدرس |
| `POST` | `/api/teacher/groups/{groupId}/students` | تسجيل طالب في المجموعة | فحص السعة القصوى ومنع التكرار النشط |
| `DELETE` | `/api/teacher/groups/{groupId}/students/{studentId}`| فك تسجيل طالب من المجموعة | إغلاق السجل (`LeftDateUtc`) وحفظ السجل التاريخي |
| `POST` | `/api/teacher/enrollments/transfer` | نقل طالب من مجموعة إلى أخرى | إغلاق الأولى وفتح الثانية ذرياً مع حفظ الحضور السابق |

---

## 7. كائنات نقل البيانات وقواعد التحقق (DTOs & Validation Rules)

### 7.1 حزم DTOs للأعوام الدراسية والفصول
- **`CreateAcademicYearRequest`**:
  - `Name`: `[Required, StringLength(100, MinimumLength = 3)]`
  - `StartDate`: `[Required, DataType(DataType.Date)]`
  - `EndDate`: `[Required, DataType(DataType.Date)]` (يجب أن يكون لاحقاً لـ StartDate).
  - `SetAsActive`: `bool` (اختياري، يحدد إذا كان يرغب في تنشيطه فور إنشائه).
- **`AcademicYearResponseDto`**:
  - `Id`, `Name`, `StartDate`, `EndDate`, `IsActive`, `IsArchived`, `TermsCount`, `GroupsCount`, `CreatedAtUtc`.
- **`CreateTermRequest`**:
  - `Name`: `[Required, StringLength(100, MinimumLength = 2)]`
  - `StartDate`: `[Required]`
  - `EndDate`: `[Required]`
- **`TermResponseDto`**:
  - `Id`, `AcademicYearId`, `Name`, `StartDate`, `EndDate`, `IsActive`.

### 7.2 حزم DTOs للمواد والمجموعات
- **`CreateSubjectRequest`**:
  - `Name`: `[Required, StringLength(200, MinimumLength = 2)]`
  - `GradeLevel`: `[StringLength(100)]`
- **`SubjectResponseDto`**:
  - `Id`, `Name`, `GradeLevel`, `IsArchived`, `ActiveGroupsCount`, `CreatedAtUtc`.
- **`CreateGroupRequest`**:
  - `SubjectId`: `[Required]` (Guid)
  - `AcademicYearId`: `[Required]` (Guid)
  - `Name`: `[Required, StringLength(150, MinimumLength = 2)]`
  - `ScheduleDescription`: `[StringLength(300)]` (مثال: "السبت والثلاثاء 4:00 عصراً")
  - `MaxCapacity`: `[Range(1, 1000)]` (اختياري)
- **`GroupResponseDto`**:
  - `Id`, `SubjectId`, `SubjectName`, `AcademicYearId`, `AcademicYearName`, `Name`, `ScheduleDescription`, `MaxCapacity`, `EnrolledStudentsCount`, `IsArchived`.

### 7.3 حزم DTOs للطلاب والتسجيل
- **`CreateStudentRequest`**:
  - `FullName`: `[Required, StringLength(200, MinimumLength = 3)]`
  - `PhoneNumber`: `[Phone, StringLength(50)]`
  - `ParentPhoneNumber`: `[Phone, StringLength(50)]`
  - `CurrentGrade`: `[StringLength(100)]`
  - `Notes`: `[StringLength(1000)]`
  - `InitialGroupId`: `Guid?` (اختياري، لتسجيله بمجموعة فور إنشائه).
- **`StudentCreatedResponseDto`**:
  - `Id`, `StudentCode`, `TemporaryPassword`, `FullName`, `CurrentGrade`, `ExpiresAtUtc`, `EnrolledGroupId`.
  *(هذا الـ DTO هو الكائن الوحيد الذي يعيد كلمة المرور المؤقتة لمشاركتها مع الطالب).*
- **`StudentDetailResponseDto`**:
  - `Id`, `StudentCode`, `FullName`, `PhoneNumber`, `ParentPhoneNumber`, `CurrentGrade`, `Notes`, `IsActive`, `MustChangePassword`, `Enrollments` (قائمة المجموعات الحالية والسابقة).
- **`EnrollStudentRequest`**:
  - `StudentId`: `[Required]` (Guid)
- **`TransferStudentRequest`**:
  - `StudentId`: `[Required]` (Guid)
  - `FromGroupId`: `[Required]` (Guid)
  - `ToGroupId`: `[Required]` (Guid)

---

## 8. خطة الاختبارات الآلية المطلوبة (Required Automated Tests Plan)

سيتم توسيع مشروع `tests/Hudoor.Api.Tests` لتغطية كافة متطلبات المرحلة الرابعة:

1. **اختبارات الأعوام الدراسية والفصول (`AcademicYearTests`)**:
   - التحقق من تفعيل عام دراسي واحد فقط، وإلغاء تنشيط العام السابق تلقائياً.
   - التحقق من منع الحذف النهائي إذا كان العام يحتوي على مجموعات أو فصول، والتحقق من نجاح الأرشفة.
   - التحقق من رفض إضافة فصل بتواريخ تقع خارج نطاق العام الدراسي.
2. **اختبارات المواد والمجموعات وعزل العلاقات (`GroupTenantIsolationTests`)**:
   - محاولة المدرس T1 إنشاء مجموعة مستخدماً `SubjectId` الخاص بالمدرس T2 ⬅️ رفض الطلب فوراً بـ `404 Not Found`.
   - محاولة المدرس T1 إنشاء مجموعة مستخدماً `AcademicYearId` الخاص بالمدرس T2 ⬅️ رفض الطلب فوراً بـ `404 Not Found`.
   - التحقق من السعة القصوى للمجموعة: رفض إضافة طالب يتجاوز السعة بـ `400 Bad Request`.
3. **اختبارات تأسيس الطلاب وإدارة الهوية (`StudentManagementTests`)**:
   - التحقق من أن تسجيل الطالب يُنشئ تلقائياً كود طالب فريد عالمياً بصيغة نظيفة (`HD-XXXXXX`).
   - التحقق من توليد كلمة مرور مؤقتة عبر CSPRNG، وتعيين `MustChangePassword = true` وصلاحية 7 أيام.
   - التحقق من أن المدرس T1 لا يستطيع قراءة أو تعديل أو تعطيل طالب يملكه المدرس T2.
   - التحقق من أن إعادة توليد كود الطالب (`regenerate-code`) ترفع `TokenVersion` وتلغي جلسات الطالب الحالية فوراً.
4. **اختبارات تسجيل وتحويل الطلاب وحفظ التاريخ (`GroupEnrollmentTests`)**:
   - محاولة تسجيل طالب من المدرس T2 في مجموعة المدرس T1 ⬅️ رفض بـ `404 Not Found`.
   - محاولة تسجيل نفس الطالب في نفس المجموعة مرتين وهي نشطة ⬅️ رفض بـ `409 Conflict`.
   - اختبار تحويل طالب من مجموعة إلى أخرى: إغلاق الأولى وتاريخ المغادرة `LeftDateUtc`، وفتح الثانية `JoinedDateUtc`، والتأكد من بقاء بيانات الحضور القديمة متصلة بالمجموعة السابقة.

---

## 9. خطة التنفيذ المقترحة خطوة بخطوة (Step-by-Step Implementation Roadmap)

لضمان عمل متقن ومنظم، سيتم تقسيم التنفيذ إلى خطوات صغيرة ومستقلة:

```
┌────────────────────────────────────────────────────────────────────────┐
│                   خطوات تنفيذ المرحلة الرابعة (Phase 4)                 │
├────────────────────────────────────────────────────────────────────────┤
│ • الخطوة 4.1: البنية التحتية والمولدات (Infrastructure & Code Generator)│
│   - خدمة توليد كود الطالب الفريد عالمياً (CSPRNG StudentCodeGenerator) │
│   - خدمة التحقق من ملكية الكيانات المعزولة (TenantOwnershipValidator)  │
│                                                                        │
│ • الخطوة 4.2: إدارة الأعوام الدراسية والفصول (AcademicYears & Terms)   │
│   - الـ DTOs والـ Controller الخاص بـ AcademicYears & Terms             │
│   - منطق التفعيل الأحادي والأرشفة الآمنة                                │
│                                                                        │
│ • الخطوة 4.3: إدارة المواد الدراسية (Subjects Management)               │
│   - الـ DTOs والـ Controller الخاص بـ Subjects                         │
│   - عمليات الإنشاء، التعديل، وفحص إمكانية الحذف والأرشفة                │
│                                                                        │
│ • الخطوة 4.4: إدارة المجموعات الدراسية (Groups Management)             │
│   - الـ DTOs والـ Controller الخاص بـ Groups                           │
│   - تطبيق الفحص الصارم لملكية المادة والعام الأكاديمي                   │
│                                                                        │
│ • الخطوة 4.5: إدارة وتأسيس الطلاب (Student Provisioning)               │
│   - الـ DTOs والـ Controller الخاص بـ Students                         │
│   - منطق التوليد الآلي والتعطيل وإعادة توليد الكود                      │
│                                                                        │
│ • الخطوة 4.6: تسجيل ونقل الطلاب بالمجموعات (Enrollment & Transfers)    │
│   - الـ DTOs والـ Controller الخاص بـ Enrollments                      │
│   - فحص السعة القصوى، منع التكرار، والتحويل الذري بين المجموعات         │
│                                                                        │
│ • الخطوة 4.7: كتابة الاختبارات التكاملية الشاملة وتشغيل الفحص          │
│   - تغطية السيناريوهات الأكاديمية بالكامل، فحص الثغرات، والتحقق        │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 10. التبعيات والقرارات المعلقة (Dependencies & Unresolved Decisions)

استناداً إلى وثيقة `docs/OPEN_QUESTIONS.md`، نؤكد أن الخطة التزمت التزاماً كاملاً بعدم افتراض أي قرارات لم تُحسم:

1. **صيغة كود الطالب (Q-02 - Proposed)**:
   - الخطة تعتمد كوداً فريداً ومولداً تلقائياً يبدأ بـ `HD-` مع أرقام عشوائية لضمان عدم حدوث تعارض (Collision Resistance) على مستوى المنصة.
2. **صلاحية كلمة المرور المؤقتة (Q-03 - Proposed)**:
   - الخطة تعتمد المهلة المقترحة (7 أيام) كقيمة افتراضية حتى يتم حسم الخيار نهائياً من العميل.
3. **تكرار هواتف الطلاب**:
   - لا يوجد قيد فريد على أرقام هواتف الطلاب أو أولياء الأمور، لأن بيئة الدروس والمراكز في مصر والشرق الأوسط تشهد تكرار رقم هاتف ولي الأمر للأشقاء (Siblings) المشتركين مع نفس المدرس.
4. **أدوار المساعدين (Q-06 - Deferred)**:
   - تم الالتزام بتأجيل نظام المساعدين (Assistants) إلى ما بعد الـ MVP وفقاً لما تم الاتفاق عليه، والعمليات تدار حصراً بحساب الـ Teacher.

---

## 11. الخلاصة والموافقة المطلوبة

تم الانتهاء من إعداد الخطة التفصيلية بنسبة 100% دون أي تعديل على كود التطبيق، أو ترحيل قاعدة البيانات، أو المساس بالمعمارية الحالية.

**نحن جاهزون للبدء بالخطوة الأولى (الخطوة 4.1: خدمات البنية التحتية وتوليد الأكواد والتحقق من الملكية) فور اعتماد الخطة.**
