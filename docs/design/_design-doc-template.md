---
type: design-doc-template
status: stable
audited: 2026-05-04
purpose: Standard skeleton every per-feature design.md follows. Lift this verbatim, fill every section, cite OLD file:line for every claim.
strict-parity: true
---

# Per-feature design.md template

This file is the canonical skeleton. Every `<feature>-design.md` under
`docs/design/` follows this structure. The design.md is the contract a
front-end implementer can scaffold from without re-reading 1000+ LOC of
OLD HTML; every cell + assertion cites OLD source or a screenshot path.

> Read order before writing a design.md:
>
> 1. `docs/parity/<feature>.md` (the audit; OLD-source citations + role gates)
> 2. OLD HTML/TS/SCSS files the audit names
> 3. `docs/design/_design-tokens.md`, `_shell-layout.md`, `_components.md`
> 4. `angular/src/app/<feature>/` (the NEW current state; if it exists)
>
> Then fill the sections below in order. Use ASCII only; use `-` for
> bullets; cite tokens by NEW CSS-var name. No invented behaviors.

## Frontmatter

```yaml
---
feature: <slug-matching-parity-audit-filename>
status: draft | screenshots-captured | complete | descoped-YYYY-MM-DD
audited: YYYY-MM-DD
old-source:
  - <relative path under P:\PatientPortalOld\... -- e.g. patientappointment-portal/src/app/components/login/login/login.component.html>
  - <add every OLD file the design references>
new-source:
  - <relative path under angular/src/app/... -- omit if NEW UI not built yet>
parity-audit: docs/parity/<feature>.md
shell-variant: 1-unauthenticated | 2-external-authenticated | 3-internal-authenticated
strict-parity: true
---
```

The frontmatter `feature` slug always matches the parity audit filename
(strip `.md`). The `shell-variant` field cites which of the three shells
in `_shell-layout.md` wraps this route -- so a reader knows whether the
feature lives behind login, behind the side-nav, or in the auth chrome.

## 1. Routes

State the OLD route(s) -> NEW route(s). Cite the OLD `app-routing.module.ts`
or component selector if the route is non-obvious.

```
- OLD: <old route> -> NEW: <new route>
- OLD: <old route 2> -> NEW: <new route 2>
```

If a feature has a deep-link (e.g., reset-password takes a `?token=`
query param), document it here.

## 2. Screen layout

ASCII wireframe for each distinct screen state, plus a relative link to
the captured OLD screenshot.

```
+--------------------------------------------------+
| <header / shell context, e.g. "auth-shell">       |
+--------------------------------------------------+
| <content layout in ASCII>                         |
+--------------------------------------------------+

OLD: ./screenshots/old/<feature>/01-<screen-slug>.png
NEW: ./screenshots/new/<feature>/01-<screen-slug>.png  (or "NEW UI not yet built")
```

If a screen has multiple states (empty / loading / populated / error),
include one ASCII block per state.

## 3. Form fields

One row per field, with these columns:

| Label | Field name | Type | Validation | Default | Conditional visibility | OLD citation |

- **Label**: the literal label string from OLD (verbatim, including any
  asterisk, quirky capitalization, or punctuation).
- **Field name**: the form control name in NEW.
- **Type**: `text | email | password | tel | textarea | select | multiselect | date | time | datetime | radio | checkbox | file | masked-text`.
- **Validation**: required / regex / min / max / cross-field rule. Cite
  the validator location in OLD (component.ts or backend).
- **Default**: value pre-filled on form open. Cite source.
- **Conditional visibility**: `*ngIf` predicate verbatim from OLD if the
  field is gated.
- **OLD citation**: `<file>:<line>` for the field's HTML declaration.

