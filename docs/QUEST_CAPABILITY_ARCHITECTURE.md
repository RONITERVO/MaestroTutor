# Shared capability catalog and event-driven programs

Accepted direction, 2026-09-27. The owner confirms that they alone use the current
PR and its installed development builds. They authorize resetting development
saves/reinstalling when this removes unnecessary migration work. No reset has
been performed by this checkpoint. Imported source files and animation collections
are not disposable application saves.

## Assessment of the review

Claude's [architecture comment](https://github.com/RONITERVO/MaestroTutor/pull/248#issuecomment-5855698004)
correctly identifies the separate action enum, three fact names, room-command
vocabulary, whole-target ownership and runtime limits. This is useful direction,
not a complete runtime specification or evidence of full codebase review.

The recommendation fits the existing shared-action design. Keep these corrections:

- The scheduler already supports repeating runs and resets the instruction budget
  for each repetition. A total per-run cap does not prohibit every continuous
  behaviour. We still need efficient subscriptions and explicit lifecycle rules.
- Stable motion IDs remain essential for saved references, Undo and reproducible
  results. Use tags plus rig compatibility to discover motions; explicitly save
  either a chosen ID or a dynamic selector with documented selection/fallback rules.
- A storage reset is permitted, but not a prerequisite for defining a catalog.
  Remove development migration layers when a replacement format is ready, not by
  disabling crash recovery or treating released web backups as disposable.
- A catalog entry does not make every device, rig or target capable of running it.
  Availability and failure reasons must come from the connected runtime.

## Contract to build toward

Keep native room/animation/physics execution in Unity and tutor/provider ownership
in the existing Maestro app. User controls, saved programs and the in-app agent
call the same validated native handlers. Developer automation uses those handlers
and the actual input adapters; it does not prove interaction parity merely by
setting object transforms directly.

A capability registration needs a stable ID and schema version; typed inputs and
outputs; required capabilities; effect/ownership channels; cancellation and
completion semantics; permission and revision requirements; and a human label,
description and examples. Export these definitions for web validation, discovery,
blocks and agent context. Never expose arbitrary reflection, filesystem, network,
C# or provider execution merely because a program can name a function.

The canonical program is a typed syntax tree with stable node identities. Blocks
and editable code are two views of that same tree. Do not promise arbitrary
JavaScript-to-blocks round trips. Structured revisions, clear conflicts, previews,
Undo and execution traces make user/agent co-editing practical.

Events use a bounded native queue and typed payloads. Programs subscribe to
state changes, object interaction, named events, buttons and timers. An agent
can explicitly run a program now; saving it does not run it or add a button.
Condition edges should wake only relevant subscribers. State machines need
explicit enter/exit/transition rules, bounded work per tick and event-loop limits.

Timers must distinguish monotonic session intervals from wall-clock appointments.
Specify pause/reload policy, one-shot versus recurring behaviour, missed ticks,
clock changes and cancellation. Do not replay a backlog of throws or movements
when the app resumes. Saved definitions and state may persist; live physics,
pending loads and unconfirmed side effects must not be replayed automatically.

Layered avatar ownership should support locomotion, upper-body gestures and look
intent where the rig and animation support independent channels. Full-body clips
may reserve several channels. Define priority, blend masks, root-motion ownership,
prop/contact constraints and manual override. A controller grab and Stop must
remain dependable even when several programs run. Incompatible rigs should report
limitations instead of pretending that arbitrary clips blend correctly.

The LLM authors and changes programs; the native scheduler runs them without
per-frame LLM calls. Progressive catalog discovery should return relevant pages
rather than putting thousands of definitions into every chat prompt. Stable
operation receipts and per-node traces must distinguish accepted, running,
completed, failed, cancelled and outcome-unknown.

## Tradeoffs

