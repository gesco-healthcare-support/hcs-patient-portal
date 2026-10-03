# packet-renderer sidecar -- WeasyPrint template + tokens -> fillable PDF (Phase 1, 2026-06-05).
#
# Single source of truth for the packet HTML templates: the pure-Python generators under
# tools/packet-templates are COPIED in and RUN at image build, baking their self-contained HTML
# (images base64-inlined) into the image. There is NO committed HTML snapshot in the repo -- the
# generators (.py) are the only source, regenerated on every image build, so nothing can drift.
#
# The .NET side embeds no HTML: it POSTs {template, tokens} and this service substitutes the
# tokens, renders via WeasyPrint --pdf-forms + the shared post_process, and returns the PDF.
#
# Versions are pinned to the toolchain the generators were verified against (WeasyPrint 69.0,
# pikepdf 10.7.2, Python 3.12). The font set is IDENTICAL to that toolchain so rendering is
# byte-for-byte the same as the reviewed templates: free metric-compatible faces (Carlito=Calibri,
# Caladea=Cambria, Liberation=Arial/Times) the templates target first, plus ttf-mscorefonts for the
# few patient pages (release / privacy / pregnancy) that name Arial / Times New Roman directly.

# Digest-pinned because this image SHIPS. The tag stays alongside the digest --
# that pair is the form Dependabot updates; see .github/dependabot.yml.
# SonarCloud's docker:S8431 objects to carrying both a tag and a digest; the
# argument for keeping them is with the docker ecosystem entry in that file.
FROM python:3.12-slim-trixie@sha256:78387bc3881b8273120a12ebe6c1ab22b018ccc2c9adf565ae1ac9b536e184ea

# WeasyPrint runtime (Pango / HarfBuzz / fontconfig) + the template font stack + curl (for the
# compose healthcheck) + poppler-utils (in-container PDF inspection while debugging). Debian
# contrib is enabled for the MS Core Fonts installer, whose EULA is accepted
# non-interactively.
# docker:S7031 wants this merged with the pip RUN below. Declined (#817): they
# change for unrelated reasons and at different rates. Merging means a version
# bump in the pip list re-runs the whole apt layer including the mscorefonts
# download, on every build, and buys almost no size back -- both RUNs already
# clean up after themselves (apt-get clean + rm -rf lists here, --no-cache-dir
# there). The same argument the generator RUNs below are kept separate for.
RUN sed -i -E 's/^Components: main$/Components: main contrib/' /etc/apt/sources.list.d/debian.sources \
 && apt-get update \
 && apt-get install -y --no-install-recommends \
      curl fontconfig fonts-crosextra-caladea fonts-crosextra-carlito \
      fonts-liberation fonts-noto-core libharfbuzz0b libpango-1.0-0 \
      libpangoft2-1.0-0 poppler-utils \
 && echo "ttf-mscorefonts-installer msttcorefonts/accepted-mscorefonts-eula select true" \
      | debconf-set-selections \
 && apt-get install -y --no-install-recommends ttf-mscorefonts-installer \
 && fc-cache -f \
 && apt-get clean \
 && rm -rf /var/lib/apt/lists/*

# weasyprint + pikepdf match the verified template toolchain; Flask + gunicorn serve
# the HTTP surface. The four top-level pins live in requirements.in; requirements.txt
# is compiled from it by pip-compile and carries every transitive package with a hash.
#
# HASH-pinned since decision 18 (B8), which reverses item 2.14 (2026-09-04). 2.14
# declined hashes because PyPI will not re-publish a version with different content.
# That still leaves a registry or mirror compromise, and this image renders documents
# carrying patient data, so it now verifies every wheel against a recorded hash. The
# cost 2.14 named -- regenerating the whole file on every bump -- is what Dependabot's
# pip entry now does. CI's own pip installs stay hash-free (decision d36); only this
# image carries hashes.
#
# --require-hashes: a wheel whose hash is not in the file fails the build, and so
# does any package the file does not list.
#
# --only-binary :all: (S8541, 2026-09-14): refuse source distributions. An sdist
# runs its own setup.py at install time -- arbitrary code executing during the
# image build with the build context present, the same class as the yarn lifecycle
# scripts disabled in #870. Every package here publishes wheels, so this costs
# nothing and closes that path. If a future bump ever lacks a wheel the build fails
# loudly rather than quietly running someone's setup.py.
#
# To change a version: edit requirements.in, then recompile INSIDE this image (the
# digest on the FROM line above), so the resolver sees the same Python and platform:
#   docker run --rm -v "<repo>/docker/packet-renderer:/w" -w /w python:3.12-slim-trixie@<digest> \
#     sh -c "pip install pip-tools==7.5.3 && pip-compile --generate-hashes --allow-unsafe --strip-extras -o requirements.txt requirements.in"
COPY docker/packet-renderer/requirements.txt /tmp/requirements.txt
RUN pip install --no-cache-dir --require-hashes --only-binary :all: -r /tmp/requirements.txt \
 && rm /tmp/requirements.txt

WORKDIR /app

# The generators (single source of truth) + their image assets + shared post_process.py.
COPY tools/packet-templates /app/generators

# Bake the templates at image build: each generator emits its self-contained HTML next to itself
# (doctor.html, patient.html, attorney emits ame_ime.html + pqme.html). app.py loads these at
# startup. A generator failure fails the build loudly.
# WORKDIR rather than `cd` (S6597). Each generator still fails the build loudly;
# splitting them also means a failure names WHICH generator broke instead of
# reporting one chained RUN.
WORKDIR /app/generators/doctor
RUN python build_doctor.py
WORKDIR /app/generators/patient
RUN python build_patient.py
WORKDIR /app/generators/attorney
RUN python build_attorney.py
WORKDIR /app

COPY docker/packet-renderer/app.py /app/app.py

EXPOSE 3001

# 2 sync workers: a single appointment generates its 3 packet kinds sequentially in one Hangfire
# job, so a small pool is ample for dev. 120s timeout covers the 15-page patient packet on a cold
# fontconfig cache. Bind matches PacketRenderer__Url in docker-compose.yml.
# NON-ROOT RUNTIME (#701), and this is the image that most needed it: the
# service parses HTML and renders PDFs through WeasyPrint, so it processes
# structured input from elsewhere in the system -- exactly the profile where a
# parser bug becomes code execution. It ran as uid 0.
#
# Created here rather than reused: python:slim ships no non-root account, unlike
# the .NET runtime images which pre-create `app`.
#
# HOME is real (--create-home) on purpose. fontconfig and WeasyPrint want a
# writable cache; with no home they fall back to paths a non-root user cannot
# write and emit cache warnings on every render.
#
# Nothing is chowned. The generators ran at BUILD time as root and their HTML is
# read-only at runtime, port 3001 is above 1024 so it needs no capability, and
# the service writes no files -- it returns the PDF in the response body. If it
# ever gains a scratch directory, that directory needs chowning to appuser.
RUN useradd --system --create-home --uid 1001 --shell /usr/sbin/nologin appuser
USER appuser

CMD ["gunicorn", "--bind", "0.0.0.0:3001", "--workers", "2", "--timeout", "120", "app:app"]
