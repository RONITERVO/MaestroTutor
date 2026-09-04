# Headless parity and first-lesson proof

## What “the same path” means

The headless client is another adapter for Maestro, not a mock app. React and the
CLI share the Core controllers that choose models, serialize history, stream tutor
and suggestion responses, normalize artifacts, dispatch suggestion tools, build
attachment variants, construct Live instructions, packetize PCM and persist the
resulting chat state. Managed mode uses Maestro's authenticated backend routes;
BYOK mode uses the Google Gemini SDK and Files API directly, as the BYOK UI does.

Only device or browser boundaries differ:

- the microphone device is replaced with deterministic 16 kHz PCM16 mono, while
  observer/STT capture begins before Live connects and uses the shared lossless
  capture-handoff contract;
- the camera is replaced with a real, decodable JPEG frame;
- local attachment selection is replaced with deterministic valid files;
- browser video-frame capture is replaced with a deterministic JPEG keyframe,
  while the original real MP4 is still uploaded and sent;
- provider-hosted Google sign-in and Stripe Checkout use their documented browser
  handoffs; and
- a test may inject a suggestion tool decision after the real streamed suggestion
  creator returns. The selected image/audio-note/music executor and provider call
  remain real.

No provider response, transcript, generated media, upload URI, billing result or
persisted state transition is mocked in a provider run.

## Requirement matrix

| User-visible requirement | Shared/UI path | Headless command/evidence |
| --- | --- | --- |
| Text-only chat | `runTutorTextTurn`, strict response parser, history persistence | `chat.turn`; `streaming.visiblyStreamed` |
| Chat with attachments | shared attachment strategy and upload planner, Files API, normal tutor turn | `chat.attachment.turn` with `text`, `image`, `audio`, `pdf`, `svg`, `video`, or `office` |
| Speech to text | shared Live STT instruction, PCM handoff, router and packetizer | `speech.transcribe`; expected-word recall, pre-connect handoff and real-time pacing evidence, input transcript deltas and input-audio hash |
| Streamed text response | shared streaming generator and Core delta events | every first-lesson chat turn requires a text delta |
| Google Search response | normal tutor tool configuration | search turn requires provider `searchQueryCount > 0` |
| Suggestion creator stream | `runReplySuggestions` and the same assistant-message context | `suggestions.process`; suggestions plus visible text deltas |
| Artifact afterstep | shared artifact normalization/sanitization and assistant message update | `suggestions.process`; artifact metadata and persisted attachment |
| Image, audio-note, music aftersteps | shared tool normalization and `executeSuggestionToolRequest` | deterministic boundary decision, then real selected executor/provider call |
| Live audio conversation | shared context, Live model compatibility, PCM stream and audio response | `live.conversation.turn`; expected-word recall, input/output transcript deltas, audio samples and SHA-256 values |
| Live audio plus visual | same Live path with a real JPEG frame at the video stream boundary | `includeVisual:true`; sent-frame count plus transcript/audio evidence |
| Post-Live aftersteps | same suggestion creator and dispatcher as chat | `runSuggestionAftersteps:true` or `journey.firstLesson` |
| Silent observer audio | shared `SpeechGate`, retained raw PCM, lossless pre-connect handoff and semantic confirmation | `live.observer.turn`; expected-word recall (including suffix), real-time input/output playback evidence, gate enabled, no video frames, transcript/audio hashes; `speech.synthetic.live` also requires a short second turn over the same socket |
| Silent observer plus visual | same observer path plus real JPEG stream injection | `includeVisual:true`; gate and sent-frame evidence |
| Translation | the same `translateText` request with an injected managed/BYOK client | `translation.create`; translated text, optionally attached to suggestions |
| Empty-input re-engagement | the UI-equivalent `"..."` provider prompt without a user bubble | `chat.reengage`; no user bubble and at least one visible streamed text delta |
| Trigger-audio TTS | shared exact `Play` trigger instruction and audio-note Live engine | `speech.tts.generate`; trigger packets/samples and captured model samples |
| Audio captured correctly | shared WAV encoding and Live audio capture | non-zero input/model sample counts and 64-character SHA-256 values |
| Coherent first lesson | one persistent language-pair chat and normal aftersteps | `journey.firstLesson`; every boolean in `coverage` must be `true` |
| Managed and BYOK parity | same Core controllers with different transport/file ports | both mandatory jobs run the same upload-enabled first lesson and raw routes; `access-parity` compares their saved semantic proof |
| Cost ownership | BYOK bills the supplied Google project; managed reserves/settles Maestro credits | managed balance, usage and charge ledgers reconcile exactly with zero stranded reservations; BYOK proof is labelled as API-key-owner billing |

