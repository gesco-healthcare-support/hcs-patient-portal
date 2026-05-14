---
slug: 2026-05-14-form-prefill-content
status: draft
audience: Adrian + automated test session
session: main-worktree userflow testing
---

# Pre-filled form content for booking-form workflows

## Why this doc exists

Every workflow walk fills the FULL booking form (7 sections, ~50-60
fields with all toggles on). Re-deciding field values mid-walk is
slow and error-prone. This doc is the **single source of truth** for
synthetic data; copy/paste straight into the form, no per-walk
decisions. All values are HIPAA-safe per `.claude/rules/hipaa-data.md`
+ `.claude/rules/test-data.md`.

## Identities (already provisioned + role assignments needed)

| Email | Role | Status | Notes |
| --- | --- | --- | --- |
| SoftwareThree@gesco.com | Patient | EmailConfirmed=1 | Used for Patient booker flows |
| SoftwareFour@gesco.com | Applicant Attorney | EmailConfirmed=1 | AA booker + AA-link target |
| SoftwareFive@gesco.com | Defense Attorney | EmailConfirmed=1 | DA-link target |
| SoftwareSix@gesco.com | Claim Examiner | EmailConfirmed=1 | CE-link target (via Claim Information modal) |
| SoftwareOne@evaluators.com | admin + Clinic Staff + Staff Supervisor | seeded as admin; needs +Clinic Staff + Staff Supervisor | Receives Clinic Staff + Supervisor fan-out emails + can do every internal action |
| SoftwareTwo@evaluators.com | admin + Clinic Staff + Staff Supervisor | seeded as admin; needs +Clinic Staff + Staff Supervisor | Same as SoftwareOne; signs packets in Workflow G |
| admin@falkinstein.test | tenant admin | seeded | Master-data CRUD only; not used for workflow emails |
| it.admin@hcs.test | IT Admin (host) | seeded | Tenant CRUD only; not used for workflow emails |

Password for all: `1q2w3E*r`. Host admin: `1q2w3E*` (no trailing r).

**Action required before Workflow B (Prep 3 in the workflow plan):**
both SoftwareOne and SoftwareTwo are auto-seeded with the tenant
`admin` role per `InternalUsersDataSeedContributor.cs:48-52,152-159`.
To put them on every role-targeted email fan-out, ADD the "Clinic
Staff" and "Staff Supervisor" roles to BOTH users via
`/identity/users` -> edit -> Roles tab. End state per user: admin +
Clinic Staff + Staff Supervisor.

## Field-by-field pre-fill values

Note: any field labeled "synthetic 9-digit string" or similar means
**type a 9-digit number live at the keyboard**. We do not commit
SSN-shaped strings to this doc per PHI scanner rules.

### Section 1 -- Appointment Details (Schedule)

| Field | Value |
| --- | --- |
| Appointment Type | varies per workflow (AME / QME / Re-Evaluation / Consultation) |
| Panel Number | `12345678` |
| Location | `Demo Clinic North` (seeded) |
| Appointment Date | varies per workflow; pick from June or July seeded slots |
| Appointment Time | first available slot for that date |

### Section 2 -- Patient Demographics (Patient self-booker)

When the Patient (SoftwareThree) is booking for themselves, most
fields auto-fill from the AbpUser + AppPatient profile. Verify on
first walk and override where needed.

| Field | Value |
| --- | --- |
| Last Name | `Three` |
| First Name | `Software` |
| Middle Name | `Test` |
| Gender Id | Male |
| Date Of Birth | `1985-04-15` |
| Email | `SoftwareThree@gesco.com` (readonly for Patient booker) |
| Cell Phone Number | `5551234567` |
| Phone Number | `5551234568` |
| Social Security Number | type a synthetic 9-digit string at the keyboard |
| Street | `100 Test Avenue` |
| Unit # | `Apt 2B` |
| City | `Los Angeles` |
| State | California |
| Zip Code | `90001` |
| Appointment Language | `English` |
| Needs Interpreter | No |
| Referred By | `Self-referral` |

### Section 2b -- Patient Demographics (synthetic patient when AA/DA/CE books)

When an external non-patient role books for a brand-new patient
(no existing patient record), use these values. Pick patient name
per workflow to keep audit trails distinct.

