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

`-RenderImports` captures the actual solid import tray and imported test geometry.
Optional `-ModelAuditDirectory` inspects user-supplied VRM files without modifying
them, and `-ModelPreview` imports/renders one local file in the Editor. See
`../docs/QUEST_MODEL_IMPORTS.md` for limits and import evidence. The import tray's
Use Maestro selects a compatible VRM as the tutor; Default restores the included
character. Switching, poses and recordings persist through save/load and Undo.
See `../docs/QUEST_CUSTOM_AVATARS.md` for the 17-channel retargeter and its limits.

The rules tray's physical Props tab fits one created item to an avatar hand. A
Maestro animation step can return, drop or throw that prop and use the existing
event/button triggers. See `../docs/QUEST_AVATAR_PROPS.md` for authoring and limits.
`-RenderRules` captures both physical rule-control pages in Verify or Build.

The book library's Usage and local storage section supports archive/restore,
protected download removal and optional forgetting of unused removed entries.
See `../docs/QUEST_MOTION_MAINTENANCE.md` for exact reimport recovery and limits.

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

The third wooden board edits reusable behaviours from the native capability
catalog. New action starts with a Maestro gesture. Block type cycles registered
actions; Prev/Next block and Prev/Next field select what to edit. Value -/+ adjusts
numbers, choices and booleans; Step chooses numeric precision. Set field uses the
selected room object or toggles an optional field. Apply draft saves one Undo
edit without running it; Discard draft reloads the latest saved version. Try
action requires a clean draft, and Stop actions remains available at any time.

Edit in book opens the same program in the full editor for text, motion choices,
expressions, functions and detailed structure. Library motion assignment follows
the same selected literal block as the tray. The Props page retains fitting plus
Repeat, On interrupt and While state controls. Grabbing or authoring a target
takes priority over playback. See [native quick edits](../docs/QUEST_NATIVE_QUICK_EDITS.md).

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
`behaviours.v2.json`, with program validation, atomic writes and backup recovery.
This pre-release format starts a fresh behaviour/trigger/button collection instead
of migrating development `rules.v1`–`rules.v5` or numeric-program `behaviours.v1` files; those old files stay untouched.
Room creations, models, motion downloads and original web chat saves are retained.
Room saves still use `room.v2.json` and keep their existing room recovery policy.
Unknown whole-collection formats remain protected. Unsupported individual programs
retain their source and diagnostic while other behaviours stay editable.
Programs use named capability calls and arguments. The generated catalog supplies
web validation and agent signatures; native domain/readiness checks still decide execution.

Current limits are 32 behaviours, 128 statement nodes per program, 128 triggers,
16 buttons (up to four on each controller), and eight concurrent sequences.
Sequences never autoplay when loading a save or returning to the app.
Quick edits preserve the canonical program's branches, expressions and result
bindings; they cannot flatten a complex program. The interpreter does not execute
arbitrary code. Version-3 event programs support session state, monotonic timers,
named events and per-channel ownership; durable state across restarts and general
parallel branches remain release work. Saved motion IDs survive renames and compatible
model replacement; loading time does not consume their action duration. See
`docs/QUEST_ANIMATION_LIBRARY.md` for library limits and migration details. Rules and mounted buttons have automated Unity
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
that fallback. Microphone use now requests Android permission when needed. If the
system dialog interrupts the app, resume with the bell and try speaking again.
Permission and file-picker notices appear on a solid token beside the book; tap
it to dismiss. App file inputs open the Android document picker and receive
bounded copies of the files explicitly selected. Camera capture, keyboard and
authentication integration remain work. Native requests have automated tests;
the permission and file-picker flows still need physical Quest acceptance.

Quest 3 evidence and outstanding checks: `../docs/QUEST_DEVICE_QA.md`.

The included avatar is generated by `ArtSource/create_maestro.py` using Blender
4.5 LTS. Its original rig and motion are reproducible; the user-supplied cartoon
reference is stored beside the generator. The character is still a development
draft. Runtime content uses the shared graphite and watercolor renderer, while
the book's page image preserves the existing application's appearance.

See `NativeBrowser/README.md`, `../docs/QUEST_V1_PLAN.md` and
`../docs/QUEST_ART_DIRECTION.md` for transport, release gaps and accepted scope.

