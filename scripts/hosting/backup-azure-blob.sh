#!/usr/bin/env bash
# Off-server backup to Azure Blob Storage (2026-10-12). The Azure counterpart of backup-offbox.sh, for the
# portal running on an Azure VM, where the office backup host that script ships to is not reachable.
#
# WHAT IT KEEPS, AND WHERE
#   Databases: this run's .bak of the host database AND every office database, uploaded together under
#   <db container>/<run stamp>/, with MANIFEST.sha256 uploaded LAST. A restore needs a matched set -- every
#   database from one run -- so a run folder WITHOUT a manifest is incomplete and must not be restored.
#   Documents: every MinIO bucket in AZURE_BACKUP_BUCKETS, copied to <documents container>/<bucket>/.
#   Incremental: only new or changed objects are sent, so a night's work grows with what changed, not with
#   the whole cabinet. (backup-offbox.sh tars every document every night; its own header says that will not
#   scale.)
#
# IT NEVER DELETES ANYTHING IN AZURE
#   rclone COPY only -- never sync, move, delete or purge. A document deleted or replaced at the source must
#   not take its backup with it: documents and appointment data are kept seven years. How long anything is
#   kept is the storage account's job (versioning, soft delete, lifecycle tiers), not this script's, and an
#   object this script overwrites survives as a blob version. tests/python/test_backup_azure_blob.py fails if
#   a destructive rclone command appears here.
#
# AUTHENTICATION
#   The VM's managed identity. There is no storage key anywhere. The identity needs Storage Blob Data
#   Contributor on the two containers. MinIO is read with the portal's scoped app key, not the root one.
#
# HOW IT RUNS
#   rclone, pinned by digest, as a throwaway container on the compose network so it reaches minio by service
#   name. Nothing is installed on the host. Credentials reach the container through an env file only root can
#   read, deleted on exit -- never on a command line, where the process list and the journal would show them.
#
# HOW IT FINDS THIS RUN'S DUMPS
#   By which .bak files the dump created, not by guessing their time stamp: backup-databases.sh names them
#   with a stamp it computes itself, so a stamp computed here can differ by a second. Every new file must
#   carry one shared stamp, and the host database must be among them, or the run stops.
#
# MARKERS AND ALERTS -- the same as backup-offbox.sh, on purpose
#   last-success is touched only after every step succeeded, and last-restore-proof only after a proven
#   restore, so backup-freshness-check.sh works unchanged on the VM. A failure exits non-zero, and OnFailure=
#   on the units emails BACKUP_ALERT_RECIPIENTS through backup-alert.sh. Do not touch a marker earlier.
#
# USAGE (as root, from the deploy root)
#   scripts/hosting/backup-azure-blob.sh                   nightly run
#   scripts/hosting/backup-azure-blob.sh --verify-restore  also download the host dump back from Azure,
#                                                          check it byte for byte, and restore it into a
#                                                          scratch database that is then dropped
#   scripts/hosting/backup-azure-blob.sh --dry-run         dump nothing, upload nothing, touch no marker;
#                                                          list what would be copied. Like every run, it
#                                                          first proves SQL Server can write BACKUP_DIR
#
# Config, read from secrets/env.prod (never echoed):
#   AZURE_BACKUP_STORAGE_ACCOUNT      required: the backup storage account's name
#   AZURE_BACKUP_DB_CONTAINER         default db-backups
#   AZURE_BACKUP_DOCUMENTS_CONTAINER  default documents
#   AZURE_BACKUP_BUCKETS              default case-evaluation-documents (separate several with spaces or commas)
#   AZURE_BACKUP_MSI_CLIENT_ID        optional: a user-assigned identity's client id; unset = system-assigned
#   MINIO_APP_ACCESS_KEY, MINIO_APP_SECRET_KEY   the scoped MinIO key the app already uses
# Env (all optional): ENV_FILE, BACKUP_DIR, BACKUP_RETENTION_DAYS (local .bak files only; default 14),
#   MARKER_DIR, COMPOSE_FILE, DOCKER_NET, RCLONE_IMAGE, and AZURE_BACKUP_EMULATOR_ENDPOINT -- LOCAL TESTING
#   ONLY: talk to an Azurite emulator with its public development key instead of the managed identity.
set -euo pipefail
# One collation for sort and comm, whatever the caller's locale.
export LC_ALL=C

REPO_DIR="${REPO_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"
cd "$REPO_DIR"

