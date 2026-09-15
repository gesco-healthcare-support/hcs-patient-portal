# Single multi-stage Dockerfile for the DbMigrator (one-shot console: applies EF
# migrations + runs seed contributors, then exits).
#   prod (DEFAULT): publish -> aspnet runtime image. CI / deploy.
#   dev:            SDK image; source bind-mounted at runtime so new migrations /
#                   seeds run from source. Re-run with
#                   `docker compose up db-migrator` (it exits on completion).

# ---- build (sdk): restore + publish; feeds prod ----
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
RUN dotnet publish src/HealthcareSupport.CaseEvaluation.DbMigrator/HealthcareSupport.CaseEvaluation.DbMigrator.csproj \
    -c Release -o /app/publish --no-restore

# ---- dev (sdk): bind-mounted source; restore + run on start ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dev
WORKDIR /app

# Redirect ALL build output (obj/bin) to a container-local path via .NET's
# artifacts output (see api/authserver Dockerfiles). Keeps the bind-mounted
# host src tree free of Linux build artifacts.
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

# Render NuGet.Config, restore, and delete it in ONE layer. Deleting it in a
# later RUN would not help -- the layer that created the file still holds it.
RUN --mount=type=secret,id=abp_nuget_key \
    sh /usr/local/bin/write-nuget-config.sh \
 && { dotnet restore HealthcareSupport.CaseEvaluation.slnx; rc=$?; rm -f NuGet.Config; exit $rc; }
RUN dotnet build src/HealthcareSupport.CaseEvaluation.DbMigrator/HealthcareSupport.CaseEvaluation.DbMigrator.csproj \
    -c Debug --no-restore

# The key is needed again at RUN time, not only at build time: the package
# cache is a named volume that shadows the one baked above, so a fresh volume
# restores from the private feed. It arrives as a Compose service secret and
# the rendered NuGet.Config is deleted before the app is exec'd (#703).
COPY --chmod=0755 docker/dev-restore-run.sh /usr/local/bin/dev-restore-run.sh
ENTRYPOINT ["/usr/local/bin/dev-restore-run.sh", \
  "src/HealthcareSupport.CaseEvaluation.DbMigrator/HealthcareSupport.CaseEvaluation.DbMigrator.csproj"]

# ---- prod (DEFAULT): small aspnet runtime image ----
# Digest-pinned because this image SHIPS. The tag stays alongside the digest --
# that pair is the form Dependabot updates; see .github/dependabot.yml for why
# the sdk build/dev stages above are deliberately left on tags.
# SonarCloud's docker:S8431 objects to carrying both a tag and a digest; the
# argument for keeping them is with the docker ecosystem entry in that file.
FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:a4556ed033fa96f984bb7a8d348851cb2d36b1281dd2420070045f664fbb5f94 AS prod
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "HealthcareSupport.CaseEvaluation.DbMigrator.dll"]
