# Single multi-stage Dockerfile for the AuthServer (OpenIddict + Razor identity UI).
#   prod (DEFAULT): publish -> aspnet runtime (~0.6 GB). CI / deploy.
#   dev:            SDK image; source bind-mounted at runtime, NuGet in a named
#                   volume; entrypoint restores + runs. Iterate with
#                   `docker compose restart authserver`.
#
# AuthServer is special: the Razor login pages need client-side libs (LeptonX,
# jQuery, Bootstrap) under wwwroot/libs, produced by `abp install-libs`. That dir
# is gitignored, so in dev (where source is bind-mounted) the entrypoint must
# regenerate it -- otherwise the login UI loses its theme.

# ---- node-runtime: the Node binaries the build and dev stages copy ----
# A named stage rather than `COPY --from=<image>` (decision 17): Dependabot's
# docker parser reads only `FROM` lines, so a digest behind a COPY could never
# be refreshed. Nothing from this stage ships; see the note on the build stage.
FROM node:22-bookworm-slim@sha256:43ac6c60b8f89723f746e8a92ce91abd5017e627ce1ddfe4238355d3a30b772c AS node-runtime

# ---- build (sdk): restore + install-libs + publish; feeds prod ----
FROM mcr.microsoft.com/dotnet/sdk:10.0.401@sha256:83e0db97c45d2e39b80123fe42940a23c423405a17f80b608a4b8768033d6392 AS build
WORKDIR /src

# The ABP Commercial feed key reaches the restore step below as a BuildKit
# secret, never as a build ARG: Docker records an ARG value in the metadata of
# every layer of the stage that declares it, so the key stayed readable through
# `docker history --no-trunc` on every machine that had built this image (#703).
# The template itself carries only a placeholder and is safe to COPY.
COPY NuGet.Config.template .
COPY --chmod=0755 docker/write-nuget-config.sh /usr/local/bin/write-nuget-config.sh
COPY --chmod=0755 docker/scrub-nuget-key.sh /usr/local/bin/scrub-nuget-key.sh

COPY Directory.Build.props .
COPY global.json .
COPY BannedSymbols.txt .
COPY HealthcareSupport.CaseEvaluation.slnx .
COPY src/ src/
COPY test/ test/

RUN echo '{}' > src/HealthcareSupport.CaseEvaluation.AuthServer/appsettings.secrets.json \
 && echo '{}' > src/HealthcareSupport.CaseEvaluation.HttpApi.Host/appsettings.secrets.json \
 && echo '{}' > src/HealthcareSupport.CaseEvaluation.DbMigrator/appsettings.secrets.json \
 && echo '{}' > test/HealthcareSupport.CaseEvaluation.TestBase/appsettings.secrets.json \
 && echo '{}' > test/HealthcareSupport.CaseEvaluation.HttpApi.Client.ConsoleTestApp/appsettings.secrets.json

# Render NuGet.Config, restore, and delete it in ONE layer. Deleting it in a
# later RUN would not help -- the layer that created the file still holds it.
# --locked-mode restores exactly the committed packages.lock.json files, as CI
# does, so a lock file that has fallen behind a project file fails here with
# NU1004 instead of building an unrecorded dependency graph. After changing a
# package: `dotnet restore --force-evaluate`, then commit the lock files.
RUN --mount=type=secret,id=abp_nuget_key \
    sh /usr/local/bin/write-nuget-config.sh \
 && { dotnet restore HealthcareSupport.CaseEvaluation.slnx --locked-mode; rc=$?; rm -f NuGet.Config; \
     [ "$rc" -ne 0 ] || sh /usr/local/bin/scrub-nuget-key.sh || rc=$?; exit $rc; }

