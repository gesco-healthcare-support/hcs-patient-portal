"""Golden-file test for the packet template generators.

WHY THIS EXISTS. #771, #783 and #790 all changed these generators, and all
three were verified the same way: run each builder, checksum the generated
HTML, confirm it is byte-identical to before. That worked -- the output is
deterministic and the check caught what it was meant to. But it lived in one
person's shell history, which means nobody else could run it, it did not run on
anyone else's PR, it proved nothing to a reviewer beyond an assertion, and
forgetting it once would have gone unnoticed. See #797.

WHAT IT ASSERTS. Each builder regenerates its documents into a temporary
directory and the SHA-256 of each file must equal the value recorded in
golden.sha256.

BRITTLE BY DESIGN. Any deliberate template change fails this test until someone
updates the recorded hash. That is the point rather than a cost: these
generators produce a medical-legal form that bills parties and can reach
opposing counsel and the WCAB, and an unnoticed change to it is the failure
worth guarding against. Refactors -- which is what the sweeps were -- must not
move a single byte, and this says so out loud.

UPDATING A HASH. Run this file with --update, read the diff it prints, and
commit the new golden.sha256 in the same change as the template edit so review
sees both together:

    python tools/packet-templates/tests/test_golden_output.py --update
"""

from __future__ import annotations

import hashlib
import os
import shutil
import subprocess
import sys
import tempfile
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
GOLDEN = os.path.join(HERE, "golden.sha256")

# builder script -> the files it writes into its working directory
BUILDERS = {
    os.path.join("doctor", "build_doctor.py"): ["doctor.html"],
    os.path.join("patient", "build_patient.py"): ["patient.html"],
    os.path.join("attorney", "build_attorney.py"): ["ame_ime.html", "pqme.html"],
}


def _sha256(path: str) -> str:
    """Hash the document exactly as written, with no normalisation.

    The builders pass newline="\\n" explicitly, so the generated file is
    byte-identical on every platform and a raw hash is meaningful everywhere.

    Deliberately NOT normalising CRLF here, though that would also make the
    test pass. Normalising fixes the measurement and leaves the artefact
    platform-dependent, so a developer's local copy and the one baked into the
    packet-renderer image stay two variants that merely mean the same thing --
    and the next consumer that compares bytes hits the same wall. Hashing raw
    also means that if a builder ever loses its newline argument, this test
    fails and says so; a normalising test would hide exactly that regression.
    """
    with open(path, "rb") as fh:
        return hashlib.sha256(fh.read()).hexdigest()


def generate() -> dict[str, str]:
    """Run every builder in a scratch directory and hash what it produced.

    The builders write with a bare `open("<name>.html", "w")`, so their output
    follows the working directory; image assets resolve from `__file__` and are
    unaffected. Generating into a temp directory therefore keeps the repo clean
    and cannot pick up a stale artefact from a previous local run -- which the
    manual checks could, since they wrote next to the source.
    """
    digests: dict[str, str] = {}
    for script, outputs in sorted(BUILDERS.items()):
        script_path = os.path.join(ROOT, script)
        work = tempfile.mkdtemp(prefix="packet-golden-")
        try:
            result = subprocess.run(
                [sys.executable, script_path],
                cwd=work,
                capture_output=True,
                text=True,
            )
            if result.returncode != 0:
                raise AssertionError(
                    "%s exited %d\nstdout:\n%s\nstderr:\n%s"
                    % (script, result.returncode, result.stdout, result.stderr)
                )
            for name in outputs:
                produced = os.path.join(work, name)
                if not os.path.isfile(produced):
                    raise AssertionError(
                        "%s did not write %s (wrote: %s)"
                        % (script, name, sorted(os.listdir(work)))
                    )
                digests[name] = _sha256(produced)
        finally:
            shutil.rmtree(work, ignore_errors=True)
    return digests


def read_golden() -> dict[str, str]:
    recorded: dict[str, str] = {}
    with open(GOLDEN, encoding="utf-8") as fh:
        for line in fh:
            line = line.strip()
            if not line or line.startswith("#"):
                continue
            digest, name = line.split(None, 1)
            recorded[name.strip()] = digest
    return recorded


def write_golden(digests: dict[str, str]) -> None:
    with open(GOLDEN, "w", encoding="utf-8", newline="\n") as fh:
        fh.write("# SHA-256 of every document the packet generators emit.\n")
        fh.write("# The builders write LF explicitly, so these are the same bytes on\n")
        fh.write("# every platform and the same bytes the packet-renderer image ships.\n")
        fh.write("# Regenerate with:\n")
        fh.write("#   python tools/packet-templates/tests/test_golden_output.py --update\n")
        fh.write("# A change here must ship with the template edit that caused it.\n")
        for name in sorted(digests):
            fh.write("%s  %s\n" % (digests[name], name))


class GoldenOutputTest(unittest.TestCase):
    def test_generated_documents_match_the_recorded_hashes(self) -> None:
        recorded = read_golden()
        produced = generate()

        self.assertEqual(
            sorted(recorded),
            sorted(produced),
            "the set of generated documents changed; update golden.sha256",
        )

        drifted = [n for n in sorted(produced) if produced[n] != recorded[n]]
        self.assertEqual(
            drifted,
            [],
            "generated output changed for: %s\n"
            "If the change was intended, re-run with --update and commit "
            "golden.sha256 alongside the template edit.\n%s"
            % (
                ", ".join(drifted),
                "\n".join(
                    "  %-14s recorded %s\n  %-14s produced %s"
                    % (n, recorded[n], "", produced[n])
                    for n in drifted
                ),
            ),
        )


if __name__ == "__main__":
    if "--update" in sys.argv:
        current = read_golden() if os.path.isfile(GOLDEN) else {}
        fresh = generate()
        for name in sorted(fresh):
            was = current.get(name)
            if was is None:
                print("added   %-14s %s" % (name, fresh[name]))
            elif was != fresh[name]:
                print("changed %-14s %s -> %s" % (name, was, fresh[name]))
            else:
                print("same    %-14s %s" % (name, fresh[name]))
        for name in sorted(set(current) - set(fresh)):
            print("removed %-14s %s" % (name, current[name]))
        write_golden(fresh)
        print("\nwrote %s" % GOLDEN)
        sys.exit(0)
    unittest.main()
