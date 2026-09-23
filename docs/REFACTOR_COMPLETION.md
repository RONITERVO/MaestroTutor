# Full architecture refactor completion record

Baseline: merged PR #246, `29e2516`, 2026-09-23. Branch:
`codex/complete-architecture-refactor`.

This work completes the remaining ownership changes in [the architecture
audit](./ARCHITECTURE.md). Existing prompt and request snapshots are acceptance
contracts, not outputs to regenerate when a structural change fails them.

| Boundary | Characterization before changes | Implementation | Verification |
| --- | --- | --- | --- |
| Chat suggestions, persistence and aftersteps | Actual-hook cache, failure, save-order, Live split and translation fixtures | Suggestion and translation coordinators with typed ports | Hook contracts and pre-move snapshots pass |
| Chat send, media and generated tools | Actual-hook request, busy/error/STT/media/re-engagement fixtures | Separate send, request preparation, capture, response, uploads, generated-image and tool owners | Initial extraction: hook contracts and unchanged snapshots passed in the 737-test suite |
| Live session, capture and playback lifecycle | Actual-hook connect, stop races, stale callbacks, decode cancellation, drain, transcripts, capture handoff and video fixtures | Session controller with separate capture, provider callback, playback, transcript, telemetry and cleanup owners; explicit browser runtime | Live extraction milestone: 10 hook tests and 709 root tests passed, along with lint/build and boundary guard |
| Browser/headless afterstep decisions and explicit differences | Actual browser and headless entry-point snapshots captured first | Core afterstep plan with explicit browser-chat, browser-live and headless policies | Entry-point snapshots unchanged; dedicated policy tests pass |
| Managed generation, files, cleanup and Live leases | Public-facade emulator tests freeze quota races, rollback, retries, ownership, provider requests and stream disconnect settlement | Separate generation, file quota/lifecycle/upload/cleanup, lease, token and music owners behind the existing facade | Initial extraction: 24 unit tests and emulator suites (16 new cases) passed; all 49 moved bodies matched baseline AST before review hardening |
| App speech/idle feature handoffs | Actual App render with real store and idle hook: 26 baseline handoff/guard/error/timing cases | Pure STT, speech-mode, reengagement and language-change coordinators, with explicit state/action ports and React lifecycle adapter | App extraction milestone: 26 baseline cases and 737 root tests passed, along with lint/build and transitive guard |

Release acceptance: unchanged model-input contracts; root tests, lint and build;
Functions unit and billing/lease/file emulator tests; gateway tests/build;
release configuration; appropriate staging and live smoke evidence; versioned
signed Android bundle for manual upload. Record exact commits, commands and
deployment evidence here as each completes. Offline tests do not establish
physical-device audio correctness.

## Chat ownership completed

`src/features/chat/coordinators/` now owns workflow decisions; the existing hook
retains its public API and supplies browser/state capabilities. `send.ts` owns
transaction ordering and speech/activity handoff, while `sendRequest.ts` prepares
history and media. User capture, text projection, attachment upload/reuse,
generated-image flows, media persistence and tool execution have separate owners.
`suggestions.ts` owns automatic suggestions and persistence order;
`suggestionTranslation.ts` owns learner-authored suggestions. No new framework or
dependency was introduced.

The transitive architecture check also covers these coordinators. It rejects
React/store/browser dependencies, including a store import hidden behind a shared
diagnostic helper. Diagnostics are supplied by the hook. Core prompt contracts
and previously committed snapshots are unchanged.

Evidence so far: baseline hook tests committed in `bc770a5`, headless baselines in
`fb4f704`, shared decisions and send baselines in `4dd0db8`, and the main chat
extractions in `5544e38` / `9c448b2`. Logs are retained locally under
`.maestro-debug/full-refactor-*`. This is intermediate evidence; the full release
gate must run again on the final complete refactor.

The four pre-existing behavior findings in the audit remain separate decisions:
text timeout cancellation, additional image context parts, registry mutability,
and direct model-setter normalization. This structural refactor must not change
those behaviors implicitly.

## Live ownership completed

