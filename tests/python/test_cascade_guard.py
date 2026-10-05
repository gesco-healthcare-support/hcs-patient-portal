"""The cascade guard's zero-SHA fallback examines the tip and nothing else (#996).

The workflow's own comment promises "EXAMINE ONLY THE TIP, NEVER HISTORY". The spelling `X~1..X`
broke that promise whenever the tip was a MERGE commit, because it means "reachable from X but not
from its first parent", i.e. everything the second parent carried. Promotion PRs into development
are merge commits, so that is the normal case.

The `run` script is extracted from the workflow file and executed against a throwaway repository,
so the test exercises what CI runs rather than a copy of it.
"""

import os
import pathlib
import shutil
import subprocess
import tempfile
import unittest

REPO_ROOT = pathlib.Path(__file__).resolve().parents[2]
WORKFLOW = REPO_ROOT / ".github" / "workflows" / "cascade-guard.yml"
ZERO = "0" * 40
CASCADE = "ci(sync): promote main to development (#1)"


def find_bash():
    """A bash that can run the script. On Windows plain `bash` is often WSL's launcher, which has no
    distribution installed here, so prefer the bash that ships beside git."""
    candidates = []
    git = shutil.which("git")
    if git:
        root = pathlib.Path(git).resolve().parents[1]
        candidates += [root / "bin" / "bash.exe", root.parent / "bin" / "bash.exe"]
    candidates.append(shutil.which("bash"))
    for c in candidates:
        if c and pathlib.Path(c).exists():
            probe = subprocess.run([str(c), "-c", "echo ok"], capture_output=True, text=True)
            if probe.returncode == 0 and probe.stdout.strip() == "ok":
                return str(c)
    return None


BASH = find_bash()


def squash_job_script():
    """The `run: |` block of the squash-detection step, read as text so the test needs no YAML
    library (none is installed in every environment that runs this suite)."""
    lines = WORKFLOW.read_text(encoding="utf-8").splitlines()
    start = next(i for i, l in enumerate(lines) if "name: Cascade commits must have two parents" in l)
    run = next(i for i in range(start, len(lines)) if lines[i].strip() == "run: |")
    indent = len(lines[run]) - len(lines[run].lstrip()) + 2
    body = []
    for l in lines[run + 1:]:
        if l.strip() and len(l) - len(l.lstrip()) < indent:
            break
        body.append(l[indent:] if l.strip() else "")
    return "\n".join(body) + "\n"


@unittest.skipUnless(BASH and shutil.which("git"), "needs a working bash and git")
class CascadeGuardTipOnly(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.mkdtemp(prefix="cascade-guard-")
        self.addCleanup(shutil.rmtree, self.dir, ignore_errors=True)
        self.repo = pathlib.Path(self.dir) / "repo"
        self.repo.mkdir()
        # Untracked, inside the repo, so a bare relative name runs it on any platform.
        script = self.repo / "guard.sh"
        script.write_text(squash_job_script().replace("\r\n", "\n"), encoding="utf-8", newline="\n")
        self.git("init", "-q", "-b", "base")
        self.git("config", "user.email", "t@example.invalid")
        self.git("config", "user.name", "t")
        self.git("config", "commit.gpgsign", "false")

    def git(self, *args):
        return subprocess.run(["git", *args], cwd=self.repo, check=True,
                              capture_output=True, text=True).stdout.strip()

    def commit(self, subject, name):
        (self.repo / name).write_text(subject, encoding="utf-8")
        self.git("add", name)
        self.git("commit", "-q", "-m", subject)
        return self.git("rev-parse", "HEAD")

    def run_guard(self, after, before=ZERO):
        env = dict(os.environ, EVENT_BEFORE=before, EVENT_AFTER=after)
        return subprocess.run([BASH, "guard.sh"], cwd=self.repo, env=env,
                              capture_output=True, text=True).returncode

    def merge_carrying_a_squashed_cascade(self):
        """A merge whose SECOND parent carries a one-parent cascade-subject commit."""
        self.commit("root", "a")
        self.git("checkout", "-q", "-b", "side")
        self.commit(CASCADE, "squashed")
        self.git("checkout", "-q", "base")
        self.commit("tip of base", "b")
        self.git("merge", "-q", "--no-ff", "-m", "Merge pull request #2 from some/branch", "side")
        return self.git("rev-parse", "HEAD")

    def test_merge_tip_does_not_drag_in_the_second_parents_history(self):
        tip = self.merge_carrying_a_squashed_cascade()
        self.assertEqual(self.run_guard(tip), 0)

    def test_a_squashed_cascade_at_the_tip_is_still_refused(self):
        self.commit("root", "a")
        tip = self.commit(CASCADE, "squashed")
        self.assertEqual(self.run_guard(tip), 1)

    def test_a_cascade_merge_at_the_tip_is_legal(self):
        self.commit("root", "a")
        self.git("checkout", "-q", "-b", "side")
        self.commit("work on main", "m")
        self.git("checkout", "-q", "base")
        self.commit("tip of base", "b")
        self.git("merge", "-q", "--no-ff", "-m", CASCADE, "side")
        self.assertEqual(self.run_guard(self.git("rev-parse", "HEAD")), 0)

    def test_an_unreadable_tip_is_not_a_pass(self):
        self.commit("root", "a")
        self.assertEqual(self.run_guard("f" * 40), 2)


if __name__ == "__main__":
    unittest.main()
