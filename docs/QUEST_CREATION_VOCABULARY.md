# Typed object creation

Development checkpoint, 2026-09-28. The public catalog now exposes one
object.create action with primitive and recipe kinds, replacing two prototype
creation IDs. The later copy kind shares Duplicate, exact source revisions and
returned identities; see QUEST_OBJECT_COPY.md. The drawing kind now shares the
physical pencil geometry and save path, with typed local points, placement, scale,
colour and radius. Shared splice/radius edits cover the full native stroke capacity;
see QUEST_DRAWING_AUTHORING.md. The current total catalog count is
tracked in QUEST_V1_PLAN.md. Existing imported-model workflows remain available separately; this
change does not add model generation or bypass asset validation.

## Contract and behavior

A shape call uses the same placement, scale and color inputs as before, plus an
explicit kind:

    {"id":"object.create","version":1,"arguments":{
      "kind":"primitive","shape":"ball","name":"Practice ball",
      "x":0.3,"y":1.3,"z":0.65,"scale":1,"red":0.2,"green":0.6,"blue":0.9
    }}

The recipe kind accepts name, x/y/z, scale and the complete recipe object. Its
native example is the existing nineteen-part robot with two animation tracks,
created idle. Shape-specific fields are forbidden on recipes and recipe data is
forbidden on shapes. The kind selector stays literal; valid scalar parameters
can be expression-bound. Recipe arrays stay literal and editable. Native domain,
room capacity, aggregate geometry and storage checks still apply.

The current `object.create` action has five kinds: primitive, recipe, copy,
drawing and template. All use completion duration through the same pending edit
operation as placement, paint and deletion. The book, agent and programs wait for
native application and durable saving before receiving the exact new `objectId`.
In temporary play, completion confirms the live edit and local Undo; only Keep
makes it durable. Imported-model loading remains a separate bounded operation.

Waiting for earlier edits does not allocate a saved object or native geometry.
The shared queue rechecks capacity, workspace and any copy-source revision before
preparation. Once preparation starts, newer authored changes invalidate the
candidate. Each accepted creation makes one Undo edit. Cancelling before native
acceptance leaves no creation; after acceptance the object remains and its save
must drain, even when the caller stops. Interruption status identifies an already
accepted object without inventing a successful output. Inspect uncertain outcomes
before retrying. A returned result is detached from the caller and never changes.

Duplicate one-off receipt delivery returns the historical result without creating
again, including after Undo. Programs retain their native-result authority and
per-run creation limits. Existing synchronous RoomEditor callers retain their
completion contracts and reuse the same data builders. These changes do not
increase world capacity or establish a frame-time guarantee. See the current
verification checkpoint in [the v1 plan](QUEST_V1_PLAN.md).

## Physical creation buttons

The room tray's Block, Ball, Cylinder, Paint, Duplicate and Erase buttons now
observe the same ordered background edit operations as catalog calls. The tray
shows preparation/saving and accepts one manual edit at a time; it does not wait
for the save worker on Unity’s interaction thread. Another tap while that edit is pending reports the
wait. Changing paint still updates the preference for future strokes/creations,
without changing the captured colour of an already pending edit.

Shared admission also waits for startup memory recovery to finish before preparing
an edit. It allocates no candidate or geometry during that wait. A busy or
conflicting disk operation is not otherwise retried automatically.

Target identity, requested values and any copy-source revision stay attached to
the original request. Physical controls retain control priority while preparing
and confirming the edit. Paint and Erase replace only the exact target’s control
or program ownership; unrelated programs, movement, sounds and model previews
continue. A retained animation draft on that target must be saved or explicitly
discarded before editing. A higher-priority grab of the target, lost focus, disabled editor or
room retirement stops observation: unaccepted work is cancelled, while accepted
work still drains through shared storage ownership. Failure never retries a
creation automatically. Status identifies an accepted uncertain edit for
inspection.

A confirmed creation/copy selects its new object only if the user has not changed
selection in the meantime. Erasing an earlier selection cannot clear a newer
selection. Undo and Redo wait while this manual edit is pending, then retain the
same single-edit history. Physical object history interrupts only its affected
targets, protects unfinished target animation drafts, and refuses a history entry
that changes during takeover rather than silently undoing another edit. Legacy
whole-room history and explicit whole-room stop still interrupt the room. The
existing held/drawing guard and asynchronous history checkpoint remain separate
from the queued creation completion path. Temporary-room edits change only that live fork and its
Undo until Keep. Physical pencil modes and retained drawing/sculpt drafts keep
their separate session controls.

These changes do not replace synchronous legacy APIs or the grouped legacy wire
contract. Avatar publication, surface placement and remaining history paths need
their own completion audit. Desktop delayed-storage and physical-button tests
establish ordering and behavior. Native validation, baseline checks, snapshot
capture and scene reconciliation still include owner-thread work; headset latency
and comfort need measured acceptance.

## Physical item physics

