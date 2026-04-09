FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG ABP_NUGET_API_KEY
WORKDIR /src

COPY NuGet.Config.template .
RUN sed "s/\${ABP_NUGET_API_KEY}/$ABP_NUGET_API_KEY/" NuGet.Config.template > NuGet.Config

COPY common.props .
COPY HealthcareSupport.CaseEvaluation.slnx .
COPY src/ src/
COPY test/ test/

# Create placeholder secrets files (ABP .csproj references these)
RUN echo '{}' > src/HealthcareSupport.CaseEvaluation.HttpApi.Host/appsettings.secrets.json
RUN echo '{}' > src/HealthcareSupport.CaseEvaluation.AuthServer/appsettings.secrets.json
RUN echo '{}' > src/HealthcareSupport.CaseEvaluation.DbMigrator/appsettings.secrets.json
RUN echo '{}' > test/HealthcareSupport.CaseEvaluation.TestBase/appsettings.secrets.json
RUN echo '{}' > test/HealthcareSupport.CaseEvaluation.HttpApi.Client.ConsoleTestApp/appsettings.secrets.json

RUN dotnet restore HealthcareSupport.CaseEvaluation.slnx
RUN dotnet publish src/HealthcareSupport.CaseEvaluation.HttpApi.Host/HealthcareSupport.CaseEvaluation.HttpApi.Host.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
COPY docker/entrypoint-dotnet.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["/entrypoint.sh"]
CMD ["dotnet", "HealthcareSupport.CaseEvaluation.HttpApi.Host.dll"]
