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
FROM nginx:alpine AS prod
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY dynamic-env.json /usr/share/nginx/html
COPY --from=build /app/dist/CaseEvaluation/browser /usr/share/nginx/html

# ---- dev: build + serve at runtime via dev-entrypoint.sh ----
# No node_modules or source baked in -- they come from the compose bind-mount and
# the named volume. `serve` provides a static SPA server (index.html fallback).
FROM base AS dev
RUN npm install -g serve
ENV NG_CONFIG=docker
COPY dev-entrypoint.sh /app/dev-entrypoint.sh
RUN chmod +x /app/dev-entrypoint.sh
EXPOSE 80
ENTRYPOINT ["/app/dev-entrypoint.sh"]
