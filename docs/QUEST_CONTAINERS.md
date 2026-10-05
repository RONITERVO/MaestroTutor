# Shared liquid containers

The container component is a **measured, saved liquid store**. Explicit authoring
and logical transfers use the same quantities as bounded native pouring and vessel scooping. The
implementation uses the contract below; desktop checks do not establish
headset acceptance. Drinking, mixing, buoyancy, fluid
forces, fluid mass and uncontained puddles remain unfinished. Authored shallow
pools use the same saved vessel component. No provider or paid
generation is involved.

## Contract

- One `RoomContainer` per created object, at most 16 in a room. Version 1 is
  cylindrical; version 2 has a rectangular footprint. Imported
  objects can be configured explicitly. The included book and Maestro cannot be containers.
- Cavity: root-local bottom centre and normalized rotation, with +Y toward the
  opening; cylinder radius 0.005–1 m or rectangular width/depth 0.01–2 m;
  height 0.01–2 m for both. This does not infer,
  replace or validate the object's visual mesh or collision interior.
- Capacity: 1–1,000,000 millilitres; amount: finite double precision, 0–capacity.
  Capacity is an explicit game-rule value. Scaling a prop changes its visual
  cavity, **not its capacity or contents**. This keeps resizing/capture from
  silently creating or destroying liquid; metric physical sizing is not inferred.
- One stable, case-sensitive liquid identifier and opaque colour. Different
  identifiers or colours cannot mix. An empty receiver adopts the source's identity
  and colour. No density, temperature or nutrition is implied.
- `object.container.edit` configures/replaces the whole component, including
  contents, or removes it. This is an explicit authoring edit and can create or
  discard liquid. Emptying uses amount 0. It requires the current fact revision.
- `object.container.transfer` takes current source/destination revisions and
  `amountMl`. It transfers min(requested, available, free space) and returns
  `transferredMl`. It is an instantaneous logical transfer: it does not move
  either vessel, require proximity or secretly play an animation.
- Both endpoints are claimed exclusively. Held/animated/authoring targets,
  suspended interactions, stale revisions, incompatible contents and full/empty
  endpoints refuse without changing either object. Failed publication changes
  neither quantity; a completed transfer is one atomic edit and one Undo.
- `object.container` returns configuration, quantity and revision. Missing
  components return configured=false with inert editable defaults. Reads have
  no effect. Programs can read accepted quantities or the separate live fact below
  and subscribe to successful physical-flow publication.

The included cup starts empty with an editable 500 ml capacity and a cavity
inside its existing open collision walls. Its exact template hash changes; existing
objects and saved exact template references are never silently substituted.

## Persistence and reuse

The ordinary room journal owns accepted quantities. Pouring has a short live
episode, exposed separately below, and never writes every simulation frame.
Copying a prop explicitly duplicates its accepted contents;
prototypes and captured constructors retain the component with independent data.
Temporary changes stay temporary until accepted. Workspace export/import and
backup operate on the same room data. Unfinished episodes and their events never replay after reload. Pouring can
resume from accepted contents and current placement after physics is started.

Room format 19 protects rectangular cavities from older readers. Clean room
formats 1–5 and 7–18 still load; version 6 remains unsupported. Snapshot intent
format 18 and archive manifest 18 carry room.v19.json. Unknown component versions
preserve the original saved file and use the existing recovery path. No migration
of uncertain old transaction evidence and no automatic data reset is added.

## Presentation and performance boundaries

Each nonempty vessel has one surface mesh, no new collider or Rigidbody, and one
material. A bounded analytic cylinder/box-volume calculation locates a horizontal
fill plane inside the authored cavity. Cylinders use 24 segments; rectangles use
four corners. This is a visual approximation;
double-precision millilitres determine transfers. The mesh refreshes
on content changes (including 20 Hz live flow), or at most 10 Hz for orientation alone. It has at most
74 vertices and 72 upward-facing triangles for cylinders, or 7 vertices and
6 triangles for rectangles. There are no per-drop particles or rigidbodies.
The display and pouring use the same cavity-volume calculation. This bounded
approximation is not a general liquid solver.

Desktop tests cover conservation, full/empty and incompatible cases, stale
revisions, human ownership, failed saves, one Undo, temporary rooms, independent
copy/prototype data, future-format preservation and horizontal bounded visual
geometry. Quest performance, pouring comfort and rendered-liquid acceptance
remain device gates.


