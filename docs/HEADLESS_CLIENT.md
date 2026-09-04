# Headless client and Core SDK

## Purpose

Maestro's headless client is a real application client without a visual shell. It is
not a simulator and it does not maintain a second implementation of product
behavior. React and the command-line harness call the same framework-neutral
journey controllers, which in turn use the same Gemini transports, managed-backend
routes, state transitions, persistence contracts and media-stream engines.

The harness exists for maintainers, CI and agent-driven release checks. It must be
safe to run repeatedly, deterministic where Maestro controls the outcome, and
explicit about every provider operation that can spend money or destroy data.

## First-release scope

- BYOK and managed access setup.
- Language selection, settings, chat-history loading and persistence.
- Streaming tutor turns and follow-up suggestions.
- Text, image, audio, PDF, SVG, video and Office attachment fixtures, image
  generation, Gemini Live audio notes and Lyria music.
- Synthetic microphone audio, real JPEG video frames, exact-trigger TTS, STT,
  Live conversation and silent-observer input at post-device-capture boundaries.
- A persistent minimum-ten-turn first lesson that requires visible chat/suggestion
  streaming, Search, aftersteps, translations, transcript deltas and audio hashes.
- Stripe test-mode Checkout followed by webhook credit reconciliation.
- Managed account summary and disposable-account deletion.
- AI content reports and managed usage/billing ledger reads.

Stripe Checkout is the only managed-credit purchase implementation. Android uses
the same Core SDK session creation and an external Custom Tab adapter, but the
production flag remains off until the Play Console records eligibility/enrollment
for the applicable external-checkout programme. The harness never fabricates a
purchase token. MCP remains deferred until the JSON-RPC contract is stable; a later
MCP adapter must be a thin mapping over the same commands.

## Architecture invariant

```text
React UI ---------+
                  +--> Core SDK journey controllers --> ports --> real providers
CLI / JSON-RPC ---+                                  \-> shared state + events
```

Code under the Core SDK must not import React, Zustand's React bindings, Capacitor,
DOM globals or a concrete persistence implementation. Platform adapters may use
those dependencies. The following are ports rather than globals:

- credentials and Firebase App Check tokens;
- application settings, conversations and secrets;
- clock, random values and identifiers;
- network fetch;
- microphone PCM and timestamped video frames;
- generated audio and artifact sinks;
- external browser navigation.

The browser shell supplies IndexedDB, secure-storage, microphone/camera,
AudioContext and hosted-page navigation adapters. The headless shell supplies
isolated or named filesystem profiles, WAV/raw-PCM and image/frame fixtures, file
artifact sinks, headless Chromium for Stripe test Checkout, and a normal-system-
browser handoff for Google sign-in. The Android shell supplies a Capacitor Custom
Tab for the same Stripe Checkout URL; it has no independent billing SDK or grant
route.

## Automation contract

The stable machine interface is JSON-RPC 2.0 over stdin/stdout. Standard output is
reserved for protocol messages; diagnostics go to standard error. A human-friendly
CLI is a wrapper over the same dispatcher.

Every command accepts or inherits an operation ID. State-changing commands emit an
ordered event trace containing the operation ID, journey phase and relevant public
metadata. Secrets, bearer tokens, App Check tokens, raw payment details and full
provider payloads must never be included in traces.

Protocol 1.2 also requires every Gemini Live transport to have a reviewed open
reason. Headless Live commands derive `user.headless-live`; callers cannot claim a
browser Whisper or UI trigger, and protocol 1.6 exposes no raw ticket/token method.
See
[`GEMINI_LIVE_OPEN_POLICY.md`](./GEMINI_LIVE_OPEN_POLICY.md).

Protocol 1.3 adds semantic input evidence to `speech.transcribe`,
`live.conversation.turn`, `live.observer.turn` and `journey.firstLesson`. Supplying
`expectedTranscript` makes the command fail unless the provider input transcript
contains at least 80% of the expected words (configurable with
`minTranscriptWordRecall`). Punctuation and common contraction differences are
normalized, so this verifies what Live heard without asserting exact model output.

