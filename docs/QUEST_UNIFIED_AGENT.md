# One Maestro app, shared room tasks

Accepted direction (2026-09-26): the original Maestro app owns all Gemini access,
conversation, subscription/BYOK routing, tool initiation and task orchestration.
Unity supplies capabilities, observed state and validated execution. Room work is
an app tool that chat or the suggestion creator can initiate after checking the
current request. It is not a separate Live persona or a second assistant product.

## Intended product flow

1. The user speaks or types in the familiar conversation. Existing speech, tutor
   and suggestion flows keep their current ownership and format.
2. Chat proposes a room tool, or the existing Live suggestion afterstep recognizes
   an explicit room request. The proposal is checked against the actual current
   user request, available capability and host permission. A model's proposal,
   quoted exercise, artifact or older message cannot authorize a mutation.
3. The app starts a task with a stable ID, source user/assistant IDs, conversation
   and access scope. Capture the original user request without rewriting or
   truncating it, together with the exact tutor-input snapshot: history after the
   normal bookmark/media preparation, current attachments, language preferences
   and system context. The host supplies these; the model emits only the handoff
   proposal. Never substitute the suggestion creator's shortened history, a
   generated task summary, the assistant's reply or whichever user message is
   newest after an asynchronous operation. Preserve the parent assistant/user IDs
   when an artifact causes a separate tool message. Starting the same tool twice
   must return the existing task rather than repeat its effects.
4. The task plans a bounded batch, reads current room state, uses the common native
   executor, records its receipt and verifies the result. Further model calls use
   the same Maestro provider/client routing and settings. The model receives actual
   state/results, not an optimistic declaration that work succeeded.
5. Progress, clarification, Stop and results belong to the original conversation.
   Completed native effects remain visible even if the final model reply or audio
   fails. Activity goes through the existing header flag and shared avatar-state
   projection; status updates require no extra chat-model call. The same saved
   recipes, animations and rules remain editable manually.

The tutor, request verifier and task loop can require different model calls. They
are roles in one app-owned workflow, not duplicated providers or independent chat
histories. Gemini keys and billing remain outside Unity. Live is a speech interface;
room actions do not require direct Live function calling. The disabled experimental
transport from e50d281 is optional infrastructure, not a v1 activation requirement.

## Keep conversation lightweight

The chat model recognizes the handoff and remains responsible for conversation.
The task receives the same starting context, then owns its subsequent planning,
observations, native receipts and verification. This is a separate task history,
not a second user-facing conversation. The same configured model can serve both
roles; separating their context/lifetime is what prevents operational detail from
crowding the tutor conversation.

Return concise progress at meaningful milestones, questions that require the
user, and a final result or concrete limitation to the chat. Persist detailed
receipts outside ordinary tutor history and expose them through task details.
Only bounded summaries should enter later tutor prompts; do not copy every
planner response, full scene snapshot or per-frame event into chat. Avoid having
the chat model narrate every agent step. The task still consumes the user's shared
API budget and needs explicit action/iteration limits and checkpoints.

Agent activity must not masquerade as the normal response-generation token.
`selectIsSending` currently treats every `gen:*` token as blocking generation,
and chat/audio/header controls use it. Add a distinct task activity in the existing
flag system and project it into shared Maestro animation states without disabling
ordinary conversation. Actual listening/speaking keep their normal priority; a
long task must not hold Maestro in a thinking animation over an active reply.
Explicit task Stop belongs to the task lifetime, separate from stopping speech.

## Ownership and implementation seams

| Owner | Responsibility | Existing implementation |
| --- | --- | --- |
| Original app | BYOK precedence, managed auth and billing | `src/api/gemini/client.ts`, `browserClientSource.ts` |
| Tutor | Language response and room tool proposal | `src/core-sdk/chat/tutorTextTurn.ts` |
| Suggestions | Normalize/verify a tool proposal after chat or Live | `src/features/chat/coordinators/suggestions.ts` |
| Tool coordinator | Start once, progress, cancellation, result persistence | `src/features/chat/coordinators/assistantTools.ts` |
| Room task | Plan/observe/execute/verify using the caller's Gemini source | `src/core-sdk/room/roomAgent.ts` |
| Native bridge | Correlated acknowledgement, revision checks and request cancellation | `src/platform/quest/roomAgentBridge.ts` |
| Unity | Shared room/rule operations used by users and agents | `RoomAgentExecutor`, `RuleWorkshop`, `RoomJournal` |

