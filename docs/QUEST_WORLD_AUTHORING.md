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
Physical construction-handle previews now use the shared snap placement action (see [snap points](QUEST_SNAP_POINTS.md)). Point definitions and snap/place/join now use the shared ordered save-completion queue. The handle observes the same receipt as the book, agent and programs; only the selected bodies and destination are held during admitted placement preparation. Pre-acceptance cancellation preserves the arrangement; accepted save failures remain visible and require recovery. Configured cylindrical/spherical patches now extend plane drawing; arbitrary mesh/skin painting and persistent liquid fields remain unfinished. Sculptable snow fields and measured packing use bounded geometry rather than individual grains. Bounded physical pouring now uses the shared container component. Bounded sliders and an editable spring-button example now extend the shared connections.
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

Shared configuration/reset/removal, sculpt and measured field-transfer actions
now use the existing ordered pending-edit queue. Inputs are captured before
waiting; stale targets require a fresh request. Preparation leaves the accepted
height field and collision untouched. Native acceptance applies the shape and
collision together, and the action completes only after its background save
confirms durability. Sculpt/transfer outputs describe that accepted edit once.
Cancellation before acceptance changes nothing; after acceptance Stop cannot
retract the shape or abandon saving. A failed accepted save keeps the live shape
unconfirmed and blocks further saving until workspace recovery. Do not blindly
replay it. Temporary-room results remain local until Keep.

The existing full-component transaction also revalidates terrain-backed liquid
capacity: a changed bed preserves the water amount or refuses overflow. Height
fields still require fixed owners. Physical sculpt/scoop/packing gestures now use
this pending-save boundary too (see below). Shared material-store configuration,
packing and transfer also use it: measured results and created IDs are published
only on completion, and store/transfer history preserves current root placement.
See [material packing](QUEST_MATERIAL_PACKING.md). Legacy synchronous editor APIs
and other authoring families still need migration; not every edit is asynchronous. Snapshot capture and native
mesh/collision reconciliation still run on the owner thread, and physical Quest
latency remains unverified.

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
at up to 20 Hz. Release, lift or the sample limit starts one queued save/Undo.
Collision and live source update together on native acceptance; completion waits
for the background save. The preview is removed on acceptance, so it cannot
cover the new collision with an old draft. Large discontinuities
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
bounded error, including the `saving` phase while queued, preparing or awaiting
durable confirmation; `object.field.capture.path` pages its exact points. Failure,
tracking interruption or pause before acceptance retains the visual draft in
memory and blocks workspace/temporary-room changes. `object.field.resolve` explicitly retries or
discards the exact retained session. Retry requires the original field, room
session and ownership; successful receipt replay cannot apply the same gesture
twice. Discard restores the accepted visual. App destruction loses unsaved drafts.
The physical tray's Retry/Discard controls use the same resolution path. Retry is
a completion action; discard is immediate. Pending resolution refuses a second
retry/discard. After native acceptance, Stop/pause removes the retryable draft
and lets the save drain. An accepted save failure leaves the live edit in place,
blocks further saving and requires inspection/recovery rather than another retry.
Receipts remain immutable and cannot affect a later gesture. Temporary-room
completion is local until Keep; snapshot/native geometry work still runs on the
owner thread and Quest latency is not yet accepted. The held scoop uses an
internal content-only transaction: root movement survives acceptance and Undo,
while authored resizing still invalidates an older candidate. The scope cannot
create/remove objects, change identity/kind or edit world resources. Packing
retains the full transaction because it creates a new ball.

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
add those bounded behaviours. The later rectangular-container addition provides
an authored shallow pool; uncontained water fields and Quest profiling remain open.

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


### Physical hand/controller packing (2026-10-04)

