---
id: OBS-35
title: Claim Examiner scope filter excludes appointments with no injury row, even when top-level ClaimExaminerEmail matches
severity: observation
status: open
found: 2026-05-21 hardening HRD-P8.4
flow: appointment-scope-visibility
component: src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs (GetListAsync filter for ClaimExaminer role)
---

# OBS-35 - CE scope misses injury-less appointment

> 2026-05-24: renamed from `OBS-26-...` to free `OBS-26` for the slot-gen-location-scoped-conflict observation that was filed concurrently on main during the hardening run.

## Symptom

Phase 8 scope-visibility check for `claimE1@gesco.com` (role: Claim Examiner). The Phase 5 run ended with 5 appointments in the tenant, all of which have top-level `AppAppointments.ClaimExaminerEmail = 'claimE1@gesco.com'`. The suite's expected scope rule (HARDENING-TEST-SUITE.md line 668):

> `ClaimExaminerEmail = me OR injury.ClaimExaminerEmail = me`

SQL cross-check (top-level only):
```sql
SELECT RequestConfirmationNumber
FROM AppAppointments
WHERE ClaimExaminerEmail IN ('claimE1@gesco.com','claime1@gesco.com')
-- A00001 .. A00005 (5 rows)
```

API result (`GET /api/app/appointments?MaxResultCount=100` as claimE1):
```
totalCount: 4
confNums: [A00001, A00002, A00003, A00004]
```

A00005 is missing. Of the 5 appointments, only A00005 has no `AppAppointmentInjuryDetails` row (its booking flow hit [[BUG-031]] -- 403 on POST `/api/app/appointment-injury-details` for Clinic Staff role -- and the injury sub-resource was not persisted). The 4 visible appointments all have at least one injury row.

So the operative scope rule appears to be more restrictive than the suite's stated `OR`: it effectively requires `injury.ClaimExaminerEmail = me`. Top-level `ClaimExaminerEmail` alone is not sufficient.

## Hypothesis

1. **Scope filter uses injury-table EXISTS check only.** The filter is something like `WHERE EXISTS (SELECT 1 FROM AppAppointmentInjuryDetails inj WHERE inj.AppointmentId = a.Id AND inj.ClaimExaminerEmail = @me)`. The top-level `ClaimExaminerEmail` is set on POST for SPA UI convenience (for the suite's R2 probes / report queries) but the scope filter ignores it. Fix: change to `OR`-shaped check matching the suite's rule.

2. **Cascade from BUG-031.** A00005's missing injury row is the only reason it falls out. If the injury-row creation were fixed (Clinic Staff allowed to POST injury details), A00005 would have an injury row with `ClaimExaminerEmail = claimE1@gesco.com` and the scope filter (whatever shape) would let it through. Fix: address BUG-031 first; this OBS likely auto-resolves.

3. **Intentional design.** CE legitimately sees only injury-linked appointments; top-level `ClaimExaminerEmail` is a separate concept (e.g., "CE listed on the appointment but not tied to an injury yet, so not yet visible to them"). If intentional, the suite's rule (Phase 8.4) is wrong and should be updated to match.

Most likely (1) combined with (2): the filter is injury-only, and BUG-031 created the discrepancy by leaving a top-level-only appointment with no injury row.

## Reproduction

1. Bootstrap fresh stack.
2. Have clistaff1 (Clinic Staff) book an appointment via `/appointments/add` deep-link for an existing patient, filling AA + DA + claim modal with CE. The injury POST fails 403 (BUG-031) but the appointment row gets created with top-level CE email populated.
3. Log in as the CE listed on that appointment.
4. `GET /api/app/appointments?MaxResultCount=100` -> appointment is NOT in the result.

## Recommended fix

Step 1: Locate the scope filter:
```bash
grep -rn "ClaimExaminer\|HasClaimExaminerScope" src/HealthcareSupport.CaseEvaluation.Application/
```

Step 2: Identify whether the current filter is injury-only or OR-shaped. Compare with the suite's expectation.

Step 3: Decide intent:
- If "injury-only" is intentional: update suite docs.
- If "OR top-level/injury" was intended (most likely given the suite's rule): change filter to:
  ```csharp
  query.Where(a =>
      a.ClaimExaminerEmail == currentUser.Email ||
      a.InjuryDetails.Any(inj => inj.ClaimExaminerEmail == currentUser.Email)
  );
  ```

Step 4: Add an integration test that asserts both shapes are visible to the CE.

## Functional impact

- A CE listed on an appointment but whose appointment has no injury row (e.g., due to BUG-031) doesn't see that appointment in their dashboard. The patient/AA/DA are unaffected.
- In practice this is rare today because most bookings go through the SPA form which always creates an injury row. The combination of (CE-as-third-party + Clinic Staff booker + BUG-031 active) is the only way this surfaces.
- Not a HIPAA leak (no data going to wrong party). It's a missed-visibility, not a misrouting.

## Related

- [[BUG-031]] -- clinic-staff 403 on injury endpoint (the underlying cause of the missing injury row).
- [[BUG-030]] -- internal-staff auto-approve (same scenario; if A00005 had been Pending, the issue would still surface once approved).
- Suite Phase 8.4 expected rule (HARDENING-TEST-SUITE.md line 668) -- doc may need adjustment depending on the design intent.