ENV_FILE="${ENV_FILE:-secrets/env.prod}"
BACKUP_DIR="${BACKUP_DIR:-./backups}"
MARKER_DIR="${MARKER_DIR:-/var/backups/hcs-portal}"
COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.prod.yml}"
DOCKER_NET="${DOCKER_NET:-hcs-patient-portal_default}"
RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-14}"
# rclone 1.75.1 (2026-09-04), pinned by digest so the tool cannot change without a commit.
RCLONE_IMAGE="${RCLONE_IMAGE:-rclone/rclone:1.75.1@sha256:45401ad7410db1d67ffdb58e19059ad20b0d8e0285a60e38bbec55cc1019c7a5}"

log() { echo "[$(date -u +%FT%TZ)] $*"; }
fail() { echo "[$(date -u +%FT%TZ)] ERROR: $*" >&2; exit 1; }

dry_run=false
verify_restore=false
for arg in "$@"; do
  case "$arg" in
    --dry-run) dry_run=true ;;
    --verify-restore) verify_restore=true ;;
    *) fail "unknown argument: $arg (expected --dry-run and/or --verify-restore)" ;;
  esac
done
if $dry_run && $verify_restore; then
  fail "--dry-run and --verify-restore cannot be combined: a restore proof needs a real upload"
fi

# One key from the env file; empty when absent. Never echoed.
env_value() { local key="$1"; { grep -E "^${key}=" "$ENV_FILE" || true; } | head -1 | cut -d= -f2-; }

# ---------------------------------------------------------------- 1. pre-flight
log "1/7 pre-flight"
[[ -f "$ENV_FILE" ]] || fail "env file not found: $ENV_FILE"

account="$(env_value AZURE_BACKUP_STORAGE_ACCOUNT)"
db_container="$(env_value AZURE_BACKUP_DB_CONTAINER)"
db_container="${db_container:-db-backups}"
docs_container="$(env_value AZURE_BACKUP_DOCUMENTS_CONTAINER)"
docs_container="${docs_container:-documents}"
buckets_raw="$(env_value AZURE_BACKUP_BUCKETS)"
buckets_raw="${buckets_raw:-case-evaluation-documents}"
msi_client_id="$(env_value AZURE_BACKUP_MSI_CLIENT_ID)"
minio_key="$(env_value MINIO_APP_ACCESS_KEY)"
minio_secret="$(env_value MINIO_APP_SECRET_KEY)"
emulator="${AZURE_BACKUP_EMULATOR_ENDPOINT:-}"

[[ -n "$account" || -n "$emulator" ]] || fail "AZURE_BACKUP_STORAGE_ACCOUNT is not set in $ENV_FILE"
[[ -n "$minio_key" && -n "$minio_secret" ]] || fail "MINIO_APP_ACCESS_KEY / MINIO_APP_SECRET_KEY not found in $ENV_FILE"
read -r -a buckets <<< "${buckets_raw//,/ }"
[[ "${#buckets[@]}" -gt 0 ]] || fail "AZURE_BACKUP_BUCKETS names no bucket"
docker network inspect "$DOCKER_NET" > /dev/null 2>&1 || fail "docker network ${DOCKER_NET} not found -- is the stack up?"

mkdir -p "$BACKUP_DIR" "$MARKER_DIR"
backup_abs="$(cd "$BACKUP_DIR" && pwd)"

# SQL Server writes the dumps itself, as the container's mssql user (uid 10001), into /var/opt/mssql/backups,
# which compose bind-mounts from BACKUP_DIR. When BACKUP_DIR does not exist at the first `up`, Docker creates it
# root-owned, mode 755, and every BACKUP DATABASE then fails with "Operating system error 5(Access is denied.)".
# A dry run dumps nothing, so it used to pass while the first real run failed (first VM bring-up, 2026-10-19).
# So every run, dry or not, first writes and removes one empty file there AS the container's own user.
probe="/var/opt/mssql/backups/.write-probe-$$"
docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" exec -T sql-server \
  bash -c 'touch "$0" && rm -f "$0"' "$probe" < /dev/null > /dev/null 2>&1 \
  || fail "SQL Server (uid 10001 in its container) cannot write its backup folder ${BACKUP_DIR}, or sql-server is not running. Fix the folder with: sudo chown 10001 ${BACKUP_DIR} && sudo chmod 775 ${BACKUP_DIR}"
stage="${MARKER_DIR}/azure-stage"
mkdir -p "$stage"

# One run at a time: the nightly and the weekly timers can overlap on a Sunday.
exec 9> "${MARKER_DIR}/backup-azure.lock"
flock -w 7200 9 || fail "another backup run still holds ${MARKER_DIR}/backup-azure.lock after two hours"

# rclone's configuration, readable only by root, gone when the script exits however it exits.
rclone_env="$(umask 077 && mktemp "${stage}/rclone-env.XXXXXX")"
proof_file=""
cleanup() {
  rm -f "$rclone_env"
  [[ -z "$proof_file" ]] || rm -f "${backup_abs}/${proof_file}"
}
trap cleanup EXIT

