# Quest 3 development verification — 2026-09-26

This is development evidence, not a release acceptance report.

## Packaged imported-motion and small-room update — not installed

APK: `D:/Projects/Builds/MaestroQuestVerify/Builds/Checkpoints/MaestroQuest-avatar-3A2ABC32.apk`.
SHA256: `3A2ABC325317DB88B138038B775D6909283ACE2F7A90D154231807A6FCA3DB53`.
135,177,732 bytes, development signed. Editor build exited 0; zero errors and
two package warnings. APK v2 signature, ARM64 and expected manifest checks pass.
The full build passed 34 EditMode, 33 required PlayMode and 15 native Android
tests, native lint and the shared web build. The two optional local-file checks
were omitted from this redistributable build. A preceding real Meshy Stage Walk
run passed all 35 PlayMode checks and rendered actual tutor clip playback.
Three Python catalogue checks also pass. Evidence is in ignored
`.quest-evidence/avatar-clips/verified-3A2ABC32` and `.quest-evidence/art`.

Added selected-Maestro embedded-clip preview, persisted walk-clip assignment,
ImportedClip visual-rule actions, Size and Preview walk controls, and clearer
blocked-step explanations. Tests cover clip deformation, interruption, pause,
rule duration, exact-model references, replacement while loading, Undo/save,
and a miniature avatar clearing an overhead book. Save/reload tests now flush
pending asynchronous saves using the pause path instead of assuming a short
scaled-time wait completes background I/O.

The Quest is charging and the user is sleeping. No installation, launch, room
restore or device acceptance occurred for this APK. The installed build remains
08F340EF. After the user returns: back up the current room before installation;
check import Play/Stop on selected Maestro; choose Walk clip and Preview walk;
compare included/custom visible gait; check Size and blocker text in the real
small room; trigger a clip from a physical rule button and tutor-state event;
confirm grip/pause/recall interruption, restart persistence and both-hand use.
Do not report the prior stationary-feet defect fixed on hardware yet.

The 96-export animation inventory is read-only. Original models are neither
modified nor bundled into the app. Full searchable motion packs, automatic role
mapping, blended transitions and collision-checked authored travel remain work
specified in QUEST_ANIMATION_LIBRARY.md.

## Installed import-location fix and Meshy acceptance

APK: `D:/Projects/Builds/MaestroQuestVerify/Builds/Checkpoints/MaestroQuest-avatar-08F340EF.apk`.
SHA256: `08F340EF16F03DBB91FBFDDD58557F38FD0C9ADD55E735C2AB2A055EB7C3C323`.
The full build passed 33 EditMode, 31 required PlayMode and 15 native Android
tests, web build, native lint, ARM64/signature/manifest checks and Editor exit 0.
Two optional private-file checks were not requested. Unity reports zero errors
and two package warnings. The APK is 135,173,219 bytes, development signed.

The user's screenshot showed Downloads > Maestro empty although both model files
were present and indexed. Opening the normal document picker at the actual
external-storage folder made the Meshy file visible. Import now supplies the
primary Download directory as an initial-location hint. The user then loaded
the textured preview and confirmed Use Maestro worked after its role was
explained. The saved room contains the matching Meshy hash on `maestro` and on
a separate imported room object. Gestures, joint posing and custom gait still
need explicit acceptance; the preview alone is deliberately not grabbable.

The user also confirmed Follow me translates the included avatar, but reported
stationary feet. A new PlayMode regression check verifies actual visible foot
mesh deformation over two gait cycles after a saved pose and through autosave;
it passes locally. This does not resolve the reported headset gait defect.

Before upgrading, the room and model library were backed up (9,217,211 bytes).
The primary room hash stayed
`FFC819EBC925FBA1DEC6135E7874190517617248D0F9D56DFDDA5E310831CAF3`
through `adb install -r`. The model copy matches the original `220A3A4E` hash.
Cold launch succeeded in 564 ms. The startup sample contains the previously
observed Horizon settings-access exceptions, with no observed managed exception
or fatal crash. Private screenshots, backup, logs and install receipt are in
ignored `.quest-evidence/import-picker`. The updated in-app picker hint still
needs user acceptance; the direct system-picker path was observed on device.

## Installed Mixamo and movement update

