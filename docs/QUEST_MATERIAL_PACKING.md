# Measured material and packing

`object.material.pack` turns a measured amount from a saved height surface into an
ordinary editable, grabbable sphere. The book, delegated Maestro task and saved
programs use the same native capability. There is no separate snow-object engine.

## Contract

Supply a source object and its current `object.field` revision, local X/Z centre,
0.005–2 m radius, requested 0.001–20 local litres, name, room position and explicit
0.05–20 kg rigid-body mass. A smooth footprint weights available vertex heights;
actual triangle areas measure the removed volume. A bounded numeric fit never
removes more than requested. A footprint missing the grid, containing less than
0.001 representable litre, or referring to an unavailable source refuses.

The receipt returns `objectId` and `amountLitres`. The latter is the actual loss
from the accepted source heights. Sphere recipe and explicit sphere collider have
matching diameters derived from that local volume. Source material label and
opaque colour become a measured-material component on the ball. Existing grab,
release, gravity, launch, collision, root animation, paint and copy apply.
Physics must already be enabled to make it fall; packing does not enable physics.

The source and ball publish in one room transaction. One Undo restores the source
and removes the ball; Redo restores the same ball identity. Failed validation,
ownership, capacity or saving leaves both sides unchanged. Durable receipt replay
cannot create a second ball. Stop cannot retract a completed edit; Undo can.
The operation consumes one program creation allowance.

## Reusable component

`object.material.edit` configures/removes one `RoomMaterialStore` on a created
object. `object.material` exposes its revision, configured flag and definition:
capacity, contents, material label and colour. Up to 16 stores per room, each
0.001–8000 local litres capacity with contents between zero and capacity. Missing
stores return inert editable defaults, never inferred material.

Quantity is a saved logical measure, separate from geometry, world scale and
rigid-body mass. Resizing or painting a ball does not rewrite its contents. Editing
a store explicitly can add/discard material. Copying or instantiating a prototype
explicitly duplicates contents. This keeps authoring distinct from conservation
operations. It does not claim density, compaction, buoyancy or mass conservation
across arbitrary user edits. Surface float rounding can make the accepted amount
slightly smaller than the request; the receipt and store report that exact amount.

Room v17, paired snapshot v16 and archive v16 preserve stores through copies,
prototypes, templates, temporary rooms, save/load and portable archives. Known
clean older room documents remain readable; unknown component versions and
uncertain snapshot evidence stay protected. Current room paths use one native
constant so subsequent versions do not require hand-editing all callers.
Features: `materialStores.v1`, `materialPacking.v1`, `heightFields.v1`, and
`actionResults.v1` for packing. Shared catalog schemas drive both validators and
the book form; the feature is discoverable without adding a bespoke agent tool.

## Boundaries and acceptance

Packing is an explicit authoring action at a requested room position. It does not
move a shovel or avatar hand, check reach, automatically catch a throw, melt snow,
mix materials, connect to liquid containers or simulate grains. Physical shovel
capture and hand-packing gestures remain further work using this same component.
The completed ball can already be handled as an ordinary prop. Existing liquid
containers still own their millilitre balance and cavity/pour geometry. A future
shared scoop/pour adapter must select one authoritative balance and convert units
explicitly; it must not keep a second synchronized copy of the same contents in a
material store. Full-room fluid or granular simulation remains outside the initial
v1 boundary.

Desktop acceptance covers measured source loss, bounded schemas, current-source
ownership, save failure, capacity, atomic Undo/Redo, receipt replay, independent
copies/prototypes, archive retention, real XRI grip/release and gravity against
accepted surface collision. A full-app shared-client journey records exact native
states; the separate Chrome fixture replays those acknowledgements to check the
generated form. Neither is a substitute for Quest performance/comfort acceptance.