## Physical pouring

An open configured container participates when scanned-room physics is running,
its geometry is ready and its position is within the active room. There is no
second per-toy liquid system or extra flat control. Users can hold both vessels;
Maestro/program movement can tilt the same objects. Paused physics, lifecycle
suspension and workspace recovery stop the simulation.

At 20 Hz, the lowest opening rim determines how much lies above the spill plane.
A bounded flow rate removes that excess. A gravity trajectory has at most 32
segments per source, with 64-hit bounded collision queries. The nearest solid
surface clips the stream. A downward crossing through another cavity's opening
can receive compatible liquid, capped by its free capacity. Misses, full or
incompatible recipients count as uncollected spill. Spilled millilitres leave this
container model: there is no persistent puddle, snow/water field or buoyancy yet.
The conservation check is source loss = received + uncollected spill.

One room-wide episode combines simultaneous flows. It publishes after 0.3 seconds
without flow, a grip release, physics pause, explicit Save, or a ten-second
checkpoint, and before an empty participating vessel changes liquid identity or
colour. One atomic room edit and one Undo restore the episode's quantities;
current placements are retained. No replayable transfer commands are queued by
the simulation. Temporary-room flow remains in its fork until explicitly kept.
If saving fails, all episode quantities return to their accepted values; physical
pouring remains stopped until the user/agent restarts physics. Configuration and
logical-transfer edits on participating containers are refused while flowing.

`object.container` continues to describe accepted data. `object.container.live`
exposes current quantity, accepted quantity, episode state and outgoing totals.
`object.container.poured` reports successful publication with received/spilled
millilitres and the number of distinct recipients. It never claims a saved result
before publication, never fires for failed saves, and never replays on reload.
The same fact/event are available to user-authored programs and Maestro.

The stream is a visual strip with no per-drop rigidbodies. There is no splash,
wetness, momentum exchange, bare-hand scooping, fluid pressure or mass calculation.
Changing visual scale does not change the authored logical millilitre capacity.
Device readability, sustained performance and real-hand pouring acceptance are
still required before release.

## Physical vessel scooping (2026-10-04)

`containerScooping.v1` lets users dip a held, configured open vessel into a larger
one while room physics runs. Included **Bucket** (empty, 2 litres) and **Water
basin** (32 litres in an editable 40 litre capacity) use this ordinary component.
They are editable procedural templates, not special-case toys. Creating either
does not start physics. A cup can receive water from a bucket using existing
pouring; compatible imported objects can use the same configured cavities.

Both openings must face upward. The smaller receiving cavity must fit wholly
inside the donor, clear of its bottom and walls, with its entire opening below
the donor's current liquid plane. This intentionally conservative model does not
infer hollow interiors from visible meshes. At most five candidate paths sample
the opening; any accepted path must have clear endpoints and an unobstructed
segment from the free surface. A solid lid, blocked ray origin or saturated
collision query cannot establish that path. A handle across the centre can leave
another sampled path open.

The simulation shares the existing 20 Hz liquid episode, lifecycle, grip handling,
publication, rollback and one Undo. Each source's total outflow is bounded to
75% of its capacity per second, shared by pouring and all dipping recipients;
receivers have the same intake limit and never exceed capacity. When several
donors qualify, the smallest containing donor wins, with stable object-ID order
for ties and receiver contention. This is deterministic admission, not a pressure
or equalisation calculation. Quantities are conserved: source loss equals intake.
A full vessel completely immersed in a compatible larger reservoir does not pour
back out under gravity while the same bounded geometry and unobstructed path
prove immersion. This prevents small controller/grab tilt from causing an endless
spill/refill cycle below the water surface. The check also applies after the live
episode finishes, so a full resting vessel stays stable. Lifting restores normal
pouring, including any excess caused by tilt. This is a bounded immersion rule,
not pressure, displacement or a general fluid solver. A regression covers a tilted
full bucket, lifting and pouring back, and uncollected spill outside the reservoir.

Lifting the vessel out stops scooping. Surface depletion also stops intake when
the full opening is no longer submerged.

