# ADR-013: Config-driven reminder cadence (Group L / G-05)

**Status:** Accepted
**Date:** 2026-06-06
**Verified by:** code-inspect + unit tests

## Context

The OLD app's appointment-reminder schedule (how many reminders, on which
T-minus / elapsed days) is unrecoverable from source: OLD's `SchedulerDomain`
was a stateless dispatcher behind `POST /api/Scheduler/postscheduler`, fired by
an external SQL Agent / AWS task whose timing lived in host infrastructure, and
the `spm.*` selection procs are empty stubs. So strict parity cannot port a
specific cadence.

On the NEW stack, six date-driven Hangfire reminder jobs already existed but
each **hardcoded** its day windows (`static readonly int[]`) and cron string. A
parallel config schema (`CaseEvaluation.Notifications.Reminders.*` ABP settings:
day-anchor lists, per-reminder crons, a master enabled flag, timezone) had been
defined but was **never read by any job** -- the values were duplicated as
hardcoded arrays. The goal: make the cadence modular and editable in one place
without re-deriving an OLD schedule that does not exist.

## Decision

Make the six date-driven reminder jobs source their cadence from the existing
`RemindersPolicy` settings via a new pure `ReminderCadence` value object (CSV
anchors -> `ShouldFire(dayCount)` membership), mirroring the `JointDeclarationCutoff`
helper convention. Specifically:

- The five exact-day-match jobs (Sec 31.5 request-scheduling, Sec 34(e)
  cancel/reschedule, appointment-day, due-date-approaching,
  due-date-document-incomplete) read their day anchors from settings and fire
  via `ReminderCadence`. Defaults equal the previously hardcoded values, so the
  cadence does not change until an admin edits a setting.
- `PackageDocumentReminderJob` keeps its at-or-past cutoff model (semantically
  different from exact-day anchors); it only gains the enabled gate.
- All six honor a master `RemindersEnabled` flag, **defaulted to false**: the
  jobs stay registered but enqueue nothing until an admin enables reminders
  per tenant in `/setting-management`.
- Cron wake-times stay as host-level class constants (a Hangfire recurring job
  has one schedule; cron cannot vary per tenant). The `*Cron` settings remain
  reserved/unused.
- G-05-02: a Joint Declaration row rides the shared package-document cadence but
  renders a distinct `JointDeclarationUploadReminder` template (Option B) so it
  is recognizable, with no second cron and no duplicate-send risk.
- G-05-01 (owner-targeted pending-document reminder) is deferred: OLD's send was
  dead code that never fired, so there is no live behavior to restore.

## Consequences

- Reminder cadence is editable per tenant in one place (ABP settings), not in
  code; the day-anchor values are the substantive knobs.
- Shipping `RemindersEnabled=false` mutes all reminders after this change -- an
  intentional behavior change pending the email gate being lifted. Re-enabling
  is a single host-setting flip, not a deploy.
- Changing a cron requires an app restart to re-register the Hangfire job
  (acceptable: cron is an operational, host-level concern).
- `ReminderCadence` is unit-tested in isolation; each job has gate + anchor-day
  tests, so the cadence logic is verified without seeding the Hangfire runtime.

## Alternatives Considered

- **Recover and port OLD's cadence** -- impossible: not in source (external
  scheduler, stub procs).
- **Migrate `PackageDocumentReminderJob` to the anchor model** -- rejected: its
  at-or-past window has different semantics; converting it would change behavior,
  violating the zero-cadence-change default.
- **Make cron config-driven (read settings at registration)** -- rejected for
  this slice: a recurring job has a single schedule (cron is inherently
  host-global), and resolving settings at host startup adds risk for little
  value. The `*Cron` settings stay reserved for a future host-level wiring.
- **Build the G-05-01 owner nudge now** -- deferred: OLD never delivered it
  (dead code), so it is a fresh feature, not a parity restoration.
- **G-05-02 own-cadence split (Option A)** -- rejected: OLD's JDF cadence is
  unknown, and a second cron risks duplicate sends; Option B (distinct template,
  shared cadence) is the parity-honoring minimum.
