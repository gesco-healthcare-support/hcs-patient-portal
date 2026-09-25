# Case Tracker feed cutover, per office (#968)

Moving one office from the outbound push to the changes feed the Case Tracker pulls, and back again
if it goes wrong.

> **Verified against the merged endpoint.** The feed landed on `main` in #1065. Every portal
> behaviour below was read from source rather than from a description of it, and re-checked after
> the merge: the path, header, parameters, page size, allowance, alert thresholds and floor
> derivation are all unchanged from the pre-merge branch. The four cursor and skip refusals moved
> from 400 to 409 before merge; that is reflected below.

## Why there is a cutover at all

Under the push, the portal calls the Case Tracker. Once the portal is publicly hosted and the Case
Tracker stays private, that direction stops working, so the Case Tracker pulls instead. The two
cannot both be live for one office: the feed hands out rows the push would send again.

Cutover is therefore per office, one switch, with a rollback.

## Before the first office, once

- [ ] **The Case Tracker is deployed with its feed commits.** Their running jar predated them as of
      2026-09-25, and the feed ships disabled on their side regardless. Do not size a first
      end-to-end test as "both sides are ready" until their deploy has happened. Confirm with them
      rather than assuming.
- [ ] **The feed token is issued out of band and set on BOTH sides.** Portal config key is
      `CaseTracker:FeedToken`. It is a secret: user secrets locally, the env file in production,
      never committed, never logged.
- [ ] **Alert recipients are set.** `CaseTracker:FeedAlertRecipients`, separated by `;` or `,`.
      Technical recipients, not intake staff: a stalled consumer can only be fixed by whoever runs
      the two systems.

### The token asymmetry, which reads backwards

Confirmed from the Case Tracker's source, and worth stating because the natural reading is the
opposite of the truth.

| Their configured token | What happens |
| --- | --- |
| blank or whitespace | They never poll. No request, no halt, nothing to resume. **Safe.** |
| contains a character the HTTP client will not send (newline, non-ASCII) | Never leaves their process. You see nothing at all, not even a 403. |
| configured, sendable, **wrong** | Reaches the portal, answers 403 `forbidden`, and **halts that office on their side**. A halt never self-clears, so each halted office needs a manual Resume after the token is fixed. |

So **forgetting the token is safe and mistyping it is not.** A wrong token at cutover halts every
office you switched, one manual Resume each.

## Per office, every time

### 1. Supply the office's tenant GUID and token to the Case Tracker

This is permanent, not a one-off. Under the push, a new office taught the Case Tracker its GUID on
first delivery. Under the feed, the GUID is in the URL they must build *before* they can poll, so
there is no first contact that could teach it. Every office you enable needs its GUID supplied out
of band, alongside its token, forever.

Read it from the host database:

```sql
SELECT Id, Name FROM SaasTenants ORDER BY Name;
```

**Case does not matter.** `SaasTenants` renders uppercase and the Case Tracker stores lowercase; a
`uniqueidentifier` is 16 binary bytes and the casing is display convention. `Guid.TryParse` behind
the route binding is case-insensitive, and so are their default collations. What matters is that it
is the same GUID, dashes kept, no braces, no surrounding whitespace.

What actually fails is a segment that is not a GUID at all. That does not match the route, so it
answers a routing **404 carrying none of the feed's error body** -- which is exactly why a wrong
value is hard to diagnose from their side.

### 2. Pre-flight

- [ ] Backups ran last night. `systemctl status hcs-portal-backup.service` reports `Result=success`.
- [ ] Note the office's pending count on the Case Tracker offices screen. It tells you whether the
      switch hands over a handful of rows or several hundred.

### 3. Start the feed

Press **Start feed** for the office on the Case Tracker offices screen.

One write does all of this, so the floor and the switch cannot disagree:

- the floor is set just below the lower of the oldest Pending row and the oldest write still in
  flight, so nothing still owed is stranded
- the push stops for that office

The floor is computed live from the outbox at that moment, not carried from a stored cursor value.
That matters because `rowversion` regenerates under an export/import move, and a carried number
would be meaningless afterwards.

**The office's push switch stays ON.** Reconcile and attendance read it too, so turning it off would
break the inbound half. `FeedActive` is the flag that matters here, not `PushEnabled`.

### 4. Turn on their polling for that office

Order matters. Until Start feed is pressed, the portal answers `feed_not_enabled`, and on their side
that logs at INFO and never halts -- so a consumer polling early is quiet rather than broken, but it
is also invisible. Press Start feed first, then enable their polling.

### 5. Verify

- [ ] Their operator screen shows the office RUNNING with the position advancing.
- [ ] The offices screen shows `FeedActive` true, `LastRequestAt` moving, `LastAdvancedAt` moving.
- [ ] `OutstandingCount` is falling, or is zero and staying there.

`LastRequestAt` moving while `LastAdvancedAt` does not means requests are arriving and the
acknowledged position is frozen. That is the stall condition; see below.

