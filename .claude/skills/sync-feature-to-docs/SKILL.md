---
name: sync-feature-to-docs
description: Reads a feature CLAUDE.md and syncs its content to docs/features/<feature-kebab>/overview.md (+ optional api.md, ui.md), updating stale sections while preserving richer human-written prose
argument-hint: <feature-name> (e.g. Appointments, DoctorAvailabilities)
---

# sync-feature-to-docs

Syncs code-derived knowledge from a feature's CLAUDE.md into the project
documentation folder. Uses section-by-section merge that preserves human
prose when it is richer than the generated version.

**Target structure:** `docs/features/<feature-kebab>/` -- organized by product
capability, not by code layer. Each feature gets its own folder with:
- `overview.md` -- primary document (always created)
- `api.md` -- API surface detail (optional, created when API sections are rich)
- `ui.md` -- Angular UI detail (optional, created when UI sections are rich)
- Cross-links to `docs/decisions/` for related ADRs (if they exist)

---

## Step 1 -- LOCATE SOURCE

Find the feature CLAUDE.md at:
`src/HealthcareSupport.CaseEvaluation.Domain/{Feature}/CLAUDE.md`

where `{Feature}` is the PascalCase name from `$ARGUMENTS` (e.g. `Appointments`).

**If the file does not exist:** stop and tell the user:
> Feature CLAUDE.md not found. Run `/generate-feature-claude-md {Feature}` first.

---

## Step 2 -- LOCATE TARGET FOLDER

Convert the feature name to lowercase kebab-case:
- `Appointments` -> `appointments`
- `DoctorAvailabilities` -> `doctor-availabilities`
- `WcabOffices` -> `wcab-offices`
- `AppointmentTypes` -> `appointment-types`

Target folder: `docs/features/{feature-kebab}/`
Primary file: `docs/features/{feature-kebab}/overview.md`

If `docs/features/{feature-kebab}/` doesn't exist, create it.

Also check for existing `api.md` and `ui.md` in the target folder -- if they
exist, they will be updated too.

---

## Step 3 -- SECTION-BY-SECTION MERGE (if overview.md exists)

Parse both the source CLAUDE.md and the target overview.md into sections,
split on `## ` headings. For each section:

1. **Section exists in CLAUDE.md but NOT in overview.md** -> ADD it
2. **Section exists in overview.md but NOT in CLAUDE.md** -> KEEP it
   (human-written, do not remove)
3. **Section exists in both** -> compare content length (character count):
   - If CLAUDE.md version is **longer**: more detail -> UPDATE with CLAUDE.md
   - If overview.md version is **longer**: richer prose -> KEEP overview.md
   - If same length (within 50 characters AND +/-10% -- both must hold):
     prefer CLAUDE.md (fresher from code). The 50-char floor prevents the
     percent rule from misfiring on very short sections.

### Sections routed to overview.md:

- `## File Map`
- `## Entity Shape`
- `## Relationships`
- `## Multi-tenancy`
- `## Mapper Configuration`
- `## Permissions`
- `## Known Gotchas`

### Sections routed to api.md (creation rule below):

Sections that route to api.md when api.md is created:
- `## API Surface` or any section with "API", "HttpApi", "Controller",
  "Endpoints" in the heading
- DTO details, contract descriptions

Creation rule (single source of truth -- evaluate top-down):

1. If `api.md` already exists in the target folder, route the sections above
   into it (update, do not recreate).
2. Else, create `api.md` if and only if at least ONE of these conditions
   holds:
   - The controller has 6 or more endpoint methods.
   - The feature has 3 or more distinct DTO types (Create, Update, Filter,
     WithNavProps all count).
   - The AppService has non-CRUD methods (bulk operations, generate,
     get-or-create, preview).
   - The API section in the CLAUDE.md exceeds 15 lines.
3. If none of the conditions in (2) hold and `api.md` does not already
   exist, include API content inline in overview.md.

### Sections routed to ui.md (creation rule below):

Sections that route to ui.md when ui.md is created:
- `## UI Surface` or any section with "Angular", "Component", "UI" in heading
- Route descriptions, component details

Creation rule (single source of truth -- evaluate top-down):

1. If the CLAUDE.md says "No Angular UI", do not create ui.md.
2. If no `## Angular UI Surface` section exists in the CLAUDE.md, include
   any UI content in overview.md.
