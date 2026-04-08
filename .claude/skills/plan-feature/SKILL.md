---
name: plan-feature
description: "Analyze a development task and create a detailed implementation plan. Reads relevant feature CLAUDE.md files, identifies affected layers, includes HIPAA checklist when PHI-touching code is involved."
argument-hint: "<task description>"
---

# plan-feature

Creates a structured implementation plan for a development task by reading
the relevant feature documentation and applying the project's conventions.

---

## Step 1 — PARSE THE TASK

Read the task description from `$ARGUMENTS`. Identify:

- **Target entity/feature:** Which feature(s) from the module map are affected?
  Map keywords to PascalCase feature names (e.g. "appointment" → Appointments,
  "doctor availability" → DoctorAvailabilities, "patient" → Patients)
- **Change type:** new feature | modify existing | bug fix | refactor | config change
- **Scope hint:** Which ABP layers are likely affected?
  - Adding a field → Domain.Shared (consts) + Domain (entity) + Contracts (DTOs) + Application (AppService) + EF Core (migration) + HttpApi (controller param) + Angular (form field)
  - Adding a new entity → ALL layers (follow reference pattern end-to-end)
  - Bug fix → Usually 1-2 layers
  - UI-only change → Angular only (but verify the API supports it)

---

## Step 2 — LOAD CONTEXT

Read these files in order (stop early if you have enough context):

1. Root `CLAUDE.md` — project overview, architecture, critical constraints
2. `.claude/discovery/reference-pattern.md` — the Appointments layer-by-layer trace
3. `.claude/discovery/conventions.md` — naming, file organization, key patterns

If modifying an **existing feature:**
4. `src/HealthcareSupport.CaseEvaluation.Domain/{Feature}/CLAUDE.md` — entity shape, relationships, business rules, gotchas

If the feature has **FK relationships** to other features:
5. Read related feature CLAUDE.md files listed in the Relationships section

If adding a **new entity:**
6. Read the reference pattern feature's CLAUDE.md (`src/.../Domain/Appointments/CLAUDE.md`)
   as the implementation template — every layer needs a corresponding file

---

## Step 3 — HIPAA IMPACT ASSESSMENT

Check whether this task touches PHI-related code. PHI entities in this project:

| Entity | PHI Fields |
|--------|-----------|
| Patient | FirstName, LastName, DateOfBirth, Email, PhoneNumber, Address fields |
| Appointment | Links to Patient; contains medical scheduling details |
| AppointmentEmployerDetail | Employer info linked to patient appointments |
| ApplicantAttorney | Attorney contact info (not PHI itself, but linked to patients) |

Set `HIPAA_IMPACT`:
- **HIGH** if the task modifies Patient, Appointment, or AppointmentEmployerDetail entities/DTOs/endpoints,
  OR adds new fields that could contain patient demographics, medical info, or PII
- **NONE** if the task only touches host-scoped lookups (Location, State, AppointmentType, etc.),
  UI layout, configuration, or non-PHI entities

---

## Step 4 — CREATE THE PLAN

Output a structured implementation plan. Follow ABP's layer dependency order:
Domain.Shared → Domain → Application.Contracts → Application → EntityFrameworkCore → HttpApi → Angular

