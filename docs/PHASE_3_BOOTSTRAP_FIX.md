# تقرير الإصلاح الأمني لتهيئة مسؤول المنصة (PlatformAdmin Bootstrap Security Fix)
**مشروع SaaS لإدارة الحضور (Hudoor SaaS)**  
**تاريخ الإصلاح:** 3 نوفمبر 2026  
**الملف التوثيقي:** `docs/PHASE_3_BOOTSTRAP_FIX.md`

---

## 1. المشكلة الأمنية (Vulnerability Context)

كشفت مراجعة المرحلة الثالثة الأمنية والمعمارية (`docs/PHASE_3_SECURITY_REVIEW.md`) عن وجود ثغرة استخدام بيانات اعتماد افتراضية ثابتة (**CWE-1392: Use of Default Credentials**):
- وجود كلمة مرور افتراضية صريحة في الكود: `"AdminSecure2026!"` واسم مستخدم افتراضي `"admin"` داخل `AdminBootstrapService.cs` كـ fallback.
- وجود نفس البيانات الافتراضية داخل ملفات التكوين `appsettings.json` و `appsettings.Development.json`.
- في حال نشر التطبيق على بيئة إنتاجية دون تكوين متغيرات بيئة، كان النظام يقوم تلقائيًا بإنشاء حساب مسؤول منصة بكلمة مرور معروفة للعامة، مما يمثل ثغرة حرجة للتحكم في المنصة.

---

## 2. الإجراءات التصحيحية المنفذة (Implemented Fixes)

### 2.1 حذف بيانات الاعتماد الافتراضية من ملفات الإعدادات
- **`src/backend/Hudoor.Api/appsettings.json`**: تم حذف قسم `"Bootstrap"` الذي يحتوي على `"AdminUsername"` و `"AdminPassword"` بالكامل.
- **`src/backend/Hudoor.Api/appsettings.Development.json`**: تم حذف قسم `"Bootstrap"` بالكامل لضمان عدم وجود أي بيانات افتراضية حتى في بيئة التطوير المحلية.

