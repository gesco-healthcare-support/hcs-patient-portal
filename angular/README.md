# Appointment Portal -- Angular SPA

The browser application of the Appointment Portal: an Angular 20 single-page app built on the ABP Commercial
Angular packages. It talks to the `HttpApi.Host` REST API and signs users in through the AuthServer with the OAuth
2.0 authorization code flow and PKCE. The npm package name is `CaseEvaluation`, the solution's internal name.

Everything here uses Yarn 4 (`packageManager` in `package.json`), not npm. Commands run from this folder.

## Build and run locally

The API (`https://localhost:44327`) and the AuthServer (`https://localhost:44368`) must be running first; see
[Getting Started](../docs/onboarding/GETTING-STARTED.md).

```bash
yarn install
npx ng build --configuration development
npx serve -s dist/CaseEvaluation/browser -p 4200
```

Open `http://localhost:4200`. A bare host redirects to `admin.localhost:4200`, the host administration surface;
an office is reached at `<office>.localhost:4200`.

Do not use `ng serve` or `yarn start`: the dev server's pre-bundling breaks ABP's dependency injection. See
[ADR-005](../docs/decisions/005-no-ng-serve-vite-workaround.md). Rebuild after a change instead.

## Build configurations

| Configuration          | Environment file                                     | Use                          |
| ---------------------- | ---------------------------------------------------- | ---------------------------- |
| `production` (default) | `src/environments/environment.prod.ts`               | The shipped image            |
| `development`          | `src/environments/environment.ts`                    | Local work                   |
| `docker`               | `src/environments/environment.docker.ts`             | The Docker development stack |
| `local`                | `src/environments/environment.local.ts` (gitignored) | Per-worktree stacks          |

The production image is built by `docker-compose.prod.yml`, which sets `NG_CONFIG=production`, so the Dockerfile
runs `yarn ng build --configuration production` (the same build as `yarn build:prod`).

At start-up `src/main.ts` fetches `dynamic-env.json` and merges it over the built environment, so one image serves
any deployment. In the production image, `prod-dynamic-env.envsh` writes that file from environment variables when
the container starts. [Angular Architecture](../docs/frontend/ANGULAR-ARCHITECTURE.md#runtime-configuration)
describes the whole sequence.

## Test, lint and format

```bash
npx ng test --watch=false --browsers=ChromeHeadless
yarn lint
yarn format:check
```

Karma and Jasmine run the unit tests; on Windows set `CHROME_BIN` to a Chrome or Edge executable first. There is no
end-to-end test target. CI runs the same commands (see
[CI Tests and Checks](../docs/devops/CI-TESTS-AND-CHECKS.md)).

## API proxies

`src/app/proxy/` is generated from the running API. Do not edit it; after a backend DTO or service change, with
`HttpApi.Host` running:

```bash
abp generate-proxy -t ng
```

## Further reading

- [Angular Architecture](../docs/frontend/ANGULAR-ARCHITECTURE.md) -- start-up, providers, configuration, tooling
- [Routing and Navigation](../docs/frontend/ROUTING-AND-NAVIGATION.md) -- the route table and its guards
- [Role-Based UI](../docs/frontend/ROLE-BASED-UI.md) -- external pages and the staff shell
- [Component Patterns](../docs/frontend/COMPONENT-PATTERNS.md) -- the main components
- [Appointment Booking Flow](../docs/frontend/APPOINTMENT-BOOKING-FLOW.md) -- the booking wizard
- `src/app/CLAUDE.md` -- conventions and gotchas for this folder
