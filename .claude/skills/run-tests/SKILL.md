---
name: run-tests
description: "Run tests using dotnet test. Parses output, categorizes failures, and suggests fixes. Supports running all tests or filtering by feature."
argument-hint: "<FeatureName> or 'all'"
---

# run-tests

Executes the project's xUnit test suite, parses results, categorizes any failures,
and suggests fixes. Can run all tests or filter by feature name.

---

## Step 1 — DETERMINE SCOPE

Parse `$ARGUMENTS`:

- **Specific feature name** (e.g. `Appointments`, `Doctors`):
  Filter to tests matching that feature name
- **`all`** or **no argument**:
  Run the full test suite
- **`ef`** or **`efcore`**:
  Run only EF Core integration tests
- **`app`** or **`application`**:
  Run only Application layer tests

---

## Step 2 — BUILD COMMAND

Construct the dotnet test command. All commands MUST run from the `P:\` drive path.

| Scope | Command |
|-------|---------|
| All tests | `dotnet test` |
| Feature filter | `dotnet test --filter "FullyQualifiedName~{Feature}"` |
| Application tests | `dotnet test test/HealthcareSupport.CaseEvaluation.Application.Tests` |
| EF Core tests | `dotnet test test/HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests` |
| Domain tests | `dotnet test test/HealthcareSupport.CaseEvaluation.Domain.Tests` |
| Single test method | `dotnet test --filter "FullyQualifiedName~{ClassName}.{MethodName}"` |

Add `--verbosity normal` for readable output.

---

## Step 3 — EXECUTE

Run the test command from the project root:

```bash
cd "P:/Patient Appointment Portal/hcs-case-evaluation-portal"
dotnet test [options] --verbosity normal
```

Capture both stdout and stderr. Set a timeout of 5 minutes (300000ms).

If the command fails to start (not a test failure, but a build/config error):
- Check if LocalDB is running: `sqllocaldb info MSSQLLocalDB`
- Check if the project builds: `dotnet build`
- Report the build error and stop

---

## Step 4 — PARSE RESULTS

Extract from the output:

- **Total tests:** look for `Total tests: N`
- **Passed:** look for `Passed: N` or count `Passed` lines
- **Failed:** look for `Failed: N` or count `Failed` lines
- **Skipped:** look for `Skipped: N`
- **Duration:** look for `Total time:` or `Duration:`

For each failure, extract:
- **Test name:** the fully qualified test method name
- **Error message:** the Shouldly assertion message or exception message
- **Stack trace:** abbreviated to the relevant frame (skip framework internals)

---

## Step 5 — CATEGORIZE FAILURES

For each failing test, classify:

| Category | Pattern | Example |
|----------|---------|---------|
| **Assertion** | `ShouldBe`, `ShouldNotBe`, `ShouldBeNull`, `ShouldContain` | Expected 2, got 3 |
| **Not Found** | `EntityNotFoundException`, `FindAsync returned null` | Seed data missing |
| **Permission** | `AbpAuthorizationException` | Missing test permissions |
| **Configuration** | `Cannot resolve service`, `DI error`, `Module not found` | Missing DI registration |
| **Database** | `SqliteException`, `DbUpdateException`, `migration` | Schema mismatch |
| **Timeout** | `TaskCanceledException`, `TimeoutException` | Slow query or deadlock |
| **Build** | Compilation error before tests run | Syntax error, missing reference |

---

## Step 6 — SUGGEST FIXES

For each failure, provide a specific suggestion:

### Assertion failures
- Read the test method to understand what it expects
- Read the source code being tested
- Determine: is the test wrong (expectations changed) or is the code wrong (regression)?
- Suggest: update the assertion value, or fix the source code

### Not Found / Seed Data
- Check if a `{Entity}DataSeedContributor` exists in `test/.../Domain.Tests/{Feature}/`
- If missing: suggest running `/design-tests {Feature}` to create seed data
- If exists: check that the hardcoded GUIDs in the seed match the test assertions

### Configuration / DI
- Check that the test module registers the required services
- Check that the test class uses the correct base class

### Database / Migration
- Check if a new migration was added but not applied to the test SQLite database
- The test DB is rebuilt from scratch each run — ensure all entity configs are correct

---

## Step 7 — FIX LOOP (if requested)

If the user asks to fix failures:

1. Apply the suggested fix for each failing test
2. Re-run the tests (same scope as original)
3. If still failing: apply a second fix attempt
4. After 2 fix passes with the same test still failing:

   > "Fix loop exhausted after 2 passes. {N} tests still failing:
   > - {test name}: {error}
   > Human intervention needed."

   **STOP.** Do NOT:
   - Remove failing tests
   - Weaken assertions (e.g., changing `ShouldBe(2)` to `ShouldBeGreaterThan(0)`)
   - Skip tests with `[Fact(Skip = "...")]`
   - Comment out test methods

---

## Step 8 — REPORT

```
Test Results ({scope}):
  Command: {exact command run}
  Duration: {time}
  
  Total: {N}  |  Passed: {N}  |  Failed: {N}  |  Skipped: {N}
  
  {If all passed:}
  All tests passed.
  
  {If failures:}
  Failures:
    1. {test name}
       Category: {Assertion / NotFound / Configuration / Database / Timeout}
       Error: {one-line error message}
       Suggestion: {specific fix}
    
    2. {test name}
       ...
  
  {If fix loop was run:}
  Fix attempts: {N} passes
  Fixed: {N} tests
  Still failing: {N} tests
```

---

## Constraints

- **Always run from P: drive** — `dotnet test` from the real path causes SNI.dll failures
- **Never weaken assertions** to make tests pass — fix the code or fix the test data
- **Never skip/remove tests** — if a test is wrong, fix it; if the code is wrong, fix the code
- **Max 2 fix passes** — then escalate to the user
- **5-minute timeout** — if tests hang, report and suggest checking LocalDB status
