#!/usr/bin/env bash
# render-config.sh <worktree-path> <AUTH_PORT> <API_PORT> <NG_PORT> <DB_NAME>
#
# Renders per-worktree appsettings.Local.json (AuthServer, HttpApi.Host,
# DbMigrator) + angular/src/environments/environment.local.ts.
# Uses Python's json.dump for the JSON files so LocalDB backslash escaping is
# handled by the library rather than by hand-rolled bash heredoc (which was a
# historical footgun -- see docs/plans/2026-04-22-worktree-setup-resume.md).
set -euo pipefail

WT="${1:?usage: render-config.sh <worktree> <AUTH> <API> <NG> <DB_NAME>}"
AUTH="${2:?}"
API="${3:?}"
NG="${4:?}"
DB="${5:?}"

[ -d "$WT" ] || { echo "error: worktree path '$WT' does not exist" >&2; exit 1; }

# AuthServer
python3 - "$WT" "$AUTH" "$API" "$NG" "$DB" <<'PY'
import json, sys, os
wt, auth, api, ng, db = sys.argv[1:]
# The literal backslash in the LocalDB server name. json.dump will escape it
# as \\ in the JSON output so the runtime parser produces one literal backslash.
conn = f"Server=(LocalDb)\\MSSQLLocalDB;Database={db};Trusted_Connection=True;TrustServerCertificate=true"
cfg = {
    "App": {
        "SelfUrl": f"https://localhost:{auth}",
        "AngularUrl": f"http://localhost:{ng}",
        "CorsOrigins": f"http://localhost:{ng},https://localhost:{api}",
        "RedirectAllowedUrls": f"http://localhost:{ng}",
    },
    "AuthServer": {"Authority": f"https://localhost:{auth}"},
    "ConnectionStrings": {"Default": conn},
    "Kestrel": {"Endpoints": {"Https": {"Url": f"https://localhost:{auth}"}}},
}
path = os.path.join(wt, "src", "HealthcareSupport.CaseEvaluation.AuthServer", "appsettings.Local.json")
os.makedirs(os.path.dirname(path), exist_ok=True)
with open(path, "w", encoding="utf-8") as f:
    json.dump(cfg, f, indent=2)
    f.write("\n")
print(f"wrote {path}")
PY

# HttpApi.Host
python3 - "$WT" "$AUTH" "$API" "$NG" "$DB" <<'PY'
import json, sys, os
wt, auth, api, ng, db = sys.argv[1:]
conn = f"Server=(LocalDb)\\MSSQLLocalDB;Database={db};Trusted_Connection=True;TrustServerCertificate=true"
cfg = {
    "App": {
        "SelfUrl": f"https://localhost:{api}",
        "AngularUrl": f"http://localhost:{ng}",
        "CorsOrigins": f"http://localhost:{ng},https://localhost:{auth}",
    },
    "AuthServer": {
        "Authority": f"https://localhost:{auth}",
        "MetaAddress": f"https://localhost:{auth}",
    },
    "ConnectionStrings": {"Default": conn},
    "Kestrel": {"Endpoints": {"Https": {"Url": f"https://localhost:{api}"}}},
}
path = os.path.join(wt, "src", "HealthcareSupport.CaseEvaluation.HttpApi.Host", "appsettings.Local.json")
os.makedirs(os.path.dirname(path), exist_ok=True)
with open(path, "w", encoding="utf-8") as f:
    json.dump(cfg, f, indent=2)
    f.write("\n")
print(f"wrote {path}")
PY

# DbMigrator
python3 - "$WT" "$AUTH" "$API" "$NG" "$DB" <<'PY'
import json, sys, os
wt, auth, api, ng, db = sys.argv[1:]
conn = f"Server=(LocalDb)\\MSSQLLocalDB;Database={db};Trusted_Connection=True;TrustServerCertificate=true"
cfg = {
    "ConnectionStrings": {"Default": conn},
    "OpenIddict": {
        "Applications": {
            "CaseEvaluation_App":     {"RootUrl": f"http://localhost:{ng}"},
            "CaseEvaluation_Swagger": {"RootUrl": f"https://localhost:{api}/"},
        }
    },
}
path = os.path.join(wt, "src", "HealthcareSupport.CaseEvaluation.DbMigrator", "appsettings.Local.json")
os.makedirs(os.path.dirname(path), exist_ok=True)
with open(path, "w", encoding="utf-8") as f:
    json.dump(cfg, f, indent=2)
    f.write("\n")
print(f"wrote {path}")
PY

# Angular environment.local.ts
mkdir -p "$WT/angular/src/environments"
cat > "$WT/angular/src/environments/environment.local.ts" <<EOF
import { Environment } from '@abp/ng.core';
const baseUrl = 'http://localhost:$NG';
export const environment = {
  production: false,
  application: { baseUrl, name: 'CaseEvaluation' },
  oAuthConfig: {
    issuer: 'https://localhost:$AUTH/', redirectUri: baseUrl,
    clientId: 'CaseEvaluation_App', responseType: 'code',
    scope: 'offline_access CaseEvaluation', requireHttps: true,
  },
  apis: {
    default: { url: 'https://localhost:$API', rootNamespace: 'HealthcareSupport.CaseEvaluation' },
    AbpAccountPublic: { url: 'https://localhost:$AUTH', rootNamespace: 'AbpAccountPublic' },
  },
} as Environment;
EOF
echo "wrote $WT/angular/src/environments/environment.local.ts"
