# Phase 4 -- CodeQL sensitive-information alerts

**Change class:** mixed, and **triage inverted the original expectation.** Triage each site first.

**19 alerts in 6 files. ONE is real.**

This block read _"19 real alerts in 6 files"_, and the change class read _"most will be deliberate
behaviour change (test WITH the fix), a few may be false positives"_, until 2026-09-08. Triage at
source that day found **1 real defect and 18 that are false positives or safe-intentional** -- close
to the exact inverse. Corrected rather than silently rewritten, because this file is TRACKED while
the correction lived only in the issue bodies: anyone planning from the repo alone would have scoped
nineteen fixes and repeated phase 1's mistake at scale.

| Issue | Alerts | Real | Disposition |
| ----- | ------ | ---- | ----------- |
| #578 | 5 | **1** | Alert 211 writes a full email address into a Warning-level log. The 3 HIGH alerts match the word "Password" inside template IDENTIFIER constants. |
| #579 | 13 | 0 | Seed passwords behind a fail-closed `IsDevelopment()` gate, verified present and called in all four contributors. |
| #580 | 1 | 0 | `CopiedGroupCounts.Total` is an `int`; field-insensitive taint through a record of ten ints. |

**Severity is not a triage signal here: all three HIGH alerts are name matches.** Acting on them
would be phase 1's mistake repeated -- there, doing what the scanner asked would have broken PDF
generation.

**The scanner also UNDER-reports.** `InternalUsersDataSeedContributor.cs:35` declares the same
`DefaultPassword` and was never flagged, so "13 alerts" is the scanner's view rather than the size of
the surface. It changes no verdict, since all four contributors are gated. Same shape as the
`docker:S6471` under-report in the phase 2 catalogue.

These remain the highest-value findings in the epic per unit of work, because they are the only ones
CodeQL raises about _this system's own handling of sensitive data_, on a system whose entire content
is PHI. The 1-in-19 ratio is the finding, not a reason to have skipped the phase.

Reminder from [00-triage-log.md](00-triage-log.md): the other 109 "CodeQL alerts" are OpenSSF
Scorecard findings and belong to phase 2. Do not conflate them.

---

## COMPLETE as of 2026-09-09 -- 0 open sensitive-information alerts

Final state of the whole population, measured rather than asserted:

```text
 7  dismissed / false positive
14  dismissed / won't fix
18  fixed (closed by code before this phase)
 0  OPEN
```

Phase 4 itself fixed **1 real defect** and dismissed **18** with per-group rationale.

| Lane | Alerts | Outcome |
| ---- | ------ | ------- |
| 4.1 emailer and dispatcher | 5 | **1 REAL**, fixed in #752; 4 dismissed |
| 4.2 seed contributors | 13 | all safe-intentional, dismissed Won't fix |
| 4.3 approval path | 1 | false positive, dismissed |

### The one real defect

`CaseEvaluationAccountEmailer.cs:183` built
`contextTag: $"AccountEmailer/ConfirmationCode/{emailAddress}"`, and `:205` wrote that tag to a
**Warning-level log** on the template-missing branch, past the empty-recipient guard. A full patient
email address reached the logs.

That tag has **three use sites across two sinks** -- `:196` and `:205` both log it, and `:218` puts it
into the background-job args -- so the fix went at the construction site, which cleans all three.

Fixed in **#752** by `EmailAddressVisibility.Mask`, mirroring the existing `SsnVisibility` precedent:
`internal static`, pure, its own unit-test file, splitting on the LAST `@` because a quoted local part
may legally contain one, and revealing no domain at all when the input is not an address.

**Masking was chosen over dropping the discriminator** (2026-09-09) to keep per-invocation correlation
on that path. That choice has a consequence nobody predicted.

### THE FIX DID NOT CLOSE THE ALERT -- the phase's most transferable finding

CodeQL re-analysed `main` with the fix in place (`afe83015`, 2026-09-09 22:35) and **alert 211 still
fired**, reporting seven flow paths.

