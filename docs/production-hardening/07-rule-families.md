# Phase 7 -- Sonar rule families

**Change class:** varies per family -- stated per row below. Most are compiler-verified mechanical
fixes where the existing suite suffices. The complexity families are behaviour-preserving refactors
and need characterization tests first.

**1,249 issues across 100 rule families.** This is bulk work, and it is the phase most likely to be
incomplete at handoff. That is fine, provided each family is finished and locked before the next
is started.

**The rule for every family, without exception:**

1. Triage a sample first -- is this real *here*?
2. Fix the family.
3. **Lock it closed** in the same PR (analyzer severity, banned symbol, or eslint rule), per
   [02-enforcement.md](02-enforcement.md) 2.5.

A family cleared without a lock returns. Two of six blockers were already false positives, so step
1 is not a formality.

---

## The families, ordered

Ordering is by (count x tractability), with accessibility first because it is 26% of the total,
one coherent workstream, and a real compliance exposure for a public medical portal.

### Tier A -- accessibility, 330 issues, one workstream

| Count | Rule | Change class |
| --- | --- | --- |
| 253 | `Web:InputWithoutLabelCheck` | Template; existing suite |
| 77 | `Web:MouseEventWithoutKeyboardEquivalentCheck` | Template; existing suite |

Form inputs without labels, and mouse handlers with no keyboard path. Beyond the scanner, this is
ADA/Section 508 exposure on a portal used by injured workers, some of whom will be using assistive
technology. Do this properly rather than by adding `aria-label` everywhere -- a visible `<label>`
is the correct fix in most cases and helps every user.

**Risk to watch:** these change DOM structure, so component specs that query by selector will
break. That is the suite doing its job. Budget for spec churn.

### Tier B -- mechanical C#, ~430 issues

| Count | Rule | What |
| --- | --- | --- |
| 119 | `external_roslyn:CA1873` | Guard expensive logging behind a level check |
| 96 | `csharpsquid:S8969` | Confirm meaning during triage |
| 65 | `external_roslyn:CA1861` | Hoist constant arrays to `static readonly` |
| 48 | `csharpsquid:S125` | Delete commented-out code |
| 29 | `external_roslyn:CA1862` | Use `StringComparison` overloads |
| 28 | `external_roslyn:CA1510` | Use `ArgumentNullException.ThrowIfNull` |
| 27 | `csharpsquid:S1192` | Duplicated string literals -> constants |
| 25 | `external_roslyn:CA1859` | Use concrete types where possible |
| 20 | `csharpsquid:S3267` | Loops to LINQ |

All compiler-verified. Existing suite suffices. Several are auto-fixable by the analyzers
themselves -- try `dotnet format analyzers` before hand-editing, then review the diff rather than
trusting it.

`S125` (commented-out code) deserves a note: deleting it is correct. It is in version control; a
comment block is not a backup.

### Tier C -- TypeScript and tooling, ~120 issues

| Count | Rule |
| --- | --- |
| 44 | `typescript:S5906` |
| 29 | `shelldre:S7688` |
| 16 | `external_roslyn:xUnit1004` (skipped tests -- see note) |
| 15 | `powershelldre:S8657` |
| 13 | `external_roslyn:RMG089` (Mapperly) |
| 11 | `css:S7924` |
| 8 | `typescript:S7758`, 8 `SYSLIB1045`, 7 each `S2933`/`S7781`/`css:S1874`/`S3358`/`S6582`/`S2325` |

**`xUnit1004` x16 is more interesting than its count.** It flags skipped tests. The suite reports
16 skipped, which matches exactly. Each one is a test someone disabled and nobody re-enabled --
find out why. Some will be legitimately blocked; others are silent coverage holes on a codebase
that is about to be hardened.

### Tier D -- complexity and design, ~90 issues

| Count | Rule | Change class |
| --- | --- | --- |
| 41 | `csharpsquid:S107` | Too many parameters -- **characterization tests first** |
| 17 | `csharpsquid:S3776` | Cognitive complexity (C#) -- **characterization tests first** |
| 19 | `python:S1192` | Duplicated literals in scripts |
| 5+5+4 | `S3776` in powershell/python/typescript | **characterization tests first** |
| 6 | `csharpsquid:S4487` | Unused private fields |

**These are the only families in phase 7 that can silently break behaviour.** S3776 means the
method is hard enough to reason about that a human refactoring it will make a mistake -- which is
the entire argument for pinning behaviour with tests before touching it.

Note the alignment with the standing thresholds in `~/.claude/rules/code-standards.md`: cognitive
complexity 15, parameters 4 (7 for DI constructors). The Sonar defaults and the house rules agree,
so clearing these families and then enforcing the threshold is coherent.

### Tier E -- the long tail

~54 families with fewer than 5 issues each, roughly 109 issues total. Batch them into a single
sweep at the end. Do not spend a research cycle per family at this size; triage in bulk, fix what is
real, log what is not.

---

## Watch for: `csharpsquid:S2068` x5 -- "hardcoded credentials"

Listed in Tier E by count, called out here by risk. Given two of six BLOCKERs were false positives
on exactly this theme, assume nothing. Read each of the five sites. Either they are real and belong
in phase 1, or they are noise and belong in the triage log -- but they should not be discovered
during a bulk sweep.

---

## Validation loop

Per family, matching the layers it touches:

```
dotnet format --verify-no-changes
dotnet build -warnaserror
dotnet test
npx ng build
npx ng test --watch=false --browsers=ChromeHeadless
npx eslint --ext .html,.ts src/app
```

Re-measure the family count after each:

```bash
curl -s "https://sonarcloud.io/api/issues/search?componentKeys=gesco-healthcare-support_hcs-patient-portal&resolved=false&ps=1&facets=rules" \
  | python -c "import sys,json;[print(v['count'],v['val']) for f in json.load(sys.stdin)['facets'] if f['property']=='rules' for v in f['values'][:20]]"
```

Baseline: 1,280 issues total, 100 families, top 15 = 920.
