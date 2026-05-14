---
id: SEED-2
title: DemoDoctorDataSeedContributor doesn't exist yet
severity: n/a
status: needs-rehydration
found: 2026-05-13
flow: data-seeding
component: Domain/DoctorManagement/* (new seed contributor needed)
---

# SEED-2 — DemoDoctor seed contributor missing

## Severity
n/a (data seeding gap)

## Status
**Needs rehydration / not yet implemented.**

## What's known from earlier session
- NEW has no `DemoDoctorDataSeedContributor`.
- Prior session inline-SQL-inserted 1 Demo Doctor + 42 availabilities; these get wiped on every `docker compose down -v`.
- Adrian wants doctors seeded automatically so testing workflows can begin from a known state without manual SQL.

## OLD parity
Per `InternalUsersDataSeedContributor.cs:19-21`: *"Doctor is a non-user reference entity managed by Staff Supervisor; no Doctor user role exists."* So in NEW, doctors are CRUD'd via the Doctor Management UI by a Supervisor. A demo seed should populate the UI's "starting state."

## Recommended fix
Write `DemoDoctorDataSeedContributor : IDataSeedContributor` that:
1. Per-tenant only (gated on `context.TenantId != null`).
2. Development-only (gated on `ASPNETCORE_ENVIRONMENT=Development`).
3. Idempotent (skip if any AppDoctors row already exists in this tenant).
4. Creates 1-2 demo doctors with:
   - First Name / Last Name / License number / Email
   - Preferred Locations (link to seeded Demo Clinic North/South)
   - Appointment Types (AME, QME, Re-Evaluation, Consultation)
   - Availabilities: 5-10 dates per type, 2-3 slots per date, spread across the next 30-60 days

## To do
- Confirm with Adrian which appointment types and how many doctors to seed.
- Confirm whether the Doctor Management UI surface should be tested first with a manual creation walk (to discover UI gaps) before the seed contributor is written.
