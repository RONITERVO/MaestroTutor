# Headless parity and first-lesson proof

## What “the same path” means

The headless client is another adapter for Maestro, not a mock app. React and the
CLI share the Core controllers that choose models, serialize history, stream tutor
and suggestion responses, normalize artifacts, dispatch suggestion tools, build
attachment variants, construct Live instructions, packetize PCM and persist the
resulting chat state. Managed mode uses Maestro's authenticated backend routes;
BYOK mode uses the Google Gemini SDK and Files API directly, as the BYOK UI does.

Only device or browser boundaries differ:

- the microphone is replaced after capture with deterministic 16 kHz PCM16 mono;
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
| Speech to text | shared Live STT instruction, PCM router and packetizer | `speech.transcribe`; input transcript deltas and input-audio hash |
| Streamed text response | shared streaming generator and Core delta events | every first-lesson chat turn requires a text delta |
| Google Search response | normal tutor tool configuration | search turn requires provider `searchQueryCount > 0` |
| Suggestion creator stream | `runReplySuggestions` and the same assistant-message context | `suggestions.process`; suggestions plus visible text deltas |
| Artifact afterstep | shared artifact normalization/sanitization and assistant message update | `suggestions.process`; artifact metadata and persisted attachment |
| Image, audio-note, music aftersteps | shared tool normalization and `executeSuggestionToolRequest` | deterministic boundary decision, then real selected executor/provider call |
| Live audio conversation | shared context, Live model compatibility, PCM stream and audio response | `live.conversation.turn`; input/output transcript deltas, audio samples and SHA-256 values |
| Live audio plus visual | same Live path with a real JPEG frame at the video stream boundary | `includeVisual:true`; sent-frame count plus transcript/audio evidence |
| Post-Live aftersteps | same suggestion creator and dispatcher as chat | `runSuggestionAftersteps:true` or `journey.firstLesson` |
| Silent observer audio | shared `SpeechGate`, retained PCM preroll and semantic confirmation | `live.observer.turn`; gate enabled, no video frames, transcript/audio evidence |
| Silent observer plus visual | same observer path plus real JPEG stream injection | `includeVisual:true`; gate and sent-frame evidence |
| Translation | the same `translateText` request with an injected managed/BYOK client | `translation.create`; translated text, optionally attached to suggestions |
| Empty-input re-engagement | the UI-equivalent `"..."` provider prompt without a user bubble | `chat.reengage`; `emptyUserRequest:true`, `userMessagePersisted:false` |
| Trigger-audio TTS | shared exact `Play` trigger instruction and audio-note Live engine | `speech.tts.generate`; trigger packets/samples and captured model samples |
| Audio captured correctly | shared WAV encoding and Live audio capture | non-zero input/model sample counts and 64-character SHA-256 values |
| Coherent first lesson | one persistent language-pair chat and normal aftersteps | `journey.firstLesson`; every boolean in `coverage` must be `true` |
| Managed and BYOK parity | same Core controllers with different transport/file ports | the same first-lesson command runs once per access mode |

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
afterstep boundary so CI deterministically exercises all three real executors.

Suggestion responses use a provider-enforced JSON Schema as well as local semantic
validation. This matters for artifact-bearing replies: HTML, SVG and JavaScript can
contain quoting that a JSON MIME request alone does not reliably escape. A malformed
response is retried and is never accepted as a suggestion or afterstep.

The returned `userTurnCount` counts only user messages created by this invocation,
not messages already in the named profile. A pass requires at least ten new user
messages and every coverage flag to be true.

## Managed provider proof

The managed job in `.github/workflows/headless-staging.yml` signs into the dedicated
staging Firebase account, completes a real Stripe test-card Checkout, runs the full
first lesson, verifies low-level generation routes, reads ledgers, clears generated
files and signs out. It uses a named isolated CI profile and the staging App Check
debug token. Production credentials are never accepted by the Checkout adapter.

Required GitHub Actions configuration:

- secrets: `HEADLESS_FIREBASE_EMAIL`, `HEADLESS_FIREBASE_PASSWORD`,
  `HEADLESS_APPCHECK_DEBUG_TOKEN`;
- variables: `MAESTRO_BACKEND_BASE_URL`, `MAESTRO_FIREBASE_API_KEY`,
  `MAESTRO_FIREBASE_APP_ID`, `MAESTRO_TEST_PACK_ID`,
  `MAESTRO_TEST_PACK_CREDITS`.

### Latest local staging proof (2026-09-02)

The expanded pre-push candidate journey completed with `accessMode: managed`, 14
new persistent user turns, `passed: true`, and all 16 coverage flags true. It
produced real Search evidence, all seven attachment classes, streamed suggestions
after every assistant response, model-selected artifacts, all three deterministic
tool-boundary/provider executions, four Live/observer input and model-audio hashes,
one video frame in each visual mode, translation, 11 exact-trigger TTS packets and
empty-input re-engagement without a persisted user bubble. The controlled Stripe
test Checkout immediately before it granted exactly 1,000 credits with one new
purchase ledger entry. Cleanup deleted six remaining generated provider files with
zero failures and account reconciliation reported zero reserved credits.

This is local staging evidence for the candidate, not a substitute for the workflow
on the pushed commit. BYOK proof remains pending until the repository has the
dedicated `HEADLESS_GEMINI_API_KEY` and the required workflow dispatch is green.

## BYOK provider proof

Create a dedicated, quota-limited Gemini Developer API key for release testing.
Do not reuse a personal production key. In GitHub, open repository **Settings →
Secrets and variables → Actions → New repository secret**, name it
`HEADLESS_GEMINI_API_KEY`, paste the key and save it. The workflow maps it only to
`MAESTRO_GEMINI_API_KEY` inside the BYOK job; the CLI rejects API-key command-line
arguments and never writes the key to a profile or trace.

Dispatch **Headless staging journey**, set **Fail unless the BYOK provider journey
can run** to true, and run the workflow on the candidate branch. The release is not
proved until both `managed-provider-smoke` and `byok-provider-smoke` are green for
the same commit. A skipped BYOK job is not evidence.

For a local run, create an ignored `.env.headless.local` with restrictive file
permissions:

```dotenv
MAESTRO_GEMINI_API_KEY=<dedicated release-test key>
MAESTRO_HEADLESS_ACCESS_MODE=byok
MAESTRO_HEADLESS_HOME=<an isolated local directory>
```

Then run:

```powershell
npm run maestro -- journey.firstLesson --access byok --env-file .env.headless.local --profile byok-release --params '{"targetLanguageCode":"es-ES","nativeLanguageCode":"en-US","paceLiveAudio":true,"timeoutMs":60000,"includeSyntheticToolDecisions":true,"uploadGeneratedMedia":false}'
```

Delete the local dotenv file or revoke the dedicated key after validation if it is
not intended to remain a maintained CI credential.

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
- `ttsTrigger`: the `Play` trigger was not packetized and sent, or no model audio
  was captured.

Always preserve the JSON result and Actions logs as release evidence. They contain
public metadata and hashes, not API keys or raw payment data.
