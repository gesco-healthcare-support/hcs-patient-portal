# HttpApi.Client -- dynamic HTTP proxy NuGet for external .NET consumers

Thin ABP module. External .NET apps reference this package and call IAppService interfaces
over HTTP with no hand-written HttpClient code. Castle DynamicProxy generates all client
implementations at runtime.

## What lives here

- `CaseEvaluationHttpApiClientModule.cs` -- the only source file; everything else is build
  artifacts. Contains the module class, `RemoteServiceName` const, and `ConfigureServices`.

## Conventions

- `AddHttpClientProxies` scans the `CaseEvaluationApplicationContractsModule` assembly and
  wires every `IAppService` as a dynamic proxy -- no proxy files to write or edit.
- `RemoteServiceName = "Default"`. Consumers must set `RemoteServices:Default:BaseUrl` in
  their own appsettings to point at the running HttpApi.Host.
- IMPORTANT: to add a new external ABP-module client (e.g. a new Volo module), add its
  `*HttpApiClientModule` to `[DependsOn(...)]` -- nothing else is needed.

## Gotchas

- There are no hand-written proxy classes in this layer. If you think you need to add one,
  you almost certainly need to add or fix an IAppService in Application.Contracts instead,
  then let ABP generate the proxy at runtime.
- `AddEmbedded<CaseEvaluationHttpApiClientModule>()` registers virtual file sets so
  downstream modules can embed resources -- do not remove it even if no virtual files exist
  yet; removal breaks the ABP module graph for consumers.

## Related

- docs/architecture/OVERVIEW.md -- overall layer map
- test/HealthcareSupport.CaseEvaluation.HttpApi.Client.ConsoleTestApp/ -- manual smoke-test
  consumer (not an automated test; run it by hand to verify proxy wiring after API changes)
