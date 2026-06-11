# Design: Paralegal acting on behalf of an attorney (delegate model)

- **Date:** 2026-06-10
- **Status:** design approved (pending spec review); implementation not started
- **Author:** Adrian (with Claude)
- **Type:** design spec (precedes an implementation plan)
- **Depends on / revises:** the planned accessor-add gate change (see
  `docs/plans/2026-06-10-intake-staff-rename-and-accessor-gating.md`, Workstream B) --
  this design REVISES that gate's rule.
- **Independent of:** the `Clinic Staff` -> `Intake Staff` rename (Workstream A).

---

## 1. Problem

Most appointments are booked by attorneys' **paralegals**, not the attorneys themselves.
The paralegal calls Gesco, gets an invite, registers, and books on behalf of the attorney,
entering the **attorney's** details in the AA/DA section. The attorney is the represented
party, but the paralegal is the actual operator who must receive notifications and be able to
manage the appointment. The paralegal does not have access to the attorney's email account.

The codebase collapses three distinct roles into one "booker" (`appointment.IdentityUserId` =
Creator), assuming that one person is simultaneously the **Actor** (manages), the **Recipient**
(receives email), and the **Principal** (named party). The paralegal scenario splits these:
Actor + Recipient = paralegal; Principal = attorney. Nothing models that split, and the AA/DA
booking flow actively assumes Actor == Principal.

## 2. Decisions (locked 2026-06-10)

| # | Decision |
|---|---|
| D1 | **Recipient model:** the represented attorney is the email **To** (named principal); the paralegal is **CC**. Reduces to today's behavior for self-bookings. |
| D2 | **Association:** per-appointment, ad hoc. No stored paralegal->attorney/firm link. |
| D3 | **Sides:** each side (applicant/defense) may have its own paralegal; **no paralegal ever spans both** sides of one appointment. |
| D4 | **Model:** first-class delegate (Approach 1). Each attorney party optionally carries one paralegal delegate. |
| D5 | **Data shape:** columns on the attorney link entities + denormalized emails on `Appointment` (not a separate entity). |
| D6 | **Identity:** the paralegal is a **full `IdentityUser`** with a new `Paralegal` role -- never given the AA/DA role (no impersonation). |
| D7 | **Opposing-side paralegal:** added by the opposing side themselves (attorney or paralegal) via a gated flow; bootstrapped by the opposing attorney (once registered) or internal staff. |
| D8 | **Delegate-management rule (side-scoped):** internal staff, OR that side's attorney, OR that side's current paralegal. The other side cannot touch it. |
| D9 | **Generic accessor-add rule:** built in Workstream B (role-set-driven, paralegal-ready); this feature appends `Paralegal` to the allowed set -> `internal OR (creator AND (AA OR DA OR Paralegal))`. One line + one test, no rule rewrite. Distinct from D8. |

Research basis: healthcare proxy/shared-access guidance (unique credentials per delegate;
delegates must be identifiable; explicit role classes) -- ONC Health IT Playbook ch.4, PMC
environmental scan of shared access. IAM delegated-access guidance (prefer explicit delegation
over impersonation; audit both the acting party and the authorizing principal; effective access
= intersection) -- Microsoft Entra/Graph delegated access, agentic-AI delegation literature.

## 3. Vocabulary

- **Principal** -- the attorney (also patient / claim examiner where applicable): the named
  party the case is *about*. Email **To**.
- **Delegate** -- the paralegal: their own login, the **actor** who books/manages their side,
  and an email **CC**.
- **Per side** -- applicant or defense. One principal attorney + at most one delegate paralegal
  per side per appointment.

## 4. Identity & role

- New external role **`Paralegal`** (single role; **side-agnostic** -- the side is derived from
  which attorney the paralegal is linked to, not encoded in the role).
- `ExternalUserType` gains **`Paralegal = 5`**; the invite dropdown offers it; registration
  assigns the `Paralegal` role (mirrors the existing AA/DA path at
  `ExternalSignupAppService.cs:573`).
- The paralegal is a complete, independent `IdentityUser` (own credentials). It is **never**
  assigned the AA/DA role, so "all attorneys" role checks, reports, and the booking-form
  own-role prefill never mistake a paralegal for an attorney.

## 5. Data model (D5)

Each attorney link gains an optional paralegal delegate, mirroring the attorney's own
nullable-`IdentityUserId` pattern:

- `AppointmentApplicantAttorney` and `AppointmentDefenseAttorney`:
  add `ParalegalEmail`, `ParalegalFirstName`, `ParalegalLastName`,
  `ParalegalIdentityUserId` (nullable `Guid`).
- `Appointment`: add denormalized `ApplicantParalegalEmail` / `DefenseParalegalEmail`
  (mirrors the existing `ApplicantAttorneyEmail` / `DefenseAttorneyEmail` that
  `AppointmentRecipientResolver` already reads).
