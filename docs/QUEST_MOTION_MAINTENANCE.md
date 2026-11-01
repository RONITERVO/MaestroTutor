# Motion library maintenance: development checkpoint

The book's animation library supports archiving, reference inspection, protected
removal of local downloads and optional forgetting of unused removed metadata.
These operations apply only to the app's private reusable-motion library. Original
GLB/VRM source files and full imported avatar models are unaffected.

## User workflow

Open Library on the physical import tray and select a saved motion. Expand
**Usage and local storage** on the detail page. It lists current walking, visual
rule steps and tutor-state assignments, including profiles for other avatars.
Long usage lists are paginated. Undo/Redo, retained saves/backups and active
playback are shown separately when they protect the download.

**Archive motion** hides a motion from the main list while keeping every existing
assignment and playback working. The **Archived** filter shows archived and
removed entries; the other search, rig, favourite and short-clip filters still
apply. **Restore to library** reverses archive without starting playback.

To replace a current assignment, select the replacement and use **Use for
walking**, **Use in selected action**, or **Tutor-state motions**. Choose the
relevant action on the physical rules tray first; switch avatars to edit another
avatar's state profile. These existing assignment controls preserve rule targets
and triggers. This is explicit reassignment, not automatic global retargeting.

An archived motion can have its **local download removed** when no current
assignment, Undo/Redo, retained save or active lease needs it. The book asks for
confirmation and checks references again before deleting the private payload.
Metadata and the stable identity remain. Use Import on the physical tray, select
the original export, then Save motions to restore the same motion identity,
name, tags and favourites. A changed motion receives a different identity.
A lost download is visibly unavailable and can also be repaired or removed.

**Forget motion details** is a separate confirmed action, available only after
download removal and with the same reference protection. It reclaims catalogue
slots and prunes source records that no remaining motion uses. A later import
creates a new identity; this action deliberately gives up the previous name,
tags and identity. It does not remove the user's original source file. This
step is optional: retaining removed metadata keeps exact reimport recovery easy.

## Shared user and agent maintenance

With `motionLibraryMaintenance.v1`, the dedicated library controls, generated
book forms and in-app agent use the same `motion.library.edit` action and native
execution receipts. The action has five explicit operations: `save`, `archive`,
`restore`, `removeDownload` and `forgetMotion`. Download removal and forgetting
retain the manual confirmation in both book interfaces. Saving ordinary metadata
or changing archive visibility does not add that extra confirmation.

`motion.library.search` finds exact IDs, including archived entries when selected.
Read `motion.library.entry`, then the exact `motion.library.name` and every page
of `motion.library.tags` before preserving existing metadata. The entry revision
covers all saved fields and is stable across restart. A stale draft is rejected;
no request chooses another motion by its name or tag. Valid names and tags retain
their exact spelling, spaces and punctuation when an unrelated flag changes.

`motion.library.inspect` explicitly audits assignments, history, retained saves,
modules, remembered values and loading/playback. Passive facts do not perform
that file audit. Inspection is a current observation, not permission for a later
removal. Removal checks again, freezes competing authored writes and retains its
workspace until accepted I/O drains. No Undo is provided for private-library
maintenance, even inside a temporary room; the world itself is unaffected.

Stopping before storage dispatch cancels the edit. A dispatched save or deletion
may finish after Stop, so the book keeps its receipt identity and reports that
uncertainty. Inspect current state and the retained receipt before an explicit
retry. A historical receipt never repeats an effect. Metadata publication failure
preserves payloads and makes the library read-only until restart/recovery can
inspect the saved state.

## Persistence and ownership

The catalogue now writes `room/motions/motions.v2.json`. Valid v1 catalogues load
with their identities and metadata intact; first write creates v2 and leaves
v1 originals unchanged. Once v2 or its backup exists, an older catalogue cannot
silently replace it. Unknown versions are protected from writes. Existing motion
payload format and rig-compatibility requirements are unchanged.

Removal serializes against imports and metadata writes. A pending removal
reserves its identity so a new assignment or lease cannot start during the
reference check. Loading/active leases block removal; inactive compiled clips
are evicted before their payload is deleted. A rejected check releases the
reservation and keeps the motion available. The catalogue records removal
before bytes are deleted; an interrupted/failed delete can be retried. Source
files are never deleted. A metadata backup may reference a lost payload; exact
reimport repairs that payload while keeping its recovered identity.

Current references and in-memory room/rule/profile history are checked, along
with supported saved-file families and their backup, pending and retained
unreadable copies. Incompatible/corrupt retained contents block removal because
the app cannot prove they are unused. It does not silently discard room recovery
files to make space. Recovery management for such files remains a release gate.

Retained-file inspection runs off the Unity thread. Its cache keeps small sets
of motion IDs keyed by file metadata, not copies of whole room recordings.
Destructive operations force a fresh scan and then check current references on
the Unity thread. The forced scan waits behind an existing snapshot reader on its
worker; Stop cancels admission before reading or publishing. It does not recover
or rewrite saved files. Unreadable evidence still blocks removal. A paused/closed/replaced book session cannot initiate a stale
removal. An already committed atomic disk mutation may finish; it never starts
playback. Archive itself does not stop a playing animation.

The existing 128 MiB payload and 1,024-entry/source limits remain. Archived
downloads still occupy disk. Removed entries still occupy metadata slots until
explicitly forgotten. Referenced motions may remain protected until old history
or recovery saves no longer reference them. There is no automatic eviction of
user downloads, no cloud deletion and no server dependency.

## Verification and remaining acceptance

Storage tests cover v1/v2 migration, unknown versions during mutation, archive
and restore, reference rejection, exact reimport identity, source-byte
preservation and catalogue/source-slot reclamation. Runtime tests cover cold
loads and active leases, reservation rollback, continuing playback after archive,
current/history/save protection, actual book requests, missing payloads and
restoration without autoplay. Web tests cover bounded dependency data, separate
confirmations and discarding confirmation when selection/session changes.

The separate library browser fixture uses synthetic native response data and
simulated acknowledgements. Its screenshots check reference explanations, protected
controls, the confirmation and page bounds; it does not establish Quest WebView
input or headset readability. See QUEST_DEVICE_QA.md for the final verified APK.

The ordinary offline Headless and visible Book journeys also exercise the actual
native library through the shared catalog. They preserve exact paged metadata,
refuse stale edits, remove/restore an unused packaged download, forget/add its
packaged identity and restore the exact starting revision and package counts.
The headless path also checks historical-receipt replay without repeating effects.
Rendered fact readbacks are inspected separately from the synthetic library UI
fixture. See [native probe](QUEST_NATIVE_ROOM_PROBE.md) for commands and evidence
boundaries; this packaged-content round trip does not certify arbitrary source
reimport, provider judgment or physical input.

On Quest, test archive while a rule plays, restore, replace assignments, remove
an unused download, restart, reimport the original, and forget an unused removed
entry. Check hand/controller scrolling, keyboard/search and responsiveness with
hundreds of motions. Hardware performance, bulk-pack import, general cross-rig
retargeting and full release acceptance remain open.
