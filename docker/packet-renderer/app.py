"""packet-renderer -- HTTP sidecar: packet template + tokens -> fillable PDF.

This service OWNS the packet HTML templates (single source of truth). The pure-Python generators
under the generators tree are run at image build and their self-contained HTML (images
base64-inlined) is baked in; this module reads each one on first use and verifies that all of them
are present on every `/health` call. The .NET packet pipeline (WeasyPrintPacketRenderer) POSTs a
template name + the resolved ##Group.Field## token map; this service substitutes the tokens,
renders the fillable PDF via WeasyPrint --pdf-forms + the shared post_process, and returns it.

Token VALUES are owned by the .NET side (PacketTokenResolver + PacketTokenMap). This service only
does the mechanical substitution + render, so there is no business logic duplicated across the
language boundary.

Routes:
  GET  /health  -> 200 {"status": "ok", "templates": [...]}      every template file verified
                   503 {"status": "error", "missing": [...]}     one or more absent or unreadable
                   Compose healthcheck + depends_on gate. `curl -f` fails on the 503, so a
                   half-built image never goes healthy and never releases the api.
  POST /render  -> 200 application/pdf      JSON body {"template": "<name>", "tokens": {..}}
                   400 missing/unknown template or malformed body
                   500 render/finalize failure -- the .NET converter treats a non-2xx as a
                       transport error so Hangfire retries the job

Template names (CONTRACT with the .NET side -- keep in sync with PacketTemplateNames in
GenerateAppointmentPacketJob): doctor | patient | attorney-ame | attorney-pqme.

HIPAA: the token map carries PHI (SSN / DOB). NEVER log the tokens, the substituted HTML, or the
PDF -- only the template name and byte sizes. The service binds to 127.0.0.1 on the compose network.
"""

import logging
import os
import re
import sys
import tempfile
from pathlib import Path

from flask import Flask, Response, jsonify, request
from weasyprint import HTML

# post_process.py is the SAME module the generators use; it lives in the generators tree so there
# is one copy. Its location is NOT the same relative to this file in both places, and neither
# offset is derivable from the other: the Dockerfile COPYs `tools/packet-templates` to
# `/app/generators`, which renames the directory AND changes its depth. Both candidates are tried
# and a failure names both, because a bare ImportError here is indistinguishable from a broken
# image layout -- and an absolute container path baked into source is the fragility this replaces.
#
# `..` is resolved rather than indexing `Path.parents`: inside the image this file sits at /app,
# whose `parents` has a single entry, so `parents[1]` would raise IndexError at import and break
# the service in production. Resolving `..` past the filesystem root is harmless.
_HERE = Path(__file__).resolve().parent
_GENERATOR_CANDIDATES = (
    _HERE / "generators",                                          # image:  /app/generators
    (_HERE / ".." / ".." / "tools" / "packet-templates").resolve(),  # repo:  <root>/tools/packet-templates
)


def _resolve_generators_root() -> Path:
    """Return the generators tree, or fail naming every location that was tried."""
    for candidate in _GENERATOR_CANDIDATES:
        if (candidate / "shared" / "post_process.py").is_file():
            return candidate
    tried = "; ".join(str(c / "shared" / "post_process.py") for c in _GENERATOR_CANDIDATES)
    raise RuntimeError(
        f"packet-renderer cannot locate post_process.py. Tried: {tried}. "
        "The image layout or the repository layout has changed; fix the candidate list above."
    )


GENERATORS_ROOT = _resolve_generators_root()
sys.path.insert(0, str(GENERATORS_ROOT / "shared"))
import post_process  # noqa: E402

logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(message)s")
log = logging.getLogger("packet-renderer")

# Baked at image build (see Dockerfile). Template name -> the generator's emitted HTML file.
_TEMPLATE_FILES = {
    "doctor": GENERATORS_ROOT / "doctor" / "doctor.html",
    "patient": GENERATORS_ROOT / "patient" / "patient.html",
    "attorney-ame": GENERATORS_ROOT / "attorney" / "ame_ime.html",
    "attorney-pqme": GENERATORS_ROOT / "attorney" / "pqme.html",
}

