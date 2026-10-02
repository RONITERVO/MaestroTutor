# Expandable Maestro animation library

Design and implementation record, 2026-09-26. Motion extraction, storage,
manual previews, stable-ID rule actions, saved walking assignments and a
searchable two-page library browser are implemented. Per-avatar Idle, Listening,
Thinking and Speaking assignments are documented in QUEST_AVATAR_ACTIVITIES.md.
Broader roles and physical-device library acceptance remain in progress. Device
work remains on hold; no new headset installation or acceptance is implied.

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

The earlier old-rig 2026-09-26 snapshot totals 96 files / 885,417,980 bytes (844.4 MiB): 18 travelling,
9 turning/contact-related, 69 mostly stationary. All use the same geometry.
Three unique embedded textures total 7,773,153 bytes; unique animation accessor
data totals 9,308,880 bytes (8.9 MiB). This last figure is raw shared curve data,
not a promised packaged or resident-memory size. There are 192 clips, including
96 clips shorter than 0.1 seconds. Flag short clips as possible export helpers;
keep them available for explicit review rather than deleting them blindly.
The collection is still growing; regenerate the report instead of hardcoding
these counts.

## Included offline collection (2026-10-02)

The current avatar's supplied `maestro-new-animations` collection has 180 exports
and 178 unique reusable motions. Every export passed native extraction and all
match rig `6e6cd9efcc2ce2881098ecfc0aa4642b3f575630b85da412ad07120978e533a7`.
The collection occupies 65,010,976 bytes after dropping repeated models/textures,
versus 1,770,073,844 input bytes. Hip Hop Dance / Hip Hop Dance 1 and open door /
open door 1 contain duplicate motion payloads; both source origins are retained.
All 178 unique payloads keep 68 animated nodes and 136 channels each. The 17
canonical posing handles are not an imported-animation joint limit. Imported
skinned renderers explicitly retain four bone influences per vertex instead of
inheriting the Android quality preset's two-influence limit. This is distinct
from the number of animated bones. Quest performance still needs measurement.

`Resources/Avatars/IncludedMotions.json` fixes the package identity, revision,
exact avatar/rig, immutable motion IDs and original source hashes/clip indices.
`StreamingAssets/MaestroContent/Motions/<hash>.motion` contains validated motion
containers with no mesh or texture duplication. The extension avoids treating
these internal containers as editor model assets. Provenance is adjacent to the
manifest. User folder observations remain review notes outside execution metadata;
they never automatically choose looping, locomotion or tutor-state triggers.

A genuinely new readable motion library copies the package once in a worker,
without compiling clips or starting playback. It consumes the normal library
budget: 178 entries and approximately 62 MiB of the 128 MiB allowance. Packaged
APK bytes and private portable-library copies are separate storage. The existing
eight-clip / 800,000-value playback cache still loads clips only on demand.

An existing catalogue never silently receives an updated collection. The shared
`motion.pack.install` action accepts `operation: add` and the exact `manifestHash`
from `motion.pack.included.package.manifestHash`. It adds missing content only.
Matching hashes keep existing IDs, personal names, tags, favourites and archive /
removed-download state, including earlier user imports with different IDs. A
changed payload has a different identity and cannot redirect a saved program.
The generated book form explicitly loads this identity from the package fact
into a read-only field; users do not need to type a hash. The agent uses the same
action and observed identity; there is no second installer. Existing per-motion library controls remain available.

`operation: restore` additionally requires one exact saved `motionId` whose
payload is in the current package. It restores that download, preserving its
personal metadata and archive state. Restore does not replace a different rig or
repair inconsistent catalogue metadata. Missing older package content still
requires its original source. Merely adding a newer collection never restores
an explicitly removed download or unhides an archived animation.

Installation validates the complete candidate catalogue and storage budget before
copying. It verifies one payload at a time and atomically publishes metadata only
after all requested copies finish. There is no automatic eviction. Stop is checked
between copies; a dispatched final save can finish. Verified but unreferenced
copies may remain after cancellation/failure and are reused by a retry. Library
writes hold workspace preservation and retirement until they drain. A package
operation never changes the avatar, assigns a gait, changes loop settings or
resumes/starts playback. Private library assets are immediate even in a temporary
room; Keep applies to room edits, not these assets.

Saved libraries and workspace archives include the exact materialized bytes and
metadata, retaining choices even after a later app package changes. Explicit fresh
recovery includes the same default motion collection in its verified portable
snapshot, before normal review/activation. It does not depend on files in the
user's original download folder.

`motion.pack.included` separates package identity and counts from status. Its
catalogued count is by exact content hash, includes archived/removed records and
is not a claim that all local payloads are healthy; ordinary playback checks the
bytes. Existing motion search returns the actual saved IDs.

