#!/usr/bin/env python3
"""Verify documentation structural integrity.

Runs a set of structural checks against the documentation system:

  1. File existence: required root-level docs exist
  2. CLAUDE.md coverage: every feature + every configured layer has CLAUDE.md
  3. Security docs: docs/security/ + docs/decisions/ exist and are non-empty
  4. Repo map freshness: docs/repo-map/index.json exists (age warning at 30 days)
  5. Consistency: feature count matches between module-map.md and Feature Index

Output:
  - Prints PASS / WARN / FAIL lines per check
  - Exits 0 if no FAILs, 1 if any FAIL

Link validation is delegated to check-links.py -- this script does not scan
markdown link targets. Run both for full coverage.
"""

from __future__ import annotations

import json
import re
import sys
from datetime import datetime, timezone
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]

REQUIRED_FILES = [
    "CLAUDE.md",
    "docs/INDEX.md",
    "docs/executive-summary.md",
    "docs/verification/BASELINE.md",
    "docs/repo-map/index.json",
    "docs/repo-map/map.md",
    "docs/repo-map/README.md",
    "docs/security/THREAT-MODEL.md",
    "docs/security/DATA-FLOWS.md",
    "docs/security/AUTHORIZATION.md",
    "docs/security/SECRETS-MANAGEMENT.md",
    "docs/security/HIPAA-COMPLIANCE.md",
    "docs/decisions/README.md",
    "docs/runbooks/LOCAL-DEV.md",
    "docs/runbooks/DOCKER-DEV.md",
    "docs/runbooks/INCIDENT-RESPONSE.md",
    ".claude/SYSTEM-STATUS.md",
    ".claude/discovery/module-map.md",
]

# Directories where every immediate child subdirectory must have a CLAUDE.md.
DOMAIN_ROOT = REPO_ROOT / "src" / "HealthcareSupport.CaseEvaluation.Domain"
DOMAIN_EXCLUDE = {
    "bin", "obj", "Data", "Identity", "OpenIddict", "Saas", "Settings",
    "Shared",  # non-feature utility folder
    "Properties",
}

# Layer-level CLAUDE.md files that should exist (see Phase 2 of the plan).
LAYER_CLAUDE_FILES = [
    "src/HealthcareSupport.CaseEvaluation.Application/CLAUDE.md",
    "src/HealthcareSupport.CaseEvaluation.Application.Contracts/CLAUDE.md",
    "src/HealthcareSupport.CaseEvaluation.Domain.Shared/CLAUDE.md",
    "src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/CLAUDE.md",
    "src/HealthcareSupport.CaseEvaluation.HttpApi/CLAUDE.md",
    "angular/src/app/CLAUDE.md",
    "test/CLAUDE.md",
]

# -----------------------------------------------------------------------------
# Check reporting
# -----------------------------------------------------------------------------

class Report:
    def __init__(self) -> None:
        self.passes: list[str] = []
        self.warns: list[str] = []
        self.fails: list[str] = []

    def ok(self, msg: str) -> None:
        self.passes.append(msg)
        print(f"PASS {msg}")

    def warn(self, msg: str) -> None:
        self.warns.append(msg)
        print(f"WARN {msg}")

    def fail(self, msg: str) -> None:
        self.fails.append(msg)
        print(f"FAIL {msg}")

    def summary(self) -> None:
        print()
        print(f"Summary: {len(self.passes)} PASS, "
              f"{len(self.warns)} WARN, {len(self.fails)} FAIL")

    def exit_code(self) -> int:
        return 1 if self.fails else 0


# -----------------------------------------------------------------------------
# Individual checks
# -----------------------------------------------------------------------------

def check_required_files(r: Report) -> None:
    for rel in REQUIRED_FILES:
        path = REPO_ROOT / rel
        if path.is_file() and path.stat().st_size > 0:
            r.ok(f"required file exists: {rel}")
        else:
            r.fail(f"required file missing or empty: {rel}")


def check_feature_claude_coverage(r: Report) -> int:
    if not DOMAIN_ROOT.is_dir():
        r.fail(f"Domain/ root not found at {DOMAIN_ROOT}")
        return 0
    covered = 0
    for child in sorted(DOMAIN_ROOT.iterdir()):
        if not child.is_dir() or child.name in DOMAIN_EXCLUDE:
            continue
        claude = child / "CLAUDE.md"
        if claude.is_file():
            covered += 1
            r.ok(f"feature CLAUDE.md: {child.name}")
        else:
            r.fail(f"feature missing CLAUDE.md: Domain/{child.name}")
    return covered


