"""packet-renderer -- HTTP sidecar: packet template + tokens -> fillable PDF.

This service OWNS the packet HTML templates (single source of truth). The pure-Python generators
under /app/generators are run at image build and their self-contained HTML (images base64-inlined)
is baked in; this module loads them at startup. The .NET packet pipeline
(WeasyPrintPacketRenderer) POSTs a template name + the resolved ##Group.Field## token map; this
service substitutes the tokens, renders the fillable PDF via WeasyPrint --pdf-forms + the shared
post_process, and returns it.

Token VALUES are owned by the .NET side (PacketTokenResolver + PacketTokenMap). This service only
does the mechanical substitution + render, so there is no business logic duplicated across the
language boundary.

Routes:
  GET  /health  -> 200 {"status": "ok", "templates": [...]}   compose healthcheck + depends_on gate
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

from flask import Flask, Response, jsonify, request
from weasyprint import HTML

# post_process.py is the SAME module the generators use; it lives in the generators tree so there
# is one copy. Add it to the path before importing.
sys.path.insert(0, "/app/generators/shared")
import post_process  # noqa: E402

logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(message)s")
log = logging.getLogger("packet-renderer")

# Baked at image build (see Dockerfile). Template name -> the generator's emitted HTML file.
_TEMPLATE_FILES = {
    "doctor": "/app/generators/doctor/doctor.html",
    "patient": "/app/generators/patient/patient.html",
    "attorney-ame": "/app/generators/attorney/ame_ime.html",
    "attorney-pqme": "/app/generators/attorney/pqme.html",
}

# Load once at startup; a missing file is a build error and crashes the worker loudly (so the
# healthcheck never goes green on a half-built image).
TEMPLATES = {}
for _name, _path in _TEMPLATE_FILES.items():
    with open(_path, "r", encoding="utf-8") as _fh:
        TEMPLATES[_name] = _fh.read()

# Mirrors PacketTokenMap.TokenRegex on the .NET side: ##Group.Field##.
TOKEN_REGEX = re.compile(r"##[A-Za-z][A-Za-z0-9_]*\.[A-Za-z][A-Za-z0-9_]*##")

app = Flask(__name__)


@app.get("/health")
def health():
    """Liveness probe; also reports the loaded template names for quick diagnosis."""
    return jsonify(status="ok", templates=sorted(TEMPLATES))


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

    template = TEMPLATES.get(payload.get("template"))
    if template is None:
        return jsonify(error=f"unknown template; expected one of {sorted(TEMPLATES)}"), 400

    tokens = payload.get("tokens") or {}
    if not isinstance(tokens, dict):
        return jsonify(error="tokens must be an object of ##Group.Field## -> value"), 400

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
    log.info("rendered %s: %d tokens -> %d bytes PDF", payload.get("template"), len(tokens), len(pdf))
    return Response(pdf, mimetype="application/pdf")


if __name__ == "__main__":
    # Local debugging only; the container runs gunicorn (see Dockerfile CMD).
    app.run(host="0.0.0.0", port=int(os.environ.get("PORT", "3001")))
