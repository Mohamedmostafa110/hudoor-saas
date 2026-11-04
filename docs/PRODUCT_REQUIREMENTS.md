# Product Requirements Document (PRD)

| Document Version | 1.0.0 |
| :--- | :--- |
| **Status** | Discovery Draft - Pending Stakeholder Review |
| **Target Platform** | Web & Progressive Web App (PWA) |
| **Primary Domain** | Independent Tutoring & K-12 Supplemental Education |

---

## 1. Product Vision & Problem Statement

### 1.1 The Problem
Independent teachers, private tutors, and small tutoring centers struggle with attendance management:
1. **Paper rosters and Excel spreadsheets** are error-prone, cumbersome to maintain during class, and easily lost or damaged.
2. **Traditional School ERPs / LMSs** (e.g., Google Classroom, Canvas, Blackboard) are bloated, complex, designed for bureaucratic institutions, and require institutional email accounts that private tutoring students do not possess.
3. **Session irregularity**: Tutoring schedules are fluid. Sessions may be rescheduled, extra review sessions added, or combined groups organized on demand without a fixed 9-to-5 timetable.
4. **Student authentication friction**: Requiring young students to register with email addresses and passwords creates massive onboarding friction, forgotten credentials, and disrupted classroom starts.

### 1.2 The Solution
**Hudoor** is an agile, multi-tenant attendance SaaS tailored specifically for independent educators. It enables:
- **Instant Teacher Isolation**: Every teacher manages their own universe of subjects, groups, and students without cross-tenant exposure.
- **Fluid Session Attendance**: On-demand session creation with immediate QR code projection or fast-tap manual marking.
- **Secure Yet Frictionless Student Access**: Students authenticate using their unique **Student Code** and **Password** (provisioned by the teacher), with self-service password changes and simple teacher-driven resets.
- **Bilingual Arabized Experience**: Deep native support for Arabic (RTL) alongside English (LTR), guaranteeing that student and subject names in Arabic maintain full typographic integrity.

---

## 2. User Personas

### 2.1 PlatformAdmin (Platform Owner)
- **Role**: Super-administrator of the multi-tenant SaaS.
- **Goals**: Onboard new teachers, manage teacher subscriptions/licenses, provision credentials, monitor platform health and tenant activity, deactivate non-paying or suspended accounts.
- **Pain Points**: Needs a central dashboard to provision teachers without touching raw database tables.

### 2.2 Teacher (Tenant Admin)
- **Role**: Independent educator or private center instructor.
- **Goals**: Create subjects and groups, register students, generate unique student access codes, launch sessions on the fly, take attendance via QR code or manual checklist, inspect historical trends, and advance students across academic years.
- **Pain Points**: Wasting 15 minutes of an 80-minute lecture calling names; tracking make-up sessions across different groups; managing grade rollovers without losing past attendance records.

### 2.3 Student (End Consumer)
- **Role**: K-12 or university student attending the teacher’s classes.
- **Goals**: Log in easily with Student Code and Password, scan the teacher's session QR code, change personal password, view attendance history and percentage, and request a quick password reset from the teacher if forgotten.
- **Pain Points**: Requiring institutional emails or complex OTP verification flows; forgetting complex passwords; having no direct way to track their attendance standing.

---

## 3. Detailed Functional Requirements

### Module 1: Platform Administration & Tenant Provisioning
- **FR-ADM-01**: PlatformAdmin can log into a dedicated admin portal using administrative credentials.
- **FR-ADM-02**: PlatformAdmin can create a new Teacher account by specifying: Full Name, Email, Unique Username, Initial Password, Phone Number, and Optional Center/Brand Name.
- **FR-ADM-03**: PlatformAdmin can activate, suspend, or deactivate teacher accounts. Suspended accounts immediately reject subsequent API calls and invalidate active sessions.
- **FR-ADM-04**: PlatformAdmin can trigger an administrative password reset for a teacher.
- **FR-ADM-05**: PlatformAdmin can view high-level platform metrics: total teachers, active teachers, aggregate sessions created, total students registered.

### Module 2: Teacher Workspace & Authentication
- **FR-TCH-01**: Teacher logs in using Username and Password.
- **FR-TCH-02**: First-login workflow prompts teacher to update their temporary password.
- **FR-TCH-03**: Teacher can reset their own password via authenticated settings.
- **FR-TCH-04**: Teacher dashboard displays:
  - Quick action: "Start New Session"
  - Summary metrics: Active Students, Active Groups, Sessions Conducted This Month, Average Attendance Rate
  - Recent sessions list with quick-edit shortcuts
  - Alerts: Students with consecutive absences (threshold configurable).

