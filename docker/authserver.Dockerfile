FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG ABP_NUGET_API_KEY
WORKDIR /src

COPY NuGet.Config.template .
RUN sed "s/\${ABP_NUGET_API_KEY}/$ABP_NUGET_API_KEY/" NuGet.Config.template > NuGet.Config

COPY common.props .
COPY HealthcareSupport.CaseEvaluation.slnx .
COPY src/ src/
RUN dotnet restore HealthcareSupport.CaseEvaluation.slnx
RUN dotnet publish src/HealthcareSupport.CaseEvaluation.AuthServer/HealthcareSupport.CaseEvaluation.AuthServer.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "HealthcareSupport.CaseEvaluation.AuthServer.dll"]