Protocol 1.4 adds real-time and browser-handoff evidence. `speech.transcribe` and
`live.observer.turn` now start paced capture before the Live transport, retain all
PCM produced while local speech recognition and the provider connection are in
flight, and transfer it through the same lossless handoff primitive used by the
browser. Retained packets are delivered to the provider on absolute PCM deadlines;
`realtimeEvidence.providerInputPacingPassed` fails if capture was real-time but the
post-connect queue was burst-sent. With `pace:true`, the harness also drains a 24
kHz playback clock for the model response before returning.
`realtimeEvidence.passed` is therefore required; receiving a transcript or audio
bytes alone is not a timing pass. See
[`LIVE_OBSERVER_AUDIO_RELIABILITY.md`](./LIVE_OBSERVER_AUDIO_RELIABILITY.md).

Protocol 1.5 makes access parity an executable contract. `system.describe` now
classifies every command as `local-parity`, `provider-parity`, or
`managed-account-only`, lists its allowed access modes, cost class and required
release proof, and reports which commands are available for the active client.
Every provider-parity command is available through both managed credits and BYOK.
The only mode-specific commands are honest Maestro-account boundaries: sign-in,
account/ledger/deletion, Stripe Checkout and hosted Google verification. BYOK
content reports remain supported because the real report route is optional-auth.
A supplied report mode must match the active client. Managed gateway ticket
issuance is internal transport plumbing, not a public headless command whose bearer
secret could leak through JSON-RPC output.

Protocol 1.6 removes raw managed provider-token creation and release from the
public contract. Managed UI and headless Live sessions now receive a one-use,
short-lived Maestro gateway ticket; neither a Gemini credential nor an internal
lease ID reaches the client. The ticket is sent in the first secure WebSocket
frame, never in its URL. The gateway keeps the provider key server-side, observes
input/output and provider usage, commits the first useful output before forwarding
it, and settles only that observed use. Setup-only or no-output sessions release
the complete reservation. A scheduled reconciler handles unused tickets and
abandoned sessions. See the billing and recovery invariants in
[`HEADLESS_COVERAGE.md`](./HEADLESS_COVERAGE.md).

Protocol 1.7 adds persistent connected-turn proof to `speech.synthetic.live`.
Set `connectedTurns` from 1 through 6 to replay the supplied PCM as distinct user
turns over one Live socket. The result contains one entry per turn and proves that
each reply finishes real-time playback after the last model-audio byte. The
staging workflow requires six turns in both managed and BYOK modes, specifically
covering later short-utterance paths that a fresh connection cannot exercise. It
sends a visual frame on every turn and reports the provider's latest snapshot for
each turn plus the correctly summed billable usage for the socket.

`provider-parity` still does not mean the user pays the same currency or provider:
BYOK charges the supplied Google project, while managed converts observed provider
cost to Maestro credits. It does mean a failed/no-output managed session cannot be
silently charged a fixed connection window. Failure evidence checks both current
`liveGateway` rows and historical `liveToken` rows so an old deployment cannot
regress unnoticed.

The paired staging gate can no longer skip BYOK when its key is missing. Both jobs
run the same generated-media upload cases and raw generation routes, retain their
proof artifacts, and a third job compares stable coverage, turn order and tool
uploads. Managed proof additionally reconciles account balance, usage ledger and
charge ledger deltas and requires zero credits reserved before and after the
journey. Direct BYOK cleanup never lists or deletes arbitrary provider files: a
named profile persists only files it created, and delete/clear refuse everything
outside that ownership set.

The UI/BYOK client and managed gateway exact-pin the same `@google/genai` version.
`verify:release-config` rejects a version range or mismatch so independently
installed package trees cannot silently exercise different Live transports.

AI output is asserted by invariant rather than exact wording. Release checks verify
the route and model used, ordered state transitions, message roles, non-empty visible
output, media metadata, persistence, final accounting events and credit-ledger
changes. Provider text is not a golden string.

## Profiles and destructive operations

Each invocation uses an isolated temporary profile unless `--profile <name>` is
provided. Named profiles live outside the repository and are resolved beneath one
dedicated Maestro harness data directory. Profile names are validated and cannot
escape that directory.

Real-provider tests use a dedicated Firebase staging project, Stripe test mode, a
disposable Firebase account and an App Check debug token stored outside source
control. Live Stripe credentials are never valid harness inputs. Account deletion
requires both the exact `DELETE` confirmation and the currently authenticated
disposable account ID.