3. If the CLAUDE.md contains an `## Angular UI Surface` section AND that
   section does not say "No Angular UI -- this entity is managed via API
   only", ALWAYS create ui.md -- subject to the minimum-content floor in (4).
4. Minimum-content floor: count how many of the four items below have
   non-empty content in the CLAUDE.md UI section. If fewer than two are
   present, SKIP ui.md and inline whatever exists into overview.md instead.
   The four items are:
   - A table of component names, file paths, and routes.
   - Which ABP pattern is used (abstract/concrete vs. custom).
   - Permission guards applied to routes and templates.
   - Services injected by the components.
5. When ui.md IS created, the four items above are the minimum viable
   structure; copy what is present and omit what is missing.

### Special handling:

- The `# {Feature Name}` title and summary paragraph always come from CLAUDE.md
- The `## Links` section is always refreshed from CLAUDE.md
- Everything between `<!-- MANUAL:START -->` and `<!-- MANUAL:END -->` markers
  in the CLAUDE.md is synced as-is under a `## Manual Notes` section
- Everything between `<!-- DOCS:MANUAL:START -->` and `<!-- DOCS:MANUAL:END -->`
  in any docs file is NEVER overwritten -- always preserved in place

---

## Step 4 -- WRITE TARGET FILES

After deciding what gets written, refresh the synced-on date:

- If any section in `overview.md`, `api.md`, or `ui.md` was updated or
  newly added, set the `<!-- Last synced from ... on {YYYY-MM-DD} -->`
  header on that file to today's UTC date. Do not bump the date on a file
  whose only change was preservation of existing prose.

### overview.md structure:

```markdown
<!-- Last synced from src/HealthcareSupport.CaseEvaluation.Domain/{Feature}/CLAUDE.md on {YYYY-MM-DD} -->

# {Feature Name}

{summary paragraph from CLAUDE.md}

{all overview sections in order -- merged per Step 3 rules}

## Links

- Feature CLAUDE.md: `src/HealthcareSupport.CaseEvaluation.Domain/{Feature}/CLAUDE.md`
- Root architecture: [CLAUDE.md](/CLAUDE.md)
- API detail: [api.md](api.md) (if exists)
- UI detail: [ui.md](ui.md) (if exists)
- Related decisions: [docs/decisions/](../../decisions/) (if any ADRs reference this feature)

<!-- DOCS:MANUAL:START -->
{preserved content from prior version, or empty for new files}
<!-- DOCS:MANUAL:END -->
```

### api.md structure (when created):

```markdown
<!-- Last synced from src/HealthcareSupport.CaseEvaluation.Domain/{Feature}/CLAUDE.md on {YYYY-MM-DD} -->

# {Feature Name} -- API

> Synced from feature CLAUDE.md. Update code-derived content there.

{API sections merged per Step 3 rules}

- Back to overview: [overview.md](overview.md)

<!-- DOCS:MANUAL:START -->
{preserved or empty}
<!-- DOCS:MANUAL:END -->
```

### ui.md structure (when created):

```markdown
<!-- Last synced from src/HealthcareSupport.CaseEvaluation.Domain/{Feature}/CLAUDE.md on {YYYY-MM-DD} -->

# {Feature Name} -- UI

> Synced from feature CLAUDE.md. Update code-derived content there.

{UI sections merged per Step 3 rules}

- Back to overview: [overview.md](overview.md)

<!-- DOCS:MANUAL:START -->
{preserved or empty}
<!-- DOCS:MANUAL:END -->
```

---

## Step 4.5 -- ADD CROSS-FEATURE LINKS AND ADR LINKS

Both sub-steps below write into the same target file
(`docs/features/{this-feature-kebab}/overview.md`), so they share one step.

### Step 4.5a -- Cross-feature FK links

Read the `## Relationships` section of the source CLAUDE.md.

**Guard:** If no `## Relationships` section is found (e.g. a leaf lookup
with no FKs), SKIP Step 4.5a entirely and log a single line:
`No relationships -- no cross-links added.` Then continue to Step 4.5b.

For each FK reference listed (both outbound: "this entity -> other entity"
and inbound: "other entity -> this entity"):

1. Identify the related feature name and convert to kebab-case
2. Check whether `docs/features/{related-feature-kebab}/overview.md` exists on disk
   (use Glob or file existence check -- do NOT assume it exists)
