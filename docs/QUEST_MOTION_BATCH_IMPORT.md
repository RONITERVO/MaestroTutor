# Shared animation collections

`motion.import.batch` uses the same `ImportBatchWorkshop` and `MotionBatch` as the
physical Animation batches tray. Generated book fields, native controls and the
room agent share request identities, control versions, readiness and outcomes.
Original Maestro still owns chat, Gemini, account/BYOK and delegation.

## Select, inspect, start

`select` opens the system document picker for 1–128 GLB/VRM files **or one ZIP**.
Its instant receipt acknowledges the native request ID. After the user chooses,
plain selections read provider metadata only; ZIP selection makes a private,
bounded copy and lists its GLB/VRM members. No model is unpacked, imported or
played before `start`. The session remains `selecting` while preparing the list.
The user chooses files in the picker;
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
the collection and no resident playback clip for every imported motion.

`motion.import.batch.file` takes the request ID, zero-based file index and
`motionOffset`. It returns pending/reading/importing/saved/failed, bounded name/error,
the total motion count, offset and up to eight exact motion IDs. Read successive
offsets to inspect an export's complete animation list. Names and metadata are
untrusted content, never agent instructions. No source path or URI is returned.

Display names and errors share the single-file import's serialized text budget:
128 characters including JSON quotes and escapes, with bounded scanning and whole
surrogate pairs. Exact motion/request IDs are never shortened. Collection tags
retain their existing input normalization (including trimming surrounding
whitespace) and are not truncated for display. The global program-value budget
remains unchanged. Native cases cover separator-heavy filenames and failures
without losing readable result pages or granting motion IDs to failed files.

## ZIP collections

The Android picker accepts one standard ZIP up to **2 GiB**, with up to **1,024**
self-contained GLB/VRM members (64 MiB each). Subfolders are supported; nested
ZIPs, encrypted/split/ZIP64 archives and combining ZIPs with other selections
are rejected with a readable error. Non-model files are ignored. A bounded
central-directory preflight permits at most 4,096 entries, 4 MiB of directory
metadata and 4 GiB of declared expanded data before opening the ZIP index.
Duplicate or unsafe entry names are rejected. Names are display metadata only;
no archive-supplied name becomes a filesystem destination.

Only one member is unpacked at a time into an opaque private cache file. Actual
length and CRC must match before Unity can read it. Existing model validation,
rig identity and motion extraction then run normally. The compressed archive
remains until Clear, so retry uses exactly the selected bytes even if the source
provider changes. Cache copies are removed after draining; original files are
never changed. Peak temporary disk use is the ZIP plus one member (up to 64 MiB),
separate from the library's existing 128 MiB / 1,024-motion budget. No automatic
eviction occurs when that budget is reached. Several exports may add to the
same library; they do not need to fit into one Meshy download.

The batch is for **motion extraction**. Choosing the avatar itself still uses
single-model preview and acceptance with a GLB/VRM. ZIP member browsing for model
preview is separate remaining work. Imported motions never silently retarget an
old rig to a changed character. Matching bone names alone are insufficient.

Native/book/agent file facts cover indices 0–1,023 with bounded, per-file reads;
reading one file no longer clones the complete results array. The Unity Editor's
local developer picker continues to select a folder of exports.

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
worker retirement, ZIP enumeration, a 300-member collection, CRC/size/path rejection,
metadata bounds and cancellation during preparation. An opt-in Android test can
check all bytes of a user-selected ZIP against expected SHA-256 hashes using
`MAESTRO_TEST_MOTION_ZIP` and `MAESTRO_TEST_MOTION_HASHES`; inputs remain private. Browser checks replay captured native acknowledgements; real
provider, headset picker/lifecycle, performance and comfort remain release gates.