`src/features/speech/live/` owns one session controller per hook instance. The
React hook only binds callbacks, activity tokens and controller disposal. Device
constructors, provider access, camera consent and browser video encoding enter
through explicit runtime ports. Capture/Whisper handoff, provider callbacks,
transcripts, decode/playback and cleanup retain their original ordering and
session/turn fences. The transitive architecture check rejects browser/store
imports into the controller graph while allowing the explicit browser adapters.

The initial eight hook lifecycle tests were committed in `ac1be72` before moves.
Two additional video/go-away cases also passed against a saved original hook
before accepting the extraction. All ten pass after extraction, with unchanged
connection snapshots. At the Live extraction milestone, root verification passed
709 tests across 106 files, lint and production build. These fixtures cover deterministic lifecycle behavior;
physical-device capture/playback remains part of release validation.

## Backend ownership completed

`functions/src/gemini.ts` remains the public route facade with its same 13
exports. `managedGemini/` separates generation and stream settlement, transactional
file quota, file ownership/eviction, upload rollback, status checks, account and
detached cleanup, shared Live/music concurrency leases, scoped token minting and
music generation. Existing `managedBilling`, `managedData`, pricing and policy
modules retain authority over charging and persisted paths.

The 16 new public-entry-point emulator cases were committed in `de7f3b2` before
extraction. They substitute only provider transport while using real Firestore
transactions and billing. Baseline and extracted runs both pass, including
competing leases, failed processing and cleanup, deletion during settlement,
oldest-file eviction, repeated deletion, detached retry backoff, generation
request bytes, disconnected stream usage settlement, token failure rollback and
music sample completion. They now run in the existing release and staging CI
emulator gate. Before review hardening, all 49 moved declaration bodies matched
the original AST. The subsequent verified failure fixes are described in
[the review record](./REFACTOR_REVIEW.md); normal provider request contracts remain unchanged.

## App handoffs completed

`src/app/coordinators/` owns speech destination routing, microphone/translation
mode actions, reengagement capture/fallback and language-context reset ordering.
App composes those operations with the existing feature APIs; `speechRoutingState`
projects fresh store state and `useLanguageSessionReset` owns React effect
cancellation. The coordinator graph cannot import those adapters, React, the
store or browser globals.

The 26 actual-App cases in `73f2b1f` passed before extraction and pass unchanged
after it. They cover captured/current translation destinations, attachment
forwarding, busy guards, stop/translation failures, fresh-state restart checks,
capture overlap and failure, history-load barriers, observer/Live handoffs,
250ms language restarts and cancellation, microphone toggles and idle scheduling.
At the App extraction milestone, the full gate passed 737 tests in 107 files, lint,
production build and the expanded transitive boundary guard. Deployment and
release artifacts still require the final acceptance evidence below.

## Review hardening and release candidate checks

The user authorized targeted fixes for verified failures after all three reviews
finished on `4030d48`. [The review record](./REFACTOR_REVIEW.md) distinguishes those
corrections from suggestions intentionally declined to preserve known behavior.
The counts below supersede the historical extraction milestones above.

- App after review hardening: 785 tests in 115 files, lint and production build pass.
- Prompt/artifact focused suite: 64 tests pass with unchanged snapshots.
- Functions: 24 unit tests and the billing, gateway and 31-case managed Gemini
  emulator suites validate the extracted services and review fixes.
- Gateway: 24 tests and TypeScript build pass.
- Release configuration and headless `system.describe` pass. The configuration
  verifier now checks the new music, tool and request owners and their public
  wiring; it retains the same behavioral requirements.
- Dependency audit thresholds pass. Existing lockfiles are unchanged: the app
  reports four high advisories in its Node tooling path, Functions reports nine
  moderate advisories through `qs`/`uuid`, and the gateway reports none. No force
  dependency upgrades are bundled into this structural refactor.
- Android candidate is 2.6.10 (84), reserved for a new signed bundle. No ADB
  device was connected during offline checks; hardware/Play validation is not
  implied by the automated lifecycle fixtures.

The initial staging allowance was nearly exhausted (99.483622 of 100 USD on
2026-09-23). With the user's explicit authorization, staging's daily admission
limit was increased to 150 USD and deployed; the production limit remains 100.
Paid staging and production results, deployment revisions and signed bundle
identity are recorded in the release PR/receipt. These local checks do not claim
that this candidate is already live.
