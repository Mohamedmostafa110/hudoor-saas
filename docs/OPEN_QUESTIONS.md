# Open Questions & Architectural Decision Backlog

| Document Version | 1.0.0 |
| :--- | :--- |
| **Status** | Open for Stakeholder Alignment |
| **Purpose** | Identify Ambiguities, Architectural Trade-offs & Unresolved Business Decisions |

---

## 1. High-Priority Architectural & Business Decisions

### Q-01: Student Authentication Model & Password Lifecycle (CONFIRMED)
- **Status**: **CONFIRMED DECISION**
- **Decision Details**:
  - Students authenticate using **Student Code + Password**.
  - Students cannot self-register; accounts and initial temporary passwords are generated exclusively by the teacher.
  - If a student forgets their password, their teacher executes a one-click reset from the student profile to generate a simple, human-readable temporary password (letters + numbers only, e.g., `Falcon482`, `Cedar735`).
  - Temporary passwords use a CSPRNG selecting from a neutral dictionary list and digits; they are **strictly prohibited** from being derived from student names, phone numbers, birth dates, or any personal information.
  - Passwords are strictly hashed (never plain text, never visible to teachers or PlatformAdmins).
  - Mandatory password change enforced on next login (`MustChangePassword = true`).
  - Active sessions are immediately invalidated via the `TokenVersion` claim check pattern in the database/middleware.
  - Reset event is logged in immutable audit records.
  - No OTPs, SMS, or third-party email dependencies in the MVP.

---

### Q-02: Student Code Format (PROPOSED DECISION - REQUIRES APPROVAL)
- **Context**: The product requirement is a single student login page (`StudentCode` + `Password`) without asking students to identify or select their teacher.
- **Structural Consequence**: Because there is no teacher selector on the login form, the `StudentCode` **must be globally unique across the entire platform** to enable deterministic account lookup.
- **Options**:
  - **Option A (Global Alphanumeric Format - Proposed Recommendation)**: Platform generates a 6-to-8 character alphanumeric code (e.g., `HD-893K2` or base32 `W7M-49D`).
    - *Pros*: Completely frictionless for students; single universal login screen; high collision resistance.
    - *Cons*: System generates codes to guarantee global collision resistance.
  - **Option B (Teacher-Prefixed Code)**: The code includes a teacher prefix (e.g., `T104-9842`).
    - *Pros*: Easy tenant namespace partitioning; teachers can choose custom numbers within their prefix.
    - *Cons*: Students type longer codes.
- **Proposed Decision Requiring Approval**: Adopt **Option A** with automated high-entropy code generation (`HD-XXXXXX`), combined with a teacher branded shortcut URL as a progressive convenience.

---

### Q-03: Temporary Password Expiration Window (PROPOSED DECISION - REQUIRES APPROVAL)
- **Context**: When a teacher generates a temporary password (e.g., `Falcon482`), how long should it remain valid before requiring another reset?
- **Options**:
  - **Option A (7 Calendar Days - Proposed Default)**: Gives the student ample time between weekly tutoring sessions to log in from home and set their permanent password.
  - **Option B (48 Hours)**: Tighter security window, but risks expiring if a student only attends class once a week.
  - **Option C (Teacher-Configurable)**: Teacher selects 24h, 3d, or 7d.
- **Proposed Decision Requiring Approval**: Adopt **Option A (7 Days)** as the default expiration window, pending explicit stakeholder sign-off.

---

### Q-04: Student Permanent Password Policy (PROPOSED DECISION - REQUIRES APPROVAL)
- **Context**: What complexity policy should be enforced when students change their temporary password?
- **Options**:
  - **Option A (Student-Friendly Standard - Proposed Recommendation)**: Minimum 6 characters, alphanumeric allowed, no mandatory special characters.
    - *Pros*: High usability for young K-12 students (primary/prep/secondary); low risk of repeated forgotten passwords.
    - *Cons*: Lower entropy than enterprise standards, but well protected when paired with endpoint rate-limiting and lockout.
  - **Option B (Enterprise Complexity)**: Minimum 10 characters with uppercase, lowercase, numbers, and symbols.
    - *Pros*: Strict corporate security.
    - *Cons*: High friction; will cause frequent forgotten passwords and overwhelm teachers with reset requests.
- **Proposed Decision Requiring Approval**: Adopt **Option A (Minimum 6-8 characters, simple alphanumeric)**.

---

### Q-05: Live Roster Refresh Mechanism during QR Attendance
- **The Ambiguity**: When a teacher projects the dynamic QR code in class, how does the teacher's screen reflect students scanning in real-time?
- **Options**:
  - **Option A (Smart HTTP Polling - Recommended for MVP)**: Teacher client polls `GET /api/teacher/sessions/{id}/live-roster` every 2–3 seconds while the QR modal is active.
    - *Pros*: Extremely simple, resilient, works through any proxy/firewall, zero WebSocket connection management.
    - *Cons*: Slight polling delay (1–2s), minor HTTP request overhead.
  - **Option B (ASP.NET Core SignalR / WebSockets)**: Persistent duplex WebSocket connection pushing individual check-in events.
    - *Pros*: Instantaneous tick animation as each student scans.
    - *Cons*: Additional state management, sticky session requirements if scaled across multiple backend instances, reconnection handling.
  - **Option C (Server-Sent Events - SSE)**: One-way reactive streaming from backend to teacher browser.
    - *Pros*: Lightweight unidirectional streaming.
    - *Cons*: Potential proxy buffering issues on legacy networks.
- **Architectural Recommendation**: Start with **Option A (Smart Polling)** for MVP simplicity and robustness, designed with an interface that can swap to **Option B (SignalR)** in Phase 2 if live animations are desired.

---

