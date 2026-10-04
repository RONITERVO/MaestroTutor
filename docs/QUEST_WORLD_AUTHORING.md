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

The checked-in generated capability catalog is the current vocabulary authority. It includes
native capability modules, generated forms, shared human/agent execution,
revision checks, durable one-off receipts, contact/proximity/settling observations,
structured program values and action inputs/results, parallel branches, pinned reusable program modules,
remembered values, priority ownership and explicit temporary-room snapshots.
See QUEST_CAPABILITY_MODULES, QUEST_EVENT_PROGRAMS, QUEST_PROGRAM_DATA,
QUEST_PROGRAM_MODULES, QUEST_PROGRAM_MEMORY, QUEST_ROOM_OWNERSHIP and
QUEST_TEMPORARY_ROOM for actual limits and lifecycle rules.

Physical drawing is a saved 3D stroke. Recipe construction currently accepts
box/sphere/cylinder parts, editable lathe profiles, closed-outline extrusions and profile sweeps (at most 32 parts) with
parented rotations and bounded tracks. The lathe increment is described in
[recipe authoring](QUEST_RECIPE_AUTHORING.md#editable-revolved-profiles-2026-10-02).
Recipe parts are visual joints, not independent rigid bodies. The object can now
use an editable compound collision recipe; see
[collision authoring](QUEST_COLLISION_AUTHORING.md). Shared attachment, aimed throws and bounded assisted catching exist; see
[physical catching](QUEST_PHYSICAL_CATCHING.md). Hit reactions, arbitrary
mesh/deforming paintable surfaces and full fluid/granular simulation are separate
capabilities, not implied by those actions. Persistent structure baselines and typed member-list bindings now let one program
create a small build, capture it, displace pieces and reset them; see
[structures](QUEST_STRUCTURES.md). The current
16-creations-per-run budget cannot be described as arbitrary castle construction.
The included Structure state waits module now expresses knocked-down/rebuilt
thresholds using the existing condition interpreter and exact pinned source,
shared by library inspection, editable drafts and agent-created programs; see
[included modules](QUEST_PROGRAM_MODULES.md#included-modules-2026-10-03).

The default library now includes a sixteen-piece **Small fort** and a two-piece
**Passive spinner**, both editable ordinary construction modules. Fort knockdown
and reset reuse the shared structure model; the spinner uses a passive hinge.
See [starter constructions](QUEST_CREATION_TEMPLATES.md#editable-construction-examples-2026-10-04).
These are bounded playable examples, not completion of the entire proposed kit.

The developer parity check now connects the real book and original chat flow to
an isolated real Unity app. It verifies both actors editing one object, a stale
agent plan preserving the human edit, shared native image delivery, receipts and
reload without replay. Only provider responses are scripted offline. This provides
a reusable integration check as the kit expands; it does not prove arbitrary games
or model reasoning. See [the live book journey](QUEST_NATIVE_ROOM_PROBE.md#live-book-and-original-chat-journey-2026-10-04).

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

The first eleven recipe templates now cover tableware, a domino, loose stacking
bricks, a pawn, a ball, a simple animated robot, a chalkboard and held chalk. They share atomic geometry,
collision and physics creation; see [starter objects](QUEST_CREATION_TEMPLATES.md).
A shared [layout operation](QUEST_LAYOUT_AUTHORING.md) now arranges/resets up to
16 existing pieces atomically with live-pose Undo. [Atomic structure creation](QUEST_BATCH_CREATION.md)
now instantiates template/inline-recipe blueprints through ordinary programs and
returns typed lists of piece IDs. [Persistent structures](QUEST_STRUCTURES.md) now
retain named slots, baselines and missing members, with shared capture/reset and
live displacement facts. Construction capture now publishes ordinary editable
constructor modules with fresh member identities and internal physical connections; see
[creation and capture](QUEST_BATCH_CREATION.md). Shared transient selection lets
users and Maestro collect, highlight and order pieces through the same state; see
[construction selection](QUEST_CONSTRUCTION_SELECTION.md). A solid shared
[construction handle](QUEST_CONSTRUCTION_MOVEMENT.md) now arranges those pieces
with one save/Undo while physics is paused. [Saved snap points](QUEST_SNAP_POINTS.md)
now align complete constructions and optionally join them through one shared edit.
Physical construction-handle previews now use the shared snap placement action (see [snap points](QUEST_SNAP_POINTS.md)). Configured cylindrical/spherical patches now extend plane drawing; arbitrary mesh/skin painting and persistent liquid fields remain unfinished. Sculptable snow fields and measured packing use bounded geometry rather than individual grains. Bounded physical pouring now uses the shared container component. Bounded sliders and an editable spring-button example now extend the shared connections.
[Surface drawing](QUEST_SURFACE_DRAWING.md) adds reusable explicit patches,
shared ink editing/readback, a physical surface pencil/eraser, and configurable
held drawing tips on ordinary created/imported objects. Pencil, Paint brush and
Eraser defaults now demonstrate that same editable component; held erasing groups
a contact gesture into one saved edit and Undo.

The complete shipped play kit should demonstrate each accepted reusable component,
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
for appearance. The shared [virtual-room capture](QUEST_ROOM_VIEW.md) capability now
returns native pixels to the user and an explicitly capturing task; device
performance and live-provider interpretation are still unverified. Real-world camera understanding is a separate permissioned input
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

## Physical hinges (2026-10-03)

The hinge increment has been generalized into one saved physical connection
component: rotating hinges, rigid joins, bounded sliders, optional break limits, native state and
a typed break event. Book controls, user programs and the agent share the same
catalog contract. See [physical connections](QUEST_PHYSICAL_CONNECTIONS.md) for
current IDs, save-format boundaries, repair semantics, budgets and verification.
Connected version-3 blueprints and construction capture preserve all three kinds with
fresh member identities. The included spring button uses ordinary recipes and condition waits.
Construction-handle snapping is described in [snap points](QUEST_SNAP_POINTS.md);
bounded pouring is described in [containers](QUEST_CONTAINERS.md); Quest performance remains a device gate.


### Measured liquid containers (2026-10-03)

`containers.v1` adds one reusable saved liquid store per created object, at most
16 per room. `object.container.edit` configures its cavity and authors its contents;
`object.container.transfer` conserves an explicitly requested amount between two
revision-guarded objects, capped by source quantity and destination space. Both
share the ordinary ownership, receipt, persistence, Undo, temporary-room and
prototype paths. `object.container` exposes the same saved state to the user and
agent. See [liquid containers](QUEST_CONTAINERS.md).

`containerPouring.v1` extends those same components with bounded hand/gravity
pouring while room physics is active. A gravity stream is clipped by solid
geometry and transfers liquid through another configured opening. One short
session publishes quantities atomically with one Undo and emits
`object.container.poured`; `object.container.live` exposes current quantities
without pretending they are already saved. Both are ordinary typed program/agent
observations. Uncollected spills leave the model; persistent puddles, water forces,
bare-hand scooping and snow fields remain unfinished. Desktop verification cannot establish
Quest pouring comfort or sustained frame performance.


### Closed-outline extrusion (2026-10-03)

The shared recipe evaluator now extrudes a bounded simple XY outline into a solid.
Concave outlines let users and Maestro make brackets, signs, puzzle shapes and
similar parts without importing meshes or using an external generation service.
The existing part editor, create/edit actions, paged profile facts, collision
configuration and saved/temporary lifecycle remain the only authoring paths.
See [recipe authoring](QUEST_RECIPE_AUTHORING.md#editable-outline-extrusion-2026-10-03)
for bounds, format protection and the separate collision/performance requirements.
CSG, arbitrary mesh/skin paint, persistent water/snow and device acceptance remain open.


### Profile sweeps (2026-10-04)

Sweeps now extend the same recipe evaluator, part editor and shared actions. An
editable closed section follows a bounded open 3D path, covering handles, bent
rods and similar parts without external generation. Exact section/path facts,
source validation, generated-vertex budgets, saving, Undo and temporary rooms
remain shared. See [recipe authoring](QUEST_RECIPE_AUTHORING.md#editable-profile-sweeps-2026-10-04).
This does not add automatic Boolean cleanup, matching collision generation or
closed spline loops. Physical proxies and headset acceptance remain explicit.


`containerScooping.v1` now adds bounded vessel dipping to that same liquid model:
whole submerged openings, conservative clear paths, shared source/receiver rate
limits and conserved quantities. Bucket and Water basin are ordinary editable
starter templates. `object.container.scooping` and `object.container.scooped`
provide typed live observations and accepted events to both users and Maestro.
The existing pouring counters retain their meaning. This does not implement
finger scooping, displacement, fluid forces or persistent water/snow fields;
see [containers](QUEST_CONTAINERS.md#physical-vessel-scooping-2026-10-04).


### Editable chess kit and reusable pigment patterns (2026-10-04)

The default library now includes a board, all six piece types and ordinary white/
black construction modules. Full setup uses 33 independent room objects. Users
and Maestro share the same source, snap points, world poses, physics and Undo;
no hidden chess-only state is introduced. Rules remain optional authored programs.
See [starter templates](QUEST_CREATION_TEMPLATES.md#chess-construction-and-reusable-patterns)
for setup, origins, budgets and transaction boundaries.

Checker/stripe materials are a reusable recipe field, including explicit projection,
counts and a second pigment. They do not add cells to the scene graph. The normal
book part editor and agent recipe edits change the same data, with exact fact
readback. Room format 14 protects this source from older readers. Physical play
and patterns still require Quest readability and sustained performance acceptance.


### Shared sculptable surfaces (2026-10-04)

A bounded height grid is now a saved component on fixed created objects. The
Snow patch default uses the same component available to user-created recipes and
imports. `object.field.edit` explicitly configures/resets it; `object.field.sculpt`
raises, lowers or levels a swept local X/Z path. Book catalog forms, programs and
the agent share both actions, exact revision guards and paged source observations.
A repeated/crossing path affects each vertex once, with smooth radial falloff.

One owner can have one field; at most four fields per room, with 4/8/16 cells per
side. The largest has 289 authored heights, 549 rendered vertices and 642 collision
triangles, including skirts/backing. One mesh and one additional nonconvex collider
are rebuilt on accepted edits, with no per-frame terrain cooking or per-cell bodies.
Changed bounds wake nearby sleeping free bodies once, so lowering support lets
gravity act. Held/animation-owned objects remain under their existing owner.
The owner must remain fixed; it can still be moved/resized through existing controls.
Existing base collision remains independent and can obstruct an authored depression.

Room v15, paired snapshot v14 and archive v14 protect the source. Copying, captured
prototypes, Undo and temporary rooms retain independent exact height arrays. The
reported volume integrates the local mesh; it is an authoring measurement, not
conserved snow. Sculpting can explicitly add/remove volume. Physical gestures are described below. The geometry component alone does not implement material scooping/packing,
gravity flow or water behaviour. Later measured-material increments are described
below; Quest performance remains unverified. This is not a fluid or granular solver.


### Physical sculpting and reusable tools (2026-10-04)

`physicalSculpting.v1` connects controller, index-fingertip and held-tool gestures
to the existing height-field evaluator. `field.tool.set` and the physical tray
choose raise/lower/level plus radius and height. A controller trigger works within
25 cm of the accepted surface; a tracked index fingertip uses a 15 mm contact
band and must first separate after enabling or interruption. This is deliberate
shape authoring: a stationary contact does not repeatedly add/remove height.

Each gesture samples at most 32 local X/Z points. The renderer previews its draft
at up to 20 Hz; collision and saved source stay on the accepted mesh until one
successful save/Undo at release, lift or the sample limit. Large discontinuities
end the current path rather than carving through the skipped space. Solid
obstructions block physical contact. Tool settings are in field-local metres;
contact distances are world metres. Preview/collision separation needs explicit
headset acceptance, particularly while other objects rest on the field.

The Sculpt brush default is ordinary editable recipe data. Any created/imported
object can receive one `object.sculptTip.edit` component on its root or a stable
recipe part, with its own settings and local +Z contact direction. It activates
only while held by the user or an existing Maestro/program attachment; loose
objects are inert. An enabled drawing tip and sculpt tip cannot share one object.
Manual tools use control ownership, while program-held tools cannot preempt a
human surface edit. No animation or physics starts from configuring a tip.

`object.field.capture` reports the gesture/session, target, brush settings and
bounded error; `object.field.capture.path` pages its exact points. A failed save,
tracking interruption or pause retains the visual draft in memory and blocks
workspace/temporary-room changes. `object.field.resolve` explicitly retries or
discards the exact retained session. Retry requires the original field, room
session and ownership; successful receipt replay cannot apply the same gesture
twice. Discard restores the accepted visual. App destruction loses unsaved drafts.
The physical tray's Retry/Discard controls use the same resolution path.

Room v16, paired snapshot v15 and archive v15 preserve sculpt-tip source through
copy, captured prototypes and workspace saves. The book and agent discover the
same capability definitions, generated forms, feature requirements and facts.
The shape gesture does not itself transfer material. Later measured-material
components are described below; granular snow and flowing water remain excluded.


## Shared surface transfer (2026-10-04)

`object.field.transfer` now moves bounded local geometric volume between two
compatible fixed height fields. It uses the shared catalog, per-endpoint current
revisions, atomic room edits and one Undo for both meshes/colliders. The receipt
reports measured source loss, destination gain and bounded rounding error; an
unrepresentable transfer refuses. This extends authoring without another snow-only
runtime or a hidden quantity ledger. See [surface transfer](QUEST_SURFACE_TRANSFER.md).

Ordinary sculpting remains a deliberate shape edit. The transfer action alone
does not imply physical capture, carrying or packing. The later components below
add those bounded behaviours; persistent pools and Quest profiling remain open.

## Measured carried material and packing (2026-10-04)

`object.material.pack` now removes measured local volume from a height surface and
creates one editable sphere with matching collision and a saved material store.
The book form, Maestro and programs share that action and typed result. Packing
uses one save/Undo; failed saves and ownership/capacity refusals preserve the patch.
`object.material.edit` and `object.material` expose the reusable component so
future scoops and vessels can retain the same quantity model. See
[material packing](QUEST_MATERIAL_PACKING.md) for units, persistence and limits.

The ball uses ordinary grabbing, release, gravity and collision. Hand-packing
gestures, full-app Quest acceptance and release work remain outstanding. The
packing increment used room v17 with paired intent/archive v16; held material tools
below use room v18 and paired intent/archive v17. Earlier checkpoints retain their
original format and evidence numbers.

## Physical catching

The shared system now adds `object.physics.catch`, a live `object.catch` fact and
`object.caught` event. A native local loop detects contact and reaches with the
visible avatar arm; root/recipe sockets use the same action. The incoming prop
remains available for human pickup/throw until contact. Per-channel/reflex ownership,
ordinary physics and explicit missed/caught outcomes support user-authored games
without giving the LLM responsibility for frame timing. See
[physical catching](QUEST_PHYSICAL_CATCHING.md) for limits and acceptance boundaries.


### Held material tools (2026-10-04)

The shared transfer kernel now has a held contact adapter on the existing sculpt
component. Users and Maestro can configure the same tip and material store on
ordinary objects. One upward-facing contact previews a take; inverted contact
previews a deposit. Lift publishes both balances once. A small carried heap uses
the same accepted/draft quantity. Retained failures, ownership and explicit
retry/discard reuse the existing surface capture lifecycle. The Material scoop
template demonstrates these components; it adds no snow-specific scripting
engine. See [the contract](QUEST_MATERIAL_PACKING.md#physical-material-tools).
Full fluid/granular simulation and physical Quest acceptance remain separate.
