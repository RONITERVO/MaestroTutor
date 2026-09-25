# Custom Maestro avatars

The import tray's **Use Maestro** action selects a previewed or selected VRM
humanoid as the tutor. **Add model** keeps its existing room-object meaning.
**Default** returns to the included Maestro. Switching is an undoable room edit;
the room stores the private library's content hash, retaining position, scale,
saved pose, recordings and rule target identity (`maestro`). It does not change
the web tutor persona, backend account or voice configuration.

The import confirmation and embedded attribution apply to both Add and Use
Maestro. Ordinary GLBs can still be room objects but need VRM humanoid metadata
to become the tutor. A valid Unity humanoid and 15 mapped bones are required:
hips, spine, head, upper/lower arms and hands, upper/lower legs and feet.
Chest and neck are optional. The included source rig has all 17 pose channels.
Fingers, toes, face expressions and spring simulation are not part of this
rotation retargeter.

The included rig exposes 15 direct pose handles. Hips and neck are recorded and
animated channels but do not have separate grab handles in the current editor.

## Animation and posing

The included skeleton remains the common animation source, with its meshes
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
