[Home](../index.md) > [Testing](./) > Mutation Harness

# Guard Mutation Harness

> Purpose: prove that the tests which claim to guard an access rule can actually fail, without a
> person doing the break-test-restore ritual by hand. Audience: developers. Tracks #1005.

## What problem this solves

A test that cannot fail guards nothing, and it still adds to the coverage number. On this repo that
has happened at least four times with the suite green throughout: an authorization harness under
which 243 permission checks were inert, a calendar test that stayed green under the mutation it
was written to catch, two lookup tests that passed only by riding a permissive fall-through, and a
body-part test that asserted a manager was not called while the write went through the repository.

A gate that cannot refuse makes every test above it vacuous, and those tests are green, so nothing
tells you. The only way to find them is to break the guard and watch whether anything notices.

## How to run it

From the repository root. Use a worktree with a clean `git status` for the files named in the manifest.

```bash
python scripts/mutation-harness.py check-manifest      # seconds, runs no tests: is every `find` text still there?
python scripts/mutation-harness.py list
python scripts/mutation-harness.py run                  # every mutant
python scripts/mutation-harness.py run --only creator-pathway-disabled
python scripts/mutation-harness.py run --changed origin/main      # only mutants a change could affect
python scripts/mutation-harness.py run --max-seconds 3000          # stop STARTING mutants after 50 min
python scripts/mutation-harness.py recover              # after a hard kill; see "If a run is killed"
```

Do not set `DOTNET_ENVIRONMENT`; the harness strips it from the child process anyway, because it
fails the whole EF suite (`.claude/rules/dotnet-env.md`).

Do not edit the files named in the manifest, and do not run `dotnet build` in the same checkout,
while a run is in progress: the working copy is deliberately mutated for the length of each mutant.
Use a second worktree.

## What a manifest entry is

`scripts/mutation-manifest.json`. Each mutant names one guard and the tests that must notice.

| field | meaning |
| --- | --- |
| `id` | unique, shown in the report |
| `file` | repo-relative file to mutate |
| `find` / `replace` | exact text; `find` must occur exactly once or the mutant is INAPPLICABLE |
| `runner` | `dotnet` (reads a trx file) or `unittest` |
| `project`, `filter` | the test project and the `--filter` / module that is supposed to guard it |
| `killed_by` | optional substrings; a kill by a test matching none of them is WRONG_KILLER |
| `expect` | `killed` (default) or `survive`; `survive` requires a `reason` naming the gap |
| `watch` | extra paths (usually the test file) that make `--changed` select this mutant |

**A concrete trap:** in `Application.Tests` the `*AppServiceTests` classes are ABSTRACT. The runnable
subclasses are the `EfCore*` ones in `EntityFrameworkCore.Tests`, which need Docker. A filter naming
the abstract class selects zero tests. The harness caught this on its first run (NO_TESTS, not a quiet
pass) and the manifest now names the concrete classes.

## Outcomes, and why none of them can be a silent pass

Only KILLED and SURVIVED are verdicts about the tests. Everything else means the harness could not
tell, and always fails the run (exit 2).

| status | meaning | verdict |
| --- | --- | --- |
| KILLED | a named test failed on the mutant, green on the original | OK when `expect: killed` |
| SURVIVED | the same tests stayed green on the mutant | SURVIVOR (exit 1), OK only when `expect: survive` |
| KILLED_BY_OTHER | something failed, but not a `killed_by` test | WRONG_KILLER (exit 1) |
| NO_TESTS | the filter selected zero tests, on the original or the mutant | INVALID |
| BASELINE_RED | the unmutated tests already fail, so a later failure proves nothing | INVALID |
| BUILD_ERROR | the mutant did not compile or import: a kill by the compiler is not a kill by a test | INVALID |
| NO_RESULT | no result file (Docker teardown, crash) or the time budget ran out | INVALID |
| TIMEOUT | the run hung; the whole process tree is killed | INVALID |
| INAPPLICABLE | `find` text absent or ambiguous: the manifest is stale | INVALID |

Exit codes: 0 all as expected; 1 a survivor, a stale `expect: survive`, or a wrong killer; 2 any
INVALID; 3 a restore failed; 4 an earlier run left a mutation outstanding, or the original is not
committed.

**The result is read from the test results, never from the exit code.** The EF suite has exited 1
with zero failures when Docker teardown threw under load, and a build failure exits non-zero too.
A harness reading `$?` would call an uncompilable mutant "killed".

