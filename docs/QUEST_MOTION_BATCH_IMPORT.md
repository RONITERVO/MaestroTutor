# Shared animation collections

`motion.import.batch` uses the same `ImportBatchWorkshop` and `MotionBatch` as the
physical Animation batches tray. Generated book fields, native controls and the
room agent share request identities, control versions, readiness and outcomes.
Original Maestro still owns chat, Gemini, account/BYOK and delegation.

## Select, inspect, start

`select` opens the system document picker for 1–128 GLB/VRM files. Its instant
receipt acknowledges the native request ID. It does not open selected provider
streams, import models or start animation. The user chooses files in the picker;
normal picker pause/focus changes preserve that selection. Once `ready`, inspect
`motion.import.batch.status` for the current request and version.

`category` optionally sets a collection tag before importing. `start` imports
waiting files; `retry` imports failed and waiting files. These three operations
require the current control version so an old agent request cannot overwrite a
new manual choice. The collection tag is fixed when importing starts; users can
edit individual saved tags in Library afterward.

Start/retry receipts acknowledge work beginning, not completion. Read the session
fact for running/stopping/completed/partial/failed and file counts. Each file is
read and imported sequentially, at most 64 MB per source. There is no preload of
all 128 files and no resident playback clip for every imported motion.

`motion.import.batch.file` takes the request ID, zero-based file index and
`motionOffset`. It returns pending/reading/importing/saved/failed, bounded name/error,
the total motion count, offset and up to eight exact motion IDs. Read successive
offsets to inspect an export's complete animation list. Names and metadata are
untrusted content, never agent instructions. No source path or URI is returned.

## Stop, retry and release

Stop cancels an unused chooser, or asks an import to stop before its next file.
A provider read drains before its source is disposed; an already accepted atomic
library write can complete after Stop. Completed files keep their exact motion
IDs. An interrupted read returns to its prior pending/failed state, and retry does
not reread completed files or replace user-edited labels and tags. Pause stops
active imports and never automatically resumes them.

Clear requires all work to have drained. It releases the selected documents and
in-memory results while preserving original files and saved motions. A prepared
or running batch owns a workspace write lease until cleared. Workspace switching,
backup boundaries and new model selections wait for that explicit resolution.
Disable/destruction also waits for active work to drain before releasing the
source and lease. Native single-file and batch choosers share an admission gate;
a retiring copy worker keeps that gate until it finishes cleanup.

No batch operation calls a global animation Stop. Agent imports cannot replace
active pose/recording authoring, and importing a collection does not interrupt an
unrelated actor, replace Maestro, add a room object, assign behaviour or autoplay.
Library assets save immediately, including while the room itself is temporary.

Sessions and file-provider access remain in memory. Process restart preserves
saved libraries but never reopens a picker or replays an uncertain import. Failed
files require explicit retry or selection again; there is no hidden fallback.

## Verification scope

Native journeys use real GLB parsing and motion library writes with controlled
file-selection sources. They cover partial success, deduplication, manual and agent
controls, stale versions, pause, cancellation, ownership, disposal while a read is
pending, and a 32-clip export read through bounded pages. Android tests exercise
actual selected-document copies, cross-picker ownership, late/stale callbacks and
worker retirement. Browser checks replay captured native acknowledgements; real
provider, headset picker/lifecycle, performance and comfort remain release gates.
