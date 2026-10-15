"""In-process stand-ins for `pikepdf` and `pypdf`, the two libraries the Python CI job
does not install.

WHY THIS EXISTS. `ci.yml`'s `Python: Test` job installs exactly one package --
`coverage==7.15.2` -- and says so in a comment: "The tests themselves are stdlib
`unittest` and need nothing installed." But `post_process.py` imports `pikepdf` and
`check_fields.py` imports `pypdf`, so neither module can be imported at all on the runner.
They are two of the four files `pyproject.toml` records as sitting in the denominator at
0%, and this is why.

Adding the real libraries to CI was the alternative and is the wrong trade: pikepdf ships
a compiled QPDF extension, which would turn an eleven-second job into a build, for code
whose logic is PDF dictionary manipulation rather than anything QPDF decides. The repo
already set the precedent -- `test_packet_renderer_paths.py:52` stubs `flask`,
`weasyprint` and `pikepdf` into `sys.modules` to import `app.py` without them.

WHAT THIS IS NOT. It is not a PDF implementation. It models the object layer
`post_process.py` actually touches -- names, dictionaries, arrays, streams, pages and the
save/open lifecycle -- so the module's own branching can be exercised and asserted. Where
a test would only be re-stating the fake's behaviour, no test is written; the assertions
target the generator's arithmetic, its branch choices, and the mutations it makes.

Nothing here is imported by production code. `tests/*` is omitted from coverage in
pyproject.toml, so this file adds no denominator of its own.
"""

from __future__ import annotations

import sys
import types
from typing import Any


def _key(k: Any) -> str:
    """Normalise a dictionary key to its "/Name" string form.

    pikepdf lets the same entry be reached as `d.Rect`, `d["/Rect"]` and `d[Name.Rect]`,
    and `post_process.py` uses all three spellings against the same objects. Collapsing
    them onto one string key is what makes those interchangeable here too.
    """
    if isinstance(k, Name):
        return str(k)
    if isinstance(k, str):
        return k if k.startswith("/") else "/" + k
    return str(k)


class _NameMeta(type):
    """Turns the attribute spelling `Name.Type` into `Name("/Type")`.

    pikepdf exposes every standard PDF name as a class attribute, and `post_process.py`
    leans on it heavily (`Name.AP`, `Name.BS`, `Name.MK`, `Name.Annots`, ...). Generating
    them on demand means the fake can never be missing one -- a hand-written list would
    fail late, at the first unusual name, in whichever test happened to reach it.
    """

    def __getattr__(cls, item: str) -> "Name":
        if item.startswith("_"):
            raise AttributeError(item)
        return cls("/" + item)


class Name(metaclass=_NameMeta):
    """A PDF name object. Compares equal to its own "/Foo" string so the real library's
    mixed comparisons (`f.get("/FT") != Name("/Btn")`) behave the same way here."""

    __slots__ = ("value",)

    def __init__(self, value: str) -> None:
        self.value = value if value.startswith("/") else "/" + value

    def __str__(self) -> str:
        return self.value

    def __repr__(self) -> str:
        return f"Name({self.value!r})"

    def __eq__(self, other: object) -> bool:
        if isinstance(other, Name):
            return self.value == other.value
        if isinstance(other, str):
            return self.value == _key(other)
        return NotImplemented

    def __ne__(self, other: object) -> bool:
        result = self.__eq__(other)
        return result if result is NotImplemented else not result

    def __hash__(self) -> int:
        return hash(self.value)


class Array(list):
    """A PDF array. A plain list is the whole behaviour `post_process.py` needs."""


class String:
    """A PDF string literal. Only `str()` of it is ever read back."""

    __slots__ = ("value",)

    def __init__(self, value: str) -> None:
        self.value = str(value)

    def __str__(self) -> str:
        return self.value

    def __eq__(self, other: object) -> bool:
        return str(self) == str(other)

    def __hash__(self) -> int:
        return hash(self.value)