{
  # Every remote is defined here, so there is no config file; this stops rclone logging a "not found"
  # notice on every call, which would crowd the journal lines a failure alert quotes.
  echo "RCLONE_CONFIG=/dev/null"
  echo "RCLONE_CONFIG_AZ_TYPE=azureblob"
  echo "RCLONE_CONFIG_AZ_NO_CHECK_CONTAINER=true"
  if [[ -n "$emulator" ]]; then
    echo "RCLONE_CONFIG_AZ_USE_EMULATOR=true"
    echo "RCLONE_CONFIG_AZ_ENDPOINT=${emulator}"
  else
    echo "RCLONE_CONFIG_AZ_ACCOUNT=${account}"
    echo "RCLONE_CONFIG_AZ_USE_MSI=true"
    [[ -z "$msi_client_id" ]] || echo "RCLONE_CONFIG_AZ_MSI_CLIENT_ID=${msi_client_id}"
  fi
  # S5332 does not apply: minio is reached by service name on the private compose bridge and serves
  # plain HTTP there; TLS ends at the reverse proxy. See the same note in backup-offbox.sh.
  echo "RCLONE_CONFIG_MINIO_TYPE=s3"
  echo "RCLONE_CONFIG_MINIO_PROVIDER=Minio"
  echo "RCLONE_CONFIG_MINIO_ENDPOINT=http://minio:9000"
  echo "RCLONE_CONFIG_MINIO_ACCESS_KEY_ID=${minio_key}"
  echo "RCLONE_CONFIG_MINIO_SECRET_ACCESS_KEY=${minio_secret}"
  echo "RCLONE_CONFIG_MINIO_NO_CHECK_BUCKET=true"
} > "$rclone_env"

rc() {
  docker run --rm --network "$DOCKER_NET" --env-file "$rclone_env" \
    -v "${backup_abs}:/backups" -v "${stage}:/stage" \
    "$RCLONE_IMAGE" "$@"
}

dry=()
if $dry_run; then
  dry=(--dry-run)
  log "     DRY RUN: nothing is dumped, uploaded or marked"
fi
[[ -z "$emulator" ]] || log "     EMULATOR MODE (${emulator}) -- local testing only, never on a server"

# ---------------------------------------------------------------- 2. databases
list_baks() { find "$backup_abs" -maxdepth 1 -name '*.bak' -type f -printf '%f\n' | sort; }
stamp_of() { sed -nE 's/^.+_([0-9]{8}-[0-9]{6})\.bak$/\1/p'; }

if $dry_run; then
  log "2/7 dry run: using the newest dump set already on disk"
  run_stamp="$(list_baks | stamp_of | sort -u | tail -1)"
  [[ -n "$run_stamp" ]] || log "     no dump on disk yet; the database step would follow a real dump"
else
  log "2/7 dumping databases (host + every office) via backup-databases.sh"
  before="$(list_baks)"
  BACKUP_DIR="$BACKUP_DIR" bash ./scripts/hosting/backup-databases.sh
  created="$(comm -13 <(printf '%s\n' "$before") <(list_baks) | sed '/^$/d')"
  [[ -n "$created" ]] || fail "the dump created no .bak file"
  stamps="$(printf '%s\n' "$created" | stamp_of | sort -u)"
  [[ "$(printf '%s\n' "$stamps" | sed '/^$/d' | wc -l)" -eq 1 ]] \
    || fail "this run's dumps do not share one time stamp: ${stamps//$'\n'/ }"
  run_stamp="$stamps"
  [[ "$(printf '%s\n' "$created" | stamp_of | wc -l)" -eq "$(printf '%s\n' "$created" | wc -l)" ]] \
    || fail "the dump created a file not named <database>_<stamp>.bak"
  [[ -f "${backup_abs}/CaseEvaluation_${run_stamp}.bak" ]] \
    || fail "the host database dump CaseEvaluation_${run_stamp}.bak is missing from this run"
  log "     run ${run_stamp}: $(printf '%s\n' "$created" | wc -l) dump(s)"

  # Local copies only. Nothing here can reach the Azure copies (each run's manifest is in Azure too).
  find "$backup_abs" -maxdepth 1 -name '*.bak' -type f -mtime "+${RETENTION_DAYS}" -print -delete
  find "$stage" -maxdepth 1 -name '*.sha256' -type f -mtime "+${RETENTION_DAYS}" -delete
fi

