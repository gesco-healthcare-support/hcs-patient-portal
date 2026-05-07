FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG ABP_NUGET_API_KEY
WORKDIR /src

COPY NuGet.Config.template .
RUN sed "s/\${ABP_NUGET_API_KEY}/$ABP_NUGET_API_KEY/" NuGet.Config.template > NuGet.Config

COPY Directory.Build.props .
COPY HealthcareSupport.CaseEvaluation.slnx .
COPY src/ src/
COPY test/ test/

# Create placeholder secrets files excluded by .dockerignore (ABP .csproj references them)
RUN echo '{}' > src/HealthcareSupport.CaseEvaluation.AuthServer/appsettings.secrets.json \
 && echo '{}' > src/HealthcareSupport.CaseEvaluation.HttpApi.Host/appsettings.secrets.json \
 && echo '{}' > src/HealthcareSupport.CaseEvaluation.DbMigrator/appsettings.secrets.json \
 && echo '{}' > test/HealthcareSupport.CaseEvaluation.TestBase/appsettings.secrets.json \
 && echo '{}' > test/HealthcareSupport.CaseEvaluation.HttpApi.Client.ConsoleTestApp/appsettings.secrets.json

RUN dotnet restore HealthcareSupport.CaseEvaluation.slnx
RUN dotnet publish src/HealthcareSupport.CaseEvaluation.HttpApi.Host/HealthcareSupport.CaseEvaluation.HttpApi.Host.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .

# See AuthServer Dockerfile note: empty-stub directories silently nuke the
# embedded localization fileset because ABP's ReplaceEmbeddedByPhysical
# fully replaces (not merges) when the physical path exists. The
# HttpApiHost module now guards each replace call with Directory.Exists,
# so missing host source directories cleanly fall back to embedded
# resources. Don't reintroduce the stubs.

ENTRYPOINT ["dotnet", "HealthcareSupport.CaseEvaluation.HttpApi.Host.dll"]
