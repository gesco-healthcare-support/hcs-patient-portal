---
id: OBS-29
title: Cookie consent banner overlays /users/invite form on first visit; form unreachable until Accept clicked
severity: observation
status: open
found: 2026-05-23 hardening HRD-P1.B.2
flow: cookie-consent-ui
component: angular/src/app/gdpr-cookie-consent/ (suspected) + global CSS z-index for the consent alert
---

# OBS-29 - Cookie consent banner blocks /users/invite form

## Symptom

On a fresh browser context, navigating to `/users/invite` shows ONLY
the cookie-consent banner. The actual form is in the DOM but hidden /
overlaid by the consent alert. The DOM tree under the page heading
returns 0 inputs.

After clicking `Accept` on the cookie banner, the form becomes
interactive (2 inputs + 1 select detected, `Send invite` button
enabled).

This made the hardening automation appear to "not find the form"
until the banner was dismissed.

## Why this is observation-worthy

The cookie banner is global (renders on every public-ish route on first
visit). For human users, this is a 1-click annoyance. For automation
(or keyboard-only users), the form being functionally inaccessible
until consent is given is a usability gap.

## Repro

1. Open a fresh browser (or clear cookies for `falkinstein.localhost`).
2. Log in as `stafsuper1`.
3. Navigate to `/users/invite`.
4. Observe: cookie banner covers / overlays the form. Form inputs are
   not focusable / not interactable.
5. Click `Accept` on the cookie banner.
6. Form becomes interactive.

## Recommended fix

Either:
- Lower the z-index of the cookie banner so it docks below the active
  form (typical pattern: bottom-of-screen, doesn't overlap content).
- Auto-dismiss on first form interaction.
- Pre-accept cookies for internal staff routes (cookie consent only
  applies to anonymous / public routes).

## Functional impact

Low. Annoyance only; the banner does eventually go away after one click.

## Related

- HRD-P1.B.2 (this run's claimE1 invite scenario surfaced it).