- `ParalegalIdentityUserId` is backfilled when an invited paralegal registers, via the same
  `AutoLink...Async` hook the attorney link already uses
  (`ExternalSignupAppService.cs:770-806`).

Migration: additive nullable columns; no data backfill needed (fresh/forward only).

## 6. Booking flow

- **Angular** (`appointment-add.component.ts` + the attorney section component): when the
  booker holds the `Paralegal` role, render a **"Paralegal (you)"** sub-block in the attorney
  section, prefilled from `currentUser`, and keep the **attorney fields fully editable**.
- **Own-role prefill fix:** `applyOwnRoleAttorneyPrefill` (`appointment-add.component.ts:793-822`)
  currently prefills + locks the attorney email to the booker when the booker holds AA/DA. It
  must fire **only for actual AA/DA self-booking**, never for a `Paralegal` booker. (Resolves
  inconsistency #2 -- today a paralegal-as-AA literally cannot type the real attorney's email.)
- **Backend:** the AA/DA upsert (`AppointmentsAppService.cs:1198-1283`) also persists the
  paralegal delegate; for the booking side, `ParalegalIdentityUserId` resolves to the booker.
- **Creator = paralegal** (unchanged ABP auditing) -- the "audit both actors" pattern: Creator
  records the acting delegate; the named party records the principal.
- **Side selection:** the booking paralegal fills exactly one attorney section as "their"
  attorney and is recorded as that side's delegate. The opposing attorney may also be entered
  for the record, **without** a paralegal (the opposing paralegal is added later, D7).

## 7. Access / authorization

- **New 8th pathway in `AppointmentAccessRules`** (read + edit): caller's `IdentityUserId`
  matches a paralegal delegate on a link row -> allowed. Symmetric with the AA/DA pathways.
  `AppointmentReadAccessGuard` hydrates the paralegal `IdentityUserId`s from the link rows (as
  it already does for AA/DA) and passes them to the rule. Grants access to both the booking
  paralegal (also Creator) and the opposing-side paralegal (once linked).
- **Generic accessor-add (built in Workstream B; extended here, D9):** the `CanManageAccessors`
  rule + guard are built in the accessor-gate plan
  (`docs/plans/2026-06-10-intake-staff-rename-and-accessor-gating.md`, Workstream B), written
  role-set-driven (`BookingFlowRoles.ExternalAccessorManagerRoles = {AA, DA}`). This feature
  appends `"Paralegal"` to that set (one line) + one test case, yielding
  `internal OR (creator AND (AA OR DA OR Paralegal))`. The rule/guard are NOT rebuilt here.
- **Delegate-management (new, side-scoped, D8):** a separate predicate
  `CanManageSideDelegate(side)` = internal OR (caller is that side's attorney) OR (caller is
  that side's current paralegal). Governs setting/changing/removing a side's paralegal. The
  opposing side cannot touch it.
- **Change-requests:** the paralegal (as creator and via the new access pathway) can submit
  cancel/reschedule -- covered once the pathway exists; the existing change-request gate
  (`AppointmentReadAccessGuard.CanEditAsync`) is unchanged for everyone else.

## 8. Notifications / recipients (D1)

Principled reframing of the To/CC partition (reduces to today's behavior for self-bookings):

- **To** = the booker's **principal**. If the booker is a paralegal, the To is *promoted* to
  the attorney they represent; otherwise the To stays the booker (a self-booking patient or
  attorney is their own principal -- unchanged).
- **CC** = everyone else: the paralegal(s), the opposing attorney + its paralegal, patient/CE
  as applicable, and the office CC list.
- Applies to **both** delivery shapes: the consolidated one-message flow (`BookerCcDispatcher`,
  status + reminders) and the per-recipient change-request flow (each principal's email CCs
  their own paralegal).
- **Personalization:** the salutation/letterhead names the **attorney** (principal). The
  notification template tokens need a per-recipient review at implementation time (see Open
  items) -- inconsistency #4, not yet enumerated.
- **Anti-ex-parte preserved:** still one consolidated message for status/reminders; only the
  To/CC labeling changes.
- **ASSUMPTION (confirm at review):** only the **booking side's** attorney is promoted to To;
  the **opposing** attorney stays **CC** (a party, as today). The minimal, faithful change is
  "swap booker-To for booking-side-attorney-To"; promoting *both* attorneys to To is an
  alternative if you want both sides addressed as co-primaries. Both keep everyone on the one
  message, so this is an addressing/salutation choice, not a delivery one.
- `RecipientRole` enum likely gains paralegal value(s) (or a delegate flag) so the resolver can
  tag CC delegates -- confirm exact shape at implementation.

## 9. Resolution of every enumerated inconsistency

| # | Inconsistency (current behavior) | Resolution |
|---|---|---|
| 1 | No delegate identity (only Patient/CE/AA/DA) | New `Paralegal` role + `ExternalUserType=5` (S4) |
| 2 | Own-role prefill locks attorney email to booker | Prefill only for AA/DA self-booking; paralegal gets editable attorney fields + own block (S6) |
| 3 | "To" = the paralegal (booker) | To promoted to the represented attorney; paralegal -> CC (S8) |
| 4 | Salutation may name the wrong person | Personalize To/salutation to the attorney principal; template token review at build (S8) |
| 5 | Attorney inbox receives PHI it may not monitor | Accepted per D1 (attorney = primary To; paralegal guaranteed a CC copy) |
| 6 | Two-party consent reaches unmonitored inbox | Consent email CCs the side's paralegal alongside the attorney (delegate feeds the resolver) |
| 7 | Accessor-gate (Change B) would block paralegal | Change B rule revised to include `Paralegal` (S7 / D9) |
| 8 | Change-request submission | Paralegal (creator + new pathway) can submit (S7) |
| 9 | "Requested by" shows the paralegal | Correct (the paralegal did request); displays can read "for [attorney]" via the principal -- minor UI note |
| 10 | One paralegal, many attorneys | Native: per-appointment delegate link, no fixed association (D2) |
| 11 | Auto-link lets the attorney act too | Intended: a registered attorney gets their own access; both delegate and principal can act |
| 12 | Invite has no paralegal type | `ExternalUserType=5` in the invite dropdown (S4) |

## 10. Phasing (implementation, not design scope)

- **Phase 1 -- booking-side paralegal (the core pain):** `Paralegal` role + invite type; data
  model (S5); booking-form paralegal block + prefill fix (S6); access pathway (S7); recipient
  promotion (S8); extend Workstream B's accessor-gate role-set with `Paralegal` (S7 / D9).
- **Phase 2 -- opposing-side paralegal:** the side-scoped delegate-management rule + UI (D8),
  the opposing-attorney-adds-their-paralegal flow (D7), and consent/notification CC for the
  opposing delegate.

## 11. Interaction with the two pending changes (reconciliation decided 2026-06-10: Option 1)

- **Rename `Clinic Staff` -> `Intake Staff`:** independent; no interaction.
- **Accessor-add gate (Workstream B):** ships standalone but **paralegal-ready** -- its
  `CanManageAccessors` rule + guard are role-set-driven
  (`BookingFlowRoles.ExternalAccessorManagerRoles = {AA, DA}`). This feature **extends** that set
  with `"Paralegal"` (one line + one test); it does NOT rebuild the rule or guard. The
  side-scoped delegate-management rule (D8 / S7), the new read/edit access pathway (S7), the
  `Paralegal` role, the data model, and the recipient reframing are all net-new here.
- **Sequence:** A (rename) -> B (accessor gate, paralegal-ready) -> this feature. This feature
  rebases on `main` after B merges. B and this feature touch the same four auth files
  (`AppointmentAccessRules.cs`, `BookingFlowRoles.cs`, `AppointmentReadAccessGuard.cs`, and the
  two Angular components); do not develop them truly in parallel.

## 12. Out of scope (YAGNI)

- Persistent paralegal<->attorney/firm association (D2 chose per-appointment).
- Per-booking toggle of attorney-inbox inclusion.
- Formal attorney->paralegal consent handshake (the relationship is organizational; the
  attorney need not approve each paralegal).
- Generalizing delegates to patient / claim examiner.

## 13. Risks

- **Recipient regression:** changing the To/CC partition risks altering who is addressed on
  existing (non-paralegal) appointments. Mitigation: the rule reduces to "booker = To" whenever
  the booker is a principal, so self-bookings are unaffected; cover with tests on both booker
  types.
- **Access pathway breadth:** a new read/edit pathway widens who can see an appointment. It is
  scoped to a linked paralegal `IdentityUserId` (no email-only match for edit), symmetric with
  AA/DA. Cover with deny-by-default tests.
- **Two add-rules coexisting:** generic accessor-add (D9) vs delegate-management (D8) must stay
  distinct to avoid an external party gaining a path they shouldn't. Cover each with its own
  pure-rule tests.

## 14. Open items to verify at implementation time

- Exact notification template tokens that personalize the salutation (confirm per-template
  whether they currently render booker vs attorney) -- inconsistency #4.
- `RecipientRole` enum change (add paralegal value(s) vs a delegate flag).
- Whether `proxy/` regen is required (new DTO fields for the paralegal block -> likely yes for
  the booking + view DTOs).
- The opposing-attorney "add my paralegal" UI entry point (reuse the add-authorized-user modal
  with a delegate option, vs a dedicated control).

## 15. HIPAA / PHI

- Paralegals are workforce members of the attorney's firm acting in a legal-representative
  capacity; access is least-privilege (their appointments only, via the linked pathway).
- The attorney remains the primary To (D1); the paralegal CC is an intentional, identifiable
  recipient. No new PHI is logged. All test/demo data stays synthetic.