# ---------------------------------------------------------------- 3-4. upload the matched set, manifest last
if [[ -n "${run_stamp:-}" ]]; then
  run_dir="${db_container}/${run_stamp}"
  log "3/7 uploading run ${run_stamp} to ${run_dir}/"
  rc copy "${dry[@]}" /backups "az:${run_dir}" --max-depth 1 --include "*_${run_stamp}.bak" \
    || fail "uploading the database dumps of run ${run_stamp} failed"

  if ! $dry_run; then
    log "4/7 verifying the upload, then writing the manifest"
    rc check /backups "az:${run_dir}" --one-way --max-depth 1 --include "*_${run_stamp}.bak" \
      || fail "the uploaded dumps of run ${run_stamp} do not match the local ones"
    manifest="${stage}/${run_stamp}.sha256"
    (cd "$backup_abs" && sha256sum --binary -- *_"${run_stamp}".bak) > "$manifest"
    # Last, on purpose: a run folder with a manifest is a complete, verified set.
    rc copyto "/stage/${run_stamp}.sha256" "az:${run_dir}/MANIFEST.sha256" \
      || fail "writing MANIFEST.sha256 for run ${run_stamp} failed -- the set is uploaded but not marked complete"
    log "     $(wc -l < "$manifest") dump(s) and MANIFEST.sha256 in ${run_dir}/"
  fi
fi

# ---------------------------------------------------------------- 5. documents
log "5/7 copying documents (new and changed objects only; nothing is ever deleted)"
for bucket in "${buckets[@]}"; do
  rc copy "${dry[@]}" "minio:${bucket}" "az:${docs_container}/${bucket}" --fast-list \
    || fail "copying documents from ${bucket} failed"
  if ! $dry_run; then
    rc check "minio:${bucket}" "az:${docs_container}/${bucket}" --one-way --fast-list \
      || fail "a document in ${bucket} is missing or different in Azure after the copy"
  fi
  log "     ${bucket} -> ${docs_container}/${bucket}/"
done

# ---------------------------------------------------------------- 6. optional restore proof
if $verify_restore; then
  log "6/7 restore proof: the host dump, downloaded back FROM AZURE, into a scratch database"
  # Not named *.bak, so no retention prune or other job mistakes it for a dump; removed on exit.
  proof_file="azure-roundtrip-${run_stamp}.bakproof"
  rc copyto "az:${run_dir}/CaseEvaluation_${run_stamp}.bak" "/backups/${proof_file}" \
    || fail "downloading the host dump back from Azure failed"

  expected="$(awk -v f="CaseEvaluation_${run_stamp}.bak" '$2 == f || $2 == "*"f { print $1 }' "$manifest")"
  actual="$(sha256sum "${backup_abs}/${proof_file}" | cut -d' ' -f1)"
  [[ -n "$expected" && "$expected" == "$actual" ]] \
    || fail "the host dump read back from Azure does not match its manifest -- the Azure copy is not trustworthy"

  sa_password="$(env_value MSSQL_SA_PASSWORD)"
  [[ -n "$sa_password" ]] || fail "MSSQL_SA_PASSWORD not found in $ENV_FILE"
  sqlc() { docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" exec -T sql-server \
    /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$sa_password" -C -b "$@"; }
  cbase="/var/opt/mssql/backups/${proof_file}"
  scratch="RestoreProofAzure_${run_stamp//-/_}"
  # MOVE to fresh files so the scratch copy cannot touch the live database's data files.
  move=""
  i=0
  for lf in $(sqlc -h -1 -W -Q "SET NOCOUNT ON; RESTORE FILELISTONLY FROM DISK=N'${cbase}';" | awk '{print $1}' | grep -v '^$'); do
    i=$((i + 1))
    move="${move} MOVE N'${lf}' TO N'/var/opt/mssql/data/${scratch}_${i}.dat',"
  done
  sqlc -Q "RESTORE DATABASE [${scratch}] FROM DISK=N'${cbase}' WITH ${move} RECOVERY, REPLACE;" \
    || fail "RESTORE FAILED from the Azure copy -- the backup is not trustworthy"
  tables="$(sqlc -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM [${scratch}].sys.tables;" | tr -d '\r ')"
  sqlc -Q "DROP DATABASE [${scratch}];" > /dev/null
  log "     RESTORE PROVED from Azure: sha256 matches the manifest, ${tables} tables read back, scratch dropped"
  touch "${MARKER_DIR}/last-restore-proof"
else
  log "6/7 restore proof skipped (pass --verify-restore to run it)"
fi

# ---------------------------------------------------------------- 7. done
if $dry_run; then
  log "7/7 DRY RUN complete: nothing was dumped, uploaded or marked"
  exit 0
fi
log "7/7 DONE ${run_stamp}: databases and documents are in storage account ${account:-emulator}"
# Last, on purpose: the freshness check reads this as "every step above succeeded".
touch "${MARKER_DIR}/last-success"
