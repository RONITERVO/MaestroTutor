# Maestro Quest v1 accepted product scope

This is the canonical accepted product scope, reconciled on 2026-11-01. It records
requirements and their boundaries; it does not claim they are all implemented or
accepted for release. Use [current delivery status](QUEST_STATUS.md) for operational
holds, the active release checklist, exact source/build identities and latest proof.
Dated evidence remains in the [delivery record](QUEST_V1_PLAN.md).

## Product and shared ownership

Deliver a native Unity mixed-reality client for long-term Meta Quest Store release,
with Quest 3 as the current hardware acceptance target and **adults 18+ for v1**.
The original web/phone app remains supported. This is another way to use Maestro,
with the same familiar language-learning conversation, not a separate tutor.

The original app owns Gemini access, managed account/credits, BYOK routing,
conversation, tools, generated media and delegated task orchestration. Unity owns
native world state, interaction and validated execution through a versioned bridge.
Do not duplicate provider clients, tutor prompts or billing in Unity, or require a
separate room-agent subscription. Shared services do not imply cross-device chat
sync; current chat storage is local IndexedDB. See [unified agent ownership](QUEST_UNIFIED_AGENT.md),
[managed access](QUEST_MANAGED_ACCESS.md) and [adult/privacy requirements](QUEST_PRIVACY.md).

## Familiar book and language-first interaction

- Use the original app's components and session across two full book pages.
  Default: current conversation on the right, preceding conversation on the left,
  earlier history on previous pages, artifacts still inline. Preserve the
  message-identity bookmark as a physical ribbon.
- Offer the accepted alternate practice layout: chat on the left, selected/latest
  artifact on the right, with clickable artifact previews in chat.
- Fill the page surfaces with the familiar interface. Add no permanent book chrome,
  counters or extra toolbars over it. Optional detailed authoring workspaces open
  as book content and return to chat. Additional interactive elements outside the
  pages must be physical 3D objects, not floating flat panels.
- Spoken or typed language is the default way to ask for simple or complex actions.
  Direct manipulation, buttons and visual/source authoring remain available to
  people who want them; learning languages must not require learning the editor.
- Preserve proactive useful artifacts and tools, including combined outputs in one
  reply. A learner need not request every helpful image, game or audio aid. Room
  mutation still requires a valid current request/intent through the shared tool
  verification and native admission path; an artifact or generated suggestion
  cannot manufacture permission.
- A delegated task receives the original request and captured tutor context through
  the app, keeps progress/Stop/results in the original conversation and uses the
  existing activity flag/avatar states. The chat model remains conversational;
  detailed planning, observations and receipts belong to task state.

