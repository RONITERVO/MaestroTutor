# Shared behaviour blocks and native rule actions

Implementation checkpoint, 2026-09-26. PC verified; not installed on Quest.

## User-facing behaviour

Workshop now has Objects and Behaviours views on the full two-page book. The
behaviour view lists sequences and their triggers on the left and editable action
blocks on the right. Users can name a sequence, add/remove/reorder steps, choose
supported actions/targets/gestures, change duration/loop/repeat/interruption policy,
add or disable state/room-event triggers and create physical room/controller buttons.
Play highlights the actual running/preparing step; Stop stops the native scheduler.
Chat stays mounted, and switching workspace views retains drafts. Stale drafts
cannot overwrite a newer rule revision and require an explicit reload.

Actions currently include recorded movement, Maestro gestures, Wait, throw a
recording, look/follow, existing imported clips/library motions and recipe animation.
The book preserves saved imported-motion and prop fields; choosing new imported
motions and fitting/editing props still uses the existing tools. New block creation
for imported/library actions is disabled until those choices have a complete book
picker. Trigger cooldown can be set through the agent contract, while the initial
book Add trigger uses one second. This is a bounded sequential block editor with
repeat/interruption controls, not a general Scratch language or free-code executor.

## Shared native implementation

`RuleWorkshop.Execute` is the common ID-based entry point used by the room agent
and book client. Physical creation/deletion of sequences, adding bindings and
adding buttons also use it. Other physical parameter edits use the same existing
validated commit/journal underneath; not every control has an identical public
command yet. The existing `RoomRules`/`RuleScheduler` remains the sole runtime.

A rule request is standalone relative to room-object commands. An edit accepts up
to 16 save/delete/bind/unbind/button/unbutton changes, validates the entire candidate
and commits one behaviour Undo entry. New IDs are assigned natively; aliases can
connect a new sequence, event and button in one edit. Replacing an existing sequence
requires its known step IDs; an empty step ID adds a step. Whole-rule revision checks
reject stale changes/play requests. Stop and inspection do not require an old draft
revision to remain valid. Rule Undo/Redo and room-object Undo/Redo remain separate;
there is no cross-domain atomic transaction claim.

The native observation includes up to 32 sequence summaries, one inspected sequence,
eight bindings per page, the selected sequence's buttons, active run/sequence/step
IDs, loading flags, queue count, rule revision and read-only/history flags. Recipe
inspection is omitted while rules are focused to stay within the 64 KiB observation
budget. Requests remain bounded to 32 KiB; agent parsing uses a lower 28,000-character
budget. Starting/loading a rule does not establish that its later motion completed.
Receipts remain session-bound; restart-safe durable operation receipts are pending.

The same managed/BYOK text-turn planner now has the bounded `rules` operation and
its shared schema. No extra provider or subscription was added. Schema enum order
is checked against native declarations; the native-exported observation is parsed
by a web test. Actual authenticated provider behaviour still needs acceptance.
Recorded speech entering the text-turn path shares this feature; separate Live
voice integration and entirely hands-free startup remain open.

## Persistence and runtime

Rule document v4 adds stable step identities and the recipe-animation action.
Versions 1–3 migrate with repeatable IDs derived from sequence ID and step position,
so repeated reads before the first save retain the same identities. Saving writes
v4 files and retains older originals. Tests cover legacy motion/prop/button fields,
backup recovery and rejecting a future version in the current primary file.
Read-only storage now also blocks in-memory rule edits and Undo/Redo.

Recipe animation is a native scheduler action. It can play an assembly whose saved
playing flag is off, apply a runtime loop override, and stop on trigger exit, manual
Stop, interruption or completion. It does not rewrite the recipe's saved playback
flags. The existing target ownership and physical-button input path remain in use.
Recipe limbs still share one approximate rest-bounds collider; this adds no articulated
limb physics or replacement for the humanoid tutor rig.

## Verification

Development APK `7EC419D6`, 135,465,547 bytes, zero build errors and two warnings.
SHA-256: `7EC419D60CBDDDB06A4BBBA522CBCC8780F4B7FF964BC7317463A2377CBF0297`.
Signature v2, ARM64 and required manifest checks pass. No device query, installation
or launch occurred. Installed checkpoint remains 08F340EF while the headset is on hold.

- 64 Unity EditMode + 69 PlayMode + 25 native Android checks pass (158 required).
  Three optional private-model checks remain skipped. Native lint passes.
- 34 room/Quest web checks pass across the suite and the final native-view fixture
  check; the shared prompt suite passes 65. TypeScript, core/prompt boundaries and
  the production web build pass.
- Native tests verify atomic batch Undo/Redo, stable IDs across reorder/save/reload,
  stale edit rejection, unchanged state after invalid data, and an agent-created
  recipe rule invoked by both tutor-state transitions and actual ray/button input.
  They observe the native robot's arm movement and stop/completion behaviour.
- A 1024 x 768 browser run checks rename/apply/Undo, adding and reordering blocks,
  state triggers, mounted buttons, active-step highlighting/Stop and return to chat.
  The real React surfaces use a Unity-exported RuleView with explicitly simulated
  replies. The reviewed page capture has readable controls without overlap. This
  does not prove Android WebView end-to-end or Quest tracking/readability.

Evidence is retained under `.quest-evidence/behaviours/verified-7EC419D6` with source
and packaged-file hashes, native observation, browser capture/checks and build/test
reports. See QUEST_SHARED_ACTIONS.md for broader parity work still required.

## Next acceptance and remaining scope

After explicit headset return, preserve saved data before upgrading. Confirm v3
rules, triggers, controller-button offsets and prop settings survive. Open Behaviours,
edit/reorder/Undo a sequence, trigger it from speech-state changes and a mounted
button, then interrupt it by grabbing its target or Stop. Create a non-autoplay
recipe robot and trigger its wave. Confirm simultaneous physical edits retain and
reject stale book drafts. Verify reload, input reachability, frame time and memory.

Complete motion/prop pickers, fine trigger editing, reusable/nested bounded actions,
per-sequence conflict handling, durable receipts and model policy evaluations before
claiming full human/agent parity. Imports, scan/physics, controller bindings and
avatar assignments still need broader agent adapters. A general developer client
and recorded input/render test harness remain planned. Store identity, signing,
production access, performance and all other v1 release gates remain open.
