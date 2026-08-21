---
id: OBS-27
title: Invite email body opens with "Hi ," (empty greeting since name not yet known)
severity: observation
status: open
found: 2026-05-23 hardening HRD-P1.B.1
flow: invite-email-template
component: src/HealthcareSupport.CaseEvaluation.Domain/NotificationTemplates/EmailBodies/InviteExternalUser.html
---

# OBS-27 - Invite email greeting is empty

## Symptom

The invite-external-user email body (as rendered in the Hangfire
`SendAppointmentEmailJob` arguments for invite jobs) starts with:

```html
<p>Hi ,</p>
<p><strong>Falkinstein</strong> has invited you to register as a
<strong>Defense Attorney</strong>. Use the button below to set your
password and complete registration. Your email address and role are
already filled in for you.</p>
```

The `Hi ,` line has an empty name token. This is because the invite is
issued BEFORE the recipient picks a first/last name (those happen during
the registration acceptance flow), so the template has no name to
substitute.

## Recommended fix

Either:

- Drop the `<p>Hi ,</p>` line entirely from `InviteExternalUser.html` -
  the next paragraph stands on its own.
- Use a fallback like `<p>Hello,</p>` (no name placeholder) - friendly
  but generic.
- Use the email address as the greeting: `<p>Hi
  defatty1@gesco.com,</p>` - explicit but feels robotic.

## Functional impact

Cosmetic. The recipient sees a weird-looking but functional email.
Does not block the flow.

## Related

- HRD-P1.B (invite-flow scenarios surfaced this).
- OBS-25 (invite-accept does not auto-confirm) - related template flow.