# Read on first use rather than at import, so this module can be imported outside the image where
# the generated HTML does not exist (it is gitignored and produced at build time).
#
# THE PROTECTION THAT USED TO LIVE IN THE EAGER LOAD NOW LIVES IN `/health`. Loading every
# template at import meant a half-built image crashed the worker immediately and never went
# healthy, so `depends_on: condition: service_healthy` held the api back. Deferring the read would
# silently give that up -- the container would start, pass its healthcheck, and fail on a real
# render carrying patient data. `/health` therefore stats every template file on every call and
# returns 503 when any is missing, which `curl -f` reports as a failure exactly as a crash did.
_TEMPLATE_CACHE: dict[str, str] = {}


def _load_template(name: str) -> str:
    """Read a template from disk on first use, caching it for this worker."""
    if name not in _TEMPLATE_CACHE:
        _TEMPLATE_CACHE[name] = _TEMPLATE_FILES[name].read_text(encoding="utf-8")
    return _TEMPLATE_CACHE[name]


def _missing_templates() -> list[str]:
    """Names whose backing file is absent, empty or unreadable, checked against the FILESYSTEM.

    Deliberately not answered from `_TEMPLATE_CACHE`. The container runs `gunicorn --workers 2`
    and each worker caches independently, so a cache-derived answer would report whether THIS
    worker happened to have read the file, not whether the image is intact.
    """
    missing = []
    for name, path in sorted(_TEMPLATE_FILES.items()):
        try:
            if not path.is_file() or path.stat().st_size == 0:
                missing.append(name)
                continue
            with open(path, "rb") as fh:
                fh.read(1)
        except OSError:
            missing.append(name)
    return missing


# Mirrors PacketTokenMap.TokenRegex on the .NET side: ##Group.Field##.
TOKEN_REGEX = re.compile(r"##[A-Za-z][A-Za-z0-9_]*\.[A-Za-z][A-Za-z0-9_]*##")

app = Flask(__name__)


@app.get("/health")
def health():
    """Liveness probe AND image-integrity check; reports the template names for quick diagnosis.

    Returns 503 when any template is missing so the compose healthcheck fails. This endpoint is
    load-bearing, not informational: `docker-compose.prod.yml:152` runs
    `curl -f http://localhost:3001/health`, and the api at `:330` declares
    `depends_on: packet-renderer: condition: service_healthy`.
    """
    missing = _missing_templates()
    if missing:
        log.error("health: template files missing or unreadable: %s", ", ".join(missing))
        return jsonify(status="error", missing=missing), 503
    return jsonify(status="ok", templates=sorted(_TEMPLATE_FILES))


@app.post("/render")
def render():
    """Substitute tokens into the named template and return the rendered fillable PDF.

    A malformed body or unknown template is a 400 (caller error). Any render/finalize failure
    propagates so Flask returns a 500 with the traceback logged -- the .NET converter then surfaces
    it as a transport error for Hangfire to retry.
    """
    payload = request.get_json(silent=True)
    if not isinstance(payload, dict):
        return jsonify(error="expected a JSON object {template, tokens}"), 400

    name = payload.get("template")
    if name not in _TEMPLATE_FILES:
        return jsonify(error=f"unknown template; expected one of {sorted(_TEMPLATE_FILES)}"), 400

    tokens = payload.get("tokens") or {}
    if not isinstance(tokens, dict):
        return jsonify(error="tokens must be an object of ##Group.Field## -> value"), 400

    try:
        template = _load_template(name)
    except OSError as exc:
        # A template present at startup can still vanish; surface it as a server error rather than
        # a traceback, and say which one.
        log.error("template %s could not be read: %s", name, exc)
        return jsonify(error=f"template {name} could not be read"), 500

    # Single-pass substitution; unknown ##tokens## stay literal (mirrors the .NET DOCX path so a
    # mapping gap shows in the output instead of being silently blanked).
    html = TOKEN_REGEX.sub(lambda m: tokens.get(m.group(0), m.group(0)), template)

    fd, path = tempfile.mkstemp(suffix=".pdf")
    os.close(fd)
    try:
        HTML(string=html).write_pdf(path, pdf_forms=True)
        post_process.finalize(path)
        with open(path, "rb") as fh:
            pdf = fh.read()
    finally:
        try:
            os.remove(path)
        except OSError:
            pass

    # Template name + sizes only -- never the tokens or content (PHI).
    log.info("rendered %s: %d tokens -> %d bytes PDF", name, len(tokens), len(pdf))
    return Response(pdf, mimetype="application/pdf")


if __name__ == "__main__":
    # Local debugging only; the container runs gunicorn (see Dockerfile CMD).
    app.run(host="0.0.0.0", port=int(os.environ.get("PORT", "3001")))
