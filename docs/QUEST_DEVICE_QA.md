# Quest 3 development verification — 2026-09-30



## Physical drawing obstruction (headset acceptance pending)

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


## Planar ink and chalkboard — pending Quest 3 acceptance

The surface-drawing increment is desktop-tested separately. Device work remains
on hold; these checks are not reported as completed:

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
