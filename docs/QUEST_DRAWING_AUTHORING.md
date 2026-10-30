# Shared pencil strokes

`object.create` has a drawing kind: ordered object-local points, a room-local
origin, uniform scale, colour and radius. It creates the same saved 3D pencil mesh
as physical drawing. The agent can author a stroke through the shared catalog;
optional book fields expose its definition. No separate model-provider connection,
new numeric action or generated engine code is involved.

## Complete stroke editing

Creation accepts 2–64 points. `object.drawing.edit` splices up to 64 points per
call into a saved stroke, or changes its local radius. Splicing at its current
point count appends; deleteCount removes points before inserting replacements.
An empty insertion deletes a range. Every intermediate result must remain valid.
This batching exposes the full native capacity: 2,048 points per stroke and
32,768 across the room. Each explicit edit has one Undo entry.

Read `object.drawing` for exact revision, point count and radius. Read
`object.drawing.points` with that revision and an offset for up to eight exact
points, without simplification. Offset equal to the count returns an empty page;
stale revisions, later offsets and missing/non-drawing objects are unavailable.
Positions are object-local metres within 10 metres of the origin; origin placement
stays within the normal 25-metre room bound. Radius is .001–.02 local metres.
Scale affects geometry and thickness uniformly. Lists of points are literal input;
this does not add arbitrary scripts or change the existing program-value budgets.

Edits preserve motion, placement, paint and physics preferences. Actual mesh,
selection outline and approximate collision bounds refresh through the same
accepted room update and Undo path. Collision remains the existing object-bound
approximation; a curved stroke is not a flexible rope or a per-segment collider.
Undo requires held items released. Normal edits require an unheld, unowned target
outside active authoring. Unrelated actors continue.

Creation returns its exact native objectId; edits return the new revision, count
and radius. Completion follows durable saving, or changes only the temporary fork
until Keep. Failed validation/storage leaves saved data and geometry unchanged.
Receipt replay returns history without another creation/edit, including after Undo.
Selection and physical pencil mode stay unchanged for agent-created strokes.
No operation automatically starts animation or physics. New drawings are fixed;
existing shared physics controls can change that preference.

## Failed physical captures

The physical pencil freezes and retains a stroke in memory if saving fails. It
shows Retry stroke (also available through Save) and Discard stroke controls;
Discard removes only this draft, never the selected saved object. New pointer
input cannot overwrite it. Workspace preservation and temporary-room changes wait
until it is resolved. Draft coordinates are frozen in their original room frame.

`object.drawing.capture` exposes phase, exact session ID, count, temporary flag and
bounded error. `object.drawing.resolve` retries or discards that exact draft through
the physical path. Failed retries retain it; stale IDs cannot affect a later draft.
The result distinguishes saved/discarded, with an empty objectId on discard.
Resolution never starts playback. Session identity is separate from durable action
receipts. App/room destruction loses unsaved memory and releases its write lease;
this is not a crash-recovery archive. No automatic retry occurs.

## Acceptance

Native coverage checks geometry, readback, storage, ownership, failed capture and
retirement. Web contracts use the generated catalog and captured native outcomes.
Browser replay can verify controls and dispatch, not physical stroke smoothness,
headset frame time or real controller tracking. Those device checks remain open.
