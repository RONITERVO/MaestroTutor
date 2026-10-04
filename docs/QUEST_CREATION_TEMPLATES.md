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
| Cup | Lathe body/handle, cylinder floor and open ring walls; solid rigid body | Editable 500 ml cylinder cavity; bounded pouring, no general fluid solver |
| Bucket | Open handled lathe vessel, compound walls and a 2 litre empty store | Dip into a larger configured vessel, lift and pour; no fluid forces |
| Water basin | Fixed open lathe vessel with 32 litres in a 40 litre store | Shared measured water source; no persistent room-wide water field |
| Plate | Shallow lathe dish, floor and rim | No automatic food/fluid behaviour |
| Spoon | Open scoop, handle and compound collision | Small rigid objects only |
| Fork | Handle, crossbar and four solid prongs | Simple rigid cutlery |
| Domino | Upright two-three piece and one simple proxy | Copy/place/topple; no domino game rules |
| Building brick | Body and four visible studs, single bounded proxy | Loose stacking plus editable Top/Bottom snap points; explicit place/join, no automatic stud interlocking |
| Pawn, rook, knight, bishop, queen, king | Six distinct editable recipes with conservative cylinder collision and Foot snap points | Independently movable; no legal-move engine |
| Chessboard | Two recipe parts, 8 by 8 material pattern, 64 named square snap points | Fixed board by default; no occupancy locks |
| Ball | Sphere with bouncy physics | Existing throw/roll/contact capabilities |
| Held chalk | Ordinary editable recipe and configurable drawing tip | Configured plane/cylinder/sphere patches; no arbitrary mesh/skin paint |
| Pencil | Yellow barrel, wood/graphite tip and narrow dark ink | Editable tip, same contact and save path as chalk |
| Paint brush | Purple handle, ferrule and blue bristles with wider ink | Dry surface strokes; no fluid/bristle simulation |
| Eraser | Pink rubber and purple sleeve with an erase tip | Swept whole-stroke selection, one save/Undo per gesture |
| Chalkboard | Five editable parts and a drawing patch on the Board part | Flat ink, whole-stroke erase; no curved projection |
| Snow patch | Fixed backing and one editable 16-cell height field | Shared raise/lower/level paths and matching collision; physical trigger/fingertip/tool gestures; no conserved snow or flow |
| Sculpt brush | Teal handle and wide gold head with a saved sculpt tip | Held-only local raise/lower/level; one save/Undo on lift, no conserved material |
| Box robot | Nineteen parented parts, two wave tracks, initially idle | Fixed whole-body proxy does not follow animated parts |

The twenty-four-template set is deliberately smaller than the complete proposed play kit. Arbitrary mesh/skin
painting, further fidgets, persistent water and physical snow transfer
remain separate increments in
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

## Editable construction examples (2026-10-04)

The included program library also contains **Small fort** and **Passive spinner**.
Both export `create(position, rotation, scale) -> list<text>` and use ordinary
`object.batch.create` source. They are original Apache-2.0 examples in
`Resources/Programs/Modules`, with no external generated-asset dependency.
The book's module search, source inspection, editable copies and the in-app agent
all use that same source and exact module hash. Saving/importing a module does not
run it. A successful constructor run is one saved batch and one Undo; it does not
start physics or install a behaviour.

- **Small fort:** sixteen independent editable objects: one fixed 68 cm square
  base, twelve Building brick copies in four three-brick towers, and three loose
  walls with an open entrance. The bricks retain the existing template's snap
  points, but are not joined. They can be picked up, rearranged or knocked down.
  The fixed platform supports a tabletop-height build. Moving that platform alone
  does not relocate the whole fort; use the existing multi-object layout tools.
- **Passive spinner:** a fixed mount and a separate three-lobed rotor joined by a
  vertical, unlimited passive hinge. Tangential physical contact turns the rotor;
  there is no motor or automatic spin. The hinge settings, recipe parts, colours
  and collision proxies are editable. Capturing the construction produces a
  reusable module whose copies connect their own fresh member IDs.

