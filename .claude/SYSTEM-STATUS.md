# System Status -- Patient Portal
Last validated: 2026-04-13
Status: DOCUMENTATION COMPLETE (augmented)

## Documentation Coverage
| Metric | Value |
|--------|-------|
| Features discovered | 17 (15 entity + 2 cross-cutting) |
| Features with CLAUDE.md | 16 (94%) |
| Layer-level CLAUDE.md files | 7 (Application, Application.Contracts, Domain.Shared, EntityFrameworkCore, HttpApi, angular/src/app, test) |
| Features with docs/features/ | 15 (88%) |
| Developer docs (architecture, API, onboarding, etc.) | 47 files |
| Security docs | 5 (docs/security/) |
| Architecture Decision Records | 5 (docs/decisions/) |
| Runbooks | 3 (docs/runbooks/) |
| Verification artifacts | 2 (docs/verification/BASELINE.md, executive-summary.md) |
| Repo map artifacts | 3 (docs/repo-map/) |
| Total documentation files | ~95 |
| Mermaid diagrams | 31+ |
| Root CLAUDE.md | 189 lines |
| Consistency health score | 94% |
| Structural check pass rate | 100% (45/45 PASS, verify_structure.py) |

## Skills Installed
| Skill | Type | Location | Status |
|-------|------|----------|--------|
| generate-feature-doc | project | .claude/skills/ | validated |
| sync-feature-to-docs | project | .claude/skills/ | validated (ADR cross-linking added) |
| verify-docs | project | .claude/skills/ | validated (expanded scan scope) |
| update-docs | project | .claude/skills/ | validated |
| plan-feature | project | .claude/skills/ | validated |
| review-pr | project | .claude/skills/ | validated (security doc check added) |
| develop | project | .claude/skills/ | validated |
| design-tests | project | .claude/skills/ | validated |
| run-tests | project | .claude/skills/ | validated |
| scheduled-doc-check | project | .claude/skills/ | validated (repo-map + augmented dirs) |
| generate-repo-map | project | .claude/skills/ | new (2026-04-13) |
| verify-structure | project | .claude/skills/ | new (2026-04-13) |
| sync-to-vault | global | ~/.claude/skills/ | validated |

## Scripts Installed (new)
| Script | Purpose |
|--------|---------|
| .claude/scripts/build-repo-map.py | Generate docs/repo-map/ artifacts (Python 3 stdlib) |
| .claude/scripts/verify_structure.py | Structural checks for CLAUDE.md coverage, required files, consistency |
| .claude/scripts/check-links.py | Validate all relative Markdown links under docs/ and .claude/ |

## Hooks
| Hook | Event | Status |
|------|-------|--------|
| phi-scanner.sh | PreToolUse | active |
| doc-staleness | PostToolUse | active |

## CI/CD Readiness
| Artifact | Status | Notes |
|----------|--------|-------|
| .github/workflows/ci.yml | ready | Now includes `docs-structure` job (verify_structure.py + check-links.py) |
| .github/workflows/doc-check.yml | ready | uncomment when ANTHROPIC_API_KEY configured |
| .github/PULL_REQUEST_TEMPLATE.md | ready | includes doc + HIPAA checklists |
| .github/CODEOWNERS | scaffolded | uncomment and assign when team exists |
| .gitignore | configured | .claude/context/, .env*, secrets excluded |

## Vault Connection
| Field | Value |
|-------|-------|
| Vault note | 02-Projects/PatientPortal/Overview.md |
| Last sync | 2026-04-08 |
| Sync method | /sync-to-vault via MCP (obsidian_update_note) |

## Documentation Augmentation (2026-04-13)

Added two-layer AI-first documentation extensions on top of existing 94%-healthy system:

- `docs/repo-map/` -- machine-readable structural index (regeneratable)
- `docs/security/` -- HIPAA-critical: threat model, PHI data flows, authorization matrix, secrets management, compliance inventory
- `docs/decisions/` -- 5 formal ADRs capturing prior decisions (Mapperly, manual controllers, dual DbContext, doctor-per-tenant, no ng serve)
- `docs/runbooks/` -- local dev troubleshooting, Docker dev runbook, incident response
- `docs/executive-summary.md` -- manager-friendly overview
- `docs/verification/BASELINE.md` -- formal verification evidence record
- 7 layer-level CLAUDE.md files at mid-layer boundaries
- 3 Python verification scripts + 2 new skills + 4 skill augmentations

See `C:\Users\RajeevG\.claude\plans\validated-orbiting-oasis.md` for the full plan.

## Maintenance
- Run `/update-docs [feature]` after code changes
- Run `/scheduled-doc-check` weekly (or configure as /loop or cloud task)
- Run `/verify-docs all` before major releases
- Run `/verify-structure` after restructures or before releases
- Run `/generate-repo-map` after adding / removing projects
- Run `/sync-to-vault` after documentation updates
