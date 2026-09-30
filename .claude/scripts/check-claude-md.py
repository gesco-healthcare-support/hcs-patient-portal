#!/usr/bin/env python3
"""Prove that every per-folder CLAUDE.md still describes the code beside it.

WHY THIS EXISTS
---------------
These files are read FIRST and trusted MOST by AI agents, which makes their staleness
more dangerous than ordinary documentation, not less. An agent reads code correctly;
what it cannot do is notice that a file is lying to it.

That is not hypothetical here. `AppointmentsAppService.cs:396` once justified a scoping
decision with "Patient is not IMultiTenant per CLAUDE.md" -- reasoning from a stale
document rather than from the class, which DOES implement IMultiTenant. The documentation
pass of 2026-09-28 found four of six audited CLAUDE.md files stale.

WHAT THIS CHECKS, AND WHAT IT DELIBERATELY DOES NOT
---------------------------------------------------
Every stale claim found in that audit was one of three mechanically checkable kinds:

  1. ORPHAN     -- a CLAUDE.md describing a folder that has no code in it at all.
  2. DANGLING   -- it names a file or folder that does not exist.
  3. OMISSION   -- it fails to mention a public type that does exist beside it.

There is a FOURTH kind this cannot catch: a claim about BEHAVIOUR that was true when
written and is no longer (one audited file still described an en.json duplicate-key bug
that had since been fixed). No checker can detect that without re-deriving the behaviour.

The answer to that fourth kind is not a cleverer checker. It is rule 4 below, and the
convention it enforces: a CLAUDE.md carries WHY and invariants, never a restatement of
what the code plainly says. A claim that is never written cannot go stale. This script
enforces the cheap proxy for that -- no code fences copying method bodies -- and the rest
is a review convention, which this docstring states so nobody mistakes the checker's
silence for proof.

DESIGN RULE INHERITED FROM AuthorizationSurface.cs
--------------------------------------------------
That type records class-level and method-level [Authorize] separately and VERBATIM rather
than modelling how ASP.NET combines them, because an earlier draft that modelled the rule
rendered an identical line when the class attribute was deleted -- silent through exactly
the edit it existed to catch.

Same discipline here: this script reports RAW observations (this token, that file, this
type) and never a derived verdict about whether a document is "good". A wrong derivation
does not look wrong; it makes the detector blind.

TOKEN SELECTION IS DELIBERATELY CONSERVATIVE
--------------------------------------------
A CLAUDE.md backticks many things that are not repository artefacts: code fragments
(`{ id, name }`), framework types (`FormGroup`), selectors (`abp-lookup-typeahead-mtm`),
method names (`buildPayload()`). Checking all of them would produce noise, and a noisy
gate gets skipped, which is worse than no gate.

So rule 2 only considers tokens that are UNAMBIGUOUSLY paths: ending in a source
extension, or ending in "/". Measured against the known-stale
angular/src/app/doctor-availabilities/CLAUDE.md, that selection finds both genuinely
missing files and produces ZERO false positives on the other 20+ backticked tokens.

Usage:  python check-claude-md.py [repo_root]
Exit:   0 clean, 1 findings, 2 usage error.
"""

import os
import re
import sys

SOURCE_EXTENSIONS = (".cs", ".ts", ".html", ".scss", ".css", ".json", ".yml", ".yaml", ".py")

# Folders never walked: vendored or build output. NOTE the distinction that cost a tuning
# round -- this list controls what is SCANNED, and must NOT control what is allowed to
# EXIST. An early version also pruned Migrations/ and proxy/, so a CLAUDE.md that
# correctly named them was reported as DANGLING. The existence index below is built
# without these semantics for exactly that reason.
SKIP_DIR_PARTS = {"node_modules", "bin", "obj", "dist", ".git", ".vs", ".angular"}

# Folders whose CODE is generated, so their types need not be named in prose.
GENERATED_DIR_PARTS = {"Migrations", "TenantMigrations", "proxy"}

# Files whose types are generated or mechanical. CaseEvaluationApplicationMappers.cs alone
# declares roughly a hundred Mapperly mapper classes; requiring a CLAUDE.md to name each
# would produce noise, and a noisy gate gets skipped, which is worse than no gate.
GENERATED_FILE_MARKERS = ("Mappers", ".g.cs", ".Designer.cs", "Generated")

# A backticked token is a path candidate only if it ends in a source extension or a slash.
BACKTICK = re.compile(r"`([^`\n]+)`")

