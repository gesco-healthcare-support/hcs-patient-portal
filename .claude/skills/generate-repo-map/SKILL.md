---
name: generate-repo-map
description: Rebuild docs/repo-map/ artifacts by running build-repo-map.py. Scans C# and TypeScript source, ranks files by in-degree, generates map.md and index.json. Use after significant structural refactors or when adding/removing projects.
argument-hint: (none)
---

# generate-repo-map

Regenerates the machine-readable repository map at `docs/repo-map/`. The map is a
deterministic artifact -- this skill wraps the Python generator and reports what
changed.

## Step 1 -- Run the generator

```bash
python .claude/scripts/build-repo-map.py
```

Expected output: `build-repo-map: OK -- <N> C# files, <M> TS files, <P> projects`

If the script exits with a non-zero code, read the stderr message. Common failure:

- Missing source tree (running outside repo root) -- cd to repo root and retry.
- Unreadable file (encoding issue) -- the script skips unreadable files silently;
  exit code 0 in that case. No action needed.

## Step 2 -- Summarize what the map now says

Read the freshly generated files:

- `docs/repo-map/map.md` -- human-readable summary
- `docs/repo-map/index.json` -- full structural data

Report to the user:

- Number of C# files, TS files, .NET projects
- Top 5 most-referenced C# files (from `top_cs_files`)
- Top 5 most-imported Angular files (from `top_ts_files`)
- Any projects that have zero references (candidates for removal or isolated
  modules)

## Step 3 -- Flag anomalies

- If a top-ranked file has changed compared to prior runs (if available via git
  diff on `docs/repo-map/index.json`), mention it -- this signals structural
  movement in the codebase.
- If the project count dropped or grew, flag it.
- If symbol extraction returned 0 symbols for a file known to have public types,
  that's a parse issue -- investigate the regex or the file's formatting.

## Step 4 -- Offer related actions

Suggest to the user:

- If the map changed significantly: update `docs/INDEX.md` if new project roots
  need navigation entries.
- If a project was added: check whether it needs a layer-level CLAUDE.md.
- Run `/verify-structure` to confirm no structural checks broke as a result.

## Output

Produce a concise report (under 200 words) summarizing the run. Do not re-print
the full map.md contents -- link to it instead.
