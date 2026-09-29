#!/usr/bin/env python3
"""Rewrite the GENERATED regions in documentation from committed snapshots.

WHY THIS EXISTS
---------------
The repository already pins snapshots against the CODE: a generator reflects some
property into a deterministic table, the table is committed, and a snapshot test fails
when the two diverge. What nothing pinned was the DOCUMENT against the snapshot.

That gap is not theoretical. Before the 2026-09 documentation pass,
docs/business-domain/APPOINTMENT-LIFECYCLE.md taught eleven appointment transitions the
state machine does not permit, missed three it does, and presented an unreachable chain
as the happy path -- while a correct description of the machine sat in the code the whole
time. A hand-kept table cannot notice that the code moved.

So: a document declares a region, this script fills it from the snapshot, and CI fails if
filling it changes the file. Same assertion as the packet golden job -- regenerate and
diff.

    <!-- GENERATED: appointment-transitions BEGIN - do not edit by hand -->
    ...written by this script...
    <!-- GENERATED: appointment-transitions END -->

DISCOVERY, NOT REGISTRATION
---------------------------
Snapshots are found by walking test/** for *.approved.txt; the fact's name is the file
stem. Nothing here lists the facts, so a new generator's snapshot becomes available the
moment it lands, with no edit to this script's discovery and none to the generator's own
pull request.

CONVERTERS ARE THE EXCEPTION, AND THAT IS DELIBERATE
----------------------------------------------------
Adrian chose (2026-09-29) that regions render as readable tables rather than embedding the
raw snapshot. The snapshot formats are per-fact and share no shape -- the authorization
surface is one line kind (`<member> -> class=<x> method=<y>`), the transition surface has
three (transition, state, trigger) -- so a fact-agnostic renderer does not exist. The
per-fact knowledge has to live somewhere, and it lives HERE rather than in each generator,
because the constraint was that facts plug in without edits to their own pull requests.

THE RISK THAT CHOICE CARRIES, AND HOW IT IS ANSWERED
-----------------------------------------------------
A renderer is a transform, and a wrong transform does not look wrong: it produces a
plausible table. That is the same failure `AuthorizationSurface` avoids by recording class
and method attributes verbatim rather than modelling how they combine -- an earlier draft
that modelled the rule rendered an IDENTICAL line when the class attribute was deleted,
silent through exactly the edit it existed to catch.

Since the transform is now required, the property is enforced by test instead:

  - LOSSLESS ROUND TRIP. Each converter's table parses back to rows equal to the
    snapshot's lines: same count, same order, each exactly once.
  - LITERAL HEADER. A round trip proves the DATA survived and says nothing about whether
    the column labels are honest -- a converter can round-trip perfectly and still label a
    column "Permission" when it holds something else. So each header is asserted against a
    spelled-out literal.

Usage:  python embed-generated-regions.py [repo_root] [--check]
        --check reports what would change and writes nothing.
Exit:   0 clean, 1 findings, 2 usage error.
"""

import os
import re
import sys

SKIP_DIR_PARTS = {"node_modules", "bin", "obj", "dist", ".git", ".vs", ".angular"}

BEGIN = re.compile(
    r"^(?P<indent>[ \t]*)<!--\s*GENERATED:\s*(?P<name>[A-Za-z0-9._-]+)\s+BEGIN\b[^>]*-->\s*$"
)
END = re.compile(r"^[ \t]*<!--\s*GENERATED:\s*(?P<name>[A-Za-z0-9._-]+)\s+END\s*-->\s*$")


def read_bytes(path):
    with open(path, "rb") as handle:
        return handle.read()


def detect_newline(raw):
    """Return the file's dominant newline so a rewrite never changes line endings.

    Learned the hard way on 2026-09-28: Python text mode silently converted four
    documents to CRLF on write. A script that rewrites every document on every run and
    flips their line endings would leave a permanently dirty tree, and CI would fail on
    a change nobody made.
    """
    return "\r\n" if b"\r\n" in raw else "\n"


def discover_snapshots(root):
    """Every test/**/<name>.approved.txt, keyed by file stem.

    Duplicate stems are an error rather than a silent last-one-wins: which snapshot a
    document got would otherwise depend on walk order, which nobody decided.
    """
    found = {}
    duplicates = {}
    test_root = os.path.join(root, "test")
    for dirpath, dirnames, filenames in os.walk(test_root):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIR_PARTS]
        for name in sorted(filenames):
            if not name.endswith(".approved.txt"):
                continue
            stem = name[: -len(".approved.txt")]
            full = os.path.join(dirpath, name)
            if stem in found:
                duplicates.setdefault(stem, [found[stem]]).append(full)
            else:
                found[stem] = full
    return found, duplicates


def snapshot_rows(path):
    """The snapshot's lines, stripped of trailing blank lines. The unit of the round trip."""
    text = read_bytes(path).decode("utf-8", errors="replace")
    rows = text.replace("\r\n", "\n").split("\n")
    while rows and not rows[-1].strip():
        rows.pop()
    return rows


# --- converters -------------------------------------------------------------------
#
# One per fact, keyed by the snapshot's stem. Each returns (header, [[cell, ...], ...]).
# Each MUST have a matching parse_* that reverses it exactly, so the round-trip test can
# prove nothing was dropped, merged or reordered.


def escape_cell(value):
    """A pipe inside a cell would silently create a column. Escape it rather than split."""
    return value.replace("|", "\\|")


def unescape_cell(value):
    return value.replace("\\|", "|")