For example, ask Maestro to build a small fort in an open area, capture its
settled layout as a structure, and create a separate ball. Those are visible
ordinary operations. The fort consumes all sixteen creations allowed in one
program run; create the ball in a separate operation. Start room physics after
room setup, then throw or roll the ball at a tower. The structure's displacement
facts and the included **Structure state waits** module can trigger a user-chosen
reaction when pieces move. The ball should not be a member of the watched fort.
Before resetting, move the ball clear or include its resting position in a
separate reset layout, so it cannot immediately knock the rebuilt fort down.
Capturing a baseline and resetting it each have their own explicit save/Undo.

Native tests exercise the actual exported fort program, one-batch persistence and
Undo/Redo, stable stacks, ball contact, a shared structure reset, spinner contact,
anchor retention, and recapturing/copying a passive hinge. Shared fixtures check
web/native source and hash parity. The full native app journey discovers each
included module, saves and runs its caller, checks its created members and undoes
that batch. Unity-rendered previews show actual geometry. These checks establish
desktop behaviour, not current Quest hand/controller feel, performance or comfort;
those remain device acceptance items. No room format or capability was added.


### Pre-release Cup metadata revision (2026-10-04)

The Cup description now accurately advertises pouring and vessel dipping. Its
recipe, components and rendered preview are identical; changing the source text
creates a new exact template hash. This is a pre-release revision under the
owner's approved development-reset policy, not a migration or alias. Already
expanded objects retain their editable data. A draft pinned to the earlier hash
must explicitly choose the revised template; unsupported hashes never substitute.
The immutable-source policy above applies to published release choices.

## Chess construction and reusable patterns

Create the Chessboard template, then call `create(position, rotation, scale)` on
**Chess pieces white** and **Chess pieces black** at the same board origin and
scale. The defaults use a horizontal board; apply the same yaw to each constructor.
These are ordinary pinned, editable modules: two 16-member prototype batches,
with no new chess action or numeric kind. Board creation and each side add one Undo
entry; a full set is three transactions. An agent should inspect available room
capacity before creating all 33 objects and report any partial completion. Use an
existing temporary-room session when the whole setup needs Keep/Discard review.
The constructors never start physics or the game automatically.

At scale 1 the board is 68.8 cm wide, with 8 cm squares. Local negative Z is the
white side. A1 is dark, H1 is light, queens start on D1/D8 and kings on E1/E8.
Each square has an explicit `ChessSquare` alignment frame, and each piece has a
matching `Foot`. Existing gripping, placement, snapping, collision and object
facts apply. Snapping does not enforce turn order, exclusive occupancy, captures
or check. The agent can read named squares and piece poses; image capture remains
an optional visual aid. Users can author additional rules using shared programs.

`recipePatterns.v1` adds an optional `pattern` to any recipe part: solid, checker
or stripes; UV/XY/XZ/YZ projection; 1–32 columns and rows; and one `#RRGGBB`
secondary pigment. The part colour is the first pigment, and object tint affects
both. The source is shared by native rendering, recipe editing, blocks, agent
calls and `object.recipe.part` facts. It adds no mesh cells, colliders or drawing
surface. Planar coordinates follow the part during movement and animation;
UV follows its generated mesh. Pixel-footprint filtering reduces distant shimmer.

The book's recipe workspace edits these fields through `object.recipe.edit` and
keeps invalid drafts. Missing patterns retain solid appearance. Patterned objects
use room format 14 (snapshot intent and archive 13), so older readers preserve
rather than silently flatten new appearance. The previous pawn was an unreleased
starter: its current dimensions, neutral pigment and foot origin intentionally
replace that draft; its content hash and native preview change. Existing expanded
objects retain their copied source. Released definitions must remain immutable.


## Measured scoop example

**Material scoop** is the 25th editable template. Its handle and blade use ordinary
recipe parts, two collision boxes and a 0.12 kg solid body. A 0.25-litre Snow
store and version-2 scoop tip demonstrate the shared measured transfer kernel.
Configure the same components through the generated action form or an agent task;
contact uses the physical adapter described in [material tools](QUEST_MATERIAL_PACKING.md#physical-material-tools).
It adds no dedicated shovel action, simulator or external generation dependency.
