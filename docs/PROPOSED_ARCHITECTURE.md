# Proposed System Architecture

| Document Version | 1.0.0 |
| :--- | :--- |
| **Status** | Architectural Blueprint |
| **Pattern** | Modular Monolith + Shared Database Multi-Tenancy |

---

## 1. System Topology Overview

```mermaid
flowchart TD
    subgraph Clients["Frontend Clients (PWA / Web)"]
        TeacherClient["Teacher PWA (Desktop/Tablet/Mobile)<br/>React 19 + Vite + Tailwind CSS"]
        StudentClient["Student PWA (Mobile Browser)<br/>React 19 + Camera Scanner"]
        AdminClient["Platform Admin Portal<br/>React 19 Dashboard"]
    end

    subgraph Gateway["Reverse Proxy & Edge"]
        Edge["Nginx / Azure App Service / Cloudflare<br/>SSL Termination & Rate Limiting"]
    end

    subgraph BackendAPI["Modular Monolith Backend (.NET 10 ASP.NET Core)"]
        AuthMiddleware["Tenant & Auth Middleware<br/>(Extracts TeacherId & Role from JWT)"]
        
        subgraph Modules["Application Modules"]
            TenantMod["Tenant & Identity Module"]
            AcademicMod["Academic Structure Module"]
            StudentMod["Student Management Module"]
            AttendanceMod["Attendance & Session Engine<br/>(QR Dynamic Validator + Manual Marking)"]
            AuditMod["Audit & History Module"]
        end
        
        EFCore["EF Core 10 DbContext<br/>(Global Query Filter: TeacherId = CurrentTeacher)"]
    end

    subgraph DataStore["Data Layer"]
        SQLServer[("Microsoft SQL Server<br/>Single DB with Row-Level Tenant Scoping")]
    end

    TeacherClient -->|HTTPS / REST| Edge
    StudentClient -->|HTTPS / REST| Edge
    AdminClient -->|HTTPS / REST| Edge

    Edge --> AuthMiddleware
    AuthMiddleware --> Modules
    Modules --> EFCore
    EFCore --> SQLServer
```

---

## 2. Monolith vs. Microservices Justification

### Selected Pattern: **Modular Monolith**
For Hudoor’s scale, team structure, and domain requirements, a Modular Monolith offers significant operational and technical advantages over microservices:
1. **Low Operational Overhead**: Single deployment unit, single database connection pool, and no complex distributed tracing or service mesh infrastructure.
2. **Strict Transactional Guarantees**: Complex operations like **Academic Year Rollover** and **Audit Log generation** can occur within native ACID transactions without distributed 2PC (Two-Phase Commit) or eventual consistency anomalies.
3. **Internal Module Encapsulation**: Modules communicate via typed interfaces and in-process mediator commands/queries. If a specific module (e.g., Attendance QR processing) needs to scale independently in the future, boundaries are cleanly drawn for easy extraction.

---

## 3. Multi-Tenancy Architecture

### 3.1 Tenancy Strategy: Shared Database, Shared Schema
- All tenants share the same SQL Server database.
- Every tenant-scoped entity implements `ITenantEntity`:
  ```csharp
  public interface ITenantEntity
  {
      Guid TeacherId { get; set; }
  }
  ```
- Cross-tenant leakage is prevented at three defensive layers:
  1. **JWT Claim Extraction**: `TeacherId` is resolved exclusively from the cryptographically verified JWT bearer token by `TenantContextMiddleware`.
  2. **EF Core Global Query Filters**:
     ```csharp
     modelBuilder.Entity<Group>()
         .HasQueryFilter(e => e.TeacherId == _tenantContext.TeacherId);
     ```
  3. **Repository / Command Handler Invariant Checks**: Every create/update mutation explicitly validates that modified child entities belong to `_tenantContext.TeacherId`.
  4. **Database Clustered/Non-Clustered Indexes**: All foreign keys and high-frequency queries lead with `(TeacherId, ...)`.

---

## 4. Entity-Relationship Model (Schema Blueprint)

