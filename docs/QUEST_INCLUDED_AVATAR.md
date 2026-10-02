# Included Maestro avatar and offline identity

The current default is the user-selected Meshy Azure Violet Doll export from
`Meshy_AI_Azure_Violet_Doll_biped.zip`, specifically the Walking GLB. The binary
is preserved unchanged, SHA-256
`dcd59fb02d378322b656c2cfbe9851d08229ac3a618e90ad5aa06eac7c88759c`.
It contains 68 skin joints, 15,283 vertices, 15,390 triangles, three embedded
textures (12,582,912 pixels total) and one Walking clip. The runtime maps the
humanoid to the existing 17 canonical pose controls while imported clips retain
their full skeleton channels. The repeated full-model copies are not also bundled. The separate included
animation collection currently contains 178 unique motion payloads from the later
180-export collection; it adds no duplicate avatar meshes or textures.

The project owner confirmed this model was generated under a **paid Meshy plan**
on 2026-10-02 and supplied it as the included default. Source identity and this
confirmation are recorded in `IncludedMaestro.provenance.json`; the package
manifest and portable model sidecar carry attribution. Meshy's [ownership
guidance](https://help.meshy.ai/en/articles/10137554-what-is-the-ownership-of-the-generated-models)
distinguishes paid-plan ownership from free-plan attribution terms. This record
does not independently verify the account or third-party source-image rights.

## Loading and selection

- `Resources/Avatars/IncludedMaestro.json` declares an exact hash, name, byte
  count, attribution and `walkClipIndex` (-1 for the canonical walk).
- `StreamingAssets/MaestroContent/IncludedMaestro.glb` is offline app content.
  Desktop reads a file; Android reads the fixed entry directly from the APK ZIP.
  Neither needs a provider, external storage access, URL or extra API key.
- Bytes are bounded, hashed and preflighted on a worker before using the existing
  model importer and humanoid retargeter. First use copies the exact binary into
  the workspace's ordinary model library. This consumes one of its 32 model
  slots and counts toward its 256 MB limit. It cannot evict another model.
- New rooms save the exact default hash and declared embedded walk. Already
  saved rooms keep their own selected hash, including the original sketch when
  its saved hash is empty. An app artwork update never changes a saved choice.
- Physical Default and `avatar.model.select` with an empty input resolve to the
  current bundled hash. The receipt returns the resolved hash. Preparing,
  committing, cancellation, ownership, temporary-room Keep and Undo/Redo use
  the existing shared selection path. This is an alias for an explicit selection,
  not a mutable reference stored in room data.
- `avatar.included` exposes package metadata and the declared walk index.
  `avatar.model` separately reports selected and actually displayed identity.
  Missing nonpackaged or damaged local copies remain visible as unavailable
  selections. A missing copy of the exact current bundle can be restored from
  the APK; damaged copies are not silently repaired. A failed new selection preserves
  the previous model. The original sketch skeleton stays packaged as canonical
  animation driver and fallback geometry.
- Model loading never starts an embedded clip. This package explicitly declares
  Walking as its default gait for new rooms/model changes; walking/following
  must still be started. Exact saved library motion choices are retained and
  can be incompatible with a replacement rig.

The first-use copy acquires a workspace write lease before dispatch. Preservation
cannot race it, and retirement drains accepted copying before opening another
owner. Normal workspace archives include the materialized model and attribution.
Fresh recovery snapshots include the same exact model bytes before their preview
can be committed; recovery continues to require its normal explicit review.

## Larger animation collections

The earlier `D:/MeshyAnimatedMaestro` collection belongs to the **old rig** and
must not be silently assigned to this default. A shared character name or
Mixamo bone naming is not proof of exact compatibility: motion identity includes
hierarchy, rest transforms, inverse bind matrices, morph targets and axis.

Current GLB motion extraction accepts exports with multiple clips and separate
exports repeating the whole character. The reusable payload retains motion and
rig data, drops repeated meshes/textures, deduplicates exact motion payloads and
keeps stable saved motion IDs. The current library allows 1,024 entries within
128 MB; at most eight clips / 800,000 curve values reside in the playback cache.
These are enforced budgets, not a promise that every set of 1,024 motions fits.
Batch selection processes up to 128 individual files or one ZIP containing up to
1,024 models sequentially (see QUEST_MOTION_BATCH_IMPORT.md); each source must
fit 64 MB, each extracted motion 8 MB, and a source's extracted clips 32 MB.
The real three-file ZIP audit extracted three compatible motions: 350,732 bytes
from 27,761,900 source bytes, with one shared rig identity. This measurement is
for those files, not a fixed compression ratio for future collections.
Ten-clip exports are convenient when within those limits. Do not rerig between
exports intended for the same collection.

Model import now supports ZIP member search and explicit preview/acceptance;
see QUEST_MODEL_IMPORT.md.

The included motion manifest fixes exact content IDs, source identities and this
rig. Fresh libraries install the collection once; existing libraries add missing
content only through an explicit shared operation. Clips compile on demand. See
QUEST_ANIMATION_LIBRARY.md for add/restore, storage and recovery behavior.

**Remaining:** visual/semantic curation, default tutor-state assignments, original
versus navigation-adjusted motion fidelity, real multi-clip exports from the new
rig and Quest performance/comfort acceptance. Cross-rig
retargeting of imported motions is not implemented; canonical gestures/poses
already retarget through the existing humanoid path. Hundreds of old-rig motions
are not automatically converted or relabelled as compatible.

## Verification

Web CI checks manifest/provenance integrity and the exact self-contained GLB with
`npm run verify:quest-assets`. Editor configuration and Android build preprocessing load the packaged model,
validate the declared walk and exercise the real retargeter. Native tests cover
compressed APK reads, integrity/manifest rejection, exact copies and quota
refusal, retirement, portable fresh recovery, first load, app-artwork updates,
human/agent alias selection, duplicate receipts, saved poses, Undo/Redo,
cancellation and the shipped model's actual posing/walking path. Shared web
checks replay native facts and receipts through the existing generated book UI.
Desktop renders and native/Android builds are PC evidence; device acceptance is
still pending and this is not a Store-ready release.
