---
id: OBS-32
title: When attorney logs in to book, AA section prefills ATTORNEY NAME with first name only (not full name)
severity: observation
status: open
found: 2026-05-23 hardening HRD-P3.2
flow: booking-form-prefill
component: angular/src/app/appointments/appointment-add.component.ts (currentUser-to-AA-section binding)
---

# OBS-32 - AA section prefill uses only first name

## Symptom

When `appatty1@gesco.com` (Applicant Attorney, registered with first name
"Marcus" and last name "Bennett") logs in and opens `/appointments/add`,
the Applicant Attorney section is auto-enabled (per the runbook lesson
that DA is auto-on when a DA logs in - same for AA when an AA logs in)
and the `ATTORNEY NAME` field is prefilled with:

```
Marcus
```

instead of the expected full name:

```
Marcus Bennett
```

The `ATTORNEY EMAIL` field correctly prefills to `appatty1@gesco.com`.
Other fields (firm name, address) are empty.

## Why this is observation-worthy

The first-name-only prefill suggests the prefill source is a single
`firstName` claim rather than a composed `fullName` or `firstName + ' '
+ lastName`. The user has to manually edit the field to add their last
name OR the form serializer downstream might happen to accept the
first-name-only value (depending on validation).

## Repro

1. Log in as `appatty1@gesco.com` (registered with first=Marcus,
   last=Bennett).
2. Navigate to `/appointments/add`.
3. Observe the Applicant Attorney Details section:
   - Include checkbox: checked (auto-on).
   - ATTORNEY NAME placeholder field: contains "Marcus" (first name only).
   - ATTORNEY EMAIL: contains "appatty1@gesco.com".

## Recommended fix

In the booking form's user-prefill binding, compose:

```ts
attorneyName: `${currentUser.firstName} ${currentUser.lastName}`.trim()
```

Or split the ATTORNEY NAME field into two: FIRST NAME and LAST NAME,
matching the rest of the form's two-field convention. (Inspecting the
view page, the form DOES have separate `firstName`/`lastName` fields in
the AA section in some renders - so the inconsistency may be that the
ADD form uses a single field while the VIEW form uses two.)

## Functional impact

Cosmetic. The booking succeeds with the first-name-only value (no
validation blocks it). Downstream emails to the attorney use the same
"Marcus" as the displayed name, which may look odd in formal
correspondence.

## Related

- HRD-P3.2 (the scenario that surfaced this).
- Booking-form-structure observations (OBS-17, OBS-18).