## Hosted-provider boundary

Google OAuth and Stripe Checkout are provider-owned web applications. Stripe test
Checkout may use headless Chromium. Google rejects automation-controlled login
pages, so its periodic boundary check uses a visible normal browser and a manual
login/MFA handoff. Maestro actions before and after either provider handoff still
run through the Core SDK. CI uses stored test-account credentials and an App Check
debug token for deterministic non-Google journeys.

## Completion criteria

The harness is release-ready only when:

1. each in-scope React action and CLI command reaches the same controller;
2. browser and headless adapter contract tests pass;
3. emulator tests prove persistence and billing invariants;
4. a bounded real Gemini journey returns visible output and final accounting;
5. a Stripe test-card checkout produces exactly one credit grant and the updated
   balance is observed through the normal account-refresh path;
6. destructive tests operate only on a disposable staging identity; and
7. every cost-bearing provider-parity path passes in both access modes for the same
   commit, and managed journey charges reconcile with both ledgers, charge no
   failed Live attempt and leave no reservation; and
8. command, profile, staging and failure-recovery procedures are documented for a
   maintainer who has repository and provider-console access but no project history.

## Current staging inventory

These identifiers are intentionally public configuration. Secret values are never
listed here.

| Resource | Value |
| --- | --- |
| Firebase alias | `staging` |
| Firebase project | `chatwithmaestro-staging` |
| Project number | `631976406346` |
| Region / Firestore location | `europe-west1` |
| Web app | `1:631976406346:web:a27cc0dc279adb1a76e5b3` |
| Android app | `1:631976406346:android:df428406fac6626f76e5b3` |
| Android package | `com.ronitervo.maestrotutor.staging` |
| Backend | `https://europe-west1-chatwithmaestro-staging.cloudfunctions.net/api` |
| Hosted app | `https://chatwithmaestro-staging.web.app` |
| reCAPTCHA Enterprise site key | `6Le5YKMtAAAAABjtrrO6VVV6zbr1LuS1vRbHTcRf` |

The staging project is on the `MaestroPayments` billing account. Firebase
Authentication uses one disposable password account for deterministic CI. The
interactive Google popup remains a separate periodic provider-boundary check.
Email/password credentials are not a product sign-in option and must not be added
to the visual UI.

The registered App Check debug token is named `Maestro Headless CI`. A debug token
bypasses device attestation and is therefore a secret even though the Firebase web
configuration is public. It is valid only for the staging web app.

The staging Android app is reserved for a future `applicationIdSuffix ".staging"`
flavor. The current signed Android application remains
`com.ronitervo.maestrotutor`, uses the production Firebase Android app, and must
never be pointed at staging by swapping `google-services.json` by hand. Staging
enables its external Stripe browser adapter for local/internal testing; production
must keep that build flag false until programme enrollment is independently proven.

## Secret and variable inventory

The same names are used locally and in GitHub Actions so there is only one runbook.

| Name | Kind | Stored in |
| --- | --- | --- |
| `GEMINI_API_KEY` | Functions secret | staging Secret Manager |
| `STRIPE_SECRET` | Functions secret, test mode only | staging Secret Manager |
| `STRIPE_WEBHOOK_SECRET` | Functions secret, test endpoint only | staging Secret Manager |
| `HEADLESS_FIREBASE_EMAIL` | harness secret | staging Secret Manager and GitHub Actions |
| `HEADLESS_FIREBASE_PASSWORD` | harness secret | staging Secret Manager and GitHub Actions |
| `HEADLESS_APPCHECK_DEBUG_TOKEN` | harness secret | staging Secret Manager and GitHub Actions |
| `HEADLESS_GEMINI_API_KEY` | dedicated quota-limited BYOK test secret | GitHub Actions only |
| `MAESTRO_FIREBASE_API_KEY` | public CI variable | GitHub Actions |
| `MAESTRO_FIREBASE_APP_ID` | public CI variable | GitHub Actions |
| `MAESTRO_BACKEND_BASE_URL` | public CI variable | GitHub Actions |
| `MAESTRO_TEST_PACK_ID` | public CI variable (`pack_1000`) | GitHub Actions |
| `MAESTRO_TEST_PACK_CREDITS` | public CI variable (`1000`) | GitHub Actions |