Rigid item physics and MRUK room scanning are described in
`../docs/QUEST_ROOM_PHYSICS.md`. The solid physics tray controls scene access,
scan visibility, gravity startup, mass and collision shape. Its surface-placement
tool uses live environment raycasting. The `-RenderPhysics` option on
`Verify-Quest.ps1` renders the actual tray without a headset. Physical-room
alignment and rigid-item physics have basic Quest 3 user confirmation, as do
palm Recall and retrieval beyond scanned walls. Live-depth placement accuracy,
long-session contact/throw behavior and performance still need Quest QA.
The user confirmed a basic room-physics test. Scanned geometry stops loose items
but does not block tool-selection rays. B/Y or the creation tray's Bring back
control recovers content; hand users also have a solid Recall pebble above either
tracked palm, activated by pointing and pinching with the opposite hand.

### Searchable animation book

The physical Library control opens a two-page catalogue: search/filter on the
left; preview, walking/rule assignment, names/tags/favourites and source terms
on the right. Book and tray selections stay synchronized. Back to chat preserves
the mounted conversation and draft. Preview requires an explicit action and a
compatible loaded rig. The native controller validates each request and prevents
stale polling or resume from replaying it. See `docs/QUEST_ANIMATION_LIBRARY.md`
for transport limits, visual verification and outstanding headset acceptance.

Controller walking and user locomotion are described in
[`QUEST_CONTROLLER_MOVEMENT.md`](../docs/QUEST_CONTROLLER_MOVEMENT.md). The
physical control tray owns separate bindings and user-rule button assignments.
Both movement modes default off; user movement needs explicit virtual view.
Device alignment, comfort and actual controller acceptance remain required.

Custom Maestro state-motion profiles are described in
[`QUEST_AVATAR_ACTIVITIES.md`](../docs/QUEST_AVATAR_ACTIVITIES.md). Select motions
and expand Tutor-state motions in the library to assign Idle, Listening,
Thinking or Speaking choices. Assignments stay with each exact avatar and
resume automatically with the conversation after leaving the library. Walking,
posing and explicit actions retain priority. Device acceptance is still needed.


Behavior programs use the same native rules scheduler and agent operations as the
physical tools. The optional book workspace shows nested functions/blocks, a JSON
editor and live variables/outcomes. Saved behaviours use the canonical behaviours.v2 collection. Incompatible
individual programs retain their exact source for repair while other behaviours
remain usable; unknown whole-collection formats remain protected.
See [the program contract](../docs/QUEST_BEHAVIOUR_PROGRAMS.md) for the supported
subset and release limitations. Set `MAESTRO_PROGRAM_EVIDENCE` to a local output
directory during `Verify-Quest.ps1` to capture real native observations for the
web validator tests. No provider or headset is used by these PC checks.

Saved motion discovery is shared by the book and original-app agent through
`motions.v1`. Search by name/tag to obtain compatible native IDs before editing
rules/programs; no provider client is added to Unity. See
[QUEST_MOTION_DISCOVERY.md](../docs/QUEST_MOTION_DISCOVERY.md) for the query,
compatibility/receipt boundaries and validation. Set `MAESTRO_MOTION_SEARCH_EVIDENCE`
to an output directory during verification to capture the native search observation.

`avatarWalk.v1` connects a saved library motion to Maestro's walking preference,
sharing validation and room Undo with the manual walk chooser. Native observations
include assignment availability and loading/fallback messages. See
[QUEST_AVATAR_MOVEMENT.md](../docs/QUEST_AVATAR_MOVEMENT.md). Set
`MAESTRO_WALK_EVIDENCE` during verification to capture its actual native observation.

`avatarActivities.v1` shares automatic idle/listening/thinking/speaking preferences
between chat and the book, with per-avatar history, atomic edits and stale-profile
protection. See [tutor-state motions](../docs/QUEST_AVATAR_ACTIVITIES.md). Saving a
preference preserves playback priorities and is distinct from starting a motion.


Animation authoring uses one animation.play capability with typed source/channel
choices. Use the tray's Source and channel field or the book's generated form.
Library choices keep exact downloaded motion IDs; arbitrary clip layering remains
unsupported. The old prototype play IDs are no longer public capabilities; old
programs remain visible as unavailable source for repair. See
[typed animation contract](../docs/QUEST_ANIMATION_VOCABULARY.md).
