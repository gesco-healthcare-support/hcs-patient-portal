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

# ---- build (sdk): restore + install-libs + publish; feeds prod ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG ABP_NUGET_API_KEY
WORKDIR /src

COPY NuGet.Config.template .
RUN sed "s/\${ABP_NUGET_API_KEY}/$ABP_NUGET_API_KEY/" NuGet.Config.template > NuGet.Config

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

RUN dotnet restore HealthcareSupport.CaseEvaluation.slnx

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
COPY --from=node:22-bookworm-slim /usr/local/bin/node /usr/local/bin/node
COPY --from=node:22-bookworm-slim /usr/local/lib/node_modules /usr/local/lib/node_modules
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
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dev
ARG ABP_NUGET_API_KEY
WORKDIR /app

# Redirect ALL build output (obj/bin) to a container-local path via .NET's
# artifacts output, so concurrent api/authserver builds on the shared
# bind-mounted ./src never collide and no Linux artifacts leak into the host
# tree. (MSBuild reads env vars as properties; proven on SDK 10.0.301.)
ENV UseArtifactsOutput=true
ENV ArtifactsPath=/artifacts

COPY NuGet.Config.template .
RUN sed "s/\${ABP_NUGET_API_KEY}/$ABP_NUGET_API_KEY/" NuGet.Config.template > NuGet.Config

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

RUN dotnet restore HealthcareSupport.CaseEvaluation.slnx
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
COPY --from=node:22-bookworm-slim /usr/local/bin/node /usr/local/bin/node
COPY --from=node:22-bookworm-slim /usr/local/lib/node_modules /usr/local/lib/node_modules
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
ENTRYPOINT ["sh", "-c", "\
  dotnet restore HealthcareSupport.CaseEvaluation.slnx >/tmp/restore.log 2>&1 || (cat /tmp/restore.log; exit 1); \
  exec dotnet run --no-launch-profile --no-restore --project src/HealthcareSupport.CaseEvaluation.AuthServer/HealthcareSupport.CaseEvaluation.AuthServer.csproj --urls http://+:8080"]

# ---- prod (DEFAULT): small aspnet runtime image ----
# Digest-pinned because this image SHIPS. The tag stays alongside the digest --
# that pair is the form Dependabot updates; see .github/dependabot.yml for why
# the sdk build/dev stages above are deliberately left on tags.
FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:a4556ed033fa96f984bb7a8d348851cb2d36b1281dd2420070045f664fbb5f94 AS prod
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .

# Do NOT add empty-stub source dirs: ABP's ReplaceEmbeddedByPhysical replaces
# (not merges) the embedded localization fileset when the physical path exists.
# The AuthServer module guards each replace with Directory.Exists + empty-check,
# so missing host source dirs fall back to the embedded fileset baked in the DLL.
ENTRYPOINT ["dotnet", "HealthcareSupport.CaseEvaluation.AuthServer.dll"]