This introduces interpreter, scheduler, schema and editor work up front. Versioned
capabilities become a compatibility commitment after release. Event fan-out, queues,
asset loading, avatar blending and tracing consume a Quest frame/memory budget;
limits and stress tests are required. More expressive programs also need better
error messages and debugging. A schema can generate consistent forms and blocks,
but cannot automatically design a comfortable book or VR interaction.

The benefit is that a new native operation has one domain implementation and one
contract. It can then participate in many compositions without inventing a new
agent tool for each scenario. There will still be new handlers and device-specific
adapters for new capabilities. Shared semantics reduce parity work; they do not
eliminate UI, provider evaluation or headset testing.

## Implementation stages and acceptance

1. **Vocabulary foundation (complete):** native registrations for the nine
   existing program actions, seven events and three facts; duration, current whole-target
   ownership and prerequisite metadata; typed capability arguments and scalar bindings;
   generated web labels/fact types/prompt fact guide; native export equality and
   CI source-drift checks and an EditMode comparison with the committed manifest. Native fact reads and event classification use the
   registrations. Numeric positions for existing controls/handlers are isolated under
   adapters.ruleStep. Saved invocations use stable capability IDs and named arguments;
   scalar binding types come from each capability schema.
2. **Canonical program storage (complete):** every behaviour stores one program;
   simple controls derive and edit literal blocks, with one interpreter for all runs.
   `behaviours.v2.json` starts a fresh development behaviour collection, preserving
   old `rules.v1`–`rules.v5` and numeric-program `behaviours.v1` files without migration. Their triggers/buttons reset too.
   Models, motion downloads, room creations and released web saves are unchanged.
   Atomic writes, last-good recovery, newer-version protection, stable node IDs,
   Undo and motion-library assignment remain. New saves require the native
   `behaviourPrograms.v3` capability. No headset installation/reset has occurred yet.
3. **Typed invocation and discovery implemented:** version-2 programs call
   stable action IDs plus capability version and named arguments. Native definitions
   generate structural schemas used by web validation, editor labels and agent
   signatures. Runtime computed arguments go through that same schema and existing
   domain validators before the existing scheduler/handler. Book/agent/native paths
   exercise these calls. Paged search, exact-definition inspection and live availability
   checks now share the same read-only native path for agent and book. Schema resource
   annotations remove the generic web validator's dependency on the simple-action enum.
   One-off execution now uses the same scheduler without saving a behaviour, with
   target/prop revision checks, conservative busy rejection and exact run cancellation.
   See [discovery](QUEST_CAPABILITY_DISCOVERY.md) and [one-off actions](QUEST_ONE_OFF_ACTIONS.md).
4. **Event-program foundation implemented:** version-3 state, indexed event waits,
   monotonic delays, named signals and Forever blocks use the same interpreter.
   Tests cover bounded storms/causal cycles, pause/reload, Stop and busy targets.
   State machines compose state with if/switch; first-class state-machine editing,
   durable state, wall-clock scheduling and hardware acceptance remain.
   See [event semantics and boundaries](QUEST_EVENT_PROGRAMS.md).
5. Introduce per-channel actor intent ownership and compatible motion blending.
   Test simultaneous walk/look/gesture/prop behaviour, manual takeover and missing
   rigs. Verify actual Quest frame timing and comfort.
6. Complete shared capability coverage, durable operation receipts, program trace
   UI and developer input/render adapters. Ship only after the release acceptance
   scenarios and headset/store checks pass.

Regenerate the manifest using `unity/Tools/Verify-Quest.ps1` with the normal Editor
and BuildMirror arguments plus `-UpdateBehaviourCatalog`. Review the generated diff.
Ordinary verification rejects drift; `npm run verify:behaviour-catalog` checks
normalized source hashes in CI. Native export comparison is a local Unity check;
the web CI runner does not claim to compile Unity. TypeScript consumes the JSON
directly, avoiding an extra manually maintained TypeScript copy. Prerequisite
metadata is descriptive; native CanRun checks remain the execution authority.


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