### Module 3: Academic Structure (Years, Terms, Subjects, Groups)
- **FR-ACA-01**: **Academic Years & Terms**:
  - Teacher defines the Active Academic Year (e.g., `2026/2027`) and optional Terms/Semesters (e.g., `Term 1`, `Term 2`).
  - System supports creating new Academic Years while locking or archiving completed years.
- **FR-ACA-02**: **Subjects**:
  - Teacher can create, edit, and archive subjects (e.g., "Arabic - First Secondary", "Religion - Second Secondary").
  - Each subject is tied strictly to the authenticated `TeacherId`.
- **FR-ACA-03**: **Groups / Classes**:
  - Teacher creates groups attached to a Subject and Academic Year (e.g., "Group A - Tuesday 4PM", "Group B - Friday 10AM").
  - Groups have customizable capacities, assigned meeting locations (physical or online), and descriptive labels.
- **FR-ACA-04**: **Student Roster Management & Provisioning**:
  - Teacher registers students individually with: Full Name, Phone Number, Parent Phone Number, Student Code, Grade/Level, and Notes.
  - Students **cannot** self-register under any circumstances.
  - **Student Code Uniqueness**: Because students log in on a single global login screen without selecting a teacher, the `StudentCode` must be **globally unique across the entire platform** (Proposed Recommendation: 6–8 character high-entropy alphanumeric format such as `HD-8492` or base32 `W7M-49D`).
  - Upon creation, the system auto-generates a unique Student Code and a simple, readable initial temporary password (e.g., `Falcon482` or `Cedar735`) using letters and digits only, without confusing characters (`0`, `O`, `1`, `I`, `l`).
  - **Generation Rule**: Temporary passwords are generated strictly using a Cryptographically Secure Pseudorandom Number Generator (CSPRNG) selecting from a curated list of neutral English/transliterated words plus digits. They are **never** derived from student names, phone numbers, birth dates, or any personal information.
  - The teacher shares this initial credential pair with the student.
  - Teacher can assign students to one or more groups.
  - Teacher can transfer a student from Group A to Group B at any time without altering attendance recorded while the student was in Group A.
  - System generates printable/exportable student cards containing the Student Name, Code, and barcode/QR.
- **FR-ACA-05**: **Teacher-Driven Student Password Reset**:
  - If a student forgets their password, the teacher can click **"Reset Password"** on the student's profile.
  - The system generates a new, secure, easy-to-share temporary password (e.g., `Swift924`).
  - Format constraint: Letters and numbers only, no special symbols, no ambiguous characters (`0/O`, `1/I/l`).
  - Generated via CSPRNG (strictly independent of student name, phone, or any personal data).
  - The temporary password is displayed to the teacher once to share with the student, and immediately hashed in the database.
  - Neither the teacher nor the PlatformAdmin can ever view or recover an existing or active student password.
  - **Session Invalidation**: All existing student sessions are invalidated atomically within the database transaction by incrementing the student's `TokenVersion`. Every authenticated student API request validates the token claim against the current database value, causing existing JWTs with the prior token version to be rejected on their immediate next request without relying on stale caches or external Redis infrastructure.
  - The temporary password is flagged with `MustChangePassword = true` and has an expiration window (**Proposed Default: 7 days**, subject to explicit client approval).
  - The reset event is permanently recorded in the audit log.
  - The flow strictly avoids email verification, SMS gateways, OTPs, or third-party dependencies.

### Module 4: Session & Attendance Management
- **FR-ATT-01**: **On-Demand Session Creation**:
  - Teacher can spin up an attendance session anytime by selecting: Group (or multiple combined groups), Subject, Session Date & Time, and Session Title/Topic (e.g., "Grammar Revision Lecture 4").
  - No rigid recurring schedule is enforced.
- **FR-ATT-02**: **Attendance Statuses**:
  - The system supports 4 primary attendance states:
    1. **Present (حاضر)**
    2. **Absent (غائب)**
    3. **Late (متأخر)** (with optional minutes late field)
    4. **Excused (معذور)** (with optional excuse reason note)
- **FR-ATT-03**: **Manual Attendance Capture**:
  - Grid / List roster view sorted alphabetically by student name or student code.
  - One-tap quick actions: "Mark All Present", "Invert Attendance", or individual toggle chips.
  - Auto-saving capability with visual sync feedback.
