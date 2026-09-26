# Expandable Maestro animation library

Design decision, 2026-09-26. This document separates the proposed large-library
architecture from the development implementation. Quest 3 is charging; no new
headset installation or acceptance is implied.

## Keep collecting originals

Keep the existing three category folders and original GLB downloads. Additional
motions can arrive later; neither the default avatar nor the catalogue needs to
be final before development continues. Use a consistent source character and
skeleton template. Keep source terms/attribution with each pack. Do not rewrite
or delete exports to make them match the app.

The read-only inventory tool is `unity/Tools/catalog_animations.py`:

```powershell
python unity/Tools/catalog_animations.py SOURCE_DIRECTORY OUTPUT_JSON
```

It refuses to write into the source directory. It checks binary accessors,
hashes geometry, full joint ancestry/rest transforms and inverse bind matrices,
and records animation payload hashes, names, duration and source categories.
Rest transforms are compared at Unity float32 precision. It does not certify
retargeting, animation quality, foot contact, license permission or runtime
performance. Unsupported compressed/sparse/external data is reported, not
silently treated as compatible. The private report belongs in ignored evidence,
not in release assets.

The current 96-file snapshot totals 885,417,980 bytes (844.4 MiB): 18 travelling,
9 turning/contact-related, 69 mostly stationary. All use the same geometry.
Three unique embedded textures total 7,773,153 bytes; unique animation accessor
data totals 9,308,880 bytes (8.9 MiB). This last figure is raw shared curve data,
not a promised packaged or resident-memory size. There are 192 clips, including
96 clips shorter than 0.1 seconds. Flag short clips as possible export helpers;
keep them available for explicit review rather than deleting them blindly.
The collection is still growing; regenerate the report instead of hardcoding
these counts.

## Asset architecture

Store one avatar definition (mesh, materials, rig, author/terms, revision), plus
separate versioned motion packs. Deduplicate exact source bytes and reusable
textures. Extract validated animation channels at import/preparation time; do
not instantiate a hidden copy of every avatar to play its animation. Preserve
all supported imported joints, even though the app's editable canonical body
rig currently exposes 17 channels.

Each motion needs a stable library ID, source content hash and clip identity,
source rig fingerprint, display name, tags, duration, required channels and
usage terms. Filenames, display names and list positions must not be foreign
keys. A user rename/move must retain their role assignments and visual rules.
Library IDs are assigned and persisted; content fingerprints detect duplicates
and revisions. A revised source should prompt relinking instead of silently
redirecting an existing rule. Keep the migration from current model-hash plus
clip-index references deterministic.

Same-rig playback is the first supported import path. Other humanoid rigs need
validated mapping, bind-pose/axis/scale conversion and an explicit preview.
Nonhumanoid object clips remain attached to their matching object hierarchy.
Do not promise universal interchange merely because a file is GLB or because
joint names match. Require explicit supported deformation channels; unsupported
material/events/cloth behaviour must not silently become a successful import.

## Categorization and movement

Preserve the user's folders as initial tags, not exclusive categories. Separate
these editable fields:

- Intent: greeting, listening, talking, thinking, idle, walk/run, dance, prop use.
- Translation: in place, navigation-driven, authored travel.
- Rotation: preserve heading, authored turn, face target.
- Vertical motion: grounded body motion, step/jump, staged performance.
- Contact: foot planting, sitting, wall/prop target, none/unknown.
- Playback: loop points, speed, blend duration, interruptions and cooldown.
- Space: estimated swept bounds and explicit user acceptance; unknown by default.

Analysis may suggest values from curves. Hip translation alone is not root
travel: parent transforms, scale, facing, rotation and foot movement also matter.
A looping walk selected for Follow should let navigation control placement and
heading. Gesture previews stay at the chosen placement. An authored travelling
performance requires a collision-checked route and clearance at the chosen
scale; it must stop on blocked space, lost scan/alignment or user intervention.
A capsule protects the body, not every animated hand, foot or carried object.
Foot planting/contact solving is separate work. Hair and cloth physics remain
excluded from v1.

Unity's separation of root rotation, vertical motion and planar movement is a
useful basis, but imported GLB curves need their own runtime handling; Unity
Editor import settings alone do not implement a Quest file picker workflow:
https://docs.unity3d.com/6000.3/Documentation/Manual/RootMotion.html

Small rooms should support stationary and miniature use, including walk
preview without travel. Do not require the user to enlarge their physical room
or make scanned walls noncollidable to play ordinary conversational animations.

## Selection and rules

Use searchable names, tags, favourites and visual previews, not hundreds of
physical buttons. Assign motions to avatar roles: idle, listen, think, speak,
walk, run, turn and optional gestures. Roles can hold weighted variations with
cooldowns and priorities. Users may override a role without replacing the
library or rewriting the activity bridge.

A visual rule references a motion ID or role, with loop/duration/speed and
interruption settings. Controller-mounted buttons, book activity and VR events
all trigger the same rule scheduler. Grip, posing, recording, pause, recall and
Stop retain priority. Importing or loading a room must not start actions.
Present library browsing inside the familiar full-page book UI; any extra
interactive controls outside the book remain physical 3D items. Browser-to-Unity
library messages need the same bounded validation as other native commands.

## Quest budgets and delivery

Load only active, next-to-blend and preview motions through a bounded cache.
Index the rest as metadata. Set separate limits for source import, disk library,
resident curves, textures, active meshes and simultaneous blending. Show import
progress, estimated cost and actionable rejection reasons. Exact limits need
Quest 3 profiling; desktop timing is not acceptance. Unload unused payloads and
retain referenced assets through saved rooms, rules and undo history. Deletion
must explain references and offer relinking. Publish versioned formats with
transactional writes, backups and tested migrations.

Ship a small offline core set. Add curated optional packs and local user imports
through the same format; new clips should not require app code or a store APK
update. Meshy is a source provider, not a runtime dependency. Do not add paid AI
generation or upload private models as part of this work.

## Current development boundary and next release gates

The current implementation imports a whole self-contained GLB/VRM, retains its
embedded clips, and bounds each file/library/live models. Development changes
add Maestro clip preview, a persisted walk selection, and an ImportedClip visual
rule action bound to the exact model hash. This is useful for individual exports,
not the completed multi-pack library. The current per-file 32-clip and library
32-file/256-MiB limits must not simply be raised to hold this collection.

Next steps: implement versioned motion-only extraction and rig compatibility;
verify equivalence against original exports; add stable IDs, role assignments
and import/search UI; migrate existing references; add blended transitions and
travel/contact policies; profile large libraries on Quest and test restart,
missing/corrupt packs, duplicate imports, updates, deletion/undo and user rigs.
Validate representative stationary, turning and travelling motions before
bulk-curating hundreds. The user's final Meshy default is still in progress.