class Dictionary:
    """A PDF dictionary supporting attribute, item and `.get` access interchangeably.

    `is_indirect` / `objgen` are real pikepdf surface and are load-bearing in `fix()`,
    which de-lists radio kid widgets from the top-level Fields array by comparing objgens.
    They default to the DIRECT-object values so a fixture only becomes indirect by going
    through `Pdf.make_indirect`, exactly as in the real library.
    """

    _PASSTHROUGH = ("is_indirect", "objgen")

    def __init__(self, **kwargs: Any) -> None:
        object.__setattr__(self, "_d", {})
        object.__setattr__(self, "is_indirect", False)
        object.__setattr__(self, "objgen", (0, 0))
        for key, value in kwargs.items():
            self._d[_key(key)] = value

    # -- mapping protocol ---------------------------------------------------
    def __getitem__(self, key: Any) -> Any:
        return self._d[_key(key)]

    def __setitem__(self, key: Any, value: Any) -> None:
        self._d[_key(key)] = value

    def __delitem__(self, key: Any) -> None:
        del self._d[_key(key)]

    def __contains__(self, key: Any) -> bool:
        return _key(key) in self._d

    def get(self, key: Any, default: Any = None) -> Any:
        return self._d.get(_key(key), default)

    def keys(self):
        return [Name(k) for k in self._d]

    def __iter__(self):
        return iter(self.keys())

    def __len__(self) -> int:
        return len(self._d)

    # -- attribute spelling of the same entries -----------------------------
    def __getattr__(self, item: str) -> Any:
        if item.startswith("_"):
            raise AttributeError(item)
        try:
            return object.__getattribute__(self, "_d")[_key(item)]
        except KeyError:
            raise AttributeError(item) from None

    def __setattr__(self, item: str, value: Any) -> None:
        if item.startswith("_") or item in self._PASSTHROUGH:
            object.__setattr__(self, item, value)
        else:
            self._d[_key(item)] = value

    def __repr__(self) -> str:
        return f"Dictionary({self._d!r})"


class Stream(Dictionary):
    """A PDF content stream. `post_process.make_xobj` constructs these positionally as
    `pikepdf.Stream(pdf, content.encode())` and then assigns dictionary entries onto them,
    so a Stream is a Dictionary that also carries bytes."""

    def __init__(self, pdf: "Pdf", data: bytes = b"") -> None:
        super().__init__()
        object.__setattr__(self, "_data", bytes(data))
        object.__setattr__(self, "_pdf", pdf)

    def read_bytes(self) -> bytes:
        return self._data

    def read_text(self) -> str:
        """The painter operators as written. Every appearance assertion reads this."""
        return self._data.decode()


class Page:
    """A page. `harvest_circle_fields` reaches its dictionary through `page.obj`."""

    __slots__ = ("obj",)

    def __init__(self, obj: Dictionary) -> None:
        self.obj = obj


class Pdf:
    """A document, opened from a path registered by the test rather than from disk.

    `Pdf.open` in the real library reads a file. Here it hands back whatever fixture the
    test registered for that path, so `fix()` / `finalize()` / `demo()` can be driven over
    a known object graph and the mutations asserted directly. `save()` records the target
    instead of writing, which is also what lets a test prove `finalize` saves at all.
    """

    _registry: dict[str, "Pdf"] = {}

    def __init__(self, root: Dictionary | None = None, pages: list[Page] | None = None):
        self.Root = root if root is not None else Dictionary()
        self.pages = pages if pages is not None else []
        self.saved_to: list[str | None] = []
        self.opened_with: dict[str, Any] = {}
        self.indirect_objects: list[Any] = []
        self._objgen_counter = 0

    @classmethod
    def register(cls, path: str, pdf: "Pdf") -> "Pdf":
        cls._registry[str(path)] = pdf
        return pdf

    @classmethod
    def reset_registry(cls) -> None:
        cls._registry.clear()

    @classmethod
    def open(cls, path: Any, **kwargs: Any) -> "Pdf":
        key = str(path)
        if key not in cls._registry:
            raise FileNotFoundError(
                f"no fixture PDF registered at {key!r}. Register one with "
                "fake_pdf_libs.Pdf.register(path, pdf) before calling the code under test."
            )
        pdf = cls._registry[key]
        pdf.opened_with = dict(kwargs)
        return pdf

    def make_indirect(self, obj: Any) -> Any:
        self._objgen_counter += 1
        if isinstance(obj, Dictionary):
            object.__setattr__(obj, "is_indirect", True)
            object.__setattr__(obj, "objgen", (self._objgen_counter, 0))
        self.indirect_objects.append(obj)
        return obj

    def save(self, path: Any = None) -> None:
        self.saved_to.append(None if path is None else str(path))