Never put a Stripe key, password, debug token, Firebase ID token, App Check JWT or
webhook signing secret in a profile, command argument, trace, issue, pull request or
committed dotenv file. The direct token environment variables exist only for
short-lived diagnosis; normal runs use renewable credentials.

## Install and run

Use Node 22, matching Functions and CI:

```powershell
npm ci
npm --prefix functions ci
npm run maestro -- system.describe
npm run maestro -- language.list --params '{"targetLanguageCode":"es-ES","limit":25}'
```

The default profile is a fresh temporary directory. Repeated commands that must
share chat state need an explicit name:

```powershell
npm run maestro -- language.select --profile release-smoke --params '{"targetLanguageCode":"es-ES","nativeLanguageCode":"en-US"}'
npm run maestro -- chat.turn --profile release-smoke --params '{"text":"Give me one short greeting.","requireInvariants":true}'
npm run maestro -- chat.attachment.turn --profile release-smoke --params '{"text":"Identify this fixture.","fixture":"pdf","cleanup":true}'
npm run maestro -- media.music.generate --profile release-smoke --params '{"prompt":"An original scale-practice track","durationSeconds":8}'
npm run maestro -- journey.firstLesson --profile release-smoke --params '{"targetLanguageCode":"es-ES","nativeLanguageCode":"en-US"}'
```

For media payloads that may exceed the operating system command-line limit,
write the JSON object to a file and use `--params-file request.json`. It is
mutually exclusive with inline `--params` and works identically in one-shot and
CI invocations. Real-audio checks should convert the source to signed PCM16 mono
at 16 kHz, place its base64 in the params file, and include the words that were
recorded:

```json
{
  "pcmBase64": "<16 kHz PCM16 mono base64>",
  "sampleRate": 16000,
  "pace": true,
  "expectedTranscript": "Hello. How are you doing? I am doing great.",
  "minTranscriptWordRecall": 0.8,
  "runSuggestionAftersteps": false
}
```

Run that params file once with each Live method and add `"includeVisual":true`
for the video variants. A passing result includes `transcriptEvidence.passed`,
non-zero model audio, audio hashes and (for video) a sent-frame count. Live result
messages omit persisted inline WAV/JPEG base64 and instead report character counts
under `omittedInlineData`, keeping JSON-RPC and CI logs bounded.

For `speech.transcribe` and `live.observer.turn`, a paced result must additionally
contain `realtimeEvidence.passed:true`, `timing.uiSpeechHandoff:true`, a non-zero
`timing.connectionHandoffSamples`, input elapsed time close to the source duration,
and model playback elapsed time at least as long as the 24 kHz audio duration. Put
distinctive words at the end of `expectedTranscript`; a prefix-only expectation
cannot detect the original suffix-loss regression.

For the lower-level persistent observer regression, use the same PCM with
`"connectedTurns":6`, `"simulateUiSpeechHandoff":true`,
`"requireRealtimeInputPacing":true`, and `"playModelAudioRealtime":true`.
There must be six completed turn records, and all must report
`playbackCompletedAfterLastByte:true`.

`scripts/create-long-live-fixture.ts` accepts either `--audio <path>` for a real
recording or `--tts-json <path>` for the CI-generated shared fixture. It produces
16 kHz PCM parameters with an expected transcript, a deliberate pause, one visual
frame, and an instruction requiring exactly five English lines followed by five
`[FI]` translations. The staging workflow reuses the same generated bytes for
managed and paid-BYOK observer-camera and conversation checks.

Named profiles resolve below `%LOCALAPPDATA%\MaestroTutor\headless` on Windows and
`~/.maestrotutor/headless` elsewhere, unless `MAESTRO_HEADLESS_HOME` is set. Names
are restricted to letters, numbers, dot, underscore and dash and cannot escape the
harness directory. Profile files contain settings, chat history and public account
summaries, never credentials.

