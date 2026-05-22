---
id: OBS-21
title: claimE1@gesco.com verification email click did not flip EmailConfirmed
severity: low
status: superseded-by-bug-029
found: 2026-05-19
last-replayed: 2026-05-21
flow: external-user-registration
component: AuthServer Pages/Account/EmailConfirmation.cshtml.cs | mail delivery
---

> **2026-05-21 replay outcome:** root cause identified as [[BUG-029]]
> -- the verification email URL for any invited user (including claimE1)
> has `host=localhost` (no tenant subdomain). Clicking the inbox link
> as-is hits `EmailConfirmation` without a resolved tenant, so
> `UserManager.FindByIdAsync` returns null and the page shows
> "verification link doesn't work anymore" without updating
> `EmailConfirmed`. The earlier hypotheses (mail-client wrap, token
> decode, stale link) are obsolete; the right primary is BUG-029.
> Closing this finding as superseded; track the fix under BUG-029.
> Workaround: host-swap `localhost` -> `<tenant>.localhost` and
> click; same token, EmailConfirmed flips to 1.
> Replay: 2026-05-21 hardening HRD-P1.B.2; claime1 verified via the
> host-swap path (`EmailConfirmed=1`, userId `14eaa925-...-3a215dd42c08`).

# Symptom

During the E2E hardening run on 2026-05-19:

- `claimE1@gesco.com` (Henry Caldwell, Claim Examiner) was registered via the admin-invite flow.
- Verification email was dispatched at 19:07:20 (API log line `SendAppointmentEmailJob: delivered (AccountEmailer/EmailConfirmationLink/f6086bb1-abd6-adea-5753-3a2153cf83be) to claime1@gesco.com`).
- User reports they clicked the verification link in the inbox.
- DB state at 19:16 still shows `EmailConfirmed = 0` for the account.
- AuthServer logs for the 30-minute window around the click show **zero** `GET /Account/EmailConfirmation` requests for the matching userId. Only `appatty1`'s and `defatty1`'s verification hits appear.
- Two follow-up appointment-requested emails were dispatched to the same address (19:13:00, 19:15:45) so the address itself reaches the inbox.

The other two invitees in the same batch (`appatty1`, `defatty1`) verified successfully via the same flow at 19:09:09 and 19:08:53 respectively.

## Hypotheses (3 competing)

1. **Email template carried a malformed link for this account only.** The URL in claimE1's email may have been truncated / wrapped / URL-encoded in a way that breaks the token. Mail-client-specific (Outlook line wrap at certain widths, or quoted-printable encoding) could corrupt the long token.
2. **Token decode failure.** DataProtection key rotation between job dispatch and click invalidated the token. Less likely given the short window and the shared Redis store.
3. **User clicked a stale link.** An earlier invite or a resend produced multiple verification URLs; the user clicked one already superseded by a later send. (No evidence of multiple sends in the log.)

## Workaround applied 2026-05-19

Flipped `EmailConfirmed=1` directly via SQL so the E2E run could continue. Not a bug fix.

```sql
UPDATE AbpUsers SET EmailConfirmed=1
WHERE UserName='claime1@gesco.com';
```

## Recommended next step

Reproduce in a fresh registration: register a new test user, capture the verification URL via the API log + the email body (compare them byte-for-byte). If they differ, the email template / encoder is the culprit (hypothesis 1). If they match and the click still doesn't reach the AuthServer, instrument the EmailConfirmation page or check DataProtection key state at click time (hypothesis 2).
