# Hudoor — Phase 5 Implementation Plan: Attendance Engine & QR Check-in
## Architecture & Technical Specification

**Author:** Senior .NET Software Architect  
**Project:** Hudoor (حضور) — Multi-Tenant Attendance SaaS  
**Target Solution:** `src/backend/Hudoor.Api`  
**Target Tests:** `tests/Hudoor.Api.Tests`  
**Target Framework:** .NET 10 (C# 14), ASP.NET Core Web API, SQL Server 2022  
**Date:** November 2026

---

## 1. Domain Overview & Invariants

Phase 5 introduces the operational attendance engine of Hudoor, covering:
1. Dynamic rotating QR Code generation and cryptographic time-window validation.
2. Fluid, on-demand session creation and lifecycle management (`Draft` $\rightarrow$ `Active` $\rightarrow$ `Closed` $\rightarrow$ `Archived`).
3. Multi-channel attendance recording (dynamic QR self-service check-in by students + manual roster marking by teachers).
4. Immutable audit logging for post-session attendance edits.
5. Student attendance self-service portal (QR check-in, attendance history, statistical summary).

### Critical Architecture & Security Invariants
- **Multi-Tenant Isolation**: Every session, attendance record, and audit log belongs strictly to `TeacherId` derived from JWT claims. Teachers cannot view or manipulate sessions of other teachers.
- **Student Scope Isolation**: Students can only check into active sessions of groups in which they are actively enrolled, belonging to their issuing teacher. Students can only view their own personal attendance history.
- **Dynamic QR Anti-Replay Security**:
  - Formula: $\text{HMAC-SHA256}(\text{SessionId} + \text{TimeStep} + \text{Salt}, \text{SecretKey})$.
  - Rotation interval: **20 seconds**.
  - Tolerance window: Current time step $\pm 1$ step (maximum 40 seconds) to prevent screenshot sharing while tolerating reasonable network transmission delays.
  - Session Salt: A 32-byte CSPRNG random cryptographic salt generated at session creation.
- **Unique Attendance Constraint**: Database unique index `(SessionId, StudentId)` prevents duplicate check-ins.
- **Audit Immutability**: Any status modification to an existing attendance record writes an append-only row to `AttendanceAuditLogs`. Audit logs can never be updated or deleted.
- **Auto-Reconciliation on Session Close**: Closing an active session automatically records missing enrolled students as `Absent` (Rule BR-ATT-061), ensuring clean and complete rosters.

---

## 2. Implementation Milestones

### Milestone 5.1: Dynamic QR Code Cryptographic Engine
- Interface: `IQrCodeService`
- Implementation: `QrCodeService`
- Capabilities:
  - `GenerateToken(Guid sessionId, string sessionSalt, DateTime? utcNow = null) -> (string Token, int RemainingSeconds, DateTime ExpiresAtUtc)`
  - `ValidateToken(string token, Guid expectedSessionId, string sessionSalt, DateTime? utcNow = null) -> bool`
  - Cryptographic constant-time comparison using `CryptographicOperations.FixedTimeEquals` to prevent timing attacks.
  - Key configuration from `JwtSettings` / `QrCodeSettings`.
- Tests: `QrCodeServiceTests` (rotation at 20s boundaries, expired tokens rejection, invalid signature rejection, wrong session rejection).

### Milestone 5.2: Teacher Session Management
- Controller: `SessionsController` (`/api/teacher/sessions`)
- Endpoints:
  - `GET /api/teacher/sessions`: filter by `groupId`, `status`, date range (`fromDate`, `toDate`).
  - `GET /api/teacher/sessions/{id}`: detailed breakdown (enrolled, present, late, excused, absent, unrecorded).
  - `POST /api/teacher/sessions`: create session for an active group with auto-generated `QrSecuritySalt`.
  - `PUT /api/teacher/sessions/{id}`: update title and session date.
  - `PATCH /api/teacher/sessions/{id}/status`: transition status (`Draft`, `Active`, `Closed`, `Archived`). On close, auto-reconciles unrecorded enrolled students to `Absent`.
  - `DELETE /api/teacher/sessions/{id}`: delete session (prohibited if attendance records exist).
- Tests: `SessionsEndpointsTests`

### Milestone 5.3: Teacher Attendance Roster & Manual Recording
- Endpoints in `SessionsController` / `AttendanceController`:
  - `GET /api/teacher/sessions/{id}/roster`: full roster of enrolled students with current attendance status.
  - `GET /api/teacher/sessions/{id}/qr-token`: retrieves live rotating QR token for active session with countdown metadata.
  - `POST /api/teacher/sessions/{id}/attendance`: record or update single student attendance (Status, MinutesLate, Notes).
  - `POST /api/teacher/sessions/{id}/attendance/bulk`: mark attendance for multiple students in batch.
  - `PUT /api/teacher/attendance/{attendanceRecordId}`: modify attendance record with mandatory audit log.
  - `GET /api/teacher/sessions/{id}/audit-logs`: view history of changes for records in this session.
- Tests: `AttendanceEndpointsTests`

### Milestone 5.4: Student Attendance Check-in & History Portal
- Controller: `StudentAttendanceController` (`/api/student/attendance`)
- Endpoints:
  - `POST /api/student/attendance/check-in`: student submits scanned QR token. Validates session active state, enrollment, QR signature/time window, records `Present` via `QRCode`.
  - `GET /api/student/attendance/history`: view own attendance records with date, group, subject, status.
  - `GET /api/student/attendance/summary`: view statistics (total sessions, present, late, excused, absent, attendance percentage).
- Tests: `StudentAttendanceEndpointsTests`

### Milestone 5.5: Comprehensive Integration & Security Verification
- Cross-tenant attack tests:
  - Teacher cannot access/modify/delete other teacher's sessions.
  - Student cannot check into other teacher's or non-enrolled group sessions.
  - Student cannot check into closed or draft sessions.
  - Stale QR token rejection tests.
- Full test suite run (`dotnet test`) and package vulnerability scan.

---

## 3. Data Transfer Objects (DTOs)

### Session DTOs (`SessionDtos.cs`)
- `CreateSessionRequest`: `Guid GroupId`, `string Title`, `DateTime? SessionDateUtc`, `string? Status`
- `UpdateSessionRequest`: `string Title`, `DateTime SessionDateUtc`
- `UpdateSessionStatusRequest`: `string Status`
- `SessionSummaryDto`: `Id`, `GroupId`, `GroupName`, `SubjectName`, `Title`, `SessionDateUtc`, `Status`, `EnrolledStudentsCount`, `PresentCount`, `LateCount`, `ExcusedCount`, `AbsentCount`, `UnrecordedCount`, `CreatedAtUtc`
- `SessionDetailDto`: Full session info with attendance metric summary.
- `SessionQrTokenDto`: `Guid SessionId`, `string QrToken`, `int RemainingSeconds`, `DateTime ExpiresAtUtc`

### Attendance DTOs (`AttendanceDtos.cs`)
- `RecordAttendanceRequest`: `Guid StudentId`, `string Status`, `int? MinutesLate`, `string? Notes`
- `BulkRecordAttendanceRequest`: `List<RecordAttendanceItemRequest> Records`, `bool MarkRemainingAsAbsent`
- `RecordAttendanceItemRequest`: `Guid StudentId`, `string Status`, `int? MinutesLate`, `string? Notes`
- `UpdateAttendanceRecordRequest`: `string Status`, `int? MinutesLate`, `string? Notes`, `string? ModificationReason`
- `AttendanceRecordResponseDto`: `Id`, `SessionId`, `StudentId`, `StudentCode`, `StudentName`, `Status`, `Method`, `RecordedAtUtc`, `MinutesLate`, `Notes`
- `AttendanceAuditLogDto`: `Id`, `AttendanceRecordId`, `PreviousStatus`, `NewStatus`, `ModifiedByTeacherId`, `ModificationReason`, `TimestampUtc`
- `SessionRosterStudentDto`: `StudentId`, `StudentCode`, `FullName`, `PhoneNumber`, `AttendanceRecordId`, `Status`, `Method`, `RecordedAtUtc`, `MinutesLate`, `Notes`

### Student Attendance DTOs (`StudentAttendanceDtos.cs`)
- `StudentCheckInRequest`: `string QrToken`
- `StudentCheckInResponseDto`: `Guid SessionId`, `string SessionTitle`, `string GroupName`, `string SubjectName`, `DateTime RecordedAtUtc`, `string Status`, `string Method`
- `StudentAttendanceHistoryDto`: `Guid SessionId`, `string SessionTitle`, `string GroupName`, `string SubjectName`, `DateTime SessionDateUtc`, `string Status`, `string Method`, `int? MinutesLate`, `DateTime RecordedAtUtc`
- `StudentAttendanceSummaryDto`: `int TotalSessions`, `int PresentCount`, `int LateCount`, `int ExcusedCount`, `int AbsentCount`, `double AttendancePercentage`
