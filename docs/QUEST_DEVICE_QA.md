# Quest 3 development verification — updated 2026-10-06

Current installed development checkpoint: **D522191F**, with the shared animated
icons and optional physical authoring trays hidden at startup. Exact installed
APK bytes were verified again for the controller-pouring check below. The app is
stopped; synthetic input and owned forwards are released, and debug properties
are restored. Earlier candidate/installation notes below remain historical.

The five-minute animated 32-brick development run averaged **71.43 FPS at 72 Hz**;
sustained performance, human comfort and full Store/provider acceptance remain
open. See [the exact icon-rendering checkpoint](#animated-book-icons--2026-10-06).
The separate Android storage diagnostic was removed after its bounded checks.

The current controller-pouring check transferred **200 mL** from the ordinary
bucket to a fixed cup and verified the saved quantities, Undo and Redo. Original
room/behaviour content and 199 of 200 external files remain byte-identical; the
normal bounded action-receipt history contains this test's completed commands.
[Scope, excluded first aim and cleanup evidence](#controller-bucket-to-cup-pour--2026-10-06).

## On-device automation resumed — 2026-10-05

The owner reconnected Quest 3 and authorized testing. This supersedes the earlier
headset holds below; earlier checkpoint installation notes remain historical.
The scanned-ink checkpoint `45468330` was installed with `adb install -r` after
backing up the installed APK, private app data and external files. No reset was
performed. Backups and real-room screenshots stay in the ignored local
`.quest-evidence/xr-operator-20261005/` directory.

Observed on the headset through the packaged WebView and native runtime:

- Passthrough, included Maestro and both live book pages render. The owner
  confirmed 18+ and authorized opening the session-only audience screen.
- The optional book Workshop created the 19-part practice robot in a temporary
  room. Native joint poses changed during playback. Stopping it allowed Discard
  to return to the original saved objects.
- The book loaded the existing Meta room through `room.environment.set`. The
  subsequent fact reported loaded, no pending worker, colliders ready and physics
  paused. Explicit layout reads returned 14 bounded surfaces and one omitted
  mesh-only anchor. This does not certify real floor/wall alignment.
- The book created a 30 cm ink layer on an exact scanned floor anchor, added one
  three-point stroke through `object.surface.edit`, and verified zero strokes
  after Undo and the same stroke ID after Redo. Anchor readback reported visible
  and retained the exact room/anchor IDs. Temporary Discard restored the original
  room. Physical pencil/hand drawing still needs acceptance. Later restart checks
  below verify saved source and exact-anchor recovery; they do not certify alignment.
- `runtime.frameIntervals` returned a real-device 30-second window with 2,062
  samples: mean 14.55 ms, p95 22.29 ms and maximum 32.82 ms. This small static room
  with a playing robot is diagnostic evidence, not GPU/compositor FPS or comfort
  acceptance. Busy-room sustained measurements remain open.

Meta XR Operator 207 connects to the development APK over its local SSE endpoint.
Its capture-permission approval crashed the SDK helper on Android 14 with
`SecurityException: Media projections require a foreground service of type
ServiceInfo.FOREGROUND_SERVICE_TYPE_MEDIA_PROJECTION`. Capture prompting was
then disabled for this test and ADB screenshots used with the owner's approval.
No Android target or application permission was weakened. Keep this separate
from the application's own book texture and virtual-room capture.

The development-only `maestro_get_input_state` tool reads actual bound controller
values, gesture ownership, ray hits and XRI selections. It does not dispatch
commands or read conversation, credentials or camera frames. It and its reader
are excluded from non-development Android builds. Controller movement/selection
acceptance must use those observations, not merely a successful Operator setter.

## Gripping through book pages and tray buttons — 2026-10-05

The real controller readout exposed a selection defect: `gripPressed` and the
Object gesture were active, and the ray hit a page or tray button, but XRI had
no hovered/selected owner. XRI stops at colliders that are not registered as an
interactable even when `hitClosestOnly` is false. The cover/wood handles behind
those surfaces were therefore unreachable from those pointing positions.

`RoomItem` now registers its owned page/action colliders after construction,
retaining explicit handles and leaving nested movable items with their own
owner. Trigger/pinch routing still owns page/button clicks. Two regressions first
failed on the original code and then passed through actual bound OpenXR input:
page and button grip/move/release, button click, and release on tracking loss.
The full check passed 831 EditMode and 636 PlayMode tests (three optional
private-model skips), both native-room/original-book journeys, production web,
Android lint and 76 Android tests (two optional skips). The audit matched 3,002
source/asset inputs, 147 packaged web files and all included content; ARM64,
v2 signing and 16 KiB alignment passed. Development checkpoint
`MaestroQuest-grip-fix-FD2C7B15.apk` (188,157,155 bytes), SHA-256
`FD2C7B15DABD454A4A5D617E86055FAF46BCE6821B80E0C434CF0EF25B8A54D1`,
was installed in place with no data reset.

On that APK, injected OpenXR controller poses/buttons exercised the actual bound
input and XRI path. Both page grips and the physics tray's Pause-button surface
selected their movable owner, moved it 0.200 m, released it and returned it with
B/Recall. The test also tapped the authorized age checkbox and Open button through
page rays; the WebView confirmed the checked box and opened chat. Triggering the
physical Workshop token opened the real book Workshop. These are automated device
checks, not ergonomic/hand-comfort acceptance. Raw evidence stays local and ignored.

A temporary pool/bucket trial filled the held bucket. A full, slightly tilted
submerged bucket then repeatedly spilled/refilled, so this trial does not close
liquid conservation acceptance. The original room was restored and physics/input
released. The shared-simulation follow-up is resolved by the next checkpoint below.

## Submerged vessel stability — 2026-10-05

A small tilt in a full submerged bucket produced repeated spill/refill below the
reservoir's surface. The controlled regression reproduced the loss. Pouring now
uses the same bounded immersion/clear-path proof as scooping, including for full
vessels, and resumes after lifting. No storage format or new tool is introduced.
The regression checks conserved submerged quantities, normal pouring back after
lifting, and ordinary uncollected spill outside the reservoir.

Development checkpoint `MaestroQuest-submerged-vessel-0EC7EC29.apk` (188,152,667
bytes), SHA-256 `0EC7EC2971037FC1CDC3FBC3EC2321196F6DF31EC1A4FB665F0CB701339BCBAD`,
was audited and installed. Checks passed: 831 EditMode / 637 PlayMode tests (three
optional private-model skips), both native-room and original-book journeys,
production web, Android lint and 76 Android tests (two optional skips). The audit
matched 3,002 frozen inputs, 147 web files, all included avatar/motion/template
content, ARM64, development manifest, v2 signing and 16 KiB alignment.

On Quest 3, the original book created a fresh temporary pool and bucket, loaded
the saved scan and started physics. Actual controller grip moved the bucket into
the pool. Readbacks showed exactly 2,000 mL received and 2,000 mL removed, with zero
difference. A later reading while still submerged remained identical, covering
stability after the flow episode had published. Lifting retained selection and
read back 1,999.977 mL in the bucket (0.023 mL less than the pool's total loss);
this does not close all pouring/obstruction acceptance. No AI provider was used.

Physics was paused, simulated input released, temporary content discarded and
all three original creation IDs verified in the restored book Workshop. The book
returned to the original chat after the authorized adult confirmation. This local
book has no configured AI access, so real-provider acceptance remains separate.
Temporary experimental/capture properties were restored, owned ADB forwards
removed and the proximity override removed. Device evidence remains ignored in
`.quest-evidence/submerged-vessel-20261005/`.

## Hand input, ink and text-field checks — 2026-10-05

The installed APK hash was read back from Quest 3 and matched `0EC7EC29` above.
Meta XR Operator supplied OpenXR hand joints and FB aim; the actual input readout
confirmed `usingHand=true`, gesture ownership, ray hits and XRI selection. The
published/delivered hand generations matched. This tests the packaged hand-input
path, not human tracking reliability, reachability or comfort.

- A right-hand pinch on the book checked the owner-authorized 18+ confirmation.
  WebView readback confirmed the checkbox and enabled Open button. Pinching Open
  then reached the familiar chat. Both left and right pages received hand input.
- Pinching the book's exposed cover selected the book, moving the wrist translated
  it 0.19998 m, and opening the hand cleared selection. A right-hand pinch on the
  left palm Recall returned the book to its home pose relative to room content.
- In a temporary room, the shared catalog created Chalkboard and enabled Surface.
  A moving hand pinch recorded one 44-point, 3 mm-radius stroke. Pinching the solid
  tray Undo removed it; Redo restored its exact stroke ID, point count and radius.
- A solid block overlapping the ray path prevented another stroke and prevented
  surface erasure, leaving the same ink identity and revision. Removing the block
  with the physical Undo control allowed erasure; another Undo restored the ink.
  These checks used paused physics, a configured board patch and a created prop,
  including the ray origin inside its collision volume. Held tools, active scanned
  obstructions, save/restart and busy-room drawing still need their own checks.
- Pinching a book search field requested Meta's keyboard service (`mInputShown`
  changed to true). Android key events entered `ink` into the actual field; the
  value was cleared and Back dismissed the IME (`mInputShown=false`). The inspected
  ADB frame showed the focused field but did not establish visible keyboard
  placement or virtual-key selection; those remain acceptance items.

All checks used the existing APK; no runtime source or package changed. Temporary
content was discarded, pencil preferences restored, the exact three original
creation IDs verified, and the familiar chat reopened. Synthetic hand overrides
were confirmed cleared and selection/page/drawing ownership released. Temporary
properties, owned forwards and the proximity override were removed. CI for
`8b335469` passed. No provider was used. Raw room images, input journals and the
structured acceptance record stay ignored under
`.quest-evidence/hand-input-20261005/`.

## Rectangular pool and vessel dipping (partial headset acceptance)

Create Shallow pool and Bucket in a clear reachable scanned area. Use the original
chat or optional book editor to inspect their exact contents, then start physics.
With controllers and hands, dip the empty upright bucket until its whole mouth
is below the water line. Lift it: the pool must lose exactly the amount received.
Pour it back inside the pool, then repeat over an outer corner so missed water is
reported as uncollected spill. Check visible water alignment after moving, scaling
and tilting the pool. No water collision, buoyancy or persistent outside puddle is
expected. A bucket crossing the floor/wall, covered by a solid lid or not submerged
must not fill through the obstruction.

Pause physics and use Load current values before editing the rectangle width.
Existing quantity and depth must load rather than reset. Undo the edit and one
scoop episode; save/restart and check accepted balances and dimensions. Repeat
inside a temporary room and discard it. Record the APK hash and sustained frame
measurements with normal and budget-heavy container counts; desktop geometry and
conservation tests do not establish Quest comfort or real tracking acceptance.

## Physical material packing (headset acceptance pending)

Enable Pack ball by asking Maestro or using the solid tray control. With hands,
first lift clear of the snow patch, touch with an index fingertip, then lift.
With controllers, hold trigger near the patch and release. Check one visible
preview and exactly one saved ball; holding/dragging must not multiply material.
Pinch-grab the ball with hands, then release/throw it under existing room physics.
Check ball size, reachable placement, surface collision and that a single Undo
removes the ball and restores the field. Repeat on a moved/scaled/tilted patch.

Place a solid prop or scanned obstruction where the ball would appear. Packing
must refuse. Interrupt a preview using the Quest system screen or tracking loss:
returning must not silently create a ball. Inspect the retained draft and explicitly
Retry/Discard. Test the same options from original chat and the optional book
workshop. Record APK hash, room setup and frame/profiler evidence; desktop tests
cannot certify tracking feel, real scan alignment or comfort.

## Physical drawing obstruction (partial automated headset evidence)

The hand-input checks above cover a created board/block with paused physics.
Complete the broader checks below before accepting the full interaction.

With a chalkboard patch enabled, place a solid object between the surface pencil
ray and the board. Trigger and pinch must not begin a stroke through it. Move the
blocker into an active stroke: the valid partial stroke should save once, and Undo
should remove it. Erase ink must leave covered strokes intact. Move the blocker
away and check drawing/erasing work again. Repeat with held Chalk and an active
scanned wall, including a board just beyond the wall. Recall/retrieving tools beyond
scanned walls must still work. Explicit ink edits in the optional book workshop or
by Maestro should remain possible. Check moved/scaled boards and repeated strokes
while sampling actual headset frame time; desktop tests do not establish comfort.
The contact query uses physical proxies, so a coarse collider may cover a visible
opening. It ignores the receiving object's and held tool's own proxies.

## Editable profile geometry (headset acceptance pending)

Create a recipe cup through the shared catalog or delegated room agent. Inspect
it in the optional book workshop; change a profile point and segment count, save,
then Undo. Verify the original chat remains the normal book view. Try invalid
crossed outlines and verify the draft remains editable without changing the room.
Check openings, inside/outside shading, readback, gripping, restart/save and repeated
create/delete resource recovery. Capture the current APK hash and frame/profiler
measurements at normal and budget-heavy use. The 262,144 generated-vertex ceiling
is an admission limit, not a measured comfortable Quest workload. The default
bounds collider does not preserve the visible hollow interior; an explicit proxy
and container component are tested separately. Do not claim container physics
from this geometry test. Desktop lathe rendering and browser replay already pass.

Also create an Extrude part with the default L outline. Change its depth, move a
numbered XY point, insert a point along a straight edge, and remove it. Verify the
visible notch from both sides, readable point labels and retained invalid drafts.
Apply an edit, Undo, close/reopen the room and repeat in a temporary room followed
by Discard. Ask Maestro for the same outline edit and compare the accepted source.
Use explicit compound collision shapes if the notch must admit another object;
the default bounds proxy does not promise visual-shape collision. Record normal
and high-profile-count frame measurements without treating the admission limit
as an acceptable performance target. Desktop extrusion tests are not device proof.

For Sweep, edit a handle's section and numbered 3D path. Check both projected path
views and the actual object from several angles. Change a path Z coordinate,
insert/remove points, Apply, Undo and reload. A reversal, folded tight bend or
out-of-bounds result must retain the draft without replacing the accepted mesh.
Compare the agent's path readback with the editor. Repeat with a concave section,
a long 16-point path and separately configured collision proxies. Record actual
Quest frame cost during creation/editing; the vertex ceiling is not a comfort claim.

## Local runtime diagnostics (device acceptance pending)

The existing book capability catalog and delegated agent can inspect
`runtime.frameIntervals`, `runtime.modelBudget` and `runtime.motionCache`.
They are read-only facts under `runtimeDiagnostics.v1`; no extra book-page UI,
separate AI client or automatic quality adjustment is added. Stored programs
using these facts require that advertised native feature. An unavailable fact
must remain unavailable rather than be interpreted as a zero measurement.

`runtime.frameIntervals` contains Unity Update intervals ending in the last
30 seconds, bounded to 4,096 samples. Mean, nearest-rank p95 and maximum are in
milliseconds; `seconds` sums retained intervals, and the oldest interval may
start before the window. `capacityLimited` reports recent dropped samples.
`hasSamples=false` means the zero numbers are placeholders. `ageSeconds` is the
age of the newest sample when the reading was cached (at most one second ago).
`editor=true` marks desktop Unity evidence. Focus changes, pause, disable and
invalid/backwards or over-1,000-second clock gaps clear the window. Sampling
uses unscaled monotonic time, not animation/capture time. The fixed arrays use
96 KiB; sampling allocates no per-frame objects. Sorting and JSON construction
are cached for one second; reads return detached values.

Model reservations include loaded and in-flight avatars, previews and imported
objects across the process. Texture MiPixels and morph million-vertices are
source-content budgets, not actual GPU/RAM bytes. Motion cache counts describe
the active room library, including in-flight clips, and exclude embedded model
animations and other workspace generations. Reads never load or evict content.

When device work resumes, capture the same three facts after a quiet minute,
while playing an included full-body motion, during physics/grabbing, and while
recording an animation. Repeat with a model preview open and after closing it.
Confirm reservation counts recover. Also try rejecting an incompatible avatar repeatedly; the
model reservation must return to its baseline each time. Candidates are
disposed explicitly because [Unity OnDestroy](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.OnDestroy.html)
is not invoked for objects that were never active. Open a Quest system screen, return, and
confirm the frame window starts fresh. Record APK hash, headset/OS, scene and
content choices, plus actual headset refresh rate and Meta profiler evidence.
These interval summaries do **not** measure GPU time, compositor/display FPS,
WebView memory, total process memory, thermal stability or comfort, and cannot
replace physical-device Store-performance acceptance. Do not apply arbitrary
72-Hz thresholds without recording the active headset mode.

The sampler neither saves nor uploads a history. Deliberately reading these
facts through the agent makes the selected summaries normal task context in
the original Maestro connection, like other catalog facts. There is no
independent telemetry endpoint and no room geometry or device identifier.

## Adult audience / privacy links: headset acceptance pending

1. Start a new book document. Confirm both pages show the audience/data notice,
   no saved chat or active media is present, and Open stays disabled until the
   unchecked 18+ checkbox is selected. Selecting it alone must not enter chat.
2. Open Privacy and Gemini terms with controller and hand input. Return to the
   book: no interrupted-session error, automatic entry, microphone or AI task.
   Confirm both links reach the intended public policy; prepared policy changes
   need separate deployment before release.
3. Select Under 18 and verify chat remains inaccessible. Restart the book and
   verify it asks again, including with existing chat/account data on the device.
4. Confirm 18+ and press Open. Verify the familiar two-page chat, native tools,
   account dialog and explicit audio-resume flow still work. Background/resume
   must not create another confirmation inside the same live book document.
5. Check both pages for readable text, working scroll/pinch and reachable controls.
   Local browser tests are not proof of headset comfort or age verification.


This is development evidence, not a release acceptance report.

## Book backup export: headset acceptance pending

The original **Save All / Save Chat** controls now use a document-owned Android
writer on Quest, with a receipt inside the original page after the file closes.
The PC checkpoint is recorded in `.quest-evidence/book-export/verification.json`.
The APK remains uninstalled while device work is on hold.

When device work resumes:

1. Save a conversation containing multilingual text and artifacts. Confirm that
   the page reports `Downloads/Maestro/<filename>` only after the operation ends.
2. Use the original Load All file picker to select that file from Downloads.
   Verify messages, bookmarks, profile/avatar and archived agent-task records;
   imported task history must not execute old actions.
3. Save twice with the same filename. Both files must remain available, and the
   receipt must use Android's resolved filename rather than hiding a collision.
4. Interrupt a large export with system UI, book/browser replacement or a focus
   loss. Expect an interruption/uncertain-result message, no completed partial
   file, and a successful fresh export after resume. A finish already dispatched
   may have completed: inspect Downloads before retrying.
5. Check low-storage failure and a large archive. The limit is 256 MiB per file;
   failure must not claim success or overwrite another export. Save smaller
   conversations separately when the archive exceeds the limit.

6. Cancel or fail the mandatory save before Load All and Backup & Reset. Neither
   may replace/delete data. Retry with a completed save, then restore its contents.
   Check a profile-only installation with no conversations. On phone builds,
   required backups must exist under Documents, not just a dismissed share sheet.
   Use an isolated test profile for reset; chat backup does not restore every
   preference or native room asset. Other tabs/provider activity must be included
   in the remaining coordinated-maintenance acceptance tests.

Android MediaStore provider tests cover pending publication and deletion; Chrome
uses a simulated transport to exercise the actual Save/Load controls and IndexedDB.
Neither establishes headset acceptance, process-crash cleanup timing or flash
power-loss durability. Native creations, motion assets and module-library entries
are not included by this chat backup path; their portability remains separate work.


## Module files: headset acceptance pending

1. In the book's reusable module library, inspect and export a module. Confirm
   its Downloads receipt, including the resolved name after repeated exports.
2. Choose that JSON file. Check its name, exact content ID, exports and referenced
   objects before pressing Import file to library. Selection must not start work.
3. Confirm the native action receipt and inspect the resulting definition. Import
   twice and verify one identical entry. No program, button or animation may start.
4. Reject an altered hash, truncated/invalid file, unsupported version and an invalid
   program without replacing library entries. Interrupt export/import and inspect
   file/library/receipt before retrying; Stop cannot retract dispatched storage.
5. Reuse the entry in a caller only after deliberate resource/signal wiring. Missing
   room objects or motion/model assets must remain missing, with no silent remap.
   A module file alone does not transfer an entire native workspace.

## Shared ownership: headset acceptance pending

The latest PC package and exact verification are recorded in draft PR #248 and
`.quest-evidence/room-ownership/verification.json`. It is not installed. Earlier
APK entries below describe their historical checkpoints, not the latest build.
See [the ownership policy](QUEST_ROOM_OWNERSHIP.md).

When device work resumes, check these on Quest 3 with the same room and assets:

1. Play a behaviour, then grip its target. The animated item should stay at the
   grasped position. Hold it with two hands, release one, then the other; it must
   remain claimed until the last release and must not restart afterwards.
2. Record movement while gripping and moving an item. Both controller and hand
   interaction should continue the take. Save and replay the actual movement.
3. Run walking and an upper-body gesture together. Direct Look/Follow or the
   assigned avatar stick should take over movement while the arms continue.
   Invalid tracking or unavailable navigation must not stop valid current work.
4. Pose/preview Maestro, then use an explicit movement control. Verify deliberate
   takeover; a program command must not override authoring or a held joint.
5. Lose focus or open system UI. Previews/programs stop, props use the established
   Stop/return behavior, and focus return alone does not replay them. Tutor-state
   animation needs its normal current-state eligibility.
6. Open the optional Workshop/Behaviours view and **In control now**. Names and
   target/channel descriptions must match physical ownership, update on release,
   remain readable and support hand/controller tapping. Ask the original chat
   agent about a blocked action and check its answer against that same evidence.

These checks remain unverified on hardware. No device access or data reset was
performed while the device hold is active.

## Earlier: shared automatic-animation preferences — not installed

APK: `D:/Projects/Builds/MaestroQuestVerify/Builds/Checkpoints/MaestroQuest-avatar-activities-9210BA28.apk`.
SHA256: `9210BA286C643711BC0132EA46948B025F194812779F8E76CF2B2F07230BED1B`.
Development v2 signature verifies; libraries are ARM64 only. All 89 runtime sources
match the tested mirror. Embedded `main-BVgyQ49l.js` matches the built web bundle
(SHA256 `c6e21b2bbfa3d2a169d13f1c18bbfa77c5d91b9c03631912d522c17131fe75de`).
81 EditMode, 76 required PlayMode, 25 Android and 1,174 app tests pass. Three optional
private-file tests were deliberately skipped. TypeScript, full/shared lint,
prompt/core guards and release-config checks pass. Functions (25) and Live gateway
(31) tests/builds pass. Evidence: `.quest-evidence/avatar-activities/`.

`avatarActivities.v1` adds atomic per-avatar idle/listening/thinking/speaking
assignments through the original app-owned agent. The book uses the same validator,
projection, save and independent Undo history. Profile revisions reject stale
agent/book requests. A dirty book draft survives an intervening agent edit.
The native integration test observes real head rotation and state transitions,
checks multi-role atomicity, manual authoring priority, failed writes, Undo/Redo
and missing-download availability. Web tests parse the actual Unity observation
and verify conflict UX. A headless Chrome check also preserves a dirty draft,
reloads the new settings and submits the observed revision. That presentation
check uses a real Unity profile in a synthetic book envelope; conflict updates
are simulated. Provider orchestration tests use a mock, not Gemini.

No device access occurred. Acceptance must include real-provider chat assignment,
co-editing, automatic playback during Live, pause/resume and model changes, plus
all earlier hardware checks. The reported default-avatar stationary feet issue
remains unresolved. This is a development checkpoint, not Store readiness.

## Earlier: shared walking preference — not installed

APK: `D:/Projects/Builds/MaestroQuestVerify/Builds/Checkpoints/MaestroQuest-avatar-walk-CF9E1370.apk`.
SHA256: `CF9E1370A329212E2895DC6E15B6FE6F87B8E11CAE67CC56BD5A8FBF1A1646AF`.
Development APK v2 signature verifies; native libraries are ARM64 only. All 88
runtime sources match the tested Unity mirror. Embedded web bytes match the built
bundle, including the new agent instructions. 81 EditMode, 75 required PlayMode,
25 Android and 1,149 app tests pass, with three optional private-file tests skipped.
TypeScript, lint, prompt/core guards and release-config checks pass. Functions (25)
and Live gateway (31) tests/builds pass. Evidence: `.quest-evidence/avatar-walk/`.

Chat/Live room tasks can choose a compatible downloaded walk or restore the included
walk, using the same native validation as manual selection and the same room Undo.
The observation exposes the saved choice, availability and loading/fallback status.
A native test observes leg rotation from the assigned animation; it also checks
manual/agent edits, failed combined edits, missing downloads and Undo. Web bridge
tests consume an actual native observation. See QUEST_AVATAR_MOVEMENT.md.

No device access occurred. The user's reported stationary feet on the included
avatar remain a hardware acceptance issue. When the user returns, preserve saved
room data and verify assignment through chat, walking/following with included and
custom avatars, missing-motion feedback, restoration and Undo alongside earlier
book, animation, physics and hand/controller checks. Real-provider acceptance also
remains open.

## Earlier: shared motion discovery — not installed

APK: `D:/Projects/Builds/MaestroQuestVerify/Builds/Checkpoints/MaestroQuest-motion-search-3B2EB463.apk`.
SHA256: `3B2EB463F3074110FE272C57C5DA3781FA78402B51EB8AA52FA0E75D52B36268`.
Development signature v2 verifies; native libraries are ARM64 only. All 87 runtime
source files match the verified Unity mirror and embedded web bytes match the
production web build. 81 EditMode, 74 required PlayMode, 25 native Android and
1,137 app tests pass. The three optional private-model/collection tests are skipped.
Full app lint, TypeScript, prompt/core guards and release-config checks pass;
Functions (25) and Live gateway (31) tests and both builds pass.

The original app's agent can discover saved animations through the same compatible
pages as the book, then use native IDs in existing rules/programs. See
QUEST_MOTION_DISCOVERY.md. Agent/provider behavior is covered by mocked-provider
journeys; native imported-model playback is covered separately in PlayMode. The
actual native search snapshot passes the web bridge validation. These are PC checks,
not headset or real-provider acceptance. Evidence: `.quest-evidence/motion-search/`.

Device hold remains. After the user returns, preserve the installed room and check
an explicit spoken/typed animation search, behavior creation from the returned name,
book/agent result agreement, and avatar switching alongside the tests below.

## Earlier: behavior programs and library compatibility — not installed

APK: `D:/Projects/Builds/MaestroQuestVerify/Builds/Checkpoints/MaestroQuest-program-library-3EC36425.apk`.
SHA256: `3EC364252AD3075A00A6250388E44519F946DFD6F4FA9716748C7FBEBDBA8184`.
Development signing, verified APK v2 signature and ARM64-only native libraries.
80 EditMode, 73 required PlayMode and 25 native Android tests pass; three optional
private-model/collection tests are skipped. The production web build and native
browser lint/build pass; embedded web bytes match the build. Existing full-app and
backend validation, and the initial successful PR release gate, are recorded in
QUEST_BEHAVIOUR_PROGRAMS.md. Evidence: `.quest-evidence/program-library/`.

This checkpoint includes the app-owned chat/Live agent, programmable behaviors,
blocks/JSON workspace, execution observations and the program/library compatibility
fix. Library references include program motions; a stale linear assignment cannot
replace a program. Newer collection filenames and recovery files block rollback
and protect unknown motion references from removal.

No device access occurred. Installed build remains `08F340EF`. When the user returns,
back up room/rules before upgrading; check migration, familiar book chat and existing
controls, then save/edit/run a program with state and button triggers. Open the motion
library while a program is selected, preview a compatible motion, return to its block
editor, and verify Undo and Stop. Complete real-provider, WebView, readable-book,
room physics and frame-timing acceptance separately. The older sections below are
historical checkpoints and do not describe the latest packaged APK.

## Earlier PC checkpoint: chat-to-agent handoff — initially not packaged

The normal tutor can propose an `agent` tool; the existing suggestion stage
verifies it, and the task retains the original request/context. Durable native
receipts, a same-chat result, task Stop/details and a nonblocking header activity
state are implemented. Browser verification uses real IndexedDB and UI with
simulated provider/native ports. See QUEST_UNIFIED_AGENT.md and
`.quest-evidence/agent-handoff/receipt.json` for scope and remaining work.
The following APK still predates this web-only checkpoint. Device hold continues.

## Earlier: shared behaviour workspace — not installed

APK: `D:/Projects/Builds/MaestroQuestVerify/Builds/Checkpoints/MaestroQuest-avatar-7EC419D6.apk`.
SHA256: `7EC419D60CBDDDB06A4BBBA522CBCC8780F4B7FF964BC7317463A2377CBF0297`.
135,465,547 bytes, development signed, zero build errors and two warnings.
Signature v2, ARM64 and required manifest checks pass. 64 EditMode, 69 required
PlayMode and 25 native checks pass (158), with three optional private-file tests
skipped. Room/Quest web checks total 34 and shared prompt checks 65; TypeScript,
web build, lint and boundaries pass. See QUEST_BEHAVIOUR_WORKSPACE.md for scope.

Native tests execute the same recipe rule through an agent request, tutor event
and real ray/button input, observing arm motion and stops. The browser page checks
use real React surfaces and native-exported data with simulated replies, not a
Quest/WebView acceptance run. Reviewed captures and build/source hashes are in
`.quest-evidence/behaviours/verified-7EC419D6`.

No device access occurred; installed build remains 08F340EF. Once the user returns,
back up saved room/rules before upgrading and test v4 migration, book edits/reorder,
Undo, stale-draft retention, state triggers, physical buttons, recipe motion and
interruption. Preserve the earlier conversational/provider and device checks below.


## Earlier: shared recipe workspace — not installed

APK: `D:/Projects/Builds/MaestroQuestVerify/Builds/Checkpoints/MaestroQuest-avatar-71987229.apk`.
SHA256: `71987229044DCDEAB6108DDAE42E8315C512859DD2C1A0C354109E07EFB7B83F`.
135,462,494 bytes, development signed; zero build errors and two warnings.
APK v2 signature, ARM64 and required manifest checks pass. 63 EditMode, 67 required
PlayMode and 25 native Android checks pass (155), plus 27 room/Quest web checks
and 65 shared prompt checks. Three optional private-file checks are skipped.
TypeScript, native lint, boundary checks and production web build pass.

The optional book workspace exposes the object/part tree and recipe animation
keys through the same native executor used by conversational room requests.
It adds inspected recipe read-back, part outlines, target-specific revision checks,
actual playback status/restart and cancellation-safe session handshakes. No
behaviour-block editor, full Live voice integration or complete action parity is
claimed. See QUEST_AGENT_WORKSPACE.md and QUEST_SHARED_ACTIONS.md.

The 1024 x 768 browser fixture checks Apply, Undo, key editing, Stop/Play and return
to mounted chat, with no page errors. It uses real React surfaces and a native
recipe, but simulated bridge receipts. Unity tests exercise real native execution;
neither evidence proves the complete WebView/headset path. Reviewed captures and
source/build hashes are archived under `.quest-evidence/workspace/verified-71987229`.

No device query, installation or launch occurred. Installed checkpoint remains
08F340EF. Once the user explicitly returns, preserve saved room data before any
upgrade and perform the conversational/workspace checks at the end of this file.

## Earlier: batch animation imports — not installed

APK: `D:/Projects/Builds/MaestroQuestVerify/Builds/Checkpoints/MaestroQuest-avatar-9A96BEB7.apk`.
SHA256: `9A96BEB7C574DC3B22DF18C221DD98D1E789363854567986F5466008F93BDC6C`.
135,429,553 bytes, development signed. The full Unity build exited 0 with zero
errors and two warnings. APK v2 signature, ARM64 and manifest checks pass; the
native batch-picker class is present in the packaged dex. 59 EditMode, 63 required
PlayMode and 22 native Android checks pass (144 total). Native lint and the shared
web build pass. Three optional private-file tests were deliberately skipped.
No web feature changed; no new web test run is claimed. No private model or
animation collection was included in the APK.

The import tray's Batch files tab selects 1–128 GLB/VRM exports and requires
Save batch before opening their streams. It imports one file at a time, shows
per-file results and categories, supports stop/resume and retry, and retains
completed motions without hidden avatar models or resident clip compilation.
Exact reimport preserves existing identities/metadata. Original files and the
separate web/single-model picker limits are unchanged. See QUEST_BATCH_IMPORTS.md
for persistence, timeout and compatibility boundaries.

Core tests check partial success, deduplication, retry of failed files, bounded
sequential source ownership, oversize rejection, originals unchanged, stopping
during read/save and saved-data reload. PlayMode exercises the tray commands,
model-free imports, error repair, cancellation on pause and explicit resume.
Native tests cover multiselect intent/counts, confirmation before reads, copy
cleanup, rejected providers, retry and stale session/request isolation.
Failure/success batch views and the ordinary import tray were rendered in Unity
and inspected: text and controls fit without overlap. Direct tray-command tests
and desktop renders do not establish headset pointer/picker acceptance.

Ignored evidence: `.quest-evidence/batch-imports/verified-9A96BEB7`, with 114 C#
source hashes and 112 web-file hashes matched to the build mirror, native Java
source hashes and AAR hash, test XMLs, build reports/logs and three reviewed PNGs.

No headset query, installation or launch occurred. Installed build remains
08F340EF while the user sleeps and Quest charges. After the user returns, back
up the saved room before installation. Test actual Quest document multiselect,
a real collection, Stop during reading/saving, Resume, Retry failed, app restart
and reselection, low space and permission loss. Measure sustained memory/frame
rate and hand/controller readability. Earlier gait, rule/prop/state-profile,
book parity and Store release gates remain open.

## Earlier: animation-library maintenance — not installed

APK: `D:/Projects/Builds/MaestroQuestVerify/Builds/Checkpoints/MaestroQuest-avatar-BCEAF516.apk`.
SHA256: `BCEAF5165C1A161D46DF0B567B032E8E3FAEE947C6D7E86B5559D3C95C08BD20`.
135,420,391 bytes, development signed. Final Unity build exited 0, with zero
errors and two warnings. APK v2 signature, ARM64 and manifest checks pass.
54 EditMode, 61 required PlayMode and 17 native Android checks pass (132), plus
18 Quest web checks (150 total). TypeScript compilation, native lint and the
shared web build pass. Three optional private-file tests were deliberately
skipped. No private animation/model pack was added to the build.

The book library now offers archive/restore, paged current-use explanations,
protected removal of a local motion download and separately confirmed forgetting
of unused removed metadata. Current assignments, room/rule/profile Undo/Redo,
retained saves/recovery copies and active/loading clips protect downloads.
Archive preserves referenced playback. Exact reimport preserves an existing
identity unless the user deliberately forgot its metadata. Missing downloads
are shown as unavailable and can also be repaired or removed. The catalogue
migrates to v2 without changing older original files. See
QUEST_MOTION_MAINTENANCE.md for controls and precise limits.

Unity tests verify real clip leases and runtime rule playback, asynchronous
removal reservation/rollback, retained-reference protection, book requests,
archive and exact reimport, absent payloads, future versions and metadata/source
slot reclamation. Browser tests cover bounded state and separate confirmations
that reset on selection/session changes. The local Edge fixture exercised search,
metadata editing, return to chat and its inline artifact, state-profile controls,
removal/forget confirmations and protected references using actual synthetic
native state. The inspected screenshots fit the two page widths and use the
existing detail scrolling. Fixture acknowledgements simulate transport; this
is not Android WebView or headset acceptance.

Ignored evidence: `.quest-evidence/motion-maintenance/verified-BCEAF516`. It
contains 109 C# source hashes and 112 bundled web-file hashes matched to the
build mirror, native AAR hash, build/test reports, browser results, native state
JSON and reviewed screenshots. The earlier props and movement features are
included.

No headset query, installation or launch was initiated. Installed build remains
08F340EF while the user sleeps and Quest charges. After the user returns, back
up the room directory before installing. Verify archive while a rule plays,
Archived search, reference explanations, restore, removing an unused download,
restart and exact reimport, then optional forgetting. Confirm used and historical
motions stay protected. Test keyboard and hand/controller scrolling with a large
collection. Previous gait, movement bindings, tutor-state profiles, prop throwing,
book/native parity and all Store release gates still need their stated acceptance.

## Earlier: avatar-held props — not installed

APK: `D:/Projects/Builds/MaestroQuestVerify/Builds/Checkpoints/MaestroQuest-avatar-DFE1FE66.apk`.
SHA256: `DFE1FE66CD2860396D919D582C07A75EF21929A5233E09B3EA86B9A1C11B01E8`.
135,396,031 bytes, development signed. Final build exited 0 with zero errors and
two warnings. APK v2 signature, ARM64 and expected manifest checks pass.
50 EditMode, 57 required PlayMode and 17 native Android checks pass (124 total),
along with native lint and the shared web build. Three deliberately optional
private-file tests were skipped. No new web feature/test result is claimed;
the shared web source is unchanged from the prior checkpoint.

A Maestro gesture, recording, embedded clip or saved-library motion can carry
one fitted creation in either hand, then return, drop or throw it. Rules reserve
both avatar and prop, including asynchronous loading. Real Unity physics checks
cover the released ball's velocity, gravity and floor bounce, blocked movement,
room pause, user grip takeover, imported Mixamo hand following, persistence and
Undo. Pointer tests activate the actual physical Props/Rules tab and controls.
Both Unity tray renders were visually inspected after fixing status-label
spacing and a text-encoding regression. The original tray size is retained.

Evidence is in ignored `.quest-evidence/avatar-props/verified-DFE1FE66`, including
106 C# source hashes matched to the build mirror, 112 bundled web-file hashes,
the native AAR hash, build reports/logs, test XMLs and reviewed tray PNGs.
See QUEST_AVATAR_PROPS.md for authoring, release timing and collision limits.
This build also includes the prior movement bindings and tutor-state profiles.
No private model or animation pack is bundled.

No headset query, installation or launch occurred. Installed build remains
08F340EF while the user sleeps and Quest charges. After the user returns, back
up the complete room directory before installing. Check a Return action first,
then fit a ball to the Meshy avatar's hand, run a compatible throw motion and
adjust release timing. Verify grab takeover, Stop before release, return after
loading, wall blocking in the small room, pause/recenter/tracking interruption,
and rule/controller/event triggers. Also test the earlier book-library,
locomotion, gait and tutor-state-profile changes which remain unaccepted on
hardware. Throw feel, device frame rate, per-limb contacts/IK and full Store
release acceptance remain open.

## Packaged searchable animation book — not installed

APK: `D:/Projects/Builds/MaestroQuestVerify/Builds/Checkpoints/MaestroQuest-avatar-800D603B.apk`.
SHA256: `800D603B71438C5F63FC68ED52FA8F6DE15E1F633818EFEC6374535013AACB6A`.
135,259,932 bytes, development signed. Final Unity build exited 0 with zero
errors and two warnings. APK v2 signature, ARM64 and expected manifest checks
pass. The full build passed 41 EditMode, 43 required PlayMode and 16 native
Android tests (100 required checks), native lint and the shared web build.
Three private-file checks were deliberately omitted. Twenty-five related web
tests also pass: 14 Quest presentation/library checks and 11 shared session/
artifact checks. TypeScript compilation passes.

The physical Library control opens a two-page browser with name/tag search,
compatibility/favourite/short-clip filters, paginated results, preview/stop,
walking/current-rule assignment, metadata editing and complete paged source
terms. The book and physical import tray share selection. Incompatible saved
selections remain explicit and cannot silently play another clip. Chat stays
mounted, hidden and inert while browsing; its composer/history return intact.
The shared tutor session remains the existing owner.

Native request validation, session/sequence acknowledgement, Stop/close
preemption, pause/focus cancellation, stale action-target rejection and bounded
responses are tested. Browser tests also cover escaped source text and retained
chat drafts. An isolated Edge render exercised actual synthetic Unity response
data at the native viewport size, checked page bounds, search and metadata
editing, returned to chat, and activated its inline HTML artifact. Screenshots
were visually inspected. The fixture simulates acknowledgements; this does not
verify Android WebView input or Quest usability. The in-app automation helper
could not initialize, so the reproducible local browser script was used instead.

Evidence is in ignored `.quest-evidence/book-library/verified-800D603B`.
No private model or motion pack is bundled. No headset query, installation or
launch occurred; installed build remains 08F340EF. After the user returns,
back up the complete room directory before installation. Check the physical
Library toggle, Quest keyboard/search, both-page scrolling, filters and long
names, rename/favourite persistence, preview/Stop, walking/rule assignment,
tray/book consistency, focus interruption, Back to chat and inline artifacts
with both controllers and hands. Confirm original saved room/rules and visible
feet. The previous stationary-feet report remains unaccepted on hardware.

Broader role profiles, blending, deletion/relinking, bulk collection delivery,
authored travel/contact, independent locomotion bindings, localization review,
sustained performance and store/authentication gates remain release work.
This is a development checkpoint, not completed v1/store readiness.

## Packaged saved-motion actions and walking — not installed

APK: `D:/Projects/Builds/MaestroQuestVerify/Builds/Checkpoints/MaestroQuest-avatar-06DEB0D4.apk`.
SHA256: `06DEB0D4ECE59F1524244549376E98FDE92523D5D5EB938DBA289149389D4095`.
135,254,285 bytes, development signed. Unity completed with exit 0, zero build
errors and two warnings. APK v2 signature, ARM64 and expected manifest checks
pass. The full build passed 41 EditMode, 41 required PlayMode and 15 native
Android tests (97 required checks), native lint and the shared web build.
The three private-file checks remain explicitly optional and were omitted from
that packaging run.

A preceding desktop run passed 41 EditMode and 43 PlayMode checks using the
real Meshy Stage Walk. The saved library motion moved its leg and visible mesh
(maximum sampled displacement 0.1275 metres), with the fitted wrapper and room
placement unchanged. Imported-object rules also animate and restore their rig.
The first new object test used scaled test time against unscaled rule playback;
its wait was corrected, and subsequent desktop and full-build runs passed.
The real-model library-walk image and settled rule/library trays were inspected.

The update adds stable-ID saved-motion actions, existing controller-button and
tutor/VR event triggers, preparing-action reservations and cancellation, and
saved walking assignments with included-gait fallback. Rename, compatible avatar
replacement, Undo, restart, missing/incompatible motion handling, state exit,
focus loss and load timeout are covered. Room/rule v2 migration leaves v1 files
untouched, retains existing exact-model clip bindings, recovers valid current
backups and refuses unknown newer versions without downgrading to an older save.

Evidence is in ignored `.quest-evidence/motion-bindings/verified-06DEB0D4` and
`desktop-verification`. No private model or motion collection is bundled. No
headset query, install or launch occurred; the installed build remains 08F340EF.
After the user returns, back up the complete room directory (including models,
motions and both save versions) before installing. Check saved-motion rules
from a mounted button and a tutor-state change; try a saved Walk clip with
Preview walk and Follow; interrupt a loading/playing action; restart and verify
old drawings, recordings, rules and buttons as well as new assignments. Confirm
visible feet, small-room clearance and both-hand readability on Quest 3.

The reported stationary-feet issue remains unaccepted on hardware. Searchable
book browsing, broader role profiles, blends, deletion/relinking, authored
travel/contact, independent locomotion bindings and long-session Quest profiling
remain release work. This checkpoint is not a completed v1 or store release.

## Packaged reusable-motion library — not installed

APK: `D:/Projects/Builds/MaestroQuestVerify/Builds/Checkpoints/MaestroQuest-avatar-EBACCF1C.apk`.
SHA256: `EBACCF1C36FFDF4BEB94C6C8CD2AD84DE1EB562098F2BF49C0F73256507F5D47`.
135,241,569 bytes, development signed. The final Editor build exited 0 with
zero errors and two warnings. APK v2 signature, ARM64 and expected manifest
checks pass. The final build passed 38 EditMode, 37 required PlayMode and 15
native Android tests, native lint and the shared web build (90 required tests).
Three explicitly optional private-file checks are excluded from that build.

The preceding private collection run passed all 78 selected Unity checks,
including real Meshy Stage Walk, source-versus-library deformation equivalence
for three category samples and the read-only 96-export extraction audit. All
source hashes stayed unchanged. Motion payloads total 11,899,044 bytes, with
one compatible rig and 96 unique clips including one deduplicated short helper.
No private avatar or motion pack is included in the APK. Source collection,
storage and runtime memory sizes are separate measurements; see
QUEST_ANIMATION_LIBRARY.md for the measured boundaries.

Added Save motions / Library controls, same-rig motion extraction, persistent
stable IDs and source terms, atomic storage/backup recovery, bounded cached
playback and Maestro previews without another persisted textured model. Tests
cover no autoplay, pause/focus loss during loading, restart, damaged/missing
copies, duplicate/revised sources, cache pins and cubic/morph curves. One-shot
sampling now holds the final frame instead of inheriting an embedded clip's
loop setting. Offscreen UI capture waits a player frame for font atlas updates;
the settled import and library tray renders have been visually inspected.

One configuration attempt was correctly rejected because Unity did not exit
within its shutdown deadline. The final complete retry passed with actual
successful Editor exits; the timeout was not treated as a successful build.
Final evidence is in ignored `.quest-evidence/motion-library/verified-EBACCF1C`;
private collection evidence remains in its `desktop-verification` sibling.

The headset is still charging and no device query, installation or launch was
performed. Installed build remains 08F340EF. After the user returns, preserve
room/model/motion data before installing and check Save motions from a second
same-rig export, Library selection/Play/Stop, duplicate import, incompatible rig
messages, grip/pose/focus interruption, restart and both-hand readability.
The previous stationary-feet report and small-room movement checks remain open.
Searchable book browsing, role assignments, stable-ID rule actions, deletion and
large-library performance acceptance are not supplied by this checkpoint.

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


## Independent controller movement — PC checkpoint, 2026-09-26

No device access while the user sleeps and Quest charges. The installed headset
build remains 08F340EF. Independent avatar/user stick controls, persistent
bindings and controller rule actions now have desktop verification. A separate
physical tray preserves the full book pages. See QUEST_CONTROLLER_MOVEMENT.md.

The current verification passed 44 EditMode and 48 required PlayMode checks,
including an actual OpenXR Touch-layout input test, both movement targets,
collision stops, reverse direction, neutral/press gates, snap turns, restoring
the original XR origin, pause/tracking interruption, saved control settings
and stable rule references. Three private-file tests were explicitly skipped.
This is not hardware evidence of passthrough transitions or MRUK alignment.

After the user returns and reconnects, verify this sequence on Quest 3:

1. Confirm existing trigger/grip, B/Y and palm Recall still work. Movement starts
   off. Enable Maestro stick, centre the right stick, then walk with it; check
   both visible feet and the selected saved gait. Check scanned walls and reverse
   away from a blocked direction. Keep avatar size suitable for the user's room.
2. Swap bindings and test the other stick. Disable/re-enable while a stick is
   held: no movement until it is centred. Verify hand tracking cannot act as
   stick input and returning controllers cannot replay a held input.
3. Choose Virtual / MR while stationary. Real passthrough must disappear, the
   virtual floor and any loaded scan surfaces must appear, and enabling Your
   movement must move only the viewpoint. Check body collision, 30-degree X/A
   turns, current controller alignment and sustained comfort. Movement must stop
   before manipulating objects and remain neutral-gated afterward.
4. Use B/Y, palm Recall and Stop / MR independently. Each must restore the real
   room alignment and turn movement off. Check scan alignment before restarting
   physics. Repeat after system UI interruption, removing the headset and
   temporary tracking loss. No virtual camera offset may persist after restart.
5. Select an existing visual action, assign a stick click or A/X through Use
   action, and verify recorded/imported animations, hold-vs-press behaviour,
   deleted action feedback and persistence. Mounted 3D action buttons and tutor
   state triggers must keep their existing scheduler semantics.

Teleportation, general trigger/grip remapping, broader movement accessibility,
per-limb contact and comfortable long-session acceptance remain open. Do not
infer these from the desktop capsule/navigation checks.

Packaged checkpoint: **7CCC2880**, development-signed and **not installed**.

- APK: `D:/Projects/Builds/MaestroQuestVerify/Builds/Checkpoints/MaestroQuest-avatar-7CCC2880.apk`
- SHA256: `7CCC288014445B83A90CA61A9D2E0D81C2CA5065F95B34E72E2E126B34939367`
- Size: 135,281,085 bytes. Full build exited 0; zero build errors, two warnings.
- APK v2 signature, required manifest entries and ARM64 architecture verified.
- 44 EditMode + 48 required PlayMode + 16 native Android checks passed (108).
  Shared web build and native lint passed. No new web source changed in this checkpoint.
- The real Touch-layout test includes trigger press/release of the view switch;
  settings tests also verify repeated edits refresh physical markings.
- All 99 C# source files matched the build mirror. Hashes, test XML, native test
  reports, build logs/report and inspected tray PNG are archived under
  `.quest-evidence/controller-movement/verified-7CCC2880`.

Earlier development builds during this work were superseded before checkpointing.
No ADB device query, install or headset launch occurred. Hardware acceptance,
production signing, application identity and the complete release gates remain.


## Per-avatar state motions — PC development, 2026-09-26

The book library now assigns compatible saved motions to Idle, Listening,
Thinking and Speaking for each exact custom avatar. See
QUEST_AVATAR_ACTIVITIES.md for settings, interruption and transition limits.
The installed headset build remains 08F340EF; device work stays on hold while
the user sleeps and charges Quest. The packaged checkpoint and desktop evidence are recorded below.

After the user returns and reconnects:

1. Use a compatible custom Maestro, save a supported animated export's motions,
   then Library. Select a motion, open Tutor-state motions and assign Speaking.
   Confirm speed/weight/gap/loop and Undo/Redo are usable with trigger and pinch.
   Library browsing and assignment must not start automatic animation.
2. Return to chat and exercise actual speaking/listening/thinking/idle. Confirm
   the chosen motions start and blend, gaps use the included animation and two
   ready choices do not immediately repeat. Keep the book interaction functional.
3. Preview, pose, record, walk and run an explicit visual-rule action while a
   tutor state is active. Each explicit owner must retain control. After it ends,
   the current tutor state may resume. Audio pause/reduced motion must suppress
   automatic state motions.
4. Change Maestro to Default or another imported avatar, then back. Confirm its
   assignments return without appearing on the other avatar. Restart the app and
   confirm preferences persist; in-memory assignment Undo history resets.
5. Interrupt during loading with system UI/headset removal. Return with audio
   still paused. Old state must not replay; a fresh active tutor state is needed.
   Check page reload/browser recovery too. Native callback and Unity cache guards
   have automated tests; actual headset/WebView scheduling remains unverified.
6. Observe feet, extra bones and expressions across transitions. The new blend
   covers canonical body joints/hips only. Verify small-room suitability and
   performance; there is no new limb/contact collision or hair/cloth solver.

Packaged checkpoint: **906ED6F1**, development-signed and **not installed**.

- APK: `D:/Projects/Builds/MaestroQuestVerify/Builds/Checkpoints/MaestroQuest-avatar-906ED6F1.apk`
- SHA256: `906ED6F13BF2074199E47A1E771699155E9D7C29ED570B3F10EB6660E852D4DE`
- Size: 135,356,835 bytes. Full build exited 0; zero build errors, two warnings.
- APK v2 signature, required manifest entries and ARM64 architecture verified.
- 47 EditMode + 52 required PlayMode + 17 native Android + 15 web checks passed
  (131). Three optional private-file tests were explicitly skipped. TypeScript,
  shared web production build and native lint passed.
- Native state emitted by Unity was inspected in a desktop Edge fixture. Search,
  metadata editing, return to chat/inline artifact interaction, restored state
  values, assignment history and horizontal page bounds passed. Fixture responses
  are simulated; actual assignment/persistence/ownership are tested in Unity.
- All 103 C# source files, 112 web build files and the native AAR matched the build
  mirror. Reports, source hashes, build logs and inspected book PNGs are archived
  in `.quest-evidence/avatar-activities/verified-906ED6F1`.

Earlier failed test attempts and an editor-shutdown timeout were corrected or
retried before this verified checkpoint. Only the successful full pipeline is
represented above. No device query, install or headset launch occurred. The
full release and physical-device acceptance gates remain open.

## Pending: conversational recipe creation (2026-09-26)

Device work remains on hold until the user explicitly reconnects/returns. After
installing shared-workspace checkpoint `7EC419D6`, verify authenticated
managed and BYOK text turns and recorded-speech turns independently. Try making a
small waving robot, a coloured ball, moving/resizing/painting a named object, and
Undo/Redo. Verify native receipts match the actual room and that unrelated language
practice does not mutate it. Pause or change conversations while a model request
is pending: no late scene action may execute. Move an object during planning and
verify a stale edit is rejected without partial creation. Save/reopen and inspect
the recipe robot. Check labels, collider/grab behaviour and sustained performance.

Open Workshop using the physical token and by a language request. Inspect a part,
confirm its outline on the actual model, edit/apply/Undo a key and rest pose, and
return to unchanged chat. Move an unrelated physics object during a draft; the
edit should still apply. Move the edited object; the stale draft must remain visible
and require reload. Switch physical selection and verify outline cleanup. Reopen
the book after cancellation and ensure no old acknowledgement triggers an edit.
Check keyboard/input reachability, text size and both-hand pointer operation.

These checks do not establish the separate Live voice path. The initial behaviour
blocks now need their own Quest acceptance described in QUEST_BEHAVIOUR_WORKSPACE.md.


## Spoken agent handoff (not headset accepted)

Use a build containing the Live/observer handoff checkpoint documented in
QUEST_UNIFIED_AGENT.md; the previously installed APK does not establish coverage.

1. With a connected room, ask aloud for a small blue box robot. Check that Maestro
   first proposes the handoff normally, then the agent status and actual result
   appear in the same chat. Inspect the robot and its task receipt; a spoken
   promise alone does not pass this check. Repeat through the passive observer.
2. Ask to translate or practise the sentence "make a blue robot", and ask a
   hypothetical about creating one. No room mutation should occur. Include
   background speech and ambiguous pointing requests in provider acceptance.
3. Continue a different language conversation while the task runs. Its original
   request and native effects must remain tied to the source turn. Stop during
   planning and final narration; existing room effects must remain recorded and
   the task activity must clear without blocking normal speech/chat.
4. Verify a turn that produces both a normal chat artifact and an agent handoff.
   The artifact stays in chat and the handoff executes once with its original
   transcript. Reloading must not automatically replay the task.
5. Change account, language conversation or native session during preparation.
   The old turn must not dispatch into the new scope. Original sent audio/camera
   context now has PC-side verification; it requires the separate acceptance below.
   Audible result scheduling has PC coverage; verify it using the checks below.


## Original Live media handoff acceptance (pending headset/provider)

PC evidence: `.quest-evidence/live-input-handoff`; 270 targeted and 65 prompt
checks pass, with TypeScript, lint, ownership guards and the production build.
Audio hardware, provider responses and native effects in these tests are simulated.
No APK was packaged, deployed or installed for this checkpoint; device hold remains.

1. Use a private test scene and request a small creation matching a visible item
   while speaking its details. Check the actual room result and receipt against
   the original item. Verify subscription and BYOK access independently.
2. Show one item during the request, then change the view before the tutor finishes.
   The agent must use the submitted request frames, not the later chat snapshot.
   Ambiguous or absent details should elicit clarification without guessed edits.
3. Disable camera sharing before a frame finishes encoding. Confirm no further
   frames are sent or retained; spoken requests must still work with audio only.
4. Exceed the 90-second audio or 4 MiB decoded-context limit. The task must explain
   that original context is unavailable and request a shorter turn, with no model
   planning or room action. Repeat with interruption and a lost native session.
5. Inspect usage, storage, frame pacing and memory during repeated long requests,
   including eight pending proposals. Confirm source-history deletion removes the
   task journal. Confirm media bytes are absent from diagnostic request logs and
   ordinary tutor history. Do not infer server cancellation or zero cost from Stop.


## Audible agent result acceptance (pending headset/provider)

PC evidence: `.quest-evidence/agent-task-speech`; 354 targeted and 65 prompt tests,
TypeScript, lint, ownership guards and the production build pass. Provider audio,
microphone hardware and native effects are simulated. No APK/install/deployment
or device query is included; the charging hold remains in force.

1. Finish a typed task and a spoken Live/observer task. Verify each actual result
   appears in original chat and speaks once in the chosen voice and languages,
   respecting the native-language playback setting. Check the usual speech flag
   and avatar state. Inspect room effects and receipts independently of narration.
2. Keep talking when the task completes. The result must wait for the current turn
   and queued speech to drain. Repeat while recording a message, playing an
   artifact, and holding interaction controls. No competing microphone capture
   or duplicate result should start.
3. Let the result speak while Live was selected but locally armed. Verify it
   returns to armed listening only after playback; then make another request.
   Repeat with the passive observer. Confirm result audio is not transcribed as
   new user speech and does not produce an accidental room request.
4. Stop speech during synthesis and during playback, and start Live while a result
   is playing. Verify output ends before new input starts. Change conversation,
   account/native session, hide/suspend the app and remove source history during
   queued and active results. No stale result should play in the new context.
5. Reload saved history and reopen task details. Saved results must not auto-play
   or repeat native actions. Manual message playback should still use normal TTS.
   Disconnect audio/provider access and verify text/receipts survive failures.
6. Repeat tasks over a long session. Inspect managed/BYOK usage and actual acoustic
   timing; simulated tests do not establish provider costs, barge-in quality,
   native microphone ownership or sustained Quest performance.


## Task result recovery and database v11 acceptance (pending Quest)

PC evidence: `.quest-evidence/agent-task-recovery`. Real Chromium/IndexedDB probes
verify migration, app history loading, rollback and deletion. This has not been
packaged or installed on Quest, and device work remains on charging hold.

1. Back up the installed app before the eventual upgrade. Retain existing chat,
   bookmarks and task records; upgrade and verify they remain readable. Open an
   old task's details and inspect acknowledged and unconfirmed actions.
2. Run a task, switch conversation, then return. Check the recorded final status
   and result against the actual room. Restart the app and repeat; there must be
   no repeat action, automatic speech or claim of success for an unconfirmed action.
3. Delete just a result, restart, and verify it remains hidden. Delete a source
   message and verify associated visible results and private task details are
   removed. Existing room objects remain governed by normal room actions.
4. Verify current chat exports include visible results and full task journals
   using the backup acceptance below. Restored references must never execute
   commands. Legacy chat-only files contain no full task evidence.
5. Interrupt storage with low-space conditions and lifecycle suspension during
   completion. Verify journal/chat consistency and recovery without dispatching
   an action whose required durable write failed. Inspect long-history load time
   and memory with retained original Live media; routine loads use compact summaries.


## Full task backup/import acceptance (pending Quest)

PC evidence: `.quest-evidence/task-backup/receipt.json`, generated by the actual
backup hook using an isolated Chromium profile and real IndexedDB. No provider,
native action, headset query, APK packaging or installation is included.

1. Export one chat and all chats with completed, hidden and interrupted tasks,
   including an approved Live turn with speech and frames. Verify the file-content
   notice, native share/picker support and readable saved receipts after restore.
   Confirm exact source context is retained while ordinary chat stays compact.
2. Load a complete archive and merge the same chat twice. Verify bookmarks,
   profile, hidden results, original context and receipts; no duplicated actions,
   automatic speech or resumed task may occur. Merge must not alter other chats.
   A selected language absent from the archive must leave the chat unchanged.
3. Start a task, then import. Observe cancellation and any last acknowledged
   effects before replacement. Imported tasks must remain read-only; inspect the
   room independently because restoring history does not restore or undo objects.
4. Try a truncated archive, incorrect task checksum, missing source turn and
   insufficient storage during staging and commit. Existing saved history must
   survive every unsuccessful replacement. After restart, verify no partial
   journal/projection state. Measure memory/time with long chats and retained media.
5. Verify legacy chat-only imports and a database v9-to-v10 upgrade preserve
   ordinary messages. Test WebView lifecycle suspension during file reading and
   commit. Staging needs extra free space; interrupted staging is cleaned by a
   subsequent import after seven days. Native room/model/library backups remain
   separate from chat/task archives.


## Conversational task control acceptance (pending provider/Quest)

PC evidence: `.quest-evidence/task-steering`, using the real browser runner,
dispatcher, database and task details with simulated model/native behavior.
Database v10-to-v11 migration preserves hidden results and backfills compact scope.
No hardware query, APK installation or provider usage is included.

1. Start a multi-step task by text and by Live speech, then ask it to stop while
   planning and during final narration. Observe status and actual room effects.
   Completed effects remain; no replacement active task may be cancelled. Measure
   latency through the actual tutor/verifier and confirm final speech occurs once.
2. Revise a running creation and answer an agent clarification with a short reply.
   Check the original constraints and actual new request are both respected, the
   prior runner finishes before new edits, and the existing object is modified.
   Continue a limited task without recreating successful earlier actions.
3. Request stop while a native action awaits acknowledgement. If the outcome is
   unknown or the room handshake changes, no speculative revision/retry may run.
   Inspect the room and make a fresh specific request. Contrast stopping the agent
   with asking to stop an object's animation; they must not be confused.
4. Say/quote/translate "stop", "continue" and "make it blue" as lesson content in
   several languages. No task control may start without actual current intent.
   Try ambiguous references with several tasks, an unavailable older task and
   background speech. The tutor should clarify, never silently select another.
5. Switch conversation, account or native room while classification/control waits;
   delete the source or import history. No stale control may act in the new scope.
   Imported and other-room records remain history; ask a fresh self-contained
   request to work in the current room. Restore backups with linked task records.
6. Repeat a turn's delivery, interrupt storage and restart the app. Claims, links
   and receipts must remain readable with no automatic resumption or speech.
   Confirm managed/BYOK usage and the existing server settlement behavior; client
   cancellation does not prove zero provider cost or remote cancellation.

## Action catalog acceptance pending

Once device work resumes, open the book's Workshop and Action catalog. Search,
page, inspect a definition and check concrete arguments. Verify that an occupied
check does not interrupt playback, readiness updates after Stop, and editing the
arguments clears the old readiness. Add a block to a behaviour draft and Apply;
then trigger it normally. Verify a simultaneous physical edit retains the draft
and requires Reload. Test readable input and hand/controller interaction on the
actual two-page surface. Ask the original chat agent for the equivalent behaviour,
including an unavailable action, and verify the native receipt matches its reply.
No headset query or installation was performed for this PC checkpoint.

## One-off actions: headset acceptance pending

Run a gesture and a recorded object movement from chat and from the book catalog.
Check the displayed run ID/phase, cancel one while another independent action
continues, and grab an animated item. Verify no saved behaviour or Undo entry is
created; busy rejection does not interrupt the current owner. Cancel an animation
during loading and verify it cannot resume. Test pause/focus loss and reconnection
without duplicate execution, including a lost acknowledgement. Verify the book
can read and operate Run/Stop controls using hands/controllers. Device work remains
on hold; PC execution, bridge tests and browser replay do not prove these checks.


## Event-program development checkpoint (2026-09-27)

Version-3 programs now retain typed state across event/timer waits in the existing
interpreter. Named signals, bounded indexed event dispatch, timer delays, causal
budgets and idle resource release share the user/agent/native execution path.
The optional book exposes event blocks, state, signals and per-behaviour Stop.
Saving does not enable a run; pause, edits and reload cancel without catch-up.
See [the current contract and boundaries](QUEST_EVENT_PROGRAMS.md). Earlier notes
marking all event waits/timers pending describe prior checkpoints. Durable state,
wall-clock scheduling, parallel branches, channel blending and full release
acceptance remain open; no headset install or backend deployment is included.


### Pending: typed contact reactions

After device work resumes, use a program waiting for object.collided with source
set to a created ball. Bind speed and otherKind in the optional book editor, and
use the received branch to play a short gesture/recording on a different declared
object. Load/check the room and start physics; drop and throw the ball onto an
object and scanned floor/wall. Verify the trace identifies the counterpart and
that motion reacts only to actual new contacts. Stop and pause must prevent later
reactions; returning focus must not restart the program. Check readability of
field controls, repeated/compound contacts, event overflow and sustained Quest
frame timing. scannedRoom intentionally does not claim floor/wall labels.
No headset installation or acceptance is recorded for this increment.


### Pending: shared event and fact discovery

In Workshop > Action catalog, switch Browse to Events, page results and inspect
Object contact began. Check typed field labels and source guidance on the book.
Switch to Room facts and inspect Room surfaces ready: false is a value, while an
unavailable runtime must show Unavailable. Load/check room surfaces and verify the
reading refreshes without a new search. Inspect Maestro state across tutor states
and audio suspension. Browsing must not stop a running animation, start physics,
create a listener, alter saved work or offer Run action for an event/fact. Ask the
chat agent to discover relevant events/facts and prepare a behaviour without starting
it; compare its evidence with the book. PC checks and browser replay do not establish
headset or real-provider acceptance. No installation is included in this increment.


### Pending: parameterized distance reactions

In the book's event editor, choose object.proximity.changed and select two room
objects, radius, hysteresis and enter/exit/either. Prepare the same behaviour through
chat and compare the saved program. Start it outside the radius, then move an object
near the other using a controller/hand or animation. Confirm one enter reaction and
one exit only beyond radius+hysteresis, with no jitter while hovering in the band.
Initially inside is a baseline, not an immediate trigger. Distance measures object
origins rather than visible mesh edges; check whether the labels make that clear.
Pause/Stop must cancel; rearming or returning focus must not replay old crossings.
Confirm missing objects fail visibly and independent waits use their own inputs.
Measure frame timing and read/control the schema-generated inputs on the headset.
PC Unity execution and browser replay do not establish these acceptance checks;
no install is included in this increment.

### Pending: visual function authoring (2026-09-30)

- In Functions & code, create a function with two number parameters and a number
  return. Use a multiply expression for its Return block; call it twice from the
  starting function with different inputs and store the result in a local.
- Bind a Wait action's seconds to that local. Its block should display the variable
  name, not the unused literal. Apply saves only; Try starts execution.
- Rename the function/parameters and reorder the parameters. Both calls and the
  return calculation should retain their meaning. Invalid names or deleting a used
  variable must keep the editor draft open without replacing the valid program.
- Check hand/controller scrolling, text entry, focus and readability of these
  controls in the actual book. This remains untested on the charging headset.

### Pending: typed lists and records (2026-09-30)

- In Functions & code, give a local a list-of-records type with id/text and red/number
  fields. Add two items, edit their values and use append/at/field expressions. Confirm
  the nested controls remain readable and usable with hands/controllers on the book.
- Ask the original chat agent to create two objects, collect their returned IDs and
  paint them through a typed function. Compare saved source and native observations
  with the equivalent manual program. Apply must save only; an explicit start runs it.
- Copy the collection and change one record in the copy. Inspect both: the source
  should be unchanged. Invalid indexes/oversized values must fail visibly without
  removing earlier creations; an arbitrary ID must not grant editing authority.
- Exercise Stop, focus loss and reconnection while a program waits. No state restore
  or automatic replay should occur. Check sustained frame timing for repeated bounded
  data edits alongside MR and the browser.
PC execution and browser replay are complete; these headset/provider checks are not.
See [the exact limits and evidence](QUEST_PROGRAM_DATA.md). No install is included.

### Pending: state and signal declaration editing (2026-09-30)

- Open Functions & code > Edit state & signals. Create a number state, an empty list
  of numbers and two named numeric signals. Use them in a helper function and a
  Forever/event-wait loop. Verify naming, type selection, initial values and scrolling
  are readable and operable with hands/controllers on the actual book.
- Rename the states/signals after blocks use them. References in every function should
  update, while literal text and native arguments stay unchanged. Try deleting a used
  state and an incompatible type change: the draft must remain repairable.
- Use another behaviour listening for the emitted signal. Rename only the sender's
  signal and verify the listener keeps its original name. Attempt conflicting payload
  types; native rejection must leave saved definitions and valid running work intact.
- Compare the same creation/editing request through original Maestro chat with manual
  results. Apply saves only; explicit Start arms waits, and signals are separate actions.
  Stop/pause must cancel without replay; a new run starts from declared initial values.
- Confirm stale edits after an incoming change require reopening the declarations.
PC native execution and browser replay do not establish these device/provider checks.
No headset query or installation was performed for this checkpoint.

### Pending: pinned program modules (2026-09-30)

- Inspect the nested counter example on the real book. Open a pinned module and read
  its exports, object requirements, signal connections and imported blocks. Confirm
  qualified state names and the running-node highlight are legible with hands/controllers.
- Edit an exported call's arguments/result visually. Apply saves and cancels an old
  run without auto-starting; an explicit Start uses the pinned version and fresh state.
- Run two instances and verify independent state. Stop, focus loss and app pause cancel
  both through the same program; reload preserves definitions without restarting them.
- Compare an original-chat request to reuse an inspected import with a manual edit.
  Neither should alter a pin or guess a library/hash. Library publication, discovery
  and a dedicated import/upgrade UI remain separate unfinished work.
- Measure compilation/inspection of a near-limit four-instance program alongside MR
  and browser interaction. No device timing or real-provider acceptance is claimed
  by the PC tests or the browser replay, and this checkpoint performs no installation.

### Reusable program library (pending headset acceptance)

Publish a saved behaviour with a chosen exported function from Reusable modules.
Confirm the completed receipt, search by its name, inspect its exact content ID and
import it into a second behaviour with explicit signal wires and object access.
Apply must not start it. Run the caller, publish a changed source as a second version,
and confirm the caller keeps its original values. Explicitly replace its pin; an
incompatible export/signature must leave the draft intact. Remove a library copy,
restart the app, and verify saved callers still run their embedded definitions.
Confirm damaged entries show an individual diagnostic and other entries remain usable.
Check readable labels, comfortable scrolling, controller/hand input and operation
status on device. PC replay and native tests do not replace this acceptance.

### Physical motion events (pending headset acceptance)

With a scanned floor, use the shared event block to wait for a solid/bouncy ball to
settle, then change its colour. Check that both rolling and spinning postpone the
reaction until the quiet period. Test report versus baseline initial policy. Hold
the ball, pause physics, and interrupt with an animation: none may report false rest.
Release/resume and check fresh qualification, then throw again and detect moving.
Change a threshold in the book and through the agent; inspect the same saved source
and measured fields. Stop and app pause must cancel without automatic replay. Check
readability of units/help and behaviour under real headset frame timing.

### Parameterized position reads — pending headset acceptance

- In the optional book fact catalog, inspect Object world position, select the
  book, Maestro and a creation, and read each. Change the target and verify the
  previous coordinates are hidden until that target is read.
- Run a program that stores a position, waits and reads again while the target is
  grabbed or animated. Verify the first snapshot stays fixed, the new read follows
  movement, and observation does not interrupt grip/animation.
- Pause the app and remove/disable the target. Verify unavailable readings and
  cancelled/failed runs rather than false origins or automatic replay.
- Check labels, record-field editing, controller/hand selection, rendering cost
  and the equivalent real-provider agent journey on Quest.


### Calculated condition waits — pending device acceptance

With the book's Condition wait editor, watch an object's position against a visible
threshold, then move it across with grip and recorded animation. Check baseline vs
current-match reporting, stable period and timeout. Verify the program does not
interrupt the movement, repeated crossings behave as configured, and state/trace
show the same result as the agent's inspection. Stop, app pause and object removal
must cancel/fail visibly without resuming on focus return. Confirm labels are
readable and hand/controller editing remains practical. Profile eight active
condition watchers with representative record/fact expressions alongside MR,
physics and book rendering; PC sampling limits alone do not establish Quest timing.

### Pending device acceptance: archive selection and preview

This workflow has PC/native/provider coverage but has not been accepted on Quest.
When device work resumes, use the shared book catalog or the chat agent to choose a
workspace archive. Confirm Android opens exactly one file chooser, Cancel returns
a cancelled request, and returning after choosing a valid ZIP shows its verified
preview through the archive-selection fact. The current room, movements and saved
programs must not be replaced or started. An interrupted chat task can inspect the
retained opening receipt on return; do not automatically restart it or the chooser.

Cancel a prepared preview and verify the original Downloads file remains. Try an
invalid file, an oversized file and a slow provider; errors must remain readable,
partial files must be cleaned, and another chooser must wait for the old provider
stream to close. Confirm book/controller/hand operation still works after returning.
Test application pause during verification and process interruption separately.
A preview is not restore acceptance. Activation and content-bound review now have
PC coverage and need the separate device checks below; previous/corrupt-workspace
recovery remains unfinished.


### Pending device acceptance: activation and content-bound review

Device work remains on hold. Run these checks only when it resumes, using a
workspace and backup the tester can afford to replace. They are not yet device
acceptance evidence.

1. Edit an object and behaviour, then activate a verified archive through the book
   catalog. Confirm the operation reports actual progress separately from its
   opening receipt, retains the accepted old workspace, keeps the book/chat alive,
   and loads imported content with programs, motion and physics stopped.
2. Inspect the imported objects, programs, model/motion choices and missing-reference
   counts. Prepare review, make an accepted edit, then attempt completion with the
   earlier hash/revision. Expect a stale review and continued activity hold. Prepare
   again and complete the new exact review. Verify stopped activity stays stopped,
   held buttons require release, and explicit new actions work afterward.
3. Repeat inspection/completion using the real chat agent. Confirm it explains the
   inspected contents and obtains the intended approval before completing review,
   reads the tracked result, and never treats the opening receipt as completion.
   Compare its arguments/results with the manual book workflow.
4. Pause/resume during preparation and completion; cancel while work is pending;
   restart with a prepared review. Check honest cancelled/interrupted/completed
   states, no automatic replay and no stuck or prematurely released controls.
   Separate process-interruption/power-loss checks around activation and approval
   from ordinary pause checks. Preserve logs and all candidate data on uncertainty.
5. Profile representative large libraries and rooms for capture/hash/save stalls,
   memory and thermal cost alongside passthrough and book rendering. Check status
   readability and continued hand/controller recall. A failed save must preserve
   live edits and keep review held; automate fault injection separately from any
   manual storage-capacity check.

Do not mark previous-workspace or corrupt-selection recovery accepted from these
checks. Previous-workspace selection needs the checks below; corrupt-selection
recovery and retained-generation cleanup still need implementation.


### Pending device acceptance: verified previous-workspace recovery

When device work resumes, make a visible accepted edit in the current workspace,
then use the book or real chat agent to read `workspace.previous` and select its
exact identity. Confirm no Android chooser opens, no live content changes during
inspection, and the tracked preview eventually verifies or reports a readable
failure. Inspect the preview, then activate it. Verify the previous room opens
under review and today's accepted edit exists in the newly retained previous
workspace. Complete review and start desired activity explicitly.

Repeat with cancellation during preparation, app pause, restart and a large motion
library. Change the selection revision after preparing a preview and confirm it
cannot activate with either old or newly substituted revision arguments. Never
silently substitute a missing source with an empty room. Test corrupt files through
controlled fixtures; retain their original bytes. These checks do not establish
recovery from unreadable current content or corrupt selection metadata.


### Pending integration and device acceptance: damaged-workspace preservation

The internal preservation boundary has PC coverage; there is no user-facing
corrupt-workspace recovery control yet. After that operation is integrated, use a
disposable fixture containing an unreadable store and a valid newer unsaved edit.
Verify that recovery keeps both the exact damaged bytes and labelled accepted
state, that temporary and saved rooms stay distinct, and that pause/teardown cannot
overwrite originals while the hold is owned. Cancel while older saves are pending
and confirm controls stay held until those saves settle.

Validate private-path/link checks on the real Quest filesystem, limited available
storage, large captures, app suspension and process termination. Compare evidence
entry hashes independently and verify ordinary archive import rejects the evidence
format. Exact selection metadata, durable recovery outcome and replacement must be
checked as part of the eventual full recovery journey; these foundation checks
alone cannot establish that corrupt-workspace recovery is accepted.


After the damaged-selection coordinator is integrated, also test both missing and
unreadable pointer files. Inspection must leave them unchanged; selection must
verify a chosen candidate, and an altered pointer or backup must invalidate that
choice. Verify the exact original files and capture hashes after recovery. Inject
interruption immediately before/after pointer commit and compare the recorded
selection after restart; no fallback room or automatic activity is acceptable.
A cancelled operation's evidence and a selected generation with a lost operation
record must remain protected until explicit maintenance. Current storage tests do
not establish this complete in-headset journey.


## Damaged-workspace recovery through the book and agent

Pending device acceptance; desktop PlayMode evidence is not headset acceptance.
Use disposable test storage with a verified retained generation and damaged current
selection or behaviour data. Inspect recovery choices, read the candidate by request
and index, select its exact hash, inspect the copied preview and missing references,
and commit only that requested preview. Confirm the ordinary book stays available,
new contents require review, and completing review still does not start activity.
Compare original damaged bytes and accepted-state evidence after replacement.

Repeat without live content owners, cancelling during verification/preservation,
and pausing or restarting around commit. Uncertain outcomes must stay held rather
than retry; a restart must reconcile the durable selection without replaying old
actions. Return to inspection for a different candidate after a safe failed attempt.
Verify that another host waits for outstanding workers and that old owners never
write into newly selected content. Exercise a low-storage failure without deleting
original data or preserved evidence. If no retained candidate verifies, test both an external archive and an explicitly
requested fresh workspace. Import preparation must leave a missing/damaged pointer
unchanged. Fresh preview must identify its source and survive restart without
automatic commit. After separate commit/review it has only the included book and
Maestro, default controls and no user programs or assets; inspect preservation of
the prior data. Unreadable coordinator history still reports unavailable; its repair
remains release work.


## Full-app shared-client integration (2026-10-02)

The required Editor room probe now exercises actual MaestroRoom startup and the
same RoomAgentClient as the book: catalog discovery, one-off create with a durable
receipt, paint, Undo paint, Undo creation and native diagnostics. This exposed and
fixed complete-room feature-count and absent-memory serialization failures. See
[probe contract](QUEST_NATIVE_ROOM_PROBE.md). The test uses an isolated workspace,
no headset and no AI provider. Android WebView, input, room scan, provider and
physical performance gates above remain required. Do not use this result to mark
those gates complete.


## Planar ink and chalkboard — partial Quest 3 evidence

Device work has resumed. The hand-input section above records pinch drawing,
physical Undo/Redo and prop obstruction on the current installed APK. The broader
checks below remain the acceptance checklist; the earlier hold is superseded:

- Ask for a Chalkboard, put it within reach and use Surface with controller trigger
  and hand pinch. Confirm the tip follows the visible patch and misses make no ink.
- Select colours, erase one stroke, then Undo/Redo. The board must remain present.
  Check the widened 3D tray is reachable and its labels remain readable.
- Move, rotate, scale and copy the marked board. Marks must follow its Board part.
- Interrupt drawing with a grab or app pause; verify the retained draft is visible,
  retry/discard is understandable, and retry cannot change a replaced patch.
- Keep/Discard temporary ink, restart, and export/import a workspace. Confirm saved
  colour, thickness and identity, and no repeated stroke after reconnect.
- Profile a representative busy room and the configured ink limits with the book,
  default avatar and its animations active. Admission limits alone are not a
  performance pass. Check ray behaviour around foreground/occluding objects;
  this increment uses explicit patch intersections, not full mesh projection.

### Held drawing tips — pending Quest acceptance

Create Chalk and Chalkboard from the shared catalog. Grip chalk with a controller,
then with hand tracking; draw a line, lift, release, Undo and Redo. Move the board:
ink must follow it. Change the tip colour/width in the book and verify the tray's
pencil preferences stay unchanged. Test imported-root/recipe-part tips, different
scales, simultaneous tools, interrupted tracking, room pause and failed-save
retry/discard. A loose/resting chalk must not draw. Maestro-held chalk must respect
human surface ownership. Measure frame time with active tools and full ink budgets.
Desktop tests and browser replay do not close this device gate.

### Physical hinge acceptance — pending Quest test

Using the shared workshop or agent, create a fixed mount and solid moving member,
configure their local hinge frames, explicitly align, then Start physics. Try a
free spinner, a limited lever, spring return and motor. Grip/release the member
and check that it stays attached. Pause/resume and reload at a nonzero angle: the
saved limits and spring target must retain the same zero reference, with no old
throw speed restored. Move/resize a member, delete/Undo its mount, and confirm
that misaligned or missing connections freeze and explain their state until
explicit repair. Test interaction against scanned surfaces and all 16 admitted
hinges while observing native frame timing. Desktop PhysX and book-form checks
do not establish Quest comfort, stability or performance.


### Included connected Spring lever — pending Quest acceptance

Inspect/copy Spring lever from the shared module library and call `create` with a
placement in clear space. Confirm that one mount and one handle appear, then Start
physics. Push the handle with an item and grip/release it using controllers and
hands: it should move within its limits, remain attached and return toward zero.
Create a second instance: it must connect only its own pieces. Undo/Redo creation,
reload at a nonzero angle and test temporary-room discard. Edit the copy's geometry
and spring settings through the ordinary source/forms. Check paused-room behavior,
scan collisions and native frame timing with multiple instances. Desktop PhysX and
native transport checks do not close these headset gates.


## Shared construction selection (headset acceptance pending)

On the movable creation tray choose **Collect pieces**. Point and tap/pinch two
creations with identical names, remove one, add it again and choose **Finish**.
Confirm outlines follow membership, the count is readable, and the objects' ordinary
item-tapped behaviours do not run during collection. Grip should still move one
piece. In the optional book workshop, confirm the same ordered members and distinct
labels, use **Locate**, reorder the origin and open **Review reusable construction**.
Opening the draft must not save anything; load current values and explicitly run.
Instantiate the published module and check placement, internal hinges and Undo.

Start collecting, then choose a drawing tool or open a Quest system screen. Picking
must end while existing members remain selected. Delete a member, Undo, and verify
it does not silently become selected again. Start/end a temporary room and change
workspace to check selection clears. Record the APK hash, input mode, button reach,
outline visibility and normal/busy-room frame measurements. Group grabbing and
socket snapping are separate unfinished features.

## Construction movement (headset acceptance pending)

With physics paused, collect two or more creations, finish collecting and choose
**Move pieces** on the creation tray or **Move together in room** in the book.
Grip the solid cross, move and rotate it, then use two grips to resize the whole
arrangement. One released grip must keep the preview; the last release must save.
Undo once should restore every member. Check both controllers and hand tracking,
including readability and reach when the first selected piece is small or low.

While holding, member grips and book selection edits must be unavailable. Recall,
opening a system screen, starting physics or beginning drawing must cancel the
preview and restore all starting poses without adding Undo. Test two hinged pieces
together; moving only one must explain that both ends are required. This is an
arrangement handle, not a physical weld: after placement the pieces retain their
ordinary physics. Test save failure/space limits separately from physical comfort.

### Rigid and breakable connection acceptance — pending Quest test

With physics paused, place two creations, choose Connect physical pieces / attach,
select the two members, load current values and join them. Their poses must remain
unchanged. Start physics and grip/throw the joined build. Test a dynamic-to-fixed
mount and a dynamic-to-dynamic pair; connected members suppress mutual collisions.
Use low positive break limits, cause a break, and observe a program waiting for
object.connection.broken. It must run once for that break. Pause/resume must not
repair it. Explicit align/rearm may repair it; rearm must not add an Undo entry.

Capture the build, instantiate its module and verify fresh internal member IDs,
retained break limits and one Undo. Verify unavailable old v6 room recovery without
silent replacement. Repeat with up to sixteen links while measuring frame timing,
power and grip comfort. Desktop tests do not substitute for this device acceptance.

### Sliding mechanism acceptance — pending Quest 3

Create the included Spring button through the ordinary module/workshop flow.
Start room physics after alignment. Grip its cap, press it into the mount, release,
and verify bounded travel and spring return. Check the same press/release thresholds
in an editable program; a held press must not repeat until release. Try pushing it
with an ordinary solid object. Report whether hand/controller physical contact is
available before advertising finger pressing. Move/rotate/resize the complete
construction, pause/resume, recenter and reopen the room. Verify resting position,
readable book controls and frame-time traces. No installation or headset test has
been performed for this increment while the device hold remains active.

### Explicit snap points — pending Quest 3

Create two building bricks. With physics paused, use the book action “Snap a
construction to a point”: first brick Bottom to second brick Top, first Place at
point, then Place and join with explicit break limits. Load both current revisions.
Verify upright/quarter-turn placement, a single Undo, scale differences and a
rotated destination. In join mode, start physics and grip/throw the construction.
Capture and recreate it, retaining editable points and fresh object IDs. Try a
mismatched family, a held member and a missing point; none may partly move/save.
This flow does not yet offer automatic near-point grip previews. Check readability
and controller/hand comfort before advertising headset acceptance.


## Bounded container pouring (pending Quest acceptance)

Use two ordinary included cups. Configure the source with 400 ml of water through
the shared container editor or ask Maestro to fill it; leave the receiver empty.
Load/check the real-room scan and start physics. Hold both cups, place the receiver
below the source lip, and tilt the source. Confirm the visible stream enters the
receiver and both levels change. Return the source upright and release it. Read
accepted contents after publication and check one Undo restores both quantities.

Repeat with a real scanned surface between the cups, a nearly full receiver and
an incompatible liquid. A blocked stream must not fill the receiver; overflow
counts as uncollected spill, without implying a visible puddle. Pause physics
while pouring and return from headset sleep: saved amounts must agree with the
last successful publication and no earlier event should replay. Check temporary
room discard and the existing runtime's pour-event wait.

Profile two-hand/controller tracking, stream readability and sustained frame time
with the maximum 16 configured containers. Desktop conservation/collision checks
do not satisfy this device gate. No device installation or test is claimed here.


## Virtual-room snapshots

On the candidate build, frame the default avatar and a newly created colored
object, ask Maestro to inspect their appearance, and compare Task details with
the native view. Verify page/chat and tool trays are absent and the real room is
not represented. Move/animate the avatar and repeat; check current pose, correct
textures, orientation and framing. Test manual catalog capture without a task:
no provider request should occur. Verify Stop, suspend/resume and reconnect do
not upload stale frames or retake an expired receipt. Profile CPU/GPU frame cost,
readback/encoding stalls, memory and thermal behavior on Quest 3. Also test the
shared image request through the actual managed and BYOK provider routes; mocked
provider transport is not model-interpretation acceptance.

## Included fort and spinner: device acceptance pending

Through the existing chat/module library, create Small fort in a clear area and
Passive spinner within reach. Confirm both constructors leave physics stopped
until explicitly started. With a valid room scan, verify the four towers settle
and stand, individual bricks can be grabbed, and a thrown ball knocks pieces away.
Capture the fort as a structure after settling; verify a displacement-driven
program reacts and a reset restores it after the projectile has been moved clear.
Check one Undo removes a newly created construction as a whole. Confirm the fixed
base is independently movable and does not imply all loose pieces move with it.

For the spinner, verify tangential contact from a held object turns the rotor
without translating its mount or detaching the hinge. Test rotor gripping with
controllers and hands; direct finger-flick support is not established by desktop
contact tests. Check grip release,
tracking loss, app pause and explicit physics restart. Inspect/copy the
construction and verify the new rotor connects to its new mount. Evaluate hand
reach, small-collider tunnelling, readability and frame time with the full kit.
Desktop ball-contact tests are not hand/controller device acceptance. Do not
mark this section passed from Editor tests or renders.


## Bucket and basin scooping: device acceptance pending

Create the ordinary Water basin and Bucket templates in a reachable clear area;
start room physics only after room setup. With controller grip, then tracked hands,
submerge the bucket's full opening inside the basin without passing through its
floor or walls. Confirm it fills, stops after lifting clear, retains its quantity,
and pours into an ordinary cup. Try a second vessel concurrently and a nearly full
receiver. Compare accepted/live quantities through the shared catalog; Undo should
restore the whole completed flow episode, with no event replay.

An opening above water, incompatible nonempty contents, inverted vessel or solid
lid should prevent scooping. A cross-handle should still permit a clear sampled
path. Depleting the basin lowers its surface and should stop intake once the full
opening is no longer submerged. Test a user program waiting on the saved-scoop
event, physics pause, tracking loss/headset sleep, temporary-room discard and save
failure recovery. Measure sustained frame time at the supported container limit.
Record visual fill-plane clarity and grip comfort separately from measured
quantities. This model has no fluid forces, displacement or persistent spill field.
No current headset installation or acceptance is claimed by desktop checks.


For liquid identity transitions, configure a second basin with a different liquid
identifier (then with just a different colour). Scoop, empty and refill promptly.
The first saved event must name the earlier liquid and only its own quantities;
the refill belongs to a new episode. Undo restores those episodes separately.
Repeat using poured intake. A matching refill should remain in the same episode.
Also verify a failed save at the transition stops intake and requires a physics
restart after storage recovery.


### Curved drawing acceptance

Configure cylinder and sphere patches on matching objects. With controllers and
hands, draw across the visible curve, lift/release, move and resize the object,
erase a stroke and Undo. Repeat with a held drawing object. Confirm no ink crosses
the hidden backside, patch seam or another solid object. Check copied objects and
saved/reloaded marks against source, then exercise temporary Keep/Discard. Record
thin strokes on large curves and sustained high-point drawing performance; desktop
geometry/render checks do not establish Quest frame-time or contact comfort.


### Held eraser and drawing kit acceptance — pending Quest

Create Pencil, Paint brush and Eraser from the same starter library. Grip each and
contact a configured board; confirm the pencil is narrow/dark and brush wider/blue,
independently of tray colour and width. Release or separate to finish one stroke.
A loose tool resting on the board must not draw or erase by itself.

Draw two crossing strokes and a separate distant stroke. Sweep the held Eraser
across the first two: only touched complete strokes should disappear. Lift it and
Undo once; both return. Repeat on cylinder/sphere patches, rotated/scaled boards,
and with a solid object obscuring part of the path. Ink beyond the obstruction
must remain. Check hand and controller grip, contact readability and sustained
frame time with the fuller drawing budget; desktop results do not establish these.

Interrupt a held erase by pausing or taking surface ownership. If a retained edit
is shown, Discard erasing restores its preview, while Retry erasing applies the
same selected IDs only when the patch remains unchanged. The optional book fields
must allow switching a tip between draw/erase and inspecting its saved mode.


### Chess kit and patterned parts (2026-10-04; device acceptance pending)

On the next authorized headset session, create the Chessboard and call both
included chess constructors at its position, yaw and scale. Check 64 readable
squares and 32 distinct movable pieces, especially rook/knight/bishop/queen/king
recognition at normal reach. Enable scanned-room physics and test moving, dropping
and gently bumping pieces; verify the board supports them and room obstacles still
apply. Place a pawn through the shared Foot-to-square snap action, including E2 to
E4, then Undo and reload. There is no automatic turn or legal-move enforcement.

In the book recipe editor, change checker to stripes, projection, cell counts and
secondary pigment. Verify invalid counts remain drafts, Apply gives one saved edit,
Undo restores the pattern, and rotating or animating the part keeps its pattern
attached. Check distant shimmer and readability while moving the head. Record
sustained frame time with the full set, book and avatar together before acceptance.
These checks are not claimed from desktop renders or native test runs.


### Shared sculptable surfaces — device acceptance pending

Create Snow patch from the starter library in an open area. In the book action
catalog, load its current values and lower a path across the surface. Check the
visible groove and native collision agree, including after moving/resizing the
fixed owner. Ask Maestro for the equivalent edit and inspect its exact action
and result. A stale revision, off-surface path or fifth field must refuse cleanly.

With room physics active, drop a ball onto the surface. Lower the support beneath
the resting ball and verify it falls to the new height. Raise/level another region,
then Undo, copy, save/reload and exercise temporary Keep/Discard. Confirm nearby
unheld objects react without taking control of held or animation-owned objects.
The base object's independent collider can block deeper depressions; this is an
authorable backing, not conserved snow. Paused physics must remain paused.

Exercise four maximum-size fields with the book, avatar and existing objects.
Measure sustained frame time, edit/collider rebuild spikes and readability in MR;
desktop tests do not certify Quest performance. Finger/tool sculpting, gathering
snow into a vessel or snowball, gravity flow and water forces are not present in
this increment. Do not record them as accepted from an authored height change.


### Physical sculpt gestures — device acceptance pending

Use a Snow patch with adequate real space. Enable Lower on the 3D tray, hold a
controller trigger close to the surface, move and release. Confirm one Undo
restores the whole path. Repeat with tracked index contact: separate once after
enabling, touch, move and lift. Try both hands, hand/controller transitions,
tracking loss and headset pause. Interruption must offer Retry/Discard and never
silently save or start again. Check the tray labels, tool reach and mode highlights.

Create the Sculpt brush. Loose contact must do nothing; grip it, touch its gold
head to the field and lift to save. Its saved radius/height must remain independent
of the tray mode. Try a program-held tool and verify that a human edit takes
priority. Reach the path limit and confirm contact must separate before restarting.
Place a solid obstruction between tool and field and confirm no stroke passes
through it. Repeat on moved/rotated/resized fields.

During a gesture only the visible mesh previews the change. Balls and controllers
still collide with the accepted surface until publication. Verify this distinction
is understandable and stable; measure frame timing for four 16-cell fields while
sculpting with other active physics. Desktop tests do not certify Quest comfort,
hand tracking quality, latency or sustained performance. This update has not been
installed while the headset hold remains active.


### Shared surface transfer

On two configured snow patches, ask Maestro to transfer one local litre from the
centre of the first patch to the second. Verify the first dips and the second
rises, and compare the returned removed/added volumes with `object.field`.
One Undo must restore both. Repeat with an edge footprint and differently sized
grids. The book form must load both current revisions; stale or identical targets
must refuse. Resizing a patch changes its displayed size, not the local litre
measure. This is an explicit saved edit; it is not a physical shovel or granular
simulation. Check frame time during maximum-size edits and collisions on the
accepted surface. Desktop results do not complete this device check.

### Measured material packing — device acceptance pending

On the current native build, create a snow patch and ask Maestro to pack half a
litre from its centre into a snowball at a visible reachable room position. The
manual fallback is the book capability form: **Pack surface material into a ball**,
select the patch, load current values, review quantity/position/mass, then Run.
The returned amount may be slightly below the request because field heights are
floats. Confirm the ball appears and the centre patch changes. With room physics
ready, grab/release it and throw it gently at the scanned floor. It should behave
as an ordinary solid prop and remain readable/selectable.

Undo once must remove the ball and restore the patch; Redo restores the same ball.
Save/restart should retain its measured contents. Resizing it must not silently
change those contents. Packing while a source sculpt gesture is active or
retained must refuse. This is an explicit action, not a physical hand-scoop or
shovel gesture. Record frame time and interaction comfort alongside other active
props; desktop tests do not establish headset acceptance. Headset work remains on
hold until resumed by the user.


### Physical material scoop — device acceptance pending

Create a Snow patch and Material scoop. Grip the tool, face its opening up, touch
the field and lift. Check that one bounded quantity leaves the field and appears
on the blade. Holding contact still or dragging must not repeatedly fill it.
Invert the tool, touch a different part of the field and lift to deposit. Inspect
the shared material/capture facts and verify one Undo restores both sides.

Repeat with controllers, tracked hands and a program-held tool. Check solid
obstructions, a full store, an empty source and incompatible material. Loose
contact must do nothing. Pause/lose tracking or take over a program-held tool
mid-contact: no automatic save/restart; explicit Retry/Discard must affect only
the retained session. Test save/reload and temporary-room discard. Check heap
readability, orientation threshold, contact comfort and mesh/collider publication
latency with other active objects. Desktop results do not establish these device
outcomes. Headset work remains on hold.


## Saved scanned ink layers — pending Quest acceptance

Use a build advertising `scanDrawingLayers.v1`. Load the intended room and check
its displayed alignment first; layer creation must not start scanning or physics.
Keep the existing device-work hold until the owner confirms availability.

1. Look at a clear wall and ask Maestro for a 60 × 40 cm drawing area. Check that
   the layer has no background panel and faces the room. Draw with the controller,
   hand and held pencil; erase, Undo and Redo. Check readability, contact and depth.
2. Restart and load the same scan: saved ink should return to the exact surface.
   Lose tracking or switch to virtual view during a stroke: capture must stop,
   retain the draft and refuse Retry while unavailable. Restore the same room and
   retry or discard explicitly. Recall must leave saved wall ink in place.
3. Load a different room or rescan so old identities disappear: ink must be hidden,
   retained and inspectable, never moved to a similarly named nearby wall. Rebind
   explicitly through the book/agent, check preserved ink, then Undo the rebind.
4. Try too-large and rotated areas near wall corners and known cutouts. Refusal
   must not create a partial layer or choose a farther wall. Openings omitted by
   Meta's scan are outside the app's knowledge; check actual overlay alignment.
5. Try edits in a temporary room and Discard; export/import a workspace containing
   layers. Check that saved IDs and ink survive, and missing anchors stay hidden.
6. Measure sustained frame time and allocation/load with the maximum admitted ink
   and several visible layers while tracked anchors update. Desktop pass counts
   do not establish Quest performance or contact comfort.


## Held chalk and fingertip material contact — 2026-10-05

On development APK `0EC7EC29`, Meta XR Operator delivered actual OpenXR controller
grip and Meta aim-hand pinch input to a temporary Chalk/Chalkboard pair. With the
tray pencil disabled, loose chalk touching the board created no stroke. Moving
held chalk and separating saved a 25-point controller stroke and a separate
43-point hand stroke, both at the tool's 4 mm radius. The disabled tray pencil
retained its separate 3 mm setting. The second stroke preserved the first ID;
neither gesture left an active or retained drawing draft.

Fingertip packing exposed an input conflict: a ray merely hovering the distant
**Add model** button prevented physical contact with a snow patch. Pointing the
hand down, so its ray missed the button, produced a preview and then one ball on
lift. The saved source stayed at 79.99999821186066 local litres during contact;
lift removed 0.24999908055178818 litre, exactly matching the ball's measured store
within 0.000001 litre. This is automated device-input evidence, not a claim about
human tracking quality, visual comfort or sustained performance.

The input correction makes only an actual page/button pinch or object grab take
precedence over fingertip contact. Two native regressions use an XR Hands joint
provider and MetaAimHand device through the real book input router: ordinary
hover allows packing/sculpting, while page pinch and tracking loss retain the
pending draft. Both fail on the old code and pass after the correction. Full
package and post-fix headset evidence follow.

Temporary objects were discarded, the original three creation IDs were verified,
tool preferences were restored and synthetic input was released. Evidence:
`.quest-evidence/held-tools-20261005/` (`pre-fix-acceptance.json`, chalk input
journals, packing observations and before/after native test results).


### Post-fix package and device result

`MaestroQuest-hand-contact-F4A5B4B4.apk` (188,151,847 bytes), SHA-256
`F4A5B4B478D6B7FF7EDFCCE62BCF0FD405BB7759E275011960A124757A652473`,
was audited and installed without resetting app data. Verification passed 831
EditMode and 639 PlayMode tests (three optional private-model skips), the 475-state
native-room journey and 71-observation original-book journey, production web and
Android checks, 76 Android tests (two optional skips), all 3,004 frozen inputs,
147 packaged web files, included content, ARM64, v2 signing and 16 KiB alignment.

On this APK, fingertip packing stayed active while that hand's ray hovered the
**Add model** button. Saved field data remained unchanged during the preview;
lift saved one 0.24999908055178818-litre ball. Physical hand-pinch Undo restored the
field and removed the ball; Redo restored the exact identity and quantity.

A separate Recall check exposed a placement defect: the finished ball was
0.07025968 m away from the preview, matching the room's vertical offset applied
twice. Physical packing was passing world coordinates into the shared room-local
packing action. This checkpoint fixes the hover conflict but does **not** claim
correct post-Recall packing placement. Correction and regression checks follow.
Temporary content was discarded, original IDs and tool settings restored, and
synthetic input/debug/proximity overrides released. Detailed assertions are in
`.quest-evidence/held-tools-20261005/post-fix-acceptance.json`.


## Recalled material packing placement — 2026-10-05

The physical gesture now converts its world-space contact/preview position into
room-local coordinates before invoking the shared packing evaluator. Publication
and retry transform the saved candidate back into world space for obstruction
checks. The shared capture fact explicitly describes its position as room-local.
No second packing implementation or saved-data format is introduced.

Two focused tests first reproduced the error with a room translated by (3, 0.4,
-2) m and rotated 63 degrees, then passed after the correction. They cover the
preview/published position, exact source loss, physical Undo/Redo placement, an
obstruction arriving before publication, retained drafts and explicit retry after
the obstruction is removed. The 189 targeted shared-catalog/book tests also pass.
Full verification passed **831 EditMode / 641 PlayMode tests** (three optional
private-model skips), **474 native-room / 74 original-book observations**, the
production web build, Android lint and **76 Android tests** (two optional skips).
The audit matched all **3,006 frozen inputs**, source/metas, 147 packaged web files,
included content, ARM64 libraries, the development manifest, v2 signing and
16 KiB alignment. APK: `MaestroQuest-recalled-packing-11B4972E.apk` (188,154,895 bytes),
SHA-256 `11B4972E0DA36A1DDA7ACEBFC3D29F0831CEC63B4D32BEFD09BD3A1BE8A56B1B`.
It was installed in place and its device APK hash matched the audited package.

On Quest, actual controller B/Recall moved the room origin about 70.2 mm upward.
The book moved the temporary snow field clear through `object.layout.apply`.
Tracked-hand fingertip contact kept the saved field unchanged during the preview;
lift created one ball. The unique `Recipe geometry/packed` transform exactly
matched the recorded world preview (0.0 m difference), and the room-local capture
fact independently transformed to that same position. The source lost
0.24999905144795773 local litre, matching the ball's store. Physical hand-pinch Undo
restored the source and removed the ball; Redo restored its exact identity and
quantity. The initial test command was retried after the freshly opened book was
ready; no application error was found in the inspected startup log.

Original room IDs and tool preferences were restored; synthetic input, test
forwards and debug/proximity overrides were released. Evidence remains local in
`.quest-evidence/recalled-packing-20261005/`, including the before/after regression
reports, package audit and `device-acceptance.json`. These are automated device
input/transform observations, not human ergonomics, real-provider or sustained
performance acceptance.


## Construction workload and room observation cost — 2026-10-05

Quest 3 was measured on the development APK `11B4972E`, using VrApi statistics,
a 72 Hz display and 45 one-second observations per condition. The temporary
workload added 32 building bricks, a fixed chessboard and a recipe robot
to the original three creations: 37 creations plus the book and Maestro, with
181 new recipe parts. The book showed the original chat between measurements.
The scanned room was loaded for the physics-running condition.

| Condition | Mean reported FPS | Mean stale count | Mean app GPU time |
| --- | ---: | ---: | ---: |
| Original room | 71.98 | 5.22 | 6.69 ms |
| Construction, physics paused | 66.89 | 9.78 | 8.31 ms |
| Construction, physics running | 67.20 | 9.22 | 8.05 ms |

This workload missed the 72 Hz target. The app's frame-interval fact reported
approximately 33.40 ms p95 while paused and 31.34 ms with physics, versus
18.54 ms before construction. These app intervals and one-second compositor
statistics are different measurements; the latter are not per-frame latency
percentiles. Temperature was 42–43 C; six physics-running samples reported
power-save level 1, with all other samples at 0. This was a warm development build
with Operator enabled, not release performance acceptance. See Meta's
[VrApi statistics definitions](https://developers.meta.com/vr/documentation/unity/ts-ovrstats/).

A 20-second `simpleperf` sample, resolved against the matching IL2CPP build,
identified scene rendering and book updates as CPU costs. Among UnityMain samples,
`RoomJournal.Snapshot` accounted for about 4.14% cumulatively, reached from the
room observer. Android `PublishRoomAgentState` accounted for about 6.45%, including
JSON parsing and reserialization. These are sampled CPU shares, not frame times;
parent/child percentages overlap and must not be added together.

The object-list publisher now projects only the fields it sends from the journal
into detached observations, then fills live positions and interaction flags from
the room. It no longer deep-copies recipes, strokes, stored motions and other
hidden components four times per second. Android still checks the envelope and
quotes the entire payload as data, but avoids serializing the parsed JSON again.
Update frequency and the human/agent contract are unchanged; inspection and
persistence still own complete copies where needed.

The allocation regression uses Unity's `GC.Alloc` recorder and rejects an
unavailable recorder. With the original copy path, an observation allocated 75
objects for a one-part recipe and 292 for a 32-part recipe; the projection makes
47 in either case. A second test verifies detached settings, ordering, placement
revisions, edits and Undo/Redo. An initial test draft used a .NET allocation
counter that returned zero; those results are not performance evidence. The
supported-counter regression failed before the fix and passed afterwards.

The animation was stopped, physics paused and temporary content discarded. All
three original creation identities were checked before closing the app for
charging. Owned forwards and the profiler sample-rate property were restored.
Private raw profiles, logs, source freeze and regression results remain local in
`.quest-evidence/performance-20261005/`. Allocation savings alone do not establish
a frame-rate improvement or close the sustained-performance gate.

The audited development package passed **833 EditMode / 641 PlayMode tests**
(three optional private-model skips), **129 focused catalog/book web tests**,
**473 native-room / 65 original-book observations**, the production web build,
Android lint and **76 Android tests** (two optional skips). The audit matched
all **3,010 frozen inputs**, source/metas, 147 packaged web files and included
content; ARM64, development manifest, v2 signing and 16 KiB alignment passed.
APK `MaestroQuest-room-observation-5C50CD1F.apk` is 188,158,039 bytes, SHA-256
`5C50CD1F5D221E022320DC15DD9F53ED76F681B8C541491F79AB8E50C6A6BF01`.
It was installed in place, its device hash matched and the owner's authorized
18+ session confirmation was completed. The same workload on this package measured:

| Condition | Samples | Mean reported FPS | Mean stale count | Mean app GPU time |
| --- | ---: | ---: | ---: | ---: |
| Original room | 46 | 71.78 | 5.17 | 6.78 ms |
| Construction, physics paused | 45 | 68.18 | 7.56 | 8.14 ms |
| Construction, physics running | 45 | 68.11 | 7.60 | 8.13 ms |

The paused/running frame-interval p95 values were 29.41/27.88 ms. Temperature
remained 42–43 C and all three post-fix windows included some power-save-level-1
samples. These short sequential warm-device windows show a modest improvement,
not a controlled thermal comparison or sustained 72 Hz acceptance. The workload
still misses the target and retains long frame intervals.

A separate 20-second post-fix profile no longer listed `RoomJournal.Snapshot`
among the entries above 1% of sampled UnityMain CPU. Android room-state publication
fell from about 6.45% to 4.16%; rendering remained prominent (about 42.09% cumulative
in `Camera::CustomRender`). These shares overlap with callers and do not establish
absolute savings in frame time. Rendering/material submission and the remaining
browser work need further measurement before changing their implementation.

Temporary content was again discarded and all original IDs checked. No synthetic
input or proximity override was used for this comparison. The app was stopped,
owned forwards removed, debug/sample-rate properties restored, and the two raw
device profiles removed after their local copies passed hash comparison.
`device-cleanup.json` records this cleanup. No release signing, deployment,
provider request, Store upload or saved-data reset occurred.


## Shared recipe materials — 2026-10-05

The preceding CPU sample identified rendering/material submission as a remaining
cost. Recipes previously created one material per part, including identically
painted copies of the same primitive. Recipe rendering now leases materials by
exact pigment, alternate pigment, pattern mode/plane/counts and cylinder mapping.
Repainting acquires a matching replacement for that owner; it never mutates a
material another object is using. Rebuilding/deleting releases each lease, and
the final owner releases the Unity material. Historical paint choices are not
retained in an unbounded cache. Geometry, joints, physics, serialized recipes and
human/agent actions remain unchanged.

The watercolor shader disables dynamic batching to retain the object coordinates
used by pigment and patterns. GPU instancing remains enabled; actual benefits
must be measured on the target hardware. Unity documents the coordinate-related
[dynamic-batching tag](https://docs.unity3d.com/6000.3/Documentation/Manual/SL-SubShaderTags.html#disablebatching-tag)
and the same-mesh/material requirement for
[GPU instancing](https://docs.unity3d.com/6000.3/Documentation/Manual/GPUInstancing.html).

Three focused PlayMode tests cover shared ownership through repaint/rebuild/
deletion, distinct pattern and cylinder settings, and actual rendered repaint
isolation and object-space pattern stability. The original implementation failed
the identical-material sharing assertion; the replacement passes all three.
The color readback assertion allows a one-millionth floating-point tolerance.
The three 256×128 rendered PNGs before painting, after painting and after moving
are byte-for-byte identical to the original implementation's corresponding
images. These small desktop images do not establish full Quest visual or
performance acceptance.

Both extracted helpers, `RecipeMaterials` and `RoomObjectObservation`, are now
included in the native exporter and web source-drift check: 300 source entries,
with unchanged 93 actions / 102 facts / 16 events. Local evidence is under
`.quest-evidence/material-sharing-20261005/`.

Full verification passed: 833 EditMode / 644 PlayMode tests (three optional
private-model skips), 129 focused web tests, 472 shared-client/native-room
observations and 81 original-book observations using scripted provider responses.
The production web build, 76 Android tests (two optional skips), lint and IL2CPP
build passed. The package audit matched all 3,015 frozen inputs, native sources
and metas, AAR, 147 web files, included avatar / 178 motions / 26 templates /
7 modules, ARM64 libraries, development manifest, v2 signature and 16 KiB alignment.

`MaestroQuest-recipe-materials-76F97F56.apk` is 188,167,871 bytes, SHA-256
`76F97F56A3EB76AC20A284AD3F0E2EE77DE8D406770D4910B9498967739C8124`.
It was installed in place, its device bytes matched, and the owner's authorized
adult confirmation was completed.

The same temporary 32-brick construction, chessboard and recipe robot was
created through the book's catalog controls. Each completed capture contains 45
one-second VrApi samples at 72 Hz, separate from profiler recording:

| Scene | Mean FPS | Mean stale count | Mean app GPU ms |
| --- | ---: | ---: | ---: |
| Original room | 71.49 | 5.82 | 6.75 |
| Construction, physics paused | 69.56 | 7.11 | 7.27 |
| Construction, scanned-room physics running | 70.51 | 6.40 | 7.12 |

The previous observation-only checkpoint measured 68.18 / 68.11 FPS and
8.14 / 8.13 ms app GPU time for the paused/running construction. Native
30-second frame-interval facts now report 2,065 / 2,085 samples, mean
14.53 / 14.39 ms, p95 28.52 / 27.74 ms and max 51.16 / 48.61 ms. This is a
modest sequential comparison, not a controlled causal or sustained thermal test:
temperature was 42–43 C, every capture contained four power-save-level-1 samples,
and a development build with Operator was used. The busy room still misses a
steady 72 FPS. Headset rendering was captured locally; human comfort and final
visual acceptance remain open.

One immediate scripted workshop close/reopen timed out at a disabled catalog
button before physics setup. The helper treated the still-closing workshop as
open and tried to click its pending controls. A later normal reopen succeeded;
scan load and Start physics completed before the physics measurement. The local
helper now waits for the native close acknowledgement (catalog controls detached)
before reopening. Five consecutive open/read-fact/close cycles passed on the same
installed app without product changes or bypassing native state guards. Cleanup
was rechecked afterward.

A separate 20-second CPU profile shows instanced rendering in use and rendering
still prominent (26.55% cumulative Camera::CustomRender, 4.70% ApplyMaterialPass).
The corresponding earlier shares were 42.09% / 10.66%, but overlapping call
shares cannot establish absolute frame-time savings. The new recording also
reported 16.03% cut/lost userspace samples, so it is diagnostic only. Matching
IL2CPP symbols and the record warning are retained with the local evidence.

The temporary scene was discarded, all three original saved object IDs were
checked, and physics/actions were stopped. Owned forwards and debug properties
were restored and the app stopped for charging. The raw device profile was
removed only after its local copy matched SHA-256. No synthetic input, proximity
override, saved-data reset, real provider request, release signing, deployment or
Store upload occurred. The ten unrelated dirty files retained their exact hashes.


## Workload animation correction — 2026-10-05

The restart checks below exposed a benchmark-helper assumption: the construction
helper requested `animation.play` with `seconds: 0` and `loop: true`. Zero selects
the saved clip duration (two seconds for the included robot); looping does not
make a one-off action indefinite. The earlier observation, shared-material and
receipt-publication comparisons therefore include the robot's geometry, but do
not establish performance with a continuously animating robot. Raw measurements
and package hashes remain unchanged. Their descriptions above/below are corrected;
a representative sustained animated-workload measurement remains open. The
runtime followed its existing duration contract. The restart test uses an explicit
30-second action and verifies actual playback immediately before force-stop.

## Receipt publication cost — 2026-10-05

The shared-material checkpoint's CPU profile still showed receipt observation
and JSON tree copying. Periodic receipt summaries cloned complete creation calls
and then removed their `call` field. The current candidate projects only the
summary's visible fields; the selected receipt still includes its exact call.
Live and restarted receipts retain detached resources, results and status.
Saved evidence, invocation IDs, recovery, replay refusal, publication cadence and
the human/agent contract do not change. Structured capture/catalog/execution
payloads are written directly to the JSON writer without cloning or reparenting
them into another JSON tree. Unity's existing vector/color layout and null fixes
remain in place.

Four focused EditMode cases pass: live and restarted hidden-recipe allocation,
selected/output detachment plus completion/restart, and structured-wire values,
nulls, escaping and parent ownership. In the four-creation receipt fixture,
Unity's GC.Alloc recorder measured these allocations per observation:

| Receipt source | 1-part calls, before | 32-part calls, before | 1-part calls, after | 32-part calls, after |
| --- | ---: | ---: | ---: | ---: |
| Restarted durable history | 2,737 | 47,401 | 236 | 236 |
| Live history plus a running wait | 2,839 | 47,503 | 478 | 478 |

These are allocation counts, not bytes or headset frame-time savings. Selected
calls and legitimate result payloads still require copying. The persisted receipt
assertion compares exact published JSON across restart; it does not equate CLR
float32 and parsed double object representations. The original implementation
failed both hidden-detail allocation assertions. Local evidence is in
`.quest-evidence/receipt-publication-20261005/`.

The 129 catalog/bridge/book web tests and source-drift check passed. Both receipt
helpers are now covered by the native exporter and web verifier (302 sources),
with unchanged 93 actions / 102 facts / 16 events. The full build passed **837
EditMode / 644 PlayMode tests** (three optional private-model skips), **474
native-room / 69 original-book observations** with offline scripted providers,
production web, Android lint and **76 Android tests** (two optional skips). The
final audit matched **3,017 frozen inputs**, native source/metas, AAR, 147 web
files, the included avatar / 178 motions / 26 templates / 7 modules, ARM64-only
libraries, development manifest, v2 signature and 16 KiB alignment.

Development APK `MaestroQuest-receipt-publication-6A7C780E.apk` is 188,184,431
bytes, SHA-256
`6A7C780E423DABD82244BB7EA4E350DA08C7EE1A45070FF09EB636A4E927FC0C`.
It was installed in place on Quest 3, the device's APK hash matched, and the
owner-authorized adult confirmation was completed.

The same temporary 32-brick construction, chessboard and recipe robot ran
through ordinary book catalog controls. Each capture lasted 45 seconds at 72 Hz;
VrApi emitted 44 original-room samples and 45 in each construction window:

| Scene | Mean FPS | Mean stale count | Mean app GPU ms |
| --- | ---: | ---: | ---: |
| Original room | 72.02 | 4.89 | 6.62 |
| Construction, physics paused | 71.18 | 5.91 | 6.93 |
| Construction, scanned-room physics running | 70.89 | 6.27 | 7.08 |

The preceding shared-material checkpoint measured 69.56 / 70.51 FPS for the
paused/running construction. Separate native 30-second frame-interval facts
reported 2,097 / 2,114 samples, mean 14.31 / 14.19 ms, p95 23.13 / 22.33 ms and
maximum 54.09 / 40.07 ms; preceding p95 values were 28.52 / 27.74 ms. These short
sequential windows do not establish causality, sustained performance or human
comfort. Temperature was 42–43 C, and each busy-room capture contained six
power-save-level-1 samples (none in the original-room capture). VrApi values are
one-second aggregates; a reported value above refresh is measurement variation,
not a higher headset refresh rate. The busy room still does not hold steady 72 FPS.

Temporary actions were stopped, physics paused and the test room discarded.
All three original object IDs were verified. The app was stopped, owned ADB
forwards removed and all three temporary debug properties matched their original
empty values. Battery was 34%, charging. No new CPU profile or visual/comfort
acceptance was taken, and no synthetic input, data reset, real provider call,
release signing, deployment or Store upload occurred. The ten unrelated dirty
files retained their exact hashes.


## Saved room and action history across restart — 2026-10-05

Installed package **6A7C780E** was unchanged; its device SHA-256 was rechecked.
The source commit `e07d6386` passed the [release gate](https://github.com/RONITERVO/MaestroTutor/actions/runs/37327155306).
No new product code or APK was needed for these checks. Private and external app
archives were made and hash-checked while the app was stopped; they remain local
under `.quest-evidence/device-durability-20261005/`.

The ordinary book workshop/catalog performed all room edits and inspections:

- A temporary room added the included 19-part robot and a 30 cm ink layer on an
  exact saved floor anchor. Two confirmed snapshots retained five three-point
  strokes. A sixth stroke and an additional block stayed temporary. An explicit
  30-second robot animation was verified playing immediately before force-stop.
- First restart: all five saved strokes retained exact IDs, points, colour and
  radius; the temporary block/stroke did not reload. The original creation
  receipt retained its exact call and object ID. The animation receipt reported
  `interrupted`, playback did not resume, and physics stayed paused. Loading the
  scan resolved the ink to its exact original room/anchor identity.
- A second temporary session saved the sixth stroke, then left a seventh stroke
  and a ball unsaved. Another verified active 30-second animation was interrupted
  by force-stop. Restart retained exactly six strokes and no temporary ball.
  Before room load the source remained intact while the layer reported hidden;
  after load its original anchor resolved and visibility returned. The pre-load
  summary is retained in the local tool evidence; a later verification retry
  overwrote the full pre-load JSON with the loaded state. No physical-alignment
  claim is derived from these readbacks.
- Test ink and robot were deleted through normal saved actions. A third restart
  confirmed their absence and both exact completed deletion receipts. The final
  `room.v20.json` matched the original backup **byte for byte**, including all
  five original definitions and structures. SHA-256:
  `1d1551b164ae44a1db16b81d0d8bd4ed7eee1187de65d45d65ee8d6668c25e93`.

Recent one-off history intentionally retains 16 terminal receipts. The first
creation receipt was verified after the first restart and later aged out during
further edits. The second cycle verified its newly interrupted receipt; the final
cycle verified newly completed deletions. History eviction is not saved-object
loss, and these checks do not promise unlimited action-history retention.

The harness also exposed two early workshop-open commands being cancelled while
native startup changed its room session. Explicit reopen after rebinding worked;
the final restart opened on its first attempt. The helper now allows a single
presentational reopen only after observing a changed session with no pending
request. It never retries a mutation. Early-open usability remains a follow-up;
the native session guard was not bypassed. Other harness corrections used the
public outgoing snapshot shape and opened the action-history tab before reading
receipts. The finite-clip assumption and performance wording are corrected above.

The app is stopped, its test forward removed and original debug properties
verified. Battery was 32%, charging, at cleanup. The ten unrelated working files
retain their exact hashes. This verifies completed saves, temporary-edit discard,
exact source/anchor recovery and interrupted action handling on Quest 3. It does
not cover killing an in-flight file publication, storage exhaustion, prolonged
write stress, real-provider parity, actual ink contact/alignment or user comfort.
No real provider call, paid generation, app-data reset, deployment, release signing
or Store submission occurred.


## Workshop opening during native startup — 2026-10-05

The book now retains an explicit workshop-open request while the initial native
maintenance session is replaced by the loaded room. Opening settles when the
native view is visible and contains the book. Only visibility intent crosses
that handoff: object edits retain the original room lease and are never replayed.
Conversation navigation, a later library choice, Back to chat and suspension
cancel the intent. Refusals/timeouts stop it; it is not a background retry loop.
A late open acknowledgement after conversation navigation is closed without
hiding chat again.

Three focused cases failed against the previous implementation. The correction
passes **225 Quest web tests in 24 files**, including 12 new navigation regressions,
plus TypeScript and focused lint. Full packaging passed **837 EditMode / 644
PlayMode tests** (three optional private-model skips), **475 native-room / 72
original-book observations** with scripted offline providers, production web,
Android lint and **76 Android tests** (two optional skips). The final audit matched
**3,018 frozen inputs**, native sources/metas, AAR, **147 web files**, the included
avatar / 178 motions / 26 templates / 7 modules, ARM64-only libraries, development
manifest, v2 signature and 16 KiB alignment. The catalog remains 93 actions / 102
facts / 16 events with 302 checked sources.

Development APK `MaestroQuest-workshop-startup-3FB9A8AF.apk` is 188,185,167
bytes, SHA-256
`3FB9A8AFC83FFF7E1951C46480EA2B603FD7890C990BA28B0640B99BA3A89FC9`.
It was installed in place on Quest 3 and its installed hash matched. The owner's
18+ confirmation was completed as authorized on each launch.

Three cold launches each received exactly **one** `workspace.open` command as
soon as the real book bridge became available, without the previous helper's
retry workaround or injected native state. All opened the five original objects
and returned to chat through the visible Back to chat control. The second run
observed a real native session replacement between its initial snapshot and the
ready workshop. The fourth launch immediately chose the latest conversation
page after opening: chat remained visible through the late acknowledgement and
for the 15-second observation window. A later, explicit open still worked.
Device WebView screenshots were inspected; no browser script errors occurred.
This resolves the early-opening follow-up recorded in the preceding section.

Private/external app backups and all local traces remain under
`.quest-evidence/workshop-startup-20261005/`. The original `room.v20.json` retained
all five definitions **byte for byte**, SHA-256
`1d1551b164ae44a1db16b81d0d8bd4ed7eee1187de65d45d65ee8d6668c25e93`.
The app is stopped, the owned test forward removed and original debug properties
restored. Battery was 34%, charging, at cleanup. All ten unrelated dirty files
retain their exact hashes.

These are real-device book/navigation checks, not a new physical-input, 3D visual,
comfort or sustained-performance acceptance. The previous 6A7C780E performance
and save-recovery measurements remain historical evidence for that checkpoint.
No real provider call, paid generation, app-data reset, deployment, release signing
or Store submission occurred. The other release gates remain open.

## Ten-minute animated workload and CPU trace — 2026-10-05

The installed **3FB9A8AF** development package ran a 600.61-second Quest 3
measurement at 72 Hz, with the original chat book, loaded scanned-room physics,
32 construction bricks on a chessboard, the 19-part recipe robot and the included
Maestro avatar. All additions and the avatar change were in explicit temporary
play. A saved program authored through the ordinary book editor ran two parallel
branches: the robot's recipe motion and exact included motion
`0cec277765100728f5433405ab3e0f7e` (Big_Wave_Hello). Each invoked 30-second looped
playback inside Forever; brief scheduler boundaries between invocations remain
possible. This corrects the finite two-second clip limitation of earlier windows.

Warm-up, midpoint and final observations showed both branches running, the robot
playing, physics running and the included avatar ready. Changing execution IDs
confirmed later cycles. The motion cache stayed at one clip / 76,540 curve values.
A virtual-only capture showed the waving avatar and construction scene; robot
occlusion and partial board cropping limit visual coverage. One normal book
inspection occurred during the timed window. No synthetic native state or
provider response was used for these device observations.

| Measurement | Observed |
| --- | --- |
| VrApi samples | 599 one-second samples; no process change or interruption |
| FPS | Mean **70.96**, minimum 66; configured refresh 72 Hz |
| Stale frames | Mean 5.46 per sample, p95 10, maximum 13 |
| App GPU time | Mean 7.04 ms, p95 7.75 ms, maximum 8.92 ms |
| Process PSS | 1,524,906 KB before / 1,519,098 KB after |
| Temperature / battery | 42–43 C / 34% to 32%, charging |
| Power level | Level 1 in eight samples; level 0 otherwise |
| Native final 30-second window | 2,103 intervals; mean 14.27 ms, p95 23.68 ms, maximum 48.34 ms |

Raw one-second FPS reached 75 because of sampling variation; refresh remained
72 Hz. PSS was approximately stable in this window, which is not a long-session
leak test. This warm, charging development run **does not pass steady 72 FPS**
and does not certify human comfort, tracked movement, provider usage or release
build performance. It is not a controlled comparison against earlier packages.

A separate 19.97-second `simpleperf` CPU trace followed the clean capture in the
same process: 64,704 samples, zero lost, 1,000 Hz, no Unity Deep Profiling.
WebView threads contributed substantial self samples: Chrome_InProcRe in
libmonochrome 19.90%, VizWebView 6.49%, Compositor 5.65%, and RenderThread in
libmonochrome 5.13%. These are sampled CPU-cycle shares, not frame times or
battery attribution. Matching IL2CPP symbols were verified by build ID
`2b1d00dd542d601ecf1b2238a85fcc2f1fba94a2`. Within UnityMain, inclusive samples
were 28.28% in Camera.CustomRender and 13.12% in RoomAgent.Update, including
4.93% in room-state serialization. Nested inclusive percentages must not be
added. OVRPlugin, vendor EGL and kernel symbol coverage remains incomplete.
The next investigation should isolate book rendering and state publication;
this trace does not by itself prove which change will improve frame pacing.

The QA behaviour was stopped and deleted through the book. The temporary room
was discarded through its catalog action after the Objects navigation defect
below was found. The original custom avatar was restored, physics paused and
`room.v20.json` remained byte-identical to the pre-test backup (SHA-256
`1d1551b164ae44a1db16b81d0d8bd4ed7eee1187de65d45d65ee8d6668c25e93`).
The app was stopped, the owned forward removed and all three original debug
properties verified. Local backups, raw metrics, program/readback evidence and
profile files remain in `.quest-evidence/animated-workload-20261005/`.

## Objects navigation after selection disappears — 2026-10-05

Returning from Behaviours used an empty native selected ID as the inspection
target, producing “Invalid room target.” The button now retains the selected
object only when it exists in the observed list, otherwise inspecting the first
available object. Missing and stale selections cannot strand the user in the
behaviour editor. Four regressions cover empty, removed, valid and absent IDs;
the empty and removed cases failed against the old implementation.

The catalog description for `room.sessionId` also now matches native behaviour:
an identity exists in saved mode, Begin/Discard replace it, and
`scene.temporaryRoom.active` identifies temporary mode. No session semantics or
storage format changed. Native export equality and source hashes were checked.

Validation passed **229 Quest web tests / 24 files**, TypeScript and focused lint;
**837 EditMode / 644 PlayMode** tests (three optional private-model skips);
**470 native-room / 74 original-book observations** with scripted offline providers;
production web, Android lint and **76 Android tests** (two optional skips).
The package audit matched 3,018 frozen inputs, native sources/metas, AAR,
147 web files and the included avatar / 178 motions / 26 templates / 7 modules.
The catalog remains 93 actions / 102 facts / 16 events and 302 checked sources.
ARM64-only libraries, development manifest, v2 signature and 16 KiB alignment passed.

`MaestroQuest-objects-navigation-CB3309AB.apk` is 188,185,287 bytes, SHA-256
`CB3309AB7DF600EC4BC5485AA074C44B43E23D781AF375819CFD15E6878ED1C3`. It was installed in place on Quest 3; the installed APK hash matched.
The authorized 18+ confirmation was completed. Real packaged book controls passed:

- Initial empty selection: Behaviours → Objects inspected the first saved object.
- Existing Maestro selection: the same transition retained Maestro.
- Selected temporary robot: after stopping its animation and discarding the fork,
  the transition inspected an existing saved object and showed the original five.

Discard first refused while the newly created robot was playing, as designed.
The test stopped it through the Animation tab and continued from the same process;
it did not restart the scenario or bypass the ownership guard. Back to chat passed,
no page errors occurred and the WebView screenshot was inspected. No native state
was injected. A saved-mode catalog read returned a nonempty session ID, confirming
the corrected description.

The original custom avatar and empty behaviour definitions were retained;
`room.v20.json` stayed byte-identical to its pre-test backup. All ten unrelated
working files retained their hashes. The app is stopped, the test forward removed
and original debug properties verified; battery was 33%, charging, at cleanup.
Evidence is in `.quest-evidence/animated-workload-20261005/`.

A read-only idle-book inventory on this package found an active globe canvas and
CSS animations, with no playing video. That is a follow-up clue, not an attribution
of the previous checkpoint's CPU cost. The ten-minute profile above remains tied
to **3FB9A8AF**; no performance improvement is claimed for this navigation update.
Physical-input/comfort, real providers, production access, signing and Store gates
remain open. No app-data reset, paid generation, deployment or Store submission occurred.


## Book hardware-buffer copies — 2026-10-05

The hardware-buffer renderer now copies only when the browser's SurfaceTexture
has a new frame, or once when a resized buffer needs the last latched image.
Idle render calls retain the existing page image. The callback and render path
share the renderer lock; callbacks from disabled/replaced surfaces are ignored.
The byte-buffer/PBO path keeps its continuous drain. The GLSurfaceView render
loop still runs continuously: this change avoids duplicate GPU copies, not
render-thread wakeups. Browser capture rate, texture resolution, globe and CSS
animations are unchanged. Android's [SurfaceTexture contract](https://developer.android.com/reference/android/graphics/SurfaceTexture)
requires updateTexImage on the owning GL thread; the callback only marks a frame
pending, and the GL render callback performs the update/copy.

A development-only Operator tool, `maestro_book_rendering`, reports received-frame,
render-call and copy counters. Its session-only `continuous` / `newFrames` modes
permit comparison in one APK; `observe` changes nothing. The Unity registration
is excluded from production, Android rejects non-debuggable applications, and
this JNI method is not exposed to page JavaScript.

Six new Android regressions cover idle retention, coalesced producer bursts,
resizing with/before a valid frame, stale callbacks after disable/recreation and
continuous/PBO compatibility. Full verification passed **837 EditMode / 644
PlayMode tests** (three optional private-model skips), **472 native-room / 71
original-book observations** with offline scripted providers, production web,
Android lint and **82 Android tests** (two optional skips). The package audit
matched **3,021 frozen inputs**, native source/metas, AAR, **147 web files**, the
included avatar / 178 motions / 26 templates / 7 modules, ARM64-only libraries,
development manifest, v2 signature and 16 KiB alignment. The catalog remains
93 actions / 102 facts / 16 events with 302 checked sources.

`MaestroQuest-frame-copy-496EC9BB.apk` is **188,178,035 bytes**, SHA-256
`496EC9BBD0272B54A1DE1512823B67C0087FBF899EA6B4D8BEE12E1B16AA0462`.
It was installed in place on Quest 3 and the installed hash matched. The owner's
authorized 18+ confirmation and actual book navigation passed with no page errors.
Stereo device screenshots were inspected: the 3D book changed from original chat
to the workshop on both pages. At the workshop readback, cumulative counters were
5,536 render calls, 2,252 received frames and 2,251 copies, with valid content.
One pending producer frame can separate those last two counters. This confirms
the copy gate is operating, not a frame-rate or energy improvement.

An Android Home key attempt exited the original process cleanly (EXIT_SELF,
status 0); relaunch restored the book. A subsequent sleep/wake retained process
12338. The first Operator callback timed out while Unity was paused; returning
the activity to the foreground recovered its counters and ordinary book
navigation. However, Quest displayed **Finding position in room** over the app.
The same-process browser recovery is therefore not a complete 3D resume or
tracking acceptance. The owner has been asked to restore tracking. No same-build
FPS comparison or new ten-minute workload result is claimed for this package.

The prepared temporary board, robot and 32 bricks were discarded through the
book before timed animation/physics capture began. The original five objects and
custom avatar were restored. Both `room.v20.json` and `behaviours.v2.json` remained
byte-identical to the pre-install backup; the room hash remains
`1d1551b164ae44a1db16b81d0d8bd4ed7eee1187de65d45d65ee8d6668c25e93`.
The app was stopped, both owned forwards removed and all original debug
properties verified. Battery was 34%, charging, at 45 C at cleanup. Local backups,
screenshots, package audits and the prepared comparison helpers are in
`.quest-evidence/book-frame-copy-20261005/`; private room imagery is not committed.
No real provider call, paid generation, data reset, deployment, release signing
or Store submission occurred. Sustained performance and the other release gates
remain open.


## Reproducible process-termination storage probe

`unity/Tools/Test-QuestStorageCrash.ps1` exercises the native paired room/memory
transaction used when keeping a temporary room. It runs only in a dedicated
Unity Editor build mirror, with fresh synthetic data under
`.quest-evidence/storage-crash/<run-id>/`. It does not read the owner's saved room,
start a provider, connect to a headset or compile into the Quest player.

After syncing and verifying the mirror with `unity/Tools/Verify-Quest.ps1`,
run with PowerShell 7:

```powershell
./unity/Tools/Test-QuestStorageCrash.ps1 `
  -Editor 'D:/Tools/Unity/6000.3.24f1/Editor/Unity.exe' `
  -BuildMirror 'D:/Projects/Builds/MaestroQuestVerify'
```

The writer stops at an existing transaction milestone and publishes a flushed
readiness record. The orchestrator verifies the child PID, start time and case
identity before forcibly terminating that process with
[Process.Kill](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.kill),
then waits for its exit. A new Editor process invokes
the production recovery implementation and checks the exact room and memory
bytes, expected retained backups, journal retirement and a second idempotent
startup. Normal verifier exit is also required. Sources must match the owned
mirror before and after the run; all logs and interrupted files are retained.

The 13 cases cover seven replacement milestones, four first-save milestones and
two interrupted recoveries. They distinguish uncommitted rollback from committed
roll-forward. The two recovery cases kill a second process after its first
recovery step, so a retry must safely finish from partially recovered files.
This differs from throwing a test exception: no transaction finally or graceful
shutdown executes in a killed writer. The probe remains a desktop filesystem
check at transaction boundaries. It does not kill during a byte write/fsync,
simulate physical power loss or full storage, or prove Android filesystem/device
behaviour. Those release checks remain separate.

### Execution evidence — 2026-10-05

All **13 cases passed**, with **15 forced terminations** (13 writers and two
recoveries), under Unity **6000.3.24f1 / WindowsEditor**. Every verifier exited
normally. The seven replacement cases covered before-journal, prepared, room,
memory, committed, room-backup and memory-backup. First-save cases covered
prepared, room, memory and committed. The recovery-interruption cases covered
both rollback and roll-forward, stopping after recovered-room.

An independent read of the retained files checked the numeric room and memory
values, the expected backup values or absence, all case identities, forced exits,
retired journals and **1,165 matching C#/meta/assembly-definition source files**.
The first-save cases verified that an uncommitted initial save leaves both
primaries absent. No mixed old/new pair remained after recovery. The complete
run is `.quest-evidence/storage-crash/92375f94ff2d4f0fb775679ec87c4ded/`;
its independent audit is `.quest-evidence/storage-crash-20261005/crash-audit.json`.
An initial probe run stopped on its own typed-string-null versus JSON-null
comparison; the files were correctly absent. Only the probe comparison was
corrected, and the complete matrix above ran fresh. The failed evidence remains
in `27207da2d23146568a7b098214d56875` rather than being overwritten.

During this work the existing verification passed **837 EditMode / 644 PlayMode
tests** (three optional private-model skips), **474 native-room / 81 original-book
observations** with offline scripted provider responses. The final probe compiled
in its fresh batch processes. No runtime/storage-format change or new APK was
needed; the installed **496EC9BB** development package remains the preceding
checkpoint. All ten unrelated dirty files retained their exact hashes.

A separate planned Quest staging-refusal test did **not** execute: launch never
reached a browser socket, no app process remained on inspection, and Guardian's
room-tracking dialog was visible. No staging obstruction was created and no test
edit was sent. The app remained stopped, test properties/forwards were restored,
and the saved room stayed byte-identical. Local backups and prepared helpers are
in `.quest-evidence/storage-refusal-20261005/`. Android interrupted-write,
low-storage and prolonged-save stress, physical tracking/resume and the
performance comparison remain open. No provider call, deployment, signing or
Store submission occurred.


## Android process-termination storage probe — 2026-10-05

The paired room/memory transaction now has **13 passing cases / 15 forced
terminations on Quest 3**, using **Unity 6000.3.24f1 IL2CPP / Android**. This is
additional platform evidence from a separate diagnostic package; it does not
replace the installed Maestro application or establish full in-app recovery UX.
No production runtime source or saved-data format changed.

The diagnostic compiles the unchanged production runtime plus a small runner
copied only into a receipt-owned build mirror. All **709 runtime files** matched
before/after packaging and in an independent audit. Templates live outside Unity's
source Assets tree; the build removes its injected files and scene and restores
backed-up project/XR settings. The diagnostic uses its own synthetic data under
`com.maestro.quest.storageprobe`, an empty scene, disabled XR startup, and no
network, camera, microphone or scene permissions. Platform access remains disabled.

The APK manifest/ARM64/signature audit passed before installation; the installed
APK hash matched. Diagnostic APK: **145,289,960 bytes**, SHA-256
`7217A1B14DCEE9B768006185F22A55E388F13B7DB9F8412033490B5F5BD6BE8F`.
It has no VR launch category or required VR feature. The SDK's optional experimental
feature declaration remains, but no tracking override or Operator command is used.
The main app remained stopped throughout.

Seven replacement cases stop before-journal, prepared, room, memory, committed,
room-backup and memory-backup. Four first-save cases stop at prepared, room, memory
and committed. Two more stop recovery itself after recovered-room, once for
rollback and once for roll-forward. The host verifies the intermediate pair and
journal, checks the live PID and package identity, sends SIGKILL as the diagnostic
UID, and confirms the process is absent before relaunching. It never retries a
still-running case merely because an observation timed out.

Each fresh verifier runs production recovery, checks exact room/memory bytes and
retained backups, retires the journal and repeats Capture to verify idempotence.
The host separately reads the archived files and checks both exact bytes and
numeric room/memory values. All 13 scenarios and 15 distinct killed PIDs were
independently audited. There was no mixed pair after recovery; interrupted first
saves correctly left both primaries absent. Verification completion is recorded
before stopping the diagnostic; a normal Android process exit is not claimed.

Evidence:

- Build/audit: `.quest-evidence/android-storage-build/08dd693c38474e56b1f5ad8979d8e253/`.
- Complete device matrix: `.quest-evidence/android-storage/eef3fd7f2f53485293e06f787ec360b7/`.
- Independent audit, original-app identities and archived synthetic files:
  `.quest-evidence/android-storage-20261005/`.

An earlier candidate was refused by the manifest audit before installation.
An initial host polling attempt then stopped before reaching a save because
`adb exec-out` did not propagate a missing-file exit status. Switching JSON reads
to the shell protocol fixed the runner; the complete matrix above ran fresh.
Failed build/attempt evidence was retained. No production storage defect was
observed in these attempts.

The original **496EC9BB** Maestro APK and saved room remained byte-identical.
After archiving all synthetic evidence, the owned diagnostic package was removed.
The headset still needs tracking recovery before the regular VR/performance tests.
This covers Android termination at transaction milestones; it does **not** cover
interruption during a byte write/fsync, physical power loss, full disk, prolonged
write stress, or the normal book's error/recovery interaction.

### Reproduce the isolated Android check

First run the normal `Verify-Quest.ps1` to synchronize and verify the owned mirror.
Then use PowerShell 7 and Python 3:

```powershell
./unity/Tools/Build-QuestAndroidStorageProbe.ps1 `
  -Editor 'D:/Tools/Unity/6000.3.24f1/Editor/Unity.exe' `
  -BuildMirror 'D:/Projects/Builds/MaestroQuestVerify' `
  -AndroidSdk 'C:/Users/ronit/AppData/Local/Android/Sdk' `
  -AndroidJdk 'C:/Program Files/Eclipse Adoptium/jdk-17.0.16.8-hotspot'

./unity/Tools/Audit-QuestAndroidStorageProbe.ps1 `
  -Receipt '<reported-evidence-directory>/build-receipt.json' `
  -AndroidSdk 'C:/Users/ronit/AppData/Local/Android/Sdk' `
  -AndroidJdk 'C:/Program Files/Eclipse Adoptium/jdk-17.0.16.8-hotspot'

python ./unity/Tools/test-quest-android-storage.py `
  --adb 'C:/Users/ronit/AppData/Local/Android/Sdk/platform-tools/adb.exe' `
  --serial '<connected-Quest-serial>' `
  --audit '<reported-evidence-directory>/audit.json'
```

The runner uses ADB port **5041**, installs only the audited diagnostic identity,
refuses to replace a different installed diagnostic APK, retains every case and
stops only its own package. It leaves that package stopped for inspection/repeat
runs; after retaining evidence it may be removed with `adb -P 5041 -s <serial>
uninstall com.maestro.quest.storageprobe`. Never use the production package name
in that cleanup command. No paid/provider call, release signing, deployment or
Store submission is part of this procedure.


## Normal book save refusal and retry — 2026-10-05

After the isolated diagnostic was removed, the Guardian tracking dialog was no
longer visible. The unchanged **496EC9BB** Maestro APK relaunched with PID 27459;
the authorized adult confirmation and chat/workshop navigation passed. An actual
stereo screenshot showed passthrough, the avatar and book without the tracking
warning. This supersedes the preceding tracking hold for further automation; it
does not establish human tracking comfort or the earlier sleep/wake acceptance.

The normal book's shared `object.create` path then handled an unavailable save
staging path on Quest. The test created an owned empty directory at
`room.v20.json.pending`, attempted one named QA block through the action catalog,
and removed that directory immediately afterwards. The action became `failed`
with the user-facing message that the previous save was retained and storage
should be checked before retrying. The failed object was absent and the five-object
room remained byte-identical.

A fresh explicit creation after removing the obstruction completed and saved its
exact returned object ID, producing six objects. Deleting only that QA object
through the same catalog restored the original five-object room byte-for-byte
(SHA-256 `1d1551b164ae44a1db16b81d0d8bd4ed7eee1187de65d45d65ee8d6668c25e93`).
The failed/create/delete receipts and ordinary save/Undo history are expected test
activity; this does not claim that all history/backup files stayed unchanged.

An observation helper initially used `innerText` on the collapsed exact-call
field and failed after the actual action had already failed correctly. The
existing receipt was then read with `textContent`; the failed creation was not
replayed. Refusal/retry/removal screenshots, exact calls, file captures and
`completed.json` are retained in `.quest-evidence/storage-refusal-20261005/`.
Relaunch/stereo evidence is in `.quest-evidence/quest-resume-20261005/`.

The owned obstruction was removed, the main app stopped, diagnostic forwards
removed and original test properties verified. This is an on-device failed-save
and explicit-retry check. It is **not** a full-disk simulation, paired-memory Keep
failure, a mid-write kill or power-loss test. No provider was used and no runtime
or installed APK changed. Sustained performance and other release gates remain.


## Same-package book-copy comparison — 2026-10-05

The unchanged **496EC9BB** development APK ran in one Quest process (PID 28896)
with normal tracking restored. A temporary room contained 32 bricks on a board,
the 19-part recipe robot, the included Maestro and loaded scanned-room physics.
The saved QA program cycled two parallel 30-second looped animations inside
Forever, using exact included motion `0cec277765100728f5433405ab3e0f7e` for Maestro.
Before and after readbacks confirmed both branches, robot playback and physics
running; the motion cache remained one clip / 76,540 curve values. Scheduler
boundaries between invocations remain possible.

Only the development copy-mode switch changed between windows. Each had ten
seconds of warm-up; the app, view, workload, texture size and capture cadence
were retained. No profiling or UI navigation occurred during the timed windows.
Battery/memory were read once per minute; renderer counters bracketed each
window, with their own slightly longer timestamp interval. Sequential windows
have idle gaps and are not a randomized thermal experiment.

| Window | Seconds / VrApi samples | Copies/s | Mean FPS at 72 Hz | App GPU mean / p95 ms | Stale mean / p95 |
| --- | --- | --- | --- | --- | --- |
| Continuous A | 120.38 / 120 | 71.82 | 70.72 | 7.10 / 7.96 | 5.44 / 10 |
| New frames B | 120.37 / 120 | 28.02 | 70.81 | 6.51 / 7.12 | 5.41 / 9 |
| Continuous C | 120.35 / 120 | 71.86 | 70.80 | 7.03 / 7.66 | 5.69 / 10 |
| New frames D | 600.35 / 600 | 28.47 | 70.90 | 6.55 / 7.25 | 5.73 / 9 |

The two new-frame windows copied exactly once per received producer frame,
reducing copies by about 60%. Their mean app GPU time was roughly 0.5 ms lower
than either adjacent continuous-copy baseline. Mean FPS changed little. This
supports reduced GPU copying in this setup; it does **not** demonstrate steady
72 FPS, an energy saving, or release performance. The GL draw loop still ran
about 71.8 times per second, with 28.0–29.0 received browser frames per second.

The ten-minute window had 600 samples, mean **70.90 FPS**, minimum 65, maximum
75 (one-second sampling variation; refresh stayed 72 Hz). GPU time peaked at
7.92 ms; stale frames averaged 5.73, with a maximum of 13. Reported CPU utilization
averaged 0.978. Power level remained zero; temperature was 42–43 C and battery
fell from 23% to 21% while charging. Process PSS was 1,429,846 KB before and
1,438,822 KB after. This short window is not a long-session leak test.

The headset rested at a fixed tilted view with inactive hands/controllers; no
synthetic input, pose or proximity override was applied. Stereo screenshots
showed the book and animated characters without a tracking warning. The board
and brick assembly were partly occluded by the book/trays. The chat page showed
the initial API-key screen, not a live provider conversation, streaming artifacts
or microphone use. These limits prevent treating the run as human movement,
comfort or representative provider acceptance.

A **separate** browser profile followed all timed windows. Over 15.94 seconds,
CDP reported 15.59 seconds of task duration, 0.36 of script execution, 5.17 of
layout and 5.75 of style recalculation (305 layouts / 1,525 style recalculations).
Its 1,405 samples mostly fell in the generic `(program)` category, so the profile
does not identify an individual native browser function. The animation snapshot
listed twelve running decorative CSS/SVG animations and one finished animation.
This is evidence to investigate layout/style invalidation and rendering next,
not proof that removing any particular animation will fix frame pacing. Profiler
and page-inspection overhead are excluded from the comparison above.

Two setup/inspection helpers initially timed out by retaining the outgoing hidden
panel's Back to chat button during navigation. The actual program remained
running. Scoping the helper to the now-visible Objects panel resolved this; the
final readback/navigation passed without any product change or workload restart.

Cleanup stopped/deleted only the QA behaviour, paused physics and discarded the
temporary room through ordinary book controls. The original avatar selection
was preserved; `room.v20.json` and `behaviours.v2.json` matched the pre-launch backup byte-for-byte.
Their SHA-256 values are respectively
`1d1551b164ae44a1db16b81d0d8bd4ed7eee1187de65d45d65ee8d6668c25e93` and
`3fa8b960dca6286231e161c600a7c1b7c8f099ccda5b7a2182e388695e19173d`.
Receipts/Undo history remain expected test activity. New-frame mode remained
selected; the app was stopped at 20% battery, owned forwards removed and all
three original debug properties verified. The ten unrelated dirty files retained
their original hashes. No source, installed APK, provider or production service
changed. Backups, raw logs, counters, profile, images and verified cleanup remain
in `.quest-evidence/book-copy-comparison-20261005/`.


## Quest chat layout feedback fix — 2026-10-06

Device profiling of the preceding **496EC9BB** build narrowed repeated style
invalidation to the chat scroller: its automatic scrollbar width interacted
with animated content and container-relative text sizing. Trace events included
scrollbar changes and repeated language-text style invalidations. Pausing SVG
animation removed the layout work; removing container sizing alone did not.
The final change keeps all animation and reserves the chat scrollbar gutter,
scoped to `.quest-book-surface [data-quest-chat-scroll]`.

A reversible, metrics-only experiment in the same Android WebView process
compared five-second windows, with all page animations running:

| Chat scroller | Task seconds | Layout seconds | Style seconds | Layouts / style recalculations |
| --- | --- | --- | --- | --- |
| Original | 4.922 | 1.623 | 1.953 | 112 / 784 |
| Stable gutter | 1.958 | 0.182 | 0.234 | 361 / 361 |
| Original restored | 4.899 | 1.648 | 1.905 | 125 / 875 |

These browser counters establish substantially less layout/style work in this
screen, not compositor performance. Repeated style recalculations fell from
seven per layout to one, while more layout passes completed in the fixed interval. The broader trace/desktop
experiments are separate from these metrics-only windows. All temporary styles
and animation overrides were removed before the diagnostic process stopped.

For appearance comparison only, animations were held at the same phase. Before
and after PNG bytes were identical (SHA-256
`ac3b22c0ed1818b82dcf0058c7b2b8133deea9fa0db13802af0b4d3624ab66f2`).
Chat width remained 402 CSS pixels, height 614, content height 680 and language
font 16 px. This verifies the tested initial screen; it is not universal image
parity. Actual wheel scrolling, no horizontal overflow, earlier-page navigation
and bookmark return passed with seeded chat/artifacts at 1024 x 768, 819 x 614
and 800 x 600. The built-app adult-entry checks also passed.

The full candidate pipeline passed **837 EditMode / 644 PlayMode tests** with
three optional private-model skips, **475 native-room observations** and
**73 original-book observations**, production web compilation, Android lint and
**82 Android tests** with two optional skips. Both integration journeys used
scripted responses without real providers. The package audit matched **3,028
frozen inputs**, native source/metas, AAR, all 147 packaged web files, the included
avatar, 178 motions, 26 templates and 7 modules. ARM64-only libraries, development
manifest, APK v2 signature and 16 KiB alignment passed.

`MaestroQuest-book-layout-FD9AEBDE.apk` is **188,178,107 bytes**, SHA-256
`FD9AEBDE5B88A78142542F57AFEED998F485EAC4966D10D1FEE0584E2729FD69`.
It was installed in place after fresh private/external backups; installed bytes
matched. The owner-authorized adult confirmation, book navigation and avatar
read passed without browser errors. The installed computed gutter is `stable`
and the chat dimensions above are retained. A stereo screenshot showed the live
book, included Maestro and recipe robot without a tracking warning.


### Candidate workload and cleanup

The candidate ran in one Quest process (PID 6644) with the same kind of temporary
workload: 32 bricks, a board, the 19-part robot, included Maestro and scanned-room
physics. A Forever program cycled parallel 30-second robot/Maestro motions,
retaining the exact included Maestro motion ID used above. Both branches, robot
playback and physics were active before and after; the motion cache stayed at
one clip / 76,540 curve values. Invocation boundaries remain possible.

The requested ten-minute window **stopped at the battery limit after 300.31
seconds / 300 VrApi samples**. Battery fell from 17% to 14% despite external
power. Temperature was 42–43 C and reported power level ranged from 0 to 1.
No browser profiling, inspection or UI navigation occurred during this window.
Minute battery/memory checks and bracketing renderer counters were retained.

| Metric | Result |
| --- | --- |
| FPS at 72 Hz | Mean 70.81; minimum 68; maximum 74 (sampling variation) |
| App GPU time | Mean 6.81 ms; p95 7.35 ms; maximum 7.85 ms |
| Stale frames | Mean 6.09; p95 8; maximum 12 |
| Reported CPU utilization | Mean 0.917 |
| Browser copies / draw calls per second | 29.57 / 71.98 |
| Process PSS, before / after | 1,577,377 / 1,553,102 KB |

This is a shortened development diagnostic, **not** a completed ten-minute run,
steady 72 FPS pass, leak test or controlled old/new APK comparison. The view
remained fixed and tilted, hands/controllers inactive, with the initial API-key
screen and partly occluded construction. No synthetic pose/input or real provider
was used. The browser layout improvement is established separately above; an
end-to-end frame-rate improvement is not established here.

Final readback confirmed an unpaused, focused app and valid book content with
new-frame copying retained. Stereo screenshots showed the live book and changed
character poses without a tracking warning. Ordinary book controls stopped and
deleted only the QA behaviour, paused physics and discarded the temporary room.
The saved original custom avatar selection was preserved. `room.v20.json` and
`behaviours.v2.json` matched the fresh backup byte-for-byte, with the same hashes
recorded in the preceding comparison. The ten unrelated dirty files retained
their original hashes. Test receipts/Undo history remain expected activity.
The app was stopped at 14%, owned forwards removed and all three original debug
properties verified. No data reset, provider call or production change occurred.

Package/diagnostic evidence is in `.quest-evidence/book-layout-20261005/`;
backups, raw device logs, exact counters, images and verified cleanup are in
`.quest-evidence/book-layout-performance-20261005/`. Sustained performance,
human/provider and Store gates remain open.


## Optional physical tool trays — 2026-10-06

The seven authoring trays now start hidden, leaving the book, Maestro and existing
creations visible. The permanent 3D Workshop blocks beside the book still open
manual authoring without an AI provider. Its Physical tools entry uses the same
`room.tools.set` action/fact as the agent and event programs. Hiding trays leaves
underlying drawing, imports, animations, behaviours and physics running.

The development candidate is `MaestroQuest-optional-tools-C43EBC56.apk` (**188,198,291 bytes**),
SHA-256 `C43EBC566BF86F98135EB2497379CCC84A877C6925488FC6ADD8756200DA2B17`.
It has not been installed or measured on Quest; the previous **FD9AEBDE** package
and saved room remain untouched while the owner fast-charges the headset.

Validation for these exact packaged inputs:

- 837 EditMode and 647 PlayMode tests passed; three optional external-model/motion
  probes skipped. All 94 targeted workspace tests also passed. New cases cover
  hidden defaults, exact receipt replay, continuing room work, actual XRI grip
  ownership, atomic hide-all refusal, Recall, workspace holds and reopening labels.
- 475 real native-room observations and 79 original-book observations passed with
  scripted offline provider responses. The book opened Creation through its actual
  generated form and native receipt, then hid all tools; its saved scene revision
  was unchanged. Existing create/edit/Undo and chat handoff still passed.
- 105 focused web tests across six files, application/probe type checks, targeted
  lint, production web compilation, Android lint and 82 Android tests passed;
  two optional Android tests skipped.
- The audit matched all 3,034 frozen inputs, 349 runtime / 218 test / 16 Editor
  C# files and metas, 66 fixture payloads, 147 packaged web files, the native AAR,
  included avatar, 178 motions, 26 templates and seven modules. Catalog counts
  are 94 actions / 103 facts / 16 events / 311 source identities.
- ARM64-only libraries, development identity, v2 signature and 16 KiB alignment
  passed. All ten unrelated dirty files retained their original hashes.

Evidence remains local in `.quest-evidence/optional-tools-20261006/`; the fresh
native room and book runs are `7cafce87dce942efba7875e19fcb84df` and
`cb7b859ff9e04eb491004272a688dae3`. No real provider, deployment, release signing
or Store upload occurred. Headset grip/hand input, readability and sustained
performance still need acceptance on this candidate. A source-level reduction
in visible tool geometry/label updates does not establish a frame-rate gain.

## Manual book action forms — 2026-10-06

The optional action editor now starts with editable settings expanded. Internal
read-only guards are grouped in Current room references; JSON arguments and the
full capability reference stay collapsed below the normal Check/Run controls.
Loading current values, snapshot guards, permanent-action confirmation and native
execution remain unchanged. The original chat remains the main interface.

The development candidate is `MaestroQuest-book-action-forms-A8D296E4.apk` (**188,198,583 bytes**),
SHA-256 `A8D296E40395E889FE86497EF739E6C8D9DF6F696ECD971970E13C60F6E7F63D`. It includes the previous optional physical tool trays.
The initial package audit preceded installation. The subsequent installation and
limited development performance result are recorded below.

Verification of these exact packaged inputs:

- 837 EditMode / 647 PlayMode tests passed, with three optional private-file skips.
- 471 native-room and 82 original-book observations passed with scripted
  offline provider responses. The book journey checks normal fields, collapsed
  source/reference sections, read-only guards and no commands on inspection. At
  1,024 by 768 and 819 by 614 pixels, the tray selector and Run button are visible
  and at least 44 pixels high; the form has no horizontal overflow. Existing tool visibility,
  create/edit/Undo, chat handoff and no-replay checks also passed.
- 66 focused web tests, application/probe type checks, targeted lint, production
  web compilation, Android lint and 82 Android tests passed; two optional Android
  tests skipped.
- The audit matched all 3,034 frozen inputs, native sources/metas, AAR,
  147 web files, the included avatar, 178 motions, 26 templates and seven modules.
  ARM64-only libraries, development manifest, v2 signature and 16 KiB alignment
  passed. All ten unrelated dirty files retained their hashes.

Local evidence: `.quest-evidence/book-action-forms-20261006/`. Fresh room/book run
IDs: `9c3f8eae889c431ab0e0fbed6c43ab87` / `a442e62e9a2c42cf900d499ed4a926c5`. Desktop viewport checks
do not establish headset readability or physical input acceptance. Real-provider,
sustained performance, storage-stress and Store release gates remain open.

### On-device acceptance and ten-minute measurement

The same **A8D296E4** APK was subsequently installed on the reconnected Quest 3
without resetting app data. Installed bytes matched the audited package. The
book verified hidden defaults, Creation alone, all seven trays shown, then all
hidden; its normal fields and read-only references worked. Room snapshots were
recorded for each visibility state. The permanent 3D Workshop blocks were then
activated through actual controller trigger input and opened the normal book
workspace with every tray hidden. All simulated inputs were released. Human
comfort and readability still require acceptance.

A temporary room used the existing 32-brick construction, board, 19-part recipe
robot, included Maestro and scanned-room physics workload. Parallel program
branches looped both animations; native facts and live traces verified both were
active before and after the capture. Results from 600.32 seconds
and 600 VrApi samples:

- FPS mean **71.20 at 72 Hz**, range 68–74.
- App GPU mean/p95/max **5.93/6.40/6.83 ms**.
- Stale-frame counter mean/p95/max **6.13/8/11**; CPU utilization mean **0.842**.
- Browser texture copies **29.89/s**, render callbacks **72.00/s**.
- PSS **1,704,506 → 1,677,746 KB**; battery **90% → 85%**, VrApi
  temperature range **40–42°C**, power-level counter **0**. No early stop.

This is a development diagnostic with a fixed tilted headset view, inactive
controller/hand inputs, the initial API-key dialog in the book and partially
visible/overlapping workload geometry. The room-view and final stereo images
retain those limits. It is not a controlled comparison with FD9AEBDE and does
not establish a frame-rate gain from hiding trays, human comfort or sustained
72 FPS acceptance. No real provider call was made.

The QA behaviour was stopped and deleted through the normal book; the temporary
room was discarded. The original room and behaviour files matched the fresh
pre-install backup byte-for-byte, including the saved custom-avatar selection.
The app was stopped; original debug properties and empty ADB forwards were
verified restored. Battery was 83% after the remaining checks. Evidence is in
the same local checkpoint directory, including `metrics-summary.json`,
`device-tools.json`, `workshop-controller-verified.json`, saved-file verification
and cleanup receipts.

## Animated book icons — 2026-10-06

The shared Globe and Target icons animate CSS boxes containing static SVG layers;
the fixed-colour palette is a self-contained SVG image. Geometry, timing, draw
order, inherited theme/hover colours and toolbar sizes remain the same. This
avoids animating inner SVG geometry in the surrounding document's layout tree.
A narrow-phone check caught an image max-width regression before packaging;
the palette now retains the former SVG's minimum toolbar footprint.

Development package `MaestroQuest-animated-icons-D522191F.apk` is **188,198,759
bytes**, SHA-256
`D522191F126BD8F1D6D09958F0637780367C30C9E57C8DEDA02669FD78A96ED3`.
It was installed on Quest 3 with exact package bytes verified and app data retained.

Verification passed: 837 EditMode / 647 PlayMode tests (three optional private-file
skips), 471 native-room and 91 original-book state observations with scripted
offline provider responses, 39 session web tests, TypeScript, targeted lint and
core boundaries. Production web, Android lint, 82 Android tests (two optional
skips), source/content audit, ARM64-only libraries, development signature and
16 KiB alignment passed. The audit matches 3,037 source inputs, native sources
and metas, AAR, all 147 web files, included avatar, 178 motions, 26 templates and
seven modules. The phone correction changed only two web files before the
original-book journey and web compilation; native inputs remained identical.

Browser comparisons covered four icon sizes, three opaque/translucent/accent
colours and four animation positions. Small antialiasing differences remain; this
is not a pixel-identical raster claim. Dynamic colour changes, event bubbling,
image decoding and 48 running test-instance animations passed. The isolated
icons produced zero layouts in a three-second desktop sample. Actual app toolbar
sizes passed at 320/390-pixel phone widths and 819/1024-pixel book widths.

The installed normal book, with the initial API-key dialog and original saved
room, produced **zero layouts in 15.19 seconds**. The preceding package produced
1,086 in 15.29 seconds. Browser task time was 5.62 seconds versus 6.96; script time
was 0.165 versus 0.191; style work was 0.707 versus 0.787. These separate samples
are diagnostic and do not establish a controlled whole-app frame-rate gain.
The first candidate profile ran on the entry page because its helper checked
readiness too early; that result was preserved separately and excluded.

Local evidence: `.quest-evidence/book-render-profile-20261006/`.

The installed package completed a **300.30-second** development run with 300
VrApi samples: 32 bricks, a board, 19-part robot, included Maestro and scanned-room
physics. Both animation branches and physics were verified active before and
after the capture. FPS mean was **71.43 at 72 Hz**, range 66–74. App GPU
mean/p95/max was **6.14/6.64/7.48 ms**; stale-frame mean/p95/max was
**5.90/8/13**; CPU utilization mean was **0.811**. Texture copies were
**29.89/s**, with **71.98/s** renderer callbacks. PSS rose from 1,565,599 to
1,593,340 KB; battery fell from 67% to 66%; VrApi temperature was 42–43°C and
power-level counter stayed zero. There was no early stop.

As before, this used a fixed tilted headset view, inactive controller/hand input,
the initial API-key dialog and partly visible/overlapping geometry. It does not
establish sustained 72 FPS, human comfort, real-provider performance, or a
controlled FPS gain over A8D296E4. A helper initially raced an unfinished
workspace close; the explicit read-only verification succeeded after navigation
settled, before the timed run began.

The QA behaviour was stopped and deleted through the book and the temporary room
was discarded. Original room/behaviour bytes, including the custom-avatar choice,
matched the fresh backup. All ten unrelated dirty files retained their hashes.
The app is stopped, inputs inactive, debug properties restored and forwards clear;
final battery was 65%. No real provider, paid generation, deployment, release key
use or Store submission occurred. Release gates remain open.

## Android partial-write and ENOSPC probe — 2026-10-06

The separate `com.maestro.quest.storageprobe` diagnostic passed **11 cases** on
Quest 3: four forced terminations during native writes, six injected native
`ENOSPC` failures, and 256 consecutive paired saves. The production
`RoomSnapshotTransaction` and other runtime sources were unchanged: the build
receipt verifies all **713 runtime files** against the source checkout and mirror.

The diagnostic APK is **182,291,295 bytes**, SHA-256
`19255B1403F5B1085915CF8CB1D5E809F412593D3992DDDD04707CB5066EF7EB`.
It is ARM64, debug-signed, and has no network, microphone, camera, scene, anchor
or hand-tracking permissions and no required VR feature/category. This package
does not replace the installed Maestro app.

A diagnostic-only native library intercepts writes to one explicitly armed,
synthetic staging path. The Android
[debug-app wrapper mechanism](https://developer.android.com/ndk/guides/wrap-script)
loads it before Unity. Production C# files are not patched. The library writes
seven actual bytes before either waiting for the host's SIGKILL or returning a
short write followed by ENOSPC. The host checks the target file length and exact
diagnostic PID before terminating it. No headset storage was filled.

| Case | Verified outcome |
| --- | --- |
| Interrupted prepared journal, room, memory, or commit-journal write | Four real SIGKILLs after seven bytes. Primaries/backups match their expected intermediate state. Repeated startup refuses the incomplete staging and leaves every workspace file byte-identical for explicit recovery. |
| ENOSPC during prepared journal, room, memory, or commit-journal write | Native error reaches a managed IOException. Recovery selects the complete earlier pair and preserves its expected backups. |
| ENOSPC during room-backup or memory-backup write | The durable committed journal recovers the complete new pair and both correct previous backups. |
| 256 consecutive saves | Each publication and capture matches the requested pair; every previous backup is checked. Final values are independently read from the device archive. Completed in **17.56 seconds**, with no leftover staging or journal files. |

The host independently audits the retained device archives, including native-hit
records, saved bytes, journal state, numeric room/memory values, backup identities
and repeated-recovery results. Test book positions wrap within the existing room
bounds while the saved counter continues increasing. Expected backups retain
their original revision IDs instead of regenerating a superficially equal document.

Build/source/manifest/signature evidence:
`.quest-evidence/android-storage-build/b5c9ede7c2ae449eae135488c5051824/`.
Complete accepted run:
`.quest-evidence/android-storage/daf5bc3539c54ffdba23c46d2b9c17e8/`.
Independent before/after archive and cleanup:
`.quest-evidence/android-byte-storage-20261006/`.

Earlier diagnostic runs exposed helper defects: Quest's descriptor path carried
a " (deleted)" annotation despite the virtual staging entry remaining present;
a marker's mode prevented the host reading it; and a test regenerated a random
memory revision for its backup comparison. Those runs were excluded. Their
available evidence is retained; the unreadable marker is explicitly recorded as
missing from that failed run's partial archive. One cancelled packaging attempt
was also excluded. The accepted run uses fresh case IDs and one audited APK.

Reproduce using the existing diagnostic build and audit commands above, then:

```powershell
python unity/Tools/test-quest-android-storage.py --adb '<adb.exe>' --serial '<Quest serial>' --audit '<build evidence>/audit.json' --byte-faults
```

`--smoke` runs only the first partial-write case and marks its receipt incomplete
for matrix coverage. Without `--byte-faults`, the earlier transaction-boundary
matrix remains available. Evidence is retained; the runner stops only its
diagnostic package. Archive the cases and verify the installed diagnostic hash
before uninstalling that package.

The build checks source hashes, restores mirror settings, and removes its
injected C# and native source files. Normal development/release packaging now
rejects a native startup wrapper or this fault library before any release signing.
That guard accepted the installed D522191F package and rejected this diagnostic.

After the accepted run, all 11 cases were archived and the diagnostic was
uninstalled. The main D522191F package and **all 200 external saved files** match
the fresh baseline; the main app remains stopped. Debug properties are empty and
ADB forwards clear. Final battery was 61%, charging; battery temperature was 44°C.

These are bounded synthetic tests of the paired snapshot writer, not actual
filesystem exhaustion, power-loss/fsync durability, every possible byte offset,
ordinary unpaired saves, all book recovery UX or prolonged save stress. The
17.56-second sequence does not close the prolonged-use gate. Partial staging is
preserved and refused, not silently repaired. Human/provider, performance and
Store acceptance gates remain open.

## Controller bucket-to-cup pour — 2026-10-06

The installed **D522191F** APK hash matched the audited animated-icons checkpoint.
Fresh stopped-app private/external backups preceded this test. Through the
original book's shared catalog, a temporary room created the ordinary Bucket
and Cup templates, configured the bucket with **200 mL** and made the cup fixed
with its existing 500 mL capacity. The existing scan loaded before physics started.
The fixed receiver isolates pouring from movement; this is not a freehand
two-vessel or real tabletop alignment claim.

Meta XR Operator supplied actual OpenXR Touch Plus grip/pose input. Native
selection stayed on the bucket at all **23 samples** from upright to 110 degrees,
with maximum observed root-position error **0.0731 mm** against the requested
path. The path accounted for the moving lip and bounded gravity trajectory to
keep the stream over the cup. After returning upright and letting flow settle,
the bucket's live and saved quantities were **0 mL** and the cup's were **200 mL**,
with idle flow state, no reported error and zero measured quantity difference.
A virtual-room capture was inspected for the vessel geometry; its view does not
prove interior fill-level readability or human comfort.

Ordinary book Undo first reversed the separate grip-placement edit without
changing contents. The next Undo reversed the single liquid episode, restoring
**200 / 0 mL**. Redo restored **0 / 200 mL**. This verifies one atomic liquid edit
for source and receiver; it does not imply that moving an object and pouring
share one history entry. No special quantity-transfer command simulated the pour.

The first controller path was excluded from the all-collected result: it aimed
outside the cup opening during the early tilt, and only 58.03397065985879 mL was
received. That trial, its poses and its exact Undo are retained, not discarded as
an application failure. The corrected path produced the complete transfer without
changing app code, container geometry or physics parameters.

Cleanup discarded the temporary room through the book and restored input. The
app is stopped, the three debug properties match their initial values, and the
owned ADB forwards are removed. Of 200 original external files, **199 match
byte-for-byte**, including the room, behaviours and asset files. The sole changed
file is the ordinary bounded action-receipt history: 12 matching completed QA
commands and four unchanged prior entries. No extra external file remained.
All ten unrelated checkout edits retained their hashes. Final battery was 55%,
charging, at 45°C.

Evidence is ignored locally under `.quest-evidence/container-pour-20261006/`:
`verified.json`, native input/pose journals, book quantity observations, exact
backup/after archives and cleanup receipts. This is automated device evidence,
not hand-tracking ergonomics, overflow/multiple-receiver acceptance, event-delivery
assertions, restart during flow or sustained performance. No provider, paid
asset generation, deployment, new APK installation, release signing or Store
submission occurred. Those release gates remain open.

## Held pencil, brush and eraser — 2026-10-06

On the installed **D522191F** development APK, a temporary chalkboard and ordinary
Pencil, Paint brush and Eraser templates were exercised through XR Operator's
actual Touch Plus grip bindings. The tray drawing mode was off. Loose pencil
contact produced zero strokes; holding and moving it produced 19- and seven-point
strokes with radius 0.002 and the template colour. The brush produced a 17-point
blue stroke with radius 0.01. Saved surface facts confirm those independent values.

One held eraser sweep removed the two contacted strokes, preserving the distant
pencil stroke. The first Undo reversed the eraser's separate placement change;
the next Undo restored both original stroke IDs, widths and point counts. Redo
removed those same two strokes. Capture returned idle without retained errors.
The virtual-room render visibly contains the restored marks; this is not a human
assessment of contact feel, readability or comfort.

Ordinary book controls discarded the temporary room and restored the preceding
drawing settings. All 198 remaining external files, including saved room and
behaviour content, match the fresh backup. The two changed files are the bounded
receipt history (nine known completed QA commands, seven prior receipts retained)
and the SDK's refreshed MRUK world-lock anchor cache. Input overrides were
released; app, debug properties and forwards were restored. All ten unrelated
working-tree files remain byte-identical. No provider call or package installation
occurred. Evidence: `.quest-evidence/held-drawing-20261006/verified.json`, input
journals, book fact readbacks, virtual render, before/after archives and cleanup.

This covers automated controller grip on a planar board, not tracked-hand comfort,
curved/scanned/animated surfaces, restart/save-failure or maximum-load performance.
It does not test the uploaded Store candidate.

## Saved learner lesson, scanned floor — 2026-10-07

Installed development APK **D85F3CB7** from `f58dab48` after independent package
checks and verified private/external backups. Reload preserved the saved lesson
and all room objects. Ordinary book composition and Meta XR Operator Send input
submitted two natural BYOK learner follow-ups; no object IDs or capability
instructions were supplied in their wording.

The first performed two queries then failed on the provider SDK's truncated SSE
frame. Developer inspection of the app's Traffic Log found the exact error;
that diagnostic access is not physical input acceptance. The second loaded
the real room and read its FLOOR anchor but did not finish placement before
testing was stopped. All nine saved objects retained their prior state, including
the apple on the table; there is no floor-placement receipt.

Battery readings changed from 58% / 42°C to 8% / 56°C with charging no longer
reported. The app was force-stopped and the headset put to sleep. Owned forwards
were removed and no Live audio remained active. Cooling and normal charging
above 40% are required before resuming. These readings do not identify the heat
source or establish a performance pass; sustained thermal/power testing remains
a release gate. Do not silently replay the interrupted task after reconnecting.

Evidence is private under `.quest-evidence/scan-placement/`: package audit,
preinstall backups, turn summaries and response diagnostics, and the stopped
room/action-receipt files. Physical floor placement and the complete novice
lesson remain unfinished. The shared stream-recovery change is desktop-only
until explicitly included in a later development build.

## Mouth-positioned speech and acoustics — pending device acceptance

The continuous native renderer and Meta HRTF have desktop rendered-audio evidence,
but are not in installed APK D85F3CB7. Resume only after the owner confirms cooling
and normal charging, then inspect actual battery/temperature and the interrupted
task without replaying it. Record the exact APK/source, output device (built-in
speakers or headphones), provider route and timestamps for each result.

1. Play ordinary tutor TTS, a completed room-agent reply, a saved Maestro replay
   and a Live reply. Each must sound once at the visible animated head/mouth,
   with no second copy at the book or fixed to the listener. User recordings and
   artifact music retain their existing destinations. Exercise managed access
   when available and BYOK; report unavailable managed access as a coverage gap.
2. Turn the head through left/right and up/down while Maestro stays still, then
   walk around the avatar and move/scale it. Direction and distance must follow
   the rendered world and remain intelligible nearby. Repeat after selecting an
   imported avatar with a different head rig; assess the mouth offset visually.
3. Listen through a long streamed answer and delayed packets. Check for seams,
   repeats, missing endings and stale audio after silence. Correlate hearing with
   submitted/played receipts; a receipt alone does not prove audible completion.
4. Stop during streaming and cached replay, then start a fresh answer. Repeat
   across app focus loss, pause/resume and output-device changes. Old PCM must
   not resume and the native voice must not be duplicated by browser fallback.
5. In full Live and observer mode, let native speech finish before speaking a
   fresh sentence. Maestro's output must not become a new user transcript or
   room request. Verify the fresh onset survives after the settling interval.
   The current native playback gate suppresses simultaneous speech; it is not
   full-duplex echo cancellation. Measure that conversational limitation.
6. Compare an unobstructed voice with a scanned wall and a moved/deleted virtual
   wall. The first direct-obstruction implementation uses approximate hard-surface
   materials and retains some speech audibility; reflections/material authoring
   remain pending. Verify cleanup, tracking loss and workspace replacement.
   Inspect `runtime.acoustics` for readiness, uploaded geometry and omissions.
   With tracking available, stable room boundaries should calculate a map after
   two seconds. Move an animated robot or dynamic prop: it should retain direct
   obstruction without repeatedly rebuilding that map. Move, grab or delete a
   fixed creation, or remove/reload the scan: stale map data must be discarded
   and the settled scene recalculated. Check `map.geometry`/`map.omitted` separately
   from direct geometry counts. Move beyond the current listener region, then
   return; reuse is bounded to four points. Measure computation cost on Quest;
   the one-second worker limit is cooperative, not a hard wall-clock guarantee.
   Toggling physics or visual opacity must not
   silently change acoustic participation. Dynamic real objects absent from the
   acoustic geometry require separate coverage; visual depth occlusion does not
   establish audio occlusion.
7. Measure the final reflection/reverb tail and microphone suppression after
   speech and Stop. The current 500 ms input-settling interval has no measured
   room-reverb guarantee. Complete a sustained thermal/frame/audio-underrun run
   on the accepted build; short muted desktop tests do not close that gate.
8. General world audio must use the same definitions and capabilities for user
   controls and agent actions. Test a reusable clip, event-triggered effect and
   continuous live source attached to separate objects. Start and stop each
   independently; deletion or cancellation must not clear another source's
   playback or shared reflection tail. Verify saved definitions and transient
   stream ownership separately.
9. Keep a radio or ambient loop playing throughout a real Live turn, then add an
   event sound while Maestro speaks. The user must still be able to speak after
   the reply, with no permanent suppression or invented transcript from the
   app's own audio. Verify managed and BYOK with headset speakers and headphones.
   The experimental final-mix monitor does not distinguish conversation tails
   from ongoing world audio and is not enabled in the production scene. See the
   [audio implementation and acceptance contract](../unity/AUDIO.md).

World-audio desktop checkpoint (2026-10-07): reusable tone definitions and
root/part/joint emitters now use the shared catalog, room journal and temporary
workspace. The native test mix verifies consumed PCM, independent cancellation,
attachment movement and exact source revisions during edits. The headless room
client creates a source, configures an emitter, waits for its native completion
receipt, and removes both. These checks use synthetic audio and muted output;
they do not establish physical audibility or microphone coexistence. Imported
clips, live sources, continuous playback and construction-module audio packaging
remain open. Installed APK D85F3CB7 is unchanged.

Continuous-audio desktop checkpoint (2026-10-07): room-owned loops now have exact
instance controls and retained lifecycle events. A failure-first native test
confirmed that pausing AudioSource alone did not stop procedural PCM consumption;
the transport now preserves queued audio and its cursor during pause. Focused
rendered tests verify pause/resume, transient gain, independent Stop, cancellation
before handoff, ordered events and explicit missed-history failure. This is muted
desktop evidence. On a cooled, charged headset, still verify a looping sound with
another effect, movement, speech, focus/device interruption and the real Live
microphone. Imported/live source adapters and mixed-audio capture remain open.

The complete desktop run passed 865 EditMode / 694 PlayMode tests (three optional
private-file skips), 186 shared editor/catalog tests and both native-room and
original-book integrations. The new headless audio journey confirms actual PCM
consumption, same-instance controls, discovery and delivery of the pause event
to a running saved program. It used no real AI provider or headset.