`language.list` returns compact records and is capped at 100 results by default;
filter by target/native code or explicitly raise `limit` (maximum 500). Language
selection never returns the internal system or suggestion prompts. `profile.get`
returns a compact state summary unless `{"includeState":true}` is requested.
Suggestion artifacts likewise return length plus SHA-256 metadata unless
`{"includeArtifactContent":true}` is explicitly requested. These opt-ins keep CI
logs bounded without weakening JSON-RPC verification when full data is needed.

For a local staging run, populate process environment variables from Secret
Manager without printing them. One safe PowerShell pattern is:

```powershell
$env:MAESTRO_BACKEND_BASE_URL = 'https://europe-west1-chatwithmaestro-staging.cloudfunctions.net/api'
$env:MAESTRO_FIREBASE_API_KEY = '<public Firebase web API key>'
$env:MAESTRO_FIREBASE_APP_ID = '1:631976406346:web:a27cc0dc279adb1a76e5b3'
$env:MAESTRO_FIREBASE_EMAIL = gcloud secrets versions access latest --secret=HEADLESS_FIREBASE_EMAIL --project=chatwithmaestro-staging
$env:MAESTRO_FIREBASE_PASSWORD = gcloud secrets versions access latest --secret=HEADLESS_FIREBASE_PASSWORD --project=chatwithmaestro-staging
$env:MAESTRO_APPCHECK_DEBUG_TOKEN = gcloud secrets versions access latest --secret=HEADLESS_APPCHECK_DEBUG_TOKEN --project=chatwithmaestro-staging
npm run maestro -- auth.signIn --profile release-smoke
```

Clear the three secret environment variables when the terminal session ends. An
ignored dotenv file may instead be passed with `--env-file`, but it must have ACLs
appropriate for secrets and must never be committed.

## JSON-RPC 2.0

Start the long-running line-delimited server:

```powershell
npm run maestro:rpc -- --profile release-smoke
```

Each stdin line is one JSON-RPC request. Responses and `maestro.event`
notifications are emitted on stdout; human diagnostics use stderr.

```json
{"jsonrpc":"2.0","id":1,"method":"language.select","params":{"targetLanguageCode":"es-ES","nativeLanguageCode":"en-US"}}
{"jsonrpc":"2.0","id":2,"method":"chat.turn","params":{"text":"Hola","requireInvariants":true}}
{"jsonrpc":"2.0","id":3,"method":"account.summary"}
```

`system.describe` is the protocol discovery command. The first release exposes:

- authentication status/sign-in/sign-out;
- language list/select and chat history/turns;
- streamed follow-up suggestions, artifact/tool aftersteps and image generation;
- managed file upload/status/delete/clear;
- synthetic PCM-to-STT/Live/observer sessions, real JPEG frame injection,
  translation, empty-input re-engagement and exact-trigger TTS;
- the full persistent first-lesson release journey;
- account summary, usage/billing ledgers and guarded deletion;
- checkout creation, return reconciliation and a Stripe test-card journey;
- AI content reports and low-level managed Gemini/Live routes.

AI commands assert shape and state invariants, not provider wording. A tutor turn
must contain visible bilingual pairs and the expected persisted roles. The speech
command accepts base64 PCM16 mono at 16 kHz and sends it through the same capture
router, packetizer, speech gate and managed Live session used after browser device
capture. It does not upload a prerecorded transcript or invoke a mock endpoint.

## Deploy staging

Always spell the project or alias. Omitting it selects production because production
remains the Firebase default.

```powershell
npm run build:staging
firebase deploy --only firestore:rules,firestore:indexes,functions --project staging
firebase deploy --only hosting --project staging
```

The committed `functions/.env.chatwithmaestro-staging` contains only non-secret
staging policy and catalogue values. `.env.staging.example` documents public client
configuration; `.env.staging.local` supplies the actual public Firebase API key for
the local build and is ignored by Git.

Before a first deploy, ensure the three Functions secrets exist. Copying the Gemini
secret between projects must use a pipe so its value never reaches terminal output.
Stripe must use the account's test/sandbox key and the signing secret from the
`Maestro staging headless` test event destination. Never copy either production
Stripe secret to staging.

After deployment, verify:

```powershell
Invoke-RestMethod https://europe-west1-chatwithmaestro-staging.cloudfunctions.net/api/health
npm run maestro -- auth.signIn --profile release-smoke
npm run maestro -- account.summary --profile release-smoke
```

