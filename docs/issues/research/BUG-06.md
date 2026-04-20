[Home](../../INDEX.md) > [Issues](../) > Research > BUG-06

# BUG-06: goBack() Always Navigates to Root -- Research

**Severity**: Low
**Status**: Open (verified 2026-04-17)
**Source files**:
- `angular/src/app/appointments/appointment/components/appointment-view.component.ts` lines 219-221
- `angular/src/app/appointments/appointment-add.component.ts` lines 417-419

---

## Current state (verified 2026-04-17)

Both components:
```typescript
goBack(): void {
  this.router.navigateByUrl('/');
}
```

Users navigating in from a filtered list, dashboard card, or deep link lose their prior context on every back action.

---

## Official documentation

- [Angular `Location`](https://angular.dev/api/common/Location) -- `location.back()` navigates back in platform history; docs also caution that `Router.navigate()` is preferred for app-managed navigation.
- [Angular router events](https://angular.dev/api/router/Event) -- `NavigationStart.navigationTrigger` + `restoredState` distinguishes imperative vs popstate nav; useful for history tracking.
- [Angular `NavigationExtras`](https://angular.dev/api/router/NavigationExtras) -- `state` payload retrievable via `router.getCurrentNavigation()?.extras.state` for passing a `returnTo` URL.

## Community findings

- [dev.to -- How to Navigate to Previous Page in Angular](https://dev.to/angular/how-to-navigate-to-previous-page-in-angular-16jm) -- `NavigationService` pattern: subscribe to `NavigationEnd`, push URLs, pop on back, fallback to sensible default when stack empty.
- [Medium -- Save Route History, Angular Routing](https://sangwin.medium.com/angular-location-history-5c6955b4cd3d) -- service-based history array exposing `currentUrl`/`previousUrl` observables.
- [Ben Nadel -- Using Router Events To Detect Back/Forward Browser Navigation](https://www.bennadel.com/blog/3533-using-router-events-to-detect-back-and-forward-browser-navigation-in-angular-7-0-4.htm) -- `pairwise()` on `RoutesRecognized` to capture previous URL reliably.
- [Medium -- How to Find Out Whether history.back() Is Still in the Same Angular Application](https://medium.com/@python-javascript-php-html-css/how-to-find-out-whether-history-back-is-still-in-the-same-angular-application-17067561383a) -- discusses SPA-boundary risk (`location.back()` can exit the app if history is empty).
- [Damir's Corner -- Inspecting previous page in Angular](https://www.damirscorner.com/blog/posts/20220610-InspectingPreviousPageInAngular.html) -- `Router.events.pipe(filter(NavigationEnd), pairwise())` pattern.

## Recommended approach

1. Replace hardcoded `navigateByUrl('/')` with injectable `NavigationService` that tracks history from `Router.events.pipe(filter(NavigationEnd))` and exposes `back(fallback = '/appointments')`.
2. Inside `back()`: if internal history stack has >= 2 entries, call `location.back()`; otherwise `router.navigateByUrl(fallback)`. Avoids SPA-exit risk.
3. Optional: accept a `returnTo` URL via `router.navigate([...], { state: { returnTo } })` for deep-link scenarios where navigator knows better than history stack.

## Gotchas / blockers

- `location.back()` with empty SPA history exits the app to the browser's previous site -- worse UX than `/`. Fallback branch is mandatory.
- Angular 20 standalone-app DI: `NavigationService` should be `providedIn: 'root'` and subscribe eagerly (constructor) so history records on bootstrap, not on first component use.
- Router events fire in SSR; no-op subscription when `isPlatformBrowser` is false if SSR is ever enabled (not currently, per project CLAUDE.md).
- Query-param/fragment preservation: `location.back()` restores them (popstate); `navigateByUrl(fallback)` does not. Matters for filtered list views.

## Open questions

- Should "back" from detail view always land on the list (deterministic), or on wherever user came from (history-aware)? Two different UX contracts.
- Does the dashboard link into appointment detail with any state that should be preserved on return (selected tab, scroll position)?

## Related

- [docs/issues/BUGS.md#bug-06](../BUGS.md#bug-06-goback-always-navigates-to-root)