- **FR-ATT-04**: **QR Code Attendance Flow**:
  - When the teacher opens a live session, the system displays a high-contrast dynamic QR Code (projectable on screen/projector or shown on tablet).
  - The QR Code encodes a signed, time-decaying session check-in token (regenerated every 15–30 seconds to prevent photo sharing).
  - Student initiates scanner:
    1. Student authenticates using their unique `Student Code` and `Password` (or has an active authenticated session).
    2. Student scans the displayed QR code via the in-app scanner.
    3. Backend verifies the student belongs to the teacher's active group for this session.
    4. Backend validates the token signature, check-in window, and records the student as **Present**.
    5. Teacher's live dashboard receives real-time / polled update reflecting the check-in.
- **FR-ATT-05**: **Post-Session Modification & Auditing**:
  - Teachers can reopen, edit, or adjust attendance records after a session has closed.
  - Every change to an attendance record creates an audit entry recording: `PreviousStatus`, `NewStatus`, `ModifiedByTeacherId`, `TimestampUtc`, and `ModificationReason`.

### Module 5: Academic Year Rollover & Progression
- **FR-ROL-01**: Teacher can trigger the "Start New Academic Year" wizard.
- **FR-ROL-02**: Bulk student progression actions:
  - **Promote**: Advance student to next academic grade/stage (e.g., Grade 10 -> Grade 11).
  - **Repeat**: Retain student in current grade level for the new year.
  - **Archive / Graduated / Departed**: Mark student inactive for the new year without deleting their historical attendance records or profile.
- **FR-ROL-03**: **Zero Data Loss Guarantee**:
  - All past academic years, historical group enrollments, session logs, and individual attendance records remain intact and queryable in historical reports.

### Module 6: Student Portal & Self-Service
- **FR-STU-01**: **Authentication & Access Control**:
  - Students access the portal via web browser or installed PWA.
  - Authentication requires **Student Code** + **Password**.
  - Students **cannot** register independently; accounts are provisioned exclusively by their teacher.
- **FR-STU-02**: **Mandatory Temporary Password Change**:
  - Upon first login (or after a teacher-initiated password reset), if `MustChangePassword` is true, the student is restricted from all portal actions until they set a new personal password.
  - Student sets a new password adhering to standard complexity (minimum 6-8 characters).
- **FR-STU-03**: **Self-Service Password Change**:
  - Authenticated students can change their password at any time from their portal profile/settings.
  - Requires submitting the current password and the new password.
- **FR-STU-04**: **Attendance Dashboard**:
  - Student views their profile summary (Student Code, Assigned Groups, Teacher Name).
  - Displays attendance statistics: Total Sessions, Present count, Absent count, Late count, Excused count, and overall Attendance Percentage.
  - Historical timeline of attended sessions with date, topic, and status.
- **FR-STU-05**: **Self-Service QR Check-in**:
  - Built-in camera scanner component within the PWA for rapid check-in during active class sessions.

---

## 4. Internationalization & Localization (i18n)

| Requirement Code | Description | Implementation Standard |
| :--- | :--- | :--- |
| **I18N-01** | Dual Language Support | Native Arabic (`ar-EG` / Modern Standard Arabic) and English (`en-US`). |
| **I18N-02** | Directionality & Layout | Automatic switching between RTL (`dir="rtl"`) and LTR (`dir="ltr"`) layout engines. |
| **I18N-03** | Data vs. UI Decoupling | System UI labels switch with language selection; user-created data (e.g., student names in Arabic) are never translated or transliterated. |
| **I18N-04** | Numeral Formatting | Configurable preference for Western Arabic numerals (`0-9`) or Eastern Arabic numerals (`٠-٩`). |
| **I18N-05** | Date Formatting | Gregorian calendar support with locale-appropriate date strings (e.g., `3 نوفمبر 2026`). Optional Hijri display for cultural alignment. |

---

## 5. Design System & Visual Requirements

### 5.1 Color Palette & Theme Specification
- **Light Theme**:
  - **Primary Accent**: Warm Gold / Amber Gold (`#C59B27` / `#D4AF37` / `#B8860B`) conveying prestige, clarity, and focus.
  - **Backgrounds**: Elegant neutral cream / warm off-white (`#FDFBF7`, `#F7F5F0`, `#EFECE6`).
  - **Text**: Deep slate / charcoal (`#1C1917`, `#292524`).
- **Dark Theme**:
  - **Primary Accent**: Crisp Light Blue / Ice Blue (`#38BDF8` / `#60A5FA`).
  - **Backgrounds**: Deep Dark Navy / Midnight Slate (`#0B1329`, `#0F172A`, `#1E293B`).
  - **Text**: Pure white & off-white mist (`#F8FAFC`, `#E2E8F0`).
