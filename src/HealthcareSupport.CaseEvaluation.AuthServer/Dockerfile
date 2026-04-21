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

# Install Node.js + ABP CLI to restore client-side libraries (wwwroot/libs/ is gitignored)
# AuthServer's Razor login pages need LeptonX theme, jQuery, Bootstrap etc.
RUN curl -fsSL https://deb.nodesource.com/setup_22.x | bash - \
    && apt-get install -y --no-install-recommends nodejs \
    && rm -rf /var/lib/apt/lists/*
RUN dotnet tool install -g Volo.Abp.Cli --version 10.0.2
ENV PATH="$PATH:/root/.dotnet/tools"
WORKDIR /src/src/HealthcareSupport.CaseEvaluation.AuthServer
RUN abp install-libs
WORKDIR /src

RUN dotnet publish src/HealthcareSupport.CaseEvaluation.AuthServer/HealthcareSupport.CaseEvaluation.AuthServer.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .

# ABP VFS in Development mode expects sibling source directories for hot-reload.
# Create empty stubs so PhysicalFileProvider doesn't throw DirectoryNotFoundException.
# ABP falls back to embedded resources when directories are empty.
RUN mkdir -p /HealthcareSupport.CaseEvaluation.Domain.Shared \
 && mkdir -p /HealthcareSupport.CaseEvaluation.Domain

ENTRYPOINT ["dotnet", "HealthcareSupport.CaseEvaluation.AuthServer.dll"]