APK: `D:/Projects/Builds/MaestroQuestVerify/Builds/Checkpoints/MaestroQuest-avatar-1EAEF7DA.apk`.
SHA256: `1EAEF7DA36A38F83C27EAFF180829E7351B4969CB12CF11500272E01AC873735`.
Size: 135,173,219 bytes. Source implementation commit `0e29c47`, with verifier
follow-up `89a884d`. The shared web bundle, native AAR/lint and ARM64 IL2CPP
build succeeded, including actual Editor exit 0. The package signature and
manifest checks passed. Unity reports zero errors and two package warnings.

The release-independent build runs 33 EditMode, 30 required PlayMode and 15
native Android tests. Two optional private-file tests are explicitly ignored
when no local model is requested; the verifier allows only those exact skips.
The preceding local Meshy run passed all 32 PlayMode tests, including actual
embedded playback, tutor replacement and posed skinned-vertex movement.

The app was closed before its room files were backed up (181,872 bytes total).
An upgrade with `adb install -r` succeeded without clearing data. The primary
room SHA256 remained
`3152A1D002E2FC9CA9E68B1B6E30ED0039670E8BAC1AC21570F271D4FC9F69E2`
through installation. The verified Meshy file was copied separately into
Downloads/Maestro and its device checksum matches the PC original (`220A3A4E`).
No private avatar is packaged in the application.

Cold launch succeeded. A device screenshot shows passthrough, book pages and
the new movement controls in both eyes. The startup sample has no observed
managed exception or fatal crash; the previously recorded Horizon settings
access exceptions remain. Evidence is in ignored `.quest-evidence/device-mixamo`.

User acceptance has been requested for Meshy import/Use Maestro, upright
appearance, gestures and posing; Look at me; and following, stopping distance,
Stop and grip interruption against an actual loaded room scan. Those results
are pending. Existing custom VRM and broader rules, performance and release
checks remain open; installation is not evidence that those checks passed.

## Installed custom-avatar update

APK: `D:/Projects/Builds/MaestroQuestVerify/Builds/Checkpoints/MaestroQuest-avatar-CA09C56E.apk`.
SHA256: `CA09C56E0EBD52745C93A03C4CB55A0743B713A14CC7F433ED16595BB5F897A9`.
Size: 135,149,540 bytes. The full web/native/ARM64 IL2CPP build and Editor exit
succeeded. Thirty-one EditMode, twenty-four PlayMode and fifteen Android tests
pass. Signature v2, architecture and manifest checks pass. Unity has zero errors
and the same two package warnings; native lint has zero errors and five warnings.

The source adds Use Maestro/Default selection, a 17-channel humanoid retargeter,
visible-joint posing, validated private model references and cancellation/fallback.
The regression suite verifies real skinned-vertex movement and preserves an active
recording when switching avatars. See QUEST_CUSTOM_AVATARS.md for scope and limits.

The user reconnected Quest 3. The app was paused before backing up both room
files (181,864 bytes total), then upgraded with `adb install -r`. The primary room
file's SHA256 was unchanged after installation; no app data was cleared. Startup
capture shows the included avatar, physical controls and passthrough in both eyes.
No managed exception or fatal crash was observed in that sample. The known
Horizon settings-access exceptions remain; this is not a clean OS-log claim.

A private copy of the user's `vroidmodel3.vrm` was placed in Downloads/Maestro,
and its SHA256 matches the PC original. It is not packaged in the APK. The exact
OPEN_DOCUMENT/OPENABLE intent resolves to the installed DocumentsUI picker.
The user has been asked to import it, choose Use Maestro, exercise gestures and
posing, and test Default/Undo. Picker callbacks, custom-avatar rendering and
physical controls remain pending human acceptance. Broader rig coverage,
long-session performance and overall store readiness remain open.

## Installed recovery update

APK: `D:/Projects/Builds/MaestroQuestVerify/Builds/Checkpoints/MaestroQuest-physics-B0F3C7EC.apk`.
SHA256: `B0F3C7ECF696528E7686D853C8A613F2B717D60317F3D84ABEC4A1114D75348D`.
Size: 135,117,967 bytes. Full build succeeded with zero errors and the same two
package warnings described below. Thirty EditMode, twenty-one PlayMode and
fifteen Android tests pass. APK signature, ARM64 and manifest checks pass.

- Saved room data was backed up again before `adb install -r`; installation and
  launch succeeded. No app data was cleared.
- Scanned colliders use a separate physics layer excluded from trigger/pinch and
  XRI grip rays. Tests click and actually grab a tool behind a synthetic scanned
  wall while retaining ball/floor/wall collision checks.
- A solid teal Recall control follows each tracked palm independently of movable
  content. Point and pinch with the opposite hand to pause physics and restore
  the room. Tests cover carrying-hand rejection and tracking-loss cleanup.