- **Strict Prohibitions**:
  - **NO Purple** (avoids standard generic SaaS template aesthetic).
  - **NO Neon Green** (avoids garish or unprofessional cyber-look).
  - **NO Generic AI Template Styling** (no bubbly cards with floating rainbow gradients).
  - **NO Excessive Animations** (micro-interactions limited to `<200ms` ease transitions for snappy classroom use).

### 5.2 Accessibility & Mobile Optimization
- **PWA First**: The application must be installable on iOS and Android homescreens via standard PWA manifest.
- **Touch-First Controls**: Attendance roster buttons must have a minimum touch target of `48px x 48px` to allow rapid one-handed tapping during live lectures.
- **High-Contrast Readability**: Essential when viewing projected QR codes under classroom lighting conditions.

---

## 6. Non-Functional Requirements (NFRs)

- **Performance**: Roster loading for a class of 200 students must complete in `< 500ms`.
- **Concurrency**: QR check-in endpoint must comfortably handle 100 simultaneous scans in a 60-second burst window per teacher without rate degradation.
- **Reliability & Offline Tolerance**: The PWA should cache recent session state locally so teachers can continue recording manual attendance if classroom Wi-Fi temporarily drops, syncing upon reconnection.
- **Auditability**: 100% of attendance modifications made after session creation must be immutable in the audit table.

---

## 7. Commercial Service Deliverable Covenant (ميثاق الالتزام التجاري وتعهدات الخدمة)

> **Binding Principle**: Every single feature, metric, and commitment published in the commercial service offering (e.g. on freelancing platforms like Nafezly) is an immutable engineering requirement. The platform must rigorously deliver each promised capability without compromise.

| Promised Client Commitment (وعد الخدمة للعميل) | Technical Implementation & Architectural Enforcement | Acceptance Verification Gate |
| :--- | :--- | :--- |
| **1. تفعيل فوري لمساحة عمل سحابية معزولة في يوم أو أقل**<br>(Cloud Multi-Tenant Workspace Provisioning) | PlatformAdmin API + Teacher Tenant seeding. Isolated `TeacherId` scope with branded workspace (Name, Center, Groups). | Teacher receives working credentials, logs in, sees active academic year, configured groups, and isolated database context immediately. |
| **2. باركود QR ديناميكي يتجدد كل 20 ثانية لمنع الغش**<br>(Dynamic Rotating 20s TTL QR Check-in) | Backend generates cryptographically signed HMAC-SHA256 time-decaying tokens with 20s TTL. Rotates automatically via SignalR/Polling on teacher projector screen. | Photos taken of the QR code older than 20 seconds are strictly rejected by the backend API with `TOKEN_EXPIRED`. |
| **3. شبكة التحضير السريع باللمس (Fast-Tap Grid)**<br>(1-Touch Manual Attendance Marking) | 48px+ touch targets on Mobile/Tablet PWA. Instant toggle chips: Present, Late, Excused, Absent + "Mark All Present" (تحضير الكل) bulk action. | Entire roster of 50 students can be marked in under 15 seconds with optimistic UI updates and instant autosave. |
| **4. إدارة حسابات الطلاب بدون إيميل أو رسائل مكلفة**<br>(Zero-Email / Zero-SMS Student Provisioning) | Teacher registers students directly (No self-registration). System auto-generates globally unique `StudentCode` (e.g. `HD-XXXX`) and human-readable temporary password (CSPRNG, letters+digits, e.g. `Falcon482`). Printable card with barcode/QR. | Students log in deterministically on unified login screen using Student Code + Password without selecting teacher or verifying email/phone. |
| **5. حل جذري لنسيان كلمة المرور في 5 ثوانٍ من المعلم**<br>(5-Second Teacher-Driven Password Reset) | 1-click "Reset Password" button on student profile. Generates readable temporary password (e.g. `Swift924`). Atomically increments `TokenVersion` in DB (invalidating all previous active JWT sessions immediately) and sets `MustChangePassword = true`. | Existing active student sessions on other phones are instantly rejected on the next request. Student must set a new permanent password on next login. |
| **6. تشفير تام لكلمات المرور بـ Argon2id**<br>(Zero-Knowledge Plaintext Privacy) | Passwords hashed using Argon2id / PBKDF2 with unique salts. No endpoint or database view exposes plaintext. | Neither the teacher, nor the PlatformAdmin, nor database logs can read or expose active student passwords. |
| **7. مساحة عمل معزولة وآمنة (Multi-Tenant Isolation)**<br>(Complete Data Privacy & Zero Leakage) | EF Core Global Query Filter (`e => e.TeacherId == _tenantContext.TeacherId`) enforced on every database query and command. | Absolute mathematical guarantee: Teacher A can never query, see, or mutate students, groups, or sessions belonging to Teacher B. |

