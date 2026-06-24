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

FROM python:3.12-slim-trixie

# WeasyPrint runtime (Pango / HarfBuzz / fontconfig) + the template font stack + curl (for the
# compose healthcheck) + poppler-utils (in-container PDF inspection while debugging). Debian
# contrib is enabled for the MS Core Fonts installer, whose EULA is accepted
# non-interactively.
RUN sed -i -E 's/^Components: main$/Components: main contrib/' /etc/apt/sources.list.d/debian.sources \
 && apt-get update \
 && apt-get install -y --no-install-recommends \
      libpango-1.0-0 libpangoft2-1.0-0 libharfbuzz0b fontconfig \
      fonts-crosextra-carlito fonts-crosextra-caladea fonts-liberation \
      fonts-noto-core poppler-utils curl \
 && echo "ttf-mscorefonts-installer msttcorefonts/accepted-mscorefonts-eula select true" \
      | debconf-set-selections \
 && apt-get install -y --no-install-recommends ttf-mscorefonts-installer \
 && fc-cache -f \
 && apt-get clean \
 && rm -rf /var/lib/apt/lists/*

# Pinned for reproducibility. weasyprint + pikepdf match the verified template toolchain;
# Flask + gunicorn serve the HTTP surface.
RUN pip install --no-cache-dir \
      weasyprint==69.0 \
      pikepdf==10.7.2 \
      Flask==3.1.0 \
      gunicorn==23.0.0

WORKDIR /app

# The generators (single source of truth) + their image assets + shared post_process.py.
COPY tools/packet-templates /app/generators

# Bake the templates at image build: each generator emits its self-contained HTML next to itself
# (doctor.html, patient.html, attorney emits ame_ime.html + pqme.html). app.py loads these at
# startup. A generator failure fails the build loudly.
RUN cd /app/generators/doctor   && python build_doctor.py \
 && cd /app/generators/patient  && python build_patient.py \
 && cd /app/generators/attorney && python build_attorney.py

COPY docker/packet-renderer/app.py /app/app.py

EXPOSE 3001

# 2 sync workers: a single appointment generates its 3 packet kinds sequentially in one Hangfire
# job, so a small pool is ample for dev. 120s timeout covers the 15-page patient packet on a cold
# fontconfig cache. Bind matches PacketRenderer__Url in docker-compose.yml.
CMD ["gunicorn", "--bind", "0.0.0.0:3001", "--workers", "2", "--timeout", "120", "app:app"]