- All tool text uses a depth-tested font material. A render regression checks
  visible foreground lettering and complete occlusion behind an opaque page.
- Actual Quest capture shows the palm control in both eyes and the corrected
  occlusion. The user answered "yes" to testing opposite-hand Recall activation,
  bringing back the book/tools and retrieving a tray beyond a scanned wall.
- Startup has no observed managed exception or fatal crash. Meta's native haptic
  sample-rate/action-set startup messages and system settings-access warnings
  remain; these were not hidden by granting extra permissions. The prior XRI
  reference-frame warning is absent from the new startup sample.

## First installed room-physics checkpoint

Initial physics APK:
`D:/Projects/Builds/MaestroQuestVerify/Builds/Checkpoints/MaestroQuest-physics-9B7AC37A.apk`.
SHA256: `9B7AC37AEACD7BB738AB6D20465389EE83BB3C260E54E0774143692C010CCF13`.
Size: 108,214,245 bytes. This includes the preceding import/rule/native-input work.

The full web/native/Unity development build succeeds. All 29 EditMode, 19 PlayMode
and 15 Android tests pass; native lint has zero errors and five warnings. Actual
PhysX checks cover gravity, floor/wall bounce, controller contact, XRI throwing,
tracking cancellation, pause/resume and animation ownership. The solid physics
tray was rendered and inspected, including corrected text contrast in linear
lighting. A rendered browser-pixel test confirms the explicit sRGB conversion.

APK v2 signature and ARM64 packaging checks pass. The packaged manifest includes
scene/anchor permissions, GameActivity, the private selected-file provider and
contextual passthrough startup. The optional SDK DevAgent stays disabled; its
editor credentials are cleared before packaging, and unused media-projection
components/foreground-service permissions are absent from the manifest.

Unity reports zero errors and two warnings: XRI's missing sample-cache directory
and Meta's GameActivity template check. The latter inspects the pre-build template;
the final APK was checked and contains the intended UnityPlayerGameActivity.
The Unity splash is disabled and the OS startup background uses passthrough.

After the user confirmed the headset was charged and reconnected, room data was
backed up locally and this checkpoint was installed with `adb install -r`.
Passthrough and the book render on Quest 3; saved user content is preserved.
The user ran room scanning and later confirmed: "physics work tested". This is
a basic device confirmation, not completion of every check below.

The session also exposed two usability faults: scanned walls blocked the ray
to misplaced tools, and hand-only recovery depended on reaching the creation
tray. A device capture exposed labels drawing through the book. Fixes and a
palm-carried 3D Recall control are included in the recovery update above.

Remaining checks include permission/setup cancel/retry, detailed scan alignment,
throws while looking away, live placement, pause/recenter/restart, both-eye
browser colors and sustained performance. See `QUEST_ROOM_PHYSICS.md` for scope
and limits. Custom Maestro replacement and store release gates remain open.

## Uninstalled model-import checkpoint

The preceding import development checkpoint is
`D:/Projects/Builds/MaestroQuestVerify/Builds/Checkpoints/MaestroQuest-imports-6DAE828D.apk`,
SHA256 `6DAE828D4022A32B9F5A8CEE73BD364118FF983BA7E20D39C5A3ADD3DD48E531`.
It includes the prior rules/native-input changes plus bounded GLB/VRM importing,
private local model copies, solid import controls and embedded-clip playback.
The development build succeeded with zero errors and one package warning; APK
v2 signature verification passed. Twenty-seven EditMode, thirteen PlayMode and
fifteen native Android tests pass. Desktop renders verify the actual controls,
shared material, and the user's `vroidmodel3.vrm` as an imported room object.
Ten of the user's 22 VRM files pass the current preflight limits; this is not a
claim that all ten have been fully loaded or that any has passed Quest QA.

No ADB/device polling, installation or headset tests were performed for this
checkpoint while the headset charged. Import
picker availability, resource use, cutout materials, large-file recovery and
physical controls still require Quest acceptance. Custom Maestro switching
remains unfinished. Rigid physics and animation release were added in the later
room-physics checkpoint above.
The user has excluded hair and cloth interaction physics from this scope.

## Previous animation checkpoint

- Unity 6000.3.24f1, Android ARM64 IL2CPP, GLES3, GameActivity.
- Package `com.maestro.quest.development`, version 1.0.0 / code 1, debug signing.
- Installed checkpoint copy:
  `D:/Projects/Builds/MaestroQuestVerify/Builds/Checkpoints/MaestroQuest-animation-916BABE2.apk`.
