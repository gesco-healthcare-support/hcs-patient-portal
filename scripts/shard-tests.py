#!/usr/bin/env python3
"""Split the EF Core test assembly across CI jobs, and prove each job ran exactly its share (#1032).

The EF Core integration tests take 21-23 minutes run one at a time, which made `Backend: Test`
the longest check on every pull request. `ci.yml` runs them as N parallel shards instead. This
script decides which test classes each shard runs, and afterwards checks that it ran them.

WHY THE CHECK IS THE POINT. A partition that silently drops a class is the failure to design
out: a test no shard's filter matches never runs, reports nothing, and the suite goes green
with a hole in it. So `verify` compares the classes a shard actually executed (read from its
.trx results) with the classes it was assigned, and fails on any difference in EITHER
direction. Since the partition is exhaustive by construction and every shard verifies its own
share, together they prove every class ran exactly once.

WHY CLASSES AND NOT TESTS. xUnit runs a whole class under one filter term, and the harness
builds one application per test, so a class is a natural, cheap unit. Comparing classes rather
than test counts also keeps `verify` honest if a theory ever expands at run time into more
results than discovery listed.

WHY SOME COLLECTIONS STAY WHOLE. The MultiOffice and RealAuthorization collections share named
in-memory databases (Cache=Shared) across their classes within one process, so a test in one class
can depend on rows another class left behind. Split across shards, those classes land in different
processes and behave differently: the first run lost 44 covered lines this way (#1051). So every
class in a named xUnit collection moves as one unit with its collection. The one exception is the
default "CaseEvaluation collection", which exists only to serialise and shares no state: each test
there builds its own database. Membership is read from the `[Collection(...)]` attributes in the
test sources, so a class added to a shared collection later is kept with it automatically.

Stdlib only, so the CI job needs nothing installed beyond Python.

Usage:
  shard-tests.py partition --listing LIST --sources DIR --shards N --index K --classes-out F --runsettings-out F
  shard-tests.py other-projects --slnx SOLUTION --sharded PROJECT
  shard-tests.py verify --classes F --trx-dir DIR
"""

from __future__ import annotations

import argparse
import re
import sys
import xml.etree.ElementTree as ET
from collections import Counter
from pathlib import Path
from typing import NoReturn
from xml.sax.saxutils import escape

LISTING_MARKER = "The following Tests are available:"
TRX_NS = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"

# Collections whose classes may be split across shards: they exist only to run one at a time and
# share no state. Matched against the attribute's argument text as written in the source.
SPLITTABLE_COLLECTIONS = frozenset({"CaseEvaluationTestConsts.CollectionDefinitionName"})

NAMESPACE_RE = re.compile(r"^\s*namespace\s+([A-Za-z_][\w.]*)", re.MULTILINE)
# `[Collection(X)]`, optionally other attributes, then modifiers, then `class Name`.
COLLECTION_CLASS_RE = re.compile(
    r"\[\s*(?:Xunit\.)?Collection(?:Attribute)?\s*\(\s*([^)\]]+?)\s*\)\s*\]"
    r"\s*(?:\[[^\]]*\]\s*)*"
    r"(?:(?:public|internal|sealed|abstract|partial|static)\s+)*class\s+(\w+)"
)


def die(msg: str) -> NoReturn:
    """Report a GitHub Actions error annotation and stop with exit code 1."""
    print(f"::error::{msg}", file=sys.stderr)
    raise SystemExit(1)


def parse_listing(text: str) -> list[str]:
    """Return the test names from `dotnet test --list-tests` output.

    vstest prints build and banner lines first, then the marker, then one indented name per
    test. Only indented lines after a marker are names; anything else ends the block.
    """
    names: list[str] = []
    in_block = False
    for line in text.splitlines():
        if line.strip() == LISTING_MARKER:
            in_block = True
            continue
        if in_block and line.startswith("    ") and line.strip():
            names.append(line.strip())
        elif in_block:
            in_block = False
    return names


def class_of(test_name: str) -> str:
    """Return the class part of a test's display name.

    Theory arguments are dropped first because they can contain dots
    (`Reads_an_email(email: "a.b@example.test")`), then everything before the last dot is the
    class.
    """
    head = test_name.split("(", 1)[0]
    if "." not in head:
        raise ValueError(f"test name has no class part: {test_name!r}")
    return head.rsplit(".", 1)[0]


def collection_members(sources: dict[str, str]) -> dict[str, str]:
    """Return {fully qualified class: collection} for classes that must stay with their collection.

    `sources` maps a file name to its C# text. Classes in a SPLITTABLE collection are left out,
    as are classes with no `[Collection]` at all; both may go to any shard.
    """
    members: dict[str, str] = {}
    for path, text in sources.items():
        found = COLLECTION_CLASS_RE.findall(text)
        if not found:
            continue
        namespace = NAMESPACE_RE.search(text)
        if namespace is None:
            raise ValueError(f"{path} declares a collection member but no namespace")
        for collection, name in found:
            if collection not in SPLITTABLE_COLLECTIONS:
                members[f"{namespace.group(1)}.{name}"] = collection
    return members


