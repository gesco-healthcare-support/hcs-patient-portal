#!/usr/bin/env bash
# DEPLOY REHEARSAL (09-30: the third migrator failure was a RUNTIME crash that only a real database reaches; the
# no-network pre-flight proves dependency injection only). Runs the tagged migrator image -- the one the deploy just
# built -- against a THROWAWAY restore of the newest backups, on an INTERNAL docker network (no route to the live
# database or anywhere else), with a throwaway admin-password folder. The live stack and live data are never touched.
# Secrets reach the containers through pipes from the compose config and are never printed or written to disk.
# Prints "REHEARSAL PASS" only if the migrator exits 0 AND logs "Successfully completed all database migrations".
# Usage (on the box, from anywhere): bash /tmp/rehearse-migrator.sh [image]
set -u
IMG=${1:-hcs-patient-portal-db-migrator}
cd ~/hcs-patient-portal || { echo "REHEARSAL FAIL: no checkout"; exit 1; }
C="docker compose --env-file secrets/env.prod -f docker-compose.prod.yml"
NET=hcs-rehearsal-net SQL=hcs-rehearsal-sql MIG=hcs-rehearsal-migrator
ts() { date -u +%H:%M:%SZ; }
fail() { echo "$(ts) REHEARSAL FAIL: $*"; exit 1; }
PF=$(mktemp -d)
cleanup() { docker rm -f "$MIG" "$SQL" > /dev/null 2>&1 < /dev/null; docker network rm "$NET" > /dev/null 2>&1 < /dev/null; sudo rm -rf -- "$PF" < /dev/null; }
trap cleanup EXIT
cleanup; PF=$(mktemp -d)
sudo chown 1654:1654 "$PF" < /dev/null && sudo chmod 700 "$PF" < /dev/null
svc_env() { $C config --format json < /dev/null 2>/dev/null | python3 -c 'import json,sys; e=json.load(sys.stdin)["services"][sys.argv[1]].get("environment") or {}; [print(f"{k}={v}") for k, v in e.items() if v is not None and "\n" not in str(v)]' "$1"; }
SQLIMG=$($C config --format json < /dev/null 2>/dev/null | python3 -c 'import json,sys; print(json.load(sys.stdin)["services"]["sql-server"]["image"])')
echo "$(ts) image $IMG ($(docker image inspect "$IMG" --format '{{.Created}}' < /dev/null)); sql $SQLIMG; free $(free -m | awk '/Mem:/{print $7}')MB"

docker network create --internal "$NET" > /dev/null < /dev/null || fail "network create"
docker run -d --name "$SQL" --network "$NET" --network-alias sql-server --env-file <(svc_env sql-server) "$SQLIMG" > /dev/null < /dev/null || fail "scratch sql start"
q() { docker exec "$SQL" bash -c '/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -b -h -1 -W -s "|" -Q "$0"' "$1" < /dev/null; }
for _ in $(seq 1 45); do q "SET NOCOUNT ON; SELECT 1" > /dev/null 2>&1 && break; sleep 2; done
q "SET NOCOUNT ON; SELECT 1" > /dev/null 2>&1 || fail "scratch sql not ready after 90 s"

STAMP=$(ls -t backups/*.bak | head -1 | grep -oE '[0-9]{8}-[0-9]{6}')
[ -n "$STAMP" ] || fail "no backup found"
docker exec "$SQL" mkdir -p /var/opt/mssql/backup < /dev/null
for bak in backups/*_"$STAMP".bak; do
  db=$(basename "$bak" "_$STAMP.bak")
  sudo docker cp "$bak" "$SQL:/var/opt/mssql/backup/$db.bak" > /dev/null < /dev/null || fail "copy $bak"   # dumps are owned by the sql uid 10001
  files=$(q "SET NOCOUNT ON; RESTORE FILELISTONLY FROM DISK = N'/var/opt/mssql/backup/$db.bak'" | awk -F'|' 'NF>2 {print $1 "|" $3}')
  moves=$(printf '%s\n' "$files" | awk -F'|' -v d="$db" '{ext = ($2 == "L") ? "_log.ldf" : ((n++ == 0) ? ".mdf" : "_" n ".ndf"); printf ", MOVE N'"'"'%s'"'"' TO N'"'"'/var/opt/mssql/data/%s%s'"'"'", $1, d, ext}')
  q "RESTORE DATABASE [$db] FROM DISK = N'/var/opt/mssql/backup/$db.bak' WITH REPLACE$moves" > /dev/null 2>&1 || fail "restore $db"
  echo "$(ts) restored $db from $STAMP"
done

# --disable-redis: the migrator's own switch (Program.cs); the rehearsal network has no redis.
docker run -d --name "$MIG" --network "$NET" --env-file <(svc_env db-migrator) \
  -e AdminPasswords__Directory=/run/admin-passwords -e AdminPasswords__VaultUri= \
  -v "$PF:/run/admin-passwords" "$IMG" --disable-redis > /dev/null < /dev/null || fail "migrator start"
for _ in $(seq 1 150); do [ "$(docker inspect -f '{{.State.Running}}' "$MIG" < /dev/null)" = false ] && break; sleep 2; done
code=$(docker inspect -f '{{.State.ExitCode}}' "$MIG" < /dev/null)
log=$(docker logs "$MIG" 2>&1 < /dev/null)
printf '%s\n' "$log" | grep -E 'Executing .* seed|Rotated the admin password|Successfully completed|Unhandled exception|Exception:' | grep -vE '^\s+at ' | cut -c1-240 | tail -20
done_line=$(printf '%s\n' "$log" | grep -c 'Successfully completed all database migrations')
[ "$code" = 0 ] && [ "$done_line" -ge 1 ] || fail "migrator exit=$code, 'Successfully completed all database migrations' x$done_line"
echo "$(ts) REHEARSAL PASS (exit 0; rotation lines: $(printf '%s\n' "$log" | grep -c 'Rotated the admin password'))"
