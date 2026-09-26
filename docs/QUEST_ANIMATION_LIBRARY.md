# Expandable Maestro animation library

Design and implementation record, 2026-09-26. Motion extraction, storage,
manual previews, stable-ID rule actions, saved walking assignments and a
searchable two-page library browser are implemented. Per-avatar Idle, Listening,
Thinking and Speaking assignments are documented in QUEST_AVATAR_ACTIVITIES.md.
Broader roles and physical-device library acceptance remain in progress. Quest 3 is charging; no new headset installation or acceptance is
implied.

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

## Implemented foundation

`MotionPack` extracts one versioned motion-only container per source clip. It
keeps named joint channels, hierarchy/rest transforms and inverse binds, while
excluding geometry, materials and images. These `.motion.glb` files use the GLB
binary container with an internal Maestro schema; they are not visible model
exports and the ordinary model importer rejects them. Originals are unchanged.

`MotionLibrary` persists the private `room/motions/motions.v1.json` catalogue.
GUID identities survive renames, duplicate imports and restarts. Payload hashes
deduplicate exact motions, source hashes/clip indices preserve provenance, and
revised motions receive new IDs. Source terms are retained separately; identical
motion payloads can have several recorded sources. Atomic payload/index writes,
a last-good backup, damaged-copy repair by reimport and refusal to downgrade an
unknown catalogue version are implemented. Missing or corrupt motions do not
play. Index damage preserves files for recovery.

Compatibility currently requires the same ordered named node hierarchy, float32
rest transforms, joint/inverse-bind data, coordinate convention and named morph
channels. Matching bone names alone is insufficient. Normal GLB and compatible
VRM 1.0 motion conventions are supported by the extractor; VRM 0.x reusable
extraction requests a newer export while ordinary avatar import remains separate.
Cross-rig motion retargeting is not implemented. Tests cover named morph curves;
this is not universal facial-expression support.

`MotionClipCompiler` builds a legacy Unity clip without creating another model,
texture, renderer or hidden rig. Linear/step quaternion conversion matches the
pinned model importer; cubic values and tangents both receive axis conversion.
Library leases keep active clips alive and evict only released cached clips.
The extracted-motion cache is limited to eight clips and 800,000 keyed component
values in total; embedded clips already in a loaded model have their separate
existing budgets. These are provisional limits, not Quest memory measurements.

Other explicit limits: 8 MiB per motion payload, 32 MiB extracted per source,
128 MiB total payload storage, 1,024 catalogue entries and source records, and
16 MiB catalogue metadata. Extraction is serialized and runs off the Unity main
thread. The existing 64 MiB source-file, 32 embedded-clip, 32 full-model and
256 MiB full-model-library limits remain; they were not raised for this feature.

The physical import tray has **Save motions** and **Library**. Import/preview an
animated export, then Save motions to retain its clips without saving another
full model; the preview is released. A selected saved custom Maestro can also
supply motions. Library opens the searchable book spread in the native app;
Next clip and Play remain available on the physical tray. Selections stay in
sync between the book and tray. Compatible saved motions preview on the current
Maestro through the existing animation ownership and fitted placement. Save/import never starts playback. Stop, pause, model changes
and editing cancel playback/loading. The normal list hides sub-0.1-second helper
clips but retains them in storage. Rendering of both the import and library
tray states is part of desktop QA.

Names, tags, favourites and filtering are editable in the book browser. The
optional short-clip filter exposes retained export helpers for explicit review.
Saved motions drive visual rules and the walking gait used by Follow. Existing
embedded-model hash/index bindings remain valid. Hundreds-of-clips headset
usability and performance still require acceptance.
The private batch audit is a developer verification path, not a headset bulk
import UI. Use `Verify-Quest.ps1 -MotionAuditDirectory SOURCE_DIRECTORY` to
exercise real extraction and restart, plus source-versus-library transform and
baked-mesh equivalence on one representative per category. Its outputs stay in
ignored `.quest-evidence/motion-library`; it refuses output under the originals.

## Two-page library browser

The physical **Library** control opens an explicit library spread. The normal
chat pages remain mounted but hidden and inert, preserving the composer,
history and shared session. Back to chat or the physical layout/history controls
return to that presentation. No persistent headers or toolbars are added over
the chat pages, and the external controls remain physical 3D items.

The left page searches names/tags and filters compatible motions, favourites
and short clips, with twelve results per page. The right page previews/stops a
motion, selects loop preview, assigns walking or the currently selected action
step, edits the name/tags/favourite, and pages through all recorded source terms.
A rename retains the ID and existing references. Assignment preserves the
selected rule's target and triggers; it rejects a stale selected step or an
incompatible/loading target. It does not create a new sequence implicitly.
Use the rules tray to create/select that sequence. Selecting/importing/editing
never starts playback. A short clip is labelled and cannot be used as a gait.

Native Unity remains authoritative. The trusted top-level page snapshot carries
one bounded request, with session and monotonically increasing sequence IDs.
Acknowledgement prevents repeated polling from replaying an action. Stop/close
preempt a pending preview; focus/pause rotates the session and discards stale
requests. Atomic metadata writes already in progress may finish, but cannot
restart playback. Native responses are capped at 32 KiB, with twelve rows and
1,500-character source-term pages; no geometry, file paths or executable content
crosses this channel. JSON is encoded as data and rendered as text. No new
JavaScript interface is exposed to artifact iframes.

Desktop checks cover native filtering/pagination, metadata updates, stable-ID
assignments, tray/book selection consistency, invalid requests, repeat/stale
request rejection and cancellation during a cold load. Web tests cover bounded
state validation, acknowledgement/preemption, UI commands, escaped source text,
and preserving the chat draft across library navigation. Native Android tests
verify data quoting and reject malformed/oversize responses.

