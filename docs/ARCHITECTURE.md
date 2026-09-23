# Architecture ownership and refactoring plan

Audited against the merged prompt-contract baseline `a3a9187` on 2026-09-23.
The goal is to make working behavior easier to change deliberately. The existing
Core SDK, feature modules, prompt snapshots, release gate and billing emulator
tests are the foundation for incremental changes.

## Where a change belongs

| Responsibility | Owner | Boundary |
| --- | --- | --- |
| Authored model instructions, schemas, context markers | `shared/prompts/` | Pure catalogue; no storage, transport or UI dependencies. See [prompt contracts](./PROMPT_CONTRACTS.md). |
| Provider request composition, retries, streaming and response decoding | `src/core-sdk/gemini/` | Requires an explicit client or lazy client resolver. No browser credential selection. |
| Shared text, suggestion and image journeys | `src/core-sdk/chat/` | Inputs, injected client/runtime, events and results. No React or store access. |
| Browser Gemini access selection and usage display | `src/api/gemini/` | `client.ts` selects BYOK/managed access; `journeys.ts` composes Core with the resolver and image usage sink. Existing generative/vision entry points remain compatible. |
| Active model values and validation | `src/core-sdk/modelRegistry.ts` | One process-local registry, shared by browser facades and Core. Preserves existing defaults and override behavior. |
| Browser model cache, URL migration and remote refresh | `src/core/config/models.ts` | Compatibility facade over the registry; owns localStorage and fetch lifecycle. |
| Timing reports and bounded event history | `src/core-sdk/turnTiming.ts` | Recorder factory with an optional storage port; no import-time browser effects. |
| Timing persistence and visibility/page-hide flushing | `src/platform/browser/turnTiming.ts` | One browser recorder with the existing storage key, schema and listeners. |
| UI, activity tokens, device capture/playback, user actions | `src/features/` | Features expose public APIs; hooks bind state and hardware to shared operations. Some hooks still own too much orchestration, detailed below. |
| Filesystem profiles, fixtures, CLI/RPC and artifact output | `src/headless/` | Calls Core with explicit clients and adapters. It is a separate application shell. |
| Prices, usage calculations, Live controls and protocol contracts | `shared/pricing/`, `shared/billing/`, `shared/live*` | Shared pure contracts/math. Browser estimates do not authorize backend charges. |
| Managed authorization, reservations, ledger writes and cleanup | `functions/src/` | Backend is authoritative. Preserve idempotency, limits and transactions. |
| Managed Live sessions and accounting | `live-gateway/src/` | Owns connection protocol and server session lifetime; shares billing/control contracts. |
| App composition and cross-feature routing | `src/app/App.tsx` | Still includes STT routing and idle orchestration; not yet a pure composition root. |

Core imports may reach pure `src/core/config`, `src/core/types`, or shared helpers.
Folder names alone do not prove purity: the dependency check follows runtime
imports and re-exports, including literal dynamic imports and `@/` aliases.

## Findings and priority

Priority reflects coupling and regression risk, not line count. Large translation,
theme and configuration tables are not refactor targets merely because they are
large. This audit covered the app shell, chat/Live coordinators, Core runtime and
its dependency graph, headless journeys, browser persistence, Functions generation
and file management, gateway boundary, and their test/CI coverage. It is not a
claim that every function is now independently tested.