def render_authorization_surface(rows):
    header = ["Member", "Class-level", "Method-level"]
    table = []
    for row in rows:
        member, _, rest = row.partition(" -> ")
        class_part, _, method_part = rest.partition(" method=")
        table.append([member, class_part.replace("class=", "", 1), method_part])
    return header, table


def parse_authorization_surface(table):
    return ["%s -> class=%s method=%s" % (a, b, c) for a, b, c in table]


CONVERTERS = {
    "authorization-surface": (render_authorization_surface, parse_authorization_surface),
}


def to_markdown(header, table):
    lines = ["| " + " | ".join(header) + " |",
             "| " + " | ".join("---" for _ in header) + " |"]
    for row in table:
        lines.append("| " + " | ".join(escape_cell(c) for c in row) + " |")
    return lines


def from_markdown(lines):
    """Reverse to_markdown: the header, then the cells, ignoring the separator row."""
    body = [l for l in lines if l.strip().startswith("|")]
    if len(body) < 2:
        return [], []
    header = [c.strip() for c in body[0].strip().strip("|").split("|")]
    table = []
    for line in body[2:]:
        cells = re.split(r"(?<!\\)\|", line.strip().strip("|"))
        table.append([unescape_cell(c.strip()) for c in cells])
    return header, table


def is_ignored(path, root):
    """A gitignored file is a working artefact, not part of the documentation corpus.

    Found by running this script before trusting it: docs/plans/ is gitignored and holds
    the plan files for this very feature, which quote the marker syntax as EXAMPLES. Left
    unfiltered, the script reported two regions naming facts that do not exist -- findings
    about its own design notes. A checker that reports on files nobody ships is a checker
    people learn to skip.
    """
    import subprocess

    try:
        result = subprocess.run(
            ["git", "check-ignore", "-q", path],
            cwd=root, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
        )
        return result.returncode == 0
    except (OSError, subprocess.SubprocessError):
        return False


def find_documents(root):
    docs = []
    for rel in ("docs",):
        for dirpath, dirnames, filenames in os.walk(os.path.join(root, rel)):
            dirnames[:] = [d for d in dirnames if d not in SKIP_DIR_PARTS]
            docs.extend(
                os.path.join(dirpath, f) for f in sorted(filenames) if f.endswith(".md")
            )
    docs.extend(
        os.path.join(root, f)
        for f in sorted(os.listdir(root))
        if f.endswith(".md") and os.path.isfile(os.path.join(root, f))
    )
    return sorted(docs)


def rewrite(text, newline, snapshots, problems, rel):
    """Replace each region's body. Returns the new text; appends any problems."""
    lines = text.split(newline)
    out = []
    index = 0
    while index < len(lines):
        line = lines[index]
        match = BEGIN.match(line)
        if not match:
            out.append(line)
            index += 1
            continue

        name = match.group("name")
        close = None
        for lookahead in range(index + 1, len(lines)):
            if BEGIN.match(lines[lookahead]):
                break  # nested BEGIN: unterminated region
            if END.match(lines[lookahead]):
                close = lookahead
                break
        if close is None:
            problems.append((rel, "UNTERMINATED", "region `%s` has no END marker" % name))
            out.append(line)
            index += 1
            continue
        close_match = END.match(lines[close])
        if close_match is not None and close_match.group("name") != name:
            problems.append(
                (rel, "MISMATCHED", "region `%s` closes with a different name" % name)
            )

        if name not in snapshots:
            problems.append(
                (rel, "NO SNAPSHOT", "region `%s` names a fact with no *.approved.txt" % name)
            )
        elif name not in CONVERTERS:
            problems.append(
                (rel, "NO CONVERTER", "region `%s` has a snapshot but no converter" % name)
            )
        else:
            render, _parse = CONVERTERS[name]
            header, table = render(snapshot_rows(snapshots[name]))
            out.append(line)
            out.extend(to_markdown(header, table))
            out.append(lines[close])
            index = close + 1
            continue

        out.extend(lines[index : close + 1])
        index = close + 1
    return newline.join(out)


def main(argv):
    args = [a for a in argv[1:] if not a.startswith("--")]
    check_only = "--check" in argv
    root = os.path.abspath(args[0]) if args else os.getcwd()
    if not os.path.isdir(root):
        print("FAIL: not a directory: %s" % root)
        return 2

    snapshots, duplicates = discover_snapshots(root)
    problems = []
    for stem, paths in sorted(duplicates.items()):
        problems.append(("-", "DUPLICATE", "`%s` is claimed by: %s" % (stem, ", ".join(paths))))

    changed = []
    for path in find_documents(root):
        raw = read_bytes(path)
        newline = detect_newline(raw)
        text = raw.decode("utf-8", errors="replace")
        if "GENERATED:" not in text:
            continue
        if is_ignored(path, root):
            continue  # a working artefact, not shipped documentation; see is_ignored
        rel = os.path.relpath(path, root).replace("\\", "/")
        updated = rewrite(text, newline, snapshots, problems, rel)
        if updated != text:
            changed.append(rel)
            if not check_only:
                with open(path, "wb") as handle:
                    handle.write(updated.encode("utf-8"))

    print("Scanned %d snapshot(s); %d converter(s) registered."
          % (len(snapshots), len(CONVERTERS)))
    if changed:
        print("")
        print("%d document(s) out of date with their snapshot:" % len(changed))
        for rel in changed:
            print("  %s" % rel)
    if problems:
        print("")
        print("%d problem(s):" % len(problems))
        for rel, kind, detail in problems:
            print("  %-14s %s  %s" % (kind, rel, detail))
    if not changed and not problems:
        print("OK: every GENERATED region matches its snapshot.")
        return 0
    return 1


if __name__ == "__main__":
    sys.exit(main(sys.argv))
