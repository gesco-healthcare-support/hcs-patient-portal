# ADR-001: Riok.Mapperly over AutoMapper

**Status:** Accepted
**Date:** 2026-04-10
**Verified by:** code-inspect

## Context

ABP Framework ships with AutoMapper as its default object-to-object mapper. AutoMapper
uses reflection at runtime to map properties between source and destination types. This
project targets .NET 10 and may pursue AOT compilation in the future. The team wanted
compile-time safety for DTO mappings and faster startup/runtime performance.

ABP Commercial 10.0.2 introduced first-party support for Riok.Mapperly via the
`Volo.Abp.Mapperly` package, which provides a `MapperBase<TSource, TDest>` base class
that integrates with ABP's DI and lifecycle.

## Decision

Use Riok.Mapperly (source-generated, compile-time mapping) instead of AutoMapper for all
entity-to-DTO and DTO-to-entity mappings.

All mapper classes are declared in a single file:
`src/.../Application/CaseEvaluationApplicationMappers.cs`

Each mapper is a `partial class` decorated with `[Mapper]` (or
`[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]`) and extends
`MapperBase<TSource, TDest>`. The module registers Mapperly via
`using Volo.Abp.Mapperly` in `CaseEvaluationApplicationModule.cs`. As of this writing,
the file contains 39 mapper classes covering all 15 domain entities plus lookup DTOs.

Custom post-mapping logic (e.g., setting `DisplayName` on `LookupDto<Guid>`) uses the
`AfterMap` override on `MapperBase`, not AutoMapper's `ForMember` fluent API.

## Consequences

**Easier:**
- Mapping errors are caught at compile time -- missing properties fail the build
- No reflection overhead at runtime; source generator emits direct property assignments
- AOT-compatible from day one
- Single file makes it easy to audit all mappings in one place

**Harder:**
- ABP documentation and community examples overwhelmingly use AutoMapper; translating
  those examples to Mapperly requires understanding the `MapperBase` abstraction
- Partial class + source generator pattern is less familiar to developers coming from
  AutoMapper's `Profile` configuration style
- Cannot use AutoMapper's `ProjectTo<>()` for EF Core query projection -- must write
  manual LINQ selects (the project already does this in repository implementations)

## Alternatives Considered

1. **AutoMapper (ABP default)** -- Rejected because it relies on runtime reflection,
   provides no compile-time guarantees, and is incompatible with AOT. The majority of
   mapping bugs surface only at runtime.

2. **Manual mapping methods** -- Rejected because they are tedious to maintain across 39+
   mapper classes and prone to missing newly added properties. Mapperly generates the
   equivalent code automatically.

3. **Mapster** -- Another source-generator option, but ABP does not provide a first-party
   integration package for it. `Volo.Abp.Mapperly` gives us ABP lifecycle integration
   out of the box.
