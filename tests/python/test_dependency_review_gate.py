"""The Dependency Review licence gate must be able to fail (#691).

`actions/dependency-review-action` ignores an input it no longer declares, with a warning in
the job log and a green result. A bump that drops `deny-licenses` would therefore switch the
only licence control in the repository off without any check going red. The workflow carries
a step that compares the inputs it passes with the inputs the pinned action declares; this
file runs THAT step's own script (extracted from the workflow, not a copy) against fixtures
and asserts it fails when it should.

What this does NOT prove: that the action refuses a denied licence. That is shown by a
poisoned dependency on a pull request, recorded in the workflow's proof note.

Needs bash and python on PATH; the script is run offline through DR_ACTION_YML.
"""

from __future__ import annotations

import os
import re
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
WORKFLOW = REPO_ROOT / ".github" / "workflows" / "dependency-review.yml"

DECLARES_ALL = """name: Dependency Review
inputs:
  fail-on-severity:
    required: false
  comment-summary-in-pr:
    required: false
  deny-licenses:
    required: false
  allow-dependencies-licenses:
    required: false
outputs:
  comment-content:
    description: x
"""


def _workflow_text() -> str:
    return WORKFLOW.read_text(encoding="utf-8").replace("\r\n", "\n")


def _step_script() -> str:
    """The `run:` block of the guard step, dedented, with python3 -> this interpreter."""
    text = _workflow_text()
    start = text.index("      - name: Assert the pinned action still declares")
    run = text.index("        run: |\n", start) + len("        run: |\n")
    end = text.index("      - uses: actions/dependency-review-action@", run)
    body = "\n".join(line[10:] for line in text[run:end].split("\n"))
    return body.replace("python3 -", f'"{Path(sys.executable).as_posix()}" -')


def _bash() -> str | None:
    # On Windows `bash` on PATH is often the WSL launcher, which cannot run a temp file here.
    for candidate in ("C:/Program Files/Git/bin/bash.exe", shutil.which("bash")):
        if candidate and Path(candidate).exists():
            return candidate
    return None


@unittest.skipUnless(_bash(), "bash not available")
class GateGuardStep(unittest.TestCase):
    def _run(self, action_yml: str, workflow: str | None = None):
        with tempfile.TemporaryDirectory() as td:
            action = Path(td, "action.yml")
            action.write_text(action_yml, encoding="utf-8")
            wf = Path(td, "wf.yml")
            wf.write_text(workflow if workflow is not None else _workflow_text(), encoding="utf-8")
            script = Path(td, "run.sh")
            script.write_text(_step_script(), encoding="utf-8", newline="\n")
            env = dict(os.environ, DR_ACTION_YML=str(action), DR_WORKFLOW=str(wf))
            return subprocess.run([_bash(), str(script)], capture_output=True, text=True,
                                  env=env, timeout=60)

    def test_passes_when_every_input_is_declared(self):
        r = self._run(DECLARES_ALL)
        self.assertEqual(r.returncode, 0, r.stderr)
        self.assertIn("inputs are declared", r.stdout)

    def test_fails_when_the_action_drops_deny_licenses(self):
        r = self._run(DECLARES_ALL.replace("  deny-licenses:\n    required: false\n", ""))
        self.assertNotEqual(r.returncode, 0, "the gate went off and this step stayed green")
        self.assertIn("deny-licenses", r.stderr)

    def test_fails_when_the_workflow_stops_passing_deny_licenses(self):
        wf = re.sub(r"\n          deny-licenses: >-\n(?:            .*\n)+", "\n", _workflow_text())
        self.assertNotIn("deny-licenses: >-", wf, "fixture mutation did not apply")
        r = self._run(DECLARES_ALL, workflow=wf)
        self.assertNotEqual(r.returncode, 0)
        self.assertIn("OFF", r.stderr)


class DenyListContent(unittest.TestCase):
    """Entries a quiet edit could drop, and the one that must never appear."""

    def setUp(self):
        m = re.search(r"deny-licenses: >-\n((?:            .*\n)+)", _workflow_text())
        self.assertIsNotNone(m, "deny-licenses block not found")
        self.denied = {x.strip() for x in m.group(1).replace("\n", " ").split(",") if x.strip()}

    def test_copyleft_families_are_denied(self):
        for lic in ("GPL-2.0-only", "GPL-3.0-or-later", "AGPL-3.0-only", "AGPL-3.0-or-later", "SSPL-1.0"):
            self.assertIn(lic, self.denied)

    def test_lgpl_is_not_denied(self):
        # The whole ABP framework is LGPL-3.0; denying it blocks the stack.
        self.assertFalse([d for d in self.denied if d.startswith("LGPL")])


if __name__ == "__main__":
    unittest.main()