```mermaid
erDiagram
    TEACHER ||--o{ ACADEMIC_YEAR : owns
    TEACHER ||--o{ SUBJECT : owns
    TEACHER ||--o{ STUDENT : manages
    TEACHER ||--o{ SESSION : conducts

    ACADEMIC_YEAR ||--o{ GROUP : contains
    SUBJECT ||--o{ GROUP : classifies

    GROUP ||--o{ GROUP_ENROLLMENT : enrolls
    STUDENT ||--o{ GROUP_ENROLLMENT : joins

    GROUP ||--o{ SESSION : hosts
    SESSION ||--o{ ATTENDANCE_RECORD : records
    STUDENT ||--o{ ATTENDANCE_RECORD : receives

    ATTENDANCE_RECORD ||--o{ ATTENDANCE_AUDIT_LOG : tracks
    STUDENT ||--o{ PASSWORD_RESET_AUDIT_LOG : audits

    TEACHER {
        Guid Id PK
        string FullName
        string Username UK
        string PasswordHash
        string Email
        string PhoneNumber
        bool IsActive
        DateTime CreatedAtUtc
    }

    ACADEMIC_YEAR {
        Guid Id PK
        Guid TeacherId FK
        string Name
        DateTime StartDate
        DateTime EndDate
        bool IsActive
        bool IsArchived
    }

    SUBJECT {
        Guid Id PK
        Guid TeacherId FK
        string Name
        string GradeLevel
        bool IsArchived
    }

    GROUP {
        Guid Id PK
        Guid TeacherId FK
        Guid SubjectId FK
        Guid AcademicYearId FK
        string Name
        string ScheduleDescription
        int MaxCapacity
        bool IsArchived
    }

    STUDENT {
        Guid Id PK
        Guid TeacherId FK
        string StudentCode UK
        string PasswordHash
        int TokenVersion
        bool MustChangePassword
        DateTime TemporaryPasswordExpiresAtUtc
        string FullName
        string PhoneNumber
        string ParentPhoneNumber
        string CurrentGrade
        bool IsActive
        DateTime CreatedAtUtc
    }

    GROUP_ENROLLMENT {
        Guid Id PK
        Guid TeacherId FK
        Guid GroupId FK
        Guid StudentId FK
        DateTime JoinedDateUtc
        DateTime LeftDateUtc
        bool IsActive
    }

    SESSION {
        Guid Id PK
        Guid TeacherId FK
        Guid GroupId FK
        string Title
        DateTime SessionDateUtc
        string Status
        string QrSecuritySalt
        DateTime CreatedAtUtc
    }

    ATTENDANCE_RECORD {
        Guid Id PK
        Guid TeacherId FK
        Guid SessionId FK
        Guid StudentId FK
        string Status
        string Method
        DateTime RecordedAtUtc
        int MinutesLate
        string Notes
    }

    ATTENDANCE_AUDIT_LOG {
        Guid Id PK
        Guid AttendanceRecordId FK
        string PreviousStatus
        string NewStatus
        Guid ModifiedByTeacherId FK
        DateTime TimestampUtc
        string ModificationReason
    }

    PASSWORD_RESET_AUDIT_LOG {
        Guid Id PK
        Guid StudentId FK
        Guid TriggeredByTeacherId FK
        DateTime TimestampUtc
        string ClientIpAddress
    }
```

---

## 5. Dynamic QR Attendance Handshake Sequence

```mermaid
sequenceDiagram
    autonumber
    actor Teacher
    actor Student
    participant TeacherUI as Teacher Device (Screen/Projector)
    participant StudentPWA as Student Mobile PWA
    participant Backend as ASP.NET Core API
    participant DB as SQL Server

    Teacher->>TeacherUI: Click "Start Session"
    TeacherUI->>Backend: POST /api/teacher/sessions/{id}/open-qr
    Backend-->>TeacherUI: Return dynamic QR token (Window: 20s)
    TeacherUI->>TeacherUI: Display rotating QR code

    Student->>StudentPWA: Open app & enter Student Code + Password
    StudentPWA->>Backend: POST /api/student/auth/login <br/> { studentCode, password }
    Backend->>Backend: Verify password hash & check MustChangePassword
    Backend-->>StudentPWA: Return Student JWT (Scoped to StudentId + TeacherId, must_change_password)

    opt First Login or Post-Reset (MustChangePassword == true)
        StudentPWA->>StudentPWA: Render Mandatory Password Change Screen
        Student->>StudentPWA: Enter new permanent password
        StudentPWA->>Backend: POST /api/student/me/change-password <br/> { currentPassword, newPassword }
        Backend->>DB: UPDATE PasswordHash, MustChangePassword = false
        Backend-->>StudentPWA: Return new refreshed JWT
    end

    Student->>StudentPWA: Open camera & scan projected QR
    StudentPWA->>Backend: POST /api/student/sessions/qr-check-in <br/> { qrToken }
    
    Backend->>Backend: 1. Validate QR Token Signature & Expiry Window
    Backend->>Backend: 2. Verify Session belongs to Student's Teacher
    Backend->>Backend: 3. Verify Student enrolled in Session's Group
    Backend->>DB: Check if attendance already recorded (SessionId, StudentId)
    
    alt Already Checked In
        DB-->>Backend: Existing Record Found
        Backend-->>StudentPWA: Return 409 Conflict ("Already checked in at HH:mm")
    else Valid Check-In
        Backend->>DB: INSERT AttendanceRecord (Status = Present, Method = QRCode)
        DB-->>Backend: Success
        Backend-->>StudentPWA: Return 200 OK ("Attendance confirmed")
        Backend-->>TeacherUI: (Via Polling / SSE) Real-time roster tick: Student Marked Present
    end
```

---

## 6. Teacher-Driven Student Password Reset Sequence

