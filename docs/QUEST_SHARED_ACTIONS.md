# Shared actions, observations and human/agent parity

Recommended implementation direction, 2026-09-26, following the user's comparison
with StateBeats, Scetch-War and StateWork. This is a design and coverage record;
it is not a claim that every proposed service or testing adapter exists.

## What the local examples actually demonstrate

Inspected local source, not only project descriptions:

| Project | Existing pattern | Relevant files |
|---|---|---|
| `D:/Projects/Games/StateBeats` | Pure tick transition, caller-bound SDK sessions, common service dispatcher, observations, explicit time and replay; CLI/MCP are adapters | `packages/core/src/engine.ts`, `packages/sdk/src/session.ts`, `packages/sdk/src/service.ts`, `packages/mcp/src/index.ts` |
| `D:/Projects/Games/Scetch-War` | Classic UI and MR grab/drop adapter submit to the same Session; interaction tests and browser drags verify the input path as well as final state | `src/client/app.js`, `src/mr/host.js`, `src/mr/interaction.js`, `tests/tabletop.test.js`, `tests/browser/tabletop.spec.js` |
| `D:/Projects/Work` | Shared validated service, actor-bound permissions, durable request receipts, revision conflicts, semantic perception adapters and exported schemas | `packages/sdk/src/service.ts`, `packages/sdk/src/perception.ts`, `packages/tools/src/mcp.ts`, `scripts/contracts.mjs` |

These are reusable architectural patterns, not evidence that all three already
have complete LLM control. For example, Scetch-War's optional Gemma commentary
path is documented as having no command channel. Nor are their local tests proof
of Maestro device compatibility. Reuse proven patterns without coupling unrelated
applications to a new shared framework prematurely.

## Intended contract

Book controls, 3D tools, conversational tools, behaviour rules and development
clients should consume the same capability-aware operations and observations.
A complete feature includes:

1. A stable action ID and typed, versioned arguments with one authoritative native
   or web handler; no button-only business logic or model-only implementation.
2. A readable observation: stable entity/part/step IDs, relevant saved data and
   live status, selection, supported actions and concrete unavailable reasons.
3. A receipt with request/operation identity, actual result, changed IDs, revisions,
   progress/errors and cancellation state. Accepted, running, completed, failed,
   cancelled and outcome-unknown are distinct. Durable edits and their receipts
   need atomic persistence before claiming restart-safe deduplication.
4. A manual control and discoverable conversational entry when appropriate, plus
   event/rule invocation for reusable runtime actions. Editor-only authoring and
   continuous tracking are not automatically callable every frame from a rule.
5. Validation, permission, conflict, persistence and end-to-end adapter tests.

Use an action catalogue as the shared specification for argument schemas, labels,
agent descriptions, required capabilities, reversibility and example cases. Derive
wire types/tool descriptions and coverage checks from it where practical. Native
validation remains authoritative. UI usability still needs designed controls; a
schema does not automatically create a good VR interface. Existing TS/C# validation
currently overlaps and needs shared conformance fixtures before claiming no drift.

Expose a small semantic client surface for discovery, observation, execution,
operation status/events and cancellation. These names describe proposed operations,
not currently callable APIs. MCP/CLI can wrap this client for development; the
in-app agent can call it directly. A transport is not an extra source of behaviour.

## One authority per domain

Keep language tutoring, chat and provider/access logic in the existing shared web
core and backend. Keep room objects, rules, physics and native avatar runtime in
Unity. The book is another view of the same web app. Do not reimplement room rules
in TypeScript or tutor policy in C# just to make every surface use one language.
Contracts cross that boundary; each domain has one implementation and owner.

Desktop Unity should run the same native services and content as Quest with a
replaceable device/input adapter. Phone/browser can edit supported saved data and
use the shared tutor; physical room tracking requires a connected native runtime.
Capabilities must report that difference instead of promising identical device
features. Cross-device synchronization is separate work, not implied by a shared
schema or common backend login.

High-rate head/hand/controller poses remain in the native input/runtime loop. The
agent selects bounded intentions such as starting a follow behaviour or playing
an animation; it does not generate every movement frame. A simulated test input
source feeds the same input router and interaction components as real devices.

## Collaboration and perception

Treat user and agent as collaborators on the same document. Selection and
references such as "this arm" resolve to explicit IDs and revisions. Agent edits
must use the observed revision; refresh and explain conflicts rather than silently
overwrite a newer human draft. Add edit ownership/locks where needed, and have
manual grab/Stop interrupt incompatible ongoing behaviour.

The object tree, behaviour blocks and timeline are views of canonical domain data.
They must retain stable identities across human and agent edits. The agent can
explain the selected step, change it through the shared operations and highlight
its target. Receipts show what really changed even if generating the final spoken
answer fails. Completed changes must not be inferred from fluent model text.