# Node + ABP CLI to restore client-side libraries (wwwroot/libs is gitignored).
#
# Node arrives by COPY from an official image, NOT from a downloaded script.
# This replaced `curl -fsSL https://deb.nodesource.com/setup_22.x | bash -`
# (Scorecard PinnedDependenciesID / downloadThenRun): that fetched a script from
# a third party and executed it AS ROOT at build time. Nothing here ships to the
# prod stage -- it is `FROM aspnet` and copies only /app/publish -- but this
# stage produces what does ship, so a compromised script could tamper with
# wwwroot/libs or the publish output. Build integrity, not runtime surface.
#
# node:22-bookworm-slim is glibc 2.36; this SDK image is Ubuntu 24.04 / glibc
# 2.39. glibc is forward-compatible, so a binary built against 2.36 runs on
# 2.39 -- the reverse would not hold, so do not "upgrade" this to a trixie or
# noble node tag without re-checking.
#
# Verified before shipping: this produces node v22.23.2 / npm 10.9.8, byte-identical
# to what the setup script installed, so `abp install-libs` sees the same toolchain.
COPY --from=node-runtime /usr/local/bin/node /usr/local/bin/node
COPY --from=node-runtime /usr/local/lib/node_modules /usr/local/lib/node_modules
RUN ln -sf /usr/local/lib/node_modules/npm/bin/npm-cli.js /usr/local/bin/npm \
    && ln -sf /usr/local/lib/node_modules/npm/bin/npx-cli.js /usr/local/bin/npx
RUN dotnet tool install -g Volo.Abp.Cli --version 10.0.2
ENV PATH="$PATH:/root/.dotnet/tools"
WORKDIR /src/src/HealthcareSupport.CaseEvaluation.AuthServer
RUN abp install-libs
WORKDIR /src

RUN dotnet publish src/HealthcareSupport.CaseEvaluation.AuthServer/HealthcareSupport.CaseEvaluation.AuthServer.csproj \
    -c Release -o /app/publish --no-restore

# ---- dev (sdk): bind-mounted source; install-libs + restore + run on start ----
FROM mcr.microsoft.com/dotnet/sdk:10.0.401@sha256:83e0db97c45d2e39b80123fe42940a23c423405a17f80b608a4b8768033d6392 AS dev
WORKDIR /app

# Redirect ALL build output (obj/bin) to a container-local path via .NET's
# artifacts output, so concurrent api/authserver builds on the shared
# bind-mounted ./src never collide and no Linux artifacts leak into the host
# tree. (MSBuild reads env vars as properties; proven on SDK 10.0.301.)
ENV UseArtifactsOutput=true
ENV ArtifactsPath=/artifacts

# The ABP Commercial feed key reaches the restore step below as a BuildKit
# secret, never as a build ARG: Docker records an ARG value in the metadata of
# every layer of the stage that declares it, so the key stayed readable through
# `docker history --no-trunc` on every machine that had built this image (#703).
# The template itself carries only a placeholder and is safe to COPY.
COPY NuGet.Config.template .
COPY --chmod=0755 docker/write-nuget-config.sh /usr/local/bin/write-nuget-config.sh
COPY --chmod=0755 docker/scrub-nuget-key.sh /usr/local/bin/scrub-nuget-key.sh

COPY Directory.Build.props .
COPY global.json .
COPY BannedSymbols.txt .
COPY HealthcareSupport.CaseEvaluation.slnx .
COPY src/ src/
COPY test/ test/

RUN echo '{}' > src/HealthcareSupport.CaseEvaluation.AuthServer/appsettings.secrets.json \
 && echo '{}' > src/HealthcareSupport.CaseEvaluation.HttpApi.Host/appsettings.secrets.json \
 && echo '{}' > src/HealthcareSupport.CaseEvaluation.DbMigrator/appsettings.secrets.json \
 && echo '{}' > test/HealthcareSupport.CaseEvaluation.TestBase/appsettings.secrets.json \
 && echo '{}' > test/HealthcareSupport.CaseEvaluation.HttpApi.Client.ConsoleTestApp/appsettings.secrets.json

# Render NuGet.Config, restore, and delete it in ONE layer. Deleting it in a
# later RUN would not help -- the layer that created the file still holds it.
# --locked-mode restores exactly the committed packages.lock.json files, as CI
# does, so a lock file that has fallen behind a project file fails here with
# NU1004 instead of building an unrecorded dependency graph. After changing a
# package: `dotnet restore --force-evaluate`, then commit the lock files.
RUN --mount=type=secret,id=abp_nuget_key \
    sh /usr/local/bin/write-nuget-config.sh \
 && { dotnet restore HealthcareSupport.CaseEvaluation.slnx --locked-mode; rc=$?; rm -f NuGet.Config; \
     [ "$rc" -ne 0 ] || sh /usr/local/bin/scrub-nuget-key.sh || rc=$?; exit $rc; }
