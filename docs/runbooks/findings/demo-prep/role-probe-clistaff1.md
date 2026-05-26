---
title: Clinic Staff (clistaff1) live role probe
date: 2026-05-25
status: ready
audience: Adrian (presenter)
---

# Clinic Staff role probe -- clistaff1@gesco.com

Verified live 2026-05-25 23:55 PT. Demo Flow 3 logs in as
clistaff1, so this is the most critical role to validate.

## What clistaff1 can do (demo-relevant)

| Surface | State |
|---|---|
| /dashboard | OK -- same 13 cards as Staff Supervisor |
| /appointments | OK -- 3 appointments visible |
| /appointments/view/A00003 (Pending) | OK -- Approve + Reject + Save + Upload + Regenerate buttons all shown |
| /appointments/view/A00001 (Approved) | OK -- Approve/Reject hidden, Save/Upload/Regenerate shown |
| /appointments/view/A00002 (Rejected) | OK -- Approve/Reject hidden, Save/Upload/Regenerate shown |
| Approve modal | Opens; Responsible User dropdown shows 6 internal users |
| /users/invite | OK -- form renders, no 403 |
| Nav menu | Full internal menu (Appointment Mgmt, Configurations, Doctor Mgmt, User Mgmt) |

## Findings

### Approve modal Responsible User dropdown

Lists 6 internal staff including legacy seed accounts:
- admin@abp.io
- admin@falkinstein.test
- clistaff1@gesco.com
- stafsuper1@gesco.com
- staff@falkinstein.test
- supervisor@falkinstein.test

**Demo tactic:** Pick `clistaff1@gesco.com` (current user) to keep
selection self-evident. Avoid the `.test` and `@abp.io` rows.

If audience asks "what's admin@abp.io?": "That's a default ABP seed
account; we'd remove those from production deployments. The list
is currently unfiltered."

### Upload Documents button visible but BUG-037 risk

Per permission audit, Clinic Staff has `AppointmentDocuments.Default`
but NOT `.Create`. Button renders but clicking it would 403 on the
upload POST.

**Demo tactic:** Use stafsuper1 for Flow 4 (document upload +
regenerate), NOT clistaff1. Or alternatively: use clistaff1 to
demo Approve, then logout and login as stafsuper1 to demo Flow 4
upload.

### What clistaff1 CANNOT do

- AppointmentDocuments.Create (BUG-037)
- AppointmentChangeRequests.Approve / .Reject (only SS + ITA)
- Any .Delete on Appointment / Patient / etc.
- InternalUsers.Create (IT-Admin only)

## Demo readiness verdict

clistaff1 is **ready for Flow 3 (Approve)** but **NOT for Flow 4
(Document Upload)** unless we accept the 403 or switch user mid-
demo. Recommended flow:

1. Flow 2 + 3 as clistaff1 (login, dashboard, appointments,
   approve A00003).
2. Logout + login as stafsuper1.
3. Flow 4 as stafsuper1 (upload docs to A00003 just-approved,
   regenerate packets).
4. Flow 5 as stafsuper1 (invite external user).

OR consolidate: do all of Flow 2-5 as stafsuper1 to avoid the
mid-demo logout, accept the slight script deviation that approval
runs as Staff Supervisor rather than Clinic Staff.

**Recommendation: ALL flows as stafsuper1.** Saves one logout
round-trip, no BUG-037 risk, no Responsible User dropdown
explanation needed.
