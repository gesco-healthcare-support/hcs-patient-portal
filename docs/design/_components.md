---
type: shared-components
status: draft
audited: 2026-05-04
old-source-roots:
  - P:\PatientPortalOld\patientappointment-portal\src\assets\css\rx-control-design\common.css
  - P:\PatientPortalOld\patientappointment-portal\src\assets\css\rx-control-design\dialog.css
  - P:\PatientPortalOld\patientappointment-portal\src\assets\css\rx-control-design\popup.css
  - P:\PatientPortalOld\patientappointment-portal\src\assets\css\rx-control-design\toast.css
  - P:\PatientPortalOld\patientappointment-portal\src\assets\css\rx-control-design\datepicker.css
  - P:\PatientPortalOld\patientappointment-portal\src\assets\css\rx-control-design\select.css
  - P:\PatientPortalOld\patientappointment-portal\src\assets\css\rx-control-design\tag.css
  - P:\PatientPortalOld\patientappointment-portal\src\assets\css\rx-control-design\spinner.css
  - P:\PatientPortalOld\patientappointment-portal\src\assets\css\rx-control-design\table-filter.css
  - P:\PatientPortalOld\patientappointment-portal\src\assets\css\rx-control-design\time.css
  - P:\PatientPortalOld\patientappointment-portal\src\assets\css\rx-control-design\tooltip.css
  - P:\PatientPortalOld\patientappointment-portal\src\assets\css\vendor\app.css
cross-reference:
  - docs/design/_design-tokens.md
  - docs/design/_shell-layout.md
strict-parity: true
---

# Shared components -- OLD-anchored UI primitive contract

The reusable UI primitives every Phase 1 feature consumes: buttons,
form fields, modals, dropdowns/select, tables (filter + pagination),
toasts, badges, spinners, tooltips, popups. Each section captures
OLD's anatomy + behavior + token usage with file:line citations, plus
the NEW Angular component name + path.

> Per-feature design docs do not re-spec these primitives. They cite
> by name (e.g., `<rx-dialog>` -> NEW `<app-confirm-dialog>`).

## OLD's primitive library: `rx-control-design`

OLD ships a custom Angular control library at
`patientappointment-portal/src/assets/css/rx-control-design/`
(10 files, 1843 LOC) plus matching component selectors:

| Selector | OLD source | Renders |
|---|---|---|
| `<rx-toast>` | `start/app.component.html`:2; `toast.css` | Top-right toast notifications |
| `<rx-dialog>` | `start/app.component.html`:3; `dialog.css` | Confirmation modals |
| `<rx-popup>` | `start/app.component.html`:4; `popup.css` | Validation modals (over-modal) |
| `<rx-spinner>` | `start/app.component.html`:5; `spinner.css` | Page-loading overlay |
| `<rx-select>` | `select.css`, `tag.css` | Searchable dropdown (Chosen.js base) |
| `<rx-tag>` | `tag.css` | Multi-select tag input |
| `<rx-date>` | `datepicker.css` | Date picker (Bootstrap datepicker base) |
| `<rx-mask>` | (referenced in `common.css`:16) | Masked input |
| `<rx-time>` | `time.css` (4 lines -- minimal) | Time input |
| `<rx-tooltip>` | `tooltip.css` (10 lines) | Inline hover tooltip |

These four (`rx-toast`, `rx-dialog`, `rx-popup`, `rx-spinner`) are
mounted globally inside `start/app.component.html`:1-6 -- they live
above every shell variant. The rest are imported per-feature.

NEW does NOT port the rx-control library directly. The NEW
equivalent strategy:

