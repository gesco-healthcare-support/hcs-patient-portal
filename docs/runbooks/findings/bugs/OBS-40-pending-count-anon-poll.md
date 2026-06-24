---
id: OBS-40
title: Dashboard pending-count polls /api/app/appointments/pending-count when user is logged-out -> 401 spam
severity: observation
status: open
found: 2026-05-25 (Mon AM hardening, console error survey)
flow: dashboard-pending-count-widget
component: angular/src/app/(dashboard or shared layout)/pending-count-poll - exact location TBD
---

# OBS-40 - pending-count polls anonymously

## Symptom

While clicking around the SPA, the browser console shows repeated:

```
Failed to load resource: the server responded with a status of 401 (Unauthorized)
@ http://falkinstein.localhost:44327/api/app/appointments/pending-count:0
```

These fire even when the user is not logged in (e.g., on the
`/?logout=true` landing or before login completes). The poll
continues at its interval regardless of auth state.

## Expected

The pending-count poll should:
1. Check `oAuthService.hasValidAccessToken()` (or equivalent) before
   firing, OR
2. Be gated behind the dashboard route guard so it only mounts when
   the user is on the dashboard, OR
3. Catch 401 silently and pause polling until token refresh succeeds.

## Reproduction

1. Open browser dev tools, Network tab.
2. Visit `/?logout=true` (or just visit the SPA without logging in).
3. Observe periodic 401s on `/api/app/appointments/pending-count`.

## Functional impact

Zero functional impact -- the dashboard widget recovers correctly
once the user logs in. But:

- Console noise distracts from real errors.
- Unnecessary network traffic (1 req/N seconds per anonymous tab).
- Server log noise (auth-rejected requests).

## Recommended fix

Identify the polling component (likely a shared header/sidebar widget
in the parity-port LeptonX layout). Wrap the poll observable in an
`if (this.oAuthService.hasValidAccessToken())` guard or use the
ABP-provided auth subscription pattern.

## Related

- No direct relation to other findings. Pure UX polish.
