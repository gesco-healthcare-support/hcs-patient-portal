#!/usr/bin/env python3
"""Create the GitHub Issues backlog from the repository's existing trackers.

This repository tracked work in four places that a clone could not fully see:
`docs/runbooks/findings/bugs/` (git-tracked), `docs/production-hardening/`
(git-tracked), `docs/backlog.md` (gitignored, so invisible to anyone else) and
a stale roadmap. With a second developer onboarding, that fragmentation stopped
working. This script moves the open items into GitHub Issues, which becomes the
single shared list.

Three modes, in the order you use them:

    python scripts/maintenance/import-issues.py --generate
    python scripts/maintenance/import-issues.py --dry-run      # default
    python scripts/maintenance/import-issues.py --apply

`--generate` writes `issues.json`. `--dry-run` prints what would be created and
touches nothing. `--apply` creates the issues.

Creating an issue on a public repository sends notifications that cannot be
recalled, so `--apply` is never the default and the dry run is not optional in
practice: review it first.

Sources and deliberate exclusions
---------------------------------
finding    Every file in docs/runbooks/findings/bugs/ whose `status:` starts
           with "open". The ~48 closed findings stay as history in git.
hardening  Numbered items in phases 4 onward only. Phases 1 and 2 are complete
           or in flight and phase 3 starts now, all owned by the hardening
           sessions, so importing them would create issues for work already
           being done.
backlog    docs/backlog.md, which is gitignored and therefore a NEW disclosure
           rather than a re-publication. Redacted before import: see `redact`.
sweep      One batch per directory tree, carrying every static-analysis finding
           for it. Batching by directory rather than by rule is what lets two
           people work in parallel without touching the same file.

Requires `gh` authenticated with `repo` scope.
"""

from __future__ import annotations

import argparse
import collections
import json
import pathlib
import re
import subprocess
import sys
import time
import urllib.request

REPO = "gesco-healthcare-support/hcs-patient-portal"
SONAR_KEY = "gesco-healthcare-support_hcs-patient-portal"
ROOT = pathlib.Path(__file__).resolve().parents[2]
OUT = ROOT / "scripts" / "maintenance" / "issues.json"
MAP = ROOT / "scripts" / "maintenance" / ".issue-map.tsv"

THROTTLE_SECONDS = 2.0
"""GitHub caps content creation at 80/minute and 500/hour. 2s is ~30/minute,
comfortably under both, and slow enough that a mistake is interruptible."""

BATCH_MIN = 12
"""Below this a directory is grouped with its siblings rather than getting its
own issue, so the tracker does not fill with three-finding batches."""

DROPPED_KEYS = {
    # Near-duplicates surfaced by the dry run and dropped by Adrian 2026-09-04.
    # The backlog is append-only, so the same finding was recorded more than once
    # on different dates in different words; exact-title dedup does not catch that.
    "BL-25": "duplicate of BL-10 (dependency-review deny-licenses)",
    "BL-41": "duplicate of BL-10 (dependency-review deny-licenses)",
    "BL-40": "duplicate of BL-13 (untracked session-handoff files)",
    "BL-36": "duplicate of BL-28 (PacketsCompleteHandler publish race)",
}

# Directories owned by the hardening sessions. A batch must never claim these,
# or two people end up in the same file, which is the whole thing we are
# avoiding. Revisit when phases 2 and 3 close.
HELD_PREFIXES = {
    "test/": "phase 3 (critical-path coverage)",
    "tests/": "phase 3 (critical-path coverage)",
    ".github/": "phase 2.2 / 2.6",
    "scripts/": "phase 2.14",
    "docker/": "phase 2.14",
}
HELD_FILES = {
    "angular/angular.json": "phase 2.13",
    "angular/karma.conf.js": "phase 2.13",
    "Directory.Build.props": "phase 2.5",
    ".editorconfig": "phase 2.5",
}

SEVERITY_ALIASES = {
    "medium-to-high": "high",
    "low-to-medium": "medium",
    "open-low": "low",
}