| OLD selector | NEW component | Location |
|---|---|---|
| `<rx-toast>` | ABP's `toasterService` (`@volo/abp.ng.theme.shared`) | injected per AppService call |
| `<rx-dialog>` | `<app-confirm-dialog>` | `angular/src/app/shared/confirm-dialog/` |
| `<rx-popup>` | `<app-validation-popup>` | `angular/src/app/shared/validation-popup/` |
| `<rx-spinner>` | ABP's `loaderInterceptor` + global `<abp-loader-bar>` | wired in `app.component.ts` |
| `<rx-select>` | NgbTypeahead + Bootstrap select (Bootstrap 5 / `@abp/ng.components`) OR ABP's lookup-component | per feature decision |
| `<rx-date>` | NgbDatepicker | per feature |
| `<rx-mask>` | `ngx-mask` (already in NEW deps) | per feature |
| `<rx-time>` | NgbTimepicker | per feature |
| `<rx-tooltip>` | `[ngbTooltip]` directive | inline |
| `<rx-tag>` | `<ng-select>` with `[multiple]="true"` | per feature |

Token-anchored visual contract is preserved; underlying library swap
is a strict-parity exception (justification: rx-control-design is
unmaintained jQuery-era code; ABP / ng-bootstrap give us better a11y +
keyboard handling for free without a behavioral delta).

## Buttons

OLD inherits Bootstrap 4 button base (`bootstrap.min.css`) with token
overrides in `vendor/app.css`:16-25. Anatomy:

```css
.btn {
  min-height: 38px;          /* vendor/app.css:17 */
  border-radius: 2px;        /* vendor/app.css:18  -> NEW --radius-sm */
  font-size: 15px;           /* vendor/app.css:23  -> NEW --fs-button-login */
  font-weight: bold;         /* vendor/app.css:24  -> NEW --fw-bold */
}
```

### Variant matrix (all observed in OLD)

| Variant | OLD class | OLD usage | Tokens |
|---|---|---|---|
| Primary CTA (form submit) | `btn btn-secondary btn-block` | `login.component.html`:30, `forgot-password.component.html`:20 | bg `var(--brand-primary)` text `#fff` -- NOTE: OLD uses `btn-secondary` for primary CTAs; this is a Bootstrap 4 idiosyncrasy of the theme, NOT a typo |
| Primary action (page) | `btn btn-primary` | `home.component.html`:6, 16 | bg `var(--brand-primary)` text `#fff` |
| Danger (Delete / Reject) | `btn btn-danger` | `demo-falkinstein.css`:18-21 (`btn-danger.pull-right`) | bg `var(--color-danger-button)` text `#fff` |
| Inline link in form | `font-sm text-black font-italic` | `login.component.html`:23 ("Forgot password!") | text `#000` italic |
| Toggle (sidenav) | `nav-item nav-link px-0` with `<i class="ion ion-md-menu">` | `top-bar.component.html`:43-46 | text `var(--brand-primary-text)` |
| Page-link (pagination) | `page-link` | `table-filter.css`:98-114 | bg `transparent`, color `#0071c1`, active border-bottom |

OLD's "pull-right" variant adds 22px horizontal padding + forced 40px
line-height (`demo-falkinstein.css`:18-21).

### NEW button mapping

NEW's shared `<app-button>` wrapper (`angular/src/app/shared/button/`)
exposes the variants by name:

```html
<app-button variant="primary" [block]="true">Sign In</app-button>
<app-button variant="danger" (click)="onReject()">Reject</app-button>
<app-button variant="link-italic">Forgot password!</app-button>
```

Implementation reuses ABP's `<button abpButton>` directive where
available; otherwise inline Bootstrap classes with the brand-token
cascade.

## Form fields

### Anatomy (OLD)

```html
<div class="form-group position-relative">
  <input type="text" class="form-control" placeholder="Email Address"
         formControlName="emailId">
  <i class="ion ion-md-mail d-block"></i>          <!-- decorative leading icon -->
</div>
```
Source: `login.component.html`:14-17.

`form-control` styling -- shared via Bootstrap 4 + `vendor/app.css`:16-19:

- `min-height: 38px`
- `border-radius: 2px` (-> `--radius-sm`)
- inherits `--font-family-base`, `--text-body`

### Validation states

OLD's `common.css`:1-19 forces a red bottom-border on any `ng-invalid`
control:

```css
.form-control.ng-invalid,
.custom-select.ng-invalid {
  border-bottom: 1px solid #dc3545 !important;
}
```

NEW preserves the visual: invalid -> red bottom-border. Implemented via
`var(--color-danger)` and the same `ng-invalid` class hook (Angular
Reactive Forms).

