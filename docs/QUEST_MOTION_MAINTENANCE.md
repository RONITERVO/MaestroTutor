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
the Unity thread. A paused/closed/replaced book session cannot initiate a stale
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

The browser fixture uses actual synthetic native response data and simulated
acknowledgements. Its screenshots check reference explanations, protected
controls, the confirmation and page bounds; it does not establish Quest WebView
input or headset readability. See QUEST_DEVICE_QA.md for the final verified APK.

On Quest, test archive while a rule plays, restore, replace assignments, remove
an unused download, restart, reimport the original, and forget an unused removed
entry. Check hand/controller scrolling, keyboard/search and responsiveness with
hundreds of motions. Hardware performance, bulk-pack import, general cross-rig
retargeting and full release acceptance remain open.
