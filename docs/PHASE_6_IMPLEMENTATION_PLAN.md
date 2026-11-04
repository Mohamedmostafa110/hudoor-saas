# Hudoor — Phase 6 Implementation Plan: Reporting, Analytics & Academic Rollover
## Architecture & Technical Specification

**Author:** Senior .NET Software Architect  
**Project:** Hudoor (حضور) — Multi-Tenant Attendance SaaS  
**Target Solution:** `src/backend/Hudoor.Api`  
**Target Tests:** `tests/Hudoor.Api.Tests`  
**Target Framework:** .NET 10 (C# 14), ASP.NET Core Web API, SQL Server 2022  
**Date:** November 2026

---

## 1. Domain Overview & Invariants

Phase 6 provides the analytics, institutional reporting, CSV/Excel export engine, and academic year rollover wizard for teachers:
1. **Teacher Analytics & Insights**: Aggregated metrics across students, groups, sessions, and attendance performance, highlighting at-risk students with irregular attendance.
2. **Attendance Matrix & Roster Reports**: Structured group attendance sheets for custom date intervals and academic terms.
3. **Export Engine**: Standards-compliant CSV and Excel-compatible exports equipped with UTF-8 BOM (`\uFEFF`) to guarantee flawless Arabic character rendering in Microsoft Excel and Google Sheets.
4. **Academic Year Rollover Wizard**: Complete transition engine advancing an academic year without data loss (Rule BR-ROL-090 & BR-ROL-091), supporting bulk student progression (`Promote`, `Repeat`, `Depart`).

### Critical Architecture & Security Invariants
- **Strict Multi-Tenant Scoping**: All reports, aggregations, exports, and rollover workflows are strictly scoped to `TeacherId` derived from authenticated JWT claims (`ITenantContext`). Cross-tenant data leakage is strictly prohibited.
- **Zero Data Loss Guarantee (Rule BR-ROL-090)**: Executing an academic rollover must **never** execute `DELETE` or `CASCADE DELETE` on historical records. Past academic years, sessions, historical enrollments, and individual attendance records remain intact and readable.
- **Atomic Rollover Transaction**: The rollover wizard executes inside an atomic database transaction. If any progression fails, the entire transition rolls back cleanly.
- **Excel UTF-8 BOM Compatibility**: All CSV export endpoints prefix output streams with the UTF-8 Byte Order Mark (`0xEF, 0xBB, 0xBF`) to ensure native Arabic text rendering in Microsoft Excel without character corruption.

---

## 2. Implementation Milestones

### Milestone 6.1: Reporting & Analytics DTOs & Service
- DTOs in `src/backend/Hudoor.Api/DTOs/ReportDtos.cs`:
  - `TeacherAnalyticsSummaryDto`: Total students, active students, total sessions, overall attendance rate, total late, total excused, total absent, at-risk students count.
  - `GroupAttendanceSummaryDto`: Group metrics, session count, enrolled students, attendance rate.
  - `AtRiskStudentDto`: Student details, attendance percentage, missed sessions count, last attended date.
  - `GroupAttendanceMatrixDto`: Full grid with dates, sessions, and student status cells.
  - `ExportRequest`: format options (`csv`).
- Service in `src/backend/Hudoor.Api/Services/IExportService.cs` & `ExportService.cs`:
  - Generates RFC 4180-compliant CSV with UTF-8 BOM and escaped fields.

### Milestone 6.2: Reports & Analytics Controller (`ReportsController`)
- Route: `/api/teacher/reports`
- Endpoints:
  - `GET /api/teacher/reports/analytics`: comprehensive KPI summary and group breakdown.
  - `GET /api/teacher/reports/at-risk`: students with attendance below threshold (default 75%) or $\ge 3$ absences.
  - `GET /api/teacher/reports/groups/{groupId}/matrix`: attendance matrix grid for group sessions.
  - `GET /api/teacher/reports/export/group-attendance`: downloadable CSV of group attendance summary.
  - `GET /api/teacher/reports/export/session-attendance`: downloadable CSV of a single session roster.
  - `GET /api/teacher/reports/export/students`: downloadable CSV of teacher's student roster.

### Milestone 6.3: Academic Rollover Wizard (`RolloverController`)
- Route: `/api/teacher/academic-years/rollover`
- Endpoints:
  - `GET /api/teacher/academic-years/{id}/rollover-summary`: preview active students by grade and groups to be transitioned.
  - `POST /api/teacher/academic-years/rollover`: executes rollover transaction:
    - Creates and activates new academic year.
    - Archives previous academic year.
    - Applies progression actions (`Promote`, `Repeat`, `Depart`) to selected students.
    - Returns detailed transition report.

### Milestone 6.4: Comprehensive Integration Verification Suite
- Unit tests for `ExportService`: CSV formatting, escaping commas/quotes, UTF-8 BOM presence.
- Integration tests in `ReportsEndpointsTests.cs`:
  - Analytics calculation verification.
  - At-risk student identification.
  - Group matrix grid generation.
  - CSV export endpoints (verifying UTF-8 BOM and headers).
  - Cross-tenant report access rejection (404/empty).
- Integration tests in `RolloverEndpointsTests.cs`:
  - Rollover preview summary.
  - Successful rollover with student promotions, repetitions, and departures.
  - Verification that historical sessions and attendance records remain intact (Zero Data Loss).
  - Rejection of invalid transitions or cross-tenant references.

---

## 3. Execution Order

1. Create `ReportDtos.cs` & `RolloverDtos.cs`.
2. Implement `IExportService` & `ExportService` and register in DI.
3. Implement `ReportsController.cs`.
4. Implement `RolloverController.cs`.
5. Write and execute test suite: `ExportServiceTests`, `ReportsEndpointsTests`, `RolloverEndpointsTests`.
6. Run full test suite (`dotnet test`) and vulnerability scan.
7. Generate Phase 6 Architectural Completion Report.
