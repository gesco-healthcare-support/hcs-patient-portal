---
title: Redesign Foundation Slice - Design
status: in-progress
date: 2026-06-12
slice: foundation (slice 1 of the UI-redesign integration)
base-branch: feat/frontend-rework
slice-branch: feat/redesign-foundation
---

# Redesign Foundation Slice - Design

## Goal

Stand up the shared visual foundation that every redesigned page depends on, with
zero user-facing page change. After this slice, any subsequent page slice can be
pixel-faithful to the `design_handoff_appointment_portal/` prototypes without
re-deriving tokens, fonts, icons, or status styling.

## Non-goals (deferred to later slices)

- No external or internal shell, no real page.
- No LeptonX removal; no edits to existing `.lpx-*` hacks in `styles.scss`.
- No old component deleted (this slice replaces no page).
- Toast service, 3D action cards, filter chips, modals, wizard stepper: each lands
  with its owning page slice, not here.

## Context (current state, verified 2026-06-12)

- App is LeptonX-themed; the redesign drops LeptonX for the authenticated surface
  (replaced shell-by-shell in later slices).
- `angular/src/styles/_brand.scss` already exists (Phase 19a) and owns the
  `--brand-*` ground-truth layer (brand hexes, type sizes, layout dims, logo slots,
  clinic name). `tokens.css` is a superset that adds a modernized layer.
- Bootstrap is present transitively (`@abp/ng.theme.shared` ~10.0.2 +
  `@volosoft/abp.ng.theme.lepton-x` ~5.0.2), so `.btn`/`.card`/`.badge` collide.
- No icon library, no `@angular/material`, no font package installed.
- Existing components use scoped feature-root classes (`.patient-home`,
  `.appointment-add`); no global utility-class prefix convention exists.

## Decisions

### D1 - Token port: modernized layer only, names verbatim
Create `angular/src/styles/_tokens.scss` holding the modernized layer of
`tokens.css` (lines 26-96) as `:root` custom properties: `--blue-*`, `--green-*`,
`--n-*`, `--st-*` (incl. `--st-purple-*` for Info Requested), `--surface-*`,
`--border`/`--border-strong`, `--r-*`, `--sh-*`, `--font`/`--font-num`, `--space`.

- OMIT the `--brand-*` block (lines 10-24): `_brand.scss` already owns it (with
  more). Overlapping values are identical, so no conflict; `_brand.scss` stays the
  single `--brand-*` authority (DRY).
- Keep token names byte-identical to `tokens.css`. Rationale: every page's
  prototype CSS in `design_handoff_appointment_portal/styles/*.css` references
  these exact names, so identical names let each page's CSS port nearly 1:1 later.
- Wire via `@use 'styles/tokens';` alongside the existing `@use 'styles/brand';`
  in `styles.scss`.

### D2 - Fonts: static `@fontsource/roboto` (v5.2.10, verified on npm)
Self-host Roboto via the static package, importing only weights 300/400/500/700/900
(latin). Deciding factor: the static package registers `font-family: 'Roboto'`,
matching `tokens.css`/`_brand.scss` verbatim (zero stack edits). We need exactly
those 5 fixed weights, so the variable font's arbitrary-weight flexibility is unused
and its multi-axis (wght+wdth) file is wasted bytes; the variable package also
registers `'Roboto Variable'`, which would force an alias change everywhere.
- Rejected: `@fontsource-variable/roboto` (family-name mismatch, unused flexibility).
- Rejected: Google Fonts CDN (third-party request on a PHI-adjacent app, breaks offline).
- To confirm at build: exact Angular wiring (angular.json `styles` array vs
  `styles.scss` import) and the installed `@font-face` family string.

### D3 - Icons: port `icons.js` as a standalone component
33 fixed line icons (24x24 viewBox, `fill=none`, `stroke=currentColor`,
stroke-width 1.8, round caps/joins), several custom (`stetho`, `lifebuoy`, funnel
`filter`, `money`). Port the set as `ap-icon`.
- Rejected: Lucide/Phosphor library - custom glyphs would not map 1:1 (visual drift
  vs a pixel-perfect mandate) and adds a dependency for 33 fixed glyphs.

### D4 - `ap-` namespace for global utility classes
Prefix all global utility classes with `ap-` to avoid Bootstrap collisions.
- Rejected: CSS `@layer` - ABP's Bootstrap is unlayered, and unlayered styles beat
  layered ones, so layering our utilities would make them lose to Bootstrap.
- Rejected: wrapper scope (`.rx-app .btn`) - raises specificity, needs a wrapper on
  every redesigned root, still risks element-level bleed.
- Component-level styles ride Angular's default emulated view-encapsulation
  (auto-scoped), so the global utility set stays small.

