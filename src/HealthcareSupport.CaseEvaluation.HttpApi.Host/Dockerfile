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

# ABP VFS in Development mode expects sibling source directories for hot-reload.
# Create empty stubs so PhysicalFileProvider doesn't throw DirectoryNotFoundException.
RUN mkdir -p /src/HealthcareSupport.CaseEvaluation.Domain.Shared \
 && mkdir -p /src/HealthcareSupport.CaseEvaluation.Domain \
 && mkdir -p /src/HealthcareSupport.CaseEvaluation.Application.Contracts \
 && mkdir -p /src/HealthcareSupport.CaseEvaluation.Application \
 && mkdir -p /src/HealthcareSupport.CaseEvaluation.HttpApi

ENTRYPOINT ["dotnet", "HealthcareSupport.CaseEvaluation.HttpApi.Host.dll"]
