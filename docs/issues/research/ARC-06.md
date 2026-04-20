[Home](../../INDEX.md) > [Issues](../) > Research > ARC-06

# ARC-06: DTO Validation Attributes Missing -- Research

**Severity**: Low
**Status**: Open (verified 2026-04-17)
**Source files**:
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/DoctorAvailabilities/DoctorAvailabilityGenerateInputDto.cs`
- `.../DoctorAvailabilityDeleteBySlotInputDto.cs`
- `.../DoctorAvailabilityDeleteByDateInputDto.cs`

---

## Current state (verified 2026-04-17)

```csharp
public class DoctorAvailabilityGenerateInputDto
{
    public DateTime FromDate { get; set; }                        // no [Required]; drift: docs say DateOnly
    public DateTime ToDate { get; set; }                          // no [Required]
    public TimeOnly FromTime { get; set; }                        // no [Required]
    public TimeOnly ToTime { get; set; }                          // no [Required]
    public BookingStatus BookingStatusId { get; set; }
    public Guid LocationId { get; set; }                          // no [Required]
    public Guid? AppointmentTypeId { get; set; }
    public int AppointmentDurationMinutes { get; set; } = 15;     // no [Range]
}
```

Non-nullable value types (DateTime, Guid, int) are implicitly `[Required]` under ASP.NET Core's nullable-context model -- but `[Range]`, `[MaxLength]`, cross-field rules, and OpenAPI schema hints are absent. Swagger emits them as optional, generated Angular proxies cannot drive reactive-form validation.

**Drift from source doc**: docs describe `FromDate` / `ToDate` as `DateOnly`; current code uses `DateTime`. Update the source doc when fixing.

---

## Official documentation

- [ASP.NET Core model validation](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/validation) -- DataAnnotations auto-wired; non-nullable value types implicit-required; `AbpValidationException` -> HTTP 400.
- [Makolyte -- API validation attributes](https://makolyte.com/aspnetcore-api-model-validation-attributes/) -- built-in attribute rundown including `[Range(typeof(DateTime), "start", "end")]`.
- [ABP Validation pipeline](https://abp.io/docs/latest/framework/fundamentals/validation) -- interception-based; `AbpValidationException`, `IValidatableObject`, `ICustomValidate`, `IObjectValidationContributor`.
- [ABP FluentValidation integration](https://abp.io/docs/4.0/fluentvalidation) -- `AbstractValidator<T>` auto-discovered via DI; supports async DB checks via `MustAsync`.
- [.NET 8 DataAnnotations update](https://weblogs.asp.net/ricardoperes/net-8-data-annotations-validation/)

## Community findings

- [ABP Medium -- Using FluentValidation](https://medium.com/abp-community/using-fluentvalidation-with-abp-framework-b5a30c761f62)
- [ABP Community -- Using FluentValidation with ABP Framework](https://abp.io/community/articles/using-fluentvalidation-with-abp-framework-2cxuwl70)
- [aspnetboilerplate #3946 -- FluentValidation with DB checks](https://github.com/aspnetboilerplate/aspnetboilerplate/issues/3946) -- supported pattern for cross-entity validation.
- [ng-openapi-gen](https://github.com/cyclosproject/ng-openapi-gen) -- Angular codegen reads `required`, `minLength`, `maxLength` from OpenAPI schema, produces `Validators.required` etc.
- [OpenAPI Generator typescript-angular](https://openapi-generator.tech/docs/generators/typescript-angular/)
- [DEV.to -- Generate Angular ReactiveForms from OpenAPI](https://dev.to/martinmcwhorter/generate-angular-reactiveforms-from-swagger-openapi-35h9)

## Recommended approach

1. Add `[Required]` explicitly (even on value types) plus `[Range(1, 480)]` on `AppointmentDurationMinutes`, sensible date ranges, and `[Required]` on all `Guid` id properties. Cosmetic at runtime (MVC already treats value types as required) but meaningful for Swagger and proxies.
2. Cross-field rules (`FromDate <= ToDate`, `FromTime < ToTime`) belong in `IValidatableObject.Validate()` on the DTO OR an `AbstractValidator<T>`. Prefer FluentValidation when validation needs services; otherwise `IValidatableObject` is zero-dependency.
3. Delete-by-date/slot DTOs: add `[Required]` on ids and a date-range sanity check.
4. Keep cross-service validation (availability of slot, etc.) in `AppointmentManager` or a FluentValidation validator with DI -- do not resolve services inside DTO validators (ABP anti-pattern).

## Gotchas / blockers

- Non-nullable value types are implicitly required; explicit `[Required]` affects Swagger and proxies, not runtime behaviour.
- `[Range]` on `DateTime` needs the typed overload: `[Range(typeof(DateTime), "start", "end")]`, invariant culture.
- FluentValidation 11+ throws `AsyncValidatorInvokedSynchronouslyException` if any `MustAsync` rule is called sync. ABP's pipeline is async-safe, but custom code paths (unit tests) must call `ValidateAsync`.
- Regenerate Angular proxies (`abp generate-proxy`) after DTO changes so reactive forms pick up new validators.
- Include all three DTOs (DeleteBySlot, DeleteByDate, Generate) in the same change set.

## Open questions

- Use `IValidatableObject` (simple, no DI) or FluentValidation (nicer for complex rules, async DB checks)? Project doesn't currently depend on FluentValidation -- adding it is a stack decision.
- Realistic business ranges for `AppointmentDurationMinutes` (15, 30, 45, 60, 120)? Affects `[Range]` bounds.
- Should `FromDate` default to `Clock.Now.Date` if null, or always be required?
- Is the `DateTime` vs `DateOnly` drift in the current DTO intentional or a regression?

## Related

- [BUG-09](BUG-09.md), [BUG-10](BUG-10.md) -- overlap: validations these DTOs should enforce
- [docs/issues/ARCHITECTURE.md#arc-06](../ARCHITECTURE.md#arc-06-dto-validation-attributes-missing)
