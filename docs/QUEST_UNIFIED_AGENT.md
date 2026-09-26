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

## Current state and remaining migration

The existing code already centralizes Gemini access in the original app. However,
`src/api/gemini/journeys.ts` still runs the legacy room planner before each Quest
text reply. This is transitional and is not the desired tool-triggered flow.

This checkpoint extracts `runRoomActionTask` from that wrapper. It accepts the
original app's provider source and conversation controls, returns native receipts
independently of a tutor reply and publishes each acknowledgement before another
model call. The compatibility wrapper calls this same implementation. It adds no
Live provider connection or second task engine. The bridge now accepts a request-
owned AbortSignal: aborting an old/completed task cannot cancel a later manual edit.

Remaining work before calling this flow implemented:

1. Add the capability-gated room tool to the existing tutor/suggestion schema and
   normalizer, including provenance tied to the current real user request.
2. Register the room task in `assistantTools`; remove the pre-planner from ordinary
   Quest tutoring once the replacement is complete. Keep chat/Live aftersteps on
   the same dispatcher, with one task per initiation ID.
3. Add durable task/operation storage, visible chat progress/results, Stop and
   recovery; bind account, conversation and native-session lifetime correctly.
   Keep task receipts out of ordinary tutor history and use a distinct activity
   token in the existing flag system that does not block chat input.
4. Validate explicit requests versus quoted exercises, duplicate/replayed tool
   blocks, stale suggestions, partial results, manual conflicts and interruption.
   Exercise both BYOK and managed clients through the same task port.
5. Test the actual web/native bridge and Quest speech flow before release. This
   direction removes the need to activate the direct Live room-function transport;
   ordinary Live billing and speech tests still apply.

The client-side draft of a separate Live room dispatcher was removed after this
direction was clarified. No direct Live tool activation, backend deployment or
Quest access occurred during the migration preparation.
