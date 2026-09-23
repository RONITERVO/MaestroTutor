# Refactor review decisions

PR [#247](https://github.com/RONITERVO/MaestroTutor/pull/247) was reviewed by
CodeRabbit, Cubic and Codex Cloud at `4030d48`. All three completed before the
fix batch was pushed. The user authorized fixes for verified existing failures
while preserving normal model inputs. The 74 inline annotations include repeated
findings across reviewers; the groups below account for every annotation.

## Verified corrections

| Area | Correction and regression coverage |
| --- | --- |
| Concurrent chat actions | Synchronous send and translation ownership reject reentrant calls before React copies activity state. Actual-hook tests verify one provider call and complete token release. |
| Optional chat work | Profile lookup and user image-generation failures no longer abort the tutor send. Assistant image errors clear loading flags. Tests cover rejected generation and verification. |
| Media persistence | Optimized local media survives remote upload failure; original bytes remain the upload source. Inactive cached variants are replaced and counted in upload progress. |
| Suggestion reuse and context | Target and sibling cache reuse apply the same structured-tail validity check. Removed the unused prompt port and zero-retry loop. Tool-context truncation keeps Unicode surrogate pairs intact. Normal request snapshots are unchanged. |
| App handoffs | Pending language resets survive callback replacement and use current speech callbacks. Delayed translation-mode restart reads the current language. Reengagement owns its whole asynchronous handoff; microphone enables can be cancelled before observer shutdown finishes. |
| Live startup and shutdown | Stale startup results release only their own resources. Concurrent stops share cleanup, cleanup resets its guard in `finally`, and capture/router/codec or consumer callback errors do not skip remaining teardown. Removed cleanup polling and its global timers. |
| Live playback and transcripts | Concurrent drains include the hardware tail and cannot mark a newer session. Transcript updates flush before completion; thinking traces compare entries structurally. Responses with audio but no transcription complete and remain replayable as an attachment. |
| Live video | Explicit ownership protects caller video elements. Readiness waits are bounded; superseded setup and encoded frames cannot replace or send through the current capture. Old frames cannot release a new frame's in-flight guard. |
| Live input and telemetry | Continuous turns honor the existing playback-settle mute. Preconnect activity belongs to the selected mode. Stream ends are counted when successfully sent; detector readiness and empty packetizer statistics each have one owner. |
| Managed file identity and deletion | Generic 403s retain ownership and quota and enter cleanup retry. Confirmed 404s or documented provider expiry can release metadata. FAILED provider files are deleted remotely before quota release. Status results retain invalid keys including `__proto__`. |
| Upload admission and accounting | Credits are reserved before eviction. Eviction pages the whole active inventory before ordering candidates. Expiring, identified upload slots transfer atomically to file metadata; repeated rollback cannot decrement another file. Admission repairs abandoned counters. Late release cannot recreate a deleted runtime document. |
| Durable remote cleanup | Processing metadata is recorded before polling, so failed cleanup retains an owner and slot. Upload rollback and clear-files enqueue opaque remote names. Duplicate enqueue preserves attempts/backoff; jobs contain no user identifier. |
| Generation accounting | Direct and streamed generation share reservation admission. Completed generation/music persist exact pending accounting and retry settlement; expiry reconciliation settles that evidence instead of refunding it. Provider failures retain the existing refund policy. Failed stream refunds cannot replace the original NDJSON error or prevent response completion. |
| Live tokens and client configuration | One lease permits one token use, even with a legacy multi-use environment setting. Token expiry derives from the lease; admission latency cannot extend it. Music uses the shared configured client with its required API version. |
| Maintenance contracts | File exceptions in the transitive import guard match exactly. Added browser artifact-only planner cases, retained the Live state union, pinned emulator settings, and updated the README and milestone labels. |

Provider absence and expiry decisions follow Google's
[error reference](https://ai.google.dev/gemini-api/docs/generate-content/api-errors)
and [Files retention documentation](https://ai.google.dev/gemini-api/docs/files).
Uploaded provider `expirationTime` is retained when supplied; legacy creation
time supplies the documented 48-hour upper bound.

## Suggestions intentionally not applied

| Review comment | Decision and evidence |
| --- | --- |
| Cubic `4087815456`: refund music on client cancellation | A dropped client fetch does not establish that provider work was cancelled. The existing managed policy charges completed work and refunds provider failures. Automatically refunding after a caller disconnects would change that policy. Completion, failure, lease cleanup and settlement recovery are exercised separately. |
| Cubic `4087815476`: construct the Live controller in an effect for StrictMode | Construction acquires no microphone, socket, timer or worker. `dispose()` resets owned resources rather than permanently disabling the controller. An actual-hook StrictMode test verifies startup and teardown after React rehearses effect cleanup. |
| Cubic `4087815508`: reset concealed counters for later turns | Each paid connection owns one response. Both counters already reset at every `start()`; the provider finalizer tears down that connection. The next user turn starts a new session, so the claimed carry-over does not occur. |
| Cubic `4087815550`: deduplicate image prompts by substring/case | This would alter established model context without proving a failure. The existing policy suppresses only identical trimmed segments. Explicit tests preserve distinct case variants and embedded prompts, alongside the identical-text case. |

The four earlier audit topics—overall stream timeout cancellation, additional
image parts, model-registry mutability and direct model-setter normalization—also
remain explicit future behavior changes, as documented in the architecture guide.

## Verification and operations

The follow-up review of `58c3241` produced 11 more annotations. All three
reviewers completed again before the follow-up batch was pushed. Every new
annotation is addressed:

| Review comments | Correction and evidence |
| --- | --- |
| `4088278960`, `4088299990`, `4088283010` | Expiry recovery releases pending settlements after a deletion claim, isolates failed rows, and reports successful recoveries so the scheduler cannot loop forever over a failed full batch. Emulator cases exercise complete account deletion and retry, unrelated accounts, and recovery after transient settlement failures. |
| `4088283025` | Both recording and settling completed work retry once; deletion conflicts remain terminal. Direct/stream tests assert one provider execution and one charge. |
| `4088300018` | A transient BYOK status lookup now rejects like the managed path. Coordinators retain cached variants; final history verification fails explicitly instead of silently omitting media from model context. Real-adapter tests cover both paths and preserve confirmed inactive-file behavior. |
| `4088300024`, `4088300042` | Quota and eviction share paginated inventory including records without `deletedAt`. Processing files remain protected for the upload reservation window, while abandoned processing records can eventually be evicted. Tests cover mixed and entirely processing inventories. |
| `4088300037` | Successful detached cleanup resolves any surviving canonical file owner by opaque provider name, then releases its quota transactionally before completing the job. Jobs still retain no UID and cannot recreate deleted metadata. Requires the added `files.name` collection-group index before Functions deployment. |
| `4088300031`, `4088300051`, `4088300009` | Cancelling microphone startup resets the observer hold; actual-App tests distinguish cancellation from unmount. The Live test now asserts exact cached audio. Pending release receipts are described in future tense. |

Regression cases were exercised against the faulty paths before applying their
fixes. Existing prompt/provider snapshots were not regenerated. The final local
gate contains 788 root tests and 41 managed-Gemini emulator cases, in addition
to the existing Functions unit, billing/gateway emulator and gateway test suites.
Exact CI, staging, production and Android artifact receipts belong to the release
PR; local tests alone do not establish live deployment or physical-device behavior.

The schema additions are internal and additive: `pendingUploadSlots` on file
quota summaries, optional provider `expirationTime` on file metadata, and
`pendingSettlement` accounting on reservations. Pending settlements contain
accounting fields, not generated content. The existing expiry sweeper recovers
them and keeps settlement idempotent; account deletion can still release holds
without charging a deleted account. Provider files whose deletion fails remain
counted until deletion/expiry is confirmed. Background cleanup retries their
opaque names and reconciles surviving local records and quota before completing.
