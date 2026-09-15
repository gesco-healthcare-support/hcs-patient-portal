# Single multi-stage Dockerfile for the HttpApi.Host.
#   prod (DEFAULT): publish -> aspnet runtime image (~0.6 GB). CI / deploy.
#   dev:            SDK image; source is bind-mounted at runtime and NuGet lives
#                   in a named volume, so editing C# means
#                   `docker compose restart api` (no image rebuild). No
#                   `dotnet watch` -- Docker Desktop's bind-mount/inotify bridge
#                   on Windows drops events (OBS-22; GH #8694/#15171).

# ---- build (sdk): restore + publish; feeds the prod stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# The ABP Commercial feed key reaches the restore step below as a BuildKit
# secret, never as a build ARG: Docker records an ARG value in the metadata of
# every layer of the stage that declares it, so the key stayed readable through
# `docker history --no-trunc` on every machine that had built this image (#703).
# The template itself carries only a placeholder and is safe to COPY.
COPY NuGet.Config.template .
COPY --chmod=0755 docker/write-nuget-config.sh /usr/local/bin/write-nuget-config.sh

COPY Directory.Build.props .
COPY BannedSymbols.txt .
COPY HealthcareSupport.CaseEvaluation.slnx .
COPY src/ src/
COPY test/ test/

# Placeholder secrets files excluded by .dockerignore (ABP .csproj references them)
RUN echo '{}' > src/HealthcareSupport.CaseEvaluation.AuthServer/appsettings.secrets.json \
 && echo '{}' > src/HealthcareSupport.CaseEvaluation.HttpApi.Host/appsettings.secrets.json \
 && echo '{}' > src/HealthcareSupport.CaseEvaluation.DbMigrator/appsettings.secrets.json \
 && echo '{}' > test/HealthcareSupport.CaseEvaluation.TestBase/appsettings.secrets.json \
 && echo '{}' > test/HealthcareSupport.CaseEvaluation.HttpApi.Client.ConsoleTestApp/appsettings.secrets.json

# Render NuGet.Config, restore, and delete it in ONE layer. Deleting it in a
# later RUN would not help -- the layer that created the file still holds it.
RUN --mount=type=secret,id=abp_nuget_key \
    sh /usr/local/bin/write-nuget-config.sh \
 && { dotnet restore HealthcareSupport.CaseEvaluation.slnx; rc=$?; rm -f NuGet.Config; exit $rc; }
RUN dotnet publish src/HealthcareSupport.CaseEvaluation.HttpApi.Host/HealthcareSupport.CaseEvaluation.HttpApi.Host.csproj \
    -c Release -o /app/publish --no-restore

# ---- dev (sdk): bind-mounted source, restore + run on container start ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dev
WORKDIR /app

# Redirect ALL build output (obj/bin) to a container-local path via .NET's
# artifacts output. Each dev container has its own /artifacts, so concurrent
# api/authserver builds on the shared bind-mounted ./src never collide, and no
# Linux build artifacts leak into the host src tree. (MSBuild reads env vars as
# properties; proven on SDK 10.0.301.)
ENV UseArtifactsOutput=true
ENV ArtifactsPath=/artifacts

# The ABP Commercial feed key reaches the restore step below as a BuildKit
# secret, never as a build ARG: Docker records an ARG value in the metadata of
# every layer of the stage that declares it, so the key stayed readable through
# `docker history --no-trunc` on every machine that had built this image (#703).
# The template itself carries only a placeholder and is safe to COPY.
COPY NuGet.Config.template .
COPY --chmod=0755 docker/write-nuget-config.sh /usr/local/bin/write-nuget-config.sh

COPY Directory.Build.props .
COPY BannedSymbols.txt .
COPY HealthcareSupport.CaseEvaluation.slnx .
COPY src/ src/
COPY test/ test/

RUN echo '{}' > src/HealthcareSupport.CaseEvaluation.AuthServer/appsettings.secrets.json \
 && echo '{}' > src/HealthcareSupport.CaseEvaluation.HttpApi.Host/appsettings.secrets.json \
 && echo '{}' > src/HealthcareSupport.CaseEvaluation.DbMigrator/appsettings.secrets.json \
 && echo '{}' > test/HealthcareSupport.CaseEvaluation.TestBase/appsettings.secrets.json \
 && echo '{}' > test/HealthcareSupport.CaseEvaluation.HttpApi.Client.ConsoleTestApp/appsettings.secrets.json

# Warm the cache (into /artifacts) so the first container start is fast; the
# entrypoint re-restores from the bind-mounted source to pick up csproj changes.
# Render NuGet.Config, restore, and delete it in ONE layer. Deleting it in a
# later RUN would not help -- the layer that created the file still holds it.
RUN --mount=type=secret,id=abp_nuget_key \
    sh /usr/local/bin/write-nuget-config.sh \
 && { dotnet restore HealthcareSupport.CaseEvaluation.slnx; rc=$?; rm -f NuGet.Config; exit $rc; }
RUN dotnet build src/HealthcareSupport.CaseEvaluation.HttpApi.Host/HealthcareSupport.CaseEvaluation.HttpApi.Host.csproj \
    -c Debug --no-restore

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Output lands in /artifacts, so the bind-mounted host src/**/obj is ignored --
# no Windows-path poison, no wipe needed. Restore + run from the live source.
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
ENTRYPOINT ["/usr/local/bin/dev-restore-run.sh", "src/HealthcareSupport.CaseEvaluation.HttpApi.Host/HealthcareSupport.CaseEvaluation.HttpApi.Host.csproj"]

# ---- prod (DEFAULT): small aspnet runtime image ----
# Digest-pinned because this image SHIPS. The tag stays alongside the digest --
# that pair is the form Dependabot updates; see .github/dependabot.yml for why
# the sdk build/dev stages above are deliberately left on tags.
# SonarCloud's docker:S8431 objects to carrying both a tag and a digest; the
# argument for keeping them is with the docker ecosystem entry in that file.
FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:a4556ed033fa96f984bb7a8d348851cb2d36b1281dd2420070045f664fbb5f94 AS prod
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build --chown=app:app /app/publish .
# NON-ROOT RUNTIME (#701). Every image in this repo ran its process as uid 0,
# including all four that ship. Root in a container is not a vulnerability by
# itself -- it is a privilege-escalation multiplier: any RCE, path traversal or
# arbitrary-write bug executes as uid 0 inside the container, and a container
# escape then starts from root rather than from an unprivileged account. This
# matters now because the portal is moving from an internal LAN VM to public
# exposure, where the attack surface no longer requires already being inside the
# corporate network.
#
# `app` (uid 1654) is PRE-CREATED by the Microsoft runtime image rather than
# invented here, so there is no useradd and no uid to keep in step.
#
# The build stages above deliberately stay root: they install packages and
# restore into system paths, and they never ship.
#
# `Logs/` is created and chowned rather than left to the app: Serilog's file
# sink writes to a path relative to WORKDIR, /app is owned by root after COPY,
# and a non-root process would fail to open it. Serilog does not throw on a
# failed sink -- it drops the log line -- so getting this wrong produces a
# container that runs perfectly and silently writes no logs at all.
RUN mkdir -p /app/Logs && chown app:app /app/Logs
USER app

# Do NOT add empty-stub source dirs: ABP's ReplaceEmbeddedByPhysical fully
# replaces (not merges) the embedded localization fileset when the physical path
# exists. The HttpApiHost module guards each replace with Directory.Exists, so
# missing host source dirs cleanly fall back to embedded resources.
ENTRYPOINT ["dotnet", "HealthcareSupport.CaseEvaluation.HttpApi.Host.dll"]
