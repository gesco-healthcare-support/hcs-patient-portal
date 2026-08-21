# Go-live fixes (F1-F4 + type reduction) -- status + verification (2026-06-07)

Base: local `main` after the parity merge (`a1ecdde`). All four fixes committed directly to
`main` per the agreed plan (`docs/plans/2026-06-07-appointment-go-live-fixes.md`).

## Commits (on local main, not yet pushed)

| Fix | Commit | What |
|-----|--------|------|
| F4  | `29b6f9d` | reschedule approval generates a FRESH confirmation number (was reusing the source's -> unique-index 500), wrapped in the shared ConfirmationNumberRetryPolicy |
| F3  | `4a3784f` | AttyCE packet prune uses set-based `DeleteDirectAsync` (idempotent) so concurrent per-recipient prunes no longer throw AbpDbConcurrencyException -> no Hangfire retry -> no duplicate emails |
| Types | `b600a8f` | run-once EF migration reclassifies the 27 legacy-type appointments (QME->PQME; Record Review/Deposition/Supplemental->IME) then deletes the 4 legacy types; no-op on a fresh DB |
| F1/F2 | `3db294b` | all bookings create Pending; internal bookings are auto-approved by the Angular client AFTER the party/injury attach sequence, so the approval gates run on the fully-populated appointment and the attaches no longer race the approval side-effects |

The email-link logging diagnostic (`SendAppointmentEmailJob` + `docker-compose.override.yml`)
remains uncommitted/local.

## Verification status

PASS (verified in the rebuilt live stack):
- **Build/boot gate:** full `docker compose up -d --build` from the committed code succeeded
  (exit 0) -- backend + Angular both compiled clean (incl. the new migration, the
  AppointmentsAppService change, and the Angular auto-approve change which also passed
  eslint --max-warnings=0 + prettier on commit). All containers came up healthy; the SPA
  rendered the booking form.
- **Migration integrity:** `db-migrator` exited 0 applying the new migration in-place on the
  existing Falkinstein volume (no wipe).
- **Type reduction (T3) -- FULLY VERIFIED:** DB now holds exactly 3 appointment types
  (AME `...0003`, IME `...0007`, Panel QME `...0002`); `type_count = 3`; `0` appointments
  reference any legacy type (the 27 were reassigned); the booking-form Type dropdown shows
  only AME / IME / Panel QME.

BLOCKED (could not complete -- environment, not code):
- **F1/F2, F3, F4 end-to-end UI verification** was interrupted by repeated Docker Desktop
  engine crashes (the `docker-desktop` WSL2 VM died 3 times -- once during the rebuild, twice
  shortly after the stack started serving requests). The engine recovered each time via a
  Docker Desktop restart but destabilized again within ~2 minutes, before an internal booking
  could be driven through to approval. The code is committed and compile/boot-verified; only
  the live walkthrough of the booking/approval/reschedule flows is outstanding.

## To finish verification once Docker is stable

1. Ensure Docker Desktop is healthy (`docker info`); if it keeps crashing, a host reboot or a
   `.wslconfig` memory bump may be needed (the concurrent backend+Angular build is heavy).
2. `docker compose up -d` (images already built); wait for `api` healthy.
3. F1/F2: book an AME as an internal user (e.g. `stafsuper1`) with Patient + AA + DA + CE +
   1 injury. Expect: appointment ends **Approved**, AA/DA/CE join rows = 1/1/1, injuries >= 1,
   and NO 409 in the browser console or api logs.
4. F3: on that approval, confirm each AttyCE packet email is sent exactly once (api logs: no
   AbpDbConcurrencyException, no job retry, no duplicate "delivered" lines).
5. F4: from the appointments list, Reschedule that appointment and approve it. Expect: a new
   row with a NEW `A#####` confirmation number and NO 500.
6. Also confirm an EXTERNAL booking (e.g. patient self-book) still lands Pending with all
   join rows (the path that already worked).
