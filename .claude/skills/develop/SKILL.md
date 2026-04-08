---
name: develop
description: "Full feature development lifecycle: plan -> implement -> test -> document -> commit -> PR. Guides through the entire process with human gates at critical points."
argument-hint: "<task description>"
---

# develop

End-to-end development workflow with human approval gates at critical decision points.
Chains plan-feature, implementation, testing, documentation, and shipping phases.

---

## Gate 1 — PLANNING

1. Run `/plan-feature $ARGUMENTS`
   - This creates a structured implementation plan with impact analysis,
     step-by-step instructions, HIPAA assessment, and test requirements

2. The plan-feature skill will present the plan and ask for approval

3. **Wait for the user to reply "approved"**
   - If the user requests changes: the plan-feature skill handles re-iteration
   - Do NOT proceed to implementation until explicit approval

---

## Phase 2 — IMPLEMENTATION

4. Implement the code changes according to the approved plan:
   - Follow the plan's step order (Domain.Shared → Domain → Contracts → Application → EF Core → HttpApi → Angular)
   - For each step, read the target file before modifying it
   - Apply changes using the conventions from `.claude/discovery/conventions.md`

5. **Critical implementation rules:**
   - Always run `dotnet` commands from `P:\` drive path
   - Add `[RemoteService(IsEnabled = false)]` on any new AppService
   - Use Riok.Mapperly `[Mapper]` for new mappers, NOT AutoMapper
   - Route create/update through the DomainManager if one exists for the feature
   - Never edit `angular/src/app/proxy/` — run `abp generate-proxy` instead
   - If EF Core entity changes were made, create a migration:
     ```bash
     dotnet ef migrations add <MigrationName> \
       --project src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore \
       --startup-project src/HealthcareSupport.CaseEvaluation.HttpApi.Host
     ```

6. Print summary of files changed:
   ```
   Implementation complete:
     Files created: [list]
     Files modified: [list]
     Migration: [name, if created]
   ```

---

## Gate 2 — TESTING

7. Run the project's tests:
   ```bash
   dotnet test
   ```

8. If tests fail:
   - Read the error output
   - Attempt to fix (max 2 attempts)
   - If still failing after 2 attempts: stop and report the failures to the user

9. Print test results:
   ```
   Test results:
     Total: [N]
     Passed: [N]
     Failed: [N]
     [If failures: list them]
   ```

10. If the plan included manual verification steps, remind the user:
    > "Manual verification needed:
    > [list the steps from the plan]
    > Confirm when manual testing is complete."

---

## Phase 3 — DOCUMENTATION

11. Run `/update-docs {Feature}` for each feature affected by the implementation
    - This regenerates CLAUDE.md, syncs to docs/, verifies accuracy, and syncs to vault

12. Print documentation summary:
    ```
    Documentation updated:
      CLAUDE.md regenerated: [list features]
      docs/ synced: [list features]
      Vault: [synced / skipped]
    ```

---

## Gate 3 — HIPAA REVIEW (conditional)

**Only if the plan flagged `HIPAA_IMPACT: HIGH`:**

13. Print HIPAA review prompt:
    ```
    This change touches PHI-related code. Please confirm:
    
    - [ ] Code changes reviewed for PHI exposure
    - [ ] No real patient data in code, tests, or logs
    - [ ] Test data uses synthetic values only
    - [ ] New API endpoints reviewed for minimal PHI exposure
    - [ ] No new PHI logging introduced
    
    Type 'hipaa-approved' to proceed, or describe concerns.
    ```

14. **Wait for the user to reply "hipaa-approved"**
    - Do NOT proceed until explicit HIPAA confirmation

**If HIPAA_IMPACT is NONE:** skip this gate entirely.

---

## Phase 4 — SHIP

15. Stage and commit the changes:
    - Stage all relevant files (source code, migrations, docs)
    - Do NOT stage `appsettings.secrets.json` or `.env` files
    - Create a commit with a descriptive message following the project's commit style:
      ```
      [feature/fix/refactor]: [short description]
      
      [longer description of what changed and why]
      
      Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
      ```

16. Ask if the user wants to create a PR:
    > "Changes committed. Create a pull request? (yes/no)"
    
    If yes:
    - Push the branch
    - Create PR using `gh pr create` with summary from the plan
    - Include the HIPAA checklist in the PR body if HIPAA_IMPACT was HIGH

---

## Final Report

```
Development complete:
  Task: {description}
  Plan: approved
  Implementation: {N} files created, {N} files modified
  Tests: {N} passed, {N} failed
  Documentation: {features} CLAUDE.md + docs/ updated
  HIPAA: {approved / not required}
  Commit: {hash}
  PR: {URL or "not created"}

Duration: {approximate time from start to finish}
```

---

## Constraints

- **Never skip a gate** — each gate requires explicit human approval
- **Never bypass the DomainManager** — if a feature has one, use it
- **Never skip documentation** — always run /update-docs after implementation
- **HIPAA gate is mandatory** for PHI-touching changes — even if the user says "skip it"
- **Max 2 fix attempts** for test failures — then escalate to the user
- **Never commit `appsettings.secrets.json`** — these contain ABP license keys
- **Run from P: drive** — dotnet commands from the real path cause SNI.dll failures