# Tokens carrying any of these are patterns or placeholders, not literal paths:
# `*.CustomFields.cs`, `{entity}.component.ts`, `angular/src/app/<feature>/`.
PATTERN_CHARS = set("*{}<>?[]$")

# public [abstract|sealed|static|partial] class|interface|record|enum|struct Name
CSHARP_TYPE = re.compile(
    r"^\s*public\s+(?:abstract\s+|sealed\s+|static\s+|partial\s+|readonly\s+)*"
    r"(?:class|interface|record|enum|struct)\s+([A-Za-z_]\w*)",
    re.MULTILINE,
)

# The opt-out. A heading whose text starts with "Not documented here"; the bullet items
# under it are type names this file deliberately does not describe. Opting out is then a
# visible edit in the diff rather than silence.
OPT_OUT_HEADING = re.compile(r"^#{1,6}\s*Not documented here\b.*$", re.MULTILINE | re.IGNORECASE)
BULLET = re.compile(r"^\s*[-*]\s+`?([A-Za-z_]\w*)`?", re.MULTILINE)

# Rule 4 proxy: a fenced block tagged as C#/TS that contains a method signature plus a
# brace, i.e. a body rather than a one-line illustration.
FENCE = re.compile(r"```(\w*)\n(.*?)```", re.DOTALL)
# The \S after the whitespace keeps the two quantifiers from matching the same characters,
# so a keyword followed by a long whitespace run with no brace is rejected in linear time.
# `\s+[^\n]*` accepted the same strings but tried every split point (quadratic).
METHOD_BODY = re.compile(
    r"(public|private|protected|internal|function|async)\s+(?:\S[^\n]*)?\{", re.MULTILINE
)

# Rule 5: a count with no command beside it.
#
# A count is self-auditing -- anyone can re-run it, so a wrong one gets caught. Prose
# reasoning is not, which is why counts were the claims in this repository's documents
# that went stale most reliably and least visibly.
#
# THE DIFFICULTY IS PRECISION, NOT DETECTION. An earlier version was dropped before
# shipping because it fired on versions, ports, dates and line numbers. A gate that
# cries wolf gets skipped, which is the same argument that kept this checker blocking
# rather than advisory. So the noun list is CLOSED and deliberately short: a number
# counts only when directly attached to something this repository can be asked to
# enumerate.
COUNTABLE_NOUNS = (
    "files", "tests", "endpoints", "entities", "permissions", "jobs", "projects",
    "migrations", "services", "controllers", "roles", "columns", "tables", "documents",
    "rules", "handlers", "repositories", "contributors", "methods", "classes",
    "interfaces", "components", "routes", "advisories", "findings", "seeders",
)
COUNT_CLAIM = re.compile(r"\b(\d+)\s+(" + "|".join(COUNTABLE_NOUNS) + r")\b", re.IGNORECASE)

# Per-line opt-out. Visible in the diff, like the "Not documented here" heading, so
# declining the rule is an edit a reviewer can see rather than silence.
COUNT_OPT_OUT = re.compile(r"<!--\s*count-ok:", re.IGNORECASE)

# A backticked span on the SAME line silences the count ONLY IF it is a COMMAND.
#
# THE DEFECT THIS ENCODES, caught by running the rule over the repository before
# trusting it. An earlier version treated ANY inline span as the backing command, and
# all three genuine count claims in this repository sit in table rows beside a backticked
# FILENAME -- `AppointmentEmployerDetailsAppService.cs` next to "8 methods", and
# `api/app/doctor-availabilities` next to "11 routes". So it was silent on 3 of 3 real
# cases, which is worse than not existing: a rule that never fires still reports clean.
#
# A filename is not evidence for a count. A command is. The distinction used here is
# deliberately dumb and therefore predictable: a command has ARGUMENTS (whitespace) and
# begins with a known tool, or it pipes.
COMMAND_TOOLS = (
    "grep", "rg", "ls", "find", "wc", "git", "gh", "python", "python3", "dotnet",
    "npm", "npx", "yarn", "curl", "awk", "sed", "cat", "sort", "uniq", "head", "tail",
)
INLINE_CODE = re.compile(r"`([^`\n]+)`")


def looks_like_a_command(span):
    """True for `grep -rc X src | wc -l`; false for `SomeType.cs` or `api/app/thing`."""
    stripped = span.strip()
    if "|" in stripped:
        return True
    if " " not in stripped:
        return False  # a bare identifier, path or route is not evidence
    first = stripped.split()[0].lstrip("$ ").lower()
    return first in COMMAND_TOOLS