3. If YES:
   a. Read the related overview.md -- does it already have a `## Related Features` section?
   b. Does it already link to this feature?
   c. If NOT already linked: add or append to the `## Related Features` section in the related
      overview.md with the format:
      `- [{This Feature}](../{this-feature-kebab}/overview.md) -- {relationship description}`
   d. In THIS feature's overview.md: add or append to a `## Related Features` section:
      `- [{Related Feature}](../{related-feature-kebab}/overview.md) -- {relationship description}`
4. If NO (docs don't exist yet for related feature): skip -- do not add broken links

Only add links where a real FK relationship is explicitly documented in `## Relationships`.
Never infer relationships from naming patterns alone.
Place `## Related Features` before the `## Links` section in each overview.md.
The relative path from `docs/features/{this-feature}/overview.md` to any related feature is
always `../{related-feature}/overview.md` -- never `../../`.

### Step 4.5b -- ADR cross-linking

After updating `overview.md`, scan `docs/decisions/` for any ADR files whose
body or title mentions this feature name (case-insensitive). If found, add a
line to the `## Links` / `## Related` section of the feature's `overview.md`:

```markdown
- Related decisions: [ADR-NNN](../../decisions/NNN-slug.md), ...
```

This creates a bidirectional link so feature readers discover relevant
decisions. If no ADRs mention the feature, skip this sub-step without adding
anything.

---

## Step 5 -- REPORT

Tell the developer:

1. Source path read
2. Target folder and files written (which of overview/api/ui were touched)
3. For each section: **updated** (from CLAUDE.md), **preserved** (docs richer),
   or **new** (added from CLAUDE.md)
4. One-line summary: e.g., "overview.md: 3 updated, 2 preserved, 1 new; api.md: created fresh"
5. If files were created fresh: note all sections are new

---

## Step 5.5 -- UPDATE DOCS INDEX

After writing the feature docs, check `docs/INDEX.md` for a
`### Feature Documentation` section.

1. If this feature is NOT listed there, add a line in alphabetical order:
   ```
   - [{Feature Name}](features/{feature-kebab}/overview.md) -- {one-line summary from CLAUDE.md}
   ```
2. If the feature IS listed but the description differs from the current
   CLAUDE.md summary, update the description
3. If the `### Feature Documentation` section doesn't exist, create it
   between `### Frontend` and `### Business Domain` in the `## Documentation by Section` area

<!-- SKILL-ASSESSMENT:
Date: 2026-04-24 (PR-2 audit applied; PR-3 results pending)
Prior date: 2026-04-03
Features synced: 15 (14 new + 1 pre-existing)
Average score: 7.9/9 across scored features (1-5)

Score breakdown by dimension:
- Human Readability: 2-3/3 -- strong on MEDIUM/COMPLEX features; File Map section is technical but necessary
- Navigation Completeness: consistently 3/3 -- all links, INDEX.md, and Context Loading table always updated
- Routing (api/ui split): 2-3/3 -- correctly split DoctorAvailabilities (3 files); other features stayed as overview-only when content was <20 lines per section

Lowest dimension: Routing -- the 20-line threshold for splitting was rarely triggered, so most features got overview-only. Doctors and Patients could have benefited from api.md splits (10+ endpoints each) but stayed as overview-only.
Times improved: 0 -- scores stayed above 7/9 threshold
Known limitations:
1. The 20-line split threshold is too high for API sections -- features with 10+ endpoints (Doctors, Patients, WcabOffices) would benefit from a separate api.md even if the content is under 20 lines
2. Step 5.5 (UPDATE DOCS INDEX) works well but relies on alphabetical insertion -- if INDEX.md has a different ordering convention, the skill would break it
3. The skill doesn't add cross-links between related features (e.g., Appointments <-> DoctorAvailabilities). The verify skill handles this, but sync could proactively add them based on the Relationships section.
4. For SIMPLE features, the overview.md is nearly identical to the CLAUDE.md -- the sync adds minimal value over just reading the CLAUDE.md directly. The value comes from the INDEX.md integration and the human-readable format.

Is sync adding value over reading CLAUDE.md directly?
Yes, for 3 reasons: (1) INDEX.md integration makes features discoverable by humans, (2) the docs/ folder is browseable in GitHub without knowing Domain paths, (3) the DOCS:MANUAL markers allow human-written additions that persist across re-syncs.
-->
