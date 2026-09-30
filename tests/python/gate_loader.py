"""Load `scripts/coverage-gate.py` for testing.

It cannot be imported by name: a hyphen is not legal in a Python identifier, so
`import coverage-gate` is a syntax error. It is loaded from its path instead,
which is safe because the module guards its entry point behind
`if __name__ == "__main__"` -- verified, so importing it neither runs `main()`
nor calls `sys.exit`.

Stdlib only, deliberately. `unittest discover` puts the start directory on
`sys.path`, so the test modules import this by name with no packaging, no
`conftest.py` and no third-party test runner.
"""

from __future__ import annotations

import importlib.util
import sys
from pathlib import Path
from types import ModuleType

REPO_ROOT = Path(__file__).resolve().parents[2]

_CACHE: dict[str, ModuleType] = {}


def load(name: str = "coverage_gate", relative: str = "scripts/coverage-gate.py") -> ModuleType:
    """Import a module from a repo-relative path under a legal module name."""
    if name in _CACHE:
        return _CACHE[name]
    source = REPO_ROOT / relative
    if not source.is_file():
        raise FileNotFoundError(
            f"expected {relative} at {source}. The suite resolves the repository "
            "root as two parents above this file; a move breaks that assumption "
            "loudly here rather than silently skipping the tests."
        )
    spec = importlib.util.spec_from_file_location(name, source)
    if spec is None or spec.loader is None:
        raise ImportError(f"could not build an import spec for {source}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    _CACHE[name] = module
    return module


gate = load()
