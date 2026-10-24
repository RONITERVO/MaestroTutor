# Animation collection import: development checkpoint

The physical import tray has a **Batch files** side tab for selecting and saving
animation collections. It changes the tray's existing twelve controls and leaves
chat pages untouched. This is a desktop/native-tested development feature;
Quest picker and large-collection headset acceptance are still pending.

## Workflow

1. Open **Batch files**, then **Choose files**. On Quest, use the Android document
   picker to select 1–128 GLB/VRM exports. The picker starts at Downloads. Long
   press/multiselect availability depends on the installed document provider.
   In the Unity Editor, choose a folder; only its direct GLB/VRM files are listed.
   More than 128 files is rejected explicitly, never silently truncated.
2. Optionally cycle **Category** before saving: none, idle, listening, thinking,
   speaking, walking, gesture, action or dance. Custom names/tags can be edited
   later in the book's Library. Original folder names are not inferred on Quest.
3. **Save batch** confirms that the selected assets may be used and begins work.
   Selection alone opens no file stream, creates no model and saves no motion.
   The display reports the current file, completed/failed/waiting totals, and
   per-file outcomes. **Prev result**, **Next result** and **More info** let users
   review results and longer error details.
4. **Stop batch** cancels a pending provider read. An atomic per-file save that
   already started finishes; remaining files wait. **Resume** continues waiting
   files. **Retry failed** retries failures and continues waiting files without
   rereading successful files. It uses the same selected documents; if an export
   is replaced with a new document, choose the files again.
5. Open **Library** to browse, rename, tag, preview or assign motions. Importing
   never plays an animation, changes Maestro or assigns a rule/tutor state.
   **Models** returns to the ordinary one-model preview/import controls.

**Clear batch** discards only the selection/results after stopping. Previously
saved motions and original exports remain. Choosing new files replaces the
previous finished or stopped selection. Results and document grants are not a
persistent background queue. After an app restart, choose the exports again;
completed exact motions reuse their existing identity and saved metadata.
Pausing, loss of focus or disabling the workshop stops work; returning does not
automatically resume. Selection itself survives the normal external picker focus
transition, with a five-minute selection timeout.

## Bounds and data ownership

Files are read sequentially. The Android fragment retains a bounded list of
user-selected document URIs, then copies only the requested document to its
private cache. Each source is limited to 64 MiB and is released before another
is opened. Actual bytes are bounded even when a provider lies about its length.
Provider reads have a two-minute timeout and a cancellation signal. Requests and
cleanup carry a selection-session identity and increasing request number, so
stale results/acknowledgements cannot overwrite or remove a newer selection.

The batch uses `MotionLibrary.ImportAsync` directly: no hidden meshes, textures
or compiled animation clips are loaded for each source. Motion extraction and
catalogue writes remain serialized. Same motion payloads deduplicate, including
helper clips; their stable IDs keep existing rule/profile assignments intact.
Different payloads get new IDs. Reimport restores an archived/removed motion
unless its metadata was deliberately forgotten. Names, favourites, source terms
and user tags are preserved; an explicitly chosen batch category is appended.

A failed source has its own outcome and does not undo earlier successes or
prevent later files from being tried. Disk/catalogue capacity failures remain
visible. Per-file atomic catalogue persistence and exact reimport handle an
interrupted app; the entire collection is not one all-or-nothing transaction.

Existing source and motion validation still apply: self-contained supported
GLB/VRM data, 32 clips per source, 32 MiB extracted per source, 8 MiB per motion,
128 MiB local motion payloads, and 1,024 catalogue entries/source records. The
same-rig requirement is unchanged. This feature does not add cross-rig
retargeting, compressed archives or portable pack manifests. A batch can contain
motions for several rigs; the Library's compatibility filter controls playback.

No new directory-wide Android storage permission or persistent document grant
is requested. Only external content-provider documents are accepted; private,
network and app-owned provider sources are rejected. Original files are never
changed. Nothing is exposed to page JavaScript. The separate web file-input
limits (eight files, 128 MiB/session) and single-model picker are unchanged.

## Verification and remaining acceptance

Core tests exercise mixed success/failure, duplicate identity, exact retries,
source-byte preservation, sequential source release, oversize rejection,
stop during read/save, category retention and restart-visible saved motions.
Unity PlayMode tests operate the batch tray commands, save without creating
models/resident clips, repair a failed export, cancel a blocked read on pause,
require explicit resume, and capture failure/success tray renders.
Android tests exercise read-only multiselect up to 128, selection without reads,
private-copy cleanup, invalid sources, retry, malformed results, cancellation,
stale session/request isolation and the unchanged single-file intent.

See QUEST_DEVICE_QA.md for the exact packaged checkpoint and test counts.
Before release, exercise Quest document multiselect (including providers without
multiselect), stop/resume while copying, loss of storage permission, low space,
process restart/reselection, a real large Meshy collection, readable hand/controller
controls, and sustained headset memory/thermal performance. These desktop bounds
are not measurements of Quest peak memory. No new headset acceptance is claimed.