`object.container.live` keeps its existing wire shape and reports current and
accepted quantities; its transfer/spill counters remain **outgoing pouring only**.
The new `object.container.scooping` fact reports incoming `scoopedMl`, outgoing
`drawnMl`, distinct `donors` and `recipients` for the current episode. Counters reset
after publication or rollback. `object.container.scooped` fires for each receiving
vessel only after successful publication, with its ID as source/value and typed
`scoopedMl`, `donors`, `liquid`, and `temporary` fields. Failed saves, Undo and
reload do not emit it. Programs can wait for this ordinary event and branch on its
fields; no separate per-container program runtime is used. There are no new saved
fields or room-format changes in this addition.

Native checks exercise real template collision, grip/lift, concurrent recipients,
capacity/rate bounds, conservation, obstruction, scoop-to-pour, successful and
failed saves, temporary discard, Undo and an actual typed event program. Full-app
and browser probes separately verify shared creation, read-only inspection and
matching catalog calls. These remain desktop evidence: Quest hand/controller
comfort, visibility, sustained frame time and tracking loss still need acceptance.
There is no displacement, trapped air, fluid mass, buoyancy, finger scooping,
uncontained puddle or snow field in this liquid model.


## Identity changes between refills (2026-10-04)

An empty vessel can adopt a new liquid identifier or colour. If it already
participated in the current live episode, both pouring and scooping publish that
episode **before** the new intake. Its outgoing and scooped event totals therefore
retain the old liquid identifier. The next tick reevaluates current poses,
geometry and capacity before transferring new contents; no pending transfer is
saved or replayed. A failed boundary save reverts the old episode and blocks flow
until physics restarts, without taking any of the new liquid.

A refill with the same identifier and colour continues the existing episode.
The boundary closes the room-wide episode, including other participating vessels,
and has its own Undo. Consecutive unlike refills therefore produce separate saved
operations and correctly attributed events. Colours still cannot mix; the event's
`liquid` field remains the authored identifier, not a new colour field. Contracts,
features and saved formats are unchanged.

Real native tests reproduce empty/refill through both physical paths, failed
boundary publication, colour-only changes, matching refills, conserved quantities
and separate Undo/event outcomes. These checks supplement the pending Quest
interaction and sustained-performance acceptance.


## Rectangular cavities and shallow pools (2026-10-04)

`rectangularContainers.v1` extends the existing component, not the action list.
The optional `rectangle: {width, depth}` replaces radius when both dimensions
are positive. Omitting it, setting it to null or setting both dimensions to zero
selects the cylinder. Partial-zero dimensions refuse. Native saved components
use version 2 only for rectangles, and version 1 for cylinders. The radius stays
editable as the fallback when removing the rectangular footprint. Cavity sizing
never alters the visual recipe or collision proxies automatically.

The included **Shallow pool** is an editable five-part fixed vessel. Its cavity is
1.18 × 0.78 × 0.29 m, capacity 266.916 litres, initially 84% full (224.20944 litres).
The visible walls and physical interior are ordinary recipe/collision parts.
It occupies one of the same 16 container slots. Creation does not start physics.
A bucket or cup can dip below the water line, collect conserved quantity, and
pour back through the opening. Contents survive save/reload, Undo/Redo and
temporary-room discard. This is a bounded authored reservoir, not flooding,
arbitrary puddles, fluid pressure, buoyancy or a room-wide water field. The water
surface has no collision; the vessel supplies floor/wall collision.

The same free-surface plane drives rendering, overflow and immersion. Box volume
uses an analytic plane fraction with bounded inversion, including near-axis
orientations. Receiving streams use the actual rectangular opening, not a circular
bounding proxy. Dipping checks the complete receiving cavity against donor walls,
floor and the liquid surface; circle/rectangle combinations share that path.
Competing donors sort by world footprint area and then stable identity.

`object.container` returns a fixed typed record: cylinder rectangles have both
width and depth zero. **Load current values** now loads the complete definition
as well as its revision, so changing shape does not silently reset contents.
The generated editor and agent both submit the same ordinary configure action.
For a reusable behaviour, a visible Read block takes one fact snapshot; structured
preferences use explicit typed member bindings when an optional input record has
no whole-record binding type. Unavailable reads fail before the action. A user
can instead keep the edited definition fixed and read only its current revision.

Desktop acceptance covers shape/version admission, independent copies, analytic
volume against numerical integration, tilted fills, real pool/bucket scooping and
pour-back, failed publication, saved quantities and temporary-room discard.
The shared fixture exercises the generated program in both runtimes. Quest
reachability, real scan alignment, surface clarity and sustained performance
remain device gates; no fluid-performance claim follows from desktop checks.
