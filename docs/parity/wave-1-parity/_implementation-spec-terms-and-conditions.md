---
title: Implementation spec — Terms & Conditions modal during registration
date: 2026-05-15
status: ready-for-implementation
audience: implementing session (currently W:\patient-portal\replicate-old-app)
parity-source:
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\term-and-condition\term-and-condition.component.ts
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\term-and-condition\term-and-condition.component.html
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\user\users\add\user-add.component.ts (lines 19, 127-129) — how the OLD register form opens the modal
audit-doc: docs/parity/wave-1-parity/terms-and-conditions.md
---

# Implementation spec — Terms & Conditions modal during registration

Self-contained, ready-to-implement spec for the **Terms & Conditions** experience
on the external-user registration form. Replaces the current passive disclaimer
text with a real **checkbox + modal + submit gate** matching OLD behavior. Paths
assume repo root (works in both `W:\patient-portal\main` and
`W:\patient-portal\replicate-old-app`).

---

## 1. Mission

OLD registration form requires the user to acknowledge T&Cs before signing up:

1. A checkbox under the Sign Up button: "I accept the Terms and Conditions"
2. A "(view)" link next to the checkbox that opens a modal popup
3. Modal shows static T&C text and a Close button
4. **Sign Up button is disabled** until the checkbox is checked