## Attachment variants

`attachmentStrategy` and `attachmentUploadPlans` are shared by the UI and CLI.
The provider receives these ordered variants:

| Selected file | Uploaded variants |
| --- | --- |
| Text, raster image, audio, PDF | original file |
| SVG | UTF-8 source text, then rasterized JPEG |
| Video | JPEG keyframe, then original video |
| Microsoft/OpenDocument office file | locally extracted UTF-8 text |

The headless fixtures are structurally valid: WAV has a RIFF header, PDF has a
cross-reference table, DOCX is an OpenXML ZIP, SVG is parseable XML, MP4 is a real
packaged animation, and images are decoded and encoded by Sharp. Uploads and file
activation checks always go to the real managed or BYOK Files API.

## First-lesson sequence

`journey.firstLesson` deliberately exceeds the minimum ten user turns so it does
not hide a requested mode merely to hit an exact count. It uses one persistent
language-pair history and performs:

1. a short text-only greeting;
2. a current fact that must use Google Search;
3. seven attachment turns: text, image, audio, PDF, SVG, video and Office;
4. STT followed by a normal chat turn containing the returned transcript;
5. Live conversation with audio;
6. Live conversation with audio and a real JPEG visual;
7. silent observer with audio;
8. silent observer with audio and a real JPEG visual;
9. translation, exact-trigger TTS, and empty-input re-engagement.

The real suggestion creator runs after every assistant response, including Live and
observer responses. Three turns inject image, audio-note and music decisions at the
afterstep boundary so CI deterministically exercises all three real executors. The
release workflow uploads every generated media type in both access modes, asserts
those uploads, then clears them. A caller may explicitly disable uploads for a
diagnostic run, but that output is not accepted as paired release proof.

Suggestion responses use a provider-enforced JSON Schema as well as local semantic
validation. This matters for artifact-bearing replies: HTML, SVG and JavaScript can
contain quoting that a JSON MIME request alone does not reliably escape. A malformed
response is retried and is never accepted as a suggestion or afterstep.

The returned `userTurnCount` counts only user messages created by this invocation,
not messages already in the named profile. A pass requires at least ten new user
messages and every coverage flag to be true. Every synthetic attachment turn must
also report confirmed provider deletion with zero cleanup failures; a best-effort
cleanup attempt alone is not release evidence.

The bundled first-lesson audio repeats the known word `Play` and now asserts that
Live actually transcribed that word. The local detector waits for a 1.2-second
intact audio window, not 1.2 seconds of VAD-selected speech.
A custom `pcmBase64` is accepted only with `expectedTranscript`; this prevents a
green response/audio check from hiding clipped or misunderstood user speech.
Paced observer/STT checks additionally fail unless the input duration tracks wall
time, PCM crossed the pre-connect handoff, and model audio completed a real-time
24 kHz playback drain.

The scheduled staging workflow additionally sends the short bundled `Play` clip
six times over one still-connected socket in managed and BYOK modes, with one
low-resolution camera frame on every turn. It rejects the run unless every input
transcript and response is non-empty, all six manual audio boundaries carry
samples, and playback for every turn completes after its final network audio byte.
It also runs one shared long English fixture through observer-camera and
conversation mode in both access modes. That fixture includes a 400 ms hesitation,
requires the complete question transcript, and forces five English/Finnish response
pairs so truncated input, output, playback, or translation cannot pass unnoticed.
The finite long conversation fixture disables provider VAD and sends one explicit
activity boundary around the whole recording, making this transport proof
independent of provider endpoint timing. The ordinary short conversation in
`journey.firstLesson` remains the automatic-VAD gate for the product chat path.

## Access-mode policy and safe cleanup

`system.describe` is the authoritative, machine-readable policy for every public
method. `provider-parity` means both modes must expose the command and provide the
declared paired release proof. `managed-account-only` is reserved for operations
that truly have no BYOK equivalent: Maestro authentication/account state and
Stripe credit purchase. One-use managed gateway ticket issuance remains private
inside the Core transport so JSON-RPC never writes its bearer secret. Tests fail
when a new method is unclassified or a cost-bearing parity method lacks paired
proof.