The physics tray's Object mode, Mass and Collision shape buttons use the same
manual completion coordinator as creation controls and the same
`BeginConfigurePhysics` operation as the shared catalog action. The request
copies its target, revision and settings before waiting. Repeated pending taps
do not change that intent, and a changed target is refused rather than overwritten.
Changing selection never redirects the original request.

Only the affected target's control/program ownership can be replaced. Grips keep
their higher priority, and unfinished target animation drafts require saving or
explicit discard. Other programs keep moving. Settings preserve one Undo,
temporary-room boundaries and the current physics start/pause state.

The tray now displays editor and physics feedback as well as scan feedback.
An active edit's preparation/saving status takes precedence over unrelated room
notifications. A failed accepted save identifies the captured item by a bounded
human-readable name, retains the live settings as unconfirmed, and blocks unsafe
saving/replay through shared recovery.

The legacy immediate `SetItemPhysics` API retains its synchronous contract.
These buttons do not imply that surface placement, avatar publication or every
legacy caller has adopted asynchronous completion. Native button/text and
storage-failure tests are desktop evidence; headset readability, latency and
interaction still require acceptance.

## Imported model preview continuity

Physical previews on saved imported objects now reserve the same target channels
as other controls. A conflicting program cannot overwrite the preview. Painting,
erasing or explicitly taking over that target releases the preview; edits to
other objects leave it running. Stop, Next and Loop affect the selected preview.
Finite completion, disable and disposal release its ownership. Saved Maestro
motion previews retain their existing animation-workshop ownership; detached
import previews remain private until accepted. Whole-room stop/lifecycle actions
still stop all previews and programs.

## Shared authoring

The discriminated schema supplies choice titles, descriptions, complete examples
and per-kind native feature requirements. The book's visual blocks and action
catalog use those choices. Switching kind keeps compatible fields and expression
wiring, removes incompatible fields, and preserves the existing result destination.
Even an invalid catalog draft retains its other valid fields when kind changes.

The physical tray uses the same schema title and examples. Its choice changes a
detached draft. Apply makes one revision-checked behaviour Undo edit without
creating anything. The full source and structure remain editable in the book.
The detailed recipe can also be authored through the existing app-owned agent.
These expert controls supplement the language-first flow.

Existing simple, prop and motion editors still use private adapters. Their field
paths and literal selectors now come from native-generated descriptors instead of
animation-specific web/native branches. Scheduler execution continues directly
through native modules; no numeric RuleStep enters execution. New public kinds
need no new enum entry or specialized editor. A new kind still requires actual
native implementation, validation, examples and meaningful tests.

## Prototype compatibility

The retired object.create.primitive and object.create.recipe IDs are no longer
public calls. Existing prototype source is retained as unavailable by per-program
compatibility isolation; it is never silently rewritten or executed. Old receipt
history uses the existing explicit recovery path if incompatible. No installed
room/chat/behaviour data has been reset, and no imported model files were moved.
This change precedes the stable v1 release contract.

## Evidence and remaining work

A shared native/web fixture validates all three primitive shapes, a complete
recipe and malformed/mixed-kind requests. Native tests cover exact created IDs,
real robot joint movement, save failure, capacity, duplicate receipts, unrelated
playback, persistence and Undo through the unified calls. A real physical-pointer
test changes kind, preserves result wiring, applies without creating, opens the
same book program, then creates and undoes the robot only after an explicit run.

Browser replay fixtures come from actual Unity outputs with local provenance.
A real Chrome probe replays the physical draft, selects both kinds, edits a name
and expression, and verifies the exact complete save request. It starts no action
and supplies no simulated acknowledgement for that save. The separate catalog
probe verifies the native recipe example with simulated fixture query/edit replies.
Native and book screenshots are inspected; these are PC checks, not headset
acceptance.

Spatial vocabulary, runtime/grouped commits, arbitration, richer world events,
collections/shared libraries, model import consolidation and generic version
negotiation remain open. Quest testing, provider journeys, final default assets
and Meta store acceptance are still release gates.

PC verification: 1,341 app tests across 157 files, 159 EditMode and 106 PlayMode
tests pass; three optional private-model/collection tests are skipped. TypeScript,
lint, catalog provenance, code/prompt guards and 25 Android bridge tests pass.
The ARM64 IL2CPP development APK builds and its v2 signature verifies. All 116
native runtime sources match the tested mirror and all 112 packaged web files
match a final production rebuild byte-for-byte. APK SHA256:
C4F00E349CBA84C854D98323E442455446E237504D6DB08CC1E9E5109BAADB68.
The APK has not been installed or accepted on Quest.


Later checkpoint: explicit shared temporary rooms and grouped snapshot commits
are now integrated in the catalog, book, native tray and receipt path. See
[temporary-room semantics](QUEST_TEMPORARY_ROOM.md). The earlier verification
counts and remaining-work list above describe the typed-creation checkpoint.