Return semantic scene information for reliable actions, and add images/rendered
views for appearance questions. Distinguish known room geometry from what a camera
currently sees. Simulated input and domain observations cannot establish visual
quality, tracking quality or comfort.

Use per-object/sequence conditions and bounded transactions for edits. Long actions
such as importing, walking or playback need operation handles, events and Stop;
"started" does not mean "finished". One Undo should restore a logical saved edit.
Stopping or restoring settings cannot undo every physical consequence of a thrown
ball. Cross-domain edits must state their actual atomicity/compensation boundary.

The host grants capabilities according to the current user's access. Permission
is not supplied by model JSON. Runtime tools cannot grant themselves developer
powers. Development-only reset, test clock and input injection belong to a separate
adapter/build surface; production agent actions use normal validators.

## Development-agent testing through the user's paths

Use three complementary levels of evidence:

- **Contract/domain scenarios:** repeatable fixtures run through the public action
  services. Compare resulting saved data, receipts and supported observations for
  manual-adapter and agent-adapter requests. Test restart/retry, stale plans,
  cancelled work, assets missing and unsupported schema versions. Do not count a
  mocked receipt as evidence of native execution.
- **Actual interaction and rendering:** launch the real Unity scene, provide
  simulated controller/head/hand input, perform ray selection, pinch/grip/release,
  page interaction and 3D button presses. Capture views and action/event traces.
  Verify that interaction reaches the same operations and outcomes; commanding an
  object directly cannot prove that its button or collider is reachable. Test the
  real Android book bridge separately from the isolated browser fixture.
- **Quest acceptance:** verify tracking, room scan alignment, passthrough, Android
  permissions/browser texture, text comfort, frame timing, memory and heat on the
  device. Simulators reduce repeated manual work but do not eliminate this gate.

Deterministic document edits/rules can have exact expected results. Unity rigid-body
simulation should use fixed-step recorded inputs and tolerance/invariant checks;
do not promise bit-identical trajectories across PC and Quest. Unity documents
fixed steps as important for reproducibility while noting PhysX determinism limits:
https://docs.unity.com/en-us/engine/6000.6/manual/physics-section/physics-overview/physics-optimization/cpu/manual-simulation
The installed project currently uses Unity 6000.3.24f1 and XRI 3.6.1; the linked
newer manual is rationale, not a claim of a package upgrade.

Keep a coverage matrix with action -> manual entry -> agent tool -> observation ->
contract case -> real interaction case -> hardware status. CI should reject missing
required mappings for new actions, and intentional unsupported cases need reasons.
Agent policy evaluations are separate from deterministic execution tests: a correct
executor does not establish that the model selects the right action or asks the
right clarification.

## Current Maestro coverage and next implementation slices

Checkpoint 7EC419D6 extends the room executor with shared rule operations and
book behaviour blocks. The native v4 rule document has stable step IDs; sequence,
trigger and button edits have revision checks and one Undo per batch. The existing
scheduler executes both agent/book requests and physical/event triggers, including
recipe animations. Runtime step IDs are visible. See QUEST_BEHAVIOUR_WORKSPACE.md.

Many physical parameter controls still call validated domain methods directly;
full operation-level parity is not established. Observations remain partial,
receipts are session-bound, imported-motion/prop book editing is incomplete and
there is no general developer MCP/action catalogue. Browser tests use simulated
native replies; Unity tests separately execute actual rule/event/button behaviour.

The next transport foundation is described in QUEST_LIVE_ROOM_ACTIONS.md: shared
room function declarations, pending-call ownership, cancellation and bounded
managed usage evidence are implemented. The gateway issuer switch is off by
default; Live controller/native execution and provider accounting acceptance remain
open. This does not add user-visible Live action parity yet.

The user's later direction is authoritative: the original Maestro app owns all
Gemini calls. Chat/suggestion aftersteps initiate the room task through the existing
tool coordinator; Live is an input/conversation interface. The room runner is now
separate from final tutor narration, with per-request bridge cancellation. See
QUEST_UNIFIED_AGENT.md. The direct Live function protocol is optional and stays off.
The text-chat dispatcher now starts the agent tool and the automatic pre-planner
has been removed. Original input snapshots, a durable task journal and nonblocking
header activity are implemented; Live capture and release acceptance remain open.

Incremental migration preserves existing tested controls:

1. Define/version the capability and action catalogue around the working room
   executor. Separate transport/session handling from domain execution. Add durable
   operation receipts and independent observation/event access, keeping migrations
   and existing user data recoverable. Extract pure validation where useful instead
   of rewriting the whole app at once.
