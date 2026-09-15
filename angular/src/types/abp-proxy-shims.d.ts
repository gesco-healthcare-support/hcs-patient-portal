// ABP CLI 10.2.0 generates `Record<string, StringValues>` for the
// `IFormFile.Headers` field in `proxy/microsoft/asp-net-core/http/models.ts`
// without emitting a definition for `StringValues`. Documented gap; the
// official ABP recommendation for Angular consumers is an ambient
// declaration matching the JSON shape of
// `Microsoft.Extensions.Primitives.StringValues` (single string, or array
// of strings when more than one value is set).
// See docs/research/proxy-regen-stringvalues-fix.md for the full trail.
//
// This shim sits OUTSIDE proxy/ so a future `abp generate-proxy` does not
// overwrite it. tsconfig.app.json picks it up via `src/**/*.d.ts`.
declare type StringValues = string | string[];