### Special cases

- `rx-select.ng-invalid .search-field input.form-control` (`common.css`:6-8) -- inside-select form-control; NEW handles via `<ng-select>`'s `class.ng-invalid`.
- `.ap-form.form-control.ng-invalid` (`common.css`:20-23) -- forced no bottom-border on the appointment form's address field; NEW preserves the override via the same class hook.

### Field-with-label pattern

```html
<div class="form-group">
  <label>Field Label *</label>
  <input class="form-control" formControlName="x" />
  <small class="form-text text-danger" *ngIf="form.controls.x.invalid && form.controls.x.touched">
    Validation message
  </small>
</div>
```

Per-feature design docs cite "form-field" pattern + the label string
+ the validation message string (verbatim from OLD).

## Modals (`<rx-dialog>` -> `<app-confirm-dialog>`)

OLD source: `dialog.css`:1-77, mounted at `start/app.component.html`:3.

### Anatomy

- Backdrop: full-viewport flex container with `background-color: rgba(0,0,0,.5)` (`var(--scrim-modal)`), z-index `9999` (`var(--z-dialog)`).
- Dialog box: `min-width: 400px; width: 500px;` (`dialog.css`:43-44), white bg, modal box-shadow (`var(--shadow-modal)`), border-radius `3px` -> `--radius-md`.
- Open animation: `transform: scale(0,0)` -> `scale(1,1)` over `0.5s ease-in-out` (`dialog.css`:62-72) -> `var(--motion-modal)`.
- Header / footer: zero border (`dialog.css`:74-76).

### Behavior

- Display class toggle: `dialogin` shows; `dialogout` hides
  (`dialog.css`:5, 27-29). NEW uses Angular `*ngIf` + `[@modalAnim]`
  trigger.
- Buttons: typically `Cancel` (btn-link) + primary CTA (btn-primary or
  btn-danger depending on action).
- Body font: `Segoe UI` family + 13px (`dialog.css`:8, 23). NEW maps
  to `var(--font-family-base)` + `var(--fs-dialog-header)`.
- Title: 20px / 24px line-height (`dialog.css`:8 -- inherited from
  `.dialogin` font shorthand). NEW: `var(--fs-dialog-title)` /
  `var(--lh-dialog-title)`.

### NEW component: `<app-confirm-dialog>`

```html
<app-confirm-dialog
  [open]="confirmOpen"
  [title]="'Confirm Reject'"
  [body]="'Reject this appointment? Notes will be emailed to the patient.'"
  [primaryLabel]="'Reject'"
  [primaryVariant]="'danger'"
  [secondaryLabel]="'Cancel'"
  (confirm)="onConfirmReject()"
  (cancel)="onCancelReject()">
</app-confirm-dialog>
```

Implementation: `angular/src/app/shared/confirm-dialog/`.

## Validation popup (`<rx-popup>` -> `<app-validation-popup>`)

OLD source: `popup.css`:1-25.

A second-tier modal layered above `<rx-dialog>` for showing a list of
validation errors (e.g., "Please fix these issues before continuing"):

