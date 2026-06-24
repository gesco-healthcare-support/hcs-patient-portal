# Proxy Regen -- `StringValues` Type-Gen Artifact Fix

Research-only output for the `abp generate-proxy -t ng` artifact in
`angular/src/app/proxy/microsoft/asp-net-core/http/models.ts` where the
generated `IFormFile` interface references an undefined TypeScript type
`StringValues`.

Status: descriptive + recommended fix. No code edits in this pass.

---

## Symptom

After `abp generate-proxy -t ng` (CLI 10.2.0, packages `@abp/ng.* 10.0.2`),
the file `angular/src/app/proxy/microsoft/asp-net-core/http/models.ts`
emits:

```ts
export interface IFormFile {
  contentType?: string;
  contentDisposition?: string;
  headers?: Record<string, StringValues>;   // <-- undefined symbol
  length?: number;
  name?: string;
  fileName?: string;
}
```

`StringValues` is referenced but never declared, never imported, and
not in any `*.d.ts` ambient file in the project (verified: no
`angular/src/types/`, no `angular/src/global-types.d.ts`, no
`*.d.ts` under `angular/src/`). `tsc --noEmit` fails with TS2304:
"Cannot find name 'StringValues'".

Triggered by `UploadAppointmentDocumentForm` in
`src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/AppointmentDocuments/AppointmentDocumentController.cs:138`,
which exposes a public `IFormFile File { get; set; }` to Swashbuckle so
multipart upload renders as a single schema. The proxy generator walks
that property, emits `IFormFile`, and the .NET-side `IHeaderDictionary`
(`IDictionary<string, StringValues>`) bleeds through unmapped.

---

## Web findings (HIGH confidence -- official ABP support threads)

ABP support has acknowledged this gap on at least four threads, dating
from 2020 through 2024. The generator has not been fixed; only
workarounds and a refactor recommendation exist.

| Source | ABP CLI era | Recommendation |
| --- | --- | --- |
| ABP Support #1174 (file upload, 2020) | pre-4.3 and 4.3+ | Use `IRemoteStreamContent`; or pass file in body pre-4.3 |
| ABP GitHub abp#6384 (2020) | early 4.x | No upstream fix; issue documents the gap |
| ABP Support #7816 (`IFormCollection`, 2022) | 5.x | Recommendation: do not put `IFormCollection` in DTO; manual workaround = strip generic |
| ABP Support #9641 (`StringValues missing`, 2024) | 8.x+ | Manual `export type StringValues = string[];` OR switch DTO to `IRemoteStreamContent` |

Two consistent answers across all four threads:

1. **Refactor away.** Replace `IFormFile` on the DTO with
   `IRemoteStreamContent` (`Volo.Abp.Content`). ABP's proxy generator
   has explicit support for it and emits a clean Angular type that
   plays with `FormData` automatically.
2. **Or shim.** Add an ambient declaration
   `export type StringValues = string[];` (or
   `string | string[]`) so the otherwise-correct `IFormFile`
   interface compiles.

CLI 10.3 changelog was not surfaced by search; no evidence the
generator has been fixed in 10.x. Treat the workaround as still
required on 10.0.2 / CLI 10.2.0. (Confidence: HIGH on workaround
required; MEDIUM on "still broken in 10.3" -- not directly verified
in release notes.)

---

## Project survey

- `angular/src/app/proxy/microsoft/asp-net-core/http/models.ts` is the
  ONLY file referencing `StringValues` (verified by grep).
- `IFormFile` is imported in two proxy files:
  - `angular/src/app/proxy/appointment-documents/models.ts` (the
    NEW-app `UploadAppointmentDocumentForm.file?: IFormFile`).
  - `angular/src/app/proxy/documents-controllers/documents.service.ts`
    (`create(input, file: IFormFile)`, `replaceFile(id, file: IFormFile)`).
- No project-side ambient declaration files exist:
  no `angular/src/types/`, no `*.d.ts` outside `node_modules/`.
- `tsconfig.json` has no `typeRoots` and an empty `paths`. `tsconfig.app.json`
  sets `types: []` and includes `src/**/*.d.ts` + `src/**/*.ts`. Adding a
  `.d.ts` anywhere under `src/` is automatically picked up.

