# Native capability modules

Development checkpoint, 2026-09-28. All nineteen catalog actions now use native
modules, including animation and movement. This completes the existing action
handler split, not the broader v1 architecture or release acceptance.

## Shared execution path

The interpreter now yields a validated CapabilityCall: a definition, detached
named arguments and a stable program node ID. Resource authorization and channel
claims come from that call. The scheduler uses the definition's duration and
operation lifecycle; it no longer decides execution from RuleActionKind or
RuleStep fields. One-off calls, saved programs, event-triggered programs and
mounted behaviour buttons continue through the same scheduler and receipt path.

A CapabilityModule owns metadata, input/output schemas, domain validation,
readiness, channel claims and operation creation. Each CapabilityOperation owns
preparation state, duration, frame updates, completion, cancellation and result.
Partial starts remain owned until cancellation; completion/failure release their
operation once. Input JSON is copied before dispatch so a caller cannot change
an already validated target or effect. New modules are trusted native C# bundled
with the app, never downloaded code or agent-written executable C#.

All nineteen actions use independent registrations. The ten wait/object/physics
modules are joined by recording, recorded throw, full-body gesture, upper-body
gesture, look, follow, embedded clip, library motion and recipe animation modules.
There is no shared animation dispatcher or RuleStep conversion on the execution
path. Related operations share target/prop ownership or spatial lifetime helpers;
those helpers do not select handlers by action kind.

Each animation module owns its schema, readiness and operation. Recordings own
their PlayableGraph; throwing samples the precise final frame before handing the
item back to physics. Embedded playback owns its selected model. Library playback
owns an asynchronous lease until successful transfer to the avatar, checks the
exact model and rig again after preparation, and disposes late arrivals after
Stop. Prop attachment uses immutable domain data instead of RuleStep. Upper-body
and spatial operations release only their own channel, preserving another
owner's gait, arm layer and room placement. Full-body cleanup is idempotent so
completion followed by host cleanup cannot undo a throw.

This extraction preserves all nineteen action contracts, versions, channel
claims and prerequisites, as well as events, facts and legacy editor mappings.
It does not silently rename saved motion choices or change saved-edit semantics.

The main physical tray now edits named invocation fields from each module's
schema, including actions without a numeric adapter. Specialized prop fitting,
motion assignment and older simple controls retain explicit
LegacyCapabilityAdapters views. Modules need no numeric kind or RuleStep fields.
Detailed authoring uses the book's generated forms and source view; quick edits
preserve control flow, expressions and results. See [native quick edits](QUEST_NATIVE_QUICK_EDITS.md).

## Demonstrated extension

object.rotation.set was added with its own module, an explicit registration and
one method in the existing room editor. No enum value, generic dispatcher branch,
web validator branch, prompt entry or custom form was needed. It takes target,
pitch, yaw and roll in degrees (each -180 to 180), using Unity Euler orientation
in room coordinates. It preserves current position and scale, clears motion and
then leaves normal gravity running. It saves before success and adds one room
Undo edit; Undo restores the prior journal pose, not an unsaved physics frame.

The capability uses the same target revision, readiness, ownership and durable
start identity as other edits. Duplicate completion receipts return the old
outcome without rotating an object again, including after Undo. Saving a program
never starts it. Unknown capabilities/versions fail validation on an older native
app rather than being substituted. Users discover available definitions through
the native catalog. Existing prototype feature gates remain for prior operations;
broader version negotiation is still part of consolidation before release.

## Catalog generation and verification

CapabilityModules explicitly lists registrations for IL2CPP; there is no runtime
assembly scan. All C# files in Runtime/Capabilities are included automatically in
the generated manifest's normalized source hashes. CI enumerates the same folder
and rejects new, missing or changed files until the manifest is regenerated.
Unity still performs native export equality; web CI does not claim to compile C#.
Native editor adapters remain covered separately by their source/identity checks.

The existing native regression suite passed after the dispatch change. New tests
exercise a module with no enum through preparation, results, partial-start failure,
completion failure and cancellation. Integration tests cover real rotation,
physics velocities/gravity, saved state, Undo/Redo, duplicate receipts, unrelated
motion, program execution and stale/held/conflicting/unsavable rejection.

program-rotation.json is shared by the native execution test and web visual-editor
test. scripts/probe-capability-modules.mjs uses real Chrome to create that exact
program through generated fields and save it without starting it. Its native
acknowledgements are simulated; actual object motion is verified in Unity.

## Remaining release work

Specialized physical-editor adapters, vocabulary review,
generic capability/version negotiation, runtime effects versus explicit grouped
saved edits, priority arbitration, richer world subscriptions, collection types
and shared program libraries remain. Existing saved-edit actions were not silently
changed to transient effects. Quest frame/save timing, hardware interaction,
real-provider journeys and store acceptance remain separate gates. No headset
installation, user-data reset or service deployment is included here.

PC verification: 1,303 app tests across 155 files, 151 EditMode and 104 PlayMode
tests, with three optional private-model checks skipped. TypeScript, lint, shared
code/prompt guards, catalog provenance, 25 Android bridge tests and the ARM64
IL2CPP development build pass. The APK's v2 signature was verified; all 112 native
runtime sources match the tested mirror and all 112 packaged web files match the
production build. All nineteen action contracts and events/facts/adapters compare
unchanged against the prior manifest, apart from source provenance.

The expanded regressions exercise late-load cancellation, successful avatar lease
transfer and completion, exact-model replacement during preparation, and repeated
cleanup after physical release. Existing walking/upper-body, recording/prop,
recipe and real-input tests also pass. Prior Chrome screenshots and exact-save
evidence cover the unchanged book UI; this extraction changes native execution.
Headset and real-provider acceptance remain unverified.
