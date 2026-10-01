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

Inspect `avatar.model` before selecting. Its fields distinguish the saved
`selectedHash` from `displayedHash`, plus object revision, loading/ready/unavailable
phase, bounded status, temporary-room state and loaded-rig posing support.
`canPose` describes the loaded rig; it does not grant authoring ownership. Empty hash
means included Maestro. A failed new selection preserves the old saved identity;
its status explains the failure. If an already saved custom model becomes missing
on a later launch, the existing restoration path can display included Maestro and
the fact reports the saved/displayed mismatch.

`avatar.model.select {target: "maestro", modelHash, revision}` requires the exact
observed revision. Empty `modelHash` chooses included Maestro. It owns Maestro's
whole target while preparing, and completes only after the prepared model is
saved and displayed. Other actors must release Maestro first. Physical selection
has trusted manual priority and can interrupt an existing actor; public arguments
cannot request that priority. Unrelated object actors remain running.

## Commit, cancellation and lifecycle

A hidden candidate is prepared while the old avatar remains visible. Once ready,
the runtime rechecks the object revision, room session and lifecycle state, saves
the new record, and applies the candidate. One successful selection has one Undo.
Save/preparation failure keeps the old model, saved record and revision. The
current workspace keeps its file-use lease until pending native work drains,
including after cancellation. Stop during preparation prevents a late commit.
After commit, Stop cannot undo it; use Undo or another explicit selection.

Temporary-room selection changes only the fork until Keep. Saving retains the
canonical pose and recorded motions. A changed model resets its embedded walking
clip index; exact saved library walking/activity choices remain unchanged and can
be unavailable on an incompatible rig. They are never silently substituted.
Selection does not request a clip or recording. Normal configured tutor activity
may resume when the model becomes ready. Old receipts never replay a selection.

The physical switch finishes live recording first. If that recording fails to
save, the retained take must be saved or discarded before switching models.
Manual pause and controller takeover cancel pending selection through the same
ownership service. Restoration, including Undo/Redo, invalidates a superseded
candidate so it cannot overwrite the restored selection.

## Verification and remaining coverage

Native checks cover GLB and VRM retargeting, library discovery, failed/corrupt/
non-humanoid loads, storage failure and retry, cancellation/pause, exact revisions,
actor conflicts, unrelated actors, Undo/Redo, saved poses, temporary Keep and
failed recording protection. `test-fixtures/browser/avatarSelection.json` captures
native discovery/selection receipts and before/loading/ready facts.
`scripts/probe-avatar-selection.mjs` discovers the captured model in the book,
fills its exact hash/revision using typed fields and inspects the ready identity.
The browser uses captured native acknowledgements; it does not import a file,
contact a provider or test a headset.

New files use the shared picker/preview/acceptance flow in QUEST_MODEL_IMPORT.md.
Shared live posing is documented in QUEST_ANIMATION_AUTHORING.md. Real-provider
selection requests and Quest memory, performance and lifecycle acceptance with
users' large models remain release gates.
