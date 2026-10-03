# Reusable planar surface drawing

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
revision. `object.surface` reads the complete plane configuration. Revision-bound
`object.surface.strokes` and `object.surface.stroke` return six identities or six
points per page, respectively. IDs, colour, radius and exact points remain
inspectable. The same catalog drives agent calls, program blocks and generated
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
workspaces. Current room files are `room.v8.json`, paired snapshot intents are
`room-snapshot.v7.json`, and portable archive manifests are version 7. Clean older
room documents can load through the existing versioned reader; older originals
remain. Prior in-flight snapshot journals and prior archives are preserved and
refused instead of being reinterpreted. This is pre-release format work under the
owner's reset permission; it does not authorize breaking future released saves.

This increment is explicitly planar. It does not project paint onto arbitrary
curved meshes, animated skin, scanned real-world walls or clothing; drawing tips do not follow imported bones. Created/imported roots and recipe parts
can be configured as drawing tools as described below. Configured planes can be
positioned independently of the visual/collision mesh. The current patch hit test
selects the nearest eligible plane, not a complete visual-occlusion query.
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

This does not add a brush-fluid simulator, curved-mesh painting or automatic robot
handwriting planning. An agent can author exact ink directly with surface edits,
or use existing hold/movement/animation capabilities with a configured tool.
Physical contact and maximum-load performance still require Quest acceptance.