- Backdrop z-index `66000`; popup z-index `66101` (above `rx-dialog`'s `9999`).
- Black backdrop (no opacity in OLD CSS -- visually equivalent to a
  full-black overlay because OLD's popups are short-lived).

NEW implements as `<app-validation-popup>` with the same layering
contract via `--z-popup` and a higher inner z-index. Realistically
used only when OLD called it; per-feature docs cite the trigger.

## Toast (`<rx-toast>` -> ABP toaster)

OLD source: `toast.css`:1-179.

### Container positions (Bootstrap-style helpers)

`top-right`, `bottom-right`, `bottom-left`, `top-left`, `top-center`,
`bottom-center`, `top-full-width`, `bottom-full-width`. Defaults to
top-right at `top:12px; right:12px` (`toast.css`:14-17).

### Toast-card anatomy

```css
.toast-card {
  pointer-events: auto;
  margin: 0 0 6px;
  padding: 18px 20px;             /* --space-toast-y / --space-toast-x */
  min-width: 300px;
  max-width: 350px;
  border-radius: 3px;             /* --radius-md */
  background-position: 15px center;
  box-shadow: 0 0 12px #999;      /* --shadow-toast */
  color: #fff;
  opacity: .8;
  text-align: left;
  display: flex;
}
.toast-card:hover { opacity: 1; }
.toast-card .toast-message { color: #fff; padding-right: 15px; }
.toast-card .toast-title  { text-transform: uppercase; margin: 16px; font-size: 18px; }
.toast-card .close        { color: #fff; margin-left: auto; font-size: 16px; }
.toast-card .icon         { font-size: 22px; margin-right: 15px; align-self: center; }
```
Source: `toast.css`:132-179.

### Variants (semantic colors)

| Variant | OLD class | OLD bg | NEW token |
|---|---|---|---|
| Success | `success` | `#51a351` | `--color-success` |
| Error | `error` | `#bd362f` | `--color-danger` |
| Warning | `warning` | `#f89406` | `--color-warning` |
| Info | `info` | `#2f96b4` | `--color-info` |

### NEW toast mapping

NEW uses ABP's `Toaster` service (`@volo/abp.ng.theme.shared`):

```typescript
this.toaster.success('Your appointment has been approved.', 'Success');
this.toaster.error('Please provide rejection notes.', 'Error');
```

ABP's default toast skin matches the OLD aesthetic (top-right,
short-lived, semantic-colored). LeptonX overrides at Phase 19b adjust
the exact bg/border to OLD's tokens.

## Spinner (`<rx-spinner>` -> ABP loader)

OLD source: `spinner.css`:1-124.

Two visual variants:

1. `.page-loading` -- full-viewport white bg overlay shown during
   initial app boot (line 21-32). Z-index `9999`.
2. `.loadin` -- request-in-flight overlay with semi-transparent white
   bg (`rgba(255,255,255,0.75)`), Segoe UI 20px/24px text, blue color,
   z-index `9999` (line 34-55). Visible only when ANY HTTP request is
   pending.

Toggle classes: `loadin` / `loadout` (`spinner.css`:34, 57-58).

### NEW spinner mapping

NEW uses ABP's `LoaderBar` (top-page progress bar, default in LeptonX)
+ optional `LoaderService` for full-page overlays. Per-feature design
docs decide which to use:

- Background fetch / list refresh: top-page bar.
- Form submit / blocking action: full-page overlay (matches OLD's
  `.loadin`).

## Tables (filter + pagination)

OLD source: `table-filter.css`:1-162.

### Anatomy

Standard Bootstrap 4 `.table` plus per-column inline filter dropdowns:

```html
<table class="table table-hover">
  <thead>
    <tr>
      <th>
        Status
        <span class="filter">
          <a class="filter-toggle" (click)="toggleFilter()">
            <i class="ion ion-md-funnel"></i>
          </a>
          <div class="fltrcntnt" [class.show]="filterOpen">
            <div class="filterheader"><h6>Filter</h6></div>
            <div class="filterbody">
              <!-- checkbox list of values -->
            </div>
            <div class="filterfotoer">
              <button class="btn btn-secondary">Apply</button>
            </div>
          </div>
        </span>
      </th>
      ...
    </tr>
  </thead>
  ...
</table>
```

### Filter dropdown behavior

- Closed: `transform: scale(0,0)` (line 25-29).
- Open: `transform: scale(1,1)` (line 39-44).
- Width: 350px (line 23).
- Bg: `#eeeeee` header (line 24), `#ffffff` body (line 71).
- Border: `1px solid #cccccc` (line 61, 73).
- Has a "tail" pointing up at the filter icon: `border-bottom: 8px solid #eeeeee` triangle (line 51-57).

### Selected-row state

```css
.table-hover tbody tr.row-selected {
  background-color: rgba(0, 0, 0, 0.18);
  cursor: pointer;
}
```
Source: `table-filter.css`:94-97.

### Pagination

```css
.pagination .page-item.active .page-link {
  background: transparent;
  color: #0071c1;                  /* note: hard-coded primary; NEW uses var(--brand-primary-text) */
  border-bottom: 1px solid #0071c1;
}
```
Source: `table-filter.css`:108-112.

### NEW table mapping

NEW uses ngx-datatable today (per the frontend audit). Visual contract:

- Apply OLD's bottom-border pagination active-state via custom CSS in
  the shared table SCSS module.
- Per-column filter modeled as ngx-datatable's `header-template` slot.
- Selected row: `[rowClass]="getRowClass"` returning `'row-selected'`.

Per-feature docs that show a list/grid cite this section's pattern +
list the column-by-column spec (filter type, sort behavior, default
sort).

## Dropdowns / Select (`<rx-select>` -> `<ng-select>`)

OLD source: `select.css`:1-341 (Chosen.js base + customizations).

### Anatomy

- Closed: shown as `.chosen-single` -- 34px tall, white bg, 1px `#e5e6e7` border, 4px radius (line 78-96).
- Open: dropdown panel `.chosen-drop` slides down with `border-bottom-radius` 4px (line 5-14).
- Search input inside dropdown: max-height 240px scroll on results (line 21-26).
- Highlighted result: `background-color: #0071c1; color: white` (line 46-50). Hard-coded blue maps to `var(--brand-primary)`.
- Disabled result: `color: #777777` (line 53-55).
- No results: `background: #eeeeee` (line 56-58).

### NEW select mapping

`<ng-select>` (`@ng-select/ng-select`) is already in NEW deps. Visual
contract preserved via custom theme variables in
`angular/src/styles/_ng-select-overrides.scss`:

```scss
.ng-select.ng-select-opened > .ng-select-container {
  border-color: var(--brand-primary-focus-border);
  box-shadow: var(--shadow-input-focus);
}
.ng-dropdown-panel .ng-option-marked,
.ng-dropdown-panel .ng-option.ng-option-selected {
  background-color: var(--brand-primary);
  color: #fff;
}
```

## Multi-select / Tags (`<rx-tag>` -> `<ng-select [multiple]>`)

OLD source: `tag.css`:1-317. Same Chosen.js base as `<rx-select>`,
plus pill rendering for selected values:

```css
.chosen-container .chosen-results li em {
  background: #feffde;             /* faint yellow highlight on filter match */
  font-style: normal;
}
```

NEW uses `<ng-select [multiple]="true">` with the same theme
overrides. Selected pills inherit `var(--brand-primary)` bg.

## Date picker (`<rx-date>` -> `NgbDatepicker`)

OLD source: `datepicker.css`:1-582.

### Anatomy

- Z-index: `10664` (line 2) -- stacks above `<rx-dialog>` so a date
  picker inside a modal doesn't get clipped. NEW: dedicated
  `--z-date-picker` if needed; default ngb stacks correctly.
- Padding: 4px (line 3).
- Border-radius: 4px (line 6).
- Inline width: 220px (line 11).

### Calendar grid

- Hover/focused day: `background: #eee` (line 116).
- Old/new month days: `color: #999` (line 122, 128).
- Today: `background: #d9edf7` (line 133, light blue) -- NEW maps to
  `--color-info-soft` (a 90%-lightness derivative; we add this to
  `_design-tokens.md` if cited).
- Range/highlighted: yellow gradient `#fdd49a` -> `#fdf59a` (line 141-150).

### NEW date-picker mapping

NgbDatepicker is already in NEW deps. SCSS overrides in
`angular/src/styles/_ngb-datepicker-overrides.scss` apply OLD's day-cell
states.

## Tooltip (`<rx-tooltip>` -> `[ngbTooltip]`)

OLD source: `tooltip.css` (10 lines, minimal). Bootstrap-default
look-and-feel; no custom tokens. NEW uses `[ngbTooltip]` directive
inline with default ngb-bootstrap theme.

## Time picker (`<rx-time>` -> `NgbTimepicker`)

OLD source: `time.css` (4 lines, near-empty). NEW uses
`NgbTimepicker` with default theme.

## Mask (`<rx-mask>` -> `ngx-mask`)

OLD source: hooks defined in `common.css`:16-18 (red-bottom-border on
invalid). NEW uses `ngx-mask` (already in deps); same `ng-invalid`
visual contract.

## Cards (Bootstrap 4 base + custom)

OLD anatomy:

```css
.socal-card-title {
  display: flex;
  padding: 10px 14px;
  justify-content: space-between;
  align-items: center;
  flex-wrap: nowrap !important;
}
```
Source: `demo-falkinstein.css`:23-29.

Used as the standard panel header for booking-form sections, doctor
list, etc. NEW preserves the layout via a shared `<app-card>` wrapper.

## "socal-label" status pill

```css
.socal-label {
  border: 1px solid #06519f;        /* var(--brand-primary) */
  background: #06519f;
  color: white;
  padding: .438rem 1.125rem;
  border-radius: 5px;               /* --radius-lg */
  text-align: center;
  font-size: .894rem;               /* --fs-label */
}
```
Source: `demo-falkinstein.css`:39-47.

Used as a status / category badge throughout the appointment views.
NEW: `<app-status-label>` component or inline `.socal-label` class
preserved.

## Page-loader transition

OLD source: `index.html` ships a commented-out `<div class="page-loader">`
with `.bg-primary` (line 14-16); never live. NEW does not implement.

## Empty / loading / error states

OLD does not have a standardized empty-state component. Each feature
inlines its own "No records found" copy (e.g., on the appointments
list). NEW standardizes via `<app-empty-state>`:

```html
<app-empty-state
  *ngIf="appointments.length === 0 && !loading"
  icon="far fa-calendar-times"
  title="No appointments yet"
  subtitle="Use the buttons above to book your first appointment.">
</app-empty-state>
```

Per-feature docs specify the icon + title + subtitle copy verbatim
from OLD where available; supply new copy where OLD is silent.

## Form-step indicator (booking flow)

The OLD booking page (`appointment-edit.component.html`, 1008 lines)
uses a custom multi-step form layout. Captured per-feature in
`docs/design/external-user-appointment-request-design.md` (Phase 11
follow-up). This shared doc does not enumerate it.

## NEW current-state delta

`angular/src/app/shared/` currently contains only
`top-header-navbar.component.ts`. None of the shared components
enumerated above (`<app-confirm-dialog>`, `<app-validation-popup>`,
`<app-empty-state>`, `<app-status-label>`, `<app-card>`,
`<app-button>`) are built yet.

Phase 19a + per-feature work fills the gap. Each per-feature design
doc names the shared components it consumes; if the component does
not yet exist, the audit-doc lifecycle annotation marks it
`[BLOCKED on shared component <name>]` until built.

## Strict-parity exceptions (cross-cutting)

1. **rx-control-design library swap.** OLD's hand-rolled jQuery-era
   controls -> ABP / ng-bootstrap / ng-select equivalents. Token
   contract preserved; underlying library is modern and
   accessibility-friendly. Documented in `_design-tokens.md`'s
   strict-parity exceptions table.
2. **Toast color tokens hard-coded in OLD CSS** (`toast.css`:66, 95,
   103, 110) -> NEW uses CSS variables. Visible pixels identical.
3. **OLD `0071c1` hard-coded primary** in `select.css`:47 +
   `table-filter.css`:110. NEW substitutes `var(--brand-primary)` /
   `var(--brand-primary-text)` so per-tenant theming flows through.
4. **`!important` overrides** in OLD CSS to defeat Bootstrap. NEW
   prefers cascade via CSS-variable layers; explicit `!important`
   only when token cascade fails.

## Verification (per primitive)

When a per-feature design doc cites a primitive, manual QA verifies:

- [ ] Visual delta from OLD screenshot < 1 pixel difference (modulo
      anti-aliasing) for the same data shape.
- [ ] Keyboard navigation (Tab, Esc, Arrow keys) works -- bonus over
      OLD's inconsistent jQuery-era handling.
- [ ] Color contrast meets WCAG AA (`var(--brand-primary)` on white
      passes; `var(--text-disabled)` on white at 18px+ passes).
- [ ] Per-tenant theme override flips primary color end-to-end
      (button, link, datepicker today-cell, ng-select highlight,
      pagination active).
