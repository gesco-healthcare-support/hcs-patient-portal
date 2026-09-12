# System design + infrastructure research (2026-08-28)

Exercise 2 of three in the production-readiness programme. This folder holds the **inputs and the
delivered outputs**, so both the question and the answer survive independently of anyone's memory.

**Read this before trusting anything below.** The research session had web access and **no
repository access**, so every statement it makes about what this code does is inherited from two
baseline documents rather than read from source. The repository verification is committed
separately at
[`../../production-hardening/10-research-corrections.md`](../../production-hardening/10-research-corrections.md);
**four of its claims do not survive contact with source**, including the one its cost model is most
sensitive to. Read the corrections alongside the deliverables, not after them.

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

## The delivered output

Archived 2026-09-01, verbatim from the research session's own bundle. Extracted from the delivered
tarball rather than from the loose copies, and the two are byte-identical where both exist.
Scale: 34 agents, 4 phases, 5.7M subagent tokens; **526 claims adversarially re-verified -- 347
clean, 161 corrected, 12 wrong, 6 unreachable.**

| File                                               | What it is                                                                                                                                                                                                                                               |
| -------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `system-design-target.md`                          | **Document A.** Three lead decisions; the portfolio problem and a 40-hour list; 14 corrections to the baselines; 14 areas with options, recommendation and operational cost; the target architecture; 8 rejected patterns; 16 adjudicated contradictions |
| `infrastructure-requirements.md`                   | **Document B.** 62 capability requirements in 5 groups, each MUST/SHOULD with reason, traceability and a test; 10 application-owned requirements; **17 anti-requirements**; scoring guidance; 9 named gaps                                               |
| `handoff/research-method-and-analysis.md`          | **Read this second.** Separates the author's judgement calls from the agents' sourced findings -- the part that should be argued with                                                                                                                    |
| `handoff/appendix-A-evidence-register.md`          | 646 sourced claims with URL, access date, agent confidence and the adversarial verifier's verdict on each                                                                                                                                                |
| `handoff/appendix-B-verification-and-critique.md`  | The integrity record: every material error the adversarial pass caught, per area                                                                                                                                                                         |
| `handoff/raw-research.json`                        | Unprocessed output from all 34 agents, for anything the appendices flattened                                                                                                                                                                             |
| `handoff/HANDOFF.md`, `handoff/HANDOFF-PROMPT.txt` | The delivery package and its receiving prompt                                                                                                                                                                                                            |

**The `handoff/` subdirectory is deliberate**, unlike exercise 1's flat layout: Document B section
9.0 cites `handoff/appendix-A-evidence-register.md` by that path, and flattening would break every
such reference. Structure preserved over convention.

**Where to start:** Document A section 1, then section 2 (the portfolio problem), then
`research-method-and-analysis.md` section 3. About twenty minutes, and it carries the decision.

## Status

**Delivered and verified 2026-08-31.** Triage and routing into the hardening epic is recorded in
[`../../production-hardening/09-system-design-intake.md`](../../production-hardening/09-system-design-intake.md).

**Two things this exercise did not settle, both flagged by the research itself:**

- **California and state law are entirely absent.** It argues federal retention clocks for a product
  handling California workers' compensation and California medical records. The research names this
  as the single item most worth an hour with counsel. Not an engineering task.
- **The capacity finding is a business decision.** 734-1,084 hours of setup and 24.5-43.5 a month
  against roughly 40. **Do not take those figures anywhere until the audit ratio is re-derived** --
  the corrections file section 5 explains why the number is suspect in the direction that inflates
  it.

## Related

- [Code standard research](../code-standard-2026-08-28/) -- exercise 1, complete, with its
  remediation plan
- [System architecture baseline](../../architecture/SYSTEM-ARCHITECTURE-BASELINE.md) -- attachment 01
- [Runtime and data profile](../../devops/RUNTIME-AND-DATA-PROFILE.md) -- attachment 02
- [CI tests and checks](../../devops/CI-TESTS-AND-CHECKS.md) -- what the pipeline enforces today
