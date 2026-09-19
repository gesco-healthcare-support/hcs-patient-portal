# Contributing to the HCS Case Evaluation Portal

Thanks for helping maintain this project. This file covers the contribution
workflow: branches, commits, pull requests, and the rules that apply to every
change. For setting up your machine and running the services locally, see
[docs/onboarding/GETTING-STARTED.md](docs/onboarding/GETTING-STARTED.md).

## Code of Conduct

By participating in this project you agree to uphold the
[Code of Conduct](CODE_OF_CONDUCT.md), including its non-negotiable HIPAA / PHI
clauses.

## Branch Model

```text
feature/* --> main --> development --> staging --> production
```

| Branch        | Role                                         |
| ------------- | -------------------------------------------- |
| `main`        | Default PR target. Latest integrated code.   |
| `development` | In-house hosted testing and experimentation. |
| `staging`     | Pre-production verification.                 |
| `production`  | Live application (not deployed yet).         |

Merges flow one direction only. Promotion PRs between long-lived branches must
use **rebase**, never a merge commit, to preserve linear history.

## Branch Protection

**The same 17 checks are required on all four branches.** There is no longer a
per-branch gradient: a change that cannot merge to `main` cannot merge anywhere.
The only thing that varies by branch is how many approvals a pull request needs.

| Branch        | Required checks | Approvals | Up to date with base |
| ------------- | --------------- | --------- | -------------------- |
| `main`        | all 17 below    | 1         | required             |
| `development` | all 17 below    | 1         | required             |
| `staging`     | all 17 below    | 1         | required             |
| `production`  | all 17 below    | **2**     | required             |

The 17, exactly as configured:

| Check                            | What it covers                                                            |
| -------------------------------- | ------------------------------------------------------------------------- |
| `Meta: Changed paths`            | Classifies the diff; unrecognised paths run the full suite                |
| `Backend: Build`                 | Compiles the solution, warnings as errors, and checks both migration sets |
| `Backend: Test`                  | Backend test suite                                                        |
| `Backend: Format Check`          | `dotnet format`                                                           |
| `Frontend: Build`                | Compiles the SPA                                                          |
| `Frontend: Test`                 | Angular specs                                                             |
| `Frontend: Lint`                 | Angular static analysis                                                   |
| `Frontend: Format Check`         | Prettier                                                                  |
| `Coverage: Floors`               | Per-stack coverage floors and the changed-lines floor                     |
| `Dependency Review`              | Licences and vulnerabilities in changed dependencies                      |
| `TruffleHog: PR commits`         | Secret scan of the diff                                                   |
| `CodeQL: csharp`                 | Code scanning, backend                                                    |
| `CodeQL: javascript-typescript`  | Code scanning, frontend                                                   |
| `Lint: Markdown`                 | Every markdown file in the repository                                     |
| `Lint: YAML workflows`           | `.github/workflows/`                                                      |
| `Commitlint: PR commits`         | Commit message format                                                     |
| `PR Title: Conventional Commits` | Pull request title format                                                 |

Two checks run on pull requests but are deliberately **not** required:

- `Docs: Structure Check` -- it is conditional on the diff touching shared paths,
  and a skipped job reports success. Requiring it would let a pull request that
  does not touch those paths satisfy it without running.
- `SonarCloud: Analysis` -- informational while its quality gate is being
  settled; making it required would block on conditions unrelated to the change.

Promotion PRs between long-lived branches still use rebase, and "up to date with
base" is enforced, so a promotion must be rebased on its target before it merges.

## Development Workflow

1. Create a feature branch from `main`:

   ```bash
   git checkout -b feature/my-feature main
   ```

2. Make your changes. Keep commits focused and follow
   [Conventional Commits](https://www.conventionalcommits.org/):

   ```text
   feat(appointments): add bulk scheduling endpoint
   fix(doctors): correct availability overlap check
   docs(patients): update API reference
   ```

3. Push and open a pull request against `main`. Use the template that appears;
   fill in the summary, test plan, documentation, and HIPAA checklist.
4. After CI passes and one approval, merge (squash is the default for feature
   branches).
5. Code promotes automatically via two workflows: `auto-pr-dev.yml` opens the
   `main` -> `development` PR on push to `main`, and `deploy-dev.yml` opens
   the `development` -> `staging` PR after its validate job passes on push
   to `development`. Review and merge each promotion PR manually.
6. Promotion `staging` -> `production` is always opened manually and
   requires two approvals.

## Commit Messages

Enforced by `commitlint` via the Husky `commit-msg` hook. Allowed types:
`feat`, `fix`, `docs`, `style`, `refactor`, `test`, `chore`, `ci`, `perf`,
`build`, `revert`.

Subject line max length: 100 characters.

## Local Hooks

Husky hooks install automatically the first time you run `yarn install` inside
`angular/`. They run on every `git commit` and `git push`:

- **On commit**: Gitleaks staged scan, lint-staged (Prettier + ESLint),
  `dotnet format --verify-no-changes` on staged `.cs` files, commitlint.
- **On push**: full-repo Gitleaks scan, `dotnet build` in Debug.

Gitleaks is installed separately (not via npm) -- see
[docs/onboarding/GETTING-STARTED.md](docs/onboarding/GETTING-STARTED.md) for
per-OS installation, WSL handling, and GUI git client setup. If Gitleaks is
missing locally, hooks print a warning and continue; CI still scans
server-side.

Do not use `git commit --no-verify` to bypass hooks except for true
emergencies. CI catches what you skipped, but only after push.

## Pull Request Rules

- Fill in every section of the PR template, including the HIPAA checklist.
- Reference the issue, ticket, or task the PR closes.
- Keep PRs focused -- PR size is labelled automatically (`size/XS` through
  `size/XL`). Large PRs are not forbidden but require a deliberate justification.
- Update relevant `CLAUDE.md` feature files and
  [docs/](docs/) pages when behaviour changes. Documentation is part of the
  definition of done.
- If the change involves PHI handling, authentication, authorisation, data
  retention, or logging, flag it in the PR description for heightened review.

## HIPAA Requirements

Non-negotiable for every contribution:

- Never include real patient data in code, tests, seed data, fixtures,
  commits, PRs, issues, logs, or documentation.
- Use synthetic or generated values everywhere.
- Do not log PHI fields.
- If you find code that could expose PHI, flag it explicitly in the PR rather
  than quietly fixing or ignoring it.

See [SECURITY.md](SECURITY.md) for how to report a suspected PHI exposure or
security vulnerability.

## Questions

- Setup problems: [docs/runbooks/LOCAL-DEV.md](docs/runbooks/LOCAL-DEV.md).
- Docker issues: [docs/runbooks/DOCKER-DEV.md](docs/runbooks/DOCKER-DEV.md).
- How the code is organised:
  [docs/architecture/OVERVIEW.md](docs/architecture/OVERVIEW.md).
- Everything else: the index at [docs/INDEX.md](docs/INDEX.md).