def read_sources(directory: Path) -> dict[str, str]:
    """Return {path: text} for every .cs file under `directory`, skipping build output."""
    if not directory.is_dir():
        raise ValueError(f"test source directory {directory} does not exist")
    return {
        str(path): path.read_text(encoding="utf-8-sig")
        for path in sorted(directory.rglob("*.cs"))
        if not {"bin", "obj"} & set(path.parts)
    }


def partition(
    counts: dict[str, int], shards: int, groups: dict[str, str] | None = None
) -> list[list[str]]:
    """Assign classes to shards, balancing test counts; deterministic for the same classes.

    Classes named in `groups` move as one unit with the other classes of the same collection;
    every other class is its own unit. Greedy: heaviest unit first (ties by name), each to the
    currently lightest shard (ties by shard number). That keeps any two shards within one unit of
    each other. Refuses a result with an empty shard, because an empty filter would make that shard
    run every test.
    """
    if shards < 1:
        raise ValueError(f"shard count must be at least 1, got {shards}")
    groups = groups or {}
    units: dict[str, list[str]] = {}
    for cls in counts:
        key = f"collection:{groups[cls]}" if cls in groups else f"class:{cls}"
        units.setdefault(key, []).append(cls)
    weight = {key: sum(counts[c] for c in members) for key, members in units.items()}
    buckets: list[list[str]] = [[] for _ in range(shards)]
    loads = [0] * shards
    for key in sorted(units, key=lambda k: (-weight[k], k)):
        lightest = min(range(shards), key=lambda i: (loads[i], i))
        buckets[lightest].extend(units[key])
        loads[lightest] += weight[key]
    if any(not bucket for bucket in buckets):
        raise ValueError(
            f"{len(units)} units cannot fill {shards} shards; an empty shard would run everything"
        )
    return [sorted(bucket) for bucket in buckets]


def filter_for(classes: list[str]) -> str:
    """Return a `dotnet test --filter` expression matching exactly these classes.

    Each term keeps the trailing dot, so `Ns.Alpha` cannot also match `Ns.AlphaBeta`.
    """
    if not classes:
        raise ValueError("refusing to build a filter for no classes; it would match every test")
    return "|".join(f"FullyQualifiedName~{cls}." for cls in sorted(classes))


def runsettings_for(classes: list[str]) -> str:
    """Return a .runsettings document selecting exactly these classes.

    A settings file rather than `--filter` on the command line: CI runs `dotnet test` inside
    `dotnet-coverage collect "<one command string>"`, and a filter of dozens of `|` terms is
    fragile to carry through that second layer of quoting. `TreatNoTestsAsError` makes a shard
    whose filter matched nothing fail instead of passing with zero tests.
    """
    return (
        '<?xml version="1.0" encoding="utf-8"?>\n'
        "<RunSettings>\n"
        "  <RunConfiguration>\n"
        f"    <TestCaseFilter>{escape(filter_for(classes))}</TestCaseFilter>\n"
        "    <TreatNoTestsAsError>true</TreatNoTestsAsError>\n"
        "  </RunConfiguration>\n"
        "</RunSettings>\n"
    )


def other_test_projects(slnx_text: str, sharded: str) -> list[str]:
    """Return every `*.Tests.csproj` in the solution except the sharded one, sorted.

    These are the fast suites (seconds, not minutes). Shard 1 runs each of them whole, so a test
    project added to the solution later is picked up without anyone editing the workflow.
    """
    root = ET.fromstring(slnx_text)
    paths = sorted(
        path.replace("\\", "/")
        for node in root.iter("Project")
        if (path := node.get("Path", "")).endswith(".Tests.csproj")
    )
    wanted = sharded.replace("\\", "/")
    if wanted not in paths:
        raise ValueError(f"the sharded project {sharded} is not in the solution")
    return [p for p in paths if p != wanted]


def executed_classes(trx_dir: Path) -> tuple[set[str], int]:
    """Return (classes, test count) recorded in every .trx file under `trx_dir`.

    Skipped tests count: they were selected by the filter, which is what `verify` checks.
    """
    files = sorted(trx_dir.rglob("*.trx")) if trx_dir.is_dir() else []
    if not files:
        raise ValueError(f"no .trx results under {trx_dir}")
    classes: set[str] = set()
    tests = 0
    for path in files:
        root = ET.parse(path).getroot()
        for method in root.iter(f"{TRX_NS}TestMethod"):
            classes.add(method.get("className", ""))
            tests += 1
    classes.discard("")
    return classes, tests


