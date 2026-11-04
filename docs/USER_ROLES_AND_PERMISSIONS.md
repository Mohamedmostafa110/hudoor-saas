# User Roles and Permissions Specification

| Document Version | 1.0.0 |
| :--- | :--- |
| **Status** | Approved Specification |
| **Security Paradigm** | Role-Based Access Control (RBAC) + Tenant Context Scoping |

---

## 1. Role Definitions & Hierarchy

```
         ┌────────────────────────┐
         │     PlatformAdmin      │  (Cross-tenant system administration)
         └────────────────────────┘
                     │ (Provisions)
                     ▼
         ┌────────────────────────┐
         │        Teacher         │  (Tenant Boundary / Isolated Data Owner)
         └────────────────────────┘
                     │ (Registers & Manages)
                     ▼
         ┌────────────────────────┐
         │        Student         │  (Scoped read-only consumer + QR check-in)
         └────────────────────────┘
```

The system strictly enforces logical isolation between tenants. While `PlatformAdmin` has cross-tenant oversight for account provisioning, they do not participate in routine classroom operations. `Teacher` owns and isolates all academic domain data. `Student` is subordinate to the teacher who created them.

---

## 2. Detailed Role Profiles

### 2.1 PlatformAdmin
- **Identity Mechanism**: High-entropy Username + Strong Password + Multi-Factor Authentication (Recommended).
- **Scope**: Global system level.
- **Responsibilities**:
  - Provisioning and onboarding new Teacher accounts.
  - Setting initial temporary passwords for teachers.
  - Activating, suspending, or permanently deactivating teacher subscriptions.
  - Monitoring system metrics, API error rates, and tenant volume.
  - Managing global system settings and maintenance flags.
- **Restrictions**:
  - Cannot mark attendance or manipulate attendance logs directly on behalf of a teacher (preserves audit integrity).
  - Cannot access student personal communication or private notes unless explicitly authorized for technical support.

### 2.2 Teacher (Tenant Admin)
- **Identity Mechanism**: Unique Username + Password.
- **Scope**: Restricted strictly to their own Tenant ID (`TeacherId`).
- **Responsibilities**:
  - Complete CRUD control over their academic universe: Subjects, Groups, Sessions, and Students.
  - Customizing academic years and term definitions.
  - Initiating live attendance sessions and displaying QR check-in codes.
  - Manually recording and overriding attendance records (Present, Absent, Late, Excused).
  - Changing their own account password and updating profile/branding details.
  - Resetting student forgotten passwords to a simple, human-readable temporary password (without ever seeing the current or past password).
  - Exporting attendance summaries (PDF, Excel/CSV).
  - Executing academic year rollovers and student progressions.
- **Restrictions**:
  - Zero visibility into other teachers' subjects, students, or sessions.
  - Cannot create another teacher account.
  - Cannot view or retrieve existing student passwords (stored strictly as irreversible cryptographic hashes).
  - Cannot modify past attendance records without leaving an auditable footprint.

### 2.3 Student (End Consumer)
- **Identity Mechanism**: **Student Code + Password**.
- **Scope**: Scoped exclusively to the specific student record under the teacher who issued the credentials.
- **Responsibilities**:
  - Authenticate to the Student Portal via their unique Student Code and Password.
  - Change their password voluntarily at any time from account settings.
  - Complete mandatory password replacement upon first login or immediately following a teacher-initiated password reset.
  - Self-service QR code attendance check-in during active classroom sessions.
  - View personal attendance summary: percentage, total present, late, excused, and unexcused absences.
  - View detailed historical list of attended sessions.
- **Restrictions**:
  - Strictly Read-Only (except submitting a self-attendance scan event for an active matching session and updating own password).
  - Cannot view other students' records or class rosters.
  - Cannot bypass the `MustChangePassword` lock screen when a temporary password is active.
  - Cannot modify or dispute attendance records within the app (must request teacher intervention).
  - Cannot register independently without prior creation by a teacher.

---

## 3. Comprehensive Permissions Matrix

| Module / Operation | PlatformAdmin | Teacher | Student |
| :--- | :---: | :---: | :---: |
| **Teacher Accounts: Create / Provision** |  Yes |  No |  No |
| **Teacher Accounts: Activate / Deactivate** |  Yes |  No |  No |
| **Teacher Accounts: Reset Password (Admin)** |  Yes |  No |  No |
| **Teacher Accounts: Change Own Password** |  No (Self) |  Yes |  No |
| **Academic Years: Create / Edit / Rollover** |  No |  Yes (Own) |  No |
| **Subjects: Create / Read / Update / Delete** |  No |  Yes (Own) |  No |
| **Groups: Create / Read / Update / Delete** |  No |  Yes (Own) |  No |
| **Students: Register / Edit / Inactivate** |  No |  Yes (Own) |  No |
| **Students: Transfer Between Groups** |  No |  Yes (Own) |  No |
| **Student Codes: Generate / Regenerate** |  No |  Yes (Own) |  No |
| **Student Password: Reset to Temporary Credential** |  No |  Yes (Own Students) |  No |
| **Student Password: Change Own Password** |  No |  No |  Yes (Self) |
| **Student Password: View Existing Plaintext** |  No (Never) |  No (Never) |  No (Never) |
| **Sessions: Create / Launch / Close** |  No |  Yes (Own) |  No |
| **QR Code: Display Dynamic Check-in Code** |  No |  Yes (Own) |  No |
| **QR Code: Scan & Self Check-in** |  No |  No |  Yes (Own) |
| **Attendance: Manual Mark / Bulk Mark** |  No |  Yes (Own) |  No |
| **Attendance: Edit Historical Record** |  No |  Yes (Own + Audited) |  No |
| **Attendance: View Own Attendance History** |  No |  Yes (Own Students) |  Yes (Self Only) |
| **Audit Logs: Inspect Attendance History** |  Yes (Platform) |  Yes (Own) |  No |
| **Reports: Export Roster & Attendance Sheets**|  No |  Yes (Own) |  No |

