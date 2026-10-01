# Shared recipe editing

`object.recipe.edit` applies an exact-revision patch to an existing recipe object.
The optional book workshop projects its draft into this same invocation; agents,
programs and generated catalog fields use the native definition. The original
Maestro app still owns provider access. Editing does not recreate the object or
change its identity, placement, tint, physics settings or recorded root motion.

## Patches and playback

`parts` and `tracks` set complete entries by stable part ID. Unmentioned entries
remain. `removeParts` and `removeTracks` explicitly remove existing entries.
Duplicate IDs and set/remove overlap are invalid. Removing a parent never silently
removes children or animation: reparent/remove its children and remove its track
in the same patch. Native ordering puts parents before children and rejects cycles.
All geometric bounds, aggregate part limits and keyframe validation still apply.

`duration` retimes untouched tracks proportionally; supplied tracks use their
explicit times in the new duration. `loop` sets the playback preference. An edit
stops that object's recipe animation and clears saved autoplay. The workshop
states this before Apply. Start animation explicitly afterward. Undo restores the
previous saved recipe, including its autoplay preference; it is separate from
cancelling a completed action. Unrelated actors continue.

There are still at most 32 parts per object, 17 tracks and 16 keys per track, with
256 recipe parts across the room. Smaller explicit patches can reach the full
native capacity within the existing 24,000-character invocation limit. The book
sends only changed entries; excessive patches retain their draft with an error.
No payload is truncated. Part/track arrays remain literal program inputs.

## Readback and current values

- `object.recipe`: exact revision, counts, duration, loop, saved autoplay and live
  playback state. Current-value mappings copy revision/duration/loop only.
- `object.recipe.part`: one full part at an exact revision/index, including local
  position, dimensions, unit quaternion, opaque RGBA colour and stable parent.
  Root parents use empty text. The part is nested under `part` in the response.
- `object.recipe.track`: up to four exact rotation keys for a track index and
  offset at that same revision; includes stable part ID and full key count.
  Offset equal to the key count returns an empty page.

Missing targets, stale revisions and out-of-range indexes/offsets are unavailable.
Coordinates and rotations are not simplified. Query identity and native validation
remain authoritative; the existing shared value limits are unchanged.

## Saving and ownership

Successful patches persist before acknowledging completion, with one Undo, or
stay in the temporary fork until Keep. Validation and write failures leave visible
geometry and saved data unchanged. Held/owned/actively authored targets are refused.
The saved patch refreshes geometry, selection bounds, tint and approximate collider.
Recipe parts remain visual children of one grabbable rigid assembly; this does not
add articulated part physics or cloth/hair interaction.

The visual workshop keeps stale and rejected drafts. Apply requires native feature
support and a completed matching receipt before clearing its draft. There is no
fallback through a different edit path. Historical receipt replay never reapplies
an edit after Undo. Older command handling remains for prototype callers; the
current book and agent instructions prefer the shared operation.

## Acceptance boundary

Native tests exercise actual recipe meshes, playback, storage and Undo, including
full-capacity reads and failures. Browser replay checks real controls against
captured native requests/results. Physical Quest frame time, readability and input
comfort remain separate device checks.