Consumer impact: nothing in the hand-written app code under
`angular/src/app/` outside of `proxy/` imports from
`proxy/microsoft/asp-net-core/http`. The `IFormFile` interface is
referenced ONLY by other generated proxy files that pass it through to
the ABP RestService transport. In practice, callers of
`appointmentDocuments.upload(...)` and `documents.create(...)` build a
`FormData` object and the `IFormFile` shape is never instantiated by
client code -- it is purely a compile-time alias on the proxy method
signature. The fix is therefore a pure compile-time concern.

---

## Recommendation

**Apply the ambient declaration shim. Do NOT refactor the backend
wrapper.**

Reasons:

- `IRemoteStreamContent` is the long-term ABP-blessed fix, but it
  changes the AppService signature, the controller, and how Angular
  builds the request body. That ripples through `AppointmentDocumentController`,
  the AME packet upload path, the JDF upload path, and the
  `documents.service.ts` proxy, and risks Swashbuckle no longer
  describing the multipart body as a single schema (the original
  reason `UploadAppointmentDocumentForm` exists).
- A base64 string for `File` breaks multipart and inflates payload by
  ~33%. Reject this option per the brief.
- The ambient shim is a 1-line, 1-file fix that costs nothing and
  unblocks `tsc --noEmit`. Strict-parity port priorities sit
  elsewhere.
- The `IFormFile` interface is type-only at the consumer; the
  `StringValues` field is never read on the client. Type alias is
  sufficient.

### Implementation

1. Create `angular/src/types/abp-proxy-shims.d.ts` with:

```ts
// Ambient shim for ABP Angular proxy gen artifact.
// `abp generate-proxy -t ng` emits `Record<string, StringValues>` on
// `IFormFile.headers` (proxy/microsoft/asp-net-core/http/models.ts)
// without declaring or importing `StringValues`. ABP CLI 10.2.0 has
// not fixed this -- see docs/research/proxy-regen-stringvalues-fix.md.
// Mirrors Microsoft.Extensions.Primitives.StringValues JSON shape
// (string OR array of strings on the wire).
declare type StringValues = string | string[];
```

2. No `tsconfig` change required. `tsconfig.app.json` already includes
   `src/**/*.d.ts`, and `tsconfig.spec.json` includes the same. The
   global `declare type` alias becomes visible to every TS file in the
   build.

3. Verify with:

```sh
cd W:/patient-portal/replicate-old-app/angular
npx tsc --noEmit
```

Expected: no `TS2304: Cannot find name 'StringValues'` from
`proxy/microsoft/asp-net-core/http/models.ts`.

4. Document the workaround in
   `angular/src/app/proxy/CLAUDE.md` (or the proxy README) so the next
   `abp generate-proxy` run does not re-trigger investigation. Add a
   note: "After regen, do not delete or edit the
   `StringValues` shim at `src/types/abp-proxy-shims.d.ts`. The
   generator continues to emit `Record<string, StringValues>` without
   declaring the type."

### Why `string | string[]` over `string[]`

`Microsoft.Extensions.Primitives.StringValues` JSON-serializes as a
single string when the underlying value is one element, and as an
array when there are multiple. ABP support thread #9641 suggests
`string[]`; that under-types the single-string case. `string | string[]`
matches ASP.NET Core's actual wire shape and is structurally
compatible with everything `string[]` accepts.

### Open / not-required follow-ups

- Long-term: switch `UploadAppointmentDocumentForm` to
  `IRemoteStreamContent` if/when the file upload code path is touched
  for other reasons. Out of scope for the current proxy regen.
- If ABP CLI is later upgraded past 10.2.0 and the upstream generator
  is fixed, the shim becomes a no-op and can be deleted. Cheap to
  revisit.

---

## Sources

- [ABP Support #1174 -- Incorrect proxy generated for file upload API: Cannot find name 'StringValues'](https://abp.io/support/questions/1174/Incorrect-proxy-generated-for-file-upload-API-Cannot-find-name-'StringValues')
- [ABP Support #7816 -- IFormCollection generates `any<string, StringValues>` with compilation errors](https://abp.io/support/questions/7816/angular-proxy-generator-for-IFormCollection-type-generate-anystring-StringValues-with-compilation-errors)
- [ABP Support #9641 -- BUG: `export type StringValues = string[];` missing](https://abp.io/support/questions/9641/BUG---export-type-StringValues--string-missing)
- [abpframework/abp #6384 -- Incorrect proxy generated for file upload API](https://github.com/abpframework/abp/issues/6384)
- [ABP Service Proxies docs](https://abp.io/docs/latest/framework/ui/angular/service-proxies)
