# Maestro Quest 1.0 delivery record

Status: active implementation. Nothing in this document claims store readiness.

Latest PC increment (2026-09-30): calculated condition waits compose inspected facts
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
catalog (16 actions, ten events and five facts), one scheduler and native
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
- User confirmed there is no Meta developer-dashboard app yet. App identity and
  store setup remain required external release gates.
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
