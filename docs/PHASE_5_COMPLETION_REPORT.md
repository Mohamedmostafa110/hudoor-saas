# Hudoor — Phase 5: Attendance Engine & Dynamic QR Check-in
## Architectural Completion Report

**Project:** Hudoor (حضور) — Multi-Tenant Private Tutoring Attendance SaaS  
**Runtime:** .NET 10 (C# 14), ASP.NET Core Web API  
**Database:** Microsoft SQL Server 2022 (`HudoorDb`)  
**Status:** **PHASE 5 COMPLETED & VERIFIED (115/115 Tests Passing)**

---

## 1. Executive Summary

Phase 5 delivers the core operational attendance engine of Hudoor, enabling live classroom session management, dynamic rotating QR code generation, multi-channel attendance recording (QR self-service check-in by students and manual/bulk recording by teachers), post-session auditable adjustments, and a student attendance portal.

All business rules, cryptographic anti-replay protections, multi-tenant boundaries, and audit trail requirements have been implemented and verified with 115 automated unit and integration tests.

---

## 2. Implemented Architecture & Services

### 2.1 Dynamic QR Cryptographic Engine
- **`IQrCodeService` / `QrCodeService`**:
  - **Dynamic Rotation**: Rotates every **20 seconds** based on Unix time steps ($T_{\text{step}} = \lfloor T_{\text{unix}} / 20 \rfloor$).
  - **Cryptographic Signature**: Computes $\text{HMAC-SHA256}(\text{SessionId} + \text{TimeStep} + \text{Salt}, \text{SigningKey})$.
  - **Replay Protection & Tolerance**: Accepts current step $\pm 1$ step (maximum 40 seconds tolerance window) to accommodate network latency while defeating screenshot forwarding to absent peers.
  - **Constant-Time Verification**: Uses `CryptographicOperations.FixedTimeEquals` to prevent side-channel timing attacks.
  - **Session Salt**: Each session generates a distinct 32-byte CSPRNG cryptographic salt upon creation.

---

### 2.2 Teacher Session Management (`/api/teacher/sessions`)

| Verb | Route | Description | Status Codes |
|---|---|---|---|
| `GET` | `/api/teacher/sessions` | List sessions with filters (`groupId`, `status`, `fromDate`, `toDate`) and attendance metric rollups | `200 OK`, `401 Unauthorized` |
| `GET` | `/api/teacher/sessions/{id}` | Get session details including real-time attendance breakdown (Present, Late, Excused, Absent, Unrecorded) | `200 OK`, `404 Not Found` |
| `POST` | `/api/teacher/sessions` | Create session for an active group with auto-generated CSPRNG `QrSecuritySalt` | `201 Created`, `400 Bad Request`, `404 Not Found` |
| `PUT` | `/api/teacher/sessions/{id}` | Update title and session date (prohibited on archived sessions) | `200 OK`, `400 Bad Request`, `404 Not Found` |
| `PATCH` | `/api/teacher/sessions/{id}/status` | Transition status (`Draft`, `Active`, `Closed`, `Archived`). When closing an `Active` session, automatically marks all unrecorded enrolled students as `Absent` (Rule BR-ATT-061) | `200 OK`, `400 Bad Request`, `404 Not Found` |
| `DELETE` | `/api/teacher/sessions/{id}` | Delete session (prohibited if attendance records exist to preserve history) | `204 NoContent`, `400 Bad Request`, `404 Not Found` |
| `GET` | `/api/teacher/sessions/{id}/qr-token` | Retrieve current rotating QR token for active session with remaining countdown seconds and expiration timestamp | `200 OK`, `400 Bad Request`, `404 Not Found` |
| `GET` | `/api/teacher/sessions/{id}/roster` | Full group roster showing each enrolled student's current attendance status | `200 OK`, `404 Not Found` |
| `POST` | `/api/teacher/sessions/{id}/attendance` | Record or update attendance for an individual student (Present, Late, Excused, Absent) | `200 OK`, `201 Created`, `400 Bad Request`, `404 Not Found` |
| `POST` | `/api/teacher/sessions/{id}/attendance/bulk` | Bulk submit attendance for multiple students, with optional `MarkRemainingAsAbsent` | `200 OK`, `400 Bad Request`, `404 Not Found` |
| `GET` | `/api/teacher/sessions/{id}/audit-logs` | Retrieve all audit log entries for modifications in this session | `200 OK`, `404 Not Found` |

---

### 2.3 Post-Session Modification & Audit Trail (`/api/teacher/attendance`)

| Verb | Route | Description | Status Codes |
|---|---|---|---|
| `PUT` | `/api/teacher/attendance/{attendanceRecordId}` | Modify attendance record. Strictly enforces mandatory `ModificationReason` when changing between Absent and Present or when modifying records older than 24 hours (Rule BR-AUD-081). Creates immutable `AttendanceAuditLog` row | `200 OK`, `400 Bad Request`, `404 Not Found` |

---

### 2.4 Student Attendance Portal (`/api/student/attendance`)

| Verb | Route | Description | Status Codes |
|---|---|---|---|
| `POST` | `/api/student/attendance/check-in` | Student scans rotating QR code to self check-in. Validates active session state, student group enrollment, cryptographic token freshness, prevents duplicate check-in (Rule BR-ATT-072), and records `Status = Present`, `Method = QRCode` | `200 OK`, `400 Bad Request`, `403 Forbidden`, `404 Not Found`, `409 Conflict` |
| `GET` | `/api/student/attendance/history` | Student views their personal attendance history (date, group, subject, status, method, minutes late) | `200 OK`, `401 Unauthorized` |
| `GET` | `/api/student/attendance/summary` | Student views personal attendance statistics (Total Sessions, Present, Late, Excused, Absent, Attendance Percentage) | `200 OK`, `401 Unauthorized` |

---

## 3. Security & Rule Compliance Matrix

1. **Anti-Replay QR Security (BR-ATT-070)**:
   - Dynamic 20s token rotation verified.
   - Stale/expired tokens older than 40s rejected with `400 Bad Request`.
   - Tampered signatures rejected with constant-time verification.
2. **Duplicate Check-in Prevention (BR-ATT-072)**:
   - Enforced by database unique constraint `(SessionId, StudentId)` and application logic returning `409 Conflict`.
3. **Session Auto-Reconciliation on Close (BR-ATT-061)**:
   - Transitioning a session from `Active` to `Closed` automatically finds all active enrolled students lacking attendance records and creates an `Absent` record for them.
4. **Audit Trail Immutability (BR-AUD-080, BR-AUD-081, BR-AUD-082)**:
   - Modifying attendance records writes to `AttendanceAuditLogs` with `PreviousStatus`, `NewStatus`, `ModifiedByTeacherId`, `ModificationReason`, and timestamp.
   - Mandatory justification enforced for status reversals (`Absent` $\leftrightarrow$ `Present`) and records $> 24$ hours old.
5. **Strict Tenant & Role Scoping**:
   - Students can only check into sessions belonging to their own teacher and groups where they have active enrollments.
   - Students cannot view or access other students' records or teacher endpoints.

---

## 4. Verification Suite Results

```text
Test Run Summary:
  Total Test Files: 1
  Failed:           0
  Passed:         115
  Skipped:          0
  Total:          115
  Duration:      10 s
```

### Vulnerability Scan:
```text
dotnet list package --vulnerable --include-transitive
The given project `Hudoor.Api` has no vulnerable packages.
The given project `Hudoor.Api.Tests` has no vulnerable packages.
```

---

## 5. Next Phase Roadmap

Phase 5 is complete and verified. The backend is ready for **Phase 6: Reporting, Analytics & Attendance Exports (Excel/CSV, PDF Summaries, and Teacher Analytics)**.
