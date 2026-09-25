# Deploying the portal

The end-to-end sequence for putting a change onto the server.

> **INCOMPLETE. Do not deploy from this yet.** Every step below marked **VERIFIED** was read from the
> server or the scripts on 2026-09-25. Every step marked **GAP** is something only Adrian knows and
> is to be filled during the walkthrough on Monday 28 September, 10:00 PT. The gaps are listed again
> at the end so they can be worked through in order.

## What runs where

**VERIFIED.** The server runs the `development` branch, not `main`. Promotion cascades
`main -> development -> staging -> production`, one direction only, and promotion PRs use rebase
rather than squash.

Deployment is manual, over SSH, building on the box. **There is no CI deploy step**: none of the
workflows contains ssh, scp, rsync, a registry push or a deployment action. `deploy-dev.yml` is
named misleadingly -- it validates a push to `development` and opens the promotion PR to `staging`.
It does not touch the server.

```bash
ssh -i ~/.ssh/appointment_portal apadmin@192.168.101.37     # office network
```

From home the box is on ZeroTier at `172.25.83.82`; the two addresses are not interchangeable and
the VPN does not route to the LAN subnet.

## Always use dc.sh, never docker compose directly

**VERIFIED**, and this is the single easiest way to break the deployment.

There is no `.env` in the repo root, so compose auto-loads nothing. A bare
`docker compose -f docker-compose.prod.yml up -d` resolves **every** secret to a blank string --
`MSSQL_SA_PASSWORD`, `MINIO_ROOT_USER`, `MINIO_ROOT_PASSWORD`, `TLS_CERT_PATH`, `TLS_KEY_PATH`,
`BASE_DOMAIN`, `ABP_NUGET_API_KEY`, `ABP_LICENSE_CODE` -- and cheerfully recreates the containers
with no database password, no TLS paths and no base domain. Compose prints warnings and carries on.

`scripts/hosting/dc.sh` injects `-f docker-compose.prod.yml` and `--env-file secrets/env.prod`, and
refuses to run if the env file is missing. It is a pass-through, so every compose subcommand works.

```bash
./scripts/hosting/dc.sh ps
./scripts/hosting/dc.sh build db-migrator api authserver angular
./scripts/hosting/dc.sh up -d
./scripts/hosting/dc.sh up -d --force-recreate reverse-proxy
```

**The tell that you got it wrong by hand:** `ps` prints "variable is not set" warnings and then lists
nothing, because the blank project context matches no running container.

## Before you start

- [ ] **Last night's backup succeeded.** **VERIFIED** as the right check. Expect
      `Result=success` and `ExecMainStatus=0`, and fresh `.bak` files for `CaseEvaluation` and each
      office database.

```bash
systemctl show hcs-portal-backup.service -p Result -p ExecMainStatus
```

- [ ] **Know the commit you are deploying and the one you are on.** `git log --oneline -1` before
      pulling, written down, is the rollback target.

- [ ] **GAP: is there a maintenance window, or an announcement?** Staff go-live has not happened, so
      this may be nothing today and something later.

## The sequence

### 1. Back up

**GAP: is the nightly backup relied on, or is an on-demand one taken first?**
`scripts/hosting/backup-databases.sh` exists and can be run by hand. Confirm whether a deploy
should take one rather than trusting last night's.

### 2. Pull

```bash
cd /home/apadmin/hcs-patient-portal
git pull --ff-only origin development
```

`--ff-only` on purpose: a merge commit created on the box would diverge the checkout from the branch
and the next pull would conflict.

**VERIFIED as a real hazard:** a pull replaces any hand-edited file. That is exactly how the off-box
backup broke for ten days in August, when a `chmod` on the box was undone by the next checkout. The
executable bits are now tracked in git (`100755` on all seven hosting scripts), so that specific
case is closed -- but the general rule stands: nothing hand-edited in the working tree survives.

### 3. Build the changed images

```bash
./scripts/hosting/dc.sh build db-migrator api authserver angular
```

**GAP: which services need rebuilding for which kind of change?** Building all four is safe but
slow. Confirm the shortcuts, especially whether an Angular-only change needs the backends rebuilt.

### 4. Run DbMigrator when the change carries migrations

**GAP: exact invocation.** It is a one-shot container. Confirm whether `up -d` runs it
automatically, or whether it is run explicitly, and how its exit code is checked.

**VERIFIED as necessary:** new images alone are not enough when a release carries migrations, and
**DbMigrator's log does not name the migrations it applied** -- it reports success without listing
them. Verify by name against the database instead:

```sql
SELECT MigrationId FROM [__EFMigrationsHistory] ORDER BY MigrationId DESC;
```

Run it against the host database and each office database. The migration must appear in **both**
sets, because host and tenant contexts have separate migration folders.

### 5. Bring the stack up

```bash
./scripts/hosting/dc.sh up -d
```

### 6. Force-recreate the reverse proxy

```bash
./scripts/hosting/dc.sh up -d --force-recreate reverse-proxy
```

**VERIFIED as load-bearing.** nginx resolves upstream container names once at worker start and
caches the IPs. A full `up -d` gives the backends new IPs but does **not** recreate the proxy, so
routing breaks silently. The tell is a 404 on the auth discovery endpoint while hitting the
container directly returns 200.

**GAP: is this needed on every deploy, or only when a backend container was recreated?**

## Verifying the deploy

Check artefacts, not exit codes. Each of these was run on 2026-09-25 and is known to work.

- [ ] **Containers healthy.**

```bash
./scripts/hosting/dc.sh ps
```

- [ ] **Migrations applied by name** -- the query in step 4, against host and each office database.

- [ ] **The app answers through the proxy.** For a machine-to-machine endpoint a refusal is a good
      signal, because it proves the route exists and the guard runs. Expect `403` with
      `{"errors":[{"code":"forbidden",...}]}`. A `404` means the route is not there, which is what a
      failed or partial deploy looks like.

```bash
curl -sk --resolve "admin.api.$BASE_DOMAIN:443:127.0.0.1" \
  "https://admin.api.$BASE_DOMAIN/api/integration/offices/<office-guid>/feed"
```

- [ ] **Reserved hostnames answer correctly.** Check the **body**, not the status -- a missing API
      route also answers 404.

```bash
curl -sk "https://api.$BASE_DOMAIN/" | grep -q missing_office_label && echo OK
```

- [ ] **Hosting scripts are still executable.** Cheap, and it broke the backups once. Expect only
      the systemd unit files, which are not executed.

```bash
git ls-files -s scripts/hosting/ | grep 100644
```

- [ ] **GAP: what does a smoke test look like for a UI change?** A login, a booking, something else?

## Rolling back

**GAP -- the whole section.** Needs: how to get back to the previous commit, whether images are
retagged or rebuilt, what happens to a migration that has already applied, and at what point a
database restore becomes the answer instead.

This is the most important gap on the page. A deploy procedure without a rehearsed rollback is a
one-way door.

## Gaps to close on Monday

1. Rollback, end to end -- the section above.
2. Whether a deploy takes its own backup or relies on the nightly one.
3. Which services need rebuilding for which kind of change.
4. How DbMigrator is actually invoked and its result checked.
5. Whether the proxy force-recreate is needed every time.
6. What a smoke test is for a UI change.
7. Whether there is a maintenance window or an announcement step.
8. Anything in `secrets/env.prod` a deployer needs to know about without the values being written
   down -- to be walked through on screen.

## Related

- [Backup and restore](hosting-backup-restore.md)
- [Local hosting verification](hosting-local-verification.md)
- [Case Tracker feed cutover](case-tracker-feed-cutover.md) -- per-office, after a deploy
