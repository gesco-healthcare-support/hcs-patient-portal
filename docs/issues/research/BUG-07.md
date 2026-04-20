[Home](../../INDEX.md) > [Issues](../) > Research > BUG-07

# BUG-07: onSubmit() Error in save() Silently Swallowed -- Research

**Severity**: Low
**Status**: Open (verified 2026-04-17)
**Source files**:
- `angular/src/app/appointments/appointment-add.component.ts` lines 407-419

---

## Current state (verified 2026-04-17)

```typescript
save(): void {
  this.onSubmit();   // async function, no await, no catch
}
```

`onSubmit()` is async with a `try/finally` but NO `catch`. Rejection becomes an unhandled promise rejection -- the browser's global listener only produces a console warning. User sees the Save button re-enable, no error toast, appointment silently not created.

---

## Official documentation

- [Angular "Unhandled errors" guide](https://angular.dev/best-practices/error-handling) -- "errors should be surfaced to developers at the callsite whenever possible." Angular does NOT auto-catch errors from APIs your code calls directly; only observable pipelines tied to `AsyncPipe` are auto-handled.
- [`provideBrowserGlobalErrorListeners`](https://angular.dev/best-practices/error-handling) -- listens for `error` and `unhandledrejection`, forwards to `ErrorHandler`; safety net, not UX mechanism.
- [Angular `ErrorHandler`](https://angular.dev/api/core/ErrorHandler) -- global hook with a single `handleError` method; best for telemetry, not toast UX.
- [Angular `HttpInterceptor`](https://angular.dev/api/common/http/HttpInterceptor) -- intercepts HTTP errors in one place; right seam for cross-cutting HTTP toast notifications.
- [MDN -- `unhandledrejection` event](https://developer.mozilla.org/en-US/docs/Web/API/Window/unhandledrejection_event) -- only guarantees a console warning, not user-visible signal.
- [RxJS `catchError`](https://rxjs.dev/api/operators/catchError) -- canonical RxJS error boundary.
- [ABP `ToasterService` (`@abp/ng.theme.shared`)](https://abp.io/docs/latest/framework/ui/angular/toaster-service) -- `success/error/warn/info(message, title, options?)`. Already `providedIn: 'root'`.

## Community findings

- [TrackJS -- Complete Angular Error Handling Guide](https://trackjs.com/blog/angular-error-handling/) -- layer-by-layer: `HttpInterceptor` for HTTP + global `ErrorHandler` + component-local try/catch for domain-specific messaging.
- [Angular University -- RxJs Error Handling](https://blog.angular-university.io/rxjs-error-handling/) -- deep coverage of `catchError`, `retry`, replacement-observable patterns.
- [dev.to -- Advanced Angular Error Handling Best Practices](https://dev.to/codewithrajat/advanced-angular-error-handling-best-practices-architecture-tips-code-examples-3939) -- `ErrorInterceptor` + `ErrorService` + `ErrorHandler` as independent testable primitives.
- [Rollbar -- Error Handling with Angular](https://rollbar.com/blog/error-handling-with-angular-8-tips-and-best-practices/) -- practical `ToasterService`-in-global-interceptor examples.

## Recommended approach

1. Wrap the async body in a component-local try/catch; surface failure with `ToasterService.error(L.instant('::SaveFailed'))` and keep the submit button enabled.
2. Add (if not present) a global `HttpErrorInterceptor` mapping HTTP errors to toasts for any uncaught backend failure; per-request opt-out via a context token for flows that do their own error UI.
3. Wire an app-wide `ErrorHandler` subclass forwarding to a telemetry sink (even just structured `console.error`) so non-HTTP exceptions in handlers don't silently die.
4. Enable ESLint rule `@typescript-eslint/no-floating-promises` to catch this class of bug at lint time.

## Gotchas / blockers

- Adding a global interceptor changes every failing HTTP call to show a toast -- audit existing flows or users see duplicate toasts where a component is already handling the error.
- ABP ships its own error interceptor/toaster wiring; verify whether the project still has ABP's `HttpErrorResponseInterceptor` in `appConfig.providers` before introducing a second one. LOW confidence -- unconfirmed in current config.
- `async` methods called from template click handlers silently swallow rejections unless `await`ed or chained with `.catch`. Lint rule catches this; worth enabling.
- `ToasterService` messages must be localization keys for the [ARC-07](ARC-07.md) fix to stick.

## Open questions

- Does the project already have a global `HttpErrorInterceptor`? If yes, what does it do on 4xx vs 5xx?
- Should form submit failures be modal (blocking) or toast (non-blocking)? Affects component API surface.
- Should server validation errors (`AbpValidationException`) render inline on fields or as toasts?

## Related

- [ARC-07](ARC-07.md) -- message localisation depends on the same toast path
- [docs/issues/BUGS.md#bug-07](../BUGS.md#bug-07-onerror-in-save-is-silently-swallowed)
