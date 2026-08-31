# System design + infrastructure research (2026-08-28)

Exercise 2 of three in the production-readiness programme. This folder holds the **inputs** to
an external research exercise, so the reasoning survives whether or not the exercise is ever run.

## Why this exercise exists

The portal has run on one internal VM behind the corporate network since launch. Making it
publicly reachable changes the threat model, the availability requirements and the operational
burden all at once. This exercise asks an independent researcher to design the target
architecture from the evidence, without being told what to conclude.

## What was sent, and what is here

| Sent as                            | Kept here as                       | Why                                                                                                                             |
| ---------------------------------- | ---------------------------------- | ------------------------------------------------------------------------------------------------------------------------------- |
| `00-RESEARCH-BRIEF.md`             | `00-RESEARCH-BRIEF.md`             | The question, the constraints, the output contract                                                                              |
| `01-system-architecture.md`        | **not duplicated**                 | Verbatim copy of [`../../architecture/SYSTEM-ARCHITECTURE-BASELINE.md`](../../architecture/SYSTEM-ARCHITECTURE-BASELINE.md)     |
| `02-runtime-and-data-profile.md`   | **not duplicated**                 | Copy of [`../../devops/RUNTIME-AND-DATA-PROFILE.md`](../../devops/RUNTIME-AND-DATA-PROFILE.md), with the office name anonymised |
| `03-deployment-and-constraints.md` | `03-deployment-and-constraints.md` | New content -- the deployment reality and the gaps a public host must close                                                     |
| `system-design-KICKOFF-PROMPT.md`  | `kickoff-prompt.md`                | The instructions that accompany the four attachments                                                                            |

Attachments 01 and 02 are deliberately **not** copied into this folder. They are the two
committed baselines, and keeping a second copy here would let the two drift apart. Rebuild the
send-ready bundle by copying those two files alongside the three in this folder.

**One difference is intentional.** The sent copy of the runtime profile replaces the real office
name with `<office-a>`. That is a client practice name and it does not belong in a document
leaving the organisation. Preserve that substitution on any future send; do not sync the
anonymised copy back over the committed baseline.

## The constraint that shapes the output

The brief **forbids naming a cloud vendor and forbids pricing anything.** It asks for
vendor-neutral MUST/SHOULD capability requirements plus an explicit anti-requirements section.

This is deliberate. A costed platform comparison already exists from 2026-08-26 and is being
withheld from this exercise on purpose, so that the architecture generates the platform
requirements rather than a chosen platform back-filling an architecture to justify it. Exercise 3
scores the platform options against the requirements this exercise produces.

If you run this exercise later, keep the withholding. Handing over the costed comparison is the
one change that would invalidate the result.

## Revisions after the brief was written

Three corrections were applied on 2026-08-31, before the bundle was considered sendable:

1. **Section 4.0 added** to `03-deployment-and-constraints.md` -- the Hangfire dashboard
   authorises every caller and is mounted in the production path. It is load-bearing for the edge
   design, because it means not every application route is either public-by-design or
   authenticated.
2. **The rate-limiting claim corrected.** An earlier draft said login and registration are both
   unthrottled. Registration is throttled on the API path; sign-in is not, and the gap is
   structural rather than an oversight -- sign-in is served by the AuthServer, a separate process
   that registers no rate limiter, whose Razor pages call application services in-process and so
   never traverse API middleware.
3. **The capacity figure qualified.** "Roughly one developer-week per month" is an estimate that
   has never been confirmed against a staffing commitment. The brief now says so and asks for
   thresholds rather than assumptions built on it.

## Status

**Not yet run.** The bundle is complete and sendable. Nothing downstream depends on it yet.

## Related

- [Code standard research](../code-standard-2026-08-28/) -- exercise 1, complete, with its
  remediation plan
- [System architecture baseline](../../architecture/SYSTEM-ARCHITECTURE-BASELINE.md) -- attachment 01
- [Runtime and data profile](../../devops/RUNTIME-AND-DATA-PROFILE.md) -- attachment 02
- [CI tests and checks](../../devops/CI-TESTS-AND-CHECKS.md) -- what the pipeline enforces today