Managed Files endpoints are already scoped to the authenticated Maestro user. The
direct Files API is key-wide, so a raw list-and-delete implementation could remove
files created by another app sharing that Google project. Named BYOK profiles now
persist the provider names returned by their own successful uploads. `files.delete`
and `files.clear` operate only on that set; if ownership persistence fails after an
upload, the new remote file is immediately rolled back.

## Managed provider proof

The managed job in `.github/workflows/headless-staging.yml` signs into the dedicated
staging Firebase account, completes a real Stripe test-card Checkout, runs the full
first lesson, verifies low-level generation routes, reads ledgers, clears generated
files and signs out. It uses a named isolated CI profile and the staging App Check
debug token. The journey refuses to start with an existing credit reservation and
fails unless its final account spend equals both its new usage entries and charge
entries with zero credits still reserved. Production credentials are never accepted
by the Checkout adapter.

Required GitHub Actions configuration:

- secrets: `HEADLESS_FIREBASE_EMAIL`, `HEADLESS_FIREBASE_PASSWORD`,
  `HEADLESS_APPCHECK_DEBUG_TOKEN`;
- variables: `MAESTRO_BACKEND_BASE_URL`, `MAESTRO_FIREBASE_API_KEY`,
  `MAESTRO_FIREBASE_APP_ID`, `MAESTRO_TEST_PACK_ID`,
  `MAESTRO_TEST_PACK_CREDITS`.

### Latest pushed staging proof (2026-09-02)