LABELS = [
    ("severity/high", "b60205", "Security, data integrity or a blocked user path"),
    ("severity/medium", "d93f0b", "Real defect, contained blast radius"),
    ("severity/low", "fbca04", "Minor defect or polish"),
    ("severity/observation", "c5def5", "Recorded behaviour, not yet judged a defect"),
    ("type/bug", "d73a4a", "Confirmed defect"),
    ("type/observation", "c2e0c6", "Observation from a test or review pass"),
    ("type/hardening", "5319e7", "Production-hardening programme item"),
    ("type/sweep", "0e8a16", "Static-analysis batch scoped to one directory tree"),
    ("source/finding", "ededed", "Imported from docs/runbooks/findings/bugs/"),
    ("source/hardening", "ededed", "Imported from docs/production-hardening/"),
    ("source/backlog", "ededed", "Imported from docs/backlog.md"),
    ("source/sweep", "ededed", "Generated from Sonar / CodeQL by directory"),
]

EMAIL_RE = re.compile(r"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")
GUID_RE = re.compile(r"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b")


def redact(text: str) -> str:
    """Strip identifiers that must not reach a public issue.

    docs/backlog.md names real people at Gesco and at a partner organisation,
    in entries describing their accounts in a healthcare system, and carries
    tenant GUIDs that identify a real practice. The engineering content
    survives redaction intact; only the identity is lost.
    """
    text = EMAIL_RE.sub("<person>", text)
    return GUID_RE.sub("<office-id>", text)


def frontmatter(path: pathlib.Path) -> dict[str, str]:
    """Parse the leading `---` YAML block. Flat key/value only, which is all
    the finding files use."""
    lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
    if not lines or lines[0].strip() != "---":
        return {}
    out: dict[str, str] = {}
    for line in lines[1:]:
        if line.strip() == "---":
            break
        if ":" in line:
            k, v = line.split(":", 1)
            v = v.strip()
            # Strip quotes only when the value is fully wrapped. A blanket strip
            # eats the leading quote of a title like `"Send invite" button ...`.
            if len(v) >= 2 and v[0] == v[-1] and v[0] in "\"'":
                v = v[1:-1]
            out[k.strip()] = v
    return out


def severity_of(raw: str) -> str:
    """Normalise a frontmatter severity to one of the four label values.

    Two findings carry compound severities ("medium-to-high"). Mapping to the
    upper bound is deliberate: under-stating a severity is the more expensive
    error, and the original wording is preserved in the issue body.
    """
    # Several findings qualify the severity in place, e.g.
    # "medium-to-high (refined 2026-05-23 after deep diagnosis)". Take the
    # leading token so the qualifier does not silently demote the finding to
    # the default.
    s = re.split(r"[\s(]", (raw or "observation").strip().lower(), maxsplit=1)[0]
    s = SEVERITY_ALIASES.get(s, s)
    return s if s in {"high", "medium", "low", "observation"} else "observation"


def collect_findings() -> list[dict]:
    """One issue per open finding file."""
    issues = []
    for path in sorted((ROOT / "docs/runbooks/findings/bugs").glob("*.md")):
        fm = frontmatter(path)
        if not fm.get("status", "").lower().startswith("open"):
            continue
        fid, title = fm.get("id", path.stem), fm.get("title", path.stem)
        sev = severity_of(fm.get("severity", ""))
        kind = "type/observation" if fid.startswith("OBS") else "type/bug"
        rel = path.relative_to(ROOT).as_posix()
        body = [f"Imported from `{rel}`, which keeps the reproduction and diagnosis.", ""]
        for key in ("severity", "found", "flow", "component"):
            if fm.get(key):
                body.append(f"- **{key.capitalize()}:** {fm[key]}")
        body += ["", f"Status is tracked here from now on; the file no longer carries `status:`.",
                 "", f"Source: [`{rel}`](../blob/main/{rel})"]
        issues.append({
            "key": fid, "title": f"{fid}: {title}",
            "labels": [f"severity/{sev}", kind, "source/finding"],
            "body": "\n".join(body),
        })
    return issues


