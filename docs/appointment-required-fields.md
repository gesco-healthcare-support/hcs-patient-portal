# Appointment Form + Claim Information Modal - Required Fields

> Purpose: which booking fields are required, and where that is enforced. Audience: developers changing
> the booking form or its server-side validation.

Legend: C = required client-side, S = required server-side

## Schedule

- Appointment Type - C, S
- Location - C, S
- Time Slot - C, S
- Appointment Date - C, S
- Appointment Time - C
- Panel Number - C, S for a Panel QME only; refused for every other type
  (`AppointmentManager.EnsurePanelNumberMatchesType`)
- Booking User (identityUserId) - C, S
- Patient (patientId) - C, S

## Patient Demographics

- First Name - C, S
- Last Name - C, S
- Email - optional; when given, its format is validated
- Date of Birth - C

## Employer Details

- Employer Name - C, S
- Occupation - C, S

## Applicant Attorney (when section enabled - default on)

- First Name - C
- Last Name - C
- Email - C, S
- Firm Name - C, S
- Phone Number - C
- Street - C
- City - C
- State - C
- Zip Code - C

## Defense Attorney (when section enabled - default on)

- First Name - C
- Last Name - C
- Email - C, S
- Firm Name - C, S
- Phone Number - C
- Street - C
- City - C
- State - C
- Zip Code - C

## Custom Fields

- Custom Field Value - C, S (only when the field is marked mandatory)

## Claim Information Modal - Always

- Date of Injury - C, S
- Claim Number - C, S
- WCAB ADJ - C, S
- Body Part(s), at least one - C, S

## Claim Information Modal - Primary Insurance (when enabled - default on)

- Insurance Name - C

## Claim Information Modal - Claim Examiner (when enabled - default on)

- Name - C
- Email - C
- Phone - C
- Street - C
- City - C
- State - C
- Zip - C

## Approval gates (server-side, checked when staff approve)

Booking can be submitted without these; approval refuses until they are met, in this order
(`AppointmentManager.FindUnmetApprovalGateAsync`):

1. At least one injury (Claim Information) row.
2. At least one active Claim Examiner.
3. For a Panel QME only: a document flagged as the panel strike list.
