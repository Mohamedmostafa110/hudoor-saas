# Security Requirements & Threat Mitigations

| Document Version | 1.0.0 |
| :--- | :--- |
| **Status** | Approved Security Architecture |
| **Focus** | Multi-Tenancy Isolation, Password Security & Student Code Threat Modeling |

---

## 1. Authentication Mechanisms

### 1.1 Teacher & PlatformAdmin Authentication
- **Credentials**: Unique Username + High-entropy Password.
- **Password Policy**:
  - Minimum 10 characters.
  - Must contain at least one uppercase letter, one lowercase letter, one number, and one non-alphanumeric character.
  - Checked against known breached passwords (HaveIBeenPwned API or bloom filter).
- **Password Storage**:
  - Hashed using **PBKDF2 with HMAC-SHA512** (100,000+ iterations) or **Argon2id** (Memory: 64MB, Iterations: 3, Parallelism: 4) via ASP.NET Core Identity's secure hasher.
  - Individual cryptographically random 128-bit salt per user.
- **Account Lockout Policy**:
  - 5 consecutive failed attempts trigger a 15-minute temporary lockout.
  - Generic authentication failure response ("Invalid username or password") to prevent username enumeration.

### 1.2 Student Authentication & Password Security
- **Credentials**: Unique Student Code + Password.
- **Self-Registration**: Strictly disabled. Teachers provision all student accounts.
- **Storage & Hash Protection**:
  - Passwords (both permanent student-chosen passwords and system-generated temporary passwords) are **never stored in plain text**.
  - System applies standard cryptographic hashing (PBKDF2/Argon2id) immediately upon generation.
  - **Zero Exposure**: No one—neither the teacher nor the PlatformAdmin—can view, retrieve, or reverse an existing student password.
- **Voluntary Password Changes**: Students can update their passwords anytime from portal settings by providing current + new passwords.

---

## 2. Student Temporary Password & Reset Security Model

> [!NOTE]
> **Confirmed Security Decision**:  
> Students authenticate using **Student Code + Password**. This provides strong credential defense while maintaining a simple, friction-free classroom recovery model through Teacher-driven password resets (no OTPs, SMS, or third-party email dependencies).

### 2.1 Threat Analysis & Mitigations

| Threat | Description | Severity | Countermeasure / Mitigation |
| :--- | :--- | :---: | :--- |
| **Credential Brute-Force** | Attacker attempts automated password guessing against student accounts. | **Low / Mitigated** | 1. Combined entropy of unique `StudentCode` + `Password`.<br/>2. Layered rate limiting (account-level + classroom-aware IP-level).<br/>3. Progressive response delay.<br/>4. Generic login errors (no code existence leakage). |
| **Predictable Temporary Passwords** | Attacker guesses teacher-generated temporary passwords. | **Low / Mitigated** | 1. Passwords generated using CSPRNG (`RandomNumberGenerator`).<br/>2. Strictly independent of student name, phone, or birth date.<br/>3. Format: Letters + Digits (e.g., `Falcon482`, `Cedar735`), avoiding ambiguous chars (`0/O`, `1/I/l`). |
| **Stale Temporary Credential Abuse** | Unclaimed temporary passwords remaining active indefinitely. | **Medium** | 1. Time-to-Live (TTL) expiration (Proposed Default: 7 days, pending approval).<br/>2. Mandatory password change enforced on first login (`MustChangePassword = true`). |
| **Session Hijacking / Shared Device** | Compromised student device remains logged in after password reset. | **High** | 1. Password updates and `TokenVersion++` execute atomically in the same database transaction.<br/>2. Every authenticated student request validates `claim.tv == student.TokenVersion` against the database, invalidating prior tokens on their immediate next request without stale caches. |
| **Insider Snooping (Teacher/Admin)** | Teacher or administrator attempts to read a student's personal password. | **Critical** | 1. Plaintext password is never saved in database, logs, or backups.<br/>2. API returns only temporary passwords at the precise moment of generation; existing passwords cannot be read. |

### 2.2 Temporary Password Generation Specification
- **Generator**: Uses .NET `RandomNumberGenerator` to assemble a neutral capitalized word from a static dictionary + high-entropy numeric suffix (e.g., `Falcon482`, `Cedar735`, `Swift924`).
- **Character Set**: Alphanumeric only (`A-Z`, `a-z`, `2-9`). Excludes confusing glyphs: `0`, `O`, `1`, `I`, `l`.
- **Generation Constraint**: Cryptographically random selection from a sanitized neutral word list followed by 3-4 random digits. Must **never** utilize the student's actual name, phone number, parent phone, or registration date.
- **Workflow & Ephemeral Presentation**:
  - Returned in the HTTP response of `POST /api/teacher/students/{id}/reset-password`.
  - Rendered in a single-view modal with a "Copy Credentials" button.
  - Immediately hashed in SQL Server; subsequent page reloads show only that a temporary password is pending redemption.