The health endpoint must report Firestore and Gemini ready, App Check required and
Stripe configured. It is not a paid-provider test; a real bounded chat turn is still
required after credits exist.

## First-time staging provider setup

A maintainer with Owner/Editor access to the staging Firebase/GCP project, Stripe
test-mode Developer access, and repository Actions-secret access can rebuild the
environment as follows. Do not reuse production payment credentials.

1. In Firebase, create the web app and reserved staging Android app shown in the
   inventory, initialize Firestore in `europe-west1`, and initialize Authentication.
   Enable Google for the human popup check. Enable email/password only for the one
   disposable CI identity; it is not exposed in Maestro's UI.
2. In reCAPTCHA Enterprise, create a website key restricted to
   `chatwithmaestro-staging.web.app` (and the exact additional staging domains that
   are genuinely used). Register that Enterprise key on the Firebase web App Check
   app and enforce App Check on the deployed API. Create one debug token named
   `Maestro Headless CI`; store it in Secret Manager and GitHub Actions, never in a
   dotenv file that can be committed.
3. In Stripe **test mode**, create a restricted key named
   `Maestro staging Functions` with Customers=Write and Checkout Sessions=Write.
   Create event destination `Maestro staging headless` at the deployed webhook URL,
   subscribed only to `checkout.session.completed` and
   `checkout.session.async_payment_succeeded`.
4. Reveal the destination signing secret with Stripe's explicit reveal control.
   A masked value such as `whsec_····` is a placeholder, not a secret; copying it
   makes every delivery fail signature verification. Store the revealed test secret
   as `STRIPE_WEBHOOK_SECRET` and the restricted test key as `STRIPE_SECRET` in the
   staging project, then redeploy `api` so the new secret versions are bound.
5. Create the disposable Firebase password user with a generated random password.
   Add the email, password and App Check debug token to both staging Secret Manager
   and repository Actions secrets. Add the public backend URL, Firebase web API key
   and app ID as repository Actions variables. Add `MAESTRO_TEST_PACK_ID` and
   `MAESTRO_TEST_PACK_CREDITS` as public repository variables. Dispatch `Headless
   staging journey` and require every Stripe, chat, attachment, image, music and
   synthetic-live step to pass.
6. Deploy Firestore rules/indexes, Functions and hosting with the explicit `staging`
   alias. Verify the health endpoint, then run the bounded real release journey
   below. Provider configuration is not proven by a green build alone.

## Real release journey

Run this in Stripe test mode only. The browser adapter refuses any session whose ID
does not begin with `cs_test_` and refuses any initial host other than
`checkout.stripe.com`. Its card number is fixed in code to Stripe's `4242` test card;
the command does not accept arbitrary card data.

1. Record `account.summary` and `account.ledgers`.
2. Run the real hosted Checkout journey:

   ```powershell
   npm run maestro -- billing.checkout.completeTest --profile release-smoke --params '{"packId":"pack_1000","expectedCredits":1000,"headless":true}'
   ```

3. The command creates a real test Checkout session through Maestro's controller,
   fills the provider-hosted page, waits for the staging redirect, polls the normal
   account refresh route, and requires exactly a 1,000-credit increase and one new
   purchase ledger entry.
4. Run `journey.firstLesson` and require every returned coverage flag. It exercises
   text/Search streaming, all seven attachment classes, STT, conversation and
   observer audio/video, suggestion artifacts/tools, translation, exact-trigger
   TTS, audio capture and re-engagement through the shared UI/Core paths. The full
   proof matrix and BYOK procedure are in `docs/HEADLESS_COVERAGE.md`.

Managed `media.music.generate` deliberately calls the authenticated
`/gemini/generate-music` route. Lyria rejects the short-lived tokens used by
Gemini Live, so the backend owns that provider WebSocket while both visual and
headless clients consume the same Core PCM result. A BYOK browser continues to
connect directly with its user-supplied key.
5. Redeliver the same Checkout event from Stripe. The balance must not change; the
   Checkout-session purchase claim is the idempotency key.
