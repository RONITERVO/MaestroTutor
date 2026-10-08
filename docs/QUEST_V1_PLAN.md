# Maestro Quest 1.0 delivery record

Status: active implementation. Nothing in this document claims store readiness.

The current installed development checkpoint is **D85F3CB7**. Headset work is
paused until the owner confirms cooling and charging; mouth-positioned speech and
native room acoustics have not yet been installed for physical acceptance.
The latest packaged development checkpoint **9F8A2625 / 1f972b6f** has local
packaging evidence only; it is not installed and does not contain the later
appearance, portable-construction, backdrop or mixed-reality movement changes.

Current desktop source supports saved per-object real-room participation and
swept world movement at any backdrop opacity. See the [current movement contract
and evidence](#swept-world-travel-in-mixed-reality-2026-10-08). These changes are
not physical Quest acceptance.
Saved visual layers now use the same native catalog and named resource forms for
users and agents. Transient viewing controls extend that foundation; see
[saved layers](#saved-visual-layers-and-shared-authoring--2026-10-08) and
[temporary layer presentation](#temporary-layer-presentation--2026-10-08).
Imported surroundings, camera choices and editable passthrough windows remain open.

User and agent authored audio is accepted v1 scope: reusable sources, object or
joint emitters, live streams, and existing events/programs controlling playback.
See the [shared world audio contract](../unity/AUDIO.md). Native rendering now
handles procedural world sounds with editable emitters, looping playback,
independent controls and events alongside the separate Maestro speech renderer.
Import/stream adapters and continuous-background-audio/Live coexistence remain
implementation gates.
The experimental reflection mixer stays out of the production scene until the
capture policy handles a mixed scene without indefinitely suppressing the user.

The owner's authored-world clarification adds shared appearance/texture/opacity,
height-changing terrain, weather, lighting and interactive water to the delivery
direction. Larger user-built settlements must use bounded simulation and streamed
detail with persistent identities, rather than making every object permanently
active. The bounded increments and outstanding requirements are recorded below;
this scope is not a claim that every feature is implemented or device-certified.
They extend the spatial-state refactor below; later image-texture import
and generation must use the original app's asset/provider flows.

The earlier development checkpoint **D522191F** retained the
original phone/book controls while isolating their animated icon rendering.
The installed normal-book profile produced zero layouts in 15.19 seconds, versus
1,086 in the preceding sample. Its five-minute animated 32-brick development run
averaged 71.43 FPS at 72 Hz; sustained performance remains open. See
[the exact package, device evidence and limits](QUEST_DEVICE_QA.md#animated-book-icons--2026-10-06).
The app is stopped, inputs/properties/forwards are restored, and saved room and
behaviour files match the fresh backup byte-for-byte.

The preceding A8D296E4 checkpoint passed native book controls and actual controller
activation of the permanent 3D Workshop blocks. Its ten-minute animated run
averaged 71.20 FPS. These separate runs are not a controlled frame-rate comparison.

On the same installed package, an actual controller-gripped bucket transferred
200 mL to a fixed cup; both accepted quantities and atomic liquid Undo/Redo match.
Temporary content was discarded. Original room/behaviour bytes are unchanged;
the normal bounded receipt history records the QA commands. Hand pouring,
overflow, event and broader interaction acceptance remain open. See
[the device result and limits](QUEST_DEVICE_QA.md#controller-bucket-to-cup-pour--2026-10-06).

The dedicated release key has a verified encrypted USB copy and a completed
owner-confirmed paper-password backup; the retyped password unlocked the exact
key and passed signature verification. A dedicated Quest Firebase registration is
now prepared in the original project, with no alternate providers or debug tokens.
Verified shared public settings and read-back endpoint URLs now pass strict
release-profile validation. The approved rollout deployed two disabled Quest
endpoints, verified runtime non-token signing, and removed its private diagnostic.
All 14 disabled-operation requests pass; the original ten functions and broader
IAM are unchanged. Quest changes to the original API, browser approval, ingress
trust, real Meta/Firebase
provider acceptance and Store work remain open. See
[the scoped rollout](QUEST_MANAGED_ACCESS.md#scoped-backend-rollout--2026-10-06)
and [signing recovery](QUEST_RELEASE_BUILD.md#dedicated-release-key-prepared--2026-10-06).

The corrected signed release candidate is **DA689683**, non-debuggable
`com.maestro.quest`, Quest 3 only, ARM64/16 KiB. Its approved private draft upload
created build **1767008267909255**, version 1.0.0/build 1. Owner dashboard screenshots
confirm both Basic malware and Security Vulnerability Review checks passed.
Virtual Reality Checks await submission/review; the Store submission is still a
draft with no binary attached. The owner's later dashboard screenshots confirm
this build is current in private Alpha. Positive Store entitlement and
account/provider acceptance remain open. See
[the exact package and checks](QUEST_RELEASE_BUILD.md#corrected-signed-quest-3-candidate--2026-10-06).

The installed development app also passed a held pencil/brush/eraser check on a
planar board, including loose-tool inactivity and swept erasure Undo/Redo. Its
temporary room was discarded and saved content verified unchanged. See
[the bounded device evidence](QUEST_DEVICE_QA.md#held-pencil-brush-and-eraser--2026-10-06).

A separate Android diagnostic now passes four partial-write terminations, six
injected ENOSPC recoveries and 256 consecutive saves using unchanged production
runtime sources. It has been removed; the installed D522191F package and all
200 external saved files match the fresh baseline. This does not close actual
full-disk, power-loss, prolonged-save or book recovery UX acceptance. See
[the bounded evidence and reproduction steps](QUEST_DEVICE_QA.md#android-partial-write-and-enospc-probe--2026-10-06).

The preceding FD9AEBDE checkpoint fixed Quest chat scrollbar layout feedback.
Its animated development run stopped at the battery limit after five minutes,
averaging 70.81 FPS at 72 Hz. Different charge, view and workload conditions mean
the latest run does not establish a controlled improvement over that measurement.

The preceding 496EC9BB same-package comparison reduced texture copies by about
60% and mean GPU time by roughly 0.5 ms; its ten-minute run averaged 70.90 FPS.
Normal book staging-refusal/explicit-retry passed. A separate, removed Android
diagnostic verifies 13 paired-storage transaction-boundary crash cases with 15
forced terminations using unchanged production IL2CPP code and synthetic data.
Those earlier boundary-only checks do not establish full-disk, mid-byte-write
or all book recovery UX; the later byte-write diagnostic has its own scope above.

An earlier ten-minute workload on 3FB9A8AF kept included Maestro and recipe-robot
animations cycling while scanned-room physics ran with 32 construction bricks.
It averaged 70.96 FPS at 72 Hz; sustained performance remains an open release
gate. The separate CPU trace points to book rendering and state publication as
investigation targets. See device QA for the exact workload and measurement limits.

Earlier checkpoint 6A7C780E also avoids copying hidden recipe arguments into
periodic receipt summaries. Its busy-room development measurement was 71.18 FPS
paused / 70.89 FPS with physics at 72 Hz; sustained performance acceptance remains
open. Those windows include robot geometry but do not establish continuously
animated workload performance (the helper requested its two-second clip). Three
restarts on that package verified completed saves, temporary-edit discard, exact
ink/anchor recovery and interrupted actions staying stopped. Mid-write/low-disk
stress remains open. The checkpoint narratives below retain their historical scope;
they are not new acceptance claims.

On-device verification resumed on 2026-10-05. Controller tests exposed and fixed
book-page/tray-button colliders blocking their movable owner. Development build
`FD2C7B15` is installed with 831 EditMode / 636 PlayMode tests, both native
integration journeys and Android/web/package checks passed. Both page grips,
tray grip, release, Recall and page/physical-button clicks passed on Quest 3.
The same device testing exposed a full submerged-bucket spill/refill loop. That
shared-simulation fix is now installed as `0EC7EC29`, with 831 EditMode / 637
PlayMode tests and full integration/web/Android/package checks passed. Actual
controller dipping gained exactly 2,000 mL while the pool lost 2,000 mL, remaining
stable underwater. The same APK now has automated hand-input evidence: book
pinch/move/release, palm Recall, a 44-point chalkboard stroke, physical Undo/Redo,
blocked drawing/erasing and restoration. Book focus requested the Quest IME and
Android key events reached the field. Human tracking/comfort, virtual-key selection,
full pouring, sustained performance and provider acceptance remain open. Original
content/input were restored. See [current device QA](QUEST_DEVICE_QA.md).

Saved scanned ink layers now extend the existing Drawing object and Canvas ink
component. The shared catalog, generated book fields, agent and physical drawing
tools use the same persisted source and Undo. Creation selects an exact loaded
plane or the viewer's current gaze; explicit rebind preserves the ink. Missing,
wrong-room or unfit anchors hide the layer without deleting it, and interrupted
strokes remain for Retry/Discard. No panel, loose-object movement, automatic
relocation, scan startup or provider client is added. Room v20 and paired
intent/archive v19 preserve exact bindings. See
[the ink contract](QUEST_SURFACE_DRAWING.md#saved-scanned-ink-layers-2026-10-05).
Verified development checkpoint: **831 EditMode / 634 PlayMode** tests (three
optional private-model skips), **474 native-room / 77 original-book observations**
with offline scripted providers, production web build, Android lint and **76 Android
tests** (two optional skips). Shared-client fixture/current-input checks, app/driver
types and lint passed. The audit matched 2,998 frozen inputs, 147 packaged web files,
all included content, ARM64 libraries, the development manifest, v2 signature and
16 KiB alignment. APK: `MaestroQuest-scanned-ink-45468330.apk` (188,156,179 bytes), SHA-256
`454683300229BAA24AB9F8210308AC46B44270AD9DA0A905462966E91EAED9C8`. It includes scan-layout inspection and saved ink,
and was **installed on Quest 3 on 2026-10-05** after app-data backup. Device work
has resumed. Book-to-native temporary creation, animation, scan inspection and
anchored-ink Undo/Redo now have on-device evidence in [device QA](QUEST_DEVICE_QA.md).
Earlier hold/installation notes below describe their respective checkpoints. CI evidence is linked
from draft PR #248; these offline checks are not real-provider or Store acceptance.

Loaded scan layout also has shared on-demand exact surface IDs, semantic labels,
room-local poses and anchor-local plane/box bounds. Changed or unavailable scans
refuse stale detail reads. Native outlines are bounded and used locally for layer
fit; they are not sent through the layout facts. Prepared privacy copy describes
explicit layout inspection and saved layer identities. See
[the scan contract](QUEST_ROOM_ENVIRONMENT.md#shared-scanned-layout-inspection-2026-10-04).

Rectangular liquid cavities and an editable Shallow pool now extend the shared
container model. Ordinary buckets and cups can dip and pour back; the catalog,
book form, agent, saved quantities and Undo remain shared. Current-record inputs
load shape and contents together and expand into explicit typed program bindings.
Room v19 and paired intent/archive v18 protect the new shape data. See
[the contract](QUEST_CONTAINERS.md#rectangular-cavities-and-shallow-pools-2026-10-04).
This is a bounded authored reservoir, not room flooding or a general fluid solver;
Quest interaction and sustained performance still require device acceptance.

The release audit separates ready source from outstanding device/provider/account
acceptance in [release packaging](QUEST_RELEASE_BUILD.md#release-audit-2026-10-04).
Quest checkout is now refused by the shared client service and managed HTTP route,
using the verified Quest Firebase app identity, in addition to the existing hidden
UI. Shared balances and original web checkout remain available. The client change
is included in the shallow-pool development APK; the backend is still undeployed. The Store purchase
model still needs a decision; the existing-service policy's interactivity limit
makes an assumed exception inappropriate. See [managed access](QUEST_MANAGED_ACCESS.md).

Physical hand/controller packing now adapts the existing measured-material kernel:
one contact previews one ball, lift/release saves it with the source change, and
one Undo restores both. Shared settings, capture/recovery facts, pinch-grabbing
and a solid Pack ball tray control use the same native path. Desktop native,
shared-client and browser verification are recorded in
[material packing](QUEST_MATERIAL_PACKING.md#physical-hand-and-controller-packing).
Real Quest interaction/performance acceptance remains open.

The native room and real-book integration drivers now share a strict TypeScript
gate in CI and before a native journey starts. Catalog replies must match the
actual request. Joining and snap Undo verify native placement facts, fixing two
old checks that compared absent rotation fields. See
[typed integration evidence](QUEST_NATIVE_ROOM_PROBE.md#typed-integration-drivers-and-complete-placement-evidence-2026-10-04).

Physical material tools now adapt the shared field/store transfer kernel. The
editable Material scoop takes one bounded dose on contact and deposits it when
inverted, with a carried preview, one atomic save/Undo and explicit retained-draft
recovery. The same saved tip is configurable from the book or agent. Device
acceptance remains open; see
[physical material tools](QUEST_MATERIAL_PACKING.md#physical-material-tools).

The original chat/Live handoff and result guidance now defer supported room work
to the native catalog, including existing model/motion imports. System file choice
and permissions remain explicit user steps. Planner reads and actions use their
own allowances within the existing nine-call ceiling, so discovery can lead to
edits and started actions can still be inspected. See
[catalog-based workflow](QUEST_UNIFIED_AGENT.md#catalog-based-handoff-narration-and-independent-allowances-2026-10-04).

The live book now has a required desktop integration journey against the actual
Unity app. It exercises the original chat's verified handoff, native creation,
concurrent human editing, stale-agent refusal, Undo, native image delivery and
reload without replay. Provider responses are explicitly scripted offline; Chrome
and the local server are disposable and owned by verification. This closes a gap
between separate browser replays and native CLI checks, while real-provider,
Android book texture/input and headset acceptance remain open. See
[the live-book boundary](QUEST_NATIVE_ROOM_PROBE.md#live-book-and-original-chat-journey-2026-10-04).

Native physical catching passed desktop verification through the shared capability path.
It includes bounded contact capture, avatar arm reaching, root/recipe sockets and
a typed caught event; see [physical catching](QUEST_PHYSICAL_CATCHING.md).
Desktop synthetic-floor acceptance does not close the pending headset gates.

Physical surface drawing now checks solid obstructions in the shared contact path
for trigger/pinch, erasing and held drawing tips. Scanned walls block ink without
changing tool recovery through walls. Logical ink editing remains available to
users, programs and the agent. The bounded query and real-physics tests are
specified in QUEST_SURFACE_DRAWING.md; device acceptance remains pending.

Editable lathe profiles now extend the existing recipe system. The agent, programs
and optional book workshop use the same create/edit capabilities; exact revisioned
profile queries expose the source. Native geometry, saved edits, Undo, mesh disposal
and admission limits are covered by native tests and the full-app journey. Cup,
plate and pawn renders and the Chrome workshop replay were inspected. This is a
geometry increment: assemblies use a box by default. Independent editable collision
recipes now add box/sphere/cylinder/ring compounds with shared book/agent/program
authoring, bounded cost and save/Undo. A native physics test exercises an open cup
catching a ball and a compound cup landing on a floor. See QUEST_COLLISION_AUTHORING.md.
Physical connections now share hinge/fixed/slider definitions, break limits, native facts
and a typed break event; see QUEST_PHYSICAL_CONNECTIONS.md. Connected blueprints
and construction capture preserve all three kinds. An editable spring-button module
uses ordinary recipes and condition waits for press/release. Saved snap points now
support explicit construction placement or fixed joining with shared current
revisions and one Undo; see QUEST_SNAP_POINTS.md. Physical grip snap previews
and bounded liquid containers/pouring/vessel scooping now extend those components. Sculptable snow fields, measured snowball packing and 25 editable templates plus
seven reusable modules now cover part of the proposed kit. Persistent liquid fields and complete device acceptance remain unfinished.
See QUEST_RECIPE_AUTHORING.md and QUEST_NATIVE_ROOM_PROBE.md.

The October 2 notebook scope and current review assessment are recorded in
[expandable world authoring](QUEST_WORLD_AUTHORING.md). Editable native recipes,
reusable object components and the existing shared event-program runtime remain
the direction. The implemented kit, geometry, assemblies, surface ink, physical joints, containers
and snow fields are documented below; further component coverage and device
acceptance remain open.
No generation credits were spent for this assessment.

The real full-app Editor room now has an isolated development file transport for
the exact shared book/agent client. The required native verification journey creates
an object, paints it, undoes both edits and reads native diagnostics. It exposed and
fixed a too-small client feature-inventory bound and JsonUtility expanding absent
program memory into an invalid unscoped object. See QUEST_NATIVE_ROOM_PROBE.md.
This verifies native command integration; provider, Android browser and headset
acceptance remain separate gates.

The native runtime now exposes bounded frame-interval, imported-model budget
and current-room motion-cache facts through the shared catalog. Book, agent
and stored programs use those same observations; no extra page controls or
separate telemetry service is introduced. Lifecycle resets prevent background
time becoming a frame stall. Counts describe resource reservations, not actual
RAM/VRAM, and desktop measurements do not satisfy headset performance gates.
See `QUEST_DEVICE_QA.md` for sampling semantics and device acceptance steps.

Rejected or cancelled custom-avatar candidates now explicitly dispose their
imported resources and release reservations, even when their Unity objects
were never active. Repeated incompatible-avatar selections no longer consume
the model budget. Disposal is idempotent and disposed instances cannot reload.

Program compatibility checks now distinguish executable blocks/expressions from
literal records. Book and agent requests share the same traversal as the module
linker and editor; data fields named `op`, `fact`, `memoryVersion` or similar no
longer create false native-feature requirements. Real blocks in imports and
schema-declared module payloads still require their advertised features.

Remembered behaviour variables now share the native interpreter, versioned syntax,
book blocks, agent observations and `program.memory.edit` action. Explicit
checkpoints, stopped-only set/reset and complete workspace archives replace the
previous internal-only storage foundation; per-run state remains the default.
See QUEST_PROGRAM_MEMORY.md for the execution, recovery and temporary-room limits.

The chosen paid-plan Meshy export is now packaged as the offline default avatar,
with exact saved identity, an explicit Walking clip and portable recovery/backup
content. The older Meshy motion collection uses a different rig and stays out of
the default library. Direct animation ZIP import now uses the existing shared
batch controls. ZIP model search and preview also share physical/book/agent
selection and explicit acceptance. The current-rig offline motion collection is
packaged with shared installation, stable identities and portable recovery. Fresh
rooms now save five exact editable tutor-state choices, selected after native
previews of 16 candidates. Existing assignments and cleared roles remain intact;
see QUEST_AVATAR_ACTIVITIES.md. Broader visual/semantic curation, continuous
playback comfort and motion-fidelity acceptance remain unfinished;
see QUEST_INCLUDED_AVATAR.md, QUEST_MODEL_IMPORT.md and QUEST_MOTION_BATCH_IMPORT.md.

Current animation authoring lets agent/program calls and optional typed book
fields edit the same pose and keyframes as the physical tray. Exact object
revisions prevent stale saved edits. Shared recording exposes start/save/discard
with native session IDs and retained failed takes. Shared avatar selection
prepares a humanoid before saving/replacing Maestro through the same physical
selection path; failure/cancellation keeps the previous selection. Shared live
posing now exposes start/rotate/save/finish/discard with native session IDs and
pose versions, the same physical joint limits and imported retargeter, held-joint
exclusion, failed-pose recovery and recording interoperation. Saved edits are
durable outside temporary mode; temporary edits stay in the fork until Keep.
No authoring operation starts playback. The current catalog inventory is generated
in `shared/generated/behaviourCatalog.json`. See QUEST_ANIMATION_AUTHORING.md and
QUEST_AVATAR_SELECTION.md.
Single-file GLB/VRM selection, preview and acceptance now share the physical
import path with book fields and agent/program calls. See QUEST_MODEL_IMPORT.md.
Animation collections now share native selection, versioned start/retry/tag controls,
Stop/Clear and bounded per-file motion results. See QUEST_MOTION_BATCH_IMPORT.md.
Controller stick preferences and programmable buttons now share the native catalog
and physical save path. Explicit live movement/view changes share native transitions
with fresh state and ownership checks. See QUEST_CONTROLLER_CONFIGURATION.md and
QUEST_CONTROLLER_MODES.md. Object physics, avatar distance/speed and exact walking
animation selection now use catalog actions with native readback and the physical
tools' save path. See QUEST_SPATIAL_SETTINGS.md. Physics Start/Pause now shares
the catalog through fresh native simulation identities and the physical service;
see QUEST_PHYSICS_SIMULATION.md. Load/Scan/Show/Hide/Cancel room setup now shares
the physical service through catalog requests and observable progress. Platform
permission/scan handoffs remain explicit; completion never claims alignment or
starts physics. See QUEST_ROOM_ENVIRONMENT.md. Live floor/table placement now shares
the physical support-offset calculation, saved edits and Undo through the catalog;
see QUEST_SURFACE_PLACEMENT.md. Object copying now shares the physical Duplicate
operation through object.create kind=copy and object.definition; geometry, asset
references and translated recordings are preserved with one Undo. See
QUEST_OBJECT_COPY.md. Drawing creation, point/radius edits and failed physical
capture resolution now share the physical pencil, catalog and optional typed book
fields. See QUEST_DRAWING_AUTHORING.md. Recipe part/track edits now use a shared
patch with paged exact readback; the book workshop uses that same action. See
QUEST_RECIPE_AUTHORING.md. The book action editor now loads real settings
and concurrency guards from catalog-declared facts. It requires an explicit
snapshot before running and never silently advances revisions; see QUEST_CURRENT_INPUTS.md.
Reusable behaviours can now choose visible current-value reads and live/fixed
preferences through the same typed program representation; see QUEST_REUSABLE_INPUTS.md.
The book now explicitly converts sequence Repeat to editable call/wait loops and
lets authors select the function receiving catalog blocks. Code and module edits
retain rejected drafts rather than silently disabling Repeat.
Parallel function branches now share the same interpreter, catalog ownership and
book/agent authoring, with explicit joined results and group cancellation. See
QUEST_PARALLEL_PROGRAMS.md. Recipe objects now also play exact named rotation tracks
on independent part channels through animation.play; live part poses share the
catalog. See QUEST_RECIPE_PART_PLAYBACK.md. Shared object.hold now lets these
parts, Maestro hands and object roots carry and release created props using the
existing collision and throw runtime; see QUEST_OBJECT_ATTACHMENTS.md. Calendar
deadlines and weekly times now use shared event waits and clock readback; see
QUEST_CALENDAR_SCHEDULES.md. The PC checkpoint below is verified;
headset acceptance remains outstanding.
Completing workspace review now drains accepted room/behaviour saves in the background under the existing edit hold; see QUEST_ACCEPTED_SAVE.md.
Aimed physical throws and read-only trajectory previews now use the same native catalog, generated book form and rigid-body physics; see QUEST_AIMED_THROWS.md.
The remaining runtime, provider, device and Store acceptance gates remain unfinished; the complete v1 goal remains active.

Remembered program values now connect workspace ownership, explicit checkpoints,
shared book/agent inspection and editing, retained-reference checks and complete
archive integration. Per-run state remains the default; saved values never restore
execution or start a behaviour. Temporary rooms now fork remembered values with room layout and publish both
through one recoverable save; physical Quest latency acceptance remains open.
See QUEST_PROGRAM_MEMORY.md and QUEST_TEMPORARY_ROOM.md.

Moving anchor zones now share the catalog, native event waits, nested book inputs
and agent authoring. Programs can observe a named recipe part, Maestro hand or
object root and request a reach-checked pickup through existing `object.hold`.
The pickup rechecks current distance and optional free-physics eligibility before
changing ownership. Normal saved book/avatar travel does not reset observation;
replaced sockets and rigid-body motion discontinuities remain guarded. These are
bounded sampled origin-distance observations, not swept contact, automatic IK or
promised catches. See QUEST_EVENT_PROGRAMS.md and QUEST_OBJECT_ATTACHMENTS.md.

Version-3 action blocks can now explicitly wait for busy channels with a bounded
timeout and ordered admission. Waiting owns no channels, keeps evaluated inputs
fixed and rechecks native readiness before execution. Book fields, source and the
agent share this option; Stop/grip/pause never replay cancelled work. See
QUEST_CHANNEL_WAITS.md. Interrupted-action resume remains separate work.

Current recovery implementation supports retained candidates, an external backup,
and an explicitly requested fresh workspace through the same shared book/agent
catalog. Original data stays preserved; preview, commit and content review remain
separate. Unavailable activation, review and recovery tracking now has a shared
inspect/preserve/reset path; its evidence can be exported and explicitly removed
using a matching completed native receipt. Neither path bypasses content review
or live workers. Retained generations can now be inventoried and exported as
portable content without opening them, including newer saved edits than their
original import manifest. A separate whole-generation preview now supports
confirmed permanent discard with optional backup. Current, previous, pointer-backup
and tracked operation roots remain protected. Private recovery export remains
separate. Explicit recovery now reserves a 65th slot when all 64 ordinary slots
are occupied; subsequent review enables confirmed cleanup without weakening
selection protection. A full reserve still requires finishing/cancelling recovery
or cleanup, and physical storage acceptance remains open.
Retained-source recovery now captures current saved edits and remembered values
through the same consistent boundary as retained export. Its preview has a new
manifest; original source bytes stay preserved, and old runtimes are gated from
promising the new behavior. See QUEST_NATIVE_ARCHIVES.md.
PR #248 records the current verified commit and package. Earlier entries
below are historical evidence, not current test totals or release acceptance.

PC explicit channel waiting (2026-10-02): version-3 invocation blocks can opt
into a 0.1–30 second channel timeout. Waiting owns no channels and preserves the
evaluated inputs; readiness, revision and pickup reach are checked again before
any native start. Overlapping waits keep arrival order, including when older
whole-object programs are triggered. A newer legacy restart cannot stop active
work before being refused behind an earlier waiter. Disjoint work and native user
priority remain independent. Stop, grip, edits and pause remove pending work
without replay; started actions retain their existing interruption policy.

Verification: **491 Unity EditMode and 410 PlayMode tests passed**, with three
optional private-file skips. **1,749 web tests in 206 files** and **76 Android
tests** passed. The final native capture passed all seven new web contract/editor
checks. TypeScript, lint, production build and catalog checks passed. Native tests
exercise real recipe-joint sequencing, XRI cancellation and current pickup reach,
plus FIFO admission, atomic claims, timeout, legacy queues, parallel branches,
module scope and instruction-budget exhaustion. The actual book editor and native
waiting status were inspected on two 512-pixel browser pages without horizontal
overflow or browser errors. This is PC evidence, not physical Quest acceptance.

The full development APK helper exited **0**. All **226 runtime / 122 test / 11
Editor C# files**, **37 native fixtures plus metadata**, the native AAR and **113
packaged web files** matched the tested inputs. The exact included avatar, all
**178 motion payloads**, ARM64-only libraries and v2 signature were checked.
Checkpoint: `D:/Projects/Builds/MaestroQuestVerify/Builds/Checkpoints/MaestroQuest-channel-waits-C36651A8.apk`.
SHA-256: `C36651A87114954083994053DC4A4AB1FA381D15F8C9E14E70E87D439C75EA73`.
Development signing; **not installed on Quest**. Interrupted-action resume,
autonomous reflex policy and remaining v1/provider/device/Store gates remain open.
See QUEST_CHANNEL_WAITS.md.

PC moving-anchor zones (2026-10-02): programs can observe a moving recipe
part, Maestro hand or object root and request an optional reach-checked pickup.
The action rechecks distance and free-physics eligibility immediately before
attachment. Native integration launches a ball into a recipe-hand zone, picks it
up, drops it and verifies actual gravity/floor collision. Other cases cover
stale reach, paused physics, exact socket replacement and normal saved avatar/book
travel. The book and agent share nested typed inputs and the same native catalog.
This is sampled proximity, not swept collision, automatic reaching or guaranteed
fast-ball catching. Reflex arbitration, IK and headset timing remain unfinished.

Verification: **478 Unity EditMode and 407 PlayMode tests passed**, with three
optional private-file skips. **1,742 web tests in 204 files** and **76 Android
tests** passed. The final native capture passed the five focused anchor contract/UI
tests. TypeScript, lint, production build and catalog checks passed. Browser
checks used native observations and the real React editor on two 512-pixel pages:
nested event bindings and optional pickup reach were editable without horizontal
overflow or browser errors. This is PC verification, not physical Quest acceptance.

The full development APK helper exited **0**. All **225 runtime / 120 test / 11
Editor C# files**, **36 native fixtures plus metadata**, the native AAR and **113
packaged web files** matched the tested inputs. The exact included avatar, all
**178 motion payloads**, ARM64-only libraries and v2 signature were checked.
Checkpoint: `D:/Projects/Builds/MaestroQuestVerify/Builds/Checkpoints/MaestroQuest-anchor-zones-8D9C9925.apk`.
SHA-256: `8D9C9925D7467D12BF5E1BB3D9A94BAF486FB697CEDBA2858DA630D2F83F3EF9`.
Development signing; **not installed on Quest**. The device hold remains in force.

PC remembered-values integration (2026-10-02): version-3 behaviours can opt into
stable remembered declarations and explicit Save remembered values blocks. The
native scheduler restores typed values only at an explicit start, waits for
checkpoint publication, bounds checkpoint admission across runs, and cancels
later blocks after Stop while accepted writes drain. Parallel branches return
values to their parent before saving. Ordinary per-run state stays unchanged.
Book blocks, agent queries and the shared `program.memory.edit` action inspect,
set and explicitly reset the same values with stopped-target and stale-revision
guards. Deleted declarations retain inspectable values until reset. Workspace
archives and retained motion-reference checks include memory; no execution resumes
from a backup. Temporary rooms reject memory work until kept or discarded.

Verification: **470 Unity EditMode and 402 PlayMode tests passed**, with three
optional private-file skips. **1,737 web tests in 202 files** and **76 Android
tests** passed; TypeScript, lint, production build and catalog checks passed.
Browser checks used actual native observations with the real React editor:
typed drafts, reset cancellation and remembered declaration editing were readable
on two 512-pixel pages with no horizontal overflow or browser errors. This is
PC verification, not physical Quest acceptance.

The full development APK helper exited **0**. All **223 runtime / 118 test / 11
Editor C# files**, **35 native fixtures plus metadata**, the native AAR and **113
packaged web files** matched the verified inputs. The exact included avatar and
all **178 motion payloads**, ARM64-only libraries and v2 signature were checked.
Checkpoint: `D:/Projects/Builds/MaestroQuestVerify/Builds/Checkpoints/MaestroQuest-program-memory-D199099E.apk`.
SHA-256: `D199099E32F09783007EB4D4CE0BD34F4B1E4E43D4B3E47A87FDF218CA8644DA`.
Development signing; **not installed on Quest**. Hardware save latency, interruption
and long-session acceptance remain open. Exact-commit CI is linked in PR #248.

PC aimed-throw increment (2026-10-02): `object.physics.launch` now sends a
Solid/Bouncy creation toward an explicit world point or exact root/recipe-part/
Maestro-hand anchor through the existing rigid body. `object.physics.trajectory`
previews the same bounded fixed-step gravity/damping estimate and current collision
checks. The generated book form, native programs and agent dispatch share one
module. Fresh execution refuses changed anchors, blocked/out-of-room paths, the
observed head clearance zone, excessive initial speed and unavailable physics.
Completion means launched; physical contacts and later movement remain authoritative.
Duplicate receipts cannot throw again. The launch adds no saved edit or Undo;
normal physics autosave and temporary-room Keep boundaries remain. See
QUEST_AIMED_THROWS.md. No IK catch, homing or promised arrival is implied.

Six new PlayMode tests cover actual flight at desktop/72 Hz fixed steps, changed
gravity, floor launch/bounce, fresh/stale anchors, wall/apex/head/bounds rejection,
ownership, pause and duplicate request IDs. Testing the real catalog/scheduler
preview exposed a missing-context fallback and verified its correction. Native
captures also drive the bridge and book component tests.

Verification passed 415 EditMode and 386 PlayMode tests (three optional private
model skips), all 1,710 web tests across 197 files and 61 Android tests. The final
51-test book/bridge run uses the packaging run's native captures. TypeScript,
ESLint, prompt/core boundaries, catalog drift, Android assembly/lint and the full
build helper passed. All 212 runtime / 107 test C# files, 32 native fixtures plus
metadata and 113 packaged web files match the tested inputs. ARM64-only contents
and v2 signature verified. Catalog now has 59 actions, 11 events and 50 facts.
APK: `D:\Projects\Builds\MaestroQuestVerify\Builds\Checkpoints\MaestroQuest-aimed-throw-EC10AAC0.apk`.
SHA-256: `EC10AAC01100736167245C019061AC7287510CC23DBB43A43C5FAC68CE39A1E1`.
Development signing; not installed on Quest. Physical Quest timing/tracking/scan
alignment/comfort and the remaining v1/provider/Store gates remain open.

PC accepted-content save increment (2026-10-02): completing workspace review
now sequences room and behaviour saves on background workers, then waits for both
writes and the exact content fingerprint before applying approval. The existing
edit/activity hold stays in place, while per-store save-dispatch leases prevent
competing autosaves or lifecycle waits. Cancellation and save/capture failure
drain every dispatched worker; retirement excludes a new owner until writing ends.
An older failed snapshot cannot prevent saving the latest accepted data. Partial
failure keeps live edits/Undo and the ordinary retry path, with review still held.
No execution resumes. See QUEST_ACCEPTED_SAVE.md.

Six new PlayMode regressions hold earlier writers while Unity frames advance,
verify saved room/behaviour contents, exercise cancellation and one-sided failure,
fail capture startup after dispatch, refuse read-only saves while an older writer
is pending, and reopen after retirement. These checks
also found and fixed a final-save notification reaching a creation tray during
teardown. The bridge and book components consume actual native completing/completed
observations; completion is distinct from the initial request acknowledgement.

Verification passed 415 EditMode and 380 PlayMode tests (three optional private
model skips), all 1,705 web tests across 196 files and 61 Android tests. A final
69-test book/bridge run checked the captures from the packaging run. TypeScript,
ESLint, prompt/core boundaries, catalog drift, Android assembly/lint and the full
build helper passed. All 210 runtime / 106 test C# files, 32 native fixtures plus
metadata and 113 packaged web files match the tested inputs. ARM64-only contents
and v2 signature verified. Catalog remains 58 actions, 11 events and 49 facts.
APK: `D:\Projects\Builds\MaestroQuestVerify\Builds\Checkpoints\MaestroQuest-accepted-save-3FBBAA41.apk`.
SHA-256: `3FBBAA41D3687906DBB7F688B2EDA1414559A7779CA2FD9237D676FE6846578D`.
Development signing; not installed on Quest. This proves PC ordering and lifecycle
behavior, not a headset frame-time budget. Other synchronous edit/receipt paths
and device/Store acceptance remain. The complete v1 goal remains active.

PC calendar-scheduling increment (2026-10-02): clock.scheduled now waits for one
explicit date/time or the next selected weekly occurrence through the shared
native event subscription, typed blocks and agent program. clock.now reports
calendar timestamps, local zone, UTC offset and weekday through shared facts.
Offset/DST choices, missed-deadline reporting or failure and clock changes are
explicit. Weekly waits do not collect a backlog. Stop, pause, focus loss and
reload cancel execution; background alarms and durable resumption remain separate.
See QUEST_CALENDAR_SCHEDULES.md.

A real native saved program created a ball at the injected on-time occurrence,
counted a later missed occurrence without creating another, and stayed stopped
after pause. Chrome edited that exact program and displayed the final native
observations; browser save acknowledgements were simulated. Calendar tests use
an injected clock, not changes to the PC clock or multiweek device acceptance.

Verification passed 415 EditMode and 374 PlayMode tests (three optional private
model skips), all 1,703 web tests across 196 files and 61 Android tests. The final
native capture and eight catalog observations were checked again in focused web
tests and Chrome; screenshots inspected. TypeScript, ESLint, prompt/core boundaries,
catalog drift, Android assembly/lint and the full build helper passed. All 209
runtime / 105 test C# files, 32 fixtures plus metadata and 113 packaged web files
match the tested inputs. The APK is ARM64-only with a verified v2 signature.
Catalog: 58 actions, 11 events, 49 facts.
APK: `D:\Projects\Builds\MaestroQuestVerify\Builds\Checkpoints\MaestroQuest-calendar-schedules-126F3BA1.apk`.
SHA-256: `126F3BA1FF3A728D77CDF91CA30D646CEEDFEEAD7804EEC4BE56477DBFCF196B`.
Development signing; not installed on Quest. Device timezone, lifecycle,
readability and performance acceptance remain outstanding. No device, production
or Store operation was performed. The complete v1 goal remains active.

PC shared-object-attachment increment (2026-10-02): object.hold now carries a
created prop at an exact recipe part, Maestro hand or object root. It shares the
existing avatar prop trajectory, collision checks and physical release runtime.
Both IDs are authorized resources; only the prop receives a whole-object claim,
so holder animation composes in parallel. Grip, replaced anchors, blocked paths,
room/app interruption and nested-chain refusal have explicit results without
hidden release or replay. object.anchor and object.attachment report live state;
programs can branch on the native release result after joining their actions.
See QUEST_OBJECT_ATTACHMENTS.md. Recipe parts now use the scheduler clock, with
an explicit physics-time-paused regression; existing whole autoplay is unchanged.

Nested catalog variants have shared native/web typed argument bindings and
resource checks, including fact inputs. The generated book editor authored the
exact robot carry/arm-motion program executed in Unity; native observations show
its ownership, release and joined didThrow=True result. Browser acknowledgements
are simulated. Inspected screenshots also exposed and verified a fix for unnamed
objects in ownership inspection. Recipe joints retain their static assembly proxy;
IK catching, physical joint chains and aimed ballistic throws remain separate work.

Verification passed 409 EditMode and 372 PlayMode tests (three optional private
model skips), all 1,697 web tests across 195 files and 61 Android tests. Eight
catalog observations were refreshed from the final native suite. TypeScript,
ESLint, prompt/core boundaries, catalog drift, Android assembly/lint and the full
build helper passed. All 208 runtime / 103 test C# files, 31 native fixtures plus
metadata and 113 packaged web files match the tested inputs. The APK is ARM64-only
with a verified v2 signature. Catalog: 58 actions, 10 events, 48 facts.
APK: `D:\Projects\Builds\MaestroQuestVerify\Builds\Checkpoints\MaestroQuest-object-attachments-61DC553D.apk`.
SHA-256: `61DC553DC27E638EF455120962DFA8A3EEF6C245DBEDA92E880A96DC2C661775`.
Development signing; not installed or accepted on Quest. No device, production
or Store operation was performed. The complete v1 goal remains active.

PC named-recipe-part increment (2026-10-02): animation.play now selects a saved
recipe track through source.part and channel=recipePart. Independent local joints
compose, including parent/child parts; same-part and whole-object claims conflict.
Playback leaves saved keys and root physics untouched. Stop/completion keeps the
live pose and suppresses that track's autoplay until an explicit whole-recipe
Restart. Unrelated tracks continue. Grips, pause, disabled/replaced objects and
parallel-group interruption retain cancellation without automatic replay.
object.recipe.pose reports actual local/world transforms, parent and playback.
These remain visual joints with the existing whole-assembly proxy collider;
generalized physical links, sockets and reflex policies remain separate work.

The same catalog schema drives typed book fields and agent/program calls. Chrome
edits a part argument into the exact program run by Unity, displays both native
joint ownership claims and their traces, then shows native completion. Rendered
screenshots were inspected; book save acknowledgements are simulated. No physical
Quest acceptance is implied. The catalog remains 57 actions and 10 events, with
46 facts; no new public animation verb, model tool or numeric action was added.

Verification passed 406 EditMode and 368 PlayMode tests (three optional private
model skips), all 1,689 web tests in 193 files, and 61 Android tests. Native catalog
observations were recaptured after the source schema changed. TypeScript, ESLint,
prompt ownership, core boundaries and catalog drift checks pass. The full build
helper exited zero; all 205 runtime/101 test C# files, 30 native fixtures plus
metadata, 113 packaged web files and the Android browser library match the verified
inputs. ARM64 contents and v2 signature verify. Development checkpoint
`MaestroQuest-recipe-parts-2AE8EBAA.apk` has SHA-256
`2AE8EBAA15071705D52155AD6C358C99BA283B0153CDCD9CEDA11E3998387D48`.
It is uninstalled. Remaining v1/provider/device/storage/performance/Store gates
stay open; the active goal and draft PR do not claim a release-ready app.

PC parallel-functions increment (2026-10-02): version-3 programs can opt into
parallelVersion:1 and run two to four function calls together, waiting for all.
Branches use private locals/state snapshots, explicit typed results and the same
native invocation/event/timer paths. All siblings are admitted before effects;
parent/children share the eight slots, creation quota and retained-value budget.
Stop or failure cancels the group without undoing completed effects. Failed
cleanup attempts every sibling before preserving the refusal of manual takeover.
Only the root emits a terminal behaviour outcome; child completion cannot claim
that the user request is complete. Failure retains its branch function/block.

The book's existing editor provides typed branch calls and separate live traces.
Pinned modules and function/declaration edits preserve the same program. Native
verification passed 403 EditMode and 365 PlayMode tests, with three optional private
model checks skipped. Eleven native logic scenarios and a two-object PlayMode
journey exercise concurrency, joins, private state, events, capacity, creation and
memory/work budgets, cancellation, ownership, cleanup failures and module imports.
An initial runtime pass hit two existing prop-interruption timing assertions;
the following two complete runtime passes passed those assertions unchanged.
Quest timing and comfort remain unverified.

All 1,684 web tests in 192 files passed; 39 final contract/book checks also validate
the final native capture. Chrome changes a branch argument through typed controls,
sends the identical native program and displays actual running/joined observations.
Screenshots were inspected; browser save acknowledgements are simulated. The full
build helper exited zero, 61 Android browser tests passed, and the 202 runtime /
99 test C# files, 29 fixtures with metadata and 113 packaged web files match.
TypeScript, lint, catalog drift, prompt ownership and core boundaries pass.
ARM64 contents and v2 signing verify. Development checkpoint
`MaestroQuest-parallel-programs-2A876967.apk` has SHA-256
`2A876967D83528F274DDBC5B6F6087F26B2C1E0AEECC7E8D7B7CC437D67912D2`.
It remains uninstalled. PR #248 stays draft; physical Quest, provider, asset,
persistence/performance and Store release gates remain open.

PC shared-recipe-authoring increment (2026-10-02): object.recipe.edit patches
existing parts and tracks through the catalog and the book workshop's Apply action.
The shared agent guide uses the same operation. Explicit stable-ID changes preserve
unrequested entries; hierarchy validation rejects missing parents/cycles and never
silently removes descendants. Duration retimes untouched keys proportionally.
Exact revisions, ownership and save-before-completion keep failed edits from changing
geometry or data. The target's recipe animation stops; unrelated actors continue.
Root motion, placement, physics and tint remain. Rebuilt recipe meshes now retain tint.

Paged exact-revision facts expose all 32 parts, 17 tracks and 16 keys per track.
Existing message/value budgets remain unchanged; large edits use explicit smaller
patches. The book sends only changed entries and retains stale/rejected drafts until
a matching completed native receipt. See QUEST_RECIPE_AUTHORING.md.

Verification passed 392 EditMode and 364 PlayMode tests (three optional private
model checks skipped), 1,675 web tests in 191 files and 61 Android browser tests.
Six native scenarios cover geometry/tint, Undo/replay, retiming, hierarchy, motion,
full-capacity readback, ownership, failures and temporary isolation. Chrome replays
the same captured native patch from the workshop and generated catalog fields;
their requests/results match and screenshots were inspected. This is PC evidence.
Production build, TypeScript, lint, catalog drift, prompt ownership and boundaries
pass. The full build helper exited zero. All 202 runtime and 97 test C# files,
28 fixtures plus metadata and 113 packaged web files match. ARM64 and v2 signature
verify. Development checkpoint `MaestroQuest-recipe-authoring-85BF40EB.apk` has SHA-256
`85BF40EBCB878B2CF98752AE636D20D684E93953BE7EAC8378F586D11AB07A59`. It remains uninstalled. Physical Quest, provider, performance and
Store acceptance remain open; PR #248 remains draft.

PC shared-drawing-authoring increment (2026-10-01): object.create kind=drawing,
object.drawing.edit and object.drawing.resolve now share native geometry, saved
edits and physical capture recovery with book fields and agent/program calls.
Creation and splicing accept 64 points per call; repeated explicit edits reach
all 2,048 native stroke points. Exact-revision pages return eight coordinates at
a time without simplification. Mesh, selection and approximate collision bounds
refresh together. Saved edits preserve placement, motion, paint and physics;
held/owned/authoring targets and stale revisions are refused. Undo requires
release, temporary changes stay in their fork, and replay is historical.

Failed physical saves retain frozen points in memory and expose exact-session
Retry/Discard through the catalog and solid tray controls. Save also retries.
New pencil input and workspace/temporary boundaries cannot replace the draft.
Discard affects only that draft. Retirement releases the write lease; unsaved
memory is not a crash archive. See QUEST_DRAWING_AUTHORING.md.

Verification passed 391 EditMode and 358 PlayMode tests (three optional private
model checks skipped), 1,661 web tests in 190 files and 61 Android browser tests.
Nine native drawing scenarios cover geometry, full-capacity edit/readback, ownership,
stale edits, Undo, temporary isolation, failed saves, retention and physical controls.
The existing avatar-wall test failed once with an interrupted-motion result, then
passed both final native runs without assertion changes. Browser replay uses actual
native creation/edit captures through generated point fields; generic book tests
also replay retained-stroke retry. These checks do not execute Quest actions.
Production build, TypeScript, lint, catalog drift, prompt ownership and boundaries
pass. The full build helper exited zero. All 199 runtime and 95 test C# files,
27 fixtures plus metadata and 113 packaged web files match. ARM64 and v2 signature
verify. Development checkpoint `MaestroQuest-drawing-authoring-BBE8417A.apk` has SHA-256
`BBE8417A05001A3B422740DBEB10572D141373ACDD558CA7E7B0DD3D504CF8C1`. It remains uninstalled. Physical Quest, provider, performance and
Store acceptance remain open; PR #248 remains draft.

PC shared-object-copy increment (2026-10-01): object.create kind=copy and
object.definition share the physical Duplicate operation with book fields and
agent/program calls. An exact source revision guards the copy; native IDs, source
ownership and normal saved-edit budgets remain authoritative. Drawing geometry,
recipe parts/tracks, model references, physics settings and recorded motion remain
editable. The copied origin translates recorded paths without retargeting them.
Copies start without animation playback; unrelated actors and the source remain
unchanged. Physical Duplicate selects the copy; agent/program calls do not.
Temporary copies stay in the fork, Undo removes one copy, and replay returns the
old receipt without another object. See QUEST_OBJECT_COPY.md.

Verification passed 389 EditMode and 349 PlayMode tests (three optional private
model checks skipped), 1,638 web tests in 189 files, and 61 Android browser tests.
Seven native copy tests cover translated drawings/recordings, recipes, independent
GLB instances/playback, physical Duplicate, temporary isolation, Undo/replay,
stale/held/owned sources, failed writes, aggregate limits and returned-ID program
chaining. The browser submits the captured native copy through generated fields
and displays its exact returned identity. It does not run a Quest action.
Production build, TypeScript, lint, catalog drift, prompt ownership and boundaries
pass. The full build helper exited zero. All 195 runtime and 93 test C# files,
26 fixtures plus metadata, and 113 packaged web files match. ARM64 and v2 signature
verify. Development checkpoint `MaestroQuest-object-copy-24B02EEB.apk` has SHA-256
`24B02EEB7D29DA47918BE6E0BCA973BC66B0377B3F3511366AD551D61FBD13A9`. It remains uninstalled. Physical Quest,
provider, performance and Store acceptance remain open; PR #248 remains draft.

PC shared-surface-placement increment (2026-10-01): object.surface.place exposes
the physical Place surface service to the book, agent and programs. It captures
one below-object or head-gaze ray, uses live upward-surface detection within four
metres, and applies collision-bound support plus 1 cm clearance through the same
saved edit and Undo. Results report actual room-space position/contact/normal and
revision. Missing surfaces never substitute coordinates or retry. Target ownership,
Stop, lifecycle changes, current room identity and target revision guard delayed
completion. Cancel/Recall/focus loss also prevent late physical tray arming.
Permission UI and real surface/fit/alignment acceptance remain device work.

Verification passed 389 EditMode and 342 PlayMode tests (three optional private
model checks skipped), 1,629 web tests in 188 files, and 61 Android browser tests.
The seven new native scenarios exercise actual colliders, journal, scheduler,
receipts and physical tray with a controlled surface-provider boundary. Chrome
replay submits the captured native request and displays its actual result;
screenshots were inspected. TypeScript, lint, catalog drift, prompt ownership,
core boundaries, production web build and Android checks pass. The full build
helper exited zero. All 193 runtime and 92 test C# files, 26 fixtures plus metadata,
and 113 packaged web files match the tested source/build. ARM64-only and v2
signature verify. Development checkpoint `MaestroQuest-surface-placement-0C9AEB09.apk`
has SHA-256 `0C9AEB09933E1F99406AAABA446007585E61D10A4DF5A10F4CCA7E6FB42C8C75`.
Evidence: `.quest-evidence/surface-placement/verification.json`. It remains
uninstalled; full v1 provider/device/Store acceptance is unfinished.

PC shared-room-setup increment (2026-10-01): Load/Scan/Show/Hide/Cancel now
share the native physical service through the catalog and generated book fields.
Fresh setup/request identities bind intent. Completion acknowledges a request;
progress, loaded geometry, collider readiness and physical alignment remain
separate. Load never silently launches scanning. Expected permission/Meta setup
focus handoffs may return; interrupted geometry loading, workspace holds and
component retirement discard late work. Cancellation and timeout retain admission
until the actual OS/SDK task drains. Physics and movement never restart themselves.
The physical Scan control becomes Cancel setup, then Wait for system while draining.
Native tests substitute only the OS/SDK boundary. Browser replay uses actual native
observations/receipts; device system screens and alignment still need acceptance.

Verification passed 389 EditMode and 335 PlayMode tests (three optional private
model checks skipped), 1,626 web tests in 187 files, and 61 Android browser tests.
Production build, TypeScript, ESLint, catalog drift, prompt ownership and core
boundaries pass. The full build helper exited zero. All 191 runtime and 91 test
C# files, 26 fixture files plus metadata, and 113 packaged web files match the
tested source/build. ARM64-only and v2 signature verify. Development checkpoint
`MaestroQuest-room-environment-5B120FAB.apk` has SHA-256
`5B120FABD8A983ED33ECCE7118D5C2E4AA152A750F91A3ACF4D0A3A9D0A1C62C`.
Evidence: `.quest-evidence/room-environment/verification.json`. It remains
uninstalled; the complete v1 goal and provider/device/Store acceptance remain open.

PC repeat-loop authoring increment (2026-10-01): explicit conversion wraps the
existing entry function in a version-3 Forever/call/sleep loop with a user-selected
cycle delay. Original function bodies, local resets and early returns remain;
version-3 per-action ownership and run-wide limits are made explicit. Code and
module editors retain rejected buffers/previews rather than silently clearing the
older Repeat flag. Catalog insertion has a visible function destination, including
the original cycle function. Shared agent guidance describes the same representation.
Saving never starts a run. Native scheduler coverage verifies locals, returns,
timing, released ownership, Stop and no lifecycle restart. Browser authoring replay
checks conversion, insertion into the cycle and save without execution commands.
The full v1 release goal remains open; this is not provider/headset acceptance.

Verification passed 389 EditMode and 328 PlayMode tests (three optional private
model checks skipped), 1,621 web tests in 186 files, and 61 Android browser tests.
Production build, TypeScript, ESLint, catalog drift, prompt ownership and core
boundaries pass. The full build helper exited zero. All 189 runtime and 90 test
C# files, 26 fixture files plus metadata, and 113 packaged web files match the
tested source/build. ARM64-only and v2 signature verify. Development checkpoint
`MaestroQuest-repeat-loops-CEEFB33E.apk` has SHA-256
`CEEFB33EBECDF26188B3A2B7C85A49BAC8BA9DBE4ABBE44A5D4B5916BF73F879`.
Evidence: `.quest-evidence/repeat-loops/verification.json`. It remains uninstalled;
device/provider/Store acceptance and the complete v1 release remain unfinished.

PC reusable-input increment (2026-10-01): program insertion now offers a literal
snapshot or ordinary editable read/action blocks generated from native current-input
metadata. Guards and chosen live preferences use one typed fact snapshot; edited
preferences stay fixed. Existing functions/state/imports survive, symbols avoid
collisions, unavailable reads stop before effects, and stale/blocked actions do not
retry. One-off Run keeps its exact reviewed snapshot. Native and web limits remain
enforced. Older sequence-level Repeat requires explicit conversion to a typed loop;
the editor preserves that draft rather than changing timing silently. The shared
room-agent guide describes the same dataflow and stable-identity rules.

Verification: 388 EditMode + 328 PlayMode tests passed, with three optional private
model checks skipped. The web suite passed 1,614 tests in 185 files, and 61 Android
browser tests passed. A generated program fixture proves a single fact read and
failure without fallback; actual scheduler/storage runs preserve later distance
changes while applying fixed speed with fresh revisions. The Chrome authoring probe
saved visible read/action blocks without execution commands; screenshots were
inspected. Its save acknowledgement now waits for the actual request, not an old
status message. Initial native test compilation needed a missing namespace import;
the final full build passed. TypeScript, lint, catalog drift, prompt ownership and
core boundary checks pass. All 189 runtime and 90 test C# files, 25 fixtures plus
metadata and 113 web files match. The full helper exited 0.

ARM64-only, v2-signature-verified development APK: `D:\Projects\Builds\MaestroQuestVerify\Builds\Checkpoints\MaestroQuest-reusable-inputs-D74BFC44.apk`.
SHA-256 `D74BFC440DC1C0D8EE44BDF4DE8A6580131781CB5F025DE50DD531C7546758A4`.
Evidence: `.quest-evidence/reusable-inputs/verification.json`. Not installed on
Quest; device/provider/Store acceptance and the complete v1 remain unfinished.

PC current-input editing increment (2026-10-01): six shared actions (14 variants)
now declare read-only fact-to-draft mappings in their native schemas. The book
loads real settings and guards before running, preserves explicit operations and
identities, and rejects late reads after draft/session changes. Default examples
cannot execute without loading or explicit advanced arguments. Recurring programs
still need fresh fact bindings; insertion retains the reviewed literal snapshot.
See QUEST_CURRENT_INPUTS.md. A final capture also corrected an older test assumption:
normal physics placement capture can advance a revision after a successful save
receipt, so later readback checks settings and monotonic revision ordering.

Verification: 387 EditMode + 327 PlayMode tests passed (three optional private-model
checks skipped), 1,608 web tests across 184 files, 49 focused final-capture checks,
and 61 Android browser tests. Four Chrome probes loaded native snapshots and
replayed 11 exact native commands; screenshots were inspected. A transient empty
browser startup retried successfully, and the final captures passed. CI exposed a
five-second timeout in one test spanning five editor scenarios; those scenarios
now run as independent tests with all assertions retained. TypeScript,
ESLint, catalog drift, prompt ownership and core boundaries passed. The full build
helper exited 0; all 189 runtime and 90 test C# sources, 24 fixtures plus metadata,
and 113 packaged web files match. ARM64-only, v2-signature-verified development APK:
`D:\Projects\Builds\MaestroQuestVerify\Builds\Checkpoints\MaestroQuest-current-inputs-8F2398CF.apk`.
SHA-256 `8F2398CFC85188465D2AD7B4FE09F1B324F3B212D9B7D539DB0A8FAB88249E1B`.
Evidence: `.quest-evidence/current-inputs/verification.json`. Not installed; device,
provider and Store acceptance remain separate. No production deployment.

PC shared-physics-simulation increment (2026-10-01): `physics.simulation.set`
and `physics.simulation` expose Start/Pause through the physical world service.
Native state identities reject delayed intent after manual reversal, scan changes,
focus/lifecycle transitions and workspace holds. Pause clears old throw velocities
and ends physics-dependent walking without restarting it later. Unrelated gestures,
timers and programs can continue; held and animated objects retain their owners.
Readiness checks are read-only, unchanged shared requests do not reset observations,
and duplicate receipts cannot restart gravity after manual Pause. Programs can
read each current state identity before invoking Start or Pause. These transient
transitions do not edit room documents or add Undo; normal placement tracking is
separate. See QUEST_PHYSICS_SIMULATION.md.
Five new native journeys test actual falling/frozen bodies, throw reset, stale
requests, workspace/lifecycle holds, actor coexistence and a start/wait/pause
program. Validation passes 386 EditMode and 327 PlayMode tests (three optional
private-model skips), 1,598 web tests across 183 files, 35 final-capture/book checks
and 61 Android browser tests. Chrome uses generated fields and reads exact captured
native state; it is capture replay, not headset/provider acceptance. Production
build, lint, native catalog equality/source drift, prompt ownership and boundaries
pass. The complete package helper exited zero. All 189 runtime and 90 test C#
sources, 24 fixtures plus metadata and 113 packaged web files match.
ARM64/v2 verified checkpoint `MaestroQuest-physics-simulation-E0E65A8E.apk`, SHA-256
`E0E65A8E6FE97D6053FAF97EA13D6CE1061556CD27F7918135391EAAD29A7A5C`.
It remains uninstalled; broader runtime, provider/device and Store gates remain.

PC shared-spatial-settings increment (2026-10-01): `object.physics.configure`,
`avatar.movement.configure` and `avatar.walk.select` now share the physical tools'
durable save path. Native facts expose accepted preferences and exact object
revisions; library selections retain stable motion IDs, and embedded selections
bind exact clip indexes to the loaded model hash. Three-entry discovery pages
cover all 32 supported embedded clips within the observation budget. Changed
settings have one Undo; identical values add none. Failed writes preserve accepted
state, temporary edits remain in their fork, and shared edits refuse competing
actors while manual tools retain interruption priority. Editing a falling object's
physics retains its current placement and does not stop other actors. None of
these preferences starts physics, following or animation playback.
Seven native journeys cover these behaviours, including stale requests, duplicate
receipts, unavailable motion files and all embedded pages. Verification passes
386 EditMode and 322 PlayMode tests (three optional private-model skips), 1,594 web
tests across 182 files, 35 final-capture/book checks and 61 Android browser tests.
Chrome's generated physics/movement/walk fields emit the exact captured native
requests and read their results; all walking-source variants also pass book tests.
Browser evidence replays native captures rather than testing a headset/provider.
Production build, lint, catalog equality/source drift, prompt ownership and core
boundaries pass. The full package helper exited zero. All 188 runtime and 89 test
C# sources, 24 fixtures plus metadata and 113 packaged web files match.
ARM64/v2 verified checkpoint `MaestroQuest-spatial-settings-67347680.apk`, SHA-256
`673476801D2130635CCB3DC973744D17307A8C05A880A9103E9DB5E247EAD604`.
The package remains uninstalled; broader runtime, provider/device and Store gates
remain open. See QUEST_SPATIAL_SETTINGS.md for the shared contract.

PC shared-controller-modes increment (2026-10-01): `controller.mode.set` now
exposes explicit Maestro/user stick opt-ins and MR/Virtual changes through the
physical movement service. The separate `controller.mode` fact supplies native
state IDs; manual changes, configuration saves, focus/tracking changes and Recall
invalidate old requests. Recall also invalidates requests while already off.
Shared changes preserve other actors and complete their own scheduler invocation.
Independent neutral-input gates prevent held sticks/buttons from starting input.
At that increment, MR restored the physical camera origin and paused physics;
the later fixed-tracking work below restores virtual content instead. Live modes
are not saved.
Manual takeover and B/Y/palm recovery retain priority. Five native journeys cover
actual avatar/user movement, snap turns, stale IDs, tracking/focus loss, held
input, actor refusal, duplicate receipts and final recovery notification identity.
PC validation passes 386 EditMode and 315 PlayMode tests (three optional private-model
skips), 1,586 web tests across 181 files, 33 final-capture/book checks and 61 Android
tests. Chrome emits four explicit changes using exact native readback; its
acknowledgements replay captures. Screenshots, production build, lint, catalog,
prompt ownership and boundaries passed. One configuration shutdown timed out;
the successful final helper exited zero. All 186 runtime and 88 test C# sources,
24 fixtures plus metadata, and 113 packaged web files match the final build.
ARM64/v2 verified checkpoint `MaestroQuest-controller-modes-177BC2D0.apk`, SHA-256
`177BC2D0A32A20F1F7DFDD43ED25729D6B1458989613F80B254DA9B69EF7ED53`.
It remains uninstalled. Broader runtime, provider/device and Store gates stay open.

PC shared-controller-configuration increment (2026-10-01): generated book fields
and agent/program calls now edit the physical movement tray's preferences through
`controller.configure`. Native configuration IDs rotate only after accepted saves
and on a new runtime; stale edits and duplicate receipts cannot reapply settings.
Independent sticks, speed/dead zone and exact saved-program button assignments
preserve unrelated values. Reserved inputs remain unavailable. Input gates require
neutral/release after saving, and later button presses use the existing scheduler.
Preferences remain immediately saved outside the temporary-room Undo boundary;
workspace preservation blocks edits, while activity review still allows manual
configuration without starting movement. Unavailable storage is explicit in facts.
Seven native journeys cover persistence/restart, failure/retry, actual button
playback, stale/reserved inputs, all four program bindings and workspace holds.
PC validation passes 386 EditMode and 310 PlayMode tests (three optional private-model
skips), 1,582 web tests across 180 files, 33 focused capture/control checks and 61
Android tests. Browser screenshots were inspected; acknowledgements replay actual
native captures, not live headset/provider execution. The full build helper exited
zero. All 184 runtime and 87 test C# sources, 24 fixtures plus metadata and 113
packaged web files match. ARM64/v2 verified checkpoint:
`MaestroQuest-controller-settings-2DF18A19.apk`, SHA-256
`2DF18A191785EEF2F2D7C3B4ADC8E857F997A1C0711CB22CFD3DA1359BE8B2DB`.
It remains uninstalled. Shared movement enable/disable and MR/Virtual view selection
are the next controller parity gap; broader runtime, provider/device and Store
acceptance remain open.

PC bounded-import-readback increment (2026-10-01): a maximum-size single-file
motion import now reports a count in its summary and exposes all exact IDs through
four bounded pages. The completed action outcome retains every ID; a new selection
invalidates old session reads. Shared single-file/batch display names and errors
are bounded by serialized JSON cost, including escapes, without shortening asset
identities or relaxing the global value budget. Category tags keep their existing
input normalization and remain untruncated in readback. Three native regressions
first reproduced the former oversized-value errors; the final suite passes 386
EditMode and 303 PlayMode tests, with three optional private-model skips. Web checks
pass 1,577 tests across 179 files and 33 focused final-capture tests; 61 Android
tests, build/lint and catalog checks pass. Browser walkthroughs read all 32 exact
motion IDs and exercise model selection through generated fields; screenshots were
inspected. These replay native captures and do not prove provider/headset behavior.
The full build helper exited successfully. All 182 runtime and 86 test C# sources,
24 fixtures plus metadata and 113 packaged web files match. ARM64/v2 verified:
`MaestroQuest-import-readback-02327703.apk`, SHA-256
`02327703DCDB56B743E47571167E7C477E3300C0305466A57727700F932D2507`.
The APK remains uninstalled. The broader runtime, provider/device and Store gates
below remain open; this resolves the readback follow-up in the preceding batch
checkpoint.

PC shared-animation-batch increment (2026-10-01): physical batch tools, generated
book fields and agent/program requests use the existing sequential motion importer
through native session IDs and control versions. Selecting files reads no provider
streams. Start/retry receipts acknowledge work; separate status and indexed file
facts expose partial results and pages of eight exact motion IDs. Explicit Stop,
resume/retry and Clear preserve completed files and user-edited metadata. Pending
sources hold workspace ownership; disable/destruction waits for reads/writes to
drain. All native chooser kinds now share admission through worker retirement.
Seven native journeys include an actual 32-distinct-clip export. PC checks pass
1,573 web tests across 178 files, 30 focused final-capture checks, 378 EditMode and
299 PlayMode tests (three optional private-model skips), and 61 Android tests.
The Chrome walkthrough uses native-capture replay, not provider/headset execution;
status and file screenshots were inspected. The full helper exited successfully;
181 runtime and 84 test C# sources, 24 fixtures plus metadata, and 113 packaged web
files match. ARM64/v2 signature-verified checkpoint:
`MaestroQuest-motion-batches-CA33D34F.apk`, SHA-256
`CA33D34F051AF3EAE79C69CA760E4920B3CA9DF0E89C3F17AD0C9EE022C182D6`.
It remains uninstalled. Next, bound the single-file model-import result fact for
maximum-size embedded motion lists too; its existing full list can exceed the
shared value budget. Other runtime, provider/device and Store gates remain open.

PC shared-model-import increment (2026-10-01): physical tools, generated book
fields and agent/program calls share one native file-choice/preview/acceptance
session. Object, Maestro, model-library and embedded-motion destinations preserve
exact asset identities and revisions; selection alone does not accept content.
Android model/archive pickers share one request/kind owner and retiring-worker
guard. Native tests cover real loaders, stale identities, failed saves, ownership,
new physical edits during acceptance, cancellation, temporary Keep and exact motion
IDs. Program object results authorize subsequent edits without charging selection
or library operations against the creation budget. PC checks pass 1,568 web tests
across 177 files, 29 focused final-capture checks, 377 EditMode and 292 PlayMode
tests (three optional private-model skips), and 59 Android browser tests. Chrome
sends exact selection/acceptance requests through generated fields; acknowledgements
replay native captures and are not provider/headset acceptance. Screenshots were
inspected. The full build helper exited successfully; 179 runtime and 83 test C#
sources, 24 fixtures plus metadata, and 113 packaged web files match.
ARM64/v2 signature-verified checkpoint: `MaestroQuest-model-import-69E4430E.apk`,
SHA-256 `69E4430E9D3534F8260E745D3ACE7D93BEBDDBECA29BBD66639DF41B652AF46B`.
It remains uninstalled. Shared batch imports and other release gates stay open.

PC shared-posing increment (2026-10-01): the physical joint handles, typed book
controls and agent/program calls share a versioned live pose session and the
same canonical joint clamps, imported retargeter and save/recovery path. Eleven
new native cases cover stale requests, held joints, failed saves, ownership,
lifecycle, recording interoperation and temporary Keep. Starting from pencil
mode preserves active strokes and never sends a global stop to unrelated actions.
PC checks pass 1,564 web tests across 176 files, 28 focused checks against the
final native capture, 372 EditMode and 283 PlayMode tests (three optional private-
model skips), and 55 Android browser tests. Chrome sends exact native start,
rotate and finish requests through generated fields; its acknowledgements replay
a native capture and are not provider/headset acceptance. Screenshots were
inspected. The full build helper exited successfully; 176 runtime and 82 test
C# sources, 24 fixtures plus metadata, and 113 packaged web files match.
ARM64/v2 signature-verified checkpoint: `MaestroQuest-pose-sessions-444E6F9F.apk`,
SHA-256 `444E6F9FCCA0BB4F1CE11B8AE60A0FB28CF2DD07F20EE3E763F242AF803CEBC9`.
It remains uninstalled. Shared picker/import and other release gates remain open.

PC pose-recovery increment (2026-10-01): a reproduced failed-write regression
could replace an unsaved manual pose when a new pose session started. Failed
poses now remain frozen for exact-revision retry or explicit discard; new
animation authoring, avatar selection and workspace boundaries wait for that
resolution. The solid tray has Save pose and Discard pose controls with wrapped
labels verified by rendered bounds and actual ray taps. Eight new native cases
cover lifecycle stops, Undo/Redo, changed targets, holds, temporary Keep and
imported-rig readback. PC verification passes 1,559 web tests across 175 files,
372 EditMode and 272 PlayMode tests (three optional private-model skips), and
55 Android browser tests. The full helper exited successfully; 174 runtime and
81 test C# sources, 24 fixture files plus metadata, and 113 packaged web files
match. ARM64/v2 signature-verified checkpoint:
`MaestroQuest-pose-retention-91C5A8B3.apk`, SHA-256
`91C5A8B3F911130DCBE8AA3752A9B2821B09380CCA85CE128ADDA95C1C859617`.
It remains uninstalled. Retained poses are in memory only; shared live posing
and device/provider/Store acceptance remain unfinished.

PC avatar-selection increment (2026-10-01): shared library discovery and exact-
revision model selection use the same prepare/save/apply path as physical Use
Maestro and Default. Failed, cancelled or stale preparation keeps the previous
avatar and saved record. Readback separates selected/displayed identities; long
values wrap inside the book page. See QUEST_AVATAR_SELECTION.md. PC verification
passes 1,559 web tests across 175 files plus 27 focused checks against the final
native capture, 372 EditMode and 264 PlayMode tests (three optional native skips),
and 55 Android browser tests. The full helper exited successfully. All 173 runtime
and 80 test C# sources, 24 fixture files plus metadata, and 113 packaged web files
match. ARM64/v2 signature-verified checkpoint: `MaestroQuest-avatar-selection-82080CAE.apk`,
SHA-256 `82080CAE72DD7569DF4064470DF50B429B3CA679CA4044DDAC6D186728E07A4B`.
It remains uninstalled; provider/device/Store acceptance is still open.

PC recording increment (2026-10-01): agent, programs and physical tools share
one recording session. Start returns immediately while native sampling continues;
finish saves one Undo without autoplay, and discard preserves the prior motion.
Failed saves retain frozen frames; retries require the same object revision and
room session. Retained takes block workspace boundaries. PC checks pass 1,555
web tests across 174 files, 370 EditMode and 257 PlayMode tests (three optional
native skips), plus 55 Android tests. The local Chrome book probe sends exact
start/finish requests against captured native acknowledgements. The full APK
helper exited successfully; 171 runtime and 78 test C# sources, 24 fixture files
plus metadata, and 113 packaged web files match the checked build. ARM64/v2
signature-verified checkpoint: `MaestroQuest-recording-sessions-67665749.apk`,
SHA-256 `67665749689FAAFA1257B93C161729FA436151ECD8CA7EF00C98ECCE756B670F`.
It remains uninstalled. Device/provider and Store release gates stay open.

Earlier PC increment (2026-09-30): calculated condition waits compose inspected facts
with existing typed expressions, debounce and explicit initial-state policies.
Programs, agent instructions and book controls use the same source; module linking
and declaration renames include condition inputs. Native tests observe actual
animation crossing a threshold without taking ownership or saving runtime changes.
PC checks pass 1,465 app tests, 231 EditMode and 138 PlayMode checks (three optional
native skips), plus 25 Android tests. The ARM64 APK is built, signature-verified
and uninstalled. See QUEST_EVENT_PROGRAMS.md for the bounded sampling and lifecycle
contract. Device/provider/store acceptance remains open.

Previous PC increment (2026-09-30): parameterized fact reads share native schemas,
typed program expressions, catalog inspection and visual input editing. Programs
and the agent can read current object positions without owning or editing them.
Stored records stay detached; missing targets remain unavailable. See
QUEST_EVENT_PROGRAMS.md. PC verification passes 1,458 app tests, 224 EditMode and
137 PlayMode checks (three optional native skips), plus 25 Android tests. The ARM64
development APK is built, signature-verified and uninstalled; device/provider/store
gates remain open.

Previous PC increment (2026-09-30): physical motion subscriptions share the native
catalog, event runtime and generated book controls. Programs can detect sustained
low linear/angular velocity and later motion, with explicit initial-state policy,
hysteresis and reset rules for ownership/physics/placement/sampling interruptions.
A real simulated ball lands, triggers a colour change and wakes a second wait when
thrown again. Scalar readback also preserves native primitive types. See
QUEST_EVENT_PROGRAMS.md. PC verification passes 1,451 app tests, 218 EditMode and
136 PlayMode checks (three optional native skips), plus 25 Android tests. The ARM64
development APK is built, signature-verified and uninstalled. Device/provider/store
gates remain open.

Previous PC increment (2026-09-30): native reusable-module publication and removal
share the capability/receipt system; catalog search inspects immutable exact versions.
The book publishes exports, wires signals and explicitly imports or replaces a pin
in a draft. Saved and running copies survive later publication or library deletion.
See QUEST_PROGRAM_MODULES.md. PC checks pass 1,449 app tests, 207 EditMode and 135
PlayMode tests (three optional native skips), plus 25 Android tests. The ARM64
development APK is built, signature-verified and uninstalled; real-provider/device/
store acceptance remains open.

Previous PC increment (2026-09-30): the optional book now edits typed program state
and named signal declarations without JSON. Atomic scoped renames preserve references;
invalid/removal/type conflicts and stale drafts preserve valid source. The same human-
authored source runs in Unity, where two behaviours exchange signals and retain typed
state without saving runtime values. PC verification passes 1,401 app tests, 195
EditMode, 133 PlayMode and 25 Android tests, with three optional native skips. See
QUEST_EVENT_PROGRAMS.md. Shared versioned libraries and device/provider/store gates
remain open; no installation is included.

Previous PC increment (2026-09-30): the book and agent share typed lists/records,
function signatures and local declarations in the same native program. Immutable
values, structural typing, data operations, bounded execution and exact object
authority support collecting creation results and passing them between functions.
PC verification passes 1,392 app tests, 195 EditMode, 132 PlayMode and 25 Android
tests, with three optional private-asset skips. The development APK is built and
uninstalled. See QUEST_PROGRAM_DATA.md; shared libraries, remaining declaration
controls and real-provider/headset/store acceptance remain open.

Previous PC increment (2026-09-30): parameterized native proximity waits let human
and agent-authored programs react when two objects enter/leave a distance boundary.
Schema-generated book inputs, computed arguments, typed fields and the existing
queue/scheduler share one contract. Origin distance, baseline, hysteresis, bounded
sampling and cancellation are explicit. Real Unity movement and browser editing
checks pass: 1,360 app tests, 186 EditMode, 131 PlayMode and 25 Android tests.
The verified development APK remains uninstalled. See QUEST_EVENT_PROGRAMS.md.

Previous PC increment (2026-09-30): book and agent share paged discovery of native
events and facts alongside actions. Exact schemas are inspected on demand;
selected facts refresh through the same reader used by programs. Unavailable is
separate from false/zero/empty values, and browsing never starts or interrupts
anything. PC verification passes 1,357 app tests, 181 EditMode and 130 PlayMode
tests, plus 25 Android bridge tests. The verified development APK is built but
uninstalled. Real-provider and headset acceptance remain pending. See
QUEST_EVENT_PROGRAMS.md for query semantics and evidence.

Previous PC increment (2026-09-30): typed native event fields let the book and agent
compose collision reactions through the same canonical programs. Actual PhysX
contact entry carries counterpart, speed and world position; source filters,
bounded dispatch and resource authority remain enforced. See
QUEST_EVENT_PROGRAMS.md. PC verification passes 1,351 app tests, 180 Unity
EditMode and 129 PlayMode tests, plus 25 Android bridge tests. The verified
development APK is built but uninstalled. Device and real-provider acceptance
remain pending.

Latest verified ownership increment (2026-09-30): shared native ownership integrates programs,
direct avatar controls, animation authoring, grips and ambient tutor activity.
Recordings cooperate with physical grabs; interrupted programs stay cancelled.
The optional book workspace and agent see the same owner/claim evidence. See
QUEST_ROOM_OWNERSHIP.md for scope, policy, verification and remaining actor work.

Prior PC increment (2026-09-27): the optional book editor supports direct
insertion into nested branches and typed value/expression controls. The same
human-authored program is verified in Chrome and executed by Unity for both
branches; source editing remains available. See QUEST_BEHAVIOUR_PROGRAMS.md.

Current architecture: the original app owns chat/Live handoff, Gemini access
and durable task journals. Canonical behaviour programs, the shared native
catalog (37 actions, ten events and 18 facts), one scheduler and native
receipts serve human controls and agent calls. Version-3 programs retain typed
session state across event/timer waits. Creation results compose with animation,
physics and durable object edits; per-channel ownership permits supported
avatar movement/gesture/gaze composition.

Prototype behaviour migration was retired with the owner's approval:
behaviours.v2.json starts a fresh development collection when installed, while
older files, room creations and existing app chat/backups remain preserved.
Earlier delivery entries describe their historical checkpoints and may refer
to retired formats. No reset or installation occurred in this increment.

PR [#248](https://github.com/RONITERVO/MaestroTutor/pull/248) remains a draft.
Its description records the exact latest pushed head, verification and APK.
PC verification does not establish real-provider or headset acceptance.
Device work remains on hold; the full v1 and Store release are incomplete.

PC-only motion discovery update (2026-09-27): the original Maestro agent can now
search the native saved animation library by name/tag with compatible-rig filtering,
favorites, archives and shared book pagination. Returned stable IDs feed existing
rules/programs. Search does not select, play or modify the room. See
QUEST_MOTION_DISCOVERY.md for scope and evidence; provider and headset acceptance
remain open.

PC-only walking preference update (2026-09-27): `avatarWalk.v1` connects compatible
library motions to the same manual/agent saved walk selection, including one-batch
Undo, stale-edit rejection, included-walk restoration and availability read-back.
Native tests observe actual leg motion after an agent assignment. This does not
resolve the reported on-headset default gait issue. See QUEST_AVATAR_MOVEMENT.md.

PC-only activity-profile update (2026-09-27): `avatarActivities.v1` adds shared
chat/book assignment of idle, listening, thinking and speaking motions. Multi-role
changes save atomically with a separate assignment Undo; both entry points check
the observed profile revision. A conflicting agent edit preserves the book draft.
Native tests cover actual rig motion and priority after assignment. See
QUEST_AVATAR_ACTIVITIES.md. This remains uninstalled and needs provider/device QA.

Earlier implementation update (2026-09-26): validated Mixamo/Unity-named GLB
humanoids can replace Maestro, including the user's actual Meshy export.
Gaze and following use the shared pose rig and scanned-room navigation, with
physical controls and visual-rule actions. 33 EditMode and 32 PlayMode checks
pass with the two optional private-model tests enabled. See QUEST_CUSTOM_AVATARS.md
and QUEST_AVATAR_MOVEMENT.md. Headset acceptance of these additions, independent
editable locomotion bindings and all remaining release gates are still open.
Development APK `1EAEF7DA` is now installed on Quest 3 with saved room data
preserved; its build runs 63 required Unity and 15 native Android tests. The
user's Meshy file is available separately in Downloads/Maestro for acceptance.
Follow-up checkpoint `08F340EF` is installed with saved room/model data retained.
It fixes the file-picker starting location and passes 64 required Unity plus
15 native checks. The user confirmed Meshy import/replacement and following
translation; reported stationary feet remain an unresolved headset gait issue.
See QUEST_DEVICE_QA.md for the exact acceptance boundaries and evidence.

PC-only motion-library update (2026-09-26): reusable same-rig extraction,
versioned storage, stable identities/provenance, bounded cached playback,
walking assignments and visual-rule actions are implemented. A searchable
book spread now supports preview, metadata editing, filters, source terms and
walking/rule assignment, with preserved chat state and bounded native messages.
See QUEST_ANIMATION_LIBRARY.md for exact boundaries and QUEST_DEVICE_QA.md for
the latest packaged checkpoint. Per-avatar tutor-state profiles and a bounded pose transition are implemented;
full-model storage management and large-library headset QA remain open. The user is sleeping while Quest charges;
device queries/installations remain on hold.

PC-only prop update (2026-09-26): Maestro animation steps can carry one fitted
creation and return, drop or throw it through the existing rule triggers. The
physical Props tab adds hand, fit and release controls without enlarging the
tray or covering book pages. Rules migrate to v3 while retaining older files.
See QUEST_AVATAR_PROPS.md for collision/ownership limits. Hardware acceptance,
contact IK and the remaining release gates are still open.

PC-only library maintenance update (2026-09-26): the book supports archive and
restore, paged usage explanations, protected local-download removal and optional
confirmed forgetting of unused removed entries. Exact reimport retains IDs unless
the user deliberately forgot them. Retained references include room/rule/profile
history and recovery files. See QUEST_MOTION_MAINTENANCE.md. No headset access or
new hardware acceptance is implied; the full Store release remains incomplete.

PC-only batch import update (2026-09-26): the physical import tray selects up to
128 motion exports, confirms before saving, shows per-file outcomes, supports
stop/resume and retry, and preserves completed imports through stable identities.
It reads one source at a time without creating hidden avatar models. See
QUEST_BATCH_IMPORTS.md. Quest multiselect and large-collection performance remain
hardware acceptance gates; device work stays on hold while the user sleeps.

Agent-first steering (2026-09-26): spoken and typed requests become the primary
interface, using existing Maestro account access and BYOK. Native versioned JSON
recipes create editable objects and simple animated characters. Users can
optionally inspect and co-edit the same data through a hierarchy, Scratch-like
behaviour blocks and an animation timeline. See QUEST_AGENT_WORKSPACE.md for
implemented foundations, architectural decisions and remaining work. This does
not imply hands-free/Live voice, complete behaviour-block authoring or Store release
are done. The initial object/part and recipe-key workspace is now packaged in
PC-verified checkpoint 71987229; hardware acceptance remains open.

Shared-action steering (2026-09-26): the user asked for human/agent parity and
development-agent testing comparable to StateBeats, Scetch-War and StateWork.
QUEST_SHARED_ACTIONS.md records inspected examples, current gaps and the proposed
contract. Build coverage through existing domain services, one authoritative
implementation per domain, shared observations and honest operation receipts.
Test semantic outcomes and actual input/rendering independently.

PC-only behaviour workspace update (2026-09-26): 7EC419D6 adds shared native rule
operations, stable step IDs, book blocks and agent access to bounded sequences,
state/object triggers and physical buttons. Recipe animations can run from those
same events/buttons. QUEST_BEHAVIOUR_WORKSPACE.md records exact coverage; full
agent parity, Live voice and hardware/store gates remain open.

PC-only Live transport foundation (2026-09-26): a closed, bounded room function
protocol now supports pending-ID matching, cancellation and tool usage evidence
through the managed gateway. The issuer switch defaults off; client/native voice
execution and real-provider continuation accounting are still unverified. See
QUEST_LIVE_ROOM_ACTIONS.md. No new APK, deployment or headset test is implied.

Unified-agent direction (2026-09-26): the original app owns Gemini and starts room
tasks through its chat/suggestion tool mechanism. Direct Live room function calling
is optional and remains disabled. The room task runner is separated from narration,
and request-owned native/client model cancellation is implemented. Text-chat tool dispatch,
original context capture, durable task receipts and nonblocking header activity now
work in local tests. Stop releases task activity even during a stalled model request;
accepted managed requests still finish on the server for exact usage settlement.
Live/observer transcript and connection text-context capture now feeds that same
verified tool dispatcher. Bounded original sent PCM/JPEG context now follows eligible
handoffs through the shared managed/BYOK client, with incomplete-input failures
before room actions. Fresh task results now use ordinary chat TTS after active
speech yields, with shared listening ownership, Stop and no replay on reload.
Database v9 atomically saves compact task results with their journals and repairs
chat on history load while preserving deletion and preventing replay. Database v10
adds validated staging and atomic full task backup/import, retaining original
media and receipts as read-only history without resuming actions. PC browser
checks cover corruption and storage-failure rollback; Quest backup acceptance
remains pending.
Conversational Stop/revision/continuation now uses the same verified handoff,
with durable task links and no saved-command replay. Database v11 preserves
compact task scope and hidden state. Real-provider intent/latency, explicit server
cancellation and provider/headset acceptance (including audible results) remain open. See QUEST_UNIFIED_AGENT.md.

## Accepted product scope

- Native Unity mixed-reality app for a long-term Meta Quest Store release.
- Current priority: actual Unity interaction and Quest mixed reality. Keep the
  current draft artwork; further visual iterations can follow functional work.
- The same Maestro tutor implementation and managed services, with a versioned
  native bridge. Preserve the familiar language, chat, suggestions, audio and
  artifact behavior; do not fork prompts or implement a second tutor in C#.
- Pencil contours and opaque watercolor based on the user's Scetch-War project.
  All included models and user creations share this Penzil-inspired spatial
  drawing style; see QUEST_ART_DIRECTION.md (user steering 2026-09-25).
- Animated physical book with two selectable layouts using the same components
  and session. Default conversation layout: familiar phone chat on the right,
  preceding chat on the left, artifacts inline, older history on earlier pages.
  Optional practice layout: chat left, selected/latest artifact right, clickable
  artifact previews in chat. Retain the message-identity bookmark as a ribbon.
- Both page surfaces are filled with content. Add no book headers, counters,
  toolbars or layout menus to the page image. New interactions outside the pages
  must be physical 3D objects, not floating flat panels (user steering 2026-09-25).
- Included full-body animated Maestro, based on the existing Maestro persona.
  Support idle, attentive listening, speaking, greeting and pointing; page turns
  and appearance animations respect reduced-motion settings.
  On 2026-09-26 the user selected their forthcoming Meshy character as the future
  included default and expects a library of several hundred animations. Keep the
  current included draft until the final model/motions are supplied and verified.
  Add separate animation packs, bounded on-demand loading and per-avatar clip
  assignments for locomotion, tutor states and visual rules. The current 32-clip
  per-model importer and common retargeted gestures do not fulfill that library.
- User-created room items: in-app editor and GLB/VRM imports first, as selected
  by the user on 2026-09-25. Include playback of compatible imported animations.
  External AI mesh-generation services and public content sharing remain outside
  this first release. The newer agent-first scope includes native procedural
  creation from validated recipes, including simple animated characters.
- In-app animation authoring: record object movement, save/edit keyframes, choose
  avatar gestures, and pose arms, legs and head with 3D handles. Joint posing is
  explicitly included by the user (2026-09-25). Playables supplies playback;
  authoring, validation, persistence and physical controls belong to this app.
- Custom Maestro avatars and visual event/condition/action rules. Support tutor
  activity gestures, gaze/head turn and following/walking for compatible rigs.
  User locomotion and avatar movement have independent editable bindings; user
  locomotion is opt-in. Solid user-created action buttons can attach to a
  controller and be operated by the opposite controller. Preserve system input
  reservations and an accessible recovery path. User chose both movement modes
  and visual rules first (2026-09-25); arbitrary user code is outside v1.
  Reusable action sequences may have multiple triggers: a controller-mounted
  button, a web tutor state transition or a VR interaction. Recorded animations
  are actions in the same system. Define once/loop and interruption behaviour;
  future event sources join the same catalog without duplicating user sequences.
- Default book and avatar are included and usable without downloading assets.
- MR physics: loose rigid items obey gravity, react to grabs/throws and collide
  with scanned floors, walls and furniture. Include animation-to-physics release
  actions and scan/permission/tracking lifecycle handling. The user explicitly
  excluded hair and cloth interaction physics in their later steering on
  2026-09-25. See QUEST_MODEL_IMPORTS.md for the implementation boundaries.
- Hand/controller interaction, comfortable seated/standing placement, save/load,
  recovery, bounded content/resource use, import validation, and accessible text.

## Repository and development ownership

Worktree: `C:/Users/ronit/.codex/worktrees/maestro-quest-v1/MaestroTutor`.
Branch: `codex/maestro-quest-v1`.
Unity project: `unity/MaestroQuest`. Shared web integration belongs in
`src/platform/quest` and minimal feature presentation adapters.
The existing web/Capacitor application remains supported.

## Delivery sequence

1. Establish supported Unity/Android toolchain, pinned package provenance and
   batch compile/test/build commands; evaluate a maintained browser texture path.
2. Build the physical book, illustrated materials and desktop simulation scene.
   Prove an actual Maestro page and interactive artifact on the book.
3. Add the shared-runtime bridge, page layout, artifact routing and bookmark
   behavior with semantic parity tests and app lifecycle coordination.
4. Ship the included rigged avatar and animations; bind actual tutor activity and
   playback to its visual state without opening extra model/audio sessions.
5. Implement scene object creation, validated import, animation control, editing,
   persistence, undo and recovery. Reject executable content in model packages.
6. Integrate Quest passthrough, placement, hands/controllers, native capabilities,
   authentication/attestation and managed access. Verify physical device behavior.
7. Complete release gates, long sessions, performance, store metadata and policy
   evidence, reproducible signed release candidate and submission preparation.

Every stage must record implemented behavior, automated evidence and remaining
hardware checks. A scaffold or editor demonstration does not complete the goal.

## Release evidence still required

- User connected and authorized Quest 3 for development. The development APK
  installs and launches; see QUEST_DEVICE_QA.md for evidence and remaining checks.
- The owner created Meta app 1763835394893209. Release package com.maestro.quest
  is selected subject to availability; signing, dashboard configuration and store
  setup remain required external release gates.
- Meta application identity, signing configuration, organization access and
  production authentication/attestation must be validated before submission.
- Unity 6000.3.24f1 and its Android build support are installed. Licensing works.
  The original deeply nested installation omitted long-path package metadata;
  reinstalling at `D:/Tools/Unity/6000.3.24f1` repaired it. A physical short-path
  build mirror avoids a separate package-cache rename failure in the worktree.
- Browser texture transport now displays the real bundled Maestro onboarding on
  Quest 3. Live sessions, interactive artifacts and long-session recovery remain.
- The server's existing Stripe-only purchase route must not be exposed in a Quest
  purchase flow without reviewing the current Meta platform policy and selecting
  the supported store approach. Backend credit authority remains shared.
- Imported models/animations and default artwork need license/provenance records.
- Current chat storage is local IndexedDB; cross-device chat sync is not implied
  by using the same backend and must not be advertised without implementation.

## Verified development milestone, 2026-09-25

- Shared chat/book presentation: 121 targeted tests pass; production web build
  succeeds. Conversation and practice fixtures have been inspected in-browser.
- ARM64 browser transport: Gradle release AAR and Android lint pass (zero errors).
- Unity project compiles and configures OpenXR, scene and included clips.
  Twenty-three EditMode and ten PlayMode tests pass, including actual imported skeleton
  movement, gesture ownership, tracked grabbing, two-hand scale limits and reset.
- Actual Unity desktop renders cover the physical book using a browser-captured
  page texture, the shared pencil shader, avatar views and sampled gestures.
- The user's later cartoon reference now defines the character direction.
  Current geometry is a development draft, not approved release artwork.
- ARM64 development APK builds, its signature verifies, and it runs on Quest 3.
  The headset capture shows passthrough, book pages, full-body avatar, physical
  page controls, pointer and three movable starter items. A short startup sample
  ran near 72 FPS; this is not a sustained performance or comfort qualification.
- Grip movement, two-hand scaling, B/Y room recovery and Meta aim pinch routing
  are implemented. The user confirmed controller movement, page tapping and
  recovery on Quest 3. Hand-only usability and physical two-hand scaling need QA.
- The physical room editor supports shapes, spatial pencil strokes, paint,
  duplicate, erase, bounded undo/redo and validated save/load with a backup.
  The user confirmed creation, drawing, editing and readable controls. Device
  capture confirms saved shapes/strokes return after a process restart. Included
  book/avatar cannot be erased. Saved poses are not physical-room anchors.
- Native pause/resume now coordinates shared media and provider shutdown, waits
  for acknowledgment and recreates the WebView if shutdown stalls. Quest settings
  interruption and fault recovery pass on the device; audio waits for the solid
  resume bell. An additional 98 targeted web tests cover interruption behavior
  and nearby regressions. Active paid/media sessions still need device QA.
- Animation authoring now supports object recording, keyframe editing, duration,
  loop/preview and Maestro joint handles. One bounded take and an optional static
  pose per object are included in save/load and undo. ScriptPlayable evaluation,
  actual XRI-driven head posing and interrupted recordings have automated tests.
  The user confirms wrist posing and playback; the remaining physical checks are
  open. Custom avatar switching is implemented in the later update below.
  Following/walking, locomotion and editable input bindings remain accepted work.
- Visual rules now support reusable multi-step recordings, gestures and waits;
  tutor activity transitions and room tap/grab/release events; conditions,
  repeat/loop and restart/ignore/queue policies. Solid buttons can be placed in
  the room or mounted on either controller. Opposite-controller activation,
  tracked placement, grip interruption, browser pause, persistence and recovery
  have automated tests. These new controls still require headset acceptance.
  The builder currently uses numbered action names and preset durations; richer
  editing, native naming/keyboard support and additional actions remain work.
- Model imports, native permission acceptance/authentication, production attestation,
  billing, room anchoring and full device/store QA remain.

## Native input development update

Microphone requests now prompt for Android consent on use and reject stale
callbacks, opaque/remote origins and unrequested resources. App file inputs can
request Android's document picker through a one-use top-document gesture check.
Only selected external-provider streams are copied, with file/session/count
limits, worker cancellation and a private read-only provider for WebView. Native
dialog interruption retains the existing explicit audio-resume policy. A solid
notice token beside the book explains permission or picker outcomes.

Twelve Android tests pass for consent/cancellation, callback ownership, actual
stream copying, private-source rejection and budgets. Fifteen web tests pass,
and a real Chrome smoke check confirms trusted hidden-input selection works while
synthetic reuse and iframe selection are denied. Unity's 33 tests and a desktop
notice render also pass. These are computer-side checks; no new build has been
installed while the headset charges. Quest picker availability, actual WebView
file reads, first-use microphone flow and ongoing paid/media sessions remain
hardware checks. GLB/VRM loading, IME, native authentication and the remaining
release scope are still incomplete.

## Primary references checked 2026-09-25

The runtime-import development update is recorded in QUEST_MODEL_IMPORTS.md.
GLB/VRM room-object importing and embedded-clip controls are now implemented for
the supported subset. Later updates below cover custom Maestro replacement and
physical simulation; springs remain disabled and import Quest acceptance remains.
Earlier milestone notes describe their checkpoints.

- https://developers.meta.com/horizon/documentation/unity/unity-development-requirements/
- https://developers.meta.com/horizon/documentation/unity/unity-project-setup/
- https://developers.meta.com/horizon/resources/publish-quest-req/
- https://developers.meta.com/horizon/resources/publish-mobile-manifest/
- https://developers.meta.com/horizon/policy/app-policies/
- https://developers.meta.com/horizon/policy/content-guidelines/
- https://unity.com/releases/editor/whats-new/6000.3.24f1

Scetch-War references: `docs/PENCIL_ART.md`, `src/mr/book-paper.js`,
`src/mr/pencil-geometry.js`, `src/mr/pencil-palette.js`, `src/mr/watercolor.js`.

## Rigid physics development update

The current scope and implementation are documented in QUEST_ROOM_PHYSICS.md.
Hair/cloth interaction physics are excluded. MRUK scene colliders, explicit
gravity startup, physical item modes, mass/collider controls, controller contact,
live-depth surface placement and recorded-motion release are implemented. The
user confirmed a basic physics test on Quest 3. Their report of unreachable tools
after scanning led to separate selection/scene-collision layers and a palm-carried
3D Recall control. The user subsequently confirmed palm recovery and retrieving
trays beyond scanned walls on Quest 3. The source passes 30 EditMode, 21 PlayMode
and 15 native Android tests and has a verified installed APK checkpoint.
Maestro-hand
attachment for throwing, persistent spatial anchors, custom Maestro headset QA,
locomotion/bindings and the remaining release gates are still open.

## Custom Maestro development update (2026-09-26)

Use Maestro and Default on the solid import tray now switch the tutor between a
compatible private VRM and the included character. The common 17-channel rig
drives imported humanoid gestures and poses while retaining recordings, rules,
room identity and undo/save/load. A valid humanoid with 15 required joints is
needed; chest and neck are optional. See QUEST_CUSTOM_AVATARS.md for limitations.

All 31 EditMode and 24 PlayMode tests passed, including actual skinned-vertex
movement, pose/gesture mapping, model switching, persistence and cancellation.
One private VRM also imported and rendered Idle, Greeting and Pointing through
the real retargeter in Unity. The full build and 15 native Android tests also
pass. Development checkpoint CA09C56E is installed on Quest 3 after a verified
room backup; startup succeeds. This does not establish custom-avatar headset
acceptance or sustained performance. The user is testing import/switch/pose and
Default/Undo; see QUEST_DEVICE_QA.md for the exact artifact and remaining checks.

### Animation library direction, 2026-09-26

The user is collecting Meshy exports in three movement categories and wants an
expandable library plus user imports. The current read-only 96-file inventory
finds repeated geometry/rig and about 8.9 MiB of unique animation accessors in
844.4 MiB of full exports. See QUEST_ANIMATION_LIBRARY.md for the motion-pack,
role mapping, stable identity, loading-budget and contact/travel design. Current
embedded-clip controls are a first step, not that complete library. Do not mark
hundreds-of-clips support or authored travel complete from individual-file tests.

The headset is charging and the user is sleeping. Keep device work on hold until
they return and reconnect it. Their reported PC crash involved the phone USB
network adapter; no Quest cause was established. Continue PC-side work.

### Saved-motion actions and walking

The reusable-motion foundation now supports stable-ID visual-rule actions for
Maestro and matching imported objects, controller/state/VR event triggering,
and persisted walking selections. Asynchronous loading reserves targets without
consuming playback time; interruption prevents late playback. Room/rule v2
migration retains v1 originals and old exact-model clip references. Broader role profiles, blending, explicit
travel/contact policies, deletion/relinking and Quest library profiling remain.
See QUEST_ANIMATION_LIBRARY.md and QUEST_DEVICE_QA.md for verification evidence.

### Independent movement controls (PC development)

The physical controller tray now provides separate user/avatar stick bindings,
explicit enablement, dead-zone and user-speed settings, and persistent X/A/stick
click assignment to visual-rule sequences or snap turns. Maestro uses its shared
navigation and gait path. User movement is confined to an explicit virtual view;
MR return restores tracking origin and pauses physics for alignment review.
See QUEST_CONTROLLER_MOVEMENT.md for boundaries and remaining headset checks.
General core-button remapping, alternate locomotion accessibility and sustained
comfort/performance acceptance remain open. The device is still on charging hold.

### Per-avatar tutor-state motions (PC development)

Idle, Listening, Thinking and Speaking can each use up to four compatible saved
motions, configured in the full-page book library. Weights, speed, reuse gaps,
looping and per-avatar assignment Undo/Redo are persisted separately from room
placements and rules. Automatic playback follows the shared tutor state and
yields to explicit ownership. Canonical body transitions blend; broader layers,
contact/travel policy and hardware acceptance remain. Android snapshot results
are invalidated across suspension/navigation to prevent stale activity reuse.
See QUEST_AVATAR_ACTIVITIES.md. Device work remains on charging hold.


## Approved catalog and event-runtime direction (2026-09-27)

The owner authorizes simplifying development-only formats and resetting their
prototype saves if needed. The accepted design, review qualifications, tradeoffs
and staged acceptance are in [QUEST_CAPABILITY_ARCHITECTURE.md](QUEST_CAPABILITY_ARCHITECTURE.md).
The first native vocabulary registry now generates web action/event labels, fact
types, typed argument schemas and prompt signatures, with drift checks. Saved
programs now invoke named, versioned capabilities. Paged discovery and live availability
share native queries with the book and agent; see [the contract](QUEST_CAPABILITY_DISCOVERY.md).
Full room-command coverage, timers/state machines and
per-channel ownership remain follow-up work. No development data was reset here.


## Canonical behaviour programs (2026-09-27)

The dual saved step-list/program representation and separate linear scheduling
path have been removed. Physical controls, motion-library assignment and simple
book editing derive action blocks from the same program used by the agent and
function editor. Stable node identities, props, exact motion IDs, revision checks,
Undo, triggers, buttons, Stop and last-good recovery are retained. Complex programs
stay in the function editor and cannot be flattened by a simple assignment.

The new `behaviours.v2.json` format starts an empty development behaviour collection,
including its triggers/buttons; retired rule files are preserved without migration.
This applies when the new build is installed. No headset installation or reset was
performed during this PC checkpoint. Room creations, models/motions, source
collections and released web chat/backup formats remain intact. See
[the program contract](QUEST_BEHAVIOUR_PROGRAMS.md) for boundaries and verification.

## One-off action execution (2026-09-27)

The book and original-app agent can now start, inspect and cancel a named native
action without saving a behaviour. One-off runs use the same program machine,
scheduler, ownership, loading and Stop/grab paths. They require observed target
and prop revisions, reject busy resources, and expose exact run phases rather
than claiming completion on acknowledgement. See [contract and evidence](QUEST_ONE_OFF_ACTIONS.md).
Earlier pending-transient-invocation notes describe prior checkpoints. Persistent
event programs, timers, channel blending and full release acceptance remain open.


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


PC programmable-physics checkpoint (2026-09-27): named push and stop-motion
capabilities share native rigid-body execution with one-off calls, saved programs,
event triggers and buttons. Instant effects report actual completion and do not
renew work/causal budgets. See [semantics and boundaries](QUEST_PROGRAM_PHYSICS.md).
The full v1 goal and device/store acceptance remain open.


PC creation-result checkpoint (2026-09-27): primitive creation now returns a typed
object ID to one-off receipts and version-3 program locals. Human blocks and the
agent use the same result binding, with native authority checks, room Undo and
no duplicate creation on receipt replay. See [contract and boundaries](QUEST_CREATION_RESULTS.md).
This does not complete all creation/import capabilities or Quest/store acceptance.

PC recipe-action checkpoint (2026-09-27): editable assemblies and animation tracks
now use the named creation/result path. Native examples and bounded array schemas
let human and agent author the same recipe, then animate its returned ID.
See [recipe contract, evidence and limits](QUEST_RECIPE_ACTIONS.md).
Hardware performance and complete v1/store acceptance remain open.

PC object-edit checkpoint (2026-09-27): named placement, scale, tint and deletion
actions now share durable native editing and can consume newly created IDs.
Painting preserves physical motion; explicit placement/resizing resets velocity.
Unrelated runs continue, and completed deletion replay cannot delete an Undo
restoration. See [object-edit semantics and evidence](QUEST_OBJECT_EDIT_ACTIONS.md).
Quest acceptance, broader operation coverage and store release remain open.


PC compatibility checkpoint (2026-09-27): a confirmed PR review finding is fixed
across saved-program storage, the native scheduler, physical buttons, observations
and the book/agent editor. One unavailable program preserves its source and
diagnostic without locking unrelated behaviours. New edits remain strict;
Undo and unknown motion references stay protected. See
[compatibility semantics](QUEST_BEHAVIOUR_PROGRAMS.md#per-program-compatibility-isolation-2026-09-27).
PC verification covers 1,296 app tests, 133 EditMode and 95 PlayMode tests, with
three optional private-model skips. Chrome repairs a real native observation.
The latest build remains uninstalled. In-app damaged-receipt recovery, modular
native capability dispatch, broader action coverage and all remaining v1
hardware/provider/store acceptance gates remain open.


PC action-history recovery checkpoint (2026-09-27): users can explicitly stop
one-off actions and recover damaged/incompatible receipts from the book or an
explicit new agent request. Exact old evidence is archived before a fresh journal
is committed; interrupted recovery resumes safely, and old starts never replay.
Saved behaviours and room objects remain unchanged. Native motion tests and
Chrome bridge checks cover the same operation. Verification covers 1,301 app,
146 EditMode and 96 PlayMode tests, with three optional private-model skips.
See [recovery semantics and remaining scope](QUEST_ACTION_RECOVERY.md).
Archive export/maintenance controls, device power-loss/latency acceptance, native
capability modularization and all remaining v1/provider/headset/store gates remain
open. This checkpoint is not installed on the headset.


PC native-module checkpoint (2026-09-28): named calls now flow directly through
the interpreter, scheduler and shared runtime. All nineteen modules own validation,
readiness, claims and operation lifetimes. Object rotation proves a new capability
can use the existing editor, receipts and generated book fields without enum or
scheduler changes. Animation modules now own graph, clip, asynchronous lease,
prop and channel cleanup directly. Specialized physical-editor adapters, vocabulary review
and the other release gates remain open.
See [module contract and evidence](QUEST_CAPABILITY_MODULES.md). This development
checkpoint does not install an APK or reset user data.

The physical behaviour tray now provides schema-driven quick edits for every
registered capability, including named-only rotation, with one Apply/Undo edit,
stale-draft protection and direct opening of the same program in the book.
Text, expressions and detailed structure stay in the existing full book editor.
See [native quick edits](QUEST_NATIVE_QUICK_EDITS.md). Updated layout acceptance
on Quest is pending while device work remains on hold.


Typed animation checkpoint (2026-09-28): one animation.play action exposes
recording, built-in gesture, embedded clip, exact library motion and recipe
sources through a shared discriminated schema. The six supported source/channel
combinations retain their native lifetimes and selected-channel claims. Book and
physical controls use the same typed choices; nested scalar expressions retain
resource authority and exact source identity. Existing prototype IDs become
unavailable preserved source rather than being silently reinterpreted.
See [animation vocabulary](QUEST_ANIMATION_VOCABULARY.md). Remaining vocabulary,
runtime/grouped commits, arbitration, world subscriptions/libraries and all
hardware/provider/store acceptance gates remain open. Device work is on hold.


Typed creation checkpoint (2026-09-28): object.create combines primitive and recipe
kinds with complete native examples, generated book/tray choices, common-field
preservation and unchanged exact output/save/Undo behavior. Private editor
projections are now generated descriptors. The catalog has thirteen public
actions. See [contract, compatibility and evidence](QUEST_CREATION_VOCABULARY.md).
Spatial consolidation and remaining runtime/release work are still open.


Temporary-room storage foundation (2026-09-28): an explicit native editor fork
isolates live changes from saved state, including physics/autosave/lifecycle
paths. A worker saves a captured snapshot; its delta becomes one saved Undo and
later edits remain temporary. Discard restores the last saved base without
replaying motion. This is not yet exposed in the book or catalog: shared async
completion/receipts, visible session scope and controls are the next integration
step. Ordinary creation defaults remain saved. See
[semantics and integration gates](QUEST_TEMPORARY_ROOM.md). No device operation,
installed data reset or service/store deployment is part of this checkpoint.


Temporary-room public integration (2026-09-28): `room.session` now exposes explicit
Begin/Keep/Discard through the same catalog, scheduler and durable execution
receipts used by the original-app agent, canonical programs, book and solid 3D
tray. Real asynchronous completion, exact scope/save identities, quiet-room
boundaries and honest Stop outcomes protect grouped snapshots. Ordinary creation
remains saved. The catalog now has fourteen actions, seven events and four facts.
PC checks pass: 1,345 app, 166 EditMode, 120 PlayMode and 25 Android tests; three
optional native tests remain skipped. The new development APK is built but not
installed. Chrome replays actual native observations; its acknowledgements alone
are simulated. See [public semantics and remaining gates](QUEST_TEMPORARY_ROOM.md).
Begin baseline I/O remains synchronous; headset performance/durability acceptance,
real-provider journeys and the broader v1/store gates remain open.


Asynchronous room-start checkpoint (2026-09-28): Begin now captures a detached
baseline and immediately isolates later edits while its worker waits for any
older autosave and writes the base. The native result stays preparing until real
completion. Failure/cancellation retains the temporary fork for explicit Keep or
Discard; Discard preserves pre-existing unsaved state and original Undo. The book
shows starting/ready and baseline-failure recovery. Native checks pass 166 EditMode
and 125 PlayMode tests (three optional skips). Normal baseline writes no longer
wait on Unity's main thread; snapshot capture, receipt/ordinary-save I/O and
pause/quit waits still need device profiling/acceptance. Full v1 remains open.


### 2026-09-30: human/agent function-authoring parity

The book now exposes typed function signatures and local declarations as visual
controls. Scoped renames, parameter reordering and call updates produce one validated
canonical program; invalid/stale drafts preserve the previous definition. Return
expressions and call arguments use the existing typed editor. Calculated wait labels
show their variable/expression. Shared fixtures verify browser-authored source in
Unity. This closes function-definition authoring parity within a program; shared
libraries, records/collections and the other release gates remain unfinished.
Headset work remains on hold; no installed data or source assets were changed.

### 2026-09-30: typed collection and record parity

The same canonical program now carries bounded typed lists/records through locals,
session state, functions and native observations. The book exposes type/literal/data
expression controls; the agent uses the same format and limits. Immutable copies,
scalar projections into native actions and unchanged object authority are tested.
Actual native creation/paint results are replayed in the book. See
[the structured-value contract](QUEST_PROGRAM_DATA.md). PC checks pass 1,392 app,
195 EditMode, 132 PlayMode and 25 Android tests (three optional native skips).
Shared libraries, state/event declaration controls and the other v1 gates remain
open. The new development APK is built but not installed; device work stays on hold.


PC managed-module library checkpoint (2026-09-30): native immutable publication and
removal now use the shared capability/receipt system; bounded module discovery uses
the existing catalog. The book publishes chosen exports, inspects exact versions,
wires signals and explicitly imports/replaces a pin in the caller draft. Saved copies
continue after source publication or library deletion. Storage/validation runs off
thread, isolated damaged entries remain preserved, and motion retention includes
saved modules. See [module library contract](QUEST_PROGRAM_MODULES.md). Installation,
real-provider/device acceptance, library backup/export and the wider v1 release gates
remain open; this checkpoint does not establish Quest Store readiness.


### 2026-09-30: native book chat export

The familiar Save All / Save Chat controls now have an Android Downloads writer
instead of depending on the desktop save-file picker. The shared adapter streams
bounded UTF-8 chunks with backpressure. Native code polls only the trusted book
origin; no JavaScript-native interface, arbitrary path, new permission, provider
call or Unity control is exposed. The success receipt appears in the original
SessionControls only after native publication. Desktop and Capacitor retain their
existing writers. This is chat-backup portability; room/model/motion/module-library
export is still unfinished.

The v1 export protocol accepts one active transaction, 24,576-byte chunks and up to
256 MiB, with exact sequence/offset checks and duplicate-response replay. Per-document
export IDs are retained (maximum 64 starts; reopen/resume for a fresh broker).
All storage work is serialized off the UI thread. A 30-second web acknowledgement
timeout reports uncertainty; 60 seconds without a native request abandons a pending
file. Stale page callbacks are ignored after a 10-second poll timeout. Suspension,
replacement and failures delete the transaction's pending row; an already queued
finish may complete, so interrupted callers are told to check Downloads.

MediaStore publication uses IS_PENDING until stream flush/close and the provider
update succeed. Android controls expiration of crash-orphaned pending rows; the
app does not write the read-only DATE_EXPIRES field. See the official
[Downloads storage guidance](https://developer.android.com/training/data-storage/shared/media)
and [pending expiry contract](https://developer.android.com/reference/android/provider/MediaStore.MediaColumns#DATE_EXPIRES).
Hardware filesystem behavior and power-loss durability remain acceptance gates.
The browser fixture uses simulated native acknowledgements and real original
Save/Load controls/IndexedDB; Android tests use an isolated fake MediaStore provider.


### 2026-09-30: confirmed backups before replacement/reset

Save All now returns a distinct saved/shared/cancelled/failed outcome. Load All and
Backup & Reset require a completed file before changing existing data. Failures
persisting the selected conversation, reading its avatar, writing or closing the
file all stop the destructive path. Modern complete archives may contain zero
conversations, so a profile created before the first lesson is still restorable;
legacy empty files and truncated/mismatched modern archives remain invalid.

Desktop and Quest share one writer adapter. On Capacitor, required recovery copies
are written to a unique Documents filename through a pending file and rename;
a cache file or share-sheet acknowledgement cannot authorize replacement/reset.
Manual sharing can still use cache when Documents is unavailable. A completed
Documents file remains saved if its subsequent share dialog is cancelled.

Reset clears all original-app IndexedDB stores in one transaction and reloads only
on its completion. It never queues an uncancellable deleteDatabase request. Double
confirmation and cancellation while an action is already running are disabled.
Changes to this tab's conversation/settings abort an outstanding reset; both
asynchronous aborts and synchronous clear/setup failures roll back queued clears.
A guard that arrives after commit is reported honestly as a completed reset.
This is not global writer quiescence: concurrent tabs, provider completions and
other independent writes still require coordinated snapshot/maintenance acceptance
before release. Chat backups do not restore every preference, credential, cache,
or native Unity room/model/motion/module file. Native content portability remains
next work, separate from this original-app backup path.

PC browser evidence uses the actual Save/Load/Reset controls and real IndexedDB,
with simulated native export acknowledgements. It covers failed mandatory saves,
atomic rollback, a conversation change during clear, successful clear/reload and
zero-chat profile restoration. No headset installation or storage acceptance is
implied by those checks.


### 2026-09-30: reusable module file portability

The library exports exact inspected definitions and previews/imports validated
Maestro module files using the shared `program.module.import` capability. Human
and agent imports receive native-issued action receipts and immutable content
identities. Importing does not execute a program, grant resources, replace pins,
or transfer room/model/motion assets. The bounded format includes embedded modules;
asset transfer and deliberate dependency rebinding remain open. Native quick edits
keep module documents opaque and visit only actual local invocation blocks. See
[portable module contract](QUEST_PROGRAM_MODULES.md#portable-module-files).


### 2026-09-30: native workspace archive foundation

Native capture now takes a detached accepted-document snapshot while scoped library
gates keep model/motion bytes stable. A bounded archive contains room, behaviour,
controller, avatar activity, model, motion and reusable module content with exact
IDs. Import verifies all entries in an owned staging directory without touching live
stores or running behaviours. Missing references remain explicit; invalid programs
remain individually unavailable. See [archive contract and evidence](QUEST_NATIVE_ARCHIVES.md).
User-facing book/agent actions, Android publication/picking, atomic activation and
recovery controls are still open. This foundation does not yet provide users a
complete native backup/restore workflow. Device work remains on hold.

### 2026-09-30: native workspace export through shared actions

`workspace.archive.export` is discoverable and runnable through the existing book
catalog, agent and program interpreter. A completed receipt means a closed binary
ZIP was published in Quest Downloads/Maestro. Native capture and Android streaming
stay outside the browser; arbitrary paths and files cannot be supplied. Duplicate
execution IDs replay their result, pending exports remain pending until publication,
and failure/Stop preserve honest outcome uncertainty. Output sizes fit program
value bounds even for the largest permitted archive; export has a bounded native
I/O allowance without extending ordinary action/loading deadlines.

PC native and Android provider tests cover publication, replay, failure and Stop.
Archive picking, reviewed generation activation/recovery and actual headset
Downloads/performance acceptance remain open. See [native archive workflow](QUEST_NATIVE_ARCHIVES.md).
Device work remains on hold; no build was installed.

### 2026-10-01: recoverable native workspace generation foundation

Native preparation now preserves a verified generation independently of active
stores. Activation rechecks the preview and atomically selects the complete root,
retaining the prior root and a replay-safe activation attempt. Restoration and
previous-workspace recovery assign fresh receipt epochs and persist a review hold;
review completion does not rewrite user code. Fault-injection/reopen tests cover
interruption before and after pointer commit, stale/tampered requests and corruption.

Production startup, runtime review enforcement and restore UI/actions are still
unconnected. They must be integrated together; a persisted flag is not enough to
prevent imported triggers. Android file selection also needs its own tracked
request because the system picker pauses the app and stops ordinary running
actions. See [generation contract and integration requirements](QUEST_NATIVE_ARCHIVES.md#recoverable-workspace-generation-store-native-foundation).

### 2026-10-01: retain accepted edits after save failures

Ordinary room and behaviour saves now preserve the pending state after background,
lifecycle or faulted-worker failures. A delayed retry writes the latest accepted
snapshot; completing an older save does not erase newer edits. The lifecycle path
has a checked result, and a rejected immediate edit keeps earlier unsaved changes.
Temporary-room pause/quit still cannot save unkept changes. Integration tests cover
real blocked storage and recovery, changes during a write and faulted workers.
This is a prerequisite for restore retention, not coordinated activation or a
promise to survive process loss while storage remains unavailable.

### 2026-10-01: native archive chooser and shared verified preview

The book, agent and programs can request an Android archive choice through
`workspace.archive.select`, inspect its exact request through the parameterized
`workspace.archive.selection` fact, and cancel/discard through
`workspace.archive.cancel`. Selection outlives the chooser's app pause independently
of the stopped action scheduler. Native code bounds the copy and verifies the ZIP
into a private generation, with no activation or automatic program execution.
Request identities prevent duplicate choosers and stale cancellation. A still-closing
provider prevents another worker. Native tests capture a real workspace, verify the
preview and its identical catalog fact, and prove active content remains unchanged.
The book test consumes the real native receipt/preview. This does not yet implement
reviewed activation, coordinated current-state retention or startup recovery.

### 2026-10-01: shared workspace activity hold

Native rules, avatar playback, recipes, authoring, movement bindings and physics now
accept one composable runtime hold. It can be acquired before loading owners;
lifecycle focus cannot clear it, and releasing it does not replay stopped actions.
Rules and controls inherit the editor's selected data directory, while receipt
history can use a fresh epoch. This is the enforcement prerequisite for reviewed
restoration. Production generation selection, coherent accepted-edit retention,
review completion and startup recovery are still being integrated.


### 2026-10-01: accepted-edit retention boundary

Native editors, library writes, import completion, physical selection and Recall now
share an edit gate separate from the activity hold. Retention waits for complete
accepted operations, finishes authoring, then captures current in-memory documents.
Activation requires a separately verified retained snapshot and binds it with the
import and origin selection in one recorded operation. A retry cannot substitute its
previous workspace; reserved retention cannot be deleted as an unused preview.
Native tests exercise accepted unsaved edits, async writer gaps, failure without
owner destruction, and both sides of the pointer commit. These are reusable native
services; production startup/switching, maintenance parity and content-bound review
remain unfinished, and no device installation is included.


### 2026-10-01: persistent book and selected workspace startup

Production startup resolves the saved workspace selection before building content,
passes its data and receipt directories, and applies any review hold before the
first frame. The XR rig, physical book/browser and agent transport stay alive while
content owners are replaced. An exact committed-pointer check and accepted-edit
hold protect the replacement; old item registrations, library UI and input links
are detached before new owners load. Interrupted replacement reopens the committed
selection after old destruction. Agent sessions rotate so stale requests cannot
write to a replacement room. Corrupt selection metadata preserves the shell and
reports unavailability; independent store damage retains its existing read-only
behavior. Activation/recovery commands, held-content maintenance parity and
content-bound review remain unfinished release work.


### 2026-10-01: persistent workspace maintenance through the shared catalog

Archive selection, cancellation, export and selection-status observation now work
through a catalog-declared workspace domain. The shell owns their services and a
persistent runner using the existing interpreter, native handlers and receipt
store. Room holds or missing content do not block maintenance; app pause still
interrupts active invocations. Native and the shared web/agent helper independently
route each start to its domain's issued ID. The book displays both histories and
supports their existing inspection, Stop and explicit receipt-recovery operations.

Native tests prove picker and receipt ownership survive room replacement, including
an interruption, and that completed opening receipts reconcile across app restart
without reopening the chooser. Export succeeds under review while room activity
stays held. Web tests accept captured native outcomes and operate the book Run
control despite unavailable room history. Complete restore activation and recovery,
content-bound review completion, retained-generation browsing/maintenance and
headset acceptance remain release work.


### 2026-10-01: tracked activation of reviewed native archives

The shared catalog now starts a persistent activation operation. It preserves the
accepted current workspace before committing the exact imported/retained pair,
replaces content through the persistent host and keeps imported activity held for
review. The opening action receipt identifies a job; typed current-workspace and
activation facts report the real result. Cancellation and teardown retain the edit
lease until workers settle. A bounded durable journal reconciles post-commit failure
and restart without replaying the switch. Reserved pairs cannot be retried after
editing resumes. The persistent browser owns library-close delivery retries until
page acknowledgement.

Native integration covers successful replacement and retention, cancellation and
failure boundaries, restart and teardown; web tests exercise the same action/facts
using native captures. Content-bound review completion, exact previous/corrupt
workspace recovery, generation maintenance and real-headset acceptance are still
required before portable restore is release-ready.


### 2026-10-01: content-bound native workspace review

The shared catalog now prepares an inspection snapshot and completes only its
exact content hash and selection revision. Native completion freezes editing,
saves accepted documents and independently rechecks the manifest. Changed content
requires a fresh review; failed saves preserve live edits without approving them.
A durable record reconciles commit-boundary failures and restart. Approval keeps
the same live owners, Undo, agent session and action-history epoch, releases only
its own hold, and does not restart stopped activity. Cancellation retains ownership
until the worker drains; uncertain outcomes keep content frozen.

Native tests cover stale reviews, restart, save failure, cancellation, commit
faults and nested holds. Web tests use captured native results for the same action
and fact controls, distinguishing an opening receipt from actual completion.
Previous/corrupt-workspace recovery, retained-generation maintenance and headset
acceptance remain release gates. Recovery must settle retiring workers before
opening new owners for the same persistent storage.


### 2026-10-01: verified previous-workspace recovery

The book and agent can inspect previous-workspace metadata and select its exact
retained identity without an Android chooser. A worker verifies and copies it into
a fresh preview bound to the full origin selection. Existing activation preserves
today's accepted edits before switching and keeps recovered activity under review.
Stale previews, missing provenance and corrupt sources cannot bypass that boundary;
cancellation removes only the unused copy. This reuses the shared catalog, preview,
activation and review operations instead of a second switching runtime.

Tests cover verified asset copies, source preservation, stale selection, lost
provenance, worker faults/cancellation and the native book/agent recovery journey.
Unreadable current content, corrupt selection and failed initialization still need
a separate recovery path, followed by generation maintenance and device acceptance.


### 2026-10-01: damaged-workspace preservation foundation

A native recovery hold can preserve accepted edits even when a current store is
unreadable. It pauses autosave and lifecycle writes, drains prior saves, and records
original bytes separately from labelled accepted documents and any temporary room.
A bounded evidence ZIP carries per-entry hashes and cannot be imported as an
ordinary workspace. Tests cover damaged data with newer live edits, cancellation
while both save workers are pending, older save completion, temporary-room
separation, capture failure and teardown without overwriting originals.

This is an internal prerequisite, not a user-facing corrupt-workspace recovery
flow. Exact pointer/candidate inspection, durable recovery selection, coordinated
content replacement and shared book/agent controls remain to be connected. The
checkpoint does not query or install on the headset; device acceptance is pending.


### 2026-10-01: explicit damaged-selection recovery storage

The generation store can inspect recovery candidates without creating a fallback
pointer, preserve both original selection files, and verify/copy an exact candidate
into a fresh preview. Commit requires private preserved evidence, rechecks all
identities, records its intended outcome durably and selects fresh action receipts
under review. Reconciliation does not replay commits. Cancelled evidence, old
generations and selected data remain protected from ordinary preview deletion.

Seventeen additional native storage cases cover stale/missing/corrupt metadata,
real model copies, altered evidence/provenance, cancellation and commit-boundary
faults. The user/agent recovery coordinator, live-owner replacement, missing-owner
handling, explicit alternatives when no candidate verifies and evidence maintenance
remain to be integrated. This checkpoint does not enable automatic recovery or
establish headset acceptance.


### 2026-10-01: shared damaged-workspace recovery and retiring owners

The persistent host now coordinates damaged recovery through four catalog actions
(inspect, select, commit, cancel), three facts and the existing book/agent controls.
It can recover an unreadable live store or a selection with no live owners, binding
exact inspection, verified preview, preserved evidence and a fresh reviewed root.
Original files remain retained; activity starts separately after ordinary review.

Host replacement waits for accepted writes, background services and destruction;
a new host cannot reuse the same data path while old workers still own it. Tests
cover cancellation, before/after commit faults, teardown/restart, partial creation,
disable/enable, stale requests and refused replacement. A captured Unity flow is
validated by web contracts and the book controls. No device queries or installs
are part of this checkpoint.

Still open: explicit no-candidate clean start/external import, repair of corrupt
recovery-operation history, evidence maintenance, broader release acceptance and
headset testing. This is a release-work checkpoint, not completed Quest v1.


### 2026-10-01: external and fresh recovery sources

Recovery can prepare an explicitly requested fresh workspace when no retained
candidate verifies. The same select action uses a typed source choice; fresh content
has only the included book and Maestro, normal defaults and no user assets/programs.
It preserves the observed selection and original data, requires a separate commit,
opens under review and never resumes prior activity. Prepared previews survive
restart without automatic activation.

The existing external archive picker now prepares without writing a selection
pointer. A prepared import can enter the same inspected recovery candidate flow,
even with a missing pointer and damaged backup. Its source is owned while copying
and retained after cancellation. Ordinary activation still establishes its baseline
and distinguishes failures there from failures after its actual selection commit.
Native and shared-control verification covers these paths; device acceptance,
corrupt recovery-history repair and evidence maintenance remain open.


### 2026-10-01: shared repair of unavailable operation history

Activation, review and recovery coordinators now expose unavailable tracking to a
shared inspect/reset capability pair. Inspection binds original status bytes and
accepted in-memory state; reset preserves both before atomically moving only the
damaged status out of use. Changed histories require a new inspection. Captures,
workspace selection, live content, review holds and receipt IDs stay independent.
No uncertain operation is replayed or approved. All workspace workers must be idle;
reset cancellation and host teardown retain ownership until their worker drains.
A cancellation after the rename still reports committed preservation.

The final native build passed 333 EditMode and 226 PlayMode checks, with three
optional private-model skips. Web checks passed 1,534 tests across 172 files;
native inspection/reset results also exercise the shared book controls. Bounded
inventory/capacity, stale history, exact raw/accepted preservation, missing owners,
separate content review, cancellation and retiring-host ownership are covered.
No headset installation is part of this checkpoint. Evidence export/retention,
full-storage handling and physical-device acceptance remain release work.


### 2026-10-01: export and removal of preserved operation-history evidence

The shared catalog now exposes inspection, diagnostic export and explicit removal
of history-repair evidence. Each entry has a byte-bound fingerprint. Publication
uses the existing native Downloads service; a distinct maestro-evidence filename
separates diagnostics from playable workspace backups. Numeric ZIP payload names,
a verifiable manifest, and importer rejection keep damaged status data inert.

Removal requires a retained completed native export receipt for the exact same
bytes. That proof survives restart; failed, interrupted, stale or expired proof
cannot authorize deletion. Cancellation before deletion preserves everything;
partial removal stays observable and requires fresh inspection. The worker keeps
path ownership through host teardown. Current room content, review holds, workspace
selection and action history remain independent. A full evidence quota can now be
reclaimed after export; there is no automatic deletion.

Verification passed 345 EditMode, 233 PlayMode (three optional private-model skips),
55 Android and 1,538 web checks. The final native capture passed 39 book/contract
checks. Independent ZIP inspection verifies every raw payload, the inventory hash
and its completed-export receipt. The final development APK and its v2 signature
were verified against the tested sources and packaged web build. No headset action
was taken.

Retained-generation cleanup is still open: the 64-generation limit can block new
imports/recovery, and current/previous pointers plus activation/recovery reservations
must be handled before old generations can be removed. Original, selected and
reserved roots are not made deletable by the new evidence feature. Other v1
device/provider/asset/performance and store acceptance gates remain unchanged.


Retained-content export increment (2026-10-01): `workspace.retention.inspect`
and `workspace.retention.export` share native catalog discovery, generated book
forms, agent calls and persistent action receipts. Inspection separates selected,
previous, pointer-backup and other retained generations from their reservations.
Inactive exports validate actual saved content into a fresh portable manifest,
including edits newer than the import, without opening the room or consuming a
retention slot. Excluded private files stay on-device. The live storage's
newer-version detector is reused to prevent exporting an older primary as current.
At this export checkpoint, generation removal remained unfinished; portable
exports are not full raw evidence. The later disposal increment is recorded below.

Native checks cover newer primary content, model payloads, complete ZIP re-import,
full retention capacity, corrupt and stale inputs, newer-version files, publication
failure, duplicate receipts, cancellation and retiring host ownership. Captured
native output is validated by the web contracts and original book controls.
Build helpers defer private ADB cleanup until actual editor shutdown; this fixes
a reproduced configuration shutdown stall while retaining terminal exit-code and
timeout checks. PC verification passed 357 EditMode and 237 PlayMode tests (three optional
private-model skips), 1,542 web tests in 172 files, 41 final native-capture
book/contract checks, and 55 Android tests. Production build, lint, catalog drift,
boundaries and prompt ownership pass. The helper exited successfully; ARM64 and
v2 signature verify. All 166 runtime and 73 test C# sources, 24 fixtures with their
metadata, and 113 packaged web files match the tested sources. Development APK
`MaestroQuest-workspace-retention-AA7C2D35.apk` has SHA-256
`AA7C2D35164A3A5F218F06A5BCA326ECB0A5A5C1CE0C65570531167457E22C85`
and remains uninstalled. PR #248 records the corresponding verified checkpoint.
Device/provider/Store acceptance and the full release goal remain open.


### 2026-10-01: explicit retained-generation disposal

The user selected confirmed discard with optional backup. Retained inventory now
leads to a separate whole-generation preview and an exact confirmed removal call.
The preview binds actual files, both selection pointers, live owners and accepted
operation history. Current, previous, pointer-backup and pending/tracked roots
remain protected; historical reservations alone no longer pin unrelated old rooms
forever. Damaged target metadata can be disposed of, but unclear selection/history
must be repaired first. No automatic quota eviction, fallback or replay is added.

`workspace.retention.reviewRemoval`, `workspace.retention.remove` and the
`workspace.retention.removal` fact use the shared catalog and durable workspace
receipts. The book derives a separate confirmation from schema metadata, resets it
when arguments change, and no longer offers workspace actions as room-program
blocks. Native deletion checks exact contents before starting and before each
file unlink; nonrecursive directory removal preserves new children. Stop after
deletion begins cannot roll back; interruption may leave a partial generation
requiring another inspection, preview and explicit confirmation. Retirement holds
the path until workers drain. Full-capacity cleanup needs no spare generation.

Desktop tests exercise selected/previous/backup/live dependencies, tracked and
changed history, corrupt target metadata, stale file/pointer identities, full
capacity, cancellation before/after the boundary, partial deletion and changed
files, duplicate durable outcomes, restart and host retirement. Captured native
previews/results are checked by the web contract and book tests. A local Chrome
fixture verifies cancel-without-dispatch and exact confirmed dispatch; its native
acknowledgement is simulated and no actual room files are deleted by that UI run.

Generation disposal does not constitute a complete raw recovery archive. The
64-generation cap plus unreadable selection still needs recovery/repair; unknown
roots are never guessed to make space. Quest device/provider/lifecycle/performance
and Store acceptance remain required for the complete v1 release.

Verification for this disposal checkpoint: 368 EditMode and 242 PlayMode tests
passed (three optional private-model skips), 1,545 web tests in 172 files, 43 final
book/contract checks, and 55 Android tests. Production build, lint, catalog drift,
prompt ownership and boundaries pass. The complete build helper exited zero;
ARM64 contents and v2 signature verify. All 167 runtime and 75 test C# sources,
24 fixture files and metadata, and 113 packaged web files match the verified
source/build. Development APK `MaestroQuest-workspace-removal-908B17FF.apk` has
SHA-256 `908B17FF7D590D1ABBB2214F322EE4768AA3464AB5F4C4D9A7BF282BC1A1FAF2`
and remains uninstalled. PR #248 stays draft and the full release goal remains open.


### 2026-10-01: shared pose and keyframe authoring

`animation.author` supports replacing/patching/removing recorded frames, timing,
looping, saved Maestro poses and clearing motion. Metadata, frame and canonical
joint facts bind readback to an exact object revision. Physical frame operations
share detached mutation helpers; all animation saves use the same validated
persist-before-journal path. Failed writes preserve saved data and Undo state.
Temporary edits remain in the fork until Keep; stale reads fail after Discard.
Remote edits refuse active physical authoring and conflicting ownership. Imported
Maestro rigs use the same canonical pose and retargeting as manual posing.

The book action catalog now also derives nested typed inputs from native schemas.
A Chrome fixture authors the captured three-frame, 17-joint nod entirely through
those controls and dispatches the exact native request. Its acknowledgement is
simulated; native PlayMode tests separately verify included/imported-rig movement,
manual/agent editing, duplicate receipts, saved Undo/Redo, rejected writes and
Keep/Discard. Recording Start/Finish/Discard is still a physical control pending
shared session ownership/identity work. This is saved-motion parity, not complete
manual-control parity or headset/provider acceptance.

Verification passed 370 EditMode, 249 PlayMode (three optional private-model skips),
1,551 web tests in 173 files, 48 focused contract/book tests and 55 Android tests.
Production build, lint, catalog drift, prompt ownership and boundaries pass. The
full build helper exited zero. All 169 runtime and 77 test C# files, 24 native
fixtures plus metadata, and 113 packaged web files match the tested source/build.
The ARM64 package and v2 signature verify. Development checkpoint
`MaestroQuest-animation-authoring-E0821836.apk` has SHA-256
`E08218360895D4BA978EDC3C099D3A0BB8D5DFA1DEBA234509D65E1BD08745AF`.
It remains uninstalled, and PR #248 remains draft.


## Included Meshy default checkpoint — 2026-10-02

The user-selected paid-plan Azure Violet Doll now ships offline as one exact
9,234,600-byte GLB, including its declared Walking clip. Its 68-joint skeleton
uses the existing 17 canonical pose controls and preserves imported animation
channels. New rooms and explicit Default selection resolve to the exact model
hash; saved selections survive artwork updates. First-use library copies,
retirement, portable recovery, selection/Undo and native fact/receipt readback
share the existing storage and action paths. See QUEST_INCLUDED_AVATAR.md.

Four new EditMode and four PlayMode cases cover package integrity, compressed
APK reads, quota/retirement, portable recovery, actual model posing/walking,
saved selections across updates, physical/agent selection, duplicate receipts,
Undo and cancellation. **419 EditMode and 390 PlayMode passed**, with the three
optional private-file tests skipped in the full package run. **1,713 web tests
across 197 files**, the **52-test native-capture/book subset**, **61 Android tests**,
Android assembly/lint, TypeScript, ESLint, production web build, catalog/core/
prompt checks and the new CI asset-integrity check passed. The full build helper
exited zero. All 213 runtime, 110 test and 11 Editor C# files, 33 native fixtures
and their metadata, the native browser AAR and 113 packaged web files match the
verified inputs. The exact avatar bytes, ARM64-only libraries and APK v2
signature were checked in the built archive.

Development checkpoint:
`D:/Projects/Builds/MaestroQuestVerify/Builds/Checkpoints/MaestroQuest-included-avatar-80EE765A.apk`

SHA-256:
`80EE765AA961C6319345EF8B24B019CAF5A8028AD6C355F78674FF5E18F7F762`

The three exports from the supplied ZIP independently passed the native motion
extractor and reopened with stable IDs. All match the new rig. Their reusable
motion payloads total 350,732 bytes versus 27,761,900 input bytes. The earlier
`D:/MeshyAnimatedMaestro` collection uses the old rig and is not assigned to this
avatar. Only Walking is packaged with the model at this checkpoint. Curated
motion packs, direct user ZIP import, further current-rig exports and cross-rig
motion retargeting remain separate work; no promise of automatic conversion.

Desktop idle/greeting/pointing previews use the actual model, retargeter and
materials. They do not prove device appearance or gesture comfort. This APK is
not installed; no headset or production deployment took place. Remaining v1
provider, storage, runtime, MR/performance/comfort and Store gates stay open.


## 2026-10-02: shared animation ZIP collections

The existing Animation batches tray, generated book controls and room agent can
now select one animation ZIP. Native preparation copies at most 2 GiB and lists
up to 1,024 GLB/VRM members; explicit Start unpacks and validates one model at a
time. Exact motion IDs, rig checks, category editing, partial results, Stop,
Retry and Clear continue through the same importer. No avatar replacement,
model placement, behaviour assignment or playback is triggered by import.

Archive metadata, expanded size, paths, member length and CRC are bounded and
checked. Plain multi-selection remains available (up to 128 files). Temporary
storage is the compressed ZIP plus one unpacked member, while the saved library
keeps its existing 1,024-motion / 128 MiB budget. Reading one result no longer
clones every batch result. See QUEST_MOTION_BATCH_IMPORT.md for the contract.

Verification: 419 Unity EditMode and 391 PlayMode tests passed (three deliberately
optional private-file checks skipped). All 71 Android tests passed in a separate
run with the selected real Meshy ZIP; all three decoded model hashes matched.
Synthetic cases cover 300-member extraction, the 1,024-member limit, malformed
and oversized metadata, duplicate/unsafe paths, CRC/size corruption, cancellation
and picker retirement. The shared book/agent subset passed 60 tests in three
files. TypeScript, ESLint, generated catalog and included-asset checks passed.
Native release assembly/lint and the full development build completed with
exit code 0; lint has six existing warnings outside the changed import code.

All 213 runtime, 110 test and 11 Editor C# files, 33 fixtures plus metadata,
the native AAR and 113 packaged web files match the verified inputs. The ZIP
importer is present in the APK's DEX; exact bundled avatar, ARM64 libraries and
APK v2 signature are verified.

Development checkpoint:
`D:/Projects/Builds/MaestroQuestVerify/Builds/Checkpoints/MaestroQuest-motion-zip-633C2A46.apk`

SHA-256:
`633C2A468D4D6C04494F592DD62F4C9BC40831C77E48F0563F106AAA673C6ABE`

The APK has not been installed. Headset picker/lifecycle, storage and performance
acceptance remain open. ZIP member selection for **model preview**, curated
current-rig default motion packs and cross-rig imported-motion retargeting are
separate remaining work. The old-rig animation collection is unchanged and is
not assigned to the new default. Other v1 release gates remain open.


## 2026-10-02: ZIP model selection and preview

Users can now choose a GLB/VRM member from the same bounded ZIP source used by
animation collections. Selection privately copies and lists the ZIP without
choosing a model. Physical Prev/Next file and Preview, generated book forms and
the room agent share versioned selection. Search returns three indexed entries
per page; the worst escaped-name case fits the existing program-value budget.
Explicit acceptance is still required for an object, Maestro, library model or
embedded motions. A failed member keeps the archive available for another try.

Back-to-files removes only the preview. Cancellation releases private copies and
the workspace lease, and newer requests reject stale choices and late failures.
A timeout/retry regression ensures an old native worker cannot fail a newer
member request. See QUEST_MODEL_IMPORT.md for limits and lifecycle details.

Verification: 419 Unity EditMode and 394 PlayMode tests passed (three optional
private-file checks skipped). All 76 Android tests passed, including extracting
all three original Meshy ZIP members and choosing its exact Walking model through
the Android model picker. The physical/native journey covers failed preview,
stale versions, explicit avatar acceptance, Undo, pause and cancellation. The
shared web subset passed 56 tests in four files; TypeScript, ESLint, catalog drift
and included-asset integrity checks passed. Native release assembly/lint and the
complete development build exited 0; six existing native lint warnings remain.

All 214 runtime, 111 test and 11 Editor C# files, 33 fixtures plus metadata,
the native AAR and 113 packaged web files match the verified inputs. Both ZIP
import paths are present in the APK DEX. Exact default avatar bytes, ARM64-only
libraries and APK v2 signature are verified.

Development checkpoint:
`D:/Projects/Builds/MaestroQuestVerify/Builds/Checkpoints/MaestroQuest-model-zip-8ED11511.apk`

SHA-256:
`8ED11511C2F8F6E4E096EEA4B51855F177AD5F2D5C3537C24D4268CE15146E90`

This APK has not been installed. Physical chooser/lifecycle, storage and Quest
performance/comfort acceptance remain open. Curated current-rig motion packs,
cross-rig imported-motion retargeting and the other v1 release gates remain
unfinished. The older motion collection is unchanged.


## 2026-10-02: included current-rig motion collection

The default avatar now includes 178 unique reusable animations from the owner's
180 current-rig exports: 65,010,976 bytes without repeated meshes or textures.
Exact duplicate motions retain both source origins. Every payload keeps the
source's 68 animated nodes and 136 channels. The 17 canonical posing handles do
not cap imported-animation bones. Imported renderers now explicitly use four
bone influences per vertex, instead of inheriting Android's two-influence preset.

New private libraries install the offline collection once without compiling clips
or starting playback. Existing libraries add missing content only through the
shared motion.pack.install action. Book forms explicitly read its package guard;
the app agent and programs use the same capability. Add preserves existing IDs,
names, tags, favourites, archive and removed-download state. Restore targets one
exact saved motion in the package. No operation assigns a gait, infers looping,
changes Maestro or starts a behaviour. Source folder observations remain notes.

The worker validates the complete catalogue and storage budget, verifies payloads
sequentially and publishes metadata once. Cancellation cannot publish a partial
collection; verified unreferenced copies can remain and are reused on retry.
Accepted writes hold preservation/retirement until drained. Explicit fresh
recovery and portable archives include the same materialized motion bytes/IDs.
A failed first-use copy can be retried without leaving a stale failure notice.
See QUEST_ANIMATION_LIBRARY.md for the storage and playback boundaries.

Verification: 427 Unity EditMode and 396 PlayMode tests passed (three optional
private-file skips). Native checks cover directory/compressed APK integrity,
exact identity preservation, explicit restore, cancellation/retry, first-use
leases, quota refusal, portable recovery, native receipts and actual shipped
motion playback. All 76 Android tests passed, including the original Meshy ZIP
and exact selected-model checks. The focused web subset passed 76 tests in four
files, including native observations/receipts, bounded facts, current-value
mappings and generated book controls. TypeScript, ESLint, catalog drift and
included-asset integrity checks passed. Full development build exited 0.

All 217 runtime, 114 test and 11 Editor C# files, 34 fixtures plus metadata,
the native AAR and 113 packaged web files match the verified inputs. Exact
avatar and all 178 motion payload hashes, ARM64-only libraries and APK v2
signature are verified.

Development checkpoint:
`D:/Projects/Builds/MaestroQuestVerify/Builds/Checkpoints/MaestroQuest-included-motions-C8C9401F.apk`

SHA-256:
`C8C9401F195C6C2095D663B4F4BA10BFAA2737B15358C18EB189B6EB641E3D17`

This APK has not been installed. Original versus navigation-adjusted hip travel,
visual/semantic curation, default tutor-state assignments and Quest storage,
performance, lifecycle and comfort acceptance remain open. These checks do not
certify preview-equivalent final Maestro motion or Store readiness. No device or
production deployment was performed; other v1 gates remain active.


## 2026-10-02: explicit authored animation travel

Embedded and reusable library motions now have an optional `movement` argument
on the existing `animation.play` capability. `authored` preserves the complete
sampled imported pose while transferring planar body displacement to Maestro's
room placement, including accumulated loop travel. Omitted/`inPlace` keeps the
existing navigation-adjusted behavior. Saved gait and tutor-state assignments
are unchanged; no source-filename classification is treated as movement policy.

Generated book fields, programs and agent calls share this contract. Optional
argument feature requirements include expression bindings, and legacy simple
steps refuse conversions that would drop the explicit policy. The capability
catalog's source fingerprint now also covers the avatar playback, retargeting
and spatial-motion implementations. The browser inspection fixture's action
definition was refreshed directly from successful native capture.

Authored movement requires running room physics and clear connected level
scanned floor. Swept body checks, personal clearance, tracking, pause and
placement changes constrain each step. Stop/failure keeps the last accepted
room position. This protects a body proxy, not every animated extremity, and
does not add foot planting, cross-rig retargeting or physical cloth/hair.

Desktop Unity verification passed 427 EditMode and 400 PlayMode tests, with
three deliberately optional private-file skips. New checks include loop carry,
wall refusal, tracking/physics loss, shared-action cleanup and source-equivalent
joints/deformed mesh after canonical pose application. The actual shipped
360_Power_Spin_Jump differed by at most 0.00000122 metres over five sampled times.
All-clip visual curation and physical Quest motion/comfort checks remain open.

The focused web subset passed 142 tests in four files. All 1,725 web tests in
200 files passed; TypeScript, ESLint, catalog source checks and included-asset
integrity checks passed. No hardware acceptance is implied.

Full development build exited 0. All 76 Android tests passed, including the real
Meshy ZIP. Verified inputs match all 218 runtime, 115 test and 11 Editor C# files,
34 fixtures plus metadata, the native AAR and 113 packaged web files. The exact
avatar, all 178 motion payload hashes, ARM64-only libraries and APK v2 signature
were checked.

Development checkpoint:
`D:/Projects/Builds/MaestroQuestVerify/Builds/Checkpoints/MaestroQuest-authored-motion-D75ADB53.apk`

SHA-256:
`D75ADB53594BDBA4BDD9A0ACE12DE16AA2C9DE2C251E75E02E8DEE90A74A3009`

This APK has not been installed. Device work remains on hold. The other v1
release gates, including full motion curation and physical Quest acceptance,
remain active.


### Carried-prop follow-up

The body sweep now excludes props actively held by Maestro. Their independent
swept trajectory/release checks remain active, and released props immediately
become ordinary body obstacles again. A native regression verifies both states.
All 427 EditMode and 401 PlayMode tests passed (three optional skips), together
with 76 Android tests. The rebuilt package exited 0 and passed the same complete
source, content, ARM64 and v2-signature checks above.

Updated development checkpoint:
`D:/Projects/Builds/MaestroQuestVerify/Builds/Checkpoints/MaestroQuest-authored-motion-9E300477.apk`

SHA-256:
`9E30047753C3873F63A27B73FCD4B1C03AAE8221BF05BA16590370FBCC0ACC6F`

Not installed; physical Quest acceptance remains open.


## 2026-10-02: managed agent stream cancellation boundary

Managed chat/agent generation now observes disconnects before awaited admission
and between token-count operations. Cancellation before dispatch never starts
provider generation and releases any completed credit reservation. Once sent,
the existing usage-drain policy remains explicit: client write/end failures do
not discard provider usage or refund completed work. Later stream chunks can no
longer erase previously reported usage merely by omitting it.

The current Google SDK does not promise service-side cancellation through
AbortSignal. QUEST_UNIFIED_AGENT.md records that provider limitation, the chosen
boundary and remaining real-provider acceptance. No second Unity provider or
account path was introduced, and failed-provider refund policy is unchanged.

Validation passed 57 provider/accounting emulator tests (eleven new cases), the
billing and Live gateway emulator scripts, 25 Functions units and 60 focused
web cancellation/handoff tests. Firestore transactions and one HTTP disconnect
are real/local; the provider is simulated. No deployment or APK/device change was
made. The last verified Quest package remains the 530261f authored-motion build.

## Program compatibility checkpoint — 2026-10-02

The shared feature gate previously scanned every JSON object for code-shaped
fields. Valid structured values could therefore fail before dispatch with an
unrelated update requirement. Seven reproduced false rejections now pass. The
shared syntax and existing typed node/expression traversal are also used by the
feature gate, avoiding a separate recursive interpretation of arbitrary data.
Each program source is parsed once for this check. Full command validation still
runs first, and Unity remains the executor and final authority on readiness.

Executable blocks, nested expression facts, imported programs and only
schema-declared `programModule` payloads contribute feature requirements.
Literal initial values, literal expression records and other native payloads
remain data. Existing object-edit guards are preserved. Module storage does not
start a program. The book bridge test verifies unchanged dispatch of valid data
and rejection of a real unsupported block before any request is sent.

Verification: **1,760 web tests in 207 files** pass, including 11 new compatibility
and bridge regressions. TypeScript, lint, core boundaries, native-catalog source
checks and production web build pass. This change affects shared web authoring
validation only; no native runtime, model or storage format changed. The previous
channel-wait APK remains the packaged native checkpoint and does not contain this
web fix. No new APK, headset install or production deployment was performed.

Temporary-room remembered values remain explicitly blocked. Supporting them needs
an isolated memory fork and coordinated room/memory publication so Keep cannot
report success with mismatched saved state; this patch does not relax that guard.

## Temporary-memory persistence foundation — 2026-10-02

Added `RoomSnapshotTransaction` to support one coordinated saved room/memory
pair. A durable prepared intent restores the old pair; a committed intent keeps
the new pair. Exact primary/backup identities, bounded data, exclusive ownership
and repeated recovery prevent the coordinator from choosing half a save or
silently overwriting changed evidence. Thirty-eight focused native filesystem
cases cover interruption, ownership, damaged evidence and format boundaries.

This is an internal prerequisite, not enabled temporary memory. Live store,
archive/retention, receipt, session-guard and book/agent integration remains open
and is specified in QUEST_TEMPORARY_ROOM.md. The current temporary-memory guard
stays in force. Device timing and power-loss verification remain release gates;
no installed files or private source assets were modified.

Final local verification for this foundation: **529 EditMode and 410 PlayMode**
tests passed, with the three expected optional private-file skips. The native
verification helper exited 0. Source/mirror C# hashes match (227 runtime, 123 test
and 11 editor files). The paired-save helper remains unconnected to live stores.


## Temporary remembered values and coordinated live saves — 2026-10-02

Connected the snapshot coordinator to real room/memory startup, ordinary writers,
retention and archive inspection. Begin now forks remembered values, checkpoints
and explicit edits stay temporary, and Keep publishes the captured room/memory
pair together. Discard restores the last confirmed pair. Later edits remain
separate; room Undo changes layout only. The same native capabilities serve the
book, agent and saved programs with exact session and revision guards.

Recovery preservation includes labelled saved/temporary memory as well as raw
files. Competing memory owners, interrupted publication, stale drafts, asset
retention and accepted-write draining are covered by regression tests. Unknown
or changed evidence is preserved and can hold further boundaries. Desktop checks
do not establish Quest latency or power-loss durability. No new APK, device
operation, production deployment or Store submission is part of this checkpoint.


Final local verification for the live integration: **560 EditMode and 412 PlayMode** tests passed, with the three expected optional private-file skips; the native helper exited 0. All **1,764 web tests in 207 files**, TypeScript, lint, core boundaries, native-catalog provenance and production web build passed. C# source/mirror hashes match: **229 runtime, 126 test and 11 editor files**. The updated browser fixture comes from the native remembered-values journey. No APK or headset operation was performed.

PC recovery-capacity verification (2026-10-02): **566 EditMode and 414 PlayMode**
tests passed, with three expected optional private-file skips; the native helper
exited 0. All **1,766 web tests in 207 files**, TypeScript, ESLint, core boundaries,
catalog provenance and production web build passed. Six new storage cases cover
retained/fresh recovery, cancellation, preparation faults, review/cleanup and
unexpected overflow. Two shared native journeys cover full-library recovery,
content review, confirmed disposal, restart with 65 candidates and the bounded
capacity error. Their actual inventory observations drive the web fixture.
Source/mirror hashes match **229 runtime, 128 test and 11 editor C# files**.
No APK build or headset operation was performed; these remain desktop checks.

PC saved-content recovery verification (2026-10-02): **578 EditMode and 415
PlayMode tests** passed, with three expected optional private-file skips; the native
helper exited 0. All **1,767 web tests in 207 files**, TypeScript, ESLint, core
boundaries, catalog provenance and production web build passed. Twelve new storage
cases cover newer room/memory contents, excluded evidence, damaged/unfinished
sources, cancellation, concurrent writers, proof validation and historical
previews. The new shared native journey recovers later saved edits and a remembered
value after preview restart, then completes review; actual observations drive the
web contract fixture. Source/mirror hashes match **229 runtime, 130 test and 11
editor C# files**. No APK build or headset operation was performed; physical
storage latency, lifecycle and power-loss acceptance remain open.


## Packaged activity defaults and managed-access audit (2026-10-02)

Development source `27069e97c514e9b3afa61d619e2a6dee3d402041` is packaged as
`MaestroQuest-tutor-defaults-E80D8EAD.apk`, SHA-256
`E80D8EADFA9666CE323B7C56F4F2777D3DE07CCFDB07BF5E4B117C76D28ECFE4`.
The complete packaging helper exited 0: 593 EditMode, 417 PlayMode, three expected
private-file skips, and 76 Android bridge tests. All 113 web files, the included
avatar and 178 exact motion payloads match source; ARM64, signature and manifest
are verified. This is a development build and was not installed on a headset.

The release audit found that Quest currently falls through to web popup sign-in
and reCAPTCHA because its local book WebView is not Capacitor. These paths do not
establish managed Quest access. A separately configured, disabled-by-default Meta
attestation -> Firebase App Check backend is now implemented, with one-time
challenges, strict release checks and transaction tests. Native attestation,
shared-app provider integration and explicit browser account linking remain to be
implemented and verified. The original account, ledger and Gemini provider stay
authoritative. See [Quest managed access](QUEST_MANAGED_ACCESS.md) for the concrete
boundary, configuration, evidence and open release gates.

Backend checkpoint validation: **58 Functions unit/CORS tests** pass, including
33 Quest cases. The complete Firestore emulator command exited 0, covering the
existing billing/Live suites and new Quest transaction checks. TypeScript,
focused lint and whitespace checks pass. Real attestation and minting remain
unverified; the endpoint stays disabled pending configuration and device work.


## Quest client attestation checkpoint (2026-10-02)

The local book now uses Firebase's CustomProvider backed by Meta Platform SDK
207.0.0. Startup initialization and entitlement precede proof generation; a
release without valid configuration or entitlement exits. Development builds may
run unconfigured but cannot attest as a Store release. The original phone/web
providers remain unchanged. Embedded Google popup sign-in is refused on Quest
until explicit browser account linking is implemented.

The existing top-level book polling bridge carries session-bound requests and
quoted native results. No native object is exposed to artifacts. Replacement,
suspension, timeout and disposal reject stale callbacks and abort the server
exchange. Native proofs are not part of saved room/agent observations. See
[managed-access integration](QUEST_MANAGED_ACCESS.md) for public configuration and
the remaining real-provider, account-link and release acceptance gates.

The complete development packaging helper exited 0: **606 EditMode and 417
PlayMode tests** passed, with three expected optional private-file skips; Android
had **75 passes and two optional private-archive skips**. The local full web run
passed **1,794 tests in 210 files**; one additional origin-isolation test passed
separately afterwards. TypeScript, lint, catalog checks, production web build and
Android lint passed. C# source/mirror hashes match **232 runtime, 132 test and 12
editor files**. All 114 web files, the included avatar and 178 motion payloads match
the packaged copies. Native integrity methods and the platform loader are present;
permissions are unchanged, and the APK signature/ARM64/development manifest pass.

Development APK: `MaestroQuest-integrity-client-386E2BF1.apk`, SHA-256
`386E2BF176BCFC6757ACF30DABE60FD8E08EA3089C22BD9E5A3810297E8CDBBE`.
It remains disabled/unconfigured for real attestation and was not installed.
No dashboard app, production deployment, release signing or Store submission was
performed. The owner has been asked for a Meta app ID and intended package name;
secrets/signing keys must not be shared in chat. Account-link implementation can
continue while that external setup is pending.


## Quest account-link server checkpoint (2026-10-02)

Implemented a disabled browser-to-book pairing backend on the original Firebase
project. It verifies the distinct Quest/web App Check app IDs, requires a recent
Google sign-in plus explicit browser approval, and issues one custom token per
approval for the verified existing UID. Codes expire after five minutes; device
secrets are never in the browser URL, and stored credentials are hashed. Atomic
approval, redemption, cancellation and account-deletion guards keep races from
reassigning an identity or issuing duplicate credentials. Bounded HTTP parsing,
strict shapes and committed Firestore throttles fail closed.

The shared client now selects a separate named Quest Firebase registration in the
same project; phone/web initialization remains unchanged. Browser approval UI,
book pairing lifecycle, custom-token sign-in and real-provider acceptance are
still pending. The endpoint remains disabled and undeployed. See
[managed-access protocol](QUEST_MANAGED_ACCESS.md).

The owner supplied public Meta app ID `1763835394893209` and selected
`com.maestro.quest` subject to Meta's package-availability validation. The native
resource records that ID with `enabled: false`; development remains
`com.maestro.quest.development`. No secret was requested/received, no dashboard
features were requested, and no build was uploaded or installed. Current code
uses local entitlement and attestation, with none of the optional Meta user-data
APIs listed on DUC; final audience, privacy and purchase scope still need review.

Local validation: **82 Functions unit/CORS tests** pass, including **24 new
pairing cases**. The complete Firestore emulator suite exited 0; a subsequent
focused run also verifies deleting an account removes pending Quest approvals.
The full shared web suite passed **1,800 tests / 210 files**, followed by **31
focused tests** after adding two more invalid-registration cases. TypeScript,
ESLint and production web compilation pass. CI verifies the final combined suite.
No Unity C# or Android native code changed; the previous native APK remains the
last packaged artifact and does not contain this checkpoint's web/config changes.


## Quest account-link client checkpoint (2026-10-02)

Implemented the browser approval page and pairing inside the existing account
dialog. The user sees a five-minute code and fixed public URL, enters it in a
real browser, verifies the displayed Google account and explicitly approves.
The book retains the device secret privately, pauses polling when suspended and
finishes on return with the existing Firebase UID and shared managed backend.
No Google popup runs inside the book WebView. Native external navigation opens
only the exact credential-free approval URL after a main-frame user gesture.

Cancellation, closing the dialog, page replacement and expired codes prevent late
sign-in. One transaction owns custom-token redemption, SDK persistence and the
backend account/balance check. A pending marker and auth-state guard recover from
process death or cancellation during non-abortable Firebase work. Strict session
storage failures prevent commit or retain cleanup recovery; ordinary best-effort
phone/browser storage behavior remains unchanged. Refreshing a token also verifies
that its original account did not change in another browser tab.

Quest keeps its balance and BYOK interface but omits checkout and the phone app's
Stripe purchase note. The browser page refuses unconfigured/wrong-origin use;
production endpoints and provider enablement remain disabled. Real Meta/Firebase
acceptance, restart/sign-out, managed chat/Live/agent accounting, and physical
browser return still require approved release configuration and headset testing.
See [managed-access lifecycle](QUEST_MANAGED_ACCESS.md).

The full shared web suite passes **1,864 tests in 218 files**. TypeScript,
ESLint, core boundaries, prompt ownership and catalog-source checks pass. A real
headless browser checked the production React components with a development-only
synthetic adapter at 390×844 and 1024×1536: manual code entry, confirmation,
synthetic sign-in/approval, cancellation and unconfigured production-page behavior
pass with no page errors or external requests. The rendered screens were inspected.
This does not claim a real provider login or physical headset acceptance.

The final complete packaging helper exited 0 after the final SDK cancellation fix:
**616 EditMode and 417 PlayMode tests** pass, with three optional private-file
skips. Android has **75 passes and two optional private-archive skips**; lint
passes. Source and mirror hashes match 233 runtime, 133 test and 12 editor C#
files. All 121 bundled web files match, including the approval page and strict
account persistence. The included avatar and all 178 motion payloads match their
manifests. ARM64, native platform loader, permissions and APK v2 signature pass.

Development checkpoint: `MaestroQuest-account-link-8F80D524.apk`, SHA-256
`8F80D52472DC6FDA1BECE02A98123A2A1774A5CE863401E632DE2F0F1AB7B4AF`.
It remains uninstalled, disabled for live managed access and development-signed.
No production deployment, Meta dashboard action, Store upload or headset action
was performed. Local evidence is in `.quest-evidence/quest-account-ui/`; the
PR records the final committed-source CI result separately.


## Quest release packaging checkpoint (2026-10-02)

Added a separate release entry point sharing the tested development packaging
pipeline. Its public profile pins package/version, Meta app ID, distinct
same-project Firebase registrations, account-link/attestation endpoints and the
intended signing certificate. Unknown/private/debug fields fail validation.
Inherited web settings are cleared before compiling the profile-specific bundle;
a receipt hashes the profile and all web files, checked again inside Unity.

Release builds use an owned mirror, IL2CPP/ARM64 and a non-debuggable player with
no development entitlement bypass. The Meta resource is enabled only in the
mirror for packaging, then restored. Unity never receives release key paths,
aliases or passwords. A separate signing step reads passwords by environment
variable name, verifies the intended certificate and checks the final manifest.
Missing signing inputs fail before starting Unity. See
[release commands and profile](QUEST_RELEASE_BUILD.md).

The complete preparation command exited 0 using the committed synthetic fixture,
without a real release key or provider requests. **629 EditMode and 417 PlayMode
tests** pass, with three optional private-file skips. Android has **75 passes and
two optional private-archive skips**, and lint passes. The full web suite passes
**1,885 tests in 219 files**; TypeScript and ESLint pass. Two source-scanning tests
failed in the initial run during Android compilation, then passed individually
and in the full four-worker rerun without changing production code or tests.

Independent APK checks match 233 runtime, 134 test and 14 editor C# sources and
38 fixture files to the mirror. All 122 packaged web files match, including the
receipt covering 121 files. The avatar and all 178 motions match their manifests.
The APK contains enabled Meta app ID `1763835394893209`, package
`com.maestro.quest`, API 32/34, ARM64 and a valid development v2 signature; its
manifest is non-debuggable. Source and restored mirror Meta resources remain
disabled. Package availability and actual provider configuration are unverified.

Preparation artifact: `MaestroQuest-intermediate-20261002-164909.apk`, SHA-256
`0577EF304F73927E0E0809232B255C00D0BE76936238053E248C39484AF221A1`.
It uses synthetic endpoints and a development certificate, **not** a release
candidate. Actual release signing has not been exercised; no production deploy,
Store upload, dashboard action or headset operation occurred. Local evidence is
in `.quest-evidence/quest-release/`. Real configuration, signing, provider/device
acceptance and Store work remain open.


Development regression also completed with helper exit 0 after adding an
explicit normal batch exit following a successful build report. The initial
attempt built an APK but stalled on shutdown and was correctly rejected; its
logs remain separate. Final checks again pass 629 EditMode, 417 PlayMode and 75
Android tests, with the same optional skips. The development manifest restores
`com.maestro.quest.development` and debuggable mode; all 121 web files and native
content match. No release receipt or synthetic provider configuration remains
in the development web bundle.

Development checkpoint: `MaestroQuest-release-tools-6ED5F072.apk`, SHA-256
`6ED5F072CFBC987AAAE0DEB9BF462B519DAF5E1BD4D2D4F8DF7A8406DD358943`.
It is development-signed and uninstalled. The final full helper used the
credential-scrubbing verification path and the explicit batch-exit fix. The
release preparation artifact above predates that development-only exit change;
its release builder and profile/content validation code are unchanged.


## Quest adult audience and privacy checkpoint (2026-10-02)

The owner chose adults 18+ for Quest v1. The book now requires an unchecked
self-confirmation checkbox and a separate Open action before mounting the original
chat, account, media and agent hooks. Under-18 decline keeps chat inaccessible;
a fresh book document asks again without storing a birth date or age record.
Phone and ordinary web entry are unchanged. A restricted welcome-screen bridge
acknowledges native suspend/resume without enabling room, file or account actions,
so reading a policy does not trip the WebView shutdown watchdog.

The native browser now permits the exact public privacy and Gemini terms URLs,
as well as the existing account-approval page, only after a top-level user
gesture. Queries, fragments, foreign hosts and custom schemes remain rejected.
The prepared public policy explains room/agent context, retained Live handoff
media, local exports, scan/pose boundaries and Meta integrity/account-link data.
See [privacy map](QUEST_PRIVACY.md) and [managed audience decision](QUEST_MANAGED_ACCESS.md).
No policy deployment or dashboard certification is implied by these source edits.

Updated the affected Transformers, ONNX, sharp, adm-zip and brace-expansion paths.
The remaining root Firebase gRPC server advisories are documented as unreachable
in the root App/Auth/App Check usage. CI permits only the exact reviewed advisory
URLs and dependency edges until 2026-11-01; it rejects new findings and critical
escalation. Functions and Live gateway keep independent audits. See
[dependency review](QUEST_DEPENDENCY_REVIEW.md); ordinary root audit remains nonzero.

Desktop validation: the full existing suite plus audience tests passes **1,890
tests in 220 files**, with five additional audit-guard tests passing separately.
TypeScript and ESLint pass. The production entry browser probe checks decline,
reload, pause/return, prevented native commands, deliberate entry, unchanged phone
entry and no AI/account request before confirmation. Browser captures cover
1024x768 and 800x600 with scrolling and no horizontal clipping. The updated
production q4 Whisper worker transcribed eight seconds of generated fixture audio
with real WASM inference (2.1 seconds on this PC); this is not Quest performance
evidence. Native packaging and exact-commit CI results are recorded in the PR and
local `.quest-evidence/quest-privacy/` evidence when complete.

Adult positioning/distribution, actual provider billing/region eligibility, public
policy deployment, device acceptance, release signing and Store submission remain
open. Self-confirmation is not verified age. Native manual room editing remains
separate from the book/AI gate; no additional Gemini path was introduced.


### Reusable planar drawing increment (2026-10-03)

`drawingSurfaces.v1` gives created objects explicit flat patches, local ink,
shared configure/add/splice/erase/readback and session-local physical tool choices.
The tenth editable starter is a chalkboard. Retained physical strokes coordinate
ownership and preserve failed saves; copying, Undo, temporary rooms and portable
archives carry the same data. See [surface drawing](QUEST_SURFACE_DRAWING.md) for
bounds and persistence versions. This does not complete arbitrary mesh painting,
user-configured drawing-tip objects, scanned-wall overlays, fidgets or liquids.
Quest acceptance is pending; no device changes are part of desktop verification.

### Configurable drawing-tip increment (2026-10-03)

`drawingTips.v1` adds a saved tip to any created/imported object root or stable
recipe part. The eleventh starter, Chalk, is ordinary editable component data.
Held tips use the shared surface capture and ownership path; ink settings remain
independent of tray preferences. One capture per room, separation after blocking,
explicit failed-save retry, Undo and temporary-room behavior are covered by native
tests. The shared catalog provides revision-bound editing and exact readback.
See [surface drawing](QUEST_SURFACE_DRAWING.md) for limits and current save formats.
Physical contact comfort, tracking loss and maximum-load Quest performance remain
unverified; no headset installation is part of this desktop checkpoint.


### Connected blueprint increment (2026-10-03)

Version-2 batch blueprints bind member slots to fresh IDs and save all pieces and
hinges in one Undo, with complete graph/placement/room-budget validation. The
second included module, Spring lever, is ordinary source using this contract.
Users and agents can inspect/copy/change it through the shared library and forms.
The reusable native hinge component handles movement and spring return. No
special fidget execution path, provider call or automatic physics start is added.
See [batch creation](QUEST_BATCH_CREATION.md) for bounds. Quest interaction and
performance acceptance remain pending, as do snapping, broader mechanisms,
container effects and release gates.


### Shared file-publication reliability increment (2026-10-03)

Native replacement writes now share bounded Windows-only handling for three known
pre-publication IO refusals. Staging, flushing, backups, ownership and recovery
remain with each store. Unknown/partial-rename/permission/full-disk errors still
fail explicitly. Receipt reservation must publish before any effect starts; no
runtime action or uncertain effect is retried. See [action recovery](QUEST_ACTION_RECOVERY.md).
This addresses the observed transient publication class without claiming its
external cause or closing device storage, abrupt-power-loss or frame-time gates.


### Reusable construction capture increment (2026-10-03)

Existing creations can be captured into an ordinary constructor module instead
of a second blueprint database. Capture preserves native/imported geometry,
paint, drawing components/ink, physics/collision, recorded root motion and closed
hinge connections. New instances do not depend on their originals. One shared
public prototype schema, generated forms and the existing module library serve
both the user and agent. See [batch creation](QUEST_BATCH_CREATION.md) for exact
boundaries and model portability. This extends the authoring foundation; it does
not close Quest interaction/performance, snapping, effects or release gates.


### Construction point and grip snapping increment (2026-10-03)

Saved, editable root-local snap points now survive templates, copies and reusable
construction capture. `object.layout.snap` places or joins a complete selection
through one shared saved edit/Undo, including relative scaling. Session-local
`room.selection.snapSettings` enables the physical construction handle's nearest
compatible point preview. Generated book controls and Maestro use that same
catalog; an explicit fact exposes the current grip preview. Ordinary item grips
keep their existing throw semantics. See [snap points](QUEST_SNAP_POINTS.md) for
matching, cancellation, breaking limits and the absent occupancy/overlap policy.
This advances the construction kit; it does not complete bounded water/snow,
remaining default assets, Quest acceptance or the account/Store release gates.


### Measured liquid-container foundation (2026-10-03)

The shared container component adds saved millilitres, bounded logical transfers,
current-state facts and lightweight level meshes. The same component travels with
copies, prototypes and room snapshots. Both transfer endpoints use ordinary
ownership/revision guards and one atomic saved Undo; failed publication leaves
both unchanged. See [containers](QUEST_CONTAINERS.md) for logical capacity,
format boundaries and exact limitations. Physical pouring, streams/spills,
water/snow fields, remaining default assets and Quest/release gates remain open.


### Bounded physical pouring (2026-10-03)

Tilting the same configured vessels now produces a bounded gravity stream while
scanned-room physics is active. Solid geometry clips the stream; a compatible
opening receives only its available capacity. Misses and overflow are accounted
for as uncollected spill. This is a logical-volume approximation, without per-drop
rigidbodies, persistent puddles, fluid forces or buoyancy.

Live quantities publish through the ordinary room journal after a quiet interval,
grip release, pause, Save or bounded checkpoint. Failed publication reverts the
whole episode. The generated catalog adds `object.container.live` and
`object.container.poured` for the same user/agent event programs; no additional
per-toy tool is needed. Two-handed grips, typed event delivery, publication/Undo,
collision blocking, overflow and failure recovery are desktop acceptance cases.
Quest pouring comfort and performance remain open. See [containers](QUEST_CONTAINERS.md).


### Editable outline extrusion (2026-10-03)

Native closed-outline extrusion extends the existing recipe evaluator and shared
part schema. Concave, bounded XY profiles become solid geometry with explicit
thickness, caps, normals and UVs. The book editor, agent/program calls and paged
profile reads share exact saved source. Invalid topology is refused before mesh
allocation; saved edits/Undo, temporary forks and generated-vertex admission keep
the existing lifecycle. Room v10, snapshot intent v9 and archive v9 protect new
geometry on downgrade. See [recipe authoring](QUEST_RECIPE_AUTHORING.md).

This does not finish sweep/CSG, the full play kit, persistent water/snow or the
hardware, provider and Store acceptance gates. No headset or paid generation is
used by this increment.


### Editable profile sweep increment (2026-10-04)

The existing native recipe evaluator now transports a closed editable profile
along a bounded open 3D path. Shared creation/editing, profile/path queries, book
point controls and user/agent source remain one system. Admission rejects invalid
outlines, reversals, folded sides and generated bounds overflow; saved edits,
Undo, temporary rooms and generated-mesh disposal retain their existing semantics.
Room format 11 and snapshot/archive formats 10 protect new geometry on downgrade.
See QUEST_RECIPE_AUTHORING.md for exact bounds and frame conventions.

This advances handles, bent rods and user-authored parts. General CSG, curved
surface painting, persistent water/snow, the remaining play kit and all unverified
headset/provider/account/Store release gates remain open.


### Shared virtual-room view (2026-10-04)

`room.view.capture` supplies a bounded native JPEG through a separate acknowledged
image channel. The optional catalog and app-owned agent inspect the same pixels;
agent-captured images are retained in task details and validated on backup import.
No Gemini client, camera permission or physical-camera integration is added to
Unity. Native facts and receipts still determine exact state and outcomes.
See [virtual-room view](QUEST_ROOM_VIEW.md) for scope, retention and limits.
Quest render/readback performance and live-provider interpretation remain open.

## Editable default constructions (2026-10-04)

Added Small fort and Passive spinner to the included pinned program library.
The fort creates sixteen independently editable bodies: a fixed base, twelve
bricks in four towers and three loose walls. The spinner creates a fixed mount
and a three-lobed passive-hinge rotor. Both are ordinary constructor source,
with no new capability, interpreter, room format or auto-started physics.

The shared web/native fixtures cover exact pins and editable copies. Native
acceptance coverage exercises the real fort program, one-batch save/Undo/Redo, stable
stacking, ball knockdown and structure reset, plus spinner contact/anchor
retention and capture/copy with fresh internal hinge identities. The full-app
verification journey inspects both included sources, saves and runs callers, verifies exact
member counts and undoes each batch. Unity previews show the real geometry.

Quest grip/direct-touch feel, stability at other scales and sustained performance
remain device acceptance items. The wider play kit, provider and Store gates
remain open. No device installation, provider call, paid generation, deployment,
release signing or submission was performed for this increment.


### Shared vessel scooping and editable water defaults (2026-10-04)

Added Bucket and Water basin templates with editable geometry, collision, physics
and version-1 container data. Dipping a smaller upward-facing open cavity inside
a larger liquid store transfers conserved contents at bounded rates. It shares
pouring's existing live episode, publication/rollback, temporary-room semantics and
Undo. Lift out to stop; pour into another configured vessel using the same data.
Solid obstructions and opening/cavity bounds prevent filling through a wall or lid.

The catalog adds `object.container.scooping` and `object.container.scooped` behind
`containerScooping.v1`. The existing live quantity schema and outgoing pour counters
remain unchanged. User and agent programs use the same typed event, including
saved scoop amount, donor count and liquid identity. No saved format was added.
Native geometry/interaction, concurrency, persistence and event tests plus shared
client/browser probes cover this increment. Current Quest acceptance is pending;
full release, persistent fields, fluid forces and bare-hand scooping remain open.
See [the contract](QUEST_CONTAINERS.md#physical-vessel-scooping-2026-10-04).


### Liquid identity boundaries and Cup metadata cleanup (2026-10-04)

Empty/refill now publishes the current physical-flow episode before a different
liquid identity or colour can enter a participating vessel. Both scooping and
pouring retain correctly attributed event totals; matching refills stay in one
episode. Failed boundary saves restore the old quantities and block further flow.
Five native regressions cover both physical intake paths, matching and colour-only
refills, conservation, failed publication and separate Undo/event outcomes.

The unreleased Cup template description now reflects implemented pouring and
scooping. Geometry/components are unchanged; its new exact hash is an explicit
pre-release revision, with no alias or silent replacement of an old draft choice.
Existing expanded objects keep their source. Release template identities remain
immutable. Current Quest acceptance and broader v1/Store gates are still open.


### Shared curved drawing patches (2026-10-04)

The existing drawing component now supports explicit cylindrical and spherical
patches. Surface-local source, shared capability fields, controller/hand pencils,
held drawing objects and rendered ink use one geometry mapping. Adaptive bounded
subdivision counts against the shared drawing budget. Erasing, failed-save retry,
copy/capture, Undo and temporary rooms retain the existing paths. See
[the drawing contract](QUEST_SURFACE_DRAWING.md#curved-patches-2026-10-04).

This uses surface version 2 only for curves, room v12, paired snapshot intents v11
and archive v11. Planes and clean prior rooms remain readable; older builds must
not fall back past current saves. Native suites (761 EditMode and 564 PlayMode,
with three optional private-file skips), the full shared-client journey, 2,197 web
tests and eight browser checks passed. Empty patch lists and end-of-page reads
now use their declared types while preserving stale-revision rejection. Current
Quest acceptance remains pending.
Arbitrary mesh/skin painting, scan overlays, remaining play kit, persistent
water/snow and wider release gates are still open.


### Shared drawing kit and held erasers (2026-10-04)

Pencil, Paint brush and Eraser extend the editable default library to sixteen
objects. Their shapes, collision, physics and draw/erase tips are ordinary source;
users and the agent can configure the same component on created/imported roots or
recipe parts. Held erasing previews complete selected strokes and commits a contact
gesture as one saved edit/Undo. Shared `removeStrokes` accepts exact unique IDs;
invalid or missing selections refuse atomically. Recovery retains that selection,
checks the original patch on retry and restores ink on discard.

The capture fact and result distinguish drawing/erasing. Explicit tip modes and
batch erasing require `drawingErasers.v1`. Erasing tips use component v2, room v13,
paired snapshot v12 and archive v12; clean prior room files remain readable and
older originals are preserved. See QUEST_SURFACE_DRAWING.md. Desktop verification passed 2,203 web tests across 249 files, TypeScript and
ESLint, 766 Unity EditMode tests and 571 PlayMode tests (three optional private-file
checks skipped). The real shared-client native journey passed 315 observations;
eleven Chrome journeys matched recorded native calls and receipts, including
duplicate-ID refusal and configurable erasing. Actual Unity starter renders and
generated controls were visually inspected. Package evidence is recorded separately.
Quest contact feel and maximum-load performance remain pending; this increment
does not complete the wider play kit, persistent water/snow, provider or Store gates.


Drawing-kit package checkpoint: `MaestroQuest-drawing-kit-576FED7F.apk`, SHA-256
`576FED7FECD9FA8793EF400AF9AE12B689B430C3ECBF90DEACD2DCCA479712A7`.
Production web build, Android lint, 76 Android unit tests (two optional skips),
IL2CPP packaging, signature and 16 KiB alignment passed. The package audit matched
1,764 frozen native inputs, 137 web files and exact default-avatar/motion/template/
module bytes. The local development APK is not installed; no provider, paid
asset-generation, deployment, release signing or Store submission occurred.


### Editable chess kit and patterned recipes checkpoint — 2026-10-04

The default library now ships a 64-square board and six editable chess piece
types, with two ordinary 16-piece construction modules. The complete physical set
uses 33 room objects. Existing physics, gripping, Foot-to-square snapping, saved
programs, temporary rooms and Undo remain shared. No chess rules or occupancy
system is hidden in native code. Pattern fields (solid/checker/stripes, projection,
counts and second pigment) extend recipe source, facts and the book part editor.
They add no individual square meshes or colliders.

Validation: 2,233 web cases across 250 files; 771 Unity EditMode and 575 PlayMode
passes (three optional private-file skips); 335 full-app native observations; twelve
Chrome journeys; 76 Android unit passes (two optional skips), Android lint and
IL2CPP development packaging. The full set settled under real native physics,
E2-to-E4 snapping and Undo passed, and all 64 checker squares passed rendered
pixel sampling after rotation. Whole chat-envelope sizes are now checked for
included constructors. The final book controls use wrapping fields with 44 px
inputs, and exact native edit/receipt matching passed after the layout change.

APK: `MaestroQuest-chess-kit-0876C981.apk`; SHA256
`0876C9818AB8004B8458165584551F21CB24E12251C9B069F4E883BCFE811475`. Package audit matched 1,805 frozen inputs,
304 runtime files, 191 test files, 15 editor files, 60 fixture payloads, all
143 web files, the AAR, default avatar, 178 motions, 22 templates and seven
modules. Signature, development manifest, arm64 ABI and 16 KiB alignment passed.
Room format 14 and paired intent/archive format 13 preserve patterned source.
This is a development checkpoint, not a release approval or headset acceptance.
The APK remains uninstalled; device work is on hold. No paid generation/provider
calls, deployment, release signing or Store submission occurred.

Remaining: the open device QA and provider/billing gates, further reusable
play-kit coverage, persistent water/snow fields, arbitrary mesh/skin drawing,
Meta/Firebase integrity/account linking, privacy/payment setup, release signing
and Store submission. See the creation-template contract and device checklist.


### Shared sculptable surface checkpoint — 2026-10-04

A reusable saved height-field component now supports the Snow patch default and
user-created fixed objects. The book's generated fields, event programs and agent
use the same configure/reset and raise/lower/level actions, exact revisions and
paged heights. The native mesh and collision share the accepted source. Surface
changes wake nearby sleeping free bodies so a ball falls when its support lowers;
held or animation-owned objects keep their existing ownership. A nonconvex mesh
also refuses an obstructed aimed throw without unsupported ClosestPoint queries.

Copy, captured prototypes, saved edits, Undo and temporary rooms preserve exact
independent grids. Room v15 and paired intent/archive v14 protect this source;
future component versions preserve original data. The room allows four fixed
fields, each at most 289 heights / 549 rendered vertices / 642 triangles, with one
additional collider per field. Default content now contains 23 templates and
seven editable program modules. The shared catalog has 83 actions, 89 facts and
15 events. This is authored geometry, not conserved material or a fluid solver.
Finger/tool sculpting, material transfer/packing and sustained Quest performance
remain open. See the world-authoring contract and device acceptance checklist.


Desktop verification passed 2,257 web tests across 251 files, TypeScript, ESLint,
generated catalog and bundled-asset checks; 776 Unity EditMode and 578 PlayMode
checks passed, with three optional private-file tests skipped. The complete native
shared-client journey passed 344 observations. The Chrome surface editor matched
that run's exact sculpt call, revision and receipt, including oversized-brush
refusal. Actual native surface/default renders and book controls were inspected.
These establish desktop behaviour only; the current Quest field/contact/frame-time
acceptance and wider provider/Store gates remain open.


### Physical sculpting checkpoint — 2026-10-04

Controller triggers, tracked index fingertips and held tools now use the same
bounded height-field evaluator as book/agent actions. A reusable saved sculpt tip
works on any supported created/imported object root or recipe part. The Sculpt
brush default is ordinary editable source. Its settings are independent of the
tray; loose tools are inert, solids block contact, and program-held tools cannot
preempt a user's edit. Physical controls remain 3D objects outside the book.

A gesture samples at most 32 points and saves as one edit/Undo. Visible geometry
previews the draft; collision/source stay accepted until successful publication.
Tracking loss, pause and failed saves retain the exact in-memory draft for explicit
Retry/Discard through shared facts/actions or the tray. Retry requires the original
field, session and ownership. Workspace/temporary-room changes remain blocked
while a draft is active/retained. App destruction loses unsaved draft memory.

Room v16 and paired intent/archive v15 preserve sculpt tips through copies,
prototypes and workspace saves. The catalog has 86 actions, 93 facts and 15 events;
the included content has 24 templates and seven modules. This is shape authoring,
not conserved snow, material transfer/packing or a general fluid solver.

Verification: 2,265 web tests across 252 files, TypeScript, ESLint, catalog and
asset checks passed. Full native suites passed 779 EditMode and 585 PlayMode tests,
with three optional private-file skips. Visual review then corrected long tray
label wrapping and preview framing; focused native checks and final renders passed.
The final full-app shared-client journey passed 355 observations. Chrome matched
its exact sculpt-tip call, revision and successful receipt. Native brush/tray and
book controls were visually inspected; physical Quest acceptance remains pending.

Production web build, 76 Android unit tests (two optional skips), Android lint and
IL2CPP development packaging passed. The APK audit matched 1,841 frozen inputs,
315 runtime files, 195 test files, 15 editor files, 61 fixture payloads, the Android
AAR and all 145 web files. The bundled avatar and all 178 motions matched exactly.
The development manifest, arm64-only payload, signature and 16 KiB alignment passed.

Checkpoint: `D:/Projects/Builds/MaestroQuestVerify/Builds/Checkpoints/MaestroQuest-physical-sculpt-3F822003.apk`.
SHA-256: `3F822003749152EF6D52B5DC4901298B444C632365CA2F7E9812E82B3069F0F3`.
Evidence: `.quest-evidence/physical-sculpt`. The package remains uninstalled; device
work is on hold. No paid generation/provider calls, deployment, release signing or
Store submission occurred. Remaining device/provider/Store gates, broader play-kit
coverage, material transfer and arbitrary mesh/skin painting remain open.


### Shared surface-volume transfer checkpoint — 2026-10-04

User/agent programs and the generated book form now use `object.field.transfer`
to move local geometric volume between two compatible fixed height fields.
Footprints respect actual grid triangle weights, availability and headroom.
The result exposes measured removal/addition and bounded rounding error;
unrepresentable amounts refuse. Both accepted meshes/colliders save atomically
with one Undo, current revisions, shared ownership and receipt replay protection.
No new saved fields or format bump. See QUEST_SURFACE_TRANSFER.md.

Verification: **2,276 web tests in 253 files**, **784 Unity EditMode** and **588
PlayMode tests** passed; three optional private-file tests skipped. TypeScript,
ESLint, catalog provenance and included-asset integrity checks passed. The full-app
shared-client journey passed **367 observations**, including both volume readbacks
and Undo. The real Chrome book editor matched the exact native call/receipt,
required both current revisions and refused an oversized quantity. Both page
screenshots were inspected; headset legibility remains unverified.

Production web, Android lint, **76 Android tests** (two optional skips) and IL2CPP
packaging passed. The package audit matched **1,853 frozen native inputs**, **318
runtime / 197 test / 15 editor C# files**, **62 fixture payloads**, the native AAR
and **145 web files**. The catalog has **87 actions / 93 facts / 15 events**. Exact
packaged bytes match the default avatar, 178 motions, 24 templates and seven modules.
ARM64-only libraries, development manifest, v2 signature and 16 KiB alignment passed.

Checkpoint: `D:/Projects/Builds/MaestroQuestVerify/Builds/Checkpoints/MaestroQuest-field-transfer-D728260E.apk`.
SHA-256: `D728260E5590F3E8E9659BD1BE26043CFB7A3316FE31E81239680ADFB76045D3`.
Development signing; **not installed**. No provider, paid generation, deployment,
release signing or Store submission occurred. Physical shovelling/carried snow,
snowball packing, real Quest performance/comfort and the existing provider,
account/payment/privacy and Store gates remain open. This checkpoint does not
complete the v1 goal or claim exact physical mass conservation.

### Measured material and snowball packing checkpoint — 2026-10-04

`object.material.pack` removes measured local volume from a fixed surface and
creates one editable sphere with matching collision and saved contents. The book,
agent and programs share its generated schema, current-source check and typed
result. `object.material.edit` and `object.material` expose the reusable carried
quantity component. Source loss and object creation save atomically; one Undo
restores the patch and removes the ball. Copies/prototypes, archives and temporary
rooms retain the component. Room v17 and paired intent/archive v16 preserve the
new data; current-room paths now use the native filename constant.

Tests exposed and fixed the shared journal's baseline rule for mixed edits and
new objects. Persistence now validates that baseline before writing, and Undo
uses observed poses for existing members without inventing a prior new-object pose.
See QUEST_MATERIAL_PACKING.md for quantities, lifecycle and extension boundaries.

Verification: **2,289 web tests in 254 files**, **791 Unity EditMode** and **592
PlayMode tests** passed, with three optional private-file skips. TypeScript,
ESLint, catalog provenance, bundled assets, production web, Android lint and **76
Android tests** (two optional skips) passed. The packaged-build full-app native
shared-client journey passed **377 observations**, without a provider. Chrome
matched that journey's exact packing call and receipt, required the current source
revision and refused oversized quantity. Native ball/surface rendering and the
two-page form/result were inspected. Real XRI grip/release, gravity, surface contact,
quantity/save consistency, Undo/Redo, receipt replay, sculpt ownership, save failure,
capacity, temporary discard and archive retention passed desktop checks.

The APK audit matched **1,865 frozen native inputs**, **321 runtime / 199 test /
15 editor C# files**, **63 fixture payloads**, the native AAR and all **145 web
files**. Catalog: **89 actions / 94 facts / 15 events**. Exact packaged assets match
the default avatar, 178 motions, 24 templates and seven modules. Development
manifest, arm64-only libraries, v2 signature and 16 KiB alignment passed.

Checkpoint: `D:/Projects/Builds/MaestroQuestVerify/Builds/Checkpoints/MaestroQuest-material-packing-7602FD63.apk`.
SHA-256: `7602FD6348144C46FFE3E621E3A85198C8671B2456110D77C332DAE2BCE0A9F0`.
Evidence: `.quest-evidence/material-packing`. **Not installed**; headset work remains
on hold. Physical shovel capture, hand-packing gestures, shared liquid/material
adapters, arbitrary mesh/skin painting, provider/device acceptance and account,
payment/privacy/signing/Store gates remain open. This checkpoint does not complete
v1 or claim granular simulation or physical mass conservation across authored edits.


### Rectangular containers and shallow pool checkpoint — 2026-10-04

The existing container component now supports rectangular cavities. The 26th
editable template, Shallow pool, shares ordinary dipping, pouring, accepted/live
quantities, events, failed-save rollback and one atomic Undo. Its box-volume plane
is shared by display, overflow and immersion. No pool-specific action or separate
simulation was added. Room v19 and paired snapshot/archive v18 protect the shape.

Loading current action inputs now copies a complete typed record without aliasing
the fact snapshot. Reusable program authoring expands optional input records into
explicit typed member bindings from one visible read. Both cylinders and rectangles
use current quantities and revisions; unavailable facts fail before the action.
The native and web fixture checks cover this shared generation path.

Verification: **2,367 web tests in 259 files**, **814 Unity EditMode / 624 PlayMode**
passes, with three optional private-model skips. Full application and driver type
checking, lint, catalog provenance and all bundled assets pass. The complete native
journey passed **473 observations**; the original book/chat journey passed **72**
with scripted offline provider responses. The Chrome form matched the real native
edit and completion receipt, preserved loaded contents and refused incomplete
rectangle dimensions. Pool and book renders were inspected. Tests also cover
wall/floor immersion refusal, conserved scoop/pour-back, failed saves, Undo and
temporary discard. These are desktop checks, not headset acceptance.

Production web, Android lint, **76 Android tests** (two optional skips) and IL2CPP
packaging passed. The package audit matched **2,794 frozen inputs**, **336 runtime /
208 test / 15 editor C# files**, 66 fixture payloads, the AAR and **147 web files**.
The catalog contains **92 actions / 98 facts / 16 events** and 291 provenance
sources. Exact packaged assets match the included avatar, 178 motions, 26 templates
and seven modules. Development manifest, arm64-only libraries, v2 signature and
16 KiB alignment passed.

Checkpoint: `D:/Projects/Builds/MaestroQuestVerify/Builds/Checkpoints/MaestroQuest-shallow-pool-71DC12CA.apk`.
SHA-256: `71DC12CA18A6BEC2A2B75310986FBA4E6A32B4C1823347D4FE42AE7D8441B94D`. **Not installed**; device work remains on hold.
Evidence: `.quest-evidence/rectangular-containers`. No paid generation, real-provider
calls, deployment, release signing or Store submission occurred. Uncontained
puddles/flooding, buoyancy and a general fluid solver are not implemented by this
bounded reservoir. Quest comfort/performance, provider/account/payment/privacy,
signing and Store gates remain open; the v1 goal is still active.


## Selected camera sources (owner clarification, 2026-10-07)

The absence of physical-camera access in the current development APK is an
implementation gap, not a v1 product restriction. The owner wants the existing
web/phone camera picker to offer independent Quest sources:

- **Real-world camera:** forward RGB camera using the Passthrough Camera API.
  Label it as a camera view, not an exact eye view; its field of view differs.
- **My mixed-reality view:** explicit user-initiated headset screen sharing via
  MediaProjection, including the real background, virtual content and interface.
- **Virtual scene:** authored Unity content without physical-room pixels.
- Keep the existing generated-image and Off choices, and make **audio-only Live**
  available without any camera or screen-sharing grant.

Route selected sources through the same preview, attachment, visual-context and
Live input pipeline used by web/phone. Gemini transport, subscription/BYOK,
conversation history and delegated-agent ownership remain in the shared app.
Do not add a second Unity Gemini client. Source identity and capture time must
survive handoff so virtual or generated pictures cannot be described as direct
physical-camera evidence. Selecting one source must never silently substitute
another after denial, unavailable hardware or expired screen-sharing consent.

Native capture must own permissions, bounded frame delivery and cleanup on
source changes, interruption, revocation and shutdown. Preview/frame budgets
must be measured on Quest; do not stream full-rate pixel payloads through JSON.
MediaProjection is the user screen-sharing path, not a hidden scene-inspection
shortcut. Test projection conflicts with developer capture tools explicitly.
Existing native virtual-room snapshots are not yet selectable video cameras.

Acceptance requires real Quest preview/snapshot/Live checks for each source,
managed and BYOK provider transport, source switching and permission denial,
physical/virtual visibility checks, interruption/restart, no retained stale
frames after Stop, and performance/thermal evidence. Mock and Editor tests do
not establish physical-camera or compositor capture acceptance.

References verified 2026-10-07:
[Passthrough Camera API](https://developers.meta.com/vr/documentation/unity/unity-pca-overview/),
[Unity camera integration](https://developers.meta.com/vr/documentation/unity/unity-pca-documentation/),
[Headset screen sharing](https://developers.meta.com/vr/documentation/native/native-media-projection/).
Implementation and device acceptance of these camera sources remain open.


## Live real-world occlusion (owner clarification, 2026-10-07)

Real objects visible to the headset, including objects absent from the saved room
scan, must hide virtual content behind them. Add Meta Environment Depth support
across book surfaces, avatar/import materials, user geometry, pencil silhouettes
and world labels. Use physical tracking-space alignment in MR; disable physical
depth in virtual locomotion mode and exclude it from virtual-only captures.

Keep hands in the live depth map initially. Test near-field hands and controllers
separately; use aligned tracked masks where the live depth is insufficient, with
proper removal when tracking is lost. Do not claim exact coverage of transparent,
reflective, very small or very close objects from depth data alone. This changes
visibility; existing scanned-room collision and authored rigid-body physics
remain separate. Depth does not automatically create dependable moving colliders.

The first implementation uses hard occlusion to preserve opaque pigment/depth
writes. Device acceptance must check real furniture moved since the scan, hands,
controllers, book readability, both eyes, MR/virtual transitions, permission and
focus interruption, and performance. Soft edge refinement follows measured
results rather than introducing transparent outlines or invisible depth writes.

## One world with adjustable reality layers (owner clarification, 2026-10-07)

The owner wants passthrough, a blended world, and a fully virtual environment to
be views of the same persistent world. The current binary VirtualRoomView and its
virtual-only thumbstick gate are an interim implementation, not the intended v1
restriction. This also supersedes treating physical-depth shutdown during all
artificial movement as the final architecture.

Expose independent shared capabilities, with the same state/receipts available
to the book, voice agent and user-authored programs:

- Environment blend: real surroundings, adjustable mixture, or fully virtual.
  Preserve clear book text by default; allow separate per-object/surface opacity.
- Environment source: included or imported virtual surroundings using the normal
  asset library, stable identities and Quest import budgets.
- Locomotion: disabled, or user-controlled movement/turning of the virtual world
  relative to physical tracking, including while passthrough is visible.
- Real-room collision: an explicit setting independent of visual blend, usable
  in mixed or virtual views. Do not infer physical behavior from opacity.
- Real-world occlusion: independently control where live depth masks virtual
  content. Fully virtual surroundings must not acquire unintended room-shaped
  holes. Hand/controller visibility can be an explicit separate preference.
- Passthrough windows: editable masks, including adjustable transparency, anchored
  to real surfaces or deliberately placed in virtual space. Window plane depth
  and live real-surface depth have different semantics and must not be conflated.

Keep the physical tracking frame fixed. Real scan geometry, depth reprojection,
physical-surface windows and tracked hands/controllers remain in that frame.
Maintain an explicit virtual-world transform for locomotion. Every spatial
observation, entity placement, target, ray, physics operation, saved program and
capture must identify/convert its coordinate space consistently. Moving the
virtual world must not move the saved real walls or mislabel an actual camera
frame. Keep room-anchored objects and virtual-world objects distinguishable;
allow explicit re-anchoring rather than silently changing their meaning.

This is a spatial-state refactor, not simply removing UserEnabled&&!Virtual.
Use one simulation with transformed physical geometry/queries, consistent
navigation and grab constraints, and an explicit policy for held objects during
world movement. Switching view/blend must not duplicate entities, reset program
state, forget the apple, or turn camera sharing on. The camera picker remains an
independent choice of what the user sends to Maestro.

Implement windows with the current compositor/framebuffer alpha approach.
Meta's surface-projected passthrough Unity APIs are deprecated for new development
as of SDK v83. References verified 2026-10-07:
[Passthrough Windows](https://developers.meta.com/vr/documentation/unity/unity-customize-passthrough-passthrough-windows/),
[Passthrough AR](https://developers.meta.com/vr/documentation/unity/unity-customize-passthrough-passthrough-ar/),
[Surface-projected deprecation](https://developers.meta.com/vr/documentation/unity/unity-customize-passthrough-surface-projected-passthrough/).

Acceptance remains open. Cover blend endpoints/intermediate values, physical and
virtual window anchors, imports, persistence and shared-agent edits, real-depth
alignment during virtual-world translation/turning, collisions independently
on/off, grab/throw/catch after movement, recall/recenter, source-accurate captures,
both eyes, and actual Quest performance. Current occlusion APK verification is a
separate incremental check and does not establish these new combinations.

### Authored living worlds: appearance, terrain, weather and water (2026-10-07)

The first native spatial refactor establishes an explicit sampled `RoomFrame`
between authored room coordinates and Unity rendering/physics coordinates.
Saved/live placements, layout/structure/snap baselines, Undo, physical placement,
recorded keyframes/playback/throw velocities, scanned-surface poses and virtual
capture metadata now share this boundary. Placement capture no longer assumes
that an object's immediate parent is the room. Direct-child captures preserve
exact values to avoid spurious saved revisions; distorted/sheared/reflected
frames are refused rather than silently flattened into a saved uniform pose.
Content-only edits keep the accepted pose when a transient display transform
cannot be represented; measured surface transfers stay independent of visual
scale. Explicit movement, copy and layout paths require a representable live pose.
Existing world-coordinate contact events and `object.position` retain their
documented meaning. This does not yet change XR-origin locomotion, implement
world/region identities or streaming, expose arbitrary hierarchy editing, or
define scaled-world physics. Uniform scale conversion in the coordinate helper
is mathematical conversion, not acceptance of miniature-world gravity or water.

Verification: 870 EditMode and 698 PlayMode tests pass, with the three expected
optional private-file skips. Both complete native room and original-book probes
pass. Five frame-math cases and four shared/manual interaction cases cover nested
parents, root translation/rotation, exact direct-child values, failed writes,
Undo and recorded throws. The first full run caught the material-transfer
admission regression; its corrected test now also asserts conserved contents and
unchanged display scale. These are desktop checks with scripted provider replies,
not physical headset, real-provider or scaled-world acceptance.

The next spatial increment removes scan-only navigation. Accepted editable
height-field colliders and the virtual floor now publish explicit walkable
surfaces. Navigation consumes these alongside scanned geometry, scoped to its
room owner; unrelated scenes and ordinary props cannot become ground through a
global layer search. Accepted mesh revisions, collider availability and local
transforms invalidate old routes. Sculpt previews do not publish a new walking
surface. Maestro drops cached path corners when that surface revision changes.
The bake is in the owner's rigid local frame, so relocating the whole frame
repositions the installed navigation data without rebaking it. Individual terrain
edits or movement still rebuild the affected current room synchronously.

Three failure-first native regressions cover accepted/preview/removed geometry,
collider disable/reenable, isolation from another world's floor, root relocation
without a new bake, and actual Maestro walking along the same sloping collider.
This retains current bounded height-field limits and the scan/alignment requirement
for starting physics. It does not yet implement independent virtual physics
readiness, user locomotion over terrain, fixed-tracking world movement, navigation
streaming or scaled-world physics. Continuous moving/streamed terrain needs a
budgeted navigation update policy; room-scale synchronous rebuilds are not a
city-scale performance guarantee. Six focused terrain/authored-motion cases pass.
The first full PlayMode run caught a diagnostic mismatch when a newly added wall
invalidated navigation earlier than the old collision sweep; the blocked movement
and accepted pose were preserved, and the explanation now describes the supported
walking surface. Physical Quest terrain/navigation acceptance remains open.

Independent simulation environment increment (2026-10-07): the shared
`physics.environment` fact distinguishes physical scan readiness, authored-ground
readiness and the selected collision policy. `physics.environment.set` changes
real collision participation with a current state identity, independently of
passthrough, depth and acoustic geometry. A changed policy pauses physics and
clears old throw speeds; explicit Start is still required. The generated book
controls, agent and saved programs use this same capability. Scan placement and
layout continue to require actual scan readiness, never a synthetic ready flag.

With real collisions enabled, simulation retains aligned-room containment. With
them disabled, accepted height surfaces and the virtual floor supply bounded
ground columns: up to 16 metres above and 0.25 metres below the actual collider.
Unsupported gaps and regions remain unavailable. Rigid-body contacts, navigation,
avatar/held-prop sweeps, throws and catch checks all apply the same scan policy.
The original scanned colliders remain available for placement and acoustics.
Ground loss pauses simulation; recovery never resumes it automatically. Dormant
objects outside admitted ground do not stop other supported objects; bodies that
actually leave their admitted region retain boundary recovery and pause behavior.

This is a runtime policy, defaulting to real collisions on in a fresh app world.
Persisted world/region settings, general region bounds, scaled-world physics,
terrain-aware user locomotion, fixed physical tracking during world movement and
replacement of the interim virtual floor remain required. These bounded columns
are not streamed city simulation, caves or a general surface-query implementation.
Appearance, weather and water delivery remain as specified below. Physical-device,
real-provider and sustained-performance acceptance of this increment remain open.

Verification passes 870 EditMode and 706 PlayMode cases (three expected optional
private-file skips), both complete native-room/original-book integrations and
91 shared-catalog/browser tests. The browser controls replay actual native
environment receipts; TypeScript, catalog provenance and included assets also
pass. Failure-first checks cover the missing capability and the dormant-object
global pause. The first whole-project run caught an outdated catalog mapping
count; a browser test fixture-name collision was corrected before its passing
rerun. No headset or provider operations occurred.


Motion-history increment (2026-10-07): authored avatar travel, carried-prop throw
samples and incoming catch samples now retain coordinates in their owning authored
room. Each use samples the current room-to-physics mapping. Translation and yaw
therefore preserve the actual action trajectory instead of appearing as a teleport,
a false throw impulse or a discontinuous incoming flight. Catch-arm smoothing
retains orientation relative to the avatar, so rotating the world cannot leave
its arm reaching toward the old world orientation. The arm also now consumes the
same real-collision policy as the ball; virtual obstacles and tracked controllers
remain blocking when scanned-wall collisions are excluded.

The motion frame keeps its owner and initial units. Lost ownership, invalid or
distorted transforms, changed scale and tilted gravity axes refuse continuation;
restoring a frame does not automatically resume a failed action. A genuine change
to Maestro's placement still interrupts authored travel. This supports continuity
of existing actions through a rigid frame move, not scaled-world physics.

Five failure-first regressions reproduced interrupted authored travel, incorrect
arm orientation, missed catches and a false prop throw reaching the existing
15 m/s clamp. The focused corrected cases pass alongside existing real native
throws, floor bounce, catch ownership and cancellation tests. Additional guards
cover invalid motion frames and collision-policy agreement. Test harnesses move
native bodies with their fixture root; that harness is not the production world
movement implementation. Whole-project and device acceptance are tracked separately.

The subsequent production-movement increment below supplies atomic body transfer,
physical binding retention and authored navigation-frame movement. The point-contract
increment below fixes queued point/event semantics in the current room. Independent
view lifecycle and recovery/persistence across all world modes remain release gates. Passthrough locomotion, view blending, world
regions, textures, weather and water are not enabled by this motion-history work;
those accepted requirements remain below.


Shared spatial-point contracts increment (2026-10-07): live object position,
attachment pose, recipe-part pose, aimed-throw input/output and contact/anchor-zone
event points now use authored room coordinates. Native physics still calculates in
physical space. The boundary converts each read/emission immediately and resolves
stored action points against the current room frame at execution. A delayed event
therefore keeps its authored location through translation/yaw. Physical speed,
radius and impulse retain their documented metric units; velocity and impulse use
room axes. Distance-only subscriptions continue measuring physical metres.

Eight changed definitions explicitly advance to version 2: `object.position`,
`object.anchor`, `object.recipe.pose`, `object.physics.trajectory`,
`object.physics.launch`, `object.collided`, `object.caught` and
`object.anchor.proximity.changed`. Recipe pose exposes `room` instead of the old
`world` record. The native and web program validators require the exact version;
omission means version 1 only for old nonsubscription native events. Human event
selection records the catalog version. Old sources remain preserved and unavailable
for repair; neither the agent nor storage silently relabels their coordinates.
Custom user events retain their existing unversioned scalar contract.

The owning room is still implicit in a running room program. These snapshots are
not durable physical anchors, cross-workspace point references or a region identity
system. Scale/gravity-axis changes remain unsupported during active watches; they
fail rather than reinterpret a sampled distance. This increment does not remove
the existing active real-physics locomotion guard, enable reality blending or
complete terrain/world persistence. A nested-parent impulse bug found during this
audit is fixed through the same authored frame, rather than its immediate parent's
axes.

Verification: 882 EditMode and 728 PlayMode cases passed, with three expected
optional private-asset skips. Forty-five focused cases included translated/yawed
throwing, anchor/part readback, delayed contact placement and nested-parent impulse.
Both full native-room and original two-page book journeys passed using their
scripted local provider; no Gemini request or headset operation was performed.
Web validation uses fresh Unity captures of throw, catch, part pose and anchor
programs; old-version rejection and visual version selection agree with native.

World/view lifetime increment (2026-10-07): Virtual and MR presentation no longer
rewind the virtual frame, pause physics, remove ground or take over autonomous
actors and programs. View changes retain Maestro's independent control opt-in;
returning to MR disables only user locomotion while swept MR travel is unfinished.
Actual focus/tracking interruption still disables live input through lifecycle
recovery, without rewinding content. Explicit Recall remains a separate recovery.
Physical scan tracking and world lock continue in Virtual view; presentation no
longer fabricates scan invalidation or treats Virtual as acoustic tracking proof.

The view-owned paper floor is removed. Accepted scene ground owns its geometry,
collision, navigation and lifetime. Planar user travel now requires accepted
level authored ground under the current/destination footprints and refuses
unsupported hills, edges and holes. Entering Virtual with no ground only changes
the view; it does not create an invisible collider or claim walking is ready.
Terrain-following locomotion, default environment authoring, cross-session frame
persistence, real-collision sweep admission and continuous blending remain open.

The changed controller.mode.set contract is version 2. Old resetting-view calls
are preserved for review rather than silently rewritten. Shared definitions,
native receipts and web consumers use the current action; controller.mode retains
its unchanged readback shape. Verification passed 883 EditMode and 730 PlayMode
cases, with three expected optional private-file skips, plus 83 focused native
cases and 1,050 shared room checks. Both complete native-room/original-book
journeys passed using local scripted responses. A real Chromium probe exercised
four generated mode controls against current native receipts. Initial probe
attempts exposed local server setup conflicts and a stale field-toggle step;
the corrected harness passed without changing product UI. No provider request or
headset operation occurred. These checks do not close physical acceptance.

Tool recovery increment (2026-10-07): B/Y,
Home, palm and creation-tray Recall now recover only explicitly registered book
and authoring trays. Created objects, Maestro, construction handles, user-authored
buttons, world pose, simulation, audio, programs and movement/view modes retain
their lifetimes. A growing country must never be collapsed into the old starter
object grid by a convenience control. Shared `room.tools.recall` and
`room.tools.recovery` expose the same native placement transaction to generated
book controls, agent discovery and programs, with exact recovery identity and book
revision guards. A physical command may interrupt only a lower-priority book
owner; an agent/program refuses a competing book owner. Held recovery tools,
tracking loss, workspace boundaries, distorted coordinate frames and save failure
refuse tool placement. Unrelated held creations stay held. Hidden trays stay hidden.
Book placement uses the existing saved journal and Undo; tray placement remains
transient. Temporary-room boundaries invalidate old recovery intent. Explicit
world recentering and persistent authored viewpoint/physical alignment remain
separate required work. Tracking coordinates must not become a presumed physical
anchor just because they were written to disk.

Verification passed 883 EditMode and 736 PlayMode cases (three expected optional
private-file skips), six focused recovery cases, 1,052 shared room checks,
application/fixture TypeScript and catalog provenance. A Chromium probe loaded
both guards, executed the generated book action and displayed its captured native
receipt; reusable blocks default to reading current values at execution. A real
saved-file sharing violation refused every recovery pose; scaled-frame limits
are now checked before interrupting a book owner. A first full run also recorded
one non-finite native reflection-mixer output; that existing test passed in the
final-source run. Its failure evidence is retained, production reflections remain
disabled, and sustained audio/headset acceptance remains a release gate.

Workspace-viewpoint persistence increment (2026-10-07): accepted virtual-world
movement now records a bounded authored floor position and horizontal heading in
room v22. Reload waits for fresh head tracking and restores that virtual location
relative to the current physical view. The XR origin, scanned room and listener
stay physical; this is not persistent real-anchor alignment. The shared shell
resets to its entry frame before another workspace loads, preventing offset
leakage. Restoring a bookmark does not enable user movement or start physics.

Navigation sampling does not churn entity/scene revisions or geometry Undo. It
shares ordinary room autosave, paired room/memory v21 transactions and the strict v20
workspace archive. Copies, temporary Keep and exports retain it; geometry Undo
and temporary discard preserve the current view. Lost tracking, runtime holds,
frozen writes and unavailable storage cannot overwrite the last accepted sample.
Legacy room documents load with an inactive bookmark and remain byte-preserved
until an ordinary save. Current missing/malformed/future bookmark fields refuse
saving over the original. Earlier development archive formats remain unsupported
and preserved; no silent archive rewrite is introduced.

Matching-source verification passes **894 EditMode / 743 PlayMode** tests (three
expected optional private-file skips), including seven workspace-viewpoint cases;
**1,052 shared room checks**, app TypeScript and catalog provenance also pass.
The full native room and original book journeys pass with offline scripted
providers. Evidence: `.quest-evidence/spatial-state/world-viewpoint-working.json`.
No new device, provider, packaging or store acceptance is claimed. Persistent physical alignment,
explicit recentering, stable world/region IDs, terrain-following user movement,
shared appearances, environment/water simulation and headset acceptance remain
open. This bookmark is not a region identity or permission to replay activity.

Explicit world-location increment (2026-10-08): world.viewpoint.set and
world.viewpoint now expose the same native relocation to generated book forms,
programs and delegated actions. The 3D movement tray adds **World origin**, using
the same handler with a fixed authored (0,0,0), yaw-zero target. This is an explicit
user-view relocation, separate from tool Recall or moving Maestro; it is not yet
an editable spawn or a durable region/world identifier.

The operation samples the current physical view and moves only the authored frame.
It retains entity definitions, native body velocities, ongoing actors/programs,
view mode and movement opt-ins. Only user movement's neutral-input gate is reset.
It requires a fresh workspace-local guard, tracked/focused view, released items,
clear destination and a completed workspace boundary. Virtual presentation also
requires accepted level ground. Scanned physical geometry is not treated as a
virtual destination obstacle; active physics with real collisions and joints
crossing the two frames instead refuse frame transfer. No ground, path, scale
change or implicit physics pause is invented.

The old writer is drained before revalidation. After native placement is prepared,
the exact viewpoint is saved before a callback-free frame transfer; failed saves
leave journal and poses unchanged. Temporary placement stays in the fork and
discard retains personal navigation, following the existing viewpoint policy.
Navigation produces no geometry Undo or scene revision, and replaying a completed
action receipt cannot repeat relocation. Current saved coordinates remain limited
to a 25-metre sphere; this is not a country-size world claim.

Final-source native verification passes **894 EditMode / 750 PlayMode** tests,
with three expected optional private-file skips. Seven new workspace cases cover
shared receipts and persistent NPC/program activity, write denial, occupancy,
virtual ground, unchanged physical tracking, physical bindings and joint refusal,
and temporary discard. The generated book form replays an actual native receipt;
1,054 shared room checks and TypeScript pass. Full room/book journeys also pass with offline scripted providers:
853501133b204eed9739a557507e7ace and 181249c9646f4096b016a0744618029d.
Evidence is under .quest-evidence/spatial-state/world-placement-*.
No provider, headset or city-scale acceptance is claimed by these checks.
Stable world/region identities, terrain-following locomotion and the appearance/
environment/medium stages below remain required work.

Durable world-scope increment (2026-10-08): room format 23 carries a versioned
world identity and one independently identified authored region. Ordinary saves,
object/audio/structure edits, Undo/Redo, temporary journal forks and viewpoint
movement retain that scope. A temporary fork is a reversible edit session in the
same world. Restoring an archive retains its world/region identities; making a
separate world copy will require an explicit future operation. Workspace generation,
runtime session, revision guards, receipts and physical tracking anchors remain
separate identities and must not be used as substitutes.

The original room file remains untouched during passive legacy inspection. Before
a fresh or legacy room exposes its identity through world.identity, startup saves a
current-format checkpoint. If that cannot be written, the original files remain
preserved, room writes are unavailable and the fact cannot advertise an invented
durable identity. Current saves with missing, malformed or future identity data
are rejected; a future identity cannot silently fall back to an older backup.
Journal snapshot application cannot substitute another world or region.

The shared catalog gives the book, programs and agent the same read-only fact.
Observation does not create regions, save files or execute programs. Equality
checks now compare object, structure and sound deltas independently of world
construction, avoiding false edits and consumed Undo history. Paired snapshot
format 22 and archive manifest 21 include the exact world scope in their verified
bytes. Unsupported development archives/intents stay preserved under the existing
recovery policy; this is not an implicit migration or fork.

This implements durable scope for the current one-region world only. Region
streaming, portable cross-region entity references, expanded coordinate bounds,
miniature scale, terrain-following locomotion, appearances, weather/light and
interactive water remain required. The 25-metre authored-coordinate limit and
current content budgets are unchanged. Native regression and book/catalog evidence
are tracked under .quest-evidence/spatial-state/world-identity-*.

Verification: all 910 EditMode tests pass. The broad PlayMode run passed 754 cases
with three expected private-file skips and one obsolete assertion that startup
had not created a room file. That assertion now checks the saved checkpoint stays
byte-for-byte unchanged while temporary Begin waits for an earlier writer. All
16 focused temporary-session and world-identity cases pass after this test-only
correction; production source is unchanged from the broad run. Shared room checks
pass 1,056 cases, plus app/probe TypeScript and catalog provenance. Both complete
native-room and original-book integrations pass with scripted providers:
56d03634dd6f46809477d99e52207efe and 10ff5c3cf24b4937b9322f3e5d098a81.
The generated book catalog displays an actual native identity capture and emits
only read requests. No provider or headset acceptance is claimed.

The owner expects worlds to grow from room toys into miniature countries with
cities, buildings, items and NPCs. Users and the agent must author the same world;
imported Blender/Meshy assets are another source of its components. Texture and
opacity are required beyond colour. Importing and generating image textures may
follow later, through Maestro's existing image-generation and media ownership.
Weather and lighting are required. Water, terrain and interactions must agree
with what is visible. This expands environmental interaction scope without
requiring a general-purpose fluid solver or a permanently simulated entire city.

**One authoritative world and explicit spaces.** Implement the spatial-state
refactor above first. World identity, region/cell identity, entity identity and
coordinate frame are distinct; programs keep stable references across streaming,
import replacement, miniature scale and locomotion. Region loading changes runtime
representation, not saved ownership. Real anchors stay in physical tracking space.
Virtual terrain, water levels, weather volumes and navigation use the explicit
virtual world space. Define units and scale conversion once for gravity, speed,
mass/density, rainfall/volume and audio distances; do not let each feature guess.

In a virtual region, the visible ground, collision surface, placement queries and
navigation must derive from the same accepted terrain. Raising a hill must not
leave an invisible flat floor above or below it. Imported static terrain may use
an inspected collision mesh; editable terrain extends the existing height-field
component into bounded tiles with shared borders. A height field cannot represent
caves or overhangs: those remain authored/imported meshes using the same surface
queries. Terrain edits publish geometry, collision and affected navigation as one
accepted revision; previews are visibly provisional. Water-region changes must
reconcile against the new terrain rather than retaining a stale floating surface.

The application cannot physically change the room's floor. In a mixed view,
physical floors/walls remain accurate and their collision participation follows
the separate user setting. A virtual lake below a real floor must not silently
remove that floor's collision. Explain or offer a supported placement/layer change
when both constraints cannot be satisfied. Virtual locomotion can traverse virtual
slopes through the world transform; it must not move saved real anchors. Grabs,
held props, miniature-world editing and recenter use the same frame conversions.

**Per-entity participation in the real room (owner clarification, 2026-10-08).**
The scanned physical floor/walls are a selectable environment layer, not a cage
that every virtual entity must obey. Users and agents must be able to give
Maestro, another NPC, a robot, a prop or an assembly an explicit collision profile.
For example, Maestro may walk on authored terrain below the real floor while a
ball still bounces on that real floor. The room-wide real-collision switch is a
master policy; a per-entity profile can exclude real surfaces while that switch
is on, and cannot secretly enable them when it is off. Saved settings have stable
identities, revision checks, Undo and the same catalog/UI/program entry points.

Use shared profile/group definitions and bounded membership/filter data, not a
new Unity layer for every character. Specify inheritance for assemblies and
imported children, explicit overrides and symmetric entity-pair contact rules.
The effective profile must govern rigid contacts, ground/placement queries,
character sweeps, navigation sources/paths, animation-root movement and supported
medium/cover interaction queries for that actor. A floor ignored by Maestro's
body cannot still trap its pathfinding. Profile changes invalidate affected
routes and revalidate current overlap/support before resuming; never silently
teleport, let an actor fall into unloaded space or retain stale collision pairs.
Restore/reload/import and newly loaded terrain must reapply the same accepted
profile. Publish effective participation and refusal reasons for agent inspection.

Visual real-depth occlusion, world blend, acoustic participation and physical
collision remain separately inspectable. A profile/preset may configure them
together, but changing collision alone must not silently change what the user
sees or hears. Acoustic queries need explicit source/listener environment policy
so an opted-out real floor need not muffle a voice in the virtual world. Tracked
hands/controllers and app locomotion get their own interaction policy; these
settings never disable Meta's physical safety boundary or imply the user can
physically walk through a real floor/wall. Keep the current whole-world motion
guard until admission accounts for every actor still using physical collisions.

Acceptance includes two actors with different real-floor policies simultaneously;
Maestro following a below-floor hill while a physical-room ball still bounces;
unchanged virtual terrain/obstacle collisions; hand-off/grab/release; changing a
profile with a running path or overlapping floor; save/export/reload; and identical
manual and agent operations. The saved per-entity checkpoint below implements
this first collision-policy step; physical release acceptance remains pending.

**Reusable appearance resources.** Add versioned, named appearance definitions
with stable IDs and per-object/part/imported-material-slot bindings. They hold
tint, texture or procedural pattern, mapping/tiling, opacity, surface rendering
mode and supported shading/emission parameters. Start with supported procedural
patterns and reusable presets; image-backed texture sources later reference
content-addressed app assets, not embedded image copies or expiring provider URLs.
Retain imported UV/material-slot identity and report unsupported shading instead
of silently painting everything with a replacement colour. Reusing a definition
and making an independent copy are explicit edits, with revision checks and Undo.
Existing colour controls must edit the same appearance state, not maintain a
second competing value.
Imported-slot bindings must include the exact model asset identity and source
material index; display names or a renderer's traversal order are not stable keys.
An avatar/model replacement must report incompatible bindings rather than apply
an old slot number to an unrelated new material. Define root/part/slot precedence
explicitly. Painting one object must not mutate another object's leased material;
shared-definition edits require the affected-member ownership checks, while an
independent copy changes only its explicitly selected bindings.

Appearance is separate from the existing `object.material` measured-volume store.
Do not repurpose that API or infer physical properties from a texture name. Surface
response data may configure friction, acoustics, wetting or buoyancy alongside
appearance through a reusable preset, but each remains independently inspectable.
Object opacity, environment blend and passthrough-window masks are separate fields.
Opacity does not disable collision, select a camera or remove acoustic geometry.
Opaque, cutout and blended rendering require explicit depth/sorting/outline rules
in both eyes; setting colour alpha in today's opaque shader is insufficient.
Preserve readable book pages and the illustrated style while permitting supported
imported appearances. Share immutable materials/textures and bounded variants;
avoid a unique material allocation for every brick or NPC instance.

**A shared environment state.** Weather and time of day are versioned world/region
components with deterministic seeds, a shared simulation clock and explicit
transitions. Include light direction, ambient illumination, authored local lights,
cloud/fog appearance, wind and precipitation. Rendering, world audio, wetness,
water inputs and behaviours consume that same state. A rain effect must agree
with its sound and surface effects. Covered areas suppress rain/wetting according
to supported collision/cover queries. Do not ask an LLM to run per-frame weather.
Use a measured budget for direct lights/shadows, particles and transparency;
emissive appearance alone must not be described as lighting nearby objects.
Real passthrough pixels are not automatically relit by virtual weather or lights.

**Water is a medium with a surface and bounds.** Extend the existing measured
liquid transfers with region water bodies, rather than adding unrelated puddle,
lake, river and aquarium implementations. A body has stable identity, containment
geometry, level/depth, supported flow and one authoritative quantity/source/sink
policy. Connect rainfall, overflow, drainage, scooping, pouring and terrain edits
through explicit transfers or boundary flows; avoid a second hidden water balance.
Finite containers and authored reservoirs may have different declared quantity
policies. Procedural ripples and splash particles are presentation, not extra
physics bodies or another liquid inventory.

Shared medium queries provide surface/depth, flow and entry/exit/submersion facts.
Rigid props apply bounded buoyancy and drag through ordinary physics. Hand,
controller and avatar contact can produce ripples, splash sound and optional
haptics. Characters use an authored traversal policy: avoid, wade, swim or refuse
an unsupported route. Maestro cannot report a walk through a deep lake when no
compatible swimming behaviour exists. Fish and other water life are ordinary
entities whose behaviours consume those same habitat/medium queries. Animation
does not exempt any actor from supported collision, contact or medium responses.
Visual rain uses pooled/instanced effects and sampled surface interactions, not
one rigid body per raindrop. V1 must disclose approximations while preserving
convincing contact, containment and cause/effect in its supported scenarios.

**Life follows relevance, not only the camera frustum.** Keep detailed physics,
animation, water interactions and programs active near the player and around
current interactions. Audible sources, an approaching NPC, a thrown object, a
held object and dependencies of an active task may keep a region active even
when the user looks away. Use hysteresis and load nearby collision before actors
arrive. A ball must not freeze just because it leaves the view. Render detail and
simulation detail are separate policies.

Distant regions retain authoritative identities, inventories, clock/state and
scheduled behaviours with cheaper bounded updates. Define catch-up and event
ordering explicitly when detailed simulation returns; never replay old commands,
reset programs, duplicate assets or invent exact offscreen collision results.
An agent can inspect unloaded state and request activation through the same
capabilities. Reads identify whether observations are active, retained or coarse.
App suspension/reload does not silently reconnect Live audio or microphones.
Offline world progression, if offered, must be an explicit policy. Quest capacity
is bounded: expose occupancy, streaming status and refusals before accepting work
that cannot run. Do not promise unlimited cities or thousands of active NPCs.

**Delivery order and parity.** First establish coordinate/region identity and
spatial query contracts; then shared appearances and rendering modes; then terrain
tiles/navigation; then environment/light state; then medium interactions and
water-aware actor behaviours; then measured streaming/detail budgets. These are
staged parts of one model, not parallel world engines. Reuse catalog modules,
facts/events, current-revision edits, runtime effects, durable receipts, asset
libraries and readable user programs. Add public capability IDs only with working
implementations. Built-in objects and imported assets use the same adapters.

Release checks must include user and agent editing the same material; texture
reuse and export/reload; transparent solids and water in both eyes with real-depth
occlusion; shared terrain/collision/nav after edits and world movement; rain under
and outside a roof; a rain-fed puddle, a floating/sinking prop and a hand ripple;
Maestro avoiding/wading/swimming according to available behaviour; water life
remaining in its habitat; looking away during a throw; leaving and returning to
a region without identity/state loss; and equivalent real managed/BYOK requests.
Measure a sustained populated scene on Quest 3 before setting published limits.

Current evidence is narrower: editable height surfaces already derive their mesh
and collider from the same accepted heights; measured material and vessel transfers,
recipe patterns, imported textures and basic ambient/program behaviour exist.
There is no completed shared appearance library/opacity control, environmental
weather/light system, regional water-medium simulation or streamed city-scale
world. None is established by the existing room-scale tests.

The bounded loading/detail choice is consistent with Meta's guidance on loading
nearby content and reducing distant detail, rather than loading an entire world:
[Open World Games and Asset Streaming](https://developers.meta.com/vr/documentation/unity/po-assetstreaming/).
Transparency and lighting budgets also need device measurement; see
[Meta mobile performance guidance](https://developers.meta.com/vr/documentation/unity/unity-mobile-performance-intro/)
and [Unity untethered XR guidance](https://docs.unity.com/en-us/engine/6000.3/manual/xr/graphics/untethered-device-optimization).
These references support rendering choices; they do not establish our simulation
semantics or certify this app's capacity. The URP reference does not imply a
render-pipeline migration for our current shaders. Reviewed 2026-10-07.

### Environment-depth implementation checkpoint

Development APK `0B7A4CF5` now enables Meta OpenXR environment depth and uses
Vulkan, which the installed Unity Meta OpenXR occlusion feature requires. The
existing HardwareBuffer book bridge selects its Vulkan path. The same shaders
mask pigment, pencil silhouettes and world lettering; virtual-only snapshots
scope out physical depth. Raw hands remain in the depth map.

Verification: 839 EditMode and 647 PlayMode passes (three expected optional
private-file skips), both full native room/book probes, shared web build, native
browser unit tests/build/lint, and a verified development-signed Android package.
After the graphics API prerequisite was corrected, the APK stage was rerun with
all other runtime/test/shader sources hash-matched to the tested mirror. Actual
Quest stereo pixels show the book, saved lesson reloads, and the read-only device
diagnostic reports environmentDepthAvailable=true. The owner confirmed on Quest 3
that a newly moved real object, hands and controllers hide virtual content, and
that the book pages look unchanged. This is initial physical acceptance; sustained
performance and wider Vulkan interruption coverage remain device acceptance checks. The third-party Vulkan copy path has an existing
Unity 6 command-buffer warning; sustained/device validation and any necessary
native transfer repair are release gates, not implied by a successful APK.

This checkpoint does not yet implement the independent blend/collision/window
settings described above, or the new selectable physical/composited cameras.

### Native microphone packaging regression

On Quest 3, the shared composer exposed Start Live with Camera Off, and a native
controller press reached the normal Live controller. Capture failed before the
provider session with “Could not start audio source”. Android Chromium reported
that both RECORD_AUDIO and MODIFY_AUDIO_SETTINGS were required; the installed
APK declared only RECORD_AUDIO even though its runtime grant was already allowed.
The native browser now declares the missing normal audio-settings permission.
A test reads the merged manifest through Android's PackageManager: it fails on
the previous manifest and passes with the correction. All 87 native browser
tests pass (two additional optional private-import fixtures skipped); AAR build
and lint pass. Corrected development APK `413FA818` preserves the saved lesson and
contains the expected ARM64 binaries, web bundle, permissions and development
signature. Native book input starts microphone-only BYOK Live successfully. The
physical request to remove the tree while keeping the apple and other objects
was transcribed correctly, and the owner confirmed hearing Maestro's reply.
The resulting native task deleted only the tree, preserving the same apple and
all unrelated objects. Placement first rejected a stale room state, then found
no suitable live surface; the apple remained on the table. The final task reply
reported that accurately. The initial spoken reply had claimed floor placement
before native completion, so this is not a full semantic pass. A natural novice
follow-up asks for help putting the apple on the floor without catalog coaching.

A real Settings focus interruption on this Vulkan APK kept the same Android
process alive. Environment depth stopped while unfocused and became available
again on return. Actual stereo pixels visibly show a newly edited unsent draft
marker after returning; the marker was then cleared. Microphone/Live stayed
stopped and media required an explicit resume. This checks one Settings focus
transition, not sustained performance or every permission/recovery sequence.


Production world-movement increment (2026-10-07): the existing virtual-view
controller path now moves the long-lived Room content frame instead of the XR
origin. Tracking, the audio listener, scanned geometry and MRUK world lock stay
physical. Translation and snap turns transfer native body poses and velocities
atomically, preserve sleeping/kinematic state and connected virtual assemblies,
and rebase boundary-recovery history without declaring an authored teleport.
Scanned ink, controller-mounted buttons and currently grabbed bodies keep their
physical poses. Accepted virtual navigation relocates its instance without
rebaking identical authored geometry on every movement tick.

This remains the interim virtual-only view and recovery contract. Active physics
against real-room colliders refuses artificial movement until physics is paused
or real collisions are explicitly disabled. Connections crossing physical and
virtual frames also refuse movement. These are visible temporary limits pending
swept whole-assembly admission, not the final independent-reality-layer policy.
Normal controls still interrupt while a user is holding an item; the transfer
itself retains physical grab poses for recovery. View exit still pauses physics,
restores the content entry pose and removes its temporary floor. Persistent world
offsets, independent blend/view lifecycle, physical-anchor migration, queued
spatial intent, terrain-following user travel and headset acceptance remain open.
The authored world and physics continue through admitted virtual-only movement;
this is not region streaming or scaled-world physics.

Verification passed 873 EditMode and 722 PlayMode tests, with three expected
optional private-file skips; both complete native-room and original-book journeys
passed. All 66 shared browser/catalog checks and provenance checks pass. The
81-case focused run also passed. The first focused run exposed two fixture
assumptions about the former shared root: whole-application focus delivery and
which root a whole-world navigation test moves. Both fixtures now use distinct
physical/content ownership. The additional full-run recovery test checks whole-room
disable/re-enable with no tracking movement or automatic physics restart. These
are local scripted-provider integrations, not headset or real-provider acceptance.

## Spatial voice and room acoustics (owner clarification, 2026-10-07)

The owner wants Maestro's audible voice to originate at the avatar's mouth and
respond to real and virtual surroundings. This is accepted v1 scope; the current
APK still uses WebView audio output and does not implement spatial voice.

Use one shared speech-output contract for ordinary chat speech, agent narration,
Live and replay. Original Maestro continues to own provider requests, subscription
and BYOK access, decoded speech, cancellation, turn ownership and transcript
history. Quest consumes that speech through one native spatial output; it must
not simultaneously play a second dry WebView copy. Web/phone keep their existing
output adapter. Avoid a second Gemini connection or a Quest-specific narrator.
Preserve bounded buffering, ordered chunks, playback completion and interruption
semantics across this boundary; native playback completion, not network arrival,
must drive speaking state and future mouth animation. Verify microphone echo
handling with native playback: moving output outside Web Audio must not cause
Maestro to hear and respond to its own speech.

Attach the mono voice emitter to the animated mouth/head transform. Imported
avatars use their mapped head plus a configurable mouth offset, with an explicit
fallback when no suitable joint exists. Headset tracking owns the listener.
Moving, turning, scaling or replacing an avatar must keep its voice attached;
virtual locomotion and re-centering must use the same physical/virtual coordinate
contract as rendering and collision. Other speaking characters and authored
sound objects use their own emitters. Book artifacts, music and ambient audio
must not all be indiscriminately routed through Maestro's mouth.

Meta XR Audio is the candidate native renderer: its HRTF spatialization supports
directional sound and room effects. Acoustic Ray Tracing models reflections,
reverb, obstruction, occlusion and diffraction from supplied acoustic geometry.
Connect simplified scanned room surfaces and eligible virtual geometry to this
system, with explicit material presets and bounded update/source budgets. A room
scan does not establish the true acoustic material of every surface, and live
visual depth is not automatically an acoustic mesh. Do not promise acoustically
accurate handling of every newly seen real object. Geometry/material overrides
and sound settings should remain editable through the shared capability catalog
and the same human controls/programs as other object properties.

Keep acoustic participation independent of visual opacity and collision enabled
state. Offer a sensible environment-following default, with explicit overrides;
changing passthrough blending must not accidentally double the room reverb or
move physical walls. Prioritize intelligible language learning: modest room
colour, bounded attenuation and an optional clear-voice mode. No extra mandatory
controls on the chat pages are needed.

Acceptance: native and headless adapter parity; exact-once ordered playback;
backpressure/underrun/cancellation; managed/BYOK chat, agent and Live output;
physical echo/self-trigger checks; both ears while turning the head and walking;
avatar replacements and custom mouth offsets; room/virtual acoustic blockers;
blend/locomotion/recenter; pause, stop and reconnection; measured Quest CPU/audio
latency and a fallback if the spatial renderer is unavailable. Mouth animation
is related but remains a separate rig/viseme feature, not implied by audio origin.

Official references verified 2026-10-07:
[Meta XR Audio features](https://developers.meta.com/vr/documentation/unity/meta-xr-audio-sdk-features/),
[Acoustic Ray Tracing](https://developers.meta.com/vr/documentation/unity/meta-xr-acoustic-ray-tracing-unity-overview/),
[Acoustic setup](https://developers.meta.com/vr/documentation/unity/meta-xr-acoustic-ray-tracing-unity-getting-started/).

First implementation increment (2026-10-07): core `SpeechOutput` now defines one
owned mono PCM renderer with sample-position reporting, bounded buffering, drain
fences and cancellation. Triggered TTS uses this contract through a browser
adapter, including actual source completion plus output latency; output failures
no longer silently skip spoken chunks. An injected renderer replaces browser
playback completely without moving provider, transcript or cache ownership.

The Unity avatar owns a native DSP-scheduled emitter that follows its visible
head after animation and imported-avatar replacement. It validates generations
and chunk order, bounds its queue and stops on focus loss, pause and device
reconfiguration. Native tests exercise actual AudioClip/AudioSource PCM using a
controlled clock, including imported-rig replacement. These are desktop playback
and lifecycle checks, not an audible headset or acoustic acceptance result.

This increment does not route WebView speech into Unity yet. Android handshake,
chunk/ack transport, Live and cached replay adapters, persistent mouth settings,
Meta HRTF/acoustic geometry and microphone echo handling are subsequent work.
The current installed APK therefore still plays speech through WebView. No new
device test is attempted while cooling/charging readiness is pending.

Validation for this increment: 2,533 web tests / 281 files; TypeScript, lint,
architecture/catalog checks and production web build pass. The synchronized
Unity mirror passes 839 EditMode and 656 PlayMode tests, with three documented
optional private-import skips. No Android source or packaged APK changed in this
increment; managed/BYOK network and physical echo tests were not rerun for it.

Live output increment (2026-10-07): the real Live controller now owns a
`SpeechOutput` supplied by its runtime adapter. Browser playback retains the
existing resampling/startup worklet, with bounded copied PCM, reset generations,
per-fence completion and explicit render-error reporting. Decoded results commit
in provider order. Stop resets the selected renderer immediately while microphone
cleanup is pending; queued provider audio cannot restart it. Speech-gated input
also waits for actual output completion instead of only an estimated end time.
An injected native-style renderer replaces browser playback in lifecycle tests
without adding a provider session; asynchronous startup, write/decode/drain
failures and cancellation cannot report a falsely completed Live turn.

The reusable `scripts/probe-speech-output.mjs` checks the actual Chromium audio
thread and analyser at 24 and 48 kHz using muted synthetic PCM, including early
fences with later audio, output tail, reset and reuse. Its server isolates the
audio modules and cache from other development servers. This establishes browser
rendering, not physical hearing, native echo cancellation or provider acceptance.
Android chunk transport/ownership, cached replay and acoustics remain open.

A failure-first regression also found that socket-close flushing could bypass
the audible-completion gate and deliver the finished Live turn while its speech
was still queued. The finalization path now waits for the selected renderer even
when the provider closes; the same test then passes. This prevents an early
chat/room-task handoff independently of which renderer is selected.

Android voice transport increment (2026-10-07): the local top-level book now
selects Unity's mouth emitter for both Live and triggered TTS. Desktop/phone web
outputs are unchanged. The Android bridge polls a transient speech mailbox;
there is no JavaScript-to-JNI interface, file URL playback, provider duplication,
or PCM in saved rooms/action receipts. One voice owner has bounded copied PCM,
little-endian chunks, native played-sample acknowledgements and drain fences.
The browser queue allows at most 120 seconds; native flow credit is two seconds
and individual exchanges contain at most eight 4,800-sample chunks. Idle polling
runs more slowly than active speech. Lost receipts resend identical chunks but
never play them twice.

Document identity, browser session and output revision reject late callbacks.
Stop, navigation, host suspension, emitter replacement, missing browser/native
heartbeats and a stalled audio device stop the voice. A retired browser revision
cannot reopen on focus return even if JavaScript did not process suspension.
Live and TTS close their existing provider on native output failure; there is no
mid-stream fallback that starts a second dry copy. Scanned/virtual acoustics,
Meta HRTF, mouth-offset controls, cached replay, physical echo and audible
direction/latency acceptance are still open. This source increment is not yet
packaged or installed, and the owner cooling/charging gate remains in effect.

Verification: 2,556 web tests across 284 files, TypeScript, lint, production web
build, core boundaries, prompt ownership and catalog provenance passed. Android
release tests/build/lint passed (90 tests, two optional private-import skips).
Unity passed 839 EditMode and 662 PlayMode tests (three optional private-import
skips), followed by the complete native-room and original-book integration
journeys. These deterministic probes used no provider and do not establish
Android device playback, audible positioning or microphone echo acceptance.

Saved voice replay increment (2026-10-07): cached Maestro speech now uses the
same selected output as new speech, including Unity's mouth emitter in the
native book. Inaudible decoding normalizes inline audio to mono 24 kHz; input
is limited to 16 MiB encoded and 120 seconds decoded. Browser replay waits for
scheduled-source completion plus the device tail. Stop, suspension and unmount
fence late decode/context callbacks; output failure clears the remaining replay
queue. Actual learner recordings are explicitly marked and retain their existing
playback destination. This is transient playback metadata, with no save migration.

All 2,569 web tests across 285 files, TypeScript, lint, production web build and
architecture/provenance checks passed. A real muted Chromium probe decoded a
stereo 32 kHz WAV, confirmed mono 24 kHz samples, rendered at both 24/48 kHz and
verified actual tail completion, Stop and fresh replay. Native transport tests
verify exact PCM and played-sample receipts; these are deterministic tests, with
no provider calls or headset hearing claim. The preceding native bridge passed
839 EditMode / 662 PlayMode tests and both full integration probes. Its release
CI passed on c54517ad. No native source changed in this replay increment. The
installed APK is unchanged; room acoustics and physical acceptance remain open.

Continuous native voice increment (2026-10-07): replace the per-packet Unity
AudioSources with one source and procedural filter per voice owner. A bounded
eight-second PCM ring retains resampling phase across packet/DSP boundaries;
underruns emit silence. A fixed receipt ring records actual audio-thread DSP
blocks, so a moving clock or a stalled/virtualized source cannot falsely finish
queued speech. Stop closes the old stream under its thread fence, then stops and
releases only that owner's source. The spatializer is ordered after PCM generation.
This supplies a continuous signal for later HRTF/effect integration; the Meta
audio package and room acoustic geometry are not enabled by this increment.

New deterministic tests cover 24/44.1/48/96 kHz output, copied PCM, interpolation
across packet boundaries, ring wrap, capacity, underruns, cross-thread close and
zero steady-render managed allocations. A muted PlayMode test also exercises
real Unity DSP callbacks and captures the output after the speech filter: it
requires one retained source, expected waveform level, bounded discontinuity,
actual played-sample completion and immediate Stop. This is desktop rendering
evidence, not physical Quest sound, head-relative direction or acoustic acceptance.

The selected speech renderer also declares whether full Live needs microphone
suppression during playback. Native output opts in because Unity's signal may
not be present in the browser echo reference. Captured samples are dropped
before retention/encoding until actual drain plus the shared 500 ms settling
interval. Browser Live retains its previous input behavior. A failure-first
test demonstrated native playback leaking through full Live's ungated input;
the regression and browser-continuity test now pass. This policy suppresses
simultaneous speech, and is not acoustic echo cancellation or a measured reverb
tail; physical acceptance and later room-effects tuning remain open.

Verification: 849 EditMode and 663 PlayMode tests passed (three optional private
fixtures skipped), including the actual muted DSP waveform test. Both complete
native-room and original-book integration probes passed. The final web suite
passed 2,571 tests / 285 files, alongside TypeScript/build, lint, core boundaries,
prompt ownership, catalog provenance, Quest asset and probe-type checks. No
provider calls or headset operations occurred in this increment. The headset
remains stopped pending owner confirmation of cooling and normal charging.

Directional voice increment (2026-10-07): pin the separately versioned Meta XR
Audio SDK 85.0.0 and its required built-in Terrain module, configure the Meta
spatializer, and include a 32-voice settings asset. Speech startup checks the
native plugin context before creating its single HRTF source. PCM generation
precedes spatialization. Room reflections remain explicitly disabled until
owned real/virtual acoustic geometry, materials and effect-tail handling exist.
Android build preprocessing checks renderer selection and the voice budget.

A muted listener-mix probe captures actual stereo output, rotates the listener
180 degrees and requires the favored ear to reverse. It also reads the native
voice count while PCM is playing and checks that synthetic-room reflections are
disabled. The focused six-test native-speech suite passes: equal-source listener
energy changes from left 0.092 / right 1.705 to left 1.705 / right 0.092, with one
active Meta voice. The first probe incorrectly queried activity only after the
output had drained; the correction samples during playback. The SDK ARM64 ELF
has 16 KiB-aligned load segments. These are desktop/binary checks, not proof of
Quest sound, echo cancellation, spatial comfort, packaged APK or room acoustics.

Final directional verification passed 849 EditMode / 664 PlayMode tests (three
optional private-import skips) and both complete native-room/original-book
integration probes. The latter use scripted provider responses, not a new paid
provider run. Catalog provenance and included-asset checks pass. The unchanged
web source retains its preceding 2,571-test pass. Headset installation and its
acceptance checklist in `QUEST_DEVICE_QA.md` remain pending.

Direct acoustic obstruction increment (2026-10-07): the owned Meta native scene
now receives explicit scanned effect meshes, created primitives, rigid recipe
parts, accepted sculpted surfaces and readable rigid imported meshes. Topology
replacement, availability loss, destruction, pause and focus loss release native
geometry; animated parts update their actual world transform. Rendering and
collider switches do not determine participation. Scan admission uses accepted,
tracked room data separately from the physics/placement gate. Virtual view keeps
the loaded scan's acoustic geometry.

Admission is bounded to 128 scanned / 384 virtual meshes, 16,384 / 49,152 triangles
and 32,768 / 98,304 vertices respectively, with at most four uploads per frame.
Each mesh is limited to 16,384 vertices and 8,192 triangles. Invalid, unreadable or
over-budget geometry is omitted without fabricating a bounding-box wall. The
shared `runtime.acoustics` fact reports native counts, pending/omitted registered
surfaces, readiness and renderer issues without changing the scene.

This stage uses approximate hard-surface material properties and conversational
occlusion intensity 0.65. Reflections remain unrouted, with both reflection sends
at -60 dB. Skinned avatars, alpha-cutout imported meshes, ink overlays and moving
real objects absent from the scan do not contribute acoustic geometry. The MRUK
effect mesh is a structural approximation; dynamic doors, acoustic materials and
reverberation still require implementation/acceptance. Visual environment depth
does not provide those acoustic facts automatically.

The first six focused PlayMode tests pass, including real, muted stereo output:
unobstructed energy 2.202, wall 0.433, moved/deleted wall 2.202. A disabled renderer
and collider leave the acoustic wall intact. Scan acceptance/tracking loss,
recipe replacement, invalid geometry, separate scan budget, re-admission and
owner lifecycle cleanup also pass. These desktop results do not close physical
sound, Live echo, Quest performance or thermal acceptance. Final whole-project
verification passed 849 EditMode and 672 PlayMode tests (three expected private
import skips), both full native room/original-book integrations, 115 web catalog
tests, TypeScript, included-asset integrity and catalog provenance. The first
full-run probe guard correctly stopped at an Editor-only provenance list change;
after exporting that matching catalog, both integrations passed without rerunning
the unchanged runtime suites. No provider calls, headset operations, APK signing
or installation occurred in this increment.

Acoustic-map ownership increment (2026-10-07): the SDK needs computed map data
in addition to geometry and a reflection mixer. A muted desktop experiment with
six walls and a translated listener confirmed that a runtime map produces both
early reflections and a late-reverb tail. Without map data, the same mixer
settings produced no reflections. The shared reverb continues briefly after an
individual source is destroyed; resetting it on every speech Stop would also
cut other room sources' reverb and is not the intended cancellation design.

The native room now owns an on-demand map calculation with at most eight explicit
points, eight stored early reflections, one worker and a one-second cooperative
compute budget. Geometry updates and native frees wait for that worker; sources
temporarily disable room acoustics through `Ready` while keeping directional
speech available. Edits, pose changes, tracking loss and destruction cancel the
pending result. Destruction transfers native-input cleanup to the job and retains
the scene lease until cleanup completes, preventing a new room from taking over
the same native context prematurely. The worker and AOT callback use no Unity
objects. Shared diagnostics distinguish pending/calculated maps from audible
reflections and never start or advance a calculation.

This is a tested integration dependency, not enabled room echoes: automatic map
selection/rebuild scheduling, material controls, a single reflection mixer,
multi-emitter Stop semantics and measured Live microphone-tail handling remain
open. The shipped path does not calculate maps yet. Desktop cost does not prove
Quest performance. The independently verified development APK D2BDBD9C remains
the previous direct-obstruction build and has not been installed.

Validation: 849 EditMode and 677 PlayMode cases passed, with the three expected
private-file skips. Seven focused map cases also passed, adding timeout recovery
and pause/focus/audio-reset callback races to the five cases in the full suite.
They hold the actual native progress callback while changing lifecycle state;
the audio-reset case invokes the app callback rather than resetting physical
hardware. Both the native-room and original-book integrations passed, along with
115 shared catalog tests, TypeScript and included-asset integrity checks. The
integration source guard initially detected the two tests added after the full
run's mirror copy; it passed after the focused runner copied and tested those
files. Production code was unchanged between the full and focused runs. The
initial catalog export rejected nine record fields; nesting the map diagnostics
fixed it without expanding the shared program format's eight-field limit.

Automatic acoustic-map scheduling increment (2026-10-07): accepted scan geometry,
the virtual floor and fixed, unheld, nonanimated creations now define a static-only
map. Dynamic bodies and animated model/recipe parts retain direct obstruction
without invalidating that map every frame. Changes of mobility, fixed pose,
availability or topology rebuild the affected native geometry. Even dynamic
native handles stay alive while the SDK worker reads its inputs.

The application waits two seconds after structural changes and half a second
after listener movement before requesting a map. A cache keeps at most four
listener positions with a 2.5 m coverage heuristic. Failed calculations back off
for 4, 16, 64 and then 120 seconds. Tracking loss cancels pending work and clears
the cached positions. This is bounded acoustic sampling, not automatic detection
of adjacent rooms or a complete acoustic reconstruction. Shared diagnostics now
report reflection-boundary counts and omissions separately from direct geometry.

The new regressions caught two issues during development. A disabled surface that
remained registered after releasing its native handle cancelled subsequent maps;
input validation now ignores that retired boundary. The first full test run also
exposed an XR fixture leak: creating interactive props without owning a manager
let XRI create a global fallback, interfering with 43 later grab-dependent cases.
The acoustic fixtures now own their manager and verify teardown restores the
previous manager count. All 14 map cases and nine grab/physics cases then passed
sequentially in one editor process before the full-suite rerun.

A muted synthetic-audio experiment uses this scheduler and the production map
owner, with a private test mixer. It measures increased output energy for early
reflections, late reverb and both together; per-source Stop leaves a room tail.
Production reflection routing remains disabled pending shared-mixer lifecycle,
multi-emitter Stop and measured Live microphone-tail handling. PCM completion
receipts must continue to report played samples independently of those tails.
Desktop evidence does not close Quest audibility, echo or performance acceptance.

Final scheduling verification passed 849 EditMode and 686 PlayMode cases, with
three expected optional private-file skips. Both complete native-room and
original-book integrations passed, along with 115 shared catalog tests,
TypeScript, catalog provenance and included-asset integrity. The first failed
reports are retained alongside the passing rerun. No provider requests or
headset operations were performed for this increment.

Shared world-audio increment (2026-10-07): `audio.source.edit`,
`object.audioEmitter.edit` and `audio.play` now expose reusable procedural sounds
through the same catalog used by human controls, programs and the agent. Sources
have stable identities and revisions; emitters attach to an object root, recipe
part or Maestro joint. Definitions participate in save, Undo, temporary rooms and
workspace export. Playback retains its starting source revision, follows motion,
and cancels independently on Stop, emitter changes, deletion or room teardown.
Completion follows actual native PCM consumption, not a duration timer.

The first adapter supports bounded sine, triangle and seeded-noise recipes, with
32 saved sources and eight concurrent world voices. It does not complete the
accepted arbitrary-audio requirement: imported clips, live adapters, continuous
playback controls, source dependencies in construction modules, speech integration
and mixed-audio Live echo handling remain required. Production reflection routing
is still disabled. See `unity/AUDIO.md` for the executable boundary and acceptance
contract. No headset or provider operations occurred in this increment.

Continuous world-audio increment (2026-10-07): `audio.start` explicitly transfers
an instance to the room after actual PCM consumption, allowing the initiating
program to continue. `audio.control` uses that exact identity and a current
revision for pause, resume, transient gain and Stop. Room-owned sounds survive
completion of their starting task; cancellation before handoff stops preparation.
An emitter has at most one active instance, and a full voice budget refuses new
starts instead of replacing another sound. No playback is restored after reload.

`audio.instances`, `audio.instance` and `audio.instance.changed` expose discovery,
readback and bounded lifecycle/control history through the existing catalog and
event scheduler. Delayed observers receive retained changes in order; overflow
and expired identities fail visibly. They cannot start or advance playback.
Native testing caught Unity requesting procedural PCM after AudioSource.Pause;
the PCM transport now fences consumption itself while preserving its queue and
resampling phase. The regression pauses one loop longer than the output watchdog
while a second continues, then resumes the same instance and stops it independently.
These changes do not implement imported clips, live adapters, mixed-audio echo
handling or physical headset acceptance.

Verification passed 865 EditMode and 694 PlayMode tests, with three expected
optional private-file skips, plus 186 shared editor/catalog tests, application
and probe TypeScript and catalog provenance. The full native-room journey starts
a real muted loop, discovers and controls the same instance, receives its pause
event in a saved program and retains its stopped receipt. The original-book
integration also passes. The installed headset build is unchanged; these runs
did not contact an AI provider or use the headset.


### Terrain-following movement and shared accepted-ground reads (2026-10-08)

Virtual player movement now follows accepted authored slopes and small steps by
moving the authored world vertically/horizontally under fixed physical tracking.
Maestro navigation projects its route approximation onto that same accepted
collider geometry before foot placement. Shared support checks retain the existing
25-degree slope and 10 cm climb/drop limits, sample the centre and eight footprint
edges, and use bounded capsule sweeps for the body/head. Ordinary props do not
become stairs; holes, steep ground, oversized steps, overhead obstacles and unknown
clearance refuse without publishing a partial movement. A step's swept landing,
not an overlap-only approximation, determines the accepted capsule position.

`world.ground` is an observation in the same generated catalog for the agent,
programs and book. It returns world/region identity, found status, the accepted
surface point/normal and traversal limits for a bounded authored point/footprint.
It excludes scan geometry and visual sculpt previews. It describes support only:
body clearance, connected routes and execution-time validity remain separate.
The generated controls need no terrain-specific web implementation. Existing
world-location actions still recheck current support and destination clearance.
Terrain walking updates the existing persistent viewpoint; reloading restores
that authored location using fresh tracking and leaves locomotion/physics paused.

This is a bounded traversal checkpoint over current accepted terrain, not region
streaming, large editable terrain tiles, miniature scale, swimming or per-entity
real-room participation. The existing active-real-physics world-transfer guard
remains. Next, replace the room-only participation assumptions in physics,
containment, navigation, ballistics, holding/catching and traversal with the shared
per-entity policy specified above. Keep real-depth visibility and acoustic policy
independent rather than deriving them from a collision checkbox.

Validation: 92 focused native checks passed, including accepted-versus-preview
geometry, ledges, steps, slopes, transformed worlds, fixed physical tracking and
a real save/reload. Shared room/program tests: 1,058 passed. The generated book
read both unsupported and supported native captures through read-only catalog
commands. Full native verification passed 910 EditMode and 763 PlayMode checks, with three
optional private-file skips. The matching-source native
headless and original-chat/book journeys both passed with local scripted provider
responses (no live provider or headset use). This change has not been packaged,
installed or checked on the headset.
Evidence: `.quest-evidence/spatial-state/terrain-traversal-*` and
`.quest-evidence/spatial-state/world-ground-book/`.


### Saved per-entity environment profiles (2026-10-08)

The real-room "glass box" now has an explicit saved participant policy. Each
book, Maestro or created/imported object holds an optional stable profile ID;
an empty ID inherits the global physical-room switch. A profile can exclude
scanned surfaces while retaining virtual terrain and object contacts. The global
switch remains an upper bound. No extra Unity layer is allocated per character:
one owning object applies the resolved scan mask to its body and compound/import
children. Missing or unsupported profiles are rejected, never silently inherited.

The same resolver now supplies native contacts, containment/readiness, navigation
source selection and foot/body sweeps, throw trajectories, held props, catch
clearance, connections and liquid transfer paths. Navigation prepares for its
own actor; current Maestro owns that navigator. Future concurrent NPC navigation
must retain separate owned query instances rather than change another actor's
route context. The current room has one region. physics.environment.ready
retains its default-scope meaning; object.environment reports the individual
actor, and physics.simulation.ready reports whether any accepted environment
can start. Loss of scan readiness freezes physical participants while accepted
virtual participants can keep running. Explicit room Pause still pauses all.

Shared authoring is exposed through environment.profile.save,
environment.profile.remove, object.environment.assign, and the corresponding
profile/list/binding facts. The existing catalog generates book fields, agent
discovery, argument validation and program calls. A shared edit names every
affected member as an explicit ownership claim and rechecks its exact membership
and profile revision. Assignments check object and chosen-profile revisions.
Held/authored/moving Maestro targets refuse conflicting edits. Enabling real
collisions refuses new scanned-surface penetration; during running physics the
target must also fit its new environment. Successful policy changes retain
position and clear the changed body's prior throw speeds. Independent bodies
keep their speeds. Removing a referenced profile requires explicit reassignment.

Definitions and bindings use the existing journal: one Undo, idempotent unchanged
edits, temporary Keep/Discard, saved snapshots and workspace archives. Room v24,
paired snapshot v23 and archive v22 record the added data explicitly. Earlier
rooms inherit the prior global policy; unknown future data remains preserved by
workspace recovery. Current limits are 16 profiles, 16 members per profile and
three definitions per fact page, fitting the existing 16-target action and
bounded program-value budgets. These are visible admission limits, not a promise
of unlimited concurrent city simulation.

Collision participation does not change real-depth occlusion, passthrough,
materials, water-medium semantics or acoustics. Existing world-transfer guards
and bounded accepted-ground columns remain. This checkpoint is not terrain
streaming, arbitrary physical-surface subsets or a general NPC locomotion system.
The next appearance, weather, water and regional-simulation steps retain the
shared spatial-state direction above.

Verification passed 915 EditMode and 772 PlayMode tests, with three optional
private-file skips, plus 1,064 shared room/program tests. App and probe TypeScript
and catalog provenance pass. Matching-source native headless and original-chat/book
journeys pass. The headless journey creates, assigns, edits, undoes and removes an
actual saved profile through the shared native transport. Native receipts/facts
also drive the generated book inputs and fact display; screenshots were inspected
for readability and overflow. Physical tests put one ball on the real floor and
another on virtual ground two metres below it, and verify scan-loss isolation.
These runs use local scripted responses, not a live provider or headset. Headset
testing remains on hold; this checkpoint has not been packaged or installed.
Evidence: .quest-evidence/spatial-state/environment-profiles-*;
native runs 3bb00a88aad64ff588f502a0bb6db0cc and
f14e4be6047e4b6eba71d3fa76913a69.


### Appearance rendering and import fidelity (2026-10-08)

Shared appearance authoring first needs correct rendering. Illustrated surfaces
now explicitly distinguish opaque, alpha-cutout and blended modes, with finite
opacity, cutoff and sidedness. Opaque/cutout surfaces write depth; blends sort in
the transparent queue and do not write depth. Separate alpha blending preserves
compositor coverage. Blended surfaces disable the solid graphite outline; cutout
outlines use the same texture/factor/vertex-alpha mask as their fill. Existing
opaque book pages and browser colour decoding keep their prior treatment.

The real GLB/VRM import path retains source material-slot indices and double-sided
flags before restyling. Duplicate material names do not become binding identities.
Imported texture, UV scale/offset, base opacity and alpha mode survive the
illustrated conversion; later colour tinting does not overwrite opacity. Shared
source slots still share one styled material within the imported instance. The
immutable surface descriptor supplies explicit render state for future bounded
appearance-material cache keys. Opacity is not a collision or acoustic policy.

This is a renderer/import prerequisite, not the persisted appearance library,
texture editor or new public appearance capability. Those remain required: named
versioned definitions, explicit reuse/copy, object/part/source-slot bindings,
revision/ownership checks, Undo and one authority for existing colour controls.
Current transparency is ordinary sorted alpha blending, not refraction or
order-independent rendering for arbitrary intersecting meshes. Quest stereo,
real-depth composition, overdraw and sustained populated-scene cost still need
physical acceptance before release.

Focused verification passed 15 native checks, including actual imported GLBs,
duplicate-named slots, both sides of thin surfaces, repainting without opacity
loss, cutout factors/vertex alpha, opaque foregrounds, overlapping blends and
compositor alpha. Native desktop captures at two eye-offset camera positions
were inspected; these are not headset stereo acceptance. Full verification passed
915 EditMode and 781 PlayMode tests with three optional private-file skips,
1,187 shared room/headless checks, app/probe TypeScript and catalog provenance.
Matching-source native headless and original-chat/book journeys both passed
with scripted responses. Android packaging is checked separately; device testing
remains on hold.
Evidence: .quest-evidence/spatial-state/appearance-render-*.

### Shared appearance authoring (2026-10-08, desktop verified)

Room format 25 stores up to 64 named, revisioned appearance definitions and up to
33 bindings per object. Supported source includes inherited imported images/UVs,
solid/checker/stripe patterns, tint, mapping scale/offset, explicit opaque/cutout/
blend mode, opacity, cutoff, sidedness and the illustrated shader's grain/shading.
Image import/generation and unsupported PBR/emission remain later delivery work.

The same catalog exposes appearance.save, appearance.remove and
object.appearance.bind to generated book forms, programs and the room agent.
Assignment reuses a definition; copy creates an independent definition and
assignment in one saved Undo. Definition changes require every currently bound
object, including dormant references, and current revisions. Nothing partially
applies when a member is busy or stale. Full-room claims use a generated ceiling
of 66, with matching native/web program, module, catalog, receipt and object-
precondition checks. Existing limits on unrelated payload arrays remain bounded.

Bindings address root, exact recipe part, or exact imported-model content hash
plus source material index. Specific bindings override root. Replaced/missing
geometry keeps dormant references instead of reinterpreting names or slot order.
Paged facts expose definitions, members, available addresses and binding state.
Book page displays, selection marks and surface ink retain their own materials.

Ordinary object Paint, including legacy chat batches, writes an explicit local
root-binding tint when bound. The recipe part palette reads and writes a bound
part's local tint through the same editor; global object paint does not recolour
a specifically bound part or imported slot. Empty local tint inherits the shared
definition. Editing a source pattern hidden by an appearance replacement refuses
with an explanation. Source pigments/textures remain explicit inputs; appearance
is separate from physical material contents, collision policy, acoustics,
passthrough blending and camera selection.

Immutable rendered variants have leases, are shared for matching source/style,
and are released when no object uses them. Reconciliation restores original
renderer slots before selecting new variants, including asynchronous model
completion. Undo/Redo, save/reload, temporary Keep/Discard and portable workspace
snapshots retain definitions and bindings. This checkpoint refused construction
capture with appearance bindings; the portable-construction checkpoint below
supersedes that restriction with a closed dependency bundle.

Final validation passed 923 EditMode and 792 PlayMode tests, with three optional
private-file skips; 1,318 shared room/headless tests; app/probe TypeScript; lint;
and generated-catalog provenance. Twenty-three focused appearance/render checks
cover real imported slots, local paint authority, immutable leases and full-room
claims. An actual workspace archive round-trip preserves shared definitions,
local tint and dormant model-slot bindings; invalid/dangling definitions refuse.
Shared definition edits also invalidate each member's object observation.

The matching-runtime native headless journey passed 534 observations, including
save/bind/paint/shared edit/Undo/removal. The original-chat/book journey created
an appearance through its generated form and completed a native receipt without
replaying it after reload. Its screenshot was inspected. These journeys used
scripted responses, not live providers or a headset. Evidence is tracked under
.quest-evidence/spatial-state/appearance-authoring-* and the native-room runs
95d04bc856e845208fdeadbf232a8c9b and 1f89b27dcc1744cf84a2f55280f3362e.
This is an authoring increment; it does not complete weather/light, water/medium
reactions, regional simulation, image-backed texture authoring, or hardware
acceptance. The device hold remains until fresh owner readiness and a health check.


### Portable construction resources (2026-10-08, desktop verified)

Reusable constructions now retain appearances, sound emitters and explicit real-room
collision profiles. This also closes a capture gap that previously omitted explicit
collision-profile bindings. Version-4 blueprints bundle exactly their dependencies;
version-2 prototypes carry local references. Existing resource-free blueprints keep
their current formats. `constructionResources.v1` gates the new public fields.

Capture converts source IDs to deterministic local symbols. Each constructor invocation
allocates independent definitions while preserving sharing among its own pieces.
Existing destination definitions, even with matching symbols/names, remain untouched.
Dormant appearance bindings and exact imported-model identities survive. Model bytes
remain verified external dependencies; this does not claim a self-contained GLB pack.
Copied tone emitters start idle. Collision profiles still obey the global real-room
switch. The same capability, generated book form, agent and program paths are used.
One accepted save/Undo includes pieces, links and definitions. Dangling or unused
bundled definitions, insufficient room capacity, failed storage and missing models
cannot leave partial resources. Temporary Keep/Discard preserves that whole boundary.

Validation: 927 EditMode cases passed. The full PlayMode run plus six targeted reruns
verify 797 unique passing cases and three optional private-file skips. Production
runtime was unchanged between those runs; the targeted pass replaced an obsolete
capture-refusal expectation and added failed-save/missing-model checks. Shared checks
pass 1,171 room/headless cases and 293 book/transport cases, plus app/probe TypeScript,
lint and generated-catalog provenance. Thirty-three shared input fixtures keep native
and web resource closure, ownership, envelope, binding and version rules aligned.

The native headless run completed 552 observations, including capture, removal of
originals, reconstruction, shared fresh IDs, idle playback facts and library cleanup
on Undo. Run: fc5096d7031f42709a12ce9d740aa1bd. The real book journey assigned an
appearance, captured the styled object through generated fields, received native
completion and reloaded without replay. Run: e590889bb4e54251986db08a11600087;
its screenshot was inspected. Two earlier book attempts exposed an automation race
around asynchronous form expansion; the runner now waits for the returned form.
These runs use scripted responses, not live providers or physical Quest testing.
Evidence: .quest-evidence/spatial-state/construction-resources-working.json and
construction-resources-*; docs/QUEST_BATCH_CREATION.md defines the versioned contract.

This checkpoint does not bundle running programs, arbitrary code, texture files,
imported/live audio or GLB bytes into a construction module. Those resource kinds,
image-backed texture authoring, weather/light, water-aware traversal and streamed
regions remain accepted v1 work. No APK was packaged, installed, signed or uploaded.
Headset testing stays on hold pending fresh owner cooling/charge readiness and a health
check. Release and provider acceptance gates remain open; the v1 goal remains active.


### Independent backdrop and real-depth controls (2026-10-08)

The shared world.presentation fact and world.presentation.set action expose a
bounded neutral-backdrop opacity and a separate real-depth preference. Generated
book inputs, agent/program invocations and the two physical tray controls use
the same native state and admission rules. Each change invalidates stale view
requests, including a manual change followed by reversal. Duplicate completed
receipts do not restore an old view after Recall.

Opacity zero reveals passthrough wherever virtual content is absent; one hides
it behind the neutral backdrop. Intermediate values blend that backdrop.
Authored objects, their textures/transparency, and the familiar book keep their
own rendering. Full opacity makes real depth ineligible to avoid unintended
room-shaped holes; a selected depth preference becomes eligible again below
one. Eligibility does not establish platform support, available depth or correct
physical alignment. The existing Meta OpenXR camera subsystem handles
premultiplied clear color.

Changing presentation preserves collision profiles, simulation, object poses,
world placement and autonomous actions. User stick locomotion still requires
full virtual presentation; reducing opacity disables that opt-in without taking
over Maestro. Recall, focus/tracking loss, workspace retirement and restart
restore normal MR. These live viewer preferences are deliberately not a saved
world asset and are not journal Undo operations.

This is the backdrop/depth increment of the accepted reality-layer design.
Imported surroundings, geometry-layer blending, editable passthrough windows,
MR locomotion admission and durable world presentation definitions remain open.
It does not implement camera sharing or infer collisions from visual opacity.
Compositor behavior, both eyes and actual depth transitions still require the
cooled/charged headset. Desktop verification passes 927 EditMode cases and 801
unique PlayMode cases with three optional private-file skips. The full PlayMode
run plus 87 focused reruns corrected two test assumptions (the expanded tray and
concurrent rigid-body capture); production runtime was unchanged between them.
Shared room/book/headless checks pass 1,489 tests in 152 files, with another 55
prompt-contract/ownership checks. App build, lint, probe types and catalog
provenance pass. The catalog contains 110 actions and 125 facts.

The full native headless journey passes 574 observations (run
af3aa4d751504df0b35561e8e8b418ac). The original-book journey changes the view
through generated inputs, checks unchanged saved scene, receives native
completion and reloads without replay (9ee9255295204f4d8638f9ec556fcf5f).
Its screenshot was inspected. These two journeys use scripted responses.

Fresh real Gemini 3.8 Flash journeys also pass on managed staging
(4f9c9bf25c7541ecae1cf367e2a409c9) and BYOK
(735738fa7b3446cfbaeaaaab4461f2ac). Ordinary English learner requests change
the backdrop to 50% with real occlusion off, then restore MR with depth preferred.
Exact native facts, objects, saved revision, collision policy, chat handoff,
provider usage and replies are checked. The two managed action turns reconcile
213 credits / USD 0.204802 across 15 usage/charge entries with no remaining
reservation. These checks cover desktop Unity, not physical depth or compositor
pixels. See QUEST_AGENT_RELEASE_COVERAGE.md for the retained failed attempts and
corrections; evidence is in .quest-evidence/spatial-state/world-presentation-*.
No new APK was packaged, installed, signed or uploaded.


### Swept world travel in mixed reality (2026-10-08)

The next increment lets the user's independently enabled stick and snap-turn
bindings move the same virtual world at any backdrop opacity. View changes keep
both movement opt-ins and require neutral input before movement resumes. Recall,
tracking/focus loss and workspace retirement still disable locomotion. Physical
head tracking, scanned surfaces, physically bound ink and held bodies stay fixed.
No collision policy is inferred from opacity, and movement needs accepted authored
ground rather than an invented flat floor.

During active simulation, world relocation first checks the whole swept frame.
Each moving collider uses its resolved environment profile: real participants
need an aligned scan and must remain in its containment; virtual-only participants
ignore the scan. Fixed physical bodies/controllers remain obstacles. Translation
uses continuous casts; yaw follows the actual pivot arc in segments of at most two
degrees. Conservative bounds, a 1-mm resting-contact tolerance, 128-result buffers
and an 8,192-query ceiling make uncertain or overly crowded transfers refuse
before mutating anything. Irregular geometry can require more clearance than its
exact mesh. This is a bounded admission policy, not a general continuous mesh
solver. Paused physics keeps the existing placement behavior. Normal controller
walking still waits while the user is gripping/manipulating an item.

Rigid movement of an entirely virtual navigation surface repositions its existing
map without rebaking. When real and authored sources move relative to one another,
old paths are removed immediately and rebaking waits for 150 ms and at least two stable frames of world
placement. Direct/manual/authored traversal continues only through current
accepted-ground support and body sweeps; following keeps ownership and reports
that its route is updating, then replans when the map is ready. Losing required
scan readiness still stops walking. This bounds repeated movement-triggered
rebuilds but does not provide streamed asynchronous navigation tiles; a final
room-scale bake is still synchronous and needs physical performance acceptance.

Book inputs, native controls, agent requests and programs use the same existing
capabilities and resolved profiles. Full desktop verification passes 935 EditMode and 812 PlayMode cases, with three
optional private-file skips. All 1,489 shared room/headless/Quest tests pass; the
actual captured controller fixture passes its two shared contract checks. App
build, lint, probe types, catalog provenance and core boundaries pass. The full
native journey passes 586 observations (ce50470ace1f45b281a4abbf2ed541b0); the
original book passes generated movement inputs and reload without replay
(61054388242a44afb1c396ab27db06f0), with its screenshot inspected.
Fresh provider journey results are recorded in QUEST_AGENT_RELEASE_COVERAGE.md. Headset acceptance remains on hold pending the
owner's cooling/charge readiness response. No APK or release change is claimed.


### Visibility composition foundation (2026-10-08)

The chosen layering model keeps visibility, physical participation and sound as
separate properties of the same persistent objects. A future named visual layer
will bind ordinary imported models, recipes and terrain; it will not create a
second environment scene or bypass the normal asset budgets. Per-object real-room
collision profiles already decide whether the scanned floor/walls constrain an
actor. A visual fade must not silently change that policy, navigation, gravity,
audio, object identity or saved geometry.

The renderer foundation now composes layer opacity and acceptance of real depth
after imported/source material settings and root/part/exact-slot appearances.
Texture masks are tested before layer opacity: fading a leaf changes its final
coverage without shrinking its cutout. Glass retains its source alpha and is
multiplied by layer opacity. A zero-opacity layer discards fragments and does not
write invisible depth. Partial opacity uses transparent blending without depth
writes or opaque pencil silhouettes; returning to one restores the source queue,
blend state, depth writes and outline state. Transparent meshes still have the
normal depth-sorting limitations of alpha blending; this is not order-independent
transparency or a physical glass simulation.

Layer state has explicit runtime ownership. Equal sources within a layer share
one leased variant; separate layers/rooms remain independent even if their
current values are equal. Continuous opacity/depth updates reuse those variants.
The existing appearance owner applies and restores the final materials, including
model refreshes and disable/destruction, rather than adding another component
that competes to replace renderer materials. Book browser-page renderers remain
excluded. Other objects nested under their own RoomItem are not inherited by
accident. The shader's existing global depth eligibility/capture bypass remains
the upper bound; disabling depth for a layer cannot enable a disabled provider.

This increment is deliberately internal: it does not expose an unsaved layer
editor, change world.presentation.set semantics, migrate stored rooms, or claim
that users can already group their imported surroundings. The next integration
must add stable saved layer IDs and independent object bindings, strict native
and shared wire validation, revision/membership claims, one-step Undo, temporary
Keep/Discard, archive roundtrips, and closed portable-construction dependencies.
Layer bindings must survive specific material overrides, replaced imported
models and newly generated terrain/ink/liquid renderers. Dynamic renderer creation
and retirement need explicit refresh hooks; rescanning every object every frame
is not an acceptable substitute. A fade must not cancel an NPC walking or a
playing sound, so visual ownership must be compatible with motion/audio ownership.
Zero visual opacity must also have an explicit picking policy: normal pointing
should not be intercepted by an invisible control, while named-object inspection
and deliberate editing remain possible. Physical collision is still independent.
Agent and book controls
must come from the same capability catalog; no separate agent-only path.

Authored layer defaults and a viewer's transient surroundings blend should remain
distinct. Smooth transient changes must not rewrite the saved room every frame.
Editable passthrough windows and physical/world anchoring remain separate pending
increments; this alpha-composition path does not implement them. Headset depth,
binocular compositor behavior, transparency overdraw and sustained performance
still need physical acceptance after the owner's readiness response.

Full desktop verification passes **935 EditMode and 822 PlayMode cases**, with
three expected optional private-file skips. Ten new layer tests cover rendered
pixels at both eye positions, opaque/cutout/blended sources, zero and partial
coverage, source restoration, imported GLB slots, appearance composition,
independent layer ownership and lease cleanup. The 23-case focused rendering run
also passes. Shared room/headless/Quest tests pass **1,489 cases in 152 files**.
The complete native journey passes 586 observations
(7de69b5048484074b9e16e8d9f0d8729); the original book journey passes native controls
and reload without replay (b51a19e7bb504a9ebfda587ae633be43). Its movement-form
screenshot was inspected. These two journeys use scripted responses; this
rendering increment makes no new provider-acceptance claim.

Native export and CI now discover runtime C#, shader/include and assembly files
recursively, alongside the existing template/module JSON and audio-mixer inputs.
They sort and hash the same source scope instead of maintaining duplicated lists
that could omit new helpers. This expands provenance coverage without changing
the 110-action/125-fact contract or any saved format. Fourteen focused native
catalog checks pass. Provenance covers 451 discovered inputs; negative checks
reject both an unrecorded uppercase shader include and a changed hash, then
pass again after exact restoration. CI still cannot compile or execute Unity; those remain
separate local checks. Evidence is retained in
.quest-evidence/spatial-state/visibility-render-*.
No APK was packaged or installed, and no release signing/upload/deployment occurred.


### Saved visual layers and shared authoring — 2026-10-08

The renderer foundation is now connected to saved room resources. A visual layer
has a stable ID, name, opacity and real-depth participation; each room object can
bind to one layer independently of its environment collision profile, acoustic
settings and material appearance. Up to 16 layers may each include all 66 current
room objects. These are present bounded-room limits, not a claim of streamed
country-sized worlds. The book's browser pages keep their original materials and
remain readable even when its frame belongs to a hidden layer.

`visibility.layer.save`, `visibility.layer.remove` and `object.visibility.assign`
are ordinary catalog capabilities. The same generated forms, native scheduler,
receipts and facts serve human, program and agent. Layer member queries page by
16, library queries by three. Shared edits require the exact definition revision
and complete member set; assignment checks both object and layer revisions.
The `visibility` ownership channel coexists with compatible movement, part
animation and audio channels; whole-target ownership still excludes competing
operations. A separate visual reconciliation path avoids reapplying rigid-body,
avatar, recipe and audio configuration. Held objects and authoring gestures
remain guarded. A saved opacity edit is a discrete edit, not a per-frame fade
animation or a viewer's transient surroundings slider.

Room v26, snapshot intent v25 and workspace archive v24 carry this state through
save, restore, one-step Undo and temporary Keep/Discard. Future layer definitions
preserve files as read-only. Portable blueprint v4 gains resource bundle v2 and
prototype v3 when needed; older resource/prototype source remains supported.
Captured modules close every reference and canonicalize local symbols. Each
instantiation allocates fresh definitions, retaining sharing inside the creation
without changing any existing destination layer. The visibility feature gate
prevents older clients from accepting these new resource forms silently.

Procedural ink, liquid surfaces and editable terrain notify their owning visual
view when materials or renderers change. Refreshes coalesce and do not rescan
all objects each frame. Objects hidden by zero layer opacity do not receive ordinary grab/hover
or intercept book/controller pointing. Their physical colliders remain active;
named-object inspection and deliberate editing still work. Visible book pages
remain pointer targets. This does not turn depth images into physical colliders
or implement programmable passthrough windows.

Verification and physical acceptance are recorded separately. Device work remains
on hold pending the owner's cooldown/charge readiness reply. Imported surroundings,
physical/world anchors, transient per-layer fades, passthrough windows, camera
source selection, streaming and sustained headset transparency performance remain
explicit follow-up work; this increment does not claim those complete.


Desktop verification for saved layers passes **941 EditMode and 828 PlayMode
cases**, with three expected optional private-file skips. The final EditMode run
adds archive round-trip and unknown-field coverage; it caught and repaired a
missing strict layer check in the archive reader. Shared room/headless/Quest
and prompt-contract verification passes **1,722 tests in 168 files**. Production build, lint, probe
TypeScript and generated catalog provenance pass. The catalog now exposes 113
actions and 129 facts, derived from 455 runtime source inputs.

The native journey passes **603 observations** in
`f83ba1d0ea1c4294928d92fb8c7e014f`. Original-book run
`70ae6c7b65ae40409fa271575d4c3715` creates a visual layer, assigns it through the
generated form, captures a layered construction, and reloads without replay.
The layer-form screenshot was inspected. Both journeys use scripted provider
responses; real-provider evidence is recorded separately in the coverage matrix.
At that checkpoint the manual forms still exposed definition IDs and revisions.
The named-resource increment below supplies shared selection for appearances,
collision profiles, audio and visibility.

No APK was packaged or installed for this increment. Full-suite simulation and
desktop pixels do not establish headset compositor, transparency performance,
hand/controller comfort or room alignment acceptance.


### Named saved resources in the shared catalog

The generated action form now offers named resource choices from native-owned
`x-choices` metadata beside `x-current`. It covers `object.visibility.assign`,
`object.environment.assign`, `object.appearance.bind` and the configure variant
of `object.audioEmitter.edit`. Each choice references an existing bounded paged
fact and exact literal destination paths at the action/variant root, including
nested object fields. Both native and shared tests check the metadata against
the registered fact types and writable reference fields. No new execution API,
agent-only tool or parallel resource store is introduced.

Users load the object's current guard, then choose a saved definition by name.
Visibility, collision and appearance selections fill ID/revision pairs together;
unrelated preferences and the object revision remain unchanged. Appearance guard
loading reads `object.definition`, covering creations, Maestro and the book.
Duplicate names display distinct identities; the selected exact ID is inspectable.
Lists page explicitly. Refresh removes old list options while retaining the
already chosen exact reference, including its old revision, until the user makes
another choice. It never refreshes a mutation guard or authorizes an automatic
retry. Failed, mismatched or delayed replies cannot overwrite a newer draft,
closed form or different room session. Selecting and browsing never run actions.

A deliberate saved-resource choice remains literal when adding a reusable
program, even if it equals the currently bound resource. The separate visible
Read block can still load the object guard at run time. Users may deliberately
change the program's current-value choices afterward. Sound emitter bindings
refer to a shared sound ID, as the native contract already specifies; the picker
does not invent a source revision guard. A future playback captures its source
revision, while an already playing instance keeps the revision it started with.

The common selector is extensible through additional catalog entries with the
same bounded library shape. It does not implement arbitrary search, live asset
imports, transient per-layer interpolation or larger streamed worlds. Those
remain separate adapters and spatial-state work, with the same shared contracts.


Named-resource verification passes **942 EditMode and 828 PlayMode cases**,
with three expected optional private-file skips, and **1,731 shared room,
headless, Quest and prompt tests in 170 files**. Focused selector regressions
were rerun after the final unnamed-resource and explicit-selection refinements.
Build/type checks, lint, catalog provenance, core boundaries and bundled-asset
integrity pass. Native run `7d666ea1c408433a9dae7fe4bdf68703` passes **606
observations**. Final original-book run `da0a822769aa43528f2172ac8837ff43`
selects all four libraries by name, receives completed native assignment receipts
and reloads without replay or browser errors. The visible named sound selector
and layer selector screenshots were inspected. An earlier complete book run
`279e83a7a4f640b8a1f19c0f34226db4` also passed; the final run improves the
sound screenshot's scroll position. The initial full PlayMode attempt was stopped
by an eight-minute focused-test supervisor timeout while still progressing;
the complete rerun used the appropriate longer window and passed.

These new journeys use scripted provider responses. The earlier real managed
and BYOK WorldPresentation runs remain separate evidence; this UI/catalog
increment does not claim a new live-provider or physical-headset pass. No new
APK, release signing, upload or deployment occurred. Device work remains on
hold pending the owner's cooldown/charge readiness and a fresh health check.


### Temporary layer presentation — 2026-10-08

`visibility.layer.present` starts a transient view transition for an existing
saved layer. A named library selection supplies its stable ID, then the ordinary
current-value read loads two guards: the layer view state and tracked world view
state. The user and agent use this same registered action and the
`visibility.presentation` fact. Changing a lookup selection invalidates the
reviewed snapshot, including when switching back. Choosing a name never executes
or refreshes a guard by itself.

Requested opacity multiplies the authored layer default. A multiplier of 1 returns
to that default; it does not force an authored translucent object opaque. The depth
preference is also an upper bound on authored and global depth eligibility. It
cannot enable an unavailable depth provider. Zero to thirty seconds controls a
smooth interpolation, with zero snapping immediately. Retargeting begins from the
current interpolated value; an identical request does not restart an active fade.
The native renderer retains its leased material identities while values change.

A completed command receipt confirms the target preference was accepted. It does
not claim the transition has finished: `progress.blending`, `currentOpacity`,
`effectiveOpacity` and `remainingSeconds` expose its progress. The fade continues
independently of the issuing program after command completion. Program Stop does
not reverse an already completed view request; a fresh guarded request changes
it. Frame progress does not invalidate the control identity.

Saved layer membership remains live: newly assigned members inherit its current
viewing preference. Editing or undoing the saved layer definition resets that
layer to the accepted authored default. The movement tray's explicit **Stop / MR**
control, tracking/focus loss, disabled view/editor, temporary-room boundaries and
workspace recovery discard transient preferences. Keep saves authored contents
only. Book/tool Recall preserves the current view and movement opt-ins, as its
existing contract promises; it is distinct from Stop / MR. The earlier backdrop
notes used “Recall” ambiguously for movement recovery. The native descriptions
now make the distinction explicit.

No frame writes a room document, creates an Undo entry, changes collisions or
sound, repositions objects, stops autonomous NPC activity, or dims the book's
browser pages. Saved collision profiles remain independently responsible for who
interacts with the scanned physical floor and walls. A transparent visual layer
can still be solid when its collision policy says so.

Desktop verification passes 947 EditMode tests, six focused final-source layer
runtime cases and 1,734 shared room/headless/Quest/prompt tests in 170 files.
The final full PlayMode suite passes **835 cases**, with only the three documented
optional external-asset skips. Native run
`0c92674367ef4f8b8afe509a56c47d84` passes 612 observations; original-book run
`f7a3e11a1ea849a99e9d551bf74a0ec4` exercises named lookup, current reads and native
fade execution, with its screenshot inspected.

Fresh managed `226b39ac50de432a9921bde2ec933fff` and BYOK
`2191c5ea08434456b183c57d9868674d` journeys both pass eight ordinary-language
action turns, including fade, restoration and final chat. Managed billing reconciles
1,178 test credits / USD 1.145306 across 70 usage/charge rows, with no reservations
remaining; introductory chat and failed attempts are excluded. The expanded test's
old ten-minute supervisor interrupted an earlier managed run; a bounded thirteen-minute
window and explicit timeout diagnostics address that harness limit without changing
app safeguards. Full evidence and limitations are in
[the coverage matrix](QUEST_AGENT_RELEASE_COVERAGE.md#temporary-layer-presentation-2026-10-08).

Physical Quest acceptance remains on hold for the existing cooling/charging
readiness reply and a fresh health check. No APK was built or installed for this
increment. Imported environment scenes, passthrough windows, camera sharing and
streamed regions remain separate accepted work.