Hands and controller triggers now adapt the same measured material packing action
already available to the agent. A single touch-and-lift contact previews and saves
one ordinary ball with its field removal and one Undo. Shared tool settings,
retained-draft recovery and exact last-created identity keep human/program/agent
observations together. Pinch-grabbing props remains available in packing mode.
The solid tray has a Pack ball control; original chat remains the default interface.
See [material packing](QUEST_MATERIAL_PACKING.md#physical-hand-and-controller-packing).
This closes the desktop hand-packing adapter, not Quest acceptance, persistent
water or a full fluid/granular simulation.


### Shared rectangular reservoirs (2026-10-04)

The liquid component now accepts a rectangular cavity in addition to a cylinder.
The editable Shallow pool is one reusable example: users can dip ordinary buckets
and cups into it and pour back. Shape, accepted quantities, current live quantities,
publication events and atomic Undo use the existing shared container system.
Room v19 and paired intent/archive v18 preserve its dimensions. No additional
pool-specific action, agent tool or simulation is introduced. The generated editor
loads current shape and contents together; program authoring turns current record
preferences into visible typed bindings from one read. See
[containers](QUEST_CONTAINERS.md#rectangular-cavities-and-shallow-pools-2026-10-04).
Uncontained puddles, water/snow particle solvers and buoyancy remain outside this
increment, and actual Quest acceptance is still required.


### Scanned ink as a reusable component (2026-10-05)

Saved wall drawings now extend the existing Drawing object and surface-ink path.
An exact scanned-plane binding supplies placement; ordinary ink supplies source,
physical tools, agent edits and Undo. The layer has no background panel and stays
saved but hidden when its room or anchor is unavailable. Explicit rebind is a
reviewable edit, not automatic guessing. See [surface drawing](QUEST_SURFACE_DRAWING.md#saved-scanned-ink-layers-2026-10-05).
This increment is desktop-verified and packaged, with headset acceptance pending; it
does not add general scene-object anchoring, triangle painting or a new scripting
runtime. Native recipes and shared components remain the expansion mechanism.


### Optional physical authoring trays (2026-10-06)

A workspace now starts with the familiar book and Maestro, with its seven physical
authoring trays hidden. Existing creations and configured user buttons keep their
normal visibility. The 3D **Workshop** blocks beside the book open the workshop
without an AI provider. **Physical tools** opens the shared generated controls. Choose creation, animation, behaviours, imports,
physics, avatar movement or controller bindings; show only the tools needed, or
explicitly show/hide all. These remain movable solid 3D controls outside the book.

The agent and event programs use the same `room.tools.set` catalog action. Its
`tray` and `visible` fields use the exact `stateId` read from `room.tools`, whose
visible/held flags describe each tray. The native service rechecks real grip
ownership. Stale visibility intent is refused; hiding all is atomic when a tray
is held or physical surface placement still needs finishing/cancelling. Duplicate
receipts do not reopen tools. There is no second agent tool or execution path.

Visibility is transient presentation state, with no room save, migration or Undo.
A new workspace/relaunch starts hidden; within a workspace, showing a tray keeps
its position. Existing Recall retrieves it without reopening hidden trays. The
book and palm recovery controls remain available. Showing a tray refreshes its
labels; hidden trays stop updating labels and remove their visible/input surfaces.
Their underlying import, animation, drawing and behaviour services stay active.
Hiding a tray does not erase drafts, stop a drawing mode, pause physics or stop a
program. Those actions retain their own explicit controls and shared capabilities.

Full native validation passed: 837 EditMode and 647 PlayMode tests, with three
optional private-file skips. The real book journey showed and hid trays through
the generated form and native receipts without changing the saved scene revision.
The audited development APK is ready; installation, physical interaction and
performance acceptance wait for the headset to finish charging. See the
[package and QA record](QUEST_DEVICE_QA.md#optional-physical-tool-trays--2026-10-06).

## Reusable illustrated appearances

The shared catalog provides these saved authoring operations:

- appearance.save creates or updates a named definition. Shared updates include
  the exact revision and every object currently using it.
- object.appearance.bind assigns it, makes an independent copy, or removes an
  exact binding. A part or imported material slot overrides the root binding.
- appearance.remove deletes only an unused definition.

These operations finish only after the shared edit queue confirms persistence.
An independent copy and its object binding are one atomic edit and one Undo.
While waiting, edits recheck the source revision, complete membership, native
surface availability and capacity. Cancellation before acceptance publishes
neither a copied resource nor a binding. After acceptance, stopping drains the
accepted save and reports it as pending; it does not report a successful receipt.
Receipts retain the revisions accepted by that edit even if a later human edit
changes the same appearance. Shared opacity changes preserve unrelated runtime
placement and the advancing world clock. Temporary Keep/Discard uses the same
resource-and-binding transaction.

Use appearance.library, appearance.definition, appearance.members,
object.appearances and object.appearance.targets to inspect current values and
stable addresses. Member/binding pages keep observations within the same bounded
program-value contract used by all other facts. Imported slots use exact model
content hashes and source material indices. Dormant bindings are retained and
reported after model or recipe changes.

Painting a single object sets a local tint override instead of mutating its
shared definition. The ordinary recipe colour editor likewise edits a bound
part's local tint. An empty override resumes the shared tint. Unsupported or
hidden source-pattern edits give an explanation. Book pages and surface ink have
their own content and are not recoloured by these bindings.

Opacity uses an explicit rendering mode and does not change collision, sound,
physical material contents or the real-world blend. Procedural patterns and
inherited imported images/UVs are available; the following increment adds explicit
local image import. Original-chat generated-image attachment uses the shared import path described below. Whole-workspace export retains appearances.
Portable construction modules currently refuse appearance-bound objects until
their dependencies can be bundled, rather than silently losing their style.

## Imported images in shared appearances — 2026-10-09

The image increment extends `AppearanceStyle`, not a separate renderer, scene or
agent-only texture tool. `patternMode=image` selects an exact private `imageHash`;
`inherit` keeps source materials and `replace` selects procedural patterns. Those
two modes require an empty image hash. Appearance tint, UV tiling/offset, surface
opacity/cutout, collision participation and audio remain independent. Root,
recipe-part and stable imported-material bindings use their existing precedence.
Book browser pages and drawing-overlay ink retain their display materials.

`image.import` owns explicit select, inspect, accept, cancel and refresh states.
The shared generated forms, agent and programs call the same native capability.
Accepting a checked preview saves an immutable private file and creates one
unbound reusable appearance with one Undo. It never changes an object's binding.
Undo or temporary-room Discard removes the definition edit, not the imported
file. `object.appearance.bind` assigns it using current object/appearance revisions.
Original-chat generated-image attachment feeds this asset pipeline;
Unity must not acquire a duplicate image-generation provider or credentials.

The initial adapter accepts static 8-bit PNG and baseline/progressive grayscale
or RGB JPEG, up to 16 MiB and 2048 pixels per side. PNG chunk lengths/checksums,
critical chunks, animation markers, JPEG frame/scan structure and EXIF bounds are
checked before native decode. PNG/JPEG EXIF orientation is applied exactly once;
original bytes keep their identity. Pixels are interpreted as sRGB; professional
ICC/HDR colour management is outside this adapter. Alpha is retained; an opaque
surface does not become translucent merely because its image contains alpha.

Each workspace owns up to 32 private images / 128 MiB. Rendering shares one
texture per used hash, with a 64 MiB decoded mipmap budget; one file read/decode
is dispatched at a time. I/O, hashing and structural inspection run off-thread;
Unity texture creation/upload stays on the main thread. These are bounded
implementation limits, not a measured Quest frame-time guarantee. A large decode
can still stall a frame and requires physical profiling before release.
Textures use mipmaps, release CPU pixels after upload, and release when their
last material owner leaves. Material variants hold texture leases through refresh.
Reads wait during workspace preservation, and cache notifications refresh only
objects that own the changed image. The idle cache does not allocate each frame.
Shutdown cancels pending reads; no late worker may install pixels in a new room.
The native upload behavior follows Unity's
[LoadImage contract](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/ImageConversion.LoadImage.html)
and [Apply contract](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Texture2D.Apply.html).

Missing, damaged or over-budget images keep their exact reference and show a
neutral checker. `image.render` reports unloaded/loading/ready/failed and a
bounded reason; `object.appearances` reflects pending material refresh too.
Library refresh retries failed bound images. It does not silently choose another
file. Successful source storage is distinct from successful pixel loading and
from human headset acceptance.

Portable archives allow 1,536 entries, enough for all bounded libraries and their
metadata, within the existing 512 MiB total archive limit.
Portable archives include original `images/<hash>.image` bytes and bounded name
metadata, plus image/missing-image counts across export, preview, recovery and
retention receipts. They capture the complete bounded library because programs
can compute asset choices later. Literal image references in saved programs and
modules also contribute to missing-asset reporting. Hash checks and parser checks
run on export and staging; native decode independently verifies bytes when used.
Room format 33, paired-snapshot filename 32 and archive format 31 keep incompatible
older/future data explicit instead of guessing migrations. Original user data is
preserved by the existing recovery process.

Acceptance must cover both the original book form and real managed/BYOK provider
requests: choose a saved image, bind it, adjust opacity, unbind while preserving
reusable content. Separate native tests inspect actual GPU pixels for all eight
orientations, alpha, shared texture ownership, missing-file recovery and archive
round trips. Android picker ownership, stream bounds and cancellation use the
same selected-file owner as other imports. Physical Quest picker usability,
visual comfort, sustained memory/performance and larger/other formats remain
release work. Chat-generated attachment is described below.


## Use a chat picture on a world object

Ask Maestro to use a picture from the current chat on an object's surface. Image
generation stays in the original app. The room agent can discover that image,
select it for the existing native preview, accept its private copy, and bind the
resulting shared appearance. Ordinary opacity, UV scale/offset, tint, Undo and
appearance reuse still apply. Book page display materials remain separate.

In the workshop, **Import an image together**, variant **selectChat**, supplies a
named **Chat image** chooser. Choose the image, load current values and run the
selection. Inspect **Image import status** until its preview is ready, then
choose **accept**, load current values and accept that preview. Use **Choose
object appearance** to bind the resulting appearance. These controls and the
agent use the same catalog.

Offers cover the newest eight locally available PNG/JPEG images after the chat's
history bookmark. They preserve the exact available byte variant; remote-only
images, oversized images and unsupported encodings need the existing file import
or a supported export. Changing chat, room or image offers cancels unaccepted
previews. Accepted images live in the private library and portable workspace;
they no longer depend on keeping that chat message.

Desktop verification passes through the original book controls and separate real
managed/BYOK image-generation and agent journeys: apply the generated picture,
change opacity, remove the binding and retain the exact saved content. Quest
latency, readability and sustained memory/performance remain separate gates.


## Passthrough openings on shared surfaces

A passthrough opening is a saved `RoomWindow` component on an existing plane surface of a created object. `object.window.edit` and `object.windows` use the same revision-guarded source through the original book forms, delegated agent and programs. Configure a plane using `object.surface.edit`, then select its exact surface ID. The plane owns position, rotation and dimensions; the window owns its stable ID, rectangular/elliptical shape and reveal amount. This keeps placement, editing and source inspection shared with surface ink. Drawing enabled/disabled controls new ink, while reveal and the object's visibility-layer opacity control the opening. Ordinary texture opacity, real-world collision profiles and real-depth settings remain separate.

An ordinary object's opening follows the object or its recipe part, including virtual-world movement. For a physical attachment, `drawing.layer.edit` creates the existing exact scanned-surface host; its `Canvas` can have a window with no ink. That host stays in the physical frame. Lost, changed-room or missing anchors hide both ink and window until the exact binding is available again; there is no nearest-wall substitution. Rebinding and surface resizing use the same boundary checks. The object can carry ink and an opening together. Remove or rebind a referencing window before removing or curving its plane.

Rendering multiplies framebuffer RGB and alpha together to expose the headset's existing passthrough underlay. Opaque foreground geometry uses depth; the mask shares the transparent queue with translucent surfaces so nearer transparent objects composite afterward. Mask depth is the authored plane, not a measurement of the real scene. As with ordinary sorted transparency, intersecting transparent geometry has ordering limitations. Overlapping masks multiply the coverage left behind. A frame should have an actual open centre when that is the intended construction; the component is not a mesh-cutting operation. The view keeps the underlay available at full backdrop opacity only while an active opening requests it. It never opts into camera sharing. Without a running headset passthrough subsystem the source remains editable and masks are not rendered. `renderingReady` reports that prerequisite, not physical alignment or visual acceptance.

Current bounds are four windows per object, one per plane, and 16 per room. Their mask geometry has no collider. Virtual-only camera snapshots omit mask renderers, preserving the virtual-only provenance; composed headset sharing retains the real rendered view. Room format 34 and prototype format 4 retain these components through storage, Undo, copying and reusable construction source. Version-33 rooms without windows remain readable. Unknown window versions block writing; malformed explicit fields fail validation and use the existing room-recovery path rather than silently defaulting the window. Quest stereo, hand/controller foreground composition, transparency sorting, anchor recovery and performance require device acceptance separately from desktop framebuffer tests.


## Organize creations into world areas

Use **Save world area** to create a named area, then **Choose creation area** on
an object. Load its current values, load saved areas and select the area by name.
Choose **Home area** to remove the named assignment. The agent uses these same
catalog actions. Area membership does not move the object or alter its physics,
materials, animations, depth visibility or sound. In particular it does not change
whether that object collides with the scanned floor; use its environment profile
for that choice. Maestro and the book remain world-owned.

Each creation has at most one area, and a world supports 32 named areas. To rename,
read `world.region` and all `world.region.members` pages at the same revision and
provide that complete membership. Move all creations out before removing an area.
Deletion and Undo keep object membership together. Copies and portable imports
start in home. Saves, temporary rooms and workspace export/restore retain exact
area and creation identities.

These saved areas also define connected native loading groups. The workspace
can retire unused groups after viewing grace and restore them when viewed or
needed by an action. This does not increase current world capacity or keep
distant regions simulated. Membership facts remain separate from runtime
readiness; see [retiring unused areas](#retiring-unused-areas).

Area save/removal and object membership assignment use the same bounded persisted
edit queue as layouts, sound and appearance. The book, agent and programs wait for
completion and inspect the accepted receipt instead of replaying a request.
Transferring a member updates its source and destination in one edit and one Undo.
Queued requests retain names, IDs and revisions; membership, capacity and stale
preconditions are rechecked before acceptance. Area metadata never holds physics
bodies or rewinds their poses or world time. Receipts retain the accepted area or
object revision after later changes, including unchanged saves. Cancellation
before acceptance applies nothing; accepted saves drain after interruption.
Temporary Keep/Discard preserves the same atomic membership semantics.

### Inspecting an area's current native dependencies

The shared `world.region.retention` fact accepts an authored area ID, or empty `id` for home creations. It reports saved members, active native objects, retained members, missing connection peers and aggregate retention reasons. A sound retains its emitter while preparing, playing or paused; manual/action channel ownership and enabled connections also carry retention across area boundaries. The report is ephemeral and does not edit the world.

`unloadingSupported` reports whether automatic retirement is enabled in this workspace. It does not authorize releasing an area or promise that current dependencies allow it. Running physics retains each active dynamic actor and its swept collision, ground, water and physical-connection dependencies, including sleeping and pending bodies. Unknown actor bounds conservatively retain the environment; unrelated static areas may retire. A missing runtime object is reported as unavailable while its saved entity remains intact.

### Saved content versus a loaded object

`object.presence` distinguishes an entity missing from the saved world from saved content whose native instance is unavailable, inactive or active. Read `object.definition` for its saved placement and revision. Drawings, surfaces, collision recipes, recorded animation frames and procedural parts/keys can also be inspected without a live object. Reads do not repair or load it. Active presence alone does not guarantee imported geometry, textures, sound or physics readiness.

The shared object list keeps unavailable saved entities and labels its coordinates with `positionSource: saved` or `live`; use this instead of treating saved placement as observed motion. Shared actions with explicit native dependencies can prepare intentionally dormant objects before their ordinary native checks; the receipt reports loading, failure or completion. An unexpectedly lost instance is still unavailable rather than silently treated as dormant. Combined facts that include playback or effective runtime collision state remain unavailable if their native instance is absent. The whole-world runtime can rebuild an unexpectedly lost creation during an accepted edit or Undo while retaining its ID and content. The workspace now schedules intentional retirement through this same mechanism; it never treats an unexpectedly lost object as an intentional unload.

During internal area loading, unfinished objects stay invisible and unavailable
for picking until the complete connected group is prepared. Cancelling or editing
its saved input prevents the old group from appearing and permits a fresh retry.
Loading restores saved/cached placement and stopped animation state; it does not
start physics. Preparation now yields between batches across frames. Automatic retirement and
view/action restoration use this same bounded path; startup and explicit physics
Start still prepare the whole saved set, and current world limits remain.

Procedural parts can prepare over multiple frames within a single object. Only
its complete, validated source appears; a cancellation, invalid later part or
changed saved recipe discards the unfinished geometry. The same evaluator keeps
its shapes and animations consistent with ordinary synchronous edits. Individual
mesh uploads and activation callbacks still have no hard frame-time guarantee.

The area's dependency report includes `navigation` while an accepted or pending
walking map holds its ground, and `waterRoute` while a detour is being planned
around its liquid. Multiple maps can need the same area. Pausing physics retires
maps; cancelling or finishing a detour releases that search. Inspecting this
report does not create a route, edit the world or enable automatic unloading.

Independent actions can continue while another area prepares. Requests needing
the same connected group share its load; cancelling one request leaves the load
running for the others. Cancelling every requester discards unfinished work.
Actions that need the same control channel still cannot compete. Editing a needed
object, shared style or collision profile can require retrying its pending action;
editing an unrelated object does not. Whole-room edits keep their existing action
cancellation behavior. Retirement remains subject to the shared dependency and
viewer-grace policy.

The dependency report also shows `observation` for objects in the headset view or
an app-owned camera capture. It considers the object's visible bounds, so a long
object can still be needed when its centre is off-screen. Connected area members
remain together. Finishing a snapshot releases that camera's dependency. Reading this diagnostic does not load content. The workspace's separate viewer
scheduler can request dormant groups through the same bounded preparation path.


The shared **Object spatial bounds** readout shows the space occupied by an
object's visuals and collision shapes in room coordinates. It can retain that
information while an area is unloaded, and updates placement without loading it.
Changing geometry can make the result unknown until the geometry is available
again. Unknown is different from empty: the app keeps it as a possible viewing
dependency. Skinned models and scan-anchored drawings currently report unknown
visual bounds. A bounding box is not proof that a place is walkable, clear or
physically supported, and this readout does not enable automatic world streaming.

### Inspecting an imported asset's footprint

The shared catalog's **Imported asset bounds** fact (`object.model.assetBounds`)
lets the user or agent inspect a validated static model using the same scale and
pivot calculation as the native import. Values are in object-local metres before
placement/scale. Changing the saved geometry settings changes the projection at
the reported object revision, without loading an unloaded model.

The content-hash metadata cache is bounded and populated by ordinary model-library
reads/saves. A cache miss, animation, skin, deformation or unsupported/unstable
transform is explicitly unknown. The imported asset envelope excludes authored
decorations, colliders, root motion and current native availability; it is not a
complete object envelope or a walkability/collision guarantee. Reading it does not
start file access, native loading, provider calls or automatic regional streaming.

### Inspecting geometry before loading

The shared “Authored object bounds” fact shows the prepared extent of an object, including ink, terrain, fill, carried material and selection outlines. It uses the accepted world data and verified model metadata, independently of whether a native object exists. Preparation is bounded and belongs to the current room; closing or replacing the room cancels it. Reading the fact is passive.

Pending or unsupported geometry is reported as unknown, never as empty space. Animated recipe parts use a conservative swept envelope; animated imported models and scan anchors remain unknown. These bounds help observation and future loading decisions. They do not prove that physics, walking or asset loading is ready. Startup loads demanded connected areas; finer automatic regional streaming and larger-world capacity are unfinished.


Area dependency inspection distinguishes objects currently in view from those
near the expected view (`approachingView`) and recently viewed
(`recentObservation`). A short grace period prevents rapid turns from immediately
releasing an area's native contents. These reasons propagate through the same
area and physical-connection graph as other dependencies. Inspection itself
does not refresh the timer or load anything. Automatic regional loading remains
unfinished; current world-size limits remain, and simulation still needs an explicit Start.


Starting physics can prepare objects whose saved content is not currently loaded.
The same Start action serves the book, agent, programs and physical tool. You can
cancel preparation or press Pause; finishing a load later will not restart it.
Changing the room setup or captured required content invalidates pending Start;
unrelated saved edits do not.
If required preparation fails, the action reports the failure and keeps physics paused.
An accepted Start enables the world even when selected ground has not loaded yet:
`physics.simulation.running=true` with `ready=false` means enabled and waiting.
No actor moves until its own ground and collision/water inputs are accepted.
Enabling an empty or fixed-only world does not load ground at the viewer or need
a camera; an accepted walking action prepares its own local inputs. That includes
a virtual-only character while the global real-room policy is on and no scan is
available. Ground loading or retirement alone never issues a new Start/Pause.
`canStart` reports current world admission, not permission, alignment approval or
proof that an actor can move. Read the actor's inputs for that distinction.
This does not enable automatic area streaming or expand current world limits.


Opening a saved room prepares demanded areas in stages while the book remains
available. Observation, capture or an explicit action supplies demand; independent
unseen areas remain dormant. Named areas and enabled physical connections load
together. Unassigned home creations load independently unless connected. A broken import in one area
does not prevent independent areas from opening. Loading preserves saved positions and
animation settings and does not switch gravity on. Failed objects stay in the
saved room so they can be inspected and retried after their asset is repaired.

During workspace review, the saved objects can open as a still preview. Approval
alone does not restart saved autoplay, including when approval happens before
loading finishes; starting activity remains a separate request.


When an area is already dormant, looking toward it can bring its connected
objects back through the same loading system. Available view information also
prioritizes areas during startup. Losing tracking or leaving a view does not
start physics, and a timed-out or broken import waits for an explicit retry.
The same display-demand path applies at startup and after retirement. Unknown
bounds remain conservative; observation does not approve physics or prove a safe
path. Finer grouping within named areas, larger-world capacity and fully local
physics failure handling remain unfinished.


Loaded supported avatars and skinned models expose spatial bounds for their
current joints and morph shapes. These are conservative envelopes: they help
visibility and loading decisions but do not replace collision shapes or prove a
walking route. Reading them does not play an animation or modify the room.
A retired model's envelope also covers its reset pose; bounds for an unprepared
animated import can still be unknown.


### Bounds for drawings on scanned surfaces

A scanned ink layer keeps its own local canvas and drawing geometry even while
the scan is unavailable. The shared authored-bounds fact can inspect that shape
without rebuilding the layer. Its room bounds use the exact saved room and wall
anchor at their current position. Moving the virtual world does not turn the
last anchor position into a permanent substitute.

If that anchor is missing, belongs to another room, no longer fits the canvas or
cannot be tracked, room bounds report unknown and observation alone does not
recreate hidden ink. Returning to the exact anchor can make the drawing appear
again through normal display loading. Rebinding remains an explicit user/agent
edit. These bounds do not verify physical alignment or start room scanning or
physics; they share the existing saved objects, catalog and Undo history.


### Imported resting shapes

An imported GLB can have a different default shape from its raw mesh, because of its skeleton or default morph weights. Maestro uses the verified resting shape for supported GLB fitting and source pivots, applies node morph overrides ahead of mesh defaults, and restores those defaults when animation stops.

The shared `object.model.restBounds` fact reports that default shape in object-local metres with the saved model scale and pivot. Reading it does not load the model or edit the world. The original `object.model.assetBounds` fact still describes static supported geometry; neither fact promises the space occupied by every future animation. Current native geometry remains available through the spatial-bounds fact. Unknown bounds are not empty space. Unsupported import math and VRM-specific deformation remain unknown in the cold resting fact. Animated or otherwise actively used objects continue to be retained independently of their resting bounds.

When a file needs the native fallback importer, fitting measures its loaded resting geometry once. Default deformations still affect its size and placement even when the cold resting fact cannot prove them. This does not turn an unknown fact into a known value or start an animation.


### Carried props and preparation

A carried object prepares the ground, obstacles and water along its current path
before following its holder. While that geometry loads, the object holds its
last accepted pose and attached drawing or sculpting tips stop editing. It then
checks the latest holder position; it does not replay the missed movement. A
throw needs fresh motion after the wait. Accepted edits to nearby dependencies
are rechecked before contact continues.

When an animation has a fitted prop, their playback time pauses together. A
standalone hold only owns the carried object and cannot pause an independently
animated holder. Preparation waits share a 30-second budget; failure or
cancellation ends the action rather than leaving an unbounded background task.

The shared `object.attachment.inputs` fact reports inactive, ready, pending or
unavailable contact inputs. Reading it does not load geometry or move anything.
Ready means that the inputs are available; native collision checks still decide
whether movement or release is possible. This uses the object's real-room
collision policy independently of passthrough visibility. It does not provide
regional unloading or collision envelopes for every animated limb.


Recorded playback and its manual preview prepare geometry for the whole bounded
recording before moving, including middle keys that a rendered frame might skip.
Waiting pauses playback time; Stop cancels the pending work. The shared
`object.recording.inputs` fact reports that preparation without starting it.
This confirms required geometry is present, not that every recorded pose or
animated limb has a collision-free path. Imported clip travel uses its own
existing movement checks.


For explicit authored travel from an imported Maestro clip, required native
ground, obstacles and water prepare before the movement query. Waiting keeps the
last accepted full pose and does not consume the action's playback time. Fitted
props share that same bounded wait. Stop cancels pending work. Read
`avatar.authored.inputs` to inspect preparation without starting playback or
loading geometry. Ready is input availability; the normal supported-ground,
body-clearance, viewer-distance and water checks still apply.

Authored imported movement now checks intermediate source keys and cubic component
turning points, supplemented by 120 Hz body substeps. Required geometry covers
that sampled interval, including an out-and-back whose endpoints coincide.
Movement commits only after all sampled segments pass the actor's selected ground,
real-room and water policies. Blocked or oversized intervals keep the last accepted
pose; loading keeps the original pending time. An interval is limited to 256
samples rather than silently dropping keys. These are body-proxy checks; they do
not certify continuous curves between samples or individual animated limbs.


### Physics preparation and interrupted movement

Read `object.physics.inputs` to inspect a solid or bouncy object's required
geometry. Physically connected moving objects prepare and wait together; merely
belonging to the same area does not join them. Separate objects attached to a
fixed anchor can remain independent. Reading this fact never starts physics or
loads an object. `ready` requires current actor admission, selected ground,
local inputs and connected joints; it is not a successful throw or a clear future path.

In an already-running world, a free dynamic object can prepare its dormant local
ground while staying stopped. An object whose policy excludes the real room
cannot borrow the scanned floor: its current authored ground column, complete
local input set and connected joints must be ready before gravity or impulses
resume. Waiting or losing ground is not a landing or settlement event. A newly
created object can carry its selected profile in the same atomic creation bundle.

This preparation does not start a paused world or bypass the safety checks for
changing an existing object's collision profile. Pause, grab, animation control,
teleport, deletion or owner retirement cancels obsolete demand and discards old
momentum. Returning control can prepare a fresh request in a running world;
unavailable inputs still need repair and an explicit restart from rest.

Ordinary `pending` preparation can retain motion in `momentumHeld`. An
`interrupted` result is different: the solver reached unavailable geometry or
left its selected ground boundary, or previously accepted ground disappeared.
An enabled but never-supported body simply waits; losing support after actual
simulation holds prior motion until a fresh owner resumes it from rest.
A temporary geometry guard stops the affected
free dynamic contact/constraint group while exact geometry prepares. A boundary
exit restores that group's last accepted poses and holds it. Independent bodies
keep running. Both cases discard velocity; finishing preparation does not resume
movement.
Inspect the reason, then explicitly pause and start physics, or grab the object,
to continue from rest. Do not retry an old throw automatically or describe this
state as a completed catch. Pause, grab, animation control, teleport and ownership
changes discard prior motion; independent consumers keep their own loads.

Taking control of one object cancels recovery for its current physical connection
group. Merely touching a different object does not give that owner control over
it: other affected free objects retain their stopped state and preparation. This
also applies when ownership changes after a native collision but before its
observation is processed. A continuing guard contact is checked again on native
Stay callbacks; recovery does not depend on a new collision Enter. An active
catch reports the actual waiting reason from the object's physics inputs, so an
interrupted object is not described as waiting for a user to release it.

If a required model is missing or damaged, recovery publishes that failure to
the visible status as well as `object.physics.inputs`. Saved content remains
intact and no placeholder is published. The remaining interrupted objects stay
stopped when another participant is grabbed. Repairing the file, or loading it
for another action, does not retry recovery or replay old movement; explicitly
restart from rest after fixing the input.

This recovery uses the same fact and controls for programs, agents and book
controls. Automatic retirement can release unrelated static areas while physics
retains its local dependencies. Explicit Start prepares resident free dynamic actors and their local dependencies;
resident imported-geometry failures with accepted collision bounds stop dependent
actors locally. Unknown bounds or placement retain the conservative global stop.
A boundary interruption uses the same local group hold and restart controls.
Selective initial scene
startup, larger worlds and hardware performance acceptance
remain open.


`object.collided` and `object.connection.broken` include the boolean event field
`interrupted`. It describes whether either participant belonged to a moving
contact/constraint group that reached unavailable geometry or left its selected
ground boundary during that native step. It is conservative step provenance, not proof that the guard caused this
particular contact or break. A real broken connection remains broken and requires
explicit rearming; the event is not erased or converted into a successful action.

Programs may store this field in a boolean variable and branch before awarding a
point, scheduling another throw or reporting uninterrupted play. The block editor
and agent use the same catalog field. `false` does not certify a completed catch
or clear future path. Direct temporary-guard contacts are never authored collision
events. Dynamic objects touching in that step share its interruption; a shared
fixed floor or anchor does not join unrelated moving objects.

Native collision and break listeners are captured when the observation occurs,
then delivered after that step is classified. Changing the physics intent with
Pause/Start before classification cancels the unfinished observation. Once
classified, it follows ordinary captured-listener delivery; a later pause does
not rewrite the completed collision. Events cannot reach a later wait or escape
the existing bounded program event queue. The captured contact position and
payload remain the original observation. Ordinary
input preparation and `object.physics.inputs.interrupted` remain separate from
this event detail.

Catch waiting reasons are bounded display text in both active facts and terminal
receipts. A timeout still reports `caught=false`; its diagnostic can be stored in
a program text variable without exceeding that value contract. Exact object and
holder identities are preserved.

### Shallow-water exits

The same `object.water.traversal.configure` policy applies to manual walking,
following, recorded root movement and `object.water.traversal.path` inspection.
An `avoid` actor that is already touching shallow water may move uphill to
shallower water or toward a nearest boundary of the expanded liquid cavity.
The entire swept segment must remain within `maxDepthMetres` (also capped at
60% of walking-body height), without going deeper. Small outward steps work
while both endpoints are still wet. Heading across the pool to a farther shore,
entering a different pool or borrowing exit permission from a dry starting point
is refused. The comparisons include a small body-scaled floating-point tolerance.

The source remains the live measured liquid level and current object/room frame.
Accepted ground, solid clearance, input availability and per-actor real-room
participation remain separate requirements. A passive query neither moves an
actor nor loads missing geometry; `allowed` does not mean it has reached dry
ground. Following uses its existing planner and every actual step is checked
again. This is grounded shallow egress, not swimming or a general escape planner;
obstacles, overlapping water boundaries or excessive depth can still prevent an
exit. A zero maximum depth disables meaningful shallow egress. Other saved
settings, water quantity, buoyancy and appearance are unchanged.


## Terrain-backed measured water

The same `object.container.edit` action now accepts `definition.terrainFloor=true`.
The owner must be fixed and have exactly one accepted `RoomHeightField`. There is
one canonical ground grid and one saved liquid amount. The container does not copy
the heights or maintain a second water inventory. Its frame, rectangular footprint
and local capacity derive from the field; `height` is the authored opening plane
and must be at or above every ground vertex. Inspect `object.container`,
`object.field` and `object.field.samples` before editing.

The surface remains perpendicular to gravity. The cavity uses the exact terrain
triangles and the volume between those triangles and the opening; the rendered
shoreline clips dry ground. Medium samples, whole-body sweeps, contact events,
buoyancy, immersion filling and gravity pouring use that same prepared geometry.
Hand/controller ripples draw on the clipped surface. The existing actor water and
real-room participation policies still apply independently to every participant.
Water queries do not grant walking clearance through solids or missing ground.

Raising or lowering ground recalculates capacity and water level while preserving
the saved amount. An edit that would overflow refuses before publication and asks
the author to transfer or explicitly remove enough liquid first. Successful edits
publish terrain, liquid and native geometry together and have one Undo. Failed
saving preserves the accepted shape and amount. Removing the ground while it is
still a reservoir also refuses; remove the container or explicitly choose an
ordinary cavity first. Logical measured transfer, physical cup filling, rain and
saved quantity events use the existing finite-container system.

A terrain reservoir is component v3, a room containing it needs room format 37,
and reusable prototypes containing it use version 6. Ordinary cylindrical and
rectangular components retain versions 1 and 2. Shared schema validation refuses
forged or inconsistent bed/frame/capacity metadata. The local quantity ceiling is
32 million millilitres, covering a maximum 4 × 4 × 2 m cavity. Quantity remains
independent of owner/world display scale, matching existing container semantics.

This is a bounded equalized reservoir. Its rectangular perimeter and opening are
explicit authored cavity boundaries; it does not infer containment from an
imported landscape or generate surrounding solid walls. Separate disconnected
basins need separate owners. It does not simulate river flow, pressure, spilling
across low terrain edges, swimming, aquatic NPCs or water displacement. Follow
routing conservatively avoids the reservoir's outer footprint; actual movement
segments still test its wet triangles. General shoreline route planning remains
open. Missing native bed geometry is unavailable, never silently treated as an
ordinary rectangular pool; observation does not load it.

The explicit physics Start prepares resident free actors and their required inputs.
An enabled world can wait for ground; walking actors prepare their own ground. Running actors
retain nearby collision, ground and water dependencies; unrelated idle areas may
retire through the [shared policy](#retiring-unused-areas);
the current 64-object, 25-metre and four-height-field limits are unchanged. Dense rain-driven level
updates still require Quest performance acceptance before release.

## Finite grounded travel — 2026-10-13

`object.navigate` adds one shared “Walk to a place” action for Maestro and eligible
creations. The original book generates its form from the same native catalog the
agent inspects and programs call. Its destination is an authored foot position;
its output is the actual root position and travelled distance. It owns locomotion
(and Maestro gaze) until actual arrival, failure or cancellation. An elapsed timer
alone cannot produce an arrival receipt. Replaying a completed receipt cannot
start another journey.

Travel reuses the existing `RoomNavigation`, ground motor, water policy and spatial
input preparation. It does not start physics or a scan. Per-object environment
profiles determine whether scanned floor/walls participate: a virtual-only actor
can travel down virtual terrain below the real floor while another actor still
uses the room. Moving the viewed world retains the authored destination. Missing
native inputs hold the last accepted pose without replaying elapsed displacement.
Bounded detours around finite water and solid objects reuse the existing route
search. Unsupported destinations and exhausted searches stop travel. This does
not implement swimming or complete crowd/interior navigation.

Maestro uses the selected walk animation. A created robot can animate its recipe
parts while its root travels; this action does not automatically invent a gait for
an arbitrary imported model. Collision uses an upright body proxy sized from
accepted root colliders, or Maestro's existing proxy, rather than animated limbs.
Physical anchors, terrain owners, connected assemblies and bodies outside the
supported dimensions refuse. The original user hand/controller controls retain
priority. Stop, changed targets, grabs, missing tracking, paused physics and
workspace replacement end the action at the last accepted placement. Successful
steps periodically update runtime placement without adding Undo entries per step.
No walking job resumes merely because saved data is loaded.

Four concurrent walking/following actions, a five-minute lifetime, 0.2–1.2 metres/second and the current
25-metre authored-world boundary keep work bounded. Existing spatial-input waits
and the shared physics dependency retention still apply. This adds no second
navigation or behavior engine. Broader NPC steering,
animation retargeting/foot planting, swimming and large-world residency remain
separate work.

Focused native checks cover created and Maestro travel, actual animation,
interruption, obstacle refusal, water routing, same-place completion, below-room
travel, moved-world coordinates, passive readiness, lifecycle cleanup and bounded
concurrency. The shared headless and original-book journeys also exercise creation,
walking and readback on an explicitly synthetic floor. Their current execution
results are recorded in the delivery plan; these journeys do not claim physical
Quest or real-provider acceptance.

## Solid obstacle detours

Finite walking plans against current solid objects as well as water. Following
can approach normally, then uses the same planner after a prop blocks its next
step. Manual stick movement continues to follow the requested direction and stops
at a blocked step. A finite journey replans when a new solid object blocks an
already accepted path; it never advances through that object while planning.

The planner expands a body's conservative horizontal bounds by the actor radius
and clearance, samples supported candidate points, and checks route edges against
live collision geometry and water policy. Up to 64 discovered solid bodies share
the existing 546-node, 2,048-edge and 128-corner limits. A tick performs at most four
candidate, edge or validation operations with a two-millisecond cooperative work
budget between operations. One operation can contain several native queries and
can exceed that target; this is not a hard real-time guarantee. Pending inputs
still use shared native preparation. Moved/disabled obstacles retire pending candidates, final paths are
revalidated, and every actual step still uses the ground motor.

Discovered solids retain their canonical object owners while a search is pending;
transient collider keys are never saved identities. Cancellation and world changes
release those references through the existing navigation owner. No saved format,
new user control, second planner or automatic region unloading is added.

Accepted fixed imported mesh collision also contributes its actual geometry to
the same asynchronous map, even when its surfaces are not designated walkable.
This lets the map use interior openings, including off-centre doorways that the
outer-bound detour candidates miss. Non-walkable geometry contributes obstacles,
not ground: a building with its own usable floor must explicitly enable walkable
surfaces. Visual-only imports and unpublished previews do not participate.

The map snapshots include structural identity, publication revision and transform.
Changing or removing a structure invalidates the old map before another route
query. Pending builds keep their existing copied-mesh ownership and two-build
limit. The map retains canonical structural owners until its consumer releases it;
stopping Maestro walking/following releases that actor's map. Gaze alone does not
retire another movement owner's inputs. Movable primitive/compound props keep the
existing live detour path rather than requiring every movement to rebuild a map.

Both walking owners check a proposed shortcut before skipping a nearby route
corner. A proximity tolerance cannot cut a turn across the map boundary. Actual
steps still use accepted support, swept body collision and water checks. The
actor's real-room participation remains independent of the backdrop: an actor
assigned virtual ground can use an imported doorway below the real floor.

This is bounded local routing, not general crowd simulation or guaranteed
interior reachability. Door clearance still depends on the actor's body and map
resolution; packed crowds, moving-obstacle prediction and complex multi-level
circulation need further work. The actor can report no route even when a more
sophisticated planner could find one. It must never report arrival from an elapsed
timer or use that limitation to bypass the selected real-room collision policy.
Current verification and physical acceptance status are in the delivery record.

## Waiting for another walking actor — 2026-10-20

Finite `object.navigate` journeys now distinguish an obstruction with an active
walking owner from a stationary prop. If an occupied destination or exhausted
route includes such an actor, the existing movement engine holds its last accepted
pose and retries no faster than every 0.35 real-time seconds, for up to three
seconds. `object.navigation` exposes `waitingForActor` and its reason through the
same passive fact used by book forms, programs and the agent. Waiting keeps the
journey pending; it does not claim arrival or stop the other actor.

Only at least 0.1 metre of accepted travel renews this allowance for a later
obstruction. Replanning and retries alone cannot reset it. A persistent obstruction
fails at the last accepted place; a cleared route resumes through the ordinary
ground, body sweep and water checks without accumulating missed movement.
Stationary props retain ordinary detour/refusal behaviour. Following retains its
existing timed retry policy. Direct stick movement retains its literal direction.

This wait respects both actors' independent environment profiles: an underground
virtual actor need not acquire the real room floor to wait or continue. Grabs,
Stop, lost tracking or physics, and target/workspace changes still cancel through
the existing movement lifecycle. The wait does not transfer movement ownership,
reserve a destination, predict another actor's path or resolve crowded corridors.
Those broader steering decisions remain separate work with bounded, observable
completion requirements.

Native checks cover recovery, expiry, cancellation through the shared capability,
and continued below-room movement. The full headless journey additionally starts
two real walking actors through public action calls and observes waiting, arrival,
independent completion and replay. Execution results and remaining physical
acceptance limits are recorded in the v1 delivery plan.

## Maestro movement handoffs — 2026-10-20

Replacing an active Maestro walk must prepare the replacement's navigation after
the previous owner has stopped. This applies to repeating Follow, switching
between Follow and the avatar movement stick, and taking over a program through
direct controls. A readiness check before takeover is not the replacement's
navigation lifetime: ownership callbacks can release the old route or change its
ground.

The new owner rechecks accepted ground before reporting that it started. If that
ground disappears during cleanup, the replacement is refused and its reservation
and map are released; the interrupted instruction does not resume automatically.
An unrelated upper-body gesture retains its own ownership. Stop continues to
release the walking owner's native references, and an old owner's later End
cannot stop the replacement.

This uses the existing controls, catalog and saved actor environment settings.
It adds no alternate navigation system, saved-data migration or crowd simulation.
Current desktop evidence and device acceptance limits are in the delivery record.

## Shared grounded following — 2026-10-13

`object.follow` exposes “Follow someone” through the same catalog, book forms,
agent calls and visual programs as finite travel. Any eligible created actor can
follow the tracked user or one exact native object. The follower owns locomotion;
the leader remains independently movable, including while held. Maestro also
reserves gaze and retains its selected gait. Recipe-part animations can coexist;
an imported character does not gain an automatic gait, and whole-target imported
animation still conflicts with root movement.

Following and `object.navigate` share one ground motor, route planner and four-action
budget. Both obey the follower's real-room collision and water policies. Thus a
virtual-only robot can follow on terrain below the real floor without changing
which surfaces another character uses. Residency follows the shared dependency
policy below; following does not enlarge current world limits.

Distance is horizontal foot-to-foot separation. Following approaches when farther
away and holds within that distance; it does not push a nearby leader away. The
user's position is projected at the follower's current ground height. An object
leader uses its current root collider bounds. Only connected accepted ground can
be traversed: hovering leaders and different floors may be unreachable. There is
no added crowd avoidance, swimming, head posing or animated-limb collision.

A run lasts 0.1–30 seconds, including waiting for a blocked route. Ordinary program
loops and events express longer behavior without a second scheduler. A bounded
retry observes changed destinations and obstacles, never accumulates movement
while waiting, and never treats elapsed time as arrival. Completion reports the
actual `state`, `reason`, root position and accepted travelled metres.
`object.navigation` passively exposes planning, waitingForActor, moving, keepingDistance, arrived,
blocked or failed for the current shared walking owner. Inactive means no shared
walking owner, not proof that all other motion stopped. Missing objects remain
unavailable; observation never loads them or starts physics.

Grabbing the follower, Stop, target edits, tracking loss, missing/replaced leaders,
workspace changes and lost physics end movement at the accepted position. A held
leader alone does not cancel following. Saved placement survives; an active job
does not restart from saved data. Focused native regression coverage includes
these boundaries, obstacle recovery, independent moving leaders, below-room
following and shared admission. Delivery results are recorded in the v1 plan.

## Imported character walking poses — 2026-10-14

Created imported humanoids can now use an explicitly assigned gait with both
`object.navigate` and `object.follow`. `object.walk.settings` reports the exact
selection and revision; `object.walk.clips` lists the saved model's embedded
clips from validated asset metadata, even after its native instance unloads. The shared `object.walk.select` action chooses an embedded clip, an exact
compatible downloaded library motion, or no walking animation. The generated
book form and agent/program calls use the same guarded save path.

The navigation body remains authoritative. Only accepted movement steps advance
the imported animation; the complete source skeleton and blend shapes are
sampled, and horizontal hip travel is cancelled to keep the image with its body.
Loading waits before moving. Missing/incompatible choices fail without changing
the saved choice or selecting an alternative. Blocked, waiting and arrived
movement restores its starting pose; resuming continues the gait phase. Stop,
tracking loss and target destruction release ownership and restore the pose.

This first imported gait path needs a supported humanoid rig. It does not infer
animal/vehicle gait, create leg motion for an unanimated model, calibrate stride
length, add foot planting or certify limb contacts. Cadence is estimated from
model height (0.65 m/s at 1.7 m, rate limited to 0.25–2). Source vertical bounce
remains. Recipe-part animation is still the existing way to animate a procedural
robot's parts while its root follows. Explicit full-body `animation.play` still
conflicts with locomotion ownership.

Room format 38 permits the existing exact walking references on imported
creations. Copies, Undo/Redo, retained saves and archives preserve those identities.
Reusable prototype version 7 expresses the choice as an optional semantic
`walking` source; model/motion references and existing sound, appearance and
collision resources remain retained. Older prototype formats cannot hide a gait.
Loading, copying and assigning never start movement or playback.
The active room filename derives from its format version, so transactional saves,
archives and recovery cannot retain a separately hardcoded older filename.

The shared world limits, physics admission, running-world retention, per-object
real-room policy and [shared retirement guards](#retiring-unused-areas) still apply. Physical Quest
comfort, imported-model gait quality and foot contact still need acceptance.

## Object lamps and transient controls — 2026-10-14

Point and spot lamps are components of created objects, including imported props
and named recipe parts. The same `object.light.edit` action, `object.lights` list
and `object.light` detail fact serve agents, generated book forms and programs.
No extra provider tool family or numeric rule opcode is introduced. Stable lamp
IDs are local to their owning object. Attachment offsets and rotations are local;
spotlights point along local +Z and range follows uniform attachment scale.

Saved definitions use room format 39 and reusable prototype version 8. Copying,
construction capture, Undo and temporary-room saving preserve their source.
Missing recipe parts, invalid rotations, future definitions and excess budgets
are rejected before publication. Failed durable saves leave the live lights
unchanged. Native light objects and shader globals follow the existing native
resource ownership and cleanup path.

`object.light.control` sets transient on/off, colour and intensity, or resets them
to saved defaults. It writes no room file and adds no Undo entry. Programs can
combine this instant action with existing events, timers, branches and waits.
The exact `stateId` from `object.light` guards competing changes; stale controls
are refused. The command can operate while the lamp's prop is held or moving.
A completed command remains after its caller stops, until an explicit control or
reset. It changes neither saved geometry nor the prop's collision/motion policy.

Runtime controls belong to the workspace, not to a rendered GameObject. Renderer
recreation and unrelated pose/paint edits preserve them. A changed lamp definition,
its removal/Undo, temporary-room entry/discard, reload, workspace replacement or
disabling the editor resets them to source defaults. Tracking/focus suspension
hides emission while retaining controls for recovery. Copies and prototypes use
saved defaults only. Facts are passive: they allocate no lights, advance no
simulation and never turn a source on merely by reading it.

The shared watercolor shader accepts additive local illumination and native hard
virtual shadows. Opaque geometry blocks light; texture-cutout holes transmit it.
Blended surfaces and faded visual layers transmit local light rather than casting
invisible opaque shadows. This is not coloured glass transmission or volumetric
lighting. Authored ambient/sun/day/weather settings remain the existing projection;
this increment does not add sun shadows. Book pages and control lettering keep
their readability. It does not relight passthrough camera pixels or cast shadows
from scanned/unscanned real objects.

Current bounds are two lamps per object, four per room and six reserved shadow-map
faces (point: six; spot: one). Disabled definitions still reserve their cost.
Local intensity is 0–4, unscaled range 0.1–8 metres, and a spot cone is 10–160 degrees.
Shadow maps use 512-pixel faces and the configured 20-metre shadow distance.
These are admission bounds, not measured Quest frame-time guarantees. Larger
worlds still need observation-driven light residency alongside regional streaming;
lighting follows the shared retention and automatic-unloading policy below.
Native `active` reports an enabled light, not proof of visible illumination,
shadows, physical alignment or hardware performance. Current evidence and remaining
acceptance are recorded in the v1 delivery plan.


Native lamp lifetime has a separate process-wide admission limit of eight Light
components and twelve shadow faces. Private replacements, disabled lamps and
retiring components count alongside current lamps across workspaces. Switching a
lamp off does not immediately free its allocation; destruction must finish.
These logical reservations do not measure Unity's cached shadow render targets.

Light creation and replacement prepare before saving or accepting Undo/Redo.
Unchanged lamps reuse their native instance, including across recipe attachment
replacement. Removing a lamp needs no replacement slot. Failed admission or
saving leaves the accepted lamps, saved definitions and history available for
retry. Discarding a temporary room also prepares the saved destination before
ending the temporary room and memory session; a refusal keeps both sessions.

User and agent programs can inspect the same passive `runtime.lightBudget` and
indexed `runtime.lightReservation` facts when `runtimeDiagnostics.v1` is
available. Reservation identity is temporary; saved target/light identity stays
separate. `preparing`, `ready` and `retiring` describe native ownership, and
`active` only reports whether the Light component is enabled in an active
hierarchy. Reading these facts does not load, illuminate or delete anything.


## Lighting dependencies during native loading — 2026-10-14

`world.region.retention` now includes `lighting`. An effective local lamp can
contribute to the observed world while its owning object is off-screen. Its
conservative range envelope retains that owner; hard-shadow lamps also retain
potential casters overlapping the envelope. Disabled and zero-intensity lamp
controls create no lighting demand. Area membership and physical connections
still propagate these dependencies through the existing retention graph.

The primary viewer, prediction/grace interval, scoped cameras, dormant display
preparation and owned virtual captures use the same dependency calculation.
Captures prepare required inputs and wait for imported shadow geometry. Missing
assets or unavailable native lamps fail explicitly, preserving an earlier
published snapshot. A loading model's placeholder is never evidence that its
final shadow geometry cannot contribute.

These envelopes remain separate from occupied visual/collision bounds. Spot
lights use enclosing range bounds; recipe attachments include the swept reach of
ancestor offsets, without copying recipes every frame. Exact scanned-plane
bindings are reprojected through the shared room frame; intentionally hidden
unmatched layers do not keep a capture waiting for an impossible lamp or caster.
Native reads remain passive and do not create lamps, reload assets or edit source.

This is conservative dependency accounting, not a visibility test or proof of
rendered shadow quality. It can retain more geometry than actually contributes.
It does not add reflection probes, sun shadows, shadowing from real obstacles,
automatic unloading or larger scene limits. Whole-world physics admission and
running-world retention remain mandatory. No room/prototype migration is needed;
the new reason is transient and the catalog remains the shared interface.


## Walking-map preparation without synchronous baking — 2026-10-14

Walking-map construction now uses Unity's [asynchronous native builder](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AI.NavMeshBuilder.UpdateNavMeshDataAsync.html).
The existing navigation owner limits the app to two concurrent native builds.
Each job holds its own immutable mesh snapshots; edits may replace the original
geometry while that work finishes. Only a result matching the current accepted
sources can become a route map. A completed map installs in the current room
frame, so moving the world cannot publish it at an obsolete physical position.
Cancellation retires publication immediately and releases native inputs after the
operation drains. No late completion can resume an action or install in a new room.

The current collision geometry remains authoritative during preparation. Short
manual and exact steps may continue when their complete support and clearance can
be checked directly. Planned walking/following waits for a matching map. An
imported authored animation that needs the map retains its first requested time,
restores the accepted pose and pauses its existing playback clock. Later requests
cannot skip the pending interval. The original `avatar.authored.inputs` fact
reports pending readiness while preserving its native dependency detail. Once
ready, the same ground, user-clearance, obstacle and water checks decide the whole
interval before committing a pose. Stop and existing ownership rules still apply.

This extends the navigation system used by the book, agent, programs and manual
movement; it adds no second planner, new action family or saved format. Source
collection, mesh snapshotting, map installation and cancellation still have
main-thread costs. Two jobs is an admission bound, not a measured headset frame-time
guarantee. Whole-world collision admission and current world limits still apply.
Idle-area retirement respects these navigation dependencies; foot planting and
physical performance acceptance remain open.


## Importing reusable sounds

`audio.import` selects a local file or a completed local chat attachment, inspects
it, then requires explicit acceptance of that preview. Both book forms and agents
use this same workflow. Acceptance creates a reusable clip source; it does not
start playback. Bind a source with `object.audioEmitter.edit`, then use `audio.play`
or a program event to play it. Undo removes the source definition while preserving
its private library file for reuse.

Supported imports are PCM/float WAV, Layer III MP3 (CBR/VBR) and a single Ogg Vorbis
stream: mono/stereo, 8–96 kHz, 0.03–600 seconds, up to 32 MiB per selected file.
MP3 and Vorbis are decoded on at most two workers into 24 kHz mono 16-bit WAV for
the existing spatial playback system. The preview shows the normalized asset's
hash, duration and channel/rate metadata. A chat offer's original hash is verified
before conversion; **accept the preview hash**, which differs for compressed
files. Existing WAV inputs retain their exact bytes. Saved rooms and archives
continue to contain only the canonical private WAV files, with the existing
32-file / 128 MiB library budget and bounded long-clip streaming.

The importer validates complete MP3 frames and Ogg page checksums, sequencing and
packet bounds before decoding. Vorbis headers have bounded metadata (64 KiB),
packet sizes (256 KiB), codebooks and a shared 16 MiB conservative setup reservation
budget. Work is cancellable and stops after two minutes. These are supported-input
limits, not a promise to repair arbitrary audio. Opus, chained/multiplexed Ogg,
free-bitrate MP3 and compressed WAV need conversion outside the app first.
Lossy-codec timing depends on available encoder metadata; use WAV for precise
sample-aligned source material. No remote URL, provider, microphone or live stream
is opened by this import action. Live-source adapters remain separate work.


### Change world sound levels together

Read `audio.mix`, then use `audio.mix.control` with `operation: "set"`, its exact
`stateId`, and master/effects/media/ambience levels from zero to one. The book's
**Adjust world sound levels** form loads the same current values. `reset` requires
only the current state ID and sets every level to one. For example, keep effects
at one and reduce ambience to 0.2 while discussing an object. Programs can make
that choice after an event and explicitly restore levels later.

These controls affect authored world sounds. A sound keeps its own gain, pause
state and position; effective native gain multiplies that gain by master and its
emitter's role. Muting does not pause, stop or free a sound. Separate conversation
and web artifact outputs keep their existing controls. Levels are not saved or
undone and reset at room boundaries/editor disable. Changing an emitter's saved
role follows the normal emitter edit rule: its old playback is cancelled, and a
new playback uses that role. Volume controls do not enable microphone access or
make echo cancellation ready.

## Directing Maestro's attention

Use **Look toward something** (`avatar.look`) to choose the user, an exact object,
or a place in authored room coordinates, for 0.1–30 seconds. The book, agent and
programs use the same action. It reuses Maestro's existing head layer and supports
the included avatar and compatible selected Maestro models. The shared
character action below extends this to imported NPCs and named recipe parts.

An object focus follows the centre of its available collision bounds, including
while that object is held or independently moved. Attention owns Maestro's gaze
channel only. Missing, replaced or unavailable targets end it; they are never
silently replaced with a similar object. A point moves with the authored world.
The action turns the head smoothly within 60° yaw and 30° pitch, without walking
or rotating the body. Upper-body gestures can continue independently. Existing
walking and whole-body animation conflicts still apply.

Read `avatar.attention` for the last native sample: focus, exact object ID,
authored point, bounded offsets, and whether the target exceeds the head range.
`applied` is false with reduced motion or before the first gaze sample. It means a
head offset was applied, not that Maestro has eye contact or can see through an
occluding surface. The completion receipt preserves the final sample; after Stop,
the passive fact becomes inactive. Grabbing Maestro, posing, editing, losing
tracking/focus, replacing the model or changing rooms interrupts attention.
Nothing restarts automatically or changes the saved pose.

This attention choice is independent of each actor's real-room collision policy,
visual occlusion and passthrough opacity. For example, Maestro may look toward a
robot on lower virtual terrain even when the robot ignores the scanned floor.
Whether Maestro can walk there is a separate navigation/clearance decision.


## Attention for imported characters and recipe parts — 2026-10-19

**Turn a character's attention** (`object.look`) exposes the same focus
choices to the book, agent and programs. Select **Humanoid head** for an imported
humanoid or Maestro, or **Recipe part** plus an exact part ID for a procedural
robot, eye or turret, for 0.1–30 seconds. A named recipe part does not need an
animation track.

The native rest frame defines forward (+Z) and up (+Y), with the model's actual
bone axes calibrated into that frame. A moving parent carries this frame. The
chosen joint approaches the target smoothly within 60 degrees yaw and 30 degrees
pitch. It overrides only the selected joint's rotation, preserving root navigation,
other joints' authored local poses, the collision proxy and saved recipe/pose.
An imported character can keep its selected walking gait while turning its head.
Other recipe parts can animate independently. Maestro retains its existing head
layer and its existing movement conflicts.

There is one attention owner per actor. It owns gaze and, for a recipe, the exact
part channel. Full-body clips and a second owner of that part conflict. Existing
autoplay on the chosen recipe part must be stopped explicitly; a refused request
does not stop it. Users can combine attention with other behaviour using the
ordinary event/parallel program system, without another scripting engine.
For example, a recipe dog can keep its Tail animation running while its Head
looks toward a ball; these remain independent parts of one program.

`object.attention` exposes the last native sample and the same result shape
as `avatar.attention`. Passive reads do not load or animate anything. If an actor
is saved but its native instance is unloaded, this fact is unavailable rather than
false; `object.presence` distinguishes those states. A resident actor with no
attention owner reports inactive. Only an explicit action acquires required native
objects. Object focus follows the exact object's available bounds, including
when held. Point focus stays in authored
world coordinates. The focus is observed, never claimed or moved.

Stop, authoring, grabbing the actor, a replaced joint/model, lost tracking or focus,
pause and workspace changes end the action. The underlying source joint is
restored before capture and on Stop, unless a newer explicit pose has replaced it.
Nothing resumes automatically. Reduced motion leaves the joint unchanged and
reports `applied=false`. This is joint aiming, not eye contact, visibility
through walls, collision-driven articulation, crowd behaviour or general IK.
The existing terrain/collision and visual-occlusion policies remain independent.

## Retiring unused areas

The active workspace checks for unused connected areas after its normal viewer
sample. It retires at most one group at a time through the existing staged native
transaction. The primary view must be tracked, current and valid. Predictive
view bounds and two seconds of recent-view grace prevent an immediate unload
when the user looks away. A disabled camera, stale tracking, workspace hold or
pending startup is never treated as evidence that an area is unused.

Viewing, capture readback, effective lamps and their shadow casters, held items,
owned actions, paused/playing sound, animation, navigation, connections and
collision dependencies retain their existing native resources. All members of
a named area and enabled connection endpoints stay together. Unassigned home
creations form independent groups when no connection joins them. The book and Maestro remain
world-owned. These checks repeat during preparation; returning to view, an edit,
a changed pose or tracking/focus loss cancels the unfinished transaction. Native
objects remain intact until the whole transaction can commit.

The passive `world.region.retention` report adds `retirementState` and
`retirementIssue`. States distinguish disabled support, waiting for a reliable
view, retained dependencies, ready candidates, preparing work, dormant groups
and failed preparation. A failed attempt retains its loaded objects and does
not restart every frame. Changed relevant input or renewed demand permits a
later attempt. Cancellation retains the preparation owner until its resources
drain, including across immediate disable/re-enable.

Dormant content keeps its exact saved identity and definition, cached placement
and stopped animation state. Looking back or invoking an ordinary shared action
restores it through the existing native acquisition path. Inspecting facts does
not load it. Retirement is runtime resource management, not deletion, a saved
edit or a new Undo entry.

`object.delete` uses the saved identity, so users, agents and programs can remove
a dormant or unavailable creation without loading it first. Active hand,
animation and authoring owners, native preparation/retirement, workspace write
holds still prevent deletion. Dependent definitions keep their exact references
and report the missing target until explicitly repaired or restored by Undo. The included book
and Maestro remain protected. A failed save leaves the definition and Undo
history unchanged. Undo restores the exact saved definition through normal
native preparation; it does not certify that a missing asset can load.
Completed receipt replay cannot delete an object restored by Undo.

An accepted edit only recreates missing views that belong to that edit. Missing
peers remain unavailable across cleanup, Undo and Redo. Unknown imported
geometry keeps the global physics stop until that geometry is restored or
explicitly removed; cleanup never starts physics itself.

This is an initial active retirement policy within the existing 64-object,
25-metre world bounds. Physics Start prepares resident free dynamic actors and their connected,
collision, selected-ground and water dependencies. Unused dormant content stays
unloaded. Fixed, held and animated objects do not initiate free-body preparation;
they can still be required by another actor. A fixed-only world may be enabled
without loading ground or observing an area. The subsequent walking owner
prepares its own ground before displacement; tracking loss or world placement
during preparation ends that movement request. Later-active actors use the same
per-body input preparation before gravity. Cancelled preparations cannot start
late, and unrelated saved edits do not invalidate the captured local inputs.
Once running, each active dynamic body retains its rotating/swept collision
envelope, ground, water and connected physical members. Sleeping and pending
bodies keep their identities and required support; retirement rechecks these
dependencies at commit rather than relying on the previous physics tick.
Unrelated unused areas can retire without pausing a ready body elsewhere.
If an actor's required sweep is unavailable, the environment stays resident.

Dormant geometry keeps its conservative collision guard. Approaching movement
prepares the required native inputs; a guard contact interrupts the affected
dynamic group, prepares its dependencies and requires an explicit restart from
rest. The engine never treats unknown collision as empty space. The existing
global stop remains for resident imports whose collision bounds or placement
cannot be proved. Previously accepted collision bounds follow the same native
identity and current placement while geometry is unavailable. A conservative
native barrier prevents unpredicted impacts from passing through the missing
object; it does not emit authored contact events. Dependent actors stop, while
unrelated actors keep running. Repair requires a new movement owner (grab, or
pause then start physics); failed momentum is discarded.

Saved-room startup now uses the same observation and acquisition path as
returning dormant content. A bounded spatial index prepares canonical bounds
without creating native views. A tracked viewer, its predicted/recent view or an
owned capture can request an area; an explicit capability can acquire its own
required inputs without a visible camera. Independent offscreen areas remain
dormant. Named area membership and enabled physical connections still define
atomic loading groups. Unassigned home objects no longer form one implicit
group. Unknown model
bounds remain conservative; they are never evidence of empty space. Ink whose
exact scan plane is unavailable cannot trigger a useless hidden display load.

Dormant existence does not mean preparation is queued. Ground and water report
actual preparation or unavailable dependencies, and never fabricate support or
dryness. Dormant acoustic geometry uses its existing bounded preparation path
independently of the native display, with the same cancellation and ownership
checks. Viewing content never starts gravity. A review hold suppresses initial
recipe autoplay even when approval precedes the object's first visible load;
explicit playback remains a separate action. Source, identity, personal
placement and Undo history do not change merely because a view loads.

Owned navigation prepares dormant local ground before any displacement, including Maestro direct following and stick input. Neutral input does not request loading, and failed/cancelled preparation cannot produce late motion. Native loading does not reset unchanged movement preferences.

When direct Maestro follow reaches its chosen distance, its active owner keeps
accepted startup preparation and the navigation map continues checking current
ground geometry. Temporary route and step inputs are released until the user moves again;
standing still does not create a fresh startup demand each frame. Stopping or
interrupting that owner releases the preparation too. Unavailable navigation stops follow,
and repairing support or starting physics again does not silently resume it.
A fresh follow command prepares against the current world.

Unknown-geometry isolation, finer grouping within named areas and larger world capacity remain required before claiming country-scale
worlds. Preparation is cooperative across frames; this is not a measured Quest frame-time or
headset quality guarantee.


## Independent home loading — 2026-10-15

Unassigned objects share the logical home region, but no longer require every
other home object to be loaded. Observing or acting on one object acquires its
actual dependency group. Automatic retirement releases only an unused group;
explicit whole-region operations still include every member and its connections.
Named authored regions retain their existing atomic grouping.

Loading and retirement use the same dependency closure and the existing staged
transaction. Preparation cannot silently expand its targets: changed group
membership, ownership, placement, tracking or relevant input cancels it before
native objects are removed. Explicit whole-home preparation also cancels if its
member list changes, even when the old objects' individual groups are unchanged.

The shared `world.region.retention` report keeps logical membership and reports
actual resident/retained counts. A home region may therefore be partially loaded.
A current failure remains visible in `retirementState`/`retirementIssue`, while
other independent home groups can finish. Failed groups wait for changed input
or renewed demand before retrying. No saved IDs, region definitions, program
schemas, limits or Undo history are changed by this runtime policy.

Book and agent inspection read the same saved identity, including dormant or
currently unavailable native objects. Selecting a saved object exposes its recipe
and properties without loading it or changing Undo history. Saved physics
preferences likewise remain readable; they do not imply a running body. Saved
walking-animation choices also remain readable during native model loading; an
unavailable clip is not replaced or started by observation. An active recipe can
still highlight its selected part. Missing parts and deleted objects are rejected;
inspection does not claim that physical actions can run without a live object.

This is a loading-granularity improvement, not evidence of larger-world capacity
or a measured headset performance gain. The existing unknown-geometry gate,
active simulation dependencies and current world budgets remain in force.


## Reusing authored dependency topology — 2026-10-15

The journal keeps one immutable description of object identities, named-area
membership, enabled connections and missing peers. Repeated loading queries use
that description without rebuilding edges. Live retention reasons still belong
to each query: holding, observing or playing one object cannot leak a hold into a
later query or another temporary room.

Accepted additions, removals, connection changes and region membership changes
invalidate the cached description. Undo, Redo and accepted snapshots use the same
publication path. Prepared, rejected and abandoned edits do not publish it.
Runtime placement, appearance, joint parameters, region names and other changes
that leave dependencies unchanged can reuse it. Forks share only immutable data
and diverge when either room accepts a dependency edit. Returned membership and
closure arrays are detached copies, so callers cannot alter the shared cache.

This reduces repeated dependency work. It does not change loading policy, physical
simulation ownership, saved capacity or the existing limits on the world. Live
conditions are still reevaluated before loading or retiring native objects.


## Finding saved objects — 2026-10-15

The book's Workshop has a shared saved-object search. Search name/ID words and
optionally choose a kind, then use Previous/Next to browse. Results include objects
outside the observed area; searching does not load or move them. Selecting a result
uses the existing native inspector. Chat remains the default authoring interface.

Agents and programs use the same `world.objects` version-1 fact:

```json
{"query":"apple","kind":"all","cursor":""}
```

The result contains `worldId`, `offset`, `total`, `next` and `entries` with exact
`id`, `name`, `kind`. Supply `next` unchanged as the next `cursor`, keeping the
same filter. Empty `next` means the result is complete. Pages contain up to three
objects and may be smaller for heavily escaped names to stay within the shared
program-value budget. Never advance by an assumed page size or shorten a name.

A changed directory or workspace/temporary session invalidates old cursors.
Restart at an empty cursor if a continuation is unavailable. Ordinary movement
and loading do not invalidate the directory. Results are a directory, not a live
pose snapshot or permission to edit. Read `object.definition` for current saved
metadata/revisions and `object.presence` to distinguish saved existence from native
readiness. A target not listed on one page must not be treated as deleted. Internal
recipe parts remain inside their owning object.

Saved-object limits remain unchanged. Modern hosts use the bounded context
[described below](#bounded-native-object-context--2026-10-15), while this directory
remains authoritative for discovery. Search does not establish larger-world or
headset performance acceptance.

### Reading one object's observed state

`object.observation` version 1 accepts an exact `target`. It returns the same
lightweight metadata as the room view, even when the object is outside that view:
identity, saved revision, name/kind, position, scale, effective root tint and
runtime flags. `runtimeState` distinguishes active, inactive and unavailable
native instances; `positionSource` says whether the origin is live or saved.
Reading never selects, loads, moves or edits the object. An unknown saved ID is
unavailable; use `object.presence` for explicit saved-existence checks.

The revision guards saved edits, not animation or physics frames. A dormant
object's false playback flag does not say its saved recipe was stopped. Tint is
not sampled pixels or every material. Use component facts for full appearance,
physics, geometry and animation settings. Successful exact observations can
supply reviewed revision evidence to a subsequent shared-client edit; unavailable
reads invalidate that fact evidence. Neither a missing list entry nor an
unavailable read authorizes deletion or recreating an existing object.

## Choosing exact object references — 2026-10-15

Object fields generated from the capability schema now use the shared saved-object
search in the book's action catalog, facts and program editor. Find opens the
same native directory used by the agent. Paging never replaces the selected ID;
objects excluded by the field's schema are visible but cannot be chosen. An
unloaded or off-page target stays selected. Reading the chosen object explicitly
uses `object.definition`, without loading its view or changing the draft.

The displayed read revision is a snapshot, not permission to edit. Changing a
target clears the action's linked-input acceptance. Load current values obtains
the required native revisions again; reusable program inputs keep their existing
explicit Read blocks. Failed/unavailable reads preserve the exact target and do
not silently substitute another. Session and temporary-room boundaries reject
late replies from the previous scope.

Construction selection now uses the same paged directory while keeping an explicit
ordered list and origin; see [construction selection](QUEST_CONSTRUCTION_SELECTION.md).
Capture reads every member revision through the same generated action inputs.
The periodic scene context is not the complete directory. Behaviour target
fields, trigger sources and reference summaries use this same directory.
Inspector context now carries its own native metadata and reviewed revisions; see
[inspection and edit context](#inspection-and-edit-context--2026-10-15). Native
journeys and learner restore now use explicit directory/target reads. These changes
introduce no additional saved schema, catalog operation, capacity increase or
independent browser world.


## Behaviour references use saved identities — 2026-10-15

The simple behaviour editor, trigger bindings, carried-object summaries, program
blocks and module requirements now share the same saved-object reader as the
catalog and agent. A reference outside the displayed inventory remains selected.
An explicit Read shows native saved metadata without loading the object or
changing the draft. An unavailable read retains the ID; it does not establish
that the object was deleted. Hiding the behaviour workspace cancels pending
reference display and prevents deferred searches from starting in a hidden view.

Target restrictions come from each action's existing native capability schema.
Changing action keeps a compatible target; an incompatible target requires an
explicit choice unless the schema allows exactly one object, such as Maestro.
The editor never chooses the first available creation. New behaviours start with
an explicit Maestro gesture. Move and Resize values remain authored values;
changing action no longer initializes them from the periodic scene inventory.

Drafts retain their edits across a temporary-room change but cannot be applied
there, even when the two rooms have equal rule revisions. Reload latest explicitly
replaces that stale draft. Search results and displayed definition revisions are
not execution guards; guarded current-input reads and native validation still
own execution. The shared inspection and edit context below preserves that
distinction. Current world limits remain unchanged.


## Inspection and edit context — 2026-10-15

An inspected object now carries its native metadata with the recipe and revision.
The book and agent can read that exact object even when it is outside the displayed
inventory. Saved placement and observed live placement remain explicitly distinct.
Reading does not load a dormant object, and missing runtime geometry does not mean
the saved object was deleted. Older native hosts retain the matching-list fallback.

User edits and delegated planner commands share a compact set of the object IDs
and revisions present in their reviewed observation, including the inspected ID.
Dispatch does not substitute a newer revision. Every required modern target must
have evidence; nested action resources are included, and a creation alias applies
only to commands after that creation. A missing read prevents the edit and asks
for current saved metadata. Rejected edits keep the draft; session and temporary
room changes require reloading before applying it elsewhere.

Retrieving a prior execution's receipt remains possible after the target disappears.
That request retains the original run ID and uses zero revisions, which cannot
authorize a fresh action. Native receipt handling recognizes the completed action
before edit checks or rejects an unknown/mismatched identity. Undo cannot make an
old receipt delete the restored object again.

The shared read sets below now extend this to multi-object actions and native
selection. Production object context is now bounded; world limits, resource
admission and the independent real-room physics, visual and sound policies are
unchanged.


## Shared object read sets — 2026-10-15

An action may involve several saved objects outside the displayed inventory.
Book and headless clients share explicit preparation for those targets: it reads
only missing exact IDs through `object.definition`, keeps already reviewed
revisions and sends the action only after every required read succeeds. Failed or
mismatched reads send no partial edit. Native guards still reject changes made
between those reads and execution. Preparation does not reserve the objects or
silently refresh an existing guard.

The action catalog retains the evidence from Load current values, including every
nested construction member. Later display updates cannot replace those guards.
Selection controls use the same preparation before selection/handle changes.
Membership comes from the native selection state, not membership in a displayed
page; exact IDs and order remain intact outside the list. A missing display label
can show the ID without treating the object as deleted. Native existence,
readiness, selection identity and action checks remain authoritative.

Delegated tasks use the exact definition reads already returned in that task's
receipts. They do not add hidden discovery calls at dispatch. Evidence cannot cross
a session or temporary-room boundary; an unavailable subsequent definition removes
its earlier fact evidence. Users and agents retain the same native validation and
receipt semantics. An earlier execution's receipt can still be inspected after its
target is gone, without granting a new action.

The bounded production context below builds on these exact read sets. They add no
saved capacity or independent world store. Larger-world quotas and live resource
admission remain separate work.


## Observation-independent controls — 2026-10-15

Opening the workshop uses the native `workspaceReady` signal, which means a content
editor is bound. It does not promise that every object is loaded, that physics is
ready, or that a requested action is allowed. An empty displayed object list does
not mean startup is unfinished. Older hosts retain their complete-list fallback.

Schema-created object fields start unselected unless the schema fixes one exact
identity. Display order, search order and example IDs never select a target for the
author. An explicit compatible target survives a variant change, including when
it is outside the current view.

Selected construction pieces can use the shared Read control to fetch their saved
name by exact ID. This is an optional display read; it does not change membership,
load the object, reserve a revision or replace the action's own reviewed inputs.
The first piece still defines the origin and unavailable reads keep the saved ID.
Standalone inspection and motion searches carry no write guards, so conflicting
old edit evidence cannot prevent a refresh. Fresh edits still require their exact
reviewed object revisions.

Native Headless and Original Book journeys now use explicit saved-directory and
target reads for counts, poses and deletion assertions. Learner restore uses a
closed-run identity receipt from that directory. The production context below can
therefore omit unrelated objects. Saved-world quotas and live-resource admission
remain separate changes.


### Embedded clip discovery without a loaded character

`object.walk.clips` and `avatar.walk.clips` use exact model hashes and source-order
indexes. Import and background spatial preparation retain a bounded, immutable
metadata cache; reading a page never opens a file, loads geometry, reserves a
motion channel or starts playback. A cold/unprepared or invalidated entry is
unavailable, not an empty animation list. Names follow the importer normalization
and remain display data. Duration describes the source timeline; `selectable`
checks its minimum duration only. The guarded selection action separately loads
and validates the actual humanoid model and clip before accepting a choice.
Saved choices and runtime playback availability remain separate facts.


### Bounded native object context — 2026-10-15

A native host advertising `objectOverview.v1` supplies
`objectOverview={version:1,scope:"context",exhaustive:false,limit:4}` alongside
`objects`. This is a few context objects, not the saved directory, a camera view,
or the set of loaded objects. The candidates are book, Maestro, the current
selection and the last inspected saved object. Duplicate and missing IDs are
omitted; other objects are not substituted to fill the view. Native code chooses
these IDs before projecting metadata, rather than building a full list and then
truncating it.

The marker remains non-exhaustive even if a small world happens to fit. Absence
never proves deletion, visibility, inactivity or unloaded state. An empty context
also does not mean an empty saved world. `workspaceReady` continues to distinguish
bound native content from startup/unavailable content. The new marker requires
`objectSearch.v1`; hosts without directory support retain their earlier wire
contract. Malformed, unadvertised, over-limit or contradictory overview messages
are rejected by the shared client.

Use `world.objects` and its continuation cursors to discover saved objects, then
exact `object.definition` / `object.observation` reads for edits and properties.
`object.presence` distinguishes saved existence from native readiness. An
inspection carries its own item and revision even when the overview omits it.
Changing selection does not delete an object or erase the earlier revision
reviewed by a user or agent. The planner and final-reply instructions explicitly
preserve these distinctions.

This projection is passive. It never loads geometry, changes selection, stops a
program, changes collision participation or defines which regions stay simulated.
The saved quota remains 64 creations plus book/Maestro, with existing spatial and
height-field limits. Capacity growth, resource admission and device performance
acceptance remain separate work.


## Procedural native resource admission

Saved recipes and native instances have separate lifetimes. A process-wide
reservation now precedes procedural hierarchy or custom-mesh allocation. The
same reservation follows private preparation, adoption, disabled geometry and
retirement. Unity's deferred destruction must finish for the owned root and
custom meshes before that capacity is available again. Closing a workspace or
cancelling a candidate does not clear its charge early.

The current independent runtime bounds are 128 instances, 512 parts and 524,288
custom-mesh source vertices, including preparation and retirement. These allow
bounded replacement headroom relative to the current saved-world limits; they
are not measured headset memory or performance guarantees. Shared primitive
meshes are represented by their part count, not by custom vertices. Imported
models, collision, terrain, ink, lights, materials and textures have separate
costs. Authored collision geometry has its own process-wide admission limits,
described below.

If admission is full, an edit fails before persistence and keeps the accepted
geometry, playback and Undo history. Dormant activation also preserves the saved
object on failure and can be retried after resources become available. The
runtime never silently deletes or evicts an unrelated object to satisfy a
request. Human and agent inspection share `runtime.proceduralBudget` and
`runtime.proceduralReservation` through the existing catalog. Reading those facts
is passive; `ready` does not prove visibility or running animation.

This guard does not increase saved capacity or the supported world extent.
Remaining saved/runtime limits and persistence costs must be separated before
those can grow.


## Authored collision resource admission

Authored compound shapes, accepted height fields and rigid imported-model
colliders share a process-wide reservation before native geometry is allocated.
It holds through preparation, adoption, disabled geometry, replacement overlap
and deferred destruction. Separately owned compound and terrain meshes must
also finish destruction before their reservation is released. Imported meshes
retain their separate model ownership; their collision components are charged
here without taking ownership of those shared mesh assets.

The independent limits are 256 retained entries, 2,048 collider components and
1,048,576 source triangles. These bound temporary overlap as well as accepted
geometry; they are not measurements of cooked PhysX memory, frame time or Quest
capacity. Saved-room collision/terrain quotas are unchanged. Default object,
book and avatar proxies, room scans, navigation and other system colliders are
outside this ledger and need their own budgets.

Failure leaves the accepted geometry, saved state and Undo/Redo history intact.
A failed dormant activation keeps the saved object available for a later retry.
Nothing is automatically evicted or deleted to make space. If actual prepared
colliders exceed the evaluator's reservation, preparation is refused and the
larger cost remains charged until rollback finishes destroying that geometry.

User and agent inspection share `runtime.collisionBudget` for limits and usage,
`runtime.collisionResources` for phase totals and `runtime.collisionResource`
for individual owners. Reads are passive and return detached metadata. A ready
lease records accepted ownership; enabled/active collider counts do not prove
simulation, contact or visible geometry. These resource limits do not change
which real-room surfaces an entity collides with or its virtual-world terrain.


## Native window preparation and lifetime (2026-10-16)

Window masks and their per-view shared quad/material bundle now prepare before
journal acceptance and immediate-save persistence. Prepared masks are detached,
inactive and render-disabled. Preparation never creates a window view or requests
passthrough. The complete accepted visibility state is applied before the native
view can request the underlay; a hidden opening does not briefly enable it.
Passthrough readiness remains a separate observation, not a consequence of
successfully preparing geometry on a desktop.

Independent process-wide limits admit 32 mask roots and 32 shared bundles across
all workspaces, including private preparation, inactive owners and deferred
destruction. The saved limits remain four windows per object and sixteen per
room. A bundle owns the quad mesh and material shared by one native view's masks.
The last window's removal releases that bundle as well as its masks. Its charge
remains until owned native objects are actually destroyed. Never-active owners
are also reaped. These are logical allocation counts, not measurements of RAM,
GPU memory, compositor resources, garbage awaiting collection or frame time.

Stable window IDs reuse their masks when reveal, rectangular/elliptical shape,
plane size or anchor changes. Rebinding detaches retained masks before an old
surface is destroyed. Surface roots still retain their ink and windows when
recipe parts are rebuilt. Visual-only window edits consume prepared surface
anchors before attaching masks, including explicit recovery of a missing surface
view. Unrelated reconciliation does not recreate missing peer windows. Removing
windows needs no replacement capacity; replacing an identity or restoring missing
native geometry can require spare capacity until old resources finish retiring.

Failed preparation, a later batch refusal or an immediate-save failure preserves
accepted source, views and history. Undo/Redo, temporary-room discard and dormant
activation use the same preparation path and retain retry on refusal. Native
allocation does not change the older queued-save entry point's persistence
policy. A candidate can be adopted only by its exact target with unchanged window
and referenced-plane definitions and surviving anchors.

`runtime.windowBudget` and `runtime.windowReservation` expose passive typed
ownership diagnostics through the same user/agent catalog. They require the
runtime diagnostics service. A bundle has an empty window ID; mask reservations
carry the stable window ID. Runtime reservation IDs and indices are not saved
identities. `ready` means native ownership was transferred, which can include an
unpublished activation candidate. `active` describes the host GameObject, not
visibility or passthrough readiness. Reads cannot allocate, evict or enable
passthrough. The native lifecycle fixture and verification scope are recorded in
[the v1 plan](QUEST_V1_PLAN.md#transactional-native-windows-2026-10-16).


## Native physics preparation and lifetime (2026-10-16)

Authored objects reserve their Rigidbody and owned PhysicsMaterial before a new
native root is constructed or an edit is accepted. Enabled physical connections
reserve a potential joint slot before persistence, including connections that are
paused, broken, unaligned or waiting for their collision/input data. Existing
bodies and connection slots are reused for ordinary edits. Disabled or removed
connections need no replacement slot.

Independent process-wide ceilings are **128 authored bodies/materials** and
**32 potential connection joints**, across workspaces. These are logical native
ownership limits, not measurements of PhysX memory, solver cost or Quest frame
time. Saved quotas remain 64 creations and 16 connections per room. Built-in
book/avatar/tools, tracked pushers and other system bodies are outside this
ledger; broadphase/contact data and simulation/query work also need separate
bounds before saved capacity increases.

Private preparation, inactive objects and deferred destruction remain charged.
The body lease follows the private root even before Rigidbody construction so a
failed, never-active build can be reclaimed. A connection keeps its slot during
pause or break. Replacing its native Joint waits for the previous Joint to be
actually destroyed; the same slot cannot own overlapping native joints. A body
slot is released only after its Rigidbody and owned material are both gone.
Potential slots with no native Joint can be released immediately.

Body preparation transfers only to the exact object ID and kind. Connection
preparation validates the exact target, connection definitions and existing view
owner before transfer. The same path covers ordinary edits, immediate saves,
Undo/Redo, temporary-room restoration, startup and dormant native activation.
If capacity is unavailable, the accepted document, saved bytes and history remain
intact and the operation can be retried. No existing content is silently evicted
to make room. Unrelated reconciliation does not create a missing peer's connection;
an explicit edit of that peer can prepare and repair it.

Admission does not start physics or alter accepted ground, per-entity real-room
participation, ownership, collision-input readiness or throw semantics. A joint
still requires aligned, available members and the existing physics readiness
checks. It holds its affected actor while waiting to recreate the native joint.
This preserves the distinction between an actor that follows authored terrain
and one that collides with scanned room surfaces.

Shared `runtime.physicsBudget` and `runtime.physicsReservation` expose detached,
passive facts to human and agent programs when runtime diagnostics are available.
Reservation IDs and indices are runtime identities, never saved references.
`ready` means ownership transferred, including unpublished or paused candidates;
`active` describes the host GameObject, and `allocated` describes whether its
Rigidbody or Joint still exists. None certifies that an actor is simulating.
Reading these facts cannot start physics or modify saved content. The captured
native lifecycle fixture and verification scope are recorded in the
[v1 plan](QUEST_V1_PLAN.md#transactional-native-physics-2026-10-16).


## Liquid preparation and lifetime (2026-10-16)

A container reserves its surface mesh, two materials and both surface/stream
renderers before an edit is accepted. This includes empty containers, so filling,
rain and pouring reuse admitted resources. Contents, colour and cavity edits
reuse the same owned instances. Removing the container releases them. There is
one independent process-wide limit of **32 native liquid owners**, across
workspaces; the saved quota remains **16 containers per room**.

Private preparation, inactive owners and resources awaiting Unity destruction
remain charged. An aborted candidate or a destroyed, never-active host is
reclaimed on registry collection without requiring its Update/OnDestroy callbacks. Exact target,
container definitions and existing view owner must still match at transfer.
Immediate saves, ordinary edits, history, temporary-room restoration and dormant
activation share this preparation. Admission refusal or failed immediate saving
preserves accepted contents, native views, history and saved bytes; the operation
can retry after capacity is freed. The older queued-save entry point retains its
existing persistence policy.
Unrelated reconciliation cannot allocate a lost peer's liquid view.

All admitted live media fit the independent native capacity. An inconsistent
capacity reports unavailable input rather than returning a silently truncated
water set. A missing or disabled native water view likewise remains an explicit
spatial dependency for affected physics and movement; absence cannot mean dry
space. This retains the existing cavity relevance, live quantity, gravity and
terrain checks. It does not enable physics, change actor collision policies or
alter volume accounting/publication.

`runtime.liquidBudget` and `runtime.liquidReservation` expose copied passive
ownership facts to human and agent programs with runtime diagnostics available.
A ready owner may still be empty, inactive or unpublished; it does not certify
water availability or simulation. Allocation fields report native root existence,
including deferred destruction. Reads cannot create water or load missing views.
These are logical ownership limits, not measured GPU memory, solver cost or frame
time. Per-tick simulation/query work, managed geometry buffers and engine internals
need separate bounds before saved capacity grows. Verification is recorded in the
[v1 plan](QUEST_V1_PLAN.md#transactional-native-liquids-2026-10-16).


## Free-body physics work admission (2026-10-16)

A free physics body reserves potential liquid-force work before gravity or
buoyancy can advance it. This is a separate process-wide allowance from saved
object quotas and native body, collider and liquid allocations. The limits are
128 grants and 524,288 logical units; a work-unit limit can be reached before
the grant limit. Each body reserves its bounded displacement sampling, native
and missing-medium candidate checks, and participant environment proofs, even
when its current position is dry. A proof or candidate can contain additional
bounded geometry work. These units are not CPU instructions, measured frame
time, memory or a PhysX solver budget.

The eligible free members of one physical connection group reserve together.
Existing grants receive credit during a group merge or demand change. Refusal
cannot partially admit new members, resize some members, or evict unrelated
accepted work. A changed collision count or world/target owner invalidates a
member's old grant until complete group admission succeeds. A fixed anchor does
not connect the work of otherwise independent moving children. Existing spatial,
connection, ownership and per-entity real-room collision policies still apply.

Missing work capacity holds the affected free bodies before PhysX or water
forces and exposes a retryable reason through the existing physics-input facts.
It cancels pending spatial-input momentum as well: capacity returning must not
replay an old throw. Preparation retries while physics remains requested;
returning capacity and passive observations cannot restart paused physics.
Pausing, grabbing, animation ownership, deactivation and destruction release a
body's potential-work grant. In the fixed-step force path, no displacement or
force sampling runs for a body without a current grant. Contact detection has a
separate admitted pass, described below, that can populate the shared displacement
cache; this force allowance alone is not a global cap on all displacement work. Saved water quantities
are unchanged. A retiring native body stops immediately and cannot reacquire
work or accept motion commands while its GameObject awaits destruction.

`runtime.physicsWorkBudget` and `runtime.physicsWorkReservation` are detached,
passive facts shared by human and agent programs. A member's `current` grant
means its current demand/owner matches; it does not certify that its complete
group, geometry, spatial inputs or simulation is ready. Standalone bodies have
empty world/region/target scope. Indices can move and reservation IDs are only
runtime identities. Reads may collect ineligible grants but cannot admit work.

This admission applies to the fixed-step free-body liquid-force path. Pouring,
scooping, rain, contact events, navigation, external observation queries,
proof/geometry internals and engine solver work remain separate. Coupled flow
needs complete-episode admission before any transfer, with unknown skipped
contact baselines and no catch-up replay. Saved capacities remain unchanged;
transaction/storage/spatial bounds and device measurements are still required
before increasing them.


## Water contact work admission (2026-10-16)

Each resident room's water-contact pass reserves its complete potential sampling
work before shared displacement capture or contact geometry queries. Its
independent process-wide limits are 32 sampling hosts and 524,288 logical units.
The reservation includes every registered non-built-in body, including fixed,
held, animated and temporarily unavailable bodies; up to 64 displacement samples
and 64 colliders per body; six Maestro joints; and both tracked inputs. Native
and known-missing liquid candidates, swept surface crossings and participant
proofs are included as bounded allowances, even when a participant is currently
dry. Nested proof/query internals and engine costs are excluded. Logical units
are not measured CPU instructions, memory or a frame-time guarantee.

A changed demand or world/region owner makes the old reservation stale. Admission
credits that host's previous cost and changes the entire reservation atomically;
refusal preserves other hosts and the old charge, without sampling a prefix.
Pausing, deactivating or destroying a host releases its allowance. The existing
manual internal tick remains available for deterministic desktop tests while
the automatic loop is disabled; inactive roots cannot step.

Waiting makes every contact unknown, clears ripples and discards previous sample
positions. A successful retry establishes a fresh baseline: movement during the
gap cannot manufacture entry, exit, crossing or catch-up effects. Returning
capacity, focus or passive diagnostic reads cannot restart paused physics.
This observation allowance does not take hand/animation ownership, stop otherwise
admitted body forces, change liquid quantities or hold the room's write gate.
Events are delivered only after the full admitted pass. If a listener replaces,
synchronizes, edits or rebases the room, remaining callbacks from that sampled
source are discarded, including when object IDs are reused.

Human and agent programs share `runtime.contactWorkBudget` and
`runtime.contactWorkReservation`. Reads are detached and passive; `current`
means the potential cost and copied scope still match, not that geometry,
tracking or any particular contact is known. Existing contact-state facts expose
unknown and the waiting reason. Real-room and virtual-ground proofs still apply
separately to each participant: a virtual prop or Maestro may use terrain below
the scanned floor without giving physical hands that same scan opt-out.

Pouring, scooping and rain-transfer episodes need separate admission before
quantity changes. External observation queries, navigation, proof internals,
engine work, transaction/storage limits and larger world capacities also remain
separate work. This checkpoint establishes contact consistency under refusal;
it does not certify Quest performance or complete the world scheduler.


## Liquid flow work admission (2026-10-16)

Each resident room requests a complete pouring, scooping and rain pass before
querying transfer geometry or changing live quantities. Process-wide limits are
64 queued/active requests, 32 simultaneous opportunities and 524,288 logical work
units, independently of saved or native object capacity. Demand counts all registered vessels, including empty receiving cups
and temporarily missing views. It includes potential vessel pairs, 32 stream
segments, bounded hit/candidate searches, five scoop paths and five rain samples.
This is a logical allowance, not measured CPU time, memory, frame time or PhysX
cost. Geometry/proof internals and external observations are not included.

Request identities are stable and scoped to the world/region; execution grants
rotate fairly on the shared early fixed-step opportunity. Resizing within a turn
credits the old charge and either admits the complete new demand or preserves the
old charge. A caller cannot evict another host or transfer through a partially
admitted room. The next opportunity prioritizes older unserved requests.
Moving free containers and their physically connected groups need the same
admission before gravity resumes. An unrelated dry body can keep running.
Hands and authored animations keep ownership; admission cannot restart paused
physics. Disabled automatic loops retain explicit internal stepping for desktop
tests, while inactive roots cannot step.

A scheduled delay preserves the live episode and its input residency, hides
streams and makes rain exposure unknown. Connected free bodies retain valid
motion while held for execution. Further ticks wait without transferring or
accumulating skipped time. A request that cannot enter the bounded queue instead
finishes previously accepted quantity changes and releases its writer. If that
save fails, the entire unfinished episode reverts, emits no success events and
requires an explicit physics restart. Waiting does not become a permanent
geometry failure. Saved quantities retain the existing single-episode Undo
behavior. Admission is separate from geometry proof: a current work grant
alone does not make an opening, path or receiving vessel ready.

Completed events copy accepted amounts and liquid identities before committing.
The episode releases write access before delivering callbacks, so a user/agent
rule can edit the world or start another episode. Synchronization, accepted edits,
world-frame changes or a new episode stop the old batch's remaining callbacks,
even if object IDs are unchanged. Old cleanup cannot erase the newly started
flow. These events report accepted changes; they are not replayed after a pause.

The shared catalog exposes `runtime.flowWorkBudget`, `runtime.flowWorkReservation`
and `runtime.flowWorkSchedule` as detached, passive diagnostics.
`object.container.live`, `object.container.scooping` and `object.container.rain`
can report `phase=waiting`. The live fact exposes the reason; rain reports
`ready=false` and zero exposure until a fresh admitted pass. Existing types and
versions remain unchanged for the existing observations. Reads may retire
ineligible requests but cannot advance selection, admit work, load geometry,
resume physics or publish a flow episode.

Per-entity collision participation is unchanged: the real room may constrain a
cup while a virtual NPC uses terrain below its floor. Visibility/occlusion and
audio remain independent of collision choice. This checkpoint does not establish
unlimited world size or Quest performance; navigation, observation/proof/engine
work, per-group metering, coordinated fairness across other work classes and
transaction/storage bounds remain release work before increasing saved capacity.


## Water contact continuity (2026-10-16)

A valid sample expires after 0.2 seconds and tracked input after 0.15 seconds,
using real elapsed time even within a slow frame. A missed interval, tracking
loss or hand/controller source change invalidates the old baseline immediately.
Resuming establishes the current contact first, with zero inferred movement;
entry, exit, crossing, speed and ripples cannot be reconstructed across the gap.
A hand/controller source roundtrip still breaks continuity even when it happens
between two sampling passes. This also applies to props and Maestro's joints.

Queued contact events retain the sampled participant and water source. Before
each callback, the participant's identity, pose, tracking update and ownership,
and the water's identity, geometry, transform and live quantity must still match.
If an earlier rule changes one participant or source, its stale event is skipped
and its contact becomes unknown; independent valid participants still deliver.
A nested sampling pass or room/frame change supersedes the remaining old batch.
Edge ripples are added only for events actually delivered. Continuous valid
submerged movement still refreshes ripples without emitting extra events.

Avatar checks retain the sampled joint positions directly in world coordinates,
so an unchanged pose is not rejected by room-coordinate roundtrip precision
when a world is translated, rotated or scaled. The authored-coordinate history
used for subsequent movement remains separate.

The existing `object.medium.contactState`, `input.medium.contactState` and
`object.medium.contact` definitions describe these rules in the shared catalog;
there are no new fields or version changes. Unknown observations clear derived
body, liquid, depth, speed and surface values and provide a reason. Human and
agent programs consume this same result and can choose to wait for fresh data.
These are bounded sampled contacts, not continuous skin or fluid simulation.
Per-entity real-room/virtual-ground policies and liquid quantities are unchanged.


## Liquid spatial dependencies (2026-10-16)

Before a physical pour, scoop or rain pass changes quantities, the flow owner
prepares its relevant spatial inputs through the same owned demand used by
navigation and contact. Potential stream bounds, full cavity bounds and the
five rain-cover paths identify saved solids and ground. Empty receivers count,
including cavities offset outside their visible artwork. Dormant dependencies
are loaded and retained; unavailable collision or liquid views cause waiting.
Unrelated distant dormant objects are not loaded merely because flow is active.
Exact native intersection and collision queries still decide the actual result.

Quantity-changing paths form complete interaction groups, including transitive
cavity overlap, possible streams and connected moving bodies. A group cannot
transfer, spill or collect rain while its required geometry is unavailable.
Independent ready groups can continue while another waits or loads. Sharing a
read-only solid obstacle or roof alone does not join their quantity dependencies.
Missing ordinary containers use their retained or anchored pose and the same lip
geometry as live pouring; nearby circuits need not merge merely because a view
is dormant. Unknown cavity bounds conservatively join groups. An unavailable
terrain bed retains a broad possible-stream envelope until its geometry loads.

All groups retain one shared journal episode and atomic save/rollback. Regrouping
already-ready inputs preserves that episode, including a bucket moving between
basins. If an owned group needs unavailable inputs, the prior episode publishes
once and releases its writer before callbacks/preparation. Continued
waiting does not repeatedly checkpoint independent flow. A failed save reverts
the episode and requires a physics restart. Callbacks can start fresh flow without old cleanup
erasing it. Pause, loss of focus or an inactive world cancels its demand; a load
cannot resume physics. Readiness notifications from successful loading do not
cancel their own preparation or clear a failed-save stop. Recovery uses the current short timestep, without
replaying missed pouring or rain.

`object.container.live`, `object.container.scooping` and `object.container.rain`
use their existing `waiting` phase for missing spatial inputs as well as work
capacity. The live observation provides a reason; rain remains unknown with zero
exposure until a fresh eligible pass. These passive facts cannot load objects,
publish changes or start physics. Types, versions, saved inventory, collision
policies and fluid quantities retain their existing contracts.

Preparation covers possible eligibility changes within the same pass. A tilted
empty receiver can receive water and then pour during its own turn; its potential
stream must be ready before the earlier donor transfers anything. The stream
bound uses the cavity's retained capacity at its current pose, with the same
geometry as actual pouring. An upright dry cavity does not acquire stream-only
obstacles. Rain-facing cavities prepare cover even when initially full or holding
a different liquid, because earlier pouring/scooping can drain them. Actual
collection still checks the resulting amount, liquid identity and exact cover.
Preparation alone neither changes contents nor creates rain intake.

This does not add fluid particles or claim unlimited simulation. Spatial bounds
are conservative. Work-capacity admission covers the whole liquid host because
the bounded transfer pass still scans all registered vessels. Complete hosts now
take fair turns as described below; independent spatial groups are not yet
separately metered. Measurement of query, loading and native-engine costs remains
required before increasing world limits.
Rain collection dependency checks do not establish complete weather presentation
or acoustic acceptance.


## Fair liquid simulation opportunities (2026-10-16)

The runtime keeps at most 64 liquid-work requests and grants at most 32 complete
host passes within the existing 524,288 logical-unit allowance. Older unserved
requests get priority, then stable registration order. Eligible continuing work
cannot retain the entire allowance indefinitely. Oversize and queue-full requests
are explicit refusals; fairness applies to accepted requests that fit the limit.
The current maximum remains 16 saved containers per room. A grant includes every
interacting group in that room, rather than granting a prefix of a cascade.
Discovery and execution retain the previous conservative whole-host cost.

An early fixed-step clock selects opportunities at approximately 20Hz, keeping
each through at least one rendered frame. Moving containers and connected bodies
check that same grant before gravity. The automatic liquid loop samples once per
opportunity and never backfills skipped pouring/rain time. This is a bounded
scheduling contract, not a measurement of CPU, PhysX or device frame time. Internal
manual test ticks remain explicit simulation steps, not a public agent API.

Waiting for a turn preserves native input residency and the current liquid
journal episode. It does not publish an Undo entry each time the grant rotates.
Valid free-body velocity is held and restored after a fresh complete input check;
grabbing, teleporting, replacing geometry or stopping physics invalidates it.
A genuine unavailable input, failed publication or lifecycle stop keeps its prior
stricter rules. Scheduled holds do not change the contact identity solely because
execution is deferred. Rain exposure becomes unknown until the next actual pass.

User and agent diagnostics share `runtime.flowWorkSchedule`: the current step,
granted execution count, queued count and limits. Existing `runtime.flowWorkBudget`
fields/types remain; reservations now include pending registered requests, while
units count current grants. `runtime.flowWorkReservation.current` additionally
requires this opportunity's grant. IDs are runtime-only; no saved world identity
or capability-specific replica is introduced. Passive reads cannot advance the
clock, claim a turn, load objects or start physics.

Pressure verification uses competing logical consumers of a real native fixture;
it does not claim that 64 fully provisioned rooms fit Quest memory. Native object,
liquid, geometry and saved-world caps remain separate. Fairness for other work
classes and per-group metering remain open before expanding saved capacity.


## Coordinated physics and liquid opportunities (2026-10-16)

Physics and liquid work share one opportunity clock and one dependency graph.
Every connected moving group is indivisible. Because the current liquid pass
scans all of a host's vessels, all registered moving vessel groups using that
pass are conservatively joined for admission, even when their native joints are
separate. The scheduler grants that complete dependency group both its force
allowance and liquid allowance together. Dry unrelated bodies remain separate.
An accepted finite, unchanged group fitting both limits gets a turn under
continuing demand, ordered by oldest unserved opportunity and registration order.

The existing limits remain: 128 physics requests, 64 liquid requests, at most
32 simultaneous whole-host liquid passes, and 524,288 logical units in each work
class. Request queues and booked execution allowances are distinct. A group too
large to fit, an incomplete dependency or a full queue is an explicit refusal;
scheduling does not promise to make an impossible group run. Current saved-world
and native-resource limits are unchanged. This does not measure PhysX, geometry
proof internals, frame time, thermal behavior or Quest memory capacity.

A normal wait preserves spatial input ownership, pending loads, valid body
velocity/spin, intact native joints and their drive references, and the live
liquid journal episode. It does not create a contact identity, replay an impulse,
add an Undo entry or accumulate missed force/flow time. Real geometry, placement,
connection, ownership and lifecycle changes retain their interruption rules.
Geometry preparation is independent of the short execution allowance; actual
movement and medium forces require both current inputs and complete admission.

Saving a completed physical movement is an owned position checkpoint. Prepared
continuous physics/flow inputs acknowledge only its exact before/after object
revision, then continue to recheck live spatial proofs. Ordinary action guards
and unfinished native acquisition remain strict. An intervening author edit
cannot be acknowledged away or revived by a later checkpoint.

`runtime.physicsWorkSchedule` and `runtime.flowWorkSchedule` expose the same step.
Both report booked allowances and waiting requests, independently of readiness:
a booked allowance can be stale until refreshed. A reservation's `current` flag
requires the full coupled work group and matching member demand/owners, but does
not certify native geometry or actual motion. Passive observations cannot change
priority, claim an allowance, load geometry or start physics. User and agent use
the same typed facts; no saved schema or separate agent world is introduced.
An intact connection can report `phase: waiting` with `active: true`: the native
constraint is retained, while movement is deferred. Use `object.physics.inputs`
to inspect actual movement readiness; the presence of a joint alone is not proof
that its bodies are currently simulating.

Whole-host coupling remains a conservative bound for preparation/publication.
Group-local quantity enumeration and a separately accounted discovery allowance
are described below. Finer execution admission still needs stable group fairness.
Contact sampling takes independent fair turns on the shared
clock; see [Fair water-contact sampling](#fair-water-contact-sampling-2026-10-16)
for observation continuity and missed-event rules. Broader engine, navigation,
external observation and storage/history work also remain bounded separately
before larger authored worlds can be supported.


## Fair water-contact sampling (2026-10-16)

Continuing contact samplers could reserve the entire allowance indefinitely,
leaving a later room unable to observe water contacts. Complete contact passes
now take fair, independent turns on the same clock as physics and liquid work.
Waiting for a contact sample does not hold unrelated motion or liquid transfers.
The request queue holds up to 64 passes; the execution limit remains 32 and the
logical work allowance remains 524,288 units. Saved/native limits are unchanged.

Automatic sampling runs at most once per opportunity. A missed opportunity makes
contact observations unknown and establishes a fresh baseline when sampling
resumes, without invented entry/exit/crossing events or speed. Existing ripple
visuals can finish naturally during a scheduled wait. Speed accounts for at least
the observed elapsed interval. A new opportunity cancels the remaining old
callback batch; tracking, exact pose, geometry, ownership and source edits retain
their existing validation. `runtime.contactWorkSchedule` and the contact facts use
the shared typed catalog. The waiting reason now fits the existing 128-character
program value limit, verified through both native and web fact readers.

A request or booked allowance is not proof of a known contact. Read
`input.medium.contactState` or `object.medium.contactState`; `known: false` means
unavailable, never dry. A newly booked allowance cannot make a missed sample
current. A short valid interval may use the last sample until refreshed; existing
0.2-second sample and 0.15-second tracking expiry still apply, with real-time
expiry and exact-pose checks before callback delivery.

The scheduler orders complete passes by oldest unserved opportunity and then
registration order. A finite unchanged fitting request receives a turn while
other eligible consumers continue. Passive reads and request-call order cannot
advance the clock or execute sampling. Oversize passes and full request queues
are explicit refusals. Observing or waiting does not apply forces, change water
quantities, add history, acquire native geometry or restart paused physics.

Each pass is still charged for its whole room, including held/animated objects,
six avatar joints and two input sources. Logical sample/candidate/proof allowances
are not measured CPU time, PhysX cost, frame time or Quest memory. Group-local
metering and separately bounded discovery/geometry work remain necessary before
larger worlds can be promised.


### Physics wait and refusal facts

`object.medium` remains readable when capacity is unavailable or a complete
physics/liquid group exceeds it. Its human-readable reason fits the shared
program text budget. `applied: false` does not prove that an object is dry; use
the contact/medium facts according to their availability and sampling contracts.
Waiting, refusal, resumed simulation and pause use the same fact for user and
agent programs. This diagnostic repair changes no physics or saved data.


## Complete liquid groups and quantity execution (2026-10-16)

Each liquid pass first prepares complete dependency groups. Containers that can
exchange quantities, including empty receivers and transitive cavity paths,
belong together. Read-only solids and rain cover remain required spatial inputs
without merging otherwise independent quantities.

Pouring, immersion, scooping and rain then enumerate those complete groups.
Stream receivers and scoop donors are searched only among the group's members.
A waiting circuit does not enter the candidate loops of a separate ready one.
Moving a container rebuilds membership before the next pass; an incomplete
prepared group never executes partially. Pour, scoop and rain retain their phase
order and one shared journal episode, publication and Undo.

The scheduler still charges the whole host conservatively and couples moving
liquid dependencies at that scope. This change does not promise smaller grants,
more saved/native objects or higher headset frame rates. Actual candidate-visit
samples are internal execution evidence; they exclude discovery, spatial proofs,
engine work and room-wide cleanup/publication. Discovery admission is described
below; complete-group execution scheduling with stable fairness remains open.


## Liquid discovery and shared admission (2026-10-16)

Before discovering liquid dependencies, the runtime reserves a logical discovery
allowance using saved object and container counts. Dormant objects and empty
containers count too. Discovery receives an independent grant before quantity
execution is requested. Required native inputs can load while execution waits.
The shared clock chooses discovery grants first; moving physics and liquid
execution then receive their complete execution allowances together, gated on
current discovery readiness. Discovery can be requested before geometry exists,
including when physics prepares first. When discovery itself has no capacity,
the liquid pass waits without capture or liquid-driven native loading.

A count or owner change requires a current grant for that stage. An unsuccessful
increase retains that stage's earlier booked charge without evicting another
owner. Discovery and execution are separate requests; cyclic cross-stage
dependencies are rejected. An unavailable prerequisite keeps execution queued
without claiming it has run. A preparation wait retains service age and valid
physics momentum; already booked execution remains charged until the next
opportunity. Reads do not advance priority.

Each admitted discovery captures collision bounds once and reuses them only
inside that synchronous pass. Subsequent passes read current placements, so
moving an obstacle or container is reflected before the next transfer. Unknown
or missing geometry remains conservative; a snapshot is not permission to ignore
unavailable dependencies.

The shared runtime.flowDiscoveryBudget fact requires runtimeDiagnostics.v1:
step is the shared opportunity; reservations include waiting claims; executions
count booked grants; units is their conservative logical charge; unitLimit is
524,288; objectLimit follows the existing 64 creations plus book and Maestro.
A booking does not certify discovery, loading or simulation completed. Passive
reads cannot acquire geometry, change quantities or start physics. User and agent
programs use the same typed record.

These units cover bounded metadata/candidate/trajectory and connection visits.
They exclude geometry/proof internals, sorting, allocation, native loading,
engine costs and measured frame time. Discovery still examines the host inventory;
execution charging and preparation/publication remain conservative whole-host
work. Saved/native capacity is unchanged. Larger worlds still require complete
group scheduling, broader cost accounting and real-device evidence.


### Saved and native container limits

The current saved format permits **16 liquid containers per room**, within the
separate limit of 64 creations plus book and Maestro. The process-wide native
liquid limit is **32**. A dormant container still belongs to the saved inventory
and discovery; it does not own a live surface or stream. None of these counts is
a frame-time or memory guarantee.

A desktop regression provisions nine independently saved native rooms with
16 containers each: 20 live containers and 124 retired containers in total.
Whole-host execution requests total 552,816 logical units against the 524,288
allowance, while discovery requests total 77,670. Eight rooms initially receive
a turn; the ninth waits, then progresses under continuing demand. Water,
dormancy and per-world Undo survive the wait and a real receiver's 1+3 / 2+2
group changes. This is shared-process contention evidence, not a nine-room user
workflow, a larger single-world format or Quest performance acceptance.

That historical whole-host pressure result is superseded by complete-group
execution below. Discovery still runs before execution admission. Actual moving
body dependencies remain coupled without making native loading depend on an
execution grant.


### Preparation and execution are different states

A booked discovery allowance may mean the room is still loading geometry.
A room with prepared inputs can still be waiting for an execution opportunity.
Neither state changes liquid quantities or accumulates missed rain/transfers.
After loading, the next liquid pass captures current placements again; it never
executes a discovery snapshot retained across native loading or physics.

This earlier staging checkpoint retained whole-host execution admission. The
complete-group scheduler described below supersedes that charging model. The canonical
world, per-entity environment policies, saved formats and native limits are
unchanged. Publication callbacks can start fresh work; an older preparation
pass cannot defer a newer nested execution request.

### Consumed simulation allowances

Discovery, free-body physics, liquid transfers and contact sampling now mark
their allowance as consumed before running the corresponding native pass.
Removing a request, retiring its native owner, shrinking demand or separating
dependencies cannot release the part already consumed in this opportunity.
Unused reservations can still be released. Only the next shared clock
opportunity clears consumed charges. This prevents regrouping from making
already-used capacity available twice.

Human and agent programs share the passive fact
`runtime.simulationWorkLedger`, available with `runtimeDiagnostics.v1`.
Its required `resource` argument selects `discoveryUnits`, `physicsUnits`,
`liquidUnits`, `contactUnits`, `physicsBodies`, `liquidPasses` or `contactPasses`.
Each read returns a bounded record:

- `step` and `resource` identify the opportunity and selected allowance.
- `committed` includes current grants and any larger consumed charges.
- `consumed` is the largest allowance used per request in this opportunity.
- `retired` is the consumed subset whose requests have been removed.
- `limit` is the existing limit for that resource.

Compare `step` when combining separate reads. Different resource reads are
separate observations even if their steps match; an assembled display is not
an atomic snapshot or permission to execute. Existing host/body budget facts
still describe current requests; they can report no owners while this ledger
retains consumed capacity. These reads do not collect owners, admit work,
capture geometry, publish quantities or advance the clock.

This ledger counts logical allowances, not individual operations, native
residency, CPU time or PhysX solver cost. Reusing the same allowance does not
grant another automatic quantity/contact tick; those clocks keep their own
cadence. It does not yet give each complete liquid group an independent
execution grant or preserve canonical member service age across regrouping.
Those remain required before expanding saved/native limits.


### Independent quantity-group execution

Discovery is still bounded per room host. Execution is admitted per complete
interacting quantity group: independent pouring circuits no longer pay for each
other or for unrelated dormant containers. Empty receivers and missing cavities
remain part of the closure when geometry or a physical connection requires them.
Read-only obstacles and roofs can be shared without coupling quantity groups.

A moving container's group and its connected body requests receive their
allowances together. Before discovery is available, a conservative placeholder
keeps the motion queued without authorizing execution. A scheduled kinematic
hold retains linear/angular velocity; trajectory discovery includes that held
motion. Native input readiness is distinct from queued, booked and executable
work. Loading never needs a quantity-execution grant first.

Every preparation captures fresh placement. A synchronous fence rejects changes
to world/source, native object identity, root transforms, gravity or container
motion during preparation; the next pass discovers again. Physics refreshes its
query after flow preparation because loading callbacks can change topology.
A grant does not authorize an older spatial snapshot. Fair booking is separate
from the current-pose execution check, so ordinary motion does not force
admission back to caller order at every clock boundary.

Service age/order belongs to canonical container identity. Regrouping, request
recreation and native retirement preserve it; canonical deletion and world
replacement create new identity. Automatic quantity execution cannot process
the same member twice in one opportunity, including after merges/splits.
Used allowances remain charged through the existing consumed-work ledger.
Explicit internal test ticks remain independently stepped; there is no missed
time catch-up in the user runtime.

All quantity groups retain the same atomic publication, rollback and Undo
episode. A wait affecting unfinished quantities still closes that shared
episode before preparation; independent ready groups can continue afterward.
This does not raise saved or native limits: 16 containers per room, 32 native
liquid instances, 64 registered work hosts, 32 simultaneous quantity-group
allowances and 524,288 liquid work units remain distinct limits.

The old runtime.flowWorkBudget and runtime.flowWorkSchedule keep host counts.
A host reservation reports the current canonical vessel count separately from
its last requested group costs; a stale discovery can therefore show a newer
vessel count before those costs have been rebuilt.
Use runtime.flowGroupSchedule, runtime.flowGroup and
runtime.flowGroupMember for group/membership readiness and costs. One member
per read keeps records within the shared program-value budget. Check step,
reservationId and firstMember across reads; indices and group membership
can change. These facts are passive and shared by user and agent.

The revised nine-room desktop fixture has ten ready two-container circuits.
They need 53,260 execution units, so all run in the same opportunity while 124
unrelated saved cups remain dormant. The older whole-host charge was 552,816.
This proves the charging/closure improvement for that fixture, not maximum
world size, measured CPU cost or Quest frame-time acceptance.

Joint preparation uses the complete prepared allowance before a native joint exists.
Actual motion and quantity transfer still require the connected joints to be active;
otherwise requiring an active joint to grant its own creation would deadlock.

Group and member reads include membershipRevision. Compare it with step,
reservationId and firstMember and restart enumeration when it differs: a merge
within the same step can retain both the reservation and first member. Readiness
and quantities remain momentary observations, not an atomic snapshot across calls.


## Native edit target reads

Native preparation reads detached copies of the edited resident objects from the
owned candidate. Removed and dormant targets are skipped before copying their
geometry. Imported-model handoff reads only its exact target. The candidate's
private identity index preserves its accepted values across later source edits;
callers cannot mutate the candidate, journal or Undo/Redo entries through a read.
Native preparation retains stable identity order and the same admission, failure,
cancellation and ownership checks.

This reduces redundant reads during native preparation, not the cost of the
entire transaction. Candidate creation still owns a complete room copy and an
identity index; validation, saving, reconciliation and history still have
room-wide work. Saved capacity is unchanged. Those remaining costs need explicit
bounds and evidence before larger worlds can be admitted.

## Room save inspection

Room storage can reuse the validation result for the exact primary-file contents.
Each save inspection still reads the bounded file and hashes its bytes; file size
and modification time alone never authorize reuse. The cache holds one fingerprint
and its validation outcome, not a mutable room. Changed contents go through the same
strict UTF-8, duplicate-field, version, wire-format and document validation path.
A primary identified as newer during either save inspection blocks publication;
invalid contents retain the existing unreadable-file recovery behavior.

This is an explicit RoomStorage opt-in. Generic stores keep revalidating by default:
a validator that consults changing assets, catalogs or other runtime state cannot
reuse an outcome based only on file contents. New store opt-ins must establish that
all inspection decisions depend only on the bytes and fixed schema.

Durable editing still includes complete candidate validation, serialization and
file publication. The optimization removes repeated parsing; it does not make
persistent edits asynchronous or expand saved limits. Larger-world admission still
needs bounded save preparation and publication with revision/lifecycle ownership.
Human and agent operations must keep the same pending, failure and completion
contract; a successful receipt must not precede durable publication.

## Prepared-save storage boundary

RoomStorage has a bounded preparation API for the asynchronous editor work. It
captures the exact saved baseline without waiting for a busy room/memory
coordinator, then owns a detached copy of the caller's document. Pure validation,
encoding and strict readback of the resulting wire bytes run on a worker after the
coordinator has been released. Expensive inspection can reuse only an exact
content-hash result and does not hold the publication lock while decoding.

One preparation is admitted per RoomStorage before its snapshot is copied.
That reservation lasts through background work, the returned candidate and
publication or disposal; failed and cancelled preparation releases it. The native
command queue must wait before copying another candidate. This per-store bound is
not a global memory or Quest performance guarantee.

Prepared publication is one attempt against the captured baseline. A different
primary, a newer-format file, a foreign preparation or an existing pending file
cannot be overwritten through that attempt. Invalid bounded primary contents keep
the existing unreadable-evidence path. Empty or oversized primary files cannot
become a prepared baseline. Successful wire readback allows the store to cache
the published content's validation result without trusting serialization alone.

Rotation is the first native completion operation using this storage boundary.
Other edits retain their existing completion paths; the editor migration is not
complete. Native integration must retain candidate/resource ownership, check the
live journal revision and lifecycle, and surface pending/failure/cancellation honestly.
The disk fingerprint cannot prove that the in-memory room is still current.
Bounded baseline reads and final file writes/flush/publication are still synchronous;
native publication also needs protection against blocking retention readers and
lifecycle transitions. Saved capacity remains unchanged.

## Pending native rotation

The shared object.rotation.set action now has completion duration. Its book form,
agent request and program block use the same pending operation and receipt. The
original synchronous editor API retains its synchronous contract; other editing
actions have not yet migrated.

A pending rotation owns one private journal candidate and one workspace writer.
It waits for a previously dispatched autosave without blocking the owner thread,
then prepares the saved bytes in the background. A second pending edit is refused
before copying another candidate. Ordinary autosave dispatch waits for this edit;
the world clock and viewer movement continue. Only the target rigid body is held.
Cancellation restores its prior motion only if no grab, teleport, physics change
or other owner invalidated it. Successful placement resets motion as before.

The commit checks the same journal, history, live target and workspace again.
Authored changes, including changed physics placements, invalidate the candidate.
Runtime clock progression and personal viewpoint updates can coexist with an
unrelated authored edit: the older checkpoint is saved, the newer live values are
preserved, and remain dirty for the next save. Whole-clock snapshot replacement
still requires the exact clock position. The later temporal checkpoint contract
below distinguishes explicit configuration/seek intent; this is not general stale merging.

Native resources are prepared immediately before conditional file publication and
journal acceptance, on the owner thread without yielding between those steps.
A completion receipt follows confirmed saving and native application. Stop,
loss of focus, suspension or workspace disable cancels during preparation;
its detached worker drains without accessing a destroyed Unity owner. The final
commit does not yield; once it begins, Stop cannot retract it. Completion callbacks
cannot recursively publish, clear newer unsaved state, or make the operation replay.
Observers see the accepted revision before receiving the native change event.

Final file writes, flushes and replacement remain synchronous. This first native
integration does not establish Quest frame-time acceptance or larger-world
capacity. Removing that remaining publication wait requires an explicit native
commit reservation, not moving an unchecked write to another thread.


## Worker staging for pending room edits

RoomStorage preparation now writes and durably flushes a private
room.vN.json.prepare.<random-id> file on its worker. Only the owned candidate can
claim that file for publication. Preparation does not change the primary, backup,
journal or native scene. This replaces the synchronous staging/flush portion
described above; it does not make the final commit fully asynchronous.

The returned candidate pins its staging bytes against ordinary writers. The
commit verifies their content, the captured primary and the journal/lifecycle
again before claiming the usual .pending path without overwriting another
writer's unfinished file. The native path still holds only its target body, not
the whole simulation. Its shared completion receipt follows actual save and
application, as before.

Publication refuses a busy room/memory coordinator or retention reader instead
of waiting on the interaction thread. A staged interactive replacement makes
one filesystem attempt, without the Windows retry sleeps used by older
synchronous save paths. A failed or cancelled candidate cannot be replayed.
Cancellation, stale revisions and completed publication release admission and
remove only the exact owned staging bytes. Substituted files and failed partial
writes remain evidence. An orphan .prepare file is never loaded or adopted as a
room, including when no primary exists. There is no automatic orphan sweep.

Baseline reads/hashes, staging identity checks, backup copying, final
rename/replacement, native reconciliation and some cleanup IO remain synchronous.
Do not interpret background staging as a Quest frame-time guarantee, a durable
write-ahead native transaction, or support for larger saved worlds. Other edit
families still need explicit migration to completion operations with their own
native resource lifetimes.

## Inspecting unfinished room-save preparations

Private save preparations now have bounded admission: at most four files, each
at most the existing 4 MiB room-document limit. Active and abandoned preparations
both count. Unexpected oversized files or directories in this private family
block new staging and remain preserved. This does not reduce the accepted room
limit or silently clean up data after a crash.

The same catalog used by book forms, programs and agents exposes
room.storage.preparations.inspect and room.storage.preparations.discard, gated
by roomStagingMaintenance.v1. Inspection is a completion action on a worker.
Its receipt supplies an inspectionId, count and more flag; the cached
room.storage.preparations and room.storage.preparations.entry facts expose
status and up to 32 entries, without private paths or file contents.
Hashing an inactive preparation does not hold the room coordinator.

Discard requires explicit intent to permanently remove the reviewed unfinished
preparation, its inspectionId, entryId and content fingerprint, and confirmation
equal to “discard ” followed by entryId. Inspection or wanting more space is not
permission. The action is irreversible. Its generated form includes a confirmation
message; agents must explain the proposed disposal before obtaining that intent.

Only an inactive, regular, bounded file is eligible. A worker rechecks ownership
and exact bytes, renames the private file to a unique claim, verifies the claim
again, and then removes it. Accepted rooms, backups and recovery evidence outside
this private preparation family are untouched. Changed files remain evidence.
New edits are briefly frozen during disposal; the worker retains a workspace
reader until it drains, so retirement cannot remove its directory prematurely.

A review is session-local and one-shot. Starting a new inspection, attempting
disposal, or suspending the workspace invalidates it. A completed receipt can be
read again without replaying disposal. Stop cancels outstanding work but cannot
promise rollback after a filesystem effect: inspect again before another request.
A restart never automatically retries disposal or adopts a preparation as a save.
Unsafe, oversized or unreadable entries remain ineligible; this is not a general
file manager or a substitute for the existing room recovery workflow.

## Background publication boundary

RoomStorage.StartPreparedPublication now provides a pure storage worker for an
already prepared candidate. The caller transfers that candidate once; disposing
the old wrapper, replaying it, or presenting it to a different store cannot steal
or duplicate the worker's effect. The existing preparation slot stays occupied
through publication and cleanup, so queuing does not bypass admission limits.

The returned operation exposes Completion, Started and CancelBeforeStart.
Cancellation wins only before the worker crosses the publication boundary.
After that boundary the worker finishes one conditional publication attempt;
cancellation cannot retract a possible durable write. Completion reports Saved
and MayHavePublished separately. A failed/unconfirmed attempt after the boundary
must be inspected, not retried automatically. In particular, an observation
failure after writing must not claim that saved data was unchanged.

The worker uses the same saved-baseline comparison, room/memory coordinator,
backup preservation, exact stage ownership and one-attempt replacement as the
synchronous API. Its future finishes after cleanup releases admission. It does
not capture Unity objects or require an owner-thread continuation.

The shared rotation action now uses an explicit native acceptance boundary.
Its worker first checks the stage and saved baseline, then releases every disk
lock before waiting for authorization. Unity checks the exact journal/history,
workspace, target and prepared native resources, accepts the edit once and starts
the existing conditional publisher. Final publication repeats its baseline check;
the earlier preflight is advisory, not a promise that the disk cannot change.

The visible rotation can appear while its save is pending. Completion confirms
both native application and saving. Before acceptance, Stop or a stale authored
revision preserves the original pose and releases only this operation's work.
After acceptance, Stop cannot undo the edit or cancel its save; inspect the
outcome before trying again. Native resources are reconciled on Unity's thread.
Only immutable data and the directory write lease outlive workspace disable or
retirement. Unrelated physics, the world clock and viewer movement stay active.

The publication future is installed in the existing save pipeline before native
callbacks run. A callback can flush newer state without waiting for Unity to
authorize itself. Runtime state captured after the prepared snapshot remains
dirty; a later save must not erase it. A final publication failure after native
acceptance leaves the live edit inspectable, preserves saved files and latches
storage read-only before later saves can run. Autosave and ordinary flush cannot
silently replace a conflicting saved baseline. Use explicit workspace inspection
or recovery; neither agent nor user should blindly replay the action.

Shared capabilities that advertise completion use the bounded persisted-edit
queue; later checkpoints below extend the original rotation path. Synchronous
editing APIs retain their save-before-return contract. Remaining synchronous
authoring/import paths and physical headset latency validation are still open;
a green storage test is not evidence of headset frame time.

Ordinary room saving now retains the exact primary accepted by the live store.
An existing room must be loaded before its first write; only an absent primary
can be initialized directly. A successful ordinary save, prepared publication or
paired room/memory save advances that identity from its exact private bytes.
A newer edit from the same session remains saveable. An obsolete preparation
cannot replace it and does not make otherwise valid newer work read-only.

An observed independent primary change instead preserves the saved files and
the live unsaved edit, and blocks replacement until reopening or recovery.
Initial temporary-room capture checks both the accepted room and remembered
values, so a fresh disk capture cannot silently adopt an outside change as its
discard baseline. Existing unfinished ordinary pending files are preserved.
Temporary IO obstructions remain retryable; a failed candidate is removed only
when this writer created it and its complete bytes still match.

These guards use the existing room/memory coordinator and publication paths.
They do not merge independent edits or freeze the whole scene. Their comparison
is against file contents, including same-size/same-timestamp replacements.
The coordinator serializes participating writers; it cannot make arbitrary
external filesystem edits transactional.

## Visual edit completion and live history

`visibility.layer.save`, `visibility.layer.remove`, `object.visibility.assign`
and `world.lighting.set` use the same queued completion path as other persisted
edits. The book, agent and programs wait for the original receipt; none needs a
separate save tool or a second invocation. Names and settings are captured at
request time. Cheap admission checks do not copy the whole room while waiting.
Layer edits require the complete current member list and recheck capacity,
revisions and released authoring ownership before acceptance.

Visual history is explicitly distinguished from placement history. A visual
object delta may change its visibility binding, local lights or passthrough
windows; it cannot create/remove an object or change its physical content. It
never checkpoints a supplied pose. Publication and Undo/Redo preserve the
current saved placement, and native reconciliation updates rendering without
resetting bodies, animated parts, audio, terrain or collision policy. Visual
Undo does not broadcast the global motion-cancellation signal. Existing manual
held-object/drawing checks still apply. Local-light/window saving remains
synchronous; it uses this history distinction without claiming async coverage.

After preparation begins, a visibility assignment can coexist with recorded
runtime movement of its own target. The journal proves that every intervening
revision advance came from runtime placement, then applies the visual delta
while retaining the newer pose. Authored placement, content edits, history changes
and workspace replacement still invalidate the preparation. This does not merge
arbitrary edits. While waiting for admission, exact target revisions are still
required; a changed waiting target needs a new request. No body is held for a
visual edit. Shared layer changes and lighting also preserve unrelated motion,
clock progression and viewer movement. Newer runtime state stays dirty for the
next checkpoint instead of being discarded by the older prepared save.

Receipts freeze the accepted definition or object revision, including unchanged
saves. World-lighting receipts retain the requested settings and accepted
revision even if a human edits the lighting before the caller observes
completion. Cancellation before acceptance changes nothing. After acceptance,
Stop drains the save and cannot retract it; a final save failure leaves the live
edit inspectable and follows the shared conflict/recovery path. Temporary
Keep/Discard and one Undo use the same journal.

Clock and weather use their own temporal delta semantics within this same
queue, described below. Whole-document restores still retain exact snapshot
semantics. Physical headset frame time and appearance remain separate acceptance
requirements.

## Clock and weather acceptance checkpoints

`world.time.configure`, `world.time.seek` and `world.weather.set` are completion
capabilities in the same bounded persisted-edit queue. The original book forms,
agent requests and program blocks use that catalog and await the original action
receipt. Requested settings/destinations are captured before waiting; no waiter
copies the whole room. Exact authored revisions, history and workspace identity
are checked again at admission. Ordinary ticking and recorded runtime placements
can continue, but another authored edit invalidates a prepared request.

Temporal changes have an explicit native acceptance boundary. Once previous
saving has drained, the owner thread resolves the requested delta against the
current clock/weather, applies it and captures that exact state. Background
preparation and conditional publication then save this detached checkpoint through
the existing accepted-primary and room/memory guards. The save future is installed
before change callbacks, so a callback flush can wait without deadlocking on the
owner thread. Completion waits for confirmed saving and native application.
The receipt is an immutable acceptance-time observation, not a claim that the
clock still shows that position when the caller reads it. Later runtime progress
remains dirty for the next checkpoint.

Clock configuration history contains settings intent: rate, running state and
daily lighting frames. Acceptance and Undo/Redo keep the current day/second.
A deliberate seek contains position intent. It preserves current settings and
records the actual position immediately before acceptance as its Undo destination.
Even a destination equal to the time at preparation remains a seek request until
acceptance, since time may advance while waiting. An unchanged configuration or
seek at acceptance creates no temporal Undo, preserves Redo and keeps the edit
revision. Whole-document snapshot restoration still restores exact saved time;
these metadata-only modes cannot be mixed with object/resource changes.

Weather starts at the current authored day/second and interpolates from the
weather sampled at acceptance. Undo/Redo restores the previous/next target, seed
and duration by starting a new transition from the currently sampled weather.
Zero duration remains immediate. Reapplying the same target, seed and duration
keeps the current fade and creates no weather Undo or revision churn. It never
backdates a new fade to the time a save worker happened to start. Seeking still
resamples an existing weather transition without replaying physical water input.

Weather admission briefly holds liquid flow while sealing the existing episode
and preparing acceptance. Event callbacks cannot reopen a flow across that
boundary. The lease releases at acceptance, cancellation or preparation failure;
clock, animation and independent objects keep running. Collected water retains
its own accepted quantity and Undo/event receipts. The existing liquid-episode
publication is still synchronous: this increment does not claim that every
water/authoring operation is free of owner-thread disk work, nor that dependent
vessels can ignore their shared flow readiness.

Before acceptance, cancellation leaves the requested temporal change unapplied.
After acceptance, Stop ends observation but cannot retract the live change or
cancel any part of its save. A final save failure preserves saved files and the
live, unconfirmed change, blocks further saving and requires inspection/recovery;
it is not a successful action receipt and must not be blindly replayed. Temporary
play uses the same live/history semantics, with durability only after Keep.
Legacy synchronous editor entry points retain save-before-return behavior.

The accepted snapshot copy and native preparation still run on the owner thread.
This design avoids waiting for worker IO while freezing world time, but it is not
a measured Quest latency or larger-world capacity claim. Those acceptance gates,
remaining synchronous authoring/import paths and complete release checks remain
open.

## Liquid content history and live continuity

Automatic pouring, scooping and rain publication, and explicit measured container
transfers, use a liquid-content transaction scope. It owns amount, liquid identity,
display colour and fluid properties only. Capacity, cavity geometry, physical
settings, connections, animations, placement and other world metadata cannot be
included in this scope. Configuring/removing a container remains a full component
edit with its existing admission and ownership requirements.

Content acceptance and Undo/Redo merge those fields into the current objects.
They preserve current canonical placement and do not apply a former pose to live
views. Observed transforms supplied during a transfer are not placement intent;
normal placement checkpoints remain responsible for saving movement. Undo restores
both balances and their previous identities together, including an empty receiver's
identity before adoption. Runtime placement advances can be merged; authored
changes invalidate a prepared edit. No-op content edits do not consume Undo/Redo.

Native reconciliation updates existing fill views and the liquid subsystem only.
It does not reconfigure rigid bodies, reset their velocities or contacts, restart
recipe parts, rebuild geometry or connections, or send the global editing/cancellation
signal. Dormant or missing objects retain their canonical content without being
rematerialized by Undo. Normal residency admission restores their views later.
This preserves independent motion while the system records the result of play.
Explicit transfers retain their existing whole-target execution claims; this
change does not bypass human ownership or other admission checks.

The journal uses one mutually exclusive internal edit scope (full, visual or
liquid contents), with scope validation and matching native reconciliation.
It is not a saved-format migration, a separate user/agent system or a new public
capability. Agent calls, generated book forms, manual Undo and simulation publish
through the same canonical journal. Other component scopes should be introduced
only with their own field ownership, conflict and native-continuity tests.

Automatic pouring, scooping and rain now accept their paired quantities into the
journal before asynchronous saving. At most two immutable snapshots wait in
acceptance order behind ordinary saves. Snapshot capture stays on the owner
thread; disk preparation and publication run on workers. A full queue pauses new
intake without accruing skipped time or freezing vessel poses. Preparing another
authored edit closes the current episode and holds new intake until that edit
has accepted or drained, so the two paths cannot compete for storage preparation.

The shared world.liquidPersistence fact distinguishes idle, saving and
unconfirmed, with bounded pending/capacity counts and a recovery explanation.
object.container.live.contents.savedAmountMl is the journal-accepted balance,
not a durable-storage receipt. A failed save preserves accepted live quantities,
blocks further saves and stops new intake; restarting physics cannot resolve it.
Previously saved files remain preserved for explicit workspace recovery. Do not
replay a transfer because its save failed. An episode that never reached journal
acceptance can still be cancelled back to its accepted balances.

Success notifications are delivered in acceptance order only after their save
succeeds, at a safe boundary with no unfinished live flow. Each save releases its
own workspace writer before notification; a later queued save may still own one.
The editor owns the queue across removal of the pouring component. An intervening
authored change/Undo to any participant, replacement of a native participant,
or changed workspace discards the obsolete notification. Moving the whole world
while saving does not revoke a valid amount event. Listener mutations, frame
changes during delivery or a new episode stop the rest of that notification batch.
Temporary forks accept locally without overwriting the ordinary room. Reload
does not replay notifications.

Container configuration/removal and explicit measured transfers now use the same
ordered pending-edit queue as other shared saved actions. Their catalog duration
is completion. The generated Book forms, user programs and agent requests receive
success only after native application and confirmed saving (or local acceptance
in a temporary room). Waiting captures intent and checks both transfer revisions;
a changed source or destination requires a fresh request. Configuration definitions
and requested amounts are detached from later caller changes.

A transfer prepares one paired content-only edit. Its output reports the capped
transferredMl once; no output is promised during preparation. Cancelling before
acceptance leaves both containers unchanged. After acceptance, Stop cannot retract
the quantities or abandon the save. A failed accepted save preserves live contents,
blocks later writes and requires workspace recovery; retrying blindly can transfer
twice. Undo restores both balances without restoring old poses or velocities.
Configuration/removal keeps full component validation, admission and native updates.
Automatic amount events remain separate from a requested transfer's action receipt;
world.liquidPersistence describes the automatic checkpoint queue, while the shared
execution receipt exposes a requested action's preparation and completion.

This does not establish a blanket latency guarantee. Legacy synchronous editor
entry points keep save-before-return semantics. Lifecycle callbacks now dispatch
[coalesced checkpoints](QUEST_ACCEPTED_SAVE.md#lifecycle-checkpoints-and-detached-owners)
without waiting for disk. Other authoring/import paths, owner-thread snapshot and
reconciliation costs, and Quest physical performance remain separate work.

## Shared geometry edit completion

`object.recipe.edit`, `object.collision.edit` and `object.model.geometry.set` use
the same ordered pending-edit path as other shared world authoring. Book controls,
programs and agent calls capture independent arguments, wait for admission and
recheck source revision, ownership and geometry prerequisites before acceptance.
No new action IDs or storage versions are introduced. Their results describe the
accepted edit, even if another edit occurs before the caller reads its receipt.

Before acceptance, cancellation or failed preparation preserves native geometry,
playback and the saved room. After acceptance, Stop lets saving drain. Publication
failure leaves the live edit unconfirmed and the prior file intact, blocks further
saves and requires recovery. Content-only transactions retain current root
placement and motion during acceptance and history. Recipe edits stop part playback
at acceptance; custom collision remains independent. Imported geometry still
requires paused physics and checks actual native readiness after application;
saved settings alone cannot produce a successful completion receipt.

Geometry construction and reconciliation still run on Unity's owner thread.
Moving persistence preparation off that thread is not a frame-time guarantee.
Legacy synchronous editor entry points retain their existing semantics.


### Completion for attached authoring components

Saved object.drawingTip.edit, object.sculptTip.edit, object.surface.edit,
object.light.edit and object.window.edit use the same ordered completion
operation as the other persisted object edits. The book, agent and programs
wait for native application and saving. Source revisions and native availability
are checked again after waiting; arguments are captured before admission.
Cancellation before acceptance leaves the requested edit unapplied. An accepted
edit continues saving after Stop. Publication failure leaves a visible but
unconfirmed edit, preserves old files and requires recovery, with no automatic
replay. Outputs retain the accepted revision and generated stroke identity.

Light and window edits keep their visual transaction scope. Drawing patches and
tool definitions use content scope so acceptance and Undo preserve the object's
current placement. Other objects and the world clock continue running. These
configuration actions do not begin physical drawing, sculpting or a contact
gesture. A saved opening on desktop still does not establish active Quest
passthrough; the separate rendering/anchor facts explain availability.

This preserves independent real-room collision, virtual terrain, visual-layer and
acoustic policies. Choosing which actors respect the physical floor never needs
to hide that floor, remove another actor's collision, or alter its saved tools.
Native view preparation and reconciliation still run on Unity's owner thread.
Physical latency and headset acceptance remain separate release checks.


## Personal world-location completion

Explicit personal relocation shares the ordered edit queue and completion
contract with authoring. The generated book action `world.viewpoint.set`,
programs, agent calls and the physical **World origin** control use one native
operation. The latter supplies the authored origin and zero heading; it does not
redefine a spawn. A new operation waits for earlier saves while the world keeps
running. Final acceptance uses the latest physical head pose and rechecks
retained native dependencies, destination support/clearance and physical transfer.
A resident imported object may still be loading its collision geometry. Completion
waits for usable geometry; a placeholder cannot prove clearance. Missing or
changed dependencies still fail before movement, and cancellation never applies a
late move when loading finishes.

Acceptance moves the authored frame once and records a personal bookmark.
Detached disk work follows. Only successful completion confirms both native
application and saving. Its output keeps that accepted destination, even if the
user has already walked elsewhere. It creates no geometry Undo or scene revision,
moves no physical tracking origin and resets no NPC activity or body velocity.
Real-room collision participation remains per actor and independent of visual
opacity, virtual ground and sound policies.

Stop before acceptance leaves the requested move unapplied. Stop after
acceptance cannot move the world back and does not abandon the accepted save.
Failed saving leaves the live location unconfirmed, preserves the prior durable
checkpoint and blocks further writes until shared recovery. Later bookmarks stay
dirty until their own save; an older completion cannot replace them. Temporary
rooms keep local navigation until Keep, with existing discard behaviour.

This is asynchronous persistence, not asynchronous geometry or a measured
frame-time guarantee. Native frame transfer and snapshot capture remain
owner-thread operations. Headset acceptance remains separate.