Phone/PC chat without a connected room must not advertise unsupported room actions.
An agent should return a capability limitation rather than start a separate cloud
room simulation or silently create a different product. No separate Unity Gemini
SDK, provider key, subscription ledger or prompt fork is required.

## Task behaviour for the long term

- Task identity is separate from the tutor's current response. Ordinary unrelated
  conversation need not kill a longer task; explicit Stop, permission/account loss,
  room-session loss and conflicting edits do prevent further actions. A follow-up
  that changes the task becomes an explicit task revision.
- Save pending operations before dispatch and receipts after native acknowledgement.
  Cancellation after dispatch can leave an unknown outcome; inspect/reconcile it
  and never blindly replay it. An animation-start acknowledgement is not evidence
  that the whole animation completed.
- Persist task state and bounded progress independently of generated prose. A
  restart should show an interrupted/uncertain task and require deliberate recovery,
  not restart mutation automatically. Keep results tied to the original conversation.
- Use scoped observations and capability groups to control cost. The initial runner
  permits three action batches plus six discovery/inspection batches; longer tasks need explicit task budgets, checkpoints and
  resumable progress, not an unlimited loop or increasingly huge system prompts.
- Native continuous physics, walking and hand/head tracking stay in Unity. Agents
  set intent, rules and targets; they do not stream per-frame transform commands.
- Manual controls, visual blocks and task agents use the same native operations and
  revisions. Developer tests should use those operations plus independent input/
  rendering tests and headset QA; direct command success does not prove usable VR.

## Text-chat handoff checkpoint (2026-09-26)

The non-Live chat path now uses the proposed flow. A connected room adds one short
`agent` tool instruction to the ordinary tutor turn. The host captures that exact
prepared input against real user/assistant IDs; a proactive reply, restored tool
block or unconnected phone chat has no handoff capability. The existing suggestion
creator verifies the tool proposal against the captured original request. Its
schema only includes `agent` for that eligible proposal. The tool carries no
rewritten prompt. `assistantTools` starts the task using the original source ID,
including when the tutor also produced an artifact.

`src/api/gemini/journeys.ts` no longer runs the legacy room planner before every
Quest reply. The app-owned task independently observes/plans/executes (three action
batches plus six discovery/inspection batches maximum) and prepares its own normal-format reply. It uses the same
browser client resolver for BYOK or managed access, retains original attachments,
history and tutor context, and publishes its result in the original conversation.
The original tutor context is task data; it cannot override the native command
schema. Detailed observations and receipts stay outside ordinary chat history.

The task journal lives in the existing app database (v11, `agentTasks`). It commits
an atomic initiation claim and each pending action before native dispatch, then
commits acknowledgements before the next model call. Duplicate initiation IDs
return existing records. An interrupted/unconfirmed operation is never replayed
automatically. Receipts survive narration failure. Deleting source conversation
messages prunes their task records; late saves cannot recreate a deleted task.
Validated backup replacement now restores full task journals as read-only history.
Explicit history deletion still clears journals; imported task references cannot run.

The existing header flag displays `agent:task`; it does not set `gen:response` or
block chat input/passive speech. The book projects agent activity to the existing
Thinking avatar state, below actual listening/speaking priority. Task status,
Stop and on-demand saved receipt details are inside chat on the book pages.
One room task runs at a time. New ordinary chat does not cancel it; source
removal, conversation/access/native-session change and explicit Stop prevent
further actions. Stop aborts owned pending native work and signals the app's model
transport during planning and final narration. It promptly releases task activity,
cancels retry backoff, and discards late text/thoughts/results even if the transport
ignores cancellation. Cancellation while resolving the client cannot later send
that task. Recorded room actions and receipts remain available after Stop.

