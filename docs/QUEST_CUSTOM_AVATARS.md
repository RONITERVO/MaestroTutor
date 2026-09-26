# Custom Maestro avatars

The import tray's **Use Maestro** action selects a previewed or selected compatible
GLB or VRM humanoid as the tutor. **Add model** keeps its existing room-object meaning.
**Default** returns to the included Maestro. Switching is an undoable room edit;
the room stores the private library's content hash, retaining position, scale,
saved pose, recordings and rule target identity (`maestro`). It does not change
the web tutor persona, backend account or voice configuration.

The user confirmed Meshy selection/replacement on Quest 3 on 2026-09-26 after
opening the actual storage folder and selecting Use Maestro. Saved room data
contains the verified Meshy asset hash for the tutor. This confirms the import
and replacement flow, not yet gestures, posing, gait or long-session performance.

The import confirmation and embedded attribution apply to both Add and Use
Maestro. VRM humanoid metadata or a validated GLB skeleton with Mixamo/Unity
humanoid bone names can establish the tutor rig. A valid Unity humanoid and 15 mapped bones are required:
hips, spine, head, upper/lower arms and hands, upper/lower legs and feet.
Chest and neck are optional. The included source rig has all 17 pose channels.
Fingers, toes, face expressions and spring simulation are not part of this
rotation retargeter.

Named GLB mapping uses the actual skin bones and their ancestors, with explicit
aliases for Mixamo Arm/ForeArm, UpLeg/Leg and Spine1/Spine2 names. It requires
unique names, the expected torso and limb chains, and nonzero limb lengths,
then asks Unity to validate the generated humanoid. Incomplete, ambiguous or
unrecognized skeletons remain ordinary room objects and receive an explanation
when Use Maestro is attempted. Files are not recognized by provider name or
filename. This is not automatic rigging of an unskinned mesh or a promise that
every export from a particular service will work.

Facing alignment uses the mapped hips and applies yaw only, keeping the model
upright. The generated Avatar belongs to the imported instance and is released
with it. Embedded animations are available on imported room objects and selected
Maestro through manual preview and ImportedClip rule actions. The common tutor
gestures, poses and recordings remain available. Shared motion packs and
automatic role assignment are still further work.

The included rig exposes 15 direct pose handles. Hips and neck are recorded and
animated channels but do not have separate grab handles in the current editor.

## Animation and posing

The included skeleton remains the common pose representation, with its meshes
hidden when a custom model is active. This preserves existing recordings, tutor
activity gestures and rule actions. The retargeter maps world rotations using
the source's stored bind pose, the target's bone axes and limb directions;
target bone lengths remain unchanged. Hips translation is scaled to the torso.
Imported models fit a 1.7-metre visual height before the user's saved scale.

Pose handles follow the visible target joints. Dragging/rotating a handle maps
back to the same canonical pose channels, so recording, undo and switching to
the included character retain the pose. Missing optional joints have no visible
handle. This is not an IK/contact solver: body proportions and animation can
still cause self-intersection or imperfect foot/hand contact. These need visual
and headset acceptance for representative characters.

Switching first finishes any active recording or pose edit, then copies the
updated room record. The completed take survives the switch and its Undo.

## Lifetime and recovery

The previous character remains visible while a requested model loads. Requests
carry a generation identity so an older async result cannot replace a newer
selection. A missing, damaged or incompatible model falls back to the included
Maestro and reports the failure on the import tray. The saved hash remains so
the user can restore the original local copy or choose Default. Imported model
scripts are never executed. The existing shared pencil/watercolor rendering and
file validation remain in use.

Six live import slots cover four room objects, one custom tutor and a preview or
replacement; aggregate vertex, texture and morph budgets remain unchanged.
Models can still be rejected when the shared memory budget is exhausted.

## Development evidence

The 2026-09-26 Unity run passed 31 EditMode and 24 PlayMode tests. It includes
real VRM loading and baked skinned-vertex movement, not just saved data checks.
Tests cover tutor selection without an extra room object, retained
limb lengths, mapped gestures, visible-joint posing, canonical channels, Default,
Undo, save/reload, missing assets, incompatible GLB rejection, stale-load
cancellation and preservation of an active recording when changing the tutor.
Room validation rejects avatar paths and hashes on the book.

The optional `-ModelPreview` verification now also renders Idle, Greeting and
Pointing on an actual compatible VRM supplied locally. The local vroidmodel3
preview completed with all 17 mapped channels and visible gesture changes.
The user's model files and rendered evidence stay outside version control; no private avatar is
included in the application package. Device import/replacement usability,
representative rig/artifact coverage and sustained performance remain required.

Development APK checkpoint CA09C56E is installed on Quest 3 after a room backup.
The full build, 55 Unity tests and 15 native tests pass; APK signature, manifest
and ARM64 checks pass. The original model was copied separately into the
headset's Downloads/Maestro folder for user acceptance. Import, custom gestures,
posing and Default/Undo have been requested but not yet confirmed on hardware.
See QUEST_DEVICE_QA.md for exact artifact identity and startup evidence.

The later Mixamo update passes 33 EditMode and 32 PlayMode tests, including two
optional tests of the user's actual Meshy export (`220A3A4E`). Its original
Running clip plays as an object; selecting it as Maestro fits it upright,
retargets the included greeting and preserves arm length. Posing the head moves
actual skinned vertices by about 0.10 metres in the test. Idle, Greeting and
Pointing renders and a runtime posed screenshot have been inspected. Synthetic
Mixamo tests also cover Default/Undo, save/reload and incomplete/ambiguous rig
rejection. The private export is never included in tests or app packaging.
`-ModelPreview <local-file> -ModelAsMaestro -RenderImports` reproduces the local
asset verification. Look/follow behavior is described in QUEST_AVATAR_MOVEMENT.md.

Development APK `1EAEF7DA` now includes named GLB support and is installed on
Quest 3. The verified Meshy export was copied separately into Downloads/Maestro.
Its import/Use Maestro, gestures and joint posing have been requested for
physical testing, but are not yet confirmed. See QUEST_DEVICE_QA.md.

## Embedded animations and future library

Development changes on 2026-09-26 connect the selected custom tutor's embedded
clips to import Play/Stop, the movement tray's walk selection, and visual-rule
ImportedClip actions. Preview and following hold horizontal clip travel at the
chosen placement; authored travel is not yet supported. Stop/pause/editing yield
ownership, and rule references are bound to the current exact model hash.
The original GLB is still stored whole. See QUEST_ANIMATION_LIBRARY.md for the
separate motion-pack architecture needed for hundreds of animations and users'
expandable libraries. These changes still require Quest acceptance.

A later PC development update implements same-rig motion-only extraction,
stable library identities, bounded cached playback and the import tray's Save
motions / Library controls. This keeps additional export geometry and textures
out of the saved motion library. Compatible GLB/VRM 1.0 motions can be previewed
on the current custom tutor; VRM 0.x reusable extraction is explicitly rejected.
Library role assignments and visual-rule references are still pending, and no
library update has been installed while the Quest charges. The current status,
limits and reproducible collection audit are in QUEST_ANIMATION_LIBRARY.md.
