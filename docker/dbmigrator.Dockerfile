FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG ABP_NUGET_API_KEY
WORKDIR /src

COPY NuGet.Config.template .
RUN sed "s/\${ABP_NUGET_API_KEY}/$ABP_NUGET_API_KEY/" NuGet.Config.template > NuGet.Config

COPY common.props .
COPY HealthcareSupport.CaseEvaluation.slnx .
COPY src/ src/
RUN dotnet restore HealthcareSupport.CaseEvaluation.slnx
RUN dotnet publish src/HealthcareSupport.CaseEvaluation.DbMigrator/HealthcareSupport.CaseEvaluation.DbMigrator.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "HealthcareSupport.CaseEvaluation.DbMigrator.dll"]
