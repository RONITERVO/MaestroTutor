# Quest 3 development verification — 2026-09-25

This is development evidence, not a release acceptance report.

## Uninstalled model-import checkpoint

The latest development checkpoint is
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

No ADB/device polling, installation or headset tests were performed while the
headset charges. The installed animation checkpoint below is unchanged. Import
picker availability, resource use, cutout materials, large-file recovery and
physical controls still require Quest acceptance. Custom Maestro switching,
scan-based rigid physics and animation release actions remain unimplemented.
The user has excluded hair and cloth interaction physics from this scope.

## Installed checkpoint

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
