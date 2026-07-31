---
id: OBS-39
title: ExternalUsersDataSeedContributor's patient@falkinstein.test creates a Patient row with empty FirstName + LastName
severity: observation
status: open
found: 2026-05-25 (Mon AM hardening, post-#250 merge)
flow: tenant-seed-contributors
component: test/HealthcareSupport.CaseEvaluation.TestBase/Data/ + ExternalUsersDataSeedContributor in DbMigrator
---

# OBS-39 - Seed patient row missing FirstName + LastName

## Symptom

After `docker compose up -d --build` against a fresh volume, the
Patients list (`/doctor-management/patients`) shows 2 rows:

| FirstName | LastName | Email |
|---|---|---|
| (empty) | (empty) | patient@falkinstein.test |
| Alex | Patient | patient1@gesco.com |

The first row is from seed; the second is the one Adrian registered
during the hardening run.

The seed contributor creates the AbpUser `patient@falkinstein.test`
with role=Patient, and apparently also creates the `AppPatients`
row but does NOT carry over Name + Surname. The AbpUser has the
Name set ("Default Patient" or similar) but the AppPatients row
gets nothing.

## Expected

Either:
1. The seed contributor populates the AppPatients row with the
   AbpUser's Name + Surname, OR
2. The seed doesn't create an AppPatients row for this user (let
   it lazy-create on first booking).

## Reproduction

```sql
-- After docker compose up -d --build with empty volume:
SELECT FirstName, LastName, Email FROM AppPatients WHERE IsDeleted=0;
-- Returns:
-- (empty)   (empty)   patient@falkinstein.test
```

## Functional impact

Low. The blank row is visible in the Patients admin list but does
not block any flow. For Tuesday demo: if you navigate to Patients
during the demo, the blank row looks unprofessional. Fix is to
either patch the seed contributor or filter empty-name rows in the
list query (the former is cleaner).

## Related

- `[[OBS-38]]` (existing-patient dropdown also doesn't prepopulate
  demographics -- consistent pattern of seed/source data not
  flowing to the Patient row consistently).
