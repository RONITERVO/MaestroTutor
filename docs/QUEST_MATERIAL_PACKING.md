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
mix materials, connect to liquid containers or simulate grains. Physical shovel capture is described below; hand-packing gestures remain further
work using this same component.
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


## Surface and carried-store transfer

`object.material.transfer` bridges the existing field geometry and measured store.
Each of two distinct objects explicitly selects `kind: field` (current revision,
local X/Z centre and radius) or `kind: store` (current revision). The shared book
form, agent and programs can take surface material into a carrier, deposit it on
a surface, transfer between carriers, or reuse the surface-to-surface kernel.

Material label and opaque colour must match exactly, including on empty receivers.
The requested 0.001–8000 local litres are bounded by source availability and
receiver capacity or field headroom. Store-to-field fitting removes the volume
actually representable by the accepted heights. The result reports measured
`removedLitres`, `addedLitres` and signed `roundingLitres`; permitted difference is
max(0.000001 litre, removedLitres × 0.000001). There is no second hidden balance.
Missing/incompatible components, unrepresentable changes or failed saves preserve
both inputs. Revisions and exclusive ownership protect both objects.

One save and one Undo changes both sides; receipt replay cannot repeat a transfer.
Temporary edits remain in their fork. Existing room v17 and archive support already
preserve both component types, so this adds no stored fields or migration. Feature:
`materialTransfer.v1`. Stores can belong to movable rigid objects, but this explicit
authoring action requires both objects released. Fields remain fixed. Transferring
contents does not change store geometry, world scale, mass or liquid-container
balances. The physical scoop below adapts this same kernel for held contact and carrying
visuals. Hand packing remains unfinished; the explicit action itself implies no gesture.


## Physical material tools

The version-2 `sculptTips` component adds `mode: scoop`, a requested
`amountLitres` (0.001–20), and zero inactive shape height. It uses the carrier's
ordinary `materialStores` component; matching material label and colour are
required. No second inventory is maintained. Old raise/lower/level tips retain
version 1, shape height and zero inactive quantity. The shared
`object.sculptTip.edit` action configures both variants; `object.sculptTip`
returns a fixed record with both parameters for programs.

While the user or a program holds a tool, contact within 15 world millimetres of
an accepted field starts one bounded transfer preview. The tip's local +Z is the
opening normal: aligned with field-up by at least 0.6 takes material; aligned
oppositely deposits it. Sideways contact does neither. Dragging or waiting cannot
multiply the dose. Lift, contact loss or release commits both quantities with one
save and Undo. Loose tools are inert. Contact uses the same solid obstruction
checks as physical sculpting. Saved tip radius uses field-local metres; quantities
remain local litres independent of scale or rigid-body mass.

Visible field geometry and the carrier's bounded heap show the draft; collision
continues to use accepted heights until publication. The heap is presentation,
with no collider, per-grain physics, inferred density or airborne spilling. Its
size indicates fraction of capacity, not a second measurement of material.
`object.material.capture` exposes tool, field, direction and a `balance` record
with saved/preview/requested/removed/added/rounding litres. The shared field
capture exposes its one exact centre.

Save failure, tracking/focus interruption and actor takeover retain the draft.
`object.field.resolve` explicitly retries/discards that exact session; retries
verify both original components, the tip and room session, and reacquire surface
ownership. A retained draft prevents editing the carrier or switching workspaces.
Moving a held carrier is allowed; successful publication saves its current pose.
No interruption automatically repeats a transfer. Discard restores accepted
visuals. Destroying the app loses an unsaved in-memory draft.

The editable **Material scoop** template includes a handle/blade recipe, simple
compound collision, a 0.25-litre store and a 0.25-litre scoop tip. Users can change
those same components or add them to imported models. Room v18, paired snapshot
v17 and archive v17 preserve the new semantics. Unknown newer components and
uncertain prior snapshot evidence remain protected. Feature:
`physicalMaterialTools.v1`. Native, shared-client and browser verification are
recorded in the PR checkpoint; physical Quest acceptance remains pending.
