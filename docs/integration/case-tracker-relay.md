# Case Tracker document reads through Azure Relay

Audience: whoever deploys the portal on the Azure VM, and the Case Tracker owner. Goal: let the Case Tracker read
portal documents from MinIO when the portal runs on Azure, without opening MinIO to the internet.

Status: design agreed in principle 2026-10-13 (the Case Tracker will never have a fixed public address). The
portal-side pieces are in the repository and off by default. **Nothing here has been run against Azure Relay yet**;
the last section lists what is unproven. No secrets, keys or addresses are included.

## Why Relay

The Case Tracker reads documents in place, over S3, from the portal's MinIO
(`case-tracker-document-sharing.md`). On the office network it reaches MinIO's vhost, `minio.<base>`, which admits
private addresses only. On Azure the portal is public and the Case Tracker has no fixed address to allow-list, so
the only alternative would be opening that vhost to everyone and relying on the MinIO credential alone.

With Azure Relay (Hybrid Connections), both ends connect OUT to the relay on port 443 and the relay joins the two
connections. Nothing on either side accepts an inbound connection, and the MinIO vhost stays closed to the internet
(`MINIO_ALLOW_DIRECTIVES="deny all;"`).

## The path

```text
Case Tracker S3 client
  --TLS for minio.<base>-->  azbridge local forwarder on the Case Tracker box (a loopback address, port 443)
  --WebSocket over 443-->    Azure Relay, hybrid connection "case-tracker-documents"
  --WebSocket over 443-->    azbridge listener on the VM, inside the reverse proxy's network namespace
  --TCP to 127.0.0.1:443-->  nginx: TLS ends here (the Let's Encrypt certificate), vhost minio.<base>
  --HTTP-->                  MinIO
```

- **TLS runs end to end between the Case Tracker and nginx.** The relay forwards bytes; it only ever carries
  ciphertext. The two WebSocket legs are TLS as well.
- **nginx sees these connections from 127.0.0.1**, because the listener shares nginx's network namespace. The MinIO
  vhost always allows loopback, so it can deny every other address.
