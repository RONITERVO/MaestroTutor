# Architecture ownership and safe change guide

The September 2026 refactor separates model behavior from application wiring.
The initial audit used prompt-contract baseline `a3a9187`; PR #246 established
Core/browser boundaries. Completion work starts from merged `29e2516`.
See [the completion record](./REFACTOR_COMPLETION.md) for baseline tests, commits
and release evidence. This is an ownership map, not a claim that every function
or physical device is independently tested.

## Where a change belongs

| Responsibility | Owner | Boundary |
| --- | --- | --- |
| Authored model instructions, schemas, context markers | `shared/prompts/` | Pure catalogue. See [prompt contracts](./PROMPT_CONTRACTS.md); additions belong here. |
| Provider request composition, retries, streaming and decoding | `src/core-sdk/gemini/` | Explicit client or lazy resolver; no browser credential selection. |
| Shared text, suggestion and image journeys | `src/core-sdk/chat/` | Explicit inputs, runtime ports, events and results; no React/store access. |
| Suggestion artifact/tool decisions | `src/core-sdk/chat/suggestionAfterstepPlan.ts` | Explicit browser-chat, browser-live and headless policies preserve raw-context and stale-field differences. |
| Browser Gemini access and usage display | `src/api/gemini/` | `client.ts` selects BYOK/managed access; `journeys.ts` supplies Core clients and usage sinks. |
| Active model values/validation | `src/core-sdk/modelRegistry.ts` | One process-local registry with established defaults/overrides. |
| Browser model cache, URL migration and remote refresh | `src/core/config/models.ts` | Browser persistence facade over the registry. |
| Timing reports and bounded events | `src/core-sdk/turnTiming.ts` | Recorder factory with optional storage port; no import-time browser effects. |
| Timing persistence and lifecycle flushing | `src/platform/browser/turnTiming.ts` | Browser singleton retains existing key, schema and lifecycle. |
| SVG repair and browser artifact/context composition | `src/platform/browser/sanitizeSvgAnimationStructure.ts`, `assistantArtifacts.ts` | Explicit sanitizer reaches artifact parsing and history; headless preserves raw SVG. |
| Chat suggestion reuse, generation, translation and persistence order | `src/features/chat/coordinators/suggestions.ts`, `suggestionTranslation.ts` | Typed state/persistence/tool ports; hook binds browser state and actions. |
| Chat send transaction and request preparation | `src/features/chat/coordinators/send.ts`, `sendRequest.ts` | Busy/error handling, activity/STT handoff and ordered preparation. |
| User capture, uploads, response projection and generated media | Other `src/features/chat/coordinators/` owners | `userMessage`, `attachmentUploads`, `textResponse`, `mediaPersistence`, `generatedImages` and `assistantTools` have separate responsibilities. |
| Live session lifetime | `src/features/speech/live/controller.ts` | One controller per hook instance; explicit ports and existing Core speech primitives. |
| Live capture, callbacks, transcripts, audio and cleanup | `src/features/speech/live/` | Separate owners preserve session/turn fences, async cancellation and cleanup order; `state.ts` owns session cells. |
| Live browser/native capabilities | `src/features/speech/live/browserRuntime.ts`, `browserVideo.ts` | Device/provider constructors, fresh camera consent and DOM encoding. Controllers cannot import these adapters. |
| UI, device controls and feature APIs | `src/features/` | Public feature indexes; hooks connect state/hardware to coordinators. |
| App speech and idle handoffs | `src/app/coordinators/` | STT destination, microphone/translation actions, reengagement and language reset order; fresh state/actions enter through ports. |
| App composition and lifecycle adapters | `src/app/App.tsx`, `speechRoutingState.ts`, `hooks/` | UI wiring, store projection and React lifetime. Existing idle scheduling remains in `useIdleReengagement`. |
| Filesystem profiles, fixtures, CLI/RPC and artifacts | `src/headless/` | Separate shell calling Core with explicit clients/adapters. |
| Prices, usage calculations and Live protocols | `shared/pricing/`, `shared/billing/`, `shared/live*` | Pure contracts/math; browser estimates cannot authorize charges. |
| Managed reservations and canonical storage | `functions/src/managedBilling.ts`, `managedData.ts` | Backend authority, transactions, idempotency, limits and Firestore paths. |
| Managed generation, files, cleanup, leases, tokens and music | `functions/src/managedGemini/` | Separate owners behind the unchanged `functions/src/gemini.ts` route facade. |
| Managed Live transport and accounting | `live-gateway/src/` | Connection protocol and server lifetime using shared billing/control contracts. |

## Guarded runtime boundaries

`npm run verify:core-boundaries` follows runtime imports and re-exports through
Core, chat coordinators, Live controllers and App coordinators. It covers literal
dynamic imports, `@/` aliases and baseUrl imports; computed loading is rejected.
It rejects browser globals, browser packages, React, the store and implicit
adapter access. Erased type-only imports are allowed. Core may reach pure
configuration/types/shared helpers. Folder names alone do not prove purity.

