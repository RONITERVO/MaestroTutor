# Editable default-world integration

Status: source review and implementation contract, **not an installed world pack**.
Reviewed on 2026-11-14 against Maestro `7a406b97` and the proposed
[HyperCosmos world commit](https://github.com/RONITERVO/MaestroTutor/commit/74086a9e077c8612db78e449a8ff7e52704f55e8).
Keep the full [accepted v1 scope](../../QUEST_V1_SCOPE.md) and
[current release gates](../../QUEST_STATUS.md). This work does not relax device,
provider, deployment, signing or Meta holds.

## Direction

Retain the proposed observatory, islands, connected paths, celestial exhibits,
procedural visual studies and learning content as a versioned, editable content
pack. Use the same saved objects, resource definitions and typed programs available
to users and agents. Do not merge a second scene, lesson, input or audio owner.
The broader Cosmos content remains integration work; a first verified area is an
implementation milestone, not completion of that vision or the living-world scope.

Keep the familiar book and language-first entry. An included setting must not force
virtual view, change languages or start narration on a learner's behalf, including
on first use.
The pack can be chosen through the original book or a language request. Existing
rooms and deleted/edited defaults are never regenerated merely because a theme or
application version changed. The minimal fresh-recovery option remains available.

The requested immersion depends on consistent consequences: a deck remains valid
support when its visual layer fades, a planet the user edits is the one the agent
observes, a saved change survives restart, and a spoken lesson is only complete
when the app's actual playback path says so.

## What the proposed branch proves and does not prove

This is source evidence; its isolated rendering/math tests were not run here.
Pinned file links below refer to the reviewed proposal, not accepted runtime code.

| Finding | Evidence | Integration decision |
| --- | --- | --- |
| Islands are private scene objects, absent from saved room definitions. | [Archipelago builder](https://github.com/RONITERVO/MaestroTutor/blob/74086a9e077c8612db78e449a8ff7e52704f55e8/unity/MaestroQuest/Assets/Maestro/Runtime/Cosmos/CosmosArchipelagoBuilder.cs#L381) and [test expecting unchanged room identities](https://github.com/RONITERVO/MaestroTutor/blob/74086a9e077c8612db78e449a8ff7e52704f55e8/unity/MaestroQuest/Assets/Maestro/Tests/PlayMode/HyperCosmosPlayModeTests.cs#L209). | Expand ordinary authored objects; legacy walkable markers alone do not provide saved identity, revision guards, editing or residency dependencies. |
| Changing visual mode disables the supporting colliders. | [Activation](https://github.com/RONITERVO/MaestroTutor/blob/74086a9e077c8612db78e449a8ff7e52704f55e8/unity/MaestroQuest/Assets/Maestro/Runtime/Cosmos/CosmosArchipelagoBuilder.cs#L69). | Use shared visibility layers. Collision and acoustic participation stay independent. |
| Flight reads grips/triggers/sticks outside existing manipulation and binding ownership. | [Flight entry](https://github.com/RONITERVO/MaestroTutor/blob/74086a9e077c8612db78e449a8ff7e52704f55e8/unity/MaestroQuest/Assets/Maestro/Runtime/Cosmos/CosmosFlightController.cs#L395). | Extend shared locomotion only when its lifecycle, bindings and viewpoint persistence are implemented. A grab/page/drawing gesture cannot also cause flight. |
| Captions progress by duration timers; no spoken narration playback is connected. | [Caption timeline](https://github.com/RONITERVO/MaestroTutor/blob/74086a9e077c8612db78e449a8ff7e52704f55e8/unity/MaestroQuest/Assets/Maestro/Runtime/Cosmos/CosmosInkPresenter.cs#L306) and [private audio sources](https://github.com/RONITERVO/MaestroTutor/blob/74086a9e077c8612db78e449a8ff7e52704f55e8/unity/MaestroQuest/Assets/Maestro/Runtime/Cosmos/CosmosAudioEngine.cs#L355). | Route teaching through original-app context, active languages and actual speech delivery. Timed captions are not evidence of heard speech. Ambient sound uses shared emitters and voice budgets. |
| The catalog declares 376 zones but contains 137 `RawZoneSeed` entries; missing zone lookup chooses a nearby entry. Word timings are calculated from text weights and duration. | [Catalog data and lookup](https://github.com/RONITERVO/MaestroTutor/blob/74086a9e077c8612db78e449a8ff7e52704f55e8/unity/MaestroQuest/Assets/Maestro/Runtime/Cosmos/CosmosCatalogData.cs#L819), [cue calculation](https://github.com/RONITERVO/MaestroTutor/blob/74086a9e077c8612db78e449a8ff7e52704f55e8/unity/MaestroQuest/Assets/Maestro/Runtime/Cosmos/CosmosCatalogData.cs#L1054). | Inventory actual supplied entries/assets and their provenance. Exact missing IDs refuse; never substitute a nearby lesson or describe estimated timings as measured audio alignment. |

The original [HyperCosmosVR](https://github.com/RONITERVO/HyperCosmosVR) implementation
has a different native renderer and asset pipeline. Its documented catalog sizes,
audio pack and PC performance are not Unity/Quest acceptance. Retain relevant source
licenses/attribution when adapting content; review actual imported bytes and text.

## Reuse the existing contracts

- `CreationTemplates` expands immutable, exact-hash JSON into detached editable
  source. `CreationPrototype` carries geometry and supported components with fresh
  object IDs. The created instance no longer depends on a display name or mutable
  template selection.
- `CreationBatch` v4 handles a closed 1–16-object construction with local symbols,
  remapped resources/links, one accepted save and Undo. Included program modules
  already expose editable constructor functions. Use this path for adding a
  bounded construction to the current room.
- `WorkspaceArchiveSnapshot` / archive v34 validate complete room47, rules,
  controls, activities, modules, assets and remembered state. Generation staging
  and activation preserve the previous accepted workspace. Reuse these for an
  explicitly chosen new/open world; they are not an additive merge API.
- `visibility.layer.save`, `object.visibility.assign` and
  `visibility.layer.present` already separate saved grouping/transient fade from
  collision and sound. Existing environment profiles remain per-entity choices.
  Ordinary static support, terrain and imported walkable meshes retain their own
  exact owners; no duplicate invisible floor or private collider registry.
- Shared appearance/image/material slots and world audio source/emitter contracts
  remain authoritative. A custom Cosmos shader or synth is not automatically an
  editable supported component: add a bounded versioned adapter if the existing
  style/audio schema cannot express the required effect. Preserve the illustrated
  style option and original book-page appearance.

See [world authoring](../../QUEST_WORLD_AUTHORING.md),
[creation templates](../../QUEST_CREATION_TEMPLATES.md),
[static support](STATIC_SUPPORT.md) and [audio ownership](../../../unity/AUDIO.md).

## Missing shared foundation

A complete bundled-world installer does not currently exist. Implement a generic
immutable pack descriptor and detached expansion/preview using existing canonical
object/resource/program schemas, not another executable DSL. A manifest records
pack ID, version, exact source hash, available entry IDs, required features, asset
hashes and provenance. Preview reports the complete proposed changes, missing
inputs/dependencies and saved/native/render/physics/audio costs before application.

Keep these operations distinct:

1. **Create/open a new themed world:** instantiate fresh world/entity/resource/
   program/region IDs, remap typed references, stage a fully validated generation,
   then activate it through the existing workspace lifecycle. Preserve the previous
   generation and make post-commit/lost-response outcomes discoverable. Exact archive
   restore still preserves the archived identities rather than creating a new copy.
2. **Add to this world:** use a bounded construction batch where sufficient. A
   larger multi-document pack needs a shared transaction/merge implementation with
   conflict, cancellation, rollback and Undo semantics. Do not replace the current
   workspace, silently delete objects to free capacity, or call a partially completed
   sequence of small operations an atomic whole-pack install.

Persist pack-instance provenance and exact slot mappings in a versioned,
archive-covered contract. A PlayerPrefs flag or separate unarchived file is not a
sufficient install receipt. Missing scenery is not permission to reinstall it.
Pack upgrades require explicit conflict-aware import/copy/merge; preserve user edits.
Do not rewrite arbitrary JSON strings to remap IDs: typed references and ordinary
lesson text must remain distinguishable. Require dependency closure and zero
unavailable programs for shipped defaults.

Current first-run `StarterDocument` includes the book, Maestro and three ordinary
practice objects. Explicit fresh recovery intentionally creates only the book and
Maestro plus baseline documents/included content. Preserve that recovery behavior;
choosing a themed new world is a separate source choice, not a change to what
“recover fresh” means. Corrupt/newer/missing-pointer stores must remain protected.

## Interaction, lessons and larger scope

Every supporting or interactive exhibit must be discoverable and editable through
ordinary world/object/part/resource facts and the same human/agent controls. A
non-interactive distant background may use a declared bounded render component,
with honest ownership and costs; it cannot stand in for playable terrain.

Use existing opt-in walking and saved independent user/avatar bindings first.
Flight/scale is a separately admitted shared movement mode: neutral re-arm,
manipulation exclusion, tracking/focus/runtime gates, whole-frame motion, Stop,
recall, workspace retirement and supported saved viewpoint semantics are required.
No raw trigger/pinch reader or direct-transform fallback. Scientific exhibit scale
must not silently change physical world units used by ground, collisions and audio.

“Look to learn” needs a generic bounded attention observation/event and original-app
context integration. Define exact subject identity, head-ray versus eye tracking,
dwell/cooldown, visibility, cancellation and conversation/language changes. A gaze
observation is neither fabricated user speech nor unrestricted permission for a
room mutation/provider call. The original chat owns tutoring and proactive useful
artifacts/tools; the content pack supplies context. Maestro retains the original
speech session, mouth/head route and exactly one output route. Shared world-audio
roles, emitters and mix own ambience; they do not replace the existing speech/web
players' volume ownership. Completion needs actual playback evidence from each
route, including interruption and an accurately labeled caption-only fallback.

The first complete integrated observatory can prove this path using a deck,
reachable bridge, editable exhibits, ambient source and an explicit request-to-learn
interaction. Continue the rest of the proposed content through the same path.
Do not increase limits or hide hundreds of objects inside one opaque scene merely
to claim coverage: current 256 saved creations, 128 native roots, 256 total recipe
parts, 512 reserved collision pieces, four terrain fields and other per-kind limits
still apply. Larger residency/streaming remains part of the accepted living-world
work and needs measured admission rather than a smaller redefinition of success.

## Acceptance before calling the world integrated

- Exact manifest bytes and IDs; missing/duplicate/unsupported data fails. Two new
  instances have fresh identities but matching authored meaning; internal sharing
  survives typed remapping and literal text is unchanged.
- Human and agent inspect/edit the same exhibit. Stale edits fail; copy, Undo,
  save, restart and archive round-trip preserve actual source/resources/programs.
- No automatic reinstall after deletion or upgrade. Failed/cancelled staging
  preserves the old world; lost responses reconcile the exact committed outcome.
  Adding content preserves unrelated world/control/program bytes and identities.
- MR/blended/virtual presentation and visibility fade retain configured support,
  physical consequences and independent collision/audio policies. Dormant or
  missing ground follows the same dependency rules as user-authored geometry.
- Grabs, drawing, book interaction and buttons cannot also move the user. Tracking
  loss, pause, focus loss and workspace replacement stop/re-arm movement correctly.
  Recall keeps its existing tool-retrieval semantics and preserves presentation
  and movement opt-ins; a pack must not silently redefine it.
- Real native Headless and rendered Book journeys exercise the shared application
  path. Later physical Quest and managed/BYOK lessons verify actual speech,
  observation/context, depth/stereo, comfort, long-session resources and performance.
  Pure shader/math tests alone cannot establish these outcomes.

Private source-review reports are under
`.quest-evidence/default-world-review-20261114/`; they are not public artifacts or
runtime verification. No proposed branch code has been merged by this review.