- SHA256: `916BABE2F109F9AE225EF40C94BF86C6966E67908A4055F9C5BA8E524DFB915F`.
- Build succeeded with zero errors; Android APK v2 signature verification passed.
- Actual APK manifest contains minimum API 32, target 34, required passthrough,
  optional hand tracking, hand permission and hardware-accelerated GameActivity.

## Observed on the physical headset

- `adb install -r` succeeded and the application launched on Quest 3.
- Device capture shows the real room through passthrough behind the physical
  book, animated Maestro, page controls and three starter objects.
- The right page displays the shared application's API-key onboarding; the left
  is the earlier-conversation surface. This new install has no conversation yet.
- A controller aim ray and tip render on the page. The user confirmed grip
  movement, trigger page interaction and B/Y recovery with “all good.” Two-hand
  resizing, hands and extended comfort still need separate physical checks.
- Read-only WebView inspection: document complete; versioned book bridge exists;
  conversation layout; two page sections; zero messages and zero artifact frames.
  WebView is 1024×768 Android pixels; actual CSS viewport is 819×614 on this device.
- A brief startup log sample reports approximately 71–73 FPS against 72 Hz, with
  some stale frames. No claim is made about a full session or stress performance.

Screenshots and raw device logs are kept locally in ignored `.quest-evidence`.
They include the user's physical surroundings and are not release/store assets.

The room-editor update installed successfully. A headset capture shows the
movable solid tool tray, shape tools, pencil, paint, erase, undo/redo, save and
recovery controls. Actual Android storage contains a version-1 room with six
objects and a backup, confirming first-save and replacement-save paths work.
The user confirmed creation, paint, erase/undo, pencil drawing and label
readability: “Everything works and labels are readable.” After that session the
save contained 16 objects, including ten strokes (225 points). A process restart
rendered the saved shapes and drawings again; the room file SHA256 was unchanged.

## Automated interaction evidence

Fifteen EditMode tests and five PlayMode tests passed. The runtime tests exercise the
installed XRI grab implementation: translated hand moves the object, disabling
the interactor releases it without gravity drift, two hands resize within limits,
and restoring placement cancels selection. Gesture tests cover recovery while
pinching, crossing from page to object, and pinching before reaching a target.
Editor coverage includes painting, duplication, erase/undo, stroke geometry,
bounded history/content, protected included objects, save/load reconstruction,
damaged-save recovery and releasing a physical tool on the same target.

## Session interruption update

The final interruption build includes the late-connect TTS acknowledgment and
music preflight fixes. All 20 Unity checks, web production build, native AAR and
lint completed; the APK installed successfully. Build processes use a separate
ADB endpoint (5041), since KAT Gateway's default server stalled Unity shutdown.

The native host now waits for the shared web runtime to close capture, speech,
Live/observer sessions and music playback before pausing WebView timers. Active
artifact frames are unmounted synchronously. Capture requests resolving after
suspension are stopped instead of reaching their callers. Audio stays paused on
return and on native cold start until the physical bell's Resume audio action.
The phone/browser default remains active.

Ninety-eight targeted web tests passed across fifteen files, including Live
transport closure, repeated STT stop ownership, pending TTS connections, cached
playback, late microphone results, artifact unmount and explicit resume. The
production web bundle, native AAR/lint and Unity development build also pass.

On Quest 3, opening Android settings yielded `suspended=true, settled=true,
active=false`. Returning yielded `suspended=false, settled=true, active=false`.
The native resume command enables activity; the physical bell is visible in the
headset capture. A deliberately stalled acknowledgment caused the 1.5-second
fallback to destroy the WebView while retaining the Unity process. Returning
recreated the complete two-page document with audio still paused. The room still
contained 16 objects and ten strokes. No Android crash was recorded. A HOME-key
test exited the Unity process cleanly; it is evidence of cold restart, not of a
same-process pause. The settings test supplied the same-process pause evidence.

These device checks used the onboarding page, without opening a paid session.
Real microphone permission, active Live/audio interruption, thermal/long-session
behavior and human use of the new bell still require hardware acceptance.

## Rules update — computer-side verification only

The local implementation passes 23 EditMode and ten PlayMode tests. New coverage
includes the same recorded action triggered by a real button/router and by web
activity; mounted-button following, opposite-controller filtering and saved
offsets; interruption of a still-moving object by an actual XRI grip; authoring
priority, bounded queues, while-state cancellation and save backup recovery.
The actual Unity rule board was rendered and its desktop labels inspected.
These checks do not establish headset reach, comfort, readability or tracking.

