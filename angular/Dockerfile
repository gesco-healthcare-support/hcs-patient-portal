# Single multi-stage Dockerfile for the Angular SPA.
#
#   prod (default target): build the SPA, serve the static dist from nginx
#                          (~0.15 GB). Used by CI / deploy.
#   dev:                   yarn + Angular toolchain only. Source and
#                          node_modules arrive at RUNTIME (compose bind-mount +
#                          named volume), so adding a dep never forces an image
#                          rebuild. The entrypoint installs + builds + serves;
#                          iterate with `docker compose restart angular`.
#
# Yarn 4 (Berry) is provided by the committed .yarn/releases binary via corepack.
# Build a specific target with: `docker build --target dev .` (compose sets it).

# ---- base: yarn 4 toolchain + manifests (cacheable) ----
FROM node:20-alpine AS base
WORKDIR /app
RUN corepack enable
COPY .yarnrc.yml package.json yarn.lock ./
COPY .yarn/releases ./.yarn/releases

# ---- deps: immutable install; layer cached unless the manifests change ----
FROM base AS deps
RUN yarn install --immutable

# ---- build: compile the SPA ----
FROM deps AS build
COPY . .
ARG NG_CONFIG=docker
RUN yarn ng build --configuration "${NG_CONFIG}"

# ---- prod (DEFAULT): static nginx image ----
# Digest-pinned because this image SHIPS. The tag stays alongside the digest --
# that pair is the form Dependabot updates; see .github/dependabot.yml for why
# the base/deps/build/dev stages above are deliberately left on tags.
# SonarCloud's docker:S8431 objects to carrying both a tag and a digest; the
# argument for keeping them is with the docker ecosystem entry in that file.
FROM nginx:alpine@sha256:72ba65eb42c10344912a84ff42408db7d34f2feb642204570ab8fc5ffd29f1d3 AS prod
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY dynamic-env.json /usr/share/nginx/html
# In-house hosting (2026-07-09, T4): regenerate dynamic-env.json from env vars at
# container start (nginx sources *.envsh in /docker-entrypoint.d/ before starting),
# so the SAME built image is re-pointable per environment -- prod subdomain-per-
# service base URLs + baseHost. Overwrites the baked dynamic-env.json above.
# --chmod=0755 is required: the nginx entrypoint IGNORES non-executable *.envsh
# files, and Windows git checkouts do not carry the +x bit, so bake it in here.
COPY --chmod=0755 prod-dynamic-env.envsh /docker-entrypoint.d/40-dynamic-env.envsh
COPY --from=build /app/dist/CaseEvaluation/browser /usr/share/nginx/html

# ---- dev: build + serve at runtime via dev-entrypoint.sh ----
# No node_modules or source baked in -- they come from the compose bind-mount and
# the named volume. `serve` provides a static SPA server (index.html fallback).
FROM base AS dev
# Version-pinned for the same reason the CI tool installs are (item 2.2): an
# unpinned global install resolves to whatever is current at build time, so two
# builds of the same commit can differ. Dev-only -- prod is the nginx stage.
#
# `--ignore-scripts` for the OTHER half of that item, which pinning alone does
# not cover (docker:S6505): a lifecycle script in the package or any of its
# dependencies runs as root at build time, so pinning decides WHICH code runs
# while this decides WHETHER arbitrary install-time code runs at all. Item 2.2
# made exactly this change to the markdownlint install in lint-meta.yml; the
# first version of this line pinned the version and left the hole open.
RUN npm install -g --ignore-scripts serve@14.2.6
ENV NG_CONFIG=docker
COPY dev-entrypoint.sh /app/dev-entrypoint.sh
RUN chmod +x /app/dev-entrypoint.sh
EXPOSE 80
ENTRYPOINT ["/app/dev-entrypoint.sh"]
