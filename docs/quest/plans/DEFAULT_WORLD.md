# Editable default-world integration

Status: the bounded bundled-world source, preview and new/open path are implemented.
Final focused native checks, full offline Headless and rendered Book journeys,
and unsigned Android compilation pass. Exact evidence and earlier
broad-regression boundaries are in the [current status](../../QUEST_STATUS.md#latest-verified-proof).
This is not headset installation or completion of the wider living-world scope.
The original source review on
2026-11-14 compared Maestro `7a406b97` with the proposed
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
- `WorkspaceArchiveSnapshot` / archive v35 validate complete room47, rules,
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

## Implemented bounded first slice

[WorldPackCatalog](../../../unity/MaestroQuest/Assets/Maestro/Runtime/Persistence/WorldPackCatalog.cs)
reads a bounded immutable source whose selected identity is the exact pack
ID/version/SHA-256, with one named entry. Descriptor version 1 strictly validates
its schema, source variants, required features and closed local dependencies.
It reuses canonical `CreationBatch` data, not a second executable language.

The initial source supports inline recipes and non-model prototypes with supported
native components, local appearances, tone sources, environment profiles and
visibility layers. External assets, model/template/motion dependencies, imported
material bindings, image appearances and non-tone sources are explicitly refused;
`dependencies` and `assets` must be empty. This boundary does not remove those
features from ordinary authoring or the accepted later pack scope. The actual
bundled Maestro avatar and motions still come from the existing baseline and their
exact payloads are validated with the prepared archive.

The included [Observatory workshop source](../../../unity/MaestroQuest/Assets/Maestro/Resources/WorldPacks/observatory.json)
contains three ordinary editable recipe objects: a low deck, a contiguous bridge,
and a globe with editable parts. Deck and bridge opt into shared fixed-primary
static support. One shared visibility layer groups the exhibits independently of
collision policy. These are 7 recipe parts and 5 reserved collision pieces; these
counts are structural admission information, not measured performance or route
acceptance. The sample starts idle and adds no narration, lesson, program, language
choice, movement mode or private audio/render/collider owner.

[WorldPackPreparation](../../../unity/MaestroQuest/Assets/Maestro/Runtime/Persistence/WorldPackPreparation.cs)
expands once into a detached snapshot using `WorkspaceDefaults.Snapshot` with the
actual bundled content. It preserves the familiar book, Maestro, baseline controls,
activities and presentation. New world, instance, object and resource identities
are retained from that expansion; typed local links/sharing are remapped, while
ordinary text is unchanged. All missing-asset/program counters must be zero. The
same prepared snapshot is staged and opened; acceptance does not expand it again.

The shared user/agent path is explicit:

1. Read `workspace.packs` and `workspace.current`; select the exact descriptor and
   current workspace revision with `workspace.pack.prepare`.
2. Follow `workspace.archive.selection` to its completed preparation and inspect
   `workspace.pack.preview`. Counts and verified archive bytes describe this exact
   candidate. Preview and `workspace.archive.cancel` leave live content unchanged.
3. Open only through the existing `workspace.archive.activate` request, generation,
   manifest and revision guards. A changed workspace invalidates the pack preview.
   Activation preserves the accepted previous workspace and opens under the
   existing activity-review hold. Review acceptance remains a separate action.
4. Inspect and edit the resulting objects with ordinary facts/actions and Undo.
   Use existing previous-workspace preparation/restoration to return; opening a
   world is not an additive batch or a whole-world object Undo operation.

[world-packs.v1.json](../../../unity/MaestroQuest/Assets/Maestro/Runtime/Persistence/WorldPackDocument.cs)
records world/instance identity, exact descriptor identity and object/resource slot
mappings. Archive v35 carries it, while retaining read compatibility for v32–v34;
old archives have no fabricated pack provenance. This is historical origin, not
ongoing ownership or proof that every original object still exists. Ordinary saves,
exports and restores keep it after user edits or deletion. Deletion never causes
reinstallation; a version update does not silently replace an accepted instance.

The provenance document is optional for ordinary worlds and older archives. If
present, it must be strict version 1 and match the accepted room world ID;
malformed, future, unrecognized or mismatched evidence is preserved and refused.
Archive v35 can carry it; older v32–v34 manifests cannot introduce it. A declared
archive entry must still be present and match its hash. Document bounds remain
sixteen instances, 256 historical object mappings in total, canonical per-kind
resource limits and 1 MiB of metadata. These limits do not enlarge authored-world
capacity or make deleted mapped objects into live dependencies.

Catalog and installed-origin pages use their declared schemas, including valid
empty and past-end pages. After returning to an ordinary world without provenance,
the installed-origin page is available and empty; no success value is substituted.
Activation changes the native session, so clients reconcile the exact tracked
status through bounded read-only polling. They do not replay activation or edits
to recover a missing reply. Preparation, committed activation, opened content and
activity-review completion remain distinct states.

## Remaining shared foundation

Keep these operations distinct:

- **Create/open a new themed world:** use the implemented detached preparation and
  existing generation activation lifecycle. Future asset/program/region expansion
  must retain exact typed mappings and dependency closure. Exact archive restore
  preserves archived identities rather than creating a new copy.
- **Add to this world:** use a bounded construction batch where sufficient. A
  larger multi-document pack still needs shared merge/transaction semantics for
  conflict, cancellation, rollback and Undo. Do not replace the current workspace,
  silently delete objects to free capacity, or call a partially completed sequence
  of small operations an atomic whole-pack install.

Broader Cosmos assets, programs, lessons and region residency are not included in
this first descriptor subset. Explicit conflict-aware import/copy/merge is still
needed for upgrades that preserve user edits. Later previews need honest measured
native/render/physics/audio costs in addition to structural admission counts.

Current first-run `StarterDocument` includes the book, Maestro and three ordinary
practice objects. Explicit fresh recovery intentionally creates only the book and
Maestro plus baseline documents/included content. Neither is changed by this pack.
Choosing a themed new world is a separate source choice, not a change to what
“recover fresh” means. Corrupt/newer/missing-pointer stores remain protected.

## Interaction, lessons and larger scope

The initial human surface is the schema-generated book catalog. The next planned,
unimplemented increment is a learner-facing **Worlds** chooser in the familiar
Book, paired with natural-language selection: choose, preview, open, edit together
and return. It must use the exact native catalog and preparation/activation/review
workflow above, retaining identities internally rather than asking learners to copy
IDs or hashes. Preserve the conversation, active languages and presentation choices.

The [current probe](../../../scripts/probe-world-pack.ts) explicitly reconciles an
expected new session. The application still needs a shared transition presenter:
the [native lease](../../../src/core-sdk/room/roomAgentClient.ts) and
[chat task scope](../../../src/features/chat/services/roomAgentTasks.ts) correctly
invalidate on workspace replacement. Keep those protections. Retain the exact
acknowledged activation request, origin revision and prepared generation/hash;
observe the new ready session and reconcile that same native outcome read-only.
Do not replay activation, broadly weaken leases or silently authorize subsequent
edits in the new world. Fresh scoped work may continue after reconciliation;
unknown outcomes remain explicit, and Stop after commit must report what committed.
The chooser and chat should present the same progress, cancellation, review and
previous-world return states without becoming another persistence owner.

Before gaze automation, add an explicit **Ask Maestro about this** entry for a
selected exhibit, carrying its actual object identity/revision into the original
chat path. This is planned interaction, not a separate lesson player: use the
learner's current conversation/languages and existing speech route. A bounded
content revision may then add ordinary editable exhibits and opt-in shared ambience;
it must not silently replace or reinstall a user's accepted world.

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

A complete integrated observatory still needs verified reachable routes, ambient
sources and an explicit request-to-learn interaction beyond the current editable
deck/bridge/globe entry. Continue the rest of the proposed content through the same path.
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

Focused and full desktop Unity Headless and rendered Book journeys have exercised
the shared source through preparation, cancellation, opening, activity review,
a mapped object edit, Undo and restoration of the original world. They verify the
actual included avatar/motion payloads and provenance identities. The [current status](../../QUEST_STATUS.md#latest-verified-proof)
and [dated checkpoint](../checkpoints/2026-11.md#included-editable-world-lifecycle--2026-11-14)
record the exact source boundaries and verified unsigned Android compilation. The Book
visual review covers advanced catalog pages, not a 3D world scene or novice
chooser. Desktop execution does not establish Quest optics, comfort, sustained
resource budgets or real-provider lesson reliability.

Private source-review reports are under
`.quest-evidence/default-world-review-20261114/`; they are not public artifacts or
runtime verification. The proposed Cosmos runtime remains unmerged. Keep broader
integration and physical acceptance open until their own evidence is complete;
these passing workflows do not establish the full Cosmos or living-world experience.