def check_layer_claude_files(r: Report) -> None:
    for rel in LAYER_CLAUDE_FILES:
        path = REPO_ROOT / rel
        if path.is_file():
            r.ok(f"layer CLAUDE.md exists: {rel}")
        else:
            r.fail(f"layer CLAUDE.md missing: {rel}")


def check_repo_map_freshness(r: Report) -> None:
    index = REPO_ROOT / "docs" / "repo-map" / "index.json"
    if not index.is_file():
        r.fail("docs/repo-map/index.json missing")
        return
    try:
        data = json.loads(index.read_text(encoding="utf-8"))
        generated_at = datetime.fromisoformat(data["generated_at"])
    except (json.JSONDecodeError, KeyError, ValueError) as exc:
        r.fail(f"docs/repo-map/index.json unreadable: {exc}")
        return
    age_days = (datetime.now(timezone.utc) - generated_at).days
    if age_days > 30:
        r.warn(f"docs/repo-map/index.json is {age_days} days old -- regenerate")
    else:
        r.ok(f"docs/repo-map/index.json fresh ({age_days} days old)")


def check_decisions_not_empty(r: Report) -> None:
    decisions = REPO_ROOT / "docs" / "decisions"
    if not decisions.is_dir():
        r.fail("docs/decisions/ missing")
        return
    adrs = [
        p for p in decisions.glob("*.md")
        if p.name.lower() != "readme.md"
    ]
    if not adrs:
        r.warn("docs/decisions/ contains no ADR files (only README)")
    else:
        r.ok(f"docs/decisions/ contains {len(adrs)} ADR files")


def check_security_dir_populated(r: Report) -> None:
    security = REPO_ROOT / "docs" / "security"
    if not security.is_dir():
        r.fail("docs/security/ missing")
        return
    md = list(security.glob("*.md"))
    if len(md) < 5:
        r.warn(f"docs/security/ has {len(md)} files (expected 5+)")
    else:
        r.ok(f"docs/security/ contains {len(md)} markdown files")


def count_feature_rows_in_claude_md(text: str) -> int:
    """Count rows of the Feature Index table in root CLAUDE.md."""
    start = text.find("### Feature CLAUDE.md Index")
    if start == -1:
        return 0
    snippet = text[start:]
    end_marker = snippet.find("\n### ")
    if end_marker != -1:
        snippet = snippet[:end_marker]
    rows = 0
    in_table = False
    for line in snippet.splitlines():
        stripped = line.strip()
        if stripped.startswith("|"):
            if "---" in stripped:
                in_table = True
                continue
            if in_table:
                rows += 1
        elif in_table and stripped == "":
            break
    return rows


def check_feature_count_consistency(r: Report, covered_count: int) -> None:
    root_claude = REPO_ROOT / "CLAUDE.md"
    if not root_claude.is_file():
        r.fail("root CLAUDE.md missing; skipping feature-count consistency")
        return
    rows = count_feature_rows_in_claude_md(root_claude.read_text(encoding="utf-8"))
    if rows == 0:
        r.warn("root CLAUDE.md Feature Index table not found or empty")
        return
    if rows == covered_count:
        r.ok(f"feature count consistent: {rows} in CLAUDE.md / {covered_count} on disk")
    else:
        r.warn(
            f"feature count mismatch: CLAUDE.md table has {rows} rows, "
            f"Domain/ has {covered_count} features"
        )


def check_proxy_not_tracked(r: Report) -> None:
    """Best-effort check that nobody has manually edited proxy/."""
    proxy_readme = REPO_ROOT / "angular" / "src" / "app" / "proxy" / "README.md"
    if proxy_readme.is_file():
        r.ok("angular/src/app/proxy/README.md present")
    else:
        r.warn("angular/src/app/proxy/README.md missing -- regenerate proxy")


# -----------------------------------------------------------------------------
# Main
# -----------------------------------------------------------------------------

def main() -> int:
    r = Report()

    print("== File existence ==")
    check_required_files(r)

    print("\n== Feature CLAUDE.md coverage ==")
    covered = check_feature_claude_coverage(r)

    print("\n== Layer CLAUDE.md coverage ==")
    check_layer_claude_files(r)

    print("\n== Repo map freshness ==")
    check_repo_map_freshness(r)

    print("\n== ADR and security coverage ==")
    check_decisions_not_empty(r)
    check_security_dir_populated(r)

    print("\n== Consistency ==")
    check_feature_count_consistency(r, covered)
    check_proxy_not_tracked(r)

    r.summary()
    return r.exit_code()


if __name__ == "__main__":
    sys.exit(main())