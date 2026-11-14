# Shared avatar selection

The agent, event programs and optional typed book controls use `avatar.model.select`
to choose the same Maestro model as physical Use Maestro and Default. This uses
the existing private GLB/VRM library, humanoid validation and canonical retargeting.
It adds no model-generation provider or model API connection.

## Discover and select

`model.library.inspect {offset}` returns up to eight entries sorted by exact
content hash, with the total count, safe display names and byte sizes. The library
remains bounded to 32 files / 256 MB. No filesystem paths leave native code.
Discovery reads metadata; it does not guarantee that a file is intact or humanoid.
Selection re-reads and hashes the chosen bytes, checks the import budgets, loads
its rig and prepares the retargeter before accepting a change. Files with missing,
damaged or incompatible content leave the existing selection and display intact.

Inspect `avatar.model` before selecting. Its fields distinguish the live journal
`selectedHash` from `displayedHash`, plus object revision, loading/ready/unavailable
phase, bounded status, temporary-room state and loaded-rig posing support.
`canPose` describes the loaded rig; it does not grant authoring ownership. Empty saved/displayed hash
means the original sketch fallback; bundled avatars have exact hashes. Queueing,
loading and publication all report busy. A rejected preparation preserves the
old saved identity; its status explains the failure. Unconfirmed publication
reports unavailable and blocks saving: disk may differ from both live identities.
Matching hashes alone are not independent verification of the saved file. If an already saved custom model becomes missing
on a later launch, the existing restoration path can display included Maestro and
the fact reports the saved/displayed mismatch.

`avatar.model.select {target: "maestro", modelHash, revision}` requires the exact
observed revision. Empty `modelHash` chooses the current included Maestro, resolved once to the hash
reported by `avatar.included`. The resolved identity is saved and returned; see
QUEST_INCLUDED_AVATAR.md. It owns Maestro's
whole target while preparing, and completes only after the prepared model is
saved and displayed. Other actors must release Maestro first. Physical selection
has trusted manual priority and can interrupt an existing actor; public arguments
cannot request that priority. Unrelated object actors remain running.

## Commit, cancellation and lifecycle

A hidden candidate is prepared while the old avatar remains visible. Physical
buttons, imported previews and catalog actions use the same ordered completion.
After loading, the runtime rechecks the exact target revision, room session and
lifecycle state, then prepares the detached save on a worker. A brief journal
reservation protects publication and native application from competing authored
edits. Runtime placement, viewpoint and clock progress may continue; it is
preserved when applying the content-only selection.

Publication is authorized only after preflight. Before authorization, cancellation
prevents a late save. Once authorized, Stop or pause cannot retract publication:
the accepted work drains, and successful completion means saved and displayed.
One changed selection contributes one Undo; a no-op retains the existing revision.
The completion receipt captures the accepted revision before change observers can
make later edits. An imported preview also keeps that same immutable receipt and
cannot steal a newer physical selection.

Preparation or pre-publication rejection keeps the prior journal, file and display.
A failure after publication starts cannot promise that the old file survives:
the live avatar is retained, uncertainty is reported, and further saves require
inspection/recovery. Failed display after a confirmed save also requires recovery.
Neither failure automatically retries. Workspace ownership remains held until
all worker and candidate cleanup drains.

Lifecycle checkpoints wait on pure worker futures, never a Unity main-thread
continuation. If publication is accepted but the live journal is still old, the
checkpoint carries the accepted avatar identity so shutdown cannot overwrite it
with the old selection. Destruction drains the accepted publication and final
checkpoint; reopening uses the saved identity.
Hidden candidates that never became active require explicit disposal even after
Unity destroys their parent. Cleanup checks CLR ownership rather than Unity's
destroyed-object null value, so their native reservations cannot leak into later
imports.

Temporary-room selection changes only the fork until Keep. Saving retains the
canonical pose and recorded motions. A changed model resets its embedded walking
clip index (the bundled default uses its declared gait); exact saved library walking/activity choices remain unchanged and can
be unavailable on an incompatible rig. They are never silently substituted.
Selection does not request a clip or recording. Normal configured tutor activity
may resume when the model becomes ready. Old receipts never replay a selection.

The physical switch takes over only Maestro. Unrelated recordings and programs
continue. If finishing Maestro’s own recording fails to
save, the retained take must be saved or discarded before switching models.
Manual pause and controller takeover cancel unaccepted selection through the same
ownership service; accepted publication still drains. Restoration, including Undo/Redo, invalidates a superseded
candidate so it cannot overwrite the restored selection.

## Verification and remaining coverage

Native checks cover GLB and VRM retargeting, library discovery, failed/corrupt/
non-humanoid loads, storage failure and retry, cancellation/pause, exact revisions,
actor conflicts, unrelated actors, Undo/Redo, saved poses, temporary Keep and
failed recording protection, queued physical completion, delayed background
publication, immutable receipts, exact-target takeover, newer selection retention,
publication uncertainty, no-op revision, and accepted Stop/pause/destruction. `test-fixtures/browser/avatarSelection.json` captures
native discovery/selection receipts and before/loading/ready facts.
`scripts/probe-avatar-selection.mjs` discovers the captured model in the book,
fills its exact hash/revision using typed fields and inspects the ready identity.
The browser uses captured native acknowledgements; it does not import a file,
contact a provider or test a headset.

New files use the shared picker/preview/acceptance flow in QUEST_MODEL_IMPORT.md.
Import acceptance subscribes to runtime interruption before its first asynchronous
yield, so a brief hold during file copying cannot be missed before native loading.
Shared live posing is documented in QUEST_ANIMATION_AUTHORING.md. Real-provider
selection requests and Quest memory, performance and lifecycle acceptance with
users' large models remain release gates.