2. Complete the initial RuleWorkshop wrapper and book blocks: route remaining
   physical parameter commands through the public operations, finish imported-motion
   and prop selection, add finer trigger editing and durable receipts. Keep runtime
   triggers and cancellation semantics common. Stable step IDs, bounded agent edits
   and initial manual blocks are implemented in 7EC419D6.
3. Extend coverage to animation library/import, avatar assignment/state profiles,
   controller bindings and room physics/scan. Connect chat and Live suggestion
   aftersteps to the same app-owned room task orchestration. Load only relevant tool groups/context per turn to
   limit planning cost as the catalogue grows.
4. Add the developer client and Unity input/render harness, then a vertical scenario:
   "make a small robot; wave when Maestro speaks; let this controller button play
   it; stop; edit the wave manually; restart and reload." Assert shared definitions,
   revisions, real playback and user interruption across the participating paths.
5. Expand the coverage matrix and device evidence before declaring v1 parity.

The benefit is one behaviour change to maintain, visible and reversible co-editing,
accessible language control, reusable user-created rules and repeatable regression
scenarios. It does not mean zero UI work per feature or zero hardware testing.

## Shared movement and physics increment

See QUEST_ROOM_CONTROLS.md for the implemented four-operation extension. Physical
avatar controls and agent intentions now use the same movement owner and Stop;
physics settings and avatar preferences use shared validators and the existing
room journal. Capability gates prevent dispatch to older runtimes. Native state
serialization is checked against the browser parser using actual Unity captures,
including absent values and live movement. This is partial domain coverage, not
completion of import/library/controller/scan parity or durable native receipts.

The subsequent user question about code and visual blocks is recorded in
QUEST_BEHAVIOUR_PROGRAMS.md. It proposes a common typed program with functions,
branches and event entry points, extending the current bounded sequences. It is
not implemented by the movement/physics action extension.


## Programmable behavior milestone (2026-09-26)

Version-1 behavior programs now compose the existing native rule actions with typed
variables, function calls/returns, conditions, switch and bounded loops. They use
one revision-checked rule command from chat-agent authoring and the optional book
editor. The book displays nested editable blocks/JSON, the native current node,
locals and recent completion/cancellation/failure outcomes. State/object events,
run-now and physical buttons share that execution path. Programs are capability
gated; v5 rule storage preserves older linear rules and future-version files.

See [the program contract and remaining roadmap](QUEST_BEHAVIOUR_PROGRAMS.md).
This milestone does not provide timers, arbitrary JavaScript/C#, Blockly round-trip
editing, cross-program libraries or durable native execution receipts. Browser
checks replay real Unity observations but simulated edits; headset acceptance and
performance testing are still required for release.

## Compatible motion discovery (2026-09-27)

`motions.v1` adds a read-only paged query to the existing command envelope. The
book and agent use `MotionLibrary.Search`; downloaded results provide stable IDs
for the existing behavior executor. Native observations invalidate compatibility
when the target model changes. See QUEST_MOTION_DISCOVERY.md. This closes discovery
for saved motions; the following increment adds activity-profile assignment.
Import and full action-catalogue parity remain unfinished.

## Shared walking preference (2026-09-27)

`avatarWalk.v1` saves a library gait or restores the included gait through the same
validation as manual selection. It joins the existing atomic room-edit journal and
per-object revision checks. The `walk` observation adds assignment and availability
read-back, plus native loading/fallback status. See QUEST_AVATAR_MOVEMENT.md; this
is a preference edit, not a movement-start command or headset gait acceptance.

## Shared automatic animation preferences (2026-09-27)

`avatarActivities.v1` connects chat to the same per-avatar profile domain used by
the book. Assign/remove/clear and Undo/Redo share validation, atomic persistence,
reference retention and a profile revision. Multi-role edits produce one history
entry. Book drafts survive conflicting agent edits; stale writes fail with fresh
read-back. The agent and book observe one projection, and existing playback
ownership stays authoritative. See QUEST_AVATAR_ACTIVITIES.md for the wire contract
and acceptance boundaries. General import, model replacement, controller bindings,
scan workflows and durable native execution receipts remain outside this increment.


## Approved catalog and event-runtime direction (2026-09-27)

The owner authorizes simplifying development-only formats and resetting their
prototype saves if needed. The accepted design, review qualifications, tradeoffs
and staged acceptance are in [QUEST_CAPABILITY_ARCHITECTURE.md](QUEST_CAPABILITY_ARCHITECTURE.md).
The first native vocabulary registry now generates web action/event labels, fact
types, scalar binding types and the prompt fact guide, with drift checks. Typed
capability invocation, full room-command coverage, timers/state machines and
per-channel ownership remain follow-up work. No development data was reset here.
