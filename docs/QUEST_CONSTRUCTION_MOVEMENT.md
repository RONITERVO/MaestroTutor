# Shared construction movement

The optional book workshop, solid creation-tray **Move pieces** button and agent
use the same native construction selection and capability catalog. This increment
adds arrangement of 1–16 creations with one save and one Undo. Ordinary grips
continue to move individual objects. Explicit [snap placement and fixed joining](QUEST_SNAP_POINTS.md) use a separate
shared action; the physical handle does not yet search for nearby snap points.
A loosely moved stack can still fall apart when physics resumes.

## Direct manipulation

Finish collecting pieces, pause physics and choose **Move pieces** or **Move
together in room**. A solid teal cross appears above the first selected piece.
Grip it to move or turn the arrangement; two grips resize it with a small movement
dead zone. Release the last grip to save. Releasing only one grip keeps the preview.
The book displays whether the handle is available or held. No extra interactive
flat panel is added outside the book.

During a grip, the user owns every member's whole-object channel. Member grips
are temporarily disabled; conflicting edits cannot change part of the preview.
The handle uses Unity's normal XR interaction system for controllers and hands.
Physical Quest reach, comfort and readability still require device acceptance.

## Shared action and projection

`object.layout.transform` takes ordered `members: [{target, revision}]`, desired
room-local `position`, absolute unit-quaternion `rotation`, and relative `scale`.
The first member's current live pose is the pivot. Positions and rotations are
transformed together; each member's size and distance from the pivot multiply by
`scale`. A factor of 1 preserves size. This is distinct from assigning the same
absolute scale to differently sized pieces.

`groupTransforms.v1` advertises the action. All revisions must be current. Physics,
drawing, member playback and animation authoring must be stopped; members must
be idle released creations with ready geometry. Both ends of every attached
physical connection must be included. Final origins must remain within 25 metres of the room
origin and every piece's scale within 0.1–4. The entire request is validated
before any saved edit. Geometry, recorded motion, connection-local frames and saved
structure baselines are unchanged. There is no collision-free placement promise.

The physical preview and action share the same allocation-free pose projection.
An invalid preview keeps the last valid placement. Release restores the original
poses, releases preview ownership, then invokes the ordinary shared action. Failed
admission or saving restores the complete starting arrangement. One successful
call creates one room save/Undo; a repeated receipt cannot apply the scale twice.
Temporary-room changes stay in the fork. Stop cannot reverse a completed edit;
use Undo.

## Handle state and lifetime

`room.selection.manipulate` takes the current selection `stateId`, exact ordered
`members` and `visible`. `constructionManipulation.v1` advertises it. The
`room.selection.manipulation` fact and inline observation expose
`{stateId, visible, holding, error}`. Showing an idle handle claims no members;
actual grip acquisition claims the full set atomically. Showing/hiding does not
change the saved room or selection identity.

Selection changes hide an idle handle. A held handle blocks selection edits.
Pause, recall/editing reset, drawing, physics start, lost ownership and workspace
teardown cancel the preview and release all claims. No cancelled preview saves or
resumes automatically. A member that was independently replaced is never restored
using stale preview data.

## Verification boundaries

PlayMode tests exercise actual XR first/last grip events and two-hand resizing,
rotation/scale around a room-local pivot, current revisions, member bounds,
complete/incomplete connections, one Undo, receipt replay, cancellation, physics start,
temporary rooms and failed-save rollback. The full-app Editor journey shows and
hides the native handle, reads its fact, transforms the included lever's two
members and verifies one Undo. The browser replay checks the real book controls
and generated transform form against those exact native calls and receipts.
These are desktop checks, not Quest performance or headset acceptance.