---

## 4. Route & API Authorization Policies

The backend ASP.NET Core API enforces authorization using policy-driven attributes and tenant context extraction:

### 4.1 Platform Admin Endpoints
- **Policy**: `RequirePlatformAdminRole`
- **Route Prefix**: `/api/admin/*`
- **Claims Required**: `Role = PlatformAdmin`
- **Example Endpoints**:
  - `POST /api/admin/teachers` (Provision new teacher)
  - `PATCH /api/admin/teachers/{id}/status` (Activate/Deactivate)
  - `POST /api/admin/teachers/{id}/reset-password`

### 4.2 Teacher Endpoints
- **Policy**: `RequireTeacherRole`
- **Route Prefix**: `/api/teacher/*`
- **Claims Required**: `Role = Teacher`, `TeacherId = {guid}`
- **Security Rule**: The backend extracts `TeacherId` directly from the authenticated JWT claims. Any client-supplied `TeacherId` in query strings or JSON payloads is discarded and overwritten with the token claim.
- **Example Endpoints**:
  - `GET /api/teacher/subjects`
  - `POST /api/teacher/sessions`
  - `POST /api/teacher/sessions/{id}/attendance`
  - `PUT /api/teacher/attendance/{attendanceId}` (Triggers audit logging)
  - `POST /api/teacher/students/{studentId}/reset-password` (Generates CSPRNG temporary password, increments student `TokenVersion` to revoke existing sessions, writes audit log)

### 4.3 Student Endpoints
- **Policy**: `RequireStudentRole`
- **Route Prefix**: `/api/student/*`
- **Claims Required**: `Role = Student`, `StudentId = {guid}`, `TeacherId = {guid}`, `TokenVersion = {int}`
- **Security Rule**: The student JWT is minted only upon successful verification of both `StudentCode` and `Password`. It contains the student's unique ID, the originating `TeacherId`, the current `TokenVersion`, and a `must_change_password` boolean claim.
  - **Token Invalidation Rule**: Each authenticated student request validates that the JWT's `TokenVersion` claim strictly matches the current `TokenVersion` on the student's database entity. If mismatched (e.g. after password reset or password change), the request is rejected immediately with `401 Unauthorized` (`SESSION_REVOKED`). Password updates and `TokenVersion` increments are executed atomically in the same database transaction, ensuring previous tokens become invalid on their next request without relying on stale caches or external dependencies.
  - **First-Use Enforcement**: If `must_change_password == true`, all endpoints except `POST /api/student/me/change-password` return `403 Forbidden` (`PASSWORD_CHANGE_REQUIRED`).
- **Example Endpoints**:
  - `POST /api/student/auth/login` (Authenticates Student Code + Password)
  - `POST /api/student/me/change-password` (Voluntary or mandatory password update; increments `TokenVersion`)
  - `GET /api/student/me/attendance-summary`
  - `GET /api/student/me/attendance-history`
  - `POST /api/student/sessions/{sessionId}/check-in` (QR code submission)

---

## 5. Session & Token Specifications

| Parameter | PlatformAdmin | Teacher | Student |
| :--- | :--- | :--- | :--- |
| **Authentication Credential** | Username + Password | Username + Password | Student Code + Password |
| **Token Type** | Bearer JWT | Bearer JWT | Bearer JWT |
| **Token Expiry** | 4 Hours | 12 Hours (Sliding Refresh) | 30 Days (Sliding) |
| **Claims Injected** | `sub`, `role`, `email` | `sub`, `role`, `teacher_id`, `name` | `sub`, `role`, `student_id`, `teacher_id`, `token_version`, `must_change_password` |
| **Session Revocation Mechanism** | Explicit account status check | Checked on refresh & sensitive mutations | Token Version / Security Stamp validation in pipeline; increments on password reset/change |

---

## 6. Access Control Edge Cases

1. **Deactivated Teacher Account**:
   - If `PlatformAdmin` deactivates Teacher T1, all subsequent API requests with T1’s JWT are rejected with `403 Forbidden` (`ACCOUNT_SUSPENDED`).
   - All students associated with Teacher T1 are immediately blocked from logging in or checking in.
2. **Student Cross-Teacher Scan Attempt**:
   - If Student S1 (belonging to Teacher T1) scans a QR code belonging to Teacher T2, the API verifies that `Session.TeacherId == Student.TeacherId`. If mismatched, the request fails with `404 Not Found` or `400 Bad Request` (`SESSION_NOT_FOUND`) without disclosing T2's information.
3. **Student Cross-Group Scan Attempt**:
   - If Student S1 belongs to Group A, but scans a QR code for a session launched exclusively for Group B, the system rejects the check-in (`STUDENT_NOT_ENROLLED_IN_SESSION_GROUP`).
4. **Tampering with TeacherId in Requests**:
   - If an authenticated Teacher T1 sends `POST /api/teacher/sessions` with `{"teacherId": "t2-guid"}`, the server ignores the payload value and injects T1's ID from the JWT.