**CodeQL cannot see that `Mask()` sanitizes.** Its taint tracking follows
`emailAddress -> Mask() -> contextTag -> LogWarning`, and any function that takes tainted input and
returns a derived string propagates taint unless the query is told otherwise.

So the risk is genuinely fixed and the query is genuinely still right about the path. Dismissed as
**Won't fix** rather than False positive, because the query is not wrong -- what closes it is our
judgement that a masked address is not an identifier.

**This file's own done-bar predicted exactly this:** _"A fix that satisfies a reviewer but not the
query has not closed the alert."_ It is the reason a phase must not be written complete off a green
workflow. Every surface signal said done -- three PRs merged, the cascade landed, the workflow green,
eighteen alerts dismissed -- and the one honest measurement said otherwise.

A permanent sanitizer model is tracked in **#772**, deliberately deferred. Dismissal is proportionate
for one alert and does not scale, so the trigger recorded there is **a second masked flow**.

### THE DONE-BAR HAD AN UNSTATED DEPENDENCY ON A WEEKLY JOB

The validation loop below says to re-run CodeQL and watch the `cs/` count drop. **It does not say that
on `main` that only happens on Mondays.**

```text
codeql-pr.yml    on: pull_request only
security.yml     on: schedule "0 6 * * 1"  plus workflow_dispatch
```

A mid-week merge therefore leaves the count unchanged for up to six days, and anyone following the
loop literally would watch a static number and reasonably conclude the fix had failed. **Dispatch
`security.yml` manually to close the loop the same day.**

### THE SCANNER UNDER-REPORTS

`InternalUsersDataSeedContributor.cs:35` declares the same `DefaultPassword` as the three flagged
contributors and carries the same fail-closed gate, and **CodeQL never flagged it**. So "13 alerts"
was the scanner's view, not the size of the surface. Same shape as the `docker:S6471` under-report
already in the phase 2 catalogue. It changes no verdict -- all four are gated -- but it is the reason
never to read an alert count as a population count.

### FOUND ALONGSIDE, AND FIXED

Reading the code next to the flagged item produced two genuine WCAG AA failures nobody had measured:

- **#744**, the banner action button. Three of six variants failing, and the hover state at 2.70:1,
  worse than the resting state it was raised for. Fixed in #755.
- **#756**, the banner callout, found while fixing #744. Two of six failing, plus its icon container
  failing unmeasured at 2.55:1. Fixed in #761.

That is phase 1's lesson for the fifth time in this epic: **the real defects were next to the flagged
one**, found by reading its neighbours rather than by any check reporting them.

### Delivered

```text
#752  d9a64b20  mask the address in the emailer context tag
#755  dea8138f  tint the banner glass button for WCAG AA
#761  2e0f3e11  darken the banner callout for WCAG AA
#768            cascade epic -> main
```

---

## The alerts, grouped by file

| Alerts | File                                                                                           | Rule                                       |
| ------ | ---------------------------------------------------------------------------------------------- | ------------------------------------------ |
| 5      | `src/...Domain/Identity/DemoExternalUsersDataSeedContributor.cs`                               | exposure-of-sensitive-information          |
| 5      | `src/...Domain/Identity/ExternalUsersDataSeedContributor.cs`                                   | exposure-of-sensitive-information          |
| 3      | `src/...Domain/Identity/DemoPatientDataSeedContributor.cs`                                     | exposure-of-sensitive-information          |
| 2      | `src/...Application/Emailing/CaseEvaluationAccountEmailer.cs`                                  | exposure-of-sensitive-information          |
| 2      | `src/...Application/Emailing/CaseEvaluationAccountEmailer.cs`                                  | cleartext-storage-of-sensitive-information |
| 1      | `src/...Application/Notifications/NotificationDispatcher.cs`                                   | cleartext-storage-of-sensitive-information |
| 1      | `src/...Application/AppointmentChangeRequests/AppointmentChangeRequestsAppService.Approval.cs` | exposure-of-sensitive-information          |

Two clusters, and they deserve different treatment.

---

## 4.1 The emailer and dispatcher (5 alerts) -- treat as real until proven otherwise

