# Executive Summary -- Patient Appointment Portal

[Home](INDEX.md) > Executive Summary

---

## What It Does

The Patient Appointment Portal is a scheduling system for workers' compensation Independent Medical Examinations (IMEs). Healthcare support staff use it to book patients with doctors at specific locations and time slots, then track each appointment through a 13-state lifecycle from initial request through billing. The system is built as a multi-tenant platform where each doctor practice operates as an isolated tenant, while shared reference data (locations, appointment types, languages) is managed centrally by the host organization.

## Technology

The application is built with Angular on the frontend, ASP.NET Core on the backend, and SQL Server as the database. It uses the ABP Commercial framework for multi-tenancy, permissions, user management, and authentication (via OpenIddict). Object mapping uses Riok.Mapperly (compile-time source generation). The project includes Docker support for containerized deployment and a database migration tool for schema management.

## Current State

The core scheduling workflow is functional: staff can create doctors, generate availability slots, register patients, and book appointments. The system has 75 documentation files, 16 feature-level technical references, and 31 architecture diagrams. A comprehensive audit identified 29 tracked issues across 5 categories:

- **5 security issues** (1 critical, 3 high, 1 medium) -- secrets in source control, PII logging, unprotected endpoints, relaxed password policy, and overly permissive CORS
- **7 data integrity issues** (2 critical, 3 high, 2 medium) -- race conditions on slot booking, missing unique constraints, slot release gaps
- **10 confirmed bugs** (3 high, 5 medium, 2 low) -- inverted slot conflict detection, status changes not persisted, N+1 HTTP calls
- **7 incomplete features** (2 high, 5 medium) -- no appointment status workflow, no Claim Examiner role, placeholder tenant dashboard, missing email system, near-zero test coverage

258 automated end-to-end tests were executed on 2026-04-02, with 246 passing, 0 unexpected failures, and 5 known gaps confirmed.

## Architecture

The system runs as three services. The Angular frontend (port 4200) provides the user interface and communicates with the ASP.NET Core API backend (port 44327), which handles all business logic, data access, and authorization. A separate Authentication Server (port 44368) manages OAuth 2.0 login, token issuance, and user identity via OpenIddict. All three services must be running for the application to function, and they must start in order: Authentication Server first, then API, then Angular. The database is SQL Server (LocalDB for development), managed through Entity Framework Core with code-first migrations.

## Security Posture

This system handles protected health information (PHI) subject to HIPAA requirements -- patient names, dates of birth, medical examination types, and attorney contact details flow through the application. Current safeguards include: a permission-based authorization system with role-based access control (Admin, Doctor, Patient, Applicant Attorney, Claim Examiner), OpenIddict OAuth 2.0 authentication, multi-tenant data isolation (each tenant's data is automatically filtered), a PHI scanner hook that runs on every development tool use, and a pull request template with a HIPAA compliance checklist.

Known security gaps require remediation before any production deployment. The most critical is that secrets (database passwords, encryption keys) were committed to source control and must be rotated. PII logging is enabled by default, one API endpoint exposes user data without authorization checks, and the password policy has no complexity requirements. Full details are documented in docs/issues/SECURITY.md.

## Team and Maintenance

The project is currently maintained by a sole developer. A second developer joining the team would need to follow the onboarding guide at docs/onboarding/GETTING-STARTED.md, which covers environment setup (Windows with short path requirement due to a SQL Server native DLL limitation), service startup procedures, database migration steps, and critical constraints (such as never using the Angular dev server due to an ABP framework incompatibility). The 75-file documentation set and 16 feature-level technical references are designed to make the codebase navigable without tribal knowledge.

## Key Risks

- **ABP Framework vendor lock-in:** The application depends heavily on ABP Commercial (a licensed framework) for multi-tenancy, permissions, identity, and UI theming. The ABP license key is required for builds. If the license expires or ABP discontinues support, significant rework would be needed.
- **Angular and ABP version coupling:** Angular major version upgrades are blocked until ABP releases compatible packages. Angular 20's Vite dev server is already incompatible with ABP's dependency injection, forcing a workaround build process.
- **Test coverage gaps:** Backend test coverage: see [docs/testing/coverage-status.md](testing/coverage-status.md). Last verified 2026-04-24. No Angular component tests exist.
- **No staging or production environment:** The application runs only on localhost. There is no CI/CD pipeline deploying to a staging or production server, no environment-specific configuration, and no operational monitoring. Docker support has been added but not yet deployed to any remote environment.
