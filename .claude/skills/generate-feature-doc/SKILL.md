---
name: generate-feature-doc
description: Reads all code files for a feature across all layers and generates a CLAUDE.md in the feature's Domain folder documenting its purpose, file map, entity shape, patterns, and gotchas
argument-hint: <feature-name> (e.g. Appointments, DoctorAvailabilities, AppointmentTypes)
---

# generate-feature-doc

Generates a feature-level CLAUDE.md by reading actual code across all ABP layers.
Invoked after writing or modifying a feature. Produces documentation anchored in
the feature's Domain folder.

---

## Step 0 -- Safety check (HIPAA / synthetic-data guard)

Before reading any file, internalise this rule and apply it throughout every
later step (especially Step 2 reads of `*DataSeedContributor.cs`,
`en.json`, AppService validation messages, and Angular template content):

- Do NOT copy any string literal that looks like a real person's name, SSN,
  date of birth, address, phone number, email, or medical record identifier
  into the generated CLAUDE.md. Some entities (Patients, Doctors, Cases)
  have seed data that may include test values that resemble real PHI.
- When in doubt, replace the literal with the placeholder `<example>` and
  describe the field shape instead (e.g., `LastName: <example> -- string,
  required, max 64 chars`).
- This guard applies to ALL output sections: File Map, Entity Shape,
  Business Rules, Angular UI Surface, Known Gotchas, and any preserved
  manual content. If you would have to redact a value to ship it, omit it.

---

## Step 1 -- LOCATE files for the feature

Given `$ARGUMENTS` as the PascalCase feature name (e.g. `Appointments`), search
these exact paths. The `{Feature}` placeholder is PascalCase; `{feature-kebab}`
is the kebab-case conversion (e.g. `DoctorAvailabilities` -> `doctor-availabilities`).

### Per-feature folders (glob each):

```
src/HealthcareSupport.CaseEvaluation.Domain.Shared/{Feature}/**/*.cs
src/HealthcareSupport.CaseEvaluation.Domain.Shared/Enums/*{Feature}*.cs
src/HealthcareSupport.CaseEvaluation.Domain.Shared/Enums/*{EntitySingular}*.cs
src/HealthcareSupport.CaseEvaluation.Domain/{Feature}/**/*.cs
src/HealthcareSupport.CaseEvaluation.Application.Contracts/{Feature}/**/*.cs
src/HealthcareSupport.CaseEvaluation.Application/{Feature}/**/*.cs
src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/{Feature}/**/*.cs
src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/{Feature}/**/*.cs
angular/src/app/{feature-kebab}/**/*.ts
angular/src/app/{feature-kebab}/**/*.html
angular/src/app/proxy/{feature-kebab}/**/*.ts
```

Angular files are REQUIRED for every feature -- do not skip if not immediately found.
If `angular/src/app/{feature-kebab}/` does not exist, check for alternate naming:
- `doctor-management/` may contain `doctors/` or `patients/` subfolders
- `shared/` may contain feature components
Always grep `angular/src/app/` for the entity name to locate all components.

For the Angular layer, you MUST read BOTH the `.ts` files AND the corresponding `.html`
template files. The template reveals content invisible in `.ts` alone:
  - Form field names and types (`ngModel`, `formControlName`)
  - Permission directives (`*abpPermission="..."`)
  - ABP UI components used (`abp-page`, `abp-lookup-select`, `abp-modal`, `abp-table`)
  - Conditional rendering (`ngIf`, `ngSwitch`) that implies business logic

**Important:** Enums often live in `Domain.Shared/Enums/` rather than in the
feature subfolder. Always grep `Domain.Shared/Enums/` for the entity name
(e.g., `AppointmentStatusType.cs` for Appointments, `BookingStatus.cs` for
DoctorAvailabilities). Include any enum files found in the File Map.

### Cross-cutting files (grep for feature name in each):

```
src/HealthcareSupport.CaseEvaluation.Application/CaseEvaluationApplicationMappers.cs
src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissions.cs
src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissionDefinitionProvider.cs
src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationDbContext.cs
src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationTenantDbContext.cs
src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json
```

For the localization JSON, grep for the feature name (and its singular form)
to surface user-facing string keys (permissions labels, menu entries,
validation messages). Record any feature-specific keys found so the doc can
reference them; ignore keys that are clearly project-wide.

If a folder does not exist for a layer, note it as "Not found" -- do not skip silently.

---

## Step 2 -- READ every file found

