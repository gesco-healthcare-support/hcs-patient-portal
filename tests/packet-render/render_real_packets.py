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

import base64
import io
import json
import os
import struct
import subprocess
import sys
import urllib.error
import urllib.request
import zlib

sys.path.insert(0, "/app")
import app  # noqa: E402  the running service's module: its template list and token pattern
import pikepdf  # noqa: E402

RENDER_URL = "http://127.0.0.1:3001/render"
# Markup when NOT escaped: unescaped, "<i>" becomes a tag and the text loses it, so the value no longer appears.
# A value such as <&'> would not catch that: "<&" is not a tag, so it renders the same either way (measured).
SYNTHETIC_VALUE = "<i>&'\"</i>"

SIGNATURE_TOKEN = "##Office.SignatureImage##"


def synthetic_signature_data_uri():
    """A tiny SYNTHETIC png (a coloured diagonal on transparency), as the data URI the .NET side sends.

    Never a real signature: the real one is never committed or used by a test.
    """
    width, height = 40, 16
    rows = b"".join(
        b"\x00" + b"".join((b"\xd0\x20\x20\xff" if x == y * 2 or x == y * 2 + 1 else b"\x00\x00\x00\x00")
                           for x in range(width))
        for y in range(height))

    def chunk(kind, data):
        return (struct.pack(">I", len(data)) + kind + data
                + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF))

    png = (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
           + chunk(b"IDAT", zlib.compress(rows)) + chunk(b"IEND", b""))
    return "data:image/png;base64," + base64.b64encode(png).decode("ascii")


# Pinned from a real render of this commit's image. See the module docstring before changing one.
EXPECTED = {
    "attorney-ame": {"pages": 2, "widgets": 0},
    "attorney-pqme": {"pages": 3, "widgets": 0},
    "doctor": {"pages": 8, "widgets": 1341},
    "doctor-blank": {"pages": 8, "widgets": 1360},
    "patient": {"pages": 12, "widgets": 723},
    "patient-blank": {"pages": 12, "widgets": 740},
}

# The Case Tracker fills these packets BY FIELD NAME, so a name that changes or disappears breaks its filler with
# nothing else going red. This file is the committed list, one "<template><TAB><field name>" per line, and the check
# compares it with the fields of the REAL rendered PDF in both directions. After a deliberate change, regenerate it:
#   docker exec -i packet-renderer python - --print-names < tests/packet-render/render_real_packets.py > tests/packet-render/field-names.txt
# The workflow copies it into the container and names it in FIELD_NAMES_PATH (docker exec -e), because stdin is the
# script, so the file cannot be read from the repository. There is no default location: an unset variable fails the check.
FIELD_NAMES_PATH = os.environ.get("FIELD_NAMES_PATH")
NAMED_TEMPLATES = ("doctor", "doctor-blank", "patient", "patient-blank")
# The blank copy is the sent packet with its per-case values turned into fields, so it must keep every name the sent
# one has: the Case Tracker fills a blank with the names it already knows.
BLANK_OF = {"doctor-blank": "doctor", "patient-blank": "patient"}


def render(name, signature=None):
    """POST the template with every token filled. signature: None = a synthetic png, "" = the office has none."""
    html = app._TEMPLATE_FILES[name].read_text(encoding="utf-8")
    tokens = dict.fromkeys(sorted(set(app.TOKEN_REGEX.findall(html))), SYNTHETIC_VALUE)
    if SIGNATURE_TOKEN in tokens:
        tokens[SIGNATURE_TOKEN] = synthetic_signature_data_uri() if signature is None else signature
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


def _walk_fields(fields, prefix):
    """Fully-qualified names of the terminal fields. A radio group is one field: its kids carry no /T of their own."""
    for field in fields:
        title = field.get("/T")
        name = prefix + str(title) if title is not None else prefix
        kids = field.get("/Kids")
        if kids is not None and any(kid.get("/T") is not None for kid in kids):
            yield from _walk_fields(kids, name + ".")
        elif title is not None:
            yield name


def field_names(pdf):
    """Sorted, de-duplicated fully-qualified field names of a PDF."""
    with pikepdf.open(io.BytesIO(pdf)) as document:
        acroform = document.Root.get("/AcroForm")
        return sorted(set(_walk_fields(acroform.get("/Fields", []) if acroform is not None else [], "")))


