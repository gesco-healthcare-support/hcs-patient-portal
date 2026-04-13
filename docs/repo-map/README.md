[Home](../INDEX.md) > Repository Map > README

# Repository Map

Machine-readable structural index of the codebase. Regenerated on demand from
source files, never hand-edited.

## Files

| File | Purpose |
|------|---------|
| `index.json` | Full machine-readable index: files, symbols, dependencies, rankings |
| `map.md` | Human-readable summary with top-ranked files and a dependency diagram |
| `README.md` | This file |

## Regenerate

```bash
python .claude/scripts/build-repo-map.py
```

The script scans C# under `src/` and `test/`, and TypeScript under
`angular/src/app/` (excluding `proxy/`, `bin/`, `obj/`, `node_modules/`,
`dist/`, `.angular/`). It uses only the Python 3 standard library.

## How the Ranking Works

- **C# files** are ranked by how many other files declare a `using` statement
  for their namespace. A file defining heavily-used types will rank high.
- **TypeScript files** are ranked by how many other files import them via a
  relative path. External package imports (non-relative) are ignored.

Ranking is heuristic, not algorithmic PageRank. Use it for navigation, not
for refactoring decisions.

## When to Regenerate

- After adding or removing a project
- After a large refactor that moves files between projects
- Before a review / release where accurate top-N rankings matter

The `scheduled-doc-check` skill flags staleness if `index.json` is older than
30 days.

## Related

- [Verify Structure Skill](../../.claude/skills/verify-structure/SKILL.md)
- [Generate Repo Map Skill](../../.claude/skills/generate-repo-map/SKILL.md)
