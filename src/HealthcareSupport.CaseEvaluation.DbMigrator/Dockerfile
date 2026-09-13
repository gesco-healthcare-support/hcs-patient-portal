# Single multi-stage Dockerfile for the DbMigrator (one-shot console: applies EF
# migrations + runs seed contributors, then exits).
#   prod (DEFAULT): publish -> aspnet runtime image. CI / deploy.
#   dev:            SDK image; source bind-mounted at runtime so new migrations /
#                   seeds run from source. Re-run with
#                   `docker compose up db-migrator` (it exits on completion).

# ---- build (sdk): restore + publish; feeds prod ----
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
RUN dotnet publish src/HealthcareSupport.CaseEvaluation.DbMigrator/HealthcareSupport.CaseEvaluation.DbMigrator.csproj \
    -c Release -o /app/publish --no-restore

# ---- dev (sdk): bind-mounted source; restore + run on start ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dev
ARG ABP_NUGET_API_KEY
WORKDIR /app

# Redirect ALL build output (obj/bin) to a container-local path via .NET's
# artifacts output (see api/authserver Dockerfiles). Keeps the bind-mounted
# host src tree free of Linux build artifacts.
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
RUN dotnet build src/HealthcareSupport.CaseEvaluation.DbMigrator/HealthcareSupport.CaseEvaluation.DbMigrator.csproj \
    -c Debug --no-restore

ENTRYPOINT ["sh", "-c", "\
  dotnet restore HealthcareSupport.CaseEvaluation.slnx >/tmp/restore.log 2>&1 || (cat /tmp/restore.log; exit 1); \
  exec dotnet run --no-launch-profile --no-restore --project src/HealthcareSupport.CaseEvaluation.DbMigrator/HealthcareSupport.CaseEvaluation.DbMigrator.csproj"]

# ---- prod (DEFAULT): small aspnet runtime image ----
# Digest-pinned because this image SHIPS. The tag stays alongside the digest --
# that pair is the form Dependabot updates; see .github/dependabot.yml for why
# the sdk build/dev stages above are deliberately left on tags.
# SonarCloud's docker:S8431 objects to carrying both a tag and a digest; the
# argument for keeping them is with the docker ecosystem entry in that file.
FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:a4556ed033fa96f984bb7a8d348851cb2d36b1281dd2420070045f664fbb5f94 AS prod
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
ENTRYPOINT ["dotnet", "HealthcareSupport.CaseEvaluation.DbMigrator.dll"]