```markdown
# Implementation Plan: [task title]

## Impact Analysis
- **Feature(s) affected:** [list with links to their CLAUDE.md files]
- **Layers to modify:** [list — e.g. Domain, Contracts, Application, EF Core, HttpApi, Angular]
- **HIPAA impact:** [HIGH / NONE]
- **Estimated complexity:** [simple (1-2 layers) / medium (3-4 layers) / complex (all layers)]
- **Multi-tenancy consideration:** [tenant-scoped / host-scoped / N/A]

## Pre-implementation Checks
- [ ] Feature CLAUDE.md read and understood
- [ ] Related feature CLAUDE.md files checked for FK impacts
- [ ] Reference pattern consulted for layer structure
[If new entity:]
- [ ] Entity scoping decided: IMultiTenant (tenant) or host-only
- [ ] DbContext placement decided: inside IsHostDatabase() guard (host) or outside (both)

## Implementation Steps

### Step 1: Domain.Shared — [what to do]
- File: `src/HealthcareSupport.CaseEvaluation.Domain.Shared/{Feature}/{Entity}Consts.cs`
- Change: [add MaxLength const, add enum value, etc.]

### Step 2: Domain — [what to do]
- File: `src/HealthcareSupport.CaseEvaluation.Domain/{Feature}/{Entity}.cs`
- Change: [add property, modify constructor, etc.]
[If business rules exist:]
- File: `src/HealthcareSupport.CaseEvaluation.Domain/{Feature}/{Entity}Manager.cs`
- Change: [update CreateAsync/UpdateAsync parameters]

### Step 3: Application.Contracts — [what to do]
- File: `src/HealthcareSupport.CaseEvaluation.Application.Contracts/{Feature}/{Entity}CreateDto.cs`
- Change: [add field with [StringLength] attribute]
- File: `src/.../Application.Contracts/{Feature}/{Entity}UpdateDto.cs`
- Change: [add matching field]
- File: `src/.../Application.Contracts/{Feature}/{Entity}Dto.cs`
- Change: [add output field]

### Step 4: Application — [what to do]
- File: `src/HealthcareSupport.CaseEvaluation.Application/{Feature}/{Entities}AppService.cs`
- Change: [pass new field to manager, update mapping]
- Note: MUST have `[RemoteService(IsEnabled = false)]` attribute
[If mapper changes needed:]
- File: `src/.../Application/CaseEvaluationApplicationMappers.cs`
- Change: [Riok.Mapperly handles new properties automatically if names match]

### Step 5: EntityFrameworkCore — [what to do]
- File: `src/.../EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationDbContext.cs`
- Change: [add column config if needed — MaxLength, required, index]
[If host-scoped entity:]
- Wrap in `if (builder.IsHostDatabase())` guard
- Also update `CaseEvaluationTenantDbContext.cs` if tenant-scoped
- File: [new migration]
- Command: `dotnet ef migrations add Add{FieldName}To{Entity} --project src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore --startup-project src/HealthcareSupport.CaseEvaluation.HttpApi.Host`

### Step 6: HttpApi — [what to do]
- File: `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/{Feature}/{Entity}Controller.cs`
- Change: [update method signatures if AppService interface changed]
- Note: Controller manually delegates — every new AppService method needs a controller method

### Step 7: Angular — [what to do]
- Run: `abp generate-proxy` (from angular/ directory) to regenerate proxy DTOs
- File: `angular/src/app/{feature-kebab}/...component.html`
- Change: [add form field, update table column]
- Note: NEVER edit files in `angular/src/app/proxy/` — they are auto-generated

## HIPAA Compliance Checklist
[Include ONLY if HIPAA_IMPACT is HIGH:]
- [ ] No real patient data in code, tests, examples, or logs
- [ ] All test data uses synthetic values (random hex strings, @example.com emails)
- [ ] New API endpoints do not expose PHI beyond what the consumer needs
- [ ] New database fields for PHI have appropriate access controls
- [ ] Logging does not capture request/response bodies containing PHI
- [ ] New entities/DTOs have PHI fields documented in the feature CLAUDE.md

[Include ALWAYS:]
## HIPAA Quick Check
- [ ] This change does not introduce new PHI handling
  [OR] This change handles PHI — full HIPAA checklist above is completed

## Documentation Impact
- [ ] Feature CLAUDE.md needs regeneration: [yes — run `/generate-feature-doc {Feature}`]
- [ ] docs/ needs re-sync: [yes — run `/sync-feature-to-docs {Feature}`]
- [ ] Root CLAUDE.md Context Loading table needs update: [only if new feature]

## Test Requirements
- [ ] Existing tests still pass: `dotnet test`
- [ ] New AppService tests in `test/.../Application.Tests/{Feature}/`
- [ ] New seed data in `test/.../Domain.Tests/{Feature}/{Entity}DataSeedContributor.cs`
- [ ] Manual verification: [specific steps to verify the change works]

## Critical Reminders
- Run all dotnet commands from `P:\` drive (not the full path)
- Never use `ng serve` — use `ng build --configuration development` + `npx serve`
- Start services in order: AuthServer → HttpApi.Host → Angular
- After backend changes: run `abp generate-proxy` in angular/ to update TypeScript proxies
```

---

## Step 5 — PRESENT FOR APPROVAL

Print the plan and ask:

> "Review this implementation plan. Reply **approved** to proceed with implementation,
> or describe what changes are needed."

**Do NOT begin implementation until the user explicitly approves.**

If the user requests changes, update the plan and re-present it.

---

## Constraints

- **Never skip the HIPAA assessment** — even if the task seems non-PHI, explicitly confirm
- **Never propose changes to `angular/src/app/proxy/`** — always use `abp generate-proxy`
- **Always include the migration command** if any entity or DbContext changes are proposed
- **Always reference specific file paths** — not just layer names
- **Always check the feature CLAUDE.md** for Known Gotchas before proposing changes —
  the gotchas may affect the implementation approach
- **If a DomainManager exists for the feature**, route create/update through it —
  do not bypass to the repository directly from the AppService
