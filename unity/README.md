# Maestro Quest development client

This is an additional Unity client for the shared Maestro runtime. It is under
active development. A development APK runs on Quest 3; it is not store ready.

Use Unity **6000.3.24f1**, with Android build support and an activated eligible
license. Install the editor under a short path: a deeply nested Windows install
can silently omit long-named package files. Keep generated build copies outside
repository/worktree watchers if Unity reports EPERM package-cache renames.

Run in PowerShell 7, using a new dedicated short build directory:

```powershell
./unity/Tools/Verify-Quest.ps1 -Editor 'D:/Tools/Unity/6000.3.24f1/Editor/Unity.exe' -BuildMirror 'D:/Builds/MaestroQuest' -RenderArt
```

Subsequent runs use a source receipt to confirm ownership of that generated copy.
The script copies Assets/Packages/ProjectSettings, configures the development
scene, runs EditMode and PlayMode tests, and optionally renders actual Unity artwork. It never
copies Library caches into the repository. Optional `-PageCapture` uses an
unmodified browser screenshot as the book texture for a desktop art preview.
This does not validate the live Android WebView or any headset interaction.

Build the shared web app, native browser AAR, and ARM64 development APK together:

```powershell
./unity/Tools/Build-QuestDevelopment.ps1 -Editor 'D:/Tools/Unity/6000.3.24f1/Editor/Unity.exe' -BuildMirror 'D:/Projects/Builds/MaestroQuestVerify' -AndroidSdk 'C:/Users/ronit/AppData/Local/Android/Sdk' -AndroidJdk 'C:/Program Files/Eclipse Adoptium/jdk-17.0.16.8-hotspot'
```

The build uses JDK 17, NDK 27.2.12479018, Android minimum 32/target 34,
GameActivity, GLES3 and development signing. Output and SHA256 are under the
mirror's `Builds` directory. Never submit this development identity or debug APK
to the store. `QuestProjectSetup.Configure` enables the XR Hands subsystem,
Meta aim, passthrough and composition layers explicitly.

Current controls: point and trigger for pages; grip to move/rotate book, Maestro
or starter objects; two grips to resize within limits; B/Y to restore the room
in front of the current view. Hand aim uses index pinch on a page for browser
interaction, or on a cover/object for grabbing. Release before changing targets.
Tracking loss and app interruption cancel selection. Objects stay where released.
The Editor also supports mouse page interaction and Home to restore placement.
The movable wooden tool tray adds block/ball/cylinder creation, paint, duplicate,
erase, undo/redo, manual save and room recovery. Tap its pencil, then hold trigger
or index pinch and move your hand to draw in space; tap the pencil again to leave
drawing mode. Book pages and physical tools keep their normal interactions.
Select a creation by tapping it or picking it up; release before paint/erase/undo.
Included book and Maestro cannot be erased or duplicated. New tools stay outside
the book pages, and each tool is a solid 3D object with a text marking.

The second wooden box contains animation tools. Select an object, then tap Record,
move it and tap Record again to save a take (up to 30 seconds, sampled at 10 Hz).
Alternatively use Save frame at successive placements. Earlier/Later selects a
frame, Replace updates it and Remove deletes it. Faster/Slower changes the take's
duration; Play, Stop and Loop control preview. A new recording replaces the
selected object's take; Undo restores the old one. Clips never autoplay on load.

Pose Maestro reveals teal joint handles. Grip or pinch a handle and move it to
bend the joint; turn it to twist. Bone lengths remain fixed and rotations have
bounded ranges. Releasing a handle saves the static pose. Save frame adds that
pose to the animation. Gesture cycles through included gestures; Auto gestures
restores the tutor's automatic activity animation. Stop hides the handles and
restores the saved placement. App interruption finishes a recording and stops
preview before the room save is flushed. This first authoring implementation has
one take per object and 1,200 frames across the room. The user has confirmed wrist
posing and playback on Quest 3; the remaining joint and authoring checks are open.

The third wooden board builds reusable action sequences. New action starts with
a Maestro gesture. Step type switches between a recording, gesture and wait;
Use target takes the current room selection. Add step appends another action.
Duration, Clip loop and Repeat control timing, while On interrupt cycles Restart,
Ignore and Queue latest. Try action previews the sequence; Stop actions ends all
rule playback. Grabbing a target or starting animation authoring takes priority.

Choose an Event and optional Condition, then Add trigger. Web events are changes
to speaking/listening/thinking/idle; VR events are taps, grabs and releases on the
selected Event source. Multiple triggers can use the same sequence. While state
ends a web-triggered sequence when its state ends. Next trigger loads an existing
binding; Remove trigger deletes it. To change a binding, remove it and add its
replacement. The current trigger cooldown is one second. Web pause invalidates
its activity baseline; browser recovery does not replay stale state transitions.

Left button, Right button and Room button create solid action buttons for the
selected sequence. Point with the opposite controller and trigger to activate a
mounted button; grip it with that controller to adjust its offset. A button
ignores the controller it is mounted on and hides if that controller loses
tracking. Room buttons can be operated by either controller. B/Y recovery also
brings room buttons back within reach. Remove button deletes the last button
created for the selected sequence. Undo rules/Redo rules are separate from room
object undo. Rule changes, bindings and button placements autosave to
`rules.v1.json`, with validation and backup recovery.

Current limits are 32 sequences, 16 steps per sequence, 128 triggers, 16 buttons
(up to four on each controller), and eight concurrent sequences on separate
targets. Sequences never autoplay when loading a save or returning to the app.
The current builder supplies numbered action names, preset durations and built-in
events/actions; it does not execute arbitrary code. Custom names, more authoring
controls, avatar replacement, gaze/follow, locomotion and editable input bindings
remain implementation work. Rules and mounted buttons have automated Unity
coverage; their headset usability has not yet been checked. The `-RenderRules`
option on `Verify-Quest.ps1` produces a desktop render of the actual solid controls.

Edits autosave after a short debounce and on application interruption, retaining
a previous valid backup. Room files contain at most 64 user objects and 32,768
stroke points, with 32 undo steps per session. Poses persist relative to the room
content origin; physical-room spatial anchors are not implemented. Drawing,
editing and readable controls are confirmed by the user on Quest 3, and saved
creations return after a process restart. Hand-only and near-touch usability,
long-session stress checks and model imports remain open.

The solid bell beside the book resumes audio after launch or an interruption.
Returning to the app keeps audio paused until that explicit action. Suspension
stops microphone capture, Live/STT/TTS, music and runnable artifacts before the
browser pauses; an unresponsive browser is recreated on return. Saved room and
conversation storage remain, though unfinished page state can be lost during
that fallback. Native microphone permission prompting is still pending.

Quest 3 evidence and outstanding checks: `../docs/QUEST_DEVICE_QA.md`.

The included avatar is generated by `ArtSource/create_maestro.py` using Blender
4.5 LTS. Its original rig and motion are reproducible; the user-supplied cartoon
reference is stored beside the generator. The character is still a development
draft. Runtime content uses the shared graphite and watercolor renderer, while
the book's page image preserves the existing application's appearance.

See `NativeBrowser/README.md`, `../docs/QUEST_V1_PLAN.md` and
`../docs/QUEST_ART_DIRECTION.md` for transport, release gaps and accepted scope.
