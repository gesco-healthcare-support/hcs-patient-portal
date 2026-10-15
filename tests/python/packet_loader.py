"""Load the packet-template generators and run them in a scratch directory.

WHY A LOADER. The five modules under `tools/packet-templates` are SCRIPTS, not an
importable package: the directory has no `__init__.py`, and two of the file names
(`build_doctor`, `build_patient`) only differ by the directory they sit in. They are
loaded from their path instead, exactly as `gate_loader.py` already does for
`scripts/coverage-gate.py`, and for the same reason -- the module guards its entry point
behind `if __name__ == "__main__"`, so importing one neither writes a document nor calls
`sys.exit`. Verified on all five before this file was written.

WHY IT MATTERS FOR COVERAGE. `tests/python/test_golden_output.py`'s sibling suite runs
each builder through `subprocess.run([sys.executable, script])`. A subprocess is invisible
to the parent `coverage run`, which is why all four of these files read 0% while being
exercised on every pull request. Loading them IN-PROCESS is the whole point: the same code
runs, and this time the run is measured.

Stdlib only, deliberately -- matching pyproject.toml's note that this suite takes no
test-runner dependency. `unittest discover` puts `tests/python` on `sys.path`, so the test
modules import this by name with no packaging and no `conftest.py`.
"""

from __future__ import annotations

import contextlib
import importlib.util
import os
import shutil
import sys
import tempfile
from pathlib import Path
from types import ModuleType

REPO_ROOT = Path(__file__).resolve().parents[2]

DOCTOR = "tools/packet-templates/doctor/build_doctor.py"
PATIENT = "tools/packet-templates/patient/build_patient.py"
ATTORNEY = "tools/packet-templates/attorney/build_attorney.py"
POST_PROCESS = "tools/packet-templates/shared/post_process.py"
CHECK_FIELDS = "tools/packet-templates/shared/check_fields.py"

_CACHE: dict[str, ModuleType] = {}


def load_module(name: str, relative: str, fresh: bool = False) -> ModuleType:
    """Import a module from a repo-relative path under a legal module name.

    `fresh=True` re-executes the module body instead of returning the cached object.
    `check_fields.py` needs that: it is a top-to-bottom script with no functions, so its
    behaviour under a different fixture can only be observed by running it again.
    """
    if not fresh and name in _CACHE:
        return _CACHE[name]
    source = REPO_ROOT / relative
    if not source.is_file():
        raise FileNotFoundError(
            f"expected {relative} at {source}. The suite resolves the repository root as "
            "two parents above this file; a move breaks that assumption loudly here "
            "rather than silently skipping the tests."
        )
    spec = importlib.util.spec_from_file_location(name, source)
    if spec is None or spec.loader is None:
        raise ImportError(f"could not build an import spec for {source}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    if not fresh:
        _CACHE[name] = module
    return module


@contextlib.contextmanager
def scratch_cwd():
    """Run a builder's `build()` in a throwaway directory.

    The builders write with a bare `open("<name>.html", "w")`, so their output follows the
    working directory while image assets resolve from `__file__` and are unaffected. The
    golden suite makes the same point about why a temp directory rather than the repo:
    it keeps the tree clean and cannot pick up a stale artefact from an earlier run.

    `os.chdir` is restored in `finally` rather than after the body, because a builder that
    raises would otherwise leave every later test in this process running from a deleted
    directory -- a failure that would present as unrelated tests failing to open files.
    """
    work = tempfile.mkdtemp(prefix="packet-coverage-")
    previous = os.getcwd()
    try:
        os.chdir(work)
        yield Path(work)
    finally:
        os.chdir(previous)
        shutil.rmtree(work, ignore_errors=True)
