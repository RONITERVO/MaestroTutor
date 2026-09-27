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

1. **Vocabulary foundation (this checkpoint):** native registrations for the nine
   existing program actions, seven events and three facts; duration, current whole-target
   ownership and prerequisite metadata; typed scalar bindings;
   generated web labels/fact types/prompt fact guide; native export equality and
   CI source-drift checks and an EditMode comparison with the committed manifest. Native fact reads and event classification use the
   registrations. Old numeric positions and RuleStep binding names are isolated under
   adapters.ruleStep, separate from the string capability identities. This is not
   the finished catalog of all room commands or generated argument validation.
2. Replace the development dual sequence/program representation with a canonical
   program format. Reset development documents if needed; document the reset and
   preserve source models/downloads and recoverable last-good writes. Remove old
   migrations only after their consumers and tests have moved.
3. Add typed capability invocation, availability and paged search, with one
   vertical path exercised by book, agent and native program. Preserve domain
   validators; generate structural schemas rather than duplicating business rules.
4. Implement native event subscriptions, monotonic timers, named events and state
   machines with explicit lifecycle policy. Test storms, recursion, pause, reload,
   deleted targets, lost tracking and Stop before claiming continuous behaviours.
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