def read_pinned_names():
    """template -> pinned names, from the committed file; None when the workflow did not copy it in."""
    if not FIELD_NAMES_PATH:
        return None
    try:
        with open(FIELD_NAMES_PATH, encoding="utf-8") as handle:
            lines = [line.rstrip(chr(10)) for line in handle if line.strip()]
    except OSError:
        return None
    pinned = {}
    for line in lines:
        template, _, name = line.partition(chr(9))
        pinned.setdefault(template, []).append(name)
    return pinned


def helvetica_problems(name, payload):
    """Only the blank copies draw their text fields with the standard Helvetica; the packets that are emailed must not."""
    if name not in NAMED_TEMPLATES:
        return []
    with pikepdf.open(io.BytesIO(payload)) as document:
        text_fields = [f for f in document.Root.AcroForm.Fields if f.get("/FT") == "/Tx"]
        helv = sum(1 for f in text_fields if "/Helv " in str(f.get("/DA", "")))
    if name in BLANK_OF:
        return [] if text_fields and helv == len(text_fields) else [f"{helv} of {len(text_fields)} text fields use /Helv"]
    return [] if helv == 0 else [f"{helv} text fields of a SENT packet use /Helv"]


def name_problems(name, payload, pinned, rendered_names):
    """A field name that is no longer there, or a new one nobody pinned. Only the templates the Case Tracker fills."""
    if name not in NAMED_TEMPLATES:
        return []
    names = field_names(payload)
    rendered_names[name] = names
    if pinned is None:
        return ["FIELD_NAMES_PATH is unset or unreadable: the workflow must copy tests/packet-render/field-names.txt in and name it"]
    want = pinned.get(name, [])
    problems = []
    missing, added = sorted(set(want) - set(names)), sorted(set(names) - set(want))
    if missing:
        problems.append(f"{len(missing)} pinned field names are gone, e.g. {missing[0]}")
    if added:
        problems.append(f"{len(added)} field names are not pinned, e.g. {added[0]}")
    if name in BLANK_OF:
        sent = pinned.get(BLANK_OF[name], [])
        lost = sorted(set(sent) - set(names))
        if lost:
            problems.append(f"the blank copy dropped {len(lost)} names of the sent packet, e.g. {lost[0]}")
    return problems


def images_on_page(pdf, index):
    with pikepdf.open(io.BytesIO(pdf)) as document:
        return len(document.pages[index].images)


def signature_problems(name, payload):
    """The QME Form 110 page (page 3 of the PQME attorney packet) carries the image, and only when one is sent."""
    if name != "attorney-pqme":
        return []
    problems = []
    if images_on_page(payload, 2) < 1:
        problems.append("the synthetic signature did not reach page 3 of the PDF")
    status, content_type, blank, _ = render(name, signature="")
    if status != 200 or content_type != "application/pdf":
        problems.append(f"the render with no signature answered HTTP {status}")
    else:
        got = measure(blank)
        if got != EXPECTED[name]:
            problems.append(f"with no signature: {got}, want {EXPECTED[name]}")
        if images_on_page(blank, 2) != 0:
            problems.append("an office with no signature must print no image on page 3")
    return problems


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


def check(name, pinned, rendered_names):
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
    problems = text_problems(payload) + signature_problems(name, payload) + name_problems(
        name, payload, pinned, rendered_names) + helvetica_problems(name, payload)
    want = EXPECTED.get(name)
    if want != got:
        problems.append(f"want {want}")
    line = f"{name}: {len(payload)} bytes, {token_count} tokens, {got['pages']} pages, {got['widgets']} fillable boxes"
    return f"{line} -- " + ("; ".join(problems) if problems else "OK"), not problems, got


def check_all(pinned, rendered_names, log):
    """Check every template the service knows; returns each pass/fail and what was measured."""
    results, measured = [], {}
    for name in sorted(app._TEMPLATE_FILES):
        line, passed, got = check(name, pinned, rendered_names)
        print(line, file=log)
        results.append(passed)
        if got is not None:
            measured[name] = got
    return results, measured


def print_pinned_names(rendered_names):
    """The field-names.txt content: one "<template><TAB><name>" per line."""
    for template in NAMED_TEMPLATES:
        for field in rendered_names.get(template, []):
            print(f"{template}{chr(9)}{field}")


def main(argv):
    printing = "--print" in argv
    printing_names = "--print-names" in argv
    rendered_names = {}
    results, measured = check_all(read_pinned_names(), rendered_names, sys.stderr if printing_names else sys.stdout)
    if printing_names:
        print_pinned_names(rendered_names)
        return 0
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
