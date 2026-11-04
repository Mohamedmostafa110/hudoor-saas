# Hudoor (حضور) — Smart Multi-Tenant Attendance SaaS 🎓⚡

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![React 19](https://img.shields.io/badge/React-19-61DAFB?logo=react&logoColor=black)](https://react.dev/)
[![TypeScript](https://img.shields.io/badge/TypeScript-5.7+-3178C6?logo=typescript&logoColor=white)](https://www.typescriptlang.org/)
[![Vite](https://img.shields.io/badge/Vite-6.0+-646CFF?logo=vite&logoColor=white)](https://vitejs.dev/)
[![Tests](https://img.shields.io/badge/Tests-128%20Passed-10B981?logo=checkmarx&logoColor=white)](./tests)
[![Security](https://img.shields.io/badge/Security-0%20Vulnerabilities-10B981)](#security--architecture)

> **منصة الحضور الذكي المخصصة للمعلمين المستقلين والمراكز الأكاديمية**  
> A purpose-built, high-security multi-tenant SaaS engineered for private tutors, educational centers, and academies with live cryptographic dynamic QR codes, fast-tap roster grids, and seamless student check-in via mobile cameras.

---

## 🌟 Key Highlights & Features

* **🛡️ Strict Multi-Tenant Isolation:** Complete logical tenant boundaries scoped per teacher (`TenantId`) enforced across all queries, repositories, and foreign keys.
* **⚡ 20-Second Cryptographic Dynamic QR:** Rotating HMAC-SHA256 tokens refreshed every 20 seconds with CSPRNG security salt and 40-second drift tolerance to completely eliminate photo sharing/cheating.
* **📷 Native Camera QR Scanner:** Built-in mobile & desktop web camera scanner with real-time feedback, laser targeting animation, and audio chimes for frictionless student attendance.
* **⏱️ 48px Fast-Tap Roster:** High-speed bulk and manual attendance marking grid designed for teachers handling large classes in seconds.
* **📊 Comprehensive Analytics & 2D Matrix:** Real-time KPI summaries, automated at-risk student detection, and UTF-8 BOM Excel/CSV reporting tailored for Arabic typography.
* **🔄 Academic Year Rollover Wizard:** Zero data loss workflow to promote, repeat, or archive students into new academic terms with full historical audit trails.
* **🎨 Modern Design System:** Bilingual (Arabic RTL primary, English LTR secondary) with Warm Gold (`#C59B27`) Light Theme & Midnight Navy (`#0B1329`) Dark Theme.

---

## 🏗️ Technology Architecture

```
Hudoor SaaS
├── src/
│   ├── backend/Hudoor.Api/       # ASP.NET Core Web API (.NET 10)
│   │   ├── Controllers/          # Auth, Teacher, Student, Sessions, Reports, Rollover
│   │   ├── Data/                 # EF Core 10, Entity Configurations, DemoDataSeeder
│   │   ├── Security/             # JWT, Password Hasher, RateLimiter, Token Revocation
│   │   └── Services/             # QR Service, Export Service, Code Generators
│   └── frontend/hudoor-web/      # React 19 + TypeScript + Vite PWA
│       ├── src/components/       # Navbar, CameraScanner, LoginView, Modals
│       ├── src/views/            # TeacherDashboard, LiveSession, FastTap, StudentPortal
│       └── src/context/          # AuthContext, ThemeContext, LanguageContext
├── tests/Hudoor.Api.Tests/       # 128 Automated Integration & Unit Tests (100% Pass)
└── docker-compose.yml            # Multi-container orchestration (API + Web + SQL Server)
```

---

## 🚀 Quick Start (Local Development)

### Prerequisites
* [.NET 10 SDK](https://dotnet.microsoft.com/)
* [Node.js 20+](https://nodejs.org/)
* [SQL Server 2022 / LocalDB / Express](https://www.microsoft.com/sql-server)

### 1. Clone & Setup
```bash
git clone https://github.com/Mohamedmostafa110/Hudoor.git
cd Hudoor
```

### 2. Run Database Migrations & Seed Demo Data
```bash
dotnet ef database update --project src/backend/Hudoor.Api
dotnet run --project src/backend/Hudoor.Api -- --seed-demo
```

### 3. Run Backend API
```bash
dotnet run --project src/backend/Hudoor.Api --urls "http://localhost:5236"
```

### 4. Run Frontend Web
```bash
cd src/frontend/hudoor-web
npm install
npm run dev
```
Open **[http://localhost:3000](http://localhost:3000)** in your browser!

---

## 🐳 Docker Deployment

Run the complete full-stack environment (Frontend + Backend + SQL Server) with a single command:

```bash
docker compose up -d --build
```

---

## 🔑 Demo Credentials

| Role | Username / Code | Password | Description |
| :--- | :--- | :--- | :--- |
| **Teacher** | `teacher_demo` | `TeacherPass2026!` | Access Teacher Dashboard, Live QR Projector, Fast-Tap Roster |
| **Student** | `HD-849201` | `StudentPass2026!` | Student Portal, Live Camera QR Check-in, Attendance Analytics |
| **Platform Admin** | `Mohamed1652006#` | `MoMs1652006$@` | Provision & manage teacher workspaces |

---

## 📜 Documentation

Full architectural specifications and security designs are located in [`docs/`](./docs/):
* [`PRODUCT_REQUIREMENTS.md`](./docs/PRODUCT_REQUIREMENTS.md) — Functional specifications & UI/UX criteria
* [`SECURITY_REQUIREMENTS.md`](./docs/SECURITY_REQUIREMENTS.md) — Threat models, token invalidation & tenant isolation
* [`DEPLOYMENT_READINESS_REPORT.md`](./docs/DEPLOYMENT_READINESS_REPORT.md) — Pre-deployment verification audit results

---

## ⚖️ License
Proprietary / MIT — Developed with pride by [Mohamed Mostafa](https://github.com/Mohamedmostafa110).
