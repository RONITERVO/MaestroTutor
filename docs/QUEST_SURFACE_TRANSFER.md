# Shared surface volume transfer

`object.field.transfer` moves geometric volume between two existing height fields.
User-authored programs, the generated book form and Maestro's agent invoke the same
native operation. It is an instantaneous saved edit, with no new toy-specific runtime.

## Quantity and geometry

Each endpoint supplies its current `object.field` revision, a local X/Z centre and
a 0.005–2 m radius. The requested amount is 0.001–8000 **local litres**, using the
same unscaled measure as `object.field.volumeLitres`. Moving, rotating or resizing
an owner does not redefine that measure; displayed world volume may differ with
scale. This is geometric authoring, not physical mass or density.

A smooth radial footprint weights each source vertex's available height and each
receiver vertex's headroom. The accepted amount is capped by those two weighted
capacities and the request. Height changes are proportional to those weights;
boundary vertices use their actual triangle areas. Adjacent triangles interpolate
changed vertices, so a visible change can extend by one grid cell beyond the disk.
Grid resolution and field dimensions can differ. Material label and opaque colour
must match exactly, including for an empty destination. Mixing is not inferred.

Heights use floating point. The receipt exposes `removedLitres`, `addedLitres` and
signed `roundingLitres = added - removed`, measured from the actual accepted heights.
The permitted difference is at most max(0.000001 litre, removedLitres × 0.000001).
The receiver is fitted to the measured source loss with at most 40 bounded search
steps. An unrepresentable transfer refuses without mutation. No hidden quantity
ledger disagrees with the saved mesh, and no rounding loss is silently discarded.
This tolerance is per transaction; it is not a claim of exact mass conservation
across an unlimited number of edits.

## Ownership, persistence and reuse

Both owners must be distinct, created, fixed, unheld and available for component
editing. The existing whole-object claims prevent competing human/program edits;
active or retained sculpt captures still protect their field. Both revisions are
rechecked before execution. Missing fields, incompatible material, full/empty
footprints and a brush missing every grid vertex refuse without saving.

One room transaction publishes both height arrays, visual meshes and colliders.
One Undo restores both; Redo restores the accepted result. A failed save changes
neither endpoint. Temporary-room edits stay in that fork. Existing room storage,
copy/prototype and archive support preserves the resulting ordinary height fields;
there are no new persisted fields or format bumps. Receipt replay does not repeat
the transfer. Stop cannot retract an already completed edit; Undo can.

Existing `object.field` and revision-bound `object.field.samples` facts expose the
result. Programs may branch on the typed action receipt and combine transfer with
movement, contact or manual triggers. The native runtime advertises
`heightFieldTransfer.v1`; older clients must not promise this action.

## Boundaries

This operation does not move a shovel, require proximity, start physics, simulate
grains, turn a field into a rigid snowball or transfer contents into a liquid
container. A separate [shared packing action](QUEST_MATERIAL_PACKING.md) now creates a rigid
ball with measured carried material. Physical shovel/hand capture remains further
work. Ordinary reset/sculpt/copy actions can intentionally create
or remove geometric volume. Full-room granular/fluid simulation stays outside the
initial v1 boundary. Desktop checks do not establish Quest performance or comfort.