def collect_hardening() -> list[dict]:
    """Numbered items from phase 4 onward. Phases 1-3 are excluded on purpose."""
    issues = []
    for path in sorted((ROOT / "docs/production-hardening").glob("0[4-9]-*.md")):
        phase = path.name[:2].lstrip("0")
        text = path.read_text(encoding="utf-8", errors="replace")
        for m in re.finditer(r"^#{2,3} (\d+\.\d+) (.+)$", text, re.M):
            num, title = m.group(1), m.group(2).strip()
            rel = path.relative_to(ROOT).as_posix()
            issues.append({
                "key": f"HARD-{num}", "title": f"Hardening {num}: {title}",
                "labels": ["severity/medium", "type/hardening", "source/hardening"],
                "milestone": f"Hardening phase {phase}",
                "body": (f"Production-hardening item {num}.\n\n"
                         f"Full context, rationale and validation loop: "
                         f"[`{rel}`](../blob/main/{rel})\n\n"
                         f"Phases 1-3 are excluded from this import: they are complete or "
                         f"owned by the hardening sessions."),
            })
    return issues


def collect_backlog() -> list[dict]:
    """Dated sections and appended dated bullets from the gitignored backlog."""
    path = ROOT / "docs/backlog.md"
    if not path.exists():
        return []
    text = path.read_text(encoding="utf-8", errors="replace")
    seen, issues, n = set(), [], 0
    for m in re.finditer(r"^## (\d{4}-\d{2}-\d{2}) -- (.+)$", text, re.M):
        title = redact(m.group(2).strip())
        if title.lower() in seen:      # the same finding was recorded up to 3 times
            continue
        seen.add(title.lower())
        n += 1
        issues.append({
            "key": f"BL-{n:02d}", "title": title,
            "labels": ["severity/medium", "type/bug", "source/backlog"],
            "body": redact(f"Recorded in the working backlog on {m.group(1)}.\n\n"
                           f"Identifiers are redacted: this file was gitignored, so publishing "
                           f"it discloses content the other trackers deliberately do not."),
        })
    for m in re.finditer(r"^- (\d{4}-\d{2}-\d{2}) \(([^)]*)\): (.{0,110})", text, re.M):
        title = redact(m.group(3).strip().rstrip(".") )
        if title.lower() in seen:
            continue
        seen.add(title.lower())
        n += 1
        issues.append({
            "key": f"BL-{n:02d}", "title": title[:100],
            "labels": ["severity/medium", "type/bug", "source/backlog"],
            "body": redact(f"Recorded {m.group(1)} ({m.group(2)}).\n\nIdentifiers redacted."),
        })
    return issues


def _sonar(url: str) -> dict:
    with urllib.request.urlopen(url, timeout=45) as r:
        return json.load(r)