`Verify-Quest.ps1 -RenderImports` emits synthetic native state for the local
`test-fixtures/browser/library-book.html` fixture. With Vite on localhost:5178,
`node unity/Tools/verify-library-book.mjs` renders both pages at 1024×768 CSS
pixels / 2048×1536 output, checks page bounds, search, metadata edit, return to chat and its inline
HTML artifact interaction. Fixture acknowledgements simulate transport; they are not Android
WebView/headset acceptance. Quest keyboard, hand/controller scrolling, long
names, large collections and sustained operation remain required device checks.

## Saved-motion rules, walking and room migration

The visual rule builder's **Saved** action selects a compatible library motion
with the existing Motion control. It uses the same controller-mounted buttons,
tutor activity events and VR item events as other action sequences. The stored
motion ID survives renaming, metadata edits and compatible model replacement.
Both Maestro and matching imported room objects can play library motions.
Missing payloads and incompatible rest rigs produce an actionable failure.

A preparing action reserves its target while loading. Its playback duration
starts only after the clip is ready; loading has a separate 30-second deadline.
Stop, state exit, focus loss, authoring, grabbing or replacement cancel ownership
and prevent a late load from starting playback. Sequence interruption policies
and the existing 30-second action duration cap still apply. Longer motions need
an explicit capped duration. No arbitrary user code is executed.

**Walk clip** cycles the included gait, embedded clips and compatible saved
motions. **Preview walk** exercises the same saved selection without room travel.
Following uses that gait while navigation owns translation. The included gait
remains available during loading or after a missing/incompatible motion; the
tray reports the fallback. A loaded clip starts only on a still-active movement
frame. Stopping, posing or losing focus cannot leave a late completion playing.
The ID persists through undo, restart and compatible avatar replacement; an
incompatible replacement retains the preference but uses the included gait.
This is not calibrated foot planting, blended gait transitions or authored travel.

Room and rule saves now use `room.v2.json` and `rules.v2.json`. Valid v1 files
load and upgrade in memory, retaining objects, recordings, raw clip selections,
sequences and controller buttons. First save writes v2; v1 originals remain
unchanged. Once v2 exists, corruption cannot silently load the stale v1 room.
Valid current backups recover damaged saves. Unknown newer versions remain
read-only even if an older valid backup exists. Invalid unrecoverable saves are
preserved and saving is disabled. Existing embedded-clip references keep their
exact model hash and index; they are not silently converted to a guessed library ID.

## Desktop verification snapshot

The 2026-09-26 runtime extraction audit read all 96 original exports (885,417,980
bytes), with zero failures and every original hash unchanged. Their 192 source
clips deduplicated to 96 motion payloads: 95 ordinary motions plus one shared
short helper. All reported one compatible rig. Saved payloads total 11,899,044
bytes (11.35 MiB), excluding the separately stored avatar and the JSON catalogue.
This is measured disk storage, not resident RAM or an APK-size estimate. Reopening
the catalogue preserved every motion ID and payload identity. Folder-derived tags
are bounded metadata; full original relative paths remain in the private audit.

The category representatives Discuss While Moving, Alert Quick Turn Right and
Agree Gesture matched the original clip's sampled local transforms and baked
visible vertices at five times per clip, including both endpoints and the short
helper. Maximum measured vertex difference was zero in all three samples. This
checks this pinned importer and these exports; it does not establish universal
retargeting, foot contact, authored travel or headset performance.

The run passed 38 EditMode and 40 PlayMode checks, including three explicitly
selected private-model/collection checks. Required checks cover persistence,
duplicate/revised sources, backup recovery, refusal to downgrade unknown versions,
damaged/missing payloads, active-cache pins/eviction, cubic values/tangents, named
morph deformation, library controls, cancellation during loading and no autoplay.
Actual Stage Walk still passed tutor replacement, posing and anchored clip
playback. Editor processes exited successfully; evidence is retained privately
under `.quest-evidence/motion-library/desktop-verification`. That checkpoint ran 38 EditMode and 37 required PlayMode checks without private
files.

A subsequent saved-motion binding run passed 41 EditMode and 43 PlayMode
checks, with the actual Stage Walk selected and the optional collection audit
omitted. The saved-library walk moved the real leg and deformed visible vertices
by up to 0.1275 metres while keeping the fitted container and room placement
fixed. New checks cover imported-object library playback, controller/state
triggers, rename stability, loading reservations/timeouts/cancellation, fallback,
compatible avatar replacement, Undo/restart and v1/v2 save protection. This is
desktop evidence; device gait and long-session performance remain open. Evidence
is in ignored `.quest-evidence/motion-bindings/desktop-verification`. That checkpoint required 41 EditMode and 41 PlayMode checks without private files.
The expanded state-profile checkpoint requires 47 EditMode and 52 PlayMode checks;
see QUEST_DEVICE_QA.md for its package and acceptance status.

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

Embedded-model preview, persisted walk selection and ImportedClip visual rules
remain available alongside the new reusable-motion foundation above. The large
library is not complete: add broader roles and explicit embedded-clip
relinking; blending beyond the canonical body state transitions; explicit travel/contact policies; library deletion/relinking
and retained references through room/rule undo. Profile import and long-session
playback on Quest, including low storage, interruption and large collections.
The user's final Meshy default is still in progress and private originals are
not bundled into any build.

## Automatic tutor-state assignments

The book now assigns up to four saved motions per Idle, Listening, Thinking and
Speaking state for each custom avatar, with weights, speed, looping, reuse gaps
and separate assignment Undo/Redo. See QUEST_AVATAR_ACTIVITIES.md for actual
ownership, persistence and blending limits. Walking remains a separate gait;
more roles, motion packs, contact/travel policy and Quest profiling remain open.
