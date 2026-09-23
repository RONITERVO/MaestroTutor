# Full architecture refactor completion record

Baseline: merged PR #246, `29e2516`, 2026-09-23. Branch:
`codex/complete-architecture-refactor`.

This work completes the remaining ownership changes in [the architecture
audit](./ARCHITECTURE.md). Existing prompt and request snapshots are acceptance
contracts, not outputs to regenerate when a structural change fails them.

| Boundary | Characterization before changes | Implementation | Verification |
| --- | --- | --- | --- |
| Chat suggestions, persistence and aftersteps | In progress | Pending | Pending |
| Chat send, media and generated tools | Pending | Pending | Pending |
| Live session, capture and playback lifecycle | Pending | Pending | Pending |
| Browser/headless afterstep decisions and explicit differences | Pending | Pending | Pending |
| Managed generation, files, cleanup and Live leases | Pending | Pending | Pending |
| App speech/idle feature handoffs | Pending | Pending | Pending |

Release acceptance: unchanged model-input contracts; root tests, lint and build;
Functions unit and billing/lease/file emulator tests; gateway tests/build;
release configuration; appropriate staging and live smoke evidence; versioned
signed Android bundle for manual upload. Record exact commits, commands and
deployment evidence here as each completes. Offline tests do not establish
physical-device audio correctness.

The four pre-existing behavior findings in the audit remain separate decisions:
text timeout cancellation, additional image context parts, registry mutability,
and direct model-setter normalization. This structural refactor must not change
those behaviors implicitly.
