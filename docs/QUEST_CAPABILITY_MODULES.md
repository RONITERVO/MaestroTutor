# Native capability modules

All public catalog actions use native modules, including animation and movement.
The generated catalog is the current registry; typed
[animation](QUEST_ANIMATION_VOCABULARY.md) and
[creation](QUEST_CREATION_VOCABULARY.md) select their native implementations.
The handler split does not complete the broader v1 architecture or release acceptance.

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

The catalog includes wait/object/physics actions, animation.play, recorded throw,
look and follow. The animation module selects a typed source implementation;
recording, gesture, embedded, library and recipe operations retain independent
lifetimes. There is no numeric kind dispatcher or RuleStep conversion on the
execution path. Related operations share target/prop ownership or spatial
lifetime helpers.

Each animation module owns its schema, readiness and operation. Recordings own
their PlayableGraph; throwing samples the precise final frame before handing the
item back to physics. Embedded playback owns its selected model. Library playback
owns an asynchronous lease until successful transfer to the avatar, checks the
exact model and rig again after preparation, and disposes late arrivals after
Stop. Prop attachment uses immutable domain data instead of RuleStep. Upper-body
and spatial operations release only their own channel, preserving another
owner's gait, arm layer and room placement. Full-body cleanup is idempotent so
completion followed by host cleanup cannot undo a throw.

The original extraction preserved nineteen public contracts. The subsequent
pre-release consolidation replaces six of those names with typed animation.play
variants while retaining source semantics and exact motion choices. Events, facts
and saved-edit semantics remain unchanged; prototype compatibility is documented
in the animation contract above.

The main physical tray now edits named invocation fields from each module's
schema, including actions without a numeric adapter. Specialized prop fitting,
motion assignment and older simple controls retain explicit
LegacyCapabilityAdapters views. Modules need no numeric kind or RuleStep fields.
Detailed authoring uses the book's generated forms and source view; quick edits
preserve control flow, expressions and results. See [native quick edits](QUEST_NATIVE_QUICK_EDITS.md).

## Native entity acquisition

Modules declare `NativeEntities(arguments)` for objects that must exist natively
before their effect can be validated and started. `NativeTargetCapability` is the
shared base for a verb that needs only its typed `target`. Composite animation
variants forward the declaration to their source provider. Explicit holders,
projectiles, props and destination anchors belong here even if the action does
not own their channels. Never infer these dependencies from every string in JSON
or from ownership claims: catching, for example, observes a projectile before it
owns that projectile. Saved-only readers keep the default empty declaration.
Declare variant requirements separately: recording/posing starts acquire their
target, while finish/discard operate on the retained session and must not demand
a replacement native instance after loss or a failed save.

The scheduler owns acquisition independently of operation preparation. It keeps
the existing run ID and receipt, exposes “Loading required objects,” and defers
`CanRun` and `Start` until the declared native entities are ready. Instant effects
still execute synchronously after acquisition. Timed effects start their duration
after acquisition, with their normal operation-loading budget remaining separate.
Stop, pause and the 30-second acquisition deadline release the demand; a late load
cannot resume a terminated run. Failed preflight also releases it.

`RoomRules.CanAdmit` is a read-only entry check shared by catalog checks and agent
execution. For dormant entities it can admit preparation without claiming the
final effect will succeed. The catalog explains that distinction. The module's
full native preflight runs again after acquisition. Neither catalog inspection
nor facts perform file I/O, activate an area, or reserve action channels.

Current activation admits an entire saved area/physical-connection closure for
one owning run, with no other running or queued work. Resident calls preserve
normal concurrency. The journal remains authoritative and imported candidates
remain private until the whole group is ready. Failed loads retain saved data
and exact asset identities. Additions to this path need coverage for actual
native retirement, cancellation, failed import retry, and the real agent entry
point, not only a mock scheduler.

Automatic streaming is still disabled. Initial native dependency declarations
cover the verbs listed in the [release plan](QUEST_V1_PLAN.md#action-owned-native-acquisition--2026-10-10);
other authoring modules still need auditing before arbitrary dormant areas are
exposed. Observer demand, per-frame preparation budgets, ground/water/navigation
dependencies and regional physics admission remain separate required work.

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

Subsequent increments added explicit grouped temporary-room edits
([temporary rooms](QUEST_TEMPORARY_ROOM.md)), shared native priority arbitration
([ownership](QUEST_ROOM_OWNERSHIP.md)), contact/proximity/motion subscriptions
([event programs](QUEST_EVENT_PROGRAMS.md)), structured collections
([program data](QUEST_PROGRAM_DATA.md)) and pinned local module libraries
([program modules](QUEST_PROGRAM_MODULES.md)). Existing saved-edit actions were
not silently changed to transient effects.

Specialized physical-editor adapters, remaining vocabulary consolidation,
generic capability/version negotiation, broader world/actor coverage and
declarative resume policies remain. Quest frame/save timing, hardware interaction,
real-provider journeys and store acceptance remain separate gates. No headset
installation, user-data reset or service deployment is included here.

Prior dispatch/quick-edit checkpoint verification: 1,303 app tests across 155 files,
151 EditMode and 104 PlayMode
tests, with three optional private-model checks skipped. TypeScript, lint, shared
code/prompt guards, catalog provenance, 25 Android bridge tests and the ARM64
IL2CPP development build pass. The APK's v2 signature was verified; all 113 native
runtime sources match the tested mirror and all 112 packaged web files match the
production build. All nineteen action contracts and events/facts/adapters compare
unchanged against the prior manifest, apart from source provenance.

The expanded regressions exercise late-load cancellation, successful avatar lease
transfer and completion, exact-model replacement during preparation, and repeated
cleanup after physical release. Existing walking/upper-body, recording/prop,
recipe and real-input tests also pass. Prior Chrome screenshots and exact-save
evidence cover the unchanged book UI; this extraction changes native execution.
Headset and real-provider acceptance remain unverified.

A completed load retains its journal identity, revision and native generation through final effect admission, including any subsequent channel wait. A new edit or a replacement journal (even at the same revision) between load completion and the next scheduler frame fails the pending action without changing the newer state. Native integration regressions exercise a newer edit, a temporary-room journal boundary, and an editor disable/re-enable at an unchanged revision.