For each file, extract:

- **What it is**: entity, domain manager, repository interface, DTO, AppService, mapper, controller, Angular component/service/route
- **Business purpose**: one line
- **Key methods**: name + one-line purpose for each public method
- **Base class**: especially `FullAuditedAggregateRoot<Guid>`, `FullAuditedEntity<Guid>`, `BasicAggregateRoot<Guid>`, `Entity<Guid>`, `DomainService`, `CaseEvaluationAppService`, `AbpController`, `MapperBase<S,D>`. If the base class is none of the above, record the literal class declaration verbatim (e.g., `: AuditedAggregateRoot<Guid>, IMultiTenant`) so the runner does not silently misclassify lighter base classes that intentionally opt out of audit fields.
- **IMultiTenant**: whether the entity implements it (check for `IMultiTenant` in class declaration)
- **Status/state fields**: any enum-typed properties suggesting a state machine
- **Relationships**: foreign key Guid properties referencing other entities
- **Comments**: anything marked TODO, HACK, NOTE, IMPORTANT, WARNING, FIXME

### For the AppService (Application/{Feature}/{Feature}AppService.cs):

Extract the **business rules** that govern this feature -- these are constraints enforced
in code, not in the domain entity itself. Look for:

- **Validation rules:** what is checked before create/update (e.g., slot availability, uniqueness)
- **Auto-generated values:** any field the AppService silently overrides or computes (e.g., confirmation numbers, status defaults)
- **One-way state changes:** operations that mark something permanently (e.g., marking a slot Booked without releasing it)
- **Frozen fields:** fields that cannot be updated after creation (check which parameters are absent from `UpdateAsync`)
- **Lookup filtering:** any lookup endpoint that returns a filtered subset rather than all records
- **Access control nuance:** any case where the `[Authorize(Permission.X)]` decorator is missing or less restrictive than expected
- **Numeric caps or format constraints:** any hardcoded limits (e.g., "A99999" overflow, max length not enforced at DB level)

**Do not list these in Known Gotchas.** Business rules are intentional design decisions.
Known Gotchas are bugs, inconsistencies, or deviations from the Reference Pattern.

### For the mapper file (CaseEvaluationApplicationMappers.cs):

This project uses **Riok.Mapperly** (NOT AutoMapper). Find all `[Mapper]`-decorated
partial classes related to the feature. The pattern is:

```csharp
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class {Entity}To{Dto}Mapper : MapperBase<{Entity}, {Dto}>
{
    public override partial {Dto} Map({Entity} source);
    public override partial void Map({Entity} source, {Dto} destination);
}
```

Some mappers have `AfterMap()` overrides to set `DisplayName` on `LookupDto<Guid>`.
Note the exact class names found.

For any `[Mapper]` class that maps TO `LookupDto<Guid>`:
- Locate the partial class body across all files. Riok.Mapperly mappers are
  `partial class` declarations and may span multiple files. Use this
  procedure:
  1. Grep for `class {Entity}To{Dto}Mapper` in
     `src/HealthcareSupport.CaseEvaluation.Application/`.
  2. For each file containing that class, read the entire `partial class`
     body (from the opening brace `{` after the class declaration to the
     matching closing brace `}`).
  3. Within those bodies, look for `protected override void AfterMap`.
- Record the EXACT assignment: e.g., `destination.DisplayName = source.Name`
  or `destination.DisplayName = $"{source.FirstName} {source.LastName}"`
- If no file containing the partial class declares `AfterMap`, write
  "No AfterMap -- DisplayName not set"
- NEVER write "Unknown" or "Check AfterMap" -- this value must always be verified directly

### For the permissions files:

Check `CaseEvaluationPermissions.cs` for a nested static class matching the feature:

```csharp
public static class {Feature}
{
    public const string Default = GroupName + ".{Feature}";
    public const string Create = Default + ".Create";
    public const string Edit = Default + ".Edit";
    public const string Delete = Default + ".Delete";
}
```

Check `CaseEvaluationPermissionDefinitionProvider.cs` for the registration:

```csharp
var perm = myGroup.AddPermission(CaseEvaluationPermissions.{Feature}.Default, L("Permission:{Feature}"));
perm.AddChild(CaseEvaluationPermissions.{Feature}.Create, L("Permission:Create"));
perm.AddChild(CaseEvaluationPermissions.{Feature}.Edit, L("Permission:Edit"));
perm.AddChild(CaseEvaluationPermissions.{Feature}.Delete, L("Permission:Delete"));
```