### The canary

`canary-authorize-removed-appservice-tests-only` is `expect: survive` on purpose. The AppService tests
run under `AddAlwaysAllowAuthorization()`, so deleting an `[Authorize]` there fails none of them.
That is the vacuity this issue is about, kept on the real suite so the harness is shown reporting
SURVIVED against it on every run. The companion mutant,
`authorize-removed-surface-snapshot`, makes the same edit and runs the authorization-surface
snapshot, which does fail: that is the net that actually protects the attribute.

When a harness with real authorization lands (#707 layer 3) the canary starts to be KILLED and the
run reports STALE_EXPECTATION. That is the signal to change it to `expect: killed`.

## Safe restore

The batch-over-the-time-limit failure strands a mutated file on disk, and on this repo that has twice
been an access check in a shared checkout. So:

1. The original is copied OUTSIDE the repo (`%TEMP%/hcs-mutation-harness/<checkout hash>/`) and its
   sha256 recorded in `journal.json` BEFORE the file is touched. Only files whose committed content is
   what is on disk are mutated.
2. Restore runs in `finally`, on SIGINT, SIGTERM and SIGBREAK (which raise, so `finally` runs) and at exit.
3. Restore is verified by sha256 against the saved original, never by `git diff` (a mutation that
   moves a line can leave a diff empty against a formatter). A mismatch is a RestoreError, exit 3.
4. `git checkout -- <file>` is never used: it has reverted real uncommitted work here twice.
5. A time budget only stops the harness STARTING a mutant. A mutant is one atomic
   mutate-test-restore, never interrupted by the budget.

### If a run is killed

A hard kill (SIGKILL, TerminateProcess, power loss, a CI job cancelled by the runner) cannot run any
handler. It leaves the journal entry behind, and the next `run` refuses to start (exit 4) until you run

```bash
python scripts/mutation-harness.py recover
```

which restores from the saved copy and verifies the hash. After ANY interrupted run, run `git status`
in the checkout; a clean tree is the only proof. Then rebuild before running tests: `--no-build`
against a stale mutated assembly gives phantom failures.

## What it costs

Measured 2026-10-03 on a developer laptop, serially, one `dotnet test` per mutant plus one control per
distinct (project, filter). Full manifest, 7 mutants:

| mutant | seconds |
| --- | --- |
| read-guard-never-refuses (EF tests) | 351 |
| canary-read-guard-refusal-guard-class-only (EF tests) | 374 |
| same-parent-never-refuses (Application tests) | 191 |
| canary-same-parent-body-parts-service-tests (EF tests) | 270 |
| creator-pathway-disabled (Application tests) | 97 |
| authorize-removed-surface-snapshot (Application tests) | 75 |
| canary-authorize-removed-appservice-tests-only (EF tests) | 116 |

About 24 minutes in all, 4 killed and 3 survivors that are documented as expected. Controls are
included in each figure that paid for one. Cost is dominated by rebuilding the mutated project and
everything above it, and by the SQL Server container for `EntityFrameworkCore.Tests`: roughly 1.5 to
6 minutes per mutant. Twenty guard mutants is therefore a one-to-two hour scheduled job, not a PR
check. The harness's own tests need no .NET and run in under a minute.

## What the first real run found

Run on 2026-10-03 against `main` at `34475af3`. Both are real, not harness artefacts: each was
re-checked by running the same mutation against a wider set of tests and watching it die.

- `EfCoreAppointmentReadAccessGuardTests` does not pin the refusal in `EnsureCanReadAsync`. Replacing
  its condition with `false && ...` left that class green. The multi-office document and packet tests
  do kill it, so the rule is protected, but not by the class named for it.
- `AppointmentChildOwnershipTests` alone does not pin `EnsureSameParent`; the create-ownership and
  per-service classes do. The body-part service tests still pin only the party check (#1116).
- The first manifest named the abstract `*AppServiceTests` classes and got NO_TESTS from the control
  on three of five mutants. A harness that read exit codes would have reported those as clean.

## Why not Stryker.NET

Measured on this repo (dotnet-stryker 5.0.0, `Domain.Tests`, mutating one file):

- It works mechanically. But the fixed cost with ZERO mutants tested was 3m22s to 3m55s: it creates
  6907 mutants for the whole `Domain` project before the `--mutate` filter ignores 6417 of them, then
  runs the initial test pass and a coverage capture.
- **It does not do the mutation that matters most here.** Deleting an `[Authorize]` attribute, the exact
  defect behind 243 inert permission checks, is not an operator Stryker applies; it mutates statements
  and expressions.
- Its coverage analysis reported `AppointmentAccessRules` as "not covered by any test" in `Domain.Tests`,
  because its tests live in `Application.Tests`. The tests that guard a rule are routinely in a different
  project from the rule, and which project that is only the manifest knows.
- Pointed at `Application.Tests` it would boot ABP's Autofac container and, for the `AppService` tests,
  the Docker-backed EF suite, once per surviving mutant.
- Generic operators on 6907 mutants produce mostly equivalent or irrelevant mutants. The question this
  issue asks is narrower: does the test for THIS guard fail when THIS guard is broken.

So the harness is bespoke and targeted, and it is not a replacement for Stryker. A reasonable later step
is Stryker, scoped by `--mutate` to the same files, run on a schedule as a source of NEW candidate
mutants for the manifest. StrykerJS for the Angular specs is untouched by this and is still open under #1005.

## Adding a mutant

1. Pick a guard that matters: a refusal, a permission check, a comparison that decides access.
2. Find the tests that are claimed to protect it, and the CONCRETE class that runs them.
3. Add the entry. Run `check-manifest`, then `run --only <id>`.
4. If it survives, that is a finding: fix the test (or record `expect: survive` with the reason and an
   issue). Do not weaken the mutation until it is caught.
5. Prefer ONE-line mutations: `if (false && <original>)` rather than deleting a block, so the mutant
   compiles.

## Wiring it into CI (deliberate follow-up, not part of the PR that added it)

Not wired here because `.github/workflows/ci.yml` and `scripts/coverage-gate.py` have open pull requests
against them. Add the job once those merge:

1. **Always, in the existing Python job:** `tests/python/test_mutation_harness.py` already runs under
   `unittest discover`, so the harness's own proof (survivor reported, kill reported, every silent route
   refused, restore verified) is gated with no workflow change. This includes a check that every
   committed manifest `find` text still occurs exactly once in `HEAD`.
2. **A new job `Mutation: Guards`**, `runs-on: ubuntu-latest`, `needs: [backend-test]`, nightly
   (`schedule`) plus `workflow_dispatch`, NOT a required check on pull requests (about 3 minutes per mutant):

   ```yaml
   mutation-guards:
     runs-on: ubuntu-latest
     timeout-minutes: 120
     steps:
       - uses: actions/checkout@<sha>
       - uses: actions/setup-dotnet@<sha>
         with: { global-json-file: global.json }
       - run: python scripts/mutation-harness.py check-manifest
       - run: python scripts/mutation-harness.py run --max-seconds 6000 --report mutation-report.json
       - if: always()
         run: python scripts/mutation-harness.py recover
       - if: always()
         uses: actions/upload-artifact@<sha>
         with: { name: mutation-report, path: mutation-report.json }
   ```

   Pin the action SHAs as the other jobs do, and copy the NuGet/ABP restore steps from `Backend: Test`.
   The `recover` step is belt and braces for a cancelled job (a runner VM is discarded, but a self-hosted
   runner is not).
3. **On a pull request, optionally**, a path-scoped variant:
   `python scripts/mutation-harness.py run --changed origin/main` selects only mutants whose file or
   `watch` paths the PR touched. Make it a required check only after measuring that it stays short.
4. **It should GATE, not only report**: exit 1 or 2 fails the job. A survivor is a finding, and an INVALID
   outcome means the harness could not check a guard, which is the failure mode it exists to prevent.
   Equivalent mutants are not a concern here because every mutant is hand-written to change behaviour;
   if one is later found equivalent, delete it from the manifest with the reason in the commit.
5. Needs Docker for the `EntityFrameworkCore.Tests` mutants; `ubuntu-latest` has it. Do not run two
   harness jobs against one checkout.

## Known limits

- Hand-written mutants test the guards someone thought of. They find a vacuous test for a known guard;
  they will not find an unknown guard. Stryker, scheduled, is the discovery tool.
- The `unittest` runner exists so the harness can test itself; the repo's Python suites are not in the manifest.
- Serial by design: parallel mutants on one working tree would mutate each other's files.