NEW today has a **passive disclaimer** ("By clicking 'Sign Up', you agree to our
terms of service…") with a **dead link** (`href="#"`). No checkbox, no modal, no
gating. This is a parity gap and a legal-acceptance gap (clicking Sign Up does
not constitute deliberate acknowledgement today).

Out of scope (do NOT add):
- Database tracking of who accepted T&Cs / when (OLD does not persist this; strict parity says no).
- Versioning of T&C text with per-user acceptance dates.
- IT-Admin UI to edit T&C text (OLD has none; localization key edit is the path).
- Per-locale T&C variants beyond `en.json` (Phase 2 if needed).
- Cookie consent (separate concern — ABP's `gdpr-cookie-consent` module at `/gdpr-cookie-consent/privacy` already handles that and is unrelated).

---

## 2. OLD behavior (verbatim source-cited)

### 2.1 OLD modal component

**`P:\PatientPortalOld\patientappointment-portal\src\app\components\term-and-condition\term-and-condition.component.ts`** (full file, 33 lines):

```typescript
import { Component, Input } from '@angular/core';
import { RxPopup } from '@rx/view';
// ...unused imports omitted...

@Component({
    selector: 'term-and-condition',
    templateUrl: './term-and-condition.component.html',
})
export class TermAndConditionComponent implements OnInit, OnDestroy {
    showComponent: boolean = false;

    constructor(private popup: RxPopup) {}

    ngOnInit(): void {
        this.showComponent = true;
    }
    ngOnDestroy(): void {}

    closePopup() {
        this.popup.hide(TermAndConditionComponent);
    }
}
```

**`term-and-condition.component.html`** (full file, 31 lines):

```html
<div class="bootbox modal fade bootbox-lg show" tabindex="-1" role="dialog"
     style="padding-right: 17px; display: block;" *ngIf="showComponent">
  <div class="modal-dialog modal-lg">
    <div class="modal-content">
      <div class="modal-header">
        <button type="button" class="bootbox-close-button close text-white" (click)="closePopup()">×</button>
        <h4 class="modal-title text-white">Terms & Conditions</h4>
      </div>
      <div class="modal-body">
        Lorem Ipsum is simply dummy text of the printing and typesetting industry. Lorem Ipsum has been
        the industry's standard dummy text ever since the 1500s, when an unknown printer took a galley
        of type and scrambled it to make a type specimen book. ...
        <br><br>
        Lorem Ipsum is simply dummy text of the printing and typesetting industry. ...
      </div>
      <div class="modal-footer">
        <button data-bb-handler="secondary" type="button" (click)="closePopup()" class="btn btn-secondary">Close</button>
      </div>
    </div>
  </div>
</div>
```

**Important**: the OLD T&C body is literally Lorem ipsum (twice). It is a
placeholder. In NEW we will substitute meaningful text via a localization key
(documented as a parity-friendly improvement in `terms-and-conditions.md`); if
strict-bug-for-bug parity is preferred, use the Lorem ipsum verbatim.

### 2.2 How OLD registers wire the modal

**`P:\PatientPortalOld\patientappointment-portal\src\app\components\user\users\add\user-add.component.ts:19,127-129`**:

```typescript
import { TermAndConditionComponent } from 'src/app/components/term-and-condition/term-and-condition.component';

@Component({
  templateUrl: './user-add.component.html',
  entryComponents: [TermAndConditionComponent, RxMessageComponent],
})
export class UserAddComponent extends UserDomain {
  // ...
  openTermaAndConidition(): void {                    // ← typo preserved in OLD
    this.popup.show(TermAndConditionComponent);
  }
}
```

`user-add.component.html` then has (paraphrased from the audit doc + OLD source):
- A checkbox `<input type="checkbox" formControlName="acceptTerms" required>`
- A label with a "View" link wired to `openTermaAndConidition()`
- The Sign Up button is disabled while the form is invalid (covers acceptTerms)

### 2.3 OLD acceptance tracking

OLD does **not** persist acceptance state. `Users` table has no `TermsAcceptedDate`,
`AcceptedTermsVersion`, or similar column. The checkbox is a transient form-state
gate only. Strict parity: do NOT add a DB column in NEW.

---

## 3. NEW current state (what already exists vs what is missing)

NEW registration is **NOT** an Angular component — it's an ABP AuthServer Razor
page rendered server-side at `:44368/Account/Register`. All form customization
lives in **`src/HealthcareSupport.CaseEvaluation.AuthServer/wwwroot/global-scripts.js`**
(1102 lines). The script:

- Injects the External User Type select (line 118 `ensureUserTypeSelect`)
- Injects First Name / Last Name / Firm Name inputs (line 209 `ensureExtraRegisterFields`)
- Hides the stock UserName field (line 326 `hideUserNameField`)
- Wires Sign Up disabled-state to form validity (line 832 `ensureButtonDisabledBinding`)
- Intercepts submit and POSTs to `/api/public/external-signup/register`
- All of the above run from `init()` at line 1050, gated on `isRegisterPage()`

### What exists today — partial T&C

**`global-scripts.js:299-317`** (verbatim):

```javascript
// OLD parity (P:\PatientPortalOld\.../user-add.component.html:50-53):
// T&C paragraph below the submit button. Idempotent.
function ensureTermsBlock(form) {
  if (form.querySelector('#external-signup-terms')) return;
  var btn = form.querySelector('button[type="submit"], #register, .register-btn');
  var anchor = btn ? (btn.closest('.form-floating, .mb-3, .mb-2') || btn) : null;
  var div = document.createElement('div');
  div.id = 'external-signup-terms';
  div.className = 'small text-muted mt-3';
  div.innerHTML =
    'By clicking "Sign Up", you agree to our '
    + '<a href="#" target="_blank" rel="noopener">terms of service and privacy policy</a>'
    + '. We’ll occasionally send you account related emails.';
  if (anchor && anchor.parentNode) {
    anchor.parentNode.insertBefore(div, anchor.nextSibling);
  } else {
    form.appendChild(div);
  }
}
```

Wired in `init()` at line 1065: `ensureTermsBlock(form);`

**Limits of this implementation:**
- No checkbox — clicking Sign Up does not require an opt-in
- The "terms of service and privacy policy" link is `href="#"` (dead)
- No modal — no way to actually view the terms
- Submit is not gated on acceptance
- Not consistent with OLD (modal + checkbox)

---

## 4. The gap — 4 things to add, 1 thing to replace

| What | Current state | Action |
|---|---|---|
| T&C checkbox + label with "View" link | Missing | **Replace** `ensureTermsBlock` with a real form-control row |
| Modal that shows T&C body | Missing — link is `href="#"` | **Add** a Bootstrap modal element + click handler that shows it |
| Submit gating on checkbox state | Missing | Mark the checkbox `required`; existing `ensureButtonDisabledBinding` will already include it (it queries `input[required], select[required]`) |
| T&C body text | Missing | **Add** localization keys `Account:Terms:Title` + `Account:Terms:Body` |
| Passive disclaimer text | Present and useless | **Remove** as part of the swap |

No backend changes needed. No DB changes needed. No new permissions. No new
DTOs. **Entirely AuthServer-side / client-side JS** with two new localization
strings.

---

## 5. Implementation — file-by-file

### 5.1 Localization — add T&C title + body

**Edit** `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json` — add two keys (keep alphabetical with existing `Account:*` keys; if no `Account:` namespace exists yet, place at the appropriate alphabetical position):

```json
"Account:Terms:Title": "Terms and Conditions",
"Account:Terms:CheckboxLabel": "I have read and accept the",
"Account:Terms:LinkLabel": "Terms and Conditions",
"Account:Terms:Close": "Close",
"Account:Terms:Body": "<p>By creating an account, you agree to the following terms governing your use of the Patient Portal:</p><h6>1. Account Use</h6><p>You agree to provide accurate information and to keep your login credentials confidential. You are responsible for all activity that occurs under your account.</p><h6>2. Privacy and PHI</h6><p>Information you submit is treated as Protected Health Information (PHI) under HIPAA. We use the information only to schedule and manage your medical evaluation appointments and to communicate with you and your authorized representatives.</p><h6>3. Authorized Sharing</h6><p>You may grant access to your appointment record to your attorney, claim examiner, or other authorized party via the in-app sharing feature. You can revoke that access at any time.</p><h6>4. Acceptable Use</h6><p>You agree not to upload malicious files, attempt to access another user's account, or use the Portal for any unlawful purpose.</p><h6>5. Changes to Terms</h6><p>We may update these Terms from time to time. Continued use of the Portal after changes constitutes acceptance.</p><p class=\"text-muted small mt-3\">If you do not agree to these Terms, do not create an account.</p>"
```

**Notes:**
- The `Body` is HTML (it will be rendered into a modal `innerHTML`). Keep it
  conservative — no `<script>`, no `<iframe>`. Localization key consumers must
  treat it as trusted content from the resource file (no user input ever flows
  here).
- If your project prefers strict OLD parity (Lorem ipsum verbatim), replace the
  `Body` value with OLD's Lorem ipsum text. The audit doc explicitly allows
  either; localization keys with meaningful content is recommended.
- The `Title` and other shorter strings will be inserted as `textContent` —
  safe by definition.

**Optionally** also add Spanish / Hindi / any other locales the project ships
(`es.json`, `hi.json`, etc.). Phase 1 ships English only.

### 5.2 Expose localized strings to `global-scripts.js`

ABP renders the AuthServer page server-side. The simplest way to pass localized
strings to the client-side script is via **`data-*` attributes on a hidden
element** rendered into the page, OR via **window-level JSON** injected through
a layout partial.

**Recommended approach: ABP's `abp.localization` client object.** When the
AuthServer page loads, ABP's bundle includes a JS object
`abp.localization.values['CaseEvaluation']` containing every localized string in
the resource. The client script reads from there.

Helper to add to `global-scripts.js` (at the top, alongside other helpers like
`resolveExternalSignupApiBaseUrl`):

```javascript
function L(key, fallback) {
  try {
    if (typeof window.abp !== 'undefined'
        && window.abp.localization
        && typeof window.abp.localization.getResource === 'function') {
      var res = window.abp.localization.getResource('CaseEvaluation');
      if (res && typeof res === 'function') {
        var resolved = res(key);
        if (resolved && resolved !== key) return resolved;
      }
      // Older ABP versions: values object lookup.
      var values = window.abp.localization.values
        && window.abp.localization.values['CaseEvaluation'];
      if (values && values[key]) return values[key];
    }
  } catch (_e) { /* fall through */ }
  return fallback;
}
```

**Important:** if you find that `abp.localization` is not available on
`/Account/Register` (it should be — the page loads `/libs/abp/core/abp.js`
which defines it — but verify), fall back to injecting a `<script>` tag in a
Razor layout that sets `window.GescoTermsStrings = { title: '...', body: '...', ... }`.
The `L()` helper can read from that too.

### 5.3 Rewrite `ensureTermsBlock`

**Edit** `src/HealthcareSupport.CaseEvaluation.AuthServer/wwwroot/global-scripts.js` — **replace** the existing `ensureTermsBlock` function (lines 299-317) with the version below. The function name and call site stay the same so `init()` continues to work; only the body changes.

```javascript
// OLD parity (P:\PatientPortalOld\patientappointment-portal\src\app\components\user\users\add\user-add.component.html:50-53
// + term-and-condition.component.{ts,html}): registration form requires a T&C
// checkbox with a "View Terms" link that opens a modal. The Sign Up button
// stays disabled until the checkbox is checked. The OLD modal body is Lorem
// ipsum placeholder text; NEW substitutes meaningful HTML from the
// `Account:Terms:Body` localization key (parity-friendly improvement per
// docs/parity/wave-1-parity/terms-and-conditions.md). Idempotent.
function ensureTermsBlock(form) {
  if (form.querySelector('#external-signup-terms')) return;

  var btn = form.querySelector('button[type="submit"], #register, .register-btn');
  var anchor = btn ? (btn.closest('.form-floating, .mb-3, .mb-2') || btn) : null;

  // Resolve localized strings (with English defaults).
  var titleText = L('Account:Terms:Title', 'Terms and Conditions');
  var checkboxLabelText = L('Account:Terms:CheckboxLabel', 'I have read and accept the');
  var linkText = L('Account:Terms:LinkLabel', 'Terms and Conditions');
  var closeText = L('Account:Terms:Close', 'Close');
  var bodyHtml = L('Account:Terms:Body',
    '<p>By creating an account, you agree to the Patient Portal Terms of Use and Privacy Policy.</p>');

  // 1. Build the checkbox row.
  //    <div id="external-signup-terms" class="form-check mt-3 mb-2">
  //      <input id="external-signup-terms-checkbox" type="checkbox" required class="form-check-input" />
  //      <label for="external-signup-terms-checkbox" class="form-check-label">
  //        {checkboxLabelText} <a href="#" id="external-signup-terms-link">{linkText}</a>
  //      </label>
  //    </div>
  var wrapper = document.createElement('div');
  wrapper.id = 'external-signup-terms';
  wrapper.className = 'form-check mt-3 mb-2';

  var checkbox = document.createElement('input');
  checkbox.id = 'external-signup-terms-checkbox';
  checkbox.name = 'AcceptTerms';
  checkbox.type = 'checkbox';
  checkbox.required = true;
  checkbox.className = 'form-check-input';

  var label = document.createElement('label');
  label.htmlFor = 'external-signup-terms-checkbox';
  label.className = 'form-check-label';
  // Build label children explicitly so we never inject anything as
  // innerHTML except the localized strings we control.
  label.appendChild(document.createTextNode(checkboxLabelText + ' '));

  var link = document.createElement('a');
  link.href = '#';
  link.id = 'external-signup-terms-link';
  link.textContent = linkText;
  link.setAttribute('role', 'button');
  // Open the modal on click; suppress the default anchor navigation.
  link.addEventListener('click', function (ev) {
    ev.preventDefault();
    ev.stopPropagation();
    showTermsModal();
  });
  label.appendChild(link);

  wrapper.appendChild(checkbox);
  wrapper.appendChild(label);

  if (anchor && anchor.parentNode) {
    anchor.parentNode.insertBefore(wrapper, anchor); // checkbox sits ABOVE the Sign Up button
  } else {
    form.appendChild(wrapper);
  }

  // 2. Build the modal (lazy: only inserted once, kept hidden).
  ensureTermsModal(titleText, bodyHtml, closeText);

  log('T&C checkbox + modal injected.');
}

function ensureTermsModal(titleText, bodyHtml, closeText) {
  if (document.getElementById('external-signup-terms-modal')) return;

  // Bootstrap 5 modal markup. Match the look-and-feel of the rest of the
  // AuthServer page (which bundles Bootstrap 5 from /libs/bootstrap/).
  var modal = document.createElement('div');
  modal.id = 'external-signup-terms-modal';
  modal.className = 'modal fade';
  modal.tabIndex = -1;
  modal.setAttribute('role', 'dialog');
  modal.setAttribute('aria-modal', 'true');
  modal.setAttribute('aria-labelledby', 'external-signup-terms-modal-title');
  modal.style.display = 'none';

  // We build the outer chrome with createElement (no string interpolation
  // of user-supplied content) and set the body via innerHTML — but the
  // body is the localization-resource value, never user input.
  modal.innerHTML = ''
    + '<div class="modal-dialog modal-lg modal-dialog-scrollable" role="document">'
    +   '<div class="modal-content">'
    +     '<div class="modal-header">'
    +       '<h5 class="modal-title" id="external-signup-terms-modal-title"></h5>'
    +       '<button type="button" class="btn-close" aria-label="Close"></button>'
    +     '</div>'
    +     '<div class="modal-body" id="external-signup-terms-modal-body"></div>'
    +     '<div class="modal-footer">'
    +       '<button type="button" id="external-signup-terms-modal-close" class="btn btn-secondary"></button>'
    +     '</div>'
    +   '</div>'
    + '</div>';

  document.body.appendChild(modal);

  // Set text/HTML content programmatically (defense against any markup
  // helpers altering placeholders).
  modal.querySelector('#external-signup-terms-modal-title').textContent = titleText;
  modal.querySelector('#external-signup-terms-modal-body').innerHTML = bodyHtml;
  modal.querySelector('#external-signup-terms-modal-close').textContent = closeText;

  // Close button handlers.
  modal.querySelector('.btn-close').addEventListener('click', hideTermsModal);
  modal.querySelector('#external-signup-terms-modal-close').addEventListener('click', hideTermsModal);

  // Backdrop click closes.
  modal.addEventListener('click', function (ev) {
    if (ev.target === modal) hideTermsModal();
  });

  // ESC key closes when modal is open.
  document.addEventListener('keydown', function (ev) {
    if (ev.key === 'Escape' && modal.classList.contains('show')) {
      hideTermsModal();
    }
  });
}

function showTermsModal() {
  var modal = document.getElementById('external-signup-terms-modal');
  if (!modal) return;
  // Prefer Bootstrap's API if available (animation + a11y focus management).
  if (window.bootstrap && typeof window.bootstrap.Modal === 'function') {
    var instance = window.bootstrap.Modal.getOrCreateInstance(modal);
    instance.show();
    return;
  }
  // Fallback: manual show.
  modal.style.display = 'block';
  modal.classList.add('show');
  document.body.classList.add('modal-open');
  var backdrop = document.createElement('div');
  backdrop.id = 'external-signup-terms-backdrop';
  backdrop.className = 'modal-backdrop fade show';
  document.body.appendChild(backdrop);
}

function hideTermsModal() {
  var modal = document.getElementById('external-signup-terms-modal');
  if (!modal) return;
  if (window.bootstrap && typeof window.bootstrap.Modal === 'function') {
    var instance = window.bootstrap.Modal.getOrCreateInstance(modal);
    instance.hide();
    return;
  }
  modal.style.display = 'none';
  modal.classList.remove('show');
  document.body.classList.remove('modal-open');
  var backdrop = document.getElementById('external-signup-terms-backdrop');
  if (backdrop) backdrop.parentNode.removeChild(backdrop);
}
```

### 5.4 Confirm submit gating already covers the checkbox

The existing `ensureButtonDisabledBinding` at line 832 queries
`form.querySelectorAll('input[required], select[required]')` and disables the
Sign Up button when any required input is empty. Our new checkbox is
`required`, so it will be included automatically. **No code change needed in
that function** — but verify the helper considers an unchecked checkbox as
"empty":

The current `allFilled` predicate at line 863:
```javascript
var allFilled = inputs.every(function (input) {
  var val = (input.value || '').trim();
  if (val === '') return false;
  if (input.tagName === 'SELECT' && input.required && val === '') return false;
  return true;
});
```

A checkbox's `value` defaults to `"on"` whether checked or not — `(input.value || '').trim()` would return `"on"` for both states. **The check needs to handle checkboxes:**

**Edit** `ensureButtonDisabledBinding` — change the `allFilled.every(...)` block to:

```javascript
var allFilled = inputs.every(function (input) {
  if (input.type === 'checkbox') {
    return input.required ? input.checked : true;
  }
  var val = (input.value || '').trim();
  if (val === '') return false;
  if (input.tagName === 'SELECT' && input.required && val === '') return false;
  return true;
});
```

(Two-line change. Surgical. Preserves all other behavior.)

### 5.5 Update the submit handler to verify acceptance server-side reachability is NOT required

The backend (`ExternalSignupAppService.RegisterAsync`) does NOT need to know
the user accepted T&Cs. OLD does not persist this. The checkbox is a pure
client-side gate. **Do not** add `AcceptTerms` to `ExternalUserSignUpDto` and
do not validate it on the server. (If you want belt-and-braces, you can
silently check `checkbox.checked` once more inside `submitExternalSignup`
before the fetch and bail with `notifyRegisterFailure(form, ...)` on a miss
— but the disabled button should already make this unreachable.)

Optional defensive snippet inside `submitExternalSignup` (early-return — line
range ~660-680, before the fetch call; do NOT add if you'd rather trust the
disabled-binding):

```javascript
var termsCheckbox = form.querySelector('#external-signup-terms-checkbox');
if (termsCheckbox && !termsCheckbox.checked) {
  notifyRegisterFailure(form, 'Please accept the Terms and Conditions before signing up.');
  isSubmitting = false;
  setRegisterFormDisabled(form, false, false);
  return;
}
```

### 5.6 Modal styling sanity check

The AuthServer page already loads Bootstrap 5 (`/libs/bootstrap/css/bootstrap.min.css`)
and the bootstrap JS bundle (`/libs/bootstrap/js/bootstrap.bundle.min.js`).
Both `.modal`, `.modal-dialog`, `.modal-content`, `.btn-close`, `.modal-backdrop`,
`.fade`, `.show` classes resolve out of the box. No new CSS needed.

If the LeptonX skin overrides modal styles, do a visual check; the audit doc
already flags branding/theming touchpoints — the modal should pick up the same
header color and font as the rest of the register page. Tweak via inline style
only if necessary.

---

## 6. Tests

### 6.1 Playwright UI scenarios (add to HARDENING-TEST-SUITE)

The existing `docs/runbooks/HARDENING-TEST-SUITE.md` already has Round 2
failure-mode scenarios. Append a new `Phase 4: T&C acceptance` block in Round 1
and matching probes in Round 2. Suggested IDs `HRD-R1.10.{1..4}` and
`HRD-R2.8.{1..3}`.

```
### HRD-R1.10.1 — T&C checkbox visible and unchecked on fresh load
Pre: navigate to http://falkinstein.localhost:44368/Account/Register
Steps:
  1. Wait for the form to settle (User Type select visible).
Pass:
  - Checkbox #external-signup-terms-checkbox exists
  - Checkbox is unchecked
  - Label reads "I have read and accept the Terms and Conditions"
  - Link "Terms and Conditions" is clickable (not href="#")
  - Sign Up button is disabled

### HRD-R1.10.2 — Modal opens on link click, closes via 3 paths
Pre: HRD-R1.10.1 reached
Steps:
  1. Click the "Terms and Conditions" link.
  2. Verify the modal #external-signup-terms-modal is visible (has class .show).
  3. Verify the modal title reads "Terms and Conditions".
  4. Verify the modal body contains the localized body text (assert known substring like "By creating an account").
  5. Click the ✕ (.btn-close) icon → modal closes.
  6. Re-open via link; click the Close button → modal closes.
  7. Re-open via link; press the Escape key → modal closes.
  8. Re-open via link; click the dimmed backdrop → modal closes.
Pass:
  - Modal opens within 500 ms
  - All 4 close paths work
  - After close, the modal is no longer .show (visible = false)
  - Page scroll is restored (body no longer .modal-open)

### HRD-R1.10.3 — Sign Up button enables only after all required fields + T&C
Pre: HRD-R1.10.1 reached
Steps:
  1. Fill all required fields (User Type, First Name, Last Name, Email,
     Password, Confirm Password, Firm Name if attorney role).
  2. Verify Sign Up button still disabled (checkbox not checked).
  3. Check the T&C checkbox.
  4. Verify Sign Up button now enabled.
  5. Uncheck the T&C checkbox.
  6. Verify Sign Up button disabled again.
Pass:
  - All transitions are instantaneous (input/change events fire)
  - Disabled-state matches expected state at each step

### HRD-R1.10.4 — Happy-path registration with T&C accepted
Pre: HRD-R1.10.3 step 4 reached (button enabled)
Steps:
  1. Click Sign Up.
Pass:
  - Network: POST /api/public/external-signup/register returns 200
  - Page transitions to the post-signup state (verification email notice)
  - No T&C-related error in the inline banner

### HRD-R2.8.1 — Direct POST to /api/public/external-signup/register without AcceptTerms still succeeds
Pre: a fresh email
Steps:
  1. From a curl / fetch outside the UI, POST a valid signup payload.
  2. Confirm the response is 200 (server does NOT require AcceptTerms — OLD parity).
Pass:
  - 200 OK
  - User row created

### HRD-R2.8.2 — Submit attempt with checkbox unchecked is impossible via UI
Pre: HRD-R1.10.3 step 1 reached (all fields filled, checkbox unchecked)
Steps:
  1. Programmatically click the Sign Up button via Playwright.
Pass:
  - Button is reported disabled by the harness
  - No POST is sent to /api/public/external-signup/register

### HRD-R2.8.3 — Bypass attempt by removing `required` attribute via DevTools (defensive snippet)
Pre: HRD-R1.10.3 step 1 reached
Steps:
  1. In the browser console: document.getElementById('external-signup-terms-checkbox').required = false;
  2. Re-evaluate form: Sign Up button enables (binding loses required).
  3. Click Sign Up.
Pass (if defensive snippet from §5.5 is in place):
  - Inline banner shows "Please accept the Terms and Conditions before signing up."
  - No POST sent.
Pass (if defensive snippet is NOT in place):
  - POST is sent (this is acceptable; OLD also has no server-side gate).
  - Document the chosen behavior in the spec footnote.
```

### 6.2 Manual smoke (no Docker rebuild needed for JS-only changes)

```bash
# Pure wwwroot/global-scripts.js change — touch the file, refresh the page.
# AuthServer serves static files directly from the container; the
# Kestrel watcher may NOT pick up wwwroot changes without a restart.
docker compose -f docker-compose.yml -f docker-compose.testing.yml restart authserver
# Open in browser: http://falkinstein.localhost:44368/Account/Register
```

If `en.json` was modified, the AuthServer needs to reload the localization
resource:

```bash
docker compose -f docker-compose.yml -f docker-compose.testing.yml restart authserver api
```

### 6.3 Unit tests

There is no JS unit-test harness in this project's AuthServer side; the
existing `wwwroot/global-scripts.js` has no test coverage. **Do not add a JS
test framework as part of this ticket.** Playwright is the regression net.

---

## 7. Acceptance criteria

A reviewer should be able to verify each in under 30 seconds:

- [ ] Fresh `/Account/Register` page shows a T&C checkbox (unchecked) above the Sign Up button.
- [ ] Checkbox label includes a working link styled like a hyperlink.
- [ ] Clicking the link opens a Bootstrap modal with title "Terms and Conditions" and the localized body.
- [ ] Modal closes via ✕ icon, Close button, Escape key, and backdrop click.
- [ ] Sign Up button is disabled when:
   - Any other required field is empty, OR
   - The T&C checkbox is unchecked.
- [ ] Sign Up button enables only when all required fields are filled AND the checkbox is checked.
- [ ] After successful registration, the form behaves identically to today (no regressions in BUG-001..006 fixes).
- [ ] No `Account:Terms:*` localization key is missing in `en.json`.
- [ ] No new backend / database / API surface changes.
- [ ] Bypassing the checkbox via direct API POST still succeeds (OLD parity — no server-side gate).
- [ ] `ensureButtonDisabledBinding` now correctly evaluates `checkbox.checked` for `input[type=checkbox][required]`.
- [ ] The passive disclaimer text (current `ensureTermsBlock` body) is gone — no duplicate text appears.
- [ ] HRD-R1.10.{1..4} and HRD-R2.8.{1..3} pass.

---

## 8. Verification procedure (Docker)

```bash
# Restart only what needs to refresh.
docker compose -f docker-compose.yml -f docker-compose.testing.yml restart authserver
# If en.json changed:
docker compose -f docker-compose.yml -f docker-compose.testing.yml restart authserver api

docker compose ps   # confirm healthy

# Open in browser
#   - Direct: http://falkinstein.localhost:44368/Account/Register
#   - Via Angular link: http://falkinstein.localhost:4200/account/register
#     (the Angular route 404s and the user is funneled to the AuthServer page)

# Inspect the network panel for any 404s on /libs/abp/core/abp.js or
# /libs/bootstrap/js/bootstrap.bundle.min.js -- both must load for the modal
# and the L() helper to work.
```

Then drive the HARDENING-TEST-SUITE Phase 4 scenarios via the Playwright MCP
or by hand.

---

## 9. Decisions already made (do not re-litigate)

| Decision | Rationale |
|---|---|
| Implementation lives in `global-scripts.js`, not in a new Razor partial | The whole register form customization is already there. Adding a Razor partial would split the source of truth and complicate testing. |
| T&C body comes from a localization key, not hardcoded | The audit doc recommends this. Per-tenant override becomes possible later via ABP's localization-management feature. |
| No database tracking of acceptance | OLD parity. The audit doc explicitly says "Acceptance tracked in DB: OLD: NO — None — match OLD." |
| No server-side validation of `AcceptTerms` | OLD parity. The checkbox is a client-side gate only. |
| Bootstrap 5 modal (not a custom hand-rolled overlay) | AuthServer already bundles Bootstrap 5 CSS + JS. Reuse the standard chrome for free a11y + animation + focus management. |
| Checkbox uses `required` attribute | Lets the existing `ensureButtonDisabledBinding` pick it up with a 2-line patch. |
| Modal title text uses `textContent`, body uses `innerHTML` | Body is HTML by design (formatted T&C). Other strings are plain text. Only the body can carry markup — and only from the resource file, never from user input. |
| Use ABP's `abp.localization` client object for string lookup | The AuthServer bundle already exposes it on every page. No new layout partial needed. Fallback strings keep the script safe if the resource is missing. |

---

## 10. Open questions / decisions needed (flag if blocking, do not stall)

1. **T&C body source of truth** — write meaningful text for the resource file (recommended, sample provided in §5.1) OR copy OLD's Lorem ipsum verbatim for strict-bug-for-bug parity. Default: meaningful text via localization key.
2. **Per-tenant T&C body** — Phase 1 ships one English body for all tenants. Phase 2 may need per-tenant overrides (different clinics → different legal language). Track in `_branding.md` follow-up; not part of this ticket.
3. **Spanish locale** — should `es.json` carry a Spanish T&C body? Skip in this ticket; ABP localization fallback returns English if a key is missing.
4. **Resend Verification page** — that flow also displays terms? (No — Resend Verification is just an email-trigger button; no T&C acknowledgment needed.)
5. **Should the modal trap focus while open?** — Bootstrap 5's modal API handles this. Manual fallback path may not — acceptable trade-off.

---

## 11. Files to be created / edited (summary)

**New files**: none.

**Edited files** (2 + 1 doc):
1. `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json` — add 5 keys: `Account:Terms:Title`, `Account:Terms:CheckboxLabel`, `Account:Terms:LinkLabel`, `Account:Terms:Close`, `Account:Terms:Body`.
2. `src/HealthcareSupport.CaseEvaluation.AuthServer/wwwroot/global-scripts.js`:
   - Add `L(key, fallback)` helper.
   - **Replace** `ensureTermsBlock` (lines 299-317) with the checkbox+modal version.
   - **Add** `ensureTermsModal`, `showTermsModal`, `hideTermsModal` helpers.
   - **Patch** `ensureButtonDisabledBinding`'s `allFilled` predicate to handle `type=checkbox`.
   - (Optional defensive) early-return in `submitExternalSignup` if the checkbox is unchecked.
3. `docs/runbooks/HARDENING-TEST-SUITE.md` — append `HRD-R1.10.{1..4}` and `HRD-R2.8.{1..3}` scenarios.

---

## 12. Commit + PR plan

Suggested commit cadence (per `commit-format.md`):

1. `feat(auth): add T&C localization keys`
2. `feat(auth): replace passive T&C disclaimer with checkbox + modal`
3. `fix(auth): gate Sign Up disabled-state on checkbox state`
4. `docs(hardening): add HRD-R1.10 + HRD-R2.8 scenarios for T&C`

PR title: `feat(auth): T&C modal during registration (OLD parity)`

PR body sections (per `pr-format.md`): Summary, Motivation, Changes (grouped by
file), Test Plan (the HARDENING-TEST-SUITE scenarios), Risk/Rollback (`revert
the global-scripts.js diff if a layout regression appears`), Screenshots (modal
open + checkbox interaction), Dependencies (None), Breaking change (None),
HIPAA/PHI Impact (No PHI; T&C body is generic legal text, no real patient
data), Closes (no GH issue; reference this spec).

---

End of spec. The implementing session should be able to execute this end-to-end
in a single focused work block (estimated 90-150 minutes including Playwright
verification).