## What watches it for you

`CaseTrackerFeedHealthJob`, cron `*/5 * * * *`:

| Alert | Condition | Surfaces within |
| --- | --- | --- |
| SILENCE | No request from the office for 15 minutes | 20 minutes |
| STALL | A row has waited 30 minutes and the acknowledged position has not moved for as long | 35 minutes |

One email when an incident starts and one when it clears. An office whose push switch is off is
skipped, because the feed refuses it and the quiet that follows is deliberate.

There is also a `cursor_ahead` alert, raised when the consumer presents a cursor beyond anything the
feed has issued, and re-armed by the next good request. That is the case a regenerated `rowversion`
produces, and it is caught in both systems.

These alerts are the only delivery visibility the portal has under the feed. The integration
failures screen lists rows with status `Failed`, and under the feed nothing fails, so that screen
shows nothing about rows sitting unclaimed.

## When an office halts

Their halts do not self-clear: once halted, they discard what comes back on every later tick until a
human acts.

| Cause | Fix |
| --- | --- |
| Wrong feed token (`forbidden`) | Correct the token, then Resume each halted office. |
| Cursor out of range (`cursor_ahead`, `cursor_below_floor`, `cursor_invalid`) | Their **Reset position** action. It clears their cursor so the next request sends none, and the portal then resumes from the position it recorded. Nothing needs to be hand-written. |
| Bad skip (`skip_invalid`) | Reset position. Their reset also clears the pending skip list, so a stale skip cannot be carried forward. |

Sending no cursor is not the same as replaying from the floor, and is safer: a floor replay would
meet any row whose payload shape has changed since the floor and halt on it.

## Rollback: return an office to push

Press **Return to push** for the office.

The drain resumes on its next pass and pushes **every** Pending row, including ones the feed already
delivered. That is accepted: the receiver's upsert absorbs the duplicates.

Turn their polling for that office off as well, or they will poll an office that now answers
`feed_not_enabled`. That is quiet on their side rather than loud, so it will not alert anyone.

## Limits worth knowing before you set anything low

- **The hourly allowance is 240 per office**, four times the one-a-minute poll. It is counted in the
  controller after the token check, so a caller without the token cannot spend an office's budget.
- **It does not share reconcile and attendance's 300/hour.** The feed path is matched before the
  `/api/integration` branch in the limiter, and a request carrying a valid feed token gets no
  middleware limiter at all, so however many offices poll, they cannot consume reconcile's budget.
  A request to the feed WITHOUT a valid token falls into a per-address bucket of 60/hour instead,
  and a rejection there answers 403 `forbidden` with the same envelope as a bad token. So during a
  misconfigured switch-on, several offices polling with a wrong token exhaust that 60/hour quickly
  and the middleware refusal is indistinguishable from the token refusal.
- **It is held in memory, per API instance.** Exact while there is one instance, which is the case
  today. If the API is ever scaled out behind the public host, the effective allowance multiplies by
  the instance count and neither side will notice.
- **Their rate-limit backoff is per process, not per office.** A 429 for one office would pause
  polling for all of them. The portal does not currently return 429, so this is latent rather than
  live.
- **Page size is fixed at 200** and is not a request parameter. Anything sent is ignored.

## After any deploy that touches the proxy

Check the reserved-hostname behaviour by the response **body**, not the status. A missing API route
also answers 404, so the status alone proves nothing:

```bash
curl -sk https://api.<base-domain>/ | grep -q missing_office_label && echo OK
```

## Endpoint reference

```text
GET /api/integration/offices/<office-guid>/feed
X-Feed-Token: <secret>          bare, exact, no scheme prefix, no whitespace

cursor    optional. Omitted OR empty both mean "from your acknowledged position"
skipped   repeated key, one cursor per occurrence (skipped=a&skipped=b)
          NOT comma-joined
```

Page size is fixed at 200 and is not a request parameter. Anything sent is ignored.

Every refusal carries a code in `errors[0]`, and every 409 carries one, so a bare 409 with no
envelope did not come from the feed.

| Status | Codes | Meaning |
| --- | --- | --- |
| 403 | `forbidden`, `allowance_exceeded`, `feed_not_enabled` | Bad token, spent allowance, or an office not on the feed. An unknown office answers the same, deliberately, so a token holder cannot enumerate offices. |
| 409 | `cursor_invalid`, `cursor_below_floor`, `cursor_ahead`, `skip_invalid` | The cursor or skip cannot be accepted. None of the four can be fixed by retrying. |
| 200 | -- | A page, possibly empty. An empty page while a write is in flight is normal. |

Never 401, which would latch the Case Tracker's shared HTTP client. Never 429.

The four cursor and skip refusals were 400 until #1065 and are 409 from it onwards. The Case
Tracker's deployed client acts on the status alone and halts on 409, which is why the change
matters; their newer code branches on `errors[0].code` and is unaffected either way, but it is
committed rather than deployed.
