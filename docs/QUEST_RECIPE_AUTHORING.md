# Shared recipe editing

`object.recipe.edit` applies an exact-revision patch to an existing recipe object.
The optional book workshop projects its draft into this same invocation; agents,
programs and generated catalog fields use the native definition. The original
Maestro app still owns provider access. Editing does not recreate the object or
change its identity, placement, tint, physics settings or recorded root motion.

## Patches and playback

`parts` and `tracks` set complete entries by stable part ID. Unmentioned entries
remain. `removeParts` and `removeTracks` explicitly remove existing entries.
Duplicate IDs and set/remove overlap are invalid. Removing a parent never silently
removes children or animation: reparent/remove its children and remove its track
in the same patch. Native ordering puts parents before children and rejects cycles.
All geometric bounds, aggregate part limits and keyframe validation still apply.

`duration` retimes untouched tracks proportionally; supplied tracks use their
explicit times in the new duration. `loop` sets the playback preference. An edit
stops that object's recipe animation and clears saved autoplay. The workshop
states this before Apply. Start animation explicitly afterward. Undo restores the
previous saved recipe, including its autoplay preference; it is separate from
cancelling a completed action. Unrelated actors continue.

There are still at most 32 parts per object, 17 tracks and 16 keys per track, with
256 recipe parts across the room. Smaller explicit patches can reach the full
native capacity within the existing 24,000-character invocation limit. The book
sends only changed entries; excessive patches retain their draft with an error.
No payload is truncated. Part/track arrays remain literal program inputs.

## Readback and current values

- `object.recipe`: exact revision, counts, duration, loop, saved autoplay and live
  playback state. Current-value mappings copy revision/duration/loop only.
- `object.recipe.part`: one full part at an exact revision/index, including local
  position, dimensions, unit quaternion, opaque RGBA colour and stable parent.
  Root parents use empty text. The part is nested under `part` in the response.
- `object.recipe.track`: up to four exact rotation keys for a track index and
  offset at that same revision; includes stable part ID and full key count.
  Offset equal to the key count returns an empty page.

Missing targets, stale revisions and out-of-range indexes/offsets are unavailable.
Coordinates and rotations are not simplified. Query identity and native validation
remain authoritative; the existing shared value limits are unchanged.

## Saving and ownership

Successful patches persist before acknowledging completion, with one Undo, or
stay in the temporary fork until Keep. Validation and write failures leave visible
geometry and saved data unchanged. Held/owned/actively authored targets are refused.
The saved patch refreshes geometry, selection bounds, tint and the default approximate
collider. Explicit collision recipes stay unchanged; edit them separately.
Recipe parts remain visual children of one grabbable rigid assembly; this does not
add articulated part physics or cloth/hair interaction.

The visual workshop keeps stale and rejected drafts. Apply requires native feature
support and a completed matching receipt before clearing its draft. There is no
fallback through a different edit path. Historical receipt replay never reapplies
an edit after Undo. Older command handling remains for prototype callers; the
current book and agent instructions prefer the shared operation.

## Acceptance boundary

Native tests exercise actual recipe meshes, playback, storage and Undo, including
full-capacity reads and failures. Browser replay checks real controls against
captured native requests/results. Physical Quest frame time, readability and input
comfort remain separate device checks.


## Editable revolved profiles (2026-10-02)

`latheGeometry.v1` extends the same version-1 recipe with shape `lathe`.
A part has a closed `profile` of 3–16 `{x,y}` points and integer `segments` 8–48.
Profile x is radius 0–0.5 and y is height -0.5–0.5; the part dimensions scale the
result in X/Y/Z. The final edge closes implicitly; do not repeat the first point.
Profiles must be simple, counter-clockwise and have nonzero area, without touching
or crossed nonadjacent edges. Primitive parts omit these fields or use [] and 0.

The compiled native mesh generator creates triangles, normals and UVs. It removes
axis-degenerate triangles and closes the angular seam. Visual openings are retained;
no external generation provider, downloaded code, OpenSCAD compiler or new mesh
library is introduced. The part transform, tint, animation and saved-edit path
remain shared. Replacing/deleting geometry releases the owned generated meshes.

The optional book workshop offers the shape, a cross-section preview, profile-point
fields and insertion/removal, and segment count. Invalid drafts are retained.
Switching into lathe starts with a cup cross-section; clicking the already selected
shape preserves the current profile. Generated capability forms and agents use
object.create kind=recipe and object.recipe.edit. New geometry is feature-gated
through nested catalog schemas for one-off actions and saved programs.

`object.recipe.part` retains its base record. `object.recipe.profile` reads up to
four exact points, segment count and conservative whole-recipe generated vertex
cost at an exact object revision. Offset equal to count is empty; stale or missing
parts are unavailable. The shared program value limits are unchanged. The room
permits at most 262,144 generated lathe/extrusion vertices in addition to the existing object,
part, imported-model and drawing bounds. This is an admission bound, not proof of
comfortable Quest performance at that maximum.

This increment adds geometry. The assembly still uses its existing approximate
rest-bounds collider by default. An explicit [collision recipe](QUEST_COLLISION_AUTHORING.md)
can provide a hollow interior for solid objects. Liquid/container behaviour is separate.
Physical connections, bounded container pouring, outline extrusion and an initial
editable play kit now share this authoring system. Sweeps, general CSG and
persistent water/snow remain open. Real Quest input/readability/performance
acceptance remains required.


## Editable outline extrusion (2026-10-03)

`extrusionGeometry.v1` adds `shape: "extrude"` to the same recipe-part schema.
The implicit closed `profile` contains 3–32 normalized XY points in [-0.5, 0.5],
in counter-clockwise order; `size.x/y` scale the outline and `size.z` sets thickness.
Concave outlines and collinear edge points are supported. Do not repeat the first
point at the end. Holes, crossings, touching edges, zero-area polygons, short
edges and nonzero `segments` are refused before allocating a Unity mesh. Segments
may be omitted or zero. The minimum edge length is 0.001 normalized units and
minimum signed doubled area is 0.0002. This is a bounded polygon operation,
not arbitrary mesh input, executable code or general CSG.

The native evaluator triangulates caps with bounded ear clipping and gives caps
and walls separate normals/UVs. Straight-edge insertions stay in the saved outline
and wall mesh; cap triangles omit redundant collinear points. Each part reserves
six vertices per profile point, at most 192. Lathe and extrusion costs share the
existing 262,144 generated-vertex room limit and 256-part limit. Built-in primitive
meshes remain separate from that generated-vertex accounting. These are admission
limits, not a headset frame-time measurement.

Users select Extrude in the existing part editor and edit numbered XY points,
including insertion/removal. Maestro and programs use the same `object.create`
recipe variant and `object.recipe.edit` patch. `object.recipe.profile` now pages
lathe and extrusion outlines with exact object revisions; extrusion has segments
zero. `object.recipe.part` supplies shape, dimensions and placement. Accepted edits,
failed writes, Undo, copied/prototype source and temporary-room discard use the
ordinary recipe lifecycle. Replaced meshes are released.

Collision stays an explicitly configured proxy. A concave visual notch does not
silently produce a concave dynamic collider; compose appropriate bounded collision
shapes when it matters for play. This also avoids giving every decorative outline
its own expensive collider. Extrusion does not add skinning or deformable physics.

Room format 10 prevents an older reader from treating new geometry as corruption
and falling back to an earlier room backup. Clean room versions 1–5 and 7–9 still
load, preserving their original files; version 6 and uncertain old transactions
remain unsupported. Paired snapshot intent and workspace archive formats are 9.
Device handling/readability and sustained performance still require Quest testing.
