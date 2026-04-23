[Home](../INDEX.md) > [Product Intent](./) > Applicant Attorneys

# Applicant Attorneys -- Intended Behavior

**Status:** draft -- Phase 2 T6, interview in progress
**Last updated:** 2026-04-24
**Primary stakeholder:** [UNKNOWN -- pending T6 interview]

> Captures INTENDED behaviour for `ApplicantAttorney` records -- firm and contact information for attorneys representing the injured worker on a workers'-comp claim. Cross-reference the sibling files [appointment-applicant-attorneys.md](appointment-applicant-attorneys.md) (the join entity linking attorneys to appointments) and [appointment-accessors.md](appointment-accessors.md) (per-appointment access grants). Every claim source-tagged; code is never cited as intent authority.

## Purpose

This document captures intent for the attorney record in the portal -- the saved firm and contact information for attorneys (applicant OR defense) who use the portal to book and manage IMEs on behalf of their clients. At MVP, both applicant and defense attorneys have a **symmetric portal experience**: they log in, see their saved firm info pre-filled, and book appointments without re-typing firm details each time. [Source: Adrian-confirmed 2026-04-24]

**Architecture note (developer-side):** the current code only has an `ApplicantAttorney` entity (no `DefenseAttorney`). Given the symmetric intent, MVP can either add a mirror `DefenseAttorney` entity or unify into a single `Attorney` entity with a side/type flag. Both satisfy the business intent Adrian described; the pick is a code-quality / migration-cost decision the developer makes during implementation, not a manager-facing question. This file captures attorney-record intent generically -- wherever "ApplicantAttorney" appears below, the same logic applies to defense attorneys under whichever architecture lands.

## Personas and goals

See [appointments.md](appointments.md) for booker-persona definitions. Applicant-attorney-specific goals captured here.

[UNKNOWN -- queued for interview: feature-specific goals for the attorney as user vs. as record.]

## Intended workflow

### Attorney record creation

[UNKNOWN -- queued for interview: is an `ApplicantAttorney` record created (a) when an attorney registers via invite, (b) when a booker types the attorney's firm info into the booking form, (c) when the host admin manually creates one, or some combination?]

### Linking an attorney to an appointment

See [appointment-applicant-attorneys.md](appointment-applicant-attorneys.md). This file captures only the attorney-record-level intent; the appointment-linkage intent lives in the join-entity doc.

### Editing attorney records

[UNKNOWN -- queued for interview: who can edit an attorney's firm info (the attorney themselves, the doctor's admin, Gesco-side admin)?]

## Business rules and invariants

### Confirmed

- **Attorney has a saved firm profile reused across bookings.** An attorney (applicant or defense) does not re-enter firm name, address, contact info each time they book. Their profile pre-fills on every new booking; they type only case-specific info. [Source: Adrian-confirmed 2026-04-24]
- **Symmetric treatment of applicant and defense attorneys.** Both have saved profiles; both log in and book the same way. The current code's applicant-only entity is an intent/code gap, not a deliberate asymmetry. [Source: Adrian-confirmed 2026-04-24]
- **No ad-hoc access grants; staff use the attorney's registered email.** A paralegal or assistant helping an attorney logs in under the attorney's account rather than getting a separate sub-user. Keeps portal access strictly tied to formal legal-party membership. [Source: Adrian-confirmed 2026-04-24; see `appointment-accessors.md` for the broader access model]

### Open

- [UNKNOWN -- queued for Adrian / manager: exactly which firm fields are required on an attorney profile at MVP? Current code has firm name, firm address, phone, fax, web, street, city, zip, state -- all optional. Manager-level call on what's actually needed for the legal record.]
- [UNKNOWN -- queued for Adrian / manager: do attorneys at the same law firm share a single firm record, or does each attorney carry their own (duplicated) firm fields? Business question driving whether "firm" is a separate entity or denormalised onto each attorney record.]

## Integration points

- **AppointmentApplicantAttorneys** (join entity) -- each join record references exactly one ApplicantAttorney record.
- **Appointments** -- indirectly, via the join entity.
- **Notifications** -- attorney email (on the IdentityUser) is a recipient on every all-parties notification per `appointments.md` ex-parte rule.

## Edge cases and error behaviors

[UNKNOWN -- to resolve during interview.]

## Success criteria

First-pass sketch:

- A returning attorney (applicant or defense) sees their firm profile pre-filled on every new booking without re-typing.
- An attorney's staff (paralegals, assistants) can use the portal as the attorney by logging in with the attorney's registered credentials; they do not need separate access.
- A defense attorney's experience mirrors an applicant attorney's: saved firm profile, the same booking flow, appointments list scoped to cases they're a party on.
- The cross-tenant rule from `appointments.md` holds here: an attorney working with multiple tenants (practices) sees a per-tenant appointments list in each, not a unified cross-tenant inbox.

## Known discrepancies with implementation

- `[observed, not authoritative]` `IdentityUserId` is required on every ApplicantAttorney record, but all firm/contact string fields are optional. Intent on which fields must be captured at creation is [UNKNOWN].
- `[observed, not authoritative]` The `ApplicantAttorney` entity collapses firm-level and attorney-level data into a single record (firm name, firm address, and the attorney's user account in one row). If a firm has several attorneys, each attorney has a separate record with repeated firm fields. Intent on whether firms should be a separate entity is [UNKNOWN].
- `[observed, not authoritative]` No tests exist for ApplicantAttorneys (per `FEAT-07`).
- `[observed, not authoritative]` Defense attorneys have NO equivalent domain entity today; they only exist as IdentityUser + "Defense Attorney" role. **Intent divergence (Adrian-confirmed 2026-04-24):** MVP intent is that defense attorneys have a saved firm profile that pre-fills on every booking, symmetric to applicant attorneys. The current code's asymmetry is NOT the intent. The fix path (parallel `DefenseAttorney` entity vs. unified `Attorney` entity with a side flag) is a developer-side architecture decision; both match the business intent.

## Outstanding questions

[UNKNOWN entries roll up to OUTSTANDING-QUESTIONS.md.]

<!-- DRAFT:MANUAL:START -->
<!-- DRAFT:MANUAL:END -->
