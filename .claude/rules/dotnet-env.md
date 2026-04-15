# .NET Environment Configuration

When running dotnet commands (dotnet run, dotnet build, dotnet test, dotnet ef) in this
project, always set DOTNET_ENVIRONMENT=Development and ASPNETCORE_ENVIRONMENT=Development
unless explicitly told otherwise.

Example: `DOTNET_ENVIRONMENT=Development dotnet run --project src/HCS.CaseEvaluation.HttpApi.Host`

This ensures development-mode configuration (appsettings.Development.json) is loaded.