# A fence within this many lines AFTER the claim also counts as its evidence, because
# "There are 45 entities.\n\nMeasured with:\n\n```bash" is the shape people write.
FENCE_LOOKAHEAD = 5


def is_skipped(path):
    return any(part in SKIP_DIR_PARTS for part in path.replace("\\", "/").split("/"))


def find_claude_files(root):
    found = []
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIR_PARTS and not d.startswith(".")]
        if "CLAUDE.md" in filenames:
            found.append(os.path.join(dirpath, "CLAUDE.md"))
    return sorted(found)


def read(path):
    with open(path, "r", encoding="utf-8", errors="replace") as handle:
        return handle.read()


def folder_has_source(folder):
    """True if the folder contains at least one source file, at any depth."""
    for dirpath, dirnames, filenames in os.walk(folder):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIR_PARTS and not d.startswith(".")]
        for name in filenames:
            if name.endswith(SOURCE_EXTENSIONS):
                return True
    return False


def path_tokens(text):
    """Backticked tokens that are unambiguously LITERAL file or directory references.

    Conservative by design. A CLAUDE.md backticks code fragments, framework types,
    selectors and method names; none of those are repository artefacts and checking them
    would swamp the real findings.
    """
    out = []
    for raw in BACKTICK.findall(text):
        token = raw.strip()
        if not token or " " in token or token.startswith(("http://", "https://")):
            continue
        if PATTERN_CHARS & set(token):
            continue  # a glob or a placeholder, not a path
        if token.startswith(".") and "/" not in token:
            continue  # a bare extension discussed in prose (`.html`), not a file
        if token.endswith("/") or token.endswith(SOURCE_EXTENSIONS):
            out.append(token)
    return sorted(set(out))