### D5 - Coexistence: additive only
All artifacts are additive `:root` custom properties, new files, and new `@use`
lines. No LeptonX removal. The app keeps running on LeptonX; redesigned pages adopt
these tokens/components as later slices build them.

## Deliverables (file-by-file)

| # | File | Change | Approach |
|---|---|---|---|
| 1 | `angular/package.json` + lockfile | add `@fontsource/roboto@^5.2.10` | code |
| 2 | `angular/src/styles/_tokens.scss` | new - modernized token layer (D1) | code |
| 3 | `angular/src/styles/_ap-utilities.scss` | new - `ap-btn`/`ap-card`/`ap-field` base + focus ring | code |
| 4 | `angular/src/styles.scss` | `@use` tokens + utilities; load Roboto weights; base body font/bg | code |
| 5 | `angular/src/app/shared/ui/icon/icon.registry.ts` | new - 33-icon SVG map + `IconName` union | code |
| 6 | `angular/src/app/shared/ui/icon/icon.component.ts` | new - `ap-icon` standalone component | test-after |
| 7 | `angular/src/app/shared/ui/status-pill/status-pill.component.ts` | new - `ap-status-pill` standalone component | test-after |
| 8 | `angular/src/app/_dev/foundation-preview.component.ts` | new - THROWAWAY verification page | code |
| 9 | `angular/src/app/app.routes.ts` | add throwaway `/foundation-preview` route | code |
| 10 | `Domain.Shared` `en.json` localization | add any missing status labels referenced by the pill | code |

## Component contracts

### `ap-icon` (IconComponent)
- Inputs: `name: IconName` (typed union of the 33 names); `size: number = 18`;
  optional `label?: string` (when set, `role="img"` + `aria-label`; otherwise
  `aria-hidden="true"` - decorative by default).
- Renders one `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor"
  stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">` with the
  registry's inner markup. Color is inherited via `currentColor`, so callers set
  color through CSS/tokens.
- Implementation note: the inner markup are static trusted constants; bind them via
  `DomSanitizer.bypassSecurityTrustHtml` (no dynamic/user input, so this is safe and
  documented as such). Fragments are only `circle/line/path/polyline/rect`.

### `ap-status-pill` (StatusPillComponent)
- Input: `status: 'Pending' | 'InfoRequested' | 'Approved' | 'Rejected' |
  'Cancelled' | 'Rescheduled'` (string-literal union for this slice; page slices map
  their proxy status value to it).
- Renders `<span class="ap-status-pill ap-status-pill--{kind}"><i dot></i>{{ label
  }}</span>` - dot AND text always (never color-alone; a11y, per token comment).
- Planned token mapping (from README banner semantics; confirm exact chip tones
  against the prototype status-chip CSS at build):
  - Pending -> `--st-pending-*`
  - InfoRequested -> `--st-purple-*`
  - Approved -> `--st-approved-*`
  - Rejected -> `--st-rejected-*`
  - Cancelled -> `--st-rejected-*` (README: "Rejected/Cancelled red")
  - Rescheduled -> `--st-info-*` (README: "Rescheduled blue")
- Labels via `| abpLocalization`; reuse existing status localization keys where
  present, add only the missing ones to `en.json`.

## Global `ap-` utilities (slice scope)
- `.ap-btn` + `--primary` (blue), `--ghost`, `--accent` (green), `--danger` (red)
- `.ap-card` (surface + `--r-lg` + `--sh-md`)
- `.ap-field` base for input/select/textarea
- Shared focus-ring using brand blue
(Chips, banners, modals, etc. deferred to their pages.)

## Verification
- THROWAWAY `/foundation-preview` route renders: full token palette swatches, all 33
  icons (labeled), all 6 status pills, button variants, a card, a field. Lets Adrian
  eyeball fidelity against `tokens.css`. Removed after sign-off (this is the "old
  component" the slice retires).
- Build proof: `npx ng build --configuration development` clean (never `ng serve` -
  Vite breaks ABP DI).
- Unit tests (test-after): `ap-icon` renders the expected registry markup for a
  given name; `ap-status-pill` maps each status to the right kind class + label.

## Risk / rollback
- Blast radius: low. Shared-file edits are additive only - `styles.scss` (`@use`
  lines + font import), `app.routes.ts` (one throwaway route), `package.json` (one
  dep). New files otherwise.
- Rollback: revert the slice PR.

## Branch / PR workflow
- `git switch feat/frontend-rework && git switch -c feat/redesign-foundation`.
- Atomic commits; PR into `feat/frontend-rework` after Adrian's live sign-off on the
  preview route. Do not push until Adrian asks.

## Open items (build-time confirmations, not blockers)
- Exact status-chip tones (Cancelled red vs neutral) vs prototype CSS.
- Which status localization keys already exist vs need adding.
- Exact Fontsource Angular wiring + installed `@font-face` family string.
