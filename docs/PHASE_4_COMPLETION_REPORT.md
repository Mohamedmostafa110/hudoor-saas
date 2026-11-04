# Hudoor — Phase 4: Teacher & Academic Domain Management
## Architectural Completion Report

**Project:** Hudoor (حضور) — Multi-Tenant Private Tutoring Attendance SaaS  
**Runtime:** .NET 10 (C# 14), ASP.NET Core Web API  
**Database:** Microsoft SQL Server 2022 (`HudoorDb`)  
**Status:** **PHASE 4 COMPLETED & VERIFIED (90/90 Tests Passing)**

---

## 1. Executive Summary

Phase 4 establishes the core academic and pedagogical domain management layer for teachers within Hudoor. Every endpoint and domain rule strictly enforces tenant scoping to the authenticated `TeacherId` extracted from validated JWT claims (`ITenantContext`), preventing cross-tenant leakage or tampering. All relationship mutations are pre-validated by the dedicated `ITenantOwnershipValidator`.

All 6 planned milestones of Phase 4 have been implemented and rigorously tested with 90 passing unit and integration tests.

---

## 2. Implemented Components & Endpoints Matrix

### 2.1 Infrastructure & Security Services
- **`IStudentCodeGenerator` / `StudentCodeGenerator`**:
  - Format: `HD-` + 6 unambiguous alphanumeric characters (`2-9`, `A-Z` excluding ambiguous `0, O, 1, I, L`).
  - Cryptographically secure CSPRNG (`RandomNumberGenerator.GetInt32`).
  - Database collision check with retry policy against global platform uniqueness.
- **`ITenantOwnershipValidator` / `TenantOwnershipValidator`**:
  - Scoped cross-tenant pre-validation for Academic Years, Terms, Subjects, Groups, Students, Group Creation, Group Enrollments, and Student Transfers.

---

### 2.2 Academic Years & Terms Management

#### Academic Years (`/api/teacher/academic-years`)
| Verb | Route | Description | Status Codes |
|---|---|---|---|
| `GET` | `/api/teacher/academic-years` | List teacher's academic years (with optional `includeArchived`) | `200 OK`, `401 Unauthorized` |
| `GET` | `/api/teacher/academic-years/{id}` | Get academic year details including nested terms | `200 OK`, `404 Not Found` |
| `POST` | `/api/teacher/academic-years` | Create academic year (`Name`, `StartDate`, `EndDate`, optional `Terms`) | `201 Created`, `400 Bad Request` |
| `PUT` | `/api/teacher/academic-years/{id}` | Update academic year name and dates | `200 OK`, `400 Bad Request`, `404 Not Found` |
| `POST` | `/api/teacher/academic-years/{id}/activate` | Set as the single active academic year (BR-ACA-010) | `200 OK`, `404 Not Found` |
| `POST` | `/api/teacher/academic-years/{id}/archive` | Archive academic year and deactivate it | `200 OK`, `404 Not Found` |
| `DELETE` | `/api/teacher/academic-years/{id}` | Hard delete (only permitted if no terms or groups exist; BR-ACA-011) | `204 NoContent`, `400 Bad Request`, `404 Not Found` |
| `POST` | `/api/teacher/academic-years/{id}/terms` | Add term to academic year | `201 Created`, `400 Bad Request`, `404 Not Found` |

#### Terms (`/api/teacher/terms`)
| Verb | Route | Description | Status Codes |
|---|---|---|---|
| `PUT` | `/api/teacher/terms/{id}` | Update term name and dates (validated within AcademicYear boundaries) | `200 OK`, `400 Bad Request`, `404 Not Found` |
| `PATCH` | `/api/teacher/terms/{id}/status` | Toggle term active status | `200 OK`, `404 Not Found` |
| `DELETE` | `/api/teacher/terms/{id}` | Delete term (only if no attendance sessions exist; BR-ACA-012) | `204 NoContent`, `400 Bad Request`, `404 Not Found` |

---

### 2.3 Subjects Management (`/api/teacher/subjects`)
| Verb | Route | Description | Status Codes |
|---|---|---|---|
| `GET` | `/api/teacher/subjects` | List teacher's subjects (with optional `includeArchived`) | `200 OK`, `401 Unauthorized` |
| `GET` | `/api/teacher/subjects/{id}` | Get subject details | `200 OK`, `404 Not Found` |
| `POST` | `/api/teacher/subjects` | Create subject (validates duplicate name conflict per teacher) | `201 Created`, `400 Bad Request`, `409 Conflict` |
| `PUT` | `/api/teacher/subjects/{id}` | Update subject details | `200 OK`, `400 Bad Request`, `404 Not Found`, `409 Conflict` |
| `POST` | `/api/teacher/subjects/{id}/archive` | Archive subject | `200 OK`, `404 Not Found` |
| `DELETE` | `/api/teacher/subjects/{id}` | Delete subject (prohibited if groups or historical data exist) | `204 NoContent`, `400 Bad Request`, `404 Not Found` |

---

### 2.4 Groups Management (`/api/teacher/groups`)
| Verb | Route | Description | Status Codes |
|---|---|---|---|
| `GET` | `/api/teacher/groups` | List groups with filters (`academicYearId`, `subjectId`, `includeArchived`) | `200 OK`, `401 Unauthorized` |
| `GET` | `/api/teacher/groups/{id}` | Get group details with enrolled student count | `200 OK`, `404 Not Found` |
| `POST` | `/api/teacher/groups` | Create group (validates Subject and AcademicYear ownership; duplicate active name check) | `201 Created`, `400 Bad Request`, `404 Not Found`, `409 Conflict` |
| `PUT` | `/api/teacher/groups/{id}` | Update group (name, schedule, capacity; checks current enrollment <= new capacity) | `200 OK`, `400 Bad Request`, `404 Not Found`, `409 Conflict` |
| `POST` | `/api/teacher/groups/{id}/archive` | Archive group | `200 OK`, `404 Not Found` |
| `DELETE` | `/api/teacher/groups/{id}` | Delete group (prohibited if sessions or enrollments exist; BR-ACA-023) | `204 NoContent`, `400 Bad Request`, `404 Not Found` |

---

### 2.5 Student Management (`/api/teacher/students`)
| Verb | Route | Description | Status Codes |
|---|---|---|---|
| `GET` | `/api/teacher/students` | List students with search (`search`), grade (`grade`), group (`groupId`), and status (`isActive`) | `200 OK`, `401 Unauthorized` |
| `GET` | `/api/teacher/students/{id}` | Get student detail with complete enrollment history | `200 OK`, `404 Not Found` |
| `POST` | `/api/teacher/students` | Provision student: auto-generates unique `StudentCode` (`HD-XXXXXX`), CSPRNG temporary password, sets `MustChangePassword = true`, `TokenVersion = 1`, optional initial group enrollment with capacity check | `201 Created`, `400 Bad Request`, `404 Not Found` |
| `PUT` | `/api/teacher/students/{id}` | Update student profile (name, phones, grade, notes) | `200 OK`, `400 Bad Request`, `404 Not Found` |
| `PATCH` | `/api/teacher/students/{id}/status` | Activate/deactivate student (deactivation increments `TokenVersion++` to immediately revoke active sessions) | `200 OK`, `404 Not Found` |
| `POST` | `/api/teacher/students/{id}/regenerate-code` | Regenerate student code (generates new unique code and increments `TokenVersion++`; BR-COD-042) | `200 OK`, `404 Not Found` |
| `DELETE` | `/api/teacher/students/{id}` | Delete student (prohibited if enrollment or attendance records exist; BR-STU-020) | `204 NoContent`, `400 Bad Request`, `404 Not Found` |

---

### 2.6 Group Enrollments & Student Transfers (`/api/teacher`)
| Verb | Route | Description | Status Codes |
|---|---|---|---|
| `GET` | `/api/teacher/groups/{groupId}/students` | List students currently enrolled in the specified group | `200 OK`, `404 Not Found` |
| `POST` | `/api/teacher/groups/{groupId}/students` | Enroll student into group (validates ownership, group capacity, prevents duplicate active enrollment) | `201 Created`, `400 Bad Request`, `404 Not Found`, `409 Conflict` |
| `DELETE` | `/api/teacher/groups/{groupId}/students/{studentId}` | Unenroll student (sets `LeftDateUtc = UtcNow`, `IsActive = false`; preserves historical attendance) | `200 OK`, `404 Not Found` |
| `POST` | `/api/teacher/enrollments/transfer` | Transfer student from one group to another (validates ownership of student + both groups, capacity check, closes old enrollment, opens new enrollment, preserves historical records; BR-STU-031) | `200 OK`, `400 Bad Request`, `404 Not Found`, `409 Conflict` |

---

## 3. Security & Domain Integrity Verification

1. **Strict Multi-Tenant Isolation**:
   - `TeacherId` is never trusted from request bodies or URLs; it is always derived from authenticated Claims Principal (`ITenantContext`).
   - Cross-tenant tests verified: Teachers cannot view, update, delete, enroll, or transfer resources belonging to other teachers. All cross-tenant attempts return `404 Not Found`.
2. **Preservation of Academic History**:
   - Destructive deletion of Academic Years, Subjects, Groups, and Students is blocked whenever related historical records exist, enforcing archival or deactivation instead.
3. **Session Revocation Mechanics**:
   - Deactivating a student or regenerating a student's code automatically increments `student.TokenVersion++`, causing immediate rejection of existing JWTs at `StudentTokenValidationMiddleware`.
4. **Vulnerability Audit**:
   - `dotnet list package --vulnerable --include-transitive` completed with 0 vulnerabilities detected.
5. **Automated Test Results**:
   - **Total Tests: 90**
   - **Passed: 90**
   - **Failed: 0**
   - **Skipped: 0**

---

## 4. Readiness for Phase 5

Phase 4 is complete and fully verified. The project is prepared for **Phase 5: Attendance Engine (Session Management, QR Code Engine, and Record Keeping)**.
