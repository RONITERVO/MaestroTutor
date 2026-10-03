# Shared liquid containers

The initial container component is a **measured, saved liquid store**. It supports
explicit authoring and transfers, with a lightweight visual level. It does not yet
perform physical hand/gravity pouring, spilling, drinking, mixing, buoyancy,
liquid forces or liquid mass. Those behaviours must build on the same quantities,
not introduce a second liquid store. No provider or paid generation is involved.

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
  no effect. Existing program conditions can read the fact; physical transfer
  event subscriptions are not implemented in this increment.

The included cup starts empty with an editable 500 ml capacity and a cavity
inside its existing open collision walls. Its exact template hash changes; existing
objects and saved exact template references are never silently substituted.

## Persistence and reuse

The ordinary room journal owns quantity. There is no automatic per-frame write or
unsaved simulation to merge. Copying a prop explicitly duplicates its contents;
prototypes and captured constructors retain the component with independent data.
Temporary changes stay temporary until accepted. Workspace export/import and
backup operate on the same room data. No physical transfer restarts after reload.

Room format 9 protects new components from an older client dropping them. Clean
room formats 1–5, 7 and 8 still load; version 6 remains unsupported. Snapshot intent
format 8 and archive manifest 8 carry room.v9.json. Unknown component versions
preserve the original saved file and use the existing recovery path. No migration
of uncertain old transaction evidence and no automatic data reset is added.

## Presentation and performance boundaries

Each nonempty vessel has one 24-segment surface mesh, no new collider or
Rigidbody, and one material. A bounded analytic cylinder-volume calculation locates a
horizontal fill plane inside the authored cylinder. This is a visual approximation;
only saved double-precision millilitres determine transfers. The mesh refreshes
on content edits or at most 10 Hz while its orientation changes. It has at most
74 vertices and 72 upward-facing triangles. There are no per-drop particles or rigidbodies.
A tilted vessel does not yet spill, so this display is not complete liquid physics.

Desktop tests cover conservation, full/empty and incompatible cases, stale
revisions, human ownership, failed saves, one Undo, temporary rooms, independent
copy/prototype data, future-format preservation and horizontal bounded visual
geometry. Quest performance, pouring comfort and rendered-liquid acceptance
remain device gates.
