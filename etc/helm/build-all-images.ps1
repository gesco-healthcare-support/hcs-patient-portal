./build-image.ps1 -ProjectPath "../../src/HealthcareSupport.CaseEvaluation.DbMigrator/HealthcareSupport.CaseEvaluation.DbMigrator.csproj" -ImageName caseevaluation/dbmigrator
./build-image.ps1 -ProjectPath "../../src/HealthcareSupport.CaseEvaluation.HttpApi.Host/HealthcareSupport.CaseEvaluation.HttpApi.Host.csproj" -ImageName caseevaluation/httpapihost
./build-image.ps1 -ProjectPath "../../angular" -ImageName caseevaluation/angular -ProjectType "angular"
./build-image.ps1 -ProjectPath "../../src/HealthcareSupport.CaseEvaluation.AuthServer/HealthcareSupport.CaseEvaluation.AuthServer.csproj" -ImageName caseevaluation/authserver
