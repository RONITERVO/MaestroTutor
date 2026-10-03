# Shared liquid containers

The container component is a **measured, saved liquid store**. Explicit authoring
and logical transfers use the same quantities as bounded native pouring. The
implementation uses the contract below; desktop checks do not establish
headset acceptance. Drinking, mixing, buoyancy, fluid
forces, fluid mass and persistent pools remain unfinished. No provider or paid
generation is involved.

## Contract

- One version-1 `RoomContainer` per created object, at most 16 in a room. Imported
  objects can be configured explicitly. The included book and Maestro cannot be containers.
- Cavity: root-local bottom centre and normalized rotation, with +Y toward the
  opening; cylinder radius 0.005–1 m and height 0.01–2 m. This does not infer,
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
  and subscribe to successful physical-pour publication.

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

Room format 10 now protects extrusion as well as saved components from older
readers. Clean room formats 1–5 and 7–9 still load; version 6 remains unsupported.
Snapshot intent format 9 and archive manifest 9 carry room.v10.json. Unknown component versions
preserve the original saved file and use the existing recovery path. No migration
of uncertain old transaction evidence and no automatic data reset is added.

## Presentation and performance boundaries

Each nonempty vessel has one 24-segment surface mesh, no new collider or
Rigidbody, and one material. A bounded analytic cylinder-volume calculation locates a
horizontal fill plane inside the authored cylinder. This is a visual approximation;
double-precision millilitres determine transfers. The mesh refreshes
on content changes (including 20 Hz live flow), or at most 10 Hz for orientation alone. It has at most
74 vertices and 72 upward-facing triangles. There are no per-drop particles or rigidbodies.
The display and pouring use the same cylinder-volume calculation. This bounded
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
surface clips the stream. A downward crossing through another cylinder's opening
can receive compatible liquid, capped by its free capacity. Misses, full or
incompatible recipients count as uncollected spill. Spilled millilitres leave this
container model: there is no persistent puddle, snow/water field or buoyancy yet.
The conservation check is source loss = received + uncollected spill.

One room-wide episode combines simultaneous flows. It publishes after 0.3 seconds
without flow, a grip release, physics pause, explicit Save, or a ten-second
checkpoint. One atomic room edit and one Undo restore the episode's quantities;
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
wetness, momentum exchange, hand scooping, fluid pressure or mass calculation.
Changing visual scale does not change the authored logical millilitre capacity.
Device readability, sustained performance and real-hand pouring acceptance are
still required before release.