`CaseEvaluationAccountEmailer.cs` carries 4 of the 19, split across both rules. An account emailer
that CodeQL flags for _both_ exposure and cleartext storage of sensitive information is very likely
putting a credential, token, or reset link somewhere it persists -- a log line, an outbox row, an
audit record.

**Why this is the priority within the phase.** Notification emails are already known to reach
external firms: during Pacific-epic verification a single Regenerate click mailed PHI-bearing
packet PDFs to two external domains. The delivery path is real and wide. Anything sensitive that
this code logs or stores is therefore not a theoretical exposure.

**Research owed:** for each of the 5, identify the sink (log sink, database column, outbox payload)
and whether the value is a credential, a token, or patient data. `NotificationDispatcher.cs` and the
outbox table are the likely persistence points.

**Expected fix shape:** redact at the boundary. `~/.claude/rules/hipaa.md` already requires this --
no `user.ssn`, no full patient names in logs. The fix is structured redaction where the value is
constructed, not a filter bolted on at the sink.

**Acceptance (EARS):** WHEN the account emailer or notification dispatcher records an event, THE
SYSTEM SHALL NOT persist credentials, tokens, or patient identifiers in cleartext to any log,
outbox payload, or audit row.

---

## 4.2 The seed contributors (13 alerts) -- verify whether they run in production

13 of 19 sit in data-seed contributors, two of which are named `Demo*`. The instinct is to dismiss
them as dev-only fixtures. **Do not dismiss them on the name.**

**The thing to check first:** DbMigrator runs in production. It is part of the compose dependency
graph and it is what creates and seeds each office database. If these contributors execute during a
production migration -- or if a `Demo*` contributor is not actually gated by environment -- then
seeded credentials are being written to a production database and possibly logged.

**Research owed, in this order:**

1. Does each contributor run unconditionally, or is it gated (environment check, configuration
   flag, `IsDevelopment`)? Read the gate; do not infer it from the class name.
2. What exactly is flagged -- a seeded password literal, a logged credential, or a returned value?
3. For any that genuinely run only in development: triage-log entry with the gate quoted as
   evidence, and no code change.

**If they do run in production**, this is more serious than the emailer cluster, because seeded
accounts with known credentials in a live system is a direct authentication bypass. Escalate to the
front of the phase.

**Related known fact:** the persisted `main_sqldata` volume does not carry default passwords -- a
local password had to be reset by hand on 2026-08-28. That hints seeding behaves differently across
environments, which is exactly the ambiguity to resolve here.

---

## 4.3 The approval path (1 alert)

`AppointmentChangeRequestsAppService.Approval.cs` -- one exposure alert on the reschedule approval
path, which handles consent timestamps and party snapshots. Lowest volume, but it sits on a flow
with legal weight. Triage with the others.

---

## Validation loop

Backend only unless a fix reaches a template:

```bash
dotnet format --verify-no-changes
dotnet build -warnaserror
dotnet test
```

Then confirm the alerts actually close: re-run CodeQL on the PR and check the alert count for `cs/`
rules drops as expected. A fix that satisfies a reviewer but not the query has not closed the alert.

**State the arithmetic before running it, because the obvious misreading is built in.** Baseline is
19. After the alert-211 fix ALONE, expect **18, not 0** -- the other 18 close by DISMISSAL, which
moves them out of `state=="open"` without any code change. "Still 18" is success here; "0" before the
dismissals have happened would mean something unexpected occurred.

**Do not branch on the exit code of the pipeline below.** `grep -c` exits 1 on zero matches, which is
indistinguishable from `gh api` having failed -- a failed enumeration and a genuine zero produce the
same status. `pipefail` does not rescue it, because the rightmost non-zero status wins and grep's
legitimate no-match 1 is the rightmost. Read the printed count, having separately confirmed that the
`gh api` call itself succeeded.

```bash
gh api repos/gesco-healthcare-support/hcs-patient-portal/code-scanning/alerts --paginate \
  -q '.[] | select(.state=="open") | .rule.id' | grep -c '^cs/'
```

Baseline: 19.