If the form has > 30 fields, group rows by section (e.g., "Patient
demographics", "Injury details") and add a sub-heading per group.

## 4. Tables / grids

For every list/grid in the feature:

| Column | Sortable | Filter type | Visible to roles | OLD citation |

Plus a free-text "Behavior" block underneath:

- Default sort.
- Pagination size (cite `SystemParameters` or component constant).
- Empty state copy + icon (verbatim from OLD).
- Row click action.
- Bulk actions, if any.

## 5. Modals + interactions

| Trigger | Modal title | Body content | Primary action | Secondary action | OLD citation |

For each modal, capture the primary and secondary button labels
verbatim (e.g., `Cancel | Confirm Reject`).

## 6. Buttons + actions

| Label | Variant | Permission gate | Pre-action confirm? | Success toast | Error toast | OLD citation |

- **Variant**: `primary | secondary | danger | link | link-italic` per `_components.md` Buttons section.
- **Permission gate**: NEW ABP permission key.
- **Pre-action confirm?**: Y/N -- if Y, link to the modal row in Section 5.
- **Success / Error toast**: verbatim message text from OLD's toaster
  call.

## 7. Role visibility matrix

| UI element | Patient | Adjuster | Applicant Atty | Defense Atty | Clinic Staff | Staff Supervisor | IT Admin |

Mark each cell:

- `Y` = visible.
- `R` = visible read-only.
- `N` = hidden.
- `(N/A)` = role does not apply (e.g., external roles for an internal-only feature).

Cite the OLD permission check location (`*ngIf="hasPermission(...)"` or
`AccessPermissionService` lookup) at the bottom of the table.

For external-user features that gate by appointment ownership /
accessor scope (rather than role alone), add a note explaining the gate
+ cite the OLD service method.

## 8. Branding tokens used

List every `--*` CSS variable consumed (cross-link to
`_design-tokens.md`):

- Layout / shell: `--brand-primary`, `--brand-primary-text`, `--bg-card`, `--border-light`
- Status pills: `--color-success`, `--color-danger`, etc.
- Spacing / radius: `--space-md`, `--radius-sm`

If the feature uses a primitive from `_components.md` (e.g.,
`<app-confirm-dialog>`), cite the primitive by name; do not re-spec it.

## 9. NEW current-state delta

Two paragraphs:

- **What `angular/src/app/<feature>/` already does.** File:line cites
  for the major components. If NEW does not yet have UI for this
  feature, write "NEW UI not yet built; this design.md drives the future
  build" and skip the rest of this section.
- **Gaps vs the OLD-anchored design above.** Bulleted list. Each bullet
  names the gap (missing field, wrong validation, missing modal, wrong
  empty state) + the OLD citation it violates.

## 10. Strict-parity exceptions

Per the branch CLAUDE.md "strict-parity exceptions" pattern, list every
deviation from OLD with a one-line rationale. Examples:

- DOCX -> PDF: report exports use PDF instead of OLD's DOCX (per branch
  CLAUDE.md "Reports -- PDF replaces DOCX").
- ABP confirm dialog: NEW uses `<app-confirm-dialog>` (ABP/ng-bootstrap
  base) instead of OLD's `<rx-dialog>`. Token contract preserved.
- Per-tenant brand tokens: NEW resolves logo / colors / clinic name via
  `BrandingService` per `_branding.md`; OLD hard-codes per deploy.

If a bug from OLD is intentionally replicated and flagged for testing,
cross-reference the `docs/parity/_parity-flags.md` entry.

## 11. OLD source citations (consolidated)

Re-list every OLD file the design references, in alphabetical order:

```
- patientappointment-portal/src/app/components/<area>/<feature>.component.html
- patientappointment-portal/src/app/components/<area>/<feature>.component.ts
- patientappointment-portal/src/app/components/<area>/<feature>.component.scss
- PatientAppointment.Domain/<Module>/<Feature>Domain.cs (if backend behavior is referenced)
```

This consolidated list helps a reviewer spot-check that every cited
file is preserved as part of the design's source-of-truth.

## 12. Verification (post-implementation)

A short GitHub-style checklist for the implementer + Adrian:

- [ ] Every form field in Section 3 renders with the correct label,
      type, default, and validation message.
- [ ] Every modal in Section 5 fires from the correct trigger and shows
      the correct body + buttons.
- [ ] The role visibility matrix in Section 7 is enforced (UI test +
      ABP authorization service test).
- [ ] Every branding token in Section 8 resolves through
      `BrandingService` / CSS-variable cascade (per-tenant override
      flips correctly).
- [ ] Pixel diff of NEW screenshot vs OLD screenshot < 1px (modulo
      anti-aliasing) for the same data shape.

## Anti-patterns (do NOT do these in any design.md)

- Do NOT invent behavior or copy NEW's current-state crude logic into
  the design. Every claim cites OLD source or a screenshot.
- Do NOT skip the role visibility matrix. Even single-role features get
  a row to make the gate explicit.
- Do NOT inline Bootstrap class names ("btn btn-secondary") -- cite
  the variant by name from `_components.md` and let the implementer
  pick the class.
- Do NOT duplicate `_design-tokens.md` content. Cite tokens by NEW
  CSS-var name (e.g., `--brand-primary`).
- Do NOT use real patient data in screenshots or examples (HIPAA
  rule -- see `.claude/rules/hipaa-data.md`).
- Do NOT use Unicode (smart quotes, em dashes, decorative symbols) -- ASCII only.
