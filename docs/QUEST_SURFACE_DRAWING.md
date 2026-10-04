# Reusable surface drawing

The `drawingSurfaces.v1` increment adds explicit drawing patches to user-created
objects, including imported model roots and named recipe parts. The Chalkboard
starter is an ordinary five-part recipe with a Front patch on its Board part.
It has no special drawing runtime. The full-page chat remains the book interface;
advanced configuration uses the existing optional catalog/workshop.

## Shared editing and readback

`object.surface.edit` configures a patch, adds or splices a stroke, removes one
stroke, clears ink, or removes the patch. The caller names the object, exact
current object revision and patch ID. Changing patch position/rotation moves its
existing ink deliberately; shrinking cannot silently crop it. `enabled` controls
new drawing, not whether saved ink is visible. Each successful edit is one saved
edit and one Undo, or one edit in the current temporary fork. Keep/Discard retain
the existing room semantics. Failed storage does not publish partial ink.

`object.surfaces` reads patch IDs, anchors, enabled state, stroke counts and object
revision. `object.surface` reads the complete patch configuration. Revision-bound
`object.surface.strokes` and `object.surface.stroke` return six identities or six
points per page, respectively. IDs, colour, radius and exact points remain
inspectable. Objects with no patches and end-of-list stroke/point pages return
typed empty lists; stale revisions and offsets beyond the end still refuse the
read. The same catalog drives agent calls, program blocks and generated
book fields. Reading current inputs refreshes revision without changing the
chosen operation or stroke geometry.

A patch uses local X/Y coordinates, local z=0, and faces local -Z. Its dimensions
are 0.02–4 local metres. The root object transform, or the selected stable recipe
part transform, carries it during movement, scaling and animation. Rendering adds
owned ink meshes with no extra colliders or rigid bodies. An imported model patch
follows its root, not skin deformation or imported bones.

Limits are four patches per object, 64 patches and 128 surface strokes per room,
32 strokes per patch, 512 points per surface stroke, and the existing shared
32,768-point room budget including free-space drawings. Shared add/splice calls
accept at most 64 points per call; physical capture can retain up to 512. These
are admission limits, not a claim of verified Quest frame time at full capacity.

## Physical tools

The tray's Surface pencil draws on enabled patches within 25 cm of the controller
or hand ray. A miss produces no floating stroke. Select a paint well to choose
next-stroke colour; it does not recolour the selected board while drawing.
Erase ink removes the nearest complete stroke per tap with one Undo, never the
board. The local erasing tolerance is twice the pencil radius, with a 1 cm minimum.

`drawing.tool.set` selects off, space, surface or surfaceErase, RGB and radius;
`drawing.tool` reads those session-local preferences. Selecting a tool does not
start a stroke. The physical trigger/pinch begins capture. Release the object
before drawing; a user grab interrupts capture and retains a completed draft.
Retry acquires ownership again and checks the exact patch has not changed.
Changed/deleted patches require discarding the retained draft. A storage failure
also retains the preview and blocks room-switch/save boundaries until explicit
retry or discard. Moving the object while a draft is retained keeps its local ink.

## Persistence and boundaries

Ink is part of copied objects, room Undo, temporary snapshots and portable
workspaces. Current room files are `room.v13.json`, paired snapshot intents are
`room-snapshot.v12.json`, and portable archive manifests are version 12. Clean older
room documents can load through the existing versioned reader; older originals
remain. Prior in-flight snapshot journals and prior archives are preserved and
refused instead of being reinterpreted. This is pre-release format work under the
owner's reset permission; it does not authorize breaking future released saves.

Configured plane, cylinder and sphere patches are supported. This does not project paint onto arbitrary
other curved meshes, animated skin, scanned real-world walls or clothing; drawing tips do not follow imported bones. Created/imported roots and recipe parts
can be configured as drawing tools as described below. Configured planes can be
positioned independently of the visual/collision mesh. Physical drawing selects the nearest eligible patch and checks the path against
solid collision proxies, including scanned-room colliders. This is not a pixel-level
visibility test; a coarse proxy can block a visible opening. The receiving object
and held drawing tool ignore their own proxies because configured planes and tips
can sit inside those approximate bounds. Controller colliders and trigger volumes
do not block drawing. Other solid objects do.
Physical Quest comfort/readability and maximum-load performance remain device
acceptance gates. Native and browser checks are documented with their evidence
when the increment is packaged.

## Configurable held drawing objects (2026-10-03)

`object.drawingTip.edit` configures or removes one tip on any user-created object,
including an imported model root or a stable recipe part. `object.drawingTip`
returns the exact saved definition, whether it exists, and the current object
revision. Agent calls, program blocks and generated fields share this contract.
The Chalk starter is ordinary editable recipe/collision/physics/tip data.