The required [Headless staging journey](https://github.com/RONITERVO/MaestroTutor/actions/runs/33567265510)
completed on commit `ff6c07cc9e55d64ec69be38afe56db044ed90ebf` with both
managed and required BYOK jobs green. The expanded managed journey reported 14
new persistent user turns, `passed: true`, and all 16 coverage flags true. It
produced real Search evidence, all seven attachment classes, streamed suggestions
after every assistant response, model-selected artifacts, all three deterministic
tool-boundary/provider executions, four Live/observer input and model-audio hashes,
one video frame in each visual mode, translation, 11 exact-trigger TTS packets and
empty-input re-engagement without a persisted user bubble. The controlled Stripe
test Checkout immediately before it granted exactly 1,000 credits with one new
purchase ledger entry. Cleanup deleted six remaining generated provider files with
zero failures and account reconciliation reported zero reserved credits. This is
historical evidence for that exact SHA; every later release candidate still needs
its own required dispatch.

### Current branch gateway proof (2026-09-03)

The local BYOK first-lesson journey completed 14 new user turns with all 18 current
coverage flags true, including generated-media uploads and API-key-owner cost
attribution. An earlier paired managed diagnostic completed chat, Search, every
attachment and all generated-media cases, but two final Live attempts reached
setup without useful output. The legacy token-at-mint transport charged 87 credits
for each failure. That 174-credit finding was the release blocker that drove the
gateway architecture; failure evidence still recognizes those historical
`liveToken` rows.

The replacement gateway was then deployed to staging and tested with the identical
real 108,203-sample human recording through the normal UI/Core Live client. Managed
and BYOK both preserved all nine expected words at real input pace and waited for
actual output playback. The managed run billed 13 credits from 4,510
provider-reported tokens, released the unused portion of its 87-credit reservation,
and left zero credits reserved. A provider-connected session with no input/useful
output then billed zero, created no usage/charge row, returned the full reservation
and left account spend unchanged. An abandoned issued ticket was also left unused;
the deployed one-minute reconciler returned its 87-credit reservation without any
client cooperation or spend delta.

The next paired run exposed a separate delivery invariant: two managed STT attempts
preserved and delivered all 127,492 input bytes but a slow connection caused the
retained prefix to reach the provider as a burst, producing no output. Both attempts
were released with zero charge. Client packetization and the gateway now
independently pace queued PCM to absolute sample deadlines. The exact short fixture
then passed every managed Live surface on gateway revision
`maestrotutor-live-gateway-00005-t4v`. Final managed operation
`first-lesson-75ff94aa-64a7-4933-a3c7-cd20ae2f5c13` completed 14 turns and all 18
coverage flags; 48 usage rows matched 48 charge rows totaling 433 credits / USD
0.413022, with zero credits reserved before and after. UI/BYOK and gateway also
exact-pin `@google/genai` 1.45.0, and release configuration rejects future drift.

These working-tree staging proofs establish the transport and money-safety
boundary. A merge/release candidate still requires the checked-in workflow's fresh
managed and BYOK jobs plus their cross-job comparison on that commit.

## BYOK provider proof

Create a dedicated, quota-limited Gemini Developer API key for release testing.
Do not reuse a personal production key. In GitHub, open repository **Settings →
Secrets and variables → Actions → New repository secret**, name it
`HEADLESS_GEMINI_API_KEY`, paste the key and save it. The workflow maps it only to
`MAESTRO_GEMINI_API_KEY` inside the BYOK job; the CLI rejects API-key command-line
arguments and never writes the key to a profile or trace.

Dispatch **Headless staging journey** on the candidate branch. The BYOK credential
is mandatory: absence fails the job instead of turning it into a green skip. The
release is not proved until `managed-provider-smoke`, `byok-provider-smoke`, and the
cross-job `access-parity` comparison are green for the same commit.

For a local run, create an ignored `.env.headless.local` with restrictive file
permissions:

```dotenv
MAESTRO_GEMINI_API_KEY=<dedicated release-test key>
MAESTRO_HEADLESS_ACCESS_MODE=byok
MAESTRO_HEADLESS_HOME=<an isolated local directory>
```

Then run:

```powershell
npm run maestro -- journey.firstLesson --access byok --env-file .env.headless.local --profile byok-release --params '{"targetLanguageCode":"es-ES","nativeLanguageCode":"en-US","paceLiveAudio":true,"timeoutMs":60000,"includeSyntheticToolDecisions":true,"uploadGeneratedMedia":true}'
npm run maestro -- files.clear --access byok --env-file .env.headless.local --profile byok-release
```

Delete the local dotenv file or revoke the dedicated key after validation if it is
not intended to remain a maintained CI credential.

## Live provider compatibility canary

Gemini Live is still a Preview surface. Managed production traffic no longer
depends on client-side ephemeral-token behavior: the Cloud Run gateway connects
with its server-held API key on `v1beta`. The old token/version matrix remains a
diagnostic for understanding provider regressions, not a shippable transport:

- [Gemini ephemeral-token guide](https://ai.google.dev/gemini-api/docs/live-api/ephemeral-tokens)
- [JavaScript SDK Live implementation](https://github.com/googleapis/js-genai/blob/main/src/live.ts)
- [open SDK connection-hang report](https://github.com/googleapis/js-genai/issues/1257)
- [open constrained-token field-mask report](https://github.com/googleapis/js-genai/issues/1492)

`scripts/probe-gemini-ephemeral-live.ts` exists to test that historical boundary against the
real provider. It sends the bundled speech at microphone pace, reproduces the UI
capture-to-Live handoff, requires the expected final words, receives model audio,
and waits for the complete 24 kHz playback duration. It can compare `api-key` and
`ephemeral` authentication, API versions, models, token lifetimes, connection
windows and constraint shapes without spending a Maestro user's credits. Production
code cannot select its `legacy-ephemeral` escape hatch.

The 2026-09-03 matrix found provider combinations that could mint a token and
open a WebSocket yet deliver no usable server response, including constrained
tokens on the current SDK and a shortened 30-second connection window. Removing
`liveConnectConstraints` made one probe pass, but would let a modified client
change the model/config after Maestro approved and priced the lease. The managed
gateway instead reads the allowlisted model/config from the consumed Firestore
ticket and ignores client attempts to choose provider credentials or configuration.
A release remains blocked until the gateway safety tests, deployed no-output canary
and both full access-mode journeys pass; unit tests alone cannot prove a Preview
WebSocket contract.

Do not retry a paid managed Live failure blindly. `journey.firstLesson` snapshots
the account and both ledgers before it starts, waits until reservations settle,
and emits `firstLesson.failureReconciled` if its bounded Live retry still fails.
The evidence correlates failed Live operation IDs with paid `liveGateway` and
historical `liveToken` ledger rows; any failed-attempt charge makes `passed:false` even if totals reconcile and
no reservation is stranded. Preserve that event with the failure. A release
candidate is blocked unless the managed and BYOK full journeys both pass from the
same commit, with matching ordered semantic evidence, zero failed-attempt charges
and zero managed credits reserved afterward.

### Managed Live billing fairness boundary

Managed Live now reserves a conservative 120-second, six-turn maximum but does not charge at
ticket mint. A one-use bearer ticket authenticates the client to Maestro's Cloud
Run WebSocket gateway. Its hash and sanitized Live setup config exist only while
the ticket is usable; consumption or expiry scrubs both, and the durable session
stores no config. The gateway loads the allowlisted model/config in memory, holds
the Gemini credential, and records
monotonic input/output/usage checkpoints. Provider usage is retained as one latest
snapshot per completed turn and then summed, because retained Live context is billed
again on later turns; treating the final snapshot as the whole socket would
undercharge multi-turn sessions. The first useful provider output is
durably checkpointed before it is forwarded, preventing a modified client from
receiving an answer and then claiming nothing arrived.

Finalization is transactional and idempotent across client close, provider close,
errors and deadline races. Useful output settles provider `usageMetadata` when
available, with a conservative observed-audio fallback. Setup/input without useful
output releases the whole reservation. Issued tickets and live sessions have
deadlines plus a scheduled recovery pass, and account deletion prevents late
settlement from recreating or charging a deleted account. The usage/charge/release
metadata includes ticket, lease and Live request identity, pricing version,
observed byte counts, usage source and finalization reason.

The invariants are enforced at four layers:

- shared metering/state-machine unit tests cover monotonic observations and race
  ordering, including server delivery of a buffered burst at 100 ms PCM cadence;
- the shared UI/headless packetizer tests prove burst replay cadence, normal
  microphone timing, and provider-send timing evidence;
- Firestore Emulator tests cover one-use concurrency, no-output release,
  provider-usage settlement, duplicate finalization, expiry recovery and deletion;
- every managed staging workflow opens a real provider-connected no-output session
  and requires unchanged spend, no usage/charge row, one auditable release and zero
  reserved credits; and
- the paired real-time observer/full-journey jobs exercise the same Core client used
  by the visual UI and compare managed against BYOK evidence.

The gateway enforces the same boundary used by its temporary hold: at most 120
seconds, six turns, one camera frame per second, low media resolution, and bounded
configuration size. At the current 1,000 credits/USD conversion the worst-case hold
is 455 credits. This is not a fixed charge: successful sessions settle from summed
provider turn usage and return the remainder; no-output sessions return the whole
hold. The staging proof additionally requires one correlated usage row, one matching
charge row, zero shortfall, zero stranded reservation, and exact agreement between
provider metadata, the checked-in pricing table, both ledgers, and the account
balance delta.

Cloud Run is the deployment surface because it supports WebSockets, bounded
request timeouts and high connection concurrency:

- [Gemini Live billing is token-based](https://ai.google.dev/gemini-api/docs/live-api/best-practices#pricing-billing)
- [Gemini Live session and connection limits](https://ai.google.dev/gemini-api/docs/live-api/session-management)
- [Gemini failed-request billing](https://ai.google.dev/gemini-api/docs/billing)
- [Cloud Run WebSocket guidance](https://cloud.google.com/run/docs/triggering/websockets)

Production remains blocked until the gateway image, Functions issuer/reconciler and
indexes are deployed together and the production no-output plus real-answer
canaries pass on the release commit. Never restore direct client tokens as a
fallback: fail closed if the gateway is unavailable.

## Reading failures

- `chatStreaming` or `suggestionAftersteps`: the provider completed, but the
  application did not observe a visible text delta. This is a streaming-path
  regression, not a wording mismatch.
- `googleSearch`: the provider returned no Search query. Retry once for provider
  nondeterminism; repeated failure is a release blocker.
- `liveAudio`, `liveVisual`, `observerAudio`, or `observerVisual`: inspect input and
  output transcript delta counts, model-audio sample count, gate state and frame
  count in the result/event trace.
- `audioCapture`: a WAV/cache path lost either input or model audio, or its digest.
- `attachments`: inspect `uploadedVariants`; the IDs, sources, targets and order
  must match the table above.
- `tools`: the deterministic afterstep decisions did not reach all three real
  executors. Never replace this with mocked output.
- `toolUploads`: at least one generated image, audio note or music result did not
  traverse the selected mode's real Files transport.
- `costAccounting`: a managed balance/ledger delta disagreed or a reservation was
  left behind; stop rather than retrying purchases or provider calls blindly.
- `ttsTrigger`: the `Play` trigger was not packetized and sent, or no model audio
  was captured.

Always preserve the JSON result and Actions logs as release evidence. They contain
public metadata and hashes, not API keys or raw payment data.