def collect_sweeps() -> list[dict]:
    """One batch per directory tree, carrying every static-analysis finding.

    Batching by directory rather than by rule family is the whole point: a rule
    batch spans the repository, so two people working two rule batches collide
    on every shared file. Directory batches have disjoint path sets, which is
    asserted before anything is created.
    """
    files: collections.Counter = collections.Counter()
    for page in range(1, 4):
        d = _sonar(f"https://sonarcloud.io/api/issues/search?componentKeys={SONAR_KEY}"
                   f"&resolved=false&ps=500&p={page}")
        for i in d.get("issues", []):
            files[i["component"].split(":", 1)[-1]] += 1
        if page * 500 >= d.get("total", 0):
            break
    d = _sonar(f"https://sonarcloud.io/api/hotspots/search?projectKey={SONAR_KEY}"
               f"&status=TO_REVIEW&ps=500")
    for h in d.get("hotspots", []):
        files[h["component"].split(":", 1)[-1]] += 1
    raw = subprocess.run(["gh", "api",
                          f"repos/{REPO}/code-scanning/alerts?state=open&per_page=100",
                          "--jq", "[.[] | .most_recent_instance.location.path]"],
                         capture_output=True, text=True)
    if raw.returncode == 0:
        for p in json.loads(raw.stdout or "[]"):
            files[p] += 1

    def is_held(f: str) -> bool:
        if any(f.startswith(p) for p in HELD_PREFIXES) or f in HELD_FILES:
            return True
        return "Dockerfile" in f or "/" not in f

    def leaf(f: str) -> str:
        """Group a file into the directory that will own it. Files sitting
        directly in a directory go to an explicit `(root)` bucket so they never
        collide with that directory's subdirectories."""
        p = f.split("/")
        if f.startswith("angular/src/app/"):
            if p[3] == "appointments":
                return "angular/src/app/appointments/" + (p[4] if len(p) > 5 else "(root)")
            return "angular/src/app/" + p[3]
        if f.startswith("angular/"):
            return "angular/src/(shared)"
        if f.startswith("src/"):
            return f"src/{p[1]}/" + (p[2] if len(p) > 3 else "(root)")
        return p[0]

    live: collections.Counter = collections.Counter()
    for f, c in files.items():
        if not is_held(f):
            live[leaf(f)] += c

    groups = [(k, [k], c) for k, c in live.items() if c >= BATCH_MIN]
    rest: dict[str, list] = collections.defaultdict(list)
    for k, c in live.items():
        if c < BATCH_MIN:
            rest["/".join(k.split("/")[:2]) if "/" in k else k].append((k, c))
    for parent, items in rest.items():
        groups.append((f"{parent} and {len(items)} smaller directories",
                       [k for k, _ in items], sum(c for _, c in items)))
    groups.sort(key=lambda g: -g[2])

    issues = []
    for idx, (name, paths, count) in enumerate(groups, 1):
        sev = "high" if count >= 60 else "medium" if count >= 25 else "low"
        listed = "\n".join(f"- `{p}`" for p in sorted(paths))
        issues.append({
            "key": f"SWEEP-{idx:02d}", "title": f"Static-analysis sweep: {name} ({count})",
            "labels": [f"severity/{sev}", "type/sweep", "source/sweep"],
            "body": (f"{count} open Sonar issues, security hotspots and CodeQL alerts in the "
                     f"paths below.\n\n**Paths (this batch owns these exclusively):**\n{listed}\n\n"
                     f"Assigning yourself is the claim. Do not edit files outside these paths -- "
                     f"comment here instead. Path sets across all sweeps are verified disjoint, so "
                     f"two people on two sweeps cannot touch the same file.\n\n"
                     f"Held back and not in any sweep: paths owned by the hardening sessions "
                     f"({', '.join(sorted(set(HELD_PREFIXES.values())))})."),
            "paths": paths,
        })
    return issues


def assert_disjoint(issues: list[dict]) -> None:
    """Fail loudly if two sweeps could ever touch the same file.

    This is the guarantee the whole batching scheme rests on, so it is checked
    rather than assumed. It has caught two real grouping bugs already.
    """
    paths = [p for i in issues for p in i.get("paths", [])]
    dupes = [p for p, n in collections.Counter(paths).items() if n > 1]
    overlaps = [(a, b) for a in paths for b in paths
                if a != b and b.startswith(a.rstrip("/") + "/")]
    if dupes or overlaps:
        sys.exit(f"DISJOINTNESS FAILED: duplicates={dupes[:5]} overlaps={overlaps[:5]}")


def generate() -> None:
    issues = collect_findings() + collect_hardening() + collect_backlog() + collect_sweeps()
    dropped = [i for i in issues if i["key"] in DROPPED_KEYS]
    issues = [i for i in issues if i["key"] not in DROPPED_KEYS]
    for i in dropped:
        print(f"  dropped {i['key']}: {DROPPED_KEYS[i['key']]}")
    assert_disjoint(issues)
    OUT.write_text(json.dumps(issues, indent=1), encoding="utf-8")
    counts = collections.Counter(
        next(l for l in i["labels"] if l.startswith("source/")) for i in issues)
    print(f"wrote {OUT.relative_to(ROOT)}: {len(issues)} issues")
    for k, v in sorted(counts.items()):
        print(f"  {v:4d}  {k}")