### For the DbContext files:

Entity configuration is **inline in OnModelCreating()** -- there is no separate
`ConfigureCaseEvaluation()` extension method. Search for `builder.Entity<{Entity}>`
in both `CaseEvaluationDbContext.cs` and `CaseEvaluationTenantDbContext.cs`. Note
whether it is inside an `if (builder.IsHostDatabase())` block (host-scoped) or
outside (both contexts / tenant-scoped).

---

## Step 2.5 -- DISCOVER INBOUND FKs

After reading all per-feature files, grep both DbContext files for FK references TO this entity.
Search `CaseEvaluationDbContext.cs` and `CaseEvaluationTenantDbContext.cs` for the pattern:
  `.HasForeignKey(x => x.{EntitySingular}Id)`
where `{EntitySingular}` is the singular of this entity name (e.g., "State" for States feature,
"AppointmentLanguage" for AppointmentLanguages).

The grep pattern must match the complete property name -- use word-boundary
anchors and tolerate any lambda parameter name (the codebase uses `x`, `e`,
`b`, `entity`, multi-line lambdas, and occasionally `nameof(...)` style).
Use a two-pass search:

1. First pass: grep for `HasForeignKey` lines in both DbContext files.
2. Second pass: for each match, check the property name with a permissive
   regex that does not assume `x =>` and does not assume single-line spacing:
   `HasForeignKey\([^)]*\.{EntitySingular}Id\b`

This prevents "AppointmentId" from matching when searching for "Appointment" vs "AppointmentType".
If the entity singular ends in a common suffix (Type, Status, Language), also search for the
full combined name (e.g., "AppointmentTypeId" not just "TypeId").

For each match:
- Record the owning entity (e.g., "Location" owns "Location.StateId")
- Record the delete behavior from the same builder block: `SetNull`, `NoAction`, or `Cascade`
- Note whether this config is inside an `IsHostDatabase()` guard

Also search for:
  `.HasOne(x => x.{EntitySingular})`
in case navigation properties reference it without an explicit `HasForeignKey` call.

Add an `## Inbound FKs` section in the generated CLAUDE.md immediately before `## Known Gotchas`.
If no inbound FKs are found, omit the section entirely -- do NOT write "None found."

Format when inbound FKs exist:

| Source Entity.Property | Delete Behavior | Host-only? | Notes |
|---|---|---|---|
| `Location.StateId` | SetNull | Yes (IsHostDatabase guard) | Optional address state |

---

## Step 3 -- CHECK FOR EXISTING CLAUDE.md

Look for an existing CLAUDE.md at:
`src/HealthcareSupport.CaseEvaluation.Domain/{Feature}/CLAUDE.md`

If it exists, extract everything between these markers and preserve it:

```
<!-- MANUAL:START -->
(content here is preserved verbatim)
<!-- MANUAL:END -->
```

If the markers don't exist but there is manual prose not matching the generated
headings, preserve it under Manual Notes.

---

## Step 4 -- WRITE the feature CLAUDE.md

Output path: `src/HealthcareSupport.CaseEvaluation.Domain/{Feature}/CLAUDE.md`

Use this exact structure:

```markdown
# {Feature Name}

{2-sentence summary: what it is, who uses it, business purpose}

## File Map

| Layer | File | Purpose |
|---|---|---|
{one row per file found, with path relative to repo root, one-line business purpose.
For Angular: group purely-presentational triplets (.ts + .html + .scss for the
same component) into a single row, but list logic-bearing files individually.
Specifically:
- Group: a concrete component triplet (e.g., `xxx.component.ts/html/scss`)
  becomes ONE row pointing at the .ts file, with purpose "concrete component
  (template + styles in adjacent .html/.scss)".
- List individually: every abstract component file
  (`abstract-xxx.component.ts`), every service (`xxx.service.ts`), every
  proxy/data-access file under `proxy/`, every `*.module.ts`, every
  `*.routes.ts`, and every standalone component file (the ones with
  `standalone: true`).
- Example: for a feature with `abstract-doctors.component.ts`,
  `doctors.component.ts`, `doctors.component.html`, `doctors.component.scss`,
  and `doctors.service.ts`, you produce three rows: one for the abstract
  component, one for the concrete component (covering the .ts/.html/.scss
  triplet), and one for the service.}

## Entity Shape

{Key fields, their types, and business meaning.}
{If a status/state enum field exists, show the possible values as a text state diagram:}
{e.g., Pending -> Approved -> CheckedIn -> CheckedOut -> Billed}

## Relationships

{Which entities this feature links to and how (FK name -> target entity).}
{Navigation properties and join tables if applicable.}

## Multi-tenancy

{IMultiTenant: yes/no.}
{If yes: what data is scoped to tenant.}
{If no: confirm intentionally host-scoped and why (shared lookup data).}
{DbContext: which context(s) configure it, inside IsHostDatabase() guard or not.}

## Mapper Configuration

{Exact Riok.Mapperly partial class names found in CaseEvaluationApplicationMappers.cs.}
{What maps to what: Entity -> Dto, WithNavProps -> WithNavPropsDto, Entity -> LookupDto.}
{Any AfterMap() overrides and what they set.}

## Permissions

{Exact permission constant names from CaseEvaluationPermissions.cs.}
{How they are registered in CaseEvaluationPermissionDefinitionProvider.cs.}
{What the Angular UI checks (e.g., *abpPermission="'CaseEvaluation.{Feature}.Create'").}

## Business Rules

{Intentional design constraints enforced by the AppService or domain -- not bugs.}
{Include:}
- {Validation rules: what is checked before create/update}
- {Auto-generated values: fields the AppService silently computes or overrides}
- {Frozen fields: what UpdateAsync intentionally does NOT update, and why}
- {One-way operations: state changes that cannot be reversed through normal flows}
- {Lookup filtering: if lookup endpoints return subsets rather than all records}
- {Access control nuance: if permissions are less restrictive than the UI implies}
{If there are no business rules beyond standard CRUD, write "Standard CRUD -- no special business rules."}

Even for SIMPLE lookup entities, always check and document:
1. Whether the AppService enforces uniqueness (grep for "Any" or "FindAsync" in CreateAsync)
2. Whether there are default values set in any CreateDto (grep for "= " in DTO property defaults)
3. Whether any field has a hardcoded limit not captured in Consts (grep for numeric literals in AppService)
If truly none apply, write: "Standard CRUD -- no validation, uniqueness, or computed fields."

## Angular UI Surface

| Component | File | Route | Purpose |
|---|---|---|---|
| {ComponentName} | `angular/src/app/{feature-kebab}/...` | `/route/path` | one-line purpose |

**Pattern detection:** Read each component file and inspect its `@Component`
decorator. If the decorator includes `standalone: true`, classify as
"Standalone Angular component". Otherwise, if the file pair follows the
`AbstractXxxComponent` + `XxxComponent` naming, classify as "ABP Suite
scaffold (abstract/concrete)". Document only the path that applies; if the
feature has both styles across components, document each component under the
matching sub-path.

### Sub-path A -- ABP Suite scaffold (abstract/concrete)

Use when the feature was generated by ABP Suite and uses the
`AbstractXxxComponent` -> `XxxComponent` pair (no `standalone: true`).

**Forms:** {If create/edit form exists, list the input fields found in the .html template.}
- {fieldName}: {input type} -- {validation or business constraint if visible in template}

**Permission guards:**
{List every *abpPermission directive and permissionGuard value used in this feature's routes
and templates. Read the route definition file and the .html templates.}

**Services injected:**
{List the services the abstract component or concrete component injects. Read the constructor
of both the abstract and concrete component files.}

### Sub-path B -- Standalone Angular component

Use when any component file has `standalone: true` in its `@Component`
decorator (Angular 14+ standalone API; default in Angular 20). There is no
abstract/concrete pair; the component imports its dependencies directly.

**Imports:** {List the modules / standalone components / directives imported
via the `imports: [...]` array of the `@Component` decorator.}

**Forms:** {If a create/edit form is present, list the input fields found in
the inline template or `.html` template.}
- {fieldName}: {input type} -- {validation or business constraint if visible in template}

**Permission guards:**
{List every *abpPermission directive used in the template, plus any route
guard configured in the standalone route definition (often a `Routes`
constant exported from a routing file or registered in `app.routes.ts`).}

**Services injected:**
{List services injected via the constructor or via `inject()` calls inside
the standalone component.}

### Other patterns

If the feature uses dialog-only flows, Angular Material primitives without
ABP UI, or another non-scaffold pattern, document it under "Other patterns"
with a one-line description and the file paths involved.

If this feature has no Angular UI (host-only entities managed via API only), write:
"No Angular UI -- this entity is managed via API only."

## Known Gotchas

{Anything found in TODO/HACK/NOTE/WARNING/FIXME comments.}
{Any deviation from the Reference Pattern in root CLAUDE.md.}
{Any missing layers (e.g., no controller, no tests, no domain manager).}

Constructor completeness: If the entity constructor accepts fewer fields than the entity has
settable properties:
- Count constructor parameters vs total non-ID non-audit properties
- Check the Manager's CreateAsync -- does it call entity.Property = value after construction?
- If yes: document as "Constructor sets {n}/{total} fields; remaining set post-construction
  by Manager (code-gen artifact)"
- If no: document as "Constructor intentionally omits {field} -- set only via Update path"
Do not list this as a gotcha if ALL required fields are in the constructor and optional
fields are set post-construction.

## Links

- Root architecture: [CLAUDE.md](/CLAUDE.md)
- Reference pattern: [CLAUDE.md#reference-pattern-appointments](/CLAUDE.md#reference-pattern-appointments)
- Docs: [docs/features/{feature-kebab}/overview.md](/docs/features/{feature-kebab}/overview.md) (if exists)

<!-- MANUAL:START -->
{preserved content from prior version, or empty}
<!-- MANUAL:END -->
```