- **The read-only MinIO user still applies** (`scripts/hosting/minio-case-tracker-access.sh --shared`: one key,
  `s3:GetObject` on each listed office's prefix only; per-office keys remain possible). Relay controls who can reach
  nginx; the MinIO user controls what can be read.

## Credentials: two keys and one identity, each able to do one thing

| Who | Authenticates as | Can |
| --- | --- | --- |
| VM listener | the VM's managed identity, granted **Azure Relay Listener** on this hybrid connection only | listen; no key on the VM |
| VM listener, fallback only | SAS rule `vm-listen` (Listen) | listen. Its keys are not handed out unless the identity path fails |
| Case Tracker | SAS rule `case-tracker-send` (Send) | send. A leaked copy cannot listen, so it cannot impersonate the portal |
| Case Tracker, to MinIO | the shared read-only MinIO user `ct-shared` | read the listed offices' documents |

The hybrid connection requires client authorization, so an anonymous sender is refused. No key is a template
output. An Owner reads a rule's keys with
`az relay hyco authorization-rule keys list -g <rg> --namespace-name <ns> --hybrid-connection-name case-tracker-documents -n case-tracker-send`.
Each rule has a primary and a secondary key, so a key can be rotated without an outage: hand over the secondary,
then regenerate the primary.

## The Host header, and why S3 signatures still verify

S3 signature v4 signs the request's `Host` header, so the host the Case Tracker connects to must be the host MinIO
checks.

- **The Case Tracker connects to the real name, `https://minio.<base>`**, with a hosts-file entry on its box that
  points that name at the forwarder's loopback address. Then:
  - the Let's Encrypt certificate verifies (the JDK already trusts its roots);
  - SNI selects the MinIO vhost;
  - the signed host is the name nginx serves.
- **Port.** Port 443 for the forwarder is simplest: the client then signs `minio.<base>` with no port. Any other port
  also works, because nginx now forwards the client's `Host` header as sent (`proxy_set_header Host $http_host`).
  Measured 2026-10-13 against real nginx and MinIO, with a client on a non-default port: the previous `$host` (which
  drops the port) gave `SignatureDoesNotMatch`, and `$http_host` gave 200. For a client on 443 the two are identical,
  so nothing changes for existing callers.
- **Addressing and region are unchanged:** path-style, and region `us-east-1`.

## The VM side (`deployCaseTrackerRelay: true` in `infra/azure/fastpath`)

1. Redeploy the fast-path template with `deployCaseTrackerRelay: true` and `relayAlertEmail` set. It creates:
   - the namespace and the hybrid connection;
   - both SAS rules;
   - the VM's listener role;
   - the no-listener alert.
2. Load the pinned azbridge image. It ships only as a tarball on the project's GitHub release page:

   ```bash
   # azbridge 0.16.1 (2025-11-13). sha256 from the release's asset metadata:
   echo "29932efc594c44d20f8a7d92a3f66d784bdde8787fb31006a67d5b692a3715df  azbridge-oci-image-0.16.1.tar" | sha256sum -c
   docker load -i azbridge-oci-image-0.16.1.tar          # prints the tag it loaded
   docker image inspect -f '{{.Id}}' <that tag>          # the id to pin
   ```

3. Add to `secrets/env.prod` (see `env.prod.example`):
   - `RELAY_NAMESPACE` and `RELAY_HYBRID_CONNECTION` (template outputs);
   - `RELAY_AZBRIDGE_IMAGE` (the loaded tag) and `RELAY_AZBRIDGE_IMAGE_ID` (its id);
   - `MINIO_ALLOW_DIRECTIVES="deny all;"`.
4. Install and start `scripts/hosting/systemd/hcs-portal-relay.service`. It runs `scripts/hosting/relay-listener.sh`:
   - starts the listener in the reverse proxy's network namespace, forwarding to `127.0.0.1:443`;
   - re-attaches it whenever a deploy recreates the proxy (a joined container keeps the old, dead namespace);
   - refuses any image but the pinned one.
5. Check it. `journalctl -u hcs-portal-relay` shows "listener started", but that line only means the container
   started; azbridge itself logs almost nothing. The proof is the hybrid connection's `listenerCount`, which the
   VM's own identity can read (the Listener role includes read on its hybrid connection): an ARM `GET` of
   `.../namespaces/<namespace>/hybridConnections/case-tracker-documents` with a token from the instance metadata
   endpoint returns `properties.listenerCount = 1`. In the Azure portal, the `ActiveListeners` metric shows the same.

## The Case Tracker side

Three changes, plus one firewall rule. The portal owner hands over the `case-tracker-send` connection string and the
per-office MinIO credentials by a private channel.

1. **Install azbridge 0.16.1 as a service**, checking the sha256 first:
   - Windows: `azbridge_installer.0.16.1.win-x64.msi`
     (`bf536049f5dac9fa93a60649f4fb0d6dbacb8045eeb602af7878f8bb9e0234e8`). Service `azbridgesvc`, configured in
     `%ProgramData%\Microsoft\Azure Relay Bridge\azbridge_config.svc.yml`.
   - Linux: `azbridge.0.16.1.linux-x64.deb`
     (`193f1bcaba0772bc6027f9b592cb7fca255b6de1475e2f02e3edf6f51ed7a938`). Service `azbridge.service`, configured in
     `/etc/azbridge/azbridge_config.svc.yml`.

   ```yaml
   AzureRelayConnectionString: "<the case-tracker-send connection string; it ends EntityPath=case-tracker-documents>"
   LocalForward:
     - RelayName: case-tracker-documents
       BindAddress: 127.0.8.1
       BindPort: 443
   ```

   If port 443 is taken on that loopback address, use another port (for example 9443) and include it in the
   endpoint below.
2. **Hosts file:** `127.0.8.1  minio.<base>`.
3. **S3 settings, set together** (a per-field fallback makes a partial change fail as an authentication error):
   - endpoint `https://minio.<base>` (or `https://minio.<base>:9443`);
   - path-style addressing, region `us-east-1`;
   - the shared read-only MinIO access key and secret (`secrets/case-tracker-minio-shared.txt`).

   No truststore change is needed: Let's Encrypt chains to roots the JDK already ships.
4. **Firewall:** outbound TCP 443 to `<namespace>.servicebus.windows.net`. Hybrid Connections uses only WebSockets
   over 443.

## Failure modes and monitoring

| What breaks | What the Case Tracker sees | What tells us |
| --- | --- | --- |
| Listener down (script, container, identity, VM) | connection refused or reset at its local forwarder | Azure Monitor alert: no listener for 15 minutes (`relayAlertEmail`) |
| Listener crash-looping | the same | the alert above; and after five quick exits the unit fails and emails via `hcs-portal-backup-alert@` |
| Reverse proxy recreated by a deploy or rollback | a short gap while the stack restarts | nothing needed: `deploy-release.sh` and `rollback-release.sh` stop the listener before the recreate and start it after |
| Reverse proxy recreated any other way (by hand) | some reads fail for about four minutes | the script re-attaches within about 15 s (7 s measured), but the old connection's network is gone, so it cannot close and Azure keeps counting it until its keep-alive expires (`listenerCount` 2, measured 3 min 45 s). Stop the unit before a manual recreate and start it after |
| Case Tracker forwarder down, or its key wrong or rotated | connection refused locally, or the relay refuses the sender | the Case Tracker's own errors; Relay metric `SenderConnections-ClientError` |
| Its MinIO credential wrong or rotated | `403 InvalidAccessKeyId` or `SignatureDoesNotMatch` | the Case Tracker's own errors; reissue with `minio-case-tracker-access.sh --shared <office ids> --rotate` |
| Its hosts entry or endpoint wrong | TLS name mismatch, or a request to the wrong host | the Case Tracker's own errors |
| Azure Relay regional outage | as listener down | the alert; no portal function other than Case Tracker document reads is affected |

Documents stay closed to the internet in every case: the MinIO vhost denies everything but loopback.

## The changes feed: through Relay, or public HTTPS?

The Case Tracker also pulls the portal's changes feed from `<office>.api.<base>`. Levon decides; both are workable.

- **A. Recommended: the feed stays on public HTTPS with its token.** The API host is public anyway, because people
  use it. The feed endpoint is already authenticated (a token the portal issues), rate-limited per address, and TLS
  end to end. Relay would close one path on a host that stays open for everything else. No extra cost, no second
  listener to keep alive.
- **B. The feed through Relay too.** A second hybrid connection and listener (about $10 a month more). nginx could then
  refuse the feed path from the internet, so a stolen feed token alone would be useless from outside. Defence in
  depth, at the price of a second moving part on both machines.

Recommendation: A now. B is an option if the feed token's exposure ever becomes a concern.

## Cost

About $15 a month: the listener is $9.78 ($0.0134 an hour), plus data beyond the free 5 GB at $1 per GB (about 10 GB
of documents a month), plus about $0.10 for the alert. Option B adds about $10 a month. Details in
`infra/azure/fastpath/COSTS.md`.

## Proven on the VM (2026-10-19 and 2026-10-20)

- **azbridge signs in with the VM's managed identity**, with no key anywhere on the VM, from a container that shares
  the reverse proxy's network namespace. The instance metadata endpoint answers from that namespace, and
  `listenerCount` went to 1.
- **The re-attach after a proxy recreate:** the script logged the re-attach 7 s after the recreate. The stranded old
  connection is the four-minute row in the table above.
- **Azure Relay is within Microsoft's HIPAA BAA scope**, per the compliance offerings appendix in the Service Trust
  Portal (checked 2026-10-19).

## Still unproven, to check before switching the Case Tracker's document reads on

- The whole chain end to end with the Case Tracker's real S3 client, the hosts entry and a document near the 100 MB
  vhost limit.
- The no-listener alert actually firing: stop the unit for 20 minutes once, as a drill.
