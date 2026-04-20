[Home](../../INDEX.md) > [Issues](../) > Research > BUG-09

# BUG-09: Past-Date Appointments Accepted -- Research

**Severity**: Medium
**Status**: Open (verified 2026-04-17, E2E test E1)
**Source files**:
- `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs` `CreateAsync` lines 162-252

---

## Current state (verified 2026-04-17)

`CreateAsync` validates FK existence, slot status (line 219), location match, type match, date alignment, and time-within-slot-range. It does NOT compare `input.AppointmentDate` against "today". E2E test E1 created an appointment on `2026-01-15` successfully.

No matching guard in `AppointmentManager.CreateAsync` either (see [ARC-02](ARC-02.md) for the wider layering concern).

---

## Official documentation

- [ABP Timing (IClock)](https://abp.io/docs/latest/framework/infrastructure/timing) -- `Clock.Now` is a base property on `ApplicationService`; `Clock.Now.Date` yields today in the configured `DateTimeKind`. `DateTime.UtcNow` is discouraged because it bypasses `AbpClockOptions.Kind`.
- [ABP IClock source](https://github.com/abpframework/abp/blob/dev/framework/src/Volo.Abp.Timing/Volo/Abp/Timing/IClock.cs) -- confirms `Now`, `Kind`, `Normalize`.
- [ABP Exception Handling](https://abp.io/docs/en/abp/latest/Exception-Handling) -- `BusinessException` with error codes + `MapCodeNamespace` for localized messages; `UserFriendlyException` is the interactive-UI form.
- [CA 8 CCR 31.3 (official)](https://www.dir.ca.gov/t8/31_3.html) / [LII mirror](https://www.law.cornell.edu/regulations/california/8-CCR-31.3) -- panel QME appointments within 90 days of request, extendable to 120; no minimum advance window.

## Community findings

- [ABP #24048 -- timezone interpretation of submitted DateTime values](https://github.com/abpframework/abp/issues/24048) -- reinforces using `IClock` not `DateTime.UtcNow`.
- [ABP Multi-Timezone article](https://abp.io/community/articles/developing-a-multitimezone-application-using-the-abp-framework-zk7fnrdq) -- `IClock.Normalize` required in multi-timezone tenants, otherwise comparisons drift.
- [aspnetboilerplate #3889](https://github.com/aspnetboilerplate/aspnetboilerplate/issues/3889) -- historical Clock.Now pitfalls.
- [LFLM 2024 DWC amendments summary](https://www.lflm.com/news-knowledge/new-timeline-to-set-qme-evaluations-amendment-to-regulations-for-medical-legal-evaluations/) -- regulation imposes only maximum windows, not minimum advance booking.

## Recommended approach

1. Guard at the top of `CreateAsync` (before FK lookups) using `Clock.Now.Date`; throw `BusinessException("CaseEvaluation:Appointments.PastDateNotAllowed")` mapped via `AbpExceptionLocalizationOptions.MapCodeNamespace`.
2. Mirror guard in `UpdateAsync` if date is mutable; plan to hoist into `AppointmentManager.CreateAsync` as part of the [ARC-02](ARC-02.md) refactor so domain tests cover it without controller scaffolding.
3. Consider an upper-bound check of 120 days forward (regulatory ceiling) to catch typos, with a separate error code. INFERENCE: regulation caps the window but does not mandate the backend check.

## Gotchas / blockers

- If `AbpClockOptions.Kind` is default `Unspecified`, `Clock.Now` is effectively `DateTime.Now` (local server time) -- verify startup config before comparing across tenants/timezones.
- Incoming `DateTime` can arrive `Kind=Unspecified` or `Utc`; comparing `.Date` across mismatched Kinds is date-correct but subtle. Use `Clock.Normalize(input.AppointmentDate).Date` if multi-timezone.
- `UserFriendlyException` needs `IStringLocalizer`; entities can't inject, so `BusinessException` + code-namespace is idiomatic for domain-layer invariants.

## Open questions

- **Product**: is this portal single-timezone (PT only) or multi-timezone?
- **Product**: should same-day appointments be allowed? `< Clock.Now.Date` vs `>= Clock.Now.Date.AddDays(1)`.
- **Product**: enforce regulatory upper bound (90 or 120 days)? Currently regulation is about schedule-by windows, not explicit booking limits.

## Related

- [BUG-10](BUG-10.md) -- same layer, same "invariants in AppService not Domain" pattern
- [ARC-02](ARC-02.md) -- fixes belong in `AppointmentManager` long-term
- Q6 in [TECHNICAL-OPEN-QUESTIONS.md](../TECHNICAL-OPEN-QUESTIONS.md#q6-was-there-supposed-to-be-a-minimum-advance-booking-window-eg-3-days)
- [docs/issues/BUGS.md#bug-09](../BUGS.md#bug-09-past-date-appointments-accepted-without-validation)