6. Account deletion is last and uses the authenticated token subject, not a caller-
   supplied `actualUserId`:

   ```powershell
   npm run maestro -- account.delete --profile release-smoke --params '{"confirmation":"DELETE","expectedUserId":"<disposable Firebase uid>"}'
   ```

Deletion destroys the disposable Auth user and managed data. Recreate the Firebase
user with a new random password, add new Secret Manager/GitHub secret versions, and
retrieve its fresh UID with `account.summary` immediately before the next destructive
run. Do not hard-code or reuse a prior UID, and never point this command at production.

## Validated staging baseline

On 2026-09-03, the server-observed gateway candidate was deployed to staging and
exercised through the normal managed Core client with the same real 108,203-sample
human recording used by BYOK. Both access modes transcribed all nine expected
words, including the final “I am doing great” clause, while input was streamed at
microphone pace and complete model audio was played at its real 24 kHz pace before
the commands returned. The managed ledger settled 4,510 provider-reported tokens
at 13 credits instead of charging the 87-credit maximum reservation, and finished
with zero reserved credits. A second provider-connected session sent no input and
produced no useful output; it released all 87 reserved credits, created no usage or
charge row, and left available/lifetime balances unchanged. The exact evidence is
recorded in [`LIVE_OBSERVER_AUDIO_RELIABILITY.md`](./LIVE_OBSERVER_AUDIO_RELIABILITY.md).

The final burst-pacing regression used exact-pinned gateway revision
`maestrotutor-live-gateway-00005-t4v` and the same short fixture that had twice
failed when a slow connection drained 127,492 buffered PCM bytes in a burst.
Managed operation `first-lesson-75ff94aa-64a7-4933-a3c7-cd20ae2f5c13`
passed all 18 coverage flags and 14 turns. Its 48 usage rows matched 48 charges
totaling 433 credits / USD 0.413022, with zero reserved before and after. The
gateway and UI share exact-pinned `@google/genai` 1.45.0.

This is local evidence for the current working tree and staging deployment. It is
not a substitute for the required managed/BYOK workflow and production canary on
the final release commit.

The release-only long conversation fixture sets `manualActivityBoundaries: true`
to wrap its finite PCM source in one explicit Live activity. Do not use that option
as a substitute for the automatic-VAD conversation exercised by
`journey.firstLesson`; it exists to make long transport and transcript fidelity
deterministic.

### Historical 2026-09-01/02 baseline

The expanded managed parity gate was also run locally against staging on
2026-09-02. It passed all 16 coverage flags with 14 new user turns after a
controlled Checkout granted exactly 1,000 credits once. The same run exercised all
seven attachment classes, Search, streamed suggestions/artifacts, image,
audio-note, music, STT, Live and observer audio/video, translation, trigger-audio
TTS, hashed audio capture and re-engagement. Six generated provider files were
then deleted with zero failures and the account had zero reserved credits. See
`docs/HEADLESS_COVERAGE.md` for the evidence boundary and the still-required BYOK
workflow proof.

The earlier 2026-09-01 baseline remains useful historical evidence:

The first complete real-provider run established these release facts:

- normal-browser Google sign-in reached the signed-in account UI and signed out;
- two Stripe `cs_test_` Checkout payments each granted exactly 1,000 credits, and a
  redelivery of the same webhook was a successful no-op;
- the webhook accepted the exact Cloud Functions `req.rawBody` bytes; parsed JSON
  is deliberately rejected because re-serialization would invalidate Stripe's
  signature;
- an ordinary tutor turn and a Google-Search turn passed bilingual invariants; the
  search turn reported one provider search query;
- a PNG upload became active, was consumed by a multimodal tutor turn, was deleted,
  and then reported inactive/deleted;
- image generation returned a PNG without placing its base64 payload in CLI output;
- synthetic 16 kHz PCM passed both directly and through the shared speech gate,
  producing input/output transcripts and model-audio chunks; and
- an AI content report was accepted, all Live leases were released, file metadata
  was cleared, and account reconciliation ended with zero reserved credits.

This baseline predates the expanded first-lesson parity gate. It remains historical
evidence only; it does not prove the current candidate. A current release requires
green managed and BYOK first-lesson jobs for the same commit.