### 2.2 تعديل منطق الخدمة في `AdminBootstrapService.cs`
تم تحديث خدمة [`AdminBootstrapService.cs`](file:///d:/AttendedanceSaaS/src/backend/Hudoor.Api/Services/AdminBootstrapService.cs) بالقواعد الصارمة التالية:
1. **الاعتماد الحصري على مصادر آمنة**:
   - قراءة اسم المستخدم حصراً من `_configuration["Bootstrap:AdminUsername"]` (المحقونة عبر User Secrets أو Cloud Configuration Providers) أو متغير البيئة `HUDOOR_ADMIN_BOOTSTRAP_USERNAME`.
   - قراءة كلمة المرور حصراً من `_configuration["Bootstrap:AdminPassword"]` أو متغير البيئة `HUDOOR_ADMIN_BOOTSTRAP_PASSWORD`.
   - **تم حذف أي Fallback افتراضي تماماً** (تمت إزالة `?? "admin"` و `?? "AdminSecure2026!"`).
2. **التعامل الصارم مع غياب بيانات الاعتماد**:
   - إذا كان اسم المستخدم أو كلمة المرور فارغاً (`string.IsNullOrWhiteSpace`):
     - **لا يتم إنشاء أي حساب مسؤول على الإطلاق**.
     - **لا تُستخدم أي كلمة مرور بديلة**.
     - يتم تسجيل تحذير واضح وآمن:
       `"Bootstrap: PlatformAdmin bootstrap credentials were not provided. Initial admin account creation skipped."`
     - يستمر تشغيل التطبيق وجميع خدماته بشكل طبيعي تماماً.
3. **التحقق من سياسة كلمات المرور**:
   - التحقق من مطابقة كلمة المرور لسياسة أمان حسابات الموظفين (`ValidateStaffPassword`: لا تقل عن 8 محارف، وتحتوي أرقاماً وحروفاً).
4. **حماية الحسابات الموجودة مسبقاً**:
   - الفحص المسبق `if (await context.PlatformAdmins.AnyAsync(cancellationToken)) return;` يضمن عدم تعديل أو استبدال أو تكرار أي حساب مسؤول موجود مسبقاً في قاعدة البيانات، حتى لو تم تغيير متغيرات البيئة.
5. **السرية التامة في السجلات (Logging Hygiene)**:
   - طباعة اسم المستخدم فقط في سجل النجاح (`"Bootstrap: Initial PlatformAdmin account provisioned successfully for '{Username}'."`).
   - عدم طباعة كلمات المرور في أي Log أو Exception أو API Response.

### 2.3 عدم المساس بنظام المصادقة أو قاعدة البيانات
- لم يتم المساس بآلية تسجيل الدخول (`POST /api/auth/platform-admin/login`) أو توكنات الـ JWT أو عزل المستأجرين.
- لم يتم حذف قاعدة البيانات أو تعديل المخطط العلائقي (`HudoorDb`).

---

## 3. الملفات المعدلة (Modified Files)

| الملف | نوع التعديل | ملخص التغيير |
| :--- | :---: | :--- |
| [`src/backend/Hudoor.Api/Services/AdminBootstrapService.cs`](file:///d:/AttendedanceSaaS/src/backend/Hudoor.Api/Services/AdminBootstrapService.cs) | تعديل كود | إزالة القيم الافتراضية، فرض التخطي الآمن عند غياب البيانات، والتحقق من سياسة كلمة المرور. |
| [`src/backend/Hudoor.Api/appsettings.json`](file:///d:/AttendedanceSaaS/src/backend/Hudoor.Api/appsettings.json) | إعدادات | إزالة قسم `Bootstrap` وبياناته الافتراضية بالكامل. |
| [`src/backend/Hudoor.Api/appsettings.Development.json`](file:///d:/AttendedanceSaaS/src/backend/Hudoor.Api/appsettings.Development.json) | إعدادات | إزالة قسم `Bootstrap` وبياناته الافتراضية بالكامل. |
| [`tests/Hudoor.Api.Tests/Integration/AdminBootstrapTests.cs`](file:///d:/AttendedanceSaaS/tests/Hudoor.Api.Tests/Integration/AdminBootstrapTests.cs) | ملف جديد | إضافة 4 اختبارات تكاملية متخصصة لتأكيد كافة سيناريوهات الـ Bootstrap. |

---

## 4. التحقق والاختبارات الآلية (Verification & Automated Tests)

تمت كتابة اختبارات تكاملية شاملة في [`AdminBootstrapTests.cs`](file:///d:/AttendedanceSaaS/tests/Hudoor.Api.Tests/Integration/AdminBootstrapTests.cs) تغطي بدقة الحالات الأربع المطلوبة:

1. **`Bootstrap_WithValidCredentials_AndEmptyTable_CreatesAdminOnce`**:
   - عند تزويد بيانات اعتماد صحيحة عبر التكوين مع جدول فارغ، يتم إنشاء حساب Admin واحد فقط، وتتطابق بياناته ويتم التحقق من صحة تشفير كلمة المرور.
2. **`Bootstrap_WithoutCredentials_AndEmptyTable_DoesNotCreateAdmin`**:
   - عند انعدام بيانات الاعتماد في التكوين ومتغيرات البيئة مع جدول فارغ، لا يتم إنشاء أي حساب إطلاقاً (`Assert.Empty(admins)`).
3. **`Bootstrap_WhenAdminAlreadyExists_DoesNotReplaceOrModifyAdmin`**:
   - عند وجود مسؤول مسبقاً في قاعدة البيانات، وتغيير متغيرات البيئة ببيانات جديدة، يتم تجاهل البيانات الجديدة تماماً ولا يتم تعديل أو استبدال حساب المسؤول الأصلي أو كلمة مروره.
4. **`Bootstrap_OnAppRestart_DoesNotDuplicateAccount`**:
   - محاكاة إعادة تشغيل التطبيق بالكامل؛ التأكد من عدم تكرار إنشاء الحساب وبقاء حساب واحد فقط في قاعدة البيانات.

### نتائج تشغيل `dotnet test`
```text
dotnet test
Test run for D:\AttendedanceSaaS\tests\Hudoor.Api.Tests\bin\Debug\net10.0\Hudoor.Api.Tests.dll (.NETCoreApp,Version=v10.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:    35, Skipped:     0, Total:    35, Duration: 4 s - Hudoor.Api.Tests.dll (net10.0)
```

### نتائج تشغيل `dotnet build`
```text
dotnet build
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

### نتائج فحص الحزم المعرضة للمخاطر (`dotnet list package --vulnerable --include-transitive`)
```text
The given project `Hudoor.Api` has no vulnerable packages given the current sources.
The given project `Hudoor.Api.Tests` has no vulnerable packages given the current sources.
```

---

## 5. الخلاصة والتأكيد النهائي

- تم استئصال الثغرة الأمنية `CWE-1392` نهائيًا من الكود وملفات الإعدادات.
- أصبح إنشاء حساب مسؤول المنصة يعتمد حصرًا على تزويد صريح وآمن لبيانات الاعتماد عبر Environment Variables أو User Secrets.
- اجتاز الحل بالكامل 35 اختباراً بنسبة نجاح 100% وبصفر تحذيرات أو أخطاء بناء وبصفر ثغرات أمنية.
- لم يتم البدء في المرحلة 4 أو المساس بأي خصائص أخرى.