---

## Step 5 -- UPDATE ROOT INDEX

After writing the feature CLAUDE.md, update the root CLAUDE.md's
`## Context Loading` -> `### Feature CLAUDE.md Index` table.

1. Search the table for a row whose Feature column matches `{Feature}` exactly.
   - If no exact match exists, search for a case-insensitive match and emit a
     warning (e.g., "Found row 'appointments' but searched for 'Appointments' --
     proceeding with case-insensitive match").
   - If still no match, insert a new row in alphabetical order.
   - Never silently create a duplicate row. If a row matches, update it in
     place; do not append a second row for the same feature.
2. Replace (or insert) the row with:
   ```
   | {Feature} | {one-line summary, target 8-12 words from CLAUDE.md line 1} | `src/.../Domain/{Feature}/CLAUDE.md` | [overview](docs/features/{feature-kebab}/overview.md) |
   ```
   - If no `docs/features/{feature-kebab}/overview.md` exists yet, use `--` for the docs/ column
3. The placeholder text in the existing row may be `(not yet documented)`,
   `TBD`, `pending`, or any other value -- do not assume a single placeholder
   string. Match on the Feature column only.

---

## Step 6 -- REPORT

Tell the developer:
1. Path of the CLAUDE.md written
2. Files scanned (count per layer)
3. Which sections were auto-generated vs preserved from a prior version
4. Any files expected but not found (gaps in the pattern):
   - Missing domain manager -> feature uses AppService directly
   - Missing controller -> might be auto-wired (unusual for this project)
   - Missing tests -> coverage gap
   - Missing Angular abstract component -> simpler feature
5. Any unusual patterns noticed that deviate from the Appointments reference

<!-- SKILL-ASSESSMENT:
Date: 2026-04-24
Features documented: 15 (14 new + 1 pre-existing)
Average score: 13.8/15 across scored features (1-5)

Score breakdown by dimension:
- File Coverage: consistently 3/3 -- skill finds all layers reliably
- Factual Accuracy: consistently 3/3 -- spot-checks always verified
- Business Rules: 2-3/3 -- strong on MEDIUM/COMPLEX, thin on SIMPLE (expected)
- Gotcha Quality: 2-3/3 -- occasionally missed proxy method count mismatches
- Actionability: 2-3/3 -- one gap: AfterMap on LookupDto mappers sometimes marked "Unknown" instead of verified

Lowest dimension: Business Rules (on SIMPLE features) and Actionability (AfterMap verification)
Times improved: 0 -- scores stayed above 12/15 threshold throughout
Known limitations:
1. Does not verify AfterMap overrides on LookupDto mappers -- should grep for the specific class
2. For SIMPLE lookup entities, Business Rules section often says "Standard CRUD" which is correct but scores low on the rubric
3. Constructor field coverage: sometimes notes that constructor accepts fewer fields than the entity has, but doesn't always distinguish code-gen artifact from deliberate design
4. Cross-feature references (inbound FKs) are sometimes incomplete -- the skill documents outbound FKs well but doesn't systematically check what other features reference this entity

One change that would most improve it:
Add a Step 2.5 after reading the entity: "Grep all DbContext files for FK references TO this entity (e.g., HasForeignKey(x => x.{Entity}Id)) and document them in an Inbound FKs section."
-->