Two provider edge cases were fixed during that run. Gemini Developer API
`countTokens` does not accept the generation call's `systemInstruction`, tools or
generation config. The backend counts contents, instruction and deterministic
config separately, then settles from provider usage metadata. Also, a provider
stream that fails after partial/thought chunks now releases its reservation with
`provider-stream-failed`; it never charges the full estimate for an incomplete
answer. If the client disconnects, the server continues consuming the provider
stream so it can settle actual usage instead of creating a disconnect billing
loophole.

Long conversations can contain Gemini File URIs that have since expired or were
deleted. Both React and headless image/chat paths now call the same verified-media
sanitizer and strip inactive history references before generation. The current
message's active attachment still travels through the normal multimodal route.

At the time of this baseline, `gemini-3.7-flash` intermittently returned provider
high-demand errors. The primary reservation was released and the configured
`gemini-3.5-flash-lite` fallback completed. Treat repeated fallback use as provider
health telemetry, not as permission to hide failures or bill failed streams.

## Hosted Google boundary

The visual app's sign-in remains `GoogleAuthProvider` plus Firebase
`signInWithPopup`; the Core SDK begins only after that provider exchange. Google
rejects login pages driven by automation-controlled browsers, so this boundary has
an explicit manual handoff instead of trying to bypass that security control. The
harness opens the staging app in a normal system Chrome or Edge process using the
named test profile. A maintainer clicks the same managed sign-in action, completes
Google login or MFA, and closes that browser window. The harness then opens the
saved profile headlessly and reports success only if the app itself exposes its
normal signed-in state. CI password auth is a renewable automation credential, not
evidence that the Google popup works.

Bootstrap or verify the provider-owned path with a named profile. The normal
browser is visible by default and waits up to four minutes for login/MFA and for
the maintainer to close it. Later runs may use `"headless":true` to inspect an
already prepared profile; headless mode never attempts to submit Google's login
pages.

```powershell
npm run maestro -- auth.google.verifyHosted --profile google-release --params '{"headless":false,"timeoutMs":240000}'
```

The adapter refuses non-staging origins (other than explicit localhost development),
and reports success only after the app itself shows its normal signed-in state.

## Maintenance and recovery

- Rotate a secret by adding a new Secret Manager version and updating the matching
  GitHub Actions secret. Revoke the old App Check debug-token resource after the new
  workflow passes.
- If Auth returns `CONFIGURATION_NOT_FOUND`, initialize Identity Platform for the
  staging project before patching providers. This is a one-time project operation.
- If App Check returns 401/403, verify the debug token belongs to the exact staging
  web app and exchange it for a fresh App Check JWT; do not disable enforcement.
- If Stripe redirects but credits do not arrive, inspect the test destination's
  delivery status, signature response, Checkout `payment_status`, and the
  `checkoutGrants/<cs_test_...>` document before retrying. Never grant credits from
  the redirect.
- If a Checkout event is delivered twice, the second delivery must be a successful
  no-op. A second ledger entry or credit delta is a release blocker.
- If JSON-RPC stdout contains human logs or stack traces, it is a protocol break.
  Provider diagnostics belong on stderr and secrets belong nowhere in either stream.
- If a generated image fails with “referenced Gemini file is not active”, verify the
  shared media sanitizer is still called before both chat and image generation; do
  not keep retrying the same stale URI.
- Stripe's hosted page is locale-sensitive and includes an AI-agent disclosure.
  The adapter fixes locale to `en-US`, selects Card explicitly, checks that
  disclosure and submits the exact final Pay action. Do not weaken the `cs_test_`
  and `checkout.stripe.com` safety guards to make a test pass.
- Functions and CI use Node 22. Firestore Emulator now requires JDK 21 even though
  the Android source compatibility remains Java 17; select JDK 21 for emulator and
  Gradle validation on maintainer machines.
- MCP remains explicitly deferred. Do not add a second business-logic
  implementation while that thin adapter waits.
- Android external Stripe checkout is a release-policy gate, not a code-completion
  guess. Keep `VITE_ANDROID_EXTERNAL_STRIPE_CHECKOUT_ENABLED=false` in production
  until a maintainer records Play programme enrollment and reporting obligations.
  The authoritative checklist is `docs/STRIPE_ONLY_BILLING.md`.
