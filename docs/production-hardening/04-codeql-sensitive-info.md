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
