# Expandable world authoring and the default play kit

Design decision, 2026-10-02, based on the owner's notebook and PR #248 review.
This is the implementation direction and acceptance plan, not a claim that the
listed new components or assets are already shipped. Existing contracts linked
below remain authoritative until an increment implements and verifies a change.

## Product decision

Users can build the game they play, primarily by asking Maestro. The agent,
optional visual/source editor and direct manipulation must edit the same saved
objects and programs. The original chat remains full-page on the book. Detailed
workspaces are optional artifacts; extra physical controls remain 3D objects.
A default object is an editable example users can copy, inspect and remix.

Use versioned construction recipes, native components and existing event programs.
Do not introduce a second general scripting engine, a separate agent scene, or a
hardcoded tool for each toy. Keep three related representations with clear owners:

- **Saved definitions:** entity IDs, recipe/asset references, components, assembly
  relationships, initial placement and pinned program/animation dependencies.
- **Live native state:** actual poses, velocities, grips, contacts, joint states,
  paint/fluid state and action ownership. Queries carry freshness and identity.
- **Evidence:** changes, receipts and bounded run/event traces. A successful command
  is distinct from a completed physical outcome and from a persisted snapshot.

Unity owns the simulated world. Generated meshes are caches of editable recipes;
imports retain their asset identity and editable component configuration. Blocks
and source are views of the existing canonical program. A shared world does not
require putting chat, assets, physics and all history in one enormous JSON file.

## What already exists

The current catalog contains 62 actions, 12 events and 59 facts. It already has
native capability modules, generated forms, shared human/agent execution,
revision checks, durable one-off receipts, contact/proximity/settling observations,
structured program values, parallel branches, pinned reusable program modules,
remembered values, priority ownership and explicit temporary-room snapshots.
See QUEST_CAPABILITY_MODULES, QUEST_EVENT_PROGRAMS, QUEST_PROGRAM_DATA,
QUEST_PROGRAM_MODULES, QUEST_PROGRAM_MEMORY, QUEST_ROOM_OWNERSHIP and
QUEST_TEMPORARY_ROOM for actual limits and lifecycle rules.