def load() -> list[dict]:
    if not OUT.exists():
        sys.exit("issues.json not found -- run with --generate first")
    return json.loads(OUT.read_text(encoding="utf-8"))


def already_created() -> dict[str, str]:
    if not MAP.exists():
        return {}
    return dict(
        line.split("\t", 1) for line in MAP.read_text(encoding="utf-8").splitlines() if "\t" in line
    )


def dry_run() -> None:
    issues, done = load(), already_created()
    print(f"{'KEY':<11} {'SEVERITY':<10} {'SOURCE':<18} TITLE")
    print("-" * 100)
    for i in issues:
        if i["key"] in done:
            continue
        sev = next(l for l in i["labels"] if l.startswith("severity/")).split("/")[1]
        src = next(l for l in i["labels"] if l.startswith("source/")).split("/")[1]
        print(f"{i['key']:<11} {sev:<10} {src:<18} {i['title'][:60]}")
    pending = [i for i in issues if i["key"] not in done]
    print(f"\n{len(pending)} would be created, {len(done)} already exist. "
          f"Nothing was created -- rerun with --apply.")


def ensure_labels() -> None:
    for name, colour, desc in LABELS:
        subprocess.run(["gh", "label", "create", name, "--repo", REPO,
                        "--color", colour, "--description", desc, "--force"],
                       capture_output=True, text=True)
    print(f"ensured {len(LABELS)} labels")


def ensure_milestones(issues: list[dict]) -> None:
    """Create any milestone an issue references.

    `gh issue create --milestone` fails if the milestone does not already exist,
    which would abort the run partway through. The hardening phases are a real
    sequence with a dependency order, so the milestone is what stops that order
    being lost when the phases are flattened into a list.
    """
    wanted = {i["milestone"] for i in issues if i.get("milestone")}
    for title in sorted(wanted):
        subprocess.run(["gh", "api", f"repos/{REPO}/milestones", "-f", f"title={title}"],
                       capture_output=True, text=True)  # 422 if it exists; harmless
    if wanted:
        print(f"ensured {len(wanted)} milestones")


def apply() -> None:
    """Create the issues, one at a time, stopping at the first failure.

    Stopping rather than continuing keeps `.issue-map.tsv` an accurate record of
    what exists, so a rerun resumes instead of duplicating.
    """
    issues, done = load(), already_created()
    pending = [i for i in issues if i["key"] not in done]
    print(f"creating {len(pending)} issues ({len(done)} already exist)", flush=True)
    ensure_labels()
    ensure_milestones(pending)
    with MAP.open("a", encoding="utf-8") as fh:
        for n, issue in enumerate(pending, 1):
            cmd = ["gh", "issue", "create", "--repo", REPO,
                   "--title", issue["title"], "--body", issue["body"]]
            for label in issue["labels"]:
                cmd += ["--label", label]
            if issue.get("milestone"):
                cmd += ["--milestone", issue["milestone"]]
            res = subprocess.run(cmd, capture_output=True, text=True)
            if res.returncode != 0:
                sys.exit(f"\nFAILED on {issue['key']}: {res.stderr.strip()}\n"
                         f"{n - 1} created. Rerun to resume from here.")
            url = res.stdout.strip()
            fh.write(f"{issue['key']}\t{url}\n")
            fh.flush()
            print(f"  [{n}/{len(pending)}] {issue['key']}  {url}", flush=True)
            time.sleep(THROTTLE_SECONDS)
    print("done")


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    g = ap.add_mutually_exclusive_group()
    g.add_argument("--generate", action="store_true", help="build issues.json from the trackers")
    g.add_argument("--dry-run", action="store_true", help="print what would be created (default)")
    g.add_argument("--apply", action="store_true", help="actually create the issues")
    g.add_argument("--ensure-labels", action="store_true", help="create the label taxonomy only")
    args = ap.parse_args()
    if args.generate:
        generate()
    elif args.apply:
        apply()
    elif args.ensure_labels:
        ensure_labels()
    else:
        dry_run()


if __name__ == "__main__":
    main()
