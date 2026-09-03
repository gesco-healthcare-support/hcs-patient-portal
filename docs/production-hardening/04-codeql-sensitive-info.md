# Phase 4 -- CodeQL sensitive-information alerts

**Change class:** mixed. Triage each site first; most will be deliberate behaviour change
(test WITH the fix), a few may be false positives (triage log, no code change).

**19 real alerts in 6 files.** These are the highest-value findings in the whole epic per unit of
work, because they are the only ones CodeQL raises about _this system's own handling of sensitive
data_, on a system whose entire content is PHI.

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

```bash
gh api repos/gesco-healthcare-support/hcs-patient-portal/code-scanning/alerts --paginate \
  -q '.[] | select(.state=="open") | .rule.id' | grep -c '^cs/'
```

Baseline: 19.