### Q-06: Teacher Assistants & Proctor Roles
- **The Ambiguity**: In large tutoring classes (e.g., 50–200 students in Egyptian high school tutoring), teachers frequently employ 1 to 3 "Assistants" (مساعدين) who actually manage the door, check student IDs, and take attendance while the teacher lectures. Does the MVP require an "Assistant" sub-role under the Teacher?
- **Options**:
  - **Option A (Deferred to Phase 2 - Recommended for MVP)**: Teachers share their login or run the session from a single device (or multiple simultaneous logins using the teacher credentials).
  - **Option B (MVP Inclusion of Assistant Role)**: Allow the Teacher to create secondary "Assistant" accounts with restricted permissions (can take attendance and view rosters, but cannot view financials, delete records, or change master settings).
- **Architectural Recommendation**: Defer full Assistant RBAC to Phase 2 to keep MVP lean, but design the database schema with an `OperatorId` audit column so multi-user operations under one teacher are future-proof.

---

### Q-07: Student Offline QR Scanning
- **The Ambiguity**: What happens if cellular data is unavailable inside a basement classroom or auditorium?
- **Options**:
  - **Option A (Online Only - Recommended for MVP)**: Both teacher screen and student mobile device require active internet connectivity. If student has no connection, teacher uses the Manual Roster Checklist to mark them Present.
  - **Option B (Local Offline Bluetooth / WebRTC Sync)**: Complex local ad-hoc networking.
- **Architectural Recommendation**: **Option A (Online Only)** for QR scanning, complemented by full offline caching on the **Teacher's device** for Manual Attendance marking.

---

### Q-08: Absence Notifications (WhatsApp / SMS)
- **The Ambiguity**: Is there an immediate requirement for automated notifications sent to parents when a student is marked Absent or Late?
- **Options**:
  - **Option A (Manual Export / Deferred)**: Teacher exports the absence report to Excel/CSV or copies a pre-formatted WhatsApp message link (`https://wa.me/{parentPhone}?text=...`) for one-click manual dispatch.
  - **Option B (Twilio / Meta Cloud WhatsApp API Integration)**: Automated background webhook triggers message dispatch upon session closure.
- **Architectural Recommendation**: **Option A** for MVP (with one-click "Message Parent on WhatsApp" deep-link button), deferring paid SMS/WhatsApp API gateways to Phase 2 due to carrier cost and API approval overhead.

---

### Q-09: Handling Multiple Teachers per Student
- **The Ambiguity**: In independent tutoring ecosystems, a single student often studies Arabic with Teacher A, and Physics with Teacher B. Under the "Student Code + Password" model, does a student have a separate credential pair for each teacher, or one unified account?
- **Options**:
  - **Option A (Completely Independent Credentials - Recommended)**: Student has Code `A-1234` for Teacher A, and Code `B-5678` for Teacher B. The PWA allows students to "Add another teacher" and store credentials in a local wallet.
    - *Pros*: Preserves strict multi-tenant isolation; teachers never share a student database; no cross-teacher data leakage.
    - *Cons*: Student manages two distinct codes.
  - **Option B (Global Unified Student Entity)**: Central student registry where teachers claim students by national ID or phone number.
    - *Pros*: One login for student.
    - *Cons*: Introduces massive privacy, verification, cross-tenant consent, and regulatory complexities.
- **Architectural Recommendation**: **Option A (Independent Codes with PWA Multi-Code Switcher)**.

---

### Q-10: Layered Login Rate Limiting Thresholds (PROPOSED DECISION - REQUIRES APPROVAL)
- **Context**: Students and teachers often access the application from a shared public IP or cellular NAT gateway inside a single tutoring center or school classroom.
- **Architectural Decision**: Implement layered rate limiting:
  - Account-level rate limiting on `StudentCode`: 5 consecutive failed attempts locks the specific code for 15 minutes.
  - Classroom-aware IP rate limiting: 100 requests per minute per IP (avoids blocking an entire classroom of 50-100 students on the same Wi-Fi).
  - Progressive backoff delays (500ms -> 1s -> 2s) and generic error messages.
- **Proposed Decision Requiring Approval**: Adopt these layered threshold values for MVP deployment.

---

## 2. Summary Decision Log

| Issue ID | Subject | Status | Decision / Recommendation | Target Gate |
| :--- | :--- | :---: | :--- | :---: |
| **Q-01** | Student Auth & Reset | **CONFIRMED** | Student Code + Password; simple teacher reset (`Falcon482`); DB atomic TokenVersion | Approved |
| **Q-02** | Student Code Format | **Proposed** | Global Alphanumeric (e.g. `HD-XXXXXX`) | Pre-Sprint 1 |
| **Q-03** | Temp Password TTL | **Proposed** | 7 Days default expiration | Pre-Sprint 1 |
| **Q-04** | Student Password Policy | **Proposed** | Min 6-8 chars alphanumeric (K-12 friendly) | Pre-Sprint 1 |
| **Q-05** | Live Roster Sync | **Proposed** | Smart HTTP Polling (2-3s interval) | Pre-Sprint 1 |
| **Q-06** | Assistant Role | **Proposed** | Defer to Phase 2; single teacher credential | Post-MVP |
| **Q-07** | Offline QR | **Proposed** | Online QR; Offline manual roster fallback | Pre-Sprint 1 |
| **Q-08** | Parent Absence Alerts | **Proposed** | One-tap WhatsApp deep link; automated API in P2 | Sprint 3 |
| **Q-09** | Multi-Teacher Students | **Proposed** | Isolated codes with client PWA code switcher | Pre-Sprint 1 |
| **Q-10** | Layered Rate Limiting | **Proposed** | 5 attempts/code lock + 100 req/min/IP classroom-aware threshold | Pre-Sprint 1 |