Tip position and rotation use the selected anchor's local coordinates; local +Z
points toward the page. Ink colour and radius are separate from object paint and
tray preferences. Radius uses the receiving patch's local metres. The contact band
is one world centimetre on either side of the tip, independent of tool scale.
A tip draws only while the user or a native held-prop attachment holds the tool.
Loose objects do not scribble. Disabled tips retain their configuration. A newly configured tip arms on a released-tool frame or after separation.

The physical tip calls the same `SpatialDrawing` capture as trigger/pinch input.
There is one active or retained capture per room. Contact loss or release closes
one stroke/save/Undo; pauses and ownership interruptions preserve the existing
capture rules. While kept held, a busy, blocked or completed contact cannot restart until the tip
separates. Release/re-grip is also an explicit new activation; a retained draft
still requires retry/discard first. Failed saves never retry automatically. User-held tools use control
priority; Maestro-held tools use program priority and cannot interrupt a human's
surface ownership. Tips never draw onto their own object's patches. Manual edits, including Undo, wait while a capture is active or retained. Disabling
or reconfiguring a tool retains its draft instead of saving during reconciliation.
Copy, archive, temporary Keep/Discard and Undo carry the same component and ink data.

This does not add a brush-fluid simulator, arbitrary curved-mesh painting or automatic robot
handwriting planning. An agent can author exact ink directly with surface edits,
or use existing hold/movement/animation capabilities with a configured tool.
Physical contact and maximum-load performance still require Quest acceptance.

## Physical obstruction (2026-10-04)

Trigger/pinch surface capture, surface erasing and held drawing tips share the
same obstruction query. A solid object between the ray origin and receiving plane
prevents contact. Starting inside another solid also prevents contact. Introducing
an obstacle during a stroke closes the valid partial stroke through the normal
save/Undo path; it does not join ink across the hidden area. A blocked eraser leaves
ink unchanged. Explicit source edits by the agent, programs or book workshop remain
available independently of physical visibility.

The query synchronizes moved colliders and uses fixed 64-entry overlap and hit
buffers, with a conservative miss if a buffer fills. Forward and reverse casts
handle either side of a one-sided scanned mesh without modifying global physics
settings. A 0.1 mm endpoint tolerance avoids treating a coplanar receiving wall as
an obstruction. This checks the centre contact ray, not the full width of a brush.
The global tool-selection mask still ignores scanned walls so users can recover
trays beyond them; only physical drawing uses this solid-obstruction policy.

Seven PlayMode scenarios exercise real physics queries, partial-stroke saving,
eraser protection, explicit source edits, moved/disabled colliders, both wall sides,
held chalk, saturated buffers and transformed surfaces. Device latency, contact
comfort and maximum-load performance remain pending headset acceptance.

## Curved patches (2026-10-04)

The same `object.surface.edit` definition accepts optional `shape` and
`curvatureRadius`. Omitting both retains a plane. Use `cylinder` or `sphere`
with radius 0.01–4 local metres. Plane radius is zero. Width is arc length,
at most one circumference; sphere height is meridian arc length capped at
0.9*pi*radius to avoid the poles. Shapes remain explicit authored geometry;
they are not inferred from an imported mesh. A patch still follows the object
root or a named recipe part, including rotation and scaling.

At local coordinate (0,0) the surface is tangent to z=0 and faces -Z. Its
curvature centre is (0,0,r). For cylinder points, x/r is angle around Y and y
is height. Sphere points use longitude x/r and latitude y/r; horizontal lengths
shrink away from the equator. Stored stroke coordinates remain exact (x,y,0).
A seam or patch edge ends physical capture; continue with another stroke.
Changing curvature deliberately remaps existing ink, subject to validation.

Saved ink and live previews map to the same surface. Subdivision is at most
five degrees and adapts further to keep chord sag below a quarter of the ink
radius. Each stroke admits at most 2,048 rendered points; the room's 32,768-point
budget now counts those rendered points, including spatial strokes. Source stays
limited to 512 editable points per surface stroke. Physical erasing measures
against the curved centreline. Back-facing or inside-shell rays do not paint;
other solid obstructions retain the shared drawing policy.

`curvedDrawingSurfaces.v1` is required for explicit geometry fields. The existing
surface definition fact exposes `geometry: {shape, curvatureRadius}` alongside
its prior fields, within the bounded program value size. Human fields, agent calls,
physical pencils and held tips edit the same source. Copies and captured
construction modules preserve curvature and ink. Curves use surface version 2;
planes retain surface version 1. Room v12, paired intent v11 and archive v11
prevent older builds from falling back past these new saved surfaces.

Native acceptance covers analytic contact, source/budget limits, actual drawing,
held chalk, erasing/Undo, scaled frames, failed-save retry, copying and temporary
Discard. Real Quest contact comfort and maximum-load performance remain open.
Arbitrary triangle/UV painting and skinned deformation remain unfinished.
The scanned-plane ink increment below adds explicit saved overlays.

## Held erasers and drawing defaults (2026-10-04)