- **Enforced First-Use Replacement**:
  - The JWT claim `must_change_password: true` locks the student into `/change-password`.
  - Attempts to call session endpoints or check-in endpoints return `403 Forbidden` (`PASSWORD_CHANGE_REQUIRED`).

### 2.3 Layered Rate Limiting & Brute-Force Architecture

To balance classroom operational realities against brute-force risks:

1. **Account-Level Rate Limiting (Primary Defense - Proposed)**:
   - Key: `StudentCode`
   - Threshold: 5 consecutive failed login attempts within a 15-minute window locks the student account temporarily for 15 minutes (or requires teacher reset).
   - *Rationale*: Directly neutralizes targeted dictionary attacks against a specific student account.
2. **Classroom-Aware IP Rate Limiting (Secondary Defense - Proposed)**:
   - Key: Client IP Address
   - **Crucial Rule**: The system **must not** apply an aggressive low-threshold IP ban (e.g., 5 attempts/IP), because 50 to 100 students in a tutoring center or auditorium frequently share a single public Wi-Fi or NAT router IP.
   - Threshold (Proposed): Volumetric protection set to 100 requests per minute per IP, preventing automated scripts from spraying across the entire platform while preventing accidental denial of service to an entire classroom.
3. **Progressive Response Delays (Confirmed Decision)**:
   - Repeated failed login attempts on the same connection or account trigger progressive artificial delays (e.g., 500ms $\rightarrow$ 1,000ms $\rightarrow$ 2,000ms) to throttle automated attacks.
4. **Generic Authentication Errors (Confirmed Decision)**:
   - Failed logins always return HTTP `401 Unauthorized` with the generic body:
     ```json
     { "error": "INVALID_CREDENTIALS", "message": "Invalid student code or password." }
     ```
   - The response time and payload remain strictly identical whether the `StudentCode` does not exist or the password was incorrect, preventing student code enumeration.

*Status Distinction*: The generic error responses and progressive delay are **Confirmed Decisions**. The specific numeric thresholds (5 attempts per code, 100 req/min per IP) are **Proposed Decisions Pending Approval**.

---

## 3. Dynamic QR Code Anti-Cheating & Integrity Model

To guarantee that students cannot take a photograph of the QR code and send it over WhatsApp or Telegram to friends outside the classroom:

1. **Short-Lived Dynamic QR Tokens**:
   - The QR code contains a payload: `{ sessionId: "...", timestamp: 1730592000, hash: "HMAC..." }`.
   - The token expires within **20 seconds**.
   - The teacher's browser refreshes the QR code every 15 seconds.
2. **One-Time Use Enforcement**:
   - Once a student successfully checks in, the database unique index on `(SessionId, StudentId)` rejects any subsequent check-in attempt.
3. **Session Window Constraint**:
   - QR check-in is valid **only** while the session status is `Active`. Once the teacher clicks "End Session", the check-in window is instantly closed.

---

## 4. Multi-Tenant Isolation & Anti-Tampering Rules

### 4.1 "Never Trust Client-Supplied TeacherId"
- **Absolute Rule**: The client application is **never** permitted to supply the authoritative `TeacherId` for data mutations or queries.
- Any `TeacherId` passed in query strings, path parameters, or JSON request bodies for tenant-scoped operations is strictly ignored and overridden by the claims extractor in the backend.

### 4.2 EF Core Global Query Filter Enforcement
Every tenant-scoped query automatically appends the SQL filter:
```sql
WHERE ([Extent].[TeacherId] = @__CurrentTenantId_0)
```
Disabling query filters (`.IgnoreQueryFilters()`) is prohibited in all application code, except in the dedicated PlatformAdmin provisioning handler.

### 4.3 Database Level Access Restriction
- The application connects to SQL Server using a service account with `SELECT`, `INSERT`, `UPDATE` permissions.
- Hard `DELETE` permissions on `AttendanceRecord` and `AttendanceAuditLog` can be revoked at the SQL database user level to prevent catastrophic accidental data loss or malicious administrative tampering.

---

## 5. Audit Logging Architecture

1. **Target Actions**:
   - Manual creation/deletion of sessions.
   - Any modification to an attendance record status (e.g., changing from `Absent` to `Present`).
   - Teacher password changes.
   - Teacher-driven student password resets (records `StudentId`, `TeacherId`, `TimestampUtc`, and `ClientIpAddress`).
   - Student code regeneration.
2. **Immutable Storage**:
   - `AttendanceAuditLogs` are written in the same database transaction as the attendance record change.
   - Logs contain: `RecordId`, `TeacherId`, `OldStatus`, `NewStatus`, `Reason`, `TimestampUtc`, and `ClientIpAddress`.
   - The application provides **no API endpoint** to update or delete audit records.
