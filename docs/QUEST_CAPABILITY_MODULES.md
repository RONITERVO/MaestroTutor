# Native capability modules

Development checkpoint, 2026-09-28. This changes native dispatch and adds a real
named-only rotation capability; it is not completion of all v1 architecture work.

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

Ten actions use independent modules: wait, primitive/recipe creation, position,
rotation, size, tint, deletion, physics impulse and physics stop. The nine existing
animation/spatial actions use an explicit adapter around the tested animation
lifecycle. That adapter still contains kind-based branches and is remaining
consolidation work; moving it does not by itself make animation internals modular.

The existing tray's numeric controls are isolated in LegacyCapabilityAdapters.
They derive temporary RuleStep views from canonical saved programs. Modules do
not need a numeric kind or new RuleStep fields. Programs without such an adapter
remain editable through the book's generated capability forms and source view;
older simple controls cannot flatten or silently discard their arguments.

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

Animation modules, schema-generated physical tray controls, vocabulary review,
generic capability/version negotiation, runtime effects versus explicit grouped
saved edits, priority arbitration, richer world subscriptions, collection types
and shared program libraries remain. Existing saved-edit actions were not silently
changed to transient effects. Quest frame/save timing, hardware interaction,
real-provider journeys and store acceptance remain separate gates. No headset
installation, user-data reset or service deployment is included here.

PC verification: 1,303 app tests across 155 files, 151 EditMode and 98 PlayMode
tests, with three optional private-model checks skipped. TypeScript, lint, shared
code/prompt guards, 25 Android bridge tests and the ARM64 IL2CPP development build
pass. Chrome screenshots and exact-save evidence were checked. Headset and
real-provider acceptance remain unverified.