| Field | Workflow B alt patient | Workflow C alt patient | Workflow D alt patient |
| --- | --- | --- | --- |
| Last Name | `Doe` | `Smith` | `Brown` |
| First Name | `Jane` | `John` | `Mary` |
| Middle Name | `T` | `Q` | `K` |
| Gender Id | Female | Male | Female |
| Date Of Birth | `1980-07-22` | `1975-11-03` | `1990-02-19` |
| Email | `jane.doe.20260514@example.test` | `john.smith.20260514@example.test` | `mary.brown.20260514@example.test` |
| Cell Phone Number | `5552220001` | `5552220002` | `5552220003` |
| Phone Number | `5552220011` | `5552220012` | `5552220013` |
| Social Security Number | type a synthetic 9-digit string | type a synthetic 9-digit string | type a synthetic 9-digit string |
| Street | `200 Synthetic Way` | `300 Faux Drive` | `400 Test Blvd` |
| Unit # | (blank) | `Ste 5` | `#9` |
| City | `San Diego` | `Sacramento` | `Oakland` |
| State | California | California | California |
| Zip Code | `92101` | `95814` | `94601` |
| Appointment Language | English | Spanish | English |
| Needs Interpreter | No | Yes -- vendor `Acme Language Services` | No |
| Referred By | `Adrian Test` | `Adrian Test` | `Adrian Test` |

### Section 3 -- Employer Details

Same set for every workflow unless noted.

| Field | Value |
| --- | --- |
| Employer Name | `Acme Construction Co` |
| Occupation | `Carpenter` |
| Phone Number | `5557770000` |
| Street | `500 Industrial Pkwy` |
| City | `Los Angeles` |
| State | California |
| Zip Code | `90021` |

### Section 4 -- Applicant Attorney

Used when AA is included (toggle ON).

| Field | Value (point at SoftwareFour) |
| --- | --- |
| Name | `Software Four` |
| Email | `SoftwareFour@gesco.com` |
| Firm Name | `Four Legal LLP` |
| Web Address | `https://four-legal.example.test` |
| Phone Number | `5554440000` |
| Fax | `5554440001` |
| Street | `400 Counsel Ave` |
| City | `Los Angeles` |
| State | California |
| Zip | `90013` |

### Section 5 -- Defense Attorney

Used when DA is included.

| Field | Value (point at SoftwareFive) |
| --- | --- |
| Name | `Software Five` |
| Email | `SoftwareFive@gesco.com` |
| Firm Name | `Five Defense LLP` |
| Web Address | `https://five-defense.example.test` |
| Phone Number | `5555550000` |
| Fax | `5555550001` |
| Street | `500 Defense Way` |
| City | `Los Angeles` |
| State | California |
| Zip | `90014` |

### Section 6 -- Claim Information modal

Open the modal via the "Add" button on the Claim Information card.

**Top fields (always filled):**

| Field | Value |
| --- | --- |
| Cumulative Trauma Injury | No |
| Date Of Injury | `2025-12-10` |
| Claim Number | `WC-2026-0001` (increment for each workflow) |
| WCAB Office (Venue) | first seeded option (e.g. Los Angeles) |
| ADJ# | `ADJ-1234567` |
| Body Parts | `Lower back, right knee, left wrist` |

**Insurance subsection (toggle ON):**

| Field | Value |
| --- | --- |
| Company Name | `StateFund California` |
| Attention | `Claims Department` |
| Phone Number | `8005551111` |
| Fax | `8005551112` |
| Street | `1750 Howe Ave` |
| STE | `100` |
| City | `Sacramento` |
| State | California |
| Zip | `95825` |

**Claim Examiner subsection (toggle ON) -- point at SoftwareSix:**

| Field | Value |
| --- | --- |
| Name | `Software Six` |
| Email | `SoftwareSix@gesco.com` |
| Phone Number | `5556660000` |
| Fax | `5556660001` |
| Street | `600 Claims Plaza` |
| STE | `200` |
| City | `Los Angeles` |
| State | California |
| Zip | `90017` |

Click `Add` to push the injury into the table; the parent submit
will fan it out to `appointment-injury-details` +
`appointment-primary-insurances` + `appointment-claim-examiners`.

### Section 7 -- Additional Authorized User

Used only in workflows that explicitly exercise the authorized-user
delegation feature. Default for B/C/D: leave empty.

When exercised (separate workflow): open modal, select
`SoftwareOne@evaluators.com` (must already exist as a user in the
tenant), choose Rights = `Full Access` from the dropdown.

### Section 8 -- Additional Details (Custom Fields)

Renders only if the tenant has custom fields configured. Falkinstein
demo tenant has no custom fields per current seed. If the section
appears unexpectedly during a walk, document the values used and
file as an observation.

## Increment rules for repeat walks

To keep DB state distinguishable when walking the same workflow twice:
- Claim Number: `WC-2026-NNNN` increments
- Email of synthetic patient: append timestamp suffix (`.20260514a`,
  `.20260514b`, etc.)
- Body Parts: append a 1-word marker per walk (`...left wrist (B1)`,
  `(B2)`, etc.)

## Out of scope

- Real names, real addresses, real SSNs, real DOBs -- HIPAA rule.
- Production-looking employer names that match real California
  workers' comp filings -- synthetic only.
- Email aliases pointing at non-Gesco inboxes -- only Adrian's
  controlled inboxes get verification links.
