# Gemini Live open policy

## The invariant

Maestro must never create a Gemini Live transport without a concrete, reviewed
and loggable reason. Quiet rooms, page focus, observer startup, retries and
component mounting are not reasons.

Every `live.connect` request crosses the Core SDK boundary with a
`liveOpenReason`. The client validates the reason even in BYOK mode. Managed
access validates it again in Cloud Functions before it reserves credits, creates
a lease or asks Gemini for an ephemeral token. The reason is client policy and is
removed before the request reaches Google's SDK.

The only allowed triggers are defined in `shared/liveOpenReason.ts`:

| Trigger | What authorizes it | Current caller |
| --- | --- | --- |
| `whisper.observer` | Local VAD followed by a non-empty local Whisper transcript | passive observer |
| `whisper.stt` | Local VAD followed by a non-empty local Whisper transcript | speech-to-text |
| `user.camera-live` | User presses the camera Live control | visual Live conversation |
| `user.headless-live` | Explicit JSON-RPC/CLI Live command | synthetic/headless Live journey and raw token command |
| `tool.audio-note` | Chat afterstep/tool selects audio-note generation | audio-note generator |
| `voice.tts-click` | User explicitly requests playback | clicked/read-aloud TTS and exact headless TTS |
| `voice.tts-auto-message` | A newly arrived assistant message is configured to auto-play | automatic response TTS |

Do not reuse a nearby trigger for a new interaction. If a new product action
does not exactly fit this table, it is not authorized until this document and the
shared allowlist are deliberately reviewed together.

## Observer and STT flow

The passive paths do not open Gemini while they wait:

```text
local microphone
  -> VAD sees sustained speech-like energy
  -> local Whisper checks the bounded PCM window
  -> transcript is shown as a local pending-user preview
  -> reviewed live-open reason is created
  -> BYOK validation or managed token/credit reservation
  -> Gemini Live transport opens
  -> retained pre-roll is replayed, then current PCM continues
```

If Whisper cannot initialize or transcribe before the transport exists, the
attempt fails closed and no paid Live transport opens. Once an authorized
transport is already active, the existing bounded energy fallback may preserve a
later turn if local inference becomes unavailable; that fallback cannot create a
new transport or lease.

The local monitor transfers its microphone stream to the Live hook after a
successful trigger. This avoids a second permission prompt and preserves the
utterance that authorized the connection. Stopping, foreground work or another
session aborts the monitor and stops its tracks.

## Visible activity

Activity tokens make the delay observable instead of appearing frozen:

- `vad:listen` — locally armed and waiting for speech;
- `whisper:loading` — loading the local model;
- `whisper:checking` — checking buffered speech;
- `whisper:triggered` — words were confirmed;
- `live:observer-connecting` / `live:connecting` — creating the authorized transport;
- `live:observer-session` / `live:session` — transport active.

The status flag renders these phases. The silent-observer blocker intentionally
ignores the observer's own VAD, Whisper and observer-Live tokens, while foreground
STT, TTS, generation, user Live and UI activity still stop or suspend it.

## Managed backend audit data

For managed access, the lease document and reservation/settlement metadata store:

- `liveOpenTrigger`;
- `liveOpenOrigin` (`whisper`, `user`, `tool` or `voice`);
- `liveOpenRequestId`;
- `liveOpenRequestedAt`.

`requestId` correlates one open attempt and is not a user id or secret.
`requestedAt` is normalized to an ISO timestamp. Missing, malformed or unknown
reasons return HTTP 400 before any billable work.

BYOK sessions do not have Maestro's managed billing ledger, but they go through
the same Core SDK reason validation. The client-only fields are stripped before
calling Google so provider request compatibility stays unchanged.

## Rollout compatibility

The managed `/gemini/live-token` request contract is intentionally fail closed.
Clients built before this policy do not send a reason and receive HTTP 400 after
the strict backend is deployed. Do not add an `unknown`, inferred or legacy
reason to keep an old client connecting: the backend cannot truthfully recover
which user/product event happened.

For a release with installed native clients, ship the reason-capable client and
coordinate the Functions cutover according to the supported-version policy. If
old native versions must remain usable, keep the old backend revision serving
them at a versioned endpoint until their support window ends; do not weaken the
new endpoint. Web clients should deploy in the same release operation as the
backend so they adopt the new contract immediately.

## Headless behavior

Synthetic PCM injected by JSON-RPC is an explicit automation action, not proof
that browser Whisper heard a person. It is therefore audited as
`user.headless-live`, including observer-shaped test journeys. This keeps tests
honest and prevents synthetic commands from impersonating a real local Whisper
decision.

`live.token.create` also derives `user.headless-live` internally. The JSON-RPC
caller cannot supply an arbitrary trigger string. Protocol 1.2 documents this
durable behavior.

## Adding or changing a Live entry point

1. Prove which real event authorizes spending. Component mount, focus and retry
   timers are never sufficient.
2. Reuse an existing trigger only if its table description is exact. Otherwise
   add one constant to `shared/liveOpenReason.ts` and update this table.
3. Pass the typed trigger at the product boundary. Create the full reason at the
   last moment before `live.connect`, not when the screen first renders.
4. For a managed route, keep server validation before lease/credit/token work and
   record the standard audit metadata.
5. Add contract tests for allowlisting, provider-field stripping and the caller's
   state transition. For Whisper-gated paths, prove silence and failed inference
   never reach `live.connect`.
6. Run the checks below and inspect all production connects:

```powershell
rg -n "\.live\.connect\(" src --glob "!*.test.*"
npm test
npm run lint
npm run build
Push-Location functions
npm test
Pop-Location
```

Any raw Google Live client introduced outside `src/core-sdk/managedGeminiClient.ts`
or the gated browser access adapter is a policy bypass and should block release.

## Failure diagnosis

- `LIVE_OPEN_REASON_REQUIRED` in the client means a caller omitted or malformed
  its reason. Fix the caller; do not weaken validation.
- HTTP 400 with “auditable Gemini Live open reason” means the backend rejected an
  old or unreviewed client payload before billing.
- Stuck in `armed` means local microphone/VAD/Whisper has not confirmed words.
  Check permission, worker/model loading and the activity flag; do not force a
  connection as a recovery path.
- Stuck in `connecting` means authorization occurred and transport setup began.
  Inspect the managed lease/token response or BYOK provider error using normal
  redacted traffic logs.