Sources: [original book/interface decisions](https://github.com/RONITERVO/MaestroTutor/blob/f7a252feafcea470ddffe78df62512cd63a04de7/docs/QUEST_V1_PLAN.md#accepted-product-scope),
[shared visual workspace](QUEST_AGENT_WORKSPACE.md), [handoff contract](QUEST_UNIFIED_AGENT.md)
and [required learner/provider coverage](QUEST_AGENT_RELEASE_COVERAGE.md).

## One editable user/agent world

Users can build the game they play, primarily by asking Maestro. Human gestures,
book controls, programs and delegated agents must edit the same saved definitions
through the same capabilities, validation, revisions, ownership, Undo and receipts.
An observed successful physical outcome and a durable saved edit are distinct from
command acceptance or a fluent reply. Concurrent human edits must remain protected.

Provide understandable views of object/part hierarchy, behaviours and animation.
Blocks and source represent one canonical typed program; recipes are editable
construction data whose meshes are derived resources. Support reusable functions,
state, branches/conditions, waits, event/time triggers and bounded parallel work.
A user or agent can start a program directly without creating a button; the same
program can also be bound to room/controller buttons, tutor states or VR events.
Local programs and Unity handle continuous behaviour without an LLM call per frame.
Stop, interruption, conflict, missing input and partial completion remain visible.

New engine primitives still need implementation and verification. Composing existing
capabilities must not pretend to supply an unavailable simulator or turn imported
meshes into editable procedural source. See [world authoring](QUEST_WORLD_AUTHORING.md),
[capability architecture](QUEST_CAPABILITY_ARCHITECTURE.md), [event programs](QUEST_EVENT_PROGRAMS.md)
and [shared program modules](QUEST_PROGRAM_MODULES.md).

## Creation, imports and the default play kit

Include the book and default full-body Maestro offline. Provide in-app drawing,
object/recipe construction, compatible GLB/VRM imports, animation imports, editing,
copying, placement and reusable constructions. Models and simple animated characters
such as a jointed box robot can be authored inside the app by either user or agent.
Imports use explicit selection/preview/acceptance, retain original source/attribution
and exact asset identities, and report unsupported content or missing dependencies.

The starter kit demonstrates reusable components rather than separate engines for
each toy: drawing/erasing tools and compatible surfaces; ordinary and mounted
buttons; tableware, dominoes and balls; building pieces and resettable constructions;
mechanical fidgets; a physically playable chess set; simple articulated characters;
containers, pouring and bounded snow/surface manipulation. Every shipped example
must be inspectable/remixable with usable physical settings and an acceptance
scenario. Chess-specific legal-move logic, arbitrary mesh/skin painting and automatic
container behaviour for every imported bowl are not implied by those examples.

The shared Penzil/Scetch-War-inspired pencil and watercolor direction applies to the
book, included characters and compatible user/imported objects. Keep Maestro clearly
cartoonish and avoid a realism-driven uncanny appearance. Functional interaction is
the current priority; art can iterate. Preserve the original page UI's appearance.
See [art direction](QUEST_ART_DIRECTION.md), [starter components and boundaries](QUEST_WORLD_AUTHORING.md#reusable-components-and-representative-defaults),
[shared model import](QUEST_MODEL_IMPORT.md) and [motion batch import](QUEST_MOTION_BATCH_IMPORT.md).

## Avatars, animation and input

Support choosing a compatible custom Maestro, recording/keyframing object motion,
posing avatar arms/legs/head, imported motion playback, gestures, attention/head
turns and follow/walk behaviour. Tutor activity and authored rules can select
animations; users retain explicit exact motion choices and per-avatar assignments.
Animated page turns, idle/listening/speaking/greeting/pointing and appearance changes
respect reduced-motion settings and manual ownership. User locomotion and Maestro
movement have separate editable bindings; user locomotion is opt-in. User-created
3D action buttons may attach to a controller and be operated by the opposite one.
Preserve reserved system inputs and an accessible hand/controller recall path.

The supplied **Meshy Azure Violet Doll** is now the included default; the original
sketch remains the canonical gesture driver/fallback. The current separate offline
collection contains 178 unique motions from 180 exports, with editable starting
tutor-state assignments. This supersedes the old “forthcoming default” wording.
The owner's expectation of several hundred default motions remains an open
curation/acceptance target; no exact final asset count was fixed. The current
178-motion collection is not evidence that this expectation is complete. Separate
motion packs, deduplication and bounded on-demand playback avoid bundling a full
character per animation.

Imported motions retain all supported animated channels; the 17 canonical posing
controls are not a 17-bone playback cap. The current included rig has 68 skin joints.
The old 32-clip-per-source limit is not the separate library's total capacity.
Exact rig identity matters: same names/Mixamo labels do not make the earlier old-rig
collection compatible. Same-rig playback and canonical humanoid gestures are distinct
from general cross-rig imported-motion retargeting, which remains unimplemented.
Curation, source-pose versus navigation-adjusted fidelity, wider avatar compatibility,
foot/ground quality and physical Quest comfort/performance remain acceptance work.
See [included avatar](QUEST_INCLUDED_AVATAR.md), [animation library](QUEST_ANIMATION_LIBRARY.md),
[starting assignments](QUEST_AVATAR_ACTIVITIES.md#included-starting-assignments),
[animation authoring](QUEST_ANIMATION_AUTHORING.md) and [custom avatars](QUEST_CUSTOM_AVATARS.md).

## One world with adjustable reality

Passthrough, blended surroundings and virtual-only presentation are views of the
same persistent world. Allow included/imported surroundings, adjustable blend and
object opacity, and editable passthrough windows on real surfaces or in virtual
space. View changes must not duplicate entities, lose the apple or enable sharing.
Keep physical tracking fixed and represent virtual-world locomotion explicitly,
including when passthrough is visible. Grabs, navigation, physics, saved placements
and camera observations must use consistent coordinate spaces.

Real-room collision participation is selectable per entity: a ball can use the real
table while Maestro uses a virtual hillside below the physical floor. Ignoring real
colliders does not remove gravity or invent virtual support. Rigid items must react
to gravity, grabs, throws and compatible scanned/virtual geometry, with explicit
animation-to-physics release. Scan permissions, alignment, interruption and recovery
are part of the feature. Live real objects, hands and controllers should occlude
virtual content appropriately; live depth visibility is not a moving-collider scan.
Collision, visual occlusion, passthrough/opacity and acoustic participation remain
independent policies.

The familiar camera picker must distinguish real-world camera, mixed-view screen
sharing and virtual scene, retaining generated-image/Off choices and audio-only
Live. Sharing is explicit and source-accurate; denial or unavailable input cannot
silently substitute another camera. All sources use the original app's media/Live
pipeline, with bounded delivery and lifecycle cleanup. See the owner decisions on
[cameras](QUEST_V1_PLAN.md#selected-camera-sources-owner-clarification-2026-10-07),
[occlusion](QUEST_V1_PLAN.md#live-real-world-occlusion-owner-clarification-2026-10-07)
and [reality layers](QUEST_V1_PLAN.md#one-world-with-adjustable-reality-layers-owner-clarification-2026-10-07).

## Living terrain, materials, water and sound

Shared authoring includes texture/material/opacity, imported and app-generated
images through the existing media pipeline, height-changing terrain, weather,
lighting and world time. Water bodies and rain must agree with their visible
geometry, quantities, contacts and actor policies. Hands, objects, Maestro and
other characters must not ignore relevant water/ground/collision interactions;
unsupported traversal or unavailable simulation must be reported honestly.
Bounded local water, pouring, snow deformation and reusable behaviours support
playable worlds; they do not imply a full fluid, granular or ecological solver.

The larger-world direction includes user-built settlements, terrain/water settings
and characters/life interacting within them. Observed, audible and interacting work
must stay appropriately active; unloaded content retains accepted identity/state.
Broader region streaming, imported surroundings, landscape/river flow, underwater
interaction and general crowd/interior navigation remain unfinished work. Current
local components must not be presented as completing that living-world direction.

Users and agents can define reusable generated/imported/procedural/live audio,
attach emitters to objects or joints and control it through the same events/programs.
Maestro's speech originates at the mouth/head and follows the avatar. Ordinary chat,
agent narration, Live and replay retain original-app ownership and exactly one
output route; music/artifacts are not automatically moved to Maestro's mouth.
Sound should respond to real and virtual surroundings with independent editable
acoustic policy, prioritizing intelligible language learning. Reflection rendering,
echo handling, additional live protocols and physical audibility/latency acceptance
remain incomplete; they are not silently removed from the audio direction.
See [shared immersion](QUEST_WORLD_AUTHORING.md#shared-immersion-contract),
[world appearances](QUEST_WORLD_AUTHORING.md#reusable-illustrated-appearances),
[living-world clarification](QUEST_V1_PLAN.md#authored-living-worlds-appearance-terrain-weather-and-water-2026-10-07)
and [world audio](../unity/AUDIO.md).

## Durability, accessibility and release acceptance

Preserve stable object/asset/program identities, meaningful Undo, saved-versus-
temporary play, explicit Keep/Discard, portable backup/restore and visible save
failures. Source changes, stop, interruption, workspace replacement and process
restart must not replay uncertain physical effects. Future released users' worlds
need compatibility/recovery; permission to reset this developer's data is not a
policy to discard user data after release. Imported assets and unfinished operations
must remain bounded and protected by their actual owners/references.

Require comfortable seated/standing use, readable book text, hand/controller parity,
reduced motion and recovery when tools are unreachable or tracking is lost. Developer
tests must exercise the same user/agent path, alongside independent physical input,
rendering and persistence checks. Native/offline evidence, real managed/BYOK provider
journeys, current-candidate Quest acceptance and Store review are separate gates.
Retain the novice English-to-Spanish lesson, proactive artifact/tool combinations,
room creation, physics and text-to-Live continuity in [release coverage](QUEST_AGENT_RELEASE_COVERAGE.md).
Passing commands or desktop suites alone do not establish complete v1.

## Current limits and explicit exclusions

Current implementation limits are evidence boundaries, not new product promises:
256 saved creations, 128 native roots, 16 liquid containers, 25-metre authored bounds
and four height fields. The motion library currently allows 1,024 entries within
128 MiB, with eight clips/800,000 curve values cached for playback. Neither those
numbers nor an internal spatial-index capacity certify a full city or every asset
combination on Quest. Expansion needs complete shared discovery/editing, bounded
work, recovery and measured hardware acceptance; increasing constants is insufficient.
The contracts linked above own detailed import/resource limits. Current full-audio
setup calls for headphones; that guidance does not prevent reading, typing or room
editing and does not certify echo or headset audio acceptance.

V1 excludes hair/cloth interaction physics, arbitrary executable native-world
scripts or runtime compilation, external AI mesh-generation service integration,
and public content sharing. Typed editable programs/recipes, normal model/image
imports and the original app's supported interactive HTML/SVG artifacts remain in
scope. Full room-scale fluid/granular simulation, universal model/material
fidelity and automatic cross-rig compatibility are not promised. No automatic
cross-device chat sync is implied. Unsupported content must remain explicit, with
accepted work preserved.

Provenance: this reconciles the [original accepted scope snapshot](https://github.com/RONITERVO/MaestroTutor/blob/f7a252feafcea470ddffe78df62512cd63a04de7/docs/QUEST_V1_PLAN.md#accepted-product-scope),
the later owner clarifications linked above, [agent-first authoring](QUEST_AGENT_WORKSPACE.md)
and the [living-world contract](QUEST_WORLD_AUTHORING.md). It changes documentation
organization, not accepted scope or operational authorization.
