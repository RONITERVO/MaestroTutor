# Quest 3 development verification — 2026-09-25

This is development evidence, not a release acceptance report.

## Installed checkpoint

- Unity 6000.3.24f1, Android ARM64 IL2CPP, GLES3, GameActivity.
- Package `com.maestro.quest.development`, version 1.0.0 / code 1, debug signing.
- APK: `D:/Projects/Builds/MaestroQuestVerify/Builds/MaestroQuest-development.apk`.
- SHA256: `BD63A52BAB39B707F23E21BD213BBB39050FCBEA0B7EC78A4944239309E63A22`.
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

## Remaining hardware and release work

- User confirmation of controller interaction, near interaction, hand-only use,
  seated/standing reach, readable text and both eyes at different distances.
- Hand-only use of the recovery tool, sustained drawing/editor usability,
  model imports and room anchoring. Saved poses are relative to
  the content origin; they are not spatial anchors in a physical room.
- Shared conversation, inline artifacts, keyboard/file selection, audio permissions,
  focus/pause stopping all media and paid sessions, and explicit media resumption.
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
