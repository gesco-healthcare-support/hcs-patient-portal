---
id: OBS-28
title: "Send invite" button on /users/invite produces no success toast or visible confirmation
severity: observation
status: open
found: 2026-05-23 hardening HRD-P1.B.1
flow: invite-external-user-ui
component: angular/src/app/external-users/components/invite-external-user.component.{ts,html}
---

# OBS-28 - Send invite UX is silent

## Symptom

On `/users/invite`, a staff supervisor / IT admin fills the Email + Role
fields and clicks `Send invite`. The page does not visibly change. No
toast message, no banner, no inline confirmation. The form fields stay
populated.

The only way to know the invite was actually issued is to:

- Inspect `HangFire.Job` for the new `SendAppointmentEmailJob` row with
  the recipient address.
- Or check `AppInvitations` for the new row.

## Functional impact

UX gap. A staff user may click `Send invite` multiple times in a row
because they don't see confirmation - which depending on rate-limit
behavior could either trigger 429s or generate duplicate (but invalidated)
invite tokens.

## Recommended fix

Surface either:
- A toast notification: "Invite sent to <email>" (success) or "Failed to
  send invite" (error).
- An inline banner that appears above the form for ~5 seconds.
- A field-reset after success so the user understands the action
  completed.

## Repro

1. Log in as `stafsuper1`.
2. Navigate to `/users/invite`.
3. Fill Email + Role, click `Send invite`.
4. Observe the UI: no visible change. No toast.
5. Verify via SQL that the row IS created in `AppInvitations`.

## Related

- HRD-P1.B scenarios.
- OBS-27 (invite email body has empty greeting).
