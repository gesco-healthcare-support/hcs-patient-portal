---
id: OBS-9
title: Two-stack port-pinning via per-worktree `.env` — abandoned approach
severity: observation
found: 2026-05-14
flow: docker-orchestration
status: superseded
---

# OBS-9 — Port-pinning via `.env` (abandoned 2026-05-14)

## Status
**Superseded.** Initially applied 2026-05-14, then reverted same day. Adrian's decision: "We will only run canonical defaults and do this the slow way."

## What was tried
The test session at `W:\patient-portal\main` and the fix session at `C:\src\patient-portal\replicate-old-app` collided on canonical ports (4200/44368/44327/1434/...) when run concurrently. Two mitigations attempted in sequence:

1. **First attempt** — `docker-compose.testing.yml` overlay + 8 env vars exported in the shell. Fragile: forgetting either dropped the stack back to canonical and broke the parallel-session workflow. Also revealed [[BUG-013]] (CORS missed AuthServer self-port), [[BUG-014]] (hardcoded URLs in emails), [[BUG-015]] (dynamic-env.json never read), [[BUG-016]] (OpenIddict subdomain RedirectUris).

2. **Second attempt** — pin alt ports in `W:\patient-portal\main\.env`. Cleaner mechanically: `docker compose up -d` worked without flags or exports. But the four BUGs in #1 made the SPA still hit canonical URLs and OpenIddict still rejected the redirect URI. The workaround stack required edits to `environment.docker.ts`, `docker-compose.yml` CORS, and SQL fixups to `OpenIddictApplications` — all of which Adrian classified as hardcoding.

## Why abandoned
Adrian's direction (2026-05-14): *"This parallel sessions never work. Stop all docker containers clean all docker cache, data etc. and run on canonical ports. We don't hard code anything though."*

New workflow: serial sessions on canonical ports. Test session finds bugs → fix session implements + pushes to main → test session reruns workflows on the fresh canonical-port build.

## Open follow-up
[[BUG-013]], [[BUG-014]], [[BUG-015]], [[BUG-016]] should still be fixed properly in the fix session. They are real bugs, just not directly relevant to the day-to-day workflow now that we're not running two stacks at once.
