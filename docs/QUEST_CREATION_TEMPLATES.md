# Editable starter objects and atomic creation

`creationComponents.v1` extends the existing `object.create` recipe kind with
optional `collision` and `physics` components. Validation admits the whole object
against recipe-part, generated-vertex and reserved collision-piece budgets before
saving. The accepted result is one object, one save and one Undo. Omitted physics
retains the existing fixed default. Empty collision shapes selects normal automatic
collision. Supplied components cannot pass through the lossy old numeric-step
editor; named capability blocks and generated controls retain them.

`creationTemplates.v1` adds a template kind to that same verb. It expands trusted
bundled JSON into the identical recipe-creation path, with no special toy runtime.
Each new object has independent editable source and uses existing paint, copy,
recipe/collision/settings edits, animation playback and saved/temporary semantics.
A template does not add bindings or programs, start room physics, or auto-play an
animation. Successful receipt replay cannot create it twice.

## First starter set

| Template | Components and intended use | Limits |
| --- | --- | --- |
| Cup | Lathe body/handle, cylinder floor and open ring walls; solid rigid body | Solid items, no liquid capacity or transfer |
| Plate | Shallow lathe dish, floor and rim | No automatic food/fluid behaviour |
| Spoon | Open scoop, handle and compound collision | Small rigid objects only |
| Fork | Handle, crossbar and four solid prongs | Simple rigid cutlery |
| Domino | Upright two-three piece and one simple proxy | Copy/place/topple; no domino game rules |
| Building brick | Body and four visible studs, single bounded proxy | Loose stacking; studs do not interlock or snap |
| Pawn | Lathe chess pawn with conservative cylinder collision | Editable/copyable piece; no chessboard or legal-move engine |
| Ball | Sphere with bouncy physics | Existing throw/roll/contact capabilities |
| Held chalk | Ordinary editable recipe and configurable drawing tip | Flat configured patches; no arbitrary curved/deforming paint |
| Chalkboard | Five editable parts and a drawing patch on the Board part | Flat ink, whole-stroke erase; no curved projection |
| Box robot | Nineteen parented parts, two wave tracks, initially idle | Fixed whole-body proxy does not follow animated parts |

The first set is deliberately smaller than the complete proposed play kit. Curved
painting, further fidgets, socket snapping, a shipped chess layout,
container transfer and snow remain separate increments in
[world authoring](QUEST_WORLD_AUTHORING.md). This set does not fulfill those gates. Existing pieces can now share a
[layout reset](QUEST_LAYOUT_AUTHORING.md) with one Undo; this does not connect
bricks or supply chess rules. [Physical connections](QUEST_PHYSICAL_CONNECTIONS.md)
now provide shared hinge/fixed/slider joins and break events; these are explicit
components, not automatic stud interlocking.

## Discovery, identity and ownership

Canonical files live in `Assets/Maestro/Resources/Creation/Templates`. They are
original procedural examples with Apache-2.0 provenance recorded in each file.
No Meshy API or image-generation service is required. The existing robot shortcut
also expands this source instead of maintaining a second geometry definition.

`creation.template {index}` reads one bounded description, exact SHA-256 hash,
provenance, physics and cost summary; start at zero and stop at `total`. Costs
separate parts, tracks, custom lathe vertices and collision pieces. Primitive
meshes are not included in the custom generated-vertex count. These budgets are
admission limits, not headset frame-time certification. The current library is
limited to 32 entries, with paging/discovery growth to be added before expanding
beyond that boundary.

`object.create {kind: "template", templateHash, name, x, y, z, scale}` pins the
exact UTF-8 source bytes. Empty name uses the template's display name. A missing
hash fails explicitly; names/tags never select a substitute. After expansion,
created objects no longer depend on the template file. Receipts retain the exact
creation choice; full source can be read through the existing object facts.

Keep released template bytes immutable. A future revision needs a distinct file
and identity, retaining published choices while supported. The LF Git attribute,
native resource/hash test and web CI asset checks protect cross-platform identity.
User-authored template publishing/import and larger library management are future
work; users can already edit and copy the expanded objects themselves.

Schema annotations `x-enum-labels` and `x-enum-images` let generic book controls
show readable choices and actual Unity-rendered previews while storing exact
hashes. Previews accept only bundled hash-addressed paths, never external URLs.
The native quick editor also displays the friendly label. Search indexes those
labels, so a cup search discovers the shared creation capability. The original
chat remains the default book surface; this chooser is an optional workspace.

## Verification boundaries

Tests cover byte identity, shared native/web contracts, independent copies,
component preservation through programs, exact hashes, atomic save/Undo, failed
save, receipt replay and real native stacking/domino collision. The full-app
Editor journey creates the template through the same shared client, reads geometry,
collision and physics, and removes the complete object with one Undo. Browser
checks use recorded native acknowledgements and compare the exact submitted call.
Unity renders provide previews of the real recipe geometry and materials.

This is desktop verification. Current Quest 3 grabbing, visibility, small-object
collision, frame time and comfort still need device acceptance. The implementation
has not installed or changed the headset, deployed providers or submitted a Store
build.

The chalkboard expands optional drawing-surface configuration alongside geometry,
collision and physics. Its blank patch is detached editable data; shared
[surface actions](QUEST_SURFACE_DRAWING.md) edit it and inspect saved ink.

The **Chalk** starter adds a single editable drawing tip to an ordinary cylinder
recipe. Hold its forward end against an enabled drawing patch, then lift to finish
a stroke. The ink settings can be changed through `object.drawingTip.edit`; the
same component works on other created/imported roots and named recipe parts.
See [drawing tools](QUEST_SURFACE_DRAWING.md#configurable-held-drawing-objects-2026-10-03).
