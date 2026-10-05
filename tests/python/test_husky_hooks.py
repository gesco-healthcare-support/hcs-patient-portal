"""The git hooks never touch the shared stash stack.

`refs/stash` is one stack per repository, shared by every worktree. Several sessions work in
parallel worktrees here, so anything a hook pushes onto the stash can be popped by a commit running
in a different worktree, and uncommitted work then lands in the wrong place without any error. That
happened on 2026-10-02: lint-staged's automatic backup crossed between two worktrees.

lint-staged pushes that backup unless it is run with `--no-stash`. A later edit can drop the flag
without anything failing, because every hook still passes with or without it; this test is the guard.
"""

import pathlib
import re
import unittest

REPO_ROOT = pathlib.Path(__file__).resolve().parents[2]
HOOKS = REPO_ROOT / "angular" / ".husky"
HOOK_NAMES = ("pre-commit", "pre-push", "commit-msg")


def code_lines(path):
    """The hook's lines with shell comments removed, so an explanation that names a command is
    not mistaken for a call to it."""
    lines = []
    for line in path.read_text(encoding="utf-8").splitlines():
        code = line.split("#", 1)[0] if not line.lstrip().startswith("#") else ""
        if code.strip():
            lines.append(code)
    return lines


class HuskyHooksTests(unittest.TestCase):
    def test_every_hook_exists(self):
        # Without this, a renamed hook would make every assertion below pass over nothing.
        for name in HOOK_NAMES:
            with self.subTest(hook=name):
                self.assertTrue((HOOKS / name).is_file(), f"angular/.husky/{name} is missing")

    def test_lint_staged_is_always_run_without_its_stash_backup(self):
        calls = [
            line
            for line in code_lines(HOOKS / "pre-commit")
            if re.search(r"\blint-staged\b", line)
        ]
        self.assertTrue(calls, "pre-commit no longer runs lint-staged; update this test with it")
        for line in calls:
            with self.subTest(call=line.strip()):
                self.assertRegex(
                    line,
                    r"\blint-staged\b.*\s--no-stash\b",
                    "lint-staged must run with --no-stash: its backup goes on the stash stack "
                    "every worktree shares, and a concurrent commit elsewhere can pop it",
                )

    def test_no_hook_calls_git_stash(self):
        for name in HOOK_NAMES:
            with self.subTest(hook=name):
                calls = [line for line in code_lines(HOOKS / name) if re.search(r"\bgit\s+stash\b", line)]
                self.assertEqual(calls, [], f"{name} uses the shared stash stack")


if __name__ == "__main__":
    unittest.main()