| Priority | Finding and evidence | Change / next acceptance criterion |
| --- | --- | --- |
| 1 — addressed here | Core text/suggestions/image journeys imported browser `api/gemini` implementations. Those implementations selected browser access implicitly when no client was supplied; image generation also reached localStorage through cost tracking. Headless isolation depended on avoiding dormant paths. | Provider algorithms now live in Core with required typed client sources. Browser facades supply access and usage sinks. Characterization tests preserve selection timing, error behavior, stream deltas, image retries and usage order; existing provider payload snapshots remain unchanged. |
| 1 — addressed here | `core/config/models.ts` combined shared values with browser cache and remote refresh. Moving only Gemini transports would leave Core transitively coupled to browser storage. | One pure registry now owns values/validation; the old configuration module remains the browser persistence facade. Tests preserve cache recovery, invalid data handling, URL migration and remote update behavior. |
| 1 — addressed here | `core-sdk/turnTiming.ts` read localStorage and registered browser lifecycle listeners at import time. Its singleton lifetime was inseparable from persistence. | Core creates independent recorders; the browser owns the singleton and lifecycle hooks. Existing timing tests now exercise the browser facade, with additional tests for recorder isolation, legacy data, debounce and lifecycle flushing. |
| 2 — next | `features/chat/hooks/useTutorConversation.ts` coordinates attachment uploads, provider calls, suggestion reuse, activity tokens, summaries/profile writes, generated artifacts/tools and history persistence. Its `fetchAndSetReplySuggestions` path (~300 lines) has no direct hook-level characterization tests. | Characterize the actual hook first: direct/sibling suggestion reuse, structured-tail regeneration, Live artifact/tool splitting, loading-token release, history/profile save order and errors. Then extract a suggestion coordinator with explicit state/persistence/tool ports. Keep React/store refs in the adapter. Do not merely relocate the whole hook. |
| 2 — next | `features/speech/hooks/useGeminiLiveConversation.ts` combines setup, capture, transcript accumulation, codec/worklet state, provider callbacks, playback and cleanup. Existing Core speech gates, packetizers and finalizers already own important parts of this behavior. | Add hook-level fixtures for stop during connect/decode, stale session callbacks, final transcript flush, capture handoff and playback drain. Then extract one session controller using existing Core primitives. Preserve session IDs and cleanup ordering. Physical capture/playback still needs device proof. |
| 3 | Suggestion artifact/tool aftersteps exist in both the chat hook and `headless/suggestionJourney.ts`. Shared normalization/dispatch already exists in `core-sdk/chat/suggestionAftersteps.ts`, but raw context and persistence choices differ by mode. | Document and freeze the differences before extracting more shared decisions. Browser Live compact raw text and headless fallback raw text must not be silently harmonized. Share decisions only when their inputs and effects are equivalent. |
| 3 | `functions/src/gemini.ts` mixes generation, upload slots, file lifecycle/cleanup, Live leases/tokens and music. These responsibilities share quota and billing state. | Extract file management and lease services in separate PRs after adding failure-path emulator coverage for reservation/release, retry cleanup and idempotency. Retain public route contracts and transaction boundaries. No billing-policy change in a structural refactor. |
| 4 | `src/app/App.tsx` includes composition plus STT/idle coordination, while the README previously claimed all logic lived in features. | Corrected the documentation now. Move routing into a focused coordinator only after action/event tests freeze feature handoffs and busy-state arbitration. |

## Behavior preserved by this change

- No authored prompt, request snapshot, history policy, role ordering, model ID,
  generation setting, fallback model or retry delay changes.
- Browser client selection is lazy: journey-start events happen before access
  resolution, and image/suggestion attempts can resolve current access again.
  An explicitly supplied client still takes precedence. `client.ts` keeps the
  existing BYOK/managed policy and Live-open gate.
- Image usage is recorded before response completion/result extraction, including
  responses containing no valid image. Browser facades retain that sink; headless
  has no browser cost-display persistence. Managed billing stays server-side.
- The model registry remains process-local shared state. This PR does not change
  it into per-profile or per-request configuration.
- Timing retains `maestro.turn-timings.v1`, schema version 1, legacy clock migration,
  30 reports, 120 events, one-second write debounce, and page-hide/hidden flushing.
- Chat/profile persistence, attachment upload/reuse, Live media lifecycle, billing
  reservations and credit limits retain their existing owners and algorithms.

## How to extend safely

1. Identify the owner above and characterize the current observable behavior at
   its real entry point before moving logic. Record inputs, event/callback order,
   side effects and failure paths; mocks should substitute ports, not the logic
   under refactor.
2. Browser callers use the API facades. Core callers pass `{ aiClient }` or
   `{ resolveAiClient }`. Add a small explicit port when Core needs a new platform
   capability; do not import a feature/store or add a global service registry.
3. Run `npm run test:prompts`, `npm run verify:core-boundaries`, `npm test`,
   `npm run lint`, and `npm run build`. Prompt snapshots are the pre-refactor
   baseline: do not update them to make a structural refactor pass.
4. The boundary guard is part of `npm test`, so the existing release gate enforces
   it automatically. Its fixture tests intentionally introduce direct and
   transitive browser dependencies. It checks first-party runtime syntax and
   known browser packages; it is not a semantic proof of arbitrary third-party
   code or computed global access. Computed module loading is rejected because
   the graph cannot inspect it.
5. Match extra validation to the affected boundary. Billing changes require the
   Functions/emulator gate; Live protocol changes require gateway tests and real
   media evidence. Offline fixtures freeze Maestro's inputs and deterministic
   behavior; they do not claim deterministic model answers or hardware coverage.

Deliver each next priority as its own reviewed, verified change. No new framework,
repository-wide rewrite, formatting campaign or arbitrary file-size target is
needed to follow this plan.