Physical drawing is a saved 3D stroke. Recipe construction currently accepts
box/sphere/cylinder parts and editable lathe profiles (at most 32 parts) with
parented rotations and bounded tracks. The lathe increment is described in
[recipe authoring](QUEST_RECIPE_AUTHORING.md#editable-revolved-profiles-2026-10-02).
Recipe parts are visual joints, not independent rigid bodies. The object can now
use an editable compound collision recipe; see
[collision authoring](QUEST_COLLISION_AUTHORING.md). Shared attachment and aimed throws exist; automatic IK catching,
hit reactions, physical hinges/springs, generic paintable surfaces, structural
blueprints and liquids/snow are not completed by those features. The current
16-creations-per-run budget cannot be described as arbitrary castle construction.

## Geometry implementation

Extend the recipe with typed, bounded geometry operations, retaining stable part
IDs. Implement primitives, rounded profiles, extrusion, revolution/lathe and
sweep first. These cover boards, cutlery, cups, chess pieces, bricks, handles and
simple robots. Materials/style, pivots, sockets and collision proxies are explicit
editable data. Organic characters continue to use GLB/VRM imports.

Compiled Unity C# generates vertex/index buffers. The agent supplies parameters
and composition, not executable C# or unbounded raw triangle arrays. Keep the
geometry backend behind a small interface so implementation libraries can change
without changing users' saved recipes. Validate first; estimate vertex/texture/
collider costs; build bounded mesh data off the main thread where supported;
apply Unity objects on the main thread. Keep the old accepted object if generation
fails or is cancelled. Content-hash caches include generator version and quality.

Evaluate Procedural Toolkit as an implementation helper: it provides mesh drafts,
primitives and geometry utilities, and documents Android testing. Adopt only the
needed subset/version after Unity 6/IL2CPP, memory, topology, UV, normals and
cancellation checks. This is a candidate, not an installed dependency or a Quest
performance certification. Direct Unity Mesh code remains suitable for small
well-defined operations.

CSG union/subtraction/intersection can be a later bounded recipe operation. Parabox
pb_CSG supplies those operations; its README is not evidence of robust handling
of every imported mesh or Quest frame-time suitability. Test coplanar faces,
degenerate input, slivers, material/UV preservation and output growth before
adoption. Never rebuild a large Boolean mesh every frame. Collision shapes must
be generated separately: a hollow cup must actually have an open collision
interior, and a detailed brick must not need a collider for each decorative stud.

OpenSCAD is useful for parametric CAD, but embedding its compiler is not the v1
runtime direction. Its language/geometry engine does not provide this app's
physics, ownership, animation, saved edits or visual editor. A later supported
subset/import adapter could translate to the same recipes, with unsupported
constructs reported explicitly. Do not maintain two independent editable sources.

Sources: [Unity Mesh](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Mesh.html),
[IL2CPP restrictions](https://docs.unity3d.com/6000.0/Documentation/Manual/scripting-restrictions.html),
[Procedural Toolkit](https://github.com/Syomus/ProceduralToolkit),
[pb_CSG](https://github.com/karl-/pb_CSG),
[OpenSCAD overview](https://openscad.org/about.html).
These describe capabilities; choosing the bounded recipe architecture is our design decision.

## Reusable components and representative defaults

| Default examples | Shared behaviour to implement or extend | Acceptance boundary |
| --- | --- | --- |
| Pencil, chalk, brush, eraser, chalkboard | Drawing tool with tip/colour/width plus paintable-surface component; persistent marks in surface-local coordinates | Marks follow moved objects; save/Undo/erase work; any compatible object can receive marks. Physical room surfaces use virtual overlays anchored to the scan. UV-less and animated imports need explicit supported modes, not an “any surface” promise. |
| Push button, toggle, controller-mounted button | Existing input bindings connected to ordinary program starts/signals; same button definition with different anchors | Manual, controller and agent starts use the same program. Grabbing/pressing and reach/visibility do not interfere. |
| Plate, spoon, fork, domino, ball | Geometry plus simple rigid body, tuned friction/restitution/mass and gripping | Floor/wall collisions, throwing, stable resting, stacking and reset; geometry detail stays separate from physical complexity. |
| Building bricks and castle | Assemblies/blueprints with stable member IDs, snap sockets, editable breakable connections and baseline slots | Loose stacking and snapped connections are different choices. One grouped creation/reset/Undo; moved/deleted/replaced parts remain identifiable. |
| Spinner, lever, spring button | Native hinge/slider/spring components with bounded joint settings | Interactive fidgets, controllable limits and observable state; no need for a separate fidget interpreter. Deformable stress balls are a later visual/soft-body approximation. |
| Chessboard and pieces | Grid/snap slots, piece identity and ordinary move observations | Physically playable board without mandatory chess rules. Maestro reads piece/square state. Reliable legal-move enforcement or competitive chess would be an optional rule module, not implied by vision. |
| Simple articulated robot | Recipe parts, sockets and existing animation/programs; physical joints only when requested | User can animate it and share object hold/release/throw behaviours. An animated arm is not automatically a physical motor/IK solver. |
| Cup, drink, bucket | Logical container volume/capacity, fill surface, bounded pour transfer and visual stream/splash | Pouring transfers conserved quantity between compatible containers; particles are presentation, not thousands of rigid bodies. Arbitrary imported bowls require a configured container shape. |
| Snow patch, snowball, shallow water area | Bounded surface field/deformation plus effects; explicit scoop/pack/transfer operations | Finger/object marks on a patch, limited piles/balls and bucket interaction. Room-wide water/snow is an optional visual volume; full granular/fluid simulation is outside initial v1 scope. |

The first shipped play kit should demonstrate each accepted reusable component,
not maximize asset count. Every template needs editable source/configuration,
provenance, thumbnail, readable description, physical settings, cost estimate and
an executable acceptance scenario. Shared style materials apply to procedural
and imported assets where compatible; an imported mesh does not automatically
acquire good collision, animation, paint or container behaviour.

## Castle example and world understanding

“Build a small castle and react if our ball knocks it down” needs an assembly
baseline, member slots with position/rotation tolerance, contact observations and
a settling interval. A reusable program watches those facts and emits a semantic
collapse event when a chosen fraction of members has left its slots for long
enough. A recent ball contact can be evidence of a likely cause, not certainty
that one contact caused the entire collapse. Grabbing, Undo, reset, paused physics
and deleted members need explicit policies. The trace exposes the supporting
member changes and contact, so user and agent can correct the rule.

Deterministic local programs perform those checks. The LLM can explain the result,
change the game, or choose a reaction without an API call on every collision or
frame. Physically catching a ball needs a native timed reach/catch capability;
an LLM watching occasional images cannot substitute for that control loop.

Use structured native facts for virtual objects and optional rendered snapshots
for appearance. Real-world camera understanding is a separate permissioned input
path. Passthrough display and an MRUK room scan do not mean Gemini sees the room.
Meta's camera API supplies physical camera frames; those alone do not include
Unity-rendered virtual pieces. The app currently has no Quest passthrough-camera
frame integration. See [Meta's camera API overview](https://developers.meta.com/vr/documentation/unity/unity-pca-overview/).

## Scale and understandable authoring

Use searchable templates, component schemas and program libraries, with advanced
fields folded away. A user can say “make this chalk blue” or “reset that castle”,
inspect the actual resulting changes, and optionally open the same fields/blocks.
New native primitives still require development and testing; programs combine
existing primitives into many games but cannot invent an unavailable simulator.

Keep per-frame work bounded. Catalog discovery and world reads are paged and
filtered; the LLM receives relevant facts instead of the complete world every
turn. Pool repeated meshes/materials, let resting rigid bodies sleep, and budget
active bodies, joints, marks, effects and generated vertices separately. Template
instancing/batch transactions should replace repeated individual saved edits for
large assemblies. Raise current object/run limits only with measured Quest 3
acceptance; an unlimited room is not a release promise.

Preserve existing explicit saved-versus-temporary semantics. Snapshotting a game
saves chosen state, not every physics tick or an automatically resumed pending
throw. Structural template creation and reset need one bounded transaction with
clear partial-failure semantics and one reviewable Undo outcome. Reconnect must
never repeat an uncertain physical effect.

A new subsystem must add its schema, native handler, facts/events, lifecycle,
ownership, cost accounting and meaningful acceptance scenario together. Generated
forms and program calls then reuse it. Unknown component versions should preserve
source and disable only the affected object/behaviour, rather than corrupting
unrelated content. Published schemas and pinned libraries become compatibility
contracts after release; the current development-reset permission does not excuse
breaking future users' saved worlds.

## Review assessment and delivery order

The latest substantive architecture comment is dated September 27:
[one expandable system for play](https://github.com/RONITERVO/MaestroTutor/pull/248#issuecomment-5859222380).
Its main direction is useful. The current implementation already addresses native
modules, animation/create vocabulary, explicit temporary play, ownership priorities,
events, structured values and pinned modules. Its old counts and missing-feature
claims are not a current audit. Preserve exact saved motion IDs; tag-based selection
must remain visible and explicit. Do not silently switch saved actions to transient
ones or automatically resume interrupted physics. Receipt recovery remains an
explicit operation with preserved evidence, not silent history deletion.

Next increments, each completed through shared authoring and native verification:

1. Extend editable geometry and collision proxies, then ship tableware, boards,
   dominoes, bricks and a simple robot as recipe templates.
2. Add assembly instancing, bounded batch edits, snapping and slot/displacement
   observations; prove create/play/knockdown/reset with a small castle.
3. Add generic surface drawing and physical joints; prove chalkboard and fidgets.
4. Add container transfer and bounded snow/surface effects after profiling the
   core kit. Full-room fluid/granular simulation remains deferred.
5. Expand content only when each reusable component passes interaction, save/Undo,
   Stop/lifecycle, manual takeover, agent parity and real Quest frame-time checks.

Meshy remains useful for curated organic/default art and optional imports. It is
not a requirement for native parametric creation. Image generation can supply
reference sheets/textures but does not create a rigged, physical, scripted object.
No Meshy credentials were read and no credits were spent for this comparison.
The user's credit balance is a reported starting point, not a verified API balance.
Any later development generation should record model/task, estimated/actual cost,
provenance and accepted output. API generation is not silently added to end-user
v1 subscriptions by this note.
