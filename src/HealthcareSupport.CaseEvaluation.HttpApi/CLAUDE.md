# HttpApi Layer

Thin manually-written ASP.NET Core controllers that expose AppService methods over REST. Controllers contain no business logic -- they delegate every call to the injected `I{Entity}AppService`.

## What Lives Here

- `Controllers/` -- one controller per feature, each extending `AbpController` and implementing the feature's `I{Entity}AppService` interface
- `Controllers/CaseEvaluationController.cs` -- base controller (localization scope)
- ABP module definition files at project root

## Conventions

1. **Controllers are manual, not auto-wired.** ABP supports auto-controller generation from AppServices; this project disables it via `[RemoteService(IsEnabled = false)]` on every AppService so that routes are explicit. Every new AppService requires a matching controller here. See [ADR-002](../../docs/decisions/002-manual-controllers-not-auto.md).
2. **Controllers implement the AppService interface.** Signature: `public class {Entities}Controller : AbpController, I{Entities}AppService`. Each method delegates directly:
   ```csharp
   public Task<AppointmentDto> GetAsync(Guid id) => _appointmentsAppService.GetAsync(id);
   ```
3. **Route template: `[Route("api/app/{entity-plural}")]`.** Plural, kebab-case. Example: `api/app/appointments`, `api/app/doctor-availabilities`.
4. **No authorization attributes here.** Permissions are enforced at the AppService layer. Adding `[Authorize]` to the controller is redundant and risks diverging from the AppService gate.
5. **No business logic, no DTO construction, no validation.** Controllers must remain pure passthroughs. Any transform belongs in the AppService or a domain service.

## Key Files

| File | Purpose |
|------|---------|
| `Controllers/CaseEvaluationController.cs` | Base class setting localization resource |
| `Controllers/{Entities}Controller.cs` | One per feature; delegates to `I{Entities}AppService` |

## Related Docs

- [Root CLAUDE.md](../../CLAUDE.md) -- ABP Conventions section (Controllers subsection)
- [docs/api/API-ARCHITECTURE.md](../../docs/api/API-ARCHITECTURE.md)
- [docs/api/ENDPOINTS-REFERENCE.md](../../docs/api/ENDPOINTS-REFERENCE.md)
- [ADR-002: Manual controllers](../../docs/decisions/002-manual-controllers-not-auto.md)
