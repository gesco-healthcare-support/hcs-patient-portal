# Onboarding — Patient Portal

## Prerequisites

| Tool | Version | Purpose |
|------|---------|---------|
| .NET SDK | 10.0 | Backend build and runtime |
| Node.js | LTS | Angular CLI and frontend tooling |
| SQL Server LocalDB | — | Database (`MSSQLLocalDB` instance) |
| Angular CLI | `npm install -g @angular/cli` | Frontend build |
| ABP CLI | `dotnet tool install -g Volo.Abp.Studio.Cli` | Framework tooling |
| Redis | (optional) | Distributed caching (disabled by default) |

## P: Drive Mapping (REQUIRED)

The real project path (~137 chars) causes `Microsoft.Data.SqlClient.SNI.dll` to exceed Windows 260-char path limit. Map a short drive:

```powershell
subst P: "C:\Users\RajeevG\Documents\Projects"
```

All dotnet commands MUST run from `P:\Patient Appointment Portal\hcs-case-evaluation-portal\`.

## Startup Sequence

**Order matters: AuthServer -> HttpApi.Host -> Angular**

```bash
# Terminal 0: Start LocalDB
sqllocaldb start MSSQLLocalDB

# Terminal 1: AuthServer (port 44368)
dotnet run --project "P:\Patient Appointment Portal\hcs-case-evaluation-portal\src\HealthcareSupport.CaseEvaluation.AuthServer"
# Wait for 200 on https://localhost:44368/.well-known/openid-configuration

# Terminal 2: API Host (port 44327)
dotnet run --project "P:\Patient Appointment Portal\hcs-case-evaluation-portal\src\HealthcareSupport.CaseEvaluation.HttpApi.Host"
# Wait for Swagger at https://localhost:44327/swagger

# Terminal 3: Angular (port 4200) — NEVER use ng serve
cd "P:\Patient Appointment Portal\hcs-case-evaluation-portal\angular"
npx ng build --configuration development
npx serve -s dist/CaseEvaluation/browser -p 4200
```

## Database Setup

```bash
# First time: run migrations + seed
dotnet run --project "P:\...\src\HealthcareSupport.CaseEvaluation.DbMigrator"
```

Default admin: `admin@abp.io` / see `TEST_PASSWORD` in `.env.local`

## Verification

```bash
curl -sk -o /dev/null -w "%{http_code}" https://localhost:44368/.well-known/openid-configuration  # 200
curl -sk -o /dev/null -w "%{http_code}" https://localhost:44327/swagger/index.html                # 200
curl -s  -o /dev/null -w "%{http_code}" http://localhost:4200/                                    # 200
```

## Common Tasks

| Task | Command |
|------|---------|
| Restore .NET packages | `dotnet restore` |
| Install Angular deps | `cd angular && npm install` |
| Add EF migration | `dotnet ef migrations add <Name> --project src/.../EntityFrameworkCore --startup-project src/.../HttpApi.Host` |
| Regenerate Angular proxies | `abp generate-proxy` (from `angular/`) |
| Run all tests | `dotnet test` |
| Run specific test | `dotnet test --filter "FullyQualifiedName~MethodName"` |

## Common Issues

| Issue | Solution |
|-------|----------|
| Named Pipes error on startup | LocalDB not started: `sqllocaldb start MSSQLLocalDB` |
| SNI.dll 500 errors | Not running from P: drive |
| `NullInjectorError: CORE_OPTIONS` | Used `ng serve` instead of `ng build` + `npx serve` |
| SSL certificate errors | `dotnet dev-certs https --trust` |
| Port conflict | Check 44327, 44368, 4200 are free |
| Angular proxy errors | API Host must be running before Angular |

## Detailed Setup

For complete setup instructions including connection strings, HTTPS certificates, Redis config, and ABP Studio integration: [docs/devops/DEVELOPMENT-SETUP.md](../../docs/devops/DEVELOPMENT-SETUP.md)