RUN dotnet build src/HealthcareSupport.CaseEvaluation.AuthServer/HealthcareSupport.CaseEvaluation.AuthServer.csproj \
    -c Debug --no-restore

# Node + ABP CLI to restore client-side libs (LeptonX/jQuery/Bootstrap) into
# wwwroot/libs at BUILD time -- baked into the image (fast Linux fs). At runtime
# a compose anonymous volume re-seeds wwwroot/libs from this baked copy, so the
# ./src bind-mount cannot hide it AND we avoid a slow install-libs writing
# thousands of small files into a Windows bind-mount on every start.
#
# Node by COPY rather than a piped setup script -- see the note on the build
# stage for the reasoning and the glibc constraint.
COPY --from=node-runtime /usr/local/bin/node /usr/local/bin/node
COPY --from=node-runtime /usr/local/lib/node_modules /usr/local/lib/node_modules
RUN ln -sf /usr/local/lib/node_modules/npm/bin/npm-cli.js /usr/local/bin/npm \
    && ln -sf /usr/local/lib/node_modules/npm/bin/npx-cli.js /usr/local/bin/npx
RUN dotnet tool install -g Volo.Abp.Cli --version 10.0.2
ENV PATH="$PATH:/root/.dotnet/tools"
WORKDIR /app/src/HealthcareSupport.CaseEvaluation.AuthServer
RUN abp install-libs
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Restore + run from the bind-mounted source. wwwroot/libs comes from the baked
# image via the compose anonymous volume; build output is in /artifacts so the
# host src/**/obj is ignored (no Windows-path poison).
# The key is needed again at RUN time, not only at build time: the package
# cache is a named volume that shadows the one baked above, so a fresh volume
# restores from the private feed. It arrives as a Compose service secret and
# the rendered NuGet.Config is deleted before the app is exec'd (#703).
#
# The listen address comes from ASPNETCORE_URLS above (and again from compose),
# the same way the prod stage has always got it. Passing --urls here as well
# was redundant, and put a clear-text URL literal in a position the Docker
# analyser parses (docker:S5332); the old sh -c form merely hid it.
COPY --chmod=0755 docker/dev-restore-run.sh /usr/local/bin/dev-restore-run.sh
ENTRYPOINT ["/usr/local/bin/dev-restore-run.sh", "src/HealthcareSupport.CaseEvaluation.AuthServer/HealthcareSupport.CaseEvaluation.AuthServer.csproj"]

# ---- prod (DEFAULT): small aspnet runtime image ----
# Digest-pinned because this image SHIPS. The tag stays alongside the digest --
# that pair is the form Dependabot updates; see .github/dependabot.yml. Since
# 2026-09-25 (decision 17) the sdk build/dev stages above are pinned the same way.
# SonarCloud's docker:S8431 objects to carrying both a tag and a digest; the
# argument for keeping them is with the docker ecosystem entry in that file.
FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:a4556ed033fa96f984bb7a8d348851cb2d36b1281dd2420070045f664fbb5f94 AS prod
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build --chown=app:app /app/publish .
# NON-ROOT RUNTIME (#701), as in the HttpApi.Host and DbMigrator prod stages;
# the reasoning is written out in full in the HttpApi.Host Dockerfile. `app`
# (uid 1654, gid 1654) is pre-created by the Microsoft runtime image. `Logs/` is
# created and chowned because Serilog's file sink writes relative to WORKDIR, /app
# is root-owned after COPY, and Serilog drops lines silently when it cannot open
# the file.
#
# One requirement specific to this service: the token-signing certificate is
# bind-mounted read-only at /app/openiddict.pfx, and this process reads it through
# its group. The file must be mode 640, group 1654, or the service fails at
# startup. scripts/hosting/gen-openiddict-cert.sh sets that when it can; an
# existing certificate needs it done once on the host -- see
# docs/security/SECRETS-MANAGEMENT.md.
RUN mkdir -p /app/Logs && chown app:app /app/Logs
USER app

# Do NOT add empty-stub source dirs: ABP's ReplaceEmbeddedByPhysical replaces
# (not merges) the embedded localization fileset when the physical path exists.
# The AuthServer module guards each replace with Directory.Exists + empty-check,
# so missing host source dirs fall back to the embedded fileset baked in the DLL.
ENTRYPOINT ["dotnet", "HealthcareSupport.CaseEvaluation.AuthServer.dll"]