def is_ignored(path, root):
    """A gitignored file legitimately does not exist in a clean checkout.

    appsettings.Local.json and appsettings.secrets.json are the live cases: a CLAUDE.md
    SHOULD tell you to create them, and flagging that as a dangling reference would
    punish correct documentation. Checked via git rather than a hardcoded list so the
    rule tracks .gitignore instead of drifting from it.
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


def build_index(root):
    """One pass over the tree: every directory name, file name, and relative path.

    Built once rather than walked per token -- the first version re-walked for every
    token and took 25 seconds. Generated directories ARE indexed: a CLAUDE.md may
    legitimately point at Migrations/ or proxy/ even though we do not scan inside them.
    """
    dir_names, file_names, rel_paths = set(), set(), set()
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIR_PARTS]
        rel_dir = os.path.relpath(dirpath, root).replace("\\", "/")
        for d in dirnames:
            dir_names.add(d)
            rel_paths.add((rel_dir + "/" + d).lstrip("./"))
        for f in filenames:
            file_names.add(f)
            rel_paths.add((rel_dir + "/" + f).lstrip("./"))
    return {"dirs": dir_names, "files": file_names, "paths": rel_paths}


def token_resolves(token, folder, root, index):
    cleaned = token.rstrip("/").lstrip("./")
    wants_dir = token.endswith("/")

    # Exact path, relative to the documenting folder or to the repo root.
    if os.path.exists(os.path.join(folder, cleaned)) or os.path.exists(os.path.join(root, cleaned)):
        return True
    if cleaned in index["paths"]:
        return True

    # Basename anywhere in the tree. Deliberately permissive: this rule exists to catch a
    # file that does NOT exist at all, not to police where it lives.
    base = os.path.basename(cleaned)
    return base in (index["dirs"] if wants_dir else index["files"])


def declared_types(folder):
    """Public types declared in .cs files DIRECTLY in this folder, not in subfolders.

    Direct-only is deliberate: a folder's CLAUDE.md documents that folder. A subfolder
    with its own code is that subfolder's business, and usually has its own CLAUDE.md.
    """
    types = {}
    if any(part in GENERATED_DIR_PARTS for part in folder.replace("\\", "/").split("/")):
        return types
    try:
        names = os.listdir(folder)
    except OSError:
        return types
    for name in sorted(names):
        if not name.endswith(".cs"):
            continue
        if any(marker in name for marker in GENERATED_FILE_MARKERS):
            continue
        full = os.path.join(folder, name)
        if not os.path.isfile(full):
            continue
        for match in CSHARP_TYPE.findall(read(full)):
            types.setdefault(match, name)
    return types


def opted_out(text):
    match = OPT_OUT_HEADING.search(text)
    if not match:
        return set()
    return set(BULLET.findall(text[match.end():]))


def uncommanded_counts(rel, text):
    """Counts asserted in prose with no command or generated block backing them.

    Quiet by default. A claim is reported ONLY when all of these hold:
      - a number is directly attached to a closed-list countable noun,
      - the line carries no inline code span,
      - no fence opens within FENCE_LOOKAHEAD lines after it,
      - the line is not itself inside a fence (sample output is not a claim),
      - the line carries no explicit opt-out marker.
    """
    findings = []
    lines = text.split("\n")

    # Which lines sit inside a fence, so sample output is never read as a claim.
    inside = [False] * len(lines)
    fenced = False
    for number, line in enumerate(lines):
        if line.lstrip().startswith("```"):
            fenced = not fenced
            inside[number] = True
            continue
        inside[number] = fenced

    for number, line in enumerate(lines):
        if inside[number] or COUNT_OPT_OUT.search(line):
            continue
        match = COUNT_CLAIM.search(line)
        if not match:
            continue
        if any(looks_like_a_command(span) for span in INLINE_CODE.findall(line)):
            continue
        window = lines[number + 1 : number + 1 + FENCE_LOOKAHEAD]
        if any(following.lstrip().startswith("```") for following in window):
            continue
        findings.append(
            (
                rel,
                "COUNT",
                'line %d asserts "%s %s" with no command beside it; add the command that '
                "produces it, or opt out with <!-- count-ok: reason -->"
                % (number + 1, match.group(1), match.group(2)),
            )
        )
    return findings


def check(path, root, index):
    folder = os.path.dirname(path)
    rel = os.path.relpath(path, root).replace("\\", "/")
    text = read(path)
    findings = []

    # Rule 1: orphan.
    if not folder_has_source(folder):
        findings.append((rel, "ORPHAN", "no source file anywhere beneath this folder"))
        return findings  # Nothing else is meaningful without code to compare against.

    # Rule 2: dangling file or directory reference.
    for token in path_tokens(text):
        if token_resolves(token, folder, root, index):
            continue
        if is_ignored(os.path.join(folder, token.rstrip("/")), root):
            continue  # gitignored by design; see is_ignored
        findings.append((rel, "DANGLING", "names `%s`, which does not exist" % token))

    # Rule 3: omission.
    excluded = opted_out(text)
    for type_name, source_file in sorted(declared_types(folder).items()):
        if type_name in excluded:
            continue
        if re.search(r"\b%s\b" % re.escape(type_name), text):
            continue
        findings.append(
            (rel, "OMISSION", "does not mention `%s` (declared in %s)" % (type_name, source_file))
        )

    # Rule 4 proxy: a copied method body.
    for lang, body in FENCE.findall(text):
        if lang.lower() in ("csharp", "cs", "typescript", "ts") and METHOD_BODY.search(body):
            findings.append(
                (rel, "RESTATEMENT", "a %s fence copies a method body; carry WHY, not WHAT" % lang)
            )
            break

    # Rule 5: a count with no command beside it.
    findings.extend(uncommanded_counts(rel, text))

    return findings


def main(argv):
    root = os.path.abspath(argv[1]) if len(argv) > 1 else os.getcwd()
    if not os.path.isdir(root):
        print("FAIL: not a directory: %s" % root)
        return 2

    files = find_claude_files(root)
    if not files:
        # Same non-empty assertion as the identifier resolver. A checker that silently
        # examines nothing reports a clean repository, which is the worst possible lie.
        print("FAIL: found no CLAUDE.md files at all under %s." % root)
        print("      Either the path is wrong or the walk is broken. Not reporting clean.")
        return 1

    index = build_index(root)
    findings = []
    for path in files:
        findings.extend(check(path, root, index))

    print("Scanned %d CLAUDE.md files under %s" % (len(files), root))
    if not findings:
        print("OK: every CLAUDE.md resolves against the code beside it.")
        return 0

    by_file = {}
    for rel, kind, detail in findings:
        by_file.setdefault(rel, []).append((kind, detail))

    print("")
    print("%d finding(s) in %d file(s):" % (len(findings), len(by_file)))
    for rel in sorted(by_file):
        print("")
        print("  %s" % rel)
        for kind, detail in by_file[rel]:
            print("    %-12s %s" % (kind, detail))
    print("")
    print("A CLAUDE.md is read before the code and trusted more than it. Fix or opt out")
    print("explicitly under a 'Not documented here' heading.")
    return 1


if __name__ == "__main__":
    sys.exit(main(sys.argv))