Pencil, Paint brush and Eraser join Chalk as ordinary editable recipe objects.
Any configured drawing tip can use `mode: "draw"` or `mode: "erase"` through
`object.drawingTip.edit`; the saved tip and its fact expose that mode. Omission
retains drawing. Explicit modes and batch erasure require `drawingErasers.v1`.
Drawing tips retain component version 1; erasing tips use version 2. Room v13,
paired snapshot v12 and archive v12 protect that distinction from older readers.

A held eraser sweeps over complete strokes on the contacted plane, cylinder or
sphere patch. Its own radius sets tolerance (twice radius, with a 1 cm minimum),
plus ink thickness. Samples connect along the configured curved surface. Movement
over 35 local centimetres ends a gesture instead of erasing across a jump. Solid
obstructions, contact loss, grip release and ownership follow the same drawing
path. Loose erasers are inert; Maestro-held tools cannot interrupt a human.

Touched strokes disappear in preview while their accepted saved source remains
unchanged. Contact loss or release commits all selected IDs as one saved edit and
one Undo. The existing tap eraser remains one whole-stroke edit per tap. Agents,
programs and generated book fields can call `object.surface.edit` with operation
`removeStrokes` and 1–32 unique exact IDs. A stale revision, duplicate or missing ID
refuses the entire edit without partial removal.

Failed saves and interruptions retain the selected IDs and original patch, with
**Retry erasing** and **Discard erasing** on the tray. Retry reacquires ownership
and checks the original patch; it never silently recomputes which strokes to erase.
Discard restores the visible ink. The shared capture fact and resolution receipt
report mode and selected-stroke count, with zero point count for erasure. Existing
room-switch and temporary-room protections apply to this retained edit too.

This is whole-stroke erasure, not cutting individual stroke segments or wet paint.
Desktop tests cover curved sweeps, physical grip/contact, solid obstruction,
priority, failed-save recovery, atomic batch editing and Undo. Quest contact feel
and performance at maximum admitted drawing capacity remain acceptance work.


## Saved scanned ink layers (2026-10-05)

`scanDrawingLayers.v1` adds `drawing.layer.edit` and `object.scanDrawing`.
A layer is a saved Drawing object with no free-space stroke, exactly one root
plane named Canvas, and one versioned `scanAnchors` binding. The binding holds
exact Meta room and anchor IDs plus plane-local x/y and roll. It adds no panel
or solid collider; ink uses the existing surface renderer, pencil, held tips,
eraser, editable stroke records and Undo. No new model provider is involved.

Read `room.scan` for its current stateId. `atGaze` creates on the tracked viewer's
first front-facing scanned plane within four metres. It refuses a first hit that
is too small rather than selecting a farther plane. `create` chooses the exact
anchorId and placement. Width/height are 0.02–4 metres; all corners and edges must
fit the loaded rectangle and available polygon outline. The native scan adapter
admits at most 256 polygon points per surface and 4,096 total. Outlines are used
locally for fit, not added to the shared layout facts. Openings absent from the
scan remain unknown. This is an overlay, not triangle/UV painting or a guarantee
that a real wall is intact, unobstructed or accurately aligned.

The plane faces the scan's +Z outward normal, 6 mm above it, converting to the
existing ink convention of local -Z. Exact matching anchors update its live pose.
Unavailable tracking, a missing anchor, a different room or a boundary that no
longer fits hides the layer and disables physical contact without deleting ink.
A matching anchor can restore it. No label or proximity substitution occurs.
`object.scanDrawing` exposes saved placement, current revision/stateId, size and
live visibility/reason. The scan ID is empty while unavailable.

`rebind` is an explicit saved edit with the current scan ID and layer revision.
It preserves all ink and adds one Undo. Generated controls refresh those guards
from one fact, while create refreshes room.scan. Existing `object.surface.edit`
can edit strokes, enable drawing, clear ink or change dimensions, but Canvas must
remain one root plane. Resizing beyond a current scan hides it until it fits or is
rebound. Delete removes the whole layer with Undo. Loose movement, grabbing,
scaling, physics, prop attachment and object animation are refused; Recall leaves
layers on their anchors. Free-space stroke facts remain for ordinary 3D strokes.

A changed or lost anchor interrupts physical capture and retains the draft.
Retry requires available tracking, the same saved binding and unchanged ink
patch; Discard is always available. Creation, rebind and stroke edits use the
ordinary saved transaction/temporary-room path. Room v20, paired snapshot v19 and
workspace archive v19 keep old readers from dropping bindings. Exports contain
saved IDs, placement and user ink, not scan meshes or polygon outlines.

Native geometry, persistence, failed-save recovery, physical capture/eraser,
missing-anchor recovery, rebind, Undo, temporary Discard and legacy-command guards
passed desktop tests. Shared-client tests validate the real native receipt/fact and
generated input guards. The audited development APK is recorded in
[the delivery record](QUEST_V1_PLAN.md). Real Quest alignment,
contact feel, scan reload/rescan recovery and sustained-load performance remain
required before release.