The two Live browser adapters are excluded as graph roots, not as dependencies:
importing either from a controller is still checked. App's store projection
likewise cannot be imported back into a coordinator. Diagnostics are ports
because a logger can transitively load the store or a native plugin. The guard
runs inside `npm test`; fixtures deliberately introduce direct/transitive
violations. It is not a semantic proof of arbitrary third-party code or computed
global access. Review new runtime dependencies.

## Contracts that preserve behavior

- Existing provider-request, prompt and artifact snapshots remain unchanged.
  Role order, history, models, settings, retries and authored instructions are
  deliberate product behavior.
- Actual chat-hook tests freeze cache reuse, streaming projection, request
  preparation, uploads/persistence, generated tools, errors, STT handoff and
  translation deduplication. Headless entry-point tests freeze its distinct
  afterstep persistence and raw-context policy.
- Actual Live-hook tests freeze connection config, pending connect/capture
  cancellation, stale callbacks, decode cancellation, transcript quiet windows,
  playback drain, confirmed capture transfer and camera consent. Existing Core
  speech primitives run with fake hardware/provider endpoints.
- Actual App tests freeze speech destinations, busy arbitration, visual/text
  reengagement, restart timing, history-load barriers and feature stop/start
  ordering, using the real store and idle hook.
- Functions emulator tests invoke the public facade with real transactions and
  billing, replacing only provider transport. They cover quota races, deletion
  fences, failed uploads/settlement, repeated cleanup, retry backoff, model
  inputs, disconnected stream settlement and Live/music lease rollback.
- Gateway tests retain server-observed usage and ticket/session invariants.
  Paid staging and production canaries test the deployed provider boundary.

Offline tests freeze Maestro's inputs and deterministic behavior. They cannot
promise deterministic model answers, physical microphone/playback behavior or
Play-installed attestation. Those claims require separate release evidence.

## How to make a future change

1. Pick the owner above. Characterize the actual public entry point before a
   structural change: inputs, effects, event order and failure behavior.
   Substitute external ports rather than the logic being moved. Keep baseline
   tests in a separate commit when practical.
2. Browser callers use API facades; Core callers pass `{ aiClient }` or
   `{ resolveAiClient }`. Add a narrow port for a platform capability, not a
   store import or global registry. React refs and getter-backed store cells
   satisfy the existing mutable-value ports.
3. Keep prompt/request snapshots unchanged for a refactor. Intentional behavior
   changes need a reviewed before/after contract and user-impact explanation;
   do not regenerate snapshots merely to get a pass.
4. Run `npm run test:prompts`, `npm run verify:core-boundaries`, `npm test`,
   `npm run lint` and `npm run build`. Managed service work also requires
   `npm --prefix functions test` and
   `npm --prefix functions run test:emulator`; shared billing/Live work needs
   gateway tests/build. CI enforces the release gate on Node 22/JDK 21.
5. Follow [production operations](./PRODUCTION_OPERATIONS.md) and the
   [release checklist](./RELEASE_CHECKLIST.md). Record candidate hashes, both
   managed/BYOK coverage, deployment revisions and artifact identity. Never
   weaken billing or App Check guards to make a canary pass.

## Preserved compatibility details

Browser client resolution remains lazy and can run again on later retries;
explicit clients take precedence. Image usage is recorded before result
extraction, including empty-image responses. Headless has no browser cost-display
persistence. The model registry remains process-local, not per-profile.

Timing retains `maestro.turn-timings.v1`, schema 1, legacy-clock migration,
30 reports, 120 events, one-second debounce and page-hide/hidden flushing.
Browser SVG serialization and headless raw SVG remain distinct. Text-only tutor
parsing consumes cleaned text without requiring SVG rendering capabilities.

Managed generation retains output ceilings, ownership checks before provider
access, admission/credit transactions and usage settlement. File deletion updates
metadata and quota atomically. Live/music share the existing lease cap. Routes,
Firestore schemas, prices and billing policies are unchanged.

## Existing behavior findings requiring separate decisions

These behaviors existed before the refactor and are intentionally preserved:

- The overall text timeout races the stream without itself aborting it. A
  cancellation change needs delayed-chunk and usage/cleanup tests first.
- Image context uses the first selected image part per history item. Additional
  parts need deliberate multi-image model-input contract review.
- Initial registry defaults share nested objects and getters expose them.
  Freezing or cloning requires a separate caller/immutability contract.
- Direct setters accept untrimmed/empty overrides; browser cache/remote refresh
  validate input first. Normalizing direct setters is an API change.

Large translation, theme and configuration tables are not refactor targets
merely because they are large. No new framework or arbitrary file-size target
is needed to maintain these boundaries.
