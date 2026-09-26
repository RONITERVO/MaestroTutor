# Maestro Quest 1.0 delivery record

Status: active implementation. Nothing in this document claims store readiness.

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
the latest packaged checkpoint. Broader role profiles, blending, deletion and
large-library headset QA remain open. The user is sleeping while Quest charges;
device queries/installations remain on hold.

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
  AI text-to-3D and public content sharing are outside this first release.
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