```mermaid
sequenceDiagram
    autonumber
    actor Teacher
    actor Student
    participant TeacherUI as Teacher Dashboard
    participant Backend as ASP.NET Core API
    participant DB as SQL Server
    participant StudentPWA as Student Mobile PWA

    Note over Teacher,Student: Student informs teacher: "I forgot my password"
    Teacher->>TeacherUI: Navigate to Student Profile & click "Reset Password"
    TeacherUI->>Backend: POST /api/teacher/students/{id}/reset-password
    
    Backend->>Backend: 1. Verify Teacher owns Student (Tenant Guard)
    Backend->>Backend: 2. CSPRNG generates neutral readable temp password (e.g., "Falcon482")
    Backend->>Backend: 3. Compute secure hash (PBKDF2/Argon2id)
    Backend->>DB: UPDATE Student SET PasswordHash = hash, MustChangePassword = true, TokenVersion = TokenVersion + 1, TempPasswordExpiresAt = Now + DefaultTtl (e.g. 7d)
    Note over Backend,DB: Incrementing TokenVersion immediately invalidates existing JWT sessions
    Backend->>DB: INSERT PasswordResetAuditLog (StudentId, TeacherId, UtcNow, Ip)
    
    Backend-->>TeacherUI: Return 200 OK with temporaryPassword: "Falcon482"
    TeacherUI->>TeacherUI: Display modal: "New Temporary Password: Falcon482"<br/>(Shown once, easy to read, copy button)
    Teacher->>Student: Teacher communicates "Falcon482" verbally or via message
    
    Student->>StudentPWA: Open app & enter Student Code + "Falcon482"
    StudentPWA->>Backend: POST /api/student/auth/login
    Backend-->>StudentPWA: Return JWT with claim must_change_password: true, tv: new_version
    StudentPWA->>StudentPWA: Force "Create Your New Password" Screen
    Student->>StudentPWA: Enters chosen personal password
    StudentPWA->>Backend: POST /api/student/me/change-password
    Backend->>DB: Hash new password, set MustChangePassword = false, TokenVersion = TokenVersion + 1
    Backend-->>StudentPWA: Password changed successfully, full portal unlocked
```

---

## 7. Practical JWT Session Revocation Strategy for Modular Monolith

A critical architectural consideration in token-based architectures is session revocation. Pure stateless JWTs cannot be revoked mid-flight by default without an active state validation mechanism.

### 7.1 Architecture Trade-Off Evaluation

| Approach | How It Works | Pros | Cons | Verdict |
| :--- | :--- | :--- | :--- | :---: |
| **1. Pure Stateless Tokens (Short TTL)** | Rely strictly on token expiration (e.g., 15 minutes) + Refresh Tokens in DB. | Fully decoupled; zero DB lookup on normal API calls. | Compromised student device remains valid for up to 15 minutes after password reset. | Rejected (violates immediate invalidation requirement) |
| **2. Distributed Token Blacklist (Redis)** | Write revoked JWT `jti` (Token ID) to Redis cache with TTL matching token expiration. | Immediate invalidation; fast key lookup. | Adds external Redis dependency, infrastructure cost, connection clustering, and operational complexity. | Over-engineered for MVP Monolith |
| **3. Database TokenVersion / Security Stamp Validation (Recommended)** | Add an integer `TokenVersion` column to the `Student` entity, embedded as claim `tv` in the JWT. On each authenticated student API request, authorization handler compares `claim.tv == student.TokenVersion` directly against the database entity via fast indexed lookup. Password updates and `TokenVersion++` execute atomically in the same database transaction. | • Instantaneous revocation: previous tokens become invalid on their immediate next request.<br/>• Eliminates stale-cache authorization vulnerabilities.<br/>• Zero external infrastructure dependencies (no Redis required for MVP).<br/>• Fits Clean Architecture Modular Monolith seamlessly. | Requires a lightweight indexed DB check per student authenticated request. | **Recommended for MVP Architecture** |

---

## 8. Frontend Architectural Blueprint

### 8.1 Technology Choice & Rationale
- **Vite + React 19 + TypeScript**: Maximum developer ergonomics, ultra-fast builds, and strict type safety across all DTO contracts.
- **Tailwind CSS v4**: Utility-first styling with native CSS variables supporting zero-runtime theme switches (Light Warm Gold vs. Dark Navy Blue) and RTL flip utilities (`ms-`, `me-`, `start-`, `end-`).
- **PWA Capabilities**: Service Worker caching of static assets and shell UI using Workbox; offline roster viewing for teachers in erratic network zones.
- **State Management**:
  - **Server State**: `@tanstack/react-query` for smart caching, background refetching, and optimistic roster updates.
  - **Client State**: Lightweight `Zustand` store for Auth tokens, Active Theme, and Language preference.

### 8.2 Internationalization (i18n) Engine
- Built with `i18next` and `react-i18next`.
- Supports dynamic `document.documentElement.setAttribute('dir', 'rtl' | 'ltr')`.
- All custom user data (Student Names, Subject Titles, Notes) bypass the translation layer and render strictly in their raw input character set.
- All custom user data (Student Names, Subject Titles, Notes) bypass the translation layer and render strictly in their raw input character set.