def verify(assigned: set[str], trx_dir: Path) -> list[str]:
    """Return the problems found; an empty list means the shard ran exactly its classes.

    A run that recorded no tests needs no check of its own: every assigned class then shows up
    as "assigned but not run", and `assigned` is never empty (`cmd_verify` refuses that).
    """
    try:
        ran, _ = executed_classes(trx_dir)
    except ValueError as exc:
        return [str(exc)]
    problems = []
    missing = sorted(assigned - ran)
    extra = sorted(ran - assigned)
    if missing:
        problems.append(f"{len(missing)} class(es) assigned but not run: {', '.join(missing)}")
    if extra:
        problems.append(f"{len(extra)} class(es) run but not assigned: {', '.join(extra)}")
    return problems


def cmd_partition(args: argparse.Namespace) -> int:
    """Write shard `--index`'s class list and filter; print every shard's size."""
    names = parse_listing(Path(args.listing).read_text(encoding="utf-8"))
    if not names:
        die(f"the listing {args.listing} holds no tests; refusing to shard an empty run")
    if not 1 <= args.index <= args.shards:
        die(f"shard index {args.index} is outside 1..{args.shards}")
    counts = Counter(class_of(name) for name in names)
    try:
        groups = collection_members(read_sources(Path(args.sources)))
        # A member the listing does not know means the source scan and the build disagree (a
        # renamed namespace, a parse miss). Keeping it whole would then silently do nothing.
        unknown = sorted(cls for cls in groups if cls not in counts)
        if unknown:
            raise ValueError(f"collection members not in the test listing: {', '.join(unknown)}")
        buckets = partition(dict(counts), args.shards, groups)
    except ValueError as exc:
        die(str(exc))
    for i, bucket in enumerate(buckets, start=1):
        print(f"shard {i}/{args.shards}: {len(bucket)} classes, {sum(counts[c] for c in bucket)} tests")
    for collection in sorted(set(groups.values())):
        members = [c for c in groups if groups[c] == collection]
        home = sorted({i for i, bucket in enumerate(buckets, start=1) for c in members if c in bucket})
        print(f"kept whole: {collection} ({len(members)} classes) -> shard {', '.join(map(str, home))}")
    chosen = buckets[args.index - 1]
    Path(args.classes_out).write_text("\n".join(chosen) + "\n", encoding="utf-8")
    Path(args.runsettings_out).write_text(runsettings_for(chosen), encoding="utf-8")
    print(f"listed {len(names)} tests in {len(counts)} classes; this is shard {args.index}/{args.shards}")
    return 0


def cmd_other_projects(args: argparse.Namespace) -> int:
    """Print the solution's other test projects, one per line, for shard 1 to run whole."""
    try:
        projects = other_test_projects(Path(args.slnx).read_text(encoding="utf-8"), args.sharded)
    except ValueError as exc:
        die(str(exc))
    print("\n".join(projects))
    return 0


def cmd_verify(args: argparse.Namespace) -> int:
    """Fail unless the shard's .trx results cover exactly its assigned classes."""
    assigned = {line.strip() for line in Path(args.classes).read_text(encoding="utf-8").splitlines() if line.strip()}
    if not assigned:
        die(f"{args.classes} lists no classes")
    problems = verify(assigned, Path(args.trx_dir))
    if problems:
        for problem in problems:
            print(f"::error::{problem}", file=sys.stderr)
        return 1
    print(f"verified: this shard ran exactly its {len(assigned)} assigned classes")
    return 0


def build_parser() -> argparse.ArgumentParser:
    """The two subcommands and their required arguments."""
    parser = argparse.ArgumentParser(
        description="Split the EF Core test assembly across CI jobs, and verify each job's share."
    )
    sub = parser.add_subparsers(dest="command", required=True)
    p = sub.add_parser("partition", help="choose this shard's classes and filter")
    p.add_argument("--listing", required=True)
    p.add_argument("--sources", required=True, help="the sharded project's source directory")
    p.add_argument("--shards", type=int, required=True)
    p.add_argument("--index", type=int, required=True)
    p.add_argument("--classes-out", required=True)
    p.add_argument("--runsettings-out", required=True)
    p.set_defaults(func=cmd_partition)
    o = sub.add_parser("other-projects", help="list the solution's other test projects")
    o.add_argument("--slnx", required=True)
    o.add_argument("--sharded", required=True)
    o.set_defaults(func=cmd_other_projects)
    v = sub.add_parser("verify", help="check the shard ran exactly its classes")
    v.add_argument("--classes", required=True)
    v.add_argument("--trx-dir", required=True)
    v.set_defaults(func=cmd_verify)
    return parser


def main(argv: list[str] | None = None) -> int:
    """Entry point; returns the process exit code."""
    args = build_parser().parse_args(argv)
    return args.func(args)


if __name__ == "__main__":
    sys.exit(main())
