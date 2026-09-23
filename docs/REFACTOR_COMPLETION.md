# Full architecture refactor completion record

Baseline: merged PR #246, `29e2516`, 2026-09-23. Branch:
`codex/complete-architecture-refactor`.

This work completes the remaining ownership changes in [the architecture
audit](./ARCHITECTURE.md). Existing prompt and request snapshots are acceptance
contracts, not outputs to regenerate when a structural change fails them.

| Boundary | Characterization before changes | Implementation | Verification |
| --- | --- | --- | --- |
| Chat suggestions, persistence and aftersteps | Actual-hook cache, failure, save-order, Live split and translation fixtures | Suggestion and translation coordinators with typed ports | Hook contracts and pre-move snapshots pass |
| Chat send, media and generated tools | Actual-hook request, busy/error/STT/media/re-engagement fixtures | Separate send, request preparation, capture, response, uploads, generated-image and tool owners | Hook contracts pass; full root suite 693 tests, lint/build passed before final diagnostic-port guard and translation extraction |
| Live session, capture and playback lifecycle | Pending | Pending | Pending |
| Browser/headless afterstep decisions and explicit differences | Actual browser and headless entry-point snapshots captured first | Core afterstep plan with explicit browser-chat, browser-live and headless policies | Entry-point snapshots unchanged; dedicated policy tests pass |
| Managed generation, files, cleanup and Live leases | Pending | Pending | Pending |
| App speech/idle feature handoffs | Pending | Pending | Pending |

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