# --------------------------------------------------------------------------
# pypdf, for check_fields.py
# --------------------------------------------------------------------------


class Annotation:
    """An annotation reached through `page.get("/Annots")`. `check_fields.py` calls
    `.get_object()` on each one, which is pypdf's indirect-reference resolution."""

    def __init__(self, **entries: Any) -> None:
        self._entries = {_key(k): v for k, v in entries.items()}

    def get_object(self) -> "Annotation":
        return self

    def get(self, key: Any, default: Any = None) -> Any:
        return self._entries.get(_key(key), default)


class ReaderPage(dict):
    """A page. `check_fields.py` only ever calls `.get("/Annots")` on it, so a dict is
    the entire surface."""


class FakeReader:
    """What `pypdf.PdfReader(path)` resolves to. Built by the test, not by parsing."""

    def __init__(
        self,
        pages: list[ReaderPage] | None = None,
        fields: dict[str, dict] | None = None,
        root: dict | None = None,
    ) -> None:
        self.pages = pages if pages is not None else []
        self.trailer = {"/Root": root if root is not None else {}}
        self._fields = fields

    def get_fields(self) -> dict | None:
        return self._fields


class _PdfReaderFactory:
    """Stands in for the `pypdf.PdfReader` CLASS.

    `check_fields.py` constructs its reader at MODULE level, against a hardcoded
    `/work/page1.pdf`. There is no seam to inject through, so the fixture is parked here
    and handed out on construction. `__new__` returning a foreign object is what makes
    `PdfReader(path)` evaluate to the prepared reader rather than to an instance of this
    class -- `__init__` is then not called, by the normal Python rule.
    """

    reader: FakeReader | None = None
    opened: list[str] = []

    def __new__(cls, path: Any, *_args: Any, **_kwargs: Any):
        cls.opened.append(str(path))
        if cls.reader is None:
            raise AssertionError(
                "fake_pdf_libs._PdfReaderFactory.reader was not set before check_fields "
                "was imported. Use fake_pdf_libs.install_pypdf(reader)."
            )
        return cls.reader


def build_pikepdf_module() -> types.ModuleType:
    """Assemble the fake as a module object, matching the real import surface."""
    module = types.ModuleType("pikepdf")
    module.Pdf = Pdf
    module.Name = Name
    module.Dictionary = Dictionary
    module.Array = Array
    module.Stream = Stream
    module.String = String
    module.Page = Page
    return module


def build_pypdf_module() -> types.ModuleType:
    module = types.ModuleType("pypdf")
    module.PdfReader = _PdfReaderFactory
    return module


def install_pikepdf() -> types.ModuleType:
    """Put the fake `pikepdf` in `sys.modules` and return it."""
    module = build_pikepdf_module()
    sys.modules["pikepdf"] = module
    return module


def install_pypdf(reader: FakeReader) -> types.ModuleType:
    """Put the fake `pypdf` in `sys.modules`, primed with the reader to hand out."""
    _PdfReaderFactory.reader = reader
    _PdfReaderFactory.opened = []
    module = build_pypdf_module()
    sys.modules["pypdf"] = module
    return module


def uninstall(*names: str) -> None:
    """Remove the fakes again so one test module cannot leak them into another."""
    for name in names:
        sys.modules.pop(name, None)