The headset is charging at the user's request. No rule build has been installed
or tested on the device yet. Keep the installed animation checkpoint distinct
from the locally built rules APK:

- Checkpoint: `D:/Projects/Builds/MaestroQuestVerify/Builds/Checkpoints/MaestroQuest-rules-6733BD44.apk`.
- SHA256: `6733BD440395D9B0F7974D37E894D23698AFA52E9B11B9055B5B14945203F6B9`.
- Full build succeeded: shared web bundle, native AAR/lint and Unity ARM64 IL2CPP.
  Unity reports zero errors and one package warning, with no C# compiler warnings;
  native lint reports zero errors and five warnings. APK v2 signature verification
  passes. Development identity and signing remain in effect.

Outstanding rule acceptance:

- Create a gesture or recording sequence, add several steps and try it.
- Attach a button to each controller; use the opposite trigger, then grip to
  adjust it. Confirm tracking loss hides the button and return restores it.
- Bind the same sequence to actual tutor-state and object-interaction events.
- Verify while-state exit, Stop actions, grabbing and posing interrupt playback.
- Confirm saved sequences, bindings and offsets survive an app restart.

## Remaining hardware and release work

Native permission/file access is implemented locally, with twelve Android tests,
fifteen web tests and a real Chrome file-gesture smoke check passing. The Unity
notice token has a desktop render; its headset readability is unverified. No
native-input build has been installed while the headset charges. Required device
checks include first-use microphone grant/deny/retry; interruption during consent;
opening/cancelling the document picker; reading an actual selected attachment in
WebView; and rejecting an artifact-frame request without exposing private files.
The existing explicit audio-resume policy also applies to native dialogs. Native
file selection is not evidence that a GLB/VRM importer exists.

Local native-input APK checkpoint:
`D:/Projects/Builds/MaestroQuestVerify/Builds/Checkpoints/MaestroQuest-native-input-635CB415.apk`.
SHA256: `635CB415BB42C14DA60E8C2D6BA223B87436EA4E24545E692A3C9FB3E4D4A885`.
The full build passes the 33 Unity checks, twelve Android tests, native lint and
web build. APK v2 signature verifies. The packaged manifest contains the private
`com.maestro.quest.development.maestro.selected` provider (`exported=false`,
`grantUriPermissions=false`) and the explicit Unity permission-dialog override.
Unity reports zero errors and one package warning; native lint has zero errors
and five warnings. This APK includes the preceding rules update and remains
uninstalled pending the user's headset availability.

Animation authoring update: 18 EditMode and seven PlayMode checks pass. This
includes a real XRI grab moving the included avatar's head, pose/keyframe undo,
Playables interpolation and looping, object recording interrupted by focus loss,
and room save/load. The development APK builds and is installed. Startup logs
contain no new managed exceptions; the known Meta function lookup remains.
Human acceptance of joint handles, physical animation controls and recording is
in progress. The user confirmed moving a wrist and playing its animation on the
headset; other joints, recording and broader comfort checks remain open.
The installed room currently has three objects (book, Maestro and a
user-created ball); no room data was restored or replaced during inspection.

The rule builder, custom avatar switching, locomotion/following and controller
mounts are accepted upcoming scope and are not available in this build.

- User confirmation of controller interaction, near interaction, hand-only use,
  seated/standing reach, readable text and both eyes at different distances.
- Hand-only use of the recovery tool, sustained drawing/editor usability,
  model imports and room anchoring. Saved poses are relative to
  the content origin; they are not spatial anchors in a physical room.
- Shared conversation, inline artifacts, keyboard/file selection, native audio
  permissions and active-session hardware acceptance of pause/resume behavior.
- Native authentication, managed access/attestation, supported purchases and store
  identity/signing. No Meta dashboard app exists yet.
- Long-session performance, thermal behavior, content limits and recovery.
- Meta OpenXR emits an unavailable `xrDiscoverSpacesMETA` lookup in this device's
  runtime. Current passthrough works; spatial-anchor compatibility still requires
  implementation and validation. The camera manager also warns that raw camera
  images are disabled; this app currently requests compositor passthrough only.

Initial device failures were corrected: use the XR Hands subsystem feature ID
instead of Microsoft's similarly named profile; enable OpenXR composition layers;
enable Android hardware View rendering and offscreen WebView tile rasterization.
