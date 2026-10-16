"""Render the four real packets through the real packet-renderer image, and check each PDF.

Runs INSIDE the image, against the service it is running, from .github/workflows/packet-render.yml:

    docker exec -i packet-renderer python - < tests/packet-render/render_real_packets.py
    docker exec -i packet-renderer python - --print < tests/packet-render/render_real_packets.py   (new pins)

WHY IT EXISTS. Nothing else in CI renders a PDF. The golden hashes cover the generators' HTML (the INPUT to
rendering), every Python test of the PDF libraries runs against a stub, and packet-real-pdf.yml runs pikepdf over
a PDF it builds itself. So a WeasyPrint upgrade, a base-image bump or a Debian font change could break every packet
with all of CI green. It nearly happened: WeasyPrint 70 returned HTTP 500 for all four packets (2026-10-12), and a
hand render was the only thing that caught it.

WHAT IT CHECKS, per template the service knows (its own list, imported from app.py, so the two cannot drift):

  - POST /render answers 200 with application/pdf, for a token map that fills EVERY ##Group.Field## token in the
    baked template;
  - the bytes are a PDF that pikepdf, the library the image ships, opens;
  - the page count and the number of fillable boxes (widget annotations) equal the pins below;
  - the PDF's text (pdftotext, from the image's poppler-utils) shows the value literally, holds no ##token## left
    unsubstituted, and no "&amp;" (a value escaped twice).

WHY THE VALUE IS SHORT. The packets FLOW: a longer value wraps and adds pages (measured 2026-10-15: 34 characters
in every token took the doctor packet from 8 pages to 9, 120 characters took the patient packet from 12 to 16). A
ten-character value renders each packet at its DESIGNED length (doctor 8 and patient 12, which are also the
counts their generators report), so a renderer or font change that makes text wrap more shows as an extra page.
It is markup when unescaped and carries & ' " too, so the escaping path is checked, not just run.

A change that moves a pin is a real change to a medical-legal form. Render it, look at it, then update the pin in
the same pull request: run with --print and copy the dict it prints.

Synthetic values only. Never put real patient data in this file.
"""

import io
import json
import subprocess
import sys
import urllib.error
import urllib.request

sys.path.insert(0, "/app")
import app  # noqa: E402  the running service's module: its template list and token pattern
import pikepdf  # noqa: E402

RENDER_URL = "http://127.0.0.1:3001/render"
# Markup when NOT escaped: unescaped, "<i>" becomes a tag and the text loses it, so the value no longer appears.
# A value such as <&'> would not catch that: "<&" is not a tag, so it renders the same either way (measured).
SYNTHETIC_VALUE = "<i>&'\"</i>"

# Pinned from a real render of this commit's image. See the module docstring before changing one.
EXPECTED = {
    "attorney-ame": {"pages": 2, "widgets": 0},
    "attorney-pqme": {"pages": 3, "widgets": 0},
    "doctor": {"pages": 8, "widgets": 1341},
    "patient": {"pages": 12, "widgets": 723},
}


def render(name):
    html = app._TEMPLATE_FILES[name].read_text(encoding="utf-8")
    tokens = dict.fromkeys(sorted(set(app.TOKEN_REGEX.findall(html))), SYNTHETIC_VALUE)
    body = json.dumps({"template": name, "tokens": tokens}).encode("utf-8")
    request = urllib.request.Request(RENDER_URL, data=body, headers={"Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(request, timeout=300) as response:
            return response.status, response.headers.get_content_type(), response.read(), len(tokens)
    except urllib.error.HTTPError as error:
        return error.code, error.headers.get_content_type(), error.read()[:300], len(tokens)


def measure(pdf):
    with pikepdf.open(io.BytesIO(pdf)) as document:
        widgets = sum(1 for page in document.pages for annotation in (page.get("/Annots") or [])
                      if annotation.get("/Subtype") == "/Widget")
        return {"pages": len(document.pages), "widgets": widgets}


def text_problems(pdf):
    result = subprocess.run(["pdftotext", "-q", "-", "-"], input=pdf, capture_output=True, check=False)
    if result.returncode != 0:
        return [f"pdftotext failed with exit {result.returncode}"]
    text = result.stdout.decode("utf-8", "replace")
    problems = []
    if SYNTHETIC_VALUE not in text:
        problems.append("the value does not appear as text: substitution or escaping is broken")
    leftover = app.TOKEN_REGEX.findall(text)
    if leftover:
        problems.append(f"{len(leftover)} tokens left unsubstituted, e.g. {leftover[0]}")
    if "&amp;" in text:
        problems.append("'&amp;' in the text: a value was escaped twice")
    return problems


def check(name):
    """One line for the log, whether this template passed, and what was measured (None if nothing was)."""
    status, content_type, payload, token_count = render(name)
    if status != 200 or content_type != "application/pdf":
        return f"{name}: HTTP {status} {content_type}: {payload!r}", False, None
    if not payload.startswith(b"%PDF-"):
        return f"{name}: the body is not a PDF (starts {payload[:8]!r})", False, None
    try:
        got = measure(payload)
    except pikepdf.PdfError as error:
        return f"{name}: pikepdf cannot open it: {error}", False, None
    problems = text_problems(payload)
    want = EXPECTED.get(name)
    if want != got:
        problems.append(f"want {want}")
    line = f"{name}: {len(payload)} bytes, {token_count} tokens, {got['pages']} pages, {got['widgets']} fillable boxes"
    return f"{line} -- " + ("; ".join(problems) if problems else "OK"), not problems, got


def main(argv):
    printing = "--print" in argv
    results, measured = [], {}
    for name in sorted(app._TEMPLATE_FILES):
        line, passed, got = check(name)
        print(line)
        results.append(passed)
        if got is not None:
            measured[name] = got
    stale = sorted(set(EXPECTED) - set(app._TEMPLATE_FILES))
    if stale:
        print(f"pins for templates the service no longer has: {', '.join(stale)}")
    if printing:
        print("EXPECTED = " + json.dumps(measured, indent=4, sort_keys=True))
    ok = all(results) and not stale
    print("PASS: every packet rendered and matches its pin" if ok else "FAIL: see the lines above")
    return 0 if ok or printing else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
