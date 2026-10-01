# Shared model import

`model.import` connects the physical import tray, the schema-generated book fields
and the delegated room agent to one native `ImportWorkshop`. It uses the existing
GLB/VRM inspection, rendering, model library, humanoid selection and motion library;
there is no second provider connection or agent filesystem access.

## Selection and preview

`operation: select` opens the system document picker for one self-contained GLB or
VRM, up to 64 MB. Its completed receipt acknowledges a native request ID, not an
imported model. The user must select the file in the system picker. Selection
survives its normal focus/pause transition; paused actions never automatically
resume. `model.import.selection` is a read-only fact with the current request ID,
phase, bounded preview metadata and a summary of the last accepted result. No source path leaves
native code. Model names and metadata are untrusted content, never instructions.

The Android model and workspace-archive facades use one `DocumentPicker` owner.
Only the correct request ID and file kind can inspect or release its private copy.
A retiring copy worker blocks another chooser until it drains. Model files retain
the 64 MB limit; workspace archives retain the 512 MB limit. The selected copy is
validated as a file inside the picker cache and is released after model reading
and loading have finished. Late callbacks cannot attach to a new request.

Phases distinguish selecting, copying, checking and preview. A preview renders
through the same style and import budgets as the physical tools, without adding
an object, replacing Maestro or playing an animation. The preview holds a workspace
write lease until acceptance or cancellation, so a workspace switch cannot silently
lose it. Existing saved content can still be edited; acceptance rechecks the affected
object and current interaction state.

## Accept one explicit destination

Every acceptance supplies the observed `requestId` and exact `modelHash`:

- `object` saves the local asset and adds one room object through the same durable
  creation transaction as other creations, with one Undo. Its returned `objectId`
  grants the same program authority for subsequent edits as other creation results.
  The per-run creation budget counts object placement, not file selection, cancel,
  avatar selection or library saves.
- `maestro` additionally supplies `target: maestro` and the current avatar `revision`.
  It checks humanoid compatibility, takes normal program ownership for agent calls,
  prepares the avatar and uses the existing save/apply transaction. Other live actors
  cannot be interrupted by an agent import. The physical button retains manual priority.
  Existing saved poses, motion choices and activity profiles remain; configured tutor
  activity can resume once the model is ready.
- `library` saves only the verified local model; it does not place or select anything.
- `motions` extracts embedded animations into the existing motion library without
  saving another model. The completed action result contains every exact motion ID. It neither assigns
  those motions to a behaviour nor starts playback.

Physical Add model, Use Maestro and Save motions use this same acceptance path when
a preview is present. Existing selections and larger animation batches retain their
established flows. New authoring, held objects and unfinished strokes are checked
before placement. Failed saves retain a usable preview for explicit retry or cancel.
An accepted object/avatar edit in a temporary room stays in the fork until Keep;
private model/motion library writes are saved immediately.

## Read all imported motion identities

The selection fact reports `accepted.motionCount`; it does not embed the full ID
list. `model.import.motions` takes that exact `requestId` and a zero-based
`motionOffset`, returning up to eight exact IDs with the total count and model
hash. Offsets 0, 8, 16 and 24 cover a maximum-size 32-motion export. The complete
action outcome still retains all IDs. An accepted non-motion destination returns
an empty page. Before acceptance or after a newer selection, old request IDs are
unavailable. Reading never imports, assigns, substitutes or plays a motion.

Display names and errors use a 128-character serialized-JSON text budget, including
quotes and escapes. Inspection scans at most 128 UTF-16 positions, preserves whole
surrogate pairs, removes markup delimiters and replaces control characters with
spaces. This bounds even separator-heavy or malformed metadata. It changes only
observation text: original files, saved asset names and exact request/model/motion
identities remain intact. The shared 1,024-character program-value budget remains
unchanged.

## Cancellation, partial outcomes and limits

`cancel` requires the current request ID and clears an unused selection or preview.
It never deletes the source file or reverses a completed acceptance. Stop on a
running acceptance prevents a later placement/avatar commit when cancellation is
observed before commit. Already accepted library writes drain; they may complete
after the action receipt was cancelled. Inspect the selection fact and library
before retrying. A model copy may remain if a later placement or avatar save fails.
These are private, content-addressed copies, not duplicate room objects.

Only the latest import session is retained in memory. Restart never reopens a picker
or replays an uncertain acceptance. Saved objects and libraries use the existing
persistence and receipt rules. The preview and any unsaved selection are lost on
process termination. Large animation batches now share native sessions and paged results; see
QUEST_MOTION_BATCH_IMPORT.md. Real-provider and headset picker, lifecycle and
memory acceptance remain release gates.

## Verification

Native journeys exercise the actual GLB/VRM loader, model and motion libraries,
physical acceptance methods and execution receipts. They cover stale identities,
duplicate requests, invalid paths, cancellation, pause, failed-save retry, humanoid
selection, ownership, durable Undo, temporary Keep and exact motion IDs. A real
32-clip export verifies the full action outcome and all four typed fact pages;
escaped filenames and errors exercise the serialized budget. Browser tests read
the captured pages through generated inputs and clear stale results when the
offset changes. Android
unit tests exercise selected-provider copies and cross-kind ownership in the common
picker. Browser checks use captured native acknowledgements; they do not open the
headset picker or prove real-provider/headset acceptance.
