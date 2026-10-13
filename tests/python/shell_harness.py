"""Shared helpers for the tests that run real shell scripts (scripts/hosting/*.sh) against fakes.

Those tests need a POSIX bash. CI runs on Linux, where PATH's bash is used. On Windows set TEST_BASH to Git Bash's
plain shell, C:/Program Files/Git/usr/bin/bash.exe, or the behavioural tests are skipped. NOT bin/bash.exe: that
launcher puts /usr/bin in front of PATH, so real coreutils win over the fakes a test puts first. And not PATH's
bash on Windows, which may be WSL's and cannot see this filesystem the same way.
"""

import os
import pathlib
import shutil
import sys


def find_bash():
    explicit = os.environ.get("TEST_BASH")
    if explicit:
        return explicit
    if sys.platform == "win32":
        return None
    return shutil.which("bash")


BASH = find_bash()


def shell_path(path):
    """A path as the shell under test should see it. Git Bash reads "C:/x" in a tar archive name as a remote host,
    and native tools want /c/x, so on Windows it becomes /c/x; on Linux the path is unchanged."""
    text = pathlib.Path(path).as_posix()
    if sys.platform == "win32" and len(text) > 1 and text[1] == ":":
        return "/" + text[0].lower() + text[2:]
    return text