The native extractor and two representative clips' transform/baked-mesh comparison
validate extraction fidelity. They do not certify every clip's semantics, looping,
foot contact or headset comfort. The current Maestro retargeting path removes
horizontal hip translation for navigation; active gaze/gesture layers can also
alter the final pose. Original-motion playback and navigation-adjusted walking
still need an explicit distinction. No default tutor-state assignments are inferred
from filenames or the user's provisional movement categories.

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

`MotionLibrary` persists the private `room/motions/motions.v2.json` catalogue.
V1 catalogues migrate without changing original files or motion identities.
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
The physical **Batch files** tab now provides a separate 1–128-file import path
with stop/resume, per-file outcomes and retry; see QUEST_BATCH_IMPORTS.md.
It avoids creating full-model previews for the collection. The private batch
audit remains a separate developer verification path. Use `Verify-Quest.ps1 -MotionAuditDirectory SOURCE_DIRECTORY` to
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

Room saves use `room.v2.json`; rules now use `rules.v3.json` after the
avatar-held prop update (QUEST_AVATAR_PROPS.md). Valid v1 rooms and v1/v2 rules
load and upgrade in memory, retaining objects, recordings, raw clip selections,
sequences and controller buttons. First write uses the current format; older
original files remain unchanged. Once a current file exists, corruption cannot
silently load a stale older collection. Valid current backups recover damaged
saves. Unknown newer versions remain read-only even with an older valid backup.
Invalid unrecoverable saves are preserved and saving is disabled. Existing
embedded-clip references keep their exact model hash and index; they are not
silently converted to a guessed library ID.

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
relinking; blending beyond the canonical body state transitions; broader travel/contact policies beyond explicit authored playback; bulk-pack management and deeper recovery tools. Archive, protected local-download
removal, explicit assignment replacement and retained room/rule/profile references
are implemented as described in QUEST_MOTION_MAINTENANCE.md. Profile import and long-session
playback on Quest, including low storage, interruption and large collections.
The selected Meshy default and the current-rig extracted motion collection are
now packaged as described above. The original full-model animation exports stay
in the user's source folder; their repeated geometry/textures are not packaged.

## Automatic tutor-state assignments

The book now assigns up to four saved motions per Idle, Listening, Thinking and
Speaking state for each custom avatar, with weights, speed, looping, reuse gaps
and separate assignment Undo/Redo. See QUEST_AVATAR_ACTIVITIES.md for actual
ownership, persistence and blending limits. Walking remains a separate gait;
more roles, optional motion packs, contact solving and Quest profiling remain open.

## Archive, reference inspection and local storage

The book now separates archiving from removing a local download. Archiving
keeps referenced playback available. Removal checks current assignments, history,
retained saves and active leases; exact reimport restores the same identity.
Optional confirmed forgetting of an unused removed entry reclaims metadata
capacity. See QUEST_MOTION_MAINTENANCE.md for user controls, migrations, recovery
limits and the hardware acceptance still required.


## Explicit authored travel (2026-10-02)

The shared `animation.play` capability accepts optional `movement` for Maestro's
embedded and library sources. Omitted or `inPlace` retains navigation-adjusted
horizontal hips. Explicit `authored` samples every imported node and transfers
the body's planar displacement into Maestro's room placement. A compensating
model-container offset preserves the source's visible pose, including unmapped
joints and deformed mesh. Looping carries the clip's end-to-start displacement
forward. This does not infer semantic root motion, loop quality or intent.

The generated book form, app agent and programs use this same argument. Calls
that include it require `authoredMotion.v1`; older calls remain compatible. The
old simple-step editor cannot represent this policy and refuses conversion
instead of silently dropping it. Existing saved gait and tutor-state assignments
keep their prior behavior.

Authored travel requires a loaded avatar, running room physics, tracking and
clear connected level scanned floor. Each accepted movement checks the scanned
floor, a swept body capsule and personal clearance. Jump height increases the
capsule clearance; this is not per-limb contact or foot planting. Attached props
retain their independent swept trajectory/release checks. The body check excludes
a prop held by Maestro, and treats it as an obstacle again immediately on release.
Blocking, tracking loss, pause or external placement changes
stop the action. Stop/completion retains the last accepted room position and
returns the model container to its normal local placement. It does not teleport
back to the start. Miniature and stationary use remain available in small rooms.

Desktop verification compares one actual shipped spin/jump at five times with
direct source sampling after canonical pose application: all node positions and
rotations match within tolerance, and maximum deformed-vertex difference is
0.00000122 metres. Synthetic checks cover loop carry, unchanged stationary
playback, wall refusal, tracking/physics loss and shared-action cleanup. This is
sampled desktop evidence, not visual acceptance of every clip or physical Quest
performance/comfort acceptance.
