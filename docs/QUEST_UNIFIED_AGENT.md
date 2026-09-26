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
  permits three batches; longer tasks need explicit task budgets, checkpoints and
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
Quest reply. The app-owned task independently observes/plans/executes (three
batches maximum) and prepares its own normal-format reply. It uses the same
browser client resolver for BYOK or managed access, retains original attachments,
history and tutor context, and publishes its result in the original conversation.
The original tutor context is task data; it cannot override the native command
schema. Detailed observations and receipts stay outside ordinary chat history.

The task journal lives in the existing app database (v8, `agentTasks`). It commits
an atomic initiation claim and each pending action before native dispatch, then
commits acknowledgements before the next model call. Duplicate initiation IDs
return existing records. An interrupted/unconfirmed operation is never replayed
automatically. Receipts survive narration failure. Deleting source conversation
messages prunes their task records; late saves cannot recreate a deleted task.
Bulk history replacement clears the task journal. Backups currently contain task
status references in chat, not the full journal; restored references cannot run.

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

This is client-side cancellation, not a guarantee that provider billing stops.
The managed backend currently drains an accepted provider stream after disconnect
to obtain final usage and settle the reservation accurately. An explicit managed
server cancellation/accounting policy remains release work. BYOK passes the abort
signal to the SDK, but already processed usage is not refunded.

## Evidence and remaining release work

At this checkpoint, 173 targeted tests plus 65 existing prompt tests pass.
TypeScript, full source lint, Core/prompt ownership guards and the production web
build pass. The browser probe also preserves an existing v7 history through the
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

1. Capture Live/observer input and context with equally reliable source identity,
   then feed its suggestion afterstep through this same dispatcher. Live does not
   have this new handoff yet. Do not enable the optional direct Live tool protocol.
2. Add conversation-driven task Stop, clarification, follow-up revision and
   bounded continuation; current explicit task Stop is a chat control. Support
   durable task-result reconciliation when changing/reloading conversations and
   include appropriate task data in export/import without automatic resumption.
3. Define explicit managed server cancellation with accurate partial-usage
   settlement; client transport abort currently retains the existing server drain
   policy. Strengthen access-change fencing across asynchronous credential refresh
   and test actual managed billing on interrupted runs.
4. Extend the shared capability catalogue to the remaining avatar/import/physics
   and animation-library actions; existing bounded room/rule coverage remains.
5. Run real-provider request-versus-exercise acceptance, actual web/native bridge
   and Quest speech/UI checks, alongside the broader v1 release requirements.
   No new APK, deployment, provider spend or headset access is implied here.