This stops app work; it does not promise that provider generation or billing stops.
Before managed provider dispatch, cancellation now halts admission and releases
any completed credit reservation. After dispatch, the managed backend drains the
accepted stream for usage even if the client disconnects or socket writes fail.
BYOK passes the abort signal to the SDK. Google's current SDK explicitly describes
AbortSignal as local cancellation rather than service cancellation, with applicable
usage still charged: [GenerateContentConfig documentation](https://googleapis.github.io/js-genai/release_docs/interfaces/types.GenerateContentConfig.html).
See the managed-stream checkpoint below for the verified boundary and limitations.

## Live/observer text-context handoff checkpoint (2026-09-26)

User-opened Live and the locally gated observer now prepare the same room handoff
at the actual connection boundary, after their fresh profile/bookmark/history
instruction is built. The preparation returns that exact instruction plus one
short spoken-handoff capability and a locally issued opaque identity. Only the
instruction reaches Gemini; the identity travels through the owned provider
callback to turn completion. Ordinary unconnected phone sessions retain their
existing instruction and callback shape.

The completed original transcript and exact connection instruction are captured
against the persisted user and assistant message IDs before the existing
suggestion creator runs. History is already serialized inside the Live instruction;
it is not rebuilt from newer messages or duplicated as a second history. The
capture binds the account, language conversation and native session at connection
time, rechecks after credential reads and is consumed once. Empty speech, forged
identities, stale scope and edited source transcripts/replies cannot initiate it.
Only eight unconsumed connection captures are retained in memory.

Live audio does not speak JSON. Its scoped instruction asks for an ordinary spoken
handoff; the shared suggestion model must verify BOTH the original explicit room
request and the tutor's proposed handoff before returning the existing `agent`
tool. Language exercises, quotations, hypotheticals and background speech are
explicitly excluded in that verification prompt. This is model intent verification,
not a deterministic guarantee of speech attribution. Real-provider acceptance is
still required. No direct Live room function protocol is enabled.

The shared afterstep retains the exact source reply when a verified Live handoff
also includes an artifact; history compaction uses the recorded tool kind and
attachment. Without this exception, the existing Live compact-history rewrite
would invalidate the source before execution. The task uses the same dispatcher,
client routing, activity, cancellation and journal as typed chat. Malformed final
narration with no readable language reply reports failure while retaining receipts,
instead of displaying a successful blank result.

Original Live input now accompanies an eligible handoff (PC verification,
2026-09-26). After explicit user approval for this use, the browser retains only
PCM and JPEG bytes whose `sendRealtimeInput` call succeeded, under the same
camera consent and speech gate. It wraps the PCM in a mono 16 kHz WAV without
resampling, trimming or inserting silence. Packet delivery times/sample offsets
and frame delivery times/audio offsets preserve the submitted order and timing.
These are client submission records, not proof of provider receipt/processing,
full video, or new observations of the room.

Capture exists only for an app-issued connected-room handoff identity. It is
sealed at turn completion and released on teardown. Barge-in marks context
incomplete. The limits per handoff are 4 MiB of decoded media (including the WAV
header), 90 seconds of sent audio, 90 JPEG frames and 2,048 audio packets. An
exceeded limit discards the entire retained payload instead of silently truncating
it. Missing, interrupted or invalid media produces a saved, readable task failure
before planning or native dispatch; the original Live conversation still completes.

A complete context is validated and copied before asynchronous access checks,
validated again before planning, and sent as inline media through the existing
shared Generate Content client for both managed access and BYOK. Each planning
and final-narration call includes it and uses the existing usage accounting; this
adds media input cost to those requests. There is no new provider client in Unity.
The suggestion creator continues to verify the original transcript and tutor
handoff proposal; it does not consume the media payload. The private task journal
retains media with the source context and is pruned with source history. Ordinary
chat and diagnostic request logs do not contain these payloads. The verifier's
in-memory lookup retains only the request text, avoiding a second media cache;
the existing handoff cache remains bounded to eight captures.

**Scope still missing:** real-provider audio/image comprehension, speech intent
acceptance, Quest memory/latency and camera availability, spoken clarification,
task steering and headset acceptance of audible task results. No claim is made to
reproduce the Live model's private compressed state or internal reasoning. This
does not establish full hands-free acceptance.

## Audible task results (PC verification, 2026-09-26)

A fresh completed, limited, failed or interrupted task result now queues the
actual projected chat message for the existing `speakMessage` path. It keeps the
same voice, target/native-language preference, cache and managed/BYOK TTS routing.
There is no new narration model call or Unity audio/provider implementation.
Existing TTS may still incur its normal synthesis cost. Stopped tasks are not
announced automatically. Failure notes currently remain English, as in chat.

The Core queue holds at most eight fresh results. It deduplicates notifications,
checks the original source/access/native-session validity before and after audio
acquisition, and rechecks active delivery. It waits for active Live/observer
turns, STT, other TTS, response generation and explicit UI holds. Idle local
monitoring yields: user-selected Live remains selected while its transport stops,
and the passive observer stays stopped under a shared TTS activity reservation.
A failed observer shutdown leaves the result waiting. Pending observer instruction
builds cannot reopen input while result speech owns audio. Listening resumes only
after output drains and the same conversation/session still owns the selection.

Stop clears pending announcements and awaits an active output shutdown before
restoring listening. Manual Live start shares that shutdown rather than stopping
the same transport twice. Pair/source/access/native-session loss, suspension and
unmount prevent late speech; separate reservation IDs keep late cleanup from
removing a newer audio owner. Results are not scanned from stored history, so
reload or repeated durable task claims never replay announcements. The message
and task receipts remain available even if automatic speech fails. This queue
only controls result delivery; it does not cancel completed native actions.

Tests compose the real queue/store with the observer lifecycle, test user-owned
Live restoration and manual Live start ordering, and verify the ordinary speech
orchestrator's language/cache selection. Provider audio and microphone devices are
simulated. Real-provider timing, acoustics, barge-in and Quest suspension/restore
remain acceptance gates; this is not full hands-free acceptance.

## Durable result recovery (PC verification, 2026-09-26)

Database v9 adds a compact `agentTaskSummaries` store indexed by conversation.
The v8 upgrade derives summaries one journal at a time; it retains original task
records and existing chat/bookmarks. New task claims and updates commit journal,
summary and persisted chat projection in one transaction. A new claim also checks
that both source messages still exist in durable history, preventing a deletion
that completed during authorization from being followed by a new task claim.

History loading and saving reconcile from the small summary records. A completed
result missing from chat is restored next to its original reply; a stale result
is updated in place while preserving its message identity, position and audio
cache. A late autosave cannot replace a newer task result. Source IDs link results
for immediate cleanup in the current view. Removing source history deletes its
journal, summary and projection; late journal writes are rejected. Deleting only
a result hides that projection across reload and later updates, retaining the
existing no-replay claim and receipts until the source history is removed.

Recovery never executes room commands, emits completion notifications or starts
speech. An unfinished journal retains its last recorded phase and unconfirmed
operations. The existing task UI explains that a task without this view's runner
is no longer running here and directs the user to inspect the room; it does not
invent success, restart it, or overwrite a potentially live runner in another
window. This is recovery on history load, not cross-window live synchronization.
Routine chat loads read no private task audio, frames, plans or room snapshots.

The real-browser recovery probe exercises v8-to-current migration, actual app history
loading across conversations, bookmark preservation, stale saves, duplicate
claims, zero automatic announcements, explicit result/source deletion and reload.
An injected IndexedDB abort verifies rollback of both journal and chat. The
existing handoff probe also passes its v7 migration, native-acknowledgement and
Stop/no-replay tests. Recovery uses real IndexedDB; provider/native actions remain
simulated or uncalled. Full task-journal backup/import now has the separate PC
coverage below. Quest upgrade and storage/lifecycle testing remain required.

## Full task backup and atomic import (PC verification, 2026-09-26)

Save All and Save Chat now stream complete task records whose original source
messages are included in the export. This preserves exact original context,
retained PCM/JPEG input, receipts, final replies and hidden-result status. The
existing `ndjson-v1` format adds `taskArchiveVersion: 1`, indexed task chunks with
SHA-256 checksums and an end record counting conversations and tasks. Checksums
detect damage; they do not authenticate claims in an externally edited file.
The usual backup controls describe the private media included in these files.

The shared Core decoder accepts legacy chat-only archives and validates new
archives strictly, including chunk order, identities, version, completeness and
display fields. It never executes imported commands. The browser adapter stages
validated entries in database v10's `backupStaging` store, then replaces chat,
bookmarks, profile, avatar image, journals and summaries in one IndexedDB
transaction. Invalid input, missing task sources or storage failure cannot leave
half-replaced history. Selected-conversation merging retains existing task IDs,
reconciles hidden/authoritative results and leaves other conversations untouched.
Both import paths reload through normal history loading, retaining its language
switch fencing and bookmark behavior.

Before committing, the app revokes prepared handoffs, stops its current task and
awaits task cleanup. Imported records are read-only: prior callbacks cannot
rewrite them, and duplicate claims cannot rerun actions. Unfinished records keep
last-known phase and uncertainty; no automatic completion announcement, speech,
provider call, native dispatch or resumption occurs. This does not undo earlier
room effects or cancel a different browser window's already dispatched action.
A chat/task archive does not replace native room saves, imported model files,
motion libraries or externally referenced attachment content.

Memory retains one conversation and one task during decoding; a task is limited
to 64 Mi characters and split into 128 Ki-character chunks. Oversized records fail
explicitly rather than being truncated. Staging requires space alongside existing
data. Failed imports discard their batch; staging abandoned by a crashed process
is removed on a later import after seven days. Export snapshots each journal
individually, not the entire live app at one instant. Restoring reconciles task
results from those retained records. Older app builds ignore the added task lines
and cannot preserve full task evidence when re-exporting; use an updated build.

`scripts/probe-task-backup.mjs` runs the actual React backup hook and real
Chromium IndexedDB in an isolated profile. It covers both export modes, full
restore, exact media/context preservation across chunks, three tasks sharing a
conversation, hidden and unfinished records, bookmarks/profile, repeated merges,
late writes, duplicate claims, malformed/truncated input, missing sources,
forced staging/commit failure, cleanup and zero completion announcements.
Codec and reset tests separately cover wrong hashes, versions, chunk order,
identity collisions and in-flight credential/receipt races. Evidence is under
`.quest-evidence/task-backup`. The checkpoint passes 164 targeted and 65 prompt
tests, TypeScript, full source lint, ownership guards, production build and both
backup/recovery browser probes. No headset, provider or native command is used.

## Conversational task control (PC verification, 2026-09-26)

Typed chat and captured Live turns can propose the existing agent tool for stop,
revision, continuation or an answer to a task's clarification. The chat model
still emits only the ordinary handoff. The existing suggestion verifier receives
the exact original request plus up to four recent host-captured task labels; it
may select a closed `task: { action, taskId }` directive on that same tool. It may
not rewrite the request, invent targets, treat lesson exercises as controls, or
convert an unavailable task into fresh work. Ambiguous references must elicit
clarification. These are model instructions and bounded runtime validation, not
proof that a real provider always classifies intent correctly.

The catalogue is captured before the text request or Live connection. It uses
compact database summaries and excludes hidden, imported, source-deleted,
foreign-conversation/account/room and control-only records. Database v11
backfills only small scope metadata, one journal at a time while preserving
hidden status. Scope metadata stays outside chat/model history. This does not
add a second provider account or autonomous tool path.

A control request gets its own durable claim before it can cancel another task.
Stop/revise can coexist only with their specifically captured target. They stop
that runner and await its cleanup/acknowledgement before proceeding. A delayed
stop never selects a replacement active task. Stop invokes no room planner or
room command; ordinary shared narration reports whether the target was running,
its final known state and retained effects. It does not undo edits or stop rule,
physics or object-animation playback. If cancelling an unacknowledged native
request rotates the room handshake, the follow-up stops too and asks for a fresh
request after inspecting/reconnecting the room.

Revision/continuation is a new bounded task using the exact new input, prior user
requests, the immediate prior operations/receipts, and a fresh room observation.
It never replays saved command batches. Prior Live audio/frames are not recopied
into later requests; each new Live handoff uses only its own approved sent media.
The normal tutor context and scene help interpret a clarification answer. The follow-up
request lineage is bounded to 64,000 characters; exceeding that limit asks for a
self-contained request without silently dropping constraints. Stop does not need
to forward that lineage and is not blocked by its size. Native action
limits are three action batches of up to eight commands plus six standalone read-only discovery/inspection batches per new user request; reaching either limit stops planning.

Earlier unconfirmed actions propagate uncertainty. Follow-ups may inspect, but
further mutations are refused until the user reviews the room and makes a fresh
specific request. The final reply and task details expose this limitation. A failed final narration retains the recorded stop outcome in chat and details. No
automatic continuation, cross-room resumption or provider-generated target
identity is accepted. Stop/revision latency still includes the normal tutor and
verifier round trip; the existing explicit Stop control remains available.

Unit tests cover text/Live routing, schema and target restrictions, original-input
preservation, stop/revision races, waiting for receipts, scope loss, failed durable
claims, duplicate requests, source/room/import filtering and uncertain actions.
The real-browser steering probe exercises the actual task runner, tool dispatcher,
IndexedDB v10-to-v11 migration, compact-scope/hidden-status preservation and task
details UI. Its simulated room creates one robot, stops its task and paints the
same robot blue on a new revision, with no second creation. Backup, recovery and existing handoff UI probes still pass. The checkpoint has
281 targeted tests (including focused follow-up checks) and 65 prompt tests,
plus TypeScript, lint, ownership guards and the production build. Evidence: `.quest-evidence/task-steering`. Real multilingual
intent, ambiguity, cancellation latency, provider billing and Quest behavior
remain acceptance gates; no APK or headset update is included.

## Evidence and remaining release work

At the original text handoff checkpoint, 229 targeted tests and 65 prompt tests passed.
The original-media checkpoint passes 270 targeted tests and 65 prompt tests. TypeScript, full
source lint, Core/prompt ownership guards and the production web build pass. The
audible-result checkpoint passes 354 targeted tests plus 65 prompt tests, with
evidence under `.quest-evidence/agent-task-speech`.
Commit/file hashes are recorded under `.quest-evidence/live-agent-handoff` and
`.quest-evidence/live-input-handoff`. The browser probe also preserves an existing v7 history through the
v8 upgrade, checks atomic competing claims and rejects writes after source deletion.
The public chat entry point now exports the shared attachment renderers used by
book surfaces; existing book rendering tests still pass.

- Core tests exercise the original context/media at the provider boundary,
  journal-before-dispatch ordering, duplicate claims, Stop/acknowledgement races,
  stale sessions/access, failed durable writes and failed narration.
- Browser composition tests exercise BYOK/managed routing with simulated provider
  responses, preserve source provenance after later user messages, and ensure
  full room snapshots never enter normal chat history.
- `node scripts/probe-room-handoff.mjs` checks the actual task UI and IndexedDB in
  an isolated browser: input while working, receipt display, reload without replay,
  source-history pruning and Stop with uncertain outcome. Captures and receipt
  are in `.quest-evidence/agent-handoff`. Native/provider ports are simulated;
  this is not headset or real-provider acceptance.
- Cancellation tests cover pending credential lookup, BYOK/managed transport
  signals, no-output and mid-stream Stop, retry backoff, late provider failures,
  same-chunk callbacks, completed-request cleanup and browser task activity.
  Stopping during final narration retains acknowledged room effects. Provider
  responses are simulated; no server-side cancellation or billing claim is made.
- Ordinary prompt baselines remain unchanged. The additional capability and
  verification prompts are scoped to eligible connected-room turns.

Remaining work:

1. Verify original-input comprehension, explicit requests versus language
   exercises/background speech, actual media billing and Quest memory/latency
   with the real provider and headset. Do not enable the optional direct Live
   tool protocol.
2. Validate conversational Stop, clarification answers, revision and continuation
   with the real provider and headset; current PC coverage uses simulated intent
   classifications and native effects. Validate database v11, full task backups
   and durable result reconciliation on Quest.
3. Validate the managed admission-stop / accepted-stream-drain policy below with
   the actual provider, including usage reporting and lifecycle interruption.
   Do not promise service-side cancellation or exact usage after a failed provider
   stream; that requires a provider-supported protocol and separate acceptance.
   Strengthen access-change fencing across asynchronous credential refresh.
4. Extend the shared capability catalogue to the remaining avatar/import/physics
   and animation-library actions; existing bounded room/rule coverage remains.
5. Run real-provider request-versus-exercise acceptance, actual web/native bridge
   and Quest speech/UI checks, alongside the broader v1 release requirements.
   No new APK, deployment, provider spend or headset access is implied here.

## One-off action execution (2026-09-27)

The book and original-app agent can now start, inspect and cancel a named native
action without saving a behaviour. One-off runs use the same program machine,
scheduler, ownership, loading and Stop/grab paths. They require observed target
and prop revisions, reject busy resources, and expose exact run phases rather
than claiming completion on acknowledgement. See [contract and evidence](QUEST_ONE_OFF_ACTIONS.md).
Earlier pending-transient-invocation notes describe prior checkpoints. Persistent
event programs, timers, channel blending and full release acceptance remain open.


## Event-program development checkpoint (2026-09-27)

Version-3 programs now retain typed state across event/timer waits in the existing
interpreter. Named signals, bounded indexed event dispatch, timer delays, causal
budgets and idle resource release share the user/agent/native execution path.
The optional book exposes event blocks, state, signals and per-behaviour Stop.
Saving does not enable a run; pause, edits and reload cancel without catch-up.
See [the current contract and boundaries](QUEST_EVENT_PROGRAMS.md). Earlier notes
marking all event waits/timers pending describe prior checkpoints. Durable state,
wall-clock scheduling, parallel branches, channel blending and full release
acceptance remain open; no headset install or backend deployment is included.


## Managed stream admission and delivery (2026-10-02)

The original backend remains the sole managed generation owner for chat and room
agents. Stop/network loss is observed before owned-file validation, between token
count requests and after the credit reservation transaction. Cancellation before
provider dispatch performs no generation; any completed reservation is released
with an explicit admission-cancellation reason. Listeners are removed on every
exit. A failed refund retains the existing expiry recovery path.

Once the provider request has been dispatched, a closed response or failed socket
write switches to usage-only draining. It does not replay generation or convert
completed work into a provider failure/refund. Final response write/end failures
also preserve completed settlement. The existing durable accounting record and
retry/recovery path remain authoritative. Operator admission limits are unchanged.

Reported usage fields are retained independently of the last output chunk. Later
reported totals replace earlier values rather than being added; absent fields
and trailing empty chunks do not erase prior usage. The final client response and
billing calculation receive the same retained metadata. These fields describe
request totals in the [Gemini usage reference](https://ai.google.dev/api/generate-content#UsageMetadata).
This does not invent missing usage or change the existing failed-provider refund
policy. An interrupted provider stream is not treated as complete usage evidence.

Verification: all 57 managed-provider emulator tests passed, with real Firestore
transactions and a simulated provider. Eleven new cases cover four admission
boundaries, a real local HTTP disconnect, connected/disconnected trailing usage,
provider-start disconnect and response write/end failures. The existing billing
and Live gateway emulator scripts also passed. All 25 Functions unit tests and
60 focused web cancellation/handoff tests in 3 files passed.
The first emulator attempt required selecting the installed Java 21 runtime;
Unity Android retains its separate Java 17 build configuration.

No provider request, production deployment, APK rebuild or headset operation was
performed for this backend increment. Real-provider accounting, Quest acceptance
and the remaining v1 gates remain open. Development evidence is retained privately
under `.quest-evidence/managed-stream-lifecycle`.
