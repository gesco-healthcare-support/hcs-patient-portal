---
title: HRD scenarios -- Terms & Conditions modal during registration
date: 2026-05-15
status: ready-for-run
parent-suite: docs/runbooks/HARDENING-TEST-SUITE.md (lives on main; copy these scenarios into it on the next main -> feat/replicate-old-app sync)
parity-source: docs/parity/wave-1-parity/_implementation-spec-terms-and-conditions.md (Section 6.1)
---

# Hardening scenarios -- T&C modal during registration

Standalone scenario file added by the T&C-modal implementation PR. The
canonical `docs/runbooks/HARDENING-TEST-SUITE.md` currently lives only on
`main`; once `feat/replicate-old-app` merges back, these scenarios should
land inside the canonical suite under the `Phase 4: T&C acceptance`
section and Round 2's bypass probes.

All scenarios run against `http://falkinstein.localhost:44368/Account/Register`
on the Docker stack. No DB seed beyond the standard demo data is required.

---

## Round 1 -- happy-path scenarios

### HRD-R1.10.1 -- T&C checkbox visible and unchecked on fresh load

**Pre:** Open `http://falkinstein.localhost:44368/Account/Register` in a
fresh browser context (no prior session cookies).

**Steps:**

1. Wait for the register form to settle (User Type select visible, First /
   Last Name fields injected, Confirm Password field injected).

**Pass:**

- Checkbox `#external-signup-terms-checkbox` exists in the DOM.
- Checkbox is unchecked.
- Label reads `I have read and accept the Terms and Conditions.` (period
  trailing the link).
- Link `#external-signup-terms-link` text is `Terms and Conditions` and
  has `role="button"`; the `href` is `#` but the click handler suppresses
  default navigation.
- Sign Up button is disabled.

### HRD-R1.10.2 -- Modal opens on link click; closes via all 4 paths

**Pre:** HRD-R1.10.1 reached.

**Steps:**

1. Click the `Terms and Conditions` link in the checkbox label.
2. Verify the modal `#external-signup-terms-modal` has class `.show` and
   is visible.
3. Verify the modal title reads `Terms and Conditions`.
4. Verify the modal body contains the localized body text (assert a
   known substring, e.g. `"By creating an account on the Patient
   Appointment Portal"`).
5. Close path 1: click the `.btn-close` icon -> modal closes.
6. Re-open via link. Close path 2: click the footer Close button ->
   modal closes.
7. Re-open via link. Close path 3: press the Escape key -> modal closes.
8. Re-open via link. Close path 4: click on the dimmed backdrop (the
   modal's own root, outside the dialog) -> modal closes.

**Pass:**

- Modal opens within 500 ms of each click.
- All four close paths work.
- After each close, the modal no longer has class `.show` and is not
  visible.
- Page scroll is restored each time (`body` no longer has `.modal-open`,
  no leftover `.modal-backdrop` element in the DOM).

### HRD-R1.10.3 -- Sign Up button transitions correctly with checkbox state

**Pre:** HRD-R1.10.1 reached.

**Steps:**

1. Fill all required register fields (User Type dropdown, First Name,
   Last Name, Email, Password, Confirm Password; plus Firm Name when
   the role is Applicant Attorney or Defense Attorney).
2. Verify Sign Up button is still disabled (T&C checkbox is unchecked).
3. Check the T&C checkbox.
4. Verify Sign Up button is now enabled.
5. Uncheck the T&C checkbox.
6. Verify Sign Up button is disabled again.
7. Re-check the T&C checkbox.
8. Verify Sign Up button is enabled again.

**Pass:**

- All transitions are immediate (the `input` / `change` events fire on
  each interaction).
- Disabled-state matches the expected state at each step.

### HRD-R1.10.4 -- Happy-path registration with T&C accepted

**Pre:** HRD-R1.10.3 step 4 reached (Sign Up button enabled).

**Steps:**

1. Click Sign Up.

**Pass:**

- Network: `POST /api/public/external-signup/register` returns 200.
- Page transitions to the post-signup state (in-page success banner
  with `Verify Email` + `Sign In` buttons).
- No T&C-related error in the inline error banner.

---

## Round 2 -- bypass / failure-mode probes

### HRD-R2.8.1 -- Direct POST to register without `AcceptTerms` still succeeds

**Pre:** A fresh email address.

**Steps:**

1. From a curl / fetch outside the UI, POST a valid signup payload
   (`tenantId`, `userType`, `email`, `password`, `confirmPassword`,
   `firstName`, `lastName`) **without** any `AcceptTerms` field.
2. Confirm the response is 200.

**Pass:**

- 200 OK.
- User row exists in the AbpUsers table.
- The server does NOT require `AcceptTerms` (OLD parity: client-side
  gate only, no server-side enforcement).

### HRD-R2.8.2 -- UI-level submit attempt is impossible while checkbox is unchecked

**Pre:** HRD-R1.10.3 step 1 reached (all fields filled, checkbox
unchecked).

**Steps:**

1. Programmatically click the Sign Up button via Playwright.

**Pass:**

- Playwright reports the button is disabled.
- No `POST /api/public/external-signup/register` is fired (verify via
  network panel or by listening for the request).

### HRD-R2.8.3 -- DevTools bypass triggers the inline-error guard

**Pre:** HRD-R1.10.3 step 1 reached (all fields filled, checkbox
unchecked).

**Steps:**

1. In the browser console, run:

   ```js
   document.getElementById('external-signup-terms-checkbox').required = false;
   ```

2. Confirm the Sign Up button enables (the disabled-binding loses the
   required check on this input).
3. Click Sign Up.

**Pass:**

- Inline error banner shows `Please accept the Terms and Conditions
  before signing up.` (sourced from the `Account:Terms:RequiredBeforeSubmit`
  localization key).
- No `POST /api/public/external-signup/register` is fired.

This is the belt-and-braces server-bypass guard documented in
`_implementation-spec-terms-and-conditions.md` Section 5.5. It is the
only line of T&C enforcement after the user defeats the primary
disabled-button gate; the server itself does not validate `AcceptTerms`.
